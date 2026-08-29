using ERP.Application.Common;
using ERP.Application.Modules.Sales.DTOs;
using ERP.Domain.Modules.Purchases;
using ERP.Domain.Modules.Sales.Entities;
using ERP.Domain.Modules.Sales.Enums;
using ERP.Domain.Modules.Sales.Interfaces;
using FluentValidation;
using MediatR;

namespace ERP.Application.Modules.Sales.UseCases;

// ── Shared input ────────────────────────────────────────────────────────

public sealed record SalesReturnLineInput(Guid InvoiceDetailId, decimal Quantity);

// ── Commands ────────────────────────────────────────────────────────────

/// <summary>
/// El cliente aporta ni <c>CustomerId</c> ni datos fiscales de línea — la devolución hereda el
/// cliente y el snapshot fiscal (VAT/ICE/precio) directamente de la factura original congelada
/// (Infraestructura CLOSED — Configuración Tributaria: los documentos consumen, nunca generan
/// impuestos). Solo autoriza la construcción del <c>Draft</c>; no invoca
/// <c>SalesReturn.Authorize()</c> (fase posterior).
/// </summary>
public sealed record CreateSalesReturnDraftCommand(
    Guid SalesInvoiceId,
    string Reason,
    List<SalesReturnLineInput> Lines
) : IRequest<Result<SalesReturnDto>>, IBranchScopedRequest;

/// <summary>
/// Reemplaza por completo las líneas de un <c>SalesReturn</c> en <c>Draft</c> — mismo patrón que
/// <c>UpdateSalesDraftCommand</c>. El motivo no es editable (el dominio no expone un método para
/// modificarlo una vez creado el borrador).
/// </summary>
public sealed record UpdateSalesReturnDraftCommand(Guid Id, List<SalesReturnLineInput> Lines)
    : IRequest<Result<SalesReturnDto>>,
        IBranchScopedRequest;

public sealed record CancelSalesReturnDraftCommand(Guid Id)
    : IRequest<Result<SalesReturnDto>>,
        IBranchScopedRequest;

// ── Validators ──────────────────────────────────────────────────────────

public sealed class CreateSalesReturnDraftValidator
    : AbstractValidator<CreateSalesReturnDraftCommand>
{
    public CreateSalesReturnDraftValidator()
    {
        RuleFor(x => x.SalesInvoiceId).NotEmpty().WithMessage("La factura origen es obligatoria.");
        RuleFor(x => x.Reason)
            .NotEmpty()
            .WithMessage("El motivo de la devolución es obligatorio.")
            .MaximumLength(SalesReturn.ReasonMaxLen);
        RuleFor(x => x.Lines).NotEmpty().WithMessage("Debe incluir al menos una línea.");
        RuleForEach(x => x.Lines)
            .ChildRules(line =>
            {
                line.RuleFor(l => l.InvoiceDetailId)
                    .NotEmpty()
                    .WithMessage("La línea de factura original es obligatoria.");
                line.RuleFor(l => l.Quantity)
                    .GreaterThan(0)
                    .WithMessage("La cantidad devuelta debe ser mayor a cero.");
            });
    }
}

public sealed class UpdateSalesReturnDraftValidator
    : AbstractValidator<UpdateSalesReturnDraftCommand>
{
    public UpdateSalesReturnDraftValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.Lines).NotEmpty().WithMessage("Debe incluir al menos una línea.");
        RuleForEach(x => x.Lines)
            .ChildRules(line =>
            {
                line.RuleFor(l => l.InvoiceDetailId)
                    .NotEmpty()
                    .WithMessage("La línea de factura original es obligatoria.");
                line.RuleFor(l => l.Quantity)
                    .GreaterThan(0)
                    .WithMessage("La cantidad devuelta debe ser mayor a cero.");
            });
    }
}

public sealed class CancelSalesReturnDraftValidator
    : AbstractValidator<CancelSalesReturnDraftCommand>
{
    public CancelSalesReturnDraftValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
    }
}

// ── Handlers ────────────────────────────────────────────────────────────

public sealed class CreateSalesReturnDraftHandler
    : IRequestHandler<CreateSalesReturnDraftCommand, Result<SalesReturnDto>>
{
    private readonly ISalesReturnRepository _returnRepo;
    private readonly ISalesInvoiceRepository _invoiceRepo;
    private readonly ICurrentTenant _t;
    private readonly ICurrentCompany _c;
    private readonly ICurrentUser _u;

    public CreateSalesReturnDraftHandler(
        ISalesReturnRepository returnRepo,
        ISalesInvoiceRepository invoiceRepo,
        ICurrentTenant t,
        ICurrentCompany c,
        ICurrentUser u
    )
    {
        _returnRepo = returnRepo;
        _invoiceRepo = invoiceRepo;
        _t = t;
        _c = c;
        _u = u;
    }

    public async Task<Result<SalesReturnDto>> Handle(
        CreateSalesReturnDraftCommand cmd,
        CancellationToken ct
    )
    {
        var invoice = await _invoiceRepo.GetByIdAsync(_t.TenantId, cmd.SalesInvoiceId, ct);
        if (invoice is null)
            return Result<SalesReturnDto>.NotFound("Factura no encontrada.");
        if (invoice.Status != SalesInvoiceStatus.Authorized)
            return Result<SalesReturnDto>.ValidationFailure(
                "Solo se pueden devolver facturas autorizadas."
            );

        var returnNumber = $"DEV-{Guid.NewGuid():N}"[..14];
        var salesReturn = SalesReturn.CreateDraft(
            _t.TenantId,
            _c.CompanyId,
            invoice.Id,
            invoice.CustomerId,
            returnNumber,
            cmd.Reason,
            _u.UserId
        );

        var linesResult = await SalesReturnLineBuilder.BuildAsync(
            cmd.Lines,
            salesReturn.Id,
            invoice,
            _t.TenantId,
            _returnRepo,
            ct
        );
        if (linesResult.Error is not null)
            return linesResult.Error;

        foreach (var line in linesResult.Lines)
            salesReturn.AddLine(line, _u.UserId);

        await _returnRepo.AddAsync(salesReturn, ct);
        await _returnRepo.SaveChangesAsync(ct);
        return Result<SalesReturnDto>.Success(SalesReturnMapper.ToDto(salesReturn));
    }
}

public sealed class UpdateSalesReturnDraftHandler
    : IRequestHandler<UpdateSalesReturnDraftCommand, Result<SalesReturnDto>>
{
    private readonly ISalesReturnRepository _returnRepo;
    private readonly ISalesInvoiceRepository _invoiceRepo;
    private readonly ICurrentTenant _t;
    private readonly ICurrentUser _u;

    public UpdateSalesReturnDraftHandler(
        ISalesReturnRepository returnRepo,
        ISalesInvoiceRepository invoiceRepo,
        ICurrentTenant t,
        ICurrentUser u
    )
    {
        _returnRepo = returnRepo;
        _invoiceRepo = invoiceRepo;
        _t = t;
        _u = u;
    }

    public async Task<Result<SalesReturnDto>> Handle(
        UpdateSalesReturnDraftCommand cmd,
        CancellationToken ct
    )
    {
        var salesReturn = await _returnRepo.GetByIdAsync(_t.TenantId, cmd.Id, ct);
        if (salesReturn is null)
            return Result<SalesReturnDto>.NotFound("Devolución no encontrada.");

        var invoice = await _invoiceRepo.GetByIdAsync(_t.TenantId, salesReturn.SalesInvoiceId, ct);
        if (invoice is null)
            return Result<SalesReturnDto>.NotFound("Factura no encontrada.");

        var linesResult = await SalesReturnLineBuilder.BuildAsync(
            cmd.Lines,
            salesReturn.Id,
            invoice,
            _t.TenantId,
            _returnRepo,
            ct
        );
        if (linesResult.Error is not null)
            return linesResult.Error;

        try
        {
            foreach (var existing in salesReturn.Lines.ToList())
                salesReturn.RemoveLine(existing.Id, _u.UserId);
            foreach (var line in linesResult.Lines)
                salesReturn.AddLine(line, _u.UserId);
        }
        catch (InvalidOperationException ex)
        {
            return Result<SalesReturnDto>.ValidationFailure(ex.Message);
        }

        await _returnRepo.SaveChangesAsync(ct);
        return Result<SalesReturnDto>.Success(SalesReturnMapper.ToDto(salesReturn));
    }
}

public sealed class CancelSalesReturnDraftHandler
    : IRequestHandler<CancelSalesReturnDraftCommand, Result<SalesReturnDto>>
{
    private readonly ISalesReturnRepository _repo;
    private readonly ICurrentTenant _t;
    private readonly ICurrentUser _u;

    public CancelSalesReturnDraftHandler(
        ISalesReturnRepository repo,
        ICurrentTenant t,
        ICurrentUser u
    )
    {
        _repo = repo;
        _t = t;
        _u = u;
    }

    public async Task<Result<SalesReturnDto>> Handle(
        CancelSalesReturnDraftCommand cmd,
        CancellationToken ct
    )
    {
        var salesReturn = await _repo.GetByIdAsync(_t.TenantId, cmd.Id, ct);
        if (salesReturn is null)
            return Result<SalesReturnDto>.NotFound("Devolución no encontrada.");

        try
        {
            salesReturn.Cancel(_u.UserId);
        }
        catch (InvalidOperationException ex)
        {
            return Result<SalesReturnDto>.ValidationFailure(ex.Message);
        }

        await _repo.SaveChangesAsync(ct);
        return Result<SalesReturnDto>.Success(SalesReturnMapper.ToDto(salesReturn));
    }
}

// ── Shared remaining-quantity validation ───────────────────────────────

/// <summary>
/// Única fuente de verdad del cálculo de remanente devolvible (cantidad original de la línea de
/// factura − cantidad ya devuelta en <c>SalesReturn</c> <c>Authorized</c>). Consumida tanto por el
/// chequeo preventivo al crear/editar el <c>Draft</c> (<see cref="SalesReturnLineBuilder"/>) como
/// por la revalidación autoritativa bajo lock en <c>AuthorizeSalesReturnHandler</c> — ambos
/// chequeos son intencionales (UX temprana vs. consistencia real bajo concurrencia), pero el
/// cálculo y el mensaje de error no deben duplicarse.
/// </summary>
internal static class SalesReturnRemainingQuantityGuard
{
    public static async Task<string?> ValidateAsync(
        ISalesReturnRepository returnRepo,
        Guid tenantId,
        SalesInvoiceDetail originalLine,
        decimal requestedQuantity,
        CancellationToken ct
    )
    {
        var alreadyReturned = await returnRepo.GetReturnedQuantityByInvoiceDetailAsync(
            tenantId,
            originalLine.Id,
            ct
        );
        var remaining = originalLine.Quantity - alreadyReturned;
        if (requestedQuantity > remaining)
            return $"Línea '{originalLine.Description}': la cantidad solicitada ({requestedQuantity}) "
                + $"excede el remanente devolvible ({remaining}).";
        return null;
    }
}

// ── Shared line-building helper ────────────────────────────────────────

file static class SalesReturnLineBuilder
{
    /// <summary>
    /// Valida y construye las líneas de una devolución contra la factura origen: cada línea debe
    /// pertenecer a esa factura y su cantidad no puede exceder el remanente devolvible (cantidad
    /// original − cantidad ya devuelta en <c>SalesReturn</c> <c>Authorized</c>, calculado vía
    /// <c>ISalesReturnRepository.GetReturnedQuantityByInvoiceDetailAsync</c>). El snapshot fiscal
    /// (precio, descuento, VAT/ICE) se hereda tal cual de la línea de factura original — nunca se
    /// resuelve de nuevo contra el ítem vigente.
    /// </summary>
    public static async Task<LinesBuildResult> BuildAsync(
        List<SalesReturnLineInput> inputs,
        Guid returnId,
        SalesInvoice invoice,
        Guid tenantId,
        ISalesReturnRepository returnRepo,
        CancellationToken ct
    )
    {
        var lines = new List<SalesReturnDetail>();
        foreach (var input in inputs)
        {
            var originalLine = invoice.Lines.FirstOrDefault(l => l.Id == input.InvoiceDetailId);
            if (originalLine is null)
                return new(
                    null!,
                    Result<SalesReturnDto>.ValidationFailure(
                        "La línea indicada no pertenece a la factura seleccionada."
                    )
                );

            var remainingError = await SalesReturnRemainingQuantityGuard.ValidateAsync(
                returnRepo,
                tenantId,
                originalLine,
                input.Quantity,
                ct
            );
            if (remainingError is not null)
                return new(null!, Result<SalesReturnDto>.ValidationFailure(remainingError));

            // TAX-LINE-SSOT-ICE-IRBPNR-01 (ADR-032 §3.3, Subfase 5D-3) — fracción de cantidad sobre
            // la línea original, misma base que ya usa PurchaseReturn (5D-1). Nunca se consulta la
            // configuración tributaria actual del ítem ni la última compra — todo sale del snapshot
            // fiscal ya congelado en originalLine (SalesInvoiceDetail.Taxes).
            var fraction = input.Quantity / originalLine.Quantity;
            decimal? iceExactAmount = null;
            if (originalLine.IceCalculationType == Domain.Modules.SriCatalogs.Enums.SriTaxCalculationType.Specific)
                iceExactAmount = Round2(fraction * originalLine.IceAmount);

            // SALES-PRESENTATIONS-02: la devolución mantiene SIEMPRE la misma presentación de la
            // venta original (PackagingLevelId/ConversionFactor/BaseUomCode heredados tal cual) —
            // no se expone forma de devolver en una presentación distinta a la vendida.
            var line = SalesReturnDetail.Create(
                returnId,
                tenantId,
                originalLine.Id,
                originalLine.Description,
                input.Quantity,
                originalLine.UnitPrice,
                originalLine.DiscountPct,
                originalLine.VatCode,
                originalLine.VatRate,
                originalLine.UomCode,
                originalLine.ItemId,
                originalLine.WarehouseId,
                originalLine.SnapshotSku,
                originalLine.SnapshotItemName,
                originalLine.IceCode,
                originalLine.IceRate,
                originalLine.PackagingLevelId,
                originalLine.ConversionFactor,
                originalLine.BaseUomCode,
                originalLine.IceCalculationType,
                iceExactAmount
            );

            // IRBPNR no tiene campo escalar legacy — vive únicamente en Taxes, prorrateado por la
            // misma fracción de cantidad. Sin fila si la línea original no tenía IRBPNR (nunca se
            // genera IRBPNR falso).
            if (!string.IsNullOrWhiteSpace(originalLine.IrbpnrCode) && originalLine.IrbpnrAmount > 0)
            {
                var irbpnrTax = originalLine.Taxes.First(t => t.TaxCode == SriTaxCategoryCodes.Irbpnr);
                line.ReplaceTaxes(
                    [
                        ERP.Domain.Modules.Sales.Entities.SalesReturnDetailTax.Create(
                            line.Id,
                            tenantId,
                            SriTaxCategoryCodes.Irbpnr,
                            irbpnrTax.TaxRateCode,
                            irbpnrTax.TaxName,
                            irbpnrTax.Rate,
                            irbpnrTax.CalculationType,
                            Round2(fraction * originalLine.IrbpnrAmount)
                        ),
                    ]
                );
            }

            lines.Add(line);
        }
        return new(lines, null);
    }

    public sealed record LinesBuildResult(
        List<SalesReturnDetail> Lines,
        Result<SalesReturnDto>? Error
    );

    private static decimal Round2(decimal value) =>
        Math.Round(value, Domain.Common.FiscalPrecision.TaxAmount, MidpointRounding.AwayFromZero);
}

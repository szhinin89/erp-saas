using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using ERP.Application.Common;
using ERP.Application.Common.Persistence;
using ERP.Application.Modules.Purchases.DTOs;
using ERP.Domain.Modules.Purchases.Entities;
using ERP.Domain.Modules.Purchases.Enums;
using ERP.Domain.Modules.Purchases.Interfaces;
using FluentValidation;
using MediatR;

namespace ERP.Application.Modules.Purchases.UseCases;

// ── Shared input ────────────────────────────────────────────────────────

public sealed record PurchaseReturnDraftLineInput(Guid OriginalInvoiceDetailId, decimal Quantity);

// ── Commands ────────────────────────────────────────────────────────────

/// <summary>
/// P0-02 Fase 5 — crea el borrador de una devolución de compra. No expone <c>BranchId</c> — el
/// handler lo resuelve exclusivamente de <c>ICurrentBranch</c> (Branch Ownership Rule, diseño
/// §5.2, mismo patrón que <c>PurchaseDraftUseCases.cs</c>). Idempotente (diseño §16.2/§16.2bis) —
/// <c>ClientRequestId</c> obligatorio.
/// </summary>
public sealed record CreatePurchaseReturnDraftCommand(
    Guid ClientRequestId,
    Guid PurchaseInvoiceId,
    string Reason,
    IReadOnlyList<PurchaseReturnDraftLineInput> Lines
) : IRequest<Result<PurchaseReturnDto>>, IBranchScopedRequest;

/// <summary>P0-02 Fase 5 — reemplaza por completo motivo y líneas de un <c>PurchaseReturn</c> en <c>Draft</c>. Sin idempotencia (no forma parte de las 8 operaciones de §16.2).</summary>
public sealed record UpdatePurchaseReturnDraftCommand(
    Guid Id,
    string Reason,
    IReadOnlyList<PurchaseReturnDraftLineInput> Lines
) : IRequest<Result<PurchaseReturnDto>>, IBranchScopedRequest;

/// <summary>
/// P0-02 Fase 5 — cancela un <c>PurchaseReturn</c> todavía en <c>Draft</c> (sin reversas). La
/// cancelación de una devolución ya <c>Authorized</c> (con reversas) es <c>CancelPurchaseReturn</c>,
/// Fase 10 — este comando rechaza esa transición con <c>PR-009</c>.
/// </summary>
public sealed record CancelPurchaseReturnDraftCommand(Guid Id, Guid ClientRequestId, string Reason)
    : IRequest<Result<PurchaseReturnDto>>,
        IBranchScopedRequest;

// ── Validators ──────────────────────────────────────────────────────────

public sealed class CreatePurchaseReturnDraftValidator
    : AbstractValidator<CreatePurchaseReturnDraftCommand>
{
    public CreatePurchaseReturnDraftValidator()
    {
        RuleFor(x => x.ClientRequestId)
            .NotEmpty()
            .WithMessage("El identificador de idempotencia es obligatorio.");
        RuleFor(x => x.PurchaseInvoiceId)
            .NotEmpty()
            .WithMessage("La factura origen es obligatoria.");
        RuleFor(x => x.Reason)
            .NotEmpty()
            .WithMessage("El motivo de la devolución es obligatorio.")
            .MaximumLength(PurchaseReturn.ReasonMaxLen);
        RuleFor(x => x.Lines).NotEmpty().WithMessage("Debe incluir al menos una línea.");
        RuleForEach(x => x.Lines)
            .ChildRules(line =>
            {
                line.RuleFor(l => l.OriginalInvoiceDetailId)
                    .NotEmpty()
                    .WithMessage("La línea de factura original es obligatoria.");
                line.RuleFor(l => l.Quantity)
                    .GreaterThan(0)
                    .WithMessage("La cantidad devuelta debe ser mayor a cero.");
            });
    }
}

public sealed class UpdatePurchaseReturnDraftValidator
    : AbstractValidator<UpdatePurchaseReturnDraftCommand>
{
    public UpdatePurchaseReturnDraftValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.Reason)
            .NotEmpty()
            .WithMessage("El motivo de la devolución es obligatorio.")
            .MaximumLength(PurchaseReturn.ReasonMaxLen);
        RuleFor(x => x.Lines).NotEmpty().WithMessage("Debe incluir al menos una línea.");
        RuleForEach(x => x.Lines)
            .ChildRules(line =>
            {
                line.RuleFor(l => l.OriginalInvoiceDetailId)
                    .NotEmpty()
                    .WithMessage("La línea de factura original es obligatoria.");
                line.RuleFor(l => l.Quantity)
                    .GreaterThan(0)
                    .WithMessage("La cantidad devuelta debe ser mayor a cero.");
            });
    }
}

public sealed class CancelPurchaseReturnDraftValidator
    : AbstractValidator<CancelPurchaseReturnDraftCommand>
{
    public CancelPurchaseReturnDraftValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.ClientRequestId)
            .NotEmpty()
            .WithMessage("El identificador de idempotencia es obligatorio.");
        RuleFor(x => x.Reason)
            .NotEmpty()
            .WithMessage("El motivo de la cancelación es obligatorio.")
            .MaximumLength(PurchaseReturn.CancellationReasonMaxLen);
    }
}

// ── Handlers ────────────────────────────────────────────────────────────

public sealed class CreatePurchaseReturnDraftHandler
    : IRequestHandler<CreatePurchaseReturnDraftCommand, Result<PurchaseReturnDto>>
{
    private readonly IPurchaseReturnRepository _returnRepo;
    private readonly IPurchaseInvoiceRepository _invoiceRepo;
    private readonly IDatabaseExceptionTranslator _dbEx;
    private readonly ICurrentTenant _t;
    private readonly ICurrentCompany _c;
    private readonly ICurrentBranch _b;
    private readonly ICurrentUser _u;

    public CreatePurchaseReturnDraftHandler(
        IPurchaseReturnRepository returnRepo,
        IPurchaseInvoiceRepository invoiceRepo,
        IDatabaseExceptionTranslator dbEx,
        ICurrentTenant t,
        ICurrentCompany c,
        ICurrentBranch b,
        ICurrentUser u
    )
    {
        _returnRepo = returnRepo;
        _invoiceRepo = invoiceRepo;
        _dbEx = dbEx;
        _t = t;
        _c = c;
        _b = b;
        _u = u;
    }

    public async Task<Result<PurchaseReturnDto>> Handle(
        CreatePurchaseReturnDraftCommand cmd,
        CancellationToken ct
    )
    {
        var tid = _t.TenantId;

        // ── §16.2 paso 3: buscar por (TenantId, ClientRequestId) antes de tocar cualquier
        // agregado — cubre tanto "mismo contenido" como "conflicto" antes de intentar insertar. ──
        var hash = ComputePayloadHash(cmd.PurchaseInvoiceId, cmd.Reason, cmd.Lines);
        var existing = await _returnRepo.GetByCreateClientRequestIdAsync(
            tid,
            cmd.ClientRequestId,
            ct
        );
        if (existing is not null)
            return existing.CreateRequestPayloadHash == hash
                ? Result<PurchaseReturnDto>.Success(Map.ToDto(existing))
                : Result<PurchaseReturnDto>.ValidationFailure(
                    // PR-012
                    "Ya existe una solicitud de creación con este identificador pero con datos distintos."
                );

        // PR-001/PR-002
        var invoice = await _invoiceRepo.GetByIdAsync(tid, cmd.PurchaseInvoiceId, ct);
        if (invoice is null)
            return Result<PurchaseReturnDto>.NotFound("Factura de compra no encontrada.");
        if (invoice.Status != Domain.Modules.Purchases.Enums.PurchaseStatus.Confirmed)
            return Result<PurchaseReturnDto>.ValidationFailure(
                // PR-002
                "Solo se pueden devolver facturas de compra confirmadas."
            );

        // PR-003 + PR-004 (preventivo, no bajo lock — revalidado bajo lock real en Fase 6)
        var lineBuildResult = await BuildLinesAsync(_returnRepo, tid, invoice, cmd.Lines, ct);
        if (lineBuildResult.Error is not null)
            return lineBuildResult.Error;

        var purchaseReturn = PurchaseReturn.CreateDraft(
            tid,
            _c.CompanyId,
            _b.BranchId,
            invoice.Id,
            invoice.SupplierId,
            cmd.Reason,
            lineBuildResult.Lines,
            _u.UserId,
            cmd.ClientRequestId,
            hash
        );

        await _returnRepo.AddAsync(purchaseReturn, ct);
        try
        {
            await _returnRepo.SaveChangesAsync(ct);
        }
        catch (Exception ex) when (_dbEx.TryGetUniqueViolation(ex, out _))
        {
            // §16.2bis — la carrera fue ganada por otra solicitud concurrente con el mismo
            // ClientRequestId: sin transacción explícita ambiente, el SaveChangesAsync fallido
            // ya revirtió su propio efecto — una nueva consulta sobre el mismo DbContext es
            // segura. Se reconsulta el registro ganador y se compara el hash (nunca se reintenta
            // el SaveChanges abortado).
            var winner = await _returnRepo.GetByCreateClientRequestIdAsync(
                tid,
                cmd.ClientRequestId,
                ct
            );
            if (winner is null)
                throw;

            return winner.CreateRequestPayloadHash == hash
                ? Result<PurchaseReturnDto>.Success(Map.ToDto(winner))
                : Result<PurchaseReturnDto>.ValidationFailure(
                    // PR-012
                    "Ya existe una solicitud de creación con este identificador pero con datos distintos."
                );
        }

        return Result<PurchaseReturnDto>.Success(Map.ToDto(purchaseReturn));
    }

    /// <summary>
    /// Huella determinista (§16.2): <c>OperationType</c> + <c>PurchaseInvoiceId</c> + <c>Reason</c>
    /// + líneas canonicalizadas (ordenadas por <c>OriginalInvoiceDetailId</c>) — nunca depende del
    /// orden de serialización JSON recibido.
    /// </summary>
    public static string ComputePayloadHash(
        Guid purchaseInvoiceId,
        string reason,
        IReadOnlyList<PurchaseReturnDraftLineInput> lines
    )
    {
        var canonicalLines = lines
            .OrderBy(l => l.OriginalInvoiceDetailId)
            .Select(l =>
                l.OriginalInvoiceDetailId.ToString("D")
                + ":"
                + l.Quantity.ToString(CultureInfo.InvariantCulture)
            );
        var canonical = string.Join(
            '',
            "CreateDraft",
            purchaseInvoiceId.ToString("D"),
            reason.Trim(),
            string.Join('', canonicalLines)
        );
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(canonical));
        return Convert.ToHexString(bytes);
    }

    private static async Task<LinesBuildResult> BuildLinesAsync(
        IPurchaseReturnRepository returnRepo,
        Guid tenantId,
        Domain.Modules.Purchases.Entities.PurchaseInvoice invoice,
        IReadOnlyList<PurchaseReturnDraftLineInput> inputs,
        CancellationToken ct
    )
    {
        var detailIds = inputs.Select(i => i.OriginalInvoiceDetailId).ToList();
        var returnedByDetailId = await returnRepo.GetReturnedQuantitiesByInvoiceDetailIdsAsync(
            tenantId,
            detailIds,
            ct
        );

        var lines = new List<PurchaseReturn.DraftLineInput>();
        foreach (var input in inputs)
        {
            var originalLine = invoice.Lines.FirstOrDefault(l =>
                l.Id == input.OriginalInvoiceDetailId
            );
            if (originalLine is null || originalLine.ItemId is null)
                return new(
                    null!,
                    // PR-003
                    Result<PurchaseReturnDto>.ValidationFailure(
                        "La línea indicada no pertenece a la factura seleccionada."
                    )
                );

            var warehouseId = originalLine.WarehouseId ?? invoice.GlobalWarehouseId;
            if (warehouseId is null)
                return new(
                    null!,
                    // PR-003
                    Result<PurchaseReturnDto>.ValidationFailure(
                        "La línea indicada no tiene una bodega asociada."
                    )
                );

            var alreadyReturned = returnedByDetailId.GetValueOrDefault(originalLine.Id);
            var remaining = originalLine.Quantity - alreadyReturned;
            if (input.Quantity > remaining)
                return new(
                    null!,
                    // PR-004
                    Result<PurchaseReturnDto>.ValidationFailure(
                        $"Línea '{originalLine.Description}': la cantidad solicitada ({input.Quantity}) "
                            + $"excede el remanente devolvible ({remaining})."
                    )
                );

            lines.Add(
                new PurchaseReturn.DraftLineInput(
                    originalLine.Id,
                    originalLine.ItemId.Value,
                    input.Quantity,
                    warehouseId.Value
                )
            );
        }

        return new(lines, null);
    }

    private sealed record LinesBuildResult(
        List<PurchaseReturn.DraftLineInput> Lines,
        Result<PurchaseReturnDto>? Error
    );
}

public sealed class UpdatePurchaseReturnDraftHandler
    : IRequestHandler<UpdatePurchaseReturnDraftCommand, Result<PurchaseReturnDto>>
{
    private readonly IPurchaseReturnRepository _returnRepo;
    private readonly IPurchaseInvoiceRepository _invoiceRepo;
    private readonly ICurrentTenant _t;
    private readonly ICurrentUser _u;

    public UpdatePurchaseReturnDraftHandler(
        IPurchaseReturnRepository returnRepo,
        IPurchaseInvoiceRepository invoiceRepo,
        ICurrentTenant t,
        ICurrentUser u
    )
    {
        _returnRepo = returnRepo;
        _invoiceRepo = invoiceRepo;
        _t = t;
        _u = u;
    }

    public async Task<Result<PurchaseReturnDto>> Handle(
        UpdatePurchaseReturnDraftCommand cmd,
        CancellationToken ct
    )
    {
        var tid = _t.TenantId;

        var purchaseReturn = await _returnRepo.GetByIdAsync(tid, cmd.Id, ct);
        if (purchaseReturn is null)
            return Result<PurchaseReturnDto>.NotFound("Devolución no encontrada.");

        var invoice = await _invoiceRepo.GetByIdAsync(tid, purchaseReturn.PurchaseInvoiceId, ct);
        if (invoice is null)
            return Result<PurchaseReturnDto>.NotFound("Factura de compra no encontrada.");

        var detailIds = cmd.Lines.Select(l => l.OriginalInvoiceDetailId).ToList();
        // El remanente excluye las líneas de esta misma devolución (todavía Draft, sin efecto en
        // §10.2 porque la consulta solo suma devoluciones Authorized) — no requiere exclusión
        // explícita.
        var returnedByDetailId = await _returnRepo.GetReturnedQuantitiesByInvoiceDetailIdsAsync(
            tid,
            detailIds,
            ct
        );

        var lines = new List<PurchaseReturn.DraftLineInput>();
        foreach (var input in cmd.Lines)
        {
            var originalLine = invoice.Lines.FirstOrDefault(l =>
                l.Id == input.OriginalInvoiceDetailId
            );
            if (originalLine is null || originalLine.ItemId is null)
                // PR-003
                return Result<PurchaseReturnDto>.ValidationFailure(
                    "La línea indicada no pertenece a la factura seleccionada."
                );

            var warehouseId = originalLine.WarehouseId ?? invoice.GlobalWarehouseId;
            if (warehouseId is null)
                // PR-003
                return Result<PurchaseReturnDto>.ValidationFailure(
                    "La línea indicada no tiene una bodega asociada."
                );

            var alreadyReturned = returnedByDetailId.GetValueOrDefault(originalLine.Id);
            var remaining = originalLine.Quantity - alreadyReturned;
            if (input.Quantity > remaining)
                // PR-004
                return Result<PurchaseReturnDto>.ValidationFailure(
                    $"Línea '{originalLine.Description}': la cantidad solicitada ({input.Quantity}) "
                        + $"excede el remanente devolvible ({remaining})."
                );

            lines.Add(
                new PurchaseReturn.DraftLineInput(
                    originalLine.Id,
                    originalLine.ItemId.Value,
                    input.Quantity,
                    warehouseId.Value
                )
            );
        }

        try
        {
            purchaseReturn.UpdateDraft(cmd.Reason, lines, _u.UserId);
        }
        catch (InvalidOperationException ex)
        {
            // PR-009
            return Result<PurchaseReturnDto>.ValidationFailure(ex.Message);
        }

        await _returnRepo.SaveChangesAsync(ct);
        return Result<PurchaseReturnDto>.Success(Map.ToDto(purchaseReturn));
    }
}

public sealed class CancelPurchaseReturnDraftHandler
    : IRequestHandler<CancelPurchaseReturnDraftCommand, Result<PurchaseReturnDto>>
{
    private readonly IPurchaseReturnRepository _returnRepo;
    private readonly ICurrentTenant _t;
    private readonly ICurrentUser _u;

    public CancelPurchaseReturnDraftHandler(
        IPurchaseReturnRepository returnRepo,
        ICurrentTenant t,
        ICurrentUser u
    )
    {
        _returnRepo = returnRepo;
        _t = t;
        _u = u;
    }

    public async Task<Result<PurchaseReturnDto>> Handle(
        CancelPurchaseReturnDraftCommand cmd,
        CancellationToken ct
    )
    {
        var purchaseReturn = await _returnRepo.GetByIdAsync(_t.TenantId, cmd.Id, ct);
        if (purchaseReturn is null)
            return Result<PurchaseReturnDto>.NotFound("Devolución no encontrada.");

        // Idempotencia mínima (§16.2): repetición exacta de una cancelación ya confirmada
        // retorna el snapshot ya persistido sin reejecutar el efecto.
        if (purchaseReturn.Status == PurchaseReturnStatus.Cancelled)
            return purchaseReturn.CancelClientRequestId == cmd.ClientRequestId
                ? Result<PurchaseReturnDto>.Success(Map.ToDto(purchaseReturn))
                // PR-012
                : Result<PurchaseReturnDto>.ValidationFailure(
                    "Ya existe una solicitud de cancelación con este identificador pero con datos distintos."
                );

        // Esta fase solo administra el borrador — cancelar una devolución ya Authorized (con
        // reversas de inventario/CxP/crédito) es CancelPurchaseReturn (Fase 10).
        if (purchaseReturn.Status != PurchaseReturnStatus.Draft)
            // PR-009
            return Result<PurchaseReturnDto>.ValidationFailure(
                "Esta devolución ya fue autorizada; use la cancelación de devolución autorizada."
            );

        var cancelHash = ComputeCancelPayloadHash(purchaseReturn.Id, cmd.Reason);
        try
        {
            purchaseReturn.Cancel(cmd.Reason, _u.UserId, cmd.ClientRequestId, cancelHash);
        }
        catch (InvalidOperationException ex)
        {
            // PR-009
            return Result<PurchaseReturnDto>.ValidationFailure(ex.Message);
        }

        await _returnRepo.SaveChangesAsync(ct);
        return Result<PurchaseReturnDto>.Success(Map.ToDto(purchaseReturn));
    }

    /// <summary>Huella determinista (§16.2): <c>OperationType</c> + <c>PurchaseReturnId</c> + <c>CancellationReason</c> normalizada.</summary>
    private static string ComputeCancelPayloadHash(Guid purchaseReturnId, string reason)
    {
        var canonical = string.Join(
            '',
            "CancelPurchaseReturn",
            purchaseReturnId.ToString("D"),
            reason.Trim()
        );
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(canonical));
        return Convert.ToHexString(bytes);
    }
}

// ── Mapping ─────────────────────────────────────────────────────────────

internal static class Map
{
    public static PurchaseReturnDto ToDto(PurchaseReturn r) =>
        new(
            r.Id,
            r.PurchaseInvoiceId,
            r.SupplierId,
            r.BranchId,
            r.ReturnNumber,
            r.Reason,
            r.Status.ToString(),
            r.FiscalStatus.ToString(),
            r.SupplierCreditNoteDocumentId,
            r.AuthorizedSubtotal,
            r.AuthorizedVatTotal,
            r.AuthorizedIceTotal,
            r.AuthorizedDiscountTotal,
            r.AuthorizedGrandTotal,
            r.AuthorizedAtUtc,
            r.CancelledAtUtc,
            r.CancellationReason,
            r.Lines.Select(l => new PurchaseReturnDetailDto(
                    l.Id,
                    l.OriginalInvoiceDetailId,
                    l.ItemId,
                    l.Quantity,
                    l.WarehouseId
                ))
                .ToList(),
            r.CreatedAt,
            r.UpdatedAt
        );
}

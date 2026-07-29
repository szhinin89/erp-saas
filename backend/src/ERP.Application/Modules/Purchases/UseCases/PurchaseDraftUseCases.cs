using ERP.Application.Common;
using ERP.Application.Modules.Purchases.DTOs;
using ERP.Application.Modules.Purchases.Services;
using ERP.Domain.MasterData.Interfaces;
using ERP.Domain.Modules.Inventory.Interfaces;
using ERP.Domain.Modules.Items.Interfaces;
using ERP.Domain.Modules.Purchases.Entities;
using ERP.Domain.Modules.Purchases.Interfaces;
using FluentValidation;
using MediatR;

namespace ERP.Application.Modules.Purchases.UseCases;

// ── Shared input ────────────────────────────────────────────────────────

public sealed record PurchaseLineInput(
    Guid? ItemId,
    string Description,
    decimal Quantity,
    decimal UnitPrice,
    string VatCode,
    Guid? WarehouseId = null,
    string? Notes = null,
    decimal DiscountPct = 0,
    string? IceCode = null,
    Guid? PurchaseOrderDetailId = null,
    decimal? OrderedQuantity = null,
    Guid? PurchaseReceptionLineId = null
);

/// <summary>
/// Resolución de código de proveedor compartida entre Create/UpdatePurchaseDraftHandler
/// (Fase 8) — evita duplicar la misma consulta en ambos handlers. ItemSupplierCode es la
/// única fuente de códigos de compra (Item.Code.PurchaseCode legacy fue eliminado).
/// </summary>
file static class SupplierCodeResolver
{
    public static async Task<string?> ResolveAsync(
        IItemRepository itemRepo,
        Guid itemId,
        Guid tenantId,
        Guid? supplierId,
        CancellationToken ct
    )
    {
        if (!supplierId.HasValue)
            return null;

        return await itemRepo.GetSupplierCodeAsync(itemId, supplierId.Value, tenantId, ct);
    }
}

// ── Commands & Queries ──────────────────────────────────────────────────

public sealed record CreatePurchaseDraftCommand(
    Guid SupplierId,
    string DocTypeCode,
    string InvoiceNumber,
    DateOnly IssueDate,
    List<PurchaseLineInput> Lines,
    string? AccessKey = null,
    string? AuthorizationNumber = null,
    DateTime? AuthorizationDate = null,
    string? TaxSupportCode = null,
    string? SriPaymentMethodCode = null,
    Guid? GlobalWarehouseId = null,
    decimal FreightCost = 0,
    decimal OtherCosts = 0,
    DateOnly? DueDate = null,
    string? Notes = null,
    Guid? PaymentTermId = null
) : IRequest<Result<PurchaseInvoiceDto>>, IBranchScopedRequest;

public sealed record UpdatePurchaseDraftCommand(
    Guid Id,
    Guid SupplierId,
    string DocTypeCode,
    string InvoiceNumber,
    DateOnly IssueDate,
    List<PurchaseLineInput> Lines,
    string? AccessKey = null,
    string? AuthorizationNumber = null,
    DateTime? AuthorizationDate = null,
    string? TaxSupportCode = null,
    string? SriPaymentMethodCode = null,
    Guid? GlobalWarehouseId = null,
    decimal FreightCost = 0,
    decimal OtherCosts = 0,
    DateOnly? DueDate = null,
    string? Notes = null,
    Guid? PaymentTermId = null
) : IRequest<Result<PurchaseInvoiceDto>>, IBranchScopedRequest;

public sealed record GetPurchaseByIdQuery(Guid Id)
    : IRequest<Result<PurchaseInvoiceDto>>,
        IBranchScopedRequest;

/// <summary>
/// Cierre de gap BranchScopeBehavior — mismo criterio que GetSalesInvoiceListQuery: exige
/// contexto de sucursal (defensa en profundidad vía BranchScopeBehavior/IBranchAccessGuard),
/// sin agregar ningún filtro WHERE BranchId — el repositorio sigue devolviendo todos los
/// documentos de la empresa, sin cambiar su comportamiento actual.
/// </summary>
public sealed record GetPurchaseListQuery(
    string? Search = null,
    string? Status = null,
    int PageNumber = 1,
    int PageSize = 25
) : IRequest<Result<PurchaseListResponse>>, IBranchScopedRequest;

public sealed record PurchaseListResponse(
    IReadOnlyList<PurchaseListDto> Items,
    int Total,
    int Page,
    int PageSize
);

// ── Validators ──────────────────────────────────────────────────────────

file static class AuthorizationRules
{
    private static readonly int[] ValidLengths = [10, 37, 49];

    public static IRuleBuilderOptions<T, string?> ValidSriAuthorization<T>(
        this IRuleBuilder<T, string?> rule
    ) =>
        rule.Must(v =>
                string.IsNullOrWhiteSpace(v)
                || (v.Trim().All(char.IsDigit) && ValidLengths.Contains(v.Trim().Length))
            )
            .WithMessage("La autorización SRI debe ser numérica y tener 10, 37 o 49 dígitos.");
}

public sealed class CreatePurchaseDraftValidator : AbstractValidator<CreatePurchaseDraftCommand>
{
    public CreatePurchaseDraftValidator()
    {
        RuleFor(x => x.SupplierId).NotEmpty().WithMessage("El proveedor es obligatorio.");
        RuleFor(x => x.DocTypeCode)
            .NotEmpty()
            .MaximumLength(PurchaseInvoice.DocTypeCodeMaxLen)
            .WithMessage("El tipo de comprobante es obligatorio.");
        RuleFor(x => x.InvoiceNumber).NotEmpty().MaximumLength(PurchaseInvoice.InvoiceNumberMaxLen);
        RuleFor(x => x.IssueDate).NotEmpty();
        RuleFor(x => x.AuthorizationNumber).ValidSriAuthorization();
        RuleFor(x => x.SriPaymentMethodCode).MaximumLength(PurchaseInvoice.SriPaymentMethodMaxLen);
        RuleFor(x => x.Lines).NotEmpty().WithMessage("Debe incluir al menos una línea.");
        RuleForEach(x => x.Lines)
            .ChildRules(line =>
            {
                line.RuleFor(l => l.Description)
                    .NotEmpty()
                    .MaximumLength(PurchaseInvoiceDetail.DescriptionMaxLen);
                line.RuleFor(l => l.Quantity)
                    .GreaterThan(0)
                    .WithMessage("La cantidad debe ser mayor a cero.");
                line.RuleFor(l => l.UnitPrice)
                    .GreaterThanOrEqualTo(0)
                    .WithMessage("El costo no puede ser negativo.");
                line.RuleFor(l => l.VatCode)
                    .NotEmpty()
                    .WithMessage("El código IVA es obligatorio por línea.");
                line.RuleFor(l => l.DiscountPct).InclusiveBetween(0, 100);
            });
        RuleFor(x => x.FreightCost).GreaterThanOrEqualTo(0);
        RuleFor(x => x.OtherCosts).GreaterThanOrEqualTo(0);
    }
}

public sealed class UpdatePurchaseDraftValidator : AbstractValidator<UpdatePurchaseDraftCommand>
{
    public UpdatePurchaseDraftValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.SupplierId).NotEmpty().WithMessage("El proveedor es obligatorio.");
        RuleFor(x => x.DocTypeCode)
            .NotEmpty()
            .MaximumLength(PurchaseInvoice.DocTypeCodeMaxLen)
            .WithMessage("El tipo de comprobante es obligatorio.");
        RuleFor(x => x.InvoiceNumber).NotEmpty().MaximumLength(PurchaseInvoice.InvoiceNumberMaxLen);
        RuleFor(x => x.IssueDate).NotEmpty();
        RuleFor(x => x.AuthorizationNumber).ValidSriAuthorization();
        RuleFor(x => x.SriPaymentMethodCode).MaximumLength(PurchaseInvoice.SriPaymentMethodMaxLen);
        RuleFor(x => x.Lines).NotEmpty().WithMessage("Debe incluir al menos una línea.");
        RuleForEach(x => x.Lines)
            .ChildRules(line =>
            {
                line.RuleFor(l => l.Description)
                    .NotEmpty()
                    .MaximumLength(PurchaseInvoiceDetail.DescriptionMaxLen);
                line.RuleFor(l => l.Quantity).GreaterThan(0);
                line.RuleFor(l => l.UnitPrice).GreaterThanOrEqualTo(0);
                line.RuleFor(l => l.VatCode)
                    .NotEmpty()
                    .WithMessage("El código IVA es obligatorio por línea.");
                line.RuleFor(l => l.DiscountPct).InclusiveBetween(0, 100);
            });
        RuleFor(x => x.FreightCost).GreaterThanOrEqualTo(0);
        RuleFor(x => x.OtherCosts).GreaterThanOrEqualTo(0);
    }
}

// ── Handlers ────────────────────────────────────────────────────────────

public sealed class CreatePurchaseDraftHandler
    : IRequestHandler<CreatePurchaseDraftCommand, Result<PurchaseInvoiceDto>>
{
    private readonly IPurchaseInvoiceRepository _repo;
    private readonly IBusinessPartnerRepository _bpRepo;
    private readonly IBusinessPartnerRoleRepository _roleRepo;
    private readonly IPaymentTermRepository _ptRepo;
    private readonly IItemRepository _itemRepo;
    private readonly IWarehouseRepository _whRepo;
    private readonly ISriTaxResolver _tax;
    private readonly ICurrentTenant _t;
    private readonly ICurrentCompany _c;
    private readonly ICurrentBranch _b;
    private readonly ICurrentUser _u;

    public CreatePurchaseDraftHandler(
        IPurchaseInvoiceRepository repo,
        IBusinessPartnerRepository bpRepo,
        IBusinessPartnerRoleRepository roleRepo,
        IPaymentTermRepository ptRepo,
        IItemRepository itemRepo,
        IWarehouseRepository whRepo,
        ISriTaxResolver tax,
        ICurrentTenant t,
        ICurrentCompany c,
        ICurrentBranch b,
        ICurrentUser u
    )
    {
        _repo = repo;
        _bpRepo = bpRepo;
        _roleRepo = roleRepo;
        _ptRepo = ptRepo;
        _itemRepo = itemRepo;
        _whRepo = whRepo;
        _tax = tax;
        _t = t;
        _c = c;
        _b = b;
        _u = u;
    }

    public async Task<Result<PurchaseInvoiceDto>> Handle(
        CreatePurchaseDraftCommand cmd,
        CancellationToken ct
    )
    {
        var supplier = await _bpRepo.GetByIdAsync(cmd.SupplierId, ct);
        if (supplier is null)
            return Result<PurchaseInvoiceDto>.NotFound("Proveedor no encontrado.");
        if (!supplier.IsActive)
            return Result<PurchaseInvoiceDto>.ValidationFailure(
                "El proveedor se encuentra inactivo."
            );

        var supplierRole = await _roleRepo.GetByTypeAsync(
            cmd.SupplierId,
            Domain.MasterData.Enums.RoleType.Supplier,
            ct
        );
        if (supplierRole?.SupplierConfig is null)
            return Result<PurchaseInvoiceDto>.ValidationFailure(
                "El proveedor no tiene configuración SRI."
            );

        var ptId = cmd.PaymentTermId ?? supplierRole.SupplierConfig.PaymentTermId;
        var pt = await _ptRepo.GetByIdAsync(_t.TenantId, ptId, ct);
        if (pt is null)
            return Result<PurchaseInvoiceDto>.ValidationFailure("La condición de pago no existe.");

        var tid = _t.TenantId;
        string? pmName = null;
        if (!string.IsNullOrWhiteSpace(cmd.SriPaymentMethodCode))
            pmName = await _tax.GetPaymentMethodNameAsync(cmd.SriPaymentMethodCode.Trim(), ct);

        var inv = PurchaseInvoice.CreateDraft(
            tid,
            _c.CompanyId,
            _b.BranchId,
            cmd.SupplierId,
            supplier.Name.LegalName,
            supplier.Identification.Number,
            cmd.DocTypeCode,
            cmd.InvoiceNumber,
            cmd.IssueDate,
            _u.UserId,
            pt.Id,
            pt.Name,
            pt.Installments,
            pt.DaysBetweenInstallments,
            cmd.AccessKey,
            cmd.AuthorizationNumber,
            cmd.AuthorizationDate,
            cmd.TaxSupportCode,
            cmd.SriPaymentMethodCode,
            pmName,
            cmd.GlobalWarehouseId,
            cmd.DueDate,
            cmd.Notes
        );

        var linesResult = await BuildLines(
            cmd.Lines,
            inv.Id,
            tid,
            cmd.GlobalWarehouseId,
            ct,
            cmd.SupplierId
        );
        if (linesResult.Error is not null)
            return linesResult.Error;

        inv.ReplaceLines(linesResult.Lines, _u.UserId);
        if (cmd.FreightCost > 0 || cmd.OtherCosts > 0)
            inv.DistributeCosts(cmd.FreightCost, cmd.OtherCosts, _u.UserId);

        await _repo.AddAsync(inv, ct);
        await _repo.SaveChangesAsync(ct);
        return Result<PurchaseInvoiceDto>.Success(PurchaseMapper.ToDto(inv));
    }

    private async Task<LinesBuildResult> BuildLines(
        List<PurchaseLineInput> inputs,
        Guid invoiceId,
        Guid tid,
        Guid? globalWhId,
        CancellationToken ct,
        Guid? supplierId = null
    )
    {
        var lines = new List<PurchaseInvoiceDetail>();
        foreach (var l in inputs)
        {
            var vatCode = l.VatCode;
            var iceCode = l.IceCode;
            string? snapshotSku = null;
            string? snapshotItemName = null;
            string? snapshotSupplierCode = null;
            string uomCode = "UNIT";

            if (l.ItemId.HasValue)
            {
                var item = await _itemRepo.GetByIdLightAsync(l.ItemId.Value, tid, ct);
                if (item is not null)
                {
                    snapshotSku = item.Code.SKU;
                    snapshotItemName = item.Code.Description;
                    snapshotSupplierCode = await SupplierCodeResolver.ResolveAsync(
                        _itemRepo,
                        l.ItemId.Value,
                        tid,
                        supplierId,
                        ct
                    );
                    uomCode = item.DefaultUomCode;

                    if (string.IsNullOrWhiteSpace(vatCode))
                        vatCode = item.TaxConfig.PurchaseVatCode ?? vatCode;
                    if (string.IsNullOrWhiteSpace(iceCode))
                        iceCode = item.TaxConfig.ExciseTaxCode;
                }
            }

            if (string.IsNullOrWhiteSpace(vatCode))
                return new(
                    null!,
                    Result<PurchaseInvoiceDto>.ValidationFailure(
                        $"Línea '{l.Description}': código IVA obligatorio. Seleccione un producto con tarifa IVA o indique el código manualmente."
                    )
                );

            string? snapshotWhCode = null;
            var whId = l.WarehouseId ?? globalWhId;
            if (whId.HasValue)
            {
                var wh = await _whRepo.GetByIdAsync(tid, whId.Value, ct);
                snapshotWhCode = wh?.Code;
            }

            var line = PurchaseInvoiceDetail.Create(
                invoiceId,
                tid,
                l.Description,
                l.Quantity,
                l.UnitPrice,
                vatCode,
                uomCode,
                l.ItemId,
                l.WarehouseId,
                l.Notes,
                l.DiscountPct,
                iceCode,
                snapshotSku,
                snapshotItemName,
                snapshotSupplierCode,
                conversionFactor: 1m,
                snapshotWarehouseCode: snapshotWhCode,
                purchaseOrderDetailId: l.PurchaseOrderDetailId,
                orderedQuantity: l.OrderedQuantity,
                purchaseReceptionLineId: l.PurchaseReceptionLineId
            );

            var taxResult = await TaxHelper.ResolveTaxesAsync(line, _tax, ct);
            if (taxResult is not null)
                return new(null!, taxResult);
            lines.Add(line);
        }
        return new(lines, null);
    }

    private sealed record LinesBuildResult(
        List<PurchaseInvoiceDetail> Lines,
        Result<PurchaseInvoiceDto>? Error
    );
}

public sealed class UpdatePurchaseDraftHandler
    : IRequestHandler<UpdatePurchaseDraftCommand, Result<PurchaseInvoiceDto>>
{
    private readonly IPurchaseInvoiceRepository _repo;
    private readonly IBusinessPartnerRepository _bpRepo;
    private readonly IBusinessPartnerRoleRepository _roleRepo;
    private readonly IPaymentTermRepository _ptRepo;
    private readonly IItemRepository _itemRepo;
    private readonly IWarehouseRepository _whRepo;
    private readonly ISriTaxResolver _tax;
    private readonly ICurrentTenant _t;
    private readonly ICurrentUser _u;

    public UpdatePurchaseDraftHandler(
        IPurchaseInvoiceRepository repo,
        IBusinessPartnerRepository bpRepo,
        IBusinessPartnerRoleRepository roleRepo,
        IPaymentTermRepository ptRepo,
        IItemRepository itemRepo,
        IWarehouseRepository whRepo,
        ISriTaxResolver tax,
        ICurrentTenant t,
        ICurrentUser u
    )
    {
        _repo = repo;
        _bpRepo = bpRepo;
        _roleRepo = roleRepo;
        _ptRepo = ptRepo;
        _itemRepo = itemRepo;
        _whRepo = whRepo;
        _tax = tax;
        _t = t;
        _u = u;
    }

    public async Task<Result<PurchaseInvoiceDto>> Handle(
        UpdatePurchaseDraftCommand cmd,
        CancellationToken ct
    )
    {
        var supplier = await _bpRepo.GetByIdAsync(cmd.SupplierId, ct);
        if (supplier is null)
            return Result<PurchaseInvoiceDto>.NotFound("Proveedor no encontrado.");
        if (!supplier.IsActive)
            return Result<PurchaseInvoiceDto>.ValidationFailure(
                "El proveedor se encuentra inactivo."
            );

        var inv = await _repo.GetByIdAsync(_t.TenantId, cmd.Id, ct);
        if (inv is null)
            return Result<PurchaseInvoiceDto>.NotFound("Compra no encontrada.");

        if (cmd.PaymentTermId.HasValue && cmd.PaymentTermId.Value != inv.PaymentTermId)
        {
            var pt = await _ptRepo.GetByIdAsync(_t.TenantId, cmd.PaymentTermId.Value, ct);
            if (pt is not null)
                inv.UpdatePaymentTermSnapshot(
                    pt.Id,
                    pt.Name,
                    pt.Installments,
                    pt.DaysBetweenInstallments
                );
        }
        else if (cmd.SupplierId != inv.SupplierId)
        {
            var role = await _roleRepo.GetByTypeAsync(
                cmd.SupplierId,
                Domain.MasterData.Enums.RoleType.Supplier,
                ct
            );
            if (role?.SupplierConfig is not null)
            {
                var pt = await _ptRepo.GetByIdAsync(
                    _t.TenantId,
                    role.SupplierConfig.PaymentTermId,
                    ct
                );
                if (pt is not null)
                    inv.UpdatePaymentTermSnapshot(
                        pt.Id,
                        pt.Name,
                        pt.Installments,
                        pt.DaysBetweenInstallments
                    );
            }
        }

        try
        {
            string? pmName = null;
            if (!string.IsNullOrWhiteSpace(cmd.SriPaymentMethodCode))
                pmName = await _tax.GetPaymentMethodNameAsync(cmd.SriPaymentMethodCode.Trim(), ct);

            inv.UpdateDraft(
                cmd.SupplierId,
                supplier.Name.LegalName,
                supplier.Identification.Number,
                cmd.DocTypeCode,
                cmd.InvoiceNumber,
                cmd.IssueDate,
                _u.UserId,
                cmd.AccessKey,
                cmd.AuthorizationNumber,
                cmd.AuthorizationDate,
                cmd.TaxSupportCode,
                cmd.SriPaymentMethodCode,
                pmName,
                cmd.GlobalWarehouseId,
                cmd.DueDate,
                cmd.Notes
            );

            var lines = new List<PurchaseInvoiceDetail>();
            foreach (var l in cmd.Lines)
            {
                var vatCode = l.VatCode;
                var iceCode = l.IceCode;
                string? snapshotSku = null;
                string? snapshotItemName = null;
                string? snapshotSupplierCode = null;
                string uomCode = "UNIT";

                if (l.ItemId.HasValue)
                {
                    var item = await _itemRepo.GetByIdLightAsync(l.ItemId.Value, _t.TenantId, ct);
                    if (item is not null)
                    {
                        snapshotSku = item.Code.SKU;
                        snapshotItemName = item.Code.Description;
                        snapshotSupplierCode = await SupplierCodeResolver.ResolveAsync(
                            _itemRepo,
                            l.ItemId.Value,
                            _t.TenantId,
                            cmd.SupplierId,
                            ct
                        );
                        uomCode = item.DefaultUomCode;

                        if (string.IsNullOrWhiteSpace(vatCode))
                            vatCode = item.TaxConfig.PurchaseVatCode ?? vatCode;
                        if (string.IsNullOrWhiteSpace(iceCode))
                            iceCode = item.TaxConfig.ExciseTaxCode;
                    }
                }

                if (string.IsNullOrWhiteSpace(vatCode))
                    return Result<PurchaseInvoiceDto>.ValidationFailure(
                        $"Línea '{l.Description}': código IVA obligatorio."
                    );

                string? snapshotWhCode = null;
                var whId = l.WarehouseId ?? cmd.GlobalWarehouseId;
                if (whId.HasValue)
                {
                    var wh = await _whRepo.GetByIdAsync(_t.TenantId, whId.Value, ct);
                    snapshotWhCode = wh?.Code;
                }

                var line = PurchaseInvoiceDetail.Create(
                    inv.Id,
                    _t.TenantId,
                    l.Description,
                    l.Quantity,
                    l.UnitPrice,
                    vatCode,
                    uomCode,
                    l.ItemId,
                    l.WarehouseId,
                    l.Notes,
                    l.DiscountPct,
                    iceCode,
                    snapshotSku,
                    snapshotItemName,
                    snapshotSupplierCode,
                    conversionFactor: 1m,
                    snapshotWarehouseCode: snapshotWhCode,
                    purchaseOrderDetailId: l.PurchaseOrderDetailId,
                    orderedQuantity: l.OrderedQuantity,
                    purchaseReceptionLineId: l.PurchaseReceptionLineId
                );

                var taxResult = await TaxHelper.ResolveTaxesAsync(line, _tax, ct);
                if (taxResult is not null)
                    return taxResult;
                lines.Add(line);
            }
            await _repo.RemoveLinesByInvoiceAsync(inv.Id, lines, ct);
            inv.ReplaceLines(lines, _u.UserId);
            if (cmd.FreightCost > 0 || cmd.OtherCosts > 0)
                inv.DistributeCosts(cmd.FreightCost, cmd.OtherCosts, _u.UserId);
        }
        catch (InvalidOperationException ex)
        {
            return Result<PurchaseInvoiceDto>.ValidationFailure(ex.Message);
        }

        await _repo.SaveChangesAsync(ct);
        return Result<PurchaseInvoiceDto>.Success(PurchaseMapper.ToDto(inv));
    }
}

file static class TaxHelper
{
    public static async Task<Result<PurchaseInvoiceDto>?> ResolveTaxesAsync(
        PurchaseInvoiceDetail line,
        ISriTaxResolver tax,
        CancellationToken ct
    )
    {
        var vatResult = await tax.GetVatRateWithNameAsync(line.VatCode, ct);
        if (vatResult is null)
            return Result<PurchaseInvoiceDto>.ValidationFailure(
                $"Código IVA '{line.VatCode}' no encontrado o inactivo."
            );

        decimal iceRate = 0;
        string? iceName = null;
        if (!string.IsNullOrWhiteSpace(line.IceCode))
        {
            var iceResult = await tax.GetIceRateWithNameAsync(line.IceCode, ct);
            if (iceResult is null)
                return Result<PurchaseInvoiceDto>.ValidationFailure(
                    $"Código ICE '{line.IceCode}' no encontrado o inactivo."
                );
            iceRate = iceResult.Rate;
            iceName = iceResult.Name;
        }

        line.ApplyTaxes(
            line.VatCode,
            vatResult.Rate,
            vatResult.Name,
            line.IceCode,
            iceRate,
            iceName
        );
        return null;
    }
}

public sealed class GetPurchaseByIdHandler
    : IRequestHandler<GetPurchaseByIdQuery, Result<PurchaseInvoiceDto>>
{
    private readonly IPurchaseInvoiceRepository _repo;
    private readonly ICurrentTenant _t;

    public GetPurchaseByIdHandler(IPurchaseInvoiceRepository repo, ICurrentTenant t)
    {
        _repo = repo;
        _t = t;
    }

    public async Task<Result<PurchaseInvoiceDto>> Handle(
        GetPurchaseByIdQuery q,
        CancellationToken ct
    )
    {
        var inv = await _repo.GetByIdAsync(_t.TenantId, q.Id, ct);
        return inv is null
            ? Result<PurchaseInvoiceDto>.NotFound("Compra no encontrada.")
            : Result<PurchaseInvoiceDto>.Success(PurchaseMapper.ToDto(inv));
    }
}

public sealed class GetPurchaseListHandler
    : IRequestHandler<GetPurchaseListQuery, Result<PurchaseListResponse>>
{
    private readonly IPurchaseInvoiceRepository _repo;
    private readonly ICurrentTenant _t;

    public GetPurchaseListHandler(IPurchaseInvoiceRepository repo, ICurrentTenant t)
    {
        _repo = repo;
        _t = t;
    }

    public async Task<Result<PurchaseListResponse>> Handle(
        GetPurchaseListQuery q,
        CancellationToken ct
    )
    {
        var (items, lineCounts, total) = await _repo.GetPagedAsync(
            _t.TenantId,
            q.Search,
            q.Status,
            q.PageNumber,
            q.PageSize,
            ct
        );
        var dtos = items
            .Select(i => new PurchaseListDto(
                i.Id,
                i.InvoiceNumber,
                i.IssueDate,
                i.SupplierId,
                i.Status.ToString(),
                lineCounts.GetValueOrDefault(i.Id),
                i.CreatedAt
            ))
            .ToList();
        return Result<PurchaseListResponse>.Success(
            new PurchaseListResponse(dtos, total, q.PageNumber, q.PageSize)
        );
    }
}

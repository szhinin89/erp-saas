using ERP.Application.Common;
using ERP.Application.Common.Persistence;
using ERP.Application.Modules.Purchases.DTOs;
using ERP.Application.Modules.Purchases.Services;
using ERP.Domain.MasterData.Interfaces;
using ERP.Domain.Modules.Inventory.Interfaces;
using ERP.Domain.Modules.Items.Entities;
using ERP.Domain.Modules.Items.Interfaces;
using ERP.Domain.Modules.Purchases.Entities;
using ERP.Domain.Modules.Purchases.Enums;
using ERP.Domain.Modules.Purchases.Interfaces;
using ERP.Domain.Modules.Purchases.PurchaseReception.Entities;
using ERP.Domain.Modules.Purchases.PurchaseReception.Interfaces;
using ERP.Domain.Modules.SriCatalogs.Enums;
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
    Guid? PurchaseReceptionLineId = null,
    Guid? PackagingLevelId = null
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

file sealed record PurchaseLinePackagingSnapshot(
    Guid? PackagingLevelId,
    string UomCode,
    string BaseUomCode,
    decimal ConversionFactor
);

file sealed record PurchaseLinePackagingResolveResult(
    PurchaseLinePackagingSnapshot? Packaging,
    Result<PurchaseInvoiceDto>? Error
);

file static class PurchaseLinePackagingResolver
{
    public static async Task<PurchaseLinePackagingResolveResult> ResolveAsync(
        IItemRepository itemRepo,
        IPurchaseReceptionDocumentRepository receptionRepo,
        Item item,
        PurchaseLineInput line,
        Guid? supplierId,
        Guid tenantId,
        CancellationToken ct
    )
    {
        var fallback = new PurchaseLinePackagingSnapshot(
            null,
            item.DefaultUomCode,
            item.DefaultUomCode,
            1m
        );

        if (line.PackagingLevelId.HasValue)
        {
            var selected = item.PackagingLevels.FirstOrDefault(p =>
                p.Id == line.PackagingLevelId.Value && p.IsActive
            );
            if (selected is null)
                return new(
                    null,
                    Result<PurchaseInvoiceDto>.ValidationFailure(
                        $"Línea '{line.Description}': la presentación seleccionada no pertenece al ítem o está inactiva."
                    )
                );

            return new(ToSnapshot(item, selected), null);
        }

        if (supplierId is null || line.PurchaseReceptionLineId is null)
            return new(fallback, null);

        var document = await receptionRepo.GetByLineIdAsync(
            tenantId,
            line.PurchaseReceptionLineId.Value,
            ct
        );
        var receptionLine = document
            ?.Lines.FirstOrDefault(l => l.Id == line.PurchaseReceptionLineId.Value);
        if (string.IsNullOrWhiteSpace(receptionLine?.SupplierCode))
            return new(fallback, null);

        var match = await itemRepo.GetSupplierCodeMatchAsync(
            supplierId.Value,
            receptionLine.SupplierCode,
            tenantId,
            ct
        );
        if (match is null || match.ItemId != item.Id || match.PackagingLevelId is null)
            return new(fallback, null);

        if (
            string.IsNullOrWhiteSpace(match.PackagingUomCode)
            || match.PackagingBaseQuantity is null
            || match.PackagingBaseQuantity <= 0
        )
            return new(fallback, null);

        return new(
            new PurchaseLinePackagingSnapshot(
                match.PackagingLevelId,
                match.PackagingUomCode,
                match.BaseUomCode,
                match.PackagingBaseQuantity.Value
            ),
            null
        );
    }

    private static PurchaseLinePackagingSnapshot ToSnapshot(Item item, ItemPackagingLevel packaging)
    {
        if (packaging.BaseQuantity <= 0)
            throw new InvalidOperationException("La cantidad base del empaque debe ser mayor a cero.");

        return new PurchaseLinePackagingSnapshot(
            packaging.Id,
            packaging.UomCode,
            item.DefaultUomCode,
            packaging.BaseQuantity
        );
    }
}

/// <summary>
/// FLOW-READY-02F.1 — carga las taxes crudas capturadas del XML para una línea de Recepción
/// Electrónica ya persistida. El cliente solo envía <c>PurchaseReceptionLineId</c> — el detalle
/// tributario siempre se lee del servidor, nunca del body (mismo principio que
/// <see cref="SupplierCodeResolver"/>).
/// </summary>
file static class ReceptionTaxLookup
{
    public static async Task<IReadOnlyList<PurchaseReceptionLineTax>> LoadAsync(
        IPurchaseReceptionDocumentRepository receptionRepo,
        Guid tenantId,
        Guid purchaseReceptionLineId,
        CancellationToken ct
    )
    {
        var doc = await receptionRepo.GetByLineIdAsync(tenantId, purchaseReceptionLineId, ct);
        var line = doc?.Lines.FirstOrDefault(l => l.Id == purchaseReceptionLineId);
        return line?.Taxes ?? Array.Empty<PurchaseReceptionLineTax>();
    }
}

file static class PurchaseAccessKeyDuplicateGuard
{
    public const string ConstraintName = "uq_purchase_invoices_tenant_access_key";
    public const string DuplicateAccessKeyMessage =
        "Ya existe una compra registrada con esta clave de acceso SRI.";

    public static async Task<PurchaseInvoice?> FindDuplicateAsync(
        IPurchaseInvoiceRepository repo,
        Guid tenantId,
        string? accessKey,
        Guid? currentInvoiceId,
        CancellationToken ct
    )
    {
        if (string.IsNullOrWhiteSpace(accessKey))
            return null;

        var existing = await repo.GetByAccessKeyAsync(tenantId, accessKey.Trim(), ct);
        if (existing is null || existing.Id == currentInvoiceId)
            return null;

        return existing;
    }

    public static Result<T>? ToConflictIfDuplicate<T>(PurchaseInvoice? duplicate) =>
        duplicate is null ? null : Result<T>.Conflict(DuplicateAccessKeyMessage);

    public static string? MapUniqueViolation(string? constraintName) =>
        string.Equals(constraintName, ConstraintName, StringComparison.Ordinal)
            ? DuplicateAccessKeyMessage
            : null;
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

public sealed record GetPurchaseByAccessKeyQuery(string AccessKey)
    : IRequest<Result<PurchaseAccessKeyLookupDto>>,
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

public sealed record PurchaseAccessKeyLookupDto(
    bool Exists,
    Guid? PurchaseId,
    string? InvoiceNumber,
    string? SupplierName,
    string? AccessKey
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
    /// <summary>
    /// CLEAN-01C: <c>DocTypeCode</c> se persiste en <c>PurchaseInvoice</c> sin FK (a diferencia de
    /// <c>DocumentSequence</c>, que sí referencia <c>SriDocType</c>) — sin esta regla, un cliente
    /// podía enviar cualquier string de hasta 5 caracteres y quedaba guardado sin validar contra el
    /// catálogo fiscal real.
    /// </summary>
    public CreatePurchaseDraftValidator(
        ERP.Application.Common.Services.ISriDocTypeCatalogResolver docTypeCatalogResolver
    )
    {
        RuleFor(x => x.SupplierId).NotEmpty().WithMessage("El proveedor es obligatorio.");
        RuleFor(x => x.DocTypeCode)
            .NotEmpty()
            .MaximumLength(PurchaseInvoice.DocTypeCodeMaxLen)
            .WithMessage("El tipo de comprobante es obligatorio.")
            .MustAsync(
                (code, ct) => docTypeCatalogResolver.IsActiveElectronicDocTypeAsync(code, ct)
            )
            .WithMessage("El tipo de comprobante no corresponde a un código SRI activo.")
            .When(x => !string.IsNullOrWhiteSpace(x.DocTypeCode));
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
    /// <summary>CLEAN-01C: mismo criterio que <see cref="CreatePurchaseDraftValidator"/> — ver comentario ahí.</summary>
    public UpdatePurchaseDraftValidator(
        ERP.Application.Common.Services.ISriDocTypeCatalogResolver docTypeCatalogResolver
    )
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.SupplierId).NotEmpty().WithMessage("El proveedor es obligatorio.");
        RuleFor(x => x.DocTypeCode)
            .NotEmpty()
            .MaximumLength(PurchaseInvoice.DocTypeCodeMaxLen)
            .WithMessage("El tipo de comprobante es obligatorio.")
            .MustAsync(
                (code, ct) => docTypeCatalogResolver.IsActiveElectronicDocTypeAsync(code, ct)
            )
            .WithMessage("El tipo de comprobante no corresponde a un código SRI activo.")
            .When(x => !string.IsNullOrWhiteSpace(x.DocTypeCode));
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
    private readonly IPurchaseReceptionDocumentRepository _receptionRepo;
    private readonly ICurrentTenant _t;
    private readonly ICurrentCompany _c;
    private readonly ICurrentBranch _b;
    private readonly ICurrentUser _u;
    private readonly IDatabaseExceptionTranslator _dbEx;

    public CreatePurchaseDraftHandler(
        IPurchaseInvoiceRepository repo,
        IBusinessPartnerRepository bpRepo,
        IBusinessPartnerRoleRepository roleRepo,
        IPaymentTermRepository ptRepo,
        IItemRepository itemRepo,
        IWarehouseRepository whRepo,
        ISriTaxResolver tax,
        IPurchaseReceptionDocumentRepository receptionRepo,
        ICurrentTenant t,
        ICurrentCompany c,
        ICurrentBranch b,
        ICurrentUser u,
        IDatabaseExceptionTranslator dbEx
    )
    {
        _repo = repo;
        _bpRepo = bpRepo;
        _roleRepo = roleRepo;
        _ptRepo = ptRepo;
        _itemRepo = itemRepo;
        _whRepo = whRepo;
        _tax = tax;
        _receptionRepo = receptionRepo;
        _t = t;
        _c = c;
        _b = b;
        _u = u;
        _dbEx = dbEx;
    }

    public async Task<Result<PurchaseInvoiceDto>> Handle(
        CreatePurchaseDraftCommand cmd,
        CancellationToken ct
    )
    {
        var tid = _t.TenantId;
        var duplicate = await PurchaseAccessKeyDuplicateGuard.FindDuplicateAsync(
            _repo,
            tid,
            cmd.AccessKey,
            currentInvoiceId: null,
            ct
        );
        var duplicateResult =
            PurchaseAccessKeyDuplicateGuard.ToConflictIfDuplicate<PurchaseInvoiceDto>(duplicate);
        if (duplicateResult is not null)
            return duplicateResult;

        var supplier = await _bpRepo.GetByIdAsync(cmd.SupplierId, ct);
        if (supplier is null)
            return Result<PurchaseInvoiceDto>.NotFound("Proveedor no encontrado.");
        if (!supplier.IsActive)
            return Result<PurchaseInvoiceDto>.ValidationFailure(
                $"El proveedor '{supplier.Name.LegalName}' se encuentra inactivo."
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
        try
        {
            await _repo.SaveChangesAsync(ct);
        }
        catch (Exception ex) when (_dbEx.TryGetUniqueViolation(ex, out var info))
        {
            var mapped = PurchaseAccessKeyDuplicateGuard.MapUniqueViolation(info.ConstraintName);
            if (mapped is not null)
                return Result<PurchaseInvoiceDto>.Conflict(mapped);
            throw;
        }
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
            var packaging = new PurchaseLinePackagingSnapshot(null, "UNIT", "UNIT", 1m);

            if (l.ItemId.HasValue)
            {
                var item = await _itemRepo.GetByIdAsync(l.ItemId.Value, tid, ct);
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
                    var packagingResult = await PurchaseLinePackagingResolver.ResolveAsync(
                        _itemRepo,
                        _receptionRepo,
                        item,
                        l,
                        supplierId,
                        tid,
                        ct
                    );
                    if (packagingResult.Error is not null)
                        return new(null!, packagingResult.Error);
                    packaging = packagingResult.Packaging!;

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
                packaging.UomCode,
                l.ItemId,
                l.WarehouseId,
                l.Notes,
                l.DiscountPct,
                iceCode,
                snapshotSku,
                snapshotItemName,
                snapshotSupplierCode,
                conversionFactor: packaging.ConversionFactor,
                snapshotWarehouseCode: snapshotWhCode,
                purchaseOrderDetailId: l.PurchaseOrderDetailId,
                orderedQuantity: l.OrderedQuantity,
                purchaseReceptionLineId: l.PurchaseReceptionLineId,
                baseUomCode: packaging.BaseUomCode,
                packagingLevelId: packaging.PackagingLevelId
            );

            var taxResult = await ResolveLineTaxesAsync(line, l.PurchaseReceptionLineId, tid, ct);
            if (taxResult is not null)
                return new(null!, taxResult);
            lines.Add(line);
        }
        return new(lines, null);
    }

    /// <summary>
    /// FLOW-READY-02F.1 — si la línea viene de Recepción Electrónica Y esa recepción capturó taxes
    /// genéricas (esta fase en adelante), se resuelve IVA/ICE/IRBPNR fiel al XML. En cualquier otro
    /// caso (línea manual, o recepción de antes de esta fase sin taxes capturadas) se mantiene el
    /// comportamiento histórico exacto (<see cref="TaxHelper.ResolveTaxesAsync"/>).
    /// </summary>
    private async Task<Result<PurchaseInvoiceDto>?> ResolveLineTaxesAsync(
        PurchaseInvoiceDetail line,
        Guid? purchaseReceptionLineId,
        Guid tenantId,
        CancellationToken ct
    )
    {
        if (purchaseReceptionLineId.HasValue)
        {
            var receptionTaxes = await ReceptionTaxLookup.LoadAsync(
                _receptionRepo,
                tenantId,
                purchaseReceptionLineId.Value,
                ct
            );
            if (receptionTaxes.Count > 0)
                return await ReceptionTaxHelper.ApplyReceptionTaxesAsync(line, receptionTaxes, _tax, ct);
        }
        return await TaxHelper.ResolveTaxesAsync(line, _tax, ct);
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
    private readonly IPurchaseReceptionDocumentRepository _receptionRepo;
    private readonly ICurrentTenant _t;
    private readonly ICurrentUser _u;
    private readonly IDatabaseExceptionTranslator _dbEx;

    public UpdatePurchaseDraftHandler(
        IPurchaseInvoiceRepository repo,
        IBusinessPartnerRepository bpRepo,
        IBusinessPartnerRoleRepository roleRepo,
        IPaymentTermRepository ptRepo,
        IItemRepository itemRepo,
        IWarehouseRepository whRepo,
        ISriTaxResolver tax,
        IPurchaseReceptionDocumentRepository receptionRepo,
        ICurrentTenant t,
        ICurrentUser u,
        IDatabaseExceptionTranslator dbEx
    )
    {
        _repo = repo;
        _bpRepo = bpRepo;
        _roleRepo = roleRepo;
        _ptRepo = ptRepo;
        _itemRepo = itemRepo;
        _whRepo = whRepo;
        _tax = tax;
        _receptionRepo = receptionRepo;
        _t = t;
        _u = u;
        _dbEx = dbEx;
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
                $"El proveedor '{supplier.Name.LegalName}' se encuentra inactivo."
            );

        var inv = await _repo.GetByIdAsync(_t.TenantId, cmd.Id, ct);
        if (inv is null)
            return Result<PurchaseInvoiceDto>.NotFound("Compra no encontrada.");

        var duplicate = await PurchaseAccessKeyDuplicateGuard.FindDuplicateAsync(
            _repo,
            _t.TenantId,
            cmd.AccessKey,
            currentInvoiceId: inv.Id,
            ct
        );
        var duplicateResult =
            PurchaseAccessKeyDuplicateGuard.ToConflictIfDuplicate<PurchaseInvoiceDto>(duplicate);
        if (duplicateResult is not null)
            return duplicateResult;

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
                var packaging = new PurchaseLinePackagingSnapshot(null, "UNIT", "UNIT", 1m);

                if (l.ItemId.HasValue)
                {
                    var item = await _itemRepo.GetByIdAsync(l.ItemId.Value, _t.TenantId, ct);
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
                        var packagingResult = await PurchaseLinePackagingResolver.ResolveAsync(
                            _itemRepo,
                            _receptionRepo,
                            item,
                            l,
                            cmd.SupplierId,
                            _t.TenantId,
                            ct
                        );
                        if (packagingResult.Error is not null)
                            return packagingResult.Error;
                        packaging = packagingResult.Packaging!;

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
                    packaging.UomCode,
                    l.ItemId,
                    l.WarehouseId,
                    l.Notes,
                    l.DiscountPct,
                    iceCode,
                    snapshotSku,
                    snapshotItemName,
                    snapshotSupplierCode,
                    conversionFactor: packaging.ConversionFactor,
                    snapshotWarehouseCode: snapshotWhCode,
                    purchaseOrderDetailId: l.PurchaseOrderDetailId,
                    orderedQuantity: l.OrderedQuantity,
                    purchaseReceptionLineId: l.PurchaseReceptionLineId,
                    baseUomCode: packaging.BaseUomCode,
                    packagingLevelId: packaging.PackagingLevelId
                );

                Result<PurchaseInvoiceDto>? taxResult = null;
                if (l.PurchaseReceptionLineId.HasValue)
                {
                    var receptionTaxes = await ReceptionTaxLookup.LoadAsync(
                        _receptionRepo,
                        _t.TenantId,
                        l.PurchaseReceptionLineId.Value,
                        ct
                    );
                    if (receptionTaxes.Count > 0)
                        taxResult = await ReceptionTaxHelper.ApplyReceptionTaxesAsync(
                            line,
                            receptionTaxes,
                            _tax,
                            ct
                        );
                    else
                        taxResult = await TaxHelper.ResolveTaxesAsync(line, _tax, ct);
                }
                else
                {
                    taxResult = await TaxHelper.ResolveTaxesAsync(line, _tax, ct);
                }
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

        try
        {
            await _repo.SaveChangesAsync(ct);
        }
        catch (Exception ex) when (_dbEx.TryGetUniqueViolation(ex, out var info))
        {
            var mapped = PurchaseAccessKeyDuplicateGuard.MapUniqueViolation(info.ConstraintName);
            if (mapped is not null)
                return Result<PurchaseInvoiceDto>.Conflict(mapped);
            throw;
        }
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

/// <summary>
/// FLOW-READY-02F.1 — resuelve el detalle fiel de impuestos (IVA/ICE/IRBPNR, y cualquier código
/// SRI no reconocido) capturado desde el XML de Recepción Electrónica, contra los catálogos SRI
/// globales. Solo se usa cuando la línea viene de Recepción y esa línea sí tiene taxes capturadas
/// (<see cref="ReceptionTaxLookup"/>) — si no, el caller cae de vuelta a
/// <see cref="TaxHelper.ResolveTaxesAsync"/> (líneas manuales o recepciones anteriores a esta fase).
/// </summary>
file static class ReceptionTaxHelper
{
    private const string SriVatTaxCode = "2";
    private const string SriIceTaxCode = "3";
    private const string SriIrbpnrTaxCode = "5";

    public static async Task<Result<PurchaseInvoiceDto>?> ApplyReceptionTaxesAsync(
        PurchaseInvoiceDetail line,
        IReadOnlyList<PurchaseReceptionLineTax> receptionTaxes,
        ISriTaxResolver tax,
        CancellationToken ct
    )
    {
        var built = new List<PurchaseInvoiceDetailTax>();

        string? vatCode = null;
        decimal vatRate = 0;
        string? vatName = null;

        string? iceCode = null;
        decimal iceRate = 0;
        string? iceName = null;
        var iceCalcType = SriTaxCalculationType.Percentage;
        decimal? iceExactAmount = null;

        foreach (var t in receptionTaxes)
        {
            var label = t.TaxCode switch
            {
                SriVatTaxCode => "IVA",
                SriIceTaxCode => "ICE",
                SriIrbpnrTaxCode => "IRBPNR",
                _ => null,
            };

            if (label is null)
            {
                // Código SRI no reconocido — nunca se descarta (regla: no borrar impuestos
                // desconocidos), pero tampoco se inventa una tarifa: se persiste tal como vino.
                built.Add(
                    PurchaseInvoiceDetailTax.Create(
                        line.Id,
                        line.TenantId,
                        t.TaxCode,
                        t.TaxRateCode,
                        $"Impuesto SRI (código {t.TaxCode})",
                        null,
                        SriTaxCalculationType.Percentage,
                        t.TaxableBase,
                        t.TaxAmount,
                        PurchaseTaxSource.Xml
                    )
                );
                continue;
            }

            var entry =
                t.TaxCode == SriVatTaxCode ? await tax.GetVatCatalogEntryAsync(t.TaxRateCode, ct)
                : t.TaxCode == SriIceTaxCode ? await tax.GetIceCatalogEntryAsync(t.TaxRateCode, ct)
                : await tax.GetIrbpnrCatalogEntryAsync(t.TaxRateCode, ct);

            if (entry is null)
                return Result<PurchaseInvoiceDto>.ValidationFailure(
                    $"El XML contiene impuesto {label} {t.TaxRateCode}, pero ese código no está "
                        + "cargado o activo en el catálogo SRI global."
                );

            built.Add(
                PurchaseInvoiceDetailTax.Create(
                    line.Id,
                    line.TenantId,
                    t.TaxCode,
                    t.TaxRateCode,
                    entry.Name,
                    entry.Percentage ?? entry.UnitValue,
                    entry.CalculationType,
                    t.TaxableBase,
                    t.TaxAmount,
                    PurchaseTaxSource.Xml
                )
            );

            switch (t.TaxCode)
            {
                case SriVatTaxCode:
                    vatCode = t.TaxRateCode;
                    vatRate = entry.Percentage ?? 0m;
                    vatName = entry.Name;
                    break;
                case SriIceTaxCode:
                    iceCode = t.TaxRateCode;
                    iceName = entry.Name;
                    iceCalcType = entry.CalculationType;
                    if (entry.CalculationType == SriTaxCalculationType.Specific)
                    {
                        iceExactAmount = t.TaxAmount;
                        iceRate = 0m;
                    }
                    else
                    {
                        iceRate = entry.Percentage ?? 0m;
                    }
                    break;
                // IRBPNR no tiene campos escalares legacy — solo vive en la colección Taxes.
            }
        }

        if (string.IsNullOrWhiteSpace(vatCode))
            return Result<PurchaseInvoiceDto>.ValidationFailure(
                "La línea no tiene impuesto IVA — no se puede resolver."
            );

        line.ApplyTaxes(vatCode, vatRate, vatName, iceCode, iceRate, iceName, iceCalcType, iceExactAmount);
        line.ReplaceTaxes(built);
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

public sealed class GetPurchaseByAccessKeyHandler
    : IRequestHandler<GetPurchaseByAccessKeyQuery, Result<PurchaseAccessKeyLookupDto>>
{
    private readonly IPurchaseInvoiceRepository _repo;
    private readonly ICurrentTenant _t;

    public GetPurchaseByAccessKeyHandler(IPurchaseInvoiceRepository repo, ICurrentTenant t)
    {
        _repo = repo;
        _t = t;
    }

    public async Task<Result<PurchaseAccessKeyLookupDto>> Handle(
        GetPurchaseByAccessKeyQuery q,
        CancellationToken ct
    )
    {
        if (string.IsNullOrWhiteSpace(q.AccessKey))
            return Result<PurchaseAccessKeyLookupDto>.ValidationFailure(
                "La clave de acceso es obligatoria."
            );

        var accessKey = q.AccessKey.Trim();
        var inv = await _repo.GetByAccessKeyAsync(_t.TenantId, accessKey, ct);
        return Result<PurchaseAccessKeyLookupDto>.Success(
            inv is null
                ? new PurchaseAccessKeyLookupDto(false, null, null, null, accessKey)
                : new PurchaseAccessKeyLookupDto(
                    true,
                    inv.Id,
                    inv.InvoiceNumber,
                    inv.SupplierName,
                    inv.AccessKey
                )
        );
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

using ERP.Application.Common;
using ERP.Application.Common.Persistence;
using ERP.Application.Modules.Companies;
using ERP.Application.Modules.Companies.UseCases.PrecisionPolicy;
using ERP.Application.Modules.Purchases.DTOs;
using ERP.Application.Modules.Purchases.PurchaseReception.DTOs;
using ERP.Application.Modules.Purchases.PurchaseReception.PurchaseDraft;
using ERP.Application.Modules.Purchases.Services;
using ERP.Domain.MasterData.Interfaces;
using ERP.Domain.Modules.Inventory.Interfaces;
using ERP.Domain.Modules.Items.Entities;
using ERP.Domain.Modules.Items.Interfaces;
using ERP.Domain.Modules.Purchases;
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

public sealed record PurchaseXmlTaxAllocationInput(Guid PurchaseReceptionLineTaxId, decimal TaxableBase, decimal TaxAmount);
public sealed record PurchaseXmlAllocationInput(decimal Subtotal, decimal Discount,
    IReadOnlyList<PurchaseXmlTaxAllocationInput> Taxes);

public sealed class PurchaseXmlAllocationValidator : AbstractValidator<PurchaseXmlAllocationInput>
{
    public PurchaseXmlAllocationValidator()
    {
        RuleFor(x => x.Subtotal).GreaterThanOrEqualTo(0);
        RuleFor(x => x.Discount).GreaterThanOrEqualTo(0).LessThanOrEqualTo(x => x.Subtotal);
        RuleFor(x => x.Taxes).NotEmpty();
        RuleForEach(x => x.Taxes).ChildRules(t =>
        {
            t.RuleFor(x => x.PurchaseReceptionLineTaxId).NotEmpty();
            t.RuleFor(x => x.TaxableBase).GreaterThanOrEqualTo(0);
            t.RuleFor(x => x.TaxAmount).GreaterThanOrEqualTo(0);
        });
    }
}

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
    Guid? PackagingLevelId = null,
    // FASE 5 PURCHASE-UX — presentación con la que se interpreta el documento del proveedor (solo
    // líneas XML). El factor NUNCA viene del body: se resuelve del nivel de empaque del ítem (o se
    // conserva el snapshot ya persistido si el nivel no cambió). No aprende nada del proveedor.
    Guid? XmlOriginPackagingLevelId = null,
    PurchaseXmlAllocationInput? XmlAllocation = null,
    // PURCHASE-COST-DISTRIBUTION-CANONICAL-01 — Id de una línea ya guardada del mismo borrador: se
    // conserva al guardar para que sus distribuciones de costos sigan referenciándola. Flete y otros
    // costos no viajan por línea: solo cambian vía la operación canónica de distribución.
    Guid? LineId = null
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

        if (line.XmlAllocation is not null || supplierId is null || line.PurchaseReceptionLineId is null)
            return new(fallback, null);

        var document = await receptionRepo.GetByLineIdAsync(
            tenantId,
            line.PurchaseReceptionLineId.Value,
            ct
        );
        var receptionLine = document?.Lines.FirstOrDefault(l =>
            l.Id == line.PurchaseReceptionLineId.Value
        );
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
            throw new ERP.Domain.Exceptions.DomainRuleViolationException(
                "La cantidad base del empaque debe ser mayor a cero."
            );

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

/// <summary>
/// PURCHASE-WAREHOUSE-BRANCH-GUARD-01 — regla de negocio: una compra solo puede enviar
/// mercadería a bodegas de su propia sucursal (Warehouse.BranchId == PurchaseInvoice.BranchId).
/// Compartida entre Create/Update/ConfirmPurchaseHandler (no "file" porque Confirm vive en otro
/// archivo del mismo módulo). Cross-branch purchase warehouses require an explicit audited
/// permission in a future feature — no implementar aquí.
/// </summary>
public static class WarehouseBranchGuard
{
    public const string CrossBranchMessage =
        "La bodega seleccionada no pertenece a la sucursal de la compra.";
    public const string NotFoundMessage = "La bodega seleccionada no existe.";

    public static async Task<WarehouseBranchCheckResult> ValidateAsync(
        IWarehouseRepository whRepo,
        Guid tenantId,
        Guid warehouseId,
        Guid invoiceBranchId,
        CancellationToken ct
    )
    {
        var wh = await whRepo.GetByIdAsync(tenantId, warehouseId, ct);
        if (wh is null)
            return new(null, Result<PurchaseInvoiceDto>.ValidationFailure(NotFoundMessage));
        if (wh.BranchId != invoiceBranchId)
            return new(wh, Result<PurchaseInvoiceDto>.ValidationFailure(CrossBranchMessage));
        return new(wh, null);
    }
}

public sealed record WarehouseBranchCheckResult(
    ERP.Domain.Modules.Inventory.Entities.Warehouse? Warehouse,
    Result<PurchaseInvoiceDto>? Error
);

file static class PurchaseAccessKeyDuplicateGuard
{
    public const string ConstraintName = "uq_purchase_invoices_tenant_access_key";
    public const string DuplicateAccessKeyMessage =
        "Ya existe una compra registrada con esta clave de acceso SRI.";

    // RECEPTION-REPROCESS-AFTER-CANCEL-STANDARD-01 — antes sin mapear: una colisión real contra
    // este constraint (dos compras activas con el mismo supplier+número) rethrow-eaba como
    // excepción no controlada. Mismo criterio que el resto de constraints de este guard.
    public const string SupplierNumberConstraintName =
        "uq_purchase_invoices_tenant_company_supplier_number";
    public const string DuplicateSupplierNumberMessage =
        "Ya existe una compra con este número para este proveedor.";

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
        constraintName == "uq_purchase_expense_access_key"
            ? "La factura ya fue registrada como gasto."
        : string.Equals(constraintName, ConstraintName, StringComparison.Ordinal)
            ? DuplicateAccessKeyMessage
        : string.Equals(constraintName, SupplierNumberConstraintName, StringComparison.Ordinal)
            ? DuplicateSupplierNumberMessage
        : null;

    /// <summary>
    /// EXPENSES-FROM-RECEPTION-01 — mitad "Compra rechaza si ya fue usada como Gasto" del chequeo
    /// cruzado (la otra mitad vive en <c>CreateExpenseDraftHandler</c>). Misma clave de acceso SRI
    /// no puede terminar registrada como Compra Y Gasto a la vez.
    /// </summary>
    public static async Task<Result<T>?> ToConflictIfUsedByExpenseAsync<T>(
        ERP.Domain.Modules.Expenses.Interfaces.IExpenseDocumentRepository expenseRepo,
        Guid tenantId,
        string? accessKey,
        CancellationToken ct
    )
    {
        if (string.IsNullOrWhiteSpace(accessKey))
            return null;

        var usedByExpense = await expenseRepo.ExistsByAccessKeyAsync(
            tenantId,
            accessKey.Trim(),
            ct
        );
        return usedByExpense
            ? Result<T>.Conflict("Ya existe un gasto registrado con esta clave de acceso SRI.")
            : null;
    }
}

/// <summary>
/// PURCHASE-DRAFT-RECOVERY F2 — cuotas manuales del borrador. Guardar las persiste tal cual (solo
/// reglas estructurales, ver <see cref="PurchaseInvoice.ReplaceDraftPaymentSchedule"/>), aunque ya
/// no cuadren con el total o con una condición de pago cambiada: nunca se ajustan ni regeneran
/// solas. Que cuadren es requisito de Confirmar (ConfirmPurchaseHandler).
/// </summary>
file static class PurchaseDraftSchedule
{
    /// <summary>True si <paramref name="schedule"/> cambia las cuotas persistidas.</summary>
    public static bool Apply(PurchaseInvoice inv, List<ConfirmScheduleInput>? schedule)
    {
        if (schedule is null)
            return false;
        if (schedule.Count == 0)
        {
            if (inv.PaymentSchedules.Count == 0)
                return false;
            inv.ClearPaymentSchedule();
            return true;
        }
        inv.ReplaceDraftPaymentSchedule(
            schedule.Select(s => (s.InstallmentNumber, s.DueDate, s.Amount, s.Notes)).ToList()
        );
        return true;
    }

    public static void ApplyRetentionIntent(
        PurchaseInvoice inv,
        PurchaseRetentionIntentDraftDto? intent
    )
    {
        if (intent is not null)
            inv.SetRetentionIntentDraft(
                intent.AppliesRetention,
                intent.EmissionPointId,
                intent.IssueDate
            );
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
    DateOnly? DueDate = null,
    string? Notes = null,
    Guid? PaymentTermId = null,
    // PURCHASE-DRAFT-RECOVERY F2 — cuotas manuales del borrador: null = no tocar las guardadas
    // (si ya no cuadran con el total se rechaza), [] = descartarlas (se generarán desde la condición
    // de pago al confirmar), con filas = reemplazarlas (deben cuadrar exactamente con el total).
    List<ConfirmScheduleInput>? Schedule = null,
    // PURCHASE-DRAFT-RECOVERY F3 — null = no tocar la intención recordada.
    PurchaseRetentionIntentDraftDto? RetentionIntent = null
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
    DateOnly? DueDate = null,
    string? Notes = null,
    Guid? PaymentTermId = null,
    // PURCHASE-DRAFT-RECOVERY F2 — cuotas manuales del borrador: null = no tocar las guardadas
    // (si ya no cuadran con el total se rechaza), [] = descartarlas (se generarán desde la condición
    // de pago al confirmar), con filas = reemplazarlas (deben cuadrar exactamente con el total).
    List<ConfirmScheduleInput>? Schedule = null,
    // PURCHASE-DRAFT-RECOVERY F3 — null = no tocar la intención recordada.
    PurchaseRetentionIntentDraftDto? RetentionIntent = null
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
    int PageSize = 25,
    // PURCHASE-CREDIT-NOTE-ENTRY-SCREEN-DUAL-MODE-01 — solo lectura: acota por proveedor para el
    // selector manual de factura afectada. FASE 5 PURCHASE-UX: search matchea InvoiceNumber o
    // proveedor (SupplierName / SupplierTaxId).
    Guid? SupplierId = null
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
                line.RuleFor(l => l.XmlAllocation).SetValidator(new PurchaseXmlAllocationValidator()!)
                    .When(l => l.XmlAllocation is not null);
                line.RuleFor(l => l.PurchaseReceptionLineId).NotNull().When(l => l.XmlAllocation is not null);
                line.RuleFor(l => l.ItemId).NotNull().When(l => l.XmlAllocation is not null);
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
        RuleForEach(x => x.Schedule)
            .ChildRules(row =>
                row.RuleFor(r => r.Notes).MaximumLength(PurchasePaymentSchedule.NotesMaxLen)
            );
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
                line.RuleFor(l => l.XmlAllocation).SetValidator(new PurchaseXmlAllocationValidator()!)
                    .When(l => l.XmlAllocation is not null);
                line.RuleFor(l => l.PurchaseReceptionLineId).NotNull().When(l => l.XmlAllocation is not null);
                line.RuleFor(l => l.ItemId).NotNull().When(l => l.XmlAllocation is not null);
                line.RuleFor(l => l.UnitPrice).GreaterThanOrEqualTo(0);
                line.RuleFor(l => l.VatCode)
                    .NotEmpty()
                    .WithMessage("El código IVA es obligatorio por línea.");
                line.RuleFor(l => l.DiscountPct).InclusiveBetween(0, 100);
            });
        RuleForEach(x => x.Schedule)
            .ChildRules(row =>
                row.RuleFor(r => r.Notes).MaximumLength(PurchasePaymentSchedule.NotesMaxLen)
            );
    }
}

// ── Handlers ────────────────────────────────────────────────────────────

public sealed class CreatePurchaseDraftHandler
    : IRequestHandler<CreatePurchaseDraftCommand, Result<PurchaseInvoiceDto>>
{
    private readonly IPurchaseInvoiceRepository _repo;
    private readonly IBusinessPartnerRepository _bpRepo;
    private readonly IBusinessPartnerRoleRepository _roleRepo;
    private readonly ERP.Application.MasterData.Services.IPaymentTermDefaultResolver _ptResolver;
    private readonly IItemRepository _itemRepo;
    private readonly IWarehouseRepository _whRepo;
    private readonly ISriTaxResolver _tax;
    private readonly IPurchaseReceptionDocumentRepository _receptionRepo;
    private readonly ERP.Domain.Modules.Expenses.Interfaces.IExpenseDocumentRepository _expenseRepo;
    private readonly ICurrentTenant _t;
    private readonly ICurrentCompany _c;
    private readonly ICurrentBranch _b;
    private readonly ICurrentUser _u;
    private readonly IDatabaseExceptionTranslator _dbEx;
    private readonly ICompanyPrecisionPolicyProvider _precision;

    public CreatePurchaseDraftHandler(
        IPurchaseInvoiceRepository repo,
        IBusinessPartnerRepository bpRepo,
        IBusinessPartnerRoleRepository roleRepo,
        ERP.Application.MasterData.Services.IPaymentTermDefaultResolver ptResolver,
        IItemRepository itemRepo,
        IWarehouseRepository whRepo,
        ISriTaxResolver tax,
        IPurchaseReceptionDocumentRepository receptionRepo,
        ERP.Domain.Modules.Expenses.Interfaces.IExpenseDocumentRepository expenseRepo,
        ICurrentTenant t,
        ICurrentCompany c,
        ICurrentBranch b,
        ICurrentUser u,
        IDatabaseExceptionTranslator dbEx,
        ICompanyPrecisionPolicyProvider precision
    )
    {
        _precision = precision;
        _repo = repo;
        _bpRepo = bpRepo;
        _roleRepo = roleRepo;
        _ptResolver = ptResolver;
        _itemRepo = itemRepo;
        _whRepo = whRepo;
        _tax = tax;
        _receptionRepo = receptionRepo;
        _expenseRepo = expenseRepo;
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
        // RECEPTION-REPROCESS-AFTER-CANCEL-STANDARD-01 — reception queda trackeado (mismo
        // DbContext que _repo) para marcarlo Processed junto con el SaveChanges de la compra más
        // abajo, igual que AuthorizePurchaseReturnUseCases/AuthorizePurchaseCreditNoteUseCases
        // hacen con la NC. Null si la compra no viene de una recepción (alta manual).
        PurchaseReceptionDocument? linkedReception = null;

        // Reception line references also identify the source when the client omits AccessKey.
        foreach (
            var lineId in cmd
                .Lines.Where(l => l.PurchaseReceptionLineId.HasValue)
                .Select(l => l.PurchaseReceptionLineId!.Value)
                .Distinct()
        )
        {
            var source = await _receptionRepo.GetByLineIdAsync(_t.TenantId, lineId, ct);
            if (source is null)
                continue;
            if (
                source.SourceDocType
                != ERP.Domain
                    .Modules
                    .Purchases
                    .PurchaseReception
                    .Enums
                    .PurchaseReceptionSourceDocType
                    .Invoice
            )
                return Result<PurchaseInvoiceDto>.ValidationFailure(
                    "Solo una factura puede generar una compra."
                );
            if (
                !string.IsNullOrWhiteSpace(cmd.AccessKey)
                && cmd.AccessKey.Trim() != source.AccessKey
            )
                return Result<PurchaseInvoiceDto>.ValidationFailure(
                    "Las lineas no corresponden a la factura recibida."
                );
            if (await _expenseRepo.ExistsByReceptionDocumentIdAsync(_t.TenantId, source.Id, ct))
                return Result<PurchaseInvoiceDto>.Conflict(
                    "La recepcion ya fue utilizada como gasto."
                );
            cmd = cmd with { AccessKey = source.AccessKey };
            linkedReception = source;
        }
        if (cmd.DocTypeCode == "04")
            return Result<PurchaseInvoiceDto>.ValidationFailure(
                "Solo una factura puede generar una compra. Procese la nota de crédito desde su flujo propio."
            );
        if (!string.IsNullOrWhiteSpace(cmd.AccessKey))
        {
            var reception = await _receptionRepo.GetByAccessKeyAsync(
                _t.TenantId,
                cmd.AccessKey.Trim(),
                ct
            );
            if (reception is not null)
            {
                if (
                    reception.SourceDocType
                    != ERP.Domain
                        .Modules
                        .Purchases
                        .PurchaseReception
                        .Enums
                        .PurchaseReceptionSourceDocType
                        .Invoice
                )
                    return Result<PurchaseInvoiceDto>.ValidationFailure(
                        "Solo una factura puede generar una compra."
                    );
                if (
                    await _expenseRepo.ExistsByReceptionDocumentIdAsync(
                        _t.TenantId,
                        reception.Id,
                        ct
                    )
                )
                    return Result<PurchaseInvoiceDto>.Conflict(
                        "La recepción ya fue utilizada como gasto."
                    );
                linkedReception = reception;
            }
        }
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

        var expenseConflict =
            await PurchaseAccessKeyDuplicateGuard.ToConflictIfUsedByExpenseAsync<PurchaseInvoiceDto>(
                _expenseRepo,
                tid,
                cmd.AccessKey,
                ct
            );
        if (expenseConflict is not null)
            return expenseConflict;

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

        var ptResult = await _ptResolver.ResolveForPurchaseAsync(
            cmd.SupplierId,
            cmd.PaymentTermId,
            ct
        );
        if (!ptResult.IsSuccess)
            return Result<PurchaseInvoiceDto>.ValidationFailure(ptResult.Error!);
        var pt = ptResult.Value!;

        if (cmd.GlobalWarehouseId.HasValue)
        {
            var whCheck = await WarehouseBranchGuard.ValidateAsync(
                _whRepo,
                tid,
                cmd.GlobalWarehouseId.Value,
                _b.BranchId,
                ct
            );
            if (whCheck.Error is not null)
                return whCheck.Error;
        }

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
            _b.BranchId,
            ct,
            cmd.SupplierId
        );
        if (linesResult.Error is not null)
            return linesResult.Error;

        var breakdownError = await PurchaseXmlBreakdown.ValidateAsync(inv, linesResult.Lines, _receptionRepo, ct);
        if (breakdownError is not null)
            return Result<PurchaseInvoiceDto>.ValidationFailure(breakdownError);
        inv.ReplaceLines(linesResult.Lines, _u.UserId);
        try
        {
            PurchaseDraftSchedule.Apply(inv, cmd.Schedule);
        }
        catch (ArgumentException ex)
        {
            return Result<PurchaseInvoiceDto>.ValidationFailure(ex.Message);
        }
        PurchaseDraftSchedule.ApplyRetentionIntent(inv, cmd.RetentionIntent);

        // RECEPTION-REPROCESS-AFTER-CANCEL-STANDARD-01 — mismo patrón que
        // AuthorizePurchaseReturnUseCases/AuthorizePurchaseCreditNoteUseCases: la recepción queda
        // Processed, vinculada a la compra recién creada. CancelPurchaseHandler revierte esto con
        // UnmarkProcessed cuando la compra se anula, dejando la recepción lista para reprocesar.
        linkedReception?.MarkProcessed(inv.Id, _u.UserId);

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
        Guid invoiceBranchId,
        CancellationToken ct,
        Guid? supplierId = null
    )
    {
        var lines = new List<PurchaseInvoiceDetail>();
        // ERP-PRECISION-OPERATIONAL-05B: cantidad/costo unitario/factor según la política de la empresa.
        var precision = await _precision.GetEffectiveAsync(ct);
        foreach (var l in inputs)
        {
            var vatCode = l.VatCode;
            var iceCode = l.IceCode;
            string? snapshotSku = null;
            string? snapshotItemName = null;
            string? snapshotSupplierCode = null;
            var packaging = new PurchaseLinePackagingSnapshot(null, "UNIT", "UNIT", 1m);
            var participatesInInventory = false;
            Item? lineItem = null;

            if (l.ItemId.HasValue)
            {
                var item = await _itemRepo.GetByIdAsync(l.ItemId.Value, tid, ct);
                if (item is null || item.CompanyId != _c.CompanyId)
                    return new(null!, Result<PurchaseInvoiceDto>.ValidationFailure("El ítem no pertenece a esta empresa."));
                participatesInInventory = item.ParticipatesInInventory;
                lineItem = item;
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
                    packaging = packaging with
                    {
                        ConversionFactor = Math.Round(
                            packaging.ConversionFactor,
                            precision.ConversionFactorDecimals,
                            MidpointRounding.AwayFromZero
                        ),
                    };
                    if (packaging.ConversionFactor <= 0m)
                        return new(
                            null!,
                            Result<PurchaseInvoiceDto>.ValidationFailure(
                                $"Línea '{l.Description}': el factor de conversión de la presentación no es representable con {precision.ConversionFactorDecimals} decimales."
                            )
                        );
                    packaging = packaging with
                    {
                        ConversionFactor = Math.Round(
                            packaging.ConversionFactor,
                            precision.ConversionFactorDecimals,
                            MidpointRounding.AwayFromZero
                        ),
                    };
                    if (packaging.ConversionFactor <= 0m)
                        return new(
                            null!,
                            Result<PurchaseInvoiceDto>.ValidationFailure(
                                $"Línea '{l.Description}': el factor de conversión de la presentación no es representable con {precision.ConversionFactorDecimals} decimales."
                            )
                        );

                    if (string.IsNullOrWhiteSpace(vatCode))
                        vatCode = item.TaxConfig.PurchaseVatCode ?? vatCode;
                    // TAX-LINE-SSOT-ICE-IRBPNR-01 (ADR-032 §3.2/Fase 3) — ICE se resuelve desde
                    // ItemSpecialTaxConfiguration, no desde TaxConfig.ExciseTaxCode (legacy
                    // compatibility mirror, ya no se lee para decisiones nuevas).
                    if (string.IsNullOrWhiteSpace(iceCode))
                        iceCode = item
                            .SpecialTaxConfigurations.FirstOrDefault(c =>
                                c.IsActive && c.SriTaxCategoryCode == SriTaxCategoryCodes.Ice
                            )
                            ?.TaxCatalogCode;
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
            var whId = participatesInInventory ? l.WarehouseId ?? globalWhId : null;
            if (whId.HasValue)
            {
                var whCheck = await WarehouseBranchGuard.ValidateAsync(
                    _whRepo,
                    tid,
                    whId.Value,
                    invoiceBranchId,
                    ct
                );
                if (whCheck.Error is not null)
                    return new(null!, whCheck.Error);
                snapshotWhCode = whCheck.Warehouse?.Code;
            }

            var normalizedLine = await PurchaseLineNormalizer.NormalizeAsync(
                l,
                precision,
                _receptionRepo,
                tid,
                ct
            );

            var line = PurchaseInvoiceDetail.Create(
                invoiceId,
                tid,
                l.Description,
                l.Quantity,
                normalizedLine.UnitPrice,
                vatCode,
                packaging.UomCode,
                l.ItemId,
                participatesInInventory ? l.WarehouseId : null,
                l.Notes,
                normalizedLine.DiscountPct,
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
                packagingLevelId: packaging.PackagingLevelId,
                quantityDecimals: precision.QuantityDecimals,
                unitCostDecimals: precision.UnitCostDecimals,
                exactDiscountAmount: normalizedLine.ExactDiscountAmount
            );
            var xmlOrigin = PurchaseXmlOriginPackagingResolver.Resolve(
                lineItem,
                l,
                precision.ConversionFactorDecimals,
                previous: null
            );
            if (xmlOrigin.Error is not null)
                return new(null!, Result<PurchaseInvoiceDto>.ValidationFailure(xmlOrigin.Error));
            line.SetXmlOrigin(xmlOrigin.LevelId, xmlOrigin.Factor);

            var taxResult = l.XmlAllocation is not null ? null : await ResolveLineTaxesAsync(
                line,
                l.PurchaseReceptionLineId,
                normalizedLine.MatchesXmlDocument,
                tid,
                ct
            );
            if (taxResult is not null)
                return new(null!, taxResult);
            if (l.XmlAllocation is not null)
            {
                var allocationError = await PurchaseXmlBreakdown.ApplyAsync(line, l.XmlAllocation,
                    _receptionRepo, _tax, ct);
                if (allocationError is not null)
                    return new(null!, Result<PurchaseInvoiceDto>.ValidationFailure(allocationError));
            }
            lines.Add(line);
        }
        return new(lines, null);
    }

    /// <summary>
    /// FLOW-READY-02F.1 — si la línea viene de Recepción Electrónica Y esa recepción capturó taxes
    /// genéricas (esta fase en adelante), se resuelve IVA/ICE/IRBPNR fiel al XML. En cualquier otro
    /// caso (línea manual, o recepción de antes de esta fase sin taxes capturadas) se mantiene el
    /// comportamiento histórico exacto (<see cref="PurchaseTaxHelper.ResolveTaxesAsync"/>).
    /// </summary>
    private async Task<Result<PurchaseInvoiceDto>?> ResolveLineTaxesAsync(
        PurchaseInvoiceDetail line,
        Guid? purchaseReceptionLineId,
        bool matchesXmlDocument,
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
                return await ReceptionTaxHelper.ApplyReceptionTaxesAsync(
                    line,
                    receptionTaxes,
                    matchesXmlDocument,
                    _tax,
                    ct
                );
        }
        return await PurchaseTaxHelper.ResolveTaxesAsync(line, _tax, ct);
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
    private readonly ERP.Application.MasterData.Services.IPaymentTermDefaultResolver _ptResolver;
    private readonly IItemRepository _itemRepo;
    private readonly IWarehouseRepository _whRepo;
    private readonly ISriTaxResolver _tax;
    private readonly IPurchaseReceptionDocumentRepository _receptionRepo;
    private readonly ICurrentTenant _t;
    private readonly ICurrentUser _u;
    private readonly IDatabaseExceptionTranslator _dbEx;
    private readonly ICompanyPrecisionPolicyProvider _precision;
    private readonly IUnitOfWork _unitOfWork;

    public UpdatePurchaseDraftHandler(
        IPurchaseInvoiceRepository repo,
        IBusinessPartnerRepository bpRepo,
        ERP.Application.MasterData.Services.IPaymentTermDefaultResolver ptResolver,
        IItemRepository itemRepo,
        IWarehouseRepository whRepo,
        ISriTaxResolver tax,
        IPurchaseReceptionDocumentRepository receptionRepo,
        ICurrentTenant t,
        ICurrentUser u,
        IDatabaseExceptionTranslator dbEx,
        ICompanyPrecisionPolicyProvider precision,
        IUnitOfWork unitOfWork
    )
    {
        _precision = precision;
        _unitOfWork = unitOfWork;
        _repo = repo;
        _bpRepo = bpRepo;
        _ptResolver = ptResolver;
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

        // PURCHASE-DRAFT-RECOVERY — Update atómico (mismo patrón que UpdateSalesDraftHandler A3 /
        // ResolvePurchaseReceptionLinesHandler): los DELETE directos de líneas y cuotas
        // (RemoveLinesByInvoiceAsync / ClearScheduleTrackingAsync), la reconstrucción del agregado
        // y el SaveChangesAsync final corren en UNA transacción de IUnitOfWork.ExecuteInTransactionAsync.
        // Cualquier rechazo o fallo de persistencia posterior hace rollback completo: compra, líneas
        // y cuotas quedan exactamente como estaban.
        try
        {
            await _unitOfWork.ExecuteInTransactionAsync(
                async token =>
                {
                    var rejection = await ApplyUpdateAsync(cmd, inv, supplier, token);
                    if (rejection is not null)
                        throw new PurchaseDraftRejectionException(rejection);
                },
                ct
            );
        }
        catch (PurchaseDraftRejectionException ex)
        {
            _unitOfWork.ClearChangeTracker();
            return ex.Rejection;
        }
        catch (ArgumentException ex)
        {
            // Reglas estructurales de dominio (p. ej. cuotas inválidas), ahora con rollback.
            _unitOfWork.ClearChangeTracker();
            return Result<PurchaseInvoiceDto>.ValidationFailure(ex.Message);
        }
        catch (ERP.Domain.Exceptions.DomainRuleViolationException ex)
        {
            _unitOfWork.ClearChangeTracker();
            return Result<PurchaseInvoiceDto>.FromDomainRule(ex);
        }
        catch (Exception ex) when (_dbEx.TryGetUniqueViolation(ex, out var info))
        {
            _unitOfWork.ClearChangeTracker();
            var mapped = PurchaseAccessKeyDuplicateGuard.MapUniqueViolation(info.ConstraintName);
            if (mapped is not null)
                return Result<PurchaseInvoiceDto>.Conflict(mapped);
            throw;
        }
        return Result<PurchaseInvoiceDto>.Success(PurchaseMapper.ToDto(inv));
    }

    /// <summary>Un LineId solo puede conservar una línea existente de este borrador, una sola vez.</summary>
    private static string? ValidateKeptLineIds(IReadOnlyList<PurchaseLineInput> inputs, PurchaseInvoice inv)
    {
        var kept = inputs.Where(l => l.LineId.HasValue).Select(l => l.LineId!.Value).ToList();
        if (kept.Distinct().Count() != kept.Count)
            return "Una línea de la compra se envió más de una vez.";
        return kept.Exists(id => inv.Lines.All(x => x.Id != id))
            ? "Una línea enviada no pertenece a esta compra."
            : null;
    }

    /// <summary>
    /// Cuerpo del Update ejecutado DENTRO de la transacción. Devuelve null si todo es válido (el
    /// UoW hace el único SaveChangesAsync + Commit); un Result de rechazo fuerza el rollback.
    /// </summary>
    private async Task<Result<PurchaseInvoiceDto>?> ApplyUpdateAsync(
        UpdatePurchaseDraftCommand cmd,
        PurchaseInvoice inv,
        ERP.Domain.MasterData.Entities.BusinessPartner supplier,
        CancellationToken ct
    )
    {
        // FASE 5 PURCHASE-UX — snapshot de origen XML ya persistido por línea de recepción: si el
        // nivel no cambia, se conserva su factor (no se recalcula desde el maestro en cada guardado).
        var previousXmlOrigins = inv.Lines
            .Where(x => x.PurchaseReceptionLineId.HasValue
                && x.XmlOriginPackagingLevelId.HasValue
                && x.XmlOriginFactor.HasValue)
            .GroupBy(x => x.PurchaseReceptionLineId!.Value)
            .ToDictionary(
                g => g.Key,
                g => (g.First().XmlOriginPackagingLevelId!.Value, g.First().XmlOriginFactor!.Value)
            );

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

        // PURCHASE-DRAFT-RECOVERY F2 — las cuotas manuales guardadas se conservan tal cual al cambiar
        // la condición de pago (nunca se regeneran solas); Confirm exige que cuadren.
        void ChangePaymentTerm(ERP.Domain.MasterData.Entities.PaymentTerm pt) =>
            inv.UpdatePaymentTermSnapshot(
                pt.Id,
                pt.Name,
                pt.Installments,
                pt.DaysBetweenInstallments
            );

        if (cmd.PaymentTermId.HasValue && cmd.PaymentTermId.Value != inv.PaymentTermId)
        {
            var ptResult = await _ptResolver.ResolveForPurchaseAsync(
                cmd.SupplierId,
                cmd.PaymentTermId,
                ct
            );
            if (!ptResult.IsSuccess)
                return Result<PurchaseInvoiceDto>.ValidationFailure(ptResult.Error!);
            ChangePaymentTerm(ptResult.Value!);
        }
        else if (cmd.SupplierId != inv.SupplierId)
        {
            // Cambio de proveedor sin PaymentTermId explícito: intenta el default de la empresa
            // activa para el nuevo proveedor (ADR-033, Fase 3b). Si no hay uno válido, se
            // mantiene el PaymentTerm actual del borrador (mismo criterio ya vigente desde
            // Fase 2) — el usuario deberá elegir uno explícito si lo necesita.
            var ptResult = await _ptResolver.ResolveForPurchaseAsync(cmd.SupplierId, null, ct);
            if (ptResult.IsSuccess)
                ChangePaymentTerm(ptResult.Value!);
        }

        if (cmd.GlobalWarehouseId.HasValue)
        {
            var whCheck = await WarehouseBranchGuard.ValidateAsync(
                _whRepo,
                _t.TenantId,
                cmd.GlobalWarehouseId.Value,
                inv.BranchId,
                ct
            );
            if (whCheck.Error is not null)
                return whCheck.Error;
        }

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
        var lineIdError = ValidateKeptLineIds(cmd.Lines, inv);
        if (lineIdError is not null)
            return Result<PurchaseInvoiceDto>.ValidationFailure(lineIdError);
        // ERP-PRECISION-OPERATIONAL-05B: cantidad/costo unitario/factor según la política de la empresa.
        var precision = await _precision.GetEffectiveAsync(ct);
        foreach (var l in cmd.Lines)
        {
            var vatCode = l.VatCode;
            var iceCode = l.IceCode;
            string? snapshotSku = null;
            string? snapshotItemName = null;
            string? snapshotSupplierCode = null;
            var packaging = new PurchaseLinePackagingSnapshot(null, "UNIT", "UNIT", 1m);
            var participatesInInventory = false;
            Item? lineItem = null;

            if (l.ItemId.HasValue)
            {
                var item = await _itemRepo.GetByIdAsync(l.ItemId.Value, _t.TenantId, ct);
                if (item is null || item.CompanyId != inv.CompanyId)
                    return Result<PurchaseInvoiceDto>.ValidationFailure("El ítem no pertenece a esta empresa.");
                participatesInInventory = item.ParticipatesInInventory;
                lineItem = item;
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
                    packaging = packaging with
                    {
                        ConversionFactor = Math.Round(
                            packaging.ConversionFactor,
                            precision.ConversionFactorDecimals,
                            MidpointRounding.AwayFromZero
                        ),
                    };
                    if (packaging.ConversionFactor <= 0m)
                        return (
                            Result<PurchaseInvoiceDto>.ValidationFailure(
                                $"Línea '{l.Description}': el factor de conversión de la presentación no es representable con {precision.ConversionFactorDecimals} decimales."
                            )
                        );

                    if (string.IsNullOrWhiteSpace(vatCode))
                        vatCode = item.TaxConfig.PurchaseVatCode ?? vatCode;
                    // TAX-LINE-SSOT-ICE-IRBPNR-01 (ADR-032 §3.2/Fase 3) — ICE se resuelve desde
                    // ItemSpecialTaxConfiguration, no desde TaxConfig.ExciseTaxCode (legacy
                    // compatibility mirror, ya no se lee para decisiones nuevas).
                    if (string.IsNullOrWhiteSpace(iceCode))
                        iceCode = item
                            .SpecialTaxConfigurations.FirstOrDefault(c =>
                                c.IsActive && c.SriTaxCategoryCode == SriTaxCategoryCodes.Ice
                            )
                            ?.TaxCatalogCode;
                }
            }

            if (string.IsNullOrWhiteSpace(vatCode))
                return Result<PurchaseInvoiceDto>.ValidationFailure(
                    $"Línea '{l.Description}': código IVA obligatorio."
                );

            string? snapshotWhCode = null;
            var whId = participatesInInventory ? l.WarehouseId ?? cmd.GlobalWarehouseId : null;
            if (whId.HasValue)
            {
                var whCheck = await WarehouseBranchGuard.ValidateAsync(
                    _whRepo,
                    _t.TenantId,
                    whId.Value,
                    inv.BranchId,
                    ct
                );
                if (whCheck.Error is not null)
                    return whCheck.Error;
                snapshotWhCode = whCheck.Warehouse?.Code;
            }

            var normalizedLine = await PurchaseLineNormalizer.NormalizeAsync(
                l,
                precision,
                _receptionRepo,
                _t.TenantId,
                ct
            );

            var line = PurchaseInvoiceDetail.Create(
                inv.Id,
                _t.TenantId,
                l.Description,
                l.Quantity,
                normalizedLine.UnitPrice,
                vatCode,
                packaging.UomCode,
                l.ItemId,
                participatesInInventory ? l.WarehouseId : null,
                l.Notes,
                normalizedLine.DiscountPct,
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
                packagingLevelId: packaging.PackagingLevelId,
                quantityDecimals: precision.QuantityDecimals,
                unitCostDecimals: precision.UnitCostDecimals,
                exactDiscountAmount: normalizedLine.ExactDiscountAmount,
                id: l.LineId
            );
            var xmlOrigin = PurchaseXmlOriginPackagingResolver.Resolve(
                lineItem,
                l,
                precision.ConversionFactorDecimals,
                l.PurchaseReceptionLineId is { } receptionLineId
                && previousXmlOrigins.TryGetValue(receptionLineId, out var previousOrigin)
                    ? previousOrigin
                    : null
            );
            if (xmlOrigin.Error is not null)
                return Result<PurchaseInvoiceDto>.ValidationFailure(xmlOrigin.Error);
            line.SetXmlOrigin(xmlOrigin.LevelId, xmlOrigin.Factor);

            Result<PurchaseInvoiceDto>? taxResult = null;
            if (l.XmlAllocation is null && l.PurchaseReceptionLineId.HasValue)
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
                        normalizedLine.MatchesXmlDocument,
                        _tax,
                        ct
                    );
                else
                    taxResult = await PurchaseTaxHelper.ResolveTaxesAsync(line, _tax, ct);
            }
            else if (l.XmlAllocation is null)
            {
                taxResult = await PurchaseTaxHelper.ResolveTaxesAsync(line, _tax, ct);
            }
            if (taxResult is not null)
                return taxResult;
            if (l.XmlAllocation is not null)
            {
                var allocationError = await PurchaseXmlBreakdown.ApplyAsync(line, l.XmlAllocation,
                    _receptionRepo, _tax, ct);
                if (allocationError is not null)
                    return Result<PurchaseInvoiceDto>.ValidationFailure(allocationError);
            }
            lines.Add(line);
        }
        var breakdownError = await PurchaseXmlBreakdown.ValidateAsync(inv, lines, _receptionRepo, ct);
        if (breakdownError is not null)
            return Result<PurchaseInvoiceDto>.ValidationFailure(breakdownError);
        await _repo.RemoveLinesByInvoiceAsync(inv.Id, lines, ct);
        // C3: ReplaceLines rechaza quitar/desglosar líneas con distribuciones (rollback completo).
        inv.ReplaceLines(lines, _u.UserId);
        _repo.ReattachCostDistributionsAsAdded(inv);
        if (PurchaseDraftSchedule.Apply(inv, cmd.Schedule))
        {
            await _repo.ClearScheduleTrackingAsync(inv.Id, ct);
            _repo.ReattachSchedulesAsAdded(inv);
        }
        PurchaseDraftSchedule.ApplyRetentionIntent(inv, cmd.RetentionIntent);
        return null;
    }
}

/// <summary>
/// Canal interno para devolver un Result de rechazo desde dentro de
/// <see cref="IUnitOfWork.ExecuteInTransactionAsync"/> (delegate sin valor de retorno): lanzarla
/// fuerza el rollback completo y el handler la traduce al mismo Result. Mismo patrón que
/// UpdateSalesDraftHandler (A3).
/// </summary>
file sealed class PurchaseDraftRejectionException(Result<PurchaseInvoiceDto> rejection)
    : Exception(rejection.Error)
{
    public Result<PurchaseInvoiceDto> Rejection { get; } = rejection;
}

internal static class PurchaseTaxHelper
{
    public static async Task<Result<PurchaseInvoiceDto>?> ResolveTaxesAsync(
        PurchaseInvoiceDetail line,
        ISriTaxResolver tax,
        CancellationToken ct
    )
    {
        if (line.IsXmlBreakdown)
            return null; // Documentary allocations are validated by PurchaseXmlBreakdown.
        var vatResult = await tax.GetVatRateWithNameAsync(line.VatCode, ct);
        if (vatResult is null)
            return Result<PurchaseInvoiceDto>.ValidationFailure(
                $"Código IVA '{line.VatCode}' no encontrado o inactivo."
            );

        var xmlIce = line.Taxes.FirstOrDefault(t => t.TaxCode == SriTaxCategoryCodes.Ice && t.Source == PurchaseTaxSource.Xml);
        if (xmlIce is not null)
        {
            var entry = await tax.GetIceCatalogEntryAsync(xmlIce.TaxRateCode, ct)
                ?? await tax.GetXmlIceCatalogEntryAsync(xmlIce.TaxRateCode, ct);
            if (entry is null)
                return Result<PurchaseInvoiceDto>.ValidationFailure(
                    $"Línea '{line.Description}': código ICE {xmlIce.TaxRateCode} ausente del catálogo GLOBAL.");
            line.ApplyTaxes(line.VatCode, vatResult.Rate, vatResult.Name, xmlIce.TaxRateCode,
                xmlIce.Rate ?? 0m, xmlIce.TaxName, xmlIce.CalculationType, xmlIce.TaxAmount);
            return null;
        }

        decimal iceRate = 0;
        string? iceName = null;
        var iceCalculationType = SriTaxCalculationType.Percentage;
        decimal? iceExactAmount = null;
        if (!string.IsNullOrWhiteSpace(line.IceCode))
        {
            // FLOW-READY-02F.1 — catalog-aware (no el legacy GetIceRateWithNameAsync, que exige
            // Percentage y por eso nunca resuelve ICE "específico" como el código 3053; para un
            // código Specific ese método devuelve null y bloquea la línea con un falso "no encontrado").
            var iceEntry = await tax.GetIceCatalogEntryAsync(line.IceCode, ct);
            if (iceEntry is null)
                return Result<PurchaseInvoiceDto>.ValidationFailure(
                    $"Código ICE '{line.IceCode}' no encontrado o inactivo."
                );
            iceName = iceEntry.Name;
            iceCalculationType = iceEntry.CalculationType;
            if (iceEntry.CalculationType != SriTaxCalculationType.Percentage)
                return Result<PurchaseInvoiceDto>.ValidationFailure(
                    $"Línea '{line.Description}': ICE {line.IceCode} requiere tarifa y base física verificadas. Conserve el importe declarado en el XML SRI.",
                    "PURCHASE_ICE_DATA_REQUIRED");
            if (!iceEntry.Percentage.HasValue)
                return Result<PurchaseInvoiceDto>.ValidationFailure(
                    $"Línea '{line.Description}': falta la tarifa ICE {line.IceCode}.",
                    "PURCHASE_ICE_DATA_REQUIRED");
            iceRate = iceEntry.Percentage.Value;
        }

        line.ApplyTaxes(
            line.VatCode,
            vatResult.Rate,
            vatResult.Name,
            line.IceCode,
            iceRate,
            iceName,
            iceCalculationType,
            iceExactAmount
        );
        return null;
    }
}

/// <summary>
/// FLOW-READY-02F.1 — resuelve el detalle fiel de impuestos (IVA/ICE/IRBPNR, y cualquier código
/// SRI no reconocido) capturado desde el XML de Recepción Electrónica, contra los catálogos SRI
/// globales. Solo se usa cuando la línea viene de Recepción y esa línea sí tiene taxes capturadas
/// (<see cref="ReceptionTaxLookup"/>) — si no, el caller cae de vuelta a
/// <see cref="PurchaseTaxHelper.ResolveTaxesAsync"/> (líneas manuales o recepciones anteriores a esta fase).
/// </summary>
internal static class ReceptionTaxHelper
{
    private const string SriVatTaxCode = SriTaxCategoryCodes.Vat;
    private const string SriIceTaxCode = SriTaxCategoryCodes.Ice;
    private const string SriIrbpnrTaxCode = SriTaxCategoryCodes.Irbpnr;

    public static async Task<Result<PurchaseInvoiceDto>?> ApplyReceptionTaxesAsync(
        PurchaseInvoiceDetail line,
        IReadOnlyList<PurchaseReceptionLineTax> receptionTaxes,
        bool matchesXmlDocument,
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
                        t.Tarifa,
                        SriTaxCalculationType.Percentage,
                        t.TaxableBase,
                        t.TaxAmount,
                        PurchaseTaxSource.Xml
                    )
                );
                continue;
            }

            var activeIce = t.TaxCode == SriIceTaxCode
                ? await tax.GetIceCatalogEntryAsync(t.TaxRateCode, ct)
                : null;
            var entry =
                t.TaxCode == SriVatTaxCode ? await tax.GetVatCatalogEntryAsync(t.TaxRateCode, ct)
                : t.TaxCode == SriIceTaxCode ? (activeIce
                    ?? await tax.GetXmlIceCatalogEntryAsync(t.TaxRateCode, ct))
                : await tax.GetIrbpnrCatalogEntryAsync(t.TaxRateCode, ct);

            if (entry is null)
                return Result<PurchaseInvoiceDto>.ValidationFailure(
                    $"El XML contiene impuesto {label} {t.TaxRateCode}, pero ese código no está "
                        + "cargado o activo en el catálogo SRI global."
                );

            // Operational edits preserve fiscal amounts and tax classifications from the XML.
            var source = PurchaseTaxSource.Xml;
            built.Add(
                PurchaseInvoiceDetailTax.Create(
                    line.Id,
                    line.TenantId,
                    t.TaxCode,
                    t.TaxRateCode,
                    entry.Name,
                    t.Tarifa,
                    entry.CalculationType,
                    t.TaxCode == SriVatTaxCode && !matchesXmlDocument
                        ? line.OperationalCostBase + receptionTaxes.Where(x => x.TaxCode == SriIceTaxCode).Sum(x => x.TaxAmount)
                        : t.TaxableBase,
                    t.TaxAmount,
                    source
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

        line.ReplaceTaxes(built);
        line.ApplyTaxes(
            vatCode,
            vatRate,
            vatName,
            iceCode,
            iceRate,
            iceName,
            iceCalcType,
            iceExactAmount
        );
        return null;
    }
}

/// <summary>
/// ERP-CORE-CLOSEOUT-05-FIX02 (P1-1) — igual criterio que GetSalesInvoiceByIdHandler
/// (ERP-CORE-CLOSEOUT-05-FIX01): la lectura de un documento puntual por GUID sí se restringe a la
/// sucursal activa del llamante (a diferencia de GetPurchaseListQuery, cuyo alcance company-wide es
/// una decisión de negocio ya documentada y sin cambios en este fix — ver comentario de esa query).
/// </summary>
public sealed class GetPurchaseByIdHandler
    : IRequestHandler<GetPurchaseByIdQuery, Result<PurchaseInvoiceDto>>
{
    private readonly IPurchaseInvoiceRepository _repo;
    private readonly IPurchaseReceptionDocumentRepository _receptionRepo;
    private readonly IItemRepository _itemRepo;
    private readonly ICurrentTenant _t;
    private readonly ICurrentBranch _b;

    public GetPurchaseByIdHandler(
        IPurchaseInvoiceRepository repo,
        IPurchaseReceptionDocumentRepository receptionRepo,
        IItemRepository itemRepo,
        ICurrentTenant t,
        ICurrentBranch b
    )
    {
        _repo = repo;
        _receptionRepo = receptionRepo;
        _itemRepo = itemRepo;
        _t = t;
        _b = b;
    }

    public async Task<Result<PurchaseInvoiceDto>> Handle(
        GetPurchaseByIdQuery q,
        CancellationToken ct
    )
    {
        var inv = await _repo.GetByIdAsync(_t.TenantId, q.Id, ct);
        if (inv is null || inv.BranchId != _b.BranchId)
            return Result<PurchaseInvoiceDto>.NotFound("Compra no encontrada.");

        var xmlSources = await LoadXmlSourcesAsync(inv, ct);
        return Result<PurchaseInvoiceDto>.Success(PurchaseMapper.ToDto(inv, xmlSources));
    }

    /// <summary>
    /// PURCHASE-DRAFT-RECOVERY F1/D1 — evidencia original de cada línea de recepción vinculada al
    /// borrador, leída de la PurchaseReceptionLine PERSISTIDA: es la misma que el backend usa al
    /// guardar (PurchaseLineNormalizer/ReceptionTaxLookup) para decidir si la línea sigue igual al
    /// XML, así frontend y backend reconocen exactamente los mismos valores. Incluye el Item Matching
    /// vigente de la recepción y la presentación aprendida, para que la UI recupere un match hecho
    /// después de guardar (nunca sobrescribe lo ya persistido en la compra — eso lo decide la UI).
    /// Solo documentos de la misma empresa que la compra.
    /// </summary>
    private async Task<IReadOnlyDictionary<Guid, PurchaseDraftLineDto>> LoadXmlSourcesAsync(
        PurchaseInvoice inv,
        CancellationToken ct
    )
    {
        var sources = new Dictionary<Guid, PurchaseDraftLineDto>();
        var pending = inv
            .Lines.Where(l => l.PurchaseReceptionLineId.HasValue)
            .Select(l => l.PurchaseReceptionLineId!.Value)
            .ToHashSet();

        while (pending.Count > 0)
        {
            var lineId = pending.First();
            pending.Remove(lineId);
            var document = await _receptionRepo.GetByLineIdWithEvidenceAsync(
                _t.TenantId,
                lineId,
                ct
            );
            if (document is null || document.CompanyId != inv.CompanyId)
                continue;

            var packagingByLine = await PurchaseDraftPackagingResolver.ResolveAsync(
                _itemRepo,
                document,
                ct
            );
            foreach (var line in document.Lines)
            {
                sources[line.Id] = PurchaseDraftMapper.ToLineDto(
                    PurchaseDraftMergedLine.FromPersisted(line),
                    packagingByLine.TryGetValue(line.Id, out var packaging)
                        ? packaging
                        : PurchaseDraftLinePackagingSnapshot.Base()
                );
                pending.Remove(line.Id);
            }
        }

        return sources;
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
            q.SupplierId,
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
                i.CreatedAt,
                i.SupplierName,
                i.SupplierTaxId,
                i.GrandTotal
            ))
            .ToList();
        return Result<PurchaseListResponse>.Success(
            new PurchaseListResponse(dtos, total, q.PageNumber, q.PageSize)
        );
    }
}

/// <summary>
/// ERP-PRECISION-OPERATIONAL-05B1/05B2 — punto único de normalización del precio unitario OPERATIVO de
/// una línea de compra a <c>purchaseUnitPriceDecimals</c> (Create y Update de borrador).
/// - Sin <c>PurchaseReceptionLineId</c> (captura manual): se normaliza.
/// - Con <c>PurchaseReceptionLineId</c>: si el precio recibido es EXACTAMENTE el de la línea de
///   recepción (snapshot fuente inmutable del XML SRI) se conserva tal cual; si fue modificado (o la
///   línea de recepción ya no se puede resolver) es un valor operativo y se normaliza. El redondeo es
///   idempotente: un precio editado que ya cumple la escala no cambia.
/// La línea de recepción y el parser XML nunca se alteran.
/// </summary>
file sealed record PurchaseLineNormalization(
    decimal UnitPrice,
    decimal DiscountPct,
    decimal? ExactDiscountAmount,
    // Exact documentary discount can be retained when its operating inputs are unchanged.
    bool MatchesXmlDocument = false
);

file static class PurchaseLineNormalizer
{
    public static async Task<PurchaseLineNormalization> NormalizeAsync(
        PurchaseLineInput line,
        EffectivePrecisionPolicyDto precision,
        IPurchaseReceptionDocumentRepository receptionRepo,
        Guid tenantId,
        CancellationToken ct
    )
    {
        PurchaseReceptionLine? original = null;

        if (line.XmlAllocation is null && line.PurchaseReceptionLineId is { } receptionLineId)
        {
            var document = await receptionRepo.GetByLineIdAsync(tenantId, receptionLineId, ct);
            original = document?.Lines.FirstOrDefault(l => l.Id == receptionLineId);
        }

        var unitPrice =
            original is not null && original.UnitPrice == line.UnitPrice
                ? line.UnitPrice
                : Math.Round(
                    line.UnitPrice,
                    precision.PurchaseUnitPriceDecimals,
                    MidpointRounding.AwayFromZero
                );

        var xmlDiscountUnchanged =
            original is not null
            && original.Quantity == line.Quantity
            && original.UnitPrice == line.UnitPrice
            && original.DiscountPct == line.DiscountPct;

        if (xmlDiscountUnchanged)
            return new(unitPrice, line.DiscountPct, original!.Discount, MatchesXmlDocument: true);

        return new(
            unitPrice,
            Math.Round(
                line.DiscountPct,
                precision.PercentageDecimals,
                MidpointRounding.AwayFromZero
            ),
            null
        );
    }
}

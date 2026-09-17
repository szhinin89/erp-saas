using ERP.Application.Common;
using ERP.Application.Common.Interfaces;
using ERP.Application.Common.Services;
using ERP.Application.Modules.Pricing.DTOs;
using ERP.Application.Modules.Pricing.Services;
using ERP.Application.Modules.Sales.DTOs;
using ERP.Domain.Common;
using ERP.Domain.Configuration.Interfaces;
using ERP.Domain.MasterData.Interfaces;
using ERP.Domain.Modules.Company.Enums;
using ERP.Domain.Modules.Company.Interfaces;
using ERP.Domain.Modules.Inventory.Interfaces;
using ERP.Domain.Modules.Items.Entities;
using ERP.Domain.Modules.Items.Interfaces;
using ERP.Domain.Modules.Purchases;
using ERP.Domain.Modules.Sales.Entities;
using ERP.Domain.Modules.Sales.Enums;
using ERP.Domain.Modules.Sales.Interfaces;
using ERP.Domain.Modules.Sales.ValueObjects;
using ERP.Domain.Modules.SriCatalogs.Enums;
using FluentValidation;
using MediatR;

namespace ERP.Application.Modules.Sales.UseCases;

// ── Shared input ────────────────────────────────────────────────────────

public sealed record SalesLineInput(
    Guid? ItemId,
    string Description,
    decimal Quantity,
    decimal UnitPrice,
    string VatCode,
    string? Notes = null,
    decimal DiscountPct = 0,
    string? IceCode = null,
    Guid? WarehouseId = null,
    // SALES-PRESENTATIONS-02: null = venta en unidad base (comportamiento actual preservado,
    // factor 1). Informado = venta por presentación (ej. caja x12) — resuelto contra
    // Item.PackagingLevels por SalesLinePackagingResolver, nunca confiado tal cual del cliente.
    Guid? PackagingLevelId = null
);

// ── Commands & Queries ──────────────────────────────────────────────────

public sealed record SalesPaymentInput(
    Guid PaymentMethodId,
    decimal Amount,
    string? Reference = null,
    CardDetailInput? CardDetail = null,
    TransferDetailInput? TransferDetail = null,
    ChequeDetailInput? ChequeDetail = null
);

public sealed record CardDetailInput(
    string? CardBrand = null,
    string? CardLastFour = null,
    string? BankName = null,
    string? AuthorizationCode = null,
    string? LotNumber = null
);

/// <summary>
/// SALES-TRANSFER-BANK-ACCOUNT-01: <see cref="CompanyBankAccountId"/> reemplaza el texto libre
/// <c>BankName</c> como fuente de verdad — obligatorio para todo pago cuyo método sea
/// Transferencia (validado en <see cref="SalesPaymentHelper.BuildPaymentsAsync"/>, nunca confiado
/// tal cual del cliente).
/// </summary>
public sealed record TransferDetailInput(
    Guid? CompanyBankAccountId = null,
    string? ReceiptNumber = null,
    string? TransferDate = null
);

public sealed record ChequeDetailInput(
    string? BankName = null,
    string? ChequeNumber = null,
    string? HolderName = null,
    string? CashDate = null
);

/// <summary>
/// El cliente nunca envía EmissionPointId ni CashSessionId (ADR — Rediseño del módulo de Caja,
/// Fase 4): el servidor los resuelve exclusivamente desde <c>ICurrentCashSession</c>. Si el
/// usuario no tiene una caja abierta, la creación del borrador se rechaza.
/// </summary>
/// <summary>ADR-033, Fase 4 — una cuota del cronograma enviada explícitamente por el usuario.</summary>
public sealed record SalesScheduleInput(
    int InstallmentNumber,
    DateOnly DueDate,
    decimal Amount,
    string? Notes = null
);

public sealed record CreateSalesDraftCommand(
    Guid CustomerId,
    DateOnly IssueDate,
    List<SalesLineInput> Lines,
    DateOnly? DueDate = null,
    string? Notes = null,
    Guid? PaymentTermId = null,
    List<SalesPaymentInput>? Payments = null,
    string? DocTypeCode = null,
    string? SriPaymentMethodCode = null,
    List<SalesScheduleInput>? Schedule = null
) : IRequest<Result<SalesInvoiceDto>>, IBranchScopedRequest;

public sealed record UpdateSalesDraftCommand(
    Guid Id,
    Guid CustomerId,
    DateOnly IssueDate,
    List<SalesLineInput> Lines,
    DateOnly? DueDate = null,
    string? Notes = null,
    Guid? PaymentTermId = null,
    List<SalesPaymentInput>? Payments = null,
    List<SalesScheduleInput>? Schedule = null
) : IRequest<Result<SalesInvoiceDto>>, IBranchScopedRequest;

public sealed record GetSalesInvoiceByIdQuery(Guid Id)
    : IRequest<Result<SalesInvoiceDto>>,
        IBranchScopedRequest;

/// <summary>
/// Fase I-6B: branch-scoped por exigencia de contexto (defensa en profundidad vía
/// BranchScopeBehavior/IBranchAccessGuard) — SalesInvoice no tiene BranchId de cabecera, así
/// que esto no filtra resultados por sucursal, solo exige que el usuario opere con una
/// sucursal activa autorizada, igual que el resto de UseCases de Sales ya migrados.
/// </summary>
public sealed record GetSalesInvoiceListQuery(
    string? Search = null,
    string? Status = null,
    int PageNumber = 1,
    int PageSize = 25
) : IRequest<Result<SalesListResponse>>, IBranchScopedRequest;

public sealed record SalesListResponse(
    IReadOnlyList<SalesListDto> Items,
    int Total,
    int Page,
    int PageSize
);

// ── Validators ──────────────────────────────────────────────────────────

public sealed class CreateSalesDraftValidator : AbstractValidator<CreateSalesDraftCommand>
{
    /// <summary>
    /// CLEAN-01C: <c>DocTypeCode</c> se persiste en <c>SalesInvoice</c> sin FK (a diferencia de
    /// <c>DocumentSequence</c>, que sí referencia <c>SriDocType</c>) — sin esta regla, un cliente
    /// podía enviar cualquier string de hasta 5 caracteres y quedaba guardado sin validar contra el
    /// catálogo fiscal real. Mismo patrón <c>MustAsync</c> que
    /// <c>UpdateCustomerRoleConfigValidator</c> (CLASS-BP-CATALOGS-01).
    /// </summary>
    public CreateSalesDraftValidator(ISriDocTypeCatalogResolver docTypeCatalogResolver)
    {
        RuleFor(x => x.CustomerId).NotEmpty().WithMessage("El cliente es obligatorio.");
        RuleFor(x => x.IssueDate).NotEmpty();
        RuleFor(x => x.DocTypeCode)
            .MustAsync((code, ct) => docTypeCatalogResolver.IsActiveElectronicDocTypeAsync(code!, ct))
            .WithMessage("El tipo de comprobante no corresponde a un código SRI activo.")
            .When(x => !string.IsNullOrWhiteSpace(x.DocTypeCode));
        RuleFor(x => x.Lines).NotEmpty().WithMessage("Debe incluir al menos una línea.");
        RuleForEach(x => x.Lines)
            .ChildRules(line =>
            {
                line.RuleFor(l => l.Description)
                    .NotEmpty()
                    .MaximumLength(SalesInvoiceDetail.DescriptionMaxLen);
                line.RuleFor(l => l.Quantity)
                    .GreaterThan(0)
                    .WithMessage("La cantidad debe ser mayor a cero.");
                line.RuleFor(l => l.UnitPrice)
                    .GreaterThanOrEqualTo(0)
                    .WithMessage("El precio no puede ser negativo.");
                line.RuleFor(l => l.VatCode)
                    .NotEmpty()
                    .WithMessage("El código IVA es obligatorio por línea.");
                line.RuleFor(l => l.DiscountPct).InclusiveBetween(0, 100);
            });
    }
}

public sealed class UpdateSalesDraftValidator : AbstractValidator<UpdateSalesDraftCommand>
{
    public UpdateSalesDraftValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.CustomerId).NotEmpty().WithMessage("El cliente es obligatorio.");
        RuleFor(x => x.IssueDate).NotEmpty();
        RuleFor(x => x.Lines).NotEmpty().WithMessage("Debe incluir al menos una línea.");
        RuleForEach(x => x.Lines)
            .ChildRules(line =>
            {
                line.RuleFor(l => l.Description)
                    .NotEmpty()
                    .MaximumLength(SalesInvoiceDetail.DescriptionMaxLen);
                line.RuleFor(l => l.Quantity).GreaterThan(0);
                line.RuleFor(l => l.UnitPrice).GreaterThanOrEqualTo(0);
                line.RuleFor(l => l.VatCode)
                    .NotEmpty()
                    .WithMessage("El código IVA es obligatorio por línea.");
                line.RuleFor(l => l.DiscountPct).InclusiveBetween(0, 100);
            });
    }
}

// ── Handlers ────────────────────────────────────────────────────────────

/// <summary>
/// SALES-INVOICE-FINAL-SEMANTIC-INTEGRITY-01 (Fase 6E) — resuelve Email/Address reales del
/// cliente para poblar <see cref="CustomerSnapshot"/> completo (antes solo se invocaba
/// CustomerSnapshot.Create con 3 argumentos posicionales, dejando Email/Address siempre NULL
/// aunque las columnas y el value object ya los soportaban). BusinessPartner no contiene estos
/// datos directamente (ver comentario de scope en BusinessPartner.cs): Email vive en
/// BusinessPartnerContact.Contact.Email (contacto primario) y Address en
/// BusinessPartnerLocation.Address.AddressLine (ubicación primaria) — con fallback al Email de
/// la ubicación primaria si no hay contacto primario con email. Nunca bloquea la creación de la
/// factura si no existen: ambos quedan null.
/// </summary>
internal static class CustomerSnapshotContactResolver
{
    public static async Task<(string? Email, string? Address)> ResolveAsync(
        IBusinessPartnerContactRepository contactRepo,
        IBusinessPartnerLocationRepository locationRepo,
        Guid businessPartnerId,
        CancellationToken ct
    )
    {
        string? email = null;
        string? address = null;

        var contacts = await contactRepo.GetByBusinessPartnerAsync(businessPartnerId, true, ct);
        var primaryContact =
            contacts.FirstOrDefault(c => c.IsPrimary) ?? contacts.FirstOrDefault();
        email = primaryContact?.Contact.Email;

        var locations = await locationRepo.GetByBusinessPartnerAsync(businessPartnerId, true, ct);
        var primaryLocation =
            locations.FirstOrDefault(l => l.IsPrimary) ?? locations.FirstOrDefault();
        address = primaryLocation?.Address.AddressLine;
        if (string.IsNullOrWhiteSpace(email))
            email = primaryLocation?.Email;

        return (email, address);
    }
}

public sealed class CreateSalesDraftHandler
    : IRequestHandler<CreateSalesDraftCommand, Result<SalesInvoiceDto>>
{
    private readonly ISalesInvoiceRepository _repo;
    private readonly IBusinessPartnerRepository _bpRepo;
    private readonly IBusinessPartnerRoleRepository _roleRepo;
    private readonly IBusinessPartnerContactRepository _bpContactRepo;
    private readonly IBusinessPartnerLocationRepository _bpLocationRepo;
    private readonly ERP.Application.MasterData.Services.IPaymentTermDefaultResolver _ptResolver;
    private readonly IPaymentMethodRepository _pmRepo;
    private readonly IItemRepository _itemRepo;
    private readonly IEmissionPointRepository _epRepo;
    private readonly ISriTaxResolver _tax;
    private readonly IPricingResolver _pricing;
    private readonly ERP.Domain.Modules.Company.Interfaces.ICompanySpecialTaxResponsibilityRepository _companyTaxRepo;
    private readonly IWarehouseRepository _warehouseRepo;
    private readonly IAverageCostService _costService;
    private readonly ICurrentTenant _t;
    private readonly ICurrentCompany _c;
    private readonly ICurrentBranch _b;
    private readonly ICurrentUser _u;
    private readonly ICurrentCashSession _cashSession;
    private readonly IOperationalPreferencesResolver _preferences;
    private readonly ERP.Application.Modules.Sales.Services.ISalesCreditRequirementPolicy _creditPolicy;
    private readonly ERP.Domain.Modules.Finance.Interfaces.ICompanyBankAccountRepository _bankAccountRepo;

    public CreateSalesDraftHandler(
        ISalesInvoiceRepository repo,
        IBusinessPartnerRepository bpRepo,
        IBusinessPartnerRoleRepository roleRepo,
        IBusinessPartnerContactRepository bpContactRepo,
        IBusinessPartnerLocationRepository bpLocationRepo,
        ERP.Application.MasterData.Services.IPaymentTermDefaultResolver ptResolver,
        IPaymentMethodRepository pmRepo,
        IItemRepository itemRepo,
        IEmissionPointRepository epRepo,
        ISriTaxResolver tax,
        IPricingResolver pricing,
        ERP.Domain.Modules.Company.Interfaces.ICompanySpecialTaxResponsibilityRepository companyTaxRepo,
        IWarehouseRepository warehouseRepo,
        IAverageCostService costService,
        ICurrentTenant t,
        ICurrentCompany c,
        ICurrentBranch b,
        ICurrentUser u,
        ICurrentCashSession cashSession,
        IOperationalPreferencesResolver preferences,
        ERP.Application.Modules.Sales.Services.ISalesCreditRequirementPolicy creditPolicy,
        ERP.Domain.Modules.Finance.Interfaces.ICompanyBankAccountRepository bankAccountRepo
    )
    {
        _repo = repo;
        _bpRepo = bpRepo;
        _roleRepo = roleRepo;
        _bpContactRepo = bpContactRepo;
        _bpLocationRepo = bpLocationRepo;
        _ptResolver = ptResolver;
        _pmRepo = pmRepo;
        _itemRepo = itemRepo;
        _epRepo = epRepo;
        _tax = tax;
        _pricing = pricing;
        _companyTaxRepo = companyTaxRepo;
        _warehouseRepo = warehouseRepo;
        _costService = costService;
        _t = t;
        _c = c;
        _b = b;
        _u = u;
        _cashSession = cashSession;
        _preferences = preferences;
        _creditPolicy = creditPolicy;
        _bankAccountRepo = bankAccountRepo;
    }

    public async Task<Result<SalesInvoiceDto>> Handle(
        CreateSalesDraftCommand cmd,
        CancellationToken ct
    )
    {
        if (!_cashSession.HasOpenSession)
            return Result<SalesInvoiceDto>.ValidationFailure(
                "No existe una caja abierta para realizar ventas."
            );

        var bp = await _bpRepo.GetByIdAsync(cmd.CustomerId, ct);
        if (bp is null)
            return Result<SalesInvoiceDto>.NotFound("Cliente no encontrado.");
        if (!bp.IsActive)
            return Result<SalesInvoiceDto>.ValidationFailure("El cliente se encuentra inactivo.");

        var customerRole = await _roleRepo.GetByTypeAsync(
            cmd.CustomerId,
            Domain.MasterData.Enums.RoleType.Customer,
            ct
        );
        if (customerRole is null)
            return Result<SalesInvoiceDto>.ValidationFailure(
                "El socio de negocio no tiene rol de Cliente."
            );

        var tid = _t.TenantId;

        // ── PaymentTermId explícito (SALES-SETTLEMENT-CREDIT-01) ─────────
        // Validado SIEMPRE que venga informado, sin importar si queda saldo pendiente — una
        // condición de pago explícita inválida/inactiva nunca se acepta silenciosamente.
        Domain.MasterData.Entities.PaymentTerm? explicitPt = null;
        if (cmd.PaymentTermId.HasValue)
        {
            var explicitResult = await _ptResolver.ResolveForSaleAsync(
                cmd.CustomerId,
                cmd.PaymentTermId,
                ct
            );
            if (!explicitResult.IsSuccess)
                return Result<SalesInvoiceDto>.ValidationFailure(explicitResult.Error!);
            explicitPt = explicitResult.Value;
        }

        // Id pre-generado: SalesLineBuilder/SalesPaymentHelper necesitan el InvoiceId como FK
        // antes de que exista el agregado — el saldo pendiente (que decide el PaymentTermSnapshot
        // definitivo) solo se conoce después de resolver líneas y pagos.
        var invoiceId = Guid.NewGuid();

        // POS-DISCOUNT-RULES-01: preferencia resuelta UNA vez por request, no por línea.
        var preferences = await _preferences.ResolveAsync(ct);

        // TAX-LINE-SSOT-ICE-IRBPNR-01 (ADR-032 §3.4/§5.1) — resuelta UNA vez por request (misma
        // empresa para todas las líneas), nunca por línea.
        var companyResponsibleCodes = await _companyTaxRepo.GetResponsibleSriTaxCategoryCodesAsync(
            _c.CompanyId,
            tid,
            ct
        );

        var linesResult = await SalesLineBuilder.BuildAsync(
            cmd.Lines,
            invoiceId,
            tid,
            _itemRepo,
            _tax,
            _pricing,
            _warehouseRepo,
            _costService,
            preferences.SalesPos,
            companyResponsibleCodes,
            ct
        );
        if (linesResult.Error is not null)
            return linesResult.Error;

        var provisionalGrandTotal = linesResult.Lines.Sum(l => l.TaxInclusiveTotal);

        List<SalesInvoicePayment> paymentItems = new();
        var cashApplied = 0m;
        if (cmd.Payments is { Count: > 0 })
        {
            var paymentsResult = await SalesPaymentHelper.BuildPaymentsAsync(
                cmd.Payments,
                invoiceId,
                tid,
                _c.CompanyId,
                _pmRepo,
                _bankAccountRepo,
                ct
            );
            if (paymentsResult.Error is not null)
                return paymentsResult.Error;
            paymentItems = paymentsResult.Items!;
            cashApplied = paymentsResult.CashApplied;
        }

        // SALES-SETTLEMENT-CREDIT-01 — saldo pendiente = total − pagos aplicados (excluye
        // método Crédito, que nunca cuenta como dinero recibido).
        var settlement = Domain.Modules.Sales.Policies.SalesSettlementPolicy.Calculate(
            provisionalGrandTotal,
            cashApplied
        );

        Domain.MasterData.Entities.PaymentTerm pt;
        if (explicitPt is not null)
        {
            pt = explicitPt;
        }
        else
        {
            // d) Default del cliente (CompanyBpSalesSettings) — mismo resolver que el explícito,
            // con explicitPaymentTermId=null.
            var customerDefaultResult = await _ptResolver.ResolveForSaleAsync(
                cmd.CustomerId,
                null,
                ct
            );
            if (customerDefaultResult.IsSuccess)
            {
                pt = customerDefaultResult.Value!;
            }
            else if (settlement.IsFullyCovered)
            {
                // Saldo cubierto: no se exige condición de pago — cae al Contado sembrado por el
                // sistema (nunca "primer PaymentTerm activo del catálogo").
                var cashFallback = await _creditPolicy.GetCashFallbackAsync(ct);
                if (!cashFallback.IsSuccess)
                    return Result<SalesInvoiceDto>.ValidationFailure(cashFallback.Error!);
                pt = cashFallback.Value!;
            }
            else
            {
                // Queda saldo pendiente sin PaymentTermId explícito ni default de cliente: b)
                // dueDate manual, c) schedule manual, o e) default de empresa — si ninguno aplica,
                // 422 con el mensaje de negocio aprobado.
                var creditResult = await _creditPolicy.ResolveCompanyOrManualAsync(
                    cmd.DueDate,
                    cmd.Schedule is { Count: > 0 },
                    ct
                );
                if (!creditResult.IsSuccess)
                    return Result<SalesInvoiceDto>.ValidationFailure(creditResult.Error!);

                if (creditResult.Value is not null)
                {
                    pt = creditResult.Value;
                }
                else
                {
                    // Satisfecho por dueDate/schedule manual — el snapshot es descriptivo, no
                    // gobierna la generación del cronograma en este caso (eso lo decide dueDate/
                    // schedule más abajo). Cae al mismo Contado sembrado por el sistema.
                    var cashFallback = await _creditPolicy.GetCashFallbackAsync(ct);
                    if (!cashFallback.IsSuccess)
                        return Result<SalesInvoiceDto>.ValidationFailure(cashFallback.Error!);
                    pt = cashFallback.Value!;
                }
            }
        }

        var (customerEmail, customerAddress) = await CustomerSnapshotContactResolver.ResolveAsync(
            _bpContactRepo,
            _bpLocationRepo,
            cmd.CustomerId,
            ct
        );
        var customerSnapshot = CustomerSnapshot.Create(
            bp.Name.LegalName,
            bp.Identification.Number,
            bp.Identification.Type,
            customerEmail,
            customerAddress
        );

        var paymentTermSnapshot = PaymentTermSnapshot.Create(
            pt.Id,
            pt.Name,
            pt.Installments,
            pt.DaysBetweenInstallments
        );

        var docTypeCode = cmd.DocTypeCode?.Trim();
        if (string.IsNullOrEmpty(docTypeCode))
            docTypeCode = "01"; // Default: Factura (fuente de verdad: tabla sri_doc_types)

        // EmissionPointId nunca viene del cliente — proviene de la caja abierta del usuario
        // (ICurrentCashSession), garantizada no-nula por HasOpenSession (Fase 2: un CashRegister
        // no puede abrir sesión sin EmissionPointId asignado).
        var emissionPointId = _cashSession.EmissionPointId!.Value;
        var emissionType = EmissionType.Electronic;
        var ep = await _epRepo.GetByIdForCompanyAsync(tid, _c.CompanyId, emissionPointId, ct);
        if (ep is not null)
            emissionType = ep.EmissionType;

        var draftNumber = $"DRAFT-{Guid.NewGuid():N}"[..14];

        var inv = SalesInvoice.CreateDraft(
            tid,
            _c.CompanyId,
            _b.BranchId,
            cmd.CustomerId,
            customerSnapshot,
            draftNumber,
            cmd.IssueDate,
            _u.UserId,
            paymentTermSnapshot,
            cashSessionId: _cashSession.CashSessionId!.Value,
            docTypeCode: docTypeCode,
            emissionPointId: emissionPointId,
            emissionType: emissionType,
            dueDate: cmd.DueDate,
            notes: cmd.Notes,
            sriPaymentMethodCode: cmd.SriPaymentMethodCode,
            id: invoiceId
        );

        inv.ReplaceLines(linesResult.Lines, _u.UserId);

        // SALES-SETTLEMENT-CREDIT-01 — cronograma/CxC solo por el saldo pendiente, nunca por el
        // total, y solo cuando efectivamente queda algo por cobrar.
        try
        {
            if (!settlement.IsFullyCovered)
            {
                if (cmd.Schedule is { Count: > 0 })
                    inv.ReplacePaymentSchedule(
                        cmd.Schedule
                            .Select(s => (s.InstallmentNumber, s.DueDate, s.Amount, s.Notes))
                            .ToList(),
                        settlement.PendingBalance
                    );
                else if (cmd.DueDate.HasValue)
                    inv.ReplacePaymentSchedule(
                        new List<(int, DateOnly, decimal, string?)>
                        {
                            (1, cmd.DueDate.Value, settlement.PendingBalance, null),
                        },
                        settlement.PendingBalance
                    );
                else
                    inv.GeneratePaymentSchedule(settlement.PendingBalance);
            }
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            return Result<SalesInvoiceDto>.ValidationFailure(ex.Message);
        }

        if (paymentItems.Count > 0)
            inv.ReplacePayments(paymentItems, _u.UserId);

        await _repo.AddAsync(inv, ct);
        await _repo.SaveChangesAsync(ct);
        return Result<SalesInvoiceDto>.Success(SalesMapper.ToDto(inv));
    }
}

public sealed class UpdateSalesDraftHandler
    : IRequestHandler<UpdateSalesDraftCommand, Result<SalesInvoiceDto>>
{
    private readonly ISalesInvoiceRepository _repo;
    private readonly IBusinessPartnerRepository _bpRepo;
    private readonly IBusinessPartnerRoleRepository _roleRepo;
    private readonly IBusinessPartnerContactRepository _bpContactRepo;
    private readonly IBusinessPartnerLocationRepository _bpLocationRepo;
    private readonly ERP.Application.MasterData.Services.IPaymentTermDefaultResolver _ptResolver;
    private readonly IPaymentMethodRepository _pmRepo;
    private readonly IItemRepository _itemRepo;
    private readonly ISriTaxResolver _tax;
    private readonly IPricingResolver _pricing;
    private readonly ERP.Domain.Modules.Company.Interfaces.ICompanySpecialTaxResponsibilityRepository _companyTaxRepo;
    private readonly IWarehouseRepository _warehouseRepo;
    private readonly IAverageCostService _costService;
    private readonly ICurrentTenant _t;
    private readonly ICurrentCompany _c;
    private readonly ICurrentBranch _b;
    private readonly ICurrentUser _u;
    private readonly IOperationalPreferencesResolver _preferences;
    private readonly ERP.Application.Modules.Sales.Services.ISalesCreditRequirementPolicy _creditPolicy;
    private readonly ERP.Domain.Modules.Finance.Interfaces.ICompanyBankAccountRepository _bankAccountRepo;

    public UpdateSalesDraftHandler(
        ISalesInvoiceRepository repo,
        IBusinessPartnerRepository bpRepo,
        IBusinessPartnerRoleRepository roleRepo,
        IBusinessPartnerContactRepository bpContactRepo,
        IBusinessPartnerLocationRepository bpLocationRepo,
        ERP.Application.MasterData.Services.IPaymentTermDefaultResolver ptResolver,
        IPaymentMethodRepository pmRepo,
        IItemRepository itemRepo,
        ISriTaxResolver tax,
        IPricingResolver pricing,
        ERP.Domain.Modules.Company.Interfaces.ICompanySpecialTaxResponsibilityRepository companyTaxRepo,
        IWarehouseRepository warehouseRepo,
        IAverageCostService costService,
        ICurrentTenant t,
        ICurrentCompany c,
        ICurrentBranch b,
        ICurrentUser u,
        IOperationalPreferencesResolver preferences,
        ERP.Application.Modules.Sales.Services.ISalesCreditRequirementPolicy creditPolicy,
        ERP.Domain.Modules.Finance.Interfaces.ICompanyBankAccountRepository bankAccountRepo
    )
    {
        _repo = repo;
        _bpRepo = bpRepo;
        _roleRepo = roleRepo;
        _bpContactRepo = bpContactRepo;
        _bpLocationRepo = bpLocationRepo;
        _ptResolver = ptResolver;
        _pmRepo = pmRepo;
        _itemRepo = itemRepo;
        _tax = tax;
        _pricing = pricing;
        _companyTaxRepo = companyTaxRepo;
        _warehouseRepo = warehouseRepo;
        _costService = costService;
        _t = t;
        _c = c;
        _b = b;
        _u = u;
        _preferences = preferences;
        _creditPolicy = creditPolicy;
        _bankAccountRepo = bankAccountRepo;
    }

    public async Task<Result<SalesInvoiceDto>> Handle(
        UpdateSalesDraftCommand cmd,
        CancellationToken ct
    )
    {
        var bp = await _bpRepo.GetByIdAsync(cmd.CustomerId, ct);
        if (bp is null)
            return Result<SalesInvoiceDto>.NotFound("Cliente no encontrado.");
        if (!bp.IsActive)
            return Result<SalesInvoiceDto>.ValidationFailure("El cliente se encuentra inactivo.");

        var inv = await _repo.GetByIdAsync(_t.TenantId, cmd.Id, ct);
        if (inv is null || inv.BranchId != _b.BranchId)
            return Result<SalesInvoiceDto>.NotFound("Factura no encontrada.");

        // ADR-033, Fase 4: si el PaymentTerm cambia (explícito o por default del nuevo cliente),
        // el cronograma se regenera automático y descarta cualquier personalización previa —
        // se decide más abajo, después de reconstruir líneas (GeneratePaymentSchedule necesita
        // el GrandTotal vigente).
        var paymentTermChanged = false;

        if (cmd.PaymentTermId.HasValue && cmd.PaymentTermId.Value != inv.PaymentTerm.Id)
        {
            var ptResult = await _ptResolver.ResolveForSaleAsync(cmd.CustomerId, cmd.PaymentTermId, ct);
            if (!ptResult.IsSuccess)
                return Result<SalesInvoiceDto>.ValidationFailure(ptResult.Error!);
            var pt = ptResult.Value!;
            inv.UpdatePaymentTerm(
                PaymentTermSnapshot.Create(
                    pt.Id,
                    pt.Name,
                    pt.Installments,
                    pt.DaysBetweenInstallments
                )
            );
            paymentTermChanged = true;
        }
        else if (cmd.CustomerId != inv.CustomerId)
        {
            // Cambio de cliente sin PaymentTermId explícito: intenta el default de la empresa
            // activa para el nuevo cliente (ADR-033, Fase 3c). Sin default válido, se mantiene
            // el PaymentTerm actual del borrador (mismo criterio ya vigente en Compras desde
            // Fase 3b) — el usuario deberá elegir uno explícito si lo necesita. Test explícito
            // (Fase 4) documenta que en ese caso NO se marca ningún default del nuevo cliente y
            // el cronograma manual existente, si lo hay, no se toca.
            var ptResult = await _ptResolver.ResolveForSaleAsync(cmd.CustomerId, null, ct);
            if (ptResult.IsSuccess)
            {
                var pt = ptResult.Value!;
                inv.UpdatePaymentTerm(
                    PaymentTermSnapshot.Create(
                        pt.Id,
                        pt.Name,
                        pt.Installments,
                        pt.DaysBetweenInstallments
                    )
                );
                paymentTermChanged = true;
            }
        }

        try
        {
            var (customerEmail, customerAddress) = await CustomerSnapshotContactResolver.ResolveAsync(
                _bpContactRepo,
                _bpLocationRepo,
                cmd.CustomerId,
                ct
            );
            var customerSnapshot = CustomerSnapshot.Create(
                bp.Name.LegalName,
                bp.Identification.Number,
                bp.Identification.Type,
                customerEmail,
                customerAddress
            );

            inv.UpdateDraft(
                cmd.CustomerId,
                customerSnapshot,
                cmd.IssueDate,
                _u.UserId,
                dueDate: cmd.DueDate,
                notes: cmd.Notes
            );

            // POS-DISCOUNT-RULES-01: preferencia resuelta UNA vez por request, no por línea.
            var preferences = await _preferences.ResolveAsync(ct);

            // TAX-LINE-SSOT-ICE-IRBPNR-01 (ADR-032 §3.4/§5.1)
            var companyResponsibleCodes =
                await _companyTaxRepo.GetResponsibleSriTaxCategoryCodesAsync(
                    _c.CompanyId,
                    _t.TenantId,
                    ct
                );

            var linesResult = await SalesLineBuilder.BuildAsync(
                cmd.Lines,
                inv.Id,
                _t.TenantId,
                _itemRepo,
                _tax,
                _pricing,
                _warehouseRepo,
                _costService,
                preferences.SalesPos,
                companyResponsibleCodes,
                ct
            );
            if (linesResult.Error is not null)
                return linesResult.Error;

            await _repo.RemoveLinesByInvoiceAsync(inv.Id, linesResult.Lines, ct);
            inv.ReplaceLines(linesResult.Lines, _u.UserId);

            // SALES-SETTLEMENT-CREDIT-01 — los pagos se resuelven ANTES que el cronograma: el
            // saldo pendiente (que decide si hace falta cronograma/CxC y de qué tamaño) depende de
            // ellos. Si el comando no trae un nuevo arreglo de Payments, los pagos existentes no
            // cambian, pero igual se recalcula cuánto de ellos es efectivo real (excluye Crédito).
            List<SalesInvoicePayment>? newPaymentItems = null;
            decimal cashApplied;
            if (cmd.Payments is { Count: > 0 })
            {
                var paymentsResult = await SalesPaymentHelper.BuildPaymentsAsync(
                    cmd.Payments,
                    inv.Id,
                    _t.TenantId,
                    _c.CompanyId,
                    _pmRepo,
                    _bankAccountRepo,
                    ct
                );
                if (paymentsResult.Error is not null)
                    return paymentsResult.Error;
                newPaymentItems = paymentsResult.Items!;
                cashApplied = paymentsResult.CashApplied;
            }
            else
            {
                cashApplied = await SalesPaymentHelper.CalculateCashAppliedAsync(
                    inv.Payments,
                    _t.TenantId,
                    _pmRepo,
                    ct
                );
            }

            var settlement = Domain.Modules.Sales.Policies.SalesSettlementPolicy.Calculate(
                inv.GrandTotal,
                cashApplied
            );

            // ADR-033 / SALES-SETTLEMENT-CREDIT-01 — reglas de regeneración/bloqueo del
            // cronograma, ahora dimensionado por el saldo pendiente, no por el total.
            //
            // SALES-SETTLEMENT-FLOW-ROBUST-01: paridad con CreateSalesDraftHandler — un cronograma
            // (c) o dueDate (b) manual enviado explícitamente en ESTE Update siempre prevalece
            // sobre la regeneración automática por cambio de PaymentTerm/cliente. Antes, la rama
            // `paymentTermChanged` se evaluaba primero y descartaba en silencio el cronograma/
            // dueDate manual que el propio comando traía — divergencia real frente a Create, donde
            // la resolución del PaymentTerm (metadata descriptiva) y la generación del cronograma
            // (cmd.Schedule > cmd.DueDate > auto) son independientes.
            if (settlement.IsFullyCovered)
            {
                // La venta quedó saldada por completo tras esta edición — no debe quedar un
                // cronograma/CxC huérfano por saldo que ya no existe.
                if (inv.PaymentSchedules.Count > 0)
                {
                    await _repo.RemovePaymentSchedulesByInvoiceAsync(inv.Id, ct);
                    inv.ClearPaymentSchedule();
                }
            }
            else if (cmd.Schedule is { Count: > 0 })
            {
                // El usuario envía un cronograma explícito en este Update — se acepta si es
                // válido (ReplacePaymentSchedule exige que la suma calce con el saldo pendiente
                // vigente, ya recalculado tras ReplaceLines/pagos). Prevalece incluso si además
                // cambió el PaymentTerm/cliente en el mismo comando.
                await _repo.RemovePaymentSchedulesByInvoiceAsync(inv.Id, ct);
                inv.ReplacePaymentSchedule(
                    cmd.Schedule
                        .Select(s => (s.InstallmentNumber, s.DueDate, s.Amount, s.Notes))
                        .ToList(),
                    settlement.PendingBalance
                );
            }
            else if (cmd.DueDate.HasValue)
            {
                // Fecha de vencimiento manual — cuota única por el saldo pendiente (mismo criterio
                // que CreateSalesDraftHandler). Prevalece incluso si además cambió el PaymentTerm/
                // cliente en el mismo comando, igual que el cronograma explícito arriba.
                await _repo.RemovePaymentSchedulesByInvoiceAsync(inv.Id, ct);
                inv.ReplacePaymentSchedule(
                    new List<(int, DateOnly, decimal, string?)>
                    {
                        (1, cmd.DueDate.Value, settlement.PendingBalance, null),
                    },
                    settlement.PendingBalance
                );
            }
            else if (paymentTermChanged)
            {
                // Cambio de condición de pago o de cliente con default válido, sin cronograma/
                // dueDate manual en este comando: regenera automático, descarta cualquier
                // personalización previa.
                await _repo.RemovePaymentSchedulesByInvoiceAsync(inv.Id, ct);
                inv.GeneratePaymentSchedule(settlement.PendingBalance);
            }
            else
            {
                var currentScheduleSum = inv.PaymentSchedules.Sum(s => s.Amount);
                if (currentScheduleSum != settlement.PendingBalance)
                {
                    if (inv.IsPaymentScheduleManual)
                        return Result<SalesInvoiceDto>.ValidationFailure(
                            $"El cronograma fue personalizado y el saldo pendiente del documento cambió "
                                + $"(nuevo saldo: {settlement.PendingBalance:F2}, cronograma actual: {currentScheduleSum:F2}). "
                                + "Debe revisar y ajustar el cronograma antes de continuar."
                        );

                    if (inv.PaymentSchedules.Count == 0)
                    {
                        // No había cronograma (venta antes saldada, ahora queda saldo pendiente):
                        // exige una regla de crédito válida — mismo orden que CreateSalesDraftHandler
                        // (aquí ya se descartaron dueDate/schedule manual arriba; solo queda el
                        // default de empresa, o el PaymentTerm ya vigente en el borrador si es
                        // utilizable).
                        var creditResult = await _creditPolicy.ResolveCompanyOrManualAsync(
                            null,
                            false,
                            ct
                        );
                        if (!creditResult.IsSuccess)
                            return Result<SalesInvoiceDto>.ValidationFailure(creditResult.Error!);

                        if (creditResult.Value is not null)
                            inv.UpdatePaymentTerm(
                                PaymentTermSnapshot.Create(
                                    creditResult.Value.Id,
                                    creditResult.Value.Name,
                                    creditResult.Value.Installments,
                                    creditResult.Value.DaysBetweenInstallments
                                )
                            );
                    }

                    // Cronograma automático: se regenera silenciosamente con el nuevo saldo.
                    await _repo.RemovePaymentSchedulesByInvoiceAsync(inv.Id, ct);
                    inv.GeneratePaymentSchedule(settlement.PendingBalance);
                }
            }

            if (newPaymentItems is not null)
            {
                await _repo.RemovePaymentsByInvoiceAsync(inv.Id, ct);
                inv.ReplacePayments(newPaymentItems, _u.UserId);
            }
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            return Result<SalesInvoiceDto>.ValidationFailure(ex.Message);
        }

        await _repo.SaveChangesAsync(ct);
        return Result<SalesInvoiceDto>.Success(SalesMapper.ToDto(inv));
    }
}

public sealed class GetSalesInvoiceByIdHandler
    : IRequestHandler<GetSalesInvoiceByIdQuery, Result<SalesInvoiceDto>>
{
    private readonly ISalesInvoiceRepository _repo;
    private readonly ERP.Domain.Modules.ElectronicDocuments.Interfaces.IElectronicDocumentRepository _edocRepo;
    private readonly ICurrentTenant _t;
    private readonly ICurrentBranch _b;

    public GetSalesInvoiceByIdHandler(
        ISalesInvoiceRepository repo,
        ERP.Domain.Modules.ElectronicDocuments.Interfaces.IElectronicDocumentRepository edocRepo,
        ICurrentTenant t,
        ICurrentBranch b
    )
    {
        _repo = repo;
        _edocRepo = edocRepo;
        _t = t;
        _b = b;
    }

    public async Task<Result<SalesInvoiceDto>> Handle(
        GetSalesInvoiceByIdQuery q,
        CancellationToken ct
    )
    {
        var inv = await _repo.GetByIdAsync(_t.TenantId, q.Id, ct);
        if (inv is null || inv.BranchId != _b.BranchId)
            return Result<SalesInvoiceDto>.NotFound("Factura no encontrada.");

        // Fase 10: ElectronicDocument es la única fuente de verdad del estado electrónico.
        var edoc = await _edocRepo.GetBySourceAsync(_t.TenantId, "Sales", inv.Id, ct);
        return Result<SalesInvoiceDto>.Success(SalesMapper.ToDto(inv, edoc));
    }
}

public sealed class GetSalesInvoiceListHandler
    : IRequestHandler<GetSalesInvoiceListQuery, Result<SalesListResponse>>
{
    private readonly ISalesInvoiceRepository _repo;
    private readonly ICurrentTenant _t;

    public GetSalesInvoiceListHandler(ISalesInvoiceRepository repo, ICurrentTenant t)
    {
        _repo = repo;
        _t = t;
    }

    public async Task<Result<SalesListResponse>> Handle(
        GetSalesInvoiceListQuery q,
        CancellationToken ct
    )
    {
        var (items, total) = await _repo.GetPagedAsync(
            _t.TenantId,
            q.Search,
            q.Status,
            q.PageNumber,
            q.PageSize,
            ct
        );
        var dtos = items
            .Select(i => new SalesListDto(
                i.Id,
                i.InvoiceNumber,
                i.IssueDate,
                i.CustomerId,
                i.Customer.Name,
                i.Status.ToString(),
                i.Lines.Count,
                i.GrandTotal,
                i.CreatedAt
            ))
            .ToList();
        return Result<SalesListResponse>.Success(
            new SalesListResponse(dtos, total, q.PageNumber, q.PageSize)
        );
    }
}

// SALES-PRESENTATIONS-02 — inspirado en PurchaseLinePackagingResolver (Purchases). A diferencia
// de Compras, esta fase no consume ItemSupplierCode (Ventas no tiene proveedor) ni IsSaleDefault
// (decisión explícita: no auto-seleccionar presentación todavía, ver comentario en Resolve).
file sealed record SalesLinePackagingSnapshot(
    Guid? PackagingLevelId,
    string UomCode,
    string BaseUomCode,
    decimal ConversionFactor
);

file static class SalesLinePackagingResolver
{
    /// <summary>
    /// PackagingLevelId null → venta en unidad base (comportamiento actual preservado, factor 1).
    /// PackagingLevelId informado → venta por presentación; debe pertenecer al ítem y estar activa.
    /// No consume IsSaleDefault automáticamente en esta fase — queda para una fase posterior que
    /// decida si auto-seleccionar la presentación de venta por defecto del ítem.
    /// </summary>
    public static (SalesLinePackagingSnapshot? Snapshot, string? Error) Resolve(
        Item item,
        Guid? packagingLevelId,
        string description
    )
    {
        if (!packagingLevelId.HasValue)
            return (
                new SalesLinePackagingSnapshot(null, item.DefaultUomCode, item.DefaultUomCode, 1m),
                null
            );

        var selected = item.PackagingLevels.FirstOrDefault(p =>
            p.Id == packagingLevelId.Value && p.IsActive
        );
        if (selected is null)
            return (
                null,
                $"Línea '{description}': la presentación seleccionada no pertenece al ítem o está inactiva."
            );

        if (selected.BaseQuantity <= 0)
            throw new InvalidOperationException(
                "La cantidad base del empaque debe ser mayor a cero."
            );

        return (
            new SalesLinePackagingSnapshot(
                selected.Id,
                selected.UomCode,
                item.DefaultUomCode,
                selected.BaseQuantity
            ),
            null
        );
    }
}

file static class SalesLineBuilder
{
    public static async Task<LinesBuildResult> BuildAsync(
        List<SalesLineInput> inputs,
        Guid invoiceId,
        Guid tid,
        IItemRepository itemRepo,
        ISriTaxResolver tax,
        IPricingResolver pricingResolver,
        IWarehouseRepository warehouseRepo,
        IAverageCostService costService,
        SalesPosPreferences salesPosPreferences,
        IReadOnlyCollection<string> companyResponsibleCodes,
        CancellationToken ct
    )
    {
        // SALES-HISTORICAL-PRICING-SNAPSHOT-01: nombre de bodega resuelto UNA vez por bodega
        // distinta usada en el Draft (evita relecturas repetidas cuando varias líneas despachan
        // de la misma bodega) — solo LEE el catálogo, nunca lo modifica.
        var warehouseNameCache = new Dictionary<Guid, string?>();
        var lines = new List<SalesInvoiceDetail>();
        foreach (var l in inputs)
        {
            // POS-DISCOUNT-RULES-01 (sales.pos.allow_manual_discount / max_discount_percent):
            // l.DiscountPct es siempre un descuento MANUAL de línea — un descuento por lista de
            // precios/promoción ya viene reflejado en l.UnitPrice (precio ya resuelto más bajo),
            // nunca en este campo, así que no hay riesgo de bloquear un descuento automático del
            // pricing engine aquí (regla 4/5 del bloque). No confundir con el piso de precio por
            // ítem (item.SaleConfig.MaxDiscountPercent, más abajo) — ese valida el precio unitario
            // contra el catálogo del producto, es un mecanismo distinto y ya existente.
            if (l.DiscountPct != 0m)
            {
                if (!salesPosPreferences.AllowManualDiscount)
                    return new(
                        null!,
                        Result<SalesInvoiceDto>.ValidationFailure(
                            $"Línea '{l.Description}': esta empresa no permite aplicar descuentos manuales."
                        )
                    );

                if (
                    salesPosPreferences.MaxDiscountPercent > 0m
                    && l.DiscountPct > salesPosPreferences.MaxDiscountPercent
                )
                    return new(
                        null!,
                        Result<SalesInvoiceDto>.ValidationFailure(
                            $"Línea '{l.Description}': el descuento máximo permitido es {salesPosPreferences.MaxDiscountPercent}%."
                        )
                    );
            }

            var vatCode = l.VatCode;
            var iceCode = l.IceCode;
            string? irbpnrCode = null;
            string? snapshotSku = null;
            string? snapshotItemName = null;
            string uomCode = "UNIT";
            string? baseUomCode = null;
            decimal conversionFactor = 1m;
            Guid? packagingLevelId = null;
            Guid? warehouseId = null;
            // SALES-HISTORICAL-PRICING-SNAPSHOT-01: resultado del Pricing Engine v2 para esta
            // línea (si el ítem lo resolvió con éxito) — usado más abajo, tras crear la línea, para
            // congelar ListPriceAtSale/PriceListId/PriceListName/PricingSource. Independiente del
            // resultado de la validación de piso de descuento (que solo consume resolvedPrice).
            PricingResult? pricingResultValue = null;
            bool tracksStockForCost = false;

            if (l.ItemId.HasValue)
            {
                // GetByIdAsync (no GetByIdLightAsync): necesitamos item.PackagingLevels cargado
                // para resolver la presentación — mismo criterio que Purchases (PurchaseLineBuilder).
                var item = await itemRepo.GetByIdAsync(l.ItemId.Value, tid, ct);
                if (item is null)
                    return new(
                        null!,
                        Result<SalesInvoiceDto>.ValidationFailure(
                            $"Línea '{l.Description}': el producto seleccionado ya no existe."
                        )
                    );

                if (!item.IsActive || !item.SaleConfig.IsForSale)
                    return new(
                        null!,
                        Result<SalesInvoiceDto>.ValidationFailure(
                            $"Línea '{l.Description}': el producto '{item.Code.Description}' está inactivo o no está habilitado para venta."
                        )
                    );

                snapshotSku = item.Code.SKU;
                snapshotItemName = item.Code.Description;

                var (packagingSnapshot, packagingError) = SalesLinePackagingResolver.Resolve(
                    item,
                    l.PackagingLevelId,
                    l.Description
                );
                if (packagingError is not null)
                    return new(null!, Result<SalesInvoiceDto>.ValidationFailure(packagingError));

                uomCode = packagingSnapshot!.UomCode;
                baseUomCode = packagingSnapshot.BaseUomCode;
                conversionFactor = packagingSnapshot.ConversionFactor;
                packagingLevelId = packagingSnapshot.PackagingLevelId;

                // Kardex: la bodega de despacho es obligatoria por línea cuando el ítem
                // controla inventario — una misma factura puede despachar de bodegas distintas.
                if (item.StockConfig.TracksStock)
                {
                    if (l.WarehouseId is null || l.WarehouseId == Guid.Empty)
                        return new(
                            null!,
                            Result<SalesInvoiceDto>.ValidationFailure(
                                $"Línea '{l.Description}': debe seleccionar la bodega de despacho para este producto."
                            )
                        );
                    warehouseId = l.WarehouseId;
                    tracksStockForCost = true;
                }

                // Configuración Tributaria CLOSED: el Item es la única fuente de verdad —
                // el VatCode/IceCode enviado por el cliente nunca prevalece sobre el ítem.
                vatCode = item.TaxConfig.SaleVatCode ?? vatCode;

                // TAX-LINE-SSOT-ICE-IRBPNR-01 (ADR-032 §3.4/§5.1) — ICE/IRBPNR se calculan SOLO si
                // se cumplen ambas condiciones: (a) el ítem tiene ItemSpecialTaxConfiguration activa
                // para ese impuesto, (b) la empresa está marcada responsable de aplicarlo en ventas.
                // La compra nunca se consulta aquí — Ventas jamás copia impuestos desde Compras.
                var iceConfig = item.SpecialTaxConfigurations.FirstOrDefault(c =>
                    c.IsActive && c.SriTaxCategoryCode == SriTaxCategoryCodes.Ice
                );
                iceCode =
                    iceConfig is not null
                    && companyResponsibleCodes.Contains(SriTaxCategoryCodes.Ice)
                        ? iceConfig.TaxCatalogCode
                        : null;

                var irbpnrConfig = item.SpecialTaxConfigurations.FirstOrDefault(c =>
                    c.IsActive && c.SriTaxCategoryCode == SriTaxCategoryCodes.Irbpnr
                );
                irbpnrCode =
                    irbpnrConfig is not null
                    && companyResponsibleCodes.Contains(SriTaxCategoryCodes.Irbpnr)
                        ? irbpnrConfig.TaxCatalogCode
                        : null;

                // Pricing Engine v2 (SSOT del precio de venta) — resuelve el precio vigente
                // para validar el piso de descuento configurado en el maestro del ítem. El precio
                // resuelto es siempre por unidad base — al vender por presentación (ej. caja x12),
                // el piso de descuento se escala por conversionFactor (SALES-PRESENTATIONS-02);
                // no se crea una tabla de precios por presentación, ni se toca PricingResolver.
                var pricingResult = await pricingResolver.ResolveAsync(item.Id, ct: ct);
                if (pricingResult.IsSuccess)
                {
                    pricingResultValue = pricingResult.Value;
                    var resolvedPrice = pricingResult.Value!.UnitPrice * conversionFactor;
                    var maxDiscountPercent = item.SaleConfig.MaxDiscountPercent;
                    if (maxDiscountPercent.HasValue)
                    {
                        var minAllowed = resolvedPrice * (1 - maxDiscountPercent.Value / 100m);
                        if (l.UnitPrice < minAllowed)
                            return new(
                                null!,
                                Result<SalesInvoiceDto>.ValidationFailure(
                                    $"Línea '{l.Description}': el precio ingresado excede el descuento máximo permitido para este producto ({maxDiscountPercent.Value}%)."
                                )
                            );
                    }
                }
            }

            if (string.IsNullOrWhiteSpace(vatCode))
                return new(
                    null!,
                    Result<SalesInvoiceDto>.ValidationFailure(
                        $"Línea '{l.Description}': código IVA obligatorio. Seleccione un producto con tarifa IVA o indique el código manualmente."
                    )
                );

            var line = SalesInvoiceDetail.Create(
                invoiceId,
                tid,
                l.Description,
                l.Quantity,
                l.UnitPrice,
                vatCode,
                uomCode,
                l.ItemId,
                l.Notes,
                l.DiscountPct,
                iceCode,
                snapshotSku,
                snapshotItemName,
                conversionFactor: conversionFactor,
                warehouseId: warehouseId,
                baseUomCode: baseUomCode,
                packagingLevelId: packagingLevelId
            );

            var taxResult = await SalesTaxHelper.ResolveTaxesAsync(line, tax, irbpnrCode, ct);
            if (taxResult is not null)
                return new(null!, taxResult);

            // SALES-HISTORICAL-PRICING-SNAPSHOT-01 — snapshot comercial histórico, resuelto con
            // los mismos datos reales ya usados arriba (pricing/costing/bodega vigentes en este
            // instante del Draft) — nunca inventado, nunca recalculado en Authorize (ver
            // AuthorizeSalesInvoiceHandler, que jamás llama SetHistoricalSnapshot).
            string? warehouseName = null;
            if (warehouseId.HasValue)
            {
                if (!warehouseNameCache.TryGetValue(warehouseId.Value, out warehouseName))
                {
                    var warehouse = await warehouseRepo.GetByIdAsync(tid, warehouseId.Value, ct);
                    warehouseName = warehouse?.Name;
                    warehouseNameCache[warehouseId.Value] = warehouseName;
                }
            }

            // Costo: solo lectura de IAverageCostService (Kardex) — 0 significa "sin stock
            // registrado" (ver contrato de ObtenerCostoPromedioAsync), tratado igual que "sin
            // dato" para no persistir un costo cero engañoso.
            decimal? unitCostAtSale = null;
            decimal? totalCostAtSale = null;
            if (tracksStockForCost && l.ItemId.HasValue && warehouseId.HasValue)
            {
                var averageCost = await costService.ObtenerCostoPromedioAsync(
                    tid,
                    l.ItemId.Value,
                    warehouseId.Value,
                    ct
                );
                if (averageCost > 0m)
                {
                    unitCostAtSale = Math.Round(
                        averageCost,
                        FiscalPrecision.UnitCost,
                        MidpointRounding.AwayFromZero
                    );
                    totalCostAtSale = Math.Round(
                        unitCostAtSale.Value * line.QuantityInBaseUom,
                        FiscalPrecision.UnitCost,
                        MidpointRounding.AwayFromZero
                    );
                }
            }

            // ListPriceAtSale = precio base/lista (BasePrice) resuelto por el Pricing Engine v2 (ya
            // escalado a la unidad vendida) ANTES de cualquier descuento — automático (regla del
            // Pricing Engine) o manual — así como antes de cualquier edición manual del precio
            // facturado. UnitPrice (pricingResultValue) es el precio YA con el descuento de regla
            // aplicado, por lo que NO sirve como ancla de "precio lista" (bug corregido: antes se
            // tomaba UnitPrice, dejando ListPriceAtSale == UnitPrice incluso cuando sí hubo
            // descuento de regla).
            decimal? listPriceAtSale =
                pricingResultValue is not null
                    ? Math.Round(
                        pricingResultValue.BasePrice * conversionFactor,
                        FiscalPrecision.UnitCost,
                        MidpointRounding.AwayFromZero
                    )
                    : null;
            var pricingSource =
                pricingResultValue is not null
                    ? (pricingResultValue.RuleApplied ?? "BaseSalePrice")
                    : null;

            // DiscountSource/DiscountDescription: el único mecanismo de descuento de línea hoy es
            // manual (l.DiscountPct — ver comentario más arriba, "SalesLineBuilder"); cuando no hay
            // descuento manual pero el precio sí quedó ajustado por una regla del Pricing Engine,
            // se documenta esa regla como el origen (RuleDescription, mismo texto humano que ya
            // usa el buscador de ítems).
            //
            // SALES-INVOICE-FINAL-SEMANTIC-INTEGRITY-01 (Fase 6D) — causa raíz corregida: antes se
            // poblaba DiscountSource=PricingRule solo con pricingResultValue.RuleApplied != null,
            // sin verificar si el precio final REALMENTE bajó frente a ListPriceAtSale. Una regla
            // puede existir en el catálogo (RuleApplied != null) sin producir ninguna rebaja
            // económica real (ej. regla que resuelve al mismo precio de lista) — en ese caso
            // decir "Descuento X% (regla)" es una mentira comercial (bug confirmado con evidencia
            // real de BD: discount_description no vacío con list_price_at_sale == unit_price).
            // effectivePricingDiscount usa la misma precisión de precio unitario (FiscalPrecision.
            // UnitCost, 6 decimales) que ListPriceAtSale/UnitPrice ya usan en este mismo método.
            var effectivePricingDiscount = listPriceAtSale.HasValue
                ? Math.Round(
                    listPriceAtSale.Value - l.UnitPrice,
                    FiscalPrecision.UnitCost,
                    MidpointRounding.AwayFromZero
                )
                : 0m;

            string? discountSource = null;
            string? discountDescription = null;
            if (l.DiscountPct > 0)
            {
                discountSource = "Manual";
                discountDescription =
                    $"Descuento manual de {l.DiscountPct:0.##}% aplicado en la línea.";
            }
            else if (pricingResultValue?.RuleApplied is not null && effectivePricingDiscount > 0)
            {
                discountSource = "PricingRule";
                discountDescription = pricingResultValue.RuleDescription;
            }

            line.SetHistoricalSnapshot(
                warehouseName,
                unitCostAtSale,
                totalCostAtSale,
                listPriceAtSale,
                pricingResultValue?.PriceListId,
                pricingResultValue?.PriceListName,
                pricingSource,
                discountSource,
                discountDescription
            );

            lines.Add(line);
        }
        return new(lines, null);
    }

    public sealed record LinesBuildResult(
        List<SalesInvoiceDetail> Lines,
        Result<SalesInvoiceDto>? Error
    );
}

file static class SalesPaymentHelper
{
    public static async Task<PaymentsBuildResult> BuildPaymentsAsync(
        List<SalesPaymentInput> inputs,
        Guid invoiceId,
        Guid tenantId,
        Guid companyId,
        IPaymentMethodRepository pmRepo,
        ERP.Domain.Modules.Finance.Interfaces.ICompanyBankAccountRepository bankAccountRepo,
        CancellationToken ct
    )
    {
        var items = new List<SalesInvoicePayment>();
        var cache = new Dictionary<Guid, PaymentMethod>();

        foreach (var input in inputs)
        {
            if (!cache.TryGetValue(input.PaymentMethodId, out var pm))
            {
                pm = await pmRepo.GetByIdAsync(tenantId, input.PaymentMethodId, ct);
                if (pm is null)
                    return PaymentsBuildResult.Fail(
                        $"Método de pago no encontrado (ID: {input.PaymentMethodId})."
                    );
                if (!pm.IsActive)
                    return PaymentsBuildResult.Fail(
                        $"El método de pago '{pm.Name}' está inactivo."
                    );
                if (pm.RequiresReference && string.IsNullOrWhiteSpace(input.Reference))
                    return PaymentsBuildResult.Fail(
                        $"El método '{pm.Name}' requiere una referencia."
                    );
                cache[pm.Id] = pm;
            }

            // SALES-TRANSFER-BANK-ACCOUNT-01: solo Transferencia acepta/exige TransferDetail con
            // CompanyBankAccountId — un TransferDetail adjunto a cualquier otro método (payload
            // manipulado) se rechaza aquí, nunca se persiste silenciosamente.
            if (
                input.TransferDetail is not null
                && pm.DetailType != PaymentMethodDetailType.Transfer
            )
                return PaymentsBuildResult.Fail(
                    $"El método '{pm.Name}' no admite datos de transferencia bancaria."
                );
            if (pm.DetailType == PaymentMethodDetailType.Transfer)
            {
                if (input.TransferDetail?.CompanyBankAccountId is not { } bankAccountId)
                    return PaymentsBuildResult.Fail(
                        $"El método '{pm.Name}' requiere seleccionar una cuenta bancaria destino."
                    );

                var bankAccount = await bankAccountRepo.GetByIdAsync(tenantId, bankAccountId, ct);
                if (bankAccount is null || bankAccount.CompanyId != companyId)
                    return PaymentsBuildResult.Fail(
                        "La cuenta bancaria seleccionada no existe o no pertenece a esta empresa."
                    );
                if (!bankAccount.IsActive)
                    return PaymentsBuildResult.Fail(
                        "La cuenta bancaria seleccionada está inactiva."
                    );
                if (string.IsNullOrWhiteSpace(input.TransferDetail!.ReceiptNumber))
                    return PaymentsBuildResult.Fail(
                        "El comprobante/referencia de la transferencia es obligatorio."
                    );
                if (ParseDate(input.TransferDetail.TransferDate) is null)
                    return PaymentsBuildResult.Fail(
                        "La fecha de operación de la transferencia es obligatoria."
                    );
            }

            var payment = SalesInvoicePayment.Create(
                invoiceId,
                tenantId,
                pm.Id,
                pm.Code,
                pm.Name,
                input.Amount,
                input.Reference
            );

            if (input.CardDetail is not null)
                payment.SetCardDetail(
                    PaymentCardDetail.Create(
                        payment.Id,
                        input.CardDetail.CardBrand,
                        input.CardDetail.CardLastFour,
                        input.CardDetail.BankName,
                        input.CardDetail.AuthorizationCode,
                        input.CardDetail.LotNumber
                    )
                );

            if (input.TransferDetail is not null)
                payment.SetTransferDetail(
                    PaymentTransferDetail.Create(
                        payment.Id,
                        input.TransferDetail.CompanyBankAccountId!.Value,
                        input.TransferDetail.ReceiptNumber ?? string.Empty,
                        ParseDate(input.TransferDetail.TransferDate) ?? default
                    )
                );

            if (input.ChequeDetail is not null)
                payment.SetChequeDetail(
                    PaymentChequeDetail.Create(
                        payment.Id,
                        input.ChequeDetail.BankName,
                        input.ChequeDetail.ChequeNumber,
                        input.ChequeDetail.HolderName,
                        ParseDate(input.ChequeDetail.CashDate)
                    )
                );

            items.Add(payment);
        }

        var paymentSum = items.Sum(p => p.Amount);
        if (paymentSum < 0)
            return PaymentsBuildResult.Fail("La suma de cobros no puede ser negativa.");

        // SALES-SETTLEMENT-CREDIT-01: el método de pago "Crédito" (IsCreditAllowed) nunca cuenta
        // como dinero recibido — cache[] ya tiene el PaymentMethod resuelto de cada item.
        var cashApplied = items
            .Where(p => !cache[p.PaymentMethodId].IsCreditAllowed)
            .Sum(p => p.Amount);

        return PaymentsBuildResult.Ok(items, cashApplied);
    }

    private static DateOnly? ParseDate(string? iso) =>
        iso is not null && DateOnly.TryParse(iso, out var d) ? d : null;

    /// <summary>
    /// SALES-SETTLEMENT-CREDIT-01 — recalcula el efectivo aplicado (excluye método Crédito) a
    /// partir de pagos YA persistidos en el agregado (UpdateSalesDraftHandler cuando el comando no
    /// trae un nuevo arreglo de Payments, es decir, los pagos existentes no cambian pero igual se
    /// necesita el saldo pendiente vigente).
    /// </summary>
    public static async Task<decimal> CalculateCashAppliedAsync(
        IEnumerable<SalesInvoicePayment> payments,
        Guid tenantId,
        IPaymentMethodRepository pmRepo,
        CancellationToken ct
    )
    {
        var cache = new Dictionary<Guid, bool>();
        decimal cashApplied = 0m;
        foreach (var p in payments)
        {
            if (!cache.TryGetValue(p.PaymentMethodId, out var isCreditAllowed))
            {
                var pm = await pmRepo.GetByIdAsync(tenantId, p.PaymentMethodId, ct);
                isCreditAllowed = pm?.IsCreditAllowed == true;
                cache[p.PaymentMethodId] = isCreditAllowed;
            }
            if (!isCreditAllowed)
                cashApplied += p.Amount;
        }
        return cashApplied;
    }

    public sealed record PaymentsBuildResult(
        List<SalesInvoicePayment>? Items,
        decimal CashApplied,
        Result<SalesInvoiceDto>? Error
    )
    {
        public static PaymentsBuildResult Ok(List<SalesInvoicePayment> items, decimal cashApplied) =>
            new(items, cashApplied, null);

        public static PaymentsBuildResult Fail(string msg) =>
            new(null, 0m, Result<SalesInvoiceDto>.ValidationFailure(msg));
    }
}

file static class SalesTaxHelper
{
    /// <summary>
    /// TAX-LINE-SSOT-ICE-IRBPNR-01 (ADR-032 §3.4/§5.1) — <paramref name="irbpnrCatalogCode"/> ya
    /// viene decidido por <see cref="SalesLineBuilder"/> (ItemSpecialTaxConfiguration del ítem AND
    /// CompanySpecialTaxResponsibility de la empresa); este helper solo resuelve el catálogo SRI y
    /// escribe el snapshot — nunca decide si aplica.
    /// </summary>
    public static async Task<Result<SalesInvoiceDto>?> ResolveTaxesAsync(
        SalesInvoiceDetail line,
        ISriTaxResolver tax,
        string? irbpnrCatalogCode,
        CancellationToken ct
    )
    {
        var vatResult = await tax.GetVatRateWithNameAsync(line.VatCode, ct);
        if (vatResult is null)
            return Result<SalesInvoiceDto>.ValidationFailure(
                $"Código IVA '{line.VatCode}' no encontrado o inactivo."
            );

        decimal iceRate = 0;
        string? iceName = null;
        var iceCalculationType = SriTaxCalculationType.Percentage;
        decimal? iceExactAmount = null;
        if (!string.IsNullOrWhiteSpace(line.IceCode))
        {
            // Paridad con Compras (PurchaseDraftUseCases.TaxHelper) — usa el catálogo completo (no
            // el legacy GetIceRateWithNameAsync, que exige Percentage) para soportar también ICE
            // "específico" en Ventas (ADR-032 §3.3, cierra el gap detectado en la auditoría previa).
            var iceEntry = await tax.GetIceCatalogEntryAsync(line.IceCode, ct);
            if (iceEntry is null)
                return Result<SalesInvoiceDto>.ValidationFailure(
                    $"Código ICE '{line.IceCode}' no encontrado o inactivo."
                );
            iceName = iceEntry.Name;
            iceCalculationType = iceEntry.CalculationType;
            if (iceEntry.CalculationType == SriTaxCalculationType.Specific)
                iceExactAmount = Math.Round(
                    (iceEntry.UnitValue ?? 0m) * line.QuantityInBaseUom,
                    ERP.Domain.Common.FiscalPrecision.TaxAmount,
                    MidpointRounding.AwayFromZero
                );
            else
                iceRate = iceEntry.Percentage ?? 0m;
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

        // IRBPNR no tiene campos escalares legacy — vive únicamente en Taxes, fijado vía
        // ReplaceTaxes (nunca toca la fila de VAT/ICE que ApplyTaxes acaba de sincronizar).
        if (!string.IsNullOrWhiteSpace(irbpnrCatalogCode))
        {
            var irbpnrEntry = await tax.GetIrbpnrCatalogEntryAsync(irbpnrCatalogCode, ct);
            if (irbpnrEntry is null)
                return Result<SalesInvoiceDto>.ValidationFailure(
                    $"Código IRBPNR '{irbpnrCatalogCode}' no encontrado o inactivo."
                );

            var irbpnrAmount =
                irbpnrEntry.CalculationType == SriTaxCalculationType.Specific
                    ? Math.Round(
                        (irbpnrEntry.UnitValue ?? 0m) * line.QuantityInBaseUom,
                        ERP.Domain.Common.FiscalPrecision.TaxAmount,
                        MidpointRounding.AwayFromZero
                    )
                    : Math.Round(
                        line.TaxableBase * (irbpnrEntry.Percentage ?? 0m) / 100m,
                        ERP.Domain.Common.FiscalPrecision.TaxAmount,
                        MidpointRounding.AwayFromZero
                    );

            line.ReplaceTaxes(
                [
                    ERP.Domain.Modules.Sales.Entities.SalesInvoiceDetailTax.Create(
                        line.Id,
                        line.TenantId,
                        SriTaxCategoryCodes.Irbpnr,
                        irbpnrCatalogCode,
                        irbpnrEntry.Name,
                        irbpnrEntry.Percentage ?? irbpnrEntry.UnitValue,
                        irbpnrEntry.CalculationType,
                        line.TaxableBase,
                        irbpnrAmount,
                        SalesTaxSource.Calculated
                    ),
                ]
            );
        }

        return null;
    }
}

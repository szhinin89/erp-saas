using ERP.Application.Common;
using ERP.Application.Common.Services;
using ERP.Application.Modules.Accounting.Posting;
using ERP.Application.Modules.Sales.Services;
using ERP.Application.Modules.Sales.UseCases;
using ERP.Application.Tests.TestSupport;
using ERP.Domain.Configuration.Interfaces;
using ERP.Domain.MasterData.Entities;
using ERP.Domain.MasterData.Interfaces;
using ERP.Domain.Modules.Accounting.Interfaces;
using ERP.Domain.Modules.Company.Interfaces;
using ERP.Domain.Modules.ElectronicDocuments.Entities;
using ERP.Domain.Modules.ElectronicDocuments.Interfaces;
using ERP.Domain.Modules.Finance.Interfaces;
using ERP.Domain.Modules.Inventory.Interfaces;
using ERP.Domain.Modules.Sales.Entities;
using ERP.Domain.Modules.Sales.Enums;
using ERP.Domain.Modules.Sales.Interfaces;
using ERP.Domain.Modules.Sales.Policies;
using ERP.Domain.Modules.Sales.ValueObjects;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;

namespace ERP.Application.Tests.Sales;

/// <summary>
/// Regresión del bug SRI [65] FECHA EMISIÓN EXTEMPORÁNEA: <see cref="AuthorizeSalesInvoiceHandler"/>
/// debe rechazar fechas de emisión futuras o fuera de la tolerancia SRI (90 días) ANTES de capturar
/// secuencial/generar XML, usando la fecha empresarial de <see cref="ICompanyClock"/> — nunca
/// <c>DateTime.UtcNow.Date</c>. También cubre Fase 4 (ADR — Rediseño del módulo de Caja): la
/// autorización ya no busca la caja abierta del usuario — usa <c>SalesInvoice.CashSessionId</c>,
/// fijado al crear el borrador.
/// </summary>
public sealed class AuthorizeSalesInvoiceHandlerTests
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid CompanyId = Guid.NewGuid();
    private static readonly Guid BranchId = Guid.NewGuid();
    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly Guid CustomerId = Guid.NewGuid();
    private static readonly Guid PaymentTermId = Guid.NewGuid();
    private static readonly Guid PaymentMethodId = Guid.NewGuid();
    private static readonly Guid CashSessionId = Guid.NewGuid();
    private static readonly Guid CashRegisterId = Guid.NewGuid();
    private static readonly Guid CashAccountingAccountId = Guid.NewGuid();

    /// <summary>
    /// SALES-COLLECTION-ACCOUNT-SSOT-CLEANUP-01 — Efectivo resuelve su cuenta contable
    /// exclusivamente desde <c>CashRegister.AccountingAccountId</c> de la caja de
    /// <see cref="CashSessionId"/> (fijado en la factura al crear el borrador). Estos mocks por
    /// defecto reproducen una caja activa con cuenta contable configurada — comportamiento feliz
    /// idéntico al que tenían las pruebas de Efectivo antes de este ticket.
    /// </summary>
    private static (
        Mock<ERP.Domain.Modules.Caja.Interfaces.ICashSessionRepository> cashSessionRepo,
        Mock<ERP.Domain.Modules.Caja.Interfaces.ICashRegisterRepository> cashRegisterRepo
    ) DefaultCashRegisterMocks(bool cashRegisterHasAccount = true, bool cashRegisterActive = true)
    {
        var session = ERP.Domain.Modules.Caja.Entities.CashSession.Open(
            TenantId,
            CompanyId,
            BranchId,
            UserId,
            CashRegisterId,
            "CAJA-01",
            "Caja Principal",
            Guid.NewGuid(),
            "001-001",
            0m,
            UserId
        );
        var cashSessionRepo = new Mock<ERP.Domain.Modules.Caja.Interfaces.ICashSessionRepository>();
        cashSessionRepo
            .Setup(r => r.GetByIdAsync(TenantId, CashSessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);

        var cashRegister = ERP.Domain.Modules.Caja.Entities.CashRegister.Create(
            TenantId,
            CompanyId,
            BranchId,
            "CAJA-01",
            "Caja Principal",
            UserId
        );
        if (cashRegisterHasAccount)
            cashRegister.SetAccountingAccount(CashAccountingAccountId, UserId);
        if (!cashRegisterActive)
            cashRegister.Disable(UserId);
        var cashRegisterRepo = new Mock<ERP.Domain.Modules.Caja.Interfaces.ICashRegisterRepository>();
        cashRegisterRepo
            .Setup(r => r.GetByIdAsync(TenantId, CashRegisterId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(cashRegister);

        return (cashSessionRepo, cashRegisterRepo);
    }

    /// <summary>ADR-033, Fase 2 P1: la condición de pago del borrador siempre está activa por
    /// defecto en estas pruebas (foco en fecha/stock/política fiscal, no en el guard de
    /// IsActive — cubierto en su propio test más abajo).</summary>
    private static Mock<IPaymentTermRepository> ActivePaymentTermRepoMock()
    {
        var repo = new Mock<IPaymentTermRepository>();
        repo.Setup(r => r.GetByIdAsync(TenantId, It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(PaymentTerm.Create(TenantId, "CONT", "Contado", 1, 0, UserId));
        return repo;
    }

    /// <summary>Factura de contado, sin punto de emisión (evita mockear captura de secuencial /
    /// emisión electrónica — fuera del alcance de esta regresión) y sin ítem/bodega en la línea
    /// (evita mockear validación de stock) — aísla exclusivamente la validación de fecha.
    /// `installments`/`daysBetween` &gt; su default de contado (1/0) simulan una condición de pago
    /// a crédito, para las pruebas de política fiscal de Consumidor Final.</summary>
    private static SalesInvoice CreateDraftInvoice(
        DateOnly issueDate,
        decimal unitPrice = 100m,
        int installments = 1,
        int daysBetween = 0
    )
    {
        var customer = CustomerSnapshot.Create("Cliente Test", "1710034065", "05");
        var paymentTerm = PaymentTermSnapshot.Create(
            PaymentTermId,
            installments > 1 || daysBetween > 0 ? "Crédito" : "Contado",
            installments: installments,
            daysBetween: daysBetween
        );

        var inv = SalesInvoice.CreateDraft(
            TenantId,
            CompanyId,
            BranchId,
            CustomerId,
            customer,
            invoiceNumber: "DRAFT-TEST",
            issueDate: issueDate,
            createdBy: UserId,
            paymentTerm: paymentTerm,
            cashSessionId: CashSessionId,
            emissionPointId: null
        );

        var line = SalesInvoiceDetail.Create(
            inv.Id,
            TenantId,
            "Producto Test",
            quantity: 1,
            unitPrice: unitPrice,
            vatCode: "10",
            uomCode: "UNIT"
        );
        inv.ReplaceLines(new[] { line }, UserId);

        var payment = SalesInvoicePayment.Create(
            inv.Id,
            TenantId,
            PaymentMethodId,
            "01",
            "Efectivo",
            ExpectedGrandTotal(unitPrice)
        );
        inv.ReplacePayments(new[] { payment }, UserId);

        return inv;
    }

    /// <summary>Replica SriTaxCalculator.Compute (IVA 15%, sin ICE, sin descuento) para no
    /// hardcodear el total esperado.</summary>
    private static decimal ExpectedGrandTotal(decimal unitPrice)
    {
        var taxable = unitPrice;
        var vat = Math.Round(taxable * 0.15m, 2, MidpointRounding.AwayFromZero);
        return Math.Round(taxable + vat, 2, MidpointRounding.AwayFromZero);
    }

    /// <summary>Consumidor Final: tipo 07, número estándar SRI — ver TaxIdentification.IsConsumidorFinal().</summary>
    private static BusinessPartner CreateConsumidorFinalBp() =>
        BusinessPartner.Create(
            TenantId,
            "07",
            "9999999999999",
            legalEntityTypeCode: 1,
            legalName: "Consumidor Final",
            createdBy: UserId
        );

    private static BusinessPartner CreateIdentifiedCustomerBp() =>
        BusinessPartner.Create(
            TenantId,
            "05",
            "1710034065",
            legalEntityTypeCode: 1,
            legalName: "Cliente Identificado",
            createdBy: UserId
        );

    /// <summary>Defaults que preservan el comportamiento vigente antes de CONFIG-DYNAMIC-OPERATIONS-02 (AllowSellWithoutStock=false → bloquea stock insuficiente, igual que siempre).</summary>
    private static OperationalPreferences DefaultOperationalPreferences(
        bool allowSellWithoutStock = false
    ) =>
        new(
            SalesPos: new SalesPosPreferences(
                true,
                false,
                true,
                0m,
                null,
                allowSellWithoutStock,
                false,
                null,
                null
            ),
            Cash: new CashPreferences(true, true, 0m, true, true, true),
            Purchases: new PurchasesPreferences(null, true, true, true, false),
            Inventory: new InventoryPreferences(false, true, false, 0m),
            Printing: new PrintingPreferences("AskBeforePrint", 1, "80mm", false, true, true, false),
            ElectronicDocuments: new ElectronicDocumentsPreferences(true, 3, true, true),
            Notifications: new NotificationsPreferences(true, false, "es")
        );

    private static (
        AuthorizeSalesInvoiceHandler handler,
        Mock<ICompanyClock> companyClock,
        Mock<ISalesReceivableRepository> receivableRepo
    ) BuildHandler(
        SalesInvoice inv,
        DateOnly companyToday,
        BusinessPartner? customerBp = null,
        SalesFiscalPolicyResult? fiscalPolicy = null,
        bool paymentMethodIsCreditAllowed = false,
        bool allowSellWithoutStock = false,
        Guid? activeBranchId = null,
        Mock<IPaymentMethodRepository>? paymentMethodRepoOverride = null,
        Mock<IPaymentMethodAccountRepository>? paymentMethodAccountRepoOverride = null,
        Mock<ICompanyBankAccountRepository>? bankAccountRepoOverride = null,
        Mock<IAccountRepository>? accountRepoOverride = null
    ) =>
        BuildHandler(
            inv,
            companyToday,
            out _,
            customerBp,
            fiscalPolicy,
            paymentMethodIsCreditAllowed,
            allowSellWithoutStock,
            activeBranchId,
            paymentMethodRepoOverride,
            paymentMethodAccountRepoOverride,
            bankAccountRepoOverride,
            accountRepoOverride
        );

    private static (
        AuthorizeSalesInvoiceHandler handler,
        Mock<ICompanyClock> companyClock,
        Mock<ISalesReceivableRepository> receivableRepo
    ) BuildHandler(
        SalesInvoice inv,
        DateOnly companyToday,
        out Mock<IPostingEngine> postingEngine,
        BusinessPartner? customerBp = null,
        SalesFiscalPolicyResult? fiscalPolicy = null,
        bool paymentMethodIsCreditAllowed = false,
        bool allowSellWithoutStock = false,
        Guid? activeBranchId = null,
        Mock<IPaymentMethodRepository>? paymentMethodRepoOverride = null,
        Mock<IPaymentMethodAccountRepository>? paymentMethodAccountRepoOverride = null,
        Mock<ICompanyBankAccountRepository>? bankAccountRepoOverride = null,
        Mock<IAccountRepository>? accountRepoOverride = null,
        Mock<ERP.Domain.Modules.Caja.Interfaces.ICashSessionRepository>? cashSessionRepoOverride =
            null,
        Mock<ERP.Domain.Modules.Caja.Interfaces.ICashRegisterRepository>? cashRegisterRepoOverride =
            null
    )
    {
        var preferences = new Mock<IOperationalPreferencesResolver>();
        preferences
            .Setup(p => p.ResolveAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(DefaultOperationalPreferences(allowSellWithoutStock));

        var repo = new Mock<ISalesInvoiceRepository>();
        repo.Setup(r => r.GetByIdAsync(TenantId, inv.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(inv);

        var tax = new Mock<ISriTaxResolver>();
        tax.Setup(t => t.GetVatRateWithNameAsync("10", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TaxRateResult(15m, "IVA 15%"));

        var stockRepo = new Mock<IStockRepository>();
        stockRepo
            .Setup(s => s.SaveChangesWithSequenceRetryAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        var edocRepo = new Mock<IElectronicDocumentRepository>();
        edocRepo
            .Setup(e =>
                e.GetBySourceAsync(TenantId, "Sales", inv.Id, It.IsAny<CancellationToken>())
            )
            .ReturnsAsync((ElectronicDocument?)null);

        var companyClock = new Mock<ICompanyClock>();
        companyClock
            .Setup(c => c.TodayAsync(CompanyId, TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(companyToday);

        var tenant = new Mock<ICurrentTenant>();
        tenant.Setup(t => t.TenantId).Returns(TenantId);
        var company = new Mock<ICurrentCompany>();
        company.Setup(c => c.CompanyId).Returns(CompanyId);
        var branch = new Mock<ICurrentBranch>();
        branch.Setup(b => b.BranchId).Returns(activeBranchId ?? BranchId);
        var user = new Mock<ICurrentUser>();
        user.Setup(u => u.UserId).Returns(UserId);

        var bpRepo = new Mock<IBusinessPartnerRepository>();
        bpRepo
            .Setup(r => r.GetByIdAsync(inv.CustomerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(customerBp);

        var fiscalPolicyResolver = new Mock<ISalesFiscalPolicyResolver>();
        fiscalPolicyResolver
            .Setup(r => r.GetEffectivePolicyAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(
                fiscalPolicy
                    ?? new SalesFiscalPolicyResult(
                        true,
                        ConsumerFinalPolicyDefaults.FallbackMaxAmount,
                        ConsumerFinalMaxAmountSource.Fallback,
                        null
                    )
            );

        // BUGFIX-SALES-CONSUMER-FINAL-CREDIT-BLOCK-01: la política de Consumidor Final también
        // debe mirar el método de pago usado (PaymentMethod.IsCreditAllowed), no solo el
        // PaymentTerm — ver comentario en AuthorizeSalesUseCases.cs.
        var paymentMethod = PaymentMethod.Create(
            TenantId,
            paymentMethodIsCreditAllowed ? "CREDITO" : "EFECTIVO",
            paymentMethodIsCreditAllowed ? "Crédito" : "Efectivo",
            requiresReference: false,
            isCreditAllowed: paymentMethodIsCreditAllowed,
            sortOrder: 1,
            createdBy: UserId
        );
        var paymentMethodRepo = paymentMethodRepoOverride ?? new Mock<IPaymentMethodRepository>();
        if (paymentMethodRepoOverride is null)
            paymentMethodRepo
                .Setup(r => r.GetByIdAsync(TenantId, PaymentMethodId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(paymentMethod);

        // SALES-TRANSFER-ACCOUNTING-CASH-VS-BANK-01 — default: la Company activa NO tiene ningún
        // PaymentMethodAccount configurado (mapa vacío) — mismo "gate de activación" que
        // AuthorizeSalesInvoiceHandler usa para companies que no han migrado: con el mapa vacío, el
        // guard fail-closed nunca se activa y el comportamiento es IDÉNTICO al previo a este
        // ticket, sin importar qué Guid de método de pago use cada test (varios usan Guids
        // distintos de la constante PaymentMethodId vía paymentMethodRepoOverride). El guard
        // fail-closed en sí se cubre en sus propios tests dedicados, con
        // paymentMethodAccountRepoOverride explícito.
        var paymentMethodAccountRepo =
            paymentMethodAccountRepoOverride ?? new Mock<IPaymentMethodAccountRepository>();
        if (paymentMethodAccountRepoOverride is null)
            paymentMethodAccountRepo
                .Setup(r =>
                    r.GetMapAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>())
                )
                .ReturnsAsync(
                    new Dictionary<
                        Guid,
                        ERP.Domain.Modules.Sales.Entities.PaymentMethodAccount
                    >()
                );

        var receivableRepo = new Mock<ISalesReceivableRepository>();

        // SALES-TRANSFER-BANK-ACCOUNT-01 — sin overrides, ningún test de este bloque usa
        // Transferencia (todos usan Efectivo por defecto), así que estos mocks nunca se invocan.
        var bankAccountRepo = bankAccountRepoOverride ?? new Mock<ICompanyBankAccountRepository>();
        var accountRepo = accountRepoOverride ?? new Mock<IAccountRepository>();
        if (accountRepoOverride is null)
            accountRepo
                .Setup(r =>
                    r.GetByIdAsync(
                        TenantId,
                        CompanyId,
                        It.IsAny<Guid>(),
                        It.IsAny<CancellationToken>()
                    )
                )
                .ReturnsAsync(ActiveAccount());

        // SALES-COLLECTION-ACCOUNT-SSOT-CLEANUP-01 — Efectivo (el método por defecto de este
        // bloque) resuelve su cuenta contable desde CashRegister — ver DefaultCashRegisterMocks.
        Mock<ERP.Domain.Modules.Caja.Interfaces.ICashSessionRepository> cashSessionRepo;
        Mock<ERP.Domain.Modules.Caja.Interfaces.ICashRegisterRepository> cashRegisterRepo;
        if (cashSessionRepoOverride is null || cashRegisterRepoOverride is null)
        {
            var defaults = DefaultCashRegisterMocks();
            cashSessionRepo = cashSessionRepoOverride ?? defaults.cashSessionRepo;
            cashRegisterRepo = cashRegisterRepoOverride ?? defaults.cashRegisterRepo;
        }
        else
        {
            cashSessionRepo = cashSessionRepoOverride;
            cashRegisterRepo = cashRegisterRepoOverride;
        }

        postingEngine = new Mock<IPostingEngine>();
        postingEngine
            .Setup(p =>
                p.IsAmountKindConfiguredAsync(
                    It.IsAny<Guid>(),
                    It.IsAny<Guid>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<ERP.Domain.Modules.Accounting.Enums.PostingAmountKind>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(true);

        var handler = new AuthorizeSalesInvoiceHandler(
            repo.Object,
            receivableRepo.Object,
            stockRepo.Object,
            ActivePaymentTermRepoMock().Object,
            tax.Object,
            Mock.Of<IDocumentSequenceRepository>(),
            Mock.Of<IEmissionPointRepository>(),
            Mock.Of<IEstablishmentRepository>(),
            edocRepo.Object,
            Mock.Of<ISalesInvoiceEmissionStrategyResolver>(),
            companyClock.Object,
            bpRepo.Object,
            fiscalPolicyResolver.Object,
            paymentMethodRepo.Object,
            paymentMethodAccountRepo.Object,
            bankAccountRepo.Object,
            cashSessionRepo.Object,
            cashRegisterRepo.Object,
            accountRepo.Object,
            postingEngine.Object,
            Mock.Of<ILogger<AuthorizeSalesInvoiceHandler>>(),
            tenant.Object,
            company.Object,
            branch.Object,
            user.Object,
            preferences.Object,
            PrecisionPolicyTestDouble.Mock()
        );

        return (handler, companyClock, receivableRepo);
    }

    /// <summary>
    /// CONFIG-DYNAMIC-OPERATIONS-02 (sales.pos.allow_sell_without_stock): factura de una sola
    /// línea CON ItemId/WarehouseId (a diferencia de CreateDraftInvoice, que los omite a propósito
    /// para no ejercer la validación de stock) y bodega con stock insuficiente para la cantidad
    /// pedida.
    /// </summary>
    private static (
        AuthorizeSalesInvoiceHandler handler,
        Mock<IStockRepository> stockRepo
    ) BuildHandlerWithInsufficientStock(
        SalesInvoice inv,
        bool allowSellWithoutStock,
        Guid? activeBranchId = null
    )
    {
        var repo = new Mock<ISalesInvoiceRepository>();
        repo.Setup(r => r.GetByIdAsync(TenantId, inv.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(inv);

        var tax = new Mock<ISriTaxResolver>();
        tax.Setup(t => t.GetVatRateWithNameAsync("10", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TaxRateResult(15m, "IVA 15%"));

        var stockRepo = new Mock<IStockRepository>();
        stockRepo
            .Setup(s => s.GetStockAsync(TenantId, It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ERP.Domain.Modules.Inventory.Entities.CurrentStock?)null); // sin stock (Quantity efectiva 0)
        stockRepo
            .Setup(s => s.SaveChangesWithSequenceRetryAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        var edocRepo = new Mock<IElectronicDocumentRepository>();
        edocRepo
            .Setup(e => e.GetBySourceAsync(TenantId, "Sales", inv.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync((ElectronicDocument?)null);

        var companyClock = new Mock<ICompanyClock>();
        companyClock
            .Setup(c => c.TodayAsync(CompanyId, TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(DateOnly.FromDateTime(DateTime.UtcNow));

        var tenant = new Mock<ICurrentTenant>();
        tenant.Setup(t => t.TenantId).Returns(TenantId);
        var company = new Mock<ICurrentCompany>();
        company.Setup(c => c.CompanyId).Returns(CompanyId);
        var branch = new Mock<ICurrentBranch>();
        branch.Setup(b => b.BranchId).Returns(activeBranchId ?? BranchId);
        var user = new Mock<ICurrentUser>();
        user.Setup(u => u.UserId).Returns(UserId);

        var bpRepo = new Mock<IBusinessPartnerRepository>();
        bpRepo
            .Setup(r => r.GetByIdAsync(inv.CustomerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((BusinessPartner?)null);

        var fiscalPolicyResolver = new Mock<ISalesFiscalPolicyResolver>();
        fiscalPolicyResolver
            .Setup(r => r.GetEffectivePolicyAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(
                new SalesFiscalPolicyResult(
                    true,
                    ConsumerFinalPolicyDefaults.FallbackMaxAmount,
                    ConsumerFinalMaxAmountSource.Fallback,
                    null
                )
            );

        var paymentMethodRepo = new Mock<IPaymentMethodRepository>();
        paymentMethodRepo
            .Setup(r => r.GetByIdAsync(TenantId, PaymentMethodId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(
                PaymentMethod.Create(
                    TenantId,
                    "EFECTIVO",
                    "Efectivo",
                    requiresReference: false,
                    isCreditAllowed: false,
                    sortOrder: 1,
                    createdBy: UserId
                )
            );

        // SALES-TRANSFER-ACCOUNTING-CASH-VS-BANK-01 — mapa vacío por default (Company sin migrar,
        // ver comentario equivalente en BuildHandler): comportamiento idéntico al previo a este
        // ticket, el guard fail-closed no se activa.
        var paymentMethodAccountRepo = new Mock<IPaymentMethodAccountRepository>();
        paymentMethodAccountRepo
            .Setup(r =>
                r.GetMapAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>())
            )
            .ReturnsAsync(
                new Dictionary<Guid, ERP.Domain.Modules.Sales.Entities.PaymentMethodAccount>()
            );

        var preferences = new Mock<IOperationalPreferencesResolver>();
        preferences
            .Setup(p => p.ResolveAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(DefaultOperationalPreferences(allowSellWithoutStock));

        // SALES-COLLECTION-ACCOUNT-SSOT-CLEANUP-01 — Efectivo (método por defecto de este bloque)
        // se resuelve antes de llegar a la validación de stock, así que necesita un mock funcional
        // (no un Mock.Of<> vacío) igual que BuildHandler.
        var accountRepo = new Mock<IAccountRepository>();
        accountRepo
            .Setup(r =>
                r.GetByIdAsync(TenantId, CompanyId, It.IsAny<Guid>(), It.IsAny<CancellationToken>())
            )
            .ReturnsAsync(ActiveAccount());
        var (cashSessionRepo, cashRegisterRepo) = DefaultCashRegisterMocks();

        var handler = new AuthorizeSalesInvoiceHandler(
            repo.Object,
            Mock.Of<ISalesReceivableRepository>(),
            stockRepo.Object,
            ActivePaymentTermRepoMock().Object,
            tax.Object,
            Mock.Of<IDocumentSequenceRepository>(),
            Mock.Of<IEmissionPointRepository>(),
            Mock.Of<IEstablishmentRepository>(),
            edocRepo.Object,
            Mock.Of<ISalesInvoiceEmissionStrategyResolver>(),
            companyClock.Object,
            bpRepo.Object,
            fiscalPolicyResolver.Object,
            paymentMethodRepo.Object,
            paymentMethodAccountRepo.Object,
            Mock.Of<ICompanyBankAccountRepository>(),
            cashSessionRepo.Object,
            cashRegisterRepo.Object,
            accountRepo.Object,
            Mock.Of<IPostingEngine>(),
            Mock.Of<ILogger<AuthorizeSalesInvoiceHandler>>(),
            tenant.Object,
            company.Object,
            branch.Object,
            user.Object,
            preferences.Object,
            PrecisionPolicyTestDouble.Mock()
        );

        return (handler, stockRepo);
    }

    private static SalesInvoice CreateDraftInvoiceWithStockTrackedLine(Guid itemId, Guid warehouseId)
    {
        var customer = CustomerSnapshot.Create("Cliente Test", "1710034065", "05");
        var paymentTerm = PaymentTermSnapshot.Create(PaymentTermId, "Contado", 1, 0);

        var inv = SalesInvoice.CreateDraft(
            TenantId,
            CompanyId,
            BranchId,
            CustomerId,
            customer,
            invoiceNumber: "DRAFT-TEST-STOCK",
            issueDate: DateOnly.FromDateTime(DateTime.UtcNow),
            createdBy: UserId,
            paymentTerm: paymentTerm,
            cashSessionId: CashSessionId,
            emissionPointId: null
        );

        var line = SalesInvoiceDetail.Create(
            inv.Id,
            TenantId,
            "Producto con stock",
            quantity: 5,
            unitPrice: 100m,
            vatCode: "10",
            uomCode: "UNIT",
            itemId: itemId,
            warehouseId: warehouseId
        );
        inv.ReplaceLines(new[] { line }, UserId);

        var payment = SalesInvoicePayment.Create(
            inv.Id,
            TenantId,
            PaymentMethodId,
            "01",
            "Efectivo",
            ExpectedGrandTotal(500m)
        );
        inv.ReplacePayments(new[] { payment }, UserId);

        return inv;
    }

    /// <summary>SALES-PRESENTATIONS-02: línea vendida por presentación (ej. 1 CAJA x12) —
    /// Quantity/UomCode reflejan lo vendido, QuantityInBaseUom/BaseUomCode lo que debe
    /// afectar stock/kardex.</summary>
    private static SalesInvoice CreateDraftInvoiceWithPresentationLine(
        Guid itemId,
        Guid warehouseId,
        decimal quantity,
        decimal conversionFactor
    )
    {
        var customer = CustomerSnapshot.Create("Cliente Test", "1710034065", "05");
        var paymentTerm = PaymentTermSnapshot.Create(PaymentTermId, "Contado", 1, 0);

        var inv = SalesInvoice.CreateDraft(
            TenantId,
            CompanyId,
            BranchId,
            CustomerId,
            customer,
            invoiceNumber: "DRAFT-TEST-PRESENTATION",
            issueDate: DateOnly.FromDateTime(DateTime.UtcNow),
            createdBy: UserId,
            paymentTerm: paymentTerm,
            cashSessionId: CashSessionId,
            emissionPointId: null
        );

        var line = SalesInvoiceDetail.Create(
            inv.Id,
            TenantId,
            "Caja x12",
            quantity: quantity,
            unitPrice: 120m,
            vatCode: "10",
            uomCode: "CAJA",
            itemId: itemId,
            warehouseId: warehouseId,
            conversionFactor: conversionFactor,
            baseUomCode: "UNIT"
        );
        inv.ReplaceLines(new[] { line }, UserId);

        var payment = SalesInvoicePayment.Create(
            inv.Id,
            TenantId,
            PaymentMethodId,
            "01",
            "Efectivo",
            ExpectedGrandTotal(quantity * 120m)
        );
        inv.ReplacePayments(new[] { payment }, UserId);

        return inv;
    }

    /// <summary>Variante de <see cref="BuildHandlerWithInsufficientStock"/> con stock disponible
    /// configurable — necesaria para probar el límite exacto en unidad base (SALES-PRESENTATIONS-02),
    /// a diferencia de la original que siempre simula stock 0.</summary>
    private static (
        AuthorizeSalesInvoiceHandler handler,
        Mock<IStockRepository> stockRepo
    ) BuildHandlerWithStockQuantity(
        SalesInvoice inv,
        decimal availableQuantity,
        Guid? activeBranchId = null
    )
    {
        var repo = new Mock<ISalesInvoiceRepository>();
        repo.Setup(r => r.GetByIdAsync(TenantId, inv.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(inv);

        var tax = new Mock<ISriTaxResolver>();
        tax.Setup(t => t.GetVatRateWithNameAsync("10", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TaxRateResult(15m, "IVA 15%"));

        var stock = ERP.Domain.Modules.Inventory.Entities.CurrentStock.Create(
            TenantId,
            Guid.NewGuid(),
            Guid.NewGuid(),
            UserId,
            CompanyId
        );
        stock.ApplyMovement(availableQuantity, UserId);

        var stockRepo = new Mock<IStockRepository>();
        stockRepo
            .Setup(s => s.GetStockAsync(TenantId, It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(stock);
        stockRepo
            .Setup(s => s.SaveChangesWithSequenceRetryAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        var edocRepo = new Mock<IElectronicDocumentRepository>();
        edocRepo
            .Setup(e => e.GetBySourceAsync(TenantId, "Sales", inv.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync((ElectronicDocument?)null);

        var companyClock = new Mock<ICompanyClock>();
        companyClock
            .Setup(c => c.TodayAsync(CompanyId, TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(DateOnly.FromDateTime(DateTime.UtcNow));

        var tenant = new Mock<ICurrentTenant>();
        tenant.Setup(t => t.TenantId).Returns(TenantId);
        var company = new Mock<ICurrentCompany>();
        company.Setup(c => c.CompanyId).Returns(CompanyId);
        var branch = new Mock<ICurrentBranch>();
        branch.Setup(b => b.BranchId).Returns(activeBranchId ?? BranchId);
        var user = new Mock<ICurrentUser>();
        user.Setup(u => u.UserId).Returns(UserId);

        var bpRepo = new Mock<IBusinessPartnerRepository>();
        bpRepo
            .Setup(r => r.GetByIdAsync(inv.CustomerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((BusinessPartner?)null);

        var fiscalPolicyResolver = new Mock<ISalesFiscalPolicyResolver>();
        fiscalPolicyResolver
            .Setup(r => r.GetEffectivePolicyAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(
                new SalesFiscalPolicyResult(
                    true,
                    ConsumerFinalPolicyDefaults.FallbackMaxAmount,
                    ConsumerFinalMaxAmountSource.Fallback,
                    null
                )
            );

        var paymentMethodRepo = new Mock<IPaymentMethodRepository>();
        paymentMethodRepo
            .Setup(r => r.GetByIdAsync(TenantId, PaymentMethodId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(
                PaymentMethod.Create(
                    TenantId,
                    "EFECTIVO",
                    "Efectivo",
                    requiresReference: false,
                    isCreditAllowed: false,
                    sortOrder: 1,
                    createdBy: UserId
                )
            );

        // SALES-TRANSFER-ACCOUNTING-CASH-VS-BANK-01 — mapa vacío por default (Company sin migrar,
        // ver comentario equivalente en BuildHandler): comportamiento idéntico al previo a este
        // ticket, el guard fail-closed no se activa.
        var paymentMethodAccountRepo = new Mock<IPaymentMethodAccountRepository>();
        paymentMethodAccountRepo
            .Setup(r =>
                r.GetMapAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>())
            )
            .ReturnsAsync(
                new Dictionary<Guid, ERP.Domain.Modules.Sales.Entities.PaymentMethodAccount>()
            );

        var preferences = new Mock<IOperationalPreferencesResolver>();
        preferences
            .Setup(p => p.ResolveAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(DefaultOperationalPreferences());

        // SALES-COLLECTION-ACCOUNT-SSOT-CLEANUP-01 — Efectivo (método por defecto de este bloque)
        // se resuelve antes de llegar a la validación de stock, así que necesita un mock funcional
        // (no un Mock.Of<> vacío) igual que BuildHandler.
        var accountRepo = new Mock<IAccountRepository>();
        accountRepo
            .Setup(r =>
                r.GetByIdAsync(TenantId, CompanyId, It.IsAny<Guid>(), It.IsAny<CancellationToken>())
            )
            .ReturnsAsync(ActiveAccount());
        var (cashSessionRepo, cashRegisterRepo) = DefaultCashRegisterMocks();

        var handler = new AuthorizeSalesInvoiceHandler(
            repo.Object,
            Mock.Of<ISalesReceivableRepository>(),
            stockRepo.Object,
            ActivePaymentTermRepoMock().Object,
            tax.Object,
            Mock.Of<IDocumentSequenceRepository>(),
            Mock.Of<IEmissionPointRepository>(),
            Mock.Of<IEstablishmentRepository>(),
            edocRepo.Object,
            Mock.Of<ISalesInvoiceEmissionStrategyResolver>(),
            companyClock.Object,
            bpRepo.Object,
            fiscalPolicyResolver.Object,
            paymentMethodRepo.Object,
            paymentMethodAccountRepo.Object,
            Mock.Of<ICompanyBankAccountRepository>(),
            cashSessionRepo.Object,
            cashRegisterRepo.Object,
            accountRepo.Object,
            Mock.Of<IPostingEngine>(),
            Mock.Of<ILogger<AuthorizeSalesInvoiceHandler>>(),
            tenant.Object,
            company.Object,
            branch.Object,
            user.Object,
            preferences.Object,
            PrecisionPolicyTestDouble.Mock()
        );

        return (handler, stockRepo);
    }

    // ── ADR-033, Fase 2 P1: condición de pago desactivada después del borrador ─────────────

    [Fact]
    public async Task Autorizar_bloqueado_si_condicion_de_pago_fue_desactivada_despues_del_borrador()
    {
        var inv = CreateDraftInvoice(new DateOnly(2026, 8, 1));

        var repo = new Mock<ISalesInvoiceRepository>();
        repo.Setup(r => r.GetByIdAsync(TenantId, inv.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(inv);

        var inactiveTerm = PaymentTerm.Create(TenantId, "30D", "30 días", 1, 30, UserId);
        inactiveTerm.Disable(UserId);
        var ptRepo = new Mock<IPaymentTermRepository>();
        ptRepo
            .Setup(r => r.GetByIdAsync(TenantId, It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(inactiveTerm);

        var tenant = new Mock<ICurrentTenant>();
        tenant.Setup(t => t.TenantId).Returns(TenantId);
        var company = new Mock<ICurrentCompany>();
        company.Setup(c => c.CompanyId).Returns(CompanyId);
        var branch = new Mock<ICurrentBranch>();
        branch.Setup(b => b.BranchId).Returns(BranchId);
        var user = new Mock<ICurrentUser>();
        user.Setup(u => u.UserId).Returns(UserId);

        var handler = new AuthorizeSalesInvoiceHandler(
            repo.Object,
            Mock.Of<ISalesReceivableRepository>(),
            Mock.Of<IStockRepository>(),
            ptRepo.Object,
            Mock.Of<ISriTaxResolver>(),
            Mock.Of<IDocumentSequenceRepository>(),
            Mock.Of<IEmissionPointRepository>(),
            Mock.Of<IEstablishmentRepository>(),
            Mock.Of<IElectronicDocumentRepository>(),
            Mock.Of<ISalesInvoiceEmissionStrategyResolver>(),
            Mock.Of<ICompanyClock>(),
            Mock.Of<IBusinessPartnerRepository>(),
            Mock.Of<ISalesFiscalPolicyResolver>(),
            Mock.Of<IPaymentMethodRepository>(),
            Mock.Of<IPaymentMethodAccountRepository>(),
            Mock.Of<ICompanyBankAccountRepository>(),
            Mock.Of<ERP.Domain.Modules.Caja.Interfaces.ICashSessionRepository>(),
            Mock.Of<ERP.Domain.Modules.Caja.Interfaces.ICashRegisterRepository>(),
            Mock.Of<IAccountRepository>(),
            Mock.Of<IPostingEngine>(),
            Mock.Of<ILogger<AuthorizeSalesInvoiceHandler>>(),
            tenant.Object,
            company.Object,
            branch.Object,
            user.Object,
            Mock.Of<IOperationalPreferencesResolver>(),
            PrecisionPolicyTestDouble.Mock()
        );

        var result = await handler.Handle(
            new AuthorizeSalesInvoiceCommand(inv.Id),
            CancellationToken.None
        );

        result.IsSuccess.Should().BeFalse();
    }

    // ── SALES-PRESENTATIONS-02: venta por presentación (caja x12) ──────────

    [Fact]
    public async Task Presentation_stock_insuficiente_en_unidad_base_bloquea_la_autorizacion()
    {
        // Stock 10 unidades base, venta 1 CAJA x12 = 12 unidades base requeridas → debe bloquear.
        var itemId = Guid.NewGuid();
        var warehouseId = Guid.NewGuid();
        var inv = CreateDraftInvoiceWithPresentationLine(
            itemId,
            warehouseId,
            quantity: 1m,
            conversionFactor: 12m
        );
        var (handler, stockRepo) = BuildHandlerWithStockQuantity(inv, availableQuantity: 10m);

        var result = await handler.Handle(
            new AuthorizeSalesInvoiceCommand(inv.Id),
            CancellationToken.None
        );

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("stock insuficiente");
        stockRepo.Verify(
            s => s.AppendMovementAsync(
                It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<Guid>(),
                It.IsAny<ERP.Domain.Modules.Inventory.Enums.StockMovementType>(), It.IsAny<decimal>(),
                It.IsAny<string>(), It.IsAny<DateOnly>(), It.IsAny<string>(), It.IsAny<Guid?>(),
                It.IsAny<string>(), It.IsAny<Guid>(), It.IsAny<decimal?>(), It.IsAny<Guid?>(),
                It.IsAny<Guid?>(), It.IsAny<CancellationToken>(), It.IsAny<Guid?>()
            ),
            Times.Never
        );
    }

    [Fact]
    public async Task Presentation_stock_suficiente_en_unidad_base_autoriza_y_descuenta_QuantityInBaseUom()
    {
        // Stock 20 unidades base, venta 1 CAJA x12 = 12 unidades base requeridas → debe autorizar
        // y descontar exactamente 12 en unidad base (nunca 1, la cantidad vendida cruda).
        var itemId = Guid.NewGuid();
        var warehouseId = Guid.NewGuid();
        var inv = CreateDraftInvoiceWithPresentationLine(
            itemId,
            warehouseId,
            quantity: 1m,
            conversionFactor: 12m
        );
        var (handler, stockRepo) = BuildHandlerWithStockQuantity(inv, availableQuantity: 20m);

        var result = await handler.Handle(
            new AuthorizeSalesInvoiceCommand(inv.Id),
            CancellationToken.None
        );

        result.IsSuccess.Should().BeTrue(result.Error);
        stockRepo.Verify(
            s => s.AppendMovementAsync(
                It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<Guid>(),
                ERP.Domain.Modules.Inventory.Enums.StockMovementType.SaleExit, -12m,
                "UNIT", It.IsAny<DateOnly>(), It.IsAny<string>(), It.IsAny<Guid?>(),
                It.IsAny<string>(), It.IsAny<Guid>(), It.IsAny<decimal?>(), It.IsAny<Guid?>(),
                It.IsAny<Guid?>(), It.IsAny<CancellationToken>(), It.IsAny<Guid?>()
            ),
            Times.Once
        );
    }

    [Fact]
    public async Task Rejects_insufficient_stock_by_default()
    {
        var itemId = Guid.NewGuid();
        var warehouseId = Guid.NewGuid();
        var inv = CreateDraftInvoiceWithStockTrackedLine(itemId, warehouseId);
        var (handler, stockRepo) = BuildHandlerWithInsufficientStock(inv, allowSellWithoutStock: false);

        var result = await handler.Handle(
            new AuthorizeSalesInvoiceCommand(inv.Id),
            CancellationToken.None
        );

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("stock insuficiente");
        stockRepo.Verify(
            s => s.AppendMovementAsync(
                It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<Guid>(),
                It.IsAny<ERP.Domain.Modules.Inventory.Enums.StockMovementType>(), It.IsAny<decimal>(),
                It.IsAny<string>(), It.IsAny<DateOnly>(), It.IsAny<string>(), It.IsAny<Guid?>(),
                It.IsAny<string>(), It.IsAny<Guid>(), It.IsAny<decimal?>(), It.IsAny<Guid?>(),
                It.IsAny<Guid?>(), It.IsAny<CancellationToken>(), It.IsAny<Guid?>()
            ),
            Times.Never
        );
    }

    [Fact]
    public async Task Allows_insufficient_stock_when_preference_enabled_and_still_posts_the_movement()
    {
        var itemId = Guid.NewGuid();
        var warehouseId = Guid.NewGuid();
        var inv = CreateDraftInvoiceWithStockTrackedLine(itemId, warehouseId);
        var (handler, stockRepo) = BuildHandlerWithInsufficientStock(inv, allowSellWithoutStock: true);

        var result = await handler.Handle(
            new AuthorizeSalesInvoiceCommand(inv.Id),
            CancellationToken.None
        );

        result.IsSuccess.Should().BeTrue(result.Error);
        stockRepo.Verify(
            s => s.AppendMovementAsync(
                TenantId, CompanyId, itemId, warehouseId,
                ERP.Domain.Modules.Inventory.Enums.StockMovementType.SaleExit, -5m,
                It.IsAny<string>(), It.IsAny<DateOnly>(), It.IsAny<string>(), It.IsAny<Guid?>(),
                It.IsAny<string>(), It.IsAny<Guid>(), It.IsAny<decimal?>(), It.IsAny<Guid?>(),
                It.IsAny<Guid?>(), It.IsAny<CancellationToken>(), It.IsAny<Guid?>()
            ),
            Times.Once
        );
    }

    [Fact]
    public async Task Rejects_future_issue_date()
    {
        var today = new DateOnly(2026, 7, 13);
        var inv = CreateDraftInvoice(issueDate: today.AddDays(4)); // reproduce factura 001-500-000000012
        var (handler, _, _) = BuildHandler(inv, today);

        var result = await handler.Handle(
            new AuthorizeSalesInvoiceCommand(inv.Id),
            CancellationToken.None
        );

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("no puede ser posterior");
        inv.Status.Should()
            .Be(
                Domain.Modules.Sales.Enums.SalesInvoiceStatus.Draft,
                "no debe autorizarse ni consumir secuencial"
            );
    }

    [Fact]
    public async Task Rejects_issue_date_older_than_90_days()
    {
        var today = new DateOnly(2026, 7, 13);
        var inv = CreateDraftInvoice(issueDate: today.AddDays(-91));
        var (handler, _, _) = BuildHandler(inv, today);

        var result = await handler.Handle(
            new AuthorizeSalesInvoiceCommand(inv.Id),
            CancellationToken.None
        );

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("demasiado antigua");
    }

    [Fact]
    public async Task Factura_con_IRBPNR_sin_PostingRuleLine_configurada_bloquea_con_mensaje_claro()
    {
        // TAX-LINE-SSOT-ICE-IRBPNR-01 Fase 5E — mismo criterio que el guard de Compras: si hay
        // IRBPNR y no existe PostingRuleLine configurada, la autorización debe bloquear ANTES de
        // capturar el secuencial SRI/persistir efectos.
        var today = new DateOnly(2026, 7, 13);
        var inv = CreateDraftInvoice(issueDate: today);
        var line = inv.Lines.Single();
        line.ReplaceTaxes(
            [
                SalesInvoiceDetailTax.Create(
                    line.Id,
                    TenantId,
                    "5",
                    "5001",
                    "IRBPNR",
                    0.1m,
                    ERP.Domain.Modules.SriCatalogs.Enums.SriTaxCalculationType.Specific,
                    line.TaxableBase,
                    2m,
                    SalesTaxSource.Calculated
                ),
            ]
        );

        var (handler, _, _) = BuildHandler(inv, today, out var postingEngine);
        postingEngine
            .Setup(p =>
                p.IsAmountKindConfiguredAsync(
                    It.IsAny<Guid>(),
                    It.IsAny<Guid>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    ERP.Domain.Modules.Accounting.Enums.PostingAmountKind.TaxIrbpnr,
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(false);

        var result = await handler.Handle(
            new AuthorizeSalesInvoiceCommand(inv.Id),
            CancellationToken.None
        );

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("IRBPNR");
        inv.Status.Should().Be(Domain.Modules.Sales.Enums.SalesInvoiceStatus.Draft);
    }

    [Fact]
    public async Task Accepts_issue_date_exactly_90_days_old_boundary()
    {
        var today = new DateOnly(2026, 7, 13);
        var inv = CreateDraftInvoice(issueDate: today.AddDays(-90));
        var (handler, _, _) = BuildHandler(inv, today);

        var result = await handler.Handle(
            new AuthorizeSalesInvoiceCommand(inv.Id),
            CancellationToken.None
        );

        result.IsSuccess.Should().BeTrue(result.Error);
        inv.Status.Should().Be(Domain.Modules.Sales.Enums.SalesInvoiceStatus.Authorized);
    }

    // ── SALES-HISTORICAL-PRICING-SNAPSHOT-01 ────────────────────────────

    [Fact]
    public async Task Authorize_congela_el_snapshot_historico_del_draft_sin_recalcularlo()
    {
        // El snapshot comercial histórico (bodega/costo/precio de lista/origen del precio y del
        // descuento) se fija exclusivamente en el Draft (SalesLineBuilder) — Authorize solo
        // recalcula impuestos (ApplyTaxes) y congela la línea (Freeze); nunca debe tocar estos
        // campos, aunque el maestro de precios/costos haya cambiado después de crear el borrador.
        var today = new DateOnly(2026, 7, 13);
        var inv = CreateDraftInvoice(issueDate: today);
        var line = inv.Lines.Single();
        var priceListId = Guid.NewGuid();
        line.SetHistoricalSnapshot(
            warehouseName: "Bodega Principal",
            unitCostAtSale: 6.5m,
            totalCostAtSale: 6.5m,
            listPriceAtSale: 100m,
            priceListId: priceListId,
            priceListName: "Lista General",
            pricingSource: "BaseSalePrice",
            discountSource: null,
            discountDescription: null
        );

        var (handler, _, _) = BuildHandler(inv, today);

        var result = await handler.Handle(
            new AuthorizeSalesInvoiceCommand(inv.Id),
            CancellationToken.None
        );

        result.IsSuccess.Should().BeTrue(result.Error);
        inv.Status.Should().Be(Domain.Modules.Sales.Enums.SalesInvoiceStatus.Authorized);

        var frozenLine = inv.Lines.Single();
        frozenLine.WarehouseName.Should().Be("Bodega Principal");
        frozenLine.UnitCostAtSale.Should().Be(6.5m);
        frozenLine.TotalCostAtSale.Should().Be(6.5m);
        frozenLine.ListPriceAtSale.Should().Be(100m);
        frozenLine.PriceListId.Should().Be(priceListId);
        frozenLine.PriceListName.Should().Be("Lista General");
        frozenLine.PricingSource.Should().Be("BaseSalePrice");
        frozenLine.DiscountSource.Should().BeNull();
        frozenLine.DiscountDescription.Should().BeNull();
    }

    [Fact]
    public async Task Authorize_de_una_linea_sin_snapshot_previo_no_fabrica_valores()
    {
        // Línea creada antes de esta fase (o sin dato disponible al capturar el Draft): todos los
        // campos históricos quedan null desde Create() — Authorize no debe rellenarlos con nada.
        var today = new DateOnly(2026, 7, 13);
        var inv = CreateDraftInvoice(issueDate: today);
        var (handler, _, _) = BuildHandler(inv, today);

        var result = await handler.Handle(
            new AuthorizeSalesInvoiceCommand(inv.Id),
            CancellationToken.None
        );

        result.IsSuccess.Should().BeTrue(result.Error);
        var frozenLine = inv.Lines.Single();
        frozenLine.WarehouseName.Should().BeNull();
        frozenLine.UnitCostAtSale.Should().BeNull();
        frozenLine.TotalCostAtSale.Should().BeNull();
        frozenLine.ListPriceAtSale.Should().BeNull();
        frozenLine.PriceListId.Should().BeNull();
        frozenLine.PriceListName.Should().BeNull();
        frozenLine.PricingSource.Should().BeNull();
        frozenLine.DiscountSource.Should().BeNull();
        frozenLine.DiscountDescription.Should().BeNull();
    }

    [Fact]
    public async Task Accepts_issue_date_equal_to_company_local_today()
    {
        // Reproduce factura 001-500-000000016: operación real a las 21:57 hora Ecuador
        // (todavía "hoy" localmente) — con la fecha empresarial correcta, esto ya no falla.
        var today = new DateOnly(2026, 7, 13);
        var inv = CreateDraftInvoice(issueDate: today);
        var (handler, companyClock, _) = BuildHandler(inv, today);

        var result = await handler.Handle(
            new AuthorizeSalesInvoiceCommand(inv.Id),
            CancellationToken.None
        );

        result.IsSuccess.Should().BeTrue(result.Error);
        inv.Status.Should().Be(Domain.Modules.Sales.Enums.SalesInvoiceStatus.Authorized);
        companyClock.Verify(
            c => c.TodayAsync(CompanyId, TenantId, It.IsAny<CancellationToken>()),
            Times.Once
        );
    }

    [Fact]
    public async Task Uses_company_clock_not_utc_now_for_date_comparison()
    {
        // Congela "hoy" en un valor arbitrario que NO coincide con DateTime.UtcNow.Date real —
        // si el handler alguna vez volviera a usar DateTime.UtcNow.Date en lugar de
        // ICompanyClock, esta factura (fechada "ayer" respecto al UTC real de la máquina de
        // pruebas) fallaría de forma intermitente según la hora en que corra la suite.
        var companyToday = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-1);
        var inv = CreateDraftInvoice(issueDate: companyToday);
        var (handler, companyClock, _) = BuildHandler(inv, companyToday);

        var result = await handler.Handle(
            new AuthorizeSalesInvoiceCommand(inv.Id),
            CancellationToken.None
        );

        result.IsSuccess.Should().BeTrue(result.Error);
        companyClock.Verify(
            c => c.TodayAsync(CompanyId, TenantId, It.IsAny<CancellationToken>()),
            Times.Once
        );
    }

    [Fact]
    public async Task Authorize_no_recibe_EmissionPointId_del_cliente()
    {
        typeof(AuthorizeSalesInvoiceCommand)
            .GetProperty("EmissionPointId")
            .Should()
            .BeNull("el cliente nunca debe poder sobreescribir el punto de emisión al autorizar");
    }

    [Fact]
    public async Task Authorize_preserva_el_CashSessionId_fijado_al_crear_el_borrador()
    {
        var today = new DateOnly(2026, 7, 13);
        var inv = CreateDraftInvoice(issueDate: today);
        var (handler, _, _) = BuildHandler(inv, today);

        var result = await handler.Handle(
            new AuthorizeSalesInvoiceCommand(inv.Id),
            CancellationToken.None
        );

        result.IsSuccess.Should().BeTrue(result.Error);
        inv.CashSessionId.Should().Be(CashSessionId);
    }

    // ── Política fiscal de Consumidor Final (COMPANY-SALES-FISCAL-POLICY-01) ──────────────

    [Fact]
    public async Task ConsumerFinal_contado_dentro_del_maximo_es_permitido()
    {
        var today = new DateOnly(2026, 7, 13);
        var inv = CreateDraftInvoice(issueDate: today, unitPrice: 40m); // total ≈ 46 < máximo 50
        var policy = new SalesFiscalPolicyResult(
            true,
            50.00m,
            ConsumerFinalMaxAmountSource.TaxRegimeDefault,
            "01"
        );
        var (handler, _, _) = BuildHandler(inv, today, CreateConsumidorFinalBp(), policy);

        var result = await handler.Handle(
            new AuthorizeSalesInvoiceCommand(inv.Id),
            CancellationToken.None
        );

        result.IsSuccess.Should().BeTrue(result.Error);
        inv.Status.Should().Be(Domain.Modules.Sales.Enums.SalesInvoiceStatus.Authorized);
    }

    [Fact]
    public async Task ConsumerFinal_contado_supera_el_maximo_es_bloqueado()
    {
        var today = new DateOnly(2026, 7, 13);
        var inv = CreateDraftInvoice(issueDate: today, unitPrice: 100m); // total ≈ 115 > máximo 50
        var policy = new SalesFiscalPolicyResult(
            true,
            50.00m,
            ConsumerFinalMaxAmountSource.TaxRegimeDefault,
            "01"
        );
        var (handler, _, _) = BuildHandler(inv, today, CreateConsumidorFinalBp(), policy);

        var result = await handler.Handle(
            new AuthorizeSalesInvoiceCommand(inv.Id),
            CancellationToken.None
        );

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("50.00");
        result.Error.Should().Contain("Consumidor Final");
        inv.Status.Should()
            .Be(Domain.Modules.Sales.Enums.SalesInvoiceStatus.Draft, "no debe autorizarse");
    }

    [Fact]
    public async Task ConsumerFinal_credito_es_bloqueado_siempre()
    {
        var today = new DateOnly(2026, 7, 13);
        // Monto bajo (dentro del máximo) para aislar que el bloqueo es por crédito, no por monto.
        var inv = CreateDraftInvoice(
            issueDate: today,
            unitPrice: 10m,
            installments: 3,
            daysBetween: 30
        );
        var policy = new SalesFiscalPolicyResult(
            true,
            50.00m,
            ConsumerFinalMaxAmountSource.TaxRegimeDefault,
            "01"
        );
        var (handler, _, _) = BuildHandler(inv, today, CreateConsumidorFinalBp(), policy);

        var result = await handler.Handle(
            new AuthorizeSalesInvoiceCommand(inv.Id),
            CancellationToken.None
        );

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("crédito");
        inv.Status.Should().Be(Domain.Modules.Sales.Enums.SalesInvoiceStatus.Draft);
    }

    /// <summary>
    /// Regresión de bug real: factura 001-001-000000016 (BUGFIX-SALES-CONSUMER-FINAL-CREDIT-BLOCK-01).
    /// Consumidor Final pagó con el método "Crédito" (PaymentMethod.IsCreditAllowed=true) sobre un
    /// PaymentTerm de Contado (installments=1, days=0) — el bloqueo original solo miraba el
    /// PaymentTerm y no vio esta señal, dejando pasar la autorización sin generar CxC pero
    /// registrando la venta como si fuera a crédito.
    /// </summary>
    [Fact]
    public async Task ConsumerFinal_metodo_pago_credito_es_bloqueado_aunque_paymentterm_sea_contado()
    {
        var today = new DateOnly(2026, 7, 13);
        // installments=1, daysBetween=0 (default) → PaymentTerm es Contado, como en la factura real.
        var inv = CreateDraftInvoice(issueDate: today, unitPrice: 10m);
        var policy = new SalesFiscalPolicyResult(
            true,
            50.00m,
            ConsumerFinalMaxAmountSource.TaxRegimeDefault,
            "01"
        );
        var (handler, _, _) = BuildHandler(
            inv,
            today,
            CreateConsumidorFinalBp(),
            policy,
            paymentMethodIsCreditAllowed: true
        );

        var result = await handler.Handle(
            new AuthorizeSalesInvoiceCommand(inv.Id),
            CancellationToken.None
        );

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("crédito");
        inv.Status.Should()
            .Be(
                Domain.Modules.Sales.Enums.SalesInvoiceStatus.Draft,
                "no debe autorizarse — reproduce y corrige la factura 001-001-000000016"
            );
    }

    [Fact]
    public async Task ConsumerFinal_contado_con_metodo_pago_contado_dentro_del_maximo_es_permitido()
    {
        var today = new DateOnly(2026, 7, 13);
        var inv = CreateDraftInvoice(issueDate: today, unitPrice: 10m); // total ≈ 11.5 < 50
        var policy = new SalesFiscalPolicyResult(
            true,
            50.00m,
            ConsumerFinalMaxAmountSource.TaxRegimeDefault,
            "01"
        );
        var (handler, _, _) = BuildHandler(
            inv,
            today,
            CreateConsumidorFinalBp(),
            policy,
            paymentMethodIsCreditAllowed: false
        );

        var result = await handler.Handle(
            new AuthorizeSalesInvoiceCommand(inv.Id),
            CancellationToken.None
        );

        result.IsSuccess.Should().BeTrue(result.Error);
        inv.Status.Should().Be(Domain.Modules.Sales.Enums.SalesInvoiceStatus.Authorized);
    }

    [Fact]
    public async Task ClienteIdentificado_credito_valido_es_permitido()
    {
        var today = new DateOnly(2026, 7, 13);
        var inv = CreateDraftInvoice(
            issueDate: today,
            unitPrice: 1000m, // supera el máximo de Consumidor Final — no debe importar aquí
            installments: 3,
            daysBetween: 30
        );
        var policy = new SalesFiscalPolicyResult(
            true,
            50.00m,
            ConsumerFinalMaxAmountSource.TaxRegimeDefault,
            "01"
        );
        var (handler, _, _) = BuildHandler(
            inv,
            today,
            CreateIdentifiedCustomerBp(),
            policy,
            paymentMethodIsCreditAllowed: true
        );

        var result = await handler.Handle(
            new AuthorizeSalesInvoiceCommand(inv.Id),
            CancellationToken.None
        );

        result.IsSuccess.Should().BeTrue(result.Error);
        inv.Status.Should().Be(Domain.Modules.Sales.Enums.SalesInvoiceStatus.Authorized);
    }

    [Fact]
    public async Task ClienteIdentificado_total_mayor_al_maximo_es_permitido()
    {
        var today = new DateOnly(2026, 7, 13);
        var inv = CreateDraftInvoice(issueDate: today, unitPrice: 1000m); // muy por encima de 50
        var policy = new SalesFiscalPolicyResult(
            true,
            50.00m,
            ConsumerFinalMaxAmountSource.TaxRegimeDefault,
            "01"
        );
        var (handler, _, _) = BuildHandler(inv, today, CreateIdentifiedCustomerBp(), policy);

        var result = await handler.Handle(
            new AuthorizeSalesInvoiceCommand(inv.Id),
            CancellationToken.None
        );

        result.IsSuccess.Should().BeTrue(result.Error);
        inv.Status.Should().Be(Domain.Modules.Sales.Enums.SalesInvoiceStatus.Authorized);
    }

    [Fact]
    public async Task ConsumerFinalMaxAmount_cero_bloquea_toda_venta_a_consumidor_final()
    {
        var today = new DateOnly(2026, 7, 13);
        var inv = CreateDraftInvoice(issueDate: today, unitPrice: 0.01m); // el total mínimo posible
        var policy = new SalesFiscalPolicyResult(
            true,
            0.00m,
            ConsumerFinalMaxAmountSource.Manual,
            "01"
        );
        var (handler, _, _) = BuildHandler(inv, today, CreateConsumidorFinalBp(), policy);

        var result = await handler.Handle(
            new AuthorizeSalesInvoiceCommand(inv.Id),
            CancellationToken.None
        );

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("0.00");
        inv.Status.Should().Be(Domain.Modules.Sales.Enums.SalesInvoiceStatus.Draft);
    }

    // ── Consistencia condición de pago ↔ método de pago (BUGFIX-SALES-CREDIT-PAYMENT-CONSISTENCY-01) ──

    [Fact]
    public async Task ClienteIdentificado_contado_con_efectivo_es_permitido_y_no_genera_CxC()
    {
        var today = new DateOnly(2026, 7, 13);
        var inv = CreateDraftInvoice(issueDate: today, unitPrice: 10m); // Contado, default efectivo
        var (handler, _, receivableRepo) = BuildHandler(
            inv,
            today,
            CreateIdentifiedCustomerBp(),
            paymentMethodIsCreditAllowed: false
        );

        var result = await handler.Handle(
            new AuthorizeSalesInvoiceCommand(inv.Id),
            CancellationToken.None
        );

        result.IsSuccess.Should().BeTrue(result.Error);
        inv.Status.Should().Be(Domain.Modules.Sales.Enums.SalesInvoiceStatus.Authorized);
        receivableRepo.Verify(
            r => r.AddAsync(It.IsAny<SalesReceivable>(), It.IsAny<CancellationToken>()),
            Times.Never,
            "Contado + Efectivo no debe generar cuenta por cobrar"
        );
    }

    [Fact]
    public async Task ClienteIdentificado_contado_con_metodo_credito_es_bloqueado()
    {
        var today = new DateOnly(2026, 7, 13);
        // installments=1, daysBetween=0 (default) → PaymentTerm Contado.
        var inv = CreateDraftInvoice(issueDate: today, unitPrice: 10m);
        var (handler, _, _) = BuildHandler(
            inv,
            today,
            CreateIdentifiedCustomerBp(),
            paymentMethodIsCreditAllowed: true
        );

        var result = await handler.Handle(
            new AuthorizeSalesInvoiceCommand(inv.Id),
            CancellationToken.None
        );

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("contado");
        result.Error.Should().Contain("crédito");
        inv.Status.Should().Be(Domain.Modules.Sales.Enums.SalesInvoiceStatus.Draft);
    }

    [Fact]
    public async Task ClienteIdentificado_credito_con_metodo_credito_es_permitido_y_genera_CxC()
    {
        var today = new DateOnly(2026, 7, 13);
        var inv = CreateDraftInvoice(
            issueDate: today,
            unitPrice: 10m,
            installments: 1,
            daysBetween: 30
        );
        var (handler, _, receivableRepo) = BuildHandler(
            inv,
            today,
            CreateIdentifiedCustomerBp(),
            paymentMethodIsCreditAllowed: true
        );

        var result = await handler.Handle(
            new AuthorizeSalesInvoiceCommand(inv.Id),
            CancellationToken.None
        );

        result.IsSuccess.Should().BeTrue(result.Error);
        inv.Status.Should().Be(Domain.Modules.Sales.Enums.SalesInvoiceStatus.Authorized);
        receivableRepo.Verify(
            r => r.AddAsync(It.IsAny<SalesReceivable>(), It.IsAny<CancellationToken>()),
            Times.Once,
            "Crédito + método Crédito debe generar cuenta por cobrar"
        );
    }

    [Fact]
    public async Task ClienteIdentificado_credito_con_solo_efectivo_es_bloqueado()
    {
        var today = new DateOnly(2026, 7, 13);
        var inv = CreateDraftInvoice(
            issueDate: today,
            unitPrice: 10m,
            installments: 1,
            daysBetween: 30
        );
        var (handler, _, receivableRepo) = BuildHandler(
            inv,
            today,
            CreateIdentifiedCustomerBp(),
            paymentMethodIsCreditAllowed: false
        );

        var result = await handler.Handle(
            new AuthorizeSalesInvoiceCommand(inv.Id),
            CancellationToken.None
        );

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("crédito");
        inv.Status.Should().Be(Domain.Modules.Sales.Enums.SalesInvoiceStatus.Draft);
        receivableRepo.Verify(
            r => r.AddAsync(It.IsAny<SalesReceivable>(), It.IsAny<CancellationToken>()),
            Times.Never
        );
    }

    [Fact]
    public async Task ConsumerFinal_metodo_credito_bloquea_con_mensaje_fiscal_no_generico()
    {
        // La regla de consistencia general también bloquearía este caso (Contado + método
        // Crédito), pero el mensaje que debe llegar al usuario es el fiscal de Consumidor Final
        // (prioridad), no el genérico de consistencia — ambos contienen "crédito", por eso se
        // verifica el texto completo, no solo una palabra.
        var today = new DateOnly(2026, 7, 13);
        var inv = CreateDraftInvoice(issueDate: today, unitPrice: 10m);
        var policy = new SalesFiscalPolicyResult(
            true,
            50.00m,
            ConsumerFinalMaxAmountSource.TaxRegimeDefault,
            "01"
        );
        var (handler, _, _) = BuildHandler(
            inv,
            today,
            CreateConsumidorFinalBp(),
            policy,
            paymentMethodIsCreditAllowed: true
        );

        var result = await handler.Handle(
            new AuthorizeSalesInvoiceCommand(inv.Id),
            CancellationToken.None
        );

        result.IsSuccess.Should().BeFalse();
        result.Error.Should()
            .Be(
                "Consumidor Final no puede registrar ventas a crédito. Seleccione un cliente identificado o cambie la condición de pago a contado."
            );
    }

    /// <summary>
    /// Hallazgo ALTO auditoría de aislamiento (Sales/Purchases cross-branch): AuthorizeSalesInvoiceCommand
    /// está marcado IBranchScopedRequest, pero ese marker solo exige sucursal activa autorizada — no
    /// garantiza que la factura cargada pertenezca a esa sucursal. Debe rechazar con NotFound (nunca
    /// revelar existencia cross-branch) cuando la factura pertenece a otra sucursal.
    /// </summary>
    [Fact]
    public async Task Factura_de_otra_sucursal_retorna_NotFound_y_no_la_autoriza()
    {
        var today = new DateOnly(2026, 7, 13);
        var inv = CreateDraftInvoice(issueDate: today);
        var otherBranchId = Guid.NewGuid();
        var (handler, _, _) = BuildHandler(
            inv,
            today,
            CreateIdentifiedCustomerBp(),
            activeBranchId: otherBranchId
        );

        var result = await handler.Handle(
            new AuthorizeSalesInvoiceCommand(inv.Id),
            CancellationToken.None
        );

        result.IsSuccess.Should().BeFalse();
        result.Code.Should().Be(ERP.Application.Common.ApiResponseCodes.Common.NotFound);
        inv.Status.Should().Be(SalesInvoiceStatus.Draft);
    }

    /// <summary>Misma factura, sucursal activa correcta (BranchId por defecto) — debe seguir funcionando.</summary>
    [Fact]
    public async Task Factura_de_la_misma_sucursal_sigue_autorizandose_correctamente()
    {
        var today = new DateOnly(2026, 7, 13);
        var inv = CreateDraftInvoice(issueDate: today);
        var (handler, _, _) = BuildHandler(inv, today, CreateIdentifiedCustomerBp());

        var result = await handler.Handle(
            new AuthorizeSalesInvoiceCommand(inv.Id),
            CancellationToken.None
        );

        result.IsSuccess.Should().BeTrue(result.Error);
        inv.Status.Should().Be(SalesInvoiceStatus.Authorized);
    }

    // ── ADR-033, Fase 4: CxC nace del cronograma persistido del documento ─────────────────

    [Fact]
    public async Task Contado_GeneraCronogramaPeroNoGeneraCxC()
    {
        var today = new DateOnly(2026, 7, 13);
        var inv = CreateDraftInvoice(issueDate: today, unitPrice: 10m); // Contado (installments=1, days=0)
        inv.GeneratePaymentSchedule();
        var (handler, _, receivableRepo) = BuildHandler(inv, today, CreateIdentifiedCustomerBp());

        var result = await handler.Handle(
            new AuthorizeSalesInvoiceCommand(inv.Id),
            CancellationToken.None
        );

        result.IsSuccess.Should().BeTrue(result.Error);
        inv.PaymentSchedules.Should().ContainSingle();
        receivableRepo.Verify(
            r => r.AddAsync(It.IsAny<SalesReceivable>(), It.IsAny<CancellationToken>()),
            Times.Never,
            "Contado no debe generar CxC aunque el cronograma exista"
        );
    }

    [Fact]
    public async Task Credito_UnaCuota_GeneraCxC_ConLosDatosExactosDelCronograma()
    {
        var today = new DateOnly(2026, 7, 13);
        var inv = CreateDraftInvoice(
            issueDate: today,
            unitPrice: 10m,
            installments: 1,
            daysBetween: 30
        );
        inv.GeneratePaymentSchedule();
        var expectedSchedule = inv.PaymentSchedules.Single();

        var (handler, _, receivableRepo) = BuildHandler(
            inv,
            today,
            CreateIdentifiedCustomerBp(),
            paymentMethodIsCreditAllowed: true
        );

        SalesReceivable? captured = null;
        receivableRepo
            .Setup(r => r.AddAsync(It.IsAny<SalesReceivable>(), It.IsAny<CancellationToken>()))
            .Callback<SalesReceivable, CancellationToken>((rx, _) => captured = rx)
            .Returns(Task.CompletedTask);

        var result = await handler.Handle(
            new AuthorizeSalesInvoiceCommand(inv.Id),
            CancellationToken.None
        );

        result.IsSuccess.Should().BeTrue(result.Error);
        captured.Should().NotBeNull();
        captured!.Installments.Should().ContainSingle();
        var installment = captured.Installments.Single();
        installment.InstallmentNumber.Should().Be(expectedSchedule.InstallmentNumber);
        installment.DueDate.Should().Be(expectedSchedule.DueDate);
        installment.Amount.Should().Be(expectedSchedule.Amount);
    }

    [Fact]
    public async Task Credito_VariasCuotas_GeneraCxC_ConFechasMontosYNumerosExactosDelCronograma()
    {
        var today = new DateOnly(2026, 7, 13);
        var inv = CreateDraftInvoice(
            issueDate: today,
            unitPrice: 100m, // GrandTotal = 100 (CreateDraftInvoice no aplica IVA a nivel de dominio)
            installments: 3,
            daysBetween: 30
        );
        // Cronograma manual con montos desiguales, distinto del prorrateo automático — prueba
        // que la autorización copia EXACTAMENTE el cronograma persistido, sin recalcular.
        inv.ReplacePaymentSchedule(
            new List<(int, DateOnly, decimal, string?)>
            {
                (1, today.AddDays(15), 40m, null),
                (2, today.AddDays(45), 35m, null),
                (3, today.AddDays(75), 25m, null),
            }
        );

        var (handler, _, receivableRepo) = BuildHandler(
            inv,
            today,
            CreateIdentifiedCustomerBp(),
            paymentMethodIsCreditAllowed: true
        );

        SalesReceivable? captured = null;
        receivableRepo
            .Setup(r => r.AddAsync(It.IsAny<SalesReceivable>(), It.IsAny<CancellationToken>()))
            .Callback<SalesReceivable, CancellationToken>((rx, _) => captured = rx)
            .Returns(Task.CompletedTask);

        var result = await handler.Handle(
            new AuthorizeSalesInvoiceCommand(inv.Id),
            CancellationToken.None
        );

        result.IsSuccess.Should().BeTrue(result.Error);
        captured.Should().NotBeNull();
        captured!.Installments.Should().HaveCount(3);
        var byNumber = captured.Installments.ToDictionary(i => i.InstallmentNumber);
        byNumber[1].DueDate.Should().Be(today.AddDays(15));
        byNumber[1].Amount.Should().Be(40m);
        byNumber[2].DueDate.Should().Be(today.AddDays(45));
        byNumber[2].Amount.Should().Be(35m);
        byNumber[3].DueDate.Should().Be(today.AddDays(75));
        byNumber[3].Amount.Should().Be(25m);
    }

    [Fact]
    public async Task Credito_SinCronogramaPersistido_UsaFallbackLegacyDeGenerateInstallments()
    {
        // Borrador "legacy" (previo a Fase 4): PaymentSchedules vacío al autorizar. Debe seguir
        // generando CxC vía el fallback defensivo (SalesReceivable.GenerateInstallments).
        var today = new DateOnly(2026, 7, 13);
        var inv = CreateDraftInvoice(
            issueDate: today,
            unitPrice: 10m,
            installments: 1,
            daysBetween: 30
        );
        inv.PaymentSchedules.Should().BeEmpty();

        var (handler, _, receivableRepo) = BuildHandler(
            inv,
            today,
            CreateIdentifiedCustomerBp(),
            paymentMethodIsCreditAllowed: true
        );

        var result = await handler.Handle(
            new AuthorizeSalesInvoiceCommand(inv.Id),
            CancellationToken.None
        );

        result.IsSuccess.Should().BeTrue(result.Error);
        receivableRepo.Verify(
            r => r.AddAsync(It.IsAny<SalesReceivable>(), It.IsAny<CancellationToken>()),
            Times.Once
        );
    }

    // ── SALES-SETTLEMENT-CREDIT-01 — CxC por saldo pendiente, no por el total ──

    [Fact]
    public async Task Pago_parcial_efectivo_mas_credito_genera_CxC_solo_por_el_saldo_pendiente()
    {
        var today = new DateOnly(2026, 7, 13);
        // installments=1, daysBetween=30 → PaymentTerm de crédito (isCreditByTerm = true).
        var inv = CreateDraftInvoice(
            issueDate: today,
            unitPrice: 100m,
            installments: 1,
            daysBetween: 30
        );
        // GrandTotal real: las líneas aún no tienen ApplyTaxes() aplicado en este punto
        // (CreateDraftInvoice no lo llama) — el Handler recalcula impuestos al inicio de Handle(),
        // así que el total contra el que debe cuadrar la suma de pagos es el que incluye IVA.
        var total = ExpectedGrandTotal(100m);
        var cashAmount = Math.Round(total * 0.6m, 2, MidpointRounding.AwayFromZero);
        var creditAmount = total - cashAmount;

        var cashMethodId = Guid.NewGuid();
        var creditMethodId = Guid.NewGuid();
        var cashPayment = SalesInvoicePayment.Create(
            inv.Id,
            TenantId,
            cashMethodId,
            "01",
            "Efectivo",
            cashAmount
        );
        var creditPayment = SalesInvoicePayment.Create(
            inv.Id,
            TenantId,
            creditMethodId,
            "20",
            "Crédito",
            creditAmount
        );
        // Pagos = total exacto (invariante de Authorize() sin tocar) — parte efectivo real, parte
        // método Crédito como marcador del saldo que queda pendiente de cobro.
        inv.ReplacePayments(new[] { cashPayment, creditPayment }, UserId);

        var paymentMethodRepo = new Mock<IPaymentMethodRepository>();
        paymentMethodRepo
            .Setup(r => r.GetByIdAsync(TenantId, cashMethodId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(
                PaymentMethod.Create(TenantId, "EFECTIVO", "Efectivo", false, false, 1, UserId)
            );
        paymentMethodRepo
            .Setup(r => r.GetByIdAsync(TenantId, creditMethodId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(
                PaymentMethod.Create(TenantId, "CREDITO", "Crédito", false, true, 2, UserId)
            );

        var (handler, _, receivableRepo) = BuildHandler(
            inv,
            today,
            CreateIdentifiedCustomerBp(),
            paymentMethodRepoOverride: paymentMethodRepo
        );

        SalesReceivable? captured = null;
        receivableRepo
            .Setup(r => r.AddAsync(It.IsAny<SalesReceivable>(), It.IsAny<CancellationToken>()))
            .Callback<SalesReceivable, CancellationToken>((r, _) => captured = r)
            .Returns(Task.CompletedTask);

        var result = await handler.Handle(
            new AuthorizeSalesInvoiceCommand(inv.Id),
            CancellationToken.None
        );

        result.IsSuccess.Should().BeTrue(result.Error);
        captured.Should().NotBeNull();
        captured!.OriginalAmount.Should().Be(creditAmount, "la CxC debe ser por el saldo pendiente, nunca por el total del documento cuando hubo abono parcial en efectivo");
        captured.OriginalAmount.Should().NotBe(total);
    }

    // ── SALES-INVOICE-FINAL-SEMANTIC-INTEGRITY-01 (Fase 6C) — SRI payment method coherente ──

    /// <summary>Mismo criterio que <see cref="CreateDraftInvoice"/> pero exponiendo
    /// <c>sriPaymentMethodCode</c> de cabecera para probar la sincronización al autorizar.</summary>
    private static SalesInvoice CreateDraftInvoiceWithHeaderSriCode(
        DateOnly issueDate,
        string? headerSriPaymentMethodCode,
        decimal unitPrice = 100m,
        int installments = 1,
        int daysBetween = 0
    )
    {
        var customer = CustomerSnapshot.Create("Cliente Test", "1710034065", "05");
        var paymentTerm = PaymentTermSnapshot.Create(
            PaymentTermId,
            installments > 1 || daysBetween > 0 ? "Crédito" : "Contado",
            installments: installments,
            daysBetween: daysBetween
        );

        var inv = SalesInvoice.CreateDraft(
            TenantId,
            CompanyId,
            BranchId,
            CustomerId,
            customer,
            invoiceNumber: "DRAFT-SRI-SYNC",
            issueDate: issueDate,
            createdBy: UserId,
            paymentTerm: paymentTerm,
            cashSessionId: CashSessionId,
            emissionPointId: null,
            sriPaymentMethodCode: headerSriPaymentMethodCode
        );

        var line = SalesInvoiceDetail.Create(
            inv.Id,
            TenantId,
            "Producto Test",
            quantity: 1,
            unitPrice: unitPrice,
            vatCode: "10",
            uomCode: "UNIT"
        );
        inv.ReplaceLines(new[] { line }, UserId);

        return inv;
    }

    [Fact]
    public async Task EfectivoUnico_SincronizaCabeceraConCodigoSriRealDelMetodo()
    {
        var today = new DateOnly(2026, 7, 13);
        var inv = CreateDraftInvoiceWithHeaderSriCode(today, headerSriPaymentMethodCode: "20", unitPrice: 100m);
        var total = ExpectedGrandTotal(100m);

        var cashMethodId = Guid.NewGuid();
        var payment = SalesInvoicePayment.Create(inv.Id, TenantId, cashMethodId, "01", "Efectivo", total);
        inv.ReplacePayments(new[] { payment }, UserId);

        var paymentMethodRepo = new Mock<IPaymentMethodRepository>();
        paymentMethodRepo
            .Setup(r => r.GetByIdAsync(TenantId, cashMethodId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(
                PaymentMethod.Create(
                    TenantId,
                    "EFECTIVO",
                    "Efectivo",
                    false,
                    false,
                    1,
                    UserId,
                    sriPaymentMethodCode: "01"
                )
            );

        var (handler, _, _) = BuildHandler(
            inv,
            today,
            CreateIdentifiedCustomerBp(),
            paymentMethodRepoOverride: paymentMethodRepo
        );

        var result = await handler.Handle(new AuthorizeSalesInvoiceCommand(inv.Id), CancellationToken.None);

        result.IsSuccess.Should().BeTrue(result.Error);
        inv.SriPaymentMethodCode.Should().Be("01", "el único pago real fue EFECTIVO — la cabecera debe reflejar su código SRI real, nunca el valor previo desalineado.");
    }

    [Fact]
    public async Task TransferenciaUnica_SincronizaCabeceraConCodigoSriRealDelMetodo()
    {
        var today = new DateOnly(2026, 7, 13);
        var inv = CreateDraftInvoiceWithHeaderSriCode(today, headerSriPaymentMethodCode: "01", unitPrice: 100m);
        var total = ExpectedGrandTotal(100m);

        var transferMethodId = Guid.NewGuid();
        var payment = SalesInvoicePayment.Create(inv.Id, TenantId, transferMethodId, "16", "Transferencia", total);
        inv.ReplacePayments(new[] { payment }, UserId);

        var paymentMethodRepo = new Mock<IPaymentMethodRepository>();
        paymentMethodRepo
            .Setup(r => r.GetByIdAsync(TenantId, transferMethodId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(
                PaymentMethod.Create(
                    TenantId,
                    "TRANSFERENCIA",
                    "Transferencia",
                    true,
                    false,
                    2,
                    UserId,
                    sriPaymentMethodCode: "16"
                )
            );

        var (handler, _, _) = BuildHandler(
            inv,
            today,
            CreateIdentifiedCustomerBp(),
            paymentMethodRepoOverride: paymentMethodRepo
        );

        var result = await handler.Handle(new AuthorizeSalesInvoiceCommand(inv.Id), CancellationToken.None);

        result.IsSuccess.Should().BeTrue(result.Error);
        inv.SriPaymentMethodCode.Should().Be("16");
    }

    [Fact]
    public async Task MixtoEfectivoTarjeta_NoFuerzaCabeceraAUnValorUnico()
    {
        var today = new DateOnly(2026, 7, 13);
        var inv = CreateDraftInvoiceWithHeaderSriCode(today, headerSriPaymentMethodCode: "05", unitPrice: 100m);
        var total = ExpectedGrandTotal(100m);
        var half = Math.Round(total / 2, 2, MidpointRounding.AwayFromZero);

        var cashMethodId = Guid.NewGuid();
        var cardMethodId = Guid.NewGuid();
        var cashPayment = SalesInvoicePayment.Create(inv.Id, TenantId, cashMethodId, "01", "Efectivo", half);
        var cardPayment = SalesInvoicePayment.Create(
            inv.Id,
            TenantId,
            cardMethodId,
            "19",
            "Tarjeta",
            total - half
        );
        inv.ReplacePayments(new[] { cashPayment, cardPayment }, UserId);

        var paymentMethodRepo = new Mock<IPaymentMethodRepository>();
        paymentMethodRepo
            .Setup(r => r.GetByIdAsync(TenantId, cashMethodId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(
                PaymentMethod.Create(TenantId, "EFECTIVO", "Efectivo", false, false, 1, UserId, sriPaymentMethodCode: "01")
            );
        paymentMethodRepo
            .Setup(r => r.GetByIdAsync(TenantId, cardMethodId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(
                PaymentMethod.Create(TenantId, "TARJETA", "Tarjeta", true, false, 2, UserId, sriPaymentMethodCode: "19")
            );

        var (handler, _, _) = BuildHandler(
            inv,
            today,
            CreateIdentifiedCustomerBp(),
            paymentMethodRepoOverride: paymentMethodRepo
        );

        var result = await handler.Handle(new AuthorizeSalesInvoiceCommand(inv.Id), CancellationToken.None);

        result.IsSuccess.Should().BeTrue(result.Error);
        inv.SriPaymentMethodCode.Should().Be("05", "pago mixto (2 métodos reales distintos) nunca debe forzar la cabecera a un único código — el XML ya resuelve el código por pago individual.");
    }

    [Fact]
    public async Task CreditoPuro_NoFuerzaCabeceraAUnCodigoDeDineroReal()
    {
        var today = new DateOnly(2026, 7, 13);
        // installments=1/daysBetween=0 (Contado) con un único pago vía método Crédito: cae en el
        // caso "crédito puro" de este guard — la consistencia PaymentTerm↔PaymentMethod se valida
        // en otro guard (paymentModality.IsConsistent), fuera del alcance de esta prueba.
        var inv = CreateDraftInvoiceWithHeaderSriCode(
            today,
            headerSriPaymentMethodCode: "01",
            unitPrice: 100m,
            installments: 1,
            daysBetween: 30
        );
        var total = ExpectedGrandTotal(100m);

        var creditMethodId = Guid.NewGuid();
        var payment = SalesInvoicePayment.Create(inv.Id, TenantId, creditMethodId, "20", "Crédito", total);
        inv.ReplacePayments(new[] { payment }, UserId);

        var paymentMethodRepo = new Mock<IPaymentMethodRepository>();
        paymentMethodRepo
            .Setup(r => r.GetByIdAsync(TenantId, creditMethodId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(
                PaymentMethod.Create(TenantId, "CREDITO", "Crédito", false, true, 3, UserId, sriPaymentMethodCode: "20")
            );

        var (handler, _, _) = BuildHandler(
            inv,
            today,
            CreateIdentifiedCustomerBp(),
            paymentMethodRepoOverride: paymentMethodRepo,
            paymentMethodIsCreditAllowed: true
        );

        var result = await handler.Handle(new AuthorizeSalesInvoiceCommand(inv.Id), CancellationToken.None);

        result.IsSuccess.Should().BeTrue(result.Error);
        // La sincronización de cabecera NUNCA se dispara para crédito puro — nunca debe reflejar
        // un código de "dinero real" cuando no entró dinero real.
        inv.SriPaymentMethodCode.Should().Be("01");
    }

    [Fact]
    public async Task SinMappingSriDelMetodoUnico_ConservaElFallbackDeCabecera()
    {
        var today = new DateOnly(2026, 7, 13);
        var inv = CreateDraftInvoiceWithHeaderSriCode(today, headerSriPaymentMethodCode: "01", unitPrice: 100m);
        var total = ExpectedGrandTotal(100m);

        var unmappedMethodId = Guid.NewGuid();
        var payment = SalesInvoicePayment.Create(inv.Id, TenantId, unmappedMethodId, "99", "Otro", total);
        inv.ReplacePayments(new[] { payment }, UserId);

        var paymentMethodRepo = new Mock<IPaymentMethodRepository>();
        paymentMethodRepo
            .Setup(r => r.GetByIdAsync(TenantId, unmappedMethodId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(
                PaymentMethod.Create(TenantId, "OTRO", "Otro", false, false, 4, UserId)
            // sriPaymentMethodCode omitido a propósito: método sin mapeo SRI configurado.
            );

        var (handler, _, _) = BuildHandler(
            inv,
            today,
            CreateIdentifiedCustomerBp(),
            paymentMethodRepoOverride: paymentMethodRepo
        );

        var result = await handler.Handle(new AuthorizeSalesInvoiceCommand(inv.Id), CancellationToken.None);

        result.IsSuccess.Should().BeTrue(result.Error);
        inv.SriPaymentMethodCode.Should().Be("01", "sin mapping SRI configurado para el único método usado, la cabecera conserva su fallback previo en vez de sincronizar un valor incorrecto o vaciarlo.");
    }

    // ── SALES-TRANSFER-ACCOUNTING-CASH-VS-BANK-01 ──────────────────────────────────────────

    [Fact]
    public async Task Tarjeta_sin_cuenta_configurada_bloquea_la_autorizacion_con_mensaje_claro()
    {
        // SALES-COLLECTION-ACCOUNT-SSOT-CLEANUP-01 — Tarjeta/Cheque son los ÚNICOS métodos que
        // siguen resolviendo su cuenta contable vía PaymentMethodAccount; sin una fila configurada
        // para la Company activa, la autorización debe rechazarse ANTES de capturar secuencial/
        // tocar inventario — nunca contabilizar silenciosamente contra Efectivo/Caja general.
        var today = new DateOnly(2026, 7, 13);
        var inv = CreateDraftInvoiceWithHeaderSriCode(today, headerSriPaymentMethodCode: "01", unitPrice: 100m);
        var total = ExpectedGrandTotal(100m);

        var cardMethodId = Guid.NewGuid();
        var payment = SalesInvoicePayment.Create(
            inv.Id,
            TenantId,
            cardMethodId,
            "19",
            "Tarjeta de Crédito",
            total
        );
        inv.ReplacePayments(new[] { payment }, UserId);

        var paymentMethodRepo = new Mock<IPaymentMethodRepository>();
        paymentMethodRepo
            .Setup(r => r.GetByIdAsync(TenantId, cardMethodId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(
                PaymentMethod.Create(
                    TenantId,
                    "TARJETA",
                    "Tarjeta de Crédito",
                    true,
                    false,
                    2,
                    UserId,
                    detailType: Domain.Modules.Sales.Enums.PaymentMethodDetailType.Card,
                    sriPaymentMethodCode: "19"
                )
            );

        // Mapa NO vacío (contiene una entrada de OTRO método) — activa el gate estricto de la
        // Company (ya "migró": al menos un PaymentMethodAccount configurado) sin incluir Tarjeta —
        // reproduce exactamente el caso reportado: cualquier método Tarjeta/Cheque sin cuenta
        // configurada bloquea, nunca cae en Caja general en silencio.
        var paymentMethodAccountRepo = new Mock<IPaymentMethodAccountRepository>();
        paymentMethodAccountRepo
            .Setup(r =>
                r.GetMapAsync(TenantId, CompanyId, It.IsAny<CancellationToken>())
            )
            .ReturnsAsync(
                new Dictionary<Guid, ERP.Domain.Modules.Sales.Entities.PaymentMethodAccount>
                {
                    [Guid.NewGuid()] = ERP.Domain.Modules.Sales.Entities.PaymentMethodAccount.Create(
                        TenantId,
                        CompanyId,
                        Guid.NewGuid(),
                        Guid.NewGuid(),
                        UserId
                    ),
                }
            );

        var (handler, _, _) = BuildHandler(
            inv,
            today,
            CreateIdentifiedCustomerBp(),
            paymentMethodRepoOverride: paymentMethodRepo,
            paymentMethodAccountRepoOverride: paymentMethodAccountRepo
        );

        var result = await handler.Handle(
            new AuthorizeSalesInvoiceCommand(inv.Id),
            CancellationToken.None
        );

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("Tarjeta de Crédito");
        result.Error.Should().Contain("cuenta contable");
        inv.Status.Should()
            .Be(
                Domain.Modules.Sales.Enums.SalesInvoiceStatus.Draft,
                "no debe autorizarse ni consumir secuencial sin cuenta contable configurada para el método de pago"
            );
    }

    [Fact]
    public async Task Efectivo_sin_PaymentMethodAccount_configurado_sigue_autorizando_por_compatibilidad()
    {
        // Compatibilidad con companies/entornos que no han corrido el backfill de
        // PaymentMethodAccountBackfillService: EFECTIVO sin fila configurada NO debe bloquear la
        // autorización (a diferencia de cualquier otro método) — el traductor de posting enruta
        // ese monto a la línea fija histórica de la PostingRule.
        var today = new DateOnly(2026, 7, 13);
        var inv = CreateDraftInvoice(issueDate: today, unitPrice: 100m);

        // Mapa vacío deliberado: ningún método tiene cuenta configurada — Company "sin migrar",
        // el gate fail-closed permanece apagado.
        var paymentMethodAccountRepo = new Mock<IPaymentMethodAccountRepository>();
        paymentMethodAccountRepo
            .Setup(r => r.GetMapAsync(TenantId, CompanyId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(
                new Dictionary<Guid, ERP.Domain.Modules.Sales.Entities.PaymentMethodAccount>()
            );

        var (handler, _, _) = BuildHandler(
            inv,
            today,
            paymentMethodAccountRepoOverride: paymentMethodAccountRepo
        );

        var result = await handler.Handle(
            new AuthorizeSalesInvoiceCommand(inv.Id),
            CancellationToken.None
        );

        result.IsSuccess.Should().BeTrue(result.Error);
        inv.Status.Should().Be(Domain.Modules.Sales.Enums.SalesInvoiceStatus.Authorized);
    }

    [Fact]
    public async Task Efectivo_con_cuenta_configurada_sigue_autorizando_igual_que_antes()
    {
        // Regresión: Efectivo con PaymentMethodAccount configurado (Caja general) debe seguir
        // autorizando exactamente igual que antes de este ticket.
        var today = new DateOnly(2026, 7, 13);
        var inv = CreateDraftInvoice(issueDate: today, unitPrice: 100m);
        var cajaGeneralAccountId = Guid.NewGuid();

        var paymentMethodAccountRepo = new Mock<IPaymentMethodAccountRepository>();
        paymentMethodAccountRepo
            .Setup(r => r.GetMapAsync(TenantId, CompanyId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(
                new Dictionary<Guid, ERP.Domain.Modules.Sales.Entities.PaymentMethodAccount>
                {
                    [PaymentMethodId] =
                        ERP.Domain.Modules.Sales.Entities.PaymentMethodAccount.Create(
                            TenantId,
                            CompanyId,
                            PaymentMethodId,
                            cajaGeneralAccountId,
                            UserId
                        ),
                }
            );

        var (handler, _, _) = BuildHandler(
            inv,
            today,
            paymentMethodAccountRepoOverride: paymentMethodAccountRepo
        );

        var result = await handler.Handle(
            new AuthorizeSalesInvoiceCommand(inv.Id),
            CancellationToken.None
        );

        result.IsSuccess.Should().BeTrue(result.Error);
        inv.Status.Should().Be(Domain.Modules.Sales.Enums.SalesInvoiceStatus.Authorized);
    }

    // ── SALES-TRANSFER-BANK-ACCOUNT-01 ──────────────────────────────────────────────────────

    private static Domain.Modules.Sales.Entities.PaymentMethod CreateTransferPaymentMethod(
        Guid id
    ) =>
        Domain.Modules.Sales.Entities.PaymentMethod.Create(
            TenantId,
            "TRANSFERENCIA",
            "Transferencia Bancaria",
            requiresReference: true,
            isCreditAllowed: false,
            sortOrder: 2,
            createdBy: UserId,
            detailType: Domain.Modules.Sales.Enums.PaymentMethodDetailType.Transfer
        );

    private static ERP.Domain.MasterData.Entities.Bank ActiveBank() =>
        ERP.Domain.MasterData.Entities.Bank.Create(TenantId, "PICHINCHA", "Banco Pichincha", null, UserId);

    private static ERP.Domain.Modules.Accounting.Entities.Account ActiveAccount(
        bool allowsPosting = true,
        bool isActive = true
    )
    {
        var account = ERP.Domain.Modules.Accounting.Entities.Account.Create(
            TenantId,
            CompanyId,
            ERP.Domain.Modules.Accounting.ValueObjects.AccountCode.Create("1.1.02.001"),
            "Banco Pichincha Cta. Cte.",
            null,
            ERP.Domain.Modules.Accounting.Enums.AccountType.Asset,
            ERP.Domain.Modules.Accounting.Enums.AccountNature.Debit,
            allowsPosting,
            UserId
        );
        if (!isActive)
            account.Disable(UserId);
        return account;
    }

    private static ERP.Domain.Modules.Finance.Entities.CompanyBankAccount ActiveBankAccount(
        Guid bankId,
        Guid accountingAccountId,
        bool isActive = true
    )
    {
        var bankAccount = ERP.Domain.Modules.Finance.Entities.CompanyBankAccount.Create(
            TenantId,
            CompanyId,
            bankId,
            ERP.Domain.Modules.Finance.Enums.BankAccountType.Checking,
            "2200123456",
            "Cuenta corriente Pichincha",
            accountingAccountId,
            UserId
        );
        if (!isActive)
            bankAccount.Disable(UserId);
        return bankAccount;
    }

    /// <summary>Factura de contado con un único pago Transferencia — con o sin TransferDetail según el test.</summary>
    private static (SalesInvoice inv, Guid transferMethodId) CreateDraftInvoiceWithTransferPayment(
        DateOnly issueDate,
        Guid? companyBankAccountId,
        decimal unitPrice = 100m
    )
    {
        var inv = CreateDraftInvoiceWithHeaderSriCode(issueDate, headerSriPaymentMethodCode: "16", unitPrice);
        var transferMethodId = Guid.NewGuid();
        var total = ExpectedGrandTotal(unitPrice);
        var payment = SalesInvoicePayment.Create(
            inv.Id,
            TenantId,
            transferMethodId,
            "16",
            "Transferencia Bancaria",
            total
        );
        if (companyBankAccountId.HasValue)
            payment.SetTransferDetail(
                PaymentTransferDetail.Create(
                    payment.Id,
                    companyBankAccountId.Value,
                    "TRX-001",
                    issueDate
                )
            );
        inv.ReplacePayments(new[] { payment }, UserId);
        return (inv, transferMethodId);
    }

    [Fact]
    public async Task Transferencia_sin_cuenta_bancaria_seleccionada_bloquea_la_autorizacion()
    {
        var today = new DateOnly(2026, 7, 13);
        var (inv, transferMethodId) = CreateDraftInvoiceWithTransferPayment(today, companyBankAccountId: null);

        var paymentMethodRepo = new Mock<IPaymentMethodRepository>();
        paymentMethodRepo
            .Setup(r => r.GetByIdAsync(TenantId, transferMethodId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateTransferPaymentMethod(transferMethodId));

        var (handler, _, _) = BuildHandler(
            inv,
            today,
            CreateIdentifiedCustomerBp(),
            paymentMethodRepoOverride: paymentMethodRepo
        );

        var result = await handler.Handle(new AuthorizeSalesInvoiceCommand(inv.Id), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("cuenta bancaria de destino");
        inv.Status.Should().Be(Domain.Modules.Sales.Enums.SalesInvoiceStatus.Draft);
    }

    [Fact]
    public async Task Transferencia_con_cuenta_bancaria_de_otra_empresa_bloquea_la_autorizacion()
    {
        var today = new DateOnly(2026, 7, 13);
        var bank = ActiveBank();
        var account = ActiveAccount();
        var bankAccount = ActiveBankAccount(bank.Id, account.Id);
        var (inv, transferMethodId) = CreateDraftInvoiceWithTransferPayment(today, bankAccount.Id);

        var paymentMethodRepo = new Mock<IPaymentMethodRepository>();
        paymentMethodRepo
            .Setup(r => r.GetByIdAsync(TenantId, transferMethodId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateTransferPaymentMethod(transferMethodId));

        var bankAccountRepo = new Mock<ICompanyBankAccountRepository>();
        bankAccountRepo
            .Setup(r => r.GetByIdAsync(TenantId, bankAccount.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync((ERP.Domain.Modules.Finance.Entities.CompanyBankAccount?)null); // no pertenece a esta empresa

        var (handler, _, _) = BuildHandler(
            inv,
            today,
            CreateIdentifiedCustomerBp(),
            paymentMethodRepoOverride: paymentMethodRepo,
            bankAccountRepoOverride: bankAccountRepo
        );

        var result = await handler.Handle(new AuthorizeSalesInvoiceCommand(inv.Id), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("no existe o no pertenece a esta empresa");
        inv.Status.Should().Be(Domain.Modules.Sales.Enums.SalesInvoiceStatus.Draft);
    }

    [Fact]
    public async Task Transferencia_con_cuenta_bancaria_inactiva_bloquea_la_autorizacion()
    {
        var today = new DateOnly(2026, 7, 13);
        var bank = ActiveBank();
        var account = ActiveAccount();
        var bankAccount = ActiveBankAccount(bank.Id, account.Id, isActive: false);
        var (inv, transferMethodId) = CreateDraftInvoiceWithTransferPayment(today, bankAccount.Id);

        var paymentMethodRepo = new Mock<IPaymentMethodRepository>();
        paymentMethodRepo
            .Setup(r => r.GetByIdAsync(TenantId, transferMethodId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateTransferPaymentMethod(transferMethodId));

        var bankAccountRepo = new Mock<ICompanyBankAccountRepository>();
        bankAccountRepo
            .Setup(r => r.GetByIdAsync(TenantId, bankAccount.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(bankAccount);

        var (handler, _, _) = BuildHandler(
            inv,
            today,
            CreateIdentifiedCustomerBp(),
            paymentMethodRepoOverride: paymentMethodRepo,
            bankAccountRepoOverride: bankAccountRepo
        );

        var result = await handler.Handle(new AuthorizeSalesInvoiceCommand(inv.Id), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("inactiva");
        inv.Status.Should().Be(Domain.Modules.Sales.Enums.SalesInvoiceStatus.Draft);
    }

    [Fact]
    public async Task Transferencia_con_cuenta_contable_inactiva_bloquea_la_autorizacion()
    {
        var today = new DateOnly(2026, 7, 13);
        var bank = ActiveBank();
        var account = ActiveAccount(isActive: false);
        var bankAccount = ActiveBankAccount(bank.Id, account.Id);
        var (inv, transferMethodId) = CreateDraftInvoiceWithTransferPayment(today, bankAccount.Id);

        var paymentMethodRepo = new Mock<IPaymentMethodRepository>();
        paymentMethodRepo
            .Setup(r => r.GetByIdAsync(TenantId, transferMethodId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateTransferPaymentMethod(transferMethodId));

        var bankAccountRepo = new Mock<ICompanyBankAccountRepository>();
        bankAccountRepo
            .Setup(r => r.GetByIdAsync(TenantId, bankAccount.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(bankAccount);

        var accountRepo = new Mock<IAccountRepository>();
        accountRepo
            .Setup(r => r.GetByIdAsync(TenantId, CompanyId, account.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(account);

        var (handler, _, _) = BuildHandler(
            inv,
            today,
            CreateIdentifiedCustomerBp(),
            paymentMethodRepoOverride: paymentMethodRepo,
            bankAccountRepoOverride: bankAccountRepo,
            accountRepoOverride: accountRepo
        );

        var result = await handler.Handle(new AuthorizeSalesInvoiceCommand(inv.Id), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("no es postable o está inactiva");
        inv.Status.Should().Be(Domain.Modules.Sales.Enums.SalesInvoiceStatus.Draft);
    }

    [Fact]
    public async Task Transferencia_valida_rutea_el_debito_a_la_cuenta_contable_de_la_cuenta_bancaria_seleccionada()
    {
        // No debe usar PaymentMethodAccount ni la línea fija de "Caja general" — el 100% del monto
        // debe quedar asignado a CompanyBankAccount.AccountingAccountId.
        var today = new DateOnly(2026, 7, 13);
        var bank = ActiveBank();
        var account = ActiveAccount();
        var bankAccount = ActiveBankAccount(bank.Id, account.Id);
        var (inv, transferMethodId) = CreateDraftInvoiceWithTransferPayment(today, bankAccount.Id, unitPrice: 100m);
        var total = ExpectedGrandTotal(100m);

        var paymentMethodRepo = new Mock<IPaymentMethodRepository>();
        paymentMethodRepo
            .Setup(r => r.GetByIdAsync(TenantId, transferMethodId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateTransferPaymentMethod(transferMethodId));

        var bankAccountRepo = new Mock<ICompanyBankAccountRepository>();
        bankAccountRepo
            .Setup(r => r.GetByIdAsync(TenantId, bankAccount.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(bankAccount);

        var accountRepo = new Mock<IAccountRepository>();
        accountRepo
            .Setup(r => r.GetByIdAsync(TenantId, CompanyId, account.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(account);

        // Mapa PaymentMethodAccount vacío a propósito: si el código incorrectamente cayera en esa
        // ruta para Transferencia, este test lo detectaría (el guard de "sin cuenta configurada"
        // rechazaría con OTRO mensaje distinto al esperado, o bloquearía cuando no debería).
        var paymentMethodAccountRepo = new Mock<IPaymentMethodAccountRepository>();
        paymentMethodAccountRepo
            .Setup(r => r.GetMapAsync(TenantId, CompanyId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(
                new Dictionary<Guid, ERP.Domain.Modules.Sales.Entities.PaymentMethodAccount>()
            );

        var (handler, _, _) = BuildHandler(
            inv,
            today,
            CreateIdentifiedCustomerBp(),
            paymentMethodRepoOverride: paymentMethodRepo,
            paymentMethodAccountRepoOverride: paymentMethodAccountRepo,
            bankAccountRepoOverride: bankAccountRepo,
            accountRepoOverride: accountRepo
        );

        var result = await handler.Handle(new AuthorizeSalesInvoiceCommand(inv.Id), CancellationToken.None);

        result.IsSuccess.Should().BeTrue(result.Error);
        inv.Status.Should().Be(Domain.Modules.Sales.Enums.SalesInvoiceStatus.Authorized);

        var authorizedEvent = inv.DomainEvents
            .OfType<Domain.Modules.Sales.Events.SalesInvoiceAuthorizedEvent>()
            .Single();
        authorizedEvent.CashByAccount.Should().ContainSingle();
        authorizedEvent.CashByAccount.Should().ContainKey(account.Id);
        authorizedEvent.CashByAccount[account.Id].Should().Be(total);
    }
}

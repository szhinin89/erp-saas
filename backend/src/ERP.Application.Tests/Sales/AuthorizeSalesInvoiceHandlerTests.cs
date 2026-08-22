using ERP.Application.Common;
using ERP.Application.Common.Services;
using ERP.Application.Modules.Sales.Services;
using ERP.Application.Modules.Sales.UseCases;
using ERP.Domain.Configuration.Interfaces;
using ERP.Domain.MasterData.Entities;
using ERP.Domain.MasterData.Interfaces;
using ERP.Domain.Modules.Company.Interfaces;
using ERP.Domain.Modules.ElectronicDocuments.Entities;
using ERP.Domain.Modules.ElectronicDocuments.Interfaces;
using ERP.Domain.Modules.Inventory.Interfaces;
using ERP.Domain.Modules.Sales.Entities;
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
        bool allowSellWithoutStock = false
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
        var paymentMethodRepo = new Mock<IPaymentMethodRepository>();
        paymentMethodRepo
            .Setup(r => r.GetByIdAsync(TenantId, PaymentMethodId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(paymentMethod);

        var receivableRepo = new Mock<ISalesReceivableRepository>();

        var handler = new AuthorizeSalesInvoiceHandler(
            repo.Object,
            receivableRepo.Object,
            stockRepo.Object,
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
            Mock.Of<ILogger<AuthorizeSalesInvoiceHandler>>(),
            tenant.Object,
            company.Object,
            user.Object,
            preferences.Object
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
    ) BuildHandlerWithInsufficientStock(SalesInvoice inv, bool allowSellWithoutStock)
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

        var preferences = new Mock<IOperationalPreferencesResolver>();
        preferences
            .Setup(p => p.ResolveAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(DefaultOperationalPreferences(allowSellWithoutStock));

        var handler = new AuthorizeSalesInvoiceHandler(
            repo.Object,
            Mock.Of<ISalesReceivableRepository>(),
            stockRepo.Object,
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
            Mock.Of<ILogger<AuthorizeSalesInvoiceHandler>>(),
            tenant.Object,
            company.Object,
            user.Object,
            preferences.Object
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
}

using ERP.Application.Common;
using ERP.Application.Common.Services;
using ERP.Application.Modules.Sales.Services;
using ERP.Application.Modules.Sales.UseCases;
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

    private static (
        AuthorizeSalesInvoiceHandler handler,
        Mock<ICompanyClock> companyClock
    ) BuildHandler(
        SalesInvoice inv,
        DateOnly companyToday,
        BusinessPartner? customerBp = null,
        SalesFiscalPolicyResult? fiscalPolicy = null
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
            Mock.Of<ILogger<AuthorizeSalesInvoiceHandler>>(),
            tenant.Object,
            company.Object,
            user.Object
        );

        return (handler, companyClock);
    }

    [Fact]
    public async Task Rejects_future_issue_date()
    {
        var today = new DateOnly(2026, 7, 13);
        var inv = CreateDraftInvoice(issueDate: today.AddDays(4)); // reproduce factura 001-500-000000012
        var (handler, _) = BuildHandler(inv, today);

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
        var (handler, _) = BuildHandler(inv, today);

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
        var (handler, _) = BuildHandler(inv, today);

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
        var (handler, companyClock) = BuildHandler(inv, today);

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
        var (handler, companyClock) = BuildHandler(inv, companyToday);

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
        var (handler, _) = BuildHandler(inv, today);

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
        var (handler, _) = BuildHandler(inv, today, CreateConsumidorFinalBp(), policy);

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
        var (handler, _) = BuildHandler(inv, today, CreateConsumidorFinalBp(), policy);

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
        var (handler, _) = BuildHandler(inv, today, CreateConsumidorFinalBp(), policy);

        var result = await handler.Handle(
            new AuthorizeSalesInvoiceCommand(inv.Id),
            CancellationToken.None
        );

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("crédito");
        inv.Status.Should().Be(Domain.Modules.Sales.Enums.SalesInvoiceStatus.Draft);
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
        var (handler, _) = BuildHandler(inv, today, CreateIdentifiedCustomerBp(), policy);

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
        var (handler, _) = BuildHandler(inv, today, CreateIdentifiedCustomerBp(), policy);

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
        var (handler, _) = BuildHandler(inv, today, CreateConsumidorFinalBp(), policy);

        var result = await handler.Handle(
            new AuthorizeSalesInvoiceCommand(inv.Id),
            CancellationToken.None
        );

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("0.00");
        inv.Status.Should().Be(Domain.Modules.Sales.Enums.SalesInvoiceStatus.Draft);
    }
}

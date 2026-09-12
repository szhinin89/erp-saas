using ERP.Application.Common.Services;
using ERP.Application.Modules.ElectronicDocuments.Services;
using ERP.Application.Modules.Sales.Services;
using ERP.Domain.Configuration.Entities;
using ERP.Domain.Configuration.Interfaces;
using ERP.Domain.Modules.Company.Entities;
using ERP.Domain.Modules.Company.Enums;
using ERP.Domain.Modules.Company.Interfaces;
using ERP.Domain.Modules.Sales.Entities;
using ERP.Domain.Modules.Sales.Enums;
using ERP.Domain.Modules.Sales.Interfaces;
using ERP.Domain.Modules.Sales.ValueObjects;
using ERP.Domain.Modules.SriCatalogs.Enums;
using FluentAssertions;
using Moq;

namespace ERP.Application.Tests.Sales;

/// <summary>
/// SALES-PRESENTATIONS-04 — verifica que SalesInvoiceElectronicDocumentDataProvider (RIDE/XML de
/// factura) usa Quantity/UomCode/UnitPrice (la presentación VISIBLE vendida), nunca
/// QuantityInBaseUom/BaseUomCode (exclusivos de stock/kardex, SALES-PRESENTATIONS-02). No existía
/// un archivo de tests dedicado para este proveedor — mismo patrón de fixture que
/// SalesReturnCreditNoteDataProviderTests.
/// </summary>
public sealed class SalesInvoiceElectronicDocumentDataProviderTests
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid CompanyId = Guid.NewGuid();
    private static readonly Guid BranchId = Guid.NewGuid();
    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly Guid CustomerId = Guid.NewGuid();
    private static readonly Guid PaymentTermId = Guid.NewGuid();
    private static readonly Guid CashSessionId = Guid.NewGuid();
    private static readonly Guid EmissionPointId = Guid.NewGuid();

    private sealed class Mocks
    {
        public Mock<ISalesInvoiceRepository> InvoiceRepo { get; } = new();
        public Mock<IEmissionPointRepository> EmissionPointRepo { get; } = new();
        public Mock<IEstablishmentRepository> EstablishmentRepo { get; } = new();
        public Mock<ICompanyRepository> CompanyRepo { get; } = new();
        public Mock<ISriSettingsRepository> SriSettingsRepo { get; } = new();
        public Mock<ISriDocTypeCatalogResolver> DocTypeResolver { get; } = new();
        public Mock<IPaymentMethodRepository> PaymentMethodRepo { get; } = new();

        public Mocks()
        {
            DocTypeResolver
                .Setup(r => r.IsActiveElectronicDocTypeAsync("01", It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);
            // Sin PaymentMethod.SriPaymentMethodCode configurado por defecto — las pruebas de este
            // fixture ejercitan el fallback al header invoice.SriPaymentMethodCode (comportamiento
            // preexistente antes de SALES-PAYMENT-METHOD-SRI-MAPPING-SSOT-01).
            PaymentMethodRepo
                .Setup(r =>
                    r.ListAsync(TenantId, It.IsAny<bool>(), It.IsAny<CancellationToken>())
                )
                .ReturnsAsync(Array.Empty<PaymentMethod>());
        }

        public SalesInvoiceElectronicDocumentDataProvider BuildProvider() =>
            new(
                InvoiceRepo.Object,
                EmissionPointRepo.Object,
                EstablishmentRepo.Object,
                CompanyRepo.Object,
                SriSettingsRepo.Object,
                DocTypeResolver.Object,
                PaymentMethodRepo.Object
            );

        public void SeedHappyPath(SalesInvoice invoice)
        {
            var establishment = Establishment.Create(
                TenantId,
                branchId: BranchId,
                CompanyId,
                code: "001",
                name: "Matriz",
                address: "Av. Principal 123",
                phone: null,
                isMain: true,
                createdBy: UserId
            );
            var emissionPoint = EmissionPoint.Create(
                TenantId,
                CompanyId,
                establishment.Id,
                code: "001",
                name: "PE-001",
                emissionType: EmissionType.Electronic,
                isDefault: true,
                createdBy: UserId
            );
            typeof(EmissionPoint)
                .GetProperty(nameof(EmissionPoint.Establishment))!
                .SetValue(emissionPoint, establishment);

            EmissionPointRepo
                .Setup(r => r.GetByIdAsync(EmissionPointId, TenantId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(emissionPoint);
            EstablishmentRepo
                .Setup(r => r.GetMainByCompanyAsync(TenantId, CompanyId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(establishment);
            CompanyRepo
                .Setup(r => r.GetByIdForTenantAsync(CompanyId, TenantId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(
                    Company.CreateManaged(TenantId, "1790012345001", "Empresa Test S.A.", createdBy: UserId)
                );
            SriSettingsRepo
                .Setup(r => r.GetByCompanyIdAsync(CompanyId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(
                    SriSettings.Create(
                        TenantId,
                        CompanyId,
                        environment: 1,
                        emissionType: 1,
                        wsdlUrl: "https://celcer.sri.gob.ec/comprobantes-electronicos-ws/RecepcionComprobantesOffline?wsdl",
                        createdBy: UserId
                    )
                );
            InvoiceRepo
                .Setup(r => r.GetByIdAsync(TenantId, invoice.Id, It.IsAny<CancellationToken>()))
                .ReturnsAsync(invoice);
        }
    }

    private static SalesInvoice BuildAuthorizedInvoiceWithPresentationLine()
    {
        var customer = CustomerSnapshot.Create("Cliente Test", "1710034065", "05");
        var paymentTerm = PaymentTermSnapshot.Create(PaymentTermId, "Contado", 1, 0);
        var inv = SalesInvoice.CreateDraft(
            TenantId,
            CompanyId,
            BranchId,
            CustomerId,
            customer,
            invoiceNumber: "001-001-000000050",
            issueDate: new DateOnly(2026, 7, 20),
            createdBy: UserId,
            paymentTerm: paymentTerm,
            cashSessionId: CashSessionId,
            sriPaymentMethodCode: "01",
            emissionPointId: EmissionPointId
        );

        // Venta de 1 CAJA x12 — Quantity visible = 1, UnitPrice por caja = 18, ConversionFactor
        // = 12 (solo relevante para stock/kardex, nunca para el XML fiscal).
        var line = SalesInvoiceDetail.Create(
            inv.Id,
            TenantId,
            "Caja x12",
            quantity: 1m,
            unitPrice: 18m,
            vatCode: "2",
            uomCode: "CAJA",
            snapshotSku: "15865",
            snapshotItemName: "Atún",
            conversionFactor: 12m,
            baseUomCode: "UNIT"
        );
        line.ApplyTaxes("2", 15m, "IVA 15%", null, 0m, null);
        inv.ReplaceLines(new[] { line }, UserId);

        var payment = SalesInvoicePayment.Create(
            inv.Id,
            TenantId,
            Guid.NewGuid(),
            "01",
            "Efectivo",
            line.TaxInclusiveTotal
        );
        inv.ReplacePayments(new[] { payment }, UserId);
        inv.Authorize(UserId);
        return inv;
    }

    [Fact]
    public async Task GetDataAsync_venta_por_presentacion_usa_Quantity_UomCode_visible_nunca_QuantityInBaseUom()
    {
        var invoice = BuildAuthorizedInvoiceWithPresentationLine();
        var m = new Mocks();
        m.SeedHappyPath(invoice);

        var provider = m.BuildProvider();
        var result = await provider.GetDataAsync(
            new ElectronicDocumentSourceReference(TenantId, CompanyId, invoice.Id)
        );

        result.IsSuccess.Should().BeTrue(result.Error);
        var detail = result.Value!.Details.Should().ContainSingle().Subject;
        // Quantity=1 (1 caja vendida), nunca 12 (QuantityInBaseUom) — el XML/RIDE fiscal debe
        // reflejar lo que el cliente compró, no la conversión interna de inventario.
        detail.Quantity.Should().Be(1m);
        detail.UnitPrice.Should().Be(18m);
        // Subtotal = Quantity * UnitPrice = 1 * 18 = 18 — nunca 216 (si se multiplicara además
        // por el ConversionFactor, doble-contando la conversión).
        detail.Subtotal.Should().Be(18m);
    }

    [Fact]
    public async Task GetDataAsync_sin_presentacion_preserva_comportamiento_actual()
    {
        var customer = CustomerSnapshot.Create("Cliente Test", "1710034065", "05");
        var paymentTerm = PaymentTermSnapshot.Create(PaymentTermId, "Contado", 1, 0);
        var invoice = SalesInvoice.CreateDraft(
            TenantId,
            CompanyId,
            BranchId,
            CustomerId,
            customer,
            invoiceNumber: "001-001-000000051",
            issueDate: new DateOnly(2026, 7, 20),
            createdBy: UserId,
            paymentTerm: paymentTerm,
            cashSessionId: CashSessionId,
            sriPaymentMethodCode: "01",
            emissionPointId: EmissionPointId
        );
        var line = SalesInvoiceDetail.Create(
            invoice.Id,
            TenantId,
            "Producto Test",
            quantity: 5m,
            unitPrice: 10m,
            vatCode: "2",
            uomCode: "UNIT",
            snapshotSku: "SKU-001"
        );
        line.ApplyTaxes("2", 15m, "IVA 15%", null, 0m, null);
        invoice.ReplaceLines(new[] { line }, UserId);
        var payment = SalesInvoicePayment.Create(
            invoice.Id,
            TenantId,
            Guid.NewGuid(),
            "01",
            "Efectivo",
            line.TaxInclusiveTotal
        );
        invoice.ReplacePayments(new[] { payment }, UserId);
        invoice.Authorize(UserId);

        var m = new Mocks();
        m.SeedHappyPath(invoice);

        var provider = m.BuildProvider();
        var result = await provider.GetDataAsync(
            new ElectronicDocumentSourceReference(TenantId, CompanyId, invoice.Id)
        );

        result.IsSuccess.Should().BeTrue(result.Error);
        var detail = result.Value!.Details.Should().ContainSingle().Subject;
        detail.Quantity.Should().Be(5m);
        detail.Subtotal.Should().Be(50m);
    }

    /// <summary>
    /// SRI-ELECTRONIC-DOCUMENTS-QA-FIX-01 — frontera normativa: los datos del emisor del XML
    /// (RUC/razón social) SIEMPRE vienen de <c>Company</c>/<c>SriSettings</c> (empresa emisora
    /// real, company-scoped), NUNCA de <c>SystemProviderSettings</c> (proveedor tecnológico
    /// global de AdminCore, ERP-CORE-CLOSEOUT-09). El constructor de
    /// <see cref="SalesInvoiceElectronicDocumentDataProvider"/> ni siquiera declara una
    /// dependencia de <c>ISystemProviderSettingsRepository</c> (ver <see cref="Mocks.BuildProvider"/>
    /// — 6 dependencias, ninguna es ese repositorio) — esta prueba deja constancia explícita de
    /// que <c>Issuer.TaxId</c>/<c>Issuer.LegalName</c> son exactamente los de la empresa activa,
    /// para que un cambio futuro que intente inyectar el proveedor de sistema como si fuera el
    /// emisor rompa aquí primero.
    /// </summary>
    [Fact]
    public async Task GetDataAsync_Issuer_proviene_de_Company_y_SriSettings_nunca_de_SystemProviderSettings()
    {
        var invoice = BuildAuthorizedInvoiceWithPresentationLine();
        var m = new Mocks();
        m.SeedHappyPath(invoice);

        var result = await m.BuildProvider()
            .GetDataAsync(new ElectronicDocumentSourceReference(TenantId, CompanyId, invoice.Id));

        result.IsSuccess.Should().BeTrue(result.Error);
        // Mismos valores sembrados en SeedHappyPath para la empresa activa (Company.CreateManaged
        // con RUC "1790012345001" / "Empresa Test S.A.") — nunca un RUC/razón social de proveedor
        // de sistema, que este provider ni siquiera puede leer.
        result.Value!.Issuer.TaxId.Should().Be("1790012345001");
        result.Value.Issuer.LegalName.Should().Be("Empresa Test S.A.");
    }

    // ── TAX-LINE-SSOT-ICE-IRBPNR-01 (ADR-032 §3.5, Subfase 5C) ──────────────────────────────

    private static SalesInvoiceDetail CreateAuthorizedLine(
        SalesInvoice invoice,
        decimal quantity,
        decimal unitPrice,
        string? iceCode = null,
        decimal iceRate = 0m,
        string? iceName = null,
        SriTaxCalculationType iceCalculationType = SriTaxCalculationType.Percentage,
        decimal? iceExactAmount = null
    )
    {
        var line = SalesInvoiceDetail.Create(
            invoice.Id,
            TenantId,
            "Línea test",
            quantity,
            unitPrice,
            vatCode: "2",
            uomCode: "UNIT",
            snapshotSku: "SKU-001",
            iceCode: iceCode
        );
        line.ApplyTaxes(
            "2",
            15m,
            "IVA 15%",
            iceCode,
            iceRate,
            iceName,
            iceCalculationType,
            iceExactAmount
        );
        return line;
    }

    private static void AddIrbpnr(SalesInvoiceDetail line, string code, decimal? rate, decimal amount) =>
        line.ReplaceTaxes(
            [
                SalesInvoiceDetailTax.Create(
                    line.Id,
                    TenantId,
                    "5",
                    code,
                    "IRBPNR",
                    rate,
                    SriTaxCalculationType.Specific,
                    line.TaxableBase,
                    amount,
                    SalesTaxSource.Calculated
                ),
            ]
        );

    private static SalesInvoice CreateAuthorizedInvoice(string invoiceNumber, SalesInvoiceDetail line)
    {
        var customer = CustomerSnapshot.Create("Cliente Test", "1710034065", "05");
        var paymentTerm = PaymentTermSnapshot.Create(PaymentTermId, "Contado", 1, 0);
        var invoice = SalesInvoice.CreateDraft(
            TenantId,
            CompanyId,
            BranchId,
            CustomerId,
            customer,
            invoiceNumber: invoiceNumber,
            issueDate: new DateOnly(2026, 7, 20),
            createdBy: UserId,
            paymentTerm: paymentTerm,
            cashSessionId: CashSessionId,
            sriPaymentMethodCode: "01",
            emissionPointId: EmissionPointId
        );
        invoice.ReplaceLines(new[] { line }, UserId);
        var payment = SalesInvoicePayment.Create(
            invoice.Id,
            TenantId,
            Guid.NewGuid(),
            "01",
            "Efectivo",
            line.TaxInclusiveTotal
        );
        invoice.ReplacePayments(new[] { payment }, UserId);
        invoice.Authorize(UserId);
        return invoice;
    }

    [Fact]
    public async Task Factura_solo_IVA_produce_unicamente_el_impuesto_VAT()
    {
        var line = CreateAuthorizedLine(
            SalesInvoice.CreateDraft(
                TenantId, CompanyId, BranchId, CustomerId,
                CustomerSnapshot.Create("Cliente Test", "1710034065", "05"),
                "001-001-000000060", new DateOnly(2026, 7, 20), UserId,
                PaymentTermSnapshot.Create(PaymentTermId, "Contado", 1, 0),
                cashSessionId: CashSessionId, sriPaymentMethodCode: "01", emissionPointId: EmissionPointId
            ),
            quantity: 5m,
            unitPrice: 10m
        );
        var invoice = CreateAuthorizedInvoice("001-001-000000060", line);
        var m = new Mocks();
        m.SeedHappyPath(invoice);

        var result = await m.BuildProvider()
            .GetDataAsync(new ElectronicDocumentSourceReference(TenantId, CompanyId, invoice.Id));

        result.IsSuccess.Should().BeTrue(result.Error);
        var detail = result.Value!.Details.Should().ContainSingle().Subject;
        detail.Taxes.Should().ContainSingle();
        var vat = detail.Taxes.Single();
        vat.TaxCode.Should().Be("VAT");
        vat.TaxPercentageCode.Should().Be("2");
        vat.TaxableBase.Should().Be(50m);
        vat.TaxRate.Should().Be(15m);
        vat.TaxAmount.Should().Be(7.5m);
    }

    [Fact]
    public async Task Factura_con_IVA_mas_ICE_Percentage_produce_VAT_e_ICE()
    {
        var draft = SalesInvoice.CreateDraft(
            TenantId, CompanyId, BranchId, CustomerId,
            CustomerSnapshot.Create("Cliente Test", "1710034065", "05"),
            "001-001-000000061", new DateOnly(2026, 7, 20), UserId,
            PaymentTermSnapshot.Create(PaymentTermId, "Contado", 1, 0),
            cashSessionId: CashSessionId, sriPaymentMethodCode: "01", emissionPointId: EmissionPointId
        );
        var line = CreateAuthorizedLine(
            draft,
            quantity: 1m,
            unitPrice: 100m,
            iceCode: "3041",
            iceRate: 10m,
            iceName: "ICE 10%"
        );
        var invoice = CreateAuthorizedInvoice("001-001-000000061", line);
        var m = new Mocks();
        m.SeedHappyPath(invoice);

        var result = await m.BuildProvider()
            .GetDataAsync(new ElectronicDocumentSourceReference(TenantId, CompanyId, invoice.Id));

        result.IsSuccess.Should().BeTrue(result.Error);
        var detail = result.Value!.Details.Should().ContainSingle().Subject;
        detail.Taxes.Should().HaveCount(2);
        detail.Taxes.Should().Contain(t => t.TaxCode == "VAT" && t.TaxPercentageCode == "2");
        var ice = detail.Taxes.Should().ContainSingle(t => t.TaxCode == "ICE").Subject;
        ice.TaxPercentageCode.Should().Be("3041");
        ice.TaxRate.Should().Be(10m);
        ice.TaxAmount.Should().Be(10m); // 100 * 10/100
    }

    [Fact]
    public async Task Factura_con_IVA_mas_ICE_Specific_conserva_el_monto_exacto()
    {
        var draft = SalesInvoice.CreateDraft(
            TenantId, CompanyId, BranchId, CustomerId,
            CustomerSnapshot.Create("Cliente Test", "1710034065", "05"),
            "001-001-000000062", new DateOnly(2026, 7, 20), UserId,
            PaymentTermSnapshot.Create(PaymentTermId, "Contado", 1, 0),
            cashSessionId: CashSessionId, sriPaymentMethodCode: "01", emissionPointId: EmissionPointId
        );
        var line = CreateAuthorizedLine(
            draft,
            quantity: 10m,
            unitPrice: 100m,
            iceCode: "3053",
            iceName: "ICE Específico",
            iceCalculationType: SriTaxCalculationType.Specific,
            iceExactAmount: 5m
        );
        var invoice = CreateAuthorizedInvoice("001-001-000000062", line);
        var m = new Mocks();
        m.SeedHappyPath(invoice);

        var result = await m.BuildProvider()
            .GetDataAsync(new ElectronicDocumentSourceReference(TenantId, CompanyId, invoice.Id));

        result.IsSuccess.Should().BeTrue(result.Error);
        var detail = result.Value!.Details.Should().ContainSingle().Subject;
        var ice = detail.Taxes.Should().ContainSingle(t => t.TaxCode == "ICE").Subject;
        ice.TaxPercentageCode.Should().Be("3053");
        ice.TaxAmount.Should().Be(5m, "un ICE específico nunca se recalcula desde una tarifa porcentual");
    }

    [Fact]
    public async Task Factura_con_IVA_mas_IRBPNR_produce_VAT_e_IRBPNR()
    {
        var draft = SalesInvoice.CreateDraft(
            TenantId, CompanyId, BranchId, CustomerId,
            CustomerSnapshot.Create("Cliente Test", "1710034065", "05"),
            "001-001-000000063", new DateOnly(2026, 7, 20), UserId,
            PaymentTermSnapshot.Create(PaymentTermId, "Contado", 1, 0),
            cashSessionId: CashSessionId, sriPaymentMethodCode: "01", emissionPointId: EmissionPointId
        );
        var line = CreateAuthorizedLine(draft, quantity: 24m, unitPrice: 0.5837m);
        AddIrbpnr(line, "5001", 0.02m, 0.48m);
        var invoice = CreateAuthorizedInvoice("001-001-000000063", line);
        var m = new Mocks();
        m.SeedHappyPath(invoice);

        var result = await m.BuildProvider()
            .GetDataAsync(new ElectronicDocumentSourceReference(TenantId, CompanyId, invoice.Id));

        result.IsSuccess.Should().BeTrue(result.Error);
        var detail = result.Value!.Details.Should().ContainSingle().Subject;
        detail.Taxes.Should().HaveCount(2);
        detail.Taxes.Should().Contain(t => t.TaxCode == "VAT");
        var irbpnr = detail.Taxes.Should().ContainSingle(t => t.TaxCode == "IRBPNR").Subject;
        irbpnr.TaxPercentageCode.Should().Be("5001");
        irbpnr.TaxAmount.Should().Be(0.48m);
    }

    [Fact]
    public async Task Factura_con_IVA_ICE_e_IRBPNR_produce_los_3_impuestos()
    {
        var draft = SalesInvoice.CreateDraft(
            TenantId, CompanyId, BranchId, CustomerId,
            CustomerSnapshot.Create("Cliente Test", "1710034065", "05"),
            "001-001-000000064", new DateOnly(2026, 7, 20), UserId,
            PaymentTermSnapshot.Create(PaymentTermId, "Contado", 1, 0),
            cashSessionId: CashSessionId, sriPaymentMethodCode: "01", emissionPointId: EmissionPointId
        );
        var line = CreateAuthorizedLine(
            draft,
            quantity: 1m,
            unitPrice: 100m,
            iceCode: "3041",
            iceRate: 10m,
            iceName: "ICE 10%"
        );
        AddIrbpnr(line, "5001", 0.02m, 0.02m);
        var invoice = CreateAuthorizedInvoice("001-001-000000064", line);
        var m = new Mocks();
        m.SeedHappyPath(invoice);

        var result = await m.BuildProvider()
            .GetDataAsync(new ElectronicDocumentSourceReference(TenantId, CompanyId, invoice.Id));

        result.IsSuccess.Should().BeTrue(result.Error);
        var detail = result.Value!.Details.Should().ContainSingle().Subject;
        detail.Taxes.Should().HaveCount(3);
        detail.Taxes.Select(t => t.TaxCode).Should().BeEquivalentTo(new[] { "VAT", "ICE", "IRBPNR" });
        // Orden VAT → ICE → IRBPNR, mismo orden en que se sincronizan/fijan en el dominio.
        detail.Taxes.Select(t => t.TaxCode).Should().ContainInOrder("VAT", "ICE", "IRBPNR");
    }

    // ── SALES-PAYMENT-METHOD-SRI-MAPPING-SSOT-01 ────────────────────────────────────────────

    private static SalesInvoice BuildAuthorizedInvoiceWithPayments(
        string invoiceNumber,
        string headerSriPaymentMethodCode,
        params (Guid PaymentMethodId, string Code, string Name, decimal Amount)[] payments
    )
    {
        var customer = CustomerSnapshot.Create("Cliente Test", "1710034065", "05");
        var paymentTerm = PaymentTermSnapshot.Create(PaymentTermId, "Contado", 1, 0);
        var invoice = SalesInvoice.CreateDraft(
            TenantId,
            CompanyId,
            BranchId,
            CustomerId,
            customer,
            invoiceNumber: invoiceNumber,
            issueDate: new DateOnly(2026, 7, 20),
            createdBy: UserId,
            paymentTerm: paymentTerm,
            cashSessionId: CashSessionId,
            sriPaymentMethodCode: headerSriPaymentMethodCode,
            emissionPointId: EmissionPointId
        );
        var line = SalesInvoiceDetail.Create(
            invoice.Id,
            TenantId,
            "Producto Test",
            quantity: 1m,
            unitPrice: payments.Sum(p => p.Amount),
            vatCode: "2",
            uomCode: "UNIT",
            snapshotSku: "SKU-001"
        );
        line.ApplyTaxes("2", 0m, "IVA 0%", null, 0m, null);
        invoice.ReplaceLines(new[] { line }, UserId);

        var salesPayments = payments
            .Select(p => SalesInvoicePayment.Create(invoice.Id, TenantId, p.PaymentMethodId, p.Code, p.Name, p.Amount))
            .ToList();
        invoice.ReplacePayments(salesPayments, UserId);
        invoice.Authorize(UserId);
        return invoice;
    }

    [Fact]
    public async Task GetDataAsync_efectivo_deriva_SRI_01_desde_mapeo_de_PaymentMethod()
    {
        var cash = PaymentMethod.Create(
            TenantId, "EFECTIVO", "Efectivo", false, false, 1, UserId,
            sriPaymentMethodCode: "01"
        );
        var invoice = BuildAuthorizedInvoiceWithPayments(
            "001-001-000000070",
            headerSriPaymentMethodCode: "20",
            (cash.Id, "EFECTIVO", "Efectivo", 10m)
        );
        var m = new Mocks();
        m.SeedHappyPath(invoice);
        m.PaymentMethodRepo
            .Setup(r => r.ListAsync(TenantId, It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { cash });

        var result = await m.BuildProvider()
            .GetDataAsync(new ElectronicDocumentSourceReference(TenantId, CompanyId, invoice.Id));

        result.IsSuccess.Should().BeTrue(result.Error);
        result.Value!.Payments.Should().ContainSingle(p => p.PaymentMethodCode == "01");
    }

    [Fact]
    public async Task GetDataAsync_tarjeta_credito_deriva_SRI_19()
    {
        var card = PaymentMethod.Create(
            TenantId, "TARJETA", "Tarjeta de Crédito", true, false, 2, UserId,
            sriPaymentMethodCode: "19"
        );
        var invoice = BuildAuthorizedInvoiceWithPayments(
            "001-001-000000071",
            headerSriPaymentMethodCode: "01",
            (card.Id, "TARJETA", "Tarjeta de Crédito", 10m)
        );
        var m = new Mocks();
        m.SeedHappyPath(invoice);
        m.PaymentMethodRepo
            .Setup(r => r.ListAsync(TenantId, It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { card });

        var result = await m.BuildProvider()
            .GetDataAsync(new ElectronicDocumentSourceReference(TenantId, CompanyId, invoice.Id));

        result.IsSuccess.Should().BeTrue(result.Error);
        result.Value!.Payments.Should().ContainSingle(p => p.PaymentMethodCode == "19");
    }

    [Fact]
    public async Task GetDataAsync_pagos_mixtos_generan_formaPago_separada_por_linea()
    {
        var cash = PaymentMethod.Create(
            TenantId, "EFECTIVO", "Efectivo", false, false, 1, UserId,
            sriPaymentMethodCode: "01"
        );
        var transfer = PaymentMethod.Create(
            TenantId, "TRANSFERENCIA", "Transferencia", true, false, 3, UserId,
            sriPaymentMethodCode: "20"
        );
        var invoice = BuildAuthorizedInvoiceWithPayments(
            "001-001-000000072",
            headerSriPaymentMethodCode: "01",
            (cash.Id, "EFECTIVO", "Efectivo", 6m),
            (transfer.Id, "TRANSFERENCIA", "Transferencia", 4m)
        );
        var m = new Mocks();
        m.SeedHappyPath(invoice);
        m.PaymentMethodRepo
            .Setup(r => r.ListAsync(TenantId, It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { cash, transfer });

        var result = await m.BuildProvider()
            .GetDataAsync(new ElectronicDocumentSourceReference(TenantId, CompanyId, invoice.Id));

        result.IsSuccess.Should().BeTrue(result.Error);
        result.Value!.Payments.Should().HaveCount(2);
        result.Value.Payments.Should().Contain(p => p.PaymentMethodCode == "01" && p.Amount == 6m);
        result.Value.Payments.Should().Contain(p => p.PaymentMethodCode == "20" && p.Amount == 4m);
    }

    [Fact]
    public async Task GetDataAsync_sin_mapeo_propio_cae_al_default_de_empresa()
    {
        // CREDITO sin SriPaymentMethodCode configurado (parámetro omitido = null).
        var credito = PaymentMethod.Create(TenantId, "CREDITO", "Crédito", false, true, 5, UserId);
        var invoice = BuildAuthorizedInvoiceWithPayments(
            "001-001-000000073",
            headerSriPaymentMethodCode: "01",
            (credito.Id, "CREDITO", "Crédito", 10m)
        );
        var m = new Mocks();
        m.SeedHappyPath(invoice);
        m.PaymentMethodRepo
            .Setup(r => r.ListAsync(TenantId, It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { credito });

        var result = await m.BuildProvider()
            .GetDataAsync(new ElectronicDocumentSourceReference(TenantId, CompanyId, invoice.Id));

        result.IsSuccess.Should().BeTrue(result.Error);
        result.Value!.Payments.Should().ContainSingle(p => p.PaymentMethodCode == "01");
    }

    [Fact]
    public async Task GetDataAsync_sin_mapeo_ni_default_de_empresa_bloquea_emision()
    {
        var otro = PaymentMethod.Create(TenantId, "OTRO", "Otro", false, false, 9, UserId);
        var invoice = BuildAuthorizedInvoiceWithPayments(
            "001-001-000000074",
            headerSriPaymentMethodCode: "01",
            (otro.Id, "OTRO", "Otro", 10m)
        );
        // Fuerza header vacío tras construir la factura (SriPaymentMethodCode ya validado como
        // obligatorio en CreateDraft — se simula el escenario límite vía reflexión, único punto
        // donde el dominio no puede bloquearlo en construcción porque el header sí es obligatorio
        // desde el alta; lo que se está probando es la resiliencia del data provider si esa
        // invariante llegara a romperse).
        typeof(SalesInvoice)
            .GetProperty(nameof(SalesInvoice.SriPaymentMethodCode))!
            .SetValue(invoice, null);

        var m = new Mocks();
        m.SeedHappyPath(invoice);
        m.PaymentMethodRepo
            .Setup(r => r.ListAsync(TenantId, It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { otro });

        var result = await m.BuildProvider()
            .GetDataAsync(new ElectronicDocumentSourceReference(TenantId, CompanyId, invoice.Id));

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("Otro");
    }

    [Fact]
    public async Task GetDataAsync_no_cruza_tenant_al_resolver_PaymentMethod()
    {
        var otherTenantCash = PaymentMethod.Create(
            Guid.NewGuid(), "EFECTIVO", "Efectivo", false, false, 1, UserId,
            sriPaymentMethodCode: "99"
        );
        var invoice = BuildAuthorizedInvoiceWithPayments(
            "001-001-000000075",
            headerSriPaymentMethodCode: "01",
            (otherTenantCash.Id, "EFECTIVO", "Efectivo", 10m)
        );
        var m = new Mocks();
        m.SeedHappyPath(invoice);
        // PaymentMethodRepo.Setup por defecto en Mocks() ya devuelve vacío para TenantId real de
        // la factura — solo configuramos explícitamente un tenant distinto, simulando un
        // PaymentMethod con el mismo Id pero perteneciente a OTRO tenant (nunca debe filtrarse).

        var result = await m.BuildProvider()
            .GetDataAsync(new ElectronicDocumentSourceReference(TenantId, CompanyId, invoice.Id));

        result.IsSuccess.Should().BeTrue(result.Error);
        // Nunca "99" (del PaymentMethod de otro tenant) — cae al default de empresa "01" porque
        // ListAsync(TenantId real) devuelve vacío (setup por defecto de Mocks()).
        result.Value!.Payments.Should().ContainSingle(p => p.PaymentMethodCode == "01");
    }

    [Fact]
    public async Task Linea_sin_IRBPNR_no_genera_nodo_IRBPNR_falso()
    {
        var draft = SalesInvoice.CreateDraft(
            TenantId, CompanyId, BranchId, CustomerId,
            CustomerSnapshot.Create("Cliente Test", "1710034065", "05"),
            "001-001-000000065", new DateOnly(2026, 7, 20), UserId,
            PaymentTermSnapshot.Create(PaymentTermId, "Contado", 1, 0),
            cashSessionId: CashSessionId, sriPaymentMethodCode: "01", emissionPointId: EmissionPointId
        );
        var line = CreateAuthorizedLine(
            draft,
            quantity: 1m,
            unitPrice: 100m,
            iceCode: "3041",
            iceRate: 10m,
            iceName: "ICE 10%"
        );
        var invoice = CreateAuthorizedInvoice("001-001-000000065", line);
        var m = new Mocks();
        m.SeedHappyPath(invoice);

        var result = await m.BuildProvider()
            .GetDataAsync(new ElectronicDocumentSourceReference(TenantId, CompanyId, invoice.Id));

        result.IsSuccess.Should().BeTrue(result.Error);
        var detail = result.Value!.Details.Should().ContainSingle().Subject;
        detail.Taxes.Should().NotContain(t => t.TaxCode == "IRBPNR");
    }
}

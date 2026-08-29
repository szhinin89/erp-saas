using ERP.Application.Common.Services;
using ERP.Application.Modules.ElectronicDocuments.Services;
using ERP.Application.Modules.Sales.Services;
using ERP.Domain.Configuration.Entities;
using ERP.Domain.Configuration.Interfaces;
using ERP.Domain.Modules.Company.Entities;
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
/// P0-01 Fase 8/10 — SalesReturnCreditNoteDataProvider: construcción del modelo común
/// (ElectronicDocumentData) a partir de una SalesReturn autorizada. Mismo patrón de test que
/// se usaría para <c>SalesInvoiceElectronicDocumentDataProvider</c> (no existe un archivo de
/// tests dedicado para ese proveedor en el repo — se sigue aquí el mismo criterio de fixture
/// que sus propios tests de handler ya usan).
/// </summary>
public sealed class SalesReturnCreditNoteDataProviderTests
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid CompanyId = Guid.NewGuid();
    private static readonly Guid BranchId = Guid.NewGuid();
    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly Guid CustomerId = Guid.NewGuid();
    private static readonly Guid PaymentTermId = Guid.NewGuid();
    private static readonly Guid CashSessionId = Guid.NewGuid();
    private static readonly Guid EmissionPointId = Guid.NewGuid();
    private static readonly Guid EstablishmentId = Guid.NewGuid();

    private static (SalesInvoice Invoice, List<SalesInvoiceDetail> Lines) BuildAuthorizedInvoice()
    {
        var customer = CustomerSnapshot.Create("Cliente Test", "1710034065", "05");
        var paymentTerm = PaymentTermSnapshot.Create(PaymentTermId, "Contado", 1, 0);

        var inv = SalesInvoice.CreateDraft(
            TenantId,
            CompanyId,
            BranchId,
            CustomerId,
            customer,
            invoiceNumber: "001-001-000000045",
            issueDate: new DateOnly(2026, 7, 20),
            createdBy: UserId,
            paymentTerm: paymentTerm,
            cashSessionId: CashSessionId,
            emissionPointId: EmissionPointId
        );

        var line = SalesInvoiceDetail.Create(
            inv.Id,
            TenantId,
            "Producto Test",
            10m,
            10m,
            vatCode: "2",
            uomCode: "UNIT"
        );
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
        return (inv, inv.Lines.ToList());
    }

    private static SalesReturn BuildAuthorizedReturn(
        SalesInvoice invoice,
        SalesInvoiceDetail originalLine,
        string? creditNoteDocumentNumber = "001-001-000000001"
    )
    {
        var salesReturn = SalesReturn.CreateDraft(
            TenantId,
            CompanyId,
            invoice.Id,
            CustomerId,
            "DEV-000001",
            "Producto en mal estado",
            UserId
        );
        salesReturn.AddLine(
            SalesReturnDetail.Create(
                salesReturn.Id,
                TenantId,
                originalLine.Id,
                originalLine.Description,
                4m,
                originalLine.UnitPrice,
                0m,
                originalLine.VatCode,
                originalLine.VatRate,
                originalLine.UomCode
            ),
            UserId
        );
        salesReturn.AddRefundAllocation(
            SalesReturnRefundAllocation.Create(
                salesReturn.Id,
                TenantId,
                SalesReturnRefundMethod.Cash,
                salesReturn.GrandTotal
            ),
            UserId
        );
        salesReturn.Authorize(UserId);
        if (creditNoteDocumentNumber is not null)
            salesReturn.SetCreditNoteDocumentNumber(creditNoteDocumentNumber);
        return salesReturn;
    }

    private sealed class Mocks
    {
        public Mock<ISalesReturnRepository> ReturnRepo { get; } = new();
        public Mock<ISalesInvoiceRepository> InvoiceRepo { get; } = new();
        public Mock<IEmissionPointRepository> EmissionPointRepo { get; } = new();
        public Mock<IEstablishmentRepository> EstablishmentRepo { get; } = new();
        public Mock<ICompanyRepository> CompanyRepo { get; } = new();
        public Mock<ISriSettingsRepository> SriSettingsRepo { get; } = new();
        public Mock<ISriDocTypeCatalogResolver> DocTypeResolver { get; } = new();

        public Mocks()
        {
            DocTypeResolver
                .Setup(r => r.IsActiveElectronicDocTypeAsync("04", It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);
        }

        public SalesReturnCreditNoteDataProvider BuildProvider() =>
            new(
                ReturnRepo.Object,
                InvoiceRepo.Object,
                EmissionPointRepo.Object,
                EstablishmentRepo.Object,
                CompanyRepo.Object,
                SriSettingsRepo.Object,
                DocTypeResolver.Object
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
                emissionType: ERP.Domain.Modules.Company.Enums.EmissionType.Electronic,
                isDefault: true,
                createdBy: UserId
            );
            // El proveedor lee emissionPoint.Establishment.Code/Address — en producción EF lo
            // carga vía Include (mismo patrón que SalesInvoiceElectronicDocumentDataProvider);
            // aquí se asigna por reflexión al construir el fixture fuera de un DbContext real.
            typeof(EmissionPoint)
                .GetProperty(nameof(EmissionPoint.Establishment))!
                .SetValue(emissionPoint, establishment);

            EmissionPointRepo
                .Setup(r =>
                    r.GetByIdAsync(EmissionPointId, TenantId, It.IsAny<CancellationToken>())
                )
                .ReturnsAsync(emissionPoint);
            EstablishmentRepo
                .Setup(r =>
                    r.GetMainByCompanyAsync(TenantId, CompanyId, It.IsAny<CancellationToken>())
                )
                .ReturnsAsync(establishment);
            CompanyRepo
                .Setup(r =>
                    r.GetByIdForTenantAsync(CompanyId, TenantId, It.IsAny<CancellationToken>())
                )
                .ReturnsAsync(
                    Company.CreateManaged(
                        TenantId,
                        "1790012345001",
                        "Empresa Test S.A.",
                        createdBy: UserId
                    )
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

    [Fact]
    public async Task GetDataAsync_con_devolucion_autorizada_construye_el_modelo_correctamente()
    {
        var (invoice, lines) = BuildAuthorizedInvoice();
        var salesReturn = BuildAuthorizedReturn(invoice, lines[0]);
        var m = new Mocks();
        m.SeedHappyPath(invoice);
        m.ReturnRepo.Setup(r =>
                r.GetByIdAsync(TenantId, salesReturn.Id, It.IsAny<CancellationToken>())
            )
            .ReturnsAsync(salesReturn);

        var provider = m.BuildProvider();
        var result = await provider.GetDataAsync(
            new ElectronicDocumentSourceReference(TenantId, CompanyId, salesReturn.Id)
        );

        result.IsSuccess.Should().BeTrue(result.Error);
        var data = result.Value!;
        data.Emission.DocTypeCode.Should().Be("04");
        data.Emission.Sequential.Should().Be("000000001");
        data.Reason.Should().Be("Producto en mal estado");
        data.ModifiedDocument.Should().NotBeNull();
        data.ModifiedDocument!.DocTypeCode.Should().Be(invoice.DocTypeCode);
        data.ModifiedDocument.Number.Should().Be(invoice.InvoiceNumber);
        data.Details.Should().ContainSingle();
        data.Totals!.GrandTotal.Should().Be(salesReturn.GrandTotal);
    }

    // SALES-PRESENTATIONS-04: la nota de crédito debe reflejar la cantidad VISIBLE devuelta (1
    // caja), nunca QuantityInBaseUom (12 unidades) — esa cantidad es exclusiva de stock/kardex
    // (ver AuthorizeSalesReturnUseCases, SALES-PRESENTATIONS-02).
    [Fact]
    public async Task GetDataAsync_devolucion_por_presentacion_usa_Quantity_visible_no_QuantityInBaseUom()
    {
        var customer = CustomerSnapshot.Create("Cliente Test", "1710034065", "05");
        var paymentTerm = PaymentTermSnapshot.Create(PaymentTermId, "Contado", 1, 0);
        var inv = SalesInvoice.CreateDraft(
            TenantId,
            CompanyId,
            BranchId,
            CustomerId,
            customer,
            invoiceNumber: "001-001-000000046",
            issueDate: new DateOnly(2026, 7, 20),
            createdBy: UserId,
            paymentTerm: paymentTerm,
            cashSessionId: CashSessionId,
            emissionPointId: EmissionPointId
        );
        var invoiceLine = SalesInvoiceDetail.Create(
            inv.Id,
            TenantId,
            "Caja x12",
            quantity: 2m,
            unitPrice: 18m,
            vatCode: "2",
            uomCode: "CAJA",
            conversionFactor: 12m,
            baseUomCode: "UNIT"
        );
        inv.ReplaceLines(new[] { invoiceLine }, UserId);
        var invPayment = SalesInvoicePayment.Create(
            inv.Id,
            TenantId,
            Guid.NewGuid(),
            "01",
            "Efectivo",
            invoiceLine.TaxInclusiveTotal
        );
        inv.ReplacePayments(new[] { invPayment }, UserId);
        inv.Authorize(UserId);
        var authorizedLine = inv.Lines.Single();

        var salesReturn = SalesReturn.CreateDraft(
            TenantId,
            CompanyId,
            inv.Id,
            CustomerId,
            "DEV-000002",
            "Producto en mal estado",
            UserId
        );
        salesReturn.AddLine(
            SalesReturnDetail.Create(
                salesReturn.Id,
                TenantId,
                authorizedLine.Id,
                authorizedLine.Description,
                quantity: 1m, // 1 caja devuelta — mantiene la presentación de la venta
                authorizedLine.UnitPrice,
                0m,
                authorizedLine.VatCode,
                authorizedLine.VatRate,
                authorizedLine.UomCode,
                packagingLevelId: authorizedLine.PackagingLevelId,
                conversionFactor: authorizedLine.ConversionFactor,
                baseUomCode: authorizedLine.BaseUomCode
            ),
            UserId
        );
        salesReturn.AddRefundAllocation(
            SalesReturnRefundAllocation.Create(
                salesReturn.Id,
                TenantId,
                SalesReturnRefundMethod.Cash,
                salesReturn.GrandTotal
            ),
            UserId
        );
        salesReturn.Authorize(UserId);
        salesReturn.SetCreditNoteDocumentNumber("001-001-000000002");

        var m = new Mocks();
        m.SeedHappyPath(inv);
        m.ReturnRepo.Setup(r =>
                r.GetByIdAsync(TenantId, salesReturn.Id, It.IsAny<CancellationToken>())
            )
            .ReturnsAsync(salesReturn);

        var provider = m.BuildProvider();
        var result = await provider.GetDataAsync(
            new ElectronicDocumentSourceReference(TenantId, CompanyId, salesReturn.Id)
        );

        result.IsSuccess.Should().BeTrue(result.Error);
        var detail = result.Value!.Details.Should().ContainSingle().Subject;
        detail.Quantity.Should().Be(1m); // Quantity visible (1 caja) — nunca 12 (unidad base)
        detail.UnitPrice.Should().Be(18m);
        detail.Subtotal.Should().Be(18m); // Quantity(1) * UnitPrice(18) — nunca 216 (1*12*18)
    }

    [Fact]
    public async Task GetDataAsync_devolucion_inexistente_retorna_NotFound()
    {
        var m = new Mocks();
        m.ReturnRepo.Setup(r =>
                r.GetByIdAsync(TenantId, It.IsAny<Guid>(), It.IsAny<CancellationToken>())
            )
            .ReturnsAsync((SalesReturn?)null);

        var provider = m.BuildProvider();
        var result = await provider.GetDataAsync(
            new ElectronicDocumentSourceReference(TenantId, CompanyId, Guid.NewGuid())
        );

        result.IsSuccess.Should().BeFalse();
    }

    [Fact]
    public async Task GetDataAsync_devolucion_no_autorizada_falla()
    {
        var (invoice, lines) = BuildAuthorizedInvoice();
        var salesReturn = SalesReturn.CreateDraft(
            TenantId,
            CompanyId,
            invoice.Id,
            CustomerId,
            "DEV-000002",
            "Motivo",
            UserId
        ); // Draft — nunca autorizada
        var m = new Mocks();
        m.SeedHappyPath(invoice);
        m.ReturnRepo.Setup(r =>
                r.GetByIdAsync(TenantId, salesReturn.Id, It.IsAny<CancellationToken>())
            )
            .ReturnsAsync(salesReturn);

        var provider = m.BuildProvider();
        var result = await provider.GetDataAsync(
            new ElectronicDocumentSourceReference(TenantId, CompanyId, salesReturn.Id)
        );

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("autorizada");
    }

    [Fact]
    public async Task GetDataAsync_sin_secuencial_de_nota_de_credito_asignado_falla()
    {
        var (invoice, lines) = BuildAuthorizedInvoice();
        var salesReturn = BuildAuthorizedReturn(invoice, lines[0], creditNoteDocumentNumber: null);
        var m = new Mocks();
        m.SeedHappyPath(invoice);
        m.ReturnRepo.Setup(r =>
                r.GetByIdAsync(TenantId, salesReturn.Id, It.IsAny<CancellationToken>())
            )
            .ReturnsAsync(salesReturn);

        var provider = m.BuildProvider();
        var result = await provider.GetDataAsync(
            new ElectronicDocumentSourceReference(TenantId, CompanyId, salesReturn.Id)
        );

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("secuencial");
    }

    [Fact]
    public async Task GetDataAsync_con_tipo_de_comprobante_04_inactivo_en_catalogo_falla()
    {
        var (invoice, lines) = BuildAuthorizedInvoice();
        var salesReturn = BuildAuthorizedReturn(invoice, lines[0]);
        var m = new Mocks();
        m.SeedHappyPath(invoice);
        m.ReturnRepo.Setup(r =>
                r.GetByIdAsync(TenantId, salesReturn.Id, It.IsAny<CancellationToken>())
            )
            .ReturnsAsync(salesReturn);
        m.DocTypeResolver.Setup(r =>
                r.IsActiveElectronicDocTypeAsync("04", It.IsAny<CancellationToken>())
            )
            .ReturnsAsync(false);

        var provider = m.BuildProvider();
        var result = await provider.GetDataAsync(
            new ElectronicDocumentSourceReference(TenantId, CompanyId, salesReturn.Id)
        );

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("04");
    }

    // ── TAX-LINE-SSOT-ICE-IRBPNR-01 (ADR-032 §3.5, Subfase 5D-4) ─────────────────

    private static (SalesInvoice Invoice, SalesInvoiceDetail Line) BuildAuthorizedInvoiceWithTaxes(
        decimal quantity,
        decimal unitPrice,
        string? iceCode = null,
        decimal iceRate = 0m,
        SriTaxCalculationType iceCalculationType = SriTaxCalculationType.Percentage,
        decimal? iceExactAmount = null,
        string? irbpnrCode = null,
        decimal irbpnrRate = 0m,
        decimal irbpnrAmount = 0m
    )
    {
        var customer = CustomerSnapshot.Create("Cliente Test", "1710034065", "05");
        var paymentTerm = PaymentTermSnapshot.Create(PaymentTermId, "Contado", 1, 0);
        var inv = SalesInvoice.CreateDraft(
            TenantId, CompanyId, BranchId, CustomerId, customer,
            invoiceNumber: "001-001-000000047", issueDate: new DateOnly(2026, 7, 20),
            createdBy: UserId, paymentTerm: paymentTerm, cashSessionId: CashSessionId,
            emissionPointId: EmissionPointId
        );
        var line = SalesInvoiceDetail.Create(
            inv.Id, TenantId, "Producto con impuestos especiales", quantity, unitPrice,
            vatCode: "2", uomCode: "UNIT", iceCode: iceCode
        );
        if (!string.IsNullOrWhiteSpace(irbpnrCode))
            line.ReplaceTaxes(
                [
                    SalesInvoiceDetailTax.Create(
                        line.Id, TenantId, "5", irbpnrCode, "IRBPNR", irbpnrRate,
                        SriTaxCalculationType.Specific, line.TaxableBase, irbpnrAmount,
                        SalesTaxSource.Calculated
                    ),
                ]
            );
        line.ApplyTaxes("2", 15m, "IVA 15%", iceCode, iceRate, "ICE", iceCalculationType, iceExactAmount);
        inv.ReplaceLines(new[] { line }, UserId);
        var payment = SalesInvoicePayment.Create(
            inv.Id, TenantId, Guid.NewGuid(), "01", "Efectivo", line.TaxInclusiveTotal
        );
        inv.ReplacePayments(new[] { payment }, UserId);
        inv.Authorize(UserId);
        return (inv, inv.Lines.Single());
    }

    private static SalesReturn BuildAuthorizedReturnWithFraction(
        SalesInvoice invoice,
        SalesInvoiceDetail originalLine,
        decimal returnQuantity,
        string creditNoteDocumentNumber
    )
    {
        var fraction = returnQuantity / originalLine.Quantity;
        decimal? iceExactAmount = null;
        if (originalLine.IceCalculationType == SriTaxCalculationType.Specific)
            iceExactAmount = Math.Round(
                fraction * originalLine.IceAmount,
                ERP.Domain.Common.FiscalPrecision.TaxAmount,
                MidpointRounding.AwayFromZero
            );

        var salesReturn = SalesReturn.CreateDraft(
            TenantId, CompanyId, invoice.Id, CustomerId, "DEV-000010", "Producto en mal estado", UserId
        );
        var line = SalesReturnDetail.Create(
            salesReturn.Id, TenantId, originalLine.Id, originalLine.Description, returnQuantity,
            originalLine.UnitPrice, 0m, originalLine.VatCode, originalLine.VatRate, originalLine.UomCode,
            iceCode: originalLine.IceCode, iceRate: originalLine.IceRate,
            iceCalculationType: originalLine.IceCalculationType, iceExactAmount: iceExactAmount
        );
        if (!string.IsNullOrWhiteSpace(originalLine.IrbpnrCode))
        {
            var irbpnrTax = originalLine.Taxes.First(t => t.TaxCode == "5");
            line.ReplaceTaxes(
                [
                    SalesReturnDetailTax.Create(
                        line.Id, TenantId, "5", irbpnrTax.TaxRateCode, irbpnrTax.TaxName, irbpnrTax.Rate,
                        irbpnrTax.CalculationType,
                        Math.Round(
                            fraction * originalLine.IrbpnrAmount,
                            ERP.Domain.Common.FiscalPrecision.TaxAmount,
                            MidpointRounding.AwayFromZero
                        )
                    ),
                ]
            );
        }
        salesReturn.AddLine(line, UserId);
        salesReturn.AddRefundAllocation(
            SalesReturnRefundAllocation.Create(
                salesReturn.Id, TenantId, SalesReturnRefundMethod.Cash, salesReturn.GrandTotal
            ),
            UserId
        );
        salesReturn.Authorize(UserId);
        salesReturn.SetCreditNoteDocumentNumber(creditNoteDocumentNumber);
        return salesReturn;
    }

    [Fact]
    public async Task GetDataAsync_NC_con_IVA_ICE_e_IRBPNR_propaga_los_tres_impuestos()
    {
        var (invoice, line) = BuildAuthorizedInvoiceWithTaxes(
            quantity: 10m, unitPrice: 100m,
            iceCode: "3010", iceRate: 10m,
            irbpnrCode: "5001", irbpnrRate: 0.02m, irbpnrAmount: 1.00m
        );
        var salesReturn = BuildAuthorizedReturnWithFraction(invoice, line, 10m, "001-001-000000010");
        var m = new Mocks();
        m.SeedHappyPath(invoice);
        m.ReturnRepo.Setup(r => r.GetByIdAsync(TenantId, salesReturn.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(salesReturn);

        var result = await m.BuildProvider()
            .GetDataAsync(new ElectronicDocumentSourceReference(TenantId, CompanyId, salesReturn.Id));

        result.IsSuccess.Should().BeTrue(result.Error);
        var detail = result.Value!.Details.Should().ContainSingle().Subject;
        detail.Taxes.Should().HaveCount(3);
        detail.Taxes.Select(t => t.TaxCode).Should().BeEquivalentTo(new[] { "VAT", "ICE", "IRBPNR" });
        var irbpnr = detail.Taxes.Should().ContainSingle(t => t.TaxCode == "IRBPNR").Subject;
        irbpnr.TaxAmount.Should().Be(1.00m);
        result.Value.Totals!.TotalTax.Should().Be(salesReturn.TotalVat + salesReturn.TotalIce + salesReturn.TotalIrbpnr);
    }

    [Fact]
    public async Task GetDataAsync_NC_parcial_prorratea_IRBPNR_por_la_fraccion_de_cantidad()
    {
        var (invoice, line) = BuildAuthorizedInvoiceWithTaxes(
            quantity: 10m, unitPrice: 100m,
            irbpnrCode: "5001", irbpnrRate: 0.02m, irbpnrAmount: 1.00m
        );
        var salesReturn = BuildAuthorizedReturnWithFraction(invoice, line, 3m, "001-001-000000011");
        var m = new Mocks();
        m.SeedHappyPath(invoice);
        m.ReturnRepo.Setup(r => r.GetByIdAsync(TenantId, salesReturn.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(salesReturn);

        var result = await m.BuildProvider()
            .GetDataAsync(new ElectronicDocumentSourceReference(TenantId, CompanyId, salesReturn.Id));

        result.IsSuccess.Should().BeTrue(result.Error);
        var detail = result.Value!.Details.Should().ContainSingle().Subject;
        var irbpnr = detail.Taxes.Should().ContainSingle(t => t.TaxCode == "IRBPNR").Subject;
        irbpnr.TaxAmount.Should().Be(0.30m); // fracción 3/10 = 0.3 sobre 1.00
    }

    [Fact]
    public async Task GetDataAsync_NC_con_ICE_Specific_conserva_el_monto_prorrateado()
    {
        var (invoice, line) = BuildAuthorizedInvoiceWithTaxes(
            quantity: 10m, unitPrice: 100m,
            iceCode: "3053", iceCalculationType: SriTaxCalculationType.Specific, iceExactAmount: 5.00m
        );
        var salesReturn = BuildAuthorizedReturnWithFraction(invoice, line, 4m, "001-001-000000012");
        var m = new Mocks();
        m.SeedHappyPath(invoice);
        m.ReturnRepo.Setup(r => r.GetByIdAsync(TenantId, salesReturn.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(salesReturn);

        var result = await m.BuildProvider()
            .GetDataAsync(new ElectronicDocumentSourceReference(TenantId, CompanyId, salesReturn.Id));

        result.IsSuccess.Should().BeTrue(result.Error);
        var detail = result.Value!.Details.Should().ContainSingle().Subject;
        var ice = detail.Taxes.Should().ContainSingle(t => t.TaxCode == "ICE").Subject;
        ice.TaxAmount.Should().Be(2.00m); // 0.4 * 5.00, nunca recalculado desde una tarifa
    }

    [Fact]
    public async Task GetDataAsync_NC_sin_IRBPNR_en_la_factura_no_genera_IRBPNR_falso()
    {
        var (invoice, line) = BuildAuthorizedInvoiceWithTaxes(quantity: 5m, unitPrice: 10m);
        var salesReturn = BuildAuthorizedReturnWithFraction(invoice, line, 5m, "001-001-000000013");
        var m = new Mocks();
        m.SeedHappyPath(invoice);
        m.ReturnRepo.Setup(r => r.GetByIdAsync(TenantId, salesReturn.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(salesReturn);

        var result = await m.BuildProvider()
            .GetDataAsync(new ElectronicDocumentSourceReference(TenantId, CompanyId, salesReturn.Id));

        result.IsSuccess.Should().BeTrue(result.Error);
        var detail = result.Value!.Details.Should().ContainSingle().Subject;
        detail.Taxes.Should().NotContain(t => t.TaxCode == "IRBPNR");
    }

    [Fact]
    public async Task GetDataAsync_Totales_de_NC_coinciden_con_el_GrandTotal_del_snapshot()
    {
        var (invoice, line) = BuildAuthorizedInvoiceWithTaxes(
            quantity: 1m, unitPrice: 100m,
            iceCode: "3010", iceRate: 10m,
            irbpnrCode: "5001", irbpnrRate: 0.02m, irbpnrAmount: 0.02m
        );
        var salesReturn = BuildAuthorizedReturnWithFraction(invoice, line, 1m, "001-001-000000014");
        var m = new Mocks();
        m.SeedHappyPath(invoice);
        m.ReturnRepo.Setup(r => r.GetByIdAsync(TenantId, salesReturn.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(salesReturn);

        var result = await m.BuildProvider()
            .GetDataAsync(new ElectronicDocumentSourceReference(TenantId, CompanyId, salesReturn.Id));

        result.IsSuccess.Should().BeTrue(result.Error);
        result.Value!.Totals!.GrandTotal.Should().Be(salesReturn.GrandTotal);
        result.Value.Totals.GrandTotal.Should().Be(line.TaxInclusiveTotal); // devolución total = mismo total que la línea original
    }

    [Fact]
    public void SalesReturnCreditNoteDataProvider_no_depende_de_IItemRepository_ni_de_CompanySpecialTaxResponsibility()
    {
        // Reglas 6/7 — el snapshot fiscal de la NC viene exclusivamente de SalesReturnDetail.Taxes
        // (a su vez heredado de SalesInvoiceDetail.Taxes en 5D-3): si el producto o la
        // responsabilidad tributaria de la empresa cambian después de la venta, la NC no puede
        // verse afectada porque este proveedor no puede leer ninguna de las dos.
        var ctor = typeof(SalesReturnCreditNoteDataProvider).GetConstructors().Single();
        ctor.GetParameters()
            .Should()
            .NotContain(
                p =>
                    p.ParameterType.Name.Contains("ItemRepository")
                    || p.ParameterType.Name.Contains("CompanySpecialTaxResponsibility"),
                "la NC de venta debe conservar el snapshot fiscal original, sin consultar el ítem "
                    + "actual ni la responsabilidad tributaria vigente de la empresa"
            );
    }
}

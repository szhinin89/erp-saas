using ERP.Application.Common.Services;
using ERP.Application.Modules.ElectronicDocuments.Services;
using ERP.Application.Modules.Sales.Services;
using ERP.Domain.Configuration.Entities;
using ERP.Domain.Configuration.Interfaces;
using ERP.Domain.Modules.Company.Entities;
using ERP.Domain.Modules.Company.Enums;
using ERP.Domain.Modules.Company.Interfaces;
using ERP.Domain.Modules.Sales.Entities;
using ERP.Domain.Modules.Sales.Interfaces;
using ERP.Domain.Modules.Sales.ValueObjects;
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

        public Mocks()
        {
            DocTypeResolver
                .Setup(r => r.IsActiveElectronicDocTypeAsync("01", It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);
        }

        public SalesInvoiceElectronicDocumentDataProvider BuildProvider() =>
            new(
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
}

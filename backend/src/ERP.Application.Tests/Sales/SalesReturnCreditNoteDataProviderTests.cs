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
                .Setup(r => r.GetByIdAsync(EmissionPointId, TenantId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(emissionPoint);
            EstablishmentRepo
                .Setup(r => r.GetMainByCompanyAsync(TenantId, CompanyId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(establishment);
            CompanyRepo
                .Setup(r => r.GetByIdForTenantAsync(CompanyId, TenantId, It.IsAny<CancellationToken>()))
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
        m.ReturnRepo
            .Setup(r => r.GetByIdAsync(TenantId, salesReturn.Id, It.IsAny<CancellationToken>()))
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

    [Fact]
    public async Task GetDataAsync_devolucion_inexistente_retorna_NotFound()
    {
        var m = new Mocks();
        m.ReturnRepo
            .Setup(r => r.GetByIdAsync(TenantId, It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
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
        m.ReturnRepo
            .Setup(r => r.GetByIdAsync(TenantId, salesReturn.Id, It.IsAny<CancellationToken>()))
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
        m.ReturnRepo
            .Setup(r => r.GetByIdAsync(TenantId, salesReturn.Id, It.IsAny<CancellationToken>()))
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
        m.ReturnRepo
            .Setup(r => r.GetByIdAsync(TenantId, salesReturn.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(salesReturn);
        m.DocTypeResolver
            .Setup(r => r.IsActiveElectronicDocTypeAsync("04", It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var provider = m.BuildProvider();
        var result = await provider.GetDataAsync(
            new ElectronicDocumentSourceReference(TenantId, CompanyId, salesReturn.Id)
        );

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("04");
    }
}

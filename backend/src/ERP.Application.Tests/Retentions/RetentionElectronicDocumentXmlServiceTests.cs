using ERP.Application.Common;
using ERP.Application.Modules.ElectronicDocuments.DTOs;
using ERP.Application.Modules.ElectronicDocuments.Services;
using ERP.Application.Modules.ElectronicDocuments.XmlBuilders;
using ERP.Application.Modules.Retentions.Services;
using ERP.Domain.Modules.Retentions.Enums;
using FluentAssertions;
using Moq;

namespace ERP.Application.Tests.Retentions;

/// <summary>
/// RETENTIONS-ELECTRONIC-WIRING-03E — <see cref="RetentionElectronicDocumentXmlService"/>: única
/// responsabilidad, orquestar <see cref="IRetentionElectronicDocumentDataProvider"/> →
/// <see cref="IRetentionXmlBuilder"/>, sin lógica propia.
/// </summary>
public sealed class RetentionElectronicDocumentXmlServiceTests
{
    private static RetentionElectronicDocumentData ValidData() =>
        new(
            Metadata: new RetentionElectronicDocumentMetadata(
                RetentionId: Guid.NewGuid(),
                TenantId: Guid.NewGuid(),
                CompanyId: Guid.NewGuid(),
                EmissionPointId: Guid.NewGuid(),
                SourceDocumentType: RetentionSourceDocumentType.ExpenseDocument,
                SourceDocumentId: Guid.NewGuid(),
                GeneratedAtUtc: new DateTime(2026, 9, 4, 12, 0, 0, DateTimeKind.Utc)
            ),
            Emission: new ElectronicDocumentEmissionContext(
                Environment: "1",
                EmissionType: "1",
                DocTypeCode: "07",
                Establishment: "001",
                EstablishmentAddress: "Av. Principal 123",
                EmissionPoint: "001",
                Sequential: "000000001",
                IssueDate: new DateTime(2026, 8, 5)
            ),
            NumeroCompleto: "001-001-000000001",
            Issuer: new ElectronicDocumentIssuerData(
                TaxId: "1790012345001",
                LegalName: "Empresa Test S.A.",
                TradeName: null,
                MatrixAddress: "Matriz 456",
                TaxRegime: null,
                IsAccountingRequired: true
            ),
            RetentionInfo: new RetentionElectronicDocumentInfo(
                SpecialTaxpayerNumber: null,
                FiscalPeriod: "08/2026"
            ),
            SubjectWithheld: new ElectronicDocumentCounterpartyData(
                IdentificationType: "05",
                IdentificationNumber: "1710034065",
                LegalName: "Proveedor Test",
                Address: null,
                Email: null
            ),
            SourceDocument: new RetentionElectronicDocumentSourceDocument(
                TaxSupportCode: "01",
                DocTypeCode: "01",
                Number: "001-001-000000456",
                AuthorizationNumber: null,
                IssueDate: new DateOnly(2026, 8, 1),
                Subtotal: 100m,
                Total: 115m
            ),
            Lines:
            [
                new RetentionElectronicDocumentTaxLine(
                    TaxType: RetentionTaxType.Income,
                    SriTaxTypeCode: "1",
                    RetentionCode: "303",
                    RetentionCodeDescription: "Honorarios profesionales",
                    BaseAmount: 100m,
                    RetentionRate: 8m,
                    RetainedAmount: 8m
                ),
            ],
            Totals: new RetentionElectronicDocumentTotals(
                TotalRetainedVat: 0m,
                TotalRetainedIncome: 8m,
                TotalRetained: 8m
            ),
            AdditionalInfo: []
        );

    [Fact]
    public async Task GenerateXmlAsync_uses_the_data_provider_and_the_xml_builder_and_returns_the_xml()
    {
        var reference = new ElectronicDocumentSourceReference(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid()
        );
        var data = ValidData();

        var dataProvider = new Mock<IRetentionElectronicDocumentDataProvider>();
        dataProvider
            .Setup(p => p.GetDataAsync(reference, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<RetentionElectronicDocumentData>.Success(data));

        var xmlBuilder = new Mock<IRetentionXmlBuilder>();
        var expectedXml = new ElectronicDocumentXml(
            Xml: "<comprobanteRetencion/>",
            Encoding: "UTF-8",
            Version: "1.0.0",
            DocumentType: ERP.Domain.Modules.ElectronicDocuments.Enums.ElectronicDocumentType.Retention,
            Environment: "1",
            AccessKey: new string('1', 49),
            GeneratedAtUtc: DateTime.UtcNow
        );
        xmlBuilder.Setup(b => b.Build(data)).Returns(Result<ElectronicDocumentXml>.Success(expectedXml));

        var service = new RetentionElectronicDocumentXmlService(dataProvider.Object, xmlBuilder.Object);

        var result = await service.GenerateXmlAsync(reference, CancellationToken.None);

        result.IsSuccess.Should().BeTrue(result.Error);
        result.Value.Should().Be(expectedXml);
        dataProvider.Verify(p => p.GetDataAsync(reference, It.IsAny<CancellationToken>()), Times.Once);
        xmlBuilder.Verify(b => b.Build(data), Times.Once);
    }

    [Fact]
    public async Task GenerateXmlAsync_propagates_a_data_provider_failure_without_calling_the_builder()
    {
        var reference = new ElectronicDocumentSourceReference(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid()
        );

        var dataProvider = new Mock<IRetentionElectronicDocumentDataProvider>();
        dataProvider
            .Setup(p => p.GetDataAsync(reference, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<RetentionElectronicDocumentData>.NotFound("La retención no existe."));

        var xmlBuilder = new Mock<IRetentionXmlBuilder>();

        var service = new RetentionElectronicDocumentXmlService(dataProvider.Object, xmlBuilder.Object);

        var result = await service.GenerateXmlAsync(reference, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Be("La retención no existe.");
        xmlBuilder.Verify(b => b.Build(It.IsAny<RetentionElectronicDocumentData>()), Times.Never);
    }

    [Fact]
    public async Task GenerateXmlAsync_propagates_a_builder_validation_failure()
    {
        var reference = new ElectronicDocumentSourceReference(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid()
        );
        var data = ValidData();

        var dataProvider = new Mock<IRetentionElectronicDocumentDataProvider>();
        dataProvider
            .Setup(p => p.GetDataAsync(reference, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<RetentionElectronicDocumentData>.Success(data));

        var xmlBuilder = new Mock<IRetentionXmlBuilder>();
        xmlBuilder
            .Setup(b => b.Build(data))
            .Returns(Result<ElectronicDocumentXml>.ValidationFailure("El secuencial debe tener 9 dígitos."));

        var service = new RetentionElectronicDocumentXmlService(dataProvider.Object, xmlBuilder.Object);

        var result = await service.GenerateXmlAsync(reference, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Be("El secuencial debe tener 9 dígitos.");
    }
}

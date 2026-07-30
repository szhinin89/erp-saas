using ERP.Application.Common;
using ERP.Application.Modules.ElectronicDocuments.DTOs;
using ERP.Application.Modules.ElectronicDocuments.XmlBuilders;
using ERP.Application.Modules.Ride.Branding;
using ERP.Application.Modules.Ride.DTOs;
using ERP.Application.Modules.Ride.Parsers;
using ERP.Application.Modules.Ride.Services;
using ERP.Application.Modules.Ride.Storage;
using ERP.Application.Modules.Ride.Templates;
using ERP.Domain.Modules.Ride.Entities;
using ERP.Domain.Modules.Ride.Enums;
using ERP.Domain.Modules.Ride.Interfaces;
using ERP.Domain.Modules.Ride.ValueObjects;
using ERP.Infrastructure.Ride.Rendering;
using FluentAssertions;
using Moq;
using System.Text;

namespace ERP.Infrastructure.Tests.Ride;

/// <summary>
/// Recorrido completo real de la Fase 7: XML real → <see cref="InvoiceRideXmlParser"/> real →
/// <see cref="DefaultInvoiceRideTemplate"/> real → <see cref="QuestPdfRideRenderer"/> real, todos
/// resueltos por <see cref="RidePipeline"/> sin ningún doble de prueba para esas 3 piezas — solo
/// el origen del XML, storage, cache y repositorio (ajenos a esta fase) siguen siendo dobles.
/// </summary>
public sealed class RidePipelineQuestPdfIntegrationTests
{
    private sealed class FakeTaxCategoryCodeResolver : ISriTaxCategoryCodeResolver
    {
        public string? Resolve(string taxCode) =>
            taxCode switch
            {
                "VAT" => "2",
                _ => null,
            };
    }

    [Fact]
    public async Task Real_invoice_flows_end_to_end_through_the_real_renderer_and_produces_a_valid_pdf()
    {
        var data = new ElectronicDocumentData(
            Emission: new ElectronicDocumentEmissionContext(
                "1",
                "1",
                "01",
                "001",
                "Av. Amazonas y Naciones Unidas",
                "001",
                "000000123",
                new DateTime(2026, 7, 8)
            ),
            Issuer: new ElectronicDocumentIssuerData(
                "1790012345001",
                "ACME CIA LTDA",
                "ACME",
                "Av. Amazonas y Naciones Unidas",
                null,
                true
            ),
            Counterparty: new ElectronicDocumentCounterpartyData(
                "05",
                "1710034065",
                "Juan Pérez",
                "Calle Falsa 123",
                "juan@example.com"
            ),
            Details:
            [
                new ElectronicDocumentDetailLine(
                    "SKU-001",
                    "Producto de prueba",
                    2m,
                    10m,
                    0m,
                    20m,
                    [new ElectronicDocumentDetailTax("VAT", "2", 20m, 15m, 3m)]
                ),
            ],
            TaxSummary: [new ElectronicDocumentTaxSummary("VAT", "2", 20m, 3m)],
            Totals: new ElectronicDocumentTotals(20m, 0m, 3m, 23m, "USD"),
            Payments: [new ElectronicDocumentPayment("01", 23m, null, null)],
            AdditionalInfo: []
        );
        var xmlResult = new InvoiceXmlBuilder(new FakeTaxCategoryCodeResolver()).Build(data);
        xmlResult.IsSuccess.Should().BeTrue(xmlResult.Error);
        var xml = xmlResult.Value!.Xml;

        var tenantId = Guid.NewGuid();
        var companyId = Guid.NewGuid();
        var electronicDocumentId = Guid.NewGuid();
        const string sourceModule = "Sales";
        var sourceEntityId = Guid.NewGuid();

        var sourceXmlProvider = new Mock<IRideSourceXmlProvider>();
        sourceXmlProvider
            .Setup(p =>
                p.GetAuthorizedXmlAsync(
                    tenantId,
                    companyId,
                    sourceModule,
                    sourceEntityId,
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(
                Result<RideSourceXmlLookup>.Success(
                    new RideSourceXmlLookup(
                        RideSourceXmlStatus.Available,
                        xml,
                        electronicDocumentId,
                        RideDocumentType.Invoice
                    )
                )
            );

        var cacheStrategy = new Mock<IRideCacheStrategy>();
        cacheStrategy
            .Setup(c =>
                c.TryGetCachedAsync(
                    It.IsAny<Guid>(),
                    It.IsAny<Guid>(),
                    It.IsAny<RideContentHash>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(Result<RidePdfMetadataDto?>.Success(null));

        var brandingProvider = new Mock<IRideBrandingProvider>();
        brandingProvider
            .Setup(b => b.GetAsync(tenantId, companyId, null, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<RideBranding>.Success(RideBranding.Empty()));

        byte[]? storedPdf = null;
        var storageService = new Mock<IRidePdfStorageService>();
        storageService
            .Setup(s =>
                s.StoreAsync(
                    tenantId,
                    RideDocumentType.Invoice,
                    electronicDocumentId,
                    It.IsAny<string>(),
                    It.IsAny<byte[]>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .Callback<Guid, RideDocumentType, Guid, string, byte[], CancellationToken>(
                (_, _, _, _, pdf, _) => storedPdf = pdf
            )
            .ReturnsAsync(Result<string>.Success("ride/path/invoice.pdf"));

        var repository = new Mock<IRidePdfDocumentRepository>();
        repository
            .Setup(r =>
                r.GetByFingerprintAsync(
                    tenantId,
                    electronicDocumentId,
                    It.IsAny<RideContentHash>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync((RidePdfDocument?)null);

        var currentUser = new Mock<ICurrentUser>();
        currentUser.SetupGet(u => u.UserId).Returns(Guid.NewGuid());

        var pipeline = new RidePipeline(
            sourceXmlProvider.Object,
            new RideXmlParserResolver([new InvoiceRideXmlParser()]),
            new RideTemplateResolver([new DefaultInvoiceRideTemplate()]),
            cacheStrategy.Object,
            new RideContentHasher(),
            brandingProvider.Object,
            new QuestPdfRideRenderer(
                RideQrCodeGeneratorTestFactory.Create(),
                RideBarcodeGeneratorTestFactory.Create(),
                new NoOpFileStorage()
            ),
            storageService.Object,
            repository.Object,
            currentUser.Object
        );

        var result = await pipeline.ExecuteAsync(
            tenantId,
            companyId,
            sourceModule,
            sourceEntityId,
            forceRegenerate: false,
            CancellationToken.None
        );

        result.IsSuccess.Should().BeTrue(result.Error);
        result.Value!.Outcome.Should().Be(RideOutcome.Generated);
        storedPdf.Should().NotBeNull();
        storedPdf.Should().NotBeEmpty();
        Encoding.ASCII.GetString(storedPdf!, 0, 5).Should().Be("%PDF-");
    }
}

using ERP.Application.Common;
using ERP.Application.Modules.ElectronicDocuments.DTOs;
using ERP.Application.Modules.ElectronicDocuments.XmlBuilders;
using ERP.Application.Modules.Ride.Branding;
using ERP.Application.Modules.Ride.DTOs;
using ERP.Application.Modules.Ride.Parsers;
using ERP.Application.Modules.Ride.Rendering;
using ERP.Application.Modules.Ride.Services;
using ERP.Application.Modules.Ride.Storage;
using ERP.Application.Modules.Ride.Templates;
using ERP.Domain.Modules.Ride.Entities;
using ERP.Domain.Modules.Ride.Enums;
using ERP.Domain.Modules.Ride.Interfaces;
using ERP.Domain.Modules.Ride.ValueObjects;
using FluentAssertions;
using Moq;

namespace ERP.Application.Tests.Ride;

/// <summary>
/// Fase 6 (ADR-025): extremo a extremo con una factura autorizada REAL — generada con
/// <c>InvoiceXmlBuilder</c> (ElectronicDocuments, FROZEN) — a través de
/// <see cref="InvoiceRideXmlParser"/>, <see cref="DefaultInvoiceRideTemplate"/> y
/// <see cref="RidePipeline"/>, resolviendo ambos exclusivamente vía
/// <see cref="IRideXmlParserResolver"/>/<see cref="IRideTemplateResolver"/> reales (no dobles) —
/// nunca mediante <c>if</c>/<c>switch</c>/reflexión. Solo las piezas de infraestructura ajenas a
/// esta fase (origen del XML, storage, renderer, repositorio) siguen siendo dobles de prueba.
/// </summary>
public sealed class RidePipelineInvoiceIntegrationTests
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

    private static string RealAuthorizedInvoiceXml()
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

        var result = new InvoiceXmlBuilder(new FakeTaxCategoryCodeResolver()).Build(data);
        result.IsSuccess.Should().BeTrue(result.Error);
        return result.Value!.Xml;
    }

    [Fact]
    public async Task Real_invoice_xml_flows_through_real_parser_and_template_resolved_by_strategy_only()
    {
        var tenantId = Guid.NewGuid();
        var companyId = Guid.NewGuid();
        var electronicDocumentId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        const string sourceModule = "Sales";
        var sourceEntityId = Guid.NewGuid();
        var xml = RealAuthorizedInvoiceXml();

        // Strategy real: mismos resolvers de producción, con el parser/plantilla reales de Factura
        // registrados — nada de if/switch/reflexión para elegirlos.
        var parserResolver = new RideXmlParserResolver([new InvoiceRideXmlParser()]);
        var templateResolver = new RideTemplateResolver([new DefaultInvoiceRideTemplate()]);

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

        IRideDocumentLayout? capturedLayout = null;
        var renderer = new Mock<IRideRenderer>();
        renderer
            .Setup(r =>
                r.RenderAsync(It.IsAny<IRideDocumentLayout>(), It.IsAny<CancellationToken>())
            )
            .Callback<IRideDocumentLayout, CancellationToken>(
                (layout, _) => capturedLayout = layout
            )
            .ReturnsAsync([1, 2, 3]);

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
        currentUser.SetupGet(u => u.UserId).Returns(userId);

        var pipeline = new RidePipeline(
            sourceXmlProvider.Object,
            parserResolver,
            templateResolver,
            cacheStrategy.Object,
            new RideContentHasher(),
            brandingProvider.Object,
            renderer.Object,
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
        result.Value.StoragePath.Should().Be("ride/path/invoice.pdf");

        capturedLayout.Should().BeOfType<InvoiceRideDocumentLayout>();
        var invoiceLayout = (InvoiceRideDocumentLayout)capturedLayout!;
        invoiceLayout.Header.GrandTotal.Should().Be(23m);
        invoiceLayout.Lines.Should().ContainSingle();
        invoiceLayout.TaxSummary.Should().ContainSingle();

        repository.Verify(
            r => r.AddAsync(It.IsAny<RidePdfDocument>(), It.IsAny<CancellationToken>()),
            Times.Once
        );
    }
}

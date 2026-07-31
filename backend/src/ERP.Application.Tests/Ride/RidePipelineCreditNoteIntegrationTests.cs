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
using ERP.Infrastructure.Ride.Rendering;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using System.Runtime.CompilerServices;

namespace ERP.Application.Tests.Ride;

/// <summary>
/// ADR-031 addendum (Fase 12, P0-01): extremo a extremo con una Nota de Crédito autorizada REAL
/// — generada con <c>CreditNoteXmlBuilder</c> (ElectronicDocuments) — a través de
/// <see cref="CreditNoteRideXmlParser"/>, <see cref="CreditNoteRideTemplate"/> y
/// <see cref="RidePipeline"/>, resolviendo ambos exclusivamente vía
/// <see cref="IRideXmlParserResolver"/>/<see cref="IRideTemplateResolver"/> reales (mismo patrón
/// que <c>RidePipelineInvoiceIntegrationTests</c>). A diferencia de esa suite, aquí el
/// <see cref="IRideRenderer"/> también es la implementación REAL (<see cref="QuestPdfRideRenderer"/>,
/// con generadores QR/código de barras reales) para probar que el PDF final no está vacío — no
/// alcanza con que el layout se componga correctamente, el requisito es un PDF generado de verdad.
/// Solo las piezas ajenas a esta fase (origen del XML, cache, storage, repositorio) siguen siendo
/// dobles de prueba.
/// </summary>
public sealed class RidePipelineCreditNoteIntegrationTests
{
    // QuestPDF exige seleccionar una licencia antes de generar cualquier documento — esta suite
    // es la única de ERP.Application.Tests que invoca el renderer real (las demás mockean
    // IRideRenderer), así que fija la licencia localmente en vez de depender de un módulo
    // compartido de otro proyecto de test.
    [ModuleInitializer]
    public static void SetQuestPdfLicense() =>
        QuestPDF.Settings.License = QuestPDF.Infrastructure.LicenseType.Community;

    private sealed class FakeTaxCategoryCodeResolver : ISriTaxCategoryCodeResolver
    {
        public string? Resolve(string taxCode) =>
            taxCode switch
            {
                "VAT" => "2",
                "ICE" => "3",
                _ => null,
            };
    }

    private static string RealAuthorizedCreditNoteXml(bool withIce)
    {
        var data = new ElectronicDocumentData(
            Emission: new ElectronicDocumentEmissionContext(
                "2",
                "1",
                "04",
                "001",
                "Av. Amazonas y Naciones Unidas",
                "001",
                "000000045",
                new DateTime(2026, 7, 30)
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
                    "Producto devuelto",
                    2m,
                    10m,
                    0m,
                    20m,
                    withIce
                        ?
                        [
                            new ElectronicDocumentDetailTax("VAT", "2", 20m, 15m, 3m),
                            new ElectronicDocumentDetailTax("ICE", "3010", 20m, 10m, 2m),
                        ]
                        : [new ElectronicDocumentDetailTax("VAT", "2", 20m, 15m, 3m)]
                ),
            ],
            TaxSummary: withIce
                ?
                [
                    new ElectronicDocumentTaxSummary("ICE", "3010", 20m, 2m),
                    new ElectronicDocumentTaxSummary("VAT", "2", 22m, 3.3m),
                ]
                : [new ElectronicDocumentTaxSummary("VAT", "2", 20m, 3m)],
            Totals: new ElectronicDocumentTotals(
                20m,
                0m,
                withIce ? 2m : 0m,
                withIce ? 25.3m : 23m,
                "USD"
            ),
            Payments: [],
            AdditionalInfo: [],
            Reason: "Producto en mal estado",
            ModifiedDocument: new ElectronicDocumentModifiedReference(
                "01",
                "001-001-000000045",
                new DateTime(2026, 7, 20)
            )
        );

        var result = new CreditNoteXmlBuilder(new FakeTaxCategoryCodeResolver()).Build(data);
        result.IsSuccess.Should().BeTrue(result.Error);
        return result.Value!.Xml;
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Real_credit_note_xml_flows_through_real_pipeline_and_produces_a_non_empty_pdf(
        bool withIce
    )
    {
        var tenantId = Guid.NewGuid();
        var companyId = Guid.NewGuid();
        var electronicDocumentId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        const string sourceModule = "Sales";
        var sourceEntityId = Guid.NewGuid();
        var xml = RealAuthorizedCreditNoteXml(withIce);

        // Strategy real: mismos resolvers de producción, con AMBAS plantillas/parsers reales
        // registrados (Factura + Nota de Crédito) — prueba explícita de que agregar CreditNote no
        // rompe la resolución de Invoice (item 8 de la Fase 12).
        var parserResolver = new RideXmlParserResolver(
            [new InvoiceRideXmlParser(), new CreditNoteRideXmlParser()]
        );
        var templateResolver = new RideTemplateResolver(
            [new DefaultInvoiceRideTemplate(), new CreditNoteRideTemplate()]
        );

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
                        RideDocumentType.CreditNote
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

        // Renderer REAL — nunca mockeado en esta suite: es justamente lo que prueba "PDF no vacío".
        var renderer = new QuestPdfRideRenderer(
            new ERP.Infrastructure.Ride.Qr.RideQrCodeGenerator(
                new ERP.Infrastructure.Codes.QrCodeGenerator(
                    NullLogger<ERP.Infrastructure.Codes.QrCodeGenerator>.Instance
                )
            ),
            new ERP.Infrastructure.Ride.Barcodes.RideBarcodeGenerator(
                new ERP.Infrastructure.Codes.Barcodes.Code128BarcodeGenerator(
                    NullLogger<ERP.Infrastructure.Codes.Barcodes.Code128BarcodeGenerator>.Instance
                )
            ),
            new Mock<ERP.Application.Common.Interfaces.IFileStorage>().Object
        );

        var storageService = new Mock<IRidePdfStorageService>();
        byte[]? generatedPdfBytes = null;
        storageService
            .Setup(s =>
                s.StoreAsync(
                    tenantId,
                    RideDocumentType.CreditNote,
                    electronicDocumentId,
                    It.IsAny<string>(),
                    It.IsAny<byte[]>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .Callback<Guid, RideDocumentType, Guid, string, byte[], CancellationToken>(
                (_, _, _, _, bytes, _) => generatedPdfBytes = bytes
            )
            .ReturnsAsync(Result<string>.Success("ride/path/credit-note.pdf"));

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
            renderer,
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
        result.Value!.Outcome.Should().Be(RideOutcome.Generated, result.Value.ReasonCode ?? string.Empty);
        result.Value.StoragePath.Should().Be("ride/path/credit-note.pdf");

        generatedPdfBytes.Should().NotBeNull();
        generatedPdfBytes.Should().NotBeEmpty("el PDF de la Nota de Crédito debe generarse realmente, no solo componerse el layout");
        // Firma de archivo PDF ("%PDF-") — confirma que son bytes de un PDF real, no un placeholder.
        System.Text.Encoding.ASCII.GetString(generatedPdfBytes!, 0, 5).Should().Be("%PDF-");

        repository.Verify(
            r => r.AddAsync(It.IsAny<RidePdfDocument>(), It.IsAny<CancellationToken>()),
            Times.Once
        );
    }
}

using ERP.Application.Common;
using ERP.Application.Modules.ElectronicDocuments.DTOs;
using ERP.Application.Modules.ElectronicDocuments.XmlBuilders;
using ERP.Application.Modules.Ride.Branding;
using ERP.Application.Modules.Ride.Parsers;
using ERP.Application.Modules.Ride.Rendering;
using ERP.Application.Modules.Ride.Services;
using ERP.Application.Modules.Ride.Templates;
using ERP.Domain.Modules.Retentions.Enums;
using ERP.Domain.Modules.Ride.ValueObjects;
using FluentAssertions;
using Moq;

namespace ERP.Application.Tests.Ride;

/// <summary>
/// RETENTIONS-ELECTRONIC-WIRING-03E — <see cref="RetentionRidePdfService"/>: orquesta
/// <see cref="IRetentionRideXmlParser"/> → <see cref="IRetentionRideTemplate"/> →
/// <see cref="IRideRenderer"/>, con branding resuelto vía <see cref="IRideBrandingProvider"/>.
/// Usa el parser/plantilla REALES (puros, sin I/O) contra XML real generado por
/// <see cref="RetentionXmlBuilder"/> — solo el renderer (QuestPDF, Infrastructure) y el branding
/// provider se mockean, porque <c>ERP.Application.Tests</c> no puede depender de Infrastructure.
/// </summary>
public sealed class RetentionRidePdfServiceTests
{
    private static string ValidRetentionXml()
    {
        var data = new RetentionElectronicDocumentData(
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

        var result = new RetentionXmlBuilder().Build(data);
        result.IsSuccess.Should().BeTrue(result.Error);
        return result.Value!.Xml;
    }

    private static (
        Mock<IRideRenderer> Renderer,
        Mock<IRideBrandingProvider> BrandingProvider
    ) MockDependencies(byte[]? pdfBytes = null)
    {
        var renderer = new Mock<IRideRenderer>();
        renderer
            .Setup(r => r.RenderAsync(It.IsAny<IRideDocumentLayout>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(pdfBytes ?? [1, 2, 3]);

        var brandingProvider = new Mock<IRideBrandingProvider>();
        brandingProvider
            .Setup(b =>
                b.GetAsync(
                    It.IsAny<Guid>(),
                    It.IsAny<Guid>(),
                    It.IsAny<Guid?>(),
                    It.IsAny<Guid?>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(Result<RideBranding>.Success(RideBranding.Empty()));

        return (renderer, brandingProvider);
    }

    [Fact]
    public async Task GeneratePdfAsync_returns_a_non_empty_pdf_from_a_real_retention_xml()
    {
        var xml = ValidRetentionXml();
        var (renderer, brandingProvider) = MockDependencies([1, 2, 3, 4]);
        var service = new RetentionRidePdfService(
            new RetentionRideXmlParser(),
            new RetentionRideTemplate(),
            renderer.Object,
            brandingProvider.Object
        );

        var result = await service.GeneratePdfAsync(
            xml,
            Guid.NewGuid(),
            Guid.NewGuid(),
            ct: CancellationToken.None
        );

        result.IsSuccess.Should().BeTrue(result.Error);
        result.Value.Should().NotBeEmpty();
        renderer.Verify(
            r => r.RenderAsync(It.IsAny<IRideDocumentLayout>(), It.IsAny<CancellationToken>()),
            Times.Once
        );
    }

    [Fact]
    public async Task GeneratePdfAsync_composes_the_layout_with_the_resolved_branding()
    {
        var xml = ValidRetentionXml();
        var (renderer, brandingProvider) = MockDependencies();
        IRideDocumentLayout? capturedLayout = null;
        renderer
            .Setup(r => r.RenderAsync(It.IsAny<IRideDocumentLayout>(), It.IsAny<CancellationToken>()))
            .Callback<IRideDocumentLayout, CancellationToken>((layout, _) => capturedLayout = layout)
            .ReturnsAsync([9]);

        var service = new RetentionRidePdfService(
            new RetentionRideXmlParser(),
            new RetentionRideTemplate(),
            renderer.Object,
            brandingProvider.Object
        );

        await service.GeneratePdfAsync(xml, Guid.NewGuid(), Guid.NewGuid(), ct: CancellationToken.None);

        capturedLayout.Should().BeOfType<RetentionRideDocumentLayout>();
    }

    [Fact]
    public async Task GeneratePdfAsync_does_not_throw_and_still_generates_when_authorization_is_pending()
    {
        // El XML de RetentionXmlBuilder nunca trae fechaAutorizacion (no existe en
        // comprobanteRetencion) — el layout siempre expone AuthorizationDate == null y el
        // fallback "no disponible" (RETENTIONS-RIDE-TEMPLATE-03C). Esto no debe impedir el PDF.
        var xml = ValidRetentionXml();
        var (renderer, brandingProvider) = MockDependencies();
        var service = new RetentionRidePdfService(
            new RetentionRideXmlParser(),
            new RetentionRideTemplate(),
            renderer.Object,
            brandingProvider.Object
        );

        var act = async () =>
            await service.GeneratePdfAsync(
                xml,
                Guid.NewGuid(),
                Guid.NewGuid(),
                ct: CancellationToken.None
            );

        var result = await act.Should().NotThrowAsync();
        result.Subject.IsSuccess.Should().BeTrue(result.Subject.Error);
    }

    [Fact]
    public async Task GeneratePdfAsync_fails_without_calling_the_renderer_when_the_xml_is_malformed()
    {
        var (renderer, brandingProvider) = MockDependencies();
        var service = new RetentionRidePdfService(
            new RetentionRideXmlParser(),
            new RetentionRideTemplate(),
            renderer.Object,
            brandingProvider.Object
        );

        var result = await service.GeneratePdfAsync(
            "<comprobanteRetencion></comprobanteRetencion>",
            Guid.NewGuid(),
            Guid.NewGuid(),
            ct: CancellationToken.None
        );

        result.IsSuccess.Should().BeFalse();
        renderer.Verify(
            r => r.RenderAsync(It.IsAny<IRideDocumentLayout>(), It.IsAny<CancellationToken>()),
            Times.Never
        );
    }

    [Fact]
    public async Task GeneratePdfAsync_propagates_a_branding_resolution_failure()
    {
        var xml = ValidRetentionXml();
        var renderer = new Mock<IRideRenderer>();
        var brandingProvider = new Mock<IRideBrandingProvider>();
        brandingProvider
            .Setup(b =>
                b.GetAsync(
                    It.IsAny<Guid>(),
                    It.IsAny<Guid>(),
                    It.IsAny<Guid?>(),
                    It.IsAny<Guid?>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(Result<RideBranding>.Failure("No se pudo resolver el branding."));

        var service = new RetentionRidePdfService(
            new RetentionRideXmlParser(),
            new RetentionRideTemplate(),
            renderer.Object,
            brandingProvider.Object
        );

        var result = await service.GeneratePdfAsync(
            xml,
            Guid.NewGuid(),
            Guid.NewGuid(),
            ct: CancellationToken.None
        );

        result.IsSuccess.Should().BeFalse();
        renderer.Verify(
            r => r.RenderAsync(It.IsAny<IRideDocumentLayout>(), It.IsAny<CancellationToken>()),
            Times.Never
        );
    }
}

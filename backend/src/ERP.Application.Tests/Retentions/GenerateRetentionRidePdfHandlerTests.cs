using ERP.Application.Common;
using ERP.Application.Modules.ElectronicDocuments.DTOs;
using ERP.Application.Modules.ElectronicDocuments.Services;
using ERP.Application.Modules.Retentions.Services;
using ERP.Application.Modules.Retentions.UseCases;
using ERP.Application.Modules.Ride.Services;
using ERP.Domain.Modules.ElectronicDocuments.Enums;
using FluentAssertions;
using Moq;

namespace ERP.Application.Tests.Retentions;

/// <summary>
/// RETENTIONS-ELECTRONIC-ENDPOINTS-03F — cubre <see cref="GenerateRetentionRidePdfHandler"/>:
/// orquesta <see cref="IRetentionElectronicDocumentXmlService"/> → <see cref="IRetentionRidePdfService"/>,
/// sin lógica propia, propagando el fallo del primer servicio que falle.
/// </summary>
public sealed class GenerateRetentionRidePdfHandlerTests
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid CompanyId = Guid.NewGuid();
    private static readonly Guid RetentionId = Guid.NewGuid();

    private static ElectronicDocumentXml SampleXml() =>
        new(
            Xml: "<comprobanteRetencion/>",
            Encoding: "UTF-8",
            Version: "1.0.0",
            DocumentType: ElectronicDocumentType.Retention,
            Environment: "1",
            AccessKey: new string('1', 49),
            GeneratedAtUtc: DateTime.UtcNow
        );

    /// <summary>ADR-036 (D-9): acceso concedido por el origen — el control fino vive en RetentionSourceAccessTests.</summary>
    private static Mock<IRetentionSourceAccess> ViewableAccess()
    {
        var access = new Mock<IRetentionSourceAccess>();
        access
            .Setup(a => a.FindViewableAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(
                ERP.Domain.Modules.Retentions.Entities.RetentionDocument.Create(
                    TenantId,
                    CompanyId,
                    Guid.NewGuid(),
                    ERP.Domain.Modules.Retentions.Enums.RetentionSourceDocumentType.PurchaseInvoice,
                    Guid.NewGuid(),
                    Guid.NewGuid(),
                    Guid.NewGuid(),
                    Guid.NewGuid()
                )
            );
        return access;
    }

    private sealed class Fixture
    {
        public Mock<IRetentionSourceAccess> Access { get; } = ViewableAccess();
        public Mock<IRetentionElectronicDocumentXmlService> XmlService { get; } = new();
        public Mock<IRetentionRidePdfService> PdfService { get; } = new();

        public GenerateRetentionRidePdfHandler Handler =>
            new(
                XmlService.Object,
                PdfService.Object,
                Mock.Of<ICurrentTenant>(t => t.TenantId == TenantId),
                Mock.Of<ICurrentCompany>(c => c.CompanyId == CompanyId),
                Access.Object
            );

        public void SetupSuccessfulXml(ElectronicDocumentXml xml) =>
            XmlService
                .Setup(s =>
                    s.GenerateXmlAsync(
                        new ElectronicDocumentSourceReference(TenantId, CompanyId, RetentionId),
                        It.IsAny<CancellationToken>()
                    )
                )
                .ReturnsAsync(Result<ElectronicDocumentXml>.Success(xml));
    }

    [Fact]
    public async Task Handle_generates_a_non_empty_pdf_by_chaining_xml_then_pdf_service()
    {
        var fx = new Fixture();
        var xml = SampleXml();
        fx.SetupSuccessfulXml(xml);
        byte[] pdfBytes = [1, 2, 3, 4];
        fx.PdfService.Setup(p =>
                p.GeneratePdfAsync(
                    xml.Xml,
                    TenantId,
                    CompanyId,
                    null,
                    null,
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(Result<byte[]>.Success(pdfBytes));

        var result = await fx.Handler.Handle(
            new GenerateRetentionRidePdfQuery(RetentionId),
            CancellationToken.None
        );

        result.IsSuccess.Should().BeTrue(result.Error);
        result.Value.Should().NotBeEmpty();
        result.Value.Should().Equal(pdfBytes);
    }

    [Fact]
    public async Task Handle_propagates_the_xml_service_failure_without_calling_the_pdf_service()
    {
        var fx = new Fixture();
        fx.XmlService.Setup(s =>
                s.GenerateXmlAsync(
                    It.IsAny<ElectronicDocumentSourceReference>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(
                Result<ElectronicDocumentXml>.ValidationFailure(
                    "La retención debe estar emitida para generar el documento electrónico."
                )
            );

        var result = await fx.Handler.Handle(
            new GenerateRetentionRidePdfQuery(RetentionId),
            CancellationToken.None
        );

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("emitida");
        fx.PdfService.Verify(
            p =>
                p.GeneratePdfAsync(
                    It.IsAny<string>(),
                    It.IsAny<Guid>(),
                    It.IsAny<Guid>(),
                    It.IsAny<Guid?>(),
                    It.IsAny<Guid?>(),
                    It.IsAny<CancellationToken>()
                ),
            Times.Never
        );
    }

    [Fact]
    public async Task Handle_propagates_the_pdf_service_failure()
    {
        var fx = new Fixture();
        var xml = SampleXml();
        fx.SetupSuccessfulXml(xml);
        fx.PdfService.Setup(p =>
                p.GeneratePdfAsync(
                    xml.Xml,
                    TenantId,
                    CompanyId,
                    null,
                    null,
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(Result<byte[]>.Failure("No se pudo resolver el branding del RIDE."));

        var result = await fx.Handler.Handle(
            new GenerateRetentionRidePdfQuery(RetentionId),
            CancellationToken.None
        );

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Be("No se pudo resolver el branding del RIDE.");
    }

    [Fact]
    public async Task Handle_returns_not_found_without_generating_when_the_origin_is_not_viewable()
    {
        var fx = new Fixture();
        fx.Access.Setup(a => a.FindViewableAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ERP.Domain.Modules.Retentions.Entities.RetentionDocument?)null);

        var result = await fx.Handler.Handle(
            new GenerateRetentionRidePdfQuery(RetentionId),
            CancellationToken.None
        );

        result.IsSuccess.Should().BeFalse();
        result.Code.Should().Be(ApiResponseCodes.Common.NotFound);
        fx.XmlService.Verify(
            s =>
                s.GenerateXmlAsync(
                    It.IsAny<ElectronicDocumentSourceReference>(),
                    It.IsAny<CancellationToken>()
                ),
            Times.Never
        );
    }
}

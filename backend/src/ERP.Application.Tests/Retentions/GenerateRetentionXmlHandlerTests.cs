using ERP.Application.Common;
using ERP.Application.Modules.ElectronicDocuments.DTOs;
using ERP.Application.Modules.ElectronicDocuments.Services;
using ERP.Application.Modules.Retentions.Services;
using ERP.Application.Modules.Retentions.UseCases;
using ERP.Domain.Modules.ElectronicDocuments.Enums;
using FluentAssertions;
using Moq;

namespace ERP.Application.Tests.Retentions;

/// <summary>
/// RETENTIONS-ELECTRONIC-ENDPOINTS-03F — cubre <see cref="GenerateRetentionXmlHandler"/>: delega
/// íntegramente en <see cref="IRetentionElectronicDocumentXmlService"/>, resolviendo
/// tenant/company del contexto actual (nunca del body/query).
/// </summary>
public sealed class GenerateRetentionXmlHandlerTests
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
            AccessKey: new string('1', 49),
            GeneratedAtUtc: DateTime.UtcNow
        );

    private sealed class Fixture
    {
        public Mock<IRetentionElectronicDocumentXmlService> XmlService { get; } = new();

        public GenerateRetentionXmlHandler Handler =>
            new(
                XmlService.Object,
                Mock.Of<ICurrentTenant>(t => t.TenantId == TenantId),
                Mock.Of<ICurrentCompany>(c => c.CompanyId == CompanyId)
            );
    }

    [Fact]
    public async Task Handle_generates_xml_using_the_current_tenant_and_company_from_context()
    {
        var fx = new Fixture();
        var xml = SampleXml();
        fx.XmlService
            .Setup(s =>
                s.GenerateXmlAsync(
                    new ElectronicDocumentSourceReference(TenantId, CompanyId, RetentionId),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(Result<ElectronicDocumentXml>.Success(xml));

        var result = await fx.Handler.Handle(
            new GenerateRetentionXmlQuery(RetentionId),
            CancellationToken.None
        );

        result.IsSuccess.Should().BeTrue(result.Error);
        result.Value.Should().Be(xml);
        fx.XmlService.Verify(
            s =>
                s.GenerateXmlAsync(
                    new ElectronicDocumentSourceReference(TenantId, CompanyId, RetentionId),
                    It.IsAny<CancellationToken>()
                ),
            Times.Once
        );
    }

    [Fact]
    public async Task Handle_propagates_a_not_found_failure_for_an_unknown_retention()
    {
        var fx = new Fixture();
        fx.XmlService
            .Setup(s =>
                s.GenerateXmlAsync(
                    It.IsAny<ElectronicDocumentSourceReference>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(Result<ElectronicDocumentXml>.NotFound("La retención no existe."));

        var result = await fx.Handler.Handle(
            new GenerateRetentionXmlQuery(RetentionId),
            CancellationToken.None
        );

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Be("La retención no existe.");
    }

    [Fact]
    public async Task Handle_propagates_a_validation_failure_for_a_draft_retention()
    {
        var fx = new Fixture();
        fx.XmlService
            .Setup(s =>
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
            new GenerateRetentionXmlQuery(RetentionId),
            CancellationToken.None
        );

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("emitida");
    }
}

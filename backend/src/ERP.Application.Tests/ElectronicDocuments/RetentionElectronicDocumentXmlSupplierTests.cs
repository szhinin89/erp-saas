using ERP.Application.Common;
using ERP.Application.Modules.ElectronicDocuments.DTOs;
using ERP.Application.Modules.ElectronicDocuments.Services;
using ERP.Application.Modules.Retentions.Services;
using ERP.Domain.Modules.ElectronicDocuments.Enums;
using FluentAssertions;
using Moq;

namespace ERP.Application.Tests.ElectronicDocuments;

/// <summary>
/// RETENTIONS-SRI-AUTHORIZATION-WIRING-04D — <see cref="RetentionElectronicDocumentXmlSupplier"/>:
/// delega íntegramente en <see cref="IRetentionElectronicDocumentXmlService"/> (03E), sin usar
/// <c>IElectronicDocumentDataProvider</c>/<c>IElectronicDocumentXmlBuilder</c> comerciales.
/// </summary>
public sealed class RetentionElectronicDocumentXmlSupplierTests
{
    private static readonly ElectronicDocumentSourceReference Reference = new(
        Guid.NewGuid(),
        Guid.NewGuid(),
        Guid.NewGuid()
    );

    private static ElectronicDocumentXml SampleRetentionXml() =>
        new(
            Xml: "<comprobanteRetencion/>",
            Encoding: "UTF-8",
            Version: "1.0.0",
            DocumentType: ElectronicDocumentType.Retention,
            Environment: "1",
            AccessKey: new string('1', 49),
            GeneratedAtUtc: DateTime.UtcNow
        );

    [Fact]
    public void DocumentType_is_Retention()
    {
        var service = new Mock<IRetentionElectronicDocumentXmlService>();
        var supplier = new RetentionElectronicDocumentXmlSupplier(service.Object);

        supplier.DocumentType.Should().Be(ElectronicDocumentType.Retention);
    }

    [Fact]
    public async Task BuildXmlAsync_delegates_to_the_retention_xml_service_and_returns_its_xml()
    {
        var xml = SampleRetentionXml();
        var service = new Mock<IRetentionElectronicDocumentXmlService>();
        service
            .Setup(s => s.GenerateXmlAsync(Reference, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<ElectronicDocumentXml>.Success(xml));
        var supplier = new RetentionElectronicDocumentXmlSupplier(service.Object);

        var result = await supplier.BuildXmlAsync(Reference, CancellationToken.None);

        result.IsSuccess.Should().BeTrue(result.Error);
        result.Value.Should().Be(xml);
        result.Value!.DocumentType.Should().Be(ElectronicDocumentType.Retention);
        service.Verify(
            s => s.GenerateXmlAsync(Reference, It.IsAny<CancellationToken>()),
            Times.Once
        );
    }

    [Fact]
    public async Task BuildXmlAsync_propagates_a_service_failure_verbatim()
    {
        var service = new Mock<IRetentionElectronicDocumentXmlService>();
        service
            .Setup(s => s.GenerateXmlAsync(Reference, It.IsAny<CancellationToken>()))
            .ReturnsAsync(
                Result<ElectronicDocumentXml>.ValidationFailure(
                    "La retención debe estar emitida para generar el documento electrónico."
                )
            );
        var supplier = new RetentionElectronicDocumentXmlSupplier(service.Object);

        var result = await supplier.BuildXmlAsync(Reference, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("emitida");
    }
}

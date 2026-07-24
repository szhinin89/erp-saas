using ERP.Application.Common.Interfaces.SRI;
using ERP.Domain.Configuration.Entities;
using ERP.Domain.Configuration.Interfaces;
using ERP.Infrastructure.Modules.Purchases.PurchaseReception;
using FluentAssertions;
using Moq;
using Xunit;

namespace ERP.Infrastructure.Tests.Modules.Purchases.PurchaseReception;

/// <summary>
/// Cubre <see cref="SriReceptionXmlProvider"/> como adaptador puro — con <see cref="ISriAuthorizationClient"/>
/// y <see cref="ISriSettingsRepository"/> mockeados, sin tocar SOAP/HTTP real (eso ya lo cubren los
/// tests propios de <c>SriSoapClient</c>/<c>SriAuthorizationClient</c> en ElectronicDocuments).
/// </summary>
public sealed class SriReceptionXmlProviderTests
{
    private static readonly Guid TenantId  = Guid.NewGuid();
    private static readonly Guid CompanyId = Guid.NewGuid();
    private const string AccessKey = "0107202601179135268800120150270001617400016174011";

    private static SriSettings BuildSettings() =>
        SriSettings.Create(TenantId, CompanyId, environment: 1, emissionType: 1,
            wsdlUrl: "https://celcer.sri.gob.ec/fake?wsdl", createdBy: Guid.NewGuid());

    [Fact]
    public async Task GetAuthorizedXmlAsync_delegates_to_the_existing_authorization_client_with_the_company_wsdl()
    {
        var settingsRepo = new Mock<ISriSettingsRepository>();
        settingsRepo.Setup(r => r.GetByCompanyIdAsync(CompanyId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(BuildSettings());

        var authClient = new Mock<ISriAuthorizationClient>();
        authClient.Setup(c => c.CheckAsync(AccessKey, "https://celcer.sri.gob.ec/fake?wsdl", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SriAuthorizationResult
            {
                Status = "AUTORIZADO",
                AuthorizationNumber = AccessKey,
                AuthorizationDate = new DateTime(2026, 7, 1, 21, 7, 0, DateTimeKind.Utc),
                DocumentXml = "<factura>...</factura>",
            });

        var provider = new SriReceptionXmlProvider(authClient.Object, settingsRepo.Object);
        var result = await provider.GetAuthorizedXmlAsync(TenantId, CompanyId, AccessKey);

        result.Authorized.Should().BeTrue();
        result.XmlContent.Should().Be("<factura>...</factura>");
        result.AuthorizationNumber.Should().Be(AccessKey);
        authClient.Verify(c => c.CheckAsync(AccessKey, "https://celcer.sri.gob.ec/fake?wsdl", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetAuthorizedXmlAsync_returns_an_error_result_when_company_has_no_sri_settings()
    {
        var settingsRepo = new Mock<ISriSettingsRepository>();
        settingsRepo.Setup(r => r.GetByCompanyIdAsync(CompanyId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((SriSettings?)null);
        var authClient = new Mock<ISriAuthorizationClient>();

        var provider = new SriReceptionXmlProvider(authClient.Object, settingsRepo.Object);
        var result = await provider.GetAuthorizedXmlAsync(TenantId, CompanyId, AccessKey);

        result.Authorized.Should().BeFalse();
        result.ErrorMessage.Should().NotBeNullOrEmpty();
        authClient.Verify(c => c.CheckAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GetAuthorizedXmlAsync_propagates_sri_transport_failures_without_throwing()
    {
        var settingsRepo = new Mock<ISriSettingsRepository>();
        settingsRepo.Setup(r => r.GetByCompanyIdAsync(CompanyId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(BuildSettings());

        var authClient = new Mock<ISriAuthorizationClient>();
        authClient.Setup(c => c.CheckAsync(AccessKey, It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SriAuthorizationResult { Status = "ERROR_CONEXION", ErrorMessage = "Timeout de red." });

        var provider = new SriReceptionXmlProvider(authClient.Object, settingsRepo.Object);
        var result = await provider.GetAuthorizedXmlAsync(TenantId, CompanyId, AccessKey);

        result.Authorized.Should().BeFalse();
        result.XmlContent.Should().BeNull();
        result.ErrorMessage.Should().Be("Timeout de red.");
    }
}

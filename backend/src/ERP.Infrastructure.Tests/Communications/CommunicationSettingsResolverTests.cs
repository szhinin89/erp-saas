using ERP.Application.Common;
using ERP.Application.Common.Interfaces;
using ERP.Domain.Configuration.Constants;
using ERP.Domain.Configuration.Enums;
using ERP.Domain.Configuration.Interfaces;
using ERP.Infrastructure.Communications;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Moq;

namespace ERP.Infrastructure.Tests.Communications;

/// <summary>
/// COMMUNICATIONS-SETTINGS-UI-01 — regresión del bug detectado por CONFIG-AUDIT-01: el resolver
/// consultaba OrgSettings con scopeId=Guid.Empty en vez del companyId real, así que nunca leía
/// lo que la empresa guarda vía UpdateCompanyEmailSettingsCommandHandler.
/// </summary>
public sealed class CommunicationSettingsResolverTests
{
    private static readonly Guid CompanyId = Guid.NewGuid();

    private sealed class Fixture
    {
        public Mock<IOrgConfigResolver> OrgConfig { get; } = new();
        public Mock<ICurrentCompany> Company { get; } = new();
        public Mock<ISecretProtector> SecretProtector { get; } = new();
        public IConfiguration Configuration { get; } = new ConfigurationBuilder().Build();

        public Fixture()
        {
            Company.Setup(c => c.CompanyId).Returns(CompanyId);
            SecretProtector.Setup(p => p.UnprotectOrPlaintext(It.IsAny<string>())).Returns<string>(v => v.Replace("dp1:", ""));
            OrgConfig
                .Setup(o => o.GetValueAsync(It.IsAny<OrgScope>(), It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((string?)null);
        }

        public CommunicationSettingsResolver Build() =>
            new(OrgConfig.Object, Configuration, Company.Object, SecretProtector.Object);
    }

    [Fact]
    public async Task ResolveEmailAsync_consulta_OrgSettings_con_el_companyId_real_no_GuidEmpty()
    {
        var f = new Fixture();

        await f.Build().ResolveEmailAsync(CancellationToken.None);

        f.OrgConfig.Verify(
            o => o.GetValueAsync(OrgScope.Company, CompanyId, OrgSettingKeys.Communications.SmtpHost, It.IsAny<CancellationToken>()),
            Times.Once);
        f.OrgConfig.Verify(
            o => o.GetValueAsync(OrgScope.Company, Guid.Empty, It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task ResolveEmailAsync_descifra_el_password_guardado_en_OrgSettings()
    {
        var f = new Fixture();
        f.OrgConfig
            .Setup(o => o.GetValueAsync(OrgScope.Company, CompanyId, OrgSettingKeys.Communications.SmtpPassword, It.IsAny<CancellationToken>()))
            .ReturnsAsync("dp1:plain-secret");

        var settings = await f.Build().ResolveEmailAsync(CancellationToken.None);

        settings.SmtpPassword.Should().Be("plain-secret");
        f.SecretProtector.Verify(p => p.UnprotectOrPlaintext("dp1:plain-secret"), Times.Once);
    }

    [Fact]
    public async Task ResolveEmailAsync_sin_OrgSettings_cae_al_fallback_por_variable_de_entorno()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Communications:Email:Enabled"] = "true",
                ["Communications:Email:SmtpHost"] = "smtp.env-fallback.com",
                ["Communications:Email:SenderEmail"] = "envfallback@empresa.com",
            })
            .Build();

        var orgConfig = new Mock<IOrgConfigResolver>();
        orgConfig
            .Setup(o => o.GetValueAsync(It.IsAny<OrgScope>(), It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string?)null);
        var company = new Mock<ICurrentCompany>();
        company.Setup(c => c.CompanyId).Returns(CompanyId);
        var secretProtector = new Mock<ISecretProtector>();

        var resolver = new CommunicationSettingsResolver(orgConfig.Object, configuration, company.Object, secretProtector.Object);

        var settings = await resolver.ResolveEmailAsync(CancellationToken.None);

        settings.Enabled.Should().BeTrue();
        settings.SmtpHost.Should().Be("smtp.env-fallback.com");
        settings.SenderEmail.Should().Be("envfallback@empresa.com");
    }
}

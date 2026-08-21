using ERP.Application.Common;
using ERP.Application.Common.Interfaces;
using ERP.Application.Modules.Communications.Services;
using ERP.Application.Modules.Communications.UseCases.GetCompanyEmailSettings;
using ERP.Application.Modules.Communications.UseCases.UpdateCompanyEmailSettings;
using ERP.Domain.Configuration.Entities;
using ERP.Domain.Configuration.Enums;
using ERP.Domain.Configuration.Interfaces;
using FluentAssertions;
using Moq;

namespace ERP.Application.Tests.Communications;

/// <summary>COMMUNICATIONS-SETTINGS-UI-01 — Get/Update de configuración SMTP por empresa (OrgSettings, scope=Company).</summary>
public sealed class CommunicationEmailSettingsHandlerTests
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid UserId = Guid.NewGuid();

    private sealed class Fixture
    {
        public Guid CompanyId { get; } = Guid.NewGuid();
        public Mock<IOrgSettingsRepository> Repo { get; } = new();
        public Mock<ICurrentTenant> Tenant { get; } = new();
        public Mock<ICurrentCompany> Company { get; } = new();
        public Mock<ICurrentUser> User { get; } = new();
        public Mock<ISecretProtector> SecretProtector { get; } = new();
        public Mock<ICommunicationSettingsResolver> SettingsResolver { get; } = new();

        public Fixture()
        {
            Tenant.Setup(t => t.TenantId).Returns(TenantId);
            Company.Setup(c => c.CompanyId).Returns(CompanyId);
            User.Setup(u => u.UserId).Returns(UserId);
            SecretProtector.Setup(p => p.Protect(It.IsAny<string>())).Returns<string>(p => $"dp1:{p}");
        }

        public GetCompanyEmailSettingsQueryHandler BuildGetHandler() =>
            new(Repo.Object, Tenant.Object, Company.Object, SettingsResolver.Object);

        public UpdateCompanyEmailSettingsCommandHandler BuildUpdateHandler() =>
            new(Repo.Object, Tenant.Object, Company.Object, User.Object, SecretProtector.Object);
    }

    private static UpdateCompanyEmailSettingsCommand ValidEnabledCommand(string? password = "s3cr3t!") =>
        new(
            Enabled: true,
            SmtpHost: "smtp.zoho.com",
            SmtpPort: 587,
            SmtpUsername: "facturacion@empresa.com",
            SmtpPassword: password,
            SenderEmail: "facturacion@empresa.com",
            SenderName: "Empresa Piloto",
            UseSsl: true,
            ReplyToEmail: null,
            MaxRetries: 3,
            DefaultLanguage: "es"
        );

    [Fact]
    public async Task Get_nunca_devuelve_el_password_en_texto_plano()
    {
        var f = new Fixture();
        f.Repo
            .Setup(r => r.GetAllForScopeAsync(TenantId, f.CompanyId, OrgScope.Company, f.CompanyId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<OrgSetting>().AsReadOnly());
        f.SettingsResolver
            .Setup(s => s.ResolveEmailAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CommunicationEmailSettings(
                true, "smtp.zoho.com", 587, "user", "the-real-password",
                "facturacion@empresa.com", "Empresa", true, null, 3, "es"));

        var result = await f.BuildGetHandler().Handle(new GetCompanyEmailSettingsQuery(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue(result.Error);
        result.Value!.PasswordConfigured.Should().BeTrue();
        result.Value.GetType().GetProperty("SmtpPassword").Should().BeNull("el DTO no debe exponer el campo password en ninguna forma");
    }

    [Fact]
    public async Task Get_sin_filas_propias_reporta_source_EnvironmentFallback()
    {
        var f = new Fixture();
        f.Repo
            .Setup(r => r.GetAllForScopeAsync(TenantId, f.CompanyId, OrgScope.Company, f.CompanyId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<OrgSetting>().AsReadOnly());
        f.SettingsResolver
            .Setup(s => s.ResolveEmailAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CommunicationEmailSettings(false, null, 587, null, null, null, null, true, null, 3, "es"));

        var result = await f.BuildGetHandler().Handle(new GetCompanyEmailSettingsQuery(), CancellationToken.None);

        result.Value!.Source.Should().Be("EnvironmentFallback");
    }

    [Fact]
    public async Task Get_con_filas_propias_reporta_source_OrgSettings()
    {
        var f = new Fixture();
        var stored = OrgSetting.Create(
            TenantId, f.CompanyId, OrgScope.Company, f.CompanyId,
            "communications.email.smtp_host", "smtp.zoho.com", SettingDataType.String, UserId);
        f.Repo
            .Setup(r => r.GetAllForScopeAsync(TenantId, f.CompanyId, OrgScope.Company, f.CompanyId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<OrgSetting> { stored }.AsReadOnly());
        f.SettingsResolver
            .Setup(s => s.ResolveEmailAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CommunicationEmailSettings(true, "smtp.zoho.com", 587, "u", "p", "s@e.com", "E", true, null, 3, "es"));

        var result = await f.BuildGetHandler().Handle(new GetCompanyEmailSettingsQuery(), CancellationToken.None);

        result.Value!.Source.Should().Be("OrgSettings");
    }

    [Fact]
    public async Task Update_guarda_cada_key_con_el_companyId_de_la_empresa_actual()
    {
        var f = new Fixture();
        f.Repo
            .Setup(r => r.GetAsync(TenantId, f.CompanyId, OrgScope.Company, f.CompanyId, "communications.email.smtp_password", It.IsAny<CancellationToken>()))
            .ReturnsAsync((OrgSetting?)null);

        var result = await f.BuildUpdateHandler().Handle(ValidEnabledCommand(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue(result.Error);
        f.Repo.Verify(
            r => r.UpsertAsync(
                It.Is<OrgSetting>(s => s.CompanyId == f.CompanyId && s.TenantId == TenantId && s.ScopeId == f.CompanyId),
                It.IsAny<CancellationToken>()),
            Times.AtLeast(9));
        f.Repo.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Update_cifra_el_password_nuevo_antes_de_persistirlo()
    {
        var f = new Fixture();
        f.Repo
            .Setup(r => r.GetAsync(TenantId, f.CompanyId, OrgScope.Company, f.CompanyId, "communications.email.smtp_password", It.IsAny<CancellationToken>()))
            .ReturnsAsync((OrgSetting?)null);

        await f.BuildUpdateHandler().Handle(ValidEnabledCommand(password: "plain-secret"), CancellationToken.None);

        f.SecretProtector.Verify(p => p.Protect("plain-secret"), Times.Once);
        f.Repo.Verify(
            r => r.UpsertAsync(
                It.Is<OrgSetting>(s => s.Key == "communications.email.smtp_password" && s.Value == "dp1:plain-secret"),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Update_sin_password_nueva_no_toca_la_key_de_password_existente()
    {
        var f = new Fixture();
        var existingPassword = OrgSetting.Create(
            TenantId, f.CompanyId, OrgScope.Company, f.CompanyId,
            "communications.email.smtp_password", "dp1:already-set", SettingDataType.String, UserId);
        f.Repo
            .Setup(r => r.GetAsync(TenantId, f.CompanyId, OrgScope.Company, f.CompanyId, "communications.email.smtp_password", It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingPassword);

        var result = await f.BuildUpdateHandler().Handle(ValidEnabledCommand(password: null), CancellationToken.None);

        result.IsSuccess.Should().BeTrue(result.Error);
        result.Value!.PasswordConfigured.Should().BeTrue();
        f.Repo.Verify(
            r => r.UpsertAsync(
                It.Is<OrgSetting>(s => s.Key == "communications.email.smtp_password"),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Update_enabled_sin_password_configurada_devuelve_ValidationFailure()
    {
        var f = new Fixture();
        f.Repo
            .Setup(r => r.GetAsync(TenantId, f.CompanyId, OrgScope.Company, f.CompanyId, "communications.email.smtp_password", It.IsAny<CancellationToken>()))
            .ReturnsAsync((OrgSetting?)null);

        var result = await f.BuildUpdateHandler().Handle(ValidEnabledCommand(password: null), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Code.Should().Be(ApiResponseCodes.Common.ValidationError);
        f.Repo.Verify(r => r.UpsertAsync(It.IsAny<OrgSetting>(), It.IsAny<CancellationToken>()), Times.Never);
        f.Repo.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Update_empresa_A_nunca_toca_filas_de_empresa_B()
    {
        var companyA = Guid.NewGuid();
        var companyB = Guid.NewGuid();

        var repo = new Mock<IOrgSettingsRepository>();
        var tenant = new Mock<ICurrentTenant>();
        tenant.Setup(t => t.TenantId).Returns(TenantId);
        var company = new Mock<ICurrentCompany>();
        company.Setup(c => c.CompanyId).Returns(companyA);
        var user = new Mock<ICurrentUser>();
        user.Setup(u => u.UserId).Returns(UserId);
        var secretProtector = new Mock<ISecretProtector>();
        secretProtector.Setup(p => p.Protect(It.IsAny<string>())).Returns("dp1:x");

        repo
            .Setup(r => r.GetAsync(TenantId, companyA, OrgScope.Company, companyA, "communications.email.smtp_password", It.IsAny<CancellationToken>()))
            .ReturnsAsync((OrgSetting?)null);

        var handler = new UpdateCompanyEmailSettingsCommandHandler(repo.Object, tenant.Object, company.Object, user.Object, secretProtector.Object);

        await handler.Handle(ValidEnabledCommand(), CancellationToken.None);

        repo.Verify(
            r => r.UpsertAsync(It.Is<OrgSetting>(s => s.CompanyId == companyB), It.IsAny<CancellationToken>()),
            Times.Never);
        repo.Verify(
            r => r.GetAsync(TenantId, companyB, It.IsAny<OrgScope>(), It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }
}

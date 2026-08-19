using ERP.Application.Modules.Media;
using ERP.Domain.Configuration.Constants;
using ERP.Domain.Configuration.Entities;
using ERP.Domain.Configuration.Enums;
using ERP.Domain.Configuration.Interfaces;
using ERP.Domain.Modules.Media.Entities;
using ERP.Domain.Modules.Media.Enums;
using ERP.Infrastructure.Services;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace ERP.Infrastructure.Tests.Services;

/// <summary>
/// CONFIG-FOUNDATION-P1-02 — CompanyBrandingResolver es la única implementación de
/// ICompanyBrandingResolver: compone org_settings (company.branding.*) + MediaFile (logo,
/// Owner=Company, Role="logo"). Nunca duplica el logo en org_settings.
/// </summary>
public sealed class CompanyBrandingResolverTests
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid CompanyId = Guid.NewGuid();
    private static readonly Guid UserId = Guid.NewGuid();

    private sealed class Fixture
    {
        public Mock<IOrgSettingsRepository> OrgRepo { get; } = new();
        public Mock<IMediaService> Media { get; } = new();

        public Fixture()
        {
            OrgRepo
                .Setup(r =>
                    r.GetAllForScopeAsync(
                        TenantId,
                        CompanyId,
                        OrgScope.Company,
                        CompanyId,
                        It.IsAny<CancellationToken>()
                    )
                )
                .ReturnsAsync(Array.Empty<OrgSetting>());
            Media
                .Setup(m =>
                    m.GetActivePrimaryAsync(
                        TenantId,
                        CompanyId,
                        MediaOwnerType.Company,
                        CompanyId,
                        "logo",
                        It.IsAny<CancellationToken>()
                    )
                )
                .ReturnsAsync((MediaFile?)null);
        }

        public CompanyBrandingResolver BuildResolver() =>
            new(OrgRepo.Object, Media.Object, NullLogger<CompanyBrandingResolver>.Instance);

        public void SetOrgSettings(params (string Key, string Value)[] values)
        {
            var settings = values
                .Select(v =>
                    OrgSetting.Create(
                        TenantId,
                        CompanyId,
                        OrgScope.Company,
                        CompanyId,
                        v.Key,
                        v.Value,
                        SettingDataType.String,
                        UserId
                    )
                )
                .ToList();
            OrgRepo
                .Setup(r =>
                    r.GetAllForScopeAsync(
                        TenantId,
                        CompanyId,
                        OrgScope.Company,
                        CompanyId,
                        It.IsAny<CancellationToken>()
                    )
                )
                .ReturnsAsync(settings.AsReadOnly());
        }

        public void SetLogo(MediaFile media) =>
            Media
                .Setup(m =>
                    m.GetActivePrimaryAsync(
                        TenantId,
                        CompanyId,
                        MediaOwnerType.Company,
                        CompanyId,
                        "logo",
                        It.IsAny<CancellationToken>()
                    )
                )
                .ReturnsAsync(media);
    }

    private static MediaFile MakeLogo(string storagePath) =>
        MediaFile.Create(
            tenantId: TenantId,
            companyId: CompanyId,
            fileName: "logo.png",
            originalFileName: "logo.png",
            contentType: "image/png",
            sizeBytes: 1024,
            storageProvider: "local",
            storagePath: storagePath,
            mediaType: MediaType.Image,
            ownerType: MediaOwnerType.Company,
            ownerId: CompanyId,
            role: "logo",
            visibility: MediaVisibility.TenantOnly,
            createdBy: UserId,
            isPrimary: true
        );

    [Fact]
    public async Task Sin_branding_configurado_devuelve_settings_vacios_no_falla()
    {
        var f = new Fixture();

        var result = await f.BuildResolver().GetAsync(TenantId, CompanyId, default);

        result.LogoStoragePath.Should().BeNull();
        result.PrimaryColor.Should().BeNull();
        result.SecondaryColor.Should().BeNull();
        result.Slogan.Should().BeNull();
        result.DocumentFooterText.Should().BeNull();
    }

    [Fact]
    public async Task Con_org_settings_validos_devuelve_los_valores()
    {
        var f = new Fixture();
        f.SetOrgSettings(
            (OrgSettingKeys.CompanyBranding.PrimaryColor, "#112233"),
            (OrgSettingKeys.CompanyBranding.SecondaryColor, "#445566"),
            (OrgSettingKeys.CompanyBranding.Slogan, "Confianza y calidad"),
            (OrgSettingKeys.CompanyBranding.DocumentFooterText, "Gracias por su compra")
        );

        var result = await f.BuildResolver().GetAsync(TenantId, CompanyId, default);

        result.PrimaryColor.Should().Be("#112233");
        result.SecondaryColor.Should().Be("#445566");
        result.Slogan.Should().Be("Confianza y calidad");
        result.DocumentFooterText.Should().Be("Gracias por su compra");
    }

    [Fact]
    public async Task Logo_MediaFile_existente_se_resuelve_como_LogoStoragePath()
    {
        var f = new Fixture();
        f.SetLogo(MakeLogo("company/abc123/logo.png"));

        var result = await f.BuildResolver().GetAsync(TenantId, CompanyId, default);

        result.LogoStoragePath.Should().Be("company/abc123/logo.png");
    }

    [Theory]
    [InlineData("not-a-color")]
    [InlineData("112233")]
    [InlineData("#12")]
    public async Task Color_invalido_en_org_settings_cae_a_null_con_warning_nunca_lanza(
        string invalidHex
    )
    {
        var f = new Fixture();
        f.SetOrgSettings((OrgSettingKeys.CompanyBranding.PrimaryColor, invalidHex));

        var result = await f.BuildResolver().GetAsync(TenantId, CompanyId, default);

        result.PrimaryColor.Should().BeNull();
    }
}

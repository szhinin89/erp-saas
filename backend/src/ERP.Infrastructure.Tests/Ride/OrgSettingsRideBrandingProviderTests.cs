using ERP.Domain.Configuration.Constants;
using ERP.Domain.Configuration.Entities;
using ERP.Domain.Configuration.Enums;
using ERP.Domain.Configuration.Interfaces;
using ERP.Infrastructure.Ride.Branding;
using FluentAssertions;
using Moq;

namespace ERP.Infrastructure.Tests.Ride;

public sealed class OrgSettingsRideBrandingProviderTests
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid CompanyId = Guid.NewGuid();
    private static readonly Guid UserId = Guid.NewGuid();

    private static OrgSetting Setting(string key, string value) =>
        OrgSetting.Create(TenantId, CompanyId, OrgScope.Company, CompanyId, key, value, SettingDataType.String, UserId);

    [Fact]
    public async Task Existing_branding_settings_populate_RideBranding()
    {
        var repo = new Mock<IOrgSettingsRepository>();
        repo.Setup(r => r.GetAllForScopeAsync(TenantId, CompanyId, OrgScope.Company, CompanyId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(
            [
                Setting(OrgSettingKeys.Ride.LogoStoragePath, "branding/logo.png"),
                Setting(OrgSettingKeys.Ride.PrimaryColorHex, "#112233"),
                Setting(OrgSettingKeys.Ride.SecondaryColorHex, "#445566"),
                Setting(OrgSettingKeys.Ride.FooterText, "Gracias por su compra"),
            ]);
        var provider = new OrgSettingsRideBrandingProvider(repo.Object);

        var result = await provider.GetAsync(TenantId, CompanyId, null, null);

        result.IsSuccess.Should().BeTrue(result.Error);
        result.Value!.LogoStoragePath.Should().Be("branding/logo.png");
        result.Value.PrimaryColorHex.Should().Be("#112233");
        result.Value.SecondaryColorHex.Should().Be("#445566");
        result.Value.FooterText.Should().Be("Gracias por su compra");
    }

    [Fact]
    public async Task No_configured_settings_returns_empty_branding_not_an_error()
    {
        var repo = new Mock<IOrgSettingsRepository>();
        repo.Setup(r => r.GetAllForScopeAsync(TenantId, CompanyId, OrgScope.Company, CompanyId, It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
        var provider = new OrgSettingsRideBrandingProvider(repo.Object);

        var result = await provider.GetAsync(TenantId, CompanyId, null, null);

        result.IsSuccess.Should().BeTrue(result.Error);
        result.Value!.LogoStoragePath.Should().BeNull();
        result.Value.PrimaryColorHex.Should().BeNull();
        result.Value.SecondaryColorHex.Should().BeNull();
        result.Value.FooterText.Should().BeNull();
    }

    [Fact]
    public async Task Blank_setting_values_are_treated_as_absent()
    {
        var repo = new Mock<IOrgSettingsRepository>();
        repo.Setup(r => r.GetAllForScopeAsync(TenantId, CompanyId, OrgScope.Company, CompanyId, It.IsAny<CancellationToken>()))
            .ReturnsAsync([Setting(OrgSettingKeys.Ride.FooterText, "   ")]);
        var provider = new OrgSettingsRideBrandingProvider(repo.Object);

        var result = await provider.GetAsync(TenantId, CompanyId, null, null);

        result.Value!.FooterText.Should().BeNull();
    }
}

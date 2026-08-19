using ERP.Domain.Modules.Company.Interfaces;
using ERP.Infrastructure.Ride.Branding;
using FluentAssertions;
using Moq;

namespace ERP.Infrastructure.Tests.Ride;

/// <summary>
/// CONFIG-FOUNDATION-P1-02 — CompanyBrandingRideProvider (antes OrgSettingsRideBrandingProvider)
/// ya no lee org_settings/MediaFile directamente: solo traduce ICompanyBrandingResolver a
/// RideBranding. Estos tests confirman esa traducción y que el provider nunca conoce las keys
/// company.branding.* — solo el objeto ya resuelto.
/// </summary>
public sealed class CompanyBrandingRideProviderTests
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid CompanyId = Guid.NewGuid();

    [Fact]
    public async Task Traduce_CompanyBrandingSettings_resuelto_a_RideBranding()
    {
        var resolver = new Mock<ICompanyBrandingResolver>();
        resolver
            .Setup(r => r.GetAsync(TenantId, CompanyId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(
                new CompanyBrandingSettings(
                    LogoStoragePath: "branding/logo.png",
                    PrimaryColor: "#112233",
                    SecondaryColor: "#445566",
                    Slogan: "Confianza y calidad",
                    DocumentFooterText: "Gracias por su compra"
                )
            );
        var provider = new CompanyBrandingRideProvider(resolver.Object);

        var result = await provider.GetAsync(TenantId, CompanyId, null, null);

        result.IsSuccess.Should().BeTrue(result.Error);
        result.Value!.LogoStoragePath.Should().Be("branding/logo.png");
        result.Value.PrimaryColorHex.Should().Be("#112233");
        result.Value.SecondaryColorHex.Should().Be("#445566");
        result.Value.FooterText.Should().Be("Gracias por su compra");
    }

    [Fact]
    public async Task Sin_marca_configurada_devuelve_RideBranding_vacio_no_un_error()
    {
        var resolver = new Mock<ICompanyBrandingResolver>();
        resolver
            .Setup(r => r.GetAsync(TenantId, CompanyId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CompanyBrandingSettings.Empty());
        var provider = new CompanyBrandingRideProvider(resolver.Object);

        var result = await provider.GetAsync(TenantId, CompanyId, null, null);

        result.IsSuccess.Should().BeTrue(result.Error);
        result.Value!.LogoStoragePath.Should().BeNull();
        result.Value.PrimaryColorHex.Should().BeNull();
        result.Value.SecondaryColorHex.Should().BeNull();
        result.Value.FooterText.Should().BeNull();
    }

    [Fact]
    public async Task Solo_depende_de_ICompanyBrandingResolver_nunca_de_OrgSettings_o_MediaFile()
    {
        // Prueba estructural: el constructor de CompanyBrandingRideProvider solo acepta
        // ICompanyBrandingResolver — si alguna vez alguien reintroduce IOrgSettingsRepository o
        // IMediaService aquí, este test deja de compilar con un solo parámetro.
        var resolver = new Mock<ICompanyBrandingResolver>();
        resolver
            .Setup(r => r.GetAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(CompanyBrandingSettings.Empty());

        var provider = new CompanyBrandingRideProvider(resolver.Object);
        var result = await provider.GetAsync(TenantId, CompanyId, null, null);

        result.IsSuccess.Should().BeTrue();
        resolver.Verify(
            r => r.GetAsync(TenantId, CompanyId, It.IsAny<CancellationToken>()),
            Times.Once
        );
    }
}

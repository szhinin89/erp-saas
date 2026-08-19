using ERP.Application.Common;
using ERP.Application.Modules.Ride.Branding;
using ERP.Domain.Modules.Company.Interfaces;
using ERP.Domain.Modules.Ride.ValueObjects;

namespace ERP.Infrastructure.Ride.Branding;

/// <summary>
/// CONFIG-FOUNDATION-P1-02: adapta la marca de empresa (<see cref="ICompanyBrandingResolver"/>,
/// fuente única — Company Branding es owner) a <see cref="RideBranding"/> para el pipeline de
/// RIDE. Reemplaza a <c>OrgSettingsRideBrandingProvider</c>, que leía <c>ride.branding.*</c>
/// directamente de <c>org_settings</c> — ese namespace nunca tuvo un flujo de escritura real y
/// quedaba desconectado de la marca que el usuario configuraba en Configuración → Empresa →
/// Marca. RIDE ya no lee <c>org_settings</c> ni <c>MediaFile</c> por sí mismo — solo conoce este
/// adaptador y el objeto ya resuelto.
///
/// v1.0 resuelve únicamente a nivel Empresa (<see cref="ICompanyBrandingResolver"/> no expone
/// scope Branch/EmissionPoint) — <paramref name="branchId"/>/<paramref name="emissionPointId"/>
/// quedan sin usar hasta que exista la jerarquía completa de ADR-025 §11 (diseño futuro, no esta
/// fase).
/// </summary>
public sealed class CompanyBrandingRideProvider : IRideBrandingProvider
{
    private readonly ICompanyBrandingResolver _brandingResolver;

    public CompanyBrandingRideProvider(ICompanyBrandingResolver brandingResolver) =>
        _brandingResolver = brandingResolver;

    public async Task<Result<RideBranding>> GetAsync(
        Guid tenantId,
        Guid companyId,
        Guid? branchId,
        Guid? emissionPointId,
        CancellationToken ct = default
    )
    {
        var settings = await _brandingResolver.GetAsync(tenantId, companyId, ct);

        var branding = RideBranding.Create(
            logoStoragePath: settings.LogoStoragePath,
            primaryColorHex: settings.PrimaryColor,
            secondaryColorHex: settings.SecondaryColor,
            footerText: settings.DocumentFooterText
        );

        return Result<RideBranding>.Success(branding);
    }
}

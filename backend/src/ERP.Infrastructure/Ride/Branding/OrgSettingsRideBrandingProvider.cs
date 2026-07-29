using ERP.Application.Common;
using ERP.Application.Modules.Ride.Branding;
using ERP.Domain.Configuration.Constants;
using ERP.Domain.Configuration.Enums;
using ERP.Domain.Configuration.Interfaces;
using ERP.Domain.Modules.Ride.ValueObjects;

namespace ERP.Infrastructure.Ride.Branding;

/// <summary>
/// Resuelve el branding del RIDE desde <c>org_settings</c> (misma infraestructura ya usada por
/// "Valores por Defecto de Facturación", FROZEN) — nunca lee archivos de disco directamente, nunca
/// crea configuración nueva. Reemplaza a <c>NullRideBrandingProvider</c> (Fase 4).
///
/// v1.0 resuelve únicamente a nivel Empresa (<see cref="OrgScope.Company"/>) — <paramref
/// name="branchId"/>/<paramref name="emissionPointId"/> quedan sin usar hasta que exista la
/// jerarquía completa de ADR-025 §11 (diseño futuro, no esta fase).
/// </summary>
public sealed class OrgSettingsRideBrandingProvider : IRideBrandingProvider
{
    private readonly IOrgSettingsRepository _orgSettingsRepository;

    public OrgSettingsRideBrandingProvider(IOrgSettingsRepository orgSettingsRepository) =>
        _orgSettingsRepository = orgSettingsRepository;

    public async Task<Result<RideBranding>> GetAsync(
        Guid tenantId,
        Guid companyId,
        Guid? branchId,
        Guid? emissionPointId,
        CancellationToken ct = default
    )
    {
        var settings = await _orgSettingsRepository.GetAllForScopeAsync(
            tenantId,
            companyId,
            OrgScope.Company,
            companyId,
            ct
        );
        var lookup = settings.ToDictionary(s => s.Key, s => s.Value);

        string? Resolve(string key) =>
            lookup.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value)
                ? value
                : null;

        var branding = RideBranding.Create(
            logoStoragePath: Resolve(OrgSettingKeys.Ride.LogoStoragePath),
            primaryColorHex: Resolve(OrgSettingKeys.Ride.PrimaryColorHex),
            secondaryColorHex: Resolve(OrgSettingKeys.Ride.SecondaryColorHex),
            footerText: Resolve(OrgSettingKeys.Ride.FooterText)
        );

        return Result<RideBranding>.Success(branding);
    }
}

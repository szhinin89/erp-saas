using System.Text.RegularExpressions;
using ERP.Application.Modules.Media;
using ERP.Domain.Configuration.Constants;
using ERP.Domain.Configuration.Enums;
using ERP.Domain.Configuration.Interfaces;
using ERP.Domain.Modules.Company.Interfaces;
using ERP.Domain.Modules.Media.Enums;
using Microsoft.Extensions.Logging;

namespace ERP.Infrastructure.Services;

/// <summary>
/// CONFIG-FOUNDATION-P1-02: implementación única de <see cref="ICompanyBrandingResolver"/>.
/// Compone <c>org_settings</c> (colores/eslogan/pie de página, scope Company, namespace
/// <see cref="OrgSettingKeys.CompanyBranding"/>) y <c>MediaFile</c> (logo, Owner=Company,
/// Role="logo") — nunca guarda el logo en org_settings.
/// </summary>
public sealed partial class CompanyBrandingResolver : ICompanyBrandingResolver
{
    private const string LogoRole = "logo";

    [GeneratedRegex("^#([0-9A-Fa-f]{3}|[0-9A-Fa-f]{6})$")]
    private static partial Regex HexColorRegex();

    private readonly IOrgSettingsRepository _orgSettingsRepository;
    private readonly IMediaService _mediaService;
    private readonly ILogger<CompanyBrandingResolver> _logger;

    public CompanyBrandingResolver(
        IOrgSettingsRepository orgSettingsRepository,
        IMediaService mediaService,
        ILogger<CompanyBrandingResolver> logger
    )
    {
        _orgSettingsRepository = orgSettingsRepository;
        _mediaService = mediaService;
        _logger = logger;
    }

    public async Task<CompanyBrandingSettings> GetAsync(
        Guid tenantId,
        Guid companyId,
        CancellationToken cancellationToken = default
    )
    {
        var settings = await _orgSettingsRepository.GetAllForScopeAsync(
            tenantId,
            companyId,
            OrgScope.Company,
            companyId,
            cancellationToken
        );
        var lookup = settings.ToDictionary(s => s.Key, s => s.Value);

        string? ResolveText(string key) =>
            lookup.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value)
                ? value
                : null;

        string? ResolveColor(string key)
        {
            var raw = ResolveText(key);
            if (raw is null)
                return null;

            if (!HexColorRegex().IsMatch(raw))
            {
                _logger.LogWarning(
                    "OrgSetting {Key} tiene un color de marca inválido ({RawValue}) — se omite (sin color, nunca un color inválido en PDF).",
                    key,
                    raw
                );
                return null;
            }

            return raw;
        }

        var logo = await _mediaService.GetActivePrimaryAsync(
            tenantId,
            companyId,
            MediaOwnerType.Company,
            companyId,
            LogoRole,
            cancellationToken
        );

        return new CompanyBrandingSettings(
            LogoStoragePath: logo?.StoragePath,
            PrimaryColor: ResolveColor(OrgSettingKeys.CompanyBranding.PrimaryColor),
            SecondaryColor: ResolveColor(OrgSettingKeys.CompanyBranding.SecondaryColor),
            Slogan: ResolveText(OrgSettingKeys.CompanyBranding.Slogan),
            DocumentFooterText: ResolveText(OrgSettingKeys.CompanyBranding.DocumentFooterText)
        );
    }
}

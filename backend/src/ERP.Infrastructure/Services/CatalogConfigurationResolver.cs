using ERP.Domain.Configuration.Constants;
using ERP.Domain.Configuration.Enums;
using ERP.Domain.Configuration.Interfaces;
using ERP.Domain.Modules.Items.Interfaces;

namespace ERP.Infrastructure.Services;

/// <summary>CONFIG-FOUNDATION-P1-04: única implementación de <see cref="ICatalogConfigurationResolver"/>.</summary>
public sealed class CatalogConfigurationResolver : ICatalogConfigurationResolver
{
    public const int DefaultMaxCategoryDepth = 3;

    private readonly IOrgSettingsRepository _orgRepo;

    public CatalogConfigurationResolver(IOrgSettingsRepository orgRepo) => _orgRepo = orgRepo;

    public async Task<int> ResolveMaxCategoryDepthAsync(
        Guid tenantId,
        Guid companyId,
        CancellationToken cancellationToken = default
    )
    {
        var setting = await _orgRepo.GetAsync(
            tenantId,
            companyId,
            OrgScope.Company,
            companyId,
            OrgSettingKeys.Catalog.MaxCategoryDepth,
            cancellationToken
        );

        return setting is not null && int.TryParse(setting.Value, out var value) && value > 0
            ? value
            : DefaultMaxCategoryDepth;
    }
}

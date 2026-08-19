using ERP.Domain.Configuration.Constants;
using ERP.Domain.Configuration.Enums;

namespace ERP.Domain.Configuration.Definitions.Modules;

/// <summary>Definitions para OrgSettingKeys.Catalog.</summary>
public static class CatalogConfigurationDefinitions
{
    public static IEnumerable<ConfigurationDefinition> All()
    {
        yield return new ConfigurationDefinition
        {
            Key = OrgSettingKeys.Catalog.MaxCategoryDepth,
            Module = "Catalog",
            DataType = ConfigurationDataType.Int,
            AllowedScopes = [OrgScope.Company],
            DefaultScope = OrgScope.Company,
            DefaultValue = "3",
            FallbackStrategy = ConfigurationFallbackStrategy.SystemDefault,
            RequiresAudit = false,
            Validator = value => int.TryParse(value, out var v) && v > 0,
            DeveloperNotes = "Default hardcodeado 3 en CreateCategoryNodeCommandHandler si ausente/inválido.",
        };
    }
}

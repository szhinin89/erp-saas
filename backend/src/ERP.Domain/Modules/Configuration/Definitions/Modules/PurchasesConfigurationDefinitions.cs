using ERP.Domain.Configuration.Constants;
using ERP.Domain.Configuration.Enums;

namespace ERP.Domain.Configuration.Definitions.Modules;

/// <summary>
/// CONFIG-DYNAMIC-OPERATIONS-01: Definitions para OrgSettingKeys.Purchases. Ninguna de estas
/// keys tiene efecto real conectado todavía (Fase C) — se guardan y exponen en
/// /settings/operations, marcadas como "aún no conectado" en la UI.
/// </summary>
public static class PurchasesConfigurationDefinitions
{
    public static IEnumerable<ConfigurationDefinition> All()
    {
        yield return Guid(OrgSettingKeys.Purchases.DefaultWarehouseId);
        yield return Bool(OrgSettingKeys.Purchases.AllowConfirmWithoutReceptionXml, "true");
        yield return Bool(OrgSettingKeys.Purchases.UpdateCostOnConfirm, "true");
        yield return Bool(OrgSettingKeys.Purchases.AllowManualCostChange, "true");
        yield return Bool(OrgSettingKeys.Purchases.RequireReasonForCostChange, "false");
    }

    private static ConfigurationDefinition Bool(string key, string defaultValue) =>
        new()
        {
            Key = key,
            Module = "Purchases",
            DataType = ConfigurationDataType.Bool,
            AllowedScopes = [OrgScope.Company],
            DefaultScope = OrgScope.Company,
            DefaultValue = defaultValue,
            FallbackStrategy = ConfigurationFallbackStrategy.SystemDefault,
            RequiresAudit = true,
            Validator = value => bool.TryParse(value, out _),
        };

    private static ConfigurationDefinition Guid(string key) =>
        new()
        {
            Key = key,
            Module = "Purchases",
            DataType = ConfigurationDataType.Guid,
            AllowedScopes = [OrgScope.Company],
            DefaultScope = OrgScope.Company,
            FallbackStrategy = ConfigurationFallbackStrategy.RequireManualSelection,
            RequiresAudit = true,
            Validator = value => System.Guid.TryParse(value, out _),
        };
}

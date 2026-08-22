using ERP.Domain.Configuration.Constants;
using ERP.Domain.Configuration.Enums;
using System.Globalization;

namespace ERP.Domain.Configuration.Definitions.Modules;

/// <summary>
/// CONFIG-DYNAMIC-OPERATIONS-01: Definitions para OrgSettingKeys.Cash. Solo
/// RequireReasonForDifference tiene efecto real conectado (Fase B) — el resto se guarda y expone
/// en /settings/operations pero aún no cambia comportamiento (Fase C).
/// </summary>
public static class CashConfigurationDefinitions
{
    public static IEnumerable<ConfigurationDefinition> All()
    {
        yield return Bool(OrgSettingKeys.Cash.RequireOpeningAmount, "true");
        yield return Bool(OrgSettingKeys.Cash.AllowCloseWithDifference, "true");
        yield return Decimal(
            OrgSettingKeys.Cash.MaxAllowedDifference,
            value => IsDecimalInRange(value, 0m, decimal.MaxValue),
            "0"
        );
        yield return Bool(OrgSettingKeys.Cash.RequireReasonForDifference, "true");
        yield return Bool(OrgSettingKeys.Cash.AllowManualInOutMovements, "true");
        yield return Bool(OrgSettingKeys.Cash.RequireReasonForMovements, "true");
    }

    private static ConfigurationDefinition Bool(string key, string defaultValue) =>
        new()
        {
            Key = key,
            Module = "Cash",
            DataType = ConfigurationDataType.Bool,
            AllowedScopes = [OrgScope.Company],
            DefaultScope = OrgScope.Company,
            DefaultValue = defaultValue,
            FallbackStrategy = ConfigurationFallbackStrategy.SystemDefault,
            RequiresAudit = true,
            Validator = value => bool.TryParse(value, out _),
        };

    private static ConfigurationDefinition Decimal(
        string key,
        Func<string?, bool> validator,
        string? defaultValue
    ) =>
        new()
        {
            Key = key,
            Module = "Cash",
            DataType = ConfigurationDataType.Decimal,
            AllowedScopes = [OrgScope.Company],
            DefaultScope = OrgScope.Company,
            DefaultValue = defaultValue,
            FallbackStrategy = ConfigurationFallbackStrategy.SystemDefault,
            RequiresAudit = true,
            Validator = validator,
        };

    private static bool IsDecimalInRange(string? value, decimal min, decimal max) =>
        decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out var parsed)
        && parsed >= min
        && parsed <= max;
}

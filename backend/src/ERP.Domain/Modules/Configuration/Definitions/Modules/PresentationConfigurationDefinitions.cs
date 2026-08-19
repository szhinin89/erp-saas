using ERP.Domain.Configuration.Constants;
using ERP.Domain.Configuration.Enums;

namespace ERP.Domain.Configuration.Definitions.Modules;

/// <summary>
/// Definitions para OrgSettingKeys.Presentation — decimales de PRESENTACIÓN (CONFIG-FOUNDATION-P1-01).
/// Nunca fiscales: FiscalPrecision es constante System, sin relación con este módulo.
/// </summary>
public static class PresentationConfigurationDefinitions
{
    private const int MinDecimals = 0;
    private const int MaxDecimals = 6;

    private static bool IsValidDecimalCount(string? value) =>
        int.TryParse(value, out var v) && v >= MinDecimals && v <= MaxDecimals;

    public static IEnumerable<ConfigurationDefinition> All()
    {
        string[] keys =
        [
            OrgSettingKeys.Presentation.DecimalSalesUnitPrice,
            OrgSettingKeys.Presentation.DecimalPurchaseUnitPrice,
            OrgSettingKeys.Presentation.DecimalQuantity,
            OrgSettingKeys.Presentation.DecimalPercentage,
            OrgSettingKeys.Presentation.DecimalTotalAmount,
        ];

        foreach (var key in keys)
        {
            yield return new ConfigurationDefinition
            {
                Key = key,
                Module = "Presentation",
                DataType = ConfigurationDataType.Int,
                AllowedScopes = [OrgScope.Company],
                DefaultScope = OrgScope.Company,
                FallbackStrategy = ConfigurationFallbackStrategy.VisualSafeDefault,
                RequiresAudit = false,
                Validator = IsValidDecimalCount,
                DeveloperNotes = "Rango 0-6. Nunca usar para redondeo fiscal — ver FiscalPrecision (ERP.Domain.Common).",
            };
        }
    }
}

using System.Globalization;
using ERP.Domain.Configuration.Constants;
using ERP.Domain.Configuration.Enums;

namespace ERP.Domain.Configuration.Definitions.Modules;

/// <summary>Definitions para OrgSettingKeys.Sales.</summary>
public static class SalesConfigurationDefinitions
{
    public static IEnumerable<ConfigurationDefinition> All()
    {
        yield return new ConfigurationDefinition
        {
            Key = OrgSettingKeys.Sales.ConsumerFinalMaxAmount,
            Module = "Sales",
            DataType = ConfigurationDataType.Decimal,
            AllowedScopes = [OrgScope.Company],
            DefaultScope = OrgScope.Company,
            FallbackStrategy = ConfigurationFallbackStrategy.DomainDefault,
            RequiresAudit = true,
            Validator = value =>
                decimal.TryParse(
                    value,
                    NumberStyles.Number,
                    CultureInfo.InvariantCulture,
                    out var parsed
                ) && parsed >= 0m,
            DeveloperNotes = "\"0\" es un valor manual válido (bloquea Consumidor Final a crédito), distinto de ausencia de fila. Fallback: régimen tributario vía ConsumerFinalPolicyDefaults.",
        };
    }
}

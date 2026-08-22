using ERP.Domain.Configuration.Constants;
using ERP.Domain.Configuration.Enums;

namespace ERP.Domain.Configuration.Definitions.Modules;

/// <summary>
/// CONFIG-DYNAMIC-OPERATIONS-01: Definitions para OrgSettingKeys.ElectronicDocuments. Solo
/// EmailOnAuthorization tiene efecto real conectado (Fase B, ver
/// SalesInvoiceAuthorizedCommunicationHandler). El resto es Fase C.
/// </summary>
public static class ElectronicDocumentsConfigurationDefinitions
{
    public static IEnumerable<ConfigurationDefinition> All()
    {
        yield return Bool(OrgSettingKeys.ElectronicDocuments.AutoRetryEnabled, "true");
        yield return Int(
            OrgSettingKeys.ElectronicDocuments.MaxRetryAttempts,
            value => IsIntInRange(value, 1, 10),
            "3"
        );
        yield return Bool(OrgSettingKeys.ElectronicDocuments.GenerateRideOnAuthorization, "true");
        yield return Bool(OrgSettingKeys.ElectronicDocuments.EmailOnAuthorization, "true");
    }

    private static ConfigurationDefinition Bool(string key, string defaultValue) =>
        new()
        {
            Key = key,
            Module = "ElectronicDocuments",
            DataType = ConfigurationDataType.Bool,
            AllowedScopes = [OrgScope.Company],
            DefaultScope = OrgScope.Company,
            DefaultValue = defaultValue,
            FallbackStrategy = ConfigurationFallbackStrategy.SystemDefault,
            RequiresAudit = true,
            Validator = value => bool.TryParse(value, out _),
        };

    private static ConfigurationDefinition Int(
        string key,
        Func<string?, bool> validator,
        string defaultValue
    ) =>
        new()
        {
            Key = key,
            Module = "ElectronicDocuments",
            DataType = ConfigurationDataType.Int,
            AllowedScopes = [OrgScope.Company],
            DefaultScope = OrgScope.Company,
            DefaultValue = defaultValue,
            FallbackStrategy = ConfigurationFallbackStrategy.SystemDefault,
            RequiresAudit = true,
            Validator = validator,
        };

    private static bool IsIntInRange(string? value, int min, int max) =>
        int.TryParse(value, out var parsed) && parsed >= min && parsed <= max;
}

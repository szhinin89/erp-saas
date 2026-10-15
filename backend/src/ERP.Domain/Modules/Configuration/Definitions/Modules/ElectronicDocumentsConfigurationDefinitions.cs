using ERP.Domain.Configuration.Constants;
using ERP.Domain.Configuration.Enums;

namespace ERP.Domain.Configuration.Definitions.Modules;

/// <summary>
/// Definitions para OrgSettingKeys.ElectronicDocuments. Conectadas a efecto real:
/// EmailOnAuthorization (CONFIG-DYNAMIC-OPERATIONS-01, ElectronicDocumentAuthorizedCommunicationHandler)
/// y AutoRetryEnabled (CONFIG-DYNAMIC-OPERATIONS-02, ElectronicDocumentRetryJob — solo apaga el
/// reintento AUTOMÁTICO del job Hangfire; el reintento manual vía IElectronicDocumentIssuer.RetryAsync
/// sigue disponible siempre).
///
/// GenerateRideOnAuthorization: sin consumidor todavía (Fase C).
/// </summary>
public static class ElectronicDocumentsConfigurationDefinitions
{
    public static IEnumerable<ConfigurationDefinition> All()
    {
        yield return Bool(OrgSettingKeys.ElectronicDocuments.AutoRetryEnabled, "true");
        yield return Int(
            OrgSettingKeys.ElectronicDocuments.MaxRetryAttempts,
            ElectronicDocumentRetryMaximum.IsValid,
            ElectronicDocumentRetryMaximum.Default.ToString(System.Globalization.CultureInfo.InvariantCulture)
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

}

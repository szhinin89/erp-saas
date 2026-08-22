using ERP.Domain.Configuration.Constants;
using ERP.Domain.Configuration.Enums;

namespace ERP.Domain.Configuration.Definitions.Modules;

/// <summary>
/// Definitions para OrgSettingKeys.ElectronicDocuments. Conectadas a efecto real:
/// EmailOnAuthorization (CONFIG-DYNAMIC-OPERATIONS-01, SalesInvoiceAuthorizedCommunicationHandler)
/// y AutoRetryEnabled (CONFIG-DYNAMIC-OPERATIONS-02, ElectronicDocumentRetryJob — solo apaga el
/// reintento AUTOMÁTICO del job Hangfire; el reintento manual vía IElectronicDocumentIssuer.RetryAsync
/// sigue disponible siempre).
///
/// MaxRetryAttempts: DELIBERADAMENTE NO conectado en este bloque. ElectronicDocumentRetryPolicy.
/// MaxAttempts (hoy hardcodeado en 5) también decide el dead-letter dentro de
/// ElectronicDocumentIssuer — infraestructura ElectronicDocuments v1.0, FROZEN (ver
/// docs/architecture/frozen-infrastructure.md). Conectarlo requiere tocar esa lógica de
/// dead-letter, fuera de alcance de CONFIG-DYNAMIC-OPERATIONS-02. Nota aparte: el default aquí
/// (3) no coincide con el hardcodeado real (5) — si en el futuro se conecta, el default debe
/// pasar a 5 para no cambiar el comportamiento vigente por defecto.
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

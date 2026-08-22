using ERP.Domain.Configuration.Constants;
using ERP.Domain.Configuration.Enums;
using System.Globalization;

namespace ERP.Domain.Configuration.Definitions.Modules;

/// <summary>
/// EmailEnabled..DefaultLanguage: infraestructura SMTP existente (Communications, pantalla propia).
///
/// SalesInvoiceAuthorizedEnabled/SendCopyToCompanyEmail (grupo Notifications de
/// /settings/operations): auditadas en CONFIG-DYNAMIC-OPERATIONS-03 (no renderizadas en UI —
/// evita settings decorativos).
/// - SalesInvoiceAuthorizedEnabled: DUPLICADA — no conectar. La regla "enviar correo de factura de
///   venta autorizada" ya está resuelta y con efecto real por
///   ElectronicDocumentsPreferences.EmailOnAuthorization ("electronic_documents.email_on_authorization",
///   CONFIG-DYNAMIC-OPERATIONS-01), la única key que SalesInvoiceAuthorizedCommunicationHandler
///   consulta hoy. Esta key ("communications.sales_invoice_authorized_enabled") es una segunda
///   representación de la misma regla, nunca leída por ningún handler — se mantiene guardada
///   (compatibilidad del contrato del DTO/API) pero EmailOnAuthorization es la única autoridad; no
///   se conecta para evitar dos fuentes de verdad para "enviar correo al autorizar".
/// - SendCopyToCompanyEmail: sin consumidor todavía (Fase C) — no auditada en
///   CONFIG-DYNAMIC-OPERATIONS-03 (no estaba en su alcance).
/// </summary>
public static class CommunicationsConfigurationDefinitions
{
    private const int HostMaxLen = 255;
    private const int UsernameMaxLen = 255;
    private const int PasswordMaxLen = 1000;
    private const int EmailMaxLen = 254;
    private const int SenderNameMaxLen = 200;
    private const int LanguageMaxLen = 10;

    public static IEnumerable<ConfigurationDefinition> All()
    {
        yield return Bool(OrgSettingKeys.Communications.EmailEnabled, "false");
        yield return String(OrgSettingKeys.Communications.SmtpHost, HostMaxLen);
        yield return Int(OrgSettingKeys.Communications.SmtpPort, value => IsIntInRange(value, 1, 65535));
        yield return String(OrgSettingKeys.Communications.SmtpUsername, UsernameMaxLen);
        yield return SensitiveString(OrgSettingKeys.Communications.SmtpPassword, PasswordMaxLen);
        yield return String(OrgSettingKeys.Communications.SenderEmail, EmailMaxLen, LooksLikeEmail);
        yield return String(OrgSettingKeys.Communications.SenderName, SenderNameMaxLen);
        yield return Bool(OrgSettingKeys.Communications.UseSsl, "true");
        yield return String(OrgSettingKeys.Communications.ReplyToEmail, EmailMaxLen, LooksLikeEmail);
        yield return Int(OrgSettingKeys.Communications.MaxRetries, value => IsIntInRange(value, 0, 20), "3");
        yield return String(OrgSettingKeys.Communications.DefaultLanguage, LanguageMaxLen, value => value!.Length is >= 2 and <= LanguageMaxLen, "es");
        yield return Bool(OrgSettingKeys.Communications.SalesInvoiceAuthorizedEnabled, "true") with
        {
            RequiresAudit = true,
        };
        yield return Bool(OrgSettingKeys.Communications.SendCopyToCompanyEmail, "false") with
        {
            RequiresAudit = true,
        };
    }

    private static ConfigurationDefinition String(
        string key,
        int maxLength,
        Func<string?, bool>? validator = null,
        string? defaultValue = null
    ) =>
        new()
        {
            Key = key,
            Module = "Communications",
            DataType = ConfigurationDataType.String,
            AllowedScopes = [OrgScope.Company],
            DefaultScope = OrgScope.Company,
            DefaultValue = defaultValue,
            FallbackStrategy = ConfigurationFallbackStrategy.RequireManualSelection,
            RequiresAudit = false,
            Validator = value => value!.Length <= maxLength && (validator?.Invoke(value) ?? true),
        };

    private static ConfigurationDefinition SensitiveString(string key, int maxLength) =>
        String(key, maxLength) with { IsSensitive = true };

    private static ConfigurationDefinition Bool(string key, string defaultValue) =>
        new()
        {
            Key = key,
            Module = "Communications",
            DataType = ConfigurationDataType.Bool,
            AllowedScopes = [OrgScope.Company],
            DefaultScope = OrgScope.Company,
            DefaultValue = defaultValue,
            FallbackStrategy = ConfigurationFallbackStrategy.SystemDefault,
            RequiresAudit = false,
            Validator = value => bool.TryParse(value, out _),
        };

    private static ConfigurationDefinition Int(
        string key,
        Func<string?, bool> validator,
        string? defaultValue = null
    ) =>
        new()
        {
            Key = key,
            Module = "Communications",
            DataType = ConfigurationDataType.Int,
            AllowedScopes = [OrgScope.Company],
            DefaultScope = OrgScope.Company,
            DefaultValue = defaultValue,
            FallbackStrategy = ConfigurationFallbackStrategy.SystemDefault,
            RequiresAudit = false,
            Validator = validator,
        };

    private static bool IsIntInRange(string? value, int min, int max) =>
        int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed)
        && parsed >= min
        && parsed <= max;

    private static bool LooksLikeEmail(string? value) =>
        value is not null && value.Length <= EmailMaxLen && value.Contains('@', StringComparison.Ordinal);
}

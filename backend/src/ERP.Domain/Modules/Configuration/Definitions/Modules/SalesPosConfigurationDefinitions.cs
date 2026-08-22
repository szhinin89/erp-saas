using ERP.Domain.Configuration.Constants;
using ERP.Domain.Configuration.Enums;
using System.Globalization;

namespace ERP.Domain.Configuration.Definitions.Modules;

/// <summary>
/// CONFIG-DYNAMIC-OPERATIONS-01: Definitions para OrgSettingKeys.SalesPos. Solo
/// AllowManualDiscount/MaxDiscountPercent tienen efecto real conectado (Fase B) — el resto se
/// guarda y expone en /settings/operations pero aún no cambia comportamiento (Fase C).
/// </summary>
public static class SalesPosConfigurationDefinitions
{
    public static IEnumerable<ConfigurationDefinition> All()
    {
        yield return Bool(OrgSettingKeys.SalesPos.RequireOpenCashSession, "true");
        yield return Bool(OrgSettingKeys.SalesPos.AllowManualPrice, "false");
        yield return Bool(OrgSettingKeys.SalesPos.AllowManualDiscount, "true");
        yield return Decimal(
            OrgSettingKeys.SalesPos.MaxDiscountPercent,
            value => IsDecimalInRange(value, 0m, 100m),
            "0"
        ) with
        {
            DeveloperNotes =
                "POS-DISCOUNT-RULES-01: 0 (el default) significa SIN tope adicional — el descuento "
                + "manual solo queda acotado por el rango 0-100 del dominio (SalesInvoiceDetail/"
                + "SalesInvoice) y por AllowManualDiscount. Un valor > 0 sí actúa como techo real. "
                + "Decisión deliberada (no 'sin configurar'): el default de fábrica no debe bloquear "
                + "operación en una empresa que aún no definió un tope explícito.",
        };
        yield return Decimal(
            OrgSettingKeys.SalesPos.RequireCustomerAboveAmount,
            value => IsDecimalInRange(value, 0m, decimal.MaxValue),
            defaultValue: null
        ) with
        {
            FallbackStrategy = ConfigurationFallbackStrategy.RequireManualSelection,
        };
        yield return Bool(OrgSettingKeys.SalesPos.AllowSellWithoutStock, "false");
        yield return Bool(OrgSettingKeys.SalesPos.AskBeforeIssue, "false");
        yield return Guid(OrgSettingKeys.SalesPos.DefaultPriceListId);
        yield return Guid(OrgSettingKeys.SalesPos.DefaultCustomerId);
    }

    private static ConfigurationDefinition Bool(string key, string defaultValue) =>
        new()
        {
            Key = key,
            Module = "Sales",
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
            Module = "Sales",
            DataType = ConfigurationDataType.Decimal,
            AllowedScopes = [OrgScope.Company],
            DefaultScope = OrgScope.Company,
            DefaultValue = defaultValue,
            FallbackStrategy = ConfigurationFallbackStrategy.SystemDefault,
            RequiresAudit = true,
            Validator = validator,
        };

    private static ConfigurationDefinition Guid(string key) =>
        new()
        {
            Key = key,
            Module = "Sales",
            DataType = ConfigurationDataType.Guid,
            AllowedScopes = [OrgScope.Company],
            DefaultScope = OrgScope.Company,
            FallbackStrategy = ConfigurationFallbackStrategy.RequireManualSelection,
            RequiresAudit = true,
            Validator = value => System.Guid.TryParse(value, out _),
        };

    private static bool IsDecimalInRange(string? value, decimal min, decimal max) =>
        decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out var parsed)
        && parsed >= min
        && parsed <= max;
}

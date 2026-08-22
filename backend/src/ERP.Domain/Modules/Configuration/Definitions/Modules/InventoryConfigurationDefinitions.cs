using ERP.Domain.Configuration.Constants;
using ERP.Domain.Configuration.Enums;
using System.Globalization;

namespace ERP.Domain.Configuration.Definitions.Modules;

/// <summary>
/// Definitions para OrgSettingKeys.Inventory. Ninguna de estas keys tiene efecto real conectado
/// todavía — se guardan y exponen en /settings/operations, marcadas como "aún no conectado" en
/// la UI.
///
/// RequireReasonForAdjustment: evaluado en CONFIG-DYNAMIC-OPERATIONS-02 y DELIBERADAMENTE NO
/// conectado. CreateStockAdjustmentCommand no tiene ningún validator hoy — Reason nunca se exige
/// como no-vacío en backend (y no se encontró un formulario de frontend que lo exija tampoco).
/// El default de esta key es "true" (pedido explícitamente por el bloque que la creó), pero
/// conectar el enforcement con ese default cambiaría el comportamiento actual por defecto (una
/// empresa que hoy puede guardar un ajuste con motivo vacío dejaría de poder hacerlo sin haber
/// tocado esta preferencia) — viola la regla "no romper comportamiento actual por default" de
/// CONFIG-DYNAMIC-OPERATIONS-02. Requiere decidir explícitamente (fuera de este bloque) si el
/// default correcto es "false" para preservar el comportamiento vigente, o si se acepta el
/// cambio de default como mejora de calidad de datos.
/// </summary>
public static class InventoryConfigurationDefinitions
{
    public static IEnumerable<ConfigurationDefinition> All()
    {
        yield return Bool(OrgSettingKeys.Inventory.AllowNegativeStock, "false");
        yield return Bool(OrgSettingKeys.Inventory.RequireReasonForAdjustment, "true");
        yield return Bool(OrgSettingKeys.Inventory.RequireApprovalForLargeAdjustment, "false");
        yield return Decimal(
            OrgSettingKeys.Inventory.LargeAdjustmentThresholdAmount,
            value => IsDecimalInRange(value, 0m, decimal.MaxValue),
            "0"
        );
    }

    private static ConfigurationDefinition Bool(string key, string defaultValue) =>
        new()
        {
            Key = key,
            Module = "Inventory",
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
            Module = "Inventory",
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

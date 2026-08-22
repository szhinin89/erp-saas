using ERP.Domain.Configuration.Constants;
using ERP.Domain.Configuration.Enums;

namespace ERP.Domain.Configuration.Definitions.Modules;

/// <summary>
/// Definitions para OrgSettingKeys.Purchases. AllowConfirmWithoutReceptionXml está conectada
/// (CONFIG-DYNAMIC-OPERATIONS-02, ConfirmPurchaseHandler — exige al menos una línea con
/// PurchaseReceptionLineId cuando está en false). Ninguno de los siguientes 3 campos se renderiza
/// en /settings/operations (evita settings decorativos), con decisión final de
/// CONFIG-DYNAMIC-OPERATIONS-03 (auditoría, no implementación):
/// - UpdateCostOnConfirm: CERRADA / no configurable. No existe un "campo de costo" separado que
///   actualizar opcionalmente — ConfirmPurchaseHandler llama IStockRepository.AppendMovementAsync
///   incondicionalmente por cada línea con ItemId, y ESE mismo call es quien calcula/persiste el
///   costo promedio corrido del Kardex (RunningAverageCost/RunningStockValue en
///   StockRepository.AppendMovementAsync). Postear el movimiento de inventario y actualizar el
///   costo no son dos pasos separables hoy: "false" tendría que significar u omitir el movimiento
///   de stock (rompe la contabilidad de inventario) o postear sin actualizar costo (no existe ese
///   parámetro/ruta). Tocar esto es rediseñar el posteo del Kardex — infraestructura de costeo
///   central, no un gate de preferencia. No se reabre sin ADR.
/// - AllowManualCostChange: CERRADA / no configurable, por decorativa. El costo de una línea de
///   compra (PurchaseInvoiceDetail.RecalcCosts, desde TaxableBase+FreightAllocated+
///   OtherCostsAllocated) siempre es editable mientras la línea no esté confirmada — no hay un
///   permiso especial que active/desactive esa edición, es simplemente cómo funciona el campo. Tras
///   Confirm, FreezeCosts()/EnsureNotFrozen() bloquea cualquier edición de forma incondicional e
///   irreversible (invariante de dominio "Freeze on Confirm", no relacionada con esta preferencia).
///   No existe una ruta distinta (p.ej. "sobrescribir costo proveniente de XML") que esta
///   preferencia pudiera gatear sin inventar lógica de negocio nueva.
/// - DefaultWarehouseId, RequireReasonForCostChange: sin consumidor todavía (Fase C) — no
///   auditadas en CONFIG-DYNAMIC-OPERATIONS-03 (no estaban en su alcance).
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

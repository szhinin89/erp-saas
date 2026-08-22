using ERP.Domain.Configuration.Constants;
using ERP.Domain.Configuration.Enums;
using System.Globalization;

namespace ERP.Domain.Configuration.Definitions.Modules;

/// <summary>
/// Definitions para OrgSettingKeys.SalesPos. Conectadas a efecto real: AllowManualDiscount/
/// MaxDiscountPercent (CONFIG-DYNAMIC-OPERATIONS-01, ApplySalesDiscountHandler +
/// SalesLineBuilder — ver también POS-DISCOUNT-RULES-01) y AllowSellWithoutStock
/// (CONFIG-DYNAMIC-OPERATIONS-02, AuthorizeSalesInvoiceHandler). El resto se guarda y expone (solo
/// vía API — NINGUNO de estos 5 campos se renderiza en /settings/operations, evita settings
/// decorativos visibles) pero no cambia comportamiento, con decisión final de
/// CONFIG-DYNAMIC-OPERATIONS-03 (auditoría, no implementación):
/// - RequireOpenCashSession: CERRADA / no configurable — no es un rediseño pendiente, es una
///   invariante estructural. CreateSalesDraftHandler exige sesión abierta porque de ahí resuelve
///   CashSessionId/EmissionPointId sin fallback, y SalesInvoice.CreateDraft(...) declara
///   cashSessionId como Guid no-nullable (no Guid?) — no existe una ruta de creación de factura sin
///   caja. "false" es hoy arquitectónicamente imposible sin cambiar el dominio de Ventas y el
///   derivador de EmissionPointId (FROZEN — secuencias documentales). No se reabre sin ADR.
/// - RequireCustomerAboveAmount: DUPLICADA — no conectar. La regla "exigir cliente identificado
///   sobre cierto monto" ya existe y es la única autoridad: ISalesFiscalPolicyResolver /
///   OrgSettingKeys.Sales.ConsumerFinalMaxAmount ("sales.consumer_final.max_amount"), enforced en
///   AuthorizeSalesInvoiceHandler contra factura de Consumidor Final. Conectar esta key crearía una
///   segunda fuente de verdad para la misma regla de negocio.
/// - DefaultCustomerId: DUPLICADA — no conectar. Ya existen dos niveles de resolución dinámica:
///   CashRegister.DefaultCustomerId (por caja, expuesto en OpenCashSessionUseCases/CajaMapper) y el
///   fallback universal a "Consumidor Final" (resolveConsumidorFinal() en useSalesPage.ts, vía API,
///   sin hardcode). Un tercer nivel a nivel Company competiría con el más específico (por caja), que
///   es el que realmente se ejercita end-to-end.
/// - DefaultPriceListId: DUPLICADA — no conectar. El motor de Pricing ya tiene su propio mecanismo
///   de lista por defecto: PriceList.IsDefault, consumido directamente por IPricingResolver cuando
///   no se pasa priceListId explícito (SSOT documentado en IPricingResolver.cs). Esta key sería una
///   segunda noción de "lista por defecto" compitiendo con la del dominio Pricing.
/// - AllowManualPrice, AskBeforeIssue: sin consumidor todavía (Fase C) — no auditadas en
///   CONFIG-DYNAMIC-OPERATIONS-03 (no estaban en su alcance), quedan pendientes para un bloque futuro.
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

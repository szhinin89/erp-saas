using ERP.Domain.Configuration.Constants;
using ERP.Domain.Configuration.Enums;
using System.Globalization;

namespace ERP.Domain.Configuration.Definitions.Modules;

/// <summary>
/// Definitions para OrgSettingKeys.Cash. Conectadas a efecto real en CloseCashSessionHandler:
/// RequireReasonForDifference (CONFIG-DYNAMIC-OPERATIONS-01), AllowCloseWithDifference y
/// MaxAllowedDifference (CONFIG-DYNAMIC-OPERATIONS-02) — las tres se evalúan juntas, en ese
/// orden, cuando session.Difference != 0. Ninguno de los otros 3 campos se renderiza en
/// /settings/operations (evita settings decorativos):
/// - RequireOpeningAmount: DIFERIDA (auditada en CONFIG-DYNAMIC-OPERATIONS-03, no implementada).
///   Hoy no existe la distinción que esta preferencia necesitaría gatear: CashSession.Open(...)
///   solo valida OpeningAmount >= 0 (OpeningAmount = 0 ya es un valor válido sin restricción) —
///   no hay forma de distinguir "el usuario dejó el monto en blanco/0 deliberadamente" de "el
///   campo nunca se llenó", porque un decimal en el body siempre llega con un valor. Conectar esto
///   requeriría agregar esa señal a OpenCashSessionCommand — fuera de alcance de un simple gate.
/// - AllowManualInOutMovements/RequireReasonForMovements: sin consumidor todavía (Fase C) — no
///   auditadas en CONFIG-DYNAMIC-OPERATIONS-03 (no estaban en su alcance).
/// </summary>
public static class CashConfigurationDefinitions
{
    public static IEnumerable<ConfigurationDefinition> All()
    {
        yield return Bool(OrgSettingKeys.Cash.RequireOpeningAmount, "true");
        yield return Bool(OrgSettingKeys.Cash.AllowCloseWithDifference, "true");
        yield return Decimal(
            OrgSettingKeys.Cash.MaxAllowedDifference,
            value => IsDecimalInRange(value, 0m, decimal.MaxValue),
            "0"
        );
        yield return Bool(OrgSettingKeys.Cash.RequireReasonForDifference, "true");
        yield return Bool(OrgSettingKeys.Cash.AllowManualInOutMovements, "true");
        yield return Bool(OrgSettingKeys.Cash.RequireReasonForMovements, "true");
    }

    private static ConfigurationDefinition Bool(string key, string defaultValue) =>
        new()
        {
            Key = key,
            Module = "Cash",
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
            Module = "Cash",
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

using ERP.Domain.Configuration.Enums;

namespace ERP.Domain.Configuration.Definitions;

/// <summary>
/// CONFIG-FOUNDATION-P1-03: tipo semántico declarado en <see cref="ConfigurationDefinition"/> —
/// distinto del <see cref="SettingDataType"/> que <c>OrgSetting</c> persiste, porque una
/// definición puede necesitar más precisión de validación que el tipo de almacenamiento (ej.
/// <see cref="ColorHex"/> se valida como color, pero se persiste como <see cref="SettingDataType.String"/>).
/// <see cref="ToPersistedType"/> es la única correspondencia oficial entre ambos — el guardrail de
/// escritura la usa para exigir que <c>OrgSetting.DataType</c> coincida con la definición.
///
/// <see cref="Json"/> existe por completitud del catálogo de tipos pero queda PROHIBIDO para
/// definitions nuevas salvo excepción explícita y documentada (Principio 12 de la arquitectura
/// objetivo: "JSON crudo no debe usarse para reglas críticas") — ninguna definition registrada en
/// esta entrega lo usa.
/// </summary>
public enum ConfigurationDataType
{
    String,
    Int,
    Decimal,
    Bool,
    Guid,
    ColorHex,
    Json,
}

public static class ConfigurationDataTypeExtensions
{
    public static SettingDataType ToPersistedType(this ConfigurationDataType type) =>
        type switch
        {
            ConfigurationDataType.String => SettingDataType.String,
            ConfigurationDataType.ColorHex => SettingDataType.String,
            ConfigurationDataType.Int => SettingDataType.Int,
            ConfigurationDataType.Decimal => SettingDataType.Decimal,
            ConfigurationDataType.Bool => SettingDataType.Bool,
            ConfigurationDataType.Guid => SettingDataType.Guid,
            ConfigurationDataType.Json => SettingDataType.Json,
            _ => throw new ArgumentOutOfRangeException(
                nameof(type),
                type,
                "ConfigurationDataType sin mapeo a SettingDataType — registrar en ToPersistedType."
            ),
        };
}

namespace ERP.Domain.Configuration.Enums;

/// <summary>
/// CONFIG-FOUNDATION-P2-01: cómo interpretar OldValue/NewValue en un ConfigurationChangeLog.
/// Distinto de ConfigurationDataType (que describe el tipo real del setting) porque un valor
/// sensible se registra como Masked/Fingerprint sin importar su DataType real — nunca en claro.
/// </summary>
public enum ConfigurationChangeValueType
{
    String,
    Int,
    Decimal,
    Bool,
    Guid,
    ColorHex,
    Json,

    /// <summary>Valor sensible ocultado (ej. contraseña) — OldValue/NewValue nunca contienen el secreto.</summary>
    Masked,

    /// <summary>Huella (hash) del contenido real — permite detectar "cambió" sin exponer el contenido (ej. certificado).</summary>
    Fingerprint,
}

namespace ERP.Domain.Configuration.Enums;

/// <summary>
/// Tipo de dato del valor almacenado en org_settings.
/// El valor siempre se persiste como TEXT; este campo indica cómo deserializarlo.
/// </summary>
public enum SettingDataType
{
    String = 1,
    Guid = 2,
    Int = 3,
    Decimal = 4,
    Bool = 5,
    Json = 6,
}

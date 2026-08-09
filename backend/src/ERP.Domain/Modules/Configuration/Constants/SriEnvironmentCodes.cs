namespace ERP.Domain.Configuration.Constants;

/// <summary>
/// Códigos oficiales SRI (Ficha Técnica, Tabla 4 "Ambiente"): binario técnico estable,
/// no configurable por tenant/empresa — fuente única, no requiere catálogo persistido
/// (CLEAN-01G retiró el catálogo <c>sri_environment</c> huérfano que duplicaba estos valores).
/// </summary>
public static class SriEnvironmentCodes
{
    /// <summary>Representación string (XML/RIDE): <c>RideHeader.Environment</c>, <c>ElectronicDocument.Environment</c>.</summary>
    public const string Testing = "1";
    public const string Production = "2";

    /// <summary>Representación int (config por empresa): <c>SriSettings.Environment</c>.</summary>
    public const int TestingValue = 1;
    public const int ProductionValue = 2;

    public static bool IsProduction(string? code) => code == Production;

    public static bool IsProduction(int code) => code == ProductionValue;
}

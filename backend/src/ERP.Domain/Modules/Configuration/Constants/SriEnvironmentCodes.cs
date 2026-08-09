namespace ERP.Domain.Configuration.Constants;

/// <summary>
/// Códigos oficiales SRI (Ficha Técnica, Tabla 4 "Ambiente"): binario técnico estable,
/// no configurable por tenant/empresa — no requiere catálogo persistido. Mismos valores que
/// <c>ERP.Domain.Modules.SriCatalogs.Entities.SriEnvironment</c> (catálogo sembrado hoy sin
/// ningún consumidor de lectura); esta clase cubre los puntos que hoy comparan el código crudo
/// ("1"/"2" como viene del XML/config) sin pasar por ese catálogo.
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

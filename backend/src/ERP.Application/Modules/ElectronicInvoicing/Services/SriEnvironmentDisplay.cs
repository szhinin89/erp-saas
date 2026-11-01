using ERP.Domain.Configuration.Constants;

namespace ERP.Application.Modules.ElectronicInvoicing.Services;

/// <summary>
/// Códigos oficiales SRI (Ficha Técnica, Tabla 4 "Ambiente") → contrato de UI ("Test"/"Production")
/// y nombre visible. Única traducción para los DTOs de estado de facturación electrónica.
/// </summary>
public static class SriEnvironmentDisplay
{
    public static (string? Environment, string? EnvironmentName) Resolve(int environment) =>
        environment switch
        {
            SriEnvironmentCodes.TestingValue => ("Test", "Pruebas"),
            SriEnvironmentCodes.ProductionValue => ("Production", "Producción"),
            _ => (null, null),
        };
}

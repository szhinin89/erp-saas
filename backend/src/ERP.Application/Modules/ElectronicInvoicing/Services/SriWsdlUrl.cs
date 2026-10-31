using ERP.Domain.Configuration.Constants;

namespace ERP.Application.Modules.ElectronicInvoicing.Services;

/// <summary>
/// Única regla local de <c>SriSettings.WsdlUrl</c>: formato (absoluta, http/https) y coherencia con el
/// ambiente. La disponibilidad real del webservice la decide <c>ISriConnectivityChecker</c> bajo demanda.
/// </summary>
public static class SriWsdlUrl
{
    // Ficha Técnica Offline §7.1 y §8.1: hosts oficiales de cada ambiente. No se usan para transmitir
    // (el endpoint sigue saliendo del WsdlUrl configurado, §7.1.4); solo para rechazar la combinación
    // contradictoria. Un host desconocido (simulador de pruebas, cambio futuro del SRI) no se juzga.
    private static readonly IReadOnlyDictionary<string, int> OfficialHostEnvironments =
        new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
        {
            ["celcer.sri.gob.ec"] = SriEnvironmentCodes.TestingValue,
            ["cel.sri.gob.ec"] = SriEnvironmentCodes.ProductionValue,
        };

    public static bool IsValid(string? wsdlUrl) =>
        Uri.TryCreate(wsdlUrl, UriKind.Absolute, out var uri)
        && (uri.Scheme == Uri.UriSchemeHttps || uri.Scheme == Uri.UriSchemeHttp);

    /// <summary>
    /// <c>true</c> si la URL apunta al host oficial del otro ambiente (p. ej. ambiente 1 Pruebas con
    /// <c>cel.sri.gob.ec</c>): transmitir así enviaría a Producción un comprobante de Pruebas, o al revés.
    /// </summary>
    public static bool ContradictsEnvironment(string? wsdlUrl, int environment) =>
        Uri.TryCreate(wsdlUrl, UriKind.Absolute, out var uri)
        && OfficialHostEnvironments.TryGetValue(uri.Host, out var hostEnvironment)
        && hostEnvironment != environment;

    /// <summary>Formato válido y sin contradicción con el ambiente configurado.</summary>
    public static bool IsValidFor(string? wsdlUrl, int environment) =>
        IsValid(wsdlUrl) && !ContradictsEnvironment(wsdlUrl, environment);
}

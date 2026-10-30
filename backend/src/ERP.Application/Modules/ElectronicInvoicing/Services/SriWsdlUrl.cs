namespace ERP.Application.Modules.ElectronicInvoicing.Services;

/// <summary>
/// Única regla local de formato de <c>SriSettings.WsdlUrl</c> (absoluta, http/https). Solo formato:
/// la disponibilidad real del webservice la decide <c>ISriConnectivityChecker</c> bajo demanda.
/// </summary>
public static class SriWsdlUrl
{
    public static bool IsValid(string? wsdlUrl) =>
        Uri.TryCreate(wsdlUrl, UriKind.Absolute, out var uri)
        && (uri.Scheme == Uri.UriSchemeHttps || uri.Scheme == Uri.UriSchemeHttp);
}

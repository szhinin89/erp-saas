using System.Net;
using Microsoft.AspNetCore.HttpOverrides;

namespace ERP.API.Hosting;

/// <summary>
/// SEC-A01 — <c>X-Forwarded-For</c> y <c>X-Forwarded-Proto</c> solo desde proxies declarados en la
/// sección <c>ForwardedHeaders</c> (<c>KnownProxies</c>: IPs, <c>KnownNetworks</c>: CIDR). Sin ninguno
/// declarado no se procesan cabeceras forwarded (<c>RemoteIpAddress</c> = conexión TCP, esquema =
/// el de la conexión). Se descartan los valores por defecto de ASP.NET (loopback) y solo se acepta
/// el último salto (<c>ForwardLimit = 1</c>). Con TLS terminado en el proxy, <c>X-Forwarded-Proto</c>
/// hace que <c>Request.IsHttps</c> sea verdadero (cookie de refresh <c>Secure</c>, sin bucle de
/// <c>UseHttpsRedirection</c>). Debe ejecutarse antes de HTTPS, autenticación y rate limiting.
/// </summary>
public static class TrustedProxyForwardedHeaders
{
    public const string SectionName = "ForwardedHeaders";

    /// <summary>Opciones del middleware, o <c>null</c> si no hay proxies confiables configurados.</summary>
    public static ForwardedHeadersOptions? Create(IConfiguration configuration)
    {
        var section = configuration.GetSection(SectionName);
        var proxies = NonBlank(section.GetSection("KnownProxies"));
        var networks = NonBlank(section.GetSection("KnownNetworks"));
        if (proxies.Length == 0 && networks.Length == 0)
            return null;

        var options = new ForwardedHeadersOptions
        {
            ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto,
            ForwardLimit = 1,
        };
        options.KnownProxies.Clear();
        options.KnownIPNetworks.Clear();
        foreach (var proxy in proxies)
            options.KnownProxies.Add(IPAddress.Parse(proxy));
        foreach (var network in networks)
            options.KnownIPNetworks.Add(System.Net.IPNetwork.Parse(network));
        return options;
    }

    /// <summary>
    /// Ignora entradas vacías: Compose publica <c>ForwardedHeaders__KnownProxies__0=""</c> cuando no
    /// se declara proxy, y eso equivale a "sin proxies" (nunca a confiar en cualquier origen).
    /// </summary>
    private static string[] NonBlank(IConfigurationSection section) =>
        [.. (section.Get<string[]>() ?? []).Where(v => !string.IsNullOrWhiteSpace(v)).Select(v => v.Trim())];
}

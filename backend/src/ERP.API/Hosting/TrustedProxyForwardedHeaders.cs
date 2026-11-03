using System.Net;
using Microsoft.AspNetCore.HttpOverrides;

namespace ERP.API.Hosting;

/// <summary>
/// SEC-A01 — <c>X-Forwarded-For</c> solo desde proxies declarados en la sección
/// <c>ForwardedHeaders</c> (<c>KnownProxies</c>: IPs, <c>KnownNetworks</c>: CIDR). Sin ninguno
/// declarado no se procesan cabeceras forwarded (<c>RemoteIpAddress</c> = conexión TCP). Se descartan
/// los valores por defecto de ASP.NET (loopback) y solo se acepta el último salto
/// (<c>ForwardLimit = 1</c>). <c>X-Forwarded-Proto</c>/HTTPS queda fuera: requisito de despliegue.
/// </summary>
public static class TrustedProxyForwardedHeaders
{
    public const string SectionName = "ForwardedHeaders";

    /// <summary>Opciones del middleware, o <c>null</c> si no hay proxies confiables configurados.</summary>
    public static ForwardedHeadersOptions? Create(IConfiguration configuration)
    {
        var section = configuration.GetSection(SectionName);
        var proxies = section.GetSection("KnownProxies").Get<string[]>() ?? [];
        var networks = section.GetSection("KnownNetworks").Get<string[]>() ?? [];
        if (proxies.Length == 0 && networks.Length == 0)
            return null;

        var options = new ForwardedHeadersOptions
        {
            ForwardedHeaders = ForwardedHeaders.XForwardedFor,
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
}

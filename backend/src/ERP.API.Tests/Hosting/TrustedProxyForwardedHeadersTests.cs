using System.Net;
using ERP.API.Hosting;
using FluentAssertions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using MsOptions = Microsoft.Extensions.Options.Options;

namespace ERP.API.Tests.Hosting;

/// <summary>
/// SEC-A01 — <c>X-Forwarded-For</c> solo se aplica cuando el emisor inmediato es un proxy declarado;
/// sin configuración no se procesa ninguna cabecera forwarded (la IP de los límites de login es la
/// de la conexión TCP).
/// </summary>
public sealed class TrustedProxyForwardedHeadersTests
{
    private static IConfiguration Config(params (string Key, string Value)[] values) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(values.ToDictionary(v => v.Key, v => (string?)v.Value))
            .Build();

    private static async Task<string?> RemoteIpAfterMiddleware(
        ForwardedHeadersOptions options,
        string connectionIp,
        string forwardedFor
    )
    {
        var context = new DefaultHttpContext();
        context.Connection.RemoteIpAddress = IPAddress.Parse(connectionIp);
        context.Request.Headers["X-Forwarded-For"] = forwardedFor;
        var middleware = new ForwardedHeadersMiddleware(
            _ => Task.CompletedTask,
            NullLoggerFactory.Instance,
            MsOptions.Create(options)
        );

        await middleware.Invoke(context);

        return context.Connection.RemoteIpAddress?.ToString();
    }

    [Fact]
    public void Sin_proxies_configurados_no_se_habilita_el_middleware()
    {
        TrustedProxyForwardedHeaders.Create(Config()).Should().BeNull();
    }

    [Fact]
    public async Task Cabecera_enviada_directamente_por_un_cliente_no_confiable_se_ignora()
    {
        var options = TrustedProxyForwardedHeaders.Create(
            Config(("ForwardedHeaders:KnownProxies:0", "10.0.0.5"))
        )!;

        var ip = await RemoteIpAfterMiddleware(options, "203.0.113.9", "1.2.3.4");

        ip.Should().Be("203.0.113.9");
    }

    [Fact]
    public async Task Loopback_no_es_confiable_salvo_que_se_declare()
    {
        var options = TrustedProxyForwardedHeaders.Create(
            Config(("ForwardedHeaders:KnownProxies:0", "10.0.0.5"))
        )!;

        var ip = await RemoteIpAfterMiddleware(options, "127.0.0.1", "1.2.3.4");

        ip.Should().Be("127.0.0.1");
    }

    [Fact]
    public async Task Proxy_declarado_aplica_solo_el_ultimo_salto()
    {
        var options = TrustedProxyForwardedHeaders.Create(
            Config(("ForwardedHeaders:KnownProxies:0", "10.0.0.5"))
        )!;

        // El cliente puede anteponer valores falsos; solo cuenta el que añadió el proxy confiable.
        var ip = await RemoteIpAfterMiddleware(options, "10.0.0.5", "6.6.6.6, 198.51.100.20");

        ip.Should().Be("198.51.100.20");
    }

    [Fact]
    public async Task Red_declarada_en_CIDR_es_confiable()
    {
        var options = TrustedProxyForwardedHeaders.Create(
            Config(("ForwardedHeaders:KnownNetworks:0", "172.18.0.0/16"))
        )!;

        var ip = await RemoteIpAfterMiddleware(options, "172.18.0.3", "198.51.100.20");

        ip.Should().Be("198.51.100.20");
    }
}

using System.Net;
using ERP.API.Auth;
using ERP.API.Hosting;
using FluentAssertions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
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
    public void Entradas_vacias_de_Compose_equivalen_a_sin_proxies_y_no_a_confiar_en_todo()
    {
        TrustedProxyForwardedHeaders
            .Create(
                Config(
                    ("ForwardedHeaders:KnownProxies:0", ""),
                    ("ForwardedHeaders:KnownNetworks:0", "  ")
                )
            )
            .Should()
            .BeNull();
    }

    [Fact]
    public void LoginThrottle_con_valores_vacios_de_Compose_conserva_los_defaults_del_codigo()
    {
        var options = new ERP.Application.Common.Config.LoginThrottleOptions();
        Config(
                ("LoginThrottle:MaxConcurrentVerifications", ""),
                ("LoginThrottle:QueueLimit", ""),
                ("LoginThrottle:AccountIpLimit", "5"),
                ("LoginThrottle:IpLimit", "0"),
                ("LoginThrottle:AccountLimit", "30"),
                ("LoginThrottle:WindowMinutes", "15"),
                ("LoginThrottle:MaxQueueWaitMilliseconds", "2000")
            )
            .GetSection("LoginThrottle")
            .Bind(options);

        options.MaxConcurrentVerifications.Should().BeNull();
        options.QueueLimit.Should().BeNull();
        options.AccountIpLimit.Should().Be(5);
        options.IpLimit.Should().Be(0, because: "K2 sigue desactivado");
        options.AccountLimit.Should().Be(30);
    }

    [Fact]
    public void Entradas_vacias_se_ignoran_junto_a_un_proxy_declarado()
    {
        var options = TrustedProxyForwardedHeaders.Create(
            Config(
                ("ForwardedHeaders:KnownProxies:0", "10.0.0.5"),
                ("ForwardedHeaders:KnownNetworks:0", "")
            )
        )!;

        options.KnownProxies.Should().ContainSingle();
        options.KnownIPNetworks.Should().BeEmpty();
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

    // ── X-Forwarded-Proto (TLS terminado en el proxy) ────────────────────────

    private const string Proxy = "10.0.0.5";

    /// <summary>
    /// Pipeline mínimo en el mismo orden que Program.cs (forwarded headers → UseHttpsRedirection →
    /// endpoint) con puerto HTTPS 443 conocido, de modo que una petición HTTP real sí se
    /// redirige. El endpoint emite la cookie de refresh real y refleja lo que la aplicación ve.
    /// </summary>
    private static async Task<HttpResponseMessage> Send(
        IConfiguration config,
        string connectionIp,
        Action<HttpRequestMessage> shape
    )
    {
        using var host = await new HostBuilder()
            .ConfigureWebHost(web =>
            {
                web.UseTestServer();
                web.ConfigureServices(services => services.AddHttpsRedirection(o => o.HttpsPort = 443));
                web.Configure(app =>
                {
                    app.Use(
                        (ctx, next) =>
                        {
                            ctx.Connection.RemoteIpAddress = IPAddress.Parse(connectionIp);
                            return next();
                        }
                    );
                    var options = TrustedProxyForwardedHeaders.Create(config);
                    if (options is not null)
                        app.UseForwardedHeaders(options);
                    app.UseHttpsRedirection();
                    app.Run(ctx =>
                    {
                        ctx.Response.Cookies.Append(
                            AuthRefreshCookie.Name,
                            "token",
                            AuthRefreshCookie.BuildOptions(ctx.Request, DateTime.UtcNow.AddDays(1))
                        );
                        ctx.Response.Headers["X-Seen"] =
                            $"{ctx.Request.Scheme}|{ctx.Request.IsHttps}|{ctx.Connection.RemoteIpAddress}";
                        return Task.CompletedTask;
                    });
                });
            })
            .StartAsync();
        var client = host.GetTestServer().CreateClient();
        var request = new HttpRequestMessage(HttpMethod.Get, "http://erp.test/api/x");
        shape(request);
        return await client.SendAsync(request);
    }

    private static IConfiguration TrustedProxyConfig() =>
        Config(("ForwardedHeaders:KnownProxies:0", Proxy));

    private static string Seen(HttpResponseMessage r) => r.Headers.GetValues("X-Seen").Single();

    [Fact]
    public async Task Proto_https_desde_proxy_confiable_establece_IsHttps_y_aplica_la_IP()
    {
        var r = await Send(
            TrustedProxyConfig(),
            Proxy,
            m =>
            {
                m.Headers.Add("X-Forwarded-Proto", "https");
                m.Headers.Add("X-Forwarded-For", "198.51.100.20");
            }
        );

        ((int)r.StatusCode).Should().Be(200);
        Seen(r).Should().Be("https|True|198.51.100.20");
    }

    [Fact]
    public async Task Cookie_de_refresh_conserva_Secure_con_TLS_terminado_en_el_proxy()
    {
        var r = await Send(
            TrustedProxyConfig(),
            Proxy,
            m => m.Headers.Add("X-Forwarded-Proto", "https")
        );

        var cookie = r.Headers.GetValues("Set-Cookie").Single().ToLowerInvariant();
        cookie.Should().Contain("secure").And.Contain("httponly").And.Contain("samesite=strict");
    }

    [Fact]
    public async Task Detras_del_proxy_con_proto_https_no_hay_bucle_de_redireccion()
    {
        var r = await Send(
            TrustedProxyConfig(),
            Proxy,
            m => m.Headers.Add("X-Forwarded-Proto", "https")
        );

        ((int)r.StatusCode).Should().Be(200, because: "un 307 repetido sería el bucle HTTPS");
        r.Headers.Location.Should().BeNull();
    }

    [Fact]
    public async Task Peticion_http_real_sin_proto_si_se_redirige_a_https()
    {
        // Control del propio test: la redirección HTTPS está activa y solo la anula un proxy confiable.
        var r = await Send(TrustedProxyConfig(), Proxy, _ => { });

        ((int)r.StatusCode).Should().Be(307);
        r.Headers.Location!.Scheme.Should().Be("https");
    }

    [Fact]
    public async Task Origen_no_confiable_no_puede_falsificar_esquema_https()
    {
        var r = await Send(
            TrustedProxyConfig(),
            "203.0.113.9",
            m => m.Headers.Add("X-Forwarded-Proto", "https")
        );

        ((int)r.StatusCode).Should().Be(307, because: "la cabecera se ignora y sigue siendo HTTP");
        r.Headers.Contains("Set-Cookie").Should().BeFalse();
    }

    [Fact]
    public async Task Origen_no_confiable_no_puede_falsificar_la_IP_del_cliente()
    {
        // Mismo origen no confiable con HTTPS real en la conexión (sin proto reenviado): la IP
        // forwarded se ignora.
        var r = await Send(
            TrustedProxyConfig(),
            "203.0.113.9",
            m =>
            {
                m.RequestUri = new Uri("https://erp.test/api/x");
                m.Headers.Add("X-Forwarded-For", "1.2.3.4");
            }
        );

        Seen(r).Should().Be("https|True|203.0.113.9");
    }

    [Fact]
    public async Task Proto_solo_cuenta_el_ultimo_salto()
    {
        // El cliente antepone "https"; el proxy confiable añadió "http". ForwardLimit = 1 → http.
        var r = await Send(
            TrustedProxyConfig(),
            Proxy,
            m => m.Headers.Add("X-Forwarded-Proto", "https, http")
        );

        ((int)r.StatusCode).Should().Be(307);
        r.Headers.Contains("Set-Cookie").Should().BeFalse();
    }

    [Fact]
    public void Opciones_procesan_For_y_Proto_con_un_salto_y_sin_confianza_implicita()
    {
        var options = TrustedProxyForwardedHeaders.Create(TrustedProxyConfig())!;

        options
            .ForwardedHeaders.Should()
            .Be(ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto);
        options.ForwardLimit.Should().Be(1);
        options.KnownProxies.Should().ContainSingle().Which.ToString().Should().Be(Proxy);
        options.KnownIPNetworks.Should().BeEmpty();
    }
}

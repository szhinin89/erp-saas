using ERP.API.Hosting;
using FluentAssertions;
using Microsoft.Extensions.Configuration;

namespace ERP.API.Tests.Hosting;

/// <summary>
/// SEC-06 — regla de despliegue: con varias instancias de API Redis es obligatorio
/// (<c>Redis:Required=true</c>); la caché en memoria solo se admite en instancia única.
/// </summary>
public sealed class RedisRequiredDeploymentTests
{
    private static IConfiguration Config(params (string Key, string? Value)[] values) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(values.Select(v => new KeyValuePair<string, string?>(v.Key, v.Value)))
            .Build();

    [Fact]
    public void Multiinstancia_sin_Redis_no_arranca()
    {
        var configuration = Config(("Redis:Required", "true"));

        var act = () =>
            ErpHostRegistration.EnsureRedisWhenRequired(
                configuration,
                ErpHostRegistration.RedisConnection(configuration)
            );

        act.Should().Throw<InvalidOperationException>().WithMessage("*Redis:Required=true*");
    }

    [Fact]
    public void Multiinstancia_con_Redis_arranca()
    {
        var configuration = Config(
            ("Redis:Required", "true"),
            ("Redis:ConnectionString", "localhost:6390")
        );

        var act = () =>
            ErpHostRegistration.EnsureRedisWhenRequired(
                configuration,
                ErpHostRegistration.RedisConnection(configuration)
            );

        act.Should().NotThrow();
    }

    // Opción A: con Redis configurado (API y worker comparten AddErpHostCore) el host verifica el
    // contrato del Redis de autorización al arrancar; sin Redis no hay nada que verificar.
    [Theory]
    [InlineData("localhost:6390", 1)]
    [InlineData(null, 0)]
    public void Con_Redis_el_host_registra_la_verificacion_estricta_al_arrancar(
        string? redis,
        int expected
    )
    {
        var configuration = Config(("Redis:ConnectionString", redis));
        var services = new Microsoft.Extensions.DependencyInjection.ServiceCollection();
        services.AddErpHostCore(
            configuration,
            new Microsoft.Extensions.Hosting.Internal.HostingEnvironment
            {
                EnvironmentName = "Testing",
                ContentRootPath = AppContext.BaseDirectory,
            }
        );

        services
            .Count(d => d.ImplementationType == typeof(PermissionsCacheRedisStartupValidator))
            .Should()
            .Be(expected);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("false")]
    public void Instancia_unica_admite_cache_en_memoria(string? required)
    {
        var configuration = Config(("Redis:Required", required));

        var act = () => ErpHostRegistration.EnsureRedisWhenRequired(configuration, null);

        act.Should().NotThrow();
    }
}

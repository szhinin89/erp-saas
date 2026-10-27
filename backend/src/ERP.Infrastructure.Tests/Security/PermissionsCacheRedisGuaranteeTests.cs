using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using ERP.Application.Access.Caching;
using ERP.Infrastructure.Access.Caching;
using FluentAssertions;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using StackExchange.Redis;
using MsOptions = Microsoft.Extensions.Options.Options;

namespace ERP.Infrastructure.Tests.Security;

/// <summary>
/// SEC-06 (opción A) — verificación estricta del Redis de la caché de permisos contra Redis real,
/// efímero y aislado (un contenedor por escenario). Requiere Docker.
/// </summary>
[Trait("Category", "Redis")]
public sealed class PermissionsCacheRedisGuaranteeTests
{
    private static readonly string[] Compliant =
    [
        "redis-server",
        "--save",
        "",
        "--appendonly",
        "no",
        "--maxmemory-policy",
        "noeviction",
    ];

    private static IContainer Redis(params string[] command) =>
        new ContainerBuilder()
            .WithImage("redis:7-alpine")
            .WithPortBinding(6379, true)
            .WithCommand(command)
            .WithWaitStrategy(
                Wait.ForUnixContainer().UntilMessageIsLogged("Ready to accept connections")
            )
            .Build();

    private static string Endpoint(IContainer redis) =>
        $"{redis.Hostname}:{redis.GetMappedPublicPort(6379)}";

    private static async Task<Func<Task>> Ensure(params string[] command)
    {
        var redis = Redis(command);
        await redis.StartAsync();
        var endpoint = Endpoint(redis);
        return async () =>
        {
            try
            {
                await PermissionsCacheRedisGuarantee.EnsureAsync(endpoint, CancellationToken.None);
            }
            finally
            {
                await redis.DisposeAsync();
            }
        };
    }

    [Fact]
    public async Task Redis_conforme_permite_el_arranque()
    {
        var act = await Ensure(Compliant);

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task Redis_por_defecto_con_snapshots_RDB_impide_el_arranque()
    {
        var act = await Ensure("redis-server");

        (await act.Should().ThrowAsync<InvalidOperationException>()).WithMessage("*save=*");
    }

    [Fact]
    public async Task Redis_con_AOF_impide_el_arranque()
    {
        var act = await Ensure("redis-server", "--save", "", "--appendonly", "yes");

        (await act.Should().ThrowAsync<InvalidOperationException>()).WithMessage("*appendonly=*");
    }

    [Fact]
    public async Task Redis_con_politica_de_expulsion_impide_el_arranque()
    {
        var act = await Ensure(
            "redis-server",
            "--save",
            "",
            "--appendonly",
            "no",
            "--maxmemory-policy",
            "volatile-lru"
        );

        (await act.Should().ThrowAsync<InvalidOperationException>()).WithMessage(
            "*maxmemory-policy=*"
        );
    }

    [Fact]
    public async Task Redis_sin_CONFIG_no_es_verificable_e_impide_el_arranque()
    {
        var act = await Ensure([.. Compliant, "--rename-command", "CONFIG", ""]);

        (await act.Should().ThrowAsync<InvalidOperationException>()).WithMessage(
            "*no se pudo verificar*"
        );
    }

    [Fact]
    public async Task Redis_inaccesible_impide_el_arranque()
    {
        var act = () =>
            PermissionsCacheRedisGuarantee.EnsureAsync(
                "127.0.0.1:1,connectTimeout=500",
                CancellationToken.None
            );

        (await act.Should().ThrowAsync<InvalidOperationException>()).WithMessage(
            "*no se pudo verificar*"
        );
    }

    // Las marcas no se expulsan: con noeviction, la presión de memoria hace fallar las escrituras
    // (Begin → false → 503 sin persistir), nunca desaloja una marca ni una versión.
    [Fact]
    public async Task Con_noeviction_la_presion_de_memoria_no_expulsa_marcas_y_falla_cerrado()
    {
        await using var redis = Redis([.. Compliant, "--maxmemory", "4mb"]);
        await redis.StartAsync();
        await using var connection = await ConnectionMultiplexer.ConnectAsync(Endpoint(redis));
        var store = new PermissionsCacheVersionStore(connection);
        var pendingScope = PermissionsCacheScope.Company(Guid.NewGuid()).Key;
        await store.BeginAsync(pendingScope);

        var db = connection.GetDatabase();
        var filler = new string('x', 64 * 1024);
        var oom = false;
        for (var i = 0; i < 1000 && !oom; i++)
        {
            try
            {
                await db.StringSetAsync($"filler:{i}", filler);
            }
            catch (RedisServerException ex) when (ex.Message.Contains("OOM"))
            {
                oom = true;
            }
        }

        oom.Should().BeTrue();
        (await db.KeyExistsAsync(PermissionsCacheVersionStore.PendingKey(pendingScope)))
            .Should()
            .BeTrue();
        var cache = Node(connection, NewMemoryCache());
        (await cache.TryBeginAsync(PermissionsCacheScope.Company(Guid.NewGuid())))
            .Should()
            .BeFalse();
    }

    // Sin persistencia, un reinicio de Redis no puede recuperar una entrada anterior a una revocación.
    [Fact]
    public async Task Reinicio_de_Redis_conforme_no_recupera_entradas_anteriores()
    {
        await using var redis = Redis(Compliant);
        await redis.StartAsync();
        var tenantId = Guid.NewGuid();
        var companyId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var profileId = Guid.NewGuid();

        // Las entradas viven fuera de Redis aquí: incluso si sobrevivieran, las versiones perdidas
        // se re-inicializan por encima de las anteriores y la entrada no vuelve a coincidir.
        var data = NewMemoryCache();
        await using (var before = await ConnectionMultiplexer.ConnectAsync(Endpoint(redis)))
        {
            var cache = Node(before, data);
            var read = await cache.ReadAsync(tenantId, companyId, userId, profileId);
            await cache.WriteAsync(tenantId, companyId, userId, profileId, ["ventas.view"], read.Stamp!);
            (await cache.ReadAsync(tenantId, companyId, userId, profileId)).Keys.Should().NotBeNull();
        }

        await redis.StopAsync();
        await redis.StartAsync();

        await using var after = await ConnectionMultiplexer.ConnectAsync(Endpoint(redis));
        var reread = await Node(after, data).ReadAsync(tenantId, companyId, userId, profileId);
        reread.Keys.Should().BeNull();
        reread.MissReason.Should().Be(PermissionsCacheMissReason.VersionMismatch);
    }

    private static IDistributedCache NewMemoryCache() =>
        new MemoryDistributedCache(MsOptions.Create(new MemoryDistributedCacheOptions()));

    private static ResilientPermissionsCacheService Node(
        IConnectionMultiplexer connection,
        IDistributedCache data
    ) =>
        new(
            new DistributedPermissionsCacheService(data, new PermissionsCacheVersionStore(connection)),
            new PermissionsCacheDiagnostics(),
            NullLogger<ResilientPermissionsCacheService>.Instance
        );
}

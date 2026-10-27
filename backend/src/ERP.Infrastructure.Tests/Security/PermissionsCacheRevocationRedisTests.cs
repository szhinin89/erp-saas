using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using ERP.Application.Access.Caching;
using ERP.Domain.Access.Entities;
using ERP.Domain.Access.Interfaces;
using ERP.Infrastructure.Access.Caching;
using FluentAssertions;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using StackExchange.Redis;
using MsOptions = Microsoft.Extensions.Options.Options;

namespace ERP.Infrastructure.Tests.Security;

/// <summary>
/// SEC-06 — versiones y marcas de invalidación con Redis real, efímero y aislado (Testcontainers),
/// compartidas por dos "instancias" de ERP con conexiones distintas. Las entradas de datos usan un
/// <see cref="IDistributedCache"/> compartido por ambas (como RedisCache en producción): lo que se
/// verifica aquí es la atomicidad y visibilidad entre instancias de versiones y marcas.
/// Requiere Docker.
/// </summary>
[Trait("Category", "Redis")]
public sealed class PermissionsCacheRevocationRedisTests : IAsyncLifetime
{
    private const string Ventas = "ventas.view";

    private readonly IContainer _redis = new ContainerBuilder()
        .WithImage("redis:7-alpine")
        .WithPortBinding(6379, true)
        .WithWaitStrategy(
            Wait.ForUnixContainer().UntilMessageIsLogged("Ready to accept connections")
        )
        .Build();

    private readonly IDistributedCache _sharedData = new MemoryDistributedCache(
        MsOptions.Create(new MemoryDistributedCacheOptions())
    );

    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly Guid _companyId = Guid.NewGuid();
    private readonly Guid _userId = Guid.NewGuid();
    private readonly Guid _profileId = Guid.NewGuid();

    private string _endpoint = null!;
    private ConnectionMultiplexer _nodeA = null!;
    private ConnectionMultiplexer _nodeB = null!;

    public async Task InitializeAsync()
    {
        await _redis.StartAsync();
        _endpoint = $"{_redis.Hostname}:{_redis.GetMappedPublicPort(6379)}";
        _nodeA = await ConnectionMultiplexer.ConnectAsync(_endpoint);
        _nodeB = await ConnectionMultiplexer.ConnectAsync(_endpoint);
    }

    public async Task DisposeAsync()
    {
        await _nodeA.DisposeAsync();
        await _nodeB.DisposeAsync();
        await _redis.DisposeAsync();
    }

    private ResilientPermissionsCacheService Node(IConnectionMultiplexer redis) =>
        new(
            new DistributedPermissionsCacheService(
                _sharedData,
                new PermissionsCacheVersionStore(redis)
            ),
            new PermissionsCacheDiagnostics(),
            NullLogger<ResilientPermissionsCacheService>.Instance
        );

    private async Task CacheEntry(IPermissionsCacheService node)
    {
        var read = await node.ReadAsync(_tenantId, _companyId, _userId, _profileId);
        read.Stamp.Should().NotBeNull();
        await node.WriteAsync(_tenantId, _companyId, _userId, _profileId, [Ventas], read.Stamp!);
        (await node.ReadAsync(_tenantId, _companyId, _userId, _profileId)).Keys.Should().Equal(Ventas);
    }

    // Prueba 3 — R3: incremento atómico entre instancias
    [Fact]
    public async Task Invalidaciones_concurrentes_desde_dos_instancias_no_pierden_incrementos()
    {
        var scope = PermissionsCacheScope.Company(_companyId).Key;
        var storeA = new PermissionsCacheVersionStore(_nodeA);
        var storeB = new PermissionsCacheVersionStore(_nodeB);
        var (_, before) = await storeA.ReadAsync([scope]);

        using var start = new ManualResetEventSlim(false);
        var tasks = Enumerable
            .Range(0, 40)
            .Select(i =>
                Task.Run(async () =>
                {
                    start.Wait();
                    var store = i % 2 == 0 ? storeA : storeB;
                    await store.BeginAsync(scope);
                    await store.CompleteAsync(scope);
                })
            )
            .ToArray();
        start.Set();
        await Task.WhenAll(tasks);

        var (pending, after) = await storeB.ReadAsync([scope]);
        after[0].Should().Be(before[0] + 40);
        pending.Should().BeFalse();
    }

    // E / prueba 1 — R1 entre instancias
    [Fact]
    public async Task Escritura_tardia_de_otra_instancia_nace_obsoleta()
    {
        var nodeA = Node(_nodeA);
        var nodeB = Node(_nodeB);
        var read = await nodeB.ReadAsync(_tenantId, _companyId, _userId, _profileId);

        (await nodeA.TryBeginAsync(PermissionsCacheScope.Company(_companyId))).Should().BeTrue();
        (await nodeA.TryCompleteAsync(PermissionsCacheScope.Company(_companyId))).Should().BeTrue();
        await nodeB.WriteAsync(_tenantId, _companyId, _userId, _profileId, [Ventas], read.Stamp!);

        (await nodeB.ReadAsync(_tenantId, _companyId, _userId, _profileId))
            .MissReason.Should()
            .Be(PermissionsCacheMissReason.VersionMismatch);
    }

    // F / prueba 4 — Complete falla: ninguna instancia reutiliza permisos obsoletos
    [Fact]
    public async Task Complete_fallido_mantiene_la_proteccion_en_todas_las_instancias()
    {
        var nodeB = Node(_nodeB);
        await CacheEntry(nodeB);
        var failingConnection = await ConnectionMultiplexer.ConnectAsync(_endpoint);
        var nodeA = Node(failingConnection);
        var scope = PermissionsCacheScope.Company(_companyId);

        (await nodeA.TryBeginAsync(scope)).Should().BeTrue();
        // Revocación persistida; la conexión de A cae antes de Complete.
        await failingConnection.DisposeAsync();
        (await nodeA.TryCompleteAsync(scope)).Should().BeFalse();

        var repo = new Mock<IAccessRepository>();
        repo.Setup(r =>
                r.GetActiveProfilePermissionsForCompanyAsync(
                    _tenantId,
                    _companyId,
                    _profileId,
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(new List<AccessProfilePermission>()); // permiso revocado en BD
        var provider = new EffectivePermissionKeysProvider(
            nodeB,
            new PermissionsCacheDiagnostics(),
            repo.Object
        );

        (await nodeB.ReadAsync(_tenantId, _companyId, _userId, _profileId))
            .MissReason.Should()
            .Be(PermissionsCacheMissReason.InvalidationPending);
        (await provider.GetAllowedKeysAsync(_tenantId, _companyId, _userId, _profileId))
            .Should()
            .BeEmpty();

        var ttl = await _nodeB
            .GetDatabase()
            .KeyTimeToLiveAsync(PermissionsCacheVersionStore.PendingKey(scope.Key));
        ttl.Should().NotBeNull();
        ttl!.Value.Should().BeLessThanOrEqualTo(PermissionsCacheVersionStore.PendingExpiry);
    }

    // H — versión desalojada o perdida: se re-inicializa por encima de la anterior
    [Fact]
    public async Task Version_perdida_no_revalida_entradas_antiguas()
    {
        var node = Node(_nodeA);
        await CacheEntry(node);

        await _nodeA
            .GetDatabase()
            .KeyDeleteAsync(
                PermissionsCacheVersionStore.VersionKey(PermissionsCacheScope.Company(_companyId).Key)
            );

        (await node.ReadAsync(_tenantId, _companyId, _userId, _profileId))
            .MissReason.Should()
            .Be(PermissionsCacheMissReason.VersionMismatch);
    }

    // H — Redis no disponible: lectura degrada a BD, invalidación visible
    [Fact]
    public async Task Redis_no_disponible_degrada_la_lectura_y_hace_visible_la_invalidacion()
    {
        var connection = await ConnectionMultiplexer.ConnectAsync(_endpoint);
        var node = Node(connection);
        await connection.DisposeAsync();

        var read = await node.ReadAsync(_tenantId, _companyId, _userId, _profileId);
        read.MissReason.Should().Be(PermissionsCacheMissReason.Exception);
        read.Stamp.Should().BeNull();
        (await node.TryBeginAsync(PermissionsCacheScope.Company(_companyId))).Should().BeFalse();
    }

    // I — claves sin datos personales y en un único slot (hash tag)
    [Fact]
    public async Task Claves_de_version_comparten_hash_tag_y_distinguen_alcance()
    {
        var node = Node(_nodeA);
        await node.ReadAsync(_tenantId, _companyId, _userId, _profileId);

        var server = _nodeA.GetServer(_nodeA.GetEndPoints()[0]);
        var keys = server.Keys(pattern: "{erp:permissions}:v:*").Select(k => k.ToString()).ToList();
        keys.Should()
            .BeEquivalentTo(
                [
                    $"{{erp:permissions}}:v:tenant:{_tenantId:N}",
                    $"{{erp:permissions}}:v:company:{_companyId:N}",
                    $"{{erp:permissions}}:v:user:{_companyId:N}:{_userId:N}",
                ]
            );
    }
}

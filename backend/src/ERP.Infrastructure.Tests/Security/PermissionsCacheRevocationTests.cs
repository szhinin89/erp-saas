using System.Diagnostics;
using System.Text.Json;
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
/// SEC-06 — revocación de permisos con la caché real (decorador resiliente + servicio distribuido
/// + versiones atómicas, modo instancia única en memoria) y el provider real. La "BD" es un
/// repositorio simulado cuyo estado cambia como lo haría una revocación confirmada.
/// </summary>
public sealed class PermissionsCacheRevocationTests
{
    private const string Ventas = "ventas.view";
    private const string Compras = "compras.view";

    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly Guid _companyId = Guid.NewGuid();
    private readonly Guid _userId = Guid.NewGuid();
    private readonly Guid _profileP1 = Guid.NewGuid();
    private readonly Guid _profileP2 = Guid.NewGuid();
    private readonly Dictionary<Guid, string[]> _db = new();
    private readonly Mock<IAccessRepository> _repo = new();

    public PermissionsCacheRevocationTests()
    {
        _db[_profileP1] = [Ventas, Compras];
        _db[_profileP2] = [Compras];
        _repo
            .Setup(r =>
                r.GetActiveProfilePermissionsForCompanyAsync(
                    It.IsAny<Guid>(),
                    It.IsAny<Guid>(),
                    It.IsAny<Guid>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(
                (Guid tenantId, Guid _, Guid profileId, CancellationToken _) =>
                    _db.GetValueOrDefault(profileId, [])
                        .Select(k =>
                            AccessProfilePermission.Create(tenantId, profileId, k, true, profileId)
                        )
                        .ToList()
            );
    }

    private static IDistributedCache NewMemoryCache() =>
        new MemoryDistributedCache(MsOptions.Create(new MemoryDistributedCacheOptions()));

    private static ResilientPermissionsCacheService Cache(
        IDistributedCache data,
        PermissionsCacheVersionStore versions
    ) =>
        new(
            new DistributedPermissionsCacheService(data, versions),
            new PermissionsCacheDiagnostics(),
            NullLogger<ResilientPermissionsCacheService>.Instance
        );

    private EffectivePermissionKeysProvider Provider(IPermissionsCacheService cache) =>
        new(cache, new PermissionsCacheDiagnostics(), _repo.Object);

    private Task<IReadOnlyList<string>> Keys(IPermissionsCacheService cache, Guid profileId) =>
        Provider(cache).GetAllowedKeysAsync(_tenantId, _companyId, _userId, profileId);

    private static async Task Invalidate(IPermissionsCacheInvalidator cache, PermissionsCacheScope scope)
    {
        (await cache.TryBeginAsync(scope)).Should().BeTrue();
        (await cache.TryCompleteAsync(scope)).Should().BeTrue();
    }

    private void VerifyDbReads(int times) =>
        _repo.Verify(
            r =>
                r.GetActiveProfilePermissionsForCompanyAsync(
                    It.IsAny<Guid>(),
                    It.IsAny<Guid>(),
                    It.IsAny<Guid>(),
                    It.IsAny<CancellationToken>()
                ),
            Times.Exactly(times)
        );

    // A — caché válida
    [Fact]
    public async Task Lectura_normal_desde_cache_valida_no_consulta_la_BD()
    {
        var cache = Cache(NewMemoryCache(), new PermissionsCacheVersionStore());

        (await Keys(cache, _profileP1)).Should().BeEquivalentTo([Ventas, Compras]);
        (await Keys(cache, _profileP1)).Should().BeEquivalentTo([Ventas, Compras]);

        VerifyDbReads(1);
    }

    // B / D — revocación en el perfil con caché cargada
    [Fact]
    public async Task Revocar_un_permiso_del_perfil_invalida_la_entrada_cargada()
    {
        var cache = Cache(NewMemoryCache(), new PermissionsCacheVersionStore());
        (await Keys(cache, _profileP1)).Should().Contain(Ventas);

        _db[_profileP1] = [Compras];
        await Invalidate(cache, PermissionsCacheScope.Company(_companyId));

        (await Keys(cache, _profileP1)).Should().BeEquivalentTo([Compras]);
    }

    // E / prueba 1 — R1: lectura anterior a la revocación, escritura posterior
    [Fact]
    public async Task Lectura_previa_a_la_revocacion_no_publica_una_entrada_valida_despues()
    {
        var cache = Cache(NewMemoryCache(), new PermissionsCacheVersionStore());
        var staleRead = new[] { Ventas, Compras };
        var revoked = false;
        _repo
            .Setup(r =>
                r.GetActiveProfilePermissionsForCompanyAsync(
                    _tenantId,
                    _companyId,
                    _profileP1,
                    It.IsAny<CancellationToken>()
                )
            )
            .Returns(async () =>
            {
                // La carga lee el estado ANTERIOR; mientras tanto la revocación se confirma e
                // invalida — y la escritura de esta carga ocurre después.
                var snapshot = revoked ? [Compras] : staleRead;
                if (!revoked)
                {
                    revoked = true;
                    await Invalidate(cache, PermissionsCacheScope.Company(_companyId));
                }
                return snapshot
                    .Select(k =>
                        AccessProfilePermission.Create(_tenantId, _profileP1, k, true, _profileP1)
                    )
                    .ToList();
            });

        (await Keys(cache, _profileP1)).Should().Contain(Ventas); // respuesta de la request en curso

        (await Keys(cache, _profileP1)).Should().BeEquivalentTo([Compras]);
    }

    // Prueba 2 — R2: reasignación P1 → P2
    [Fact]
    public async Task Reasignar_P1_a_P2_no_reutiliza_permisos_de_P1_aunque_no_se_invalide()
    {
        var cache = Cache(NewMemoryCache(), new PermissionsCacheVersionStore());
        (await Keys(cache, _profileP1)).Should().Contain(Ventas);

        // Ni siquiera hace falta la invalidación: la entrada lleva el perfil para el que se calculó.
        (await Keys(cache, _profileP2)).Should().BeEquivalentTo([Compras]);
    }

    [Fact]
    public async Task Reasignacion_concurrente_con_una_carga_de_P1_no_deja_P1_vigente()
    {
        var cache = Cache(NewMemoryCache(), new PermissionsCacheVersionStore());
        var read = await cache.ReadAsync(_tenantId, _companyId, _userId, _profileP1);

        await Invalidate(cache, PermissionsCacheScope.User(_companyId, _userId)); // P1 → P2
        await cache.WriteAsync(_tenantId, _companyId, _userId, _profileP1, [Ventas], read.Stamp!);

        (await cache.ReadAsync(_tenantId, _companyId, _userId, _profileP1)).Keys.Should().BeNull();
        (await cache.ReadAsync(_tenantId, _companyId, _userId, _profileP2))
            .MissReason.Should()
            .Be(PermissionsCacheMissReason.ProfileMismatch);
    }

    // Prueba 3 — R3 en memoria: invalidaciones concurrentes no pierden incrementos
    [Fact]
    public async Task Invalidaciones_concurrentes_en_memoria_producen_versiones_distintas()
    {
        var versions = new PermissionsCacheVersionStore();
        var scope = PermissionsCacheScope.Company(_companyId).Key;
        var (_, before) = await versions.ReadAsync([scope]);

        using var start = new ManualResetEventSlim(false);
        var tasks = Enumerable
            .Range(0, 50)
            .Select(_ =>
                Task.Run(async () =>
                {
                    start.Wait();
                    await versions.BeginAsync(scope);
                    await versions.CompleteAsync(scope);
                })
            )
            .ToArray();
        start.Set();
        await Task.WhenAll(tasks);

        var (pending, after) = await versions.ReadAsync([scope]);
        after[0].Should().Be(before[0] + 50);
        pending.Should().BeFalse();
    }

    // F / prueba 4 — fallo de Complete: la marca de Begin persiste y nadie reutiliza la entrada
    [Fact]
    public async Task Invalidacion_en_curso_no_usa_ni_publica_entradas_y_resuelve_desde_BD()
    {
        var versions = new PermissionsCacheVersionStore();
        var cache = Cache(NewMemoryCache(), versions);
        (await Keys(cache, _profileP1)).Should().Contain(Ventas);

        (await cache.TryBeginAsync(PermissionsCacheScope.Company(_companyId))).Should().BeTrue();
        _db[_profileP1] = [Compras]; // persistida; Complete no llega (fallo)

        var read = await cache.ReadAsync(_tenantId, _companyId, _userId, _profileP1);
        read.MissReason.Should().Be(PermissionsCacheMissReason.InvalidationPending);
        read.Stamp.Should().BeNull();
        (await Keys(cache, _profileP1)).Should().BeEquivalentTo([Compras]);
        (await Keys(cache, _profileP1)).Should().BeEquivalentTo([Compras]);
        VerifyDbReads(3); // sin publicar: cada request mientras dure la marca va a BD
    }

    [Fact]
    public async Task Fallo_de_invalidacion_es_visible_como_false()
    {
        var redis = new Mock<IConnectionMultiplexer>();
        redis
            .Setup(r => r.GetDatabase(It.IsAny<int>(), It.IsAny<object?>()))
            .Throws(new RedisConnectionException(ConnectionFailureType.UnableToConnect, "down"));
        var cache = Cache(NewMemoryCache(), new PermissionsCacheVersionStore(redis.Object));
        var scope = PermissionsCacheScope.Company(_companyId);

        (await cache.TryBeginAsync(scope)).Should().BeFalse();
        (await cache.TryCompleteAsync(scope)).Should().BeFalse();
    }

    // H / prueba 5 — fallo de lectura de caché no concede permisos adicionales
    [Fact]
    public async Task Fallo_de_lectura_de_cache_resuelve_desde_BD_sin_publicar()
    {
        var redis = new Mock<IConnectionMultiplexer>();
        redis
            .Setup(r => r.GetDatabase(It.IsAny<int>(), It.IsAny<object?>()))
            .Throws(new RedisConnectionException(ConnectionFailureType.UnableToConnect, "down"));
        var cache = Cache(NewMemoryCache(), new PermissionsCacheVersionStore(redis.Object));

        var read = await cache.ReadAsync(_tenantId, _companyId, _userId, _profileP2);
        read.MissReason.Should().Be(PermissionsCacheMissReason.Exception);
        read.Stamp.Should().BeNull();
        (await Keys(cache, _profileP2)).Should().BeEquivalentTo([Compras]);
    }

    [Fact]
    public async Task Fallo_de_escritura_de_cache_no_concede_permisos()
    {
        var data = new Mock<IDistributedCache>();
        data.Setup(d => d.GetAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((byte[]?)null);
        data.Setup(d =>
                d.SetAsync(
                    It.IsAny<string>(),
                    It.IsAny<byte[]>(),
                    It.IsAny<DistributedCacheEntryOptions>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ThrowsAsync(new InvalidOperationException("write failed"));
        var cache = Cache(data.Object, new PermissionsCacheVersionStore());

        (await Keys(cache, _profileP2)).Should().BeEquivalentTo([Compras]);
        (await Keys(cache, _profileP2)).Should().BeEquivalentTo([Compras]);
    }

    [Fact]
    public async Task Sello_mas_antiguo_que_el_maximo_no_se_publica()
    {
        var data = NewMemoryCache();
        var cache = Cache(data, new PermissionsCacheVersionStore());
        var read = await cache.ReadAsync(_tenantId, _companyId, _userId, _profileP1);
        var old = read.Stamp! with
        {
            ObservedAtTimestamp =
                Stopwatch.GetTimestamp()
                - (long)(
                    (DistributedPermissionsCacheService.MaxStampAge.TotalSeconds + 1)
                    * Stopwatch.Frequency
                ),
        };

        await cache.WriteAsync(_tenantId, _companyId, _userId, _profileP1, [Ventas], old);

        (await cache.ReadAsync(_tenantId, _companyId, _userId, _profileP1))
            .MissReason.Should()
            .Be(PermissionsCacheMissReason.NotFound);
    }

    // G — dos revocaciones concurrentes sobre la misma empresa
    [Fact]
    public async Task Dos_revocaciones_concurrentes_invalidan_ambas_y_la_entrada_no_revive()
    {
        var cache = Cache(NewMemoryCache(), new PermissionsCacheVersionStore());
        var scope = PermissionsCacheScope.Company(_companyId);
        var read = await cache.ReadAsync(_tenantId, _companyId, _userId, _profileP1);

        await cache.TryBeginAsync(scope); // revocación A
        await cache.TryBeginAsync(scope); // revocación B
        await cache.TryCompleteAsync(scope); // A completa: B sigue en curso
        (await cache.ReadAsync(_tenantId, _companyId, _userId, _profileP1))
            .MissReason.Should()
            .Be(PermissionsCacheMissReason.InvalidationPending);
        await cache.TryCompleteAsync(scope);

        await cache.WriteAsync(_tenantId, _companyId, _userId, _profileP1, [Ventas], read.Stamp!);
        (await cache.ReadAsync(_tenantId, _companyId, _userId, _profileP1))
            .MissReason.Should()
            .Be(PermissionsCacheMissReason.VersionMismatch);
    }

    // I / prueba 9 — aislamiento de tenant y empresa
    [Fact]
    public async Task Invalidar_una_empresa_no_afecta_a_otra_y_el_tenant_invalida_todas()
    {
        var cache = Cache(NewMemoryCache(), new PermissionsCacheVersionStore());
        var otherCompany = Guid.NewGuid();
        await Cached(cache, _companyId);
        await Cached(cache, otherCompany);

        await Invalidate(cache, PermissionsCacheScope.Company(otherCompany));
        (await cache.ReadAsync(_tenantId, _companyId, _userId, _profileP1)).Keys.Should().NotBeNull();
        (await cache.ReadAsync(_tenantId, otherCompany, _userId, _profileP1)).Keys.Should().BeNull();

        await Invalidate(cache, PermissionsCacheScope.Tenant(_tenantId));
        (await cache.ReadAsync(_tenantId, _companyId, _userId, _profileP1)).Keys.Should().BeNull();
    }

    private async Task Cached(IPermissionsCacheService cache, Guid companyId)
    {
        var read = await cache.ReadAsync(_tenantId, companyId, _userId, _profileP1);
        await cache.WriteAsync(_tenantId, companyId, _userId, _profileP1, [Ventas], read.Stamp!);
        (await cache.ReadAsync(_tenantId, companyId, _userId, _profileP1)).Keys.Should().Equal(Ventas);
    }

    [Fact]
    public async Task Entrada_con_formato_anterior_sin_perfil_nunca_se_reutiliza()
    {
        var data = NewMemoryCache();
        var cache = Cache(data, new PermissionsCacheVersionStore());
        await data.SetAsync(
            $"permissions:{_companyId:N}:{_userId:N}",
            JsonSerializer.SerializeToUtf8Bytes(
                new { CompanyVersion = 0L, TenantVersion = 0L, Keys = new[] { Ventas } },
                new JsonSerializerOptions(JsonSerializerDefaults.Web)
            )
        );

        (await cache.ReadAsync(_tenantId, _companyId, _userId, _profileP1))
            .MissReason.Should()
            .Be(PermissionsCacheMissReason.ProfileMismatch);
    }

    // Opción A — contrato del Redis de autorización (evaluación pura)
    [Fact]
    public void Redis_conforme_no_tiene_incumplimientos()
    {
        PermissionsCacheRedisGuarantee
            .Evaluate("noeviction", "", "no", "master", "0")
            .Should()
            .BeEmpty();
    }

    [Theory]
    [InlineData("allkeys-lru", "", "no", "master", "0", "maxmemory-policy")]
    [InlineData("volatile-lru", "", "no", "master", "0", "maxmemory-policy")]
    [InlineData("noeviction", "3600 1 300 100", "no", "master", "0", "save")]
    [InlineData("noeviction", "", "yes", "master", "0", "appendonly")]
    [InlineData("noeviction", "", "no", "slave", "0", "role")]
    [InlineData("noeviction", "", "no", "master", "1", "connected_slaves")]
    [InlineData(null, "", "no", "master", "0", "maxmemory-policy=<no verificable>")]
    [InlineData("noeviction", null, "no", "master", "0", "save=<no verificable>")]
    [InlineData("noeviction", "", "no", null, null, "role=<no verificable>")]
    public void Redis_incompatible_o_no_verificable_se_rechaza(
        string? policy,
        string? save,
        string? appendonly,
        string? role,
        string? replicas,
        string expected
    )
    {
        PermissionsCacheRedisGuarantee
            .Evaluate(policy, save, appendonly, role, replicas)
            .Should()
            .Contain(v => v.StartsWith(expected, StringComparison.Ordinal));
    }

    // Prueba 10 — TTL de 300 s y cota de la marca de invalidación
    [Fact]
    public async Task TTL_de_la_entrada_sigue_en_300_segundos_y_la_marca_lo_cubre()
    {
        DistributedCacheEntryOptions? options = null;
        var data = new Mock<IDistributedCache>();
        data.Setup(d =>
                d.SetAsync(
                    It.IsAny<string>(),
                    It.IsAny<byte[]>(),
                    It.IsAny<DistributedCacheEntryOptions>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .Callback(
                (string _, byte[] _, DistributedCacheEntryOptions o, CancellationToken _) =>
                    options = o
            )
            .Returns(Task.CompletedTask);
        var cache = Cache(data.Object, new PermissionsCacheVersionStore());
        var read = await cache.ReadAsync(_tenantId, _companyId, _userId, _profileP1);

        await cache.WriteAsync(_tenantId, _companyId, _userId, _profileP1, [Ventas], read.Stamp!);

        DistributedPermissionsCacheService.DefaultTtlSeconds.Should().Be(300);
        options!.AbsoluteExpirationRelativeToNow.Should().Be(TimeSpan.FromSeconds(300));
        PermissionsCacheVersionStore
            .PendingExpiry.Should()
            .BeGreaterThan(
                DistributedPermissionsCacheService.MaxStampAge + TimeSpan.FromSeconds(300)
            );
    }
}

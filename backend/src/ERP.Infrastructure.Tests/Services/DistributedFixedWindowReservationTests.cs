using ERP.Infrastructure.Services;
using FluentAssertions;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using MsOptions = Microsoft.Extensions.Options.Options;

namespace ERP.Infrastructure.Tests.Services;

/// <summary>
/// SEC-A01 — <see cref="DistributedFixedWindowRateLimit.TryReserveAllAsync"/> y
/// <see cref="DistributedFixedWindowRateLimit.ReleaseAllAsync"/> con el respaldo en memoria: reserva
/// sin consumo parcial, liberación nunca negativa y sin efecto sobre la ventana siguiente. La ruta
/// Redis (Lua) se prueba en <see cref="DistributedFixedWindowRateLimitRedisTests"/>.
/// </summary>
public sealed class DistributedFixedWindowReservationTests
{
    private static readonly TimeSpan Window = TimeSpan.FromMinutes(15);

    // Inicio exacto de una ventana de 15 minutos.
    private static readonly DateTimeOffset Now = new(2026, 11, 3, 10, 0, 0, TimeSpan.Zero);

    private readonly IDistributedCache _cache = new MemoryDistributedCache(
        MsOptions.Create(new MemoryDistributedCacheOptions())
    );

    private Task<WindowReservation?> Reserve(
        DateTimeOffset now,
        params (string Key, int Limit)[] counters
    ) =>
        DistributedFixedWindowRateLimit.TryReserveAllAsync(
            _cache,
            null,
            counters,
            Window,
            now.UtcDateTime,
            CancellationToken.None
        );

    private Task Release(WindowReservation reservation, DateTimeOffset now) =>
        DistributedFixedWindowRateLimit.ReleaseAllAsync(
            _cache,
            null,
            reservation,
            now.UtcDateTime,
            CancellationToken.None
        );

    private async Task<int> Count(string windowedKey)
    {
        var raw = await _cache.GetStringAsync(windowedKey);
        return raw is null ? 0 : int.Parse(raw);
    }

    [Fact]
    public async Task Las_claves_llevan_el_numero_de_ventana()
    {
        var reservation = await Reserve(Now, ("a", 5));

        var windowId = Now.ToUnixTimeMilliseconds() / (long)Window.TotalMilliseconds;
        reservation!.Keys.Should().Equal($"a:{windowId}");
        reservation.WindowEndMs.Should().Be((windowId + 1) * (long)Window.TotalMilliseconds);
    }

    [Fact]
    public async Task Si_un_contador_esta_lleno_no_se_modifica_ninguno_ni_se_crean_claves()
    {
        var full = await Reserve(Now, ("b", 1));
        var keyA = $"a:{full!.Keys[0].Split(':')[1]}";
        var keyC = $"c:{full.Keys[0].Split(':')[1]}";

        var rejected = await Reserve(Now, ("a", 5), ("b", 1), ("c", 5));

        rejected.Should().BeNull();
        (await Count(keyA)).Should().Be(0);
        (await Count(full.Keys[0])).Should().Be(1);
        (await Count(keyC)).Should().Be(0);
        (await _cache.GetStringAsync(keyA)).Should().BeNull();
        (await _cache.GetStringAsync(keyC)).Should().BeNull();
    }

    [Fact]
    public async Task Admitida_incrementa_todos_los_contadores()
    {
        var reservation = await Reserve(Now, ("a", 5), ("b", 5));

        (await Count(reservation!.Keys[0])).Should().Be(1);
        (await Count(reservation.Keys[1])).Should().Be(1);
    }

    [Fact]
    public async Task Cincuenta_reservas_concurrentes_con_limite_5_admiten_exactamente_5()
    {
        using var start = new ManualResetEventSlim(false);
        var tasks = Enumerable
            .Range(0, 50)
            .Select(_ =>
                Task.Run(async () =>
                {
                    start.Wait();
                    return await Reserve(Now, ("ip", 100), ("pair", 5));
                })
            )
            .ToArray();
        start.Set();

        var results = await Task.WhenAll(tasks);

        results.Count(r => r is not null).Should().Be(5);
    }

    [Fact]
    public async Task Liberar_decrementa_elimina_al_llegar_a_cero_y_nunca_queda_negativo()
    {
        var first = await Reserve(Now, ("a", 5));
        var second = await Reserve(Now, ("a", 5));
        var key = first!.Keys[0];

        await Release(second!, Now);
        (await Count(key)).Should().Be(1);

        await Release(first, Now);
        (await _cache.GetStringAsync(key)).Should().BeNull();

        await Release(first, Now);
        (await _cache.GetStringAsync(key)).Should().BeNull();
    }

    [Fact]
    public async Task Liberacion_tardia_no_afecta_la_ventana_siguiente()
    {
        var old = await Reserve(Now, ("a", 5));
        var next = Now + Window;
        var current = await Reserve(next, ("a", 5));

        await Release(old!, next);

        current!.Keys[0].Should().NotBe(old!.Keys[0]);
        (await Count(current.Keys[0])).Should().Be(1);
    }

    [Fact]
    public async Task La_ventana_siguiente_empieza_con_cupo_completo()
    {
        (await Reserve(Now, ("a", 1))).Should().NotBeNull();
        (await Reserve(Now, ("a", 1))).Should().BeNull();

        (await Reserve(Now + Window, ("a", 1))).Should().NotBeNull();
    }
}

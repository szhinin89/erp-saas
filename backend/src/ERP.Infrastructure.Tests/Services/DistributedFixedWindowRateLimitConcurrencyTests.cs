using ERP.Application.Common.Config;
using ERP.Infrastructure.Services;
using FluentAssertions;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using MsOptions = Microsoft.Extensions.Options.Options;

namespace ERP.Infrastructure.Tests.Services;

/// <summary>
/// ZH-AUTH-PASSWORD-RESET-SECURITY-HOTFIX-01 (verificación final) — N solicitudes concurrentes para
/// la misma identidad nunca superan el límite (fallback en memoria: sincronización process-local).
/// </summary>
public sealed class DistributedFixedWindowRateLimitConcurrencyTests
{
    private const int Limit = 3;
    private const int Requests = 20;

    [Fact]
    public async Task Memoria_20_solicitudes_concurrentes_misma_identidad_solo_3_obtienen_permiso()
    {
        var cache = new MemoryDistributedCache(
            MsOptions.Create(new MemoryDistributedCacheOptions())
        );
        var throttle = new PasswordResetRequestThrottle(cache, Options(Limit));

        var granted = await RunConcurrentlyAsync(() => throttle.TryAcquireAsync("ana@test.com"));

        granted.Should().Be(Limit);
    }

    [Fact]
    public async Task Memoria_cache_lenta_no_permite_carrera_entre_lectura_y_escritura()
    {
        // Ensancha la ventana Get→Set para que una implementación no atómica falle de forma determinista.
        var cache = new SlowCache(
            new MemoryDistributedCache(MsOptions.Create(new MemoryDistributedCacheOptions()))
        );
        var throttle = new PasswordResetRequestThrottle(cache, Options(Limit));

        var granted = await RunConcurrentlyAsync(() => throttle.TryAcquireAsync("ana@test.com"));

        granted.Should().Be(Limit);
    }

    internal static async Task<int> RunConcurrentlyAsync(Func<Task<bool>> acquire)
    {
        using var start = new ManualResetEventSlim(false);
        var tasks = Enumerable
            .Range(0, Requests)
            .Select(_ =>
                Task.Run(async () =>
                {
                    start.Wait();
                    return await acquire();
                })
            )
            .ToArray();
        start.Set();
        var results = await Task.WhenAll(tasks);
        return results.Count(r => r);
    }

    internal static Microsoft.Extensions.Options.IOptions<PasswordResetOptions> Options(
        int limit
    ) =>
        MsOptions.Create(
            new PasswordResetOptions
            {
                IdentityRequestLimit = limit,
                IdentityRequestWindowMinutes = 60,
            }
        );

    private sealed class SlowCache(IDistributedCache inner) : IDistributedCache
    {
        public byte[]? Get(string key) => inner.Get(key);

        public async Task<byte[]?> GetAsync(string key, CancellationToken token = default)
        {
            var value = await inner.GetAsync(key, token);
            await Task.Delay(20, token);
            return value;
        }

        public void Set(string key, byte[] value, DistributedCacheEntryOptions options) =>
            inner.Set(key, value, options);

        public Task SetAsync(
            string key,
            byte[] value,
            DistributedCacheEntryOptions options,
            CancellationToken token = default
        ) => inner.SetAsync(key, value, options, token);

        public void Refresh(string key) => inner.Refresh(key);

        public Task RefreshAsync(string key, CancellationToken token = default) =>
            inner.RefreshAsync(key, token);

        public void Remove(string key) => inner.Remove(key);

        public Task RemoveAsync(string key, CancellationToken token = default) =>
            inner.RemoveAsync(key, token);
    }
}

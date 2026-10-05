using ERP.Application.Common.Config;
using ERP.Infrastructure.Services;
using FluentAssertions;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace ERP.Infrastructure.Tests.Services;

/// <summary>
/// ZH-AUTH-PASSWORD-RESET-SECURITY-HOTFIX-01 — cupo por identidad (clave sin email en claro) y
/// entrega simulada que nunca registra el enlace.
/// </summary>
public sealed class PasswordResetSecurityServicesTests
{
    private const string Email = "ana.perez@test.com";

    private static PasswordResetRequestThrottle Throttle(IDistributedCache cache, int limit) =>
        new(
            cache,
            Microsoft.Extensions.Options.Options.Create(
                new PasswordResetOptions
                {
                    IdentityRequestLimit = limit,
                    IdentityRequestWindowMinutes = 60,
                }
            )
        );

    [Fact]
    public async Task Throttle_permite_hasta_el_cupo_y_luego_rechaza_por_email_normalizado()
    {
        var throttle = Throttle(
            new MemoryDistributedCache(
                Microsoft.Extensions.Options.Options.Create(new MemoryDistributedCacheOptions())
            ),
            limit: 2
        );

        (await throttle.TryAcquireAsync(Email)).Should().BeTrue();
        (await throttle.TryAcquireAsync(" ANA.PEREZ@test.com ")).Should().BeTrue();
        (await throttle.TryAcquireAsync(Email)).Should().BeFalse();
        (await throttle.TryAcquireAsync("otra@test.com"))
            .Should()
            .BeTrue("cada identidad tiene su propio cupo");
    }

    [Fact]
    public async Task Throttle_nunca_usa_el_email_en_claro_en_la_clave_ni_en_el_valor()
    {
        var cache = new RecordingCache();

        await Throttle(cache, limit: 3).TryAcquireAsync(Email);

        cache
            .Keys.Should()
            .ContainSingle()
            .Which.Should()
            .StartWith("erp:auth:pwreset:rl:")
            .And.NotContain("ana")
            .And.NotContain("@");
        cache.Values.Should().AllSatisfy(v => v.Should().NotContain("ana"));
    }

    [Fact]
    public async Task Sender_simulado_no_registra_enlace_token_ni_email()
    {
        var logger = new RecordingLogger();
        const string link = "https://app.test/reset-password?token=SECRETO-RAW-123&tenantId=abc";

        await new LoggingPasswordResetLinkSender(logger).SendPasswordResetLinkAsync(Email, link);

        logger.Text.Should().Contain("PasswordResetDeliverySimulated");
        logger
            .Text.Should()
            .NotContain("SECRETO-RAW-123")
            .And.NotContain("token=")
            .And.NotContain(Email);
    }

    private sealed class RecordingCache : IDistributedCache
    {
        private readonly Dictionary<string, byte[]> _store = new();
        public IEnumerable<string> Keys => _store.Keys;
        public IEnumerable<string> Values =>
            _store.Values.Select(System.Text.Encoding.UTF8.GetString);

        public byte[]? Get(string key) => _store.GetValueOrDefault(key);

        public Task<byte[]?> GetAsync(string key, CancellationToken token = default) =>
            Task.FromResult(Get(key));

        public void Set(string key, byte[] value, DistributedCacheEntryOptions options) =>
            _store[key] = value;

        public Task SetAsync(
            string key,
            byte[] value,
            DistributedCacheEntryOptions options,
            CancellationToken token = default
        )
        {
            Set(key, value, options);
            return Task.CompletedTask;
        }

        public void Refresh(string key) { }

        public Task RefreshAsync(string key, CancellationToken token = default) =>
            Task.CompletedTask;

        public void Remove(string key) => _store.Remove(key);

        public Task RemoveAsync(string key, CancellationToken token = default)
        {
            Remove(key);
            return Task.CompletedTask;
        }
    }

    private sealed class RecordingLogger : ILogger<LoggingPasswordResetLinkSender>
    {
        private readonly List<string> _lines = new();
        public string Text => string.Join("\n", _lines);

        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter
        )
        {
            var values = state is IEnumerable<KeyValuePair<string, object?>> pairs
                ? string.Join(";", pairs.Select(p => $"{p.Key}={p.Value}"))
                : string.Empty;
            _lines.Add($"{eventId.Name}|{formatter(state, exception)}|{values}");
        }
    }
}

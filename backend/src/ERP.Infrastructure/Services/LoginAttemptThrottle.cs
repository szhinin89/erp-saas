using System.Security.Cryptography;
using System.Text;
using System.Threading.RateLimiting;
using ERP.Application.Common.Config;
using ERP.Application.Common.Interfaces;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using StackExchange.Redis;

namespace ERP.Infrastructure.Services;

/// <summary>
/// SEC-A01 — admisión compartida de <c>login</c> y <c>global-login</c>.
/// <list type="number">
/// <item>Reserva atómica (<see cref="DistributedFixedWindowRateLimit.TryReserveAllAsync"/>) de K2 (IP,
/// si está activo), K1 (cuenta + IP) y K3 (cuenta, si está activo): si uno está lleno no se consume
/// ninguno y se responde 429.</item>
/// <item>Permiso de concurrencia por instancia (<see cref="ConcurrencyLimiter"/>): sin permiso dentro
/// de la espera máxima se devuelve la reserva y se responde 429.</item>
/// <item>Login exitoso → se devuelve la reserva. Fallo, error o cancelación → se conserva hasta que
/// vence la ventana.</item>
/// </list>
/// Las claves contienen el SHA-256 del usuario/IP, nunca el valor en claro. Redis configurado pero
/// inaccesible → <see cref="LoginAdmissionStatus.Unavailable"/> (sin respaldo en memoria). Sin Redis
/// configurado, el respaldo en memoria solo se admite en Development/Testing (instancia única).
/// </summary>
public sealed class LoginAttemptThrottle : ILoginAttemptThrottle, IDisposable
{
    private const string KeyPrefix = "erp:auth:login:";

    private readonly IDistributedCache _cache;
    private readonly IConnectionMultiplexer? _redis;
    private readonly ILogger<LoginAttemptThrottle> _logger;
    private readonly TimeProvider _time;
    private readonly LoginThrottleOptions _options;
    private readonly bool _localFallbackAllowed;
    private readonly ConcurrencyLimiter _concurrency;

    /// <param name="redis">Conexión Redis compartida si está configurada; sin ella, respaldo en memoria.</param>
    public LoginAttemptThrottle(
        IDistributedCache cache,
        IOptions<LoginThrottleOptions> options,
        IHostEnvironment environment,
        ILogger<LoginAttemptThrottle> logger,
        IConnectionMultiplexer? redis = null,
        TimeProvider? timeProvider = null
    )
    {
        _cache = cache;
        _redis = redis;
        _logger = logger;
        _time = timeProvider ?? TimeProvider.System;
        _options = options.Value;
        _localFallbackAllowed =
            environment.IsDevelopment() || environment.IsEnvironment("Testing");

        var permits = _options.MaxConcurrentVerifications is > 0 and var p
            ? p
            : Environment.ProcessorCount;
        _concurrency = new ConcurrencyLimiter(
            new ConcurrencyLimiterOptions
            {
                PermitLimit = permits,
                QueueLimit = _options.QueueLimit is >= 0 and var q ? q : permits * 4,
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
            }
        );
    }

    public async Task<ILoginAttempt> BeginAsync(
        string identifier,
        string? clientIp,
        CancellationToken cancellationToken
    )
    {
        if (_redis is null && !_localFallbackAllowed)
        {
            _logger.LogError(
                "SEC-A01: login rechazado — Redis no configurado fuera de Development/Testing."
            );
            return RejectedAttempt.Unavailable;
        }

        WindowReservation? reservation;
        try
        {
            reservation = await DistributedFixedWindowRateLimit.TryReserveAllAsync(
                _cache,
                _redis,
                Counters(identifier, clientIp),
                TimeSpan.FromMinutes(Math.Max(1, _options.WindowMinutes)),
                _time.GetUtcNow(),
                cancellationToken
            );
        }
        catch (Exception ex) when (IsRedisFailure(ex))
        {
            _logger.LogError(ex, "SEC-A01: login rechazado — Redis de límites no disponible.");
            return RejectedAttempt.Unavailable;
        }

        if (reservation is null)
            return RejectedAttempt.RateLimited;

        RateLimitLease lease;
        try
        {
            using var wait = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            wait.CancelAfter(TimeSpan.FromMilliseconds(Math.Max(0, _options.MaxQueueWaitMilliseconds)));
            lease = await _concurrency.AcquireAsync(1, wait.Token);
        }
        catch (OperationCanceledException)
        {
            await ReleaseAsync(reservation);
            cancellationToken.ThrowIfCancellationRequested();
            return RejectedAttempt.RateLimited;
        }

        if (!lease.IsAcquired)
        {
            lease.Dispose();
            await ReleaseAsync(reservation);
            return RejectedAttempt.RateLimited;
        }

        return new AdmittedAttempt(this, reservation, lease);
    }

    public void Dispose() => _concurrency.Dispose();

    private (string Key, int Limit)[] Counters(string identifier, string? clientIp)
    {
        var account = identifier.Trim().ToLowerInvariant();
        var ip = string.IsNullOrWhiteSpace(clientIp) ? "unknown" : clientIp.Trim();
        var counters = new List<(string Key, int Limit)>(3);
        if (_options.IpLimit > 0)
            counters.Add(($"{KeyPrefix}ip:{Fingerprint(ip)}", _options.IpLimit));
        counters.Add(
            ($"{KeyPrefix}pair:{Fingerprint($"{account}\n{ip}")}", Math.Max(1, _options.AccountIpLimit))
        );
        if (_options.AccountLimit > 0)
            counters.Add(($"{KeyPrefix}acct:{Fingerprint(account)}", _options.AccountLimit));
        return [.. counters];
    }

    // Una devolución que falla deja el cupo consumido hasta que vence la ventana (lado seguro).
    private async Task ReleaseAsync(WindowReservation reservation)
    {
        try
        {
            await DistributedFixedWindowRateLimit.ReleaseAllAsync(
                _cache,
                _redis,
                reservation,
                _time.GetUtcNow(),
                CancellationToken.None
            );
        }
        catch (Exception ex) when (IsRedisFailure(ex))
        {
            _logger.LogWarning(ex, "SEC-A01: no se pudo devolver la reserva de login.");
        }
    }

    private static bool IsRedisFailure(Exception ex) => ex is RedisException or TimeoutException;

    private static string Fingerprint(string value) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));

    private sealed class RejectedAttempt(LoginAdmissionStatus status) : ILoginAttempt
    {
        public static readonly RejectedAttempt RateLimited = new(LoginAdmissionStatus.RateLimited);
        public static readonly RejectedAttempt Unavailable = new(LoginAdmissionStatus.Unavailable);

        public LoginAdmissionStatus Status => status;

        public Task MarkSucceededAsync() => Task.CompletedTask;

        public void Dispose() { }
    }

    private sealed class AdmittedAttempt(
        LoginAttemptThrottle owner,
        WindowReservation reservation,
        RateLimitLease lease
    ) : ILoginAttempt
    {
        private int _released;

        public LoginAdmissionStatus Status => LoginAdmissionStatus.Admitted;

        public Task MarkSucceededAsync() =>
            Interlocked.Exchange(ref _released, 1) == 0
                ? owner.ReleaseAsync(reservation)
                : Task.CompletedTask;

        public void Dispose() => lease.Dispose();
    }
}

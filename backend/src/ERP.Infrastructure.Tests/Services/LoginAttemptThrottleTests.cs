using ERP.Application.Common.Config;
using ERP.Application.Common.Interfaces;
using ERP.Infrastructure.Services;
using FluentAssertions;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using StackExchange.Redis;
using MsOptions = Microsoft.Extensions.Options.Options;

namespace ERP.Infrastructure.Tests.Services;

/// <summary>
/// SEC-A01 — <see cref="LoginAttemptThrottle"/> con el respaldo en memoria (Testing): K1 cuenta+IP,
/// K2 IP, K3 cuenta, concurrencia por instancia y fail-closed sin Redis disponible. Los límites son
/// los candidatos de pruebas locales.
/// </summary>
public sealed class LoginAttemptThrottleTests
{
    private const string StoreIp = "198.51.100.10";

    private static LoginAttemptThrottle Throttle(
        LoginThrottleOptions? options = null,
        string environment = "Testing",
        IConnectionMultiplexer? redis = null
    ) =>
        new(
            new MemoryDistributedCache(MsOptions.Create(new MemoryDistributedCacheOptions())),
            MsOptions.Create(options ?? new LoginThrottleOptions()),
            new StubHostEnvironment(environment),
            NullLogger<LoginAttemptThrottle>.Instance,
            redis,
            new FixedTime(new DateTimeOffset(2026, 11, 3, 10, 0, 0, TimeSpan.Zero))
        );

    /// <summary>Un intento completo: admite, y si se admitió termina con éxito o fallo.</summary>
    private static async Task<LoginAdmissionStatus> Attempt(
        LoginAttemptThrottle throttle,
        string user,
        string ip,
        bool succeeds
    )
    {
        using var attempt = await throttle.BeginAsync(user, ip, CancellationToken.None);
        if (attempt.Status == LoginAdmissionStatus.Admitted && succeeds)
            await attempt.MarkSucceededAsync();
        return attempt.Status;
    }

    [Fact]
    public async Task Veinte_cajeros_en_la_misma_IP_iniciando_sesion_varias_veces_nunca_son_rechazados()
    {
        var throttle = Throttle(new LoginThrottleOptions { IpLimit = 100 });

        var statuses = new List<LoginAdmissionStatus>();
        for (var round = 0; round < 10; round++)
            for (var n = 1; n <= 20; n++)
                statuses.Add(await Attempt(throttle, $"cajero{n:00}", StoreIp, succeeds: true));

        statuses.Should().OnlyContain(s => s == LoginAdmissionStatus.Admitted);
    }

    [Fact]
    public async Task Veinte_cajeros_con_dos_errores_cada_uno_siguen_entrando()
    {
        var throttle = Throttle(new LoginThrottleOptions { IpLimit = 100 });

        var statuses = new List<LoginAdmissionStatus>();
        for (var n = 1; n <= 20; n++)
        {
            statuses.Add(await Attempt(throttle, $"cajero{n:00}", StoreIp, succeeds: false));
            statuses.Add(await Attempt(throttle, $"cajero{n:00}", StoreIp, succeeds: false));
            statuses.Add(await Attempt(throttle, $"cajero{n:00}", StoreIp, succeeds: true));
        }

        statuses.Should().OnlyContain(s => s == LoginAdmissionStatus.Admitted);
    }

    [Fact]
    public async Task Ataque_desde_una_IP_bloquea_solo_el_par_cuenta_IP()
    {
        var throttle = Throttle();
        const string attacker = "203.0.113.66";

        for (var i = 0; i < 5; i++)
            (await Attempt(throttle, "cajero01", attacker, succeeds: false))
                .Should()
                .Be(LoginAdmissionStatus.Admitted);

        (await Attempt(throttle, "cajero01", attacker, succeeds: false))
            .Should()
            .Be(LoginAdmissionStatus.RateLimited);
        (await Attempt(throttle, "CAJERO01 ", attacker, succeeds: false))
            .Should()
            .Be(LoginAdmissionStatus.RateLimited, "el usuario se normaliza");
        (await Attempt(throttle, "cajero01", StoreIp, succeeds: true))
            .Should()
            .Be(LoginAdmissionStatus.Admitted, "el cajero legítimo usa otro par cuenta+IP");
        (await Attempt(throttle, "cajero02", attacker, succeeds: false))
            .Should()
            .Be(LoginAdmissionStatus.Admitted, "K2 por IP está desactivado por defecto");
    }

    [Fact]
    public async Task Ataque_distribuido_alcanza_K3_y_bloquea_la_cuenta_desde_cualquier_IP_riesgo_residual()
    {
        var throttle = Throttle();

        for (var i = 1; i <= 30; i++)
            (await Attempt(throttle, "cajero01", $"203.0.113.{i}", succeeds: false))
                .Should()
                .Be(LoginAdmissionStatus.Admitted);

        (await Attempt(throttle, "cajero01", "203.0.113.200", succeeds: false))
            .Should()
            .Be(LoginAdmissionStatus.RateLimited);
        // Bloqueo dirigido temporal (sin cookie de dispositivo): también alcanza al cajero real
        // hasta que vence la ventana. Otras cuentas no se ven afectadas.
        (await Attempt(throttle, "cajero01", StoreIp, succeeds: true))
            .Should()
            .Be(LoginAdmissionStatus.RateLimited);
        (await Attempt(throttle, "cajero02", StoreIp, succeeds: true))
            .Should()
            .Be(LoginAdmissionStatus.Admitted);
    }

    [Fact]
    public async Task Rechazo_por_K1_no_consume_K3()
    {
        var throttle = Throttle(new LoginThrottleOptions { AccountIpLimit = 1, AccountLimit = 2 });

        (await Attempt(throttle, "ana", "203.0.113.1", false)).Should().Be(LoginAdmissionStatus.Admitted);
        for (var i = 0; i < 10; i++)
            (await Attempt(throttle, "ana", "203.0.113.1", false))
                .Should()
                .Be(LoginAdmissionStatus.RateLimited);

        // Si los rechazos hubieran consumido K3 (límite 2) esta solicitud sería rechazada.
        (await Attempt(throttle, "ana", "203.0.113.2", false)).Should().Be(LoginAdmissionStatus.Admitted);
    }

    [Fact]
    public async Task K2_activo_limita_una_IP_que_prueba_muchas_cuentas()
    {
        var throttle = Throttle(new LoginThrottleOptions { IpLimit = 10 });
        const string attacker = "203.0.113.66";

        for (var n = 0; n < 10; n++)
            (await Attempt(throttle, $"user{n}", attacker, false))
                .Should()
                .Be(LoginAdmissionStatus.Admitted);

        (await Attempt(throttle, "user99", attacker, false))
            .Should()
            .Be(LoginAdmissionStatus.RateLimited);
        (await Attempt(throttle, "user99", StoreIp, false))
            .Should()
            .Be(LoginAdmissionStatus.Admitted);
    }

    [Fact]
    public async Task K2_desactivado_y_K3_desactivado_no_limitan()
    {
        var throttle = Throttle(new LoginThrottleOptions { IpLimit = 0, AccountLimit = 0 });

        for (var i = 1; i <= 100; i++)
            (await Attempt(throttle, "cajero01", $"203.0.113.{i}", false))
                .Should()
                .Be(LoginAdmissionStatus.Admitted);
    }

    [Fact]
    public async Task Sin_permiso_de_concurrencia_responde_RateLimited_y_devuelve_la_reserva()
    {
        var throttle = Throttle(
            new LoginThrottleOptions
            {
                AccountIpLimit = 2,
                MaxConcurrentVerifications = 1,
                QueueLimit = 0,
            }
        );

        var holder = await throttle.BeginAsync("ana", StoreIp, CancellationToken.None);
        holder.Status.Should().Be(LoginAdmissionStatus.Admitted);

        using (var overflow = await throttle.BeginAsync("ana", StoreIp, CancellationToken.None))
            overflow.Status.Should().Be(LoginAdmissionStatus.RateLimited);

        holder.Dispose(); // fallo: conserva su cupo (1 de 2)

        // Si el rechazo por capacidad no hubiera devuelto su reserva, K1 (2) ya estaría lleno.
        (await Attempt(throttle, "ana", StoreIp, false)).Should().Be(LoginAdmissionStatus.Admitted);
        (await Attempt(throttle, "ana", StoreIp, false)).Should().Be(LoginAdmissionStatus.RateLimited);
    }

    [Fact]
    public async Task La_cola_absorbe_rafagas_y_rechaza_al_vencer_la_espera_maxima()
    {
        var throttle = Throttle(
            new LoginThrottleOptions
            {
                MaxConcurrentVerifications = 1,
                QueueLimit = 1,
                MaxQueueWaitMilliseconds = 100,
            }
        );

        using var holder = await throttle.BeginAsync("ana", StoreIp, CancellationToken.None);

        var queued = throttle.BeginAsync("luis", StoreIp, CancellationToken.None);
        using (var overflow = await throttle.BeginAsync("eva", StoreIp, CancellationToken.None))
            overflow.Status.Should().Be(LoginAdmissionStatus.RateLimited, "cola llena");

        using var timedOut = await queued;
        timedOut.Status.Should().Be(LoginAdmissionStatus.RateLimited, "venció la espera máxima");
    }

    [Fact]
    public async Task En_cola_obtiene_el_permiso_cuando_se_libera()
    {
        var throttle = Throttle(
            new LoginThrottleOptions
            {
                MaxConcurrentVerifications = 1,
                QueueLimit = 1,
                MaxQueueWaitMilliseconds = 5000,
            }
        );

        var holder = await throttle.BeginAsync("ana", StoreIp, CancellationToken.None);
        var queued = throttle.BeginAsync("luis", StoreIp, CancellationToken.None);
        holder.Dispose();

        using var admitted = await queued;
        admitted.Status.Should().Be(LoginAdmissionStatus.Admitted);
    }

    [Fact]
    public async Task Redis_configurado_pero_inaccesible_responde_Unavailable()
    {
        using var unreachable = await ConnectionMultiplexer.ConnectAsync(
            "127.0.0.1:1,abortConnect=false,connectTimeout=200,syncTimeout=500,asyncTimeout=500,connectRetry=0"
        );
        var throttle = Throttle(redis: unreachable, environment: "Production");

        using var attempt = await throttle.BeginAsync("ana", StoreIp, CancellationToken.None);

        attempt.Status.Should().Be(LoginAdmissionStatus.Unavailable);
    }

    [Fact]
    public async Task Sin_Redis_fuera_de_Development_o_Testing_no_usa_el_respaldo_en_memoria()
    {
        var throttle = Throttle(environment: "Production");

        using var attempt = await throttle.BeginAsync("ana", StoreIp, CancellationToken.None);

        attempt.Status.Should().Be(LoginAdmissionStatus.Unavailable);
    }

    [Fact]
    public async Task Sin_Redis_en_Development_usa_el_respaldo_en_memoria()
    {
        var throttle = Throttle(environment: "Development");

        using var attempt = await throttle.BeginAsync("ana", StoreIp, CancellationToken.None);

        attempt.Status.Should().Be(LoginAdmissionStatus.Admitted);
    }

    private sealed class FixedTime(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    internal sealed class StubHostEnvironment(string name) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = name;
        public string ApplicationName { get; set; } = "ERP.Infrastructure.Tests";
        public string ContentRootPath { get; set; } = "";
        public IFileProvider ContentRootFileProvider { get; set; } = null!;
    }
}

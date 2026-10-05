using System.Collections.Concurrent;
using System.Net;
using System.Text;
using System.Text.Json;
using ERP.API.Tests.Support;
using ERP.Application.Auth.UseCases.PasswordReset;
using ERP.Application.Common.Interfaces;
using ERP.Domain.Access.Entities;
using ERP.Domain.Modules.Company.Entities;
using ERP.Domain.Tenants.Entities;
using ERP.Infrastructure.Persistence;
using ERP.Infrastructure.Services;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace ERP.API.Tests.Integration;

/// <summary>
/// ZH-AUTH-PASSWORD-RESET-SECURITY-HOTFIX-01 — por HTTP real (pipeline, rate limiter, handler y
/// PostgreSQL reales): forgot-password no permite enumerar cuentas, no emite token sin una cuenta
/// inequívoca, nunca registra token/enlace/email, limita por IP (429) y por identidad (neutral), y
/// el reset con token sigue funcionando. Requiere Docker.
/// </summary>
[Trait("Category", "PostgreSql")]
public sealed class ForgotPasswordSecurityHttpTests
    : IClassFixture<ForgotPasswordSecurityHttpTests.Fixture>
{
    private const string NewPassword = "N3wPassword!2026";
    private readonly Fixture _f;

    public ForgotPasswordSecurityHttpTests(Fixture fixture) => _f = fixture;

    // C/D/E + F/G — misma respuesta pública; token solo para la cuenta inequívoca.
    [Fact]
    public async Task Existente_inexistente_y_multiples_tenants_responden_identico_y_solo_la_existente_emite_token()
    {
        using var app = _f.CreateApp();
        using var client = app.Client;

        var existing = await ForgotAsync(client, _f.EmailNeutral);
        var nonexistent = await ForgotAsync(client, $"nadie-{Guid.NewGuid():N}@test.com");
        var multiple = await ForgotAsync(client, _f.EmailMultiTenant);

        existing.Status.Should().Be(HttpStatusCode.OK);
        nonexistent.Should().BeEquivalentTo(existing);
        multiple.Should().BeEquivalentTo(existing);

        (await _f.TokensForAsync(_f.UserNeutralId)).Should().HaveCount(1);
        (await _f.TokensForAsync(_f.UserMultiTenantId)).Should().BeEmpty();
        app.SentLinks.Should().ContainSingle("solo la cuenta inequívoca recibe entrega");
    }

    // A/B — ni token raw, ni enlace, ni email en los logs del handler ni del sender.
    [Fact]
    public async Task Token_y_enlace_nunca_aparecen_en_logs()
    {
        using var app = _f.CreateApp();
        using var client = app.Client;

        await ForgotAsync(client, _f.EmailLogs);

        var link = app.SentLinks.Should().ContainSingle().Subject;
        var rawToken = Uri.UnescapeDataString(link.Split("token=")[1].Split('&')[0]);
        var logs = app.AllLogText();
        logs.Should()
            .Contain("PasswordResetDeliveryRequested")
            .And.Contain("PasswordResetDeliverySimulated");
        logs.Should().NotContain(rawToken);
        logs.Should().NotContain(Uri.EscapeDataString(rawToken));
        logs.Should().NotContain(link);
        logs.Should().NotContain("token=");
        logs.Should().NotContain(_f.EmailLogs);
    }

    // H/I/K — una nueva solicitud invalida la anterior; el token vigente restablece una vez.
    [Fact]
    public async Task Nueva_solicitud_invalida_la_anterior_y_el_token_vigente_restablece_una_sola_vez()
    {
        using var app = _f.CreateApp();
        using var client = app.Client;

        await ForgotAsync(client, _f.EmailReset);
        await ForgotAsync(client, _f.EmailReset);

        var tokens = await _f.TokensForAsync(_f.UserResetId);
        tokens.Should().HaveCount(2);
        tokens
            .OrderBy(t => t.CreatedAt)
            .First()
            .Used.Should()
            .BeTrue("emitir uno nuevo invalida el anterior");

        var firstToken = TokenFrom(app.SentLinks[0]);
        var currentToken = TokenFrom(app.SentLinks[1]);

        (await ResetAsync(client, firstToken)).Should().Be(HttpStatusCode.BadRequest);
        (await ResetAsync(client, currentToken)).Should().Be(HttpStatusCode.OK);
        (await ResetAsync(client, currentToken))
            .Should()
            .Be(HttpStatusCode.BadRequest, "single-use");
    }

    // M/N — cupo por identidad: al excederse no hay token y la respuesta no cambia,
    // con o sin cuenta.
    [Fact]
    public async Task Cupo_por_identidad_suprime_en_silencio_exista_o_no_la_cuenta()
    {
        using var app = _f.CreateApp(identityLimit: 2);
        using var client = app.Client;
        var nonexistent = $"nadie-{Guid.NewGuid():N}@test.com";

        var existingResponses = new List<PublicResponse>();
        var nonexistentResponses = new List<PublicResponse>();
        for (var i = 0; i < 4; i++)
        {
            existingResponses.Add(await ForgotAsync(client, _f.EmailThrottle));
            nonexistentResponses.Add(await ForgotAsync(client, nonexistent));
        }

        existingResponses
            .Concat(nonexistentResponses)
            .Should()
            .AllBeEquivalentTo(existingResponses[0]);
        existingResponses[0].Status.Should().Be(HttpStatusCode.OK);
        (await _f.TokensForAsync(_f.UserThrottleId)).Should().HaveCount(2);
        app.AllLogText().Should().Contain("RateLimited");
    }

    // L/N — límite por IP: 429 RATE_LIMITED igual para cualquier email; reset-password no comparte la política.
    [Fact]
    public async Task Limite_por_IP_responde_429_sin_distinguir_cuentas_y_no_afecta_reset_password()
    {
        using var app = _f.CreateApp(ipLimit: 2);
        using var client = app.Client;

        (await ForgotAsync(client, $"x-{Guid.NewGuid():N}@test.com"))
            .Status.Should()
            .Be(HttpStatusCode.OK);
        (await ForgotAsync(client, $"y-{Guid.NewGuid():N}@test.com"))
            .Status.Should()
            .Be(HttpStatusCode.OK);

        var limitedNonexistent = await ForgotAsync(client, $"z-{Guid.NewGuid():N}@test.com");
        var limitedExisting = await ForgotAsync(client, _f.EmailNeutral);

        limitedNonexistent.Status.Should().Be(HttpStatusCode.TooManyRequests);
        limitedNonexistent.Code.Should().Be("RATE_LIMITED");
        limitedExisting.Should().BeEquivalentTo(limitedNonexistent);

        for (var i = 0; i < 5; i++)
            (await ResetAsync(client, "token-invalido")).Should().Be(HttpStatusCode.BadRequest);
    }

    // ── helpers ─────────────────────────────────────────────────────────────────────────

    /// <summary>Parte pública comparable: status, envelope (sin meta volátil) y nombres de headers.</summary>
    private sealed record PublicResponse(
        HttpStatusCode Status,
        string? Code,
        string Envelope,
        string HeaderNames
    );

    private static async Task<PublicResponse> ForgotAsync(HttpClient client, string email)
    {
        using var response = await client.PostAsync(
            "/api/v1/auth/forgot-password",
            JsonContent(JsonSerializer.Serialize(new { email }))
        );
        var text = await response.Content.ReadAsStringAsync();
        string? code = null;
        var envelope = text;
        if (text.TrimStart().StartsWith('{'))
        {
            var root = JsonDocument.Parse(text).RootElement;
            code = root.TryGetProperty("code", out var c) ? c.GetString() : null;
            envelope = string.Join(
                "|",
                root.EnumerateObject()
                    .Where(p => p.Name != "meta")
                    .Select(p => $"{p.Name}={p.Value.GetRawText()}")
            );
        }
        var headerNames = string.Join(
            ",",
            response
                .Headers.Concat(response.Content.Headers)
                .Select(h => h.Key)
                .Where(k => !k.Equals("Date", StringComparison.OrdinalIgnoreCase))
                .OrderBy(k => k, StringComparer.OrdinalIgnoreCase)
        );
        return new PublicResponse(response.StatusCode, code, envelope, headerNames);
    }

    private async Task<HttpStatusCode> ResetAsync(HttpClient client, string token)
    {
        using var response = await client.PostAsync(
            "/api/v1/auth/reset-password",
            JsonContent(
                JsonSerializer.Serialize(
                    new
                    {
                        token,
                        newPassword = NewPassword,
                        tenantId = _f.TenantId,
                    }
                )
            )
        );
        return response.StatusCode;
    }

    private static string TokenFrom(string link) =>
        Uri.UnescapeDataString(link.Split("token=")[1].Split('&')[0]);

    private static StringContent JsonContent(string json) =>
        new(json, Encoding.UTF8, "application/json");

    // ── fixture ─────────────────────────────────────────────────────────────────────────

    public sealed class Fixture : IAsyncLifetime
    {
        private readonly PostgreSqlTestWebAppFactory _factory = new();
        private readonly Guid _adminId = Guid.NewGuid();

        public Guid TenantId { get; private set; }
        public string EmailNeutral { get; } = $"neutral-{Guid.NewGuid():N}@test.com";
        public string EmailLogs { get; } = $"logs-{Guid.NewGuid():N}@test.com";
        public string EmailReset { get; } = $"reset-{Guid.NewGuid():N}@test.com";
        public string EmailThrottle { get; } = $"throttle-{Guid.NewGuid():N}@test.com";
        public string EmailMultiTenant { get; } = $"multi-{Guid.NewGuid():N}@test.com";
        public Guid UserNeutralId { get; private set; }
        public Guid UserResetId { get; private set; }
        public Guid UserThrottleId { get; private set; }
        public Guid UserMultiTenantId { get; private set; }

        public async Task InitializeAsync()
        {
            Environment.SetEnvironmentVariable(
                "JWT__SECRETKEY",
                IntegrationTestConstants.JwtSecretKey
            );
            Environment.SetEnvironmentVariable("JWT__ISSUER", "ZHTechnologies");
            Environment.SetEnvironmentVariable("JWT__AUDIENCE", "ERPUsers");
            await _factory.InitializeAsync();
            await _factory.MigrateAsync();

            using var scope = _factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ErpDbContext>();
            var tenant = Tenant.Create("ZH-PwReset", $"zh-pr-{Guid.NewGuid():N}", _adminId);
            var otherTenant = Tenant.Create("ZH-PwReset-B", $"zh-prb-{Guid.NewGuid():N}", _adminId);
            db.Tenants.AddRange(tenant, otherTenant);
            await db.SaveChangesAsync();
            TenantId = tenant.Id;

            var company = Company.CreateManaged(
                tenant.Id,
                $"179{Guid.NewGuid():N}"[..13],
                "Empresa A",
                createdBy: _adminId
            );
            var otherCompany = Company.CreateManaged(
                otherTenant.Id,
                $"179{Guid.NewGuid():N}"[..13],
                "Empresa B",
                createdBy: _adminId
            );
            db.Companies.AddRange(company, otherCompany);
            await db.SaveChangesAsync();

            var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();
            async Task<Guid> SeedUserAsync(string email, params Guid[] companyIds)
            {
                var user = IdentityUser.Create(
                    $"u-{Guid.NewGuid():N}"[..20],
                    "Usuario",
                    "Prueba",
                    email,
                    hasher.HashPassword("Correcta#2026"),
                    _adminId
                );
                db.IdentityUsers.Add(user);
                await db.SaveChangesAsync();
                foreach (var companyId in companyIds)
                    db.CompanyUserMemberships.Add(
                        CompanyUserMembership.Create(companyId, user.Id, "Admin", null, _adminId)
                    );
                await db.SaveChangesAsync();
                return user.Id;
            }

            UserNeutralId = await SeedUserAsync(EmailNeutral, company.Id);
            await SeedUserAsync(EmailLogs, company.Id);
            UserResetId = await SeedUserAsync(EmailReset, company.Id);
            UserThrottleId = await SeedUserAsync(EmailThrottle, company.Id);
            UserMultiTenantId = await SeedUserAsync(EmailMultiTenant, company.Id, otherCompany.Id);
        }

        public async Task DisposeAsync() => await _factory.DisposeAsync();

        public async Task<List<ERP.Domain.Auth.Entities.PasswordResetToken>> TokensForAsync(
            Guid userId
        )
        {
            using var scope = _factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ErpDbContext>();
            return await db
                .PasswordResetTokens.AsNoTracking()
                .Where(t => t.UserId == userId)
                .ToListAsync();
        }

        /// <summary>
        /// App aislada (rate limiter y caché propios) con loggers de captura para el handler y el
        /// sender, y un sender que conserva el enlace en memoria (solo para el test) y delega en el
        /// sender real.
        /// </summary>
        public TestApp CreateApp(int ipLimit = 1000, int identityLimit = 1000)
        {
            var handlerLog = new CapturingLogger<ForgotPasswordHandler>();
            var senderLog = new CapturingLogger<LoggingPasswordResetLinkSender>();
            var links = new ConcurrentQueue<string>();
            var app = _factory.WithWebHostBuilder(builder =>
            {
                builder.UseSetting("PasswordReset:IpRequestLimit", ipLimit.ToString());
                builder.UseSetting("PasswordReset:IdentityRequestLimit", identityLimit.ToString());
                builder.ConfigureTestServices(services =>
                {
                    services.AddSingleton<ILogger<ForgotPasswordHandler>>(handlerLog);
                    services.AddScoped<IPasswordResetLinkSender>(_ => new RecordingSender(
                        new LoggingPasswordResetLinkSender(senderLog),
                        links
                    ));
                });
            });
            return new TestApp(app, handlerLog, senderLog, links);
        }
    }

    public sealed class TestApp(
        WebApplicationFactory<Program> app,
        CapturingLogger<ForgotPasswordHandler> handlerLog,
        CapturingLogger<LoggingPasswordResetLinkSender> senderLog,
        ConcurrentQueue<string> links
    ) : IDisposable
    {
        public HttpClient Client { get; } = app.CreateClient();
        public IReadOnlyList<string> SentLinks => links.ToList();

        public string AllLogText() => handlerLog.AllText() + "\n" + senderLog.AllText();

        public void Dispose()
        {
            Client.Dispose();
            app.Dispose();
        }
    }

    private sealed class RecordingSender(
        IPasswordResetLinkSender inner,
        ConcurrentQueue<string> links
    ) : IPasswordResetLinkSender
    {
        public Task SendPasswordResetLinkAsync(
            string toEmail,
            string resetLink,
            CancellationToken cancellationToken = default
        )
        {
            links.Enqueue(resetLink);
            return inner.SendPasswordResetLinkAsync(toEmail, resetLink, cancellationToken);
        }
    }
}

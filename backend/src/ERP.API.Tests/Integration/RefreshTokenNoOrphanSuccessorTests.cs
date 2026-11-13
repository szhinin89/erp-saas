using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ERP.Application.Common.Interfaces;
using ERP.Domain.Access.Entities;
using ERP.Domain.Modules.Company.Enums;
using ERP.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ERP.API.Tests.Integration;

/// <summary>
/// SEC-SUSP-02b — el refresh valida usuario/tenant/empresa/membresía ANTES de consumir el token:
/// un rechazo legítimo no deja un sucesor activo huérfano ni dispara una falsa alarma de robo en
/// el reintento. PostgreSQL desechable (Testcontainers).
/// </summary>
[Trait("Category", "PostgreSql")]
public sealed class RefreshTokenNoOrphanSuccessorTests : IClassFixture<CompanyIdentityUpdateFixture>
{
    private readonly CompanyIdentityUpdateFixture _f;

    public RefreshTokenNoOrphanSuccessorTests(CompanyIdentityUpdateFixture f) => _f = f;

    private sealed record Session(Guid CompanyId, Guid UserId, string Refresh);

    private async Task<Session> NewSessionAsync()
    {
        var companyId = await _f.CreateCompanyAsync(_f.TenantId);
        using var scope = _f.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ErpDbContext>();
        var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();
        var user = IdentityUser.Create(
            $"rt-{Guid.NewGuid():N}"[..12],
            "Refresh",
            "User",
            $"rt-{Guid.NewGuid():N}@test.com",
            hasher.HashPassword("Refresh-Pass-123!x"),
            _f.GlobalAdminId
        );
        db.IdentityUsers.Add(user);
        db.CompanyUserMemberships.Add(
            CompanyUserMembership.Create(companyId, user.Id, "Admin", null, _f.GlobalAdminId)
        );
        await db.SaveChangesAsync();
        var tokens = scope.ServiceProvider.GetRequiredService<IRefreshTokenService>();
        var (raw, _) = await tokens.CreateAsync(
            user.Id,
            _f.TenantId,
            companyId,
            RefreshUserType.Identity
        );
        return new Session(companyId, user.Id, raw);
    }

    private async Task MutateCompanyAsync(Guid id, Action<ERP.Domain.Modules.Company.Entities.Company> change)
    {
        using var scope = _f.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ErpDbContext>();
        var c = await db.Companies.IgnoreQueryFilters().SingleAsync(x => x.Id == id);
        change(c);
        await db.SaveChangesAsync();
    }

    private async Task SetPendingSetupAsync(Guid id)
    {
        using var scope = _f.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ErpDbContext>();
        var c = await db.Companies.IgnoreQueryFilters().SingleAsync(x => x.Id == id);
        db.Entry(c).Property(x => x.OperationalStatus).CurrentValue =
            CompanyOperationalStatus.PendingSetup;
        await db.SaveChangesAsync();
    }

    private async Task<List<(bool Revoked, string? Reason)>> TokensAsync(Guid userId)
    {
        using var scope = _f.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ErpDbContext>();
        var t = await db
            .RefreshTokens.IgnoreQueryFilters()
            .Where(x => x.UserId == userId)
            .OrderBy(x => x.CreatedAt)
            .ToListAsync();
        return t.Select(x => (x.IsRevoked, x.ReasonRevoked)).ToList();
    }

    private async Task<HttpResponseMessage> RefreshAsync(string raw)
    {
        using var client = _f.App.CreateClient();
        return await client.PostAsJsonAsync("/api/v1/auth/refresh", new { refreshToken = raw });
    }

    private static string? NewRefresh(string body)
    {
        try
        {
            return JsonDocument
                .Parse(body)
                .RootElement.GetProperty("data")
                .GetProperty("refreshToken")
                .GetString();
        }
        catch
        {
            return null;
        }
    }

    private static void ExpectUntouched(List<(bool Revoked, string? Reason)> tokens) =>
        tokens.Should().ContainSingle().Which.Revoked.Should().BeFalse();

    [Fact]
    public async Task Refresh_valido_rota_y_entrega_un_unico_sucesor()
    {
        var s = await NewSessionAsync();

        var http = await RefreshAsync(s.Refresh);

        http.StatusCode.Should().Be(HttpStatusCode.OK, await http.Content.ReadAsStringAsync());
        var tokens = await TokensAsync(s.UserId);
        tokens.Should().HaveCount(2);
        tokens.Count(t => !t.Revoked).Should().Be(1);
    }

    [Fact]
    public async Task Empresa_deshabilitada_rechaza_sin_consumir_ni_crear_sucesor()
    {
        var s = await NewSessionAsync();
        await MutateCompanyAsync(s.CompanyId, c => c.IsActive = false);

        var http = await RefreshAsync(s.Refresh);

        http.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        ExpectUntouched(await TokensAsync(s.UserId));
    }

    [Fact]
    public async Task Empresa_suspendida_conserva_la_sesion()
    {
        var s = await NewSessionAsync();
        await MutateCompanyAsync(s.CompanyId, c => c.SuspendOperations());

        var http = await RefreshAsync(s.Refresh);

        http.StatusCode.Should().Be(HttpStatusCode.OK, await http.Content.ReadAsStringAsync());
        (await TokensAsync(s.UserId)).Count(t => !t.Revoked).Should().Be(1);
    }

    [Fact]
    public async Task PendingSetup_conserva_la_sesion()
    {
        var s = await NewSessionAsync();
        await SetPendingSetupAsync(s.CompanyId);

        var http = await RefreshAsync(s.Refresh);

        http.StatusCode.Should().Be(HttpStatusCode.OK, await http.Content.ReadAsStringAsync());
        (await TokensAsync(s.UserId)).Count(t => !t.Revoked).Should().Be(1);
    }

    [Fact]
    public async Task Usuario_inactivo_rechaza_sin_consumir_ni_crear_sucesor()
    {
        var s = await NewSessionAsync();
        using (var scope = _f.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ErpDbContext>();
            var u = await db.IdentityUsers.IgnoreQueryFilters().SingleAsync(x => x.Id == s.UserId);
            u.Deactivate(_f.GlobalAdminId);
            await db.SaveChangesAsync();
        }

        var http = await RefreshAsync(s.Refresh);

        http.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        ExpectUntouched(await TokensAsync(s.UserId));
    }

    [Fact]
    public async Task Membresia_inactiva_rechaza_sin_consumir_ni_crear_sucesor()
    {
        var s = await NewSessionAsync();
        using (var scope = _f.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ErpDbContext>();
            var m = await db
                .CompanyUserMemberships.IgnoreQueryFilters()
                .SingleAsync(x => x.IdentityUserId == s.UserId && x.CompanyId == s.CompanyId);
            m.Deactivate(_f.GlobalAdminId);
            await db.SaveChangesAsync();
        }

        var http = await RefreshAsync(s.Refresh);

        http.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        ExpectUntouched(await TokensAsync(s.UserId));
    }

    [Fact]
    public async Task Token_expirado_se_rechaza_sin_sucesor()
    {
        var s = await NewSessionAsync();
        using (var scope = _f.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ErpDbContext>();
            await db
                .RefreshTokens.IgnoreQueryFilters()
                .Where(x => x.UserId == s.UserId)
                .ExecuteUpdateAsync(x => x.SetProperty(t => t.ExpiresAt, DateTime.UtcNow.AddMinutes(-1)));
        }

        var http = await RefreshAsync(s.Refresh);

        http.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await TokensAsync(s.UserId)).Should().ContainSingle();
    }

    [Fact]
    public async Task Token_revocado_explicitamente_se_rechaza_sin_sucesor()
    {
        var s = await NewSessionAsync();
        using (var scope = _f.CreateScope())
        {
            var tokens = scope.ServiceProvider.GetRequiredService<IRefreshTokenService>();
            await tokens.RevokeAsync(s.Refresh, "logout");
        }

        var http = await RefreshAsync(s.Refresh);

        http.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await TokensAsync(s.UserId)).Should().ContainSingle();
    }

    [Fact]
    public async Task Reutilizacion_real_tardia_revoca_la_familia()
    {
        var s = await NewSessionAsync();
        var first = await RefreshAsync(s.Refresh);
        first.StatusCode.Should().Be(HttpStatusCode.OK);
        var successor = NewRefresh(await first.Content.ReadAsStringAsync());
        successor.Should().NotBeNullOrEmpty();

        // Envejece la revocación por rotación fuera de la ventana de gracia.
        using (var scope = _f.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ErpDbContext>();
            await db
                .RefreshTokens.IgnoreQueryFilters()
                .Where(x => x.UserId == s.UserId && x.IsRevoked)
                .ExecuteUpdateAsync(x => x.SetProperty(t => t.RevokedAt, DateTime.UtcNow.AddHours(-1)));
        }

        var replay = await RefreshAsync(s.Refresh);

        replay.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await TokensAsync(s.UserId)).Should().OnlyContain(t => t.Revoked);
        (await RefreshAsync(successor!)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Dos_refresh_concurrentes_producen_un_unico_sucesor_activo()
    {
        var s = await NewSessionAsync();

        var results = await Task.WhenAll(RefreshAsync(s.Refresh), RefreshAsync(s.Refresh));

        results.Count(r => r.StatusCode == HttpStatusCode.OK).Should().Be(1);
        results.Count(r => r.StatusCode == HttpStatusCode.Unauthorized).Should().Be(1);
        var tokens = await TokensAsync(s.UserId);
        tokens.Should().HaveCount(2);
        tokens.Count(t => !t.Revoked).Should().Be(1);
    }

    [Fact]
    public async Task Rechazo_y_reintento_posterior_no_dispara_falsa_alarma_de_robo()
    {
        var s = await NewSessionAsync();
        await MutateCompanyAsync(s.CompanyId, c => c.IsActive = false);

        (await RefreshAsync(s.Refresh)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await RefreshAsync(s.Refresh)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        ExpectUntouched(await TokensAsync(s.UserId));

        // Al rehabilitar la empresa, el mismo token sigue siendo utilizable: nunca se quemó ni se
        // revocó la familia por "reutilización".
        await MutateCompanyAsync(s.CompanyId, c => c.IsActive = true);
        var ok = await RefreshAsync(s.Refresh);

        ok.StatusCode.Should().Be(HttpStatusCode.OK, await ok.Content.ReadAsStringAsync());
        var tokens = await TokensAsync(s.UserId);
        tokens.Should().HaveCount(2);
        tokens.Count(t => !t.Revoked).Should().Be(1);
        tokens.Should().NotContain(t => t.Reason == "Reutilización detectada");
    }
}

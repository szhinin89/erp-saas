using System.Net;
using System.Net.Http.Json;
using ERP.API.Tests.Support;
using ERP.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace ERP.API.Tests.Hosting;

[Trait("Category", "PostgreSql")]
public sealed class PostgreSqlTestWebAppFactoryTests
{
    [Fact]
    public async Task Installation_is_completed_before_base_and_derived_hosts_start()
    {
        await using var factory = new PostgreSqlTestWebAppFactory();
        await factory.InitializeAsync();
        // No access to Services/Server/CreateClient before this independent DB check.
        await using (var connection = new NpgsqlConnection(factory.ConnectionString))
        {
            await connection.OpenAsync();
            await using var state = new NpgsqlCommand("""
                SELECT is_initialized AND setup_token_hash IS NULL AND setup_token_expiry_utc IS NULL
                FROM system_setup_state WHERE id = 1
                """, connection);
            Assert.Equal(true, await state.ExecuteScalarAsync());
            await using var ownership = new NpgsqlCommand("SELECT pg_try_advisory_lock(1514686021, 1)", connection);
            Assert.Equal(true, await ownership.ExecuteScalarAsync());
        }

        await factory.MigrateAsync(); // Existing fixtures call this again: preparation is idempotent.
        using var client = factory.CreateClient();
        await using var derived = factory.WithWebHostBuilder(_ => { });
        using var derivedClient = derived.CreateClient();
        foreach (var http in new[] { client, derivedClient })
        {
            var state = await http.GetFromJsonAsync<System.Text.Json.JsonElement>("/api/v1/setup/status");
            Assert.True(state.GetProperty("data").GetProperty("isInitialized").GetBoolean());
            using var forbidden = await http.GetAsync("/api/v1/admin-core/companies");
            Assert.Equal(HttpStatusCode.Unauthorized, forbidden.StatusCode);
        }
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ErpDbContext>();
        var user = await db.IdentityUsers.SingleAsync(u => u.Username == PostgreSqlTestWebAppFactory.InitialOwnerUsername);
        Assert.Single(await db.GlobalUserRoles.Where(r => r.UserId == user.Id).ToListAsync());
        Assert.Single(await db.CompanyUserMemberships.IgnoreQueryFilters().Where(m => m.IdentityUserId == user.Id).ToListAsync());
        Assert.Single(await db.CompanyUserBranches.IgnoreQueryFilters().ToListAsync());
    }
}

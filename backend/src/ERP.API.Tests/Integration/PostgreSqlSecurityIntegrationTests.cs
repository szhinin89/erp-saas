using ERP.API.Tests.Support;
using ERP.Domain.MasterData.Entities;
using ERP.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ERP.API.Tests.Integration;

/// <summary>
/// Integración con PostgreSQL 16 real (Testcontainers). Requiere Docker.
/// </summary>
[Trait("Category", "PostgreSql")]
public sealed class PostgreSqlSecurityIntegrationTests : IAsyncLifetime
{
    private PostgreSqlTestWebAppFactory? _factory;

    public async Task InitializeAsync()
    {
        _factory = new PostgreSqlTestWebAppFactory();
        await _factory.InitializeAsync();
        await _factory.MigrateAsync();
    }

    public async Task DisposeAsync()
    {
        if (_factory is not null)
            await _factory.DisposeAsync();
    }

    [Fact]
    public async Task PG_unique_business_partner_identification_enforced()
    {
        using var scope = _factory!.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ErpDbContext>();

        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        var bp1 = BusinessPartner.Create(tenantId, "04", "1790016919001", 2, "Empresa A", userId);
        db.BusinessPartners.Add(bp1);
        await db.SaveChangesAsync();

        var bp2 = BusinessPartner.Create(tenantId, "04", "1790016919001", 2, "Empresa B", userId);
        db.BusinessPartners.Add(bp2);

        var act = async () => await db.SaveChangesAsync();
        await act.Should().ThrowAsync<DbUpdateException>();
    }

    /// <summary>
    /// TECH-DEBT-API-BUSINESSPARTNER-UNIQUE-FLAKY-01A — confirma el scope real del índice
    /// (uq_mbp_identification: tenant_id + identification_type + identification_number): la MISMA
    /// identificación en OTRO tenant debe poder registrarse sin conflicto — BusinessPartner es
    /// tenant-scoped, no un catálogo global, y el índice nunca fue pensado para bloquear esto.
    /// </summary>
    [Fact]
    public async Task PG_same_identification_in_different_tenant_is_allowed()
    {
        using var scope = _factory!.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ErpDbContext>();

        var userId = Guid.NewGuid();

        var bp1 = BusinessPartner.Create(Guid.NewGuid(), "04", "1790016919001", 2, "Empresa A", userId);
        db.BusinessPartners.Add(bp1);
        await db.SaveChangesAsync();

        var bp2 = BusinessPartner.Create(Guid.NewGuid(), "04", "1790016919001", 2, "Empresa B", userId);
        db.BusinessPartners.Add(bp2);

        var act = async () => await db.SaveChangesAsync();
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task PG_migrations_apply_and_database_connects()
    {
        using var scope = _factory!.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ErpDbContext>();
        (await db.Database.CanConnectAsync()).Should().BeTrue();
        (await db.Database.GetAppliedMigrationsAsync()).Should().NotBeEmpty();
    }
}

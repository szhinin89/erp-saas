using ERP.API.Tests.Support;
using ERP.Domain.MasterData.Entities;
using ERP.Domain.MasterData.Enums;
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

        var bp1 = BusinessPartner.Create(
            tenantId,
            "04",
            "1790016919001",
            PersonType.Legal,
            "Empresa A",
            userId
        );
        db.BusinessPartners.Add(bp1);
        await db.SaveChangesAsync();

        var bp2 = BusinessPartner.Create(
            tenantId,
            "04",
            "1790016919001",
            PersonType.Legal,
            "Empresa B",
            userId
        );
        db.BusinessPartners.Add(bp2);

        var act = async () => await db.SaveChangesAsync();
        await act.Should().ThrowAsync<DbUpdateException>();
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

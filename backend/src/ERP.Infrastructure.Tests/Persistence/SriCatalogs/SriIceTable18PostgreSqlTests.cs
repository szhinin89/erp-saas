using ERP.Application.Common;
using ERP.Domain.Modules.SriCatalogs.Entities;
using ERP.Domain.Modules.SriCatalogs.Enums;
using ERP.Infrastructure.Persistence;
using ERP.Infrastructure.Persistence.Configurations.SriCatalogs;
using ERP.Infrastructure.Persistence.Repositories.SriCatalogs;
using ERP.Infrastructure.Persistence.Services;
using FluentAssertions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Moq;
using Npgsql;
using Testcontainers.PostgreSql;

namespace ERP.Infrastructure.Tests.Persistence.SriCatalogs;

[Trait("Category", "PostgreSql")]
public sealed class SriIceTable18PostgreSqlTests
{
    [Fact]
    public async Task Fresh_install_matches_the_seed_and_operational_readers()
    {
        await using var postgres = await StartAsync("ice_table18_install_test");
        await using var db = CreateContext(postgres);
        await db.Database.MigrateAsync();

        var persisted = await db.SriIceRates.AsNoTracking().OrderBy(x => x.Code).ToListAsync();
        persisted.Should().BeEquivalentTo(SeedRates(), options => options.WithStrictOrdering());

        var lookup = new SriCatalogLookupRepository(db);
        var selectable = await lookup.GetActiveIceRatesAsync();
        selectable.Should().HaveCount(38).And.OnlyContain(x => x.CalculationType != SriTaxCalculationType.Mixed);
        selectable.Select(x => x.Code).Should().NotContain(["3041", "3630", "3680", "3051", "3620"]);

        var resolver = new SriTaxResolver(db);
        (await resolver.GetIceCatalogEntryAsync("3041")).Should().BeNull("la cerveza industrial es mixta y no calculable");
        var energizer = await resolver.GetIceCatalogEntryAsync("3101");
        energizer!.Name.Should().Be("ICE Bebidas Energizantes");
        energizer.Percentage.Should().Be(10m);
        var cigarettes = await resolver.GetIceCatalogEntryAsync("3011");
        cigarettes!.CalculationType.Should().Be(SriTaxCalculationType.Specific);
        cigarettes.UnitValue.Should().BeNull();
        (await resolver.GetIceCatalogEntryAsync("3051")).Should().BeNull();
        var vehicles = persisted.Single(x => x.Code == "3073");
        vehicles.Name.Should().Contain("Vehículos");
        vehicles.Percentage.Should().Be(5m);
        var weapons = persisted.Single(x => x.Code == "3630");
        weapons.Name.Should().Contain("Armas");
        weapons.Percentage.Should().Be(30m);

        (await db.Database.SqlQueryRaw<int>("SELECT count(*)::integer AS \"Value\" FROM global.sri_normative_source").SingleAsync())
            .Should().Be(10);
        var invalidValidity = () => db.Database.ExecuteSqlRawAsync(
            "UPDATE global.sri_ice_rate SET valid_until = DATE '2025-12-31' WHERE code = '3053'");
        (await invalidValidity.Should().ThrowAsync<PostgresException>()).Which.SqlState.Should().Be("23514");
    }

    private static async Task<PostgreSqlContainer> StartAsync(string database)
    {
        var postgres = new PostgreSqlBuilder().WithImage("postgres:16-alpine")
            .WithDatabase(database).WithUsername("erp").WithPassword("test_only").Build();
        await postgres.StartAsync();
        return postgres;
    }

    private static ErpDbContext CreateContext(PostgreSqlContainer postgres) => new(
        new DbContextOptionsBuilder<ErpDbContext>().UseNpgsql(postgres.GetConnectionString()).Options,
        Mock.Of<ICurrentTenant>(), Mock.Of<IPublisher>(), Mock.Of<ICurrentCompany>());

    private static List<SriIceRate> SeedRates()
    {
        var model = new ModelBuilder();
        model.ApplyConfiguration(new SriIceRateConfiguration());
        return model.Model.FindEntityType(typeof(SriIceRate))!.GetSeedData().Select(d => new SriIceRate
        {
            Code = (string)d[nameof(SriIceRate.Code)]!,
            Name = (string)d[nameof(SriIceRate.Name)]!,
            Percentage = (decimal?)d[nameof(SriIceRate.Percentage)],
            UnitValue = (decimal?)d[nameof(SriIceRate.UnitValue)],
            SpecificUnit = (string?)d[nameof(SriIceRate.SpecificUnit)],
            NormativeSourceId = (Guid?)d[nameof(SriIceRate.NormativeSourceId)],
            ValidFrom = (DateOnly?)d[nameof(SriIceRate.ValidFrom)],
            ValidUntil = (DateOnly?)d[nameof(SriIceRate.ValidUntil)],
            CalculationType = (SriTaxCalculationType)d[nameof(SriIceRate.CalculationType)]!,
            IsActive = (bool)d[nameof(SriIceRate.IsActive)]!,
        }).OrderBy(x => x.Code, StringComparer.Ordinal).ToList();
    }
}

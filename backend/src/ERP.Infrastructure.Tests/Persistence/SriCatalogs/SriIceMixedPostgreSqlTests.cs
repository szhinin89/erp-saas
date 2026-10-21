using ERP.Application.Common;
using ERP.Application.Modules.Catalog.UseCases;
using ERP.Domain.Modules.SriCatalogs.Entities;
using ERP.Domain.Modules.SriCatalogs.Enums;
using ERP.Infrastructure.Persistence;
using ERP.Infrastructure.Persistence.Configurations.SriCatalogs;
using ERP.Infrastructure.Persistence.Catalogs;
using ERP.Infrastructure.Services;
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
public sealed class SriIceMixedPostgreSqlTests
{
    [Fact]
    public async Task Fresh_install_keeps_mixed_rows_out_of_operational_readers()
    {
        await using var postgres = new PostgreSqlBuilder().WithImage("postgres:16-alpine")
            .WithDatabase("ice_mixed_isolated_test").WithUsername("erp").WithPassword("test_only").Build();
        await postgres.StartAsync();
        await using var db = new ErpDbContext(new DbContextOptionsBuilder<ErpDbContext>()
            .UseNpgsql(postgres.GetConnectionString()).Options,
            Mock.Of<ICurrentTenant>(), Mock.Of<IPublisher>(), Mock.Of<ICurrentCompany>());
        await db.Database.MigrateAsync();
        var lookup = new SriCatalogLookupRepository(db);
        var taxResolver = new SriTaxResolver(db);
        var codes = new[] { "3031", "3033" };
        foreach (var code in codes)
        {
            var row = await db.SriIceRates.AsNoTracking().SingleAsync(x => x.Code == code);
            row.CalculationType.Should().Be(SriTaxCalculationType.Mixed);
            row.Percentage.Should().Be(75m);
            row.UnitValue.Should().Be(10.41m);
            row.SpecificUnit.Should().Be("USD por litro de alcohol puro");
            row.IsActive.Should().BeFalse();
            var source = await db.Set<SriNormativeSource>().SingleAsync(x => x.Id == row.NormativeSourceId);
            source.Version.Should().Be("2026");
            source.ReferenceUrl.Should().Contain("sri.gob.ec");
            source.Notes.Should().Contain("2.34");
            (await taxResolver.GetIceCatalogEntryAsync(code)).Should().BeNull();
            (await taxResolver.GetIceRateAsync(code)).Should().BeNull();
            (await new SriGlobalRateReader(db).GetIcePercentageAsync(code)).Should().BeNull();
            (await new SriCatalogResolver(db).ResolveIceRatesAsync([code])).Should().BeEmpty();
        }
        var selected = await lookup.GetActiveIceRatesAsync();
        selected.Select(x => x.Code).Should().NotIntersectWith(codes);
        selected.Should().HaveCount(38);
        var response = await new GetSriIceRatesQueryHandler(lookup).Handle(new GetSriIceRatesQuery(), CancellationToken.None);
        response.IsSuccess.Should().BeTrue();
        var specific = response.Value!.Single(x => x.Code == "3053");
        specific.CalculationType.Should().Be("Specific");
        var percentage = response.Value!.Single(x => x.Code == "3073");
        percentage.CalculationType.Should().Be("Percentage");
        percentage.Percentage.Should().Be(5m);

        // The DB rejects activating a mixed row, dropping either component, unit or source.
        string[] invalidStatements = [
            "UPDATE global.sri_ice_rate SET is_active = true WHERE code = '3031'",
            "UPDATE global.sri_ice_rate SET percentage = NULL WHERE code = '3031'",
            "UPDATE global.sri_ice_rate SET unit_value = NULL WHERE code = '3031'",
            "UPDATE global.sri_ice_rate SET specific_unit = '' WHERE code = '3031'",
            "UPDATE global.sri_ice_rate SET normative_source_id = NULL WHERE code = '3031'",
        ];
        foreach (var sql in invalidStatements)
        {
            var invalid = () => db.Database.ExecuteSqlRawAsync(sql);
            (await invalid.Should().ThrowAsync<PostgresException>()).Which.SqlState.Should().Be("23514");
        }
        var deleteSource = () => db.Database.ExecuteSqlRawAsync("DELETE FROM global.sri_normative_source WHERE id = '40000000-0000-0000-0000-000000000006'");
        (await deleteSource.Should().ThrowAsync<PostgresException>()).Which.SqlState.Should().Be("23503");

        // An EF insert must also persist false despite DEFAULT true (ADR-042).
        await db.Database.ExecuteSqlRawAsync("DELETE FROM global.sri_ice_rate WHERE code = '3033'");
        db.SriIceRates.Add(new SriIceRate { Code = "3033", Name = "ICE Alcohol", Percentage = 75m,
            UnitValue = 10.41m, SpecificUnit = "USD por litro de alcohol puro", CalculationType = SriTaxCalculationType.Mixed,
            NormativeSourceId = SriNormativeSourceConfiguration.IceSpecific2026Id, IsActive = false });
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();
        (await db.SriIceRates.SingleAsync(x => x.Code == "3033")).IsActive.Should().BeFalse();
    }
}

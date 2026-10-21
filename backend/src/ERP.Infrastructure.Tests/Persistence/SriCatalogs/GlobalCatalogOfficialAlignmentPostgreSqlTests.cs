using ERP.Application.Common;
using ERP.Domain.Modules.SriCatalogs.Entities;
using ERP.Domain.Modules.SriCatalogs.Enums;
using ERP.Infrastructure.Persistence;
using ERP.Infrastructure.Persistence.Repositories;
using ERP.Infrastructure.Persistence.Repositories.SriCatalogs;
using ERP.Infrastructure.Seeding.InstallData;
using ERP.Infrastructure.Services;
using FluentAssertions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Testcontainers.PostgreSql;

namespace ERP.Infrastructure.Tests.Persistence.SriCatalogs;

[Trait("Category", "PostgreSql")]
public sealed class GlobalCatalogOfficialAlignmentPostgreSqlTests
{
    private const string GeoStateSql = """
        SELECT 'p|' || id || '|' || country_id || '|' || name || '|' || is_active AS "Value" FROM global.geo_provinces
        UNION ALL SELECT 'c|' || id || '|' || province_id || '|' || name || '|' || is_active FROM global.geo_cantons
        UNION ALL SELECT 'q|' || id || '|' || canton_id || '|' || name || '|' || is_active FROM global.geo_parishes
        ORDER BY 1
        """;

    [Fact]
    public async Task Fresh_install_loads_only_the_inec_cge_2026_geography_once()
    {
        // Official installation: the single baseline, then the InstallData bootstrap (one INEC CGE 2026 script).
        await using var freshDb = await StartAsync("global_alignment_fresh_test");
        await using var fresh = CreateContext(freshDb);
        await fresh.Database.MigrateAsync();
        var installer = new InstallDataBootstrapService(fresh, Microsoft.Extensions.Options.Options.Create(new InstallDataOptions { Enabled = true }),
            NullLogger<InstallDataBootstrapService>.Instance);
        await installer.ApplyPendingAsync();
        var afterFirstRun = await Rows(fresh, GeoStateSql);
        await installer.ApplyPendingAsync();
        (await Rows(fresh, GeoStateSql)).Should().Equal(afterFirstRun, "cada script se aplica una sola vez por checksum");
        (await Rows(fresh, "SELECT script_name AS \"Value\" FROM install_data_scripts"))
            .Should().Equal("ERP.Infrastructure.Seeding.InstallData.001_inec_cge_2026_geography.sql");

        (await Count(fresh, "SELECT count(*)::integer AS \"Value\" FROM global.geo_provinces")).Should().Be(25);
        (await Count(fresh, "SELECT count(*)::integer AS \"Value\" FROM global.geo_cantons")).Should().Be(224);
        (await Count(fresh, "SELECT count(*)::integer AS \"Value\" FROM global.geo_parishes")).Should().Be(1048);
        afterFirstRun.Should().OnlyContain(x => x.EndsWith("|true", StringComparison.Ordinal), "una instalación nueva no trae códigos retirados");
        // Codes recodified by the CGE 2026 exist only under their 2026 code (La Concordia 0808 -> 2302).
        (await Count(fresh, "SELECT count(*)::integer AS \"Value\" FROM global.geo_cantons WHERE id IN ('0808', '9001', '9003', '9004')")).Should().Be(0);
        (await Count(fresh, "SELECT count(*)::integer AS \"Value\" FROM global.geo_parishes WHERE id IN ('011551', '140157', '900151')")).Should().Be(0);

        var geography = new GeographyReadRepository(fresh);
        (await geography.GetCantonsByProvinceAsync("23")).Should().Contain(x => x.Id == "2302" && x.IsActive);
        (await geography.GetProvincesByCountryAsync("EC")).Should().HaveCount(25)
            .And.Contain(x => x.Id == "90" && x.Name == "ZONA EN ESTUDIO")
            .And.Contain(x => x.Id == "02" && x.Name == "BOLÍVAR");
        (await Rows(fresh, "SELECT country_id || '#' AS \"Value\" FROM global.geo_provinces WHERE id = '01'"))
            .Should().Equal("EC#");
        // The country row belongs to the SRI catalog seed; the geography script does not rewrite it.
        (await fresh.SriCountries.AsNoTracking().SingleAsync(x => x.Code == "ECU")).Name.Should().Be("ECUADOR");
    }

    [Fact]
    public async Task Catalog_corrections_reach_the_operational_readers()
    {
        await using var postgres = await StartAsync("global_alignment_readers_test");
        await using var db = CreateContext(postgres);
        await db.Database.MigrateAsync();

        var usage = new IdentificationUsageValidator(db);
        (await usage.IsAllowedAsync("09", IdentificationUsageType.Customer)).Should().BeFalse();
        (await usage.IsAllowedAsync("09", IdentificationUsageType.Carrier)).Should().BeFalse();
        (await usage.IsAllowedAsync("05", IdentificationUsageType.Customer)).Should().BeTrue();

        var lookup = new SriCatalogLookupRepository(db);
        (await lookup.GetActiveTaxRegimesAsync()).Select(x => x.Name)
            .Should().BeEquivalentTo("Régimen General", "RIMPE – Emprendedor", "RIMPE – Negocio Popular");
        (await lookup.GetActiveRetentionCodesAsync(null)).Should().NotContain(x => x.TaxType == "ISD");
        (await db.SriErrorCodes.AsNoTracking().CountAsync(x => x.Code == "2" || x.Code == "10" || x.Code == "26")).Should().Be(3);
        (await db.SriDocTypes.AsNoTracking().SingleAsync(x => x.Code == "08")).Name
            .Should().Be("Boletos o entradas a espectáculos públicos");
        (await db.SriCountries.AsNoTracking().SingleAsync(x => x.Code == "ECU")).SriCode.Should().Be("593");
        (await db.SriDocTypes.AsNoTracking().SingleAsync(x => x.Code == "02")).IsElectronic.Should().BeFalse();
        (await db.Set<SriNormativeSource>().AsNoTracking().Select(x => x.IntroducedInMigration).Distinct().ToListAsync())
            .Should().Equal("InitialProductionBaseline");
        (await Count(db, "SELECT count(*)::integer AS \"Value\" FROM global.sri_ice_rate WHERE is_active")).Should().Be(38);
    }

    private static async Task<List<string>> Rows(ErpDbContext db, string sql) =>
        await db.Database.SqlQueryRaw<string>(sql).ToListAsync();

    private static async Task<int> Count(ErpDbContext db, string sql) =>
        await db.Database.SqlQueryRaw<int>(sql).SingleAsync();

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
}

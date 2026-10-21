using ERP.Domain.Modules.SriCatalogs.Entities;
using ERP.Infrastructure.Persistence.Repositories.SriCatalogs;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace ERP.Infrastructure.Tests.Persistence.SriCatalogs;

[Trait("Category", "PostgreSql")]
public sealed class SriTaxSupportPostgreSqlTests
{
    [Fact]
    public async Task Fresh_install_matches_official_selectable_catalog()
    {
        var fixture = new SriRetentionCatalogDatabaseFixture();
        try
        {
            await fixture.InitializeAsync();
            await using var db = fixture.CreateContext();
            (await new SriCatalogLookupRepository(db).GetActiveTaxSupportCodesAsync())
                .ToDictionary(x => x.Code, x => x.Name).Should().BeEquivalentTo(SriTaxSupportSeedTests.Official);
            var all = await db.SriTaxSupports.AsNoTracking().ToDictionaryAsync(x => x.Code);
            all.Keys.Should().BeEquivalentTo(Enumerable.Range(0, 21).Select(x => x.ToString("D2")));
            all["00"].IsActive.Should().BeFalse();
            foreach (var code in Enumerable.Range(16, 5).Select(x => x.ToString("D2")))
                all[code].IsActive.Should().BeFalse();

            // An EF insert must persist false despite DEFAULT true (ADR-042).
            db.SriTaxSupports.Add(new SriTaxSupport { Code = "T9", Name = "inactive probe", IsActive = false });
            await db.SaveChangesAsync();
            db.ChangeTracker.Clear();
            (await db.SriTaxSupports.SingleAsync(x => x.Code == "T9")).IsActive.Should().BeFalse();
        }
        finally { await fixture.DisposeAsync(); }
    }
}

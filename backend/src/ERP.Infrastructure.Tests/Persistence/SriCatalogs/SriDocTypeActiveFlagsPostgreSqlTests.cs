using ERP.Domain.Modules.SriCatalogs.Entities;
using ERP.Infrastructure.Persistence.Configurations.SriCatalogs;
using ERP.Infrastructure.Persistence.Repositories.SriCatalogs;
using ERP.Infrastructure.Persistence.Services;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace ERP.Infrastructure.Tests.Persistence.SriCatalogs;

/// <summary>
/// Catálogo <c>global.sri_doc_type</c> en PostgreSQL real con todas las migraciones: los flags
/// declarados en <see cref="SriDocTypeConfiguration"/> son los que quedan en BD. Antes el default de
/// BD (true) hacía que EF omitiera los false del seed y 02/08/09/18 terminaban activos.
/// </summary>
[Trait("Category", "PostgreSql")]
public sealed class SriDocTypeActiveFlagsPostgreSqlTests : IClassFixture<SriRetentionCatalogDatabaseFixture>
{
    private readonly SriRetentionCatalogDatabaseFixture _db;

    public SriDocTypeActiveFlagsPostgreSqlTests(SriRetentionCatalogDatabaseFixture db) => _db = db;

    [Fact]
    public async Task Los_flags_persistidos_coinciden_con_el_seed_declarado()
    {
        var declared = DeclaredSeed();
        await using var db = _db.CreateContext();

        var stored = await db.SriDocTypes.AsNoTracking().ToDictionaryAsync(d => d.Code);

        stored.Keys.Should().Contain(declared.Keys);
        foreach (var (code, seed) in declared)
        {
            stored[code].IsActive.Should().Be(seed.IsActive, $"is_active de {code} es el declarado");
            stored[code].IsElectronic.Should().Be(seed.IsElectronic, $"is_electronic de {code} es el declarado");
        }
    }

    [Theory]
    [InlineData("02")]
    [InlineData("08")]
    [InlineData("09")]
    [InlineData("18")]
    public async Task Un_tipo_declarado_inactivo_permanece_inactivo_para_los_consumidores(string code)
    {
        await using var db = _db.CreateContext();

        (await db.SriDocTypes.AsNoTracking().SingleAsync(d => d.Code == code)).IsActive.Should().BeFalse();
        (await new SriCatalogLookupRepository(db).GetActiveDocTypesAsync())
            .Should().NotContain(d => d.Code == code);
        (await new SriDocTypeCatalogResolver(db).IsActiveElectronicDocTypeAsync(code)).Should().BeFalse();
    }

    [Fact]
    public async Task Un_tipo_activo_sigue_activo()
    {
        await using var db = _db.CreateContext();

        (await new SriCatalogLookupRepository(db).GetActiveDocTypesAsync())
            .Select(d => d.Code).Should().Contain(["01", "03", "04", "05", "06", "07"]);
        (await new SriDocTypeCatalogResolver(db).IsActiveElectronicDocTypeAsync("01")).Should().BeTrue();
    }

    [Fact]
    public async Task Insertar_un_tipo_inactivo_por_EF_persiste_false()
    {
        // Regresión de la causa: con ValueGeneratedNever, EF envía el false explícito pese al DEFAULT true.
        await using (var db = _db.CreateContext())
        {
            db.SriDocTypes.Add(new SriDocType
            {
                Code = "T9", Name = "Prueba inactiva", ShortName = "T9", IsActive = false, IsElectronic = false,
            });
            await db.SaveChangesAsync();
        }

        await using var check = _db.CreateContext();
        var stored = await check.SriDocTypes.AsNoTracking().SingleAsync(d => d.Code == "T9");
        stored.IsActive.Should().BeFalse();
        stored.IsElectronic.Should().BeFalse();
    }

    private static Dictionary<string, (bool IsActive, bool IsElectronic)> DeclaredSeed()
    {
        var modelBuilder = new ModelBuilder();
        modelBuilder.ApplyConfiguration(new SriDocTypeConfiguration());
        return modelBuilder.Model.FindEntityType(typeof(SriDocType))!.GetSeedData().ToDictionary(
            s => (string)s[nameof(SriDocType.Code)]!,
            s => ((bool)s[nameof(SriDocType.IsActive)]!, (bool)s[nameof(SriDocType.IsElectronic)]!));
    }
}

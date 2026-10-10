using ERP.Domain.Modules.SriCatalogs.Entities;
using ERP.Infrastructure.Persistence.Configurations.SriCatalogs;
using ERP.Infrastructure.Persistence.Repositories.SriCatalogs;
using ERP.Infrastructure.Persistence.Services;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace ERP.Infrastructure.Tests.Persistence.SriCatalogs;

/// <summary>
/// SRI-VAT-SEED-ACTIVE-FLAGS — catálogo <c>global.sri_vat_rate</c> en PostgreSQL real con todas las
/// migraciones: los flags y tarifas declarados en <see cref="SriVatRateConfiguration"/> son los que
/// quedan en BD. Antes el DEFAULT true hacía que EF omitiera el false del seed y los códigos
/// históricos 2 (12%) y 3 (14%) terminaban activos.
/// </summary>
[Trait("Category", "PostgreSql")]
public sealed class SriVatRateActiveFlagsPostgreSqlTests : IClassFixture<SriRetentionCatalogDatabaseFixture>
{
    private readonly SriRetentionCatalogDatabaseFixture _db;

    public SriVatRateActiveFlagsPostgreSqlTests(SriRetentionCatalogDatabaseFixture db) => _db = db;

    [Fact]
    public async Task Flags_y_tarifas_persistidos_coinciden_con_el_seed_declarado()
    {
        var declared = DeclaredSeed();
        await using var db = _db.CreateContext();

        var stored = await db.SriVatRates.AsNoTracking().ToDictionaryAsync(v => v.Code);

        stored.Keys.Should().Contain(declared.Keys);
        foreach (var (code, seed) in declared)
        {
            stored[code].IsActive.Should().Be(seed.IsActive, $"is_active de {code} es el declarado");
            stored[code].Percentage.Should().Be(seed.Percentage, $"la tarifa de {code} no cambia");
        }
    }

    [Theory]
    [InlineData("2")]
    [InlineData("3")]
    public async Task Un_codigo_declarado_inactivo_permanece_inactivo_para_los_consumidores(string code)
    {
        await using var db = _db.CreateContext();

        (await db.SriVatRates.AsNoTracking().SingleAsync(v => v.Code == code)).IsActive.Should().BeFalse();
        (await new SriTaxResolver(db).GetVatRateAsync(code)).Should().BeNull();
        // Fecha dentro de la vigencia histórica de 2 y 3: solo el flag los excluye.
        (await new SriCatalogLookupRepository(db).GetActiveVatRatesAsync(new DateOnly(2016, 6, 1)))
            .Should().NotContain(v => v.Code == code);
    }

    [Theory]
    [InlineData("0", 0.00)]
    [InlineData("4", 15.00)]
    [InlineData("5", 5.00)]
    [InlineData("6", 0.00)]
    [InlineData("7", 0.00)]
    [InlineData("8", 8.00)]
    [InlineData("10", 13.00)]
    public async Task Las_tarifas_activas_correctas_no_se_alteran(string code, decimal percentage)
    {
        await using var db = _db.CreateContext();

        var stored = await db.SriVatRates.AsNoTracking().SingleAsync(v => v.Code == code);
        stored.IsActive.Should().BeTrue();
        stored.Percentage.Should().Be(percentage);
        (await new SriTaxResolver(db).GetVatRateAsync(code)).Should().Be(percentage);
    }

    [Fact]
    public async Task Insertar_una_tarifa_inactiva_por_EF_persiste_false()
    {
        // Regresión de la causa: con ValueGeneratedNever, EF envía el false explícito pese al DEFAULT true.
        await using (var db = _db.CreateContext())
        {
            db.SriVatRates.Add(new SriVatRate { Code = "T9", Name = "Prueba inactiva", Percentage = 1.00m, IsActive = false });
            await db.SaveChangesAsync();
        }

        await using var check = _db.CreateContext();
        (await check.SriVatRates.AsNoTracking().SingleAsync(v => v.Code == "T9")).IsActive.Should().BeFalse();
    }

    private static Dictionary<string, (bool IsActive, decimal Percentage)> DeclaredSeed()
    {
        var modelBuilder = new ModelBuilder();
        modelBuilder.ApplyConfiguration(new SriVatRateConfiguration());
        return modelBuilder.Model.FindEntityType(typeof(SriVatRate))!.GetSeedData().ToDictionary(
            s => (string)s[nameof(SriVatRate.Code)]!,
            s => ((bool)s[nameof(SriVatRate.IsActive)]!, (decimal)s[nameof(SriVatRate.Percentage)]!));
    }
}

using ERP.Domain.Common;
using ERP.Domain.Modules.SriCatalogs.Entities;
using ERP.Infrastructure.Persistence;
using ERP.Infrastructure.Persistence.Configurations.SriCatalogs;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace ERP.Infrastructure.Tests.Persistence.SriCatalogs;

/// <summary>
/// ZH-SRI-RETENTION-CATALOG-SSOT-01 — primer slice real de <c>SriCatalogComplianceTests</c> (ADR-037 D11).
/// Verifica el catálogo global de retenciones a nivel de modelo/seed (sin base de datos): globalidad,
/// vigencias sin solape, unicidad de la regla vigente, mappings de la Ficha Técnica v2.34 Tabla 20,
/// emitibilidad, el caso 728 y la trazabilidad normativa. El comportamiento del resolver sobre la base
/// migrada vive en <see cref="SriRetentionCatalogResolutionIntegrationTests"/>.
/// </summary>
public sealed class SriCatalogComplianceTests
{
    private const string IvaTaxType = "IVA";

    // ── 1. Catálogos globales: sin TenantId/CompanyId, esquema global ────────────────────────────────

    [Fact]
    public void Ninguna_entidad_de_catalogo_SRI_es_de_tenant_ni_de_empresa()
    {
        var catalogTypes = typeof(SriRetentionCode)
            .Assembly.GetTypes()
            .Where(t => t is { IsClass: true, IsAbstract: false } && t.Namespace == typeof(SriRetentionCode).Namespace)
            .ToList();

        catalogTypes.Should().Contain([typeof(SriRetentionCode), typeof(SriRetentionCodeVersion), typeof(SriNormativeSource)]);
        foreach (var type in catalogTypes)
        {
            typeof(ITenantScopedEntity).IsAssignableFrom(type).Should().BeFalse($"{type.Name} es un catálogo global");
            typeof(ICompanyScopedEntity).IsAssignableFrom(type).Should().BeFalse($"{type.Name} es un catálogo global");
            typeof(ICompanyOperationalEntity).IsAssignableFrom(type).Should().BeFalse($"{type.Name} es un catálogo global");
            type.GetProperty("TenantId").Should().BeNull($"{type.Name} no debe tener TenantId");
            type.GetProperty("CompanyId").Should().BeNull($"{type.Name} no debe tener CompanyId");
        }
    }

    [Fact]
    public void Catalogo_de_retenciones_vive_en_esquema_global_sin_query_filters()
    {
        var model = BuildRetentionModel();
        foreach (var clr in new[] { typeof(SriRetentionCode), typeof(SriRetentionCodeVersion), typeof(SriNormativeSource) })
        {
            var entity = model.FindEntityType(clr)!;
            entity.GetSchema().Should().Be("global");
            entity.GetDeclaredQueryFilters().Should().BeEmpty("ADR-037 D13: nunca HasQueryFilter por habilitación");
        }
    }

    // ── 2-3. Vigencias: sin solape y exactamente una regla vigente ───────────────────────────────────

    [Fact]
    public void Versiones_del_mismo_concepto_no_se_solapan()
    {
        foreach (var group in Versions().GroupBy(v => v.RetentionCodeId))
        {
            var ordered = group.OrderBy(v => v.ValidFrom ?? DateOnly.MinValue).ToList();
            for (var i = 1; i < ordered.Count; i++)
            {
                var previousEnd = ordered[i - 1].ValidUntil ?? DateOnly.MaxValue;
                var currentStart = ordered[i].ValidFrom ?? DateOnly.MinValue;
                currentStart.Should().BeAfter(previousEnd, $"el concepto {group.Key} tiene vigencias solapadas");
            }
            group.Count(v => v.ValidUntil is null).Should().BeLessThanOrEqualTo(1);
        }
    }

    [Fact]
    public void Cada_concepto_versionado_tiene_exactamente_una_regla_vigente_en_cada_fecha_frontera()
    {
        var versions = Versions();
        var probeDates = versions
            .SelectMany(v => new[] { v.ValidFrom, v.ValidUntil })
            .OfType<DateOnly>()
            .Append(new DateOnly(2026, 10, 2))
            .Distinct()
            .ToList();

        foreach (var group in versions.GroupBy(v => v.RetentionCodeId))
        foreach (var date in probeDates)
        {
            group.Count(v => IsValidOn(v, date))
                .Should()
                .Be(1, $"el concepto {group.Key} debe tener exactamente una versión vigente el {date:yyyy-MM-dd}");
        }
    }

    // ── 4. Mappings IVA de la Ficha Técnica v2.34 Tabla 20 completos ─────────────────────────────────

    [Theory]
    [InlineData("721", 10, "9")]
    [InlineData("723", 20, "10")]
    [InlineData("725", 30, "1")]
    [InlineData("IVA-50", 50, "11")]
    [InlineData("726", 70, "2")]
    [InlineData("727", 100, "3")]
    [InlineData("IVA-0", 0, "7")]
    [InlineData("IVA-NP", 0, "8")]
    public void Tabla20_IVA_esta_completa_con_porcentaje_y_codigo_oficial(string businessCode, int percentage, string xmlCode)
    {
        var concept = Concepts().Single(c => c.TaxType == IvaTaxType && c.Code == businessCode);
        concept.Percentage.Should().Be(percentage);

        var version = Versions().Single(v => v.RetentionCodeId == concept.Id);
        version.XmlCode.Should().Be(xmlCode);
        version.Percentage.Should().Be(percentage);
        version.NormativeSourceId.Should().Be(SriNormativeSourceConfiguration.FichaV234Table20Id);
    }

    [Fact]
    public void Identidad_interna_y_clave_de_negocio_son_independientes_del_codigo_XML()
    {
        // ADR-037 D5: ningún concepto IVA usa su codigoRetencion oficial como clave de negocio.
        var ivaIds = Concepts().Where(c => c.TaxType == IvaTaxType).ToDictionary(c => c.Id, c => c.Code);
        foreach (var version in Versions().Where(v => ivaIds.ContainsKey(v.RetentionCodeId) && v.XmlCode is not null))
            ivaIds[version.RetentionCodeId].Should().NotBe(version.XmlCode);

        Concepts().Should().NotContain(c => c.Code == "729" || c.Code == "730", "no se inventan códigos con forma oficial");
    }

    // ── 5-6. Emitibilidad y caso 728 ─────────────────────────────────────────────────────────────────

    [Fact]
    public void Todo_concepto_IVA_o_Renta_es_emitible_salvo_la_excepcion_documentada_728()
    {
        var nonEmittableAllowList = new HashSet<string> { "IVA:728" };
        var versions = Versions();
        foreach (var concept in Concepts().Where(c => c.TaxType is "IVA" or "RENTA"))
        {
            var version = versions.Single(v => v.RetentionCodeId == concept.Id && IsValidOn(v, new DateOnly(2026, 10, 2)));
            if (nonEmittableAllowList.Contains($"{concept.TaxType}:{concept.Code}"))
                continue;
            version.XmlCode.Should().NotBeNullOrWhiteSpace($"{concept.TaxType} {concept.Code} debe ser emitible");
            version.XmlCode!.Length.Should().BeLessThanOrEqualTo(SriRetentionCodeVersion.XmlCodeMaxLen, "XSD: codigoRetencion máx. 5");
        }
    }

    [Fact]
    public void Concepto_728_IVA_15_se_conserva_pero_no_es_emitible()
    {
        var concept = Concepts().Single(c => c.TaxType == IvaTaxType && c.Code == "728");
        concept.Percentage.Should().Be(15m);
        concept.IsActive.Should().BeFalse("sin representación XML oficial no se habilita para operaciones nuevas");
        concept.Id.Should().Be(Guid.Parse("10000000-0000-0000-0000-000000000006"), "se conserva la identidad histórica");

        var version = Versions().Single(v => v.RetentionCodeId == concept.Id);
        version.XmlCode.Should().BeNull("la Tabla 20 no define retención de IVA del 15%");
        version.AtsCode.Should().BeNull();
    }

    [Fact]
    public void Retencion_en_cero_y_no_procede_existen_pero_no_son_seleccionables()
    {
        // RetentionDocumentLine exige tasa y valor > 0: ofrecerlos en selectores generaría opciones inválidas.
        Concepts().Single(c => c.Code == "IVA-0").IsActive.Should().BeFalse();
        Concepts().Single(c => c.Code == "IVA-NP").IsActive.Should().BeFalse();
        Concepts().Single(c => c.Code == "IVA-50").IsActive.Should().BeTrue();
    }

    [Fact]
    public void Renta_conserva_su_codigo_y_no_fija_porcentaje_no_verificado()
    {
        var incomeIds = Concepts().Where(c => c.TaxType == "RENTA").ToDictionary(c => c.Id, c => c.Code);
        var incomeVersions = Versions().Where(v => incomeIds.ContainsKey(v.RetentionCodeId)).ToList();

        incomeVersions.Should().HaveCount(incomeIds.Count);
        foreach (var v in incomeVersions)
        {
            v.XmlCode.Should().Be(incomeIds[v.RetentionCodeId], "comportamiento previo del XML preservado");
            v.Percentage.Should().BeNull("porcentaje de Renta no verificado (ADR-037 DR-4)");
            v.NormativeSourceId.Should().Be(SriNormativeSourceConfiguration.AtsIncomeRetentionCatalogId);
        }
    }

    // ── 3. Ningún histórico se elimina ni se modifica ────────────────────────────────────────────────

    [Fact]
    public void Los_20_conceptos_originales_se_conservan_con_su_identidad_codigo_y_porcentaje()
    {
        var original = new (string Id, string TaxType, string Code, decimal Pct)[]
        {
            ("10000000-0000-0000-0000-000000000001", "IVA", "721", 10m), ("10000000-0000-0000-0000-000000000002", "IVA", "723", 20m),
            ("10000000-0000-0000-0000-000000000003", "IVA", "725", 30m), ("10000000-0000-0000-0000-000000000004", "IVA", "726", 70m),
            ("10000000-0000-0000-0000-000000000005", "IVA", "727", 100m), ("10000000-0000-0000-0000-000000000006", "IVA", "728", 15m),
            ("20000000-0000-0000-0000-000000000001", "RENTA", "303", 10m), ("20000000-0000-0000-0000-000000000002", "RENTA", "304", 2m),
            ("20000000-0000-0000-0000-000000000003", "RENTA", "307", 1.75m), ("20000000-0000-0000-0000-000000000004", "RENTA", "309", 8m),
            ("20000000-0000-0000-0000-000000000005", "RENTA", "310", 1m), ("20000000-0000-0000-0000-000000000006", "RENTA", "312", 1m),
            ("20000000-0000-0000-0000-000000000007", "RENTA", "320", 2.75m), ("20000000-0000-0000-0000-000000000008", "RENTA", "325", 1.75m),
            ("20000000-0000-0000-0000-000000000009", "RENTA", "327", 1.75m), ("20000000-0000-0000-0000-000000000010", "RENTA", "341", 2m),
            ("20000000-0000-0000-0000-000000000011", "RENTA", "342", 1m), ("20000000-0000-0000-0000-000000000012", "RENTA", "343", 1.75m),
            ("20000000-0000-0000-0000-000000000013", "RENTA", "344", 2.75m), ("30000000-0000-0000-0000-000000000001", "ISD", "4580", 5m),
        };
        var concepts = Concepts().ToDictionary(c => c.Id);

        foreach (var (id, taxType, code, pct) in original)
        {
            var concept = concepts[Guid.Parse(id)];
            (concept.TaxType, concept.Code, concept.Percentage).Should().Be((taxType, code, pct));
            concept.IsActive.Should().Be(code != "728", "solo 728 cambia su habilitación operativa");
        }
    }

    [Fact]
    public void La_migracion_del_slice_solo_agrega_datos_globales_y_solo_deshabilita_728()
    {
        var migrationType = typeof(ErpDbContext).Assembly.GetTypes().Single(t =>
            t.GetCustomAttributes(typeof(MigrationAttribute), false).OfType<MigrationAttribute>()
                .Any(a => a.Id.EndsWith("_" + SriNormativeSourceConfiguration.IntroducedInMigration, StringComparison.Ordinal)));
        var migration = (Migration)Activator.CreateInstance(migrationType)!;
        migration.ActiveProvider = "Npgsql.EntityFrameworkCore.PostgreSQL";

        var operations = migration.UpOperations;

        operations.OfType<Microsoft.EntityFrameworkCore.Migrations.Operations.DeleteDataOperation>().Should().BeEmpty();
        operations.OfType<Microsoft.EntityFrameworkCore.Migrations.Operations.DropTableOperation>().Should().BeEmpty();
        operations.OfType<Microsoft.EntityFrameworkCore.Migrations.Operations.AlterColumnOperation>().Should().BeEmpty();
        operations.OfType<Microsoft.EntityFrameworkCore.Migrations.Operations.ITableMigrationOperation>()
            .Select(o => o.Schema).Should().OnlyContain(schema => schema == "global", "no se tocan datos de tenant ni snapshots");

        var update = operations.OfType<Microsoft.EntityFrameworkCore.Migrations.Operations.UpdateDataOperation>().Should().ContainSingle().Subject;
        update.Table.Should().Be("sri_retention_code");
        update.KeyValues[0, 0].Should().Be(Guid.Parse("10000000-0000-0000-0000-000000000006"));
        update.Columns.Should().Equal("is_active");
        update.Values[0, 0].Should().Be(false);
    }

    // ── 13. Trazabilidad normativa ────────────────────────────────────────────────────────────────────

    [Fact]
    public void Toda_version_referencia_una_fuente_normativa_trazable_a_su_migracion()
    {
        var sources = Sources().ToDictionary(s => s.Id);
        foreach (var version in Versions())
            sources.Should().ContainKey(version.NormativeSourceId);

        var migrationIds = typeof(ErpDbContext)
            .Assembly.GetTypes()
            .Select(t => t.GetCustomAttributes(typeof(MigrationAttribute), false).OfType<MigrationAttribute>().FirstOrDefault())
            .OfType<MigrationAttribute>()
            .Select(a => a.Id)
            .ToList();

        foreach (var source in sources.Values)
        {
            source.Document.Should().NotBeNullOrWhiteSpace();
            migrationIds.Should().Contain(id => id.EndsWith("_" + source.IntroducedInMigration, StringComparison.Ordinal),
                $"la fuente {source.Id} debe nombrar una migración real");
        }

        var ficha = sources[SriNormativeSourceConfiguration.FichaV234Table20Id];
        ficha.Version.Should().Be("2.34");
        ficha.DocumentSha256.Should().Be("7333aebfbdf2cb3ba83f9fc67a7a7f0346ca59506480a260cc42f96dbdfc13c9");
        sources[SriNormativeSourceConfiguration.AtsIncomeRetentionCatalogId].Document.Should().NotBe("FICHA_TECNICA_OFFLINE",
            "la ficha remite al catálogo ATS para Renta: no se atribuye a la ficha");
    }

    // ── Inmutabilidad: manifiesto aprobado de versiones (ADR-037 D9) ─────────────────────────────────

    [Fact]
    public void Versiones_publicadas_coinciden_con_el_manifiesto_aprobado()
    {
        // Cambiar una fila existente (porcentaje, código, fuente, vigencia de inicio) es una violación de
        // D9: este manifiesto solo puede crecer o cerrar ValidUntil, siempre en un PR revisado.
        var actual = Versions()
            .OrderBy(v => v.Id)
            .Select(v => $"{v.Id}|{v.RetentionCodeId}|{v.ValidFrom}|{v.Percentage}|{v.XmlCode}|{v.NormativeSourceId}")
            .ToList();

        var ficha = SriNormativeSourceConfiguration.FichaV234Table20Id;
        var ats = SriNormativeSourceConfiguration.AtsIncomeRetentionCatalogId;
        string Iva(int n, decimal pct, string? xml) =>
            $"41000000-0000-0000-0000-{n:D12}|10000000-0000-0000-0000-{n:D12}||{pct}|{xml}|{ficha}";
        string Income(int n, string xml) =>
            $"42000000-0000-0000-0000-{n:D12}|20000000-0000-0000-0000-{n:D12}|||{xml}|{ats}";

        var expected = new List<string>
        {
            Iva(1, 10.00m, "9"), Iva(2, 20.00m, "10"), Iva(3, 30.00m, "1"), Iva(4, 70.00m, "2"),
            Iva(5, 100.00m, "3"), Iva(6, 15.00m, null), Iva(7, 50.00m, "11"), Iva(8, 0.00m, "7"), Iva(9, 0.00m, "8"),
            Income(1, "303"), Income(2, "304"), Income(3, "307"), Income(4, "309"), Income(5, "310"), Income(6, "312"),
            Income(7, "320"), Income(8, "325"), Income(9, "327"), Income(10, "341"), Income(11, "342"),
            Income(12, "343"), Income(13, "344"),
        };

        actual.Should().BeEquivalentTo(expected);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────────────────────────────

    private static bool IsValidOn(SriRetentionCodeVersion v, DateOnly date) =>
        (v.ValidFrom is null || v.ValidFrom <= date) && (v.ValidUntil is null || v.ValidUntil >= date);

    private static Microsoft.EntityFrameworkCore.Metadata.IMutableModel BuildRetentionModel()
    {
        var modelBuilder = new ModelBuilder();
        modelBuilder.ApplyConfiguration(new SriRetentionCodeConfiguration());
        modelBuilder.ApplyConfiguration(new SriNormativeSourceConfiguration());
        modelBuilder.ApplyConfiguration(new SriRetentionCodeVersionConfiguration());
        return modelBuilder.Model;
    }

    private static List<T> Seed<T>(Func<IDictionary<string, object?>, T> map) =>
        BuildRetentionModel().FindEntityType(typeof(T))!.GetSeedData().Select(map).ToList();

    private static List<SriRetentionCode> Concepts() =>
        Seed(d => new SriRetentionCode
        {
            Id = (Guid)d[nameof(SriRetentionCode.Id)]!,
            TaxType = (string)d[nameof(SriRetentionCode.TaxType)]!,
            Code = (string)d[nameof(SriRetentionCode.Code)]!,
            Name = (string)d[nameof(SriRetentionCode.Name)]!,
            Percentage = (decimal)d[nameof(SriRetentionCode.Percentage)]!,
            IsActive = (bool)d[nameof(SriRetentionCode.IsActive)]!,
        });

    private static List<SriRetentionCodeVersion> Versions() =>
        Seed(d => new SriRetentionCodeVersion
        {
            Id = (Guid)d[nameof(SriRetentionCodeVersion.Id)]!,
            RetentionCodeId = (Guid)d[nameof(SriRetentionCodeVersion.RetentionCodeId)]!,
            ValidFrom = (DateOnly?)d[nameof(SriRetentionCodeVersion.ValidFrom)],
            ValidUntil = (DateOnly?)d[nameof(SriRetentionCodeVersion.ValidUntil)],
            Percentage = (decimal?)d[nameof(SriRetentionCodeVersion.Percentage)],
            XmlCode = (string?)d[nameof(SriRetentionCodeVersion.XmlCode)],
            AtsCode = (string?)d[nameof(SriRetentionCodeVersion.AtsCode)],
            NormativeSourceId = (Guid)d[nameof(SriRetentionCodeVersion.NormativeSourceId)]!,
        });

    private static List<SriNormativeSource> Sources() =>
        Seed(d => new SriNormativeSource
        {
            Id = (Guid)d[nameof(SriNormativeSource.Id)]!,
            Document = (string)d[nameof(SriNormativeSource.Document)]!,
            Version = (string?)d[nameof(SriNormativeSource.Version)],
            DocumentSha256 = (string?)d[nameof(SriNormativeSource.DocumentSha256)],
            IntroducedInMigration = (string)d[nameof(SriNormativeSource.IntroducedInMigration)]!,
        });
}

using ERP.Domain.Common;
using ERP.Domain.Modules.SriCatalogs.Entities;
using ERP.Domain.Modules.SriCatalogs.Enums;
using ERP.Infrastructure.Persistence;
using ERP.Infrastructure.Persistence.Configurations.SriCatalogs;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
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
    private static readonly DateOnly LastLegacyIncomeDay = new(2026, 8, 5);
    private static readonly DateOnly AtsBlockStart = new(2026, 8, 6);
    private static readonly string[] RetiredIncomeCodes = ["341", "342", "344"];
    private static readonly string[] ConditionalIncomeCodes = ["310", "327"];

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
    public void Cada_concepto_versionado_tiene_a_lo_sumo_una_regla_vigente_y_exactamente_una_si_no_fue_retirado()
    {
        var versions = Versions();
        var retiredIds = Concepts().Where(c => RetiredIncomeCodes.Contains(c.Code)).Select(c => c.Id).ToHashSet();
        var probeDates = versions
            .SelectMany(v => new[] { v.ValidFrom, v.ValidUntil, v.ValidUntil?.AddDays(1) })
            .OfType<DateOnly>()
            .Append(new DateOnly(2026, 10, 2))
            .Distinct()
            .ToList();

        foreach (var group in versions.GroupBy(v => v.RetentionCodeId))
            foreach (var date in probeDates)
            {
                var count = group.Count(v => IsValidOn(v, date));
                count.Should().BeLessThanOrEqualTo(1, $"el concepto {group.Key} no puede tener reglas ambiguas el {date:yyyy-MM-dd}");
                if (!retiredIds.Contains(group.Key))
                    count.Should().Be(1, $"el concepto {group.Key} debe tener exactamente una versión vigente el {date:yyyy-MM-dd}");
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
    public void Todo_concepto_IVA_o_Renta_vigente_es_emitible_salvo_la_excepcion_documentada_728()
    {
        var nonEmittableAllowList = new HashSet<string> { "IVA:728" };
        var versions = Versions();
        var today = new DateOnly(2026, 10, 2);
        foreach (var concept in Concepts().Where(c => c.TaxType is "IVA" or "RENTA"))
        {
            var version = versions.SingleOrDefault(v => v.RetentionCodeId == concept.Id && IsValidOn(v, today));
            if (version is null)
            {
                RetiredIncomeCodes.Should().Contain(concept.Code, "solo los códigos retirados del ATS vigente quedan sin versión");
                continue;
            }
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
    public void Versiones_de_Renta_heredadas_conservan_su_codigo_sin_porcentaje_y_cierran_el_2026_08_05()
    {
        var incomeIds = Concepts().Where(c => c.TaxType == "RENTA").ToDictionary(c => c.Id, c => c.Code);
        var legacy = Versions().Where(v => v.NormativeSourceId == SriNormativeSourceConfiguration.AtsIncomeRetentionCatalogId).ToList();

        legacy.Should().HaveCount(incomeIds.Count);
        foreach (var v in legacy)
        {
            v.XmlCode.Should().Be(incomeIds[v.RetentionCodeId], "comportamiento previo del XML preservado para fechas anteriores");
            v.Percentage.Should().BeNull("porcentaje heredado no verificado: no se reescribe (ADR-037 D9)");
            v.RateKind.Should().Be(SriRetentionRateKind.Fixed);
            v.ValidFrom.Should().BeNull();
            v.ValidUntil.Should().Be(LastLegacyIncomeDay, "único cambio permitido: cerrar la vigencia");
        }
    }

    // ── 3. Ningún histórico se elimina ni se modifica ────────────────────────────────────────────────

    [Fact]
    public void Los_20_conceptos_originales_conservan_su_identidad_y_codigo()
    {
        var original = new (string Id, string TaxType, string Code)[]
        {
            ("10000000-0000-0000-0000-000000000001", "IVA", "721"), ("10000000-0000-0000-0000-000000000002", "IVA", "723"),
            ("10000000-0000-0000-0000-000000000003", "IVA", "725"), ("10000000-0000-0000-0000-000000000004", "IVA", "726"),
            ("10000000-0000-0000-0000-000000000005", "IVA", "727"), ("10000000-0000-0000-0000-000000000006", "IVA", "728"),
            ("20000000-0000-0000-0000-000000000001", "RENTA", "303"), ("20000000-0000-0000-0000-000000000002", "RENTA", "304"),
            ("20000000-0000-0000-0000-000000000003", "RENTA", "307"), ("20000000-0000-0000-0000-000000000004", "RENTA", "309"),
            ("20000000-0000-0000-0000-000000000005", "RENTA", "310"), ("20000000-0000-0000-0000-000000000006", "RENTA", "312"),
            ("20000000-0000-0000-0000-000000000007", "RENTA", "320"), ("20000000-0000-0000-0000-000000000008", "RENTA", "325"),
            ("20000000-0000-0000-0000-000000000009", "RENTA", "327"), ("20000000-0000-0000-0000-000000000010", "RENTA", "341"),
            ("20000000-0000-0000-0000-000000000011", "RENTA", "342"), ("20000000-0000-0000-0000-000000000012", "RENTA", "343"),
            ("20000000-0000-0000-0000-000000000013", "RENTA", "344"), ("30000000-0000-0000-0000-000000000001", "ISD", "4580"),
        };
        var concepts = Concepts().ToDictionary(c => c.Id);

        foreach (var (id, taxType, code) in original)
        {
            var concept = concepts[Guid.Parse(id)];
            (concept.TaxType, concept.Code).Should().Be((taxType, code), "nunca se renumera ni se reasigna una identidad");
        }

        var iva = Concepts().Where(c => c.TaxType == IvaTaxType && c.Code is "721" or "723" or "725" or "726" or "727").ToList();
        iva.Should().OnlyContain(c => c.IsActive, "los conceptos IVA corregidos en el slice anterior no cambian");
        concepts[Guid.Parse("30000000-0000-0000-0000-000000000001")].Percentage.Should().Be(5m);
    }

    [Fact]
    public void La_migracion_del_slice_solo_agrega_datos_globales_y_solo_deshabilita_728()
    {
        var operations = UpOperationsOf(SriNormativeSourceConfiguration.IntroducedInMigration);

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

    // ── ZH-SRI-RETENTION-INCOME-CATALOG-SSOT-01: Renta alineada con el Catálogo ATS 06/08/2026 ─────────

    [Theory]
    [InlineData("303", 10)]
    [InlineData("304", 10)]
    [InlineData("307", 3)]
    [InlineData("309", 3)]
    [InlineData("312", 2)]
    [InlineData("320", 10)]
    [InlineData("325", 25)]
    [InlineData("343", 1)]
    public void Renta_vigente_desde_2026_08_06_tiene_la_tasa_fija_exacta_del_ATS(string code, int officialRate)
    {
        var concept = Concepts().Single(c => c.TaxType == "RENTA" && c.Code == code);
        var current = Versions().Single(v => v.RetentionCodeId == concept.Id && IsValidOn(v, AtsBlockStart));
        var previous = Versions().Single(v => v.RetentionCodeId == concept.Id && IsValidOn(v, LastLegacyIncomeDay));

        current.RateKind.Should().Be(SriRetentionRateKind.Fixed);
        current.Percentage.Should().Be(officialRate);
        current.XmlCode.Should().Be(code);
        current.AtsCode.Should().Be(code, "el código ATS ahora sí está confirmado por la fuente oficial");
        current.NormativeSourceId.Should().Be(SriNormativeSourceConfiguration.AtsIncomeTable310From20260806Id);
        previous.Id.Should().NotBe(current.Id, "2026-08-05 resuelve la versión anterior y 2026-08-06 la nueva");
        concept.IsActive.Should().BeTrue();
        concept.Percentage.Should().Be(officialRate, "la tasa operativa del concepto sigue a la versión vigente");
    }

    [Theory]
    [InlineData("310", "1 /0 según resolución NAC-DGERCGC26-00000028")]
    [InlineData("327", "12 o 14")]
    public void Tasas_condicionales_nunca_se_convierten_en_tasa_fija(string code, string officialRule)
    {
        var concept = Concepts().Single(c => c.TaxType == "RENTA" && c.Code == code);
        var current = Versions().Single(v => v.RetentionCodeId == concept.Id && IsValidOn(v, AtsBlockStart));

        current.RateKind.Should().Be(SriRetentionRateKind.Conditional);
        current.Percentage.Should().BeNull("no se inventa un porcentaje");
        current.RateRuleText.Should().Be(officialRule);
        concept.IsActive.Should().BeFalse("el ERP no puede determinar la tasa: no se ofrece para operaciones nuevas");
    }

    [Fact]
    public void Codigos_341_342_344_quedan_retirados_sin_version_nueva_pero_conservados()
    {
        foreach (var code in RetiredIncomeCodes)
        {
            var concept = Concepts().Single(c => c.TaxType == "RENTA" && c.Code == code);
            concept.IsActive.Should().BeFalse($"{code} no existe en el Catálogo ATS vigente");
            var versions = Versions().Where(v => v.RetentionCodeId == concept.Id).ToList();
            versions.Should().ContainSingle("solo la versión heredada, cerrada").Which.ValidUntil.Should().Be(LastLegacyIncomeDay);
            versions.Should().NotContain(v => IsValidOn(v, AtsBlockStart));
        }
    }

    [Fact]
    public void Todo_concepto_habilitado_con_tasa_fija_vigente_tiene_la_misma_tasa_operativa()
    {
        // Guardia de la denormalización: SriRetentionCode.Percentage (tasa operativa para operaciones nuevas)
        // nunca diverge de la versión vigente.
        var today = new DateOnly(2026, 10, 2);
        var versions = Versions();
        foreach (var concept in Concepts().Where(c => c.IsActive))
        {
            var current = versions.SingleOrDefault(v => v.RetentionCodeId == concept.Id && IsValidOn(v, today));
            if (current is null || current.RateKind != SriRetentionRateKind.Fixed || current.Percentage is null)
                continue;
            concept.Percentage.Should().Be(current.Percentage!.Value, $"{concept.TaxType} {concept.Code}");
        }
        Concepts().Where(c => c.IsActive && c.TaxType == "RENTA").Should().OnlyContain(c => !ConditionalIncomeCodes.Contains(c.Code));
    }

    [Fact]
    public void La_fuente_ATS_2026_es_trazable_al_archivo_oficial()
    {
        var source = Sources().Single(s => s.Id == SriNormativeSourceConfiguration.AtsIncomeTable310From20260806Id);
        source.Document.Should().Be("CATALOGO_ATS");
        source.DocumentSha256.Should().Be("bd3f7834f2cd31187af39cd2f4c685a646d7e49316e92ee493da5f2dd9776e3e");
        source.IntroducedInMigration.Should().Be(SriNormativeSourceConfiguration.IncomeAtsIntroducedInMigration);
    }

    [Fact]
    public void La_migracion_ATS_no_reescribe_historia_ni_toca_datos_de_tenant_fuera_de_los_defaults_afectados()
    {
        var operations = UpOperationsOf(SriNormativeSourceConfiguration.IncomeAtsIntroducedInMigration);

        operations.OfType<Microsoft.EntityFrameworkCore.Migrations.Operations.DeleteDataOperation>().Should().BeEmpty();
        operations.OfType<Microsoft.EntityFrameworkCore.Migrations.Operations.DropTableOperation>().Should().BeEmpty();
        operations.OfType<Microsoft.EntityFrameworkCore.Migrations.Operations.AlterColumnOperation>().Should().BeEmpty();

        var allowedVersionColumns = new[] { "valid_until", "rate_kind", "rate_rule_text" };
        foreach (var update in operations.OfType<Microsoft.EntityFrameworkCore.Migrations.Operations.UpdateDataOperation>())
        {
            update.Schema.Should().Be("global");
            if (update.Table == "sri_retention_code_version")
                update.Columns.Should().OnlyContain(c => allowedVersionColumns.Contains(c), "una versión solo cierra vigencia o recibe las columnas nuevas");
            else
                update.Table.Should().Be("sri_retention_code");
        }

        var sql = operations.OfType<Microsoft.EntityFrameworkCore.Migrations.Operations.SqlOperation>().Should().ContainSingle().Subject.Sql;
        sql.Should().Contain("UPDATE master_supplier_retention_defaults").And.Contain("SET is_active = false");
        sql.Should().NotContain("20000000-0000-0000-0000-000000000001'", "303 conserva su significado");
        sql.Should().NotContain("20000000-0000-0000-0000-000000000006'", "312 conserva su significado");
        sql.Should().NotContainAny("retention_document", "DELETE");
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
        // Cambiar una fila existente (porcentaje, código, fuente, tipo o regla) es una violación de D9: este
        // manifiesto solo puede crecer o cerrar ValidUntil, siempre en un PR revisado.
        var actual = Versions()
            .OrderBy(v => v.Id)
            .Select(v => $"{v.Id}|{v.RetentionCodeId}|{v.ValidFrom:yyyy-MM-dd}|{v.ValidUntil:yyyy-MM-dd}|{v.RateKind}|{Pct(v.Percentage)}|{v.RateRuleText}|{v.XmlCode}|{v.NormativeSourceId}")
            .ToList();

        var ficha = SriNormativeSourceConfiguration.FichaV234Table20Id;
        var ats = SriNormativeSourceConfiguration.AtsIncomeRetentionCatalogId;
        var ats2026 = SriNormativeSourceConfiguration.AtsIncomeTable310From20260806Id;
        string Iva(int n, decimal pct, string? xml) =>
            $"41000000-0000-0000-0000-{n:D12}|10000000-0000-0000-0000-{n:D12}|||Fixed|{Pct(pct)}||{xml}|{ficha}";
        string Legacy(int n, string xml) =>
            $"42000000-0000-0000-0000-{n:D12}|20000000-0000-0000-0000-{n:D12}||2026-08-05|Fixed|||{xml}|{ats}";
        string Fixed(int n, decimal pct, string xml) =>
            $"43000000-0000-0000-0000-{n:D12}|20000000-0000-0000-0000-{n:D12}|2026-08-06||Fixed|{Pct(pct)}||{xml}|{ats2026}";
        string Conditional(int n, string rule, string xml) =>
            $"43000000-0000-0000-0000-{n:D12}|20000000-0000-0000-0000-{n:D12}|2026-08-06||Conditional||{rule}|{xml}|{ats2026}";

        var expected = new List<string>
        {
            Iva(1, 10m, "9"), Iva(2, 20m, "10"), Iva(3, 30m, "1"), Iva(4, 70m, "2"),
            Iva(5, 100m, "3"), Iva(6, 15m, null), Iva(7, 50m, "11"), Iva(8, 0m, "7"), Iva(9, 0m, "8"),
            Legacy(1, "303"), Legacy(2, "304"), Legacy(3, "307"), Legacy(4, "309"), Legacy(5, "310"), Legacy(6, "312"),
            Legacy(7, "320"), Legacy(8, "325"), Legacy(9, "327"), Legacy(10, "341"), Legacy(11, "342"),
            Legacy(12, "343"), Legacy(13, "344"),
            // Catálogo ATS oficial, Tabla 3.10 desde 06/08/2026 (Catalogo_ATS.xls SHA-256 bd3f7834…776e3e).
            Fixed(1, 10m, "303"), Fixed(2, 10m, "304"), Fixed(3, 3m, "307"), Fixed(4, 3m, "309"),
            Conditional(5, "1 /0 según resolución NAC-DGERCGC26-00000028", "310"), Fixed(6, 2m, "312"),
            Fixed(7, 10m, "320"), Fixed(8, 25m, "325"), Conditional(9, "12 o 14", "327"), Fixed(12, 1m, "343"),
        };

        actual.Should().BeEquivalentTo(expected);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────────────────────────────

    private static string Pct(decimal? value) =>
        value is null ? "" : value.Value.ToString("0.####", System.Globalization.CultureInfo.InvariantCulture);

    private static IReadOnlyList<Microsoft.EntityFrameworkCore.Migrations.Operations.MigrationOperation> UpOperationsOf(string migrationName)
    {
        var migrationType = typeof(ErpDbContext).Assembly.GetTypes().Single(t =>
            t.GetCustomAttributes(typeof(MigrationAttribute), false).OfType<MigrationAttribute>()
                .Any(a => a.Id.EndsWith("_" + migrationName, StringComparison.Ordinal)));
        var migration = (Migration)Activator.CreateInstance(migrationType)!;
        migration.ActiveProvider = "Npgsql.EntityFrameworkCore.PostgreSQL";
        return migration.UpOperations;
    }

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
            RateKind = (SriRetentionRateKind)d[nameof(SriRetentionCodeVersion.RateKind)]!,
            RateRuleText = (string?)d[nameof(SriRetentionCodeVersion.RateRuleText)],
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

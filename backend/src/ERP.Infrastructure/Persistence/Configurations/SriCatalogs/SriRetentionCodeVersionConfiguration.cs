using ERP.Domain.Modules.SriCatalogs.Entities;
using ERP.Domain.Modules.SriCatalogs.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ERP.Infrastructure.Persistence.Configurations.SriCatalogs;

/// <summary>
/// ZH-SRI-RETENTION-CATALOG-SSOT-01 (ADR-037 D4/D5/D9) — versiones de representación oficial del catálogo de
/// retenciones. Fuente única de los mappings concepto → <c>codigoRetencion</c>: nunca un switch/diccionario en
/// builders o providers. Reglas de evolución: solo altas y cierre de <c>ValidUntil</c>; jamás se modifica el
/// porcentaje, el código o la fuente de una versión existente (protegido por <c>SriCatalogComplianceTests</c>).
/// </summary>
public class SriRetentionCodeVersionConfiguration
    : IEntityTypeConfiguration<SriRetentionCodeVersion>
{
    public void Configure(EntityTypeBuilder<SriRetentionCodeVersion> builder)
    {
        builder.ToTable("sri_retention_code_version", schema: "global");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.RetentionCodeId).HasColumnName("retention_code_id").IsRequired();
        builder.Property(x => x.ValidFrom).HasColumnName("valid_from");
        builder.Property(x => x.ValidUntil).HasColumnName("valid_until");
        builder.Property(x => x.Percentage).HasColumnName("percentage").HasPrecision(7, 4);
        // Columna nueva con DEFAULT Fixed: las versiones existentes quedan Fixed sin reescribirse (ADR-037 D9).
        // ValueGeneratedNever: EF siempre escribe el valor real (Conditional incluido) en seeds e inserts.
        builder
            .Property(x => x.RateKind)
            .HasColumnName("rate_kind")
            .HasConversion<int>()
            .HasDefaultValue(SriRetentionRateKind.Fixed)
            .ValueGeneratedNever()
            .IsRequired();
        builder
            .Property(x => x.RateRuleText)
            .HasColumnName("rate_rule_text")
            .HasMaxLength(SriRetentionCodeVersion.RateRuleTextMaxLen);
        builder
            .Property(x => x.XmlCode)
            .HasColumnName("xml_code")
            .HasMaxLength(SriRetentionCodeVersion.XmlCodeMaxLen);
        builder
            .Property(x => x.AtsCode)
            .HasColumnName("ats_code")
            .HasMaxLength(SriRetentionCodeVersion.AtsCodeMaxLen);
        builder
            .Property(x => x.NormativeSourceId)
            .HasColumnName("normative_source_id")
            .IsRequired();

        builder
            .HasOne<SriRetentionCode>()
            .WithMany()
            .HasForeignKey(x => x.RetentionCodeId)
            .OnDelete(DeleteBehavior.Restrict);
        builder
            .HasOne<SriNormativeSource>()
            .WithMany()
            .HasForeignKey(x => x.NormativeSourceId)
            .OnDelete(DeleteBehavior.Restrict);

        // Dos versiones del mismo concepto no pueden empezar el mismo día (NULL = "sin límite" cuenta como
        // un valor). El no solapamiento completo lo garantizan el resolver (>1 vigente = error) y los
        // compliance tests (ADR-037: EXCLUDE con btree_gist queda como decisión diferida DR-5).
        builder
            .HasIndex(x => new { x.RetentionCodeId, x.ValidFrom })
            .IsUnique()
            .AreNullsDistinct(false)
            .HasDatabaseName("uq_sri_retention_code_version_start");

        builder.HasData(Seed());
    }

    private static readonly Guid Table20 = SriNormativeSourceConfiguration.FichaV234Table20Id;
    private static readonly Guid AtsIncome =
        SriNormativeSourceConfiguration.AtsIncomeRetentionCatalogId;
    private static readonly Guid AtsIncome20260806 =
        SriNormativeSourceConfiguration.AtsIncomeTable310From20260806Id;

    /// <summary>Último día de las versiones de Renta heredadas (no verificadas); el bloque ATS verificado rige desde el 06/08/2026.</summary>
    public static readonly DateOnly LegacyIncomeValidUntil = new(2026, 8, 5);

    public static readonly DateOnly AtsIncome20260806ValidFrom = new(2026, 8, 6);

    private static SriRetentionCodeVersion[] Seed() =>
        [
            // ── IVA — Ficha Técnica v2.34, Tabla 20 (confirmado contra el PDF oficial, página 33) ──────────
            Iva(
                "41000000-0000-0000-0000-000000000001",
                "10000000-0000-0000-0000-000000000001",
                10.00m,
                "9"
            ), // 721
            Iva(
                "41000000-0000-0000-0000-000000000002",
                "10000000-0000-0000-0000-000000000002",
                20.00m,
                "10"
            ), // 723
            Iva(
                "41000000-0000-0000-0000-000000000003",
                "10000000-0000-0000-0000-000000000003",
                30.00m,
                "1"
            ), // 725
            Iva(
                "41000000-0000-0000-0000-000000000004",
                "10000000-0000-0000-0000-000000000004",
                70.00m,
                "2"
            ), // 726
            Iva(
                "41000000-0000-0000-0000-000000000005",
                "10000000-0000-0000-0000-000000000005",
                100.00m,
                "3"
            ), // 727
            // 728 (15 %): la Tabla 20 no define una retención de IVA del 15 % → sin XmlCode: no emitible
            // (fail-closed) hasta que exista fuente oficial. El concepto se conserva (snapshots/defaults).
            Iva(
                "41000000-0000-0000-0000-000000000006",
                "10000000-0000-0000-0000-000000000006",
                15.00m,
                null
            ),
            Iva(
                "41000000-0000-0000-0000-000000000007",
                "10000000-0000-0000-0000-000000000007",
                50.00m,
                "11"
            ), // IVA-50
            Iva(
                "41000000-0000-0000-0000-000000000008",
                "10000000-0000-0000-0000-000000000008",
                0.00m,
                "7"
            ), // IVA-0
            Iva(
                "41000000-0000-0000-0000-000000000009",
                "10000000-0000-0000-0000-000000000009",
                0.00m,
                "8"
            ), // IVA-NP
            // ── RENTA — catálogo ATS (referido por la Ficha; NO verificado): mismo código que el concepto,
            // sin porcentaje exigible. Conserva exactamente el comportamiento previo del XML. ─────────────
            Income(
                "42000000-0000-0000-0000-000000000001",
                "20000000-0000-0000-0000-000000000001",
                "303",
                LegacyIncomeValidUntil
            ),
            Income(
                "42000000-0000-0000-0000-000000000002",
                "20000000-0000-0000-0000-000000000002",
                "304",
                LegacyIncomeValidUntil
            ),
            Income(
                "42000000-0000-0000-0000-000000000003",
                "20000000-0000-0000-0000-000000000003",
                "307",
                LegacyIncomeValidUntil
            ),
            Income(
                "42000000-0000-0000-0000-000000000004",
                "20000000-0000-0000-0000-000000000004",
                "309",
                LegacyIncomeValidUntil
            ),
            Income(
                "42000000-0000-0000-0000-000000000005",
                "20000000-0000-0000-0000-000000000005",
                "310",
                LegacyIncomeValidUntil
            ),
            Income(
                "42000000-0000-0000-0000-000000000006",
                "20000000-0000-0000-0000-000000000006",
                "312",
                LegacyIncomeValidUntil
            ),
            Income(
                "42000000-0000-0000-0000-000000000007",
                "20000000-0000-0000-0000-000000000007",
                "320",
                LegacyIncomeValidUntil
            ),
            Income(
                "42000000-0000-0000-0000-000000000008",
                "20000000-0000-0000-0000-000000000008",
                "325",
                LegacyIncomeValidUntil
            ),
            Income(
                "42000000-0000-0000-0000-000000000009",
                "20000000-0000-0000-0000-000000000009",
                "327",
                LegacyIncomeValidUntil
            ),
            Income(
                "42000000-0000-0000-0000-000000000010",
                "20000000-0000-0000-0000-000000000010",
                "341",
                LegacyIncomeValidUntil
            ),
            Income(
                "42000000-0000-0000-0000-000000000011",
                "20000000-0000-0000-0000-000000000011",
                "342",
                LegacyIncomeValidUntil
            ),
            Income(
                "42000000-0000-0000-0000-000000000012",
                "20000000-0000-0000-0000-000000000012",
                "343",
                LegacyIncomeValidUntil
            ),
            Income(
                "42000000-0000-0000-0000-000000000013",
                "20000000-0000-0000-0000-000000000013",
                "344",
                LegacyIncomeValidUntil
            ),
            // ── RENTA — Catálogo ATS oficial, Tabla 3.10 desde 06/08/2026 (verificado contra el XLS). ─────────
            // 341/342/344 no existen en el bloque vigente: sin versión nueva (no resolubles desde esa fecha).
            IncomeFixed(
                "43000000-0000-0000-0000-000000000001",
                "20000000-0000-0000-0000-000000000001",
                "303",
                10.00m
            ),
            IncomeFixed(
                "43000000-0000-0000-0000-000000000002",
                "20000000-0000-0000-0000-000000000002",
                "304",
                10.00m
            ),
            IncomeFixed(
                "43000000-0000-0000-0000-000000000003",
                "20000000-0000-0000-0000-000000000003",
                "307",
                3.00m
            ),
            IncomeFixed(
                "43000000-0000-0000-0000-000000000004",
                "20000000-0000-0000-0000-000000000004",
                "309",
                3.00m
            ),
            IncomeConditional(
                "43000000-0000-0000-0000-000000000005",
                "20000000-0000-0000-0000-000000000005",
                "310",
                "1 /0 según resolución NAC-DGERCGC26-00000028"
            ),
            IncomeFixed(
                "43000000-0000-0000-0000-000000000006",
                "20000000-0000-0000-0000-000000000006",
                "312",
                2.00m
            ),
            IncomeFixed(
                "43000000-0000-0000-0000-000000000007",
                "20000000-0000-0000-0000-000000000007",
                "320",
                10.00m
            ),
            IncomeFixed(
                "43000000-0000-0000-0000-000000000008",
                "20000000-0000-0000-0000-000000000008",
                "325",
                25.00m
            ),
            IncomeConditional(
                "43000000-0000-0000-0000-000000000009",
                "20000000-0000-0000-0000-000000000009",
                "327",
                "12 o 14"
            ),
            IncomeFixed(
                "43000000-0000-0000-0000-000000000012",
                "20000000-0000-0000-0000-000000000012",
                "343",
                1.00m
            ),
            // ISD (4580): sin versión — el comprobante de retención del ERP no emite ISD (RetentionTaxType
            // solo IVA/Renta); cualquier intento de resolverlo falla cerrado por "sin versión vigente".
        ];

    private static SriRetentionCodeVersion Iva(
        string id,
        string conceptId,
        decimal percentage,
        string? xmlCode
    ) =>
        new()
        {
            Id = Guid.Parse(id),
            RetentionCodeId = Guid.Parse(conceptId),
            ValidFrom = null,
            ValidUntil = null,
            Percentage = percentage,
            XmlCode = xmlCode,
            AtsCode = null,
            NormativeSourceId = Table20,
        };

    private static SriRetentionCodeVersion Income(
        string id,
        string conceptId,
        string xmlCode,
        DateOnly? validUntil
    ) =>
        new()
        {
            Id = Guid.Parse(id),
            RetentionCodeId = Guid.Parse(conceptId),
            ValidFrom = null,
            ValidUntil = validUntil,
            Percentage = null,
            XmlCode = xmlCode,
            AtsCode = null,
            NormativeSourceId = AtsIncome,
        };

    private static SriRetentionCodeVersion IncomeFixed(
        string id,
        string conceptId,
        string xmlCode,
        decimal percentage
    ) =>
        new()
        {
            Id = Guid.Parse(id),
            RetentionCodeId = Guid.Parse(conceptId),
            ValidFrom = AtsIncome20260806ValidFrom,
            ValidUntil = null,
            RateKind = SriRetentionRateKind.Fixed,
            Percentage = percentage,
            XmlCode = xmlCode,
            AtsCode = xmlCode,
            NormativeSourceId = AtsIncome20260806,
        };

    /// <summary>Tarifa no única en la fuente: sin porcentaje, con el texto literal de la regla (fail-closed al resolver).</summary>
    private static SriRetentionCodeVersion IncomeConditional(
        string id,
        string conceptId,
        string xmlCode,
        string rateRuleText
    ) =>
        new()
        {
            Id = Guid.Parse(id),
            RetentionCodeId = Guid.Parse(conceptId),
            ValidFrom = AtsIncome20260806ValidFrom,
            ValidUntil = null,
            RateKind = SriRetentionRateKind.Conditional,
            Percentage = null,
            RateRuleText = rateRuleText,
            XmlCode = xmlCode,
            AtsCode = xmlCode,
            NormativeSourceId = AtsIncome20260806,
        };
}

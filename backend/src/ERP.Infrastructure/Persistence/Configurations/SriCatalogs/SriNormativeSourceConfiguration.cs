using ERP.Domain.Modules.SriCatalogs.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ERP.Infrastructure.Persistence.Configurations.SriCatalogs;

/// <summary>
/// ZH-SRI-RETENTION-CATALOG-SSOT-01 (ADR-037 D10/D14) — fuentes normativas de los catálogos globales SRI.
/// Única vía de alta: este HasData materializado por migraciones; nunca CRUD ni edición manual.
/// </summary>
public class SriNormativeSourceConfiguration : IEntityTypeConfiguration<SriNormativeSource>
{
    /// <summary>Ficha Técnica Comprobantes Electrónicos Esquema Offline v2.34 — Tabla 20 (retención del IVA).</summary>
    public static readonly Guid FichaV234Table20Id = Guid.Parse("40000000-0000-0000-0000-000000000001");

    /// <summary>Catálogo ATS de retención en la fuente de Renta, al que remite la Ficha v2.34 §9.15 (no verificado).</summary>
    public static readonly Guid AtsIncomeRetentionCatalogId = Guid.Parse("40000000-0000-0000-0000-000000000002");

    public const string IntroducedInMigration = "SriRetentionCatalogVersioning";

    /// <summary>
    /// ZH-SRI-RETENTION-INCOME-CATALOG-SSOT-01 — Catálogo ATS oficial (Catalogo_ATS.xls), hoja TABLAS RETENCIONES,
    /// Tabla 3.10, bloque "DESDE 06/AGOSTO/2026": fuente verificada de los códigos y tarifas de Renta.
    /// </summary>
    public static readonly Guid AtsIncomeTable310From20260806Id = Guid.Parse("40000000-0000-0000-0000-000000000003");

    public const string IncomeAtsIntroducedInMigration = "SriRetentionIncomeCatalogAts20260806";

    public void Configure(EntityTypeBuilder<SriNormativeSource> builder)
    {
        builder.ToTable("sri_normative_source", schema: "global");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id");
        builder
            .Property(x => x.Document)
            .HasColumnName("document")
            .HasMaxLength(SriNormativeSource.DocumentMaxLen)
            .IsRequired();
        builder.Property(x => x.Version).HasColumnName("version").HasMaxLength(SriNormativeSource.VersionMaxLen);
        builder.Property(x => x.Section).HasColumnName("section").HasMaxLength(SriNormativeSource.SectionMaxLen);
        builder.Property(x => x.PublishedOn).HasColumnName("published_on");
        builder
            .Property(x => x.ReferenceUrl)
            .HasColumnName("reference_url")
            .HasMaxLength(SriNormativeSource.ReferenceUrlMaxLen);
        builder
            .Property(x => x.DocumentSha256)
            .HasColumnName("document_sha256")
            .HasMaxLength(SriNormativeSource.Sha256Len);
        builder
            .Property(x => x.IntroducedInMigration)
            .HasColumnName("introduced_in_migration")
            .HasMaxLength(SriNormativeSource.MigrationMaxLen)
            .IsRequired();
        builder.Property(x => x.Notes).HasColumnName("notes").HasMaxLength(SriNormativeSource.NotesMaxLen);

        builder.HasData(
            new SriNormativeSource
            {
                Id = FichaV234Table20Id,
                Document = "FICHA_TECNICA_OFFLINE",
                Version = "2.34",
                Section = "§9.15 Tabla 20 — Retención del IVA (incluye retención en cero y no procede)",
                PublishedOn = new DateOnly(2026, 7, 27),
                ReferenceUrl =
                    "https://www.sri.gob.ec/o/sri-portlet-biblioteca-alfresco-internet/descargar/f8d9bb36-5632-4f96-b463-b9265b55338c/FICHA%20TE%cc%81CNICA%20COMPROBANTES%20ELECTRO%cc%81NICOS%20ESQUEMA%20OFFLINE%20Versio%cc%81n%202.34.pdf",
                DocumentSha256 = "7333aebfbdf2cb3ba83f9fc67a7a7f0346ca59506480a260cc42f96dbdfc13c9",
                IntroducedInMigration = IntroducedInMigration,
                Notes =
                    "Porcentaje → codigoRetencion: 10%→9, 20%→10, 30%→1, 50%→11, 70%→2, 100%→3, en cero→7, no procede→8. "
                    + "La tabla no define 15% (concepto 728 sin representación). Sin fecha de inicio confirmada: "
                    + "las versiones se registran sin límite inferior (ValidFrom null).",
            },
            new SriNormativeSource
            {
                Id = AtsIncomeRetentionCatalogId,
                Document = "CATALOGO_ATS",
                Version = null,
                Section = "Retención en la fuente de Impuesto a la Renta (referido por Ficha v2.34 §9.15)",
                PublishedOn = null,
                ReferenceUrl = "http://www.sri.gob.ec/web/guest/formularios-e-instructivos1",
                DocumentSha256 = null,
                IntroducedInMigration = IntroducedInMigration,
                Notes =
                    "PENDIENTE DE VERIFICACIÓN (ADR-037 DR-4). El XML usa el mismo código del catálogo, como en el "
                    + "ejemplo oficial de la Ficha v2.34 Anexo 1 (codigoRetencion 323B1). Porcentajes no "
                    + "verificados: las versiones de Renta no fijan Percentage y no se exige coincidencia.",
            },
            new SriNormativeSource
            {
                Id = AtsIncomeTable310From20260806Id,
                Document = "CATALOGO_ATS",
                Version = "2026-08-06",
                Section =
                    "Hoja TABLAS RETENCIONES — Tabla 3.10 Conceptos de retención en la fuente de IR (AIR), bloque DESDE 06/AGOSTO/2026",
                PublishedOn = new DateOnly(2026, 8, 6),
                ReferenceUrl =
                    "https://www.sri.gob.ec/o/sri-portlet-biblioteca-alfresco-internet/descargar/e6a826af-b22c-40bb-8752-d711f293b8f9/Catalogo_ATS.xls",
                DocumentSha256 = "bd3f7834f2cd31187af39cd2f4c685a646d7e49316e92ee493da5f2dd9776e3e",
                IntroducedInMigration = IncomeAtsIntroducedInMigration,
                Notes =
                    "Descargado el 2026-10-02 desde https://www.sri.gob.ec/formularios-e-instructivos1. Fecha de "
                    + "actualización = inicio del bloque (06/08/2026). Tarifas no numéricas (\"12 o 14\", \"1 /0 según "
                    + "resolución…\") se registran como regla condicional, nunca como porcentaje.",
            }
        );
    }
}

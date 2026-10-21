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
    public static readonly Guid IceSpecific2026Id = Guid.Parse("40000000-0000-0000-0000-000000000006");
    /// <summary>Ficha técnica offline 2.34 — Tabla 18: códigos y conceptos ICE (sus columnas de tarifa son de 2023).</summary>
    public static readonly Guid FichaV234Table18Id = Guid.Parse("40000000-0000-0000-0000-000000000007");
    /// <summary>LRTI art. 82 — tarifas ad valorem de los grupos I, II y III.</summary>
    public static readonly Guid LrtiArticle82Id = Guid.Parse("40000000-0000-0000-0000-000000000008");
    /// <summary>Tarifas ad valorem restablecidas desde 2024-05-21 por la nulidad del D.E. 645.</summary>
    public static readonly Guid IceRestored20240521Id = Guid.Parse("40000000-0000-0000-0000-000000000009");
    /// <summary>Matriz resumen ICE del SRI: bienes y servicios gravados, exenciones y no gravados.</summary>
    public static readonly Guid IceSriSummaryMatrixId = Guid.Parse("40000000-0000-0000-0000-00000000000a");
    /// <summary>Ficha Técnica Comprobantes Electrónicos Esquema Offline v2.34 — Tabla 20 (retención del IVA).</summary>
    public static readonly Guid FichaV234Table20Id = Guid.Parse(
        "40000000-0000-0000-0000-000000000001"
    );

    /// <summary>Catálogo ATS de retención en la fuente de Renta, al que remite la Ficha v2.34 §9.15 (no verificado).</summary>
    public static readonly Guid AtsIncomeRetentionCatalogId = Guid.Parse(
        "40000000-0000-0000-0000-000000000002"
    );

    /// <summary>
    /// Migración que siembra todas las fuentes normativas: la baseline única de ERP ZH V1 (GREENFIELD).
    /// Una fuente posterior a la primera instalación publicada nombra la migración incremental que la incorpora.
    /// </summary>
    public const string IntroducedInMigration = "InitialProductionBaseline";

    /// <summary>
    /// ZH-SRI-RETENTION-INCOME-CATALOG-SSOT-01 — Catálogo ATS oficial (Catalogo_ATS.xls), hoja TABLAS RETENCIONES,
    /// Tabla 3.10, bloque "DESDE 06/AGOSTO/2026": fuente verificada de los códigos y tarifas de Renta.
    /// </summary>
    public static readonly Guid AtsIncomeTable310From20260806Id = Guid.Parse(
        "40000000-0000-0000-0000-000000000003"
    );

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
        builder
            .Property(x => x.Version)
            .HasColumnName("version")
            .HasMaxLength(SriNormativeSource.VersionMaxLen);
        builder
            .Property(x => x.Section)
            .HasColumnName("section")
            .HasMaxLength(SriNormativeSource.SectionMaxLen);
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
        builder
            .Property(x => x.Notes)
            .HasColumnName("notes")
            .HasMaxLength(SriNormativeSource.NotesMaxLen);

        builder.HasData(
            new SriNormativeSource
            {
                Id = IceSpecific2026Id, Document = "NAC-DGERCGC25-00000040", Version = "2026",
                Section = "Artículo único — tarifas específicas ICE período fiscal 2026",
                PublishedOn = null,
                ReferenceUrl = "https://www.sri.gob.ec/o/sri-portlet-biblioteca-alfresco-internet/descargar?id=c5a91b7c-c7ff-45c2-9b0d-ca0670fb8723&nombre=NAC-DGERCGC25-00000040.pdf",
                IntroducedInMigration = IntroducedInMigration,
                Notes = "Firmada 2025-12-29; aplicable 2026-01-01 a 2026-12-31 (período fiscal 2026). Cigarrillos USD 0,16 por unidad; alcohol (uso distinto a bebidas alcohólicas y farmacéuticos) y bebidas alcohólicas USD 10,41, cerveza artesanal USD 1,56 y cerveza industrial USD 13,62 por litro de alcohol puro; bebidas no alcohólicas y gaseosas con más de 25 g de azúcar por litro, excepto energizantes, USD 0,18 por 100 g de azúcar añadida; fundas plásticas USD 0,08 por funda. Ad valorem 75% de bebidas alcohólicas y cerveza: LRTI art. 82 grupo IV. Códigos: Ficha técnica offline 2.34, Tabla 18; no usar sus columnas de 2023 como tarifa 2026. Las filas ICE activas de base física no comercial conservan UnitValue nulo. Cálculo mixto y condiciones de aplicación no implementados.",
            },
            new SriNormativeSource
            {
                Id = FichaV234Table18Id, Document = "FICHA_TECNICA_OFFLINE", Version = "2.34",
                Section = "§9.13 Tabla 18 — Tarifa del ICE (códigos y conceptos)",
                PublishedOn = new DateOnly(2026, 7, 27),
                ReferenceUrl = "https://www.sri.gob.ec/o/sri-portlet-biblioteca-alfresco-internet/descargar/f8d9bb36-5632-4f96-b463-b9265b55338c/FICHA%20TE%cc%81CNICA%20COMPROBANTES%20ELECTRO%cc%81NICOS%20ESQUEMA%20OFFLINE%20Versio%cc%81n%202.34.pdf",
                DocumentSha256 = "7333aebfbdf2cb3ba83f9fc67a7a7f0346ca59506480a260cc42f96dbdfc13c9",
                IntroducedInMigration = IntroducedInMigration,
                Notes = "Fuente de identidad de los códigos ICE (pp. 30-32). Sus tarifas corresponden a enero y febrero-diciembre de 2023; la propia tabla remite a la normativa vigente. Un código histórico ausente de la tabla queda no seleccionable sin inferir equivalencias.",
            },
            new SriNormativeSource
            {
                Id = LrtiArticle82Id, Document = "LRTI", Version = "RO 525 2024-03-25",
                Section = "Art. 82 — grupos I, II y III (tarifa ad valorem); grupo IV ad valorem 75%",
                ReferenceUrl = "https://www.sri.gob.ec/o/sri-portlet-biblioteca-alfresco-internet/descargar/7da8c609-4299-4597-8825-29446242c63b/LEY_DE_REGIMEN_TRIBUTARIO_INTERNO_-_LRTI.pdf",
                DocumentSha256 = "a0f2bfcb8b68a45a5c3aa6e3cab21b1471ec8b01bb0312287544024bf213cb6e",
                IntroducedInMigration = IntroducedInMigration,
                Notes = "Codificación publicada por el SRI, última reforma Suplemento RO 525, 25-III-2024; consultada 2026-10-21. Fecha de inicio de cada tarifa no verificada (ValidFrom nulo). Rige sobre la columna de Tabla 18 cuando difieren (televisión pagada 15%, clubes 30%). Las filas reformadas por el D.E. 645 se rigen por la fuente 40000000-0000-0000-0000-000000000009.",
            },
            new SriNormativeSource
            {
                Id = IceRestored20240521Id, Document = "SRI-NAC-DNJ-2024-0042-OF",
                Section = "Tarifas ad valorem ICE que rigen por la nulidad del D.E. 645 (SENAE Boletín 88-2024)",
                PublishedOn = new DateOnly(2024, 5, 30),
                ReferenceUrl = "https://www.aduana.gob.ec/gaceta-boletin/tarifas-ad-valorem-del-impuesto-a-los-consumos-especiales-ice-que-rigen-por-la-derogacion-del-decreto-ejecutivo-no-645-de-10-de-enero-de-2023/",
                IntroducedInMigration = IntroducedInMigration,
                Notes = "Oficio SRI de 2024-05-29 reproducido por SENAE: desde 2024-05-21 tabaco calentado y líquidos con nicotina 150%, armas de fuego, deportivas y municiones 300%, aviones, avionetas, helicópteros, motos acuáticas, tricares, cuadrones, yates y barcos de recreo 15%. Armas: el D.E. 302 (2024-06-12) reduciría a 30% desde 2024-07-01; texto oficial y alcance no verificados, por lo que 3630/3730 no son seleccionables.",
            },
            new SriNormativeSource
            {
                Id = IceSriSummaryMatrixId, Document = "SRI_MATRIZ_RESUMEN_ICE", Version = "2024",
                Section = "Hoja 2024 — bienes y servicios gravados, exenciones y beneficios",
                ReferenceUrl = "https://www.sri.gob.ec/o/sri-portlet-biblioteca-alfresco-internet/descargar/3ba17b2c-6e81-4383-8d3e-ec6f7ee1160e/Tabla%20Resumen%20ICE%20.xlsx",
                DocumentSha256 = "ea9e054e96b1b052109d6786690944ebf0c3c185233b1c8c776e5c10faf1c31f",
                IntroducedInMigration = IntroducedInMigration,
                Notes = "Enlazada desde https://www.sri.gob.ec/impuesto-consumos-especiales; consultada 2026-10-21. Frente a la hoja 2021 ya no grava videojuegos, calefones a gas ni telefonía, y declara exentos los vehículos eléctricos e híbridos. Sus tarifas reformadas por el D.E. 645 no rigen desde 2024-05-21.",
            },
            new SriNormativeSource
            {
                Id = SriAnnulmentPolicyVersionConfiguration.LegacySourceId,
                Document = "NAC-DGERCGC16-00000092", Section = "Art. 7 — anulación en línea, 90 días",
                PublishedOn = new DateOnly(2016, 2, 22),
                ReferenceUrl = "https://www.sri.gob.ec/o/sri-portlet-biblioteca-alfresco-internet/descargar/c97242e6-c271-4eb8-8f6a-f687313118ba/Guia%20para%20contribuyentes%20de%20anulaci%C3%B3n%20de%20comprobantes%20electr%C3%B3nicos.pdf",
                IntroducedInMigration = IntroducedInMigration,
                Notes = "Historia por fecha de emisión, hasta 2025-07-31; no se reinterpreta evidencia fiscal persistida.",
            },
            new SriNormativeSource
            {
                Id = SriAnnulmentPolicyVersionConfiguration.CurrentSourceId,
                Document = "NAC-DGERCGC25-00000017", Section = "Art. 1 a), transitoria primera; reforma NAC-DGERCGC25-00000014 art. 3",
                ReferenceUrl = "https://www.sri.gob.ec/o/sri-portlet-biblioteca-alfresco-internet/descargar?id=e98fc8a6-299e-4ea9-8de7-2f6c70dbb4f5&nombre=NAC-DGERCGC25-00000017.pdf",
                IntroducedInMigration = IntroducedInMigration,
                Notes = "Firmada 2025-07-29. Día 7 del mes siguiente para emisión desde 2025-08-01; extensión por descanso obligatorio/feriado nacional o local según art. 3 reformado. No aplica excepción ISD al dominio IVA/Renta.",
            },
            new SriNormativeSource
            {
                Id = FichaV234Table20Id,
                Document = "FICHA_TECNICA_OFFLINE",
                Version = "2.34",
                Section =
                    "§9.15 Tabla 20 — Retención del IVA (incluye retención en cero y no procede)",
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
                Section =
                    "Retención en la fuente de Impuesto a la Renta (referido por Ficha v2.34 §9.15)",
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
                IntroducedInMigration = IntroducedInMigration,
                Notes =
                    "Descargado el 2026-10-02 desde https://www.sri.gob.ec/formularios-e-instructivos1. Fecha de "
                    + "actualización = inicio del bloque (06/08/2026). Tarifas no numéricas (\"12 o 14\", \"1 /0 según "
                    + "resolución…\") se registran como regla condicional, nunca como porcentaje.",
            }
        );
    }
}

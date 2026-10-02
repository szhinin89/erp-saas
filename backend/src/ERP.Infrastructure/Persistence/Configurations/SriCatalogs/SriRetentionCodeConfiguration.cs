using ERP.Domain.Modules.SriCatalogs.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ERP.Infrastructure.Persistence.Configurations.SriCatalogs;

public class SriRetentionCodeConfiguration : IEntityTypeConfiguration<SriRetentionCode>
{
    public void Configure(EntityTypeBuilder<SriRetentionCode> builder)
    {
        builder.ToTable("sri_retention_code", schema: "global");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id").HasDefaultValueSql("gen_random_uuid()");
        builder.Property(x => x.TaxType).HasColumnName("tax_type").HasMaxLength(10).IsRequired();
        builder.Property(x => x.Code).HasColumnName("code").HasMaxLength(10).IsRequired();
        builder.Property(x => x.Name).HasColumnName("name").HasMaxLength(300).IsRequired();
        builder.Property(x => x.Percentage).HasColumnName("percentage").HasPrecision(7, 4);
        builder
            .Property(x => x.AppliesTo)
            .HasColumnName("applies_to")
            .HasMaxLength(15)
            .HasDefaultValue("SUPPLIER");
        // ZH-SRI-RETENTION-CATALOG-SSOT-01 — IsActive es la habilitación operativa (ADR-037 D13). Con
        // DEFAULT true y generación "on add", EF omite un `false` (valor CLR por defecto) en seeds e
        // inserts y la fila quedaría habilitada. ValueGeneratedNever conserva el DEFAULT de la columna
        // pero obliga a EF a escribir siempre el valor real.
        builder
            .Property(x => x.IsActive)
            .HasColumnName("is_active")
            .HasDefaultValue(true)
            .ValueGeneratedNever();

        builder
            .HasIndex(x => new { x.TaxType, x.Code })
            .IsUnique()
            .HasDatabaseName("uq_sri_ret_code");

        builder.HasData(
            // IVA
            new SriRetentionCode
            {
                Id = Guid.Parse("10000000-0000-0000-0000-000000000001"),
                TaxType = "IVA",
                Code = "721",
                Name = "Ret. IVA 10% – Bienes (tarifa vigente)",
                Percentage = 10.00m,
            },
            new SriRetentionCode
            {
                Id = Guid.Parse("10000000-0000-0000-0000-000000000002"),
                TaxType = "IVA",
                Code = "723",
                Name = "Ret. IVA 20% – Servicios (tarifa vigente)",
                Percentage = 20.00m,
            },
            new SriRetentionCode
            {
                Id = Guid.Parse("10000000-0000-0000-0000-000000000003"),
                TaxType = "IVA",
                Code = "725",
                Name = "Ret. IVA 30% – Presuntivo bienes",
                Percentage = 30.00m,
            },
            new SriRetentionCode
            {
                Id = Guid.Parse("10000000-0000-0000-0000-000000000004"),
                TaxType = "IVA",
                Code = "726",
                Name = "Ret. IVA 70% – Presuntivo servicios",
                Percentage = 70.00m,
            },
            new SriRetentionCode
            {
                Id = Guid.Parse("10000000-0000-0000-0000-000000000005"),
                TaxType = "IVA",
                Code = "727",
                Name = "Ret. IVA 100% – Liq. compra / honorarios",
                Percentage = 100.00m,
            },
            new SriRetentionCode
            {
                Id = Guid.Parse("10000000-0000-0000-0000-000000000006"),
                TaxType = "IVA",
                Code = "728",
                Name = "Ret. IVA 15% – Constructoras",
                Percentage = 15.00m,
                // ZH-SRI-RETENTION-CATALOG-SSOT-01 — la Ficha v2.34 Tabla 20 no define una retención de IVA
                // del 15 %: sin representación XML oficial, no se habilita para operaciones nuevas. Se
                // conservan Id, código y referencias históricas (ADR-037 D9/D13); se rehabilita solo por
                // migración cuando exista fuente oficial.
                IsActive = false,
            },
            // ZH-SRI-RETENTION-CATALOG-SSOT-01 — conceptos de la Ficha Técnica v2.34, Tabla 20, que el
            // catálogo no tenía. Identidad interna (Id) y clave de negocio (Code) son independientes del
            // codigoRetencion oficial, que vive en SriRetentionCodeVersion.XmlCode (ADR-037 D5): el Code
            // es mnemónico y deliberadamente NO imita un código oficial/formulario (no 729, 730…).
            new SriRetentionCode
            {
                Id = Guid.Parse("10000000-0000-0000-0000-000000000007"),
                TaxType = "IVA",
                Code = "IVA-50",
                Name = "Ret. IVA 50%",
                Percentage = 50.00m,
            },
            // Retención en cero (Tabla 20; Res. NAC-DGERCGC15-00000284) y "No procede retención":
            // representación confirmada, pero RetentionDocumentLine exige tasa y valor > 0, así que hoy
            // no pueden usarse en una línea. Se registran NO habilitados (no seleccionables) para no
            // ofrecer opciones que el dominio rechaza; su representación XML queda versionada.
            new SriRetentionCode
            {
                Id = Guid.Parse("10000000-0000-0000-0000-000000000008"),
                TaxType = "IVA",
                Code = "IVA-0",
                Name = "Ret. IVA 0% – Retención en cero",
                Percentage = 0.00m,
                IsActive = false,
            },
            new SriRetentionCode
            {
                Id = Guid.Parse("10000000-0000-0000-0000-000000000009"),
                TaxType = "IVA",
                Code = "IVA-NP",
                Name = "No procede retención de IVA",
                Percentage = 0.00m,
                IsActive = false,
            },
            // RENTA
            new SriRetentionCode
            {
                Id = Guid.Parse("20000000-0000-0000-0000-000000000001"),
                TaxType = "RENTA",
                Code = "303",
                // ZH-SRI-RETENTION-INCOME-CATALOG-SSOT-01 — nombre y tarifa operativa vigentes (Catálogo ATS,
                // Tabla 3.10 desde 06/08/2026). La historia vive en sri_retention_code_version.
                Name = "Honorarios profesionales y demás pagos por servicios relacionados con el título profesional",
                Percentage = 10.00m,
            },
            new SriRetentionCode
            {
                Id = Guid.Parse("20000000-0000-0000-0000-000000000002"),
                TaxType = "RENTA",
                Code = "304",
                // ZH-SRI-RETENTION-INCOME-CATALOG-SSOT-01 — nombre y tarifa operativa vigentes (Catálogo ATS,
                // Tabla 3.10 desde 06/08/2026). La historia vive en sri_retention_code_version.
                Name = "Servicios predomina el intelecto no relacionados con el título profesional",
                Percentage = 10.00m,
            },
            new SriRetentionCode
            {
                Id = Guid.Parse("20000000-0000-0000-0000-000000000003"),
                TaxType = "RENTA",
                Code = "307",
                // ZH-SRI-RETENTION-INCOME-CATALOG-SSOT-01 — nombre y tarifa operativa vigentes (Catálogo ATS,
                // Tabla 3.10 desde 06/08/2026). La historia vive en sri_retention_code_version.
                Name = "Servicios predomina la mano de obra",
                Percentage = 3.00m,
            },
            new SriRetentionCode
            {
                Id = Guid.Parse("20000000-0000-0000-0000-000000000004"),
                TaxType = "RENTA",
                Code = "309",
                // ZH-SRI-RETENTION-INCOME-CATALOG-SSOT-01 — nombre y tarifa operativa vigentes (Catálogo ATS,
                // Tabla 3.10 desde 06/08/2026). La historia vive en sri_retention_code_version.
                Name = "Servicios prestados por medios de comunicación y agencias de publicidad",
                Percentage = 3.00m,
            },
            new SriRetentionCode
            {
                Id = Guid.Parse("20000000-0000-0000-0000-000000000005"),
                TaxType = "RENTA",
                Code = "310",
                // ZH-SRI-RETENTION-INCOME-CATALOG-SSOT-01 — nombre oficial (Catálogo ATS, Tabla 3.10 desde
                // 06/08/2026). Tarifa CONDICIONAL ("1 /0 según resolución NAC-DGERCGC26-00000028"): el ERP no puede determinarla,
                // así que no se habilita; Percentage queda como valor heredado sin efecto operativo.
                Name = "Servicio de transporte privado de pasajeros o transporte público o privado de carga",
                Percentage = 1.00m,
                IsActive = false,
            },
            new SriRetentionCode
            {
                Id = Guid.Parse("20000000-0000-0000-0000-000000000006"),
                TaxType = "RENTA",
                Code = "312",
                // ZH-SRI-RETENTION-INCOME-CATALOG-SSOT-01 — nombre y tarifa operativa vigentes (Catálogo ATS,
                // Tabla 3.10 desde 06/08/2026). La historia vive en sri_retention_code_version.
                Name = "Transferencia de bienes muebles de naturaleza corporal",
                Percentage = 2.00m,
            },
            new SriRetentionCode
            {
                Id = Guid.Parse("20000000-0000-0000-0000-000000000007"),
                TaxType = "RENTA",
                Code = "320",
                // ZH-SRI-RETENTION-INCOME-CATALOG-SSOT-01 — nombre y tarifa operativa vigentes (Catálogo ATS,
                // Tabla 3.10 desde 06/08/2026). La historia vive en sri_retention_code_version.
                Name = "Arrendamiento bienes inmuebles",
                Percentage = 10.00m,
            },
            new SriRetentionCode
            {
                Id = Guid.Parse("20000000-0000-0000-0000-000000000008"),
                TaxType = "RENTA",
                Code = "325",
                // ZH-SRI-RETENTION-INCOME-CATALOG-SSOT-01 — nombre y tarifa operativa vigentes (Catálogo ATS,
                // Tabla 3.10 desde 06/08/2026). La historia vive en sri_retention_code_version.
                Name = "Anticipo dividendos",
                Percentage = 25.00m,
            },
            new SriRetentionCode
            {
                Id = Guid.Parse("20000000-0000-0000-0000-000000000009"),
                TaxType = "RENTA",
                Code = "327",
                // ZH-SRI-RETENTION-INCOME-CATALOG-SSOT-01 — nombre oficial (Catálogo ATS, Tabla 3.10 desde
                // 06/08/2026). Tarifa CONDICIONAL ("12 o 14"): el ERP no puede determinarla,
                // así que no se habilita; Percentage queda como valor heredado sin efecto operativo.
                Name = "Dividendos distribuidos a personas naturales residentes",
                Percentage = 1.75m,
                IsActive = false,
            },
            new SriRetentionCode
            {
                Id = Guid.Parse("20000000-0000-0000-0000-000000000010"),
                TaxType = "RENTA",
                Code = "341",
                Name = "Otras retenciones aplicables al 2%",
                Percentage = 2.00m,
                // ZH-SRI-RETENTION-INCOME-CATALOG-SSOT-01 — no existe en el Catálogo ATS vigente (Tabla 3.10,
                // desde 06/08/2026): retirado de operaciones nuevas; se conserva para históricos.
                IsActive = false,
            },
            new SriRetentionCode
            {
                Id = Guid.Parse("20000000-0000-0000-0000-000000000011"),
                TaxType = "RENTA",
                Code = "342",
                Name = "Otras retenciones aplicables al 1%",
                Percentage = 1.00m,
                // ZH-SRI-RETENTION-INCOME-CATALOG-SSOT-01 — no existe en el Catálogo ATS vigente (Tabla 3.10,
                // desde 06/08/2026): retirado de operaciones nuevas; se conserva para históricos.
                IsActive = false,
            },
            new SriRetentionCode
            {
                Id = Guid.Parse("20000000-0000-0000-0000-000000000012"),
                TaxType = "RENTA",
                Code = "343",
                // ZH-SRI-RETENTION-INCOME-CATALOG-SSOT-01 — nombre y tarifa operativa vigentes (Catálogo ATS,
                // Tabla 3.10 desde 06/08/2026). La historia vive en sri_retention_code_version.
                Name = "Otras retenciones aplicables el 1% (incluye régimen RIMPE - Emprendedores, para este caso aplica con cualquier forma de pago inclusive los pagos que deban realizar las tarjetas de crédito/débito)",
                Percentage = 1.00m,
            },
            new SriRetentionCode
            {
                Id = Guid.Parse("20000000-0000-0000-0000-000000000013"),
                TaxType = "RENTA",
                Code = "344",
                Name = "Otras retenciones aplicables al 2.75%",
                Percentage = 2.75m,
                // ZH-SRI-RETENTION-INCOME-CATALOG-SSOT-01 — no existe en el Catálogo ATS vigente (Tabla 3.10,
                // desde 06/08/2026): retirado de operaciones nuevas; se conserva para históricos.
                IsActive = false,
            },
            // ISD
            new SriRetentionCode
            {
                Id = Guid.Parse("30000000-0000-0000-0000-000000000001"),
                TaxType = "ISD",
                Code = "4580",
                Name = "ISD – Impuesto a la Salida de Divisas",
                Percentage = 5.00m,
            }
        );
    }
}

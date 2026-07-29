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
        builder.Property(x => x.AppliesTo).HasColumnName("applies_to").HasMaxLength(15).HasDefaultValue("SUPPLIER");
        builder.Property(x => x.IsActive).HasColumnName("is_active").HasDefaultValue(true);

        builder.HasIndex(x => new { x.TaxType, x.Code }).IsUnique().HasDatabaseName("uq_sri_ret_code");

        builder.HasData(
            // IVA
            new SriRetentionCode { Id = Guid.Parse("10000000-0000-0000-0000-000000000001"), TaxType = "IVA", Code = "721", Name = "Ret. IVA 10% – Bienes (tarifa vigente)", Percentage = 10.00m },
            new SriRetentionCode { Id = Guid.Parse("10000000-0000-0000-0000-000000000002"), TaxType = "IVA", Code = "723", Name = "Ret. IVA 20% – Servicios (tarifa vigente)", Percentage = 20.00m },
            new SriRetentionCode { Id = Guid.Parse("10000000-0000-0000-0000-000000000003"), TaxType = "IVA", Code = "725", Name = "Ret. IVA 30% – Presuntivo bienes", Percentage = 30.00m },
            new SriRetentionCode { Id = Guid.Parse("10000000-0000-0000-0000-000000000004"), TaxType = "IVA", Code = "726", Name = "Ret. IVA 70% – Presuntivo servicios", Percentage = 70.00m },
            new SriRetentionCode { Id = Guid.Parse("10000000-0000-0000-0000-000000000005"), TaxType = "IVA", Code = "727", Name = "Ret. IVA 100% – Liq. compra / honorarios", Percentage = 100.00m },
            new SriRetentionCode { Id = Guid.Parse("10000000-0000-0000-0000-000000000006"), TaxType = "IVA", Code = "728", Name = "Ret. IVA 15% – Constructoras", Percentage = 15.00m },
            // RENTA
            new SriRetentionCode { Id = Guid.Parse("20000000-0000-0000-0000-000000000001"), TaxType = "RENTA", Code = "303", Name = "Honorarios profesionales y demás servicios", Percentage = 10.00m },
            new SriRetentionCode { Id = Guid.Parse("20000000-0000-0000-0000-000000000002"), TaxType = "RENTA", Code = "304", Name = "Servicios – predomina mano de obra", Percentage = 2.00m },
            new SriRetentionCode { Id = Guid.Parse("20000000-0000-0000-0000-000000000003"), TaxType = "RENTA", Code = "307", Name = "Publicidad y comunicación", Percentage = 1.75m },
            new SriRetentionCode { Id = Guid.Parse("20000000-0000-0000-0000-000000000004"), TaxType = "RENTA", Code = "309", Name = "Arrendamiento bienes inmuebles (persona natural)", Percentage = 8.00m },
            new SriRetentionCode { Id = Guid.Parse("20000000-0000-0000-0000-000000000005"), TaxType = "RENTA", Code = "310", Name = "Seguros y reaseguros (10% de primas)", Percentage = 1.00m },
            new SriRetentionCode { Id = Guid.Parse("20000000-0000-0000-0000-000000000006"), TaxType = "RENTA", Code = "312", Name = "Transf. bienes muebles de naturaleza corporal", Percentage = 1.00m },
            new SriRetentionCode { Id = Guid.Parse("20000000-0000-0000-0000-000000000007"), TaxType = "RENTA", Code = "320", Name = "Servicios entre sociedades", Percentage = 2.75m },
            new SriRetentionCode { Id = Guid.Parse("20000000-0000-0000-0000-000000000008"), TaxType = "RENTA", Code = "325", Name = "Compra bienes corporales muebles", Percentage = 1.75m },
            new SriRetentionCode { Id = Guid.Parse("20000000-0000-0000-0000-000000000009"), TaxType = "RENTA", Code = "327", Name = "Actividades de construcción (contrato)", Percentage = 1.75m },
            new SriRetentionCode { Id = Guid.Parse("20000000-0000-0000-0000-000000000010"), TaxType = "RENTA", Code = "341", Name = "Otras retenciones aplicables al 2%", Percentage = 2.00m },
            new SriRetentionCode { Id = Guid.Parse("20000000-0000-0000-0000-000000000011"), TaxType = "RENTA", Code = "342", Name = "Otras retenciones aplicables al 1%", Percentage = 1.00m },
            new SriRetentionCode { Id = Guid.Parse("20000000-0000-0000-0000-000000000012"), TaxType = "RENTA", Code = "343", Name = "Otras retenciones aplicables al 1.75%", Percentage = 1.75m },
            new SriRetentionCode { Id = Guid.Parse("20000000-0000-0000-0000-000000000013"), TaxType = "RENTA", Code = "344", Name = "Otras retenciones aplicables al 2.75%", Percentage = 2.75m },
            // ISD
            new SriRetentionCode { Id = Guid.Parse("30000000-0000-0000-0000-000000000001"), TaxType = "ISD", Code = "4580", Name = "ISD – Impuesto a la Salida de Divisas", Percentage = 5.00m }
        );
    }
}

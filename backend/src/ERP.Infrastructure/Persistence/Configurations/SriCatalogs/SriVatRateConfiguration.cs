using ERP.Domain.Modules.SriCatalogs.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ERP.Infrastructure.Persistence.Configurations.SriCatalogs;

public class SriVatRateConfiguration : IEntityTypeConfiguration<SriVatRate>
{
    public void Configure(EntityTypeBuilder<SriVatRate> builder)
    {
        builder.ToTable("sri_vat_rate", schema: "global");
        builder.HasKey(x => x.Code);
        builder.Property(x => x.Code).HasColumnName("code").HasMaxLength(5);
        builder.Property(x => x.Name).HasColumnName("name").HasMaxLength(60).IsRequired();
        builder.Property(x => x.Percentage).HasColumnName("percentage").HasPrecision(6, 2);
        builder.Property(x => x.IsActive).HasColumnName("is_active").HasDefaultValue(true);
        builder.Property(x => x.ValidFrom).HasColumnName("valid_from");
        builder.Property(x => x.ValidUntil).HasColumnName("valid_until");

        builder.HasData(
            new SriVatRate { Code = "0",  Name = "0% IVA",                Percentage = 0.00m,  IsActive = true,  ValidFrom = new DateOnly(2008, 1, 1),  ValidUntil = null },
            new SriVatRate { Code = "2",  Name = "12% IVA (histÃ³rico)",   Percentage = 12.00m, IsActive = false, ValidFrom = new DateOnly(2008, 1, 1),  ValidUntil = new DateOnly(2016, 5,  31) },
            new SriVatRate { Code = "3",  Name = "14% IVA (histÃ³rico)",   Percentage = 14.00m, IsActive = false, ValidFrom = new DateOnly(2016, 6, 1),  ValidUntil = new DateOnly(2017, 5,  31) },
            new SriVatRate { Code = "4",  Name = "No Objeto de IVA",      Percentage = 0.00m,  IsActive = true,  ValidFrom = new DateOnly(2008, 1, 1),  ValidUntil = null },
            new SriVatRate { Code = "5",  Name = "Exento de IVA",         Percentage = 0.00m,  IsActive = true,  ValidFrom = new DateOnly(2008, 1, 1),  ValidUntil = null },
            new SriVatRate { Code = "6",  Name = "No Objeto IVA (Serv.)", Percentage = 0.00m,  IsActive = true,  ValidFrom = new DateOnly(2008, 1, 1),  ValidUntil = null },
            new SriVatRate { Code = "7",  Name = "Diferencial de precio", Percentage = 0.00m,  IsActive = true,  ValidFrom = new DateOnly(2008, 1, 1),  ValidUntil = null },
            new SriVatRate { Code = "8",  Name = "5% IVA (reducido)",     Percentage = 5.00m,  IsActive = true,  ValidFrom = new DateOnly(2024, 1, 1),  ValidUntil = null },
            new SriVatRate { Code = "10", Name = "15% IVA (vigente)",     Percentage = 15.00m, IsActive = true,  ValidFrom = new DateOnly(2024, 4, 1),  ValidUntil = null },
            new SriVatRate { Code = "11", Name = "13% IVA (transitorio)", Percentage = 13.00m, IsActive = false, ValidFrom = new DateOnly(2023, 1, 1),  ValidUntil = new DateOnly(2023, 12, 31) }
        );
    }
}

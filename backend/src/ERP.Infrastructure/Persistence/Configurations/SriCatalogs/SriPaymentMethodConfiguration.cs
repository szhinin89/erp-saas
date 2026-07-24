using ERP.Domain.Modules.SriCatalogs.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ERP.Infrastructure.Persistence.Configurations.SriCatalogs;

public class SriPaymentMethodConfiguration : IEntityTypeConfiguration<SriPaymentMethod>
{
    public void Configure(EntityTypeBuilder<SriPaymentMethod> builder)
    {
        builder.ToTable("sri_payment_method", schema: "global");
        builder.HasKey(x => x.Code);
        builder.Property(x => x.Code).HasColumnName("code").HasMaxLength(5);
        builder.Property(x => x.Name).HasColumnName("name").HasMaxLength(80).IsRequired();
        builder.Property(x => x.IsActive).HasColumnName("is_active").HasDefaultValue(true);

        builder.HasData(
            new SriPaymentMethod { Code = "01", Name = "Sin utilización del sistema financiero",      IsActive = true },
            new SriPaymentMethod { Code = "15", Name = "Compensación de deudas",                      IsActive = true },
            new SriPaymentMethod { Code = "16", Name = "Tarjeta de débito",                           IsActive = true },
            new SriPaymentMethod { Code = "17", Name = "Dinero electrónico",                          IsActive = true },
            new SriPaymentMethod { Code = "18", Name = "Tarjeta prepago",                             IsActive = true },
            new SriPaymentMethod { Code = "19", Name = "Tarjeta de crédito",                          IsActive = true },
            new SriPaymentMethod { Code = "20", Name = "Otros con utilización del sistema financiero", IsActive = true },
            new SriPaymentMethod { Code = "21", Name = "Endoso de títulos",                           IsActive = true }
        );
    }
}

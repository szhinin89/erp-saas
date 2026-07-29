using ERP.Domain.Modules.SriCatalogs.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ERP.Infrastructure.Persistence.Configurations.SriCatalogs;

public class SriSupplierTypeConfiguration : IEntityTypeConfiguration<SriSupplierType>
{
    public void Configure(EntityTypeBuilder<SriSupplierType> builder)
    {
        builder.ToTable("sri_supplier_type", schema: "global");
        builder.HasKey(x => x.Code);
        builder.Property(x => x.Code).HasColumnName("code").HasMaxLength(2);
        builder.Property(x => x.Name).HasColumnName("name").HasMaxLength(60).IsRequired();
        builder.Property(x => x.IsActive).HasColumnName("is_active").HasDefaultValue(true);

        // Tabla 26 (Tipo Proveedor de Reembolso) de la Ficha Técnica del SRI — Esquema Offline.
        builder.HasData(
            new SriSupplierType { Code = "01", Name = "Persona Natural", IsActive = true },
            new SriSupplierType { Code = "02", Name = "Sociedad", IsActive = true }
        );
    }
}

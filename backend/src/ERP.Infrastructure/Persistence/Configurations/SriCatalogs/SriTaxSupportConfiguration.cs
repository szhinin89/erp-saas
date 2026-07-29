using ERP.Domain.Modules.SriCatalogs.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ERP.Infrastructure.Persistence.Configurations.SriCatalogs;

public class SriTaxSupportConfiguration : IEntityTypeConfiguration<SriTaxSupport>
{
    public void Configure(EntityTypeBuilder<SriTaxSupport> builder)
    {
        builder.ToTable("sri_tax_support", schema: "global");
        builder.HasKey(x => x.Code);
        builder.Property(x => x.Code).HasColumnName("code").HasMaxLength(5);
        builder.Property(x => x.Name).HasColumnName("name").HasMaxLength(200).IsRequired();
        builder.Property(x => x.IsActive).HasColumnName("is_active").HasDefaultValue(true);

        builder.HasData(
            new SriTaxSupport { Code = "01", Name = "Crédito Tributario para declaración de IVA", IsActive = true },
            new SriTaxSupport { Code = "02", Name = "Costo o Gasto para declaración del IR", IsActive = true },
            new SriTaxSupport { Code = "03", Name = "Activo Fijo – Crédito Tributario IVA", IsActive = true },
            new SriTaxSupport { Code = "04", Name = "Activo Fijo – Costo o Gasto IR", IsActive = true },
            new SriTaxSupport { Code = "05", Name = "Liquidación Gastos de Viaje, Hospedaje y Alimentación", IsActive = true },
            new SriTaxSupport { Code = "06", Name = "Retención en la Fuente", IsActive = true },
            new SriTaxSupport { Code = "07", Name = "Distribución de Dividendos, Beneficios o Ganancias", IsActive = true },
            new SriTaxSupport { Code = "08", Name = "Impuesto a los Activos en el Exterior", IsActive = true },
            new SriTaxSupport { Code = "09", Name = "Retención del IVA 30%", IsActive = true },
            new SriTaxSupport { Code = "10", Name = "Retención del IVA 70%", IsActive = true },
            new SriTaxSupport { Code = "11", Name = "Retención del IVA 100%", IsActive = true },
            new SriTaxSupport { Code = "12", Name = "Exportación de Bienes", IsActive = true },
            new SriTaxSupport { Code = "13", Name = "No aplica", IsActive = true },
            new SriTaxSupport { Code = "14", Name = "Exportación de servicios con domicilio en el exterior", IsActive = true },
            new SriTaxSupport { Code = "15", Name = "Proveedor directo de exportador de bienes", IsActive = true },
            new SriTaxSupport { Code = "16", Name = "Provisiones de cuentas incobrables", IsActive = true },
            new SriTaxSupport { Code = "17", Name = "Nota de crédito deducible", IsActive = true },
            new SriTaxSupport { Code = "18", Name = "Importaciones", IsActive = true },
            new SriTaxSupport { Code = "19", Name = "Reembolso de gastos", IsActive = true },
            new SriTaxSupport { Code = "20", Name = "Notas de crédito por devoluciones", IsActive = true }
        );
    }
}

using ERP.Domain.Modules.SriCatalogs.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ERP.Infrastructure.Persistence.Configurations.SriCatalogs;

public class SriDocTypeConfiguration : IEntityTypeConfiguration<SriDocType>
{
    public void Configure(EntityTypeBuilder<SriDocType> builder)
    {
        builder.ToTable("sri_doc_type", schema: "global");
        builder.HasKey(x => x.Code);
        builder.Property(x => x.Code).HasColumnName("code").HasMaxLength(5);
        builder.Property(x => x.Name).HasColumnName("name").HasMaxLength(80).IsRequired();
        builder.Property(x => x.ShortName).HasColumnName("short_name").HasMaxLength(20).IsRequired();
        builder.Property(x => x.IsElectronic).HasColumnName("is_electronic").HasDefaultValue(true);
        builder.Property(x => x.IsActive).HasColumnName("is_active").HasDefaultValue(true);

        builder.HasData(
            new SriDocType { Code = "01", Name = "FACTURA",                                                   ShortName = "FACTURA",    IsElectronic = true,  IsActive = true  },
            new SriDocType { Code = "02", Name = "NOTA DE VENTA- RISE",                                       ShortName = "NV_RISE",    IsElectronic = true,  IsActive = false },
            new SriDocType { Code = "03", Name = "Liquidación de Compra de Bienes y Prestación de Servicios", ShortName = "LIQ_COMPRA", IsElectronic = true,  IsActive = true  },
            new SriDocType { Code = "04", Name = "Nota de Crédito",                                           ShortName = "N_CREDITO",  IsElectronic = true,  IsActive = true  },
            new SriDocType { Code = "05", Name = "Nota de Débito",                                            ShortName = "N_DEBITO",   IsElectronic = true,  IsActive = true  },
            new SriDocType { Code = "06", Name = "Guía de Remisión",                                          ShortName = "G_REMISION", IsElectronic = true,  IsActive = true  },
            new SriDocType { Code = "07", Name = "Comprobante de Retención",                                  ShortName = "retention",  IsElectronic = true,  IsActive = true  },
            new SriDocType { Code = "08", Name = "Tiquete de Máquina Registradora",                           ShortName = "TIQUETE",    IsElectronic = false, IsActive = false },
            new SriDocType { Code = "09", Name = "Tiquete de Caja Registradora",                              ShortName = "CAJA_REG",   IsElectronic = false, IsActive = false },
            new SriDocType { Code = "18", Name = "Documento Electrónico de Importación",                      ShortName = "DEI",        IsElectronic = false, IsActive = false }
        );
    }
}

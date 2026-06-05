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
            new SriDocType { Code = "01", Name = "salesBill",                                                   ShortName = "salesBill",    IsElectronic = true,  IsActive = true  },
            new SriDocType { Code = "02", Name = "note de Venta - RISE",                                      ShortName = "NV_RISE",    IsElectronic = true,  IsActive = true  },
            new SriDocType { Code = "03", Name = "LiquidaciÃ³n de Compra de Bienes y PrestaciÃ³n de Servicios", ShortName = "LIQ_COMPRA", IsElectronic = true,  IsActive = true  },
            new SriDocType { Code = "04", Name = "note de CrÃ©dito",                                           ShortName = "N_CREDITO",  IsElectronic = true,  IsActive = true  },
            new SriDocType { Code = "05", Name = "note de DÃ©bito",                                            ShortName = "N_DEBITO",   IsElectronic = true,  IsActive = true  },
            new SriDocType { Code = "06", Name = "GuÃ­a de RemisiÃ³n",                                          ShortName = "G_REMISION", IsElectronic = true,  IsActive = true  },
            new SriDocType { Code = "07", Name = "Comprobante de RetenciÃ³n",                                  ShortName = "retention",  IsElectronic = true,  IsActive = true  },
            new SriDocType { Code = "08", Name = "Tiquete de MÃ¡quina Registradora",                           ShortName = "TIQUETE",    IsElectronic = false, IsActive = false },
            new SriDocType { Code = "09", Name = "Tiquete de Caja Registradora",                              ShortName = "CAJA_REG",   IsElectronic = false, IsActive = false },
            new SriDocType { Code = "18", Name = "Documento ElectrÃ³nico de ImportaciÃ³n",                      ShortName = "DEI",        IsElectronic = false, IsActive = false }
        );
    }
}

using ERP.Domain.Modules.SriCatalogs.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ERP.Infrastructure.Persistence.Configurations.SriCatalogs;

public class SriUomConfiguration : IEntityTypeConfiguration<SriUom>
{
    public void Configure(EntityTypeBuilder<SriUom> builder)
    {
        builder.ToTable("sri_uom");
        builder.HasKey(x => x.Code);
        builder.Property(x => x.Code).HasColumnName("code").HasMaxLength(10);
        builder.Property(x => x.Name).HasColumnName("name").HasMaxLength(80).IsRequired();
        builder.Property(x => x.Abbrev).HasColumnName("abbrev").HasMaxLength(15);
        builder.Property(x => x.IsActive).HasColumnName("is_active").HasDefaultValue(true);

        builder.HasData(
            new SriUom { Code = "01", Name = "Unidad Biológica",  Abbrev = "UB"    },
            new SriUom { Code = "02", Name = "Caja",              Abbrev = "CAJA"  },
            new SriUom { Code = "03", Name = "Decena",            Abbrev = "DEC"   },
            new SriUom { Code = "04", Name = "Docena (12 un.)",   Abbrev = "DOC"   },
            new SriUom { Code = "05", Name = "Fardo",             Abbrev = "FARDO" },
            new SriUom { Code = "06", Name = "Gramo",             Abbrev = "G"     },
            new SriUom { Code = "07", Name = "Kilogramo",         Abbrev = "KG"    },
            new SriUom { Code = "08", Name = "Libra",             Abbrev = "LB"    },
            new SriUom { Code = "09", Name = "Litro",             Abbrev = "LT"    },
            new SriUom { Code = "10", Name = "Metro",             Abbrev = "M"     },
            new SriUom { Code = "11", Name = "Metro cuadrado",    Abbrev = "M2"    },
            new SriUom { Code = "12", Name = "Metro cúbico",     Abbrev = "M3"    },
            new SriUom { Code = "13", Name = "Mililitro",         Abbrev = "ML"    },
            new SriUom { Code = "14", Name = "Paquete",           Abbrev = "PAQ"   },
            new SriUom { Code = "15", Name = "Par",               Abbrev = "PAR"   },
            new SriUom { Code = "16", Name = "Quintal",           Abbrev = "QQ"    },
            new SriUom { Code = "17", Name = "Rollo",             Abbrev = "ROLLO" },
            new SriUom { Code = "18", Name = "Tonelada",          Abbrev = "TON"   },
            new SriUom { Code = "19", Name = "Unidad",            Abbrev = "UN"    },
            new SriUom { Code = "20", Name = "Vehículo",         Abbrev = "VEH"   },
            new SriUom { Code = "21", Name = "Set",               Abbrev = "SET"   },
            new SriUom { Code = "22", Name = "Surtido",           Abbrev = "SURT"  }
        );
    }
}

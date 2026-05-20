using ERP.Domain.Modules.SriCatalogs.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ERP.Infrastructure.Persistence.Configurations.SriCatalogs;

public class SriCountryConfiguration : IEntityTypeConfiguration<SriCountry>
{
    public void Configure(EntityTypeBuilder<SriCountry> builder)
    {
        builder.ToTable("sri_country");
        builder.HasKey(x => x.Code);
        builder.Property(x => x.Code).HasColumnName("code").HasMaxLength(3).IsFixedLength();
        builder.Property(x => x.Iso2).HasColumnName("iso2").HasMaxLength(2).IsFixedLength().IsRequired();
        builder.HasAlternateKey(x => x.Iso2);
        builder.Property(x => x.Name).HasColumnName("name").HasMaxLength(100).IsRequired();
        builder.Property(x => x.PhoneCode).HasColumnName("phone_code").HasMaxLength(10);
        builder.Property(x => x.IsActive).HasColumnName("is_active").HasDefaultValue(true);

        builder.HasData(
            new SriCountry { Code = "ECU", Iso2 = "EC", Name = "ECUADOR",               PhoneCode = "+593" },
            new SriCountry { Code = "USA", Iso2 = "US", Name = "ESTADOS UNIDOS",         PhoneCode = "+1"   },
            new SriCountry { Code = "COL", Iso2 = "CO", Name = "COLOMBIA",               PhoneCode = "+57"  },
            new SriCountry { Code = "PER", Iso2 = "PE", Name = "PERÚ",                  PhoneCode = "+51"  },
            new SriCountry { Code = "BOL", Iso2 = "BO", Name = "BOLIVIA",               PhoneCode = "+591" },
            new SriCountry { Code = "CHL", Iso2 = "CL", Name = "CHILE",                 PhoneCode = "+56"  },
            new SriCountry { Code = "ARG", Iso2 = "AR", Name = "ARGENTINA",             PhoneCode = "+54"  },
            new SriCountry { Code = "BRA", Iso2 = "BR", Name = "BRASIL",               PhoneCode = "+55"  },
            new SriCountry { Code = "MEX", Iso2 = "MX", Name = "MÉXICO",              PhoneCode = "+52"  },
            new SriCountry { Code = "VEN", Iso2 = "VE", Name = "VENEZUELA",             PhoneCode = "+58"  },
            new SriCountry { Code = "PAN", Iso2 = "PA", Name = "PANAMÁ",              PhoneCode = "+507" },
            new SriCountry { Code = "CRI", Iso2 = "CR", Name = "COSTA RICA",           PhoneCode = "+506" },
            new SriCountry { Code = "GTM", Iso2 = "GT", Name = "GUATEMALA",             PhoneCode = "+502" },
            new SriCountry { Code = "HND", Iso2 = "HN", Name = "HONDURAS",             PhoneCode = "+504" },
            new SriCountry { Code = "NIC", Iso2 = "NI", Name = "NICARAGUA",             PhoneCode = "+505" },
            new SriCountry { Code = "SLV", Iso2 = "SV", Name = "EL SALVADOR",          PhoneCode = "+503" },
            new SriCountry { Code = "DOM", Iso2 = "DO", Name = "REPÚBLICA DOMINICANA", PhoneCode = "+1"   },
            new SriCountry { Code = "URY", Iso2 = "UY", Name = "URUGUAY",              PhoneCode = "+598" },
            new SriCountry { Code = "PRY", Iso2 = "PY", Name = "PARAGUAY",             PhoneCode = "+595" },
            new SriCountry { Code = "ESP", Iso2 = "ES", Name = "ESPAÑA",              PhoneCode = "+34"  },
            new SriCountry { Code = "DEU", Iso2 = "DE", Name = "ALEMANIA",             PhoneCode = "+49"  },
            new SriCountry { Code = "ITA", Iso2 = "IT", Name = "ITALIA",               PhoneCode = "+39"  },
            new SriCountry { Code = "FRA", Iso2 = "FR", Name = "FRANCIA",             PhoneCode = "+33"  },
            new SriCountry { Code = "GBR", Iso2 = "GB", Name = "REINO UNIDO",          PhoneCode = "+44"  },
            new SriCountry { Code = "JPN", Iso2 = "JP", Name = "JAPÓN",              PhoneCode = "+81"  },
            new SriCountry { Code = "CHN", Iso2 = "CN", Name = "CHINA",               PhoneCode = "+86"  },
            new SriCountry { Code = "IND", Iso2 = "IN", Name = "INDIA",               PhoneCode = "+91"  },
            new SriCountry { Code = "CAN", Iso2 = "CA", Name = "CANADÁ",             PhoneCode = "+1"   },
            new SriCountry { Code = "AUS", Iso2 = "AU", Name = "AUSTRALIA",           PhoneCode = "+61"  }
        );
    }
}

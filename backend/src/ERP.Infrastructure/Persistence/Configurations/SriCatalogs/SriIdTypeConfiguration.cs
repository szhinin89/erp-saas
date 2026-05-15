using ERP.Domain.Modules.SriCatalogs.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ERP.Infrastructure.Persistence.Configurations.SriCatalogs;

public class SriIdTypeConfiguration : IEntityTypeConfiguration<SriIdType>
{
    public void Configure(EntityTypeBuilder<SriIdType> builder)
    {
        builder.ToTable("sri_id_type");
        builder.HasKey(x => x.Code);
        builder.Property(x => x.Code).HasColumnName("code").HasMaxLength(5);
        builder.Property(x => x.Name).HasColumnName("name").HasMaxLength(70).IsRequired();
        builder.Property(x => x.Digits).HasColumnName("digits");

        builder.HasData(
            new SriIdType { Code = "04", Name = "Registro Único de Contribuyentes", Digits = 13   },
            new SriIdType { Code = "05", Name = "Cédula de ciudadanía",             Digits = 10   },
            new SriIdType { Code = "06", Name = "Pasaporte",                        Digits = null },
            new SriIdType { Code = "07", Name = "Consumidor Final",                 Digits = null },
            new SriIdType { Code = "08", Name = "Identificación del exterior",      Digits = null },
            new SriIdType { Code = "09", Name = "Placa",                            Digits = null }
        );
    }
}

using ERP.Domain.Modules.SriCatalogs.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ERP.Infrastructure.Persistence.Configurations.SriCatalogs;

public class SriEmissionTypeConfiguration : IEntityTypeConfiguration<SriEmissionType>
{
    public void Configure(EntityTypeBuilder<SriEmissionType> builder)
    {
        builder.ToTable("sri_emission_type", schema: "global");
        builder.HasKey(x => x.Code);
        builder.Property(x => x.Code).HasColumnName("code");
        builder.Property(x => x.Name).HasColumnName("name").HasMaxLength(60).IsRequired();

        builder.HasData(
            new SriEmissionType { Code = 1, Name = "EmisiÃ³n Normal" },
            new SriEmissionType { Code = 2, Name = "EmisiÃ³n por Indisponibilidad del Sistema" }
        );
    }
}

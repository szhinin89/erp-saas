using ERP.Domain.MasterData.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ERP.Infrastructure.Persistence.Configurations.MasterData;

public class PersonTypeCatalogConfiguration : IEntityTypeConfiguration<PersonTypeCatalog>
{
    public void Configure(EntityTypeBuilder<PersonTypeCatalog> builder)
    {
        builder.ToTable("person_type", schema: "global");
        builder.HasKey(x => x.Code);
        builder.Property(x => x.Code).HasColumnName("code");
        builder.Property(x => x.Name).HasColumnName("name").HasMaxLength(60).IsRequired();

        // Códigos alineados 1:1 con ERP.Domain.MasterData.Enums.PersonType — nunca reordenar.
        builder.HasData(
            new PersonTypeCatalog { Code = 1, Name = "Natural (persona física)" },
            new PersonTypeCatalog { Code = 2, Name = "Jurídica (empresa/sociedad)" },
            new PersonTypeCatalog { Code = 3, Name = "Gubernamental" },
            new PersonTypeCatalog { Code = 4, Name = "Organización / ONG" }
        );
    }
}

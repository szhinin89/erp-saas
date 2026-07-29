using ERP.Domain.Modules.SriCatalogs.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ERP.Infrastructure.Persistence.Configurations.SriCatalogs;

public class SriEnvironmentConfiguration : IEntityTypeConfiguration<SriEnvironment>
{
    public void Configure(EntityTypeBuilder<SriEnvironment> builder)
    {
        builder.ToTable("sri_environment", schema: "global");
        builder.HasKey(x => x.Code);
        builder.Property(x => x.Code).HasColumnName("code");
        builder.Property(x => x.Name).HasColumnName("name").HasMaxLength(25).IsRequired();
        builder.Property(x => x.Abbrev).HasColumnName("abbrev").HasMaxLength(6).IsRequired();

        // Códigos oficiales SRI (Ficha Técnica, Tabla 4 "Ambiente"): 1=Pruebas, 2=Producción —
        // verificado contra la ficha técnica publicada por el SRI. No invertir.
        builder.HasData(
            new SriEnvironment { Code = 1, Name = "Pruebas", Abbrev = "TEST" },
            new SriEnvironment { Code = 2, Name = "Producción", Abbrev = "PROD" }
        );
    }
}

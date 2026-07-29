using ERP.Domain.MasterData.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

/**
 * Configuración de EF Core para la entidad LegalEntityTypeCatalog.
 * Mapea la entidad a la tabla "legal_entity_type" en el esquema "global".
 * Define las propiedades, claves y datos iniciales (HasData) para el catálogo de tipos de entidad legal.
 */
public class LegalEntityTypeCatalogConfiguration: IEntityTypeConfiguration<LegalEntityTypeCatalog>
{
    public void Configure(EntityTypeBuilder<LegalEntityTypeCatalog> builder)
    {
        builder.ToTable("legal_entity_type", schema: "global");

        builder.HasKey(x => x.Code);

        builder.Property(x => x.Code)
            .HasColumnName("code");

        builder.Property(x => x.Name)
            .HasColumnName("name")
            .HasMaxLength(60)
            .IsRequired();

        builder.Property(x => x.SriTaxCategory)
            .HasColumnName("sri_tax_category")
            .HasMaxLength(30)
            .IsRequired();

        builder.Property(x => x.IsActive)
            .HasColumnName("is_active")
            .HasDefaultValue(true);


        builder.HasData(
            new LegalEntityTypeCatalog
            {
                Code = 1,
                Name = "Persona Natural",
                SriTaxCategory = "NATURAL",
                IsActive = true
            },
            new LegalEntityTypeCatalog
            {
                Code = 2,
                Name = "Sociedad Privada",
                SriTaxCategory = "PRIVATE",
                IsActive = true
            },
            new LegalEntityTypeCatalog
            {
                Code = 3,
                Name = "Institución Pública",
                SriTaxCategory = "PUBLIC",
                IsActive = true
            }
        );
    }
}

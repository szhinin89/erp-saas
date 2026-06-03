using ERP.Domain.Modules.SriCatalogs.Entities;
using ERP.Domain.Products.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ERP.Infrastructure.Persistence.Configurations;

public class TaxRateConfiguration : IEntityTypeConfiguration<TaxRate>
{
    public void Configure(EntityTypeBuilder<TaxRate> builder)
    {
        builder.ToTable("tax_rates");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.SubscriberId).HasColumnName("subscriber_id").IsRequired();

        builder.Property(x => x.Code).HasColumnName("code").HasMaxLength(20).IsRequired();
        builder.Property(x => x.Name).HasColumnName("name").HasMaxLength(120).IsRequired();
        builder.Property(x => x.Type).HasColumnName("type").IsRequired();

        // FKs a tablas globales — fuente única del porcentaje
        builder.Property(x => x.SriVatCode).HasColumnName("sri_vat_code").HasMaxLength(10);
        builder.Property(x => x.SriIceCode).HasColumnName("sri_ice_code").HasMaxLength(10);

        builder.HasOne(x => x.SriVatRate)
            .WithMany()
            .HasForeignKey(x => x.SriVatCode)
            .HasPrincipalKey(v => v.Code)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.SriIceRate)
            .WithMany()
            .HasForeignKey(x => x.SriIceCode)
            .HasPrincipalKey(v => v.Code)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.Restrict);

        // Percentage es computed desde navigation — no se persiste
        builder.Ignore(x => x.Percentage);

        builder.Property(x => x.IsActive).HasColumnName("is_active").IsRequired();
        builder.Property(x => x.CreatedAt).HasColumnName("created_at");
        builder.Property(x => x.UpdatedAt).HasColumnName("updated_at");
        builder.Property(x => x.CreatedBy).HasColumnName("created_by");
        builder.Property(x => x.UpdatedBy).HasColumnName("updated_by");

        builder.HasIndex(x => x.SubscriberId).HasDatabaseName("ix_tax_rates_subscriber_id");
        builder.HasIndex(x => new { x.SubscriberId, x.Type, x.Code })
            .IsUnique()
            .HasDatabaseName("ix_tax_rates_subscriber_type_code");
    }
}

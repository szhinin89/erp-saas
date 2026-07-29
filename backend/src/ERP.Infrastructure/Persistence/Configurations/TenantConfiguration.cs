using ERP.Domain.Tenants.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ERP.Infrastructure.Persistence.Configurations;

public class TenantConfiguration : IEntityTypeConfiguration<Tenant>
{
    public void Configure(EntityTypeBuilder<Tenant> builder)
    {
        builder.ToTable("tenants");

        builder.HasKey(t => t.Id).HasName("PK_tenants");
        builder.Property(t => t.Id).HasColumnName("id");
        builder.Property(t => t.Name).HasColumnName("name").HasMaxLength(200).IsRequired();
        builder.Property(t => t.Slug).HasColumnName("slug").HasMaxLength(100).IsRequired();
        builder.Property(t => t.IsActive).HasColumnName("is_active").IsRequired();
        builder
            .Property(t => t.PreferredLanguage)
            .HasColumnName("preferred_language")
            .HasMaxLength(10)
            .IsRequired()
            .HasDefaultValue("es");

        builder.Property(t => t.CreatedAt).HasColumnName("created_at");
        builder.Property(t => t.UpdatedAt).HasColumnName("updated_at");
        builder.Property(t => t.CreatedBy).HasColumnName("created_by");
        builder.Property(t => t.UpdatedBy).HasColumnName("updated_by");

        builder
            .Property<uint>("xmin")
            .HasColumnName("xmin")
            .HasColumnType("xid")
            .IsRequired()
            .ValueGeneratedOnAddOrUpdate()
            .IsConcurrencyToken();

        builder.HasIndex(t => t.Slug).IsUnique().HasDatabaseName("ix_tenants_slug");
    }
}

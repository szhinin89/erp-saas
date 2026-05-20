using ERP.Domain.Configuration.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ERP.Infrastructure.Persistence.Configurations;

public sealed class RetentionSettingsConfiguration : IEntityTypeConfiguration<RetentionSettings>
{
    public void Configure(EntityTypeBuilder<RetentionSettings> builder)
    {
        builder.ToTable("retention_settings");

        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasColumnName("id");
        builder.Property(e => e.SubscriberId).HasColumnName("subscriber_id").IsRequired();
        builder.Property(e => e.TaxType).HasColumnName("tax_type").HasMaxLength(RetentionSettings.TaxTypeMaxLen).IsRequired();
        builder.Property(e => e.SubjectType).HasColumnName("subject_type").HasMaxLength(RetentionSettings.SubjectTypeMaxLen).IsRequired();
        builder.Property(e => e.SriCode).HasColumnName("sri_code").HasMaxLength(RetentionSettings.SriCodeMaxLen).IsRequired();
        builder.Property(e => e.Percentage).HasColumnName("percentage").HasPrecision(18, 4).IsRequired();
        builder.Property(e => e.IsActive).HasColumnName("is_active").IsRequired();
        builder.Property(e => e.CreatedAt).HasColumnName("created_at");
        builder.Property(e => e.UpdatedAt).HasColumnName("updated_at");
        builder.Property(e => e.CreatedBy).HasColumnName("created_by");
        builder.Property(e => e.UpdatedBy).HasColumnName("updated_by");

        builder.HasIndex(e => new { e.SubscriberId, e.TaxType, e.SubjectType, e.SriCode }).IsUnique().HasDatabaseName("uq_retention_settings_subscriber_tax_subject_code");
    }
}

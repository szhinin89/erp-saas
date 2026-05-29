using ERP.Domain.Configuration.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ERP.Infrastructure.Persistence.Configurations;

public sealed class SriSettingsConfiguration : IEntityTypeConfiguration<SriSettings>
{
    public void Configure(EntityTypeBuilder<SriSettings> builder)
    {
        builder.ToTable("sri_settings");

        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasColumnName("id").IsRequired();
        builder.Property(e => e.SubscriberId).HasColumnName("subscriber_id").IsRequired();
        builder.Property(e => e.CompanyId).HasColumnName("company_id").IsRequired();
        builder.Property(e => e.CertP12Path).HasColumnName("cert_p12_path").HasMaxLength(SriSettings.CertPathMaxLen).IsRequired();
        builder.Property(e => e.CertPassword).HasColumnName("cert_password").HasMaxLength(SriSettings.CertPasswordMaxLen).IsRequired();
        builder.Property(e => e.Environment).HasColumnName("environment").IsRequired();
        builder.Property(e => e.EmissionType).HasColumnName("emission_type").IsRequired();
        builder.Property(e => e.WsdlUrl).HasColumnName("wsdl_url").HasMaxLength(SriSettings.WsdlUrlMaxLen).IsRequired();
        builder.Property(e => e.CreatedAt).HasColumnName("created_at");
        builder.Property(e => e.UpdatedAt).HasColumnName("updated_at");
        builder.Property(e => e.CreatedBy).HasColumnName("created_by");
        builder.Property(e => e.UpdatedBy).HasColumnName("updated_by");

        builder.HasIndex(e => e.CompanyId).IsUnique().HasDatabaseName("uq_sri_settings_company_id");
    }
}

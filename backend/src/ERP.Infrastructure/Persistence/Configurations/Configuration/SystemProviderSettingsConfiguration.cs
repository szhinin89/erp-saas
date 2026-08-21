using ERP.Domain.Configuration.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ERP.Infrastructure.Persistence.Configurations.Configuration;

public sealed class SystemProviderSettingsConfiguration
    : IEntityTypeConfiguration<SystemProviderSettings>
{
    public void Configure(EntityTypeBuilder<SystemProviderSettings> builder)
    {
        builder.ToTable("system_provider_settings");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id").ValueGeneratedNever();
        builder
            .Property(x => x.Ruc)
            .HasColumnName("ruc")
            .HasMaxLength(SystemProviderSettings.RucLength);
        builder
            .Property(x => x.LegalName)
            .HasColumnName("legal_name")
            .HasMaxLength(SystemProviderSettings.LegalNameMaxLen);
        builder
            .Property(x => x.CiiuCode)
            .HasColumnName("ciiu_code")
            .HasMaxLength(SystemProviderSettings.CiiuCodeMaxLen);
        builder
            .Property(x => x.Enabled)
            .HasColumnName("enabled")
            .HasDefaultValue(false)
            .IsRequired();
        builder.Property(x => x.EffectiveDate).HasColumnName("effective_date");
        builder.Property(x => x.UpdatedAtUtc).HasColumnName("updated_at_utc");
        builder.Property(x => x.UpdatedBy).HasColumnName("updated_by");
    }
}

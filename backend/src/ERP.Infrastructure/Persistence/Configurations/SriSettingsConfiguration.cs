using ERP.Domain.Configuration.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ERP.Infrastructure.Persistence.Configurations;

public sealed class SriSettingsConfiguration : IEntityTypeConfiguration<SriSettings>
{
    public void Configure(EntityTypeBuilder<SriSettings> builder)
    {
        builder.ToTable("sri_settings");

        builder.HasKey(e => e.TenantId);
        builder.Property(e => e.TenantId).HasColumnName("tenant_id").IsRequired();
        builder.Property(e => e.Ruc).HasColumnName("ruc").HasMaxLength(SriSettings.RucMaxLen).IsRequired();
        builder.Property(e => e.LegalName).HasColumnName("legal_name").HasMaxLength(SriSettings.LegalNameMaxLen).IsRequired();
        builder.Property(e => e.TradeName).HasColumnName("trade_name").HasMaxLength(SriSettings.TradeNameMaxLen);
        builder.Property(e => e.MainAddress).HasColumnName("main_address").HasMaxLength(SriSettings.AddressMaxLen).IsRequired();
        builder.Property(e => e.RequiresAccounting).HasColumnName("requires_accounting").IsRequired();
        builder.Property(e => e.SpecialTaxpayer).HasColumnName("special_taxpayer").HasMaxLength(SriSettings.SpecialTaxpayerMaxLen);
        builder.Property(e => e.EstabCode).HasColumnName("estab_code").HasMaxLength(SriSettings.EstabMaxLen).IsRequired();
        builder.Property(e => e.EmPointCode).HasColumnName("em_point_code").HasMaxLength(SriSettings.EmPointMaxLen).IsRequired();
        builder.Property(e => e.CurrentSequential).HasColumnName("current_sequential").IsRequired();
        builder.Property(e => e.CertP12Path).HasColumnName("cert_p12_path").HasMaxLength(SriSettings.CertPathMaxLen).IsRequired();
        builder.Property(e => e.CertPassword).HasColumnName("cert_password").HasMaxLength(SriSettings.CertPasswordMaxLen).IsRequired();
        builder.Property(e => e.Environment).HasColumnName("environment").IsRequired();
        builder.Property(e => e.EmissionType).HasColumnName("emission_type").IsRequired();
        builder.Property(e => e.WsdlUrl).HasColumnName("wsdl_url").HasMaxLength(SriSettings.WsdlUrlMaxLen).IsRequired();
        builder.Property(e => e.CreatedAt).HasColumnName("created_at");
        builder.Property(e => e.UpdatedAt).HasColumnName("updated_at");
        builder.Property(e => e.CreatedBy).HasColumnName("created_by");
        builder.Property(e => e.UpdatedBy).HasColumnName("updated_by");

        builder.HasIndex(e => e.Ruc).IsUnique().HasDatabaseName("uq_sri_settings_ruc");
    }
}

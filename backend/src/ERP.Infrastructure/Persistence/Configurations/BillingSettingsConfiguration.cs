using ERP.Domain.Configuration.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ERP.Infrastructure.Persistence.Configurations;

public sealed class BillingSettingsConfiguration : IEntityTypeConfiguration<BillingSettings>
{
    public void Configure(EntityTypeBuilder<BillingSettings> builder)
    {
        builder.ToTable("billing_settings");

        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasColumnName("id").IsRequired();
        builder.Property(e => e.SubscriberId).HasColumnName("subscriber_id").IsRequired();
        builder.Property(e => e.LegalName).HasColumnName("legal_name").HasMaxLength(BillingSettings.LegalNameMaxLen).IsRequired();
        builder.Property(e => e.TradeName).HasColumnName("trade_name").HasMaxLength(BillingSettings.TradeNameMaxLen).IsRequired();
        builder.Property(e => e.Ruc).HasColumnName("ruc").HasMaxLength(BillingSettings.RucMaxLen).IsRequired();
        builder.Property(e => e.MainAddress).HasColumnName("main_address").HasMaxLength(BillingSettings.AddressMaxLen).IsRequired();
        builder.Property(e => e.Phone).HasColumnName("phone").HasMaxLength(BillingSettings.PhoneMaxLen).IsRequired();
        builder.Property(e => e.Email).HasColumnName("email").HasMaxLength(BillingSettings.EmailMaxLen);
        builder.Property(e => e.RequiresAccounting).HasColumnName("requires_accounting").IsRequired();
        builder.Property(e => e.SpecialTaxpayer).HasColumnName("special_taxpayer").HasMaxLength(BillingSettings.SpecialTaxpayerMaxLen);
        builder.Property(e => e.LogoBase64).HasColumnName("logo_base64").HasMaxLength(BillingSettings.LogoBase64MaxLen);
        builder.Property(e => e.FooterText).HasColumnName("footer_text").HasMaxLength(BillingSettings.FooterTextMaxLen);
        builder.Property(e => e.ReceiptWidth).HasColumnName("receipt_width").IsRequired();
        builder.Property(e => e.CreatedAt).HasColumnName("created_at");
        builder.Property(e => e.UpdatedAt).HasColumnName("updated_at");
        builder.Property(e => e.CreatedBy).HasColumnName("created_by");
        builder.Property(e => e.UpdatedBy).HasColumnName("updated_by");

        builder.HasIndex(e => e.SubscriberId).IsUnique().HasDatabaseName("uq_billing_settings_subscriber");
    }
}

using ERP.Domain.Configuration.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ERP.Infrastructure.Persistence.Configurations;

public sealed class SubscriberBillingProfileConfiguration : IEntityTypeConfiguration<SubscriberBillingProfile>
{
    public void Configure(EntityTypeBuilder<SubscriberBillingProfile> builder)
    {
        builder.ToTable("subscriber_billing_profile");

        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasColumnName("id").IsRequired();
        builder.Property(e => e.SubscriberId).HasColumnName("subscriber_id").IsRequired();

        // ── Identificación fiscal SRI ──────────────────────────────
        builder.Property(e => e.IdentificationType)
            .HasColumnName("identification_type")
            .HasMaxLength(SubscriberBillingProfile.IdentificationTypeMaxLen)
            .IsRequired();
        builder.Property(e => e.IdentificationNumber)
            .HasColumnName("identification_number")
            .HasMaxLength(SubscriberBillingProfile.IdentificationNumberMaxLen)
            .IsRequired();

        // ── Datos legales ──────────────────────────────────────────
        builder.Property(e => e.LegalName)
            .HasColumnName("legal_name")
            .HasMaxLength(SubscriberBillingProfile.LegalNameMaxLen)
            .IsRequired();
        builder.Property(e => e.TradeName)
            .HasColumnName("trade_name")
            .HasMaxLength(SubscriberBillingProfile.TradeNameMaxLen);
        builder.Property(e => e.Address)
            .HasColumnName("address")
            .HasMaxLength(SubscriberBillingProfile.AddressMaxLen)
            .IsRequired();
        builder.Property(e => e.Phone)
            .HasColumnName("phone")
            .HasMaxLength(SubscriberBillingProfile.PhoneMaxLen);
        builder.Property(e => e.Email)
            .HasColumnName("email")
            .HasMaxLength(SubscriberBillingProfile.EmailMaxLen);
        builder.Property(e => e.Country)
            .HasColumnName("country")
            .HasMaxLength(SubscriberBillingProfile.CountryMaxLen)
            .IsRequired();
        builder.Property(e => e.City)
            .HasColumnName("city")
            .HasMaxLength(SubscriberBillingProfile.CityMaxLen);

        // ── Régimen fiscal SRI ─────────────────────────────────────
        builder.Property(e => e.RequiresAccounting).HasColumnName("requires_accounting").IsRequired();
        builder.Property(e => e.SpecialTaxpayer)
            .HasColumnName("special_taxpayer")
            .HasMaxLength(SubscriberBillingProfile.SpecialTaxpayerMaxLen);

        // ── Configuración de recibos ───────────────────────────────
        builder.Property(e => e.LogoBase64)
            .HasColumnName("logo_base64")
            .HasMaxLength(SubscriberBillingProfile.LogoBase64MaxLen);
        builder.Property(e => e.FooterText)
            .HasColumnName("footer_text")
            .HasMaxLength(SubscriberBillingProfile.FooterTextMaxLen);
        builder.Property(e => e.ReceiptWidth).HasColumnName("receipt_width").IsRequired();

        // ── Vínculo SaaS ───────────────────────────────────────────
        builder.Property(e => e.BusinessPartnerId).HasColumnName("business_partner_id");

        // ── Auditoría ──────────────────────────────────────────────
        builder.Property(e => e.CreatedAt).HasColumnName("created_at");
        builder.Property(e => e.UpdatedAt).HasColumnName("updated_at");
        builder.Property(e => e.CreatedBy).HasColumnName("created_by");
        builder.Property(e => e.UpdatedBy).HasColumnName("updated_by");

        builder.HasIndex(e => e.SubscriberId).IsUnique().HasDatabaseName("uq_subscriber_billing_profile_subscriber");
    }
}

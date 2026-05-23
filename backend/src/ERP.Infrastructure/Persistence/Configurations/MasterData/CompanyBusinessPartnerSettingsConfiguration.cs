using ERP.Domain.MasterData.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ERP.Infrastructure.Persistence.Configurations.MasterData;

public sealed class CompanyBusinessPartnerSettingsConfiguration
    : IEntityTypeConfiguration<CompanyBusinessPartnerSettings>
{
    public void Configure(EntityTypeBuilder<CompanyBusinessPartnerSettings> builder)
    {
        builder.ToTable("master_company_bp_settings");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.SubscriberId).HasColumnName("subscriber_id").IsRequired();
        builder.Property(x => x.CompanyId).HasColumnName("company_id").IsRequired();
        builder.Property(x => x.BusinessPartnerId).HasColumnName("business_partner_id").IsRequired();
        builder.Property(x => x.CreditLimit).HasColumnName("credit_limit").HasPrecision(18, 2);
        builder.Property(x => x.PaymentDays).HasColumnName("payment_days").HasDefaultValue((short)0);
        builder.Property(x => x.IsBlocked).HasColumnName("is_blocked").IsRequired().HasDefaultValue(false);
        builder.Property(x => x.PriceListId).HasColumnName("price_list_id");
        builder.Property(x => x.UpdatedAt).HasColumnName("updated_at");
        builder.Property(x => x.UpdatedBy).HasColumnName("updated_by");

        // Una sola configuración por (company, business_partner)
        builder.HasIndex(x => new { x.CompanyId, x.BusinessPartnerId })
               .IsUnique()
               .HasDatabaseName("uq_mcbps_company_bp");

        builder.HasIndex(x => new { x.SubscriberId, x.CompanyId })
               .HasDatabaseName("ix_mcbps_subscriber_company");
    }
}

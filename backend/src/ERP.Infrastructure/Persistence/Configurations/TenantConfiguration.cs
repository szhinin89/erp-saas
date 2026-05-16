using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ERP.Domain.Tenants.Entities;

namespace ERP.Infrastructure.Persistence.Configurations;

public class TenantConfiguration : IEntityTypeConfiguration<Tenant>
{
    public void Configure(EntityTypeBuilder<Tenant> builder)
    {
        builder.ToTable("tenants");

        builder.HasKey(t => t.Id);
        builder.Property(t => t.Id).HasColumnName("id");
        builder.Property(t => t.Name).HasColumnName("name").HasMaxLength(200).IsRequired();
        builder.Property(t => t.Slug).HasColumnName("slug").HasMaxLength(100).IsRequired();
        builder.Property(t => t.IsActive).HasColumnName("is_active").IsRequired();
        builder.Property(t => t.PasswordResetMode).HasColumnName("password_reset_mode").IsRequired();
        builder.Property(t => t.Ruc).HasColumnName("ruc").HasMaxLength(15);
        builder.Property(t => t.ShortName).HasColumnName("short_name").HasMaxLength(100);
        builder.Property(t => t.TradeName).HasColumnName("trade_name").HasMaxLength(120);
        builder.Property(t => t.Dinardap).HasColumnName("dinardap").HasMaxLength(20);
        builder.Property(t => t.LogoUrl).HasColumnName("logo_url").HasMaxLength(500);
        builder.Property(t => t.DisplayOrder).HasColumnName("display_order").IsRequired();
        builder.Property(t => t.Priority).HasColumnName("priority").IsRequired();
        builder.Property(t => t.ElectronicBillingTrialEnabled).HasColumnName("electronic_billing_trial_enabled").IsRequired().HasDefaultValue(false);
        builder.Property(t => t.PlanCode).HasColumnName("plan_code").HasMaxLength(64);

        // Parámetros operativos configurables por la empresa
        builder.Property(t => t.Currency).HasColumnName("currency").HasMaxLength(10).IsRequired().HasDefaultValue("USD");
        builder.Property(t => t.Language).HasColumnName("language").HasMaxLength(10).IsRequired().HasDefaultValue("es");
        builder.Property(t => t.Timezone).HasColumnName("timezone").HasMaxLength(100).IsRequired().HasDefaultValue("America/Guayaquil");
        builder.Property(t => t.InvoicePrefix).HasColumnName("invoice_prefix").HasMaxLength(20);
        builder.Property(t => t.DefaultCreditDays).HasColumnName("default_credit_days").IsRequired().HasDefaultValue(30);
        builder.Property(t => t.EnabledModulesJson).HasColumnName("enabled_modules").HasColumnType("text");
        builder.Property(t => t.CreatedAt).HasColumnName("created_at");
        builder.Property(t => t.UpdatedAt).HasColumnName("updated_at");
        builder.Property(t => t.CreatedBy).HasColumnName("created_by");
        builder.Property(t => t.UpdatedBy).HasColumnName("updated_by");

        builder.HasIndex(t => t.Slug)
            .IsUnique()
            .HasDatabaseName("ix_tenants_slug");
    }
}

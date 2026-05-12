using ERP.Domain.Subscriptions.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ERP.Infrastructure.Persistence.Configurations;

public sealed class SaasFeatureDefinitionConfiguration : IEntityTypeConfiguration<SaasFeatureDefinition>
{
    public void Configure(EntityTypeBuilder<SaasFeatureDefinition> builder)
    {
        builder.ToTable("saas_feature_definitions");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.Code).HasColumnName("code").HasMaxLength(SaasFeatureDefinition.CodeMaxLen).IsRequired();
        builder.Property(x => x.Name).HasColumnName("name").HasMaxLength(SaasFeatureDefinition.NameMaxLen).IsRequired();
        builder.Property(x => x.Description).HasColumnName("description").HasMaxLength(2000);
        builder.Property(x => x.IsMetered).HasColumnName("is_metered").IsRequired();
        builder.Property(x => x.Kind).HasColumnName("feature_kind").IsRequired();
        builder.Property(x => x.ResourceRef).HasColumnName("resource_ref").HasMaxLength(SaasFeatureDefinition.ResourceRefMaxLen);
        builder.HasIndex(x => x.Code).IsUnique().HasDatabaseName("ux_saas_feature_definitions_code");
    }
}

public sealed class SaasPlanConfiguration : IEntityTypeConfiguration<SaasPlan>
{
    public void Configure(EntityTypeBuilder<SaasPlan> builder)
    {
        builder.ToTable("saas_plans");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.Code).HasColumnName("code").HasMaxLength(SaasPlan.CodeMaxLen).IsRequired();
        builder.Property(x => x.Name).HasColumnName("name").HasMaxLength(SaasPlan.NameMaxLen).IsRequired();
        builder.Property(x => x.ShortLabel).HasColumnName("short_label").HasMaxLength(SaasPlan.ShortLabelMaxLen);
        builder.Property(x => x.IsActive).HasColumnName("is_active").IsRequired();
        builder.Property(x => x.PriceAmount).HasColumnName("price_amount").HasPrecision(18, 2).IsRequired();
        builder.Property(x => x.Currency).HasColumnName("currency").HasMaxLength(SaasPlan.CurrencyMaxLen).IsRequired();
        builder.Property(x => x.BillingCycle).HasColumnName("billing_cycle").HasMaxLength(SaasPlan.BillingCycleMaxLen).IsRequired();
        builder.Property(x => x.IsPubliclyVisible).HasColumnName("is_publicly_visible").IsRequired();
        builder.Property(x => x.IsRecommended).HasColumnName("is_recommended").IsRequired();
        builder.Property(x => x.SortOrder).HasColumnName("sort_order").IsRequired();
        builder.Property(x => x.ExternalBillingRef).HasColumnName("external_billing_ref").HasMaxLength(SaasPlan.ExternalBillingRefMaxLen);
        builder.Property(x => x.MenuConfigJson).HasColumnName("menu_config").HasColumnType("jsonb");
        builder.HasIndex(x => x.Code).IsUnique().HasDatabaseName("ux_saas_plans_code");
    }
}

public sealed class SaasPlanFeatureConfiguration : IEntityTypeConfiguration<SaasPlanFeature>
{
    public void Configure(EntityTypeBuilder<SaasPlanFeature> builder)
    {
        builder.ToTable("saas_plan_features");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.PlanId).HasColumnName("plan_id").IsRequired();
        builder.Property(x => x.FeatureId).HasColumnName("feature_id").IsRequired();
        builder.Property(x => x.IsIncluded).HasColumnName("is_included").IsRequired();
        builder.Property(x => x.LimitPerPeriod).HasColumnName("limit_per_period");
        builder.HasIndex(x => new { x.PlanId, x.FeatureId }).IsUnique().HasDatabaseName("ux_saas_plan_features_plan_feature");
    }
}

public sealed class TenantSaasSubscriptionConfiguration : IEntityTypeConfiguration<TenantSaasSubscription>
{
    public void Configure(EntityTypeBuilder<TenantSaasSubscription> builder)
    {
        builder.ToTable("tenant_saas_subscriptions");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.TenantId).HasColumnName("tenant_id").IsRequired();
        builder.Property(x => x.PlanId).HasColumnName("plan_id").IsRequired();
        builder.Property(x => x.Status).HasColumnName("status").IsRequired();
        builder.Property(x => x.StartedAtUtc).HasColumnName("started_at_utc").IsRequired();
        builder.Property(x => x.CurrentPeriodEndUtc).HasColumnName("current_period_end_utc");
        builder.Property(x => x.CreatedAt).HasColumnName("created_at");
        builder.Property(x => x.UpdatedAt).HasColumnName("updated_at");
        builder.Property(x => x.CreatedBy).HasColumnName("created_by");
        builder.Property(x => x.UpdatedBy).HasColumnName("updated_by");
        builder.HasIndex(x => x.TenantId).IsUnique().HasDatabaseName("ux_tenant_saas_subscriptions_tenant");
    }
}

public sealed class TenantSubscriptionFeatureOverrideConfiguration : IEntityTypeConfiguration<TenantSubscriptionFeatureOverride>
{
    public void Configure(EntityTypeBuilder<TenantSubscriptionFeatureOverride> builder)
    {
        builder.ToTable("tenant_subscription_feature_overrides");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.TenantId).HasColumnName("tenant_id").IsRequired();
        builder.Property(x => x.SubscriptionId).HasColumnName("subscription_id").IsRequired();
        builder.Property(x => x.FeatureId).HasColumnName("feature_id").IsRequired();
        builder.Property(x => x.IsEnabled).HasColumnName("is_enabled").IsRequired();
        builder.Property(x => x.LimitOverridePerPeriod).HasColumnName("limit_override_per_period");
        builder.Property(x => x.CreatedAt).HasColumnName("created_at");
        builder.Property(x => x.UpdatedAt).HasColumnName("updated_at");
        builder.Property(x => x.CreatedBy).HasColumnName("created_by");
        builder.Property(x => x.UpdatedBy).HasColumnName("updated_by");
        builder.HasIndex(x => new { x.SubscriptionId, x.FeatureId }).IsUnique().HasDatabaseName("ux_tenant_sub_feat_override_sub_feature");
    }
}

public sealed class TenantSubscriptionUsageConfiguration : IEntityTypeConfiguration<TenantSubscriptionUsage>
{
    public void Configure(EntityTypeBuilder<TenantSubscriptionUsage> builder)
    {
        builder.ToTable("tenant_subscription_usages");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.TenantId).HasColumnName("tenant_id").IsRequired();
        builder.Property(x => x.FeatureId).HasColumnName("feature_id").IsRequired();
        builder.Property(x => x.PeriodKey).HasColumnName("period_key").HasMaxLength(32).IsRequired();
        builder.Property(x => x.Quantity).HasColumnName("quantity").IsRequired();
        builder.Property(x => x.CreatedAt).HasColumnName("created_at");
        builder.Property(x => x.UpdatedAt).HasColumnName("updated_at");
        builder.Property(x => x.CreatedBy).HasColumnName("created_by");
        builder.Property(x => x.UpdatedBy).HasColumnName("updated_by");
        builder.HasIndex(x => new { x.TenantId, x.FeatureId, x.PeriodKey }).IsUnique()
            .HasDatabaseName("ux_tenant_subscription_usages_period");
    }
}

using ERP.Domain.Subscriptions.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ERP.Infrastructure.Persistence.Configurations;

public sealed class PlatformFeatureConfiguration : IEntityTypeConfiguration<PlatformFeature>
{
    public void Configure(EntityTypeBuilder<PlatformFeature> builder)
    {
        builder.ToTable("platform_features");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.Code).HasColumnName("code").HasMaxLength(PlatformFeature.CodeMaxLen).IsRequired();
        builder.Property(x => x.Name).HasColumnName("name").HasMaxLength(PlatformFeature.NameMaxLen).IsRequired();
        builder.Property(x => x.Description).HasColumnName("description").HasMaxLength(2000);
        builder.Property(x => x.IsMetered).HasColumnName("is_metered").IsRequired();
        builder.Property(x => x.Kind).HasColumnName("feature_kind").IsRequired();
        builder.Property(x => x.ResourceRef).HasColumnName("resource_ref").HasMaxLength(PlatformFeature.ResourceRefMaxLen);
        builder.HasIndex(x => x.Code).IsUnique().HasDatabaseName("ux_platform_features_code");
    }
}

public sealed class CommercialPlanConfiguration : IEntityTypeConfiguration<CommercialPlan>
{
    public void Configure(EntityTypeBuilder<CommercialPlan> builder)
    {
        builder.ToTable("commercial_plans");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.Code).HasColumnName("code").HasMaxLength(CommercialPlan.CodeMaxLen).IsRequired();
        builder.Property(x => x.Name).HasColumnName("name").HasMaxLength(CommercialPlan.NameMaxLen).IsRequired();
        builder.Property(x => x.ShortLabel).HasColumnName("short_label").HasMaxLength(CommercialPlan.ShortLabelMaxLen);
        builder.Property(x => x.IsActive).HasColumnName("is_active").IsRequired();
        builder.Property(x => x.PriceAmount).HasColumnName("price_amount").HasPrecision(18, 2).IsRequired();
        builder.Property(x => x.Currency).HasColumnName("currency").HasMaxLength(CommercialPlan.CurrencyMaxLen).IsRequired();
        builder.Property(x => x.BillingCycle).HasColumnName("billing_cycle").HasMaxLength(CommercialPlan.BillingCycleMaxLen).IsRequired();
        builder.Property(x => x.IsPubliclyVisible).HasColumnName("is_publicly_visible").IsRequired();
        builder.Property(x => x.IsRecommended).HasColumnName("is_recommended").IsRequired();
        builder.Property(x => x.SortOrder).HasColumnName("sort_order").IsRequired();
        builder.Property(x => x.ExternalBillingRef).HasColumnName("external_billing_ref").HasMaxLength(CommercialPlan.ExternalBillingRefMaxLen);
        builder.Property(x => x.MenuConfigJson).HasColumnName("menu_config").HasColumnType("jsonb");
        builder.Property(x => x.MenuSidebarLayout)
            .HasColumnName("menu_sidebar_layout")
            .HasMaxLength(CommercialPlan.MenuSidebarLayoutMaxLen)
            .IsRequired()
            .HasDefaultValue(CommercialPlan.MenuSidebarLayoutHorizontal);
        builder.HasIndex(x => x.Code).IsUnique().HasDatabaseName("ux_commercial_plans_code");
    }
}

public sealed class CommercialPlanFeatureConfiguration : IEntityTypeConfiguration<CommercialPlanFeature>
{
    public void Configure(EntityTypeBuilder<CommercialPlanFeature> builder)
    {
        builder.ToTable("commercial_plan_features");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.PlanId).HasColumnName("plan_id").IsRequired();
        builder.Property(x => x.FeatureId).HasColumnName("feature_id").IsRequired();
        builder.Property(x => x.IsIncluded).HasColumnName("is_included").IsRequired();
        builder.Property(x => x.LimitPerPeriod).HasColumnName("limit_per_period");
        builder.HasIndex(x => new { x.PlanId, x.FeatureId }).IsUnique().HasDatabaseName("ux_commercial_plan_features_plan_feature");
    }
}

public sealed class SubscriberSubscriptionConfiguration : IEntityTypeConfiguration<SubscriberSubscription>
{
    public void Configure(EntityTypeBuilder<SubscriberSubscription> builder)
    {
        builder.ToTable("subscriber_subscriptions");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.SubscriberId).HasColumnName("subscriber_id").IsRequired();
        builder.Property(x => x.PlanId).HasColumnName("plan_id").IsRequired();
        builder.Property(x => x.Status).HasColumnName("status").IsRequired();
        builder.Property(x => x.StartedAtUtc).HasColumnName("started_at_utc").IsRequired();
        builder.Property(x => x.CurrentPeriodEndUtc).HasColumnName("current_period_end_utc");
        builder.Property(x => x.CreatedAt).HasColumnName("created_at");
        builder.Property(x => x.UpdatedAt).HasColumnName("updated_at");
        builder.Property(x => x.CreatedBy).HasColumnName("created_by");
        builder.Property(x => x.UpdatedBy).HasColumnName("updated_by");
        builder.HasIndex(x => x.SubscriberId).IsUnique().HasDatabaseName("ux_subscriber_subscriptions_subscriber");
    }
}

public sealed class SubscriptionFeatureOverrideConfiguration : IEntityTypeConfiguration<SubscriptionFeatureOverride>
{
    public void Configure(EntityTypeBuilder<SubscriptionFeatureOverride> builder)
    {
        builder.ToTable("subscription_feature_overrides");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.SubscriberId).HasColumnName("subscriber_id").IsRequired();
        builder.Property(x => x.SubscriptionId).HasColumnName("subscription_id").IsRequired();
        builder.Property(x => x.FeatureId).HasColumnName("feature_id").IsRequired();
        builder.Property(x => x.IsEnabled).HasColumnName("is_enabled").IsRequired();
        builder.Property(x => x.LimitOverridePerPeriod).HasColumnName("limit_override_per_period");
        builder.Property(x => x.CreatedAt).HasColumnName("created_at");
        builder.Property(x => x.UpdatedAt).HasColumnName("updated_at");
        builder.Property(x => x.CreatedBy).HasColumnName("created_by");
        builder.Property(x => x.UpdatedBy).HasColumnName("updated_by");
        builder.HasIndex(x => new { x.SubscriptionId, x.FeatureId }).IsUnique().HasDatabaseName("ux_subscription_feature_override_sub_feature");
    }
}

public sealed class SubscriberSubscriptionEventConfiguration : IEntityTypeConfiguration<SubscriberSubscriptionEvent>
{
    public void Configure(EntityTypeBuilder<SubscriberSubscriptionEvent> builder)
    {
        builder.ToTable("subscriber_subscription_events");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.SubscriberId).HasColumnName("subscriber_id").IsRequired();
        builder.Property(x => x.SubscriptionId).HasColumnName("subscription_id");
        builder.Property(x => x.EventType).HasColumnName("event_type").HasMaxLength(64).IsRequired();
        builder.Property(x => x.PreviousPlanId).HasColumnName("previous_plan_id");
        builder.Property(x => x.NewPlanId).HasColumnName("new_plan_id");
        builder.Property(x => x.MetadataJson).HasColumnName("metadata_json").HasColumnType("jsonb");
        builder.Property(x => x.OccurredAtUtc).HasColumnName("occurred_at_utc").IsRequired();
        builder.Property(x => x.CreatedAt).HasColumnName("created_at");
        builder.Property(x => x.UpdatedAt).HasColumnName("updated_at");
        builder.Property(x => x.CreatedBy).HasColumnName("created_by");
        builder.Property(x => x.UpdatedBy).HasColumnName("updated_by");
        builder.HasIndex(x => new { x.SubscriberId, x.OccurredAtUtc })
            .HasDatabaseName("ix_subscriber_subscription_events_subscriber_occurred");
    }
}

public sealed class SubscriptionUsageConfiguration : IEntityTypeConfiguration<SubscriptionUsage>
{
    public void Configure(EntityTypeBuilder<SubscriptionUsage> builder)
    {
        builder.ToTable("subscription_usages");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.SubscriberId).HasColumnName("subscriber_id").IsRequired();
        builder.Property(x => x.FeatureId).HasColumnName("feature_id").IsRequired();
        builder.Property(x => x.PeriodKey).HasColumnName("period_key").HasMaxLength(32).IsRequired();
        builder.Property(x => x.Quantity).HasColumnName("quantity").IsRequired();
        builder.Property(x => x.CreatedAt).HasColumnName("created_at");
        builder.Property(x => x.UpdatedAt).HasColumnName("updated_at");
        builder.Property(x => x.CreatedBy).HasColumnName("created_by");
        builder.Property(x => x.UpdatedBy).HasColumnName("updated_by");
        builder.HasIndex(x => new { x.SubscriberId, x.FeatureId, x.PeriodKey }).IsUnique()
            .HasDatabaseName("ux_subscription_usages_period");
    }
}

public sealed class CommercialPlanLimitConfiguration : IEntityTypeConfiguration<CommercialPlanLimit>
{
    public void Configure(EntityTypeBuilder<CommercialPlanLimit> builder)
    {
        builder.ToTable("commercial_plan_limits");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.CommercialPlanId).HasColumnName("commercial_plan_id").IsRequired();
        builder.Property(x => x.LimitCode).HasColumnName("limit_code").HasMaxLength(CommercialPlanLimit.CodeMaxLen).IsRequired();
        builder.Property(x => x.LimitValue).HasColumnName("limit_value").IsRequired();
        builder.Property(x => x.PeriodType).HasColumnName("period_type").HasMaxLength(CommercialPlanLimit.PeriodTypeMaxLen).IsRequired();
        builder.Property(x => x.IsHardLimit).HasColumnName("is_hard_limit").IsRequired().HasDefaultValue(true);
        builder.HasIndex(x => new { x.CommercialPlanId, x.LimitCode })
            .IsUnique()
            .HasDatabaseName("ux_commercial_plan_limits_plan_code");
        builder.HasOne<CommercialPlan>()
            .WithMany()
            .HasForeignKey(x => x.CommercialPlanId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

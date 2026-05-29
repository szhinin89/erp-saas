using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ERP.Domain.Subscribers.Entities;

namespace ERP.Infrastructure.Persistence.Configurations;

public class SubscriberConfiguration : IEntityTypeConfiguration<Subscriber>
{
    public void Configure(EntityTypeBuilder<Subscriber> builder)
    {
        builder.ToTable("subscribers");

        // Root tenant aggregate: Id is the subscriber key — no subscriber_id column on this table.
        builder.Ignore(t => t.SubscriberId);

        builder.HasKey(t => t.Id).HasName("PK_subscribers");
        builder.Property(t => t.Id).HasColumnName("id");
        builder.Property(t => t.Name).HasColumnName("name").HasMaxLength(200).IsRequired();
        builder.Property(t => t.Slug).HasColumnName("slug").HasMaxLength(100).IsRequired();
        builder.Property(t => t.IsActive).HasColumnName("is_active").IsRequired();
        builder.Property(t => t.PasswordResetMode).HasColumnName("password_reset_mode").IsRequired();
        builder.Property(t => t.DisplayOrder).HasColumnName("display_order").IsRequired();
        builder.Property(t => t.Priority).HasColumnName("priority").IsRequired();
        builder.Property(t => t.PlanCode).HasColumnName("plan_code").HasMaxLength(64);
        builder.Property(t => t.PreferredLanguage).HasColumnName("preferred_language").HasMaxLength(10).IsRequired().HasDefaultValue("es");

        // Lifecycle (control plane)
        builder.Property(t => t.LifecycleStatus).HasColumnName("lifecycle_status").IsRequired().HasDefaultValue(SubscriberLifecycleStatus.Active);
        builder.Property(t => t.SuspendedReason).HasColumnName("suspended_reason").HasMaxLength(500);
        builder.Property(t => t.SuspendedAtUtc).HasColumnName("suspended_at_utc");

        builder.Ignore(t => t.IsOperational);

        builder.Property(t => t.CreatedAt).HasColumnName("created_at");
        builder.Property(t => t.UpdatedAt).HasColumnName("updated_at");
        builder.Property(t => t.CreatedBy).HasColumnName("created_by");
        builder.Property(t => t.UpdatedBy).HasColumnName("updated_by");

        // Optimistic concurrency: if two handlers update simultaneously, the second fails
        builder.Property(t => t.UpdatedAt).IsConcurrencyToken();

        builder.HasIndex(t => t.Slug)
            .IsUnique()
            .HasDatabaseName("ix_subscribers_slug");
    }
}

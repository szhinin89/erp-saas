using ERP.Domain.Platform.Audit.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ERP.Infrastructure.Persistence.Configurations.Platform;

public sealed class PlatformAuditLogConfiguration : IEntityTypeConfiguration<PlatformAuditLog>
{
    public void Configure(EntityTypeBuilder<PlatformAuditLog> builder)
    {
        builder.ToTable("platform_audit_logs");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id");

        builder.Property(x => x.ActorUserId).HasColumnName("actor_user_id");
        builder.Property(x => x.ActorEmail)
            .HasColumnName("actor_email")
            .HasMaxLength(PlatformAuditLog.ActorEmailMaxLen);

        builder.Property(x => x.Action)
            .HasColumnName("action")
            .HasMaxLength(PlatformAuditLog.ActionMaxLen)
            .IsRequired();

        builder.Property(x => x.TargetSubscriberId).HasColumnName("target_subscriber_id");

        builder.Property(x => x.ResourceType)
            .HasColumnName("resource_type")
            .HasMaxLength(PlatformAuditLog.ResourceTypeMaxLen);

        builder.Property(x => x.ResourceId).HasColumnName("resource_id");

        builder.Property(x => x.OldValueJson)
            .HasColumnName("old_value_json")
            .HasColumnType("jsonb");

        builder.Property(x => x.NewValueJson)
            .HasColumnName("new_value_json")
            .HasColumnType("jsonb");

        builder.Property(x => x.IpAddress)
            .HasColumnName("ip_address")
            .HasMaxLength(PlatformAuditLog.IpAddressMaxLen);

        builder.Property(x => x.CorrelationId)
            .HasColumnName("correlation_id")
            .HasMaxLength(PlatformAuditLog.CorrelationIdMaxLen);

        builder.Property(x => x.Notes)
            .HasColumnName("notes")
            .HasColumnType("text");

        builder.Property(x => x.CreatedAtUtc)
            .HasColumnName("created_at_utc")
            .IsRequired();

        // Optimized indexes for audit queries
        builder.HasIndex(x => x.CreatedAtUtc)
            .HasDatabaseName("ix_platform_audit_created_at");

        builder.HasIndex(x => x.TargetSubscriberId)
            .HasDatabaseName("ix_platform_audit_target_subscriber");

        builder.HasIndex(x => x.ActorUserId)
            .HasDatabaseName("ix_platform_audit_actor_user");

        builder.HasIndex(x => x.Action)
            .HasDatabaseName("ix_platform_audit_action");
    }
}

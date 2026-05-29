using ERP.Domain.Platform.Provisioning;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ERP.Infrastructure.Persistence.Configurations.Platform;

public sealed class PlatformProvisioningLockConfiguration : IEntityTypeConfiguration<PlatformProvisioningLock>
{
    public void Configure(EntityTypeBuilder<PlatformProvisioningLock> builder)
    {
        builder.ToTable("platform_provisioning_lock");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id)
            .HasColumnName("id");
        builder.Property(x => x.IsLocked)
            .HasColumnName("is_locked")
            .IsRequired();
        builder.Property(x => x.LockedAtUtc)
            .HasColumnName("locked_at_utc");
        builder.Property(x => x.LockedByInstance)
            .HasColumnName("locked_by_instance")
            .HasMaxLength(200);
        builder.Property(x => x.ExpiresAtUtc)
            .HasColumnName("expires_at_utc");
        builder.Property(x => x.UpdatedAt)
            .HasColumnName("updated_at")
            .IsRequired();
    }
}

public sealed class PlatformProvisioningAuditEventConfiguration
    : IEntityTypeConfiguration<PlatformProvisioningAuditEvent>
{
    public void Configure(EntityTypeBuilder<PlatformProvisioningAuditEvent> builder)
    {
        builder.ToTable("platform_provisioning_audit");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id)
            .HasColumnName("id");
        builder.Property(x => x.EventType)
            .HasColumnName("event_type")
            .IsRequired();
        builder.Property(x => x.TimestampUtc)
            .HasColumnName("timestamp_utc")
            .IsRequired();
        builder.Property(x => x.SubscriberId)
            .HasColumnName("subscriber_id");
        builder.Property(x => x.CompanyId)
            .HasColumnName("company_id");
        builder.Property(x => x.OperatorUserId)
            .HasColumnName("operator_user_id");
        builder.Property(x => x.InstanceId)
            .HasColumnName("instance_id")
            .HasMaxLength(PlatformProvisioningAuditEvent.InstanceIdMaxLen);
        builder.Property(x => x.Metadata)
            .HasColumnName("metadata")
            .HasColumnType("jsonb")
            .HasMaxLength(PlatformProvisioningAuditEvent.MetadataMaxLen);
        builder.Property(x => x.IsSuccess)
            .HasColumnName("is_success")
            .IsRequired();
        builder.Property(x => x.ErrorMessage)
            .HasColumnName("error_message")
            .HasMaxLength(PlatformProvisioningAuditEvent.ErrorMessageMaxLen);
        builder.Property(x => x.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        builder.HasIndex(x => x.TimestampUtc).HasDatabaseName("ix_platform_provisioning_audit_timestamp");
        builder.HasIndex(x => x.EventType).HasDatabaseName("ix_platform_provisioning_audit_event_type");
    }
}

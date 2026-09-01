using ERP.Domain.Modules.Communications.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ERP.Infrastructure.Persistence.Configurations.Communications;

public sealed class CommunicationOutboxConfiguration : IEntityTypeConfiguration<CommunicationOutbox>
{
    public void Configure(EntityTypeBuilder<CommunicationOutbox> builder)
    {
        builder.ToTable("communication_outbox");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id").IsRequired();
        builder.Property(x => x.TenantId).HasColumnName("tenant_id").IsRequired();
        builder.Property(x => x.CompanyId).HasColumnName("company_id").IsRequired();
        builder.Property(x => x.BranchId).HasColumnName("branch_id");
        builder.Property(x => x.Channel).HasColumnName("channel").HasConversion<string>().HasMaxLength(30).IsRequired();
        builder.Property(x => x.Purpose).HasColumnName("purpose").HasMaxLength(CommunicationOutbox.PurposeMaxLen).IsRequired();
        builder.Property(x => x.RecipientName).HasColumnName("recipient_name").HasMaxLength(CommunicationOutbox.RecipientNameMaxLen);
        builder.Property(x => x.RecipientEmail).HasColumnName("recipient_email").HasMaxLength(CommunicationOutbox.RecipientEmailMaxLen);
        builder.Property(x => x.RecipientPhone).HasColumnName("recipient_phone").HasMaxLength(CommunicationOutbox.RecipientPhoneMaxLen);
        builder.Property(x => x.Subject).HasColumnName("subject").HasMaxLength(CommunicationOutbox.SubjectMaxLen).IsRequired();
        builder.Property(x => x.BodyHtml).HasColumnName("body_html").HasMaxLength(CommunicationOutbox.BodyMaxLen);
        builder.Property(x => x.BodyText).HasColumnName("body_text").HasMaxLength(CommunicationOutbox.BodyMaxLen);
        builder.Property(x => x.Status).HasColumnName("status").HasConversion<string>().HasMaxLength(30).IsRequired();
        builder.Property(x => x.Priority).HasColumnName("priority").HasConversion<string>().HasMaxLength(30).IsRequired();
        builder.Property(x => x.ScheduledAtUtc).HasColumnName("scheduled_at_utc").IsRequired();
        builder.Property(x => x.NextAttemptAtUtc).HasColumnName("next_attempt_at_utc");
        builder.Property(x => x.ProcessingStartedAtUtc).HasColumnName("processing_started_at_utc");
        builder.Property(x => x.SentAtUtc).HasColumnName("sent_at_utc");
        builder.Property(x => x.FailedAtUtc).HasColumnName("failed_at_utc");
        builder.Property(x => x.RetryCount).HasColumnName("retry_count").IsRequired();
        builder.Property(x => x.MaxRetries).HasColumnName("max_retries").IsRequired();
        builder.Property(x => x.LastError).HasColumnName("last_error").HasMaxLength(CommunicationOutbox.LastErrorMaxLen);
        builder.Property(x => x.CorrelationType).HasColumnName("correlation_type").HasMaxLength(CommunicationOutbox.CorrelationTypeMaxLen);
        builder.Property(x => x.CorrelationId).HasColumnName("correlation_id");
        builder.Property(x => x.IdempotencyKey).HasColumnName("idempotency_key").HasMaxLength(CommunicationOutbox.IdempotencyKeyMaxLen);
        builder.Property(x => x.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(x => x.UpdatedAt).HasColumnName("updated_at");
        builder.Property(x => x.CreatedBy).HasColumnName("created_by").IsRequired();
        builder.Property(x => x.UpdatedBy).HasColumnName("updated_by");

        builder.HasMany(x => x.Attachments)
            .WithOne()
            .HasForeignKey(x => x.CommunicationOutboxId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(x => x.Attachments).UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.HasIndex(x => new { x.TenantId, x.CompanyId, x.Status, x.ScheduledAtUtc, x.NextAttemptAtUtc })
            .HasDatabaseName("ix_communication_outbox_due");
        builder.HasIndex(x => new { x.TenantId, x.CompanyId, x.CorrelationType, x.CorrelationId, x.Purpose, x.RecipientEmail })
            .HasDatabaseName("ix_communication_outbox_correlation");
        builder.HasIndex(x => new { x.TenantId, x.CompanyId, x.IdempotencyKey })
            .IsUnique()
            .HasFilter("idempotency_key IS NOT NULL")
            .HasDatabaseName("ux_communication_outbox_idempotency");
    }
}

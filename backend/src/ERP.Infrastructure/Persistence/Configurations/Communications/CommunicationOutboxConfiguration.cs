using ERP.Domain.Modules.Communications.Entities;
using ERP.Domain.Modules.Communications.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ERP.Infrastructure.Persistence.Configurations.Communications;

public sealed class CommunicationOutboxConfiguration : IEntityTypeConfiguration<CommunicationOutbox>
{
    /// <summary>
    /// ZH-COMMUNICATIONS-CONTRACT-01 (ADR-039 D3) — invariante de alcance en la BD: Company exige
    /// tenant y empresa; System no tiene tenant, empresa ni sucursal (NULL, nunca un centinela).
    /// </summary>
    public const string ScopeCheckConstraint = "ck_communication_outbox_scope";

    public void Configure(EntityTypeBuilder<CommunicationOutbox> builder)
    {
        builder.ToTable(
            "communication_outbox",
            t => t.HasCheckConstraint(
                ScopeCheckConstraint,
                "(scope_kind = 'Company' AND tenant_id IS NOT NULL AND company_id IS NOT NULL) "
                    + "OR (scope_kind = 'System' AND tenant_id IS NULL AND company_id IS NULL AND branch_id IS NULL)"
            )
        );

        builder.HasKey(x => x.Id);
        builder.Ignore(x => x.Scope);
        builder.Ignore(x => x.DomainEvents);
        builder.Property(x => x.Id).HasColumnName("id").IsRequired();
        builder.Property(x => x.ScopeKind).HasColumnName("scope_kind").HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(x => x.TenantId).HasColumnName("tenant_id");
        builder.Property(x => x.CompanyId).HasColumnName("company_id");
        builder.Property(x => x.BranchId).HasColumnName("branch_id");
        builder.Property(x => x.Channel).HasColumnName("channel").HasConversion<string>().HasMaxLength(30).IsRequired();
        builder.Property(x => x.Purpose).HasColumnName("purpose").HasMaxLength(CommunicationOutbox.PurposeMaxLen).IsRequired();
        builder.Property(x => x.SourceModule).HasColumnName("source_module").HasMaxLength(CommunicationSource.ModuleMaxLen);
        builder.Property(x => x.SourceType).HasColumnName("source_type").HasMaxLength(CommunicationSource.TypeMaxLen);
        builder.Property(x => x.SourceId).HasColumnName("source_id");
        builder.Property(x => x.RecipientRole).HasColumnName("recipient_role").HasConversion<string>().HasMaxLength(30);
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
        builder.Property(x => x.IdempotencyKey).HasColumnName("idempotency_key").HasMaxLength(CommunicationOutbox.IdempotencyKeyMaxLen);
        builder.Property(x => x.ResendOfCommunicationId).HasColumnName("resend_of_communication_id");
        builder.Property(x => x.ResendSequence).HasColumnName("resend_sequence").IsRequired();
        builder.Property(x => x.ClaimToken).HasColumnName("claim_token");
        builder.Property(x => x.LeaseUntilUtc).HasColumnName("lease_until_utc");
        builder.Property(x => x.FailureCategory).HasColumnName("failure_category").HasConversion<string>().HasMaxLength(30);
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
        // ZH-COMMUNICATIONS-DELIVERY-HARDENING-01 — el claim es cross-tenant (no filtra por
        // tenant/company): índice parcial solo sobre filas reclamables (Pending o Processing), que
        // son una fracción mínima de la tabla (Sent/Failed/Cancelled quedan fuera).
        builder.HasIndex(x => new { x.Status, x.ScheduledAtUtc })
            .HasFilter("status IN ('Pending', 'Processing')")
            .HasDatabaseName("ix_communication_outbox_claimable");
        // Antes ix_communication_outbox_correlation (correlation_type/correlation_id): mismas columnas
        // renombradas a source_type/source_id. Sirve a búsquedas por origen (monitor, reconciliación).
        builder.HasIndex(x => new { x.TenantId, x.CompanyId, x.SourceType, x.SourceId, x.Purpose, x.RecipientEmail })
            .HasDatabaseName("ix_communication_outbox_source");
        // Autoridad de idempotencia (INSERT … ON CONFLICT DO NOTHING). NULLS NOT DISTINCT: las filas
        // System (tenant/empresa NULL) también quedan protegidas (PostgreSQL 15+; el ERP usa 16).
        builder.HasIndex(x => new { x.TenantId, x.CompanyId, x.IdempotencyKey })
            .IsUnique()
            .AreNullsDistinct(false)
            .HasFilter("idempotency_key IS NOT NULL")
            .HasDatabaseName("ux_communication_outbox_idempotency");
    }
}

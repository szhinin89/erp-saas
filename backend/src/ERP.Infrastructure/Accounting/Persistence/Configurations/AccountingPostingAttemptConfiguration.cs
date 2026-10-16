using ERP.Domain.Modules.Accounting.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ERP.Infrastructure.Accounting.Persistence.Configurations;

/// <summary>ACCOUNTING-CONTINUITY-01 — historial append-only de intentos de contabilización.</summary>
public sealed class AccountingPostingAttemptConfiguration
    : IEntityTypeConfiguration<AccountingPostingAttempt>
{
    public void Configure(EntityTypeBuilder<AccountingPostingAttempt> builder)
    {
        builder.ToTable("accounting_posting_attempts");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id").IsRequired();
        builder.Property(x => x.TenantId).HasColumnName("tenant_id").IsRequired();
        builder.Property(x => x.CompanyId).HasColumnName("company_id").IsRequired();
        builder
            .Property(x => x.AccountingPostingId)
            .HasColumnName("accounting_posting_id")
            .IsRequired();
        builder.Property(x => x.AttemptNumber).HasColumnName("attempt_number").IsRequired();
        builder
            .Property(x => x.Trigger)
            .HasColumnName("trigger")
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();
        builder.Property(x => x.TriggeredBy).HasColumnName("triggered_by").IsRequired();
        builder
            .Property(x => x.CorrelationId)
            .HasColumnName("correlation_id")
            .HasMaxLength(AccountingPostingAttempt.CorrelationIdMaxLength);
        builder
            .Property(x => x.Reason)
            .HasColumnName("reason")
            .HasMaxLength(AccountingPostingAttempt.ReasonMaxLength);
        builder.Property(x => x.StartedAtUtc).HasColumnName("started_at_utc").IsRequired();
        builder.Property(x => x.FinishedAtUtc).HasColumnName("finished_at_utc").IsRequired();
        builder
            .Property(x => x.Outcome)
            .HasColumnName("outcome")
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();
        builder
            .Property(x => x.ErrorCode)
            .HasColumnName("error_code")
            .HasMaxLength(AccountingPostingAttempt.ErrorCodeMaxLength);
        builder
            .Property(x => x.ErrorMessage)
            .HasColumnName("error_message")
            .HasMaxLength(AccountingPostingAttempt.ErrorMessageMaxLength);
        builder.Property(x => x.JournalEntryId).HasColumnName("journal_entry_id");

        builder.Property(x => x.CreatedAt).HasColumnName("created_at");
        builder.Property(x => x.UpdatedAt).HasColumnName("updated_at");
        builder.Property(x => x.CreatedBy).HasColumnName("created_by");
        builder.Property(x => x.UpdatedBy).HasColumnName("updated_by");

        builder
            .HasIndex(x => new { x.AccountingPostingId, x.AttemptNumber })
            .IsUnique()
            .HasDatabaseName("uq_accounting_posting_attempts_posting_number");
        builder
            .HasIndex(x => new { x.TenantId, x.CompanyId })
            .HasDatabaseName("ix_accounting_posting_attempts_scope");

        builder
            .HasOne<AccountingPosting>()
            .WithMany()
            .HasForeignKey(x => x.AccountingPostingId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

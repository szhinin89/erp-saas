using ERP.Domain.Modules.Accounting.Entities;
using ERP.Infrastructure.Audit;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ERP.Infrastructure.Accounting.Persistence.Configurations;

public sealed class ManualJournalAttemptConfiguration : IEntityTypeConfiguration<ManualJournalAttempt>
{
    public void Configure(EntityTypeBuilder<ManualJournalAttempt> b)
    {
        b.ToTable("manual_journal_attempts");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasColumnName("id");
        b.Property(x => x.TenantId).HasColumnName("tenant_id");
        b.Property(x => x.CompanyId).HasColumnName("company_id");
        b.Property(x => x.JournalEntryId).HasColumnName("journal_entry_id");
        b.Property(x => x.AttemptedAtUtc).HasColumnName("attempted_at_utc");
        b.Property(x => x.AttemptedBy).HasColumnName("attempted_by");
        b.Property(x => x.IdempotencyKey).HasColumnName("idempotency_key");
        b.Property(x => x.Success).HasColumnName("success");
        b.Property(x => x.ErrorCode).HasColumnName("error_code").HasMaxLength(100);
        b.Property(x => x.ErrorMessage).HasColumnName("error_message").HasMaxLength(1000);
        b.Property(x => x.ExpectedVersion).HasColumnName("expected_version");
        b.Property(x => x.ResultVersion).HasColumnName("result_version");
        b.Property(x => x.ResultStatus).HasColumnName("result_status").HasMaxLength(30);
        b.Property(x => x.EntryNumber).HasColumnName("entry_number");
        b.HasIndex(x => new { x.CompanyId, x.JournalEntryId, x.IdempotencyKey }).IsUnique();
        b.HasOne<JournalEntry>().WithMany().HasForeignKey(x => x.JournalEntryId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class JournalEntryAuditConfiguration : IEntityTypeConfiguration<JournalEntryAudit>
{
    public void Configure(EntityTypeBuilder<JournalEntryAudit> b)
    {
        b.ConfigureAuditBase("journal_entry_audit");
        b.Property(x => x.CompanyId).HasColumnName("company_id");
        b.Property(x => x.SnapshotJson).HasColumnName("snapshot_json").HasColumnType("jsonb");
    }
}

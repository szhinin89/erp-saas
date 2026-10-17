using ERP.Domain.Modules.Accounting.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ERP.Infrastructure.Accounting.Persistence.Configurations;

/// <summary>
/// ACCOUNTING-CONTINUITY-01 — obligación contable durable. Índice único por la misma identidad
/// que <c>journal_entries</c> (Company + SourceModule + SourceEventId + FactType).
/// </summary>
public sealed class AccountingPostingConfiguration : IEntityTypeConfiguration<AccountingPosting>
{
    public void Configure(EntityTypeBuilder<AccountingPosting> builder)
    {
        builder.ToTable("accounting_postings");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id").IsRequired();
        builder.Property(x => x.TenantId).HasColumnName("tenant_id").IsRequired();
        builder.Property(x => x.CompanyId).HasColumnName("company_id").IsRequired();
        builder
            .Property(x => x.SourceModule)
            .HasColumnName("source_module")
            .HasMaxLength(AccountingPosting.SourceModuleMaxLength)
            .IsRequired();
        builder
            .Property(x => x.FactType)
            .HasColumnName("fact_type")
            .HasMaxLength(AccountingPosting.FactTypeMaxLength)
            .IsRequired();
        builder.Property(x => x.SourceEventId).HasColumnName("source_event_id").IsRequired();
        builder
            .Property(x => x.SourceDocumentNumber)
            .HasColumnName("source_document_number")
            .HasMaxLength(AccountingPosting.SourceDocumentNumberMaxLength);
        builder
            .Property(x => x.BusinessDate)
            .HasColumnName("business_date")
            .HasColumnType("date")
            .IsRequired();
        builder
            .Property(x => x.EntryDate)
            .HasColumnName("entry_date")
            .HasColumnType("date")
            .IsRequired();
        builder.Property(x => x.PayloadVersion).HasColumnName("payload_version").IsRequired();
        builder
            .Property(x => x.Payload)
            .HasColumnName("payload")
            .HasColumnType("jsonb")
            .IsRequired();
        builder
            .Property(x => x.Status)
            .HasColumnName("status")
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();
        builder
            .Property(x => x.ErrorCode)
            .HasColumnName("error_code")
            .HasMaxLength(AccountingPosting.ErrorCodeMaxLength);
        builder
            .Property(x => x.ErrorMessage)
            .HasColumnName("error_message")
            .HasMaxLength(AccountingPosting.ErrorMessageMaxLength);
        builder.Property(x => x.Attempts).HasColumnName("attempts").IsRequired();
        builder.Property(x => x.LastAttemptAtUtc).HasColumnName("last_attempt_at_utc");
        builder.Property(x => x.JournalEntryId).HasColumnName("journal_entry_id");
        builder
            .Property(x => x.CorrelationId)
            .HasColumnName("correlation_id")
            .HasMaxLength(AccountingPosting.CorrelationIdMaxLength);

        builder.Property(x => x.SourceDocumentId).HasColumnName("source_document_id");

        builder.Property(x => x.CreatedAt).HasColumnName("created_at");
        builder.Property(x => x.UpdatedAt).HasColumnName("updated_at");
        builder.Property(x => x.CreatedBy).HasColumnName("created_by");
        builder.Property(x => x.UpdatedBy).HasColumnName("updated_by");
        builder.Property<uint>("xmin").HasColumnType("xid").IsRowVersion();

        builder
            .HasIndex(x => new
            {
                x.CompanyId,
                x.SourceModule,
                x.SourceEventId,
                x.FactType,
            })
            .IsUnique()
            .HasDatabaseName("uq_accounting_postings_company_source_event_fact");
        builder
            .HasIndex(x => new
            {
                x.TenantId,
                x.CompanyId,
                x.Status,
                x.EntryDate,
            })
            .HasDatabaseName("ix_accounting_postings_scope_status_entry_date");
        builder
            .HasIndex(x => new { x.TenantId, x.CompanyId, x.SourceDocumentId })
            .HasDatabaseName("ix_accounting_postings_scope_source_document")
            .HasFilter("source_document_id IS NOT NULL");

        builder
            .HasOne<JournalEntry>()
            .WithMany()
            .HasForeignKey(x => x.JournalEntryId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

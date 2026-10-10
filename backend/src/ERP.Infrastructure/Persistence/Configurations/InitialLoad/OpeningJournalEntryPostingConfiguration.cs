using ERP.Domain.Modules.InitialLoad.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ERP.Infrastructure.Persistence.Configurations.InitialLoad;

/// <summary>
/// IL-8A — versiones del ASI de apertura: historial completo por empresa, versión única por empresa
/// y a lo sumo UNA vigente (índice único parcial <c>is_current</c>).
/// </summary>
public sealed class OpeningJournalEntryPostingConfiguration : IEntityTypeConfiguration<OpeningJournalEntryPosting>
{
    public void Configure(EntityTypeBuilder<OpeningJournalEntryPosting> builder)
    {
        builder.ToTable("opening_journal_entry_postings");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.TenantId).HasColumnName("tenant_id").IsRequired();
        builder.Property(x => x.CompanyId).HasColumnName("company_id").IsRequired();
        builder.Property(x => x.Version).HasColumnName("version").IsRequired();
        builder.Property(x => x.IsCurrent).HasColumnName("is_current").IsRequired().ValueGeneratedNever();
        builder.Property(x => x.SupersededAt).HasColumnName("superseded_at");
        builder.Property(x => x.EntryDate).HasColumnName("entry_date").HasColumnType("date").IsRequired();
        builder
            .Property(x => x.TotalAmount)
            .HasColumnName("total_amount")
            .HasColumnType("numeric(18,2)")
            .IsRequired();
        builder.Property(x => x.LineCount).HasColumnName("line_count").IsRequired();
        builder.Property(x => x.Status).HasColumnName("status").IsRequired();
        builder.Property(x => x.JournalEntryId).HasColumnName("journal_entry_id");
        builder.Property(x => x.PostedAt).HasColumnName("posted_at");
        builder
            .Property(x => x.ErrorCode)
            .HasColumnName("error_code")
            .HasMaxLength(OpeningBalancePosting.ErrorCodeMaxLength);
        builder
            .Property(x => x.ErrorMessage)
            .HasColumnName("error_message")
            .HasMaxLength(OpeningBalancePosting.ErrorMessageMaxLength);
        builder.Property(x => x.Attempts).HasColumnName("attempts").IsRequired();

        builder.Property(x => x.CreatedAt).HasColumnName("created_at");
        builder.Property(x => x.UpdatedAt).HasColumnName("updated_at");
        builder.Property(x => x.CreatedBy).HasColumnName("created_by");
        builder.Property(x => x.UpdatedBy).HasColumnName("updated_by");
        builder.Property<uint>("xmin").HasColumnType("xid").IsRowVersion();

        builder
            .HasIndex(x => x.CompanyId)
            .IsUnique()
            .HasFilter("is_current")
            .HasDatabaseName("ux_opening_journal_entry_postings_company_current");
        builder
            .HasIndex(x => new { x.CompanyId, x.Version })
            .IsUnique()
            .HasDatabaseName("ux_opening_journal_entry_postings_company_version");
        builder
            .HasIndex(x => new { x.TenantId, x.CompanyId })
            .HasDatabaseName("ix_opening_journal_entry_postings_tenant_company");
    }
}

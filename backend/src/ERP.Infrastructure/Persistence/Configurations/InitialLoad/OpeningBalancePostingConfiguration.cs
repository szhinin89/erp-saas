using ERP.Domain.Modules.InitialLoad.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ERP.Infrastructure.Persistence.Configurations.InitialLoad;

/// <summary>IL-7A — estado contable de la apertura: una fila por empresa + lote.</summary>
public sealed class OpeningBalancePostingConfiguration : IEntityTypeConfiguration<OpeningBalancePosting>
{
    public void Configure(EntityTypeBuilder<OpeningBalancePosting> builder)
    {
        builder.ToTable("opening_balance_postings");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.TenantId).HasColumnName("tenant_id").IsRequired();
        builder.Property(x => x.CompanyId).HasColumnName("company_id").IsRequired();
        builder.Property(x => x.ImportBatchId).HasColumnName("import_batch_id").IsRequired();
        builder.Property(x => x.ImportType).HasColumnName("import_type").IsRequired();
        builder
            .Property(x => x.FactType)
            .HasColumnName("fact_type")
            .HasMaxLength(OpeningBalancePosting.FactTypeMaxLength)
            .IsRequired();
        builder.Property(x => x.EntryDate).HasColumnName("entry_date").HasColumnType("date").IsRequired();
        builder.Property(x => x.Amount).HasColumnName("amount").HasColumnType("numeric(18,2)").IsRequired();
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
            .HasOne<ImportBatch>()
            .WithMany()
            .HasForeignKey(x => x.ImportBatchId)
            .OnDelete(DeleteBehavior.Restrict);

        builder
            .HasIndex(x => new { x.CompanyId, x.ImportBatchId })
            .IsUnique()
            .HasDatabaseName("ux_opening_balance_postings_company_batch");
        builder
            .HasIndex(x => new { x.TenantId, x.CompanyId, x.Status })
            .HasDatabaseName("ix_opening_balance_postings_company_status");
    }
}

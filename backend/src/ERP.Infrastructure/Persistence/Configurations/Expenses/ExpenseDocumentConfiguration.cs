using ERP.Domain.Modules.Expenses.Entities;
using ERP.Domain.Modules.Expenses.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ERP.Infrastructure.Persistence.Configurations.Expenses;

public sealed class ExpenseDocumentConfiguration : IEntityTypeConfiguration<ExpenseDocument>
{
    public void Configure(EntityTypeBuilder<ExpenseDocument> builder)
    {
        builder.ToTable("expense_document");

        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasColumnName("id");
        builder.Property(e => e.TenantId).HasColumnName("tenant_id").IsRequired();
        builder.Property(e => e.SupplierId).HasColumnName("supplier_id");
        builder.Property(e => e.DocType)
            .HasColumnName("doc_type")
            .HasMaxLength(20)
            .HasConversion(
                v => v.ToDbValue(),
                v => ExpenseDocumentTypeExtensions.FromDbValue(v))
            .IsRequired();
        builder.Property(e => e.DocNumber).HasColumnName("doc_number").HasMaxLength(30);
        builder.Property(e => e.AccessKey).HasColumnName("access_key").HasMaxLength(49);
        builder.Property(e => e.IssueDate).HasColumnName("issue_date");
        builder.Property(e => e.Concept).HasColumnName("concept").HasMaxLength(500);
        builder.Property(e => e.Category).HasColumnName("category").HasMaxLength(50);
        builder.Property(e => e.Subtotal).HasColumnName("subtotal").HasPrecision(18, 4);
        builder.Property(e => e.TaxTotal).HasColumnName("tax_total").HasPrecision(18, 4);
        builder.Property(e => e.Total).HasColumnName("total").HasPrecision(18, 4);
        builder.Property(e => e.TotalNotesApplied).HasColumnName("total_notes_applied").HasPrecision(18, 4);
        builder.Property(e => e.Status)
            .HasColumnName("status")
            .HasMaxLength(20)
            .HasConversion<string>();
        builder.Property(e => e.ValidatedBy).HasColumnName("validated_by");
        builder.Property(e => e.ValidatedAt).HasColumnName("validated_at");
        builder.Property(e => e.ApprovedBy).HasColumnName("approved_by");
        builder.Property(e => e.ApprovedAt).HasColumnName("approved_at");
        builder.Property(e => e.RejectedBy).HasColumnName("rejected_by");
        builder.Property(e => e.RejectedAt).HasColumnName("rejected_at");
        builder.Property(e => e.RejectionReason).HasColumnName("rejection_reason").HasMaxLength(500);
        builder.Property(e => e.Notes).HasColumnName("notes").HasMaxLength(1000);
        builder.Property(e => e.XmlPath).HasColumnName("xml_path").HasMaxLength(500);
        builder.Property(e => e.JournalEntryId).HasColumnName("journal_entry_id");
        builder.Property(e => e.IsActive).HasColumnName("is_active");
        builder.Property(e => e.CreatedAt).HasColumnName("created_at");
        builder.Property(e => e.UpdatedAt).HasColumnName("updated_at");
        builder.Property(e => e.CreatedBy).HasColumnName("created_by");
        builder.Property(e => e.UpdatedBy).HasColumnName("updated_by");

        builder.HasMany(e => e.Details).WithOne().HasForeignKey(d => d.ExpenseId).OnDelete(DeleteBehavior.Cascade);
    }
}

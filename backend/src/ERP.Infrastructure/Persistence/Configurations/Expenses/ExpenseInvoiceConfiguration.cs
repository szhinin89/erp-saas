using ERP.Domain.Modules.Expenses.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ERP.Infrastructure.Persistence.Configurations.Expenses;

public sealed class ExpenseInvoiceConfiguration : IEntityTypeConfiguration<ExpenseInvoice>
{
    public void Configure(EntityTypeBuilder<ExpenseInvoice> builder)
    {
        builder.ToTable("expense_invoice");

        builder.HasKey(g => g.Id);
        builder.Property(g => g.Id).HasColumnName("id");
        builder.Property(g => g.SubscriberId).HasColumnName("subscriber_id").IsRequired();
        builder.Property(g => g.AccessKey).HasColumnName("access_key").HasMaxLength(ExpenseInvoice.AccessKeyLen);
        builder.Property(g => g.XmlPath).HasColumnName("xml_path").HasMaxLength(ExpenseInvoice.XmlPathMaxLen);
        builder.Property(g => g.SupplierId).HasColumnName("supplier_id");
        builder.Property(g => g.InvoiceNumber).HasColumnName("invoice_number").HasMaxLength(ExpenseInvoice.InvoiceNumberMaxLen);
        builder.Property(g => g.IssueDate).HasColumnName("issue_date").IsRequired();
        builder.Property(g => g.Concept).HasColumnName("concept").HasMaxLength(ExpenseInvoice.ConceptMaxLen).IsRequired();
        builder.Property(g => g.Category).HasColumnName("category").HasMaxLength(ExpenseInvoice.CategoryMaxLen).IsRequired();
        builder.Property(g => g.Subtotal).HasColumnName("subtotal").HasPrecision(18, 4).IsRequired();
        builder.Property(g => g.TaxTotal).HasColumnName("tax_total").HasPrecision(18, 4).IsRequired();
        builder.Property(g => g.Total).HasColumnName("total").HasPrecision(18, 4).IsRequired();
        builder.Property(g => g.TotalNotesApplied).HasColumnName("total_notes_applied").HasPrecision(18, 4).IsRequired();
        builder.Property(g => g.Status).HasColumnName("status").HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(g => g.Notes).HasColumnName("notes").HasMaxLength(ExpenseInvoice.NotesMaxLen);
        builder.Property(g => g.ValidatedBy).HasColumnName("validated_by");
        builder.Property(g => g.ValidatedAt).HasColumnName("validated_at");
        builder.Property(g => g.ApprovedBy).HasColumnName("approved_by");
        builder.Property(g => g.ApprovedAt).HasColumnName("approved_at");
        builder.Property(g => g.RejectedBy).HasColumnName("rejected_by");
        builder.Property(g => g.RejectedAt).HasColumnName("rejected_at");
        builder.Property(g => g.RejectionReason).HasColumnName("rejection_reason").HasMaxLength(ExpenseInvoice.RejectionReasonMaxLen);
        builder.Property(g => g.JournalEntryId).HasColumnName("journal_entry_id");
        builder.Property(g => g.IsActive).HasColumnName("is_active").IsRequired();
        builder.Property(g => g.CreatedAt).HasColumnName("created_at");
        builder.Property(g => g.UpdatedAt).HasColumnName("updated_at");
        builder.Property(g => g.CreatedBy).HasColumnName("created_by");
        builder.Property(g => g.UpdatedBy).HasColumnName("updated_by");

        builder.HasIndex(g => new { g.SubscriberId, g.IssueDate }).HasDatabaseName("ix_expense_invoice_subscriber_date");
        builder.HasIndex(g => new { g.SubscriberId, g.Category }).HasDatabaseName("ix_expense_invoice_subscriber_category");
        builder.HasIndex(g => new { g.SubscriberId, g.AccessKey }).IsUnique().HasFilter("access_key IS NOT NULL").HasDatabaseName("uq_expense_invoice_access_key");
        builder.HasIndex(g => new { g.SubscriberId, g.Status }).HasDatabaseName("ix_expense_invoice_subscriber_status");
    }
}

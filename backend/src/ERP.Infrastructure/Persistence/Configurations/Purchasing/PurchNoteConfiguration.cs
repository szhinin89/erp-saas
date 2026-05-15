using ERP.Domain.Modules.Purchasing.Entities;
using ERP.Domain.Modules.Expenses.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ERP.Infrastructure.Persistence.Configurations.Purchasing;

public sealed class PurchNoteConfiguration : IEntityTypeConfiguration<PurchNote>
{
    public void Configure(EntityTypeBuilder<PurchNote> builder)
    {
        builder.ToTable("purch_note");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.TenantId).HasColumnName("tenant_id").IsRequired();
        builder.Property(x => x.SupplierId).HasColumnName("supplier_id").IsRequired();
        builder.Property(x => x.PurchBillId).HasColumnName("purch_bill_id");
        builder.Property(x => x.ExpenseInvoiceId).HasColumnName("expense_invoice_id");
        builder.Property(x => x.NoteType).HasColumnName("note_type").HasMaxLength(PurchNote.NoteTypeMaxLen).IsRequired();
        builder.Property(x => x.Reason).HasColumnName("reason").HasMaxLength(PurchNote.ReasonMaxLen).IsRequired();
        builder.Property(x => x.AccessKey).HasColumnName("access_key").HasMaxLength(PurchNote.AccessKeyMaxLen).IsRequired();
        builder.Property(x => x.IssueDate).HasColumnName("issue_date").IsRequired();
        builder.Property(x => x.EstabCode).HasColumnName("estab_code").HasMaxLength(PurchNote.EstabMaxLen).IsRequired();
        builder.Property(x => x.EmPointCode).HasColumnName("em_point_code").HasMaxLength(PurchNote.EmPointMaxLen).IsRequired();
        builder.Property(x => x.Sequential).HasColumnName("sequential").HasMaxLength(PurchNote.SequentialMaxLen).IsRequired();
        builder.Property(x => x.Subtotal).HasColumnName("subtotal").HasPrecision(18, 4).IsRequired();
        builder.Property(x => x.VatTotal).HasColumnName("vat_total").HasPrecision(18, 4).IsRequired();
        builder.Property(x => x.Total).HasColumnName("total").HasPrecision(18, 4).IsRequired();
        builder.Property(x => x.Status).HasColumnName("status").HasMaxLength(PurchNote.StatusMaxLen).IsRequired();
        builder.Property(x => x.XmlPath).HasColumnName("xml_path").HasMaxLength(PurchNote.XmlPathMaxLen);
        builder.Property(x => x.AuthNumber).HasColumnName("auth_number").HasMaxLength(PurchNote.AuthNumberMaxLen);
        builder.Property(x => x.AuthDate).HasColumnName("auth_date");
        builder.Property(x => x.JournalEntryId).HasColumnName("journal_entry_id");
        builder.Property(x => x.CreatedAt).HasColumnName("created_at");
        builder.Property(x => x.UpdatedAt).HasColumnName("updated_at");
        builder.Property(x => x.CreatedBy).HasColumnName("created_by");
        builder.Property(x => x.UpdatedBy).HasColumnName("updated_by");

        builder.HasIndex(x => new { x.TenantId, x.AccessKey }).IsUnique().HasDatabaseName("uq_purch_note_access_key");
        builder.HasIndex(x => new { x.TenantId, x.SupplierId, x.Status }).HasDatabaseName("ix_purch_note_tenant_supplier_status");
        builder.HasIndex(x => x.PurchBillId).HasDatabaseName("ix_purch_note_bill_id");
        builder.HasIndex(x => x.ExpenseInvoiceId).HasDatabaseName("ix_purch_note_expense_id");

        builder.HasOne(x => x.Supplier).WithMany().HasForeignKey(x => x.SupplierId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.PurchBill).WithMany().HasForeignKey(x => x.PurchBillId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.ExpenseInv).WithMany().HasForeignKey(x => x.ExpenseInvoiceId).OnDelete(DeleteBehavior.Restrict);
        builder.HasMany(x => x.Lines).WithOne().HasForeignKey(d => d.PurchNoteId).OnDelete(DeleteBehavior.Cascade);
    }
}

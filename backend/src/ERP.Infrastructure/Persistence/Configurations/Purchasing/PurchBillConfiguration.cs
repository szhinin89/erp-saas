using ERP.Domain.Modules.Purchasing.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ERP.Infrastructure.Persistence.Configurations.Purchasing;

public sealed class PurchBillConfiguration : IEntityTypeConfiguration<PurchBill>
{
    public void Configure(EntityTypeBuilder<PurchBill> builder)
    {
        builder.ToTable("purch_bill");

        builder.HasKey(c => c.Id);
        builder.Property(c => c.Id).HasColumnName("id");
        builder.Property(c => c.SubscriberId).HasColumnName("subscriber_id").IsRequired();
        builder.Property(c => c.BusinessPartnerId).HasColumnName("business_partner_id").IsRequired();
        builder.Property(c => c.BusinessPartnerId).HasColumnName("business_partner_id");
        builder.Property(c => c.InvoiceNumber).HasColumnName("invoice_number").HasMaxLength(PurchBill.InvoiceNumberMaxLen).IsRequired();
        builder.Property(c => c.AccessKey).HasColumnName("access_key").HasMaxLength(PurchBill.AccessKeyLen);
        builder.Property(c => c.XmlPath).HasColumnName("xml_path").HasMaxLength(PurchBill.XmlPathMaxLen);
        builder.Property(c => c.InvoiceDate).HasColumnName("invoice_date").IsRequired();
        builder.Property(c => c.DueDate).HasColumnName("due_date");
        builder.Property(c => c.Status).HasColumnName("status").HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(c => c.PaymentTerms).HasColumnName("payment_terms").HasMaxLength(PurchBill.PaymentTermsMaxLen).IsRequired();
        builder.Property(c => c.Subtotal).HasColumnName("subtotal").HasPrecision(18, 4).IsRequired();
        builder.Property(c => c.VatTotal).HasColumnName("vat_total").HasPrecision(18, 4).IsRequired();
        builder.Property(c => c.Total).HasColumnName("total").HasPrecision(18, 4).IsRequired();
        builder.Property(c => c.TotalNotesApplied).HasColumnName("total_notes_applied").HasPrecision(18, 4).IsRequired();
        builder.Property(c => c.Notes).HasColumnName("notes").HasMaxLength(PurchBill.NotesMaxLen);
        builder.Property(c => c.ValidatedBy).HasColumnName("validated_by");
        builder.Property(c => c.ValidatedAt).HasColumnName("validated_at");
        builder.Property(c => c.ApprovedBy).HasColumnName("approved_by");
        builder.Property(c => c.ApprovedAt).HasColumnName("approved_at");
        builder.Property(c => c.RejectedBy).HasColumnName("rejected_by");
        builder.Property(c => c.RejectedAt).HasColumnName("rejected_at");
        builder.Property(c => c.RejectionReason).HasColumnName("rejection_reason").HasMaxLength(PurchBill.RejectionReasonMaxLen);
        builder.Property(c => c.JournalEntryId).HasColumnName("journal_entry_id");
        builder.Property(c => c.CreatedAt).HasColumnName("created_at");
        builder.Property(c => c.UpdatedAt).HasColumnName("updated_at");
        builder.Property(c => c.CreatedBy).HasColumnName("created_by");
        builder.Property(c => c.UpdatedBy).HasColumnName("updated_by");

        builder.HasMany(c => c.Lines).WithOne().HasForeignKey(d => d.PurchBillId).OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(c => new { c.SubscriberId, c.BusinessPartnerId, c.InvoiceNumber }).IsUnique().HasDatabaseName("uq_purch_bill_supplier_invoice");
        builder.HasIndex(c => new { c.SubscriberId, c.AccessKey }).IsUnique().HasFilter("access_key IS NOT NULL").HasDatabaseName("uq_purch_bill_access_key");
        builder.HasIndex(c => new { c.SubscriberId, c.Status }).HasDatabaseName("ix_purch_bill_subscriber_status");
        builder.HasIndex(c => new { c.SubscriberId, c.BusinessPartnerId, c.Status }).HasDatabaseName("ix_purch_bill_subscriber_supplier_status");
    }
}

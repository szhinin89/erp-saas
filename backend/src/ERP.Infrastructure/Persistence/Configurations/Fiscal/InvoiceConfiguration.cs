using ERP.Domain.Modules.Fiscal.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ERP.Infrastructure.Persistence.Configurations.Fiscal;

public sealed class InvoiceConfiguration : IEntityTypeConfiguration<Invoice>
{
    public void Configure(EntityTypeBuilder<Invoice> builder)
    {
        builder.ToTable("invoice");

        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasColumnName("id");
        builder.Property(e => e.PublicId).HasColumnName("public_id").IsRequired();
        builder.Property(e => e.SubscriberId).HasColumnName("subscriber_id").IsRequired();
        builder.Property(e => e.CompanyId).HasColumnName("company_id");
        builder.Property(e => e.BranchId).HasColumnName("branch_id").IsRequired();
        builder.Property(e => e.BusinessPartnerId).HasColumnName("business_partner_id").IsRequired();
        builder.Property(e => e.WarehouseId).HasColumnName("warehouse_id").IsRequired();
        builder.Property(e => e.DocType).HasColumnName("doc_type").HasMaxLength(10).IsRequired();
        builder.Property(e => e.EstabCode).HasColumnName("estab_code").HasMaxLength(Invoice.EstabMaxLen).IsRequired();
        builder.Property(e => e.EmPointCode).HasColumnName("em_point_code").HasMaxLength(Invoice.EmPointMaxLen).IsRequired();
        builder.Property(e => e.Sequential).HasColumnName("sequential").HasMaxLength(Invoice.SequentialMaxLen).IsRequired();
        builder.Property(e => e.AccessKey).HasColumnName("access_key").HasMaxLength(Invoice.AccessKeyMaxLen).IsRequired();
        builder.Property(e => e.IssueDate).HasColumnName("issue_date").IsRequired();
        builder.Property(e => e.Subtotal).HasColumnName("subtotal").HasPrecision(18, 4);
        builder.Property(e => e.TaxTotal).HasColumnName("tax_total").HasPrecision(18, 4);
        builder.Property(e => e.Total).HasColumnName("total").HasPrecision(18, 4);
        builder.Property(e => e.TotalDiscount).HasColumnName("total_discount").HasPrecision(18, 4);
        builder.Property(e => e.PaymentMethodCode).HasColumnName("payment_method_code").HasMaxLength(Invoice.PaymentMethodMaxLen);
        builder.Property(e => e.PaymentTermDays).HasColumnName("payment_term_days");
        builder.Property(e => e.Notes).HasColumnName("notes").HasMaxLength(Invoice.NotesMaxLen);
        builder.Property(e => e.Status).HasColumnName("status").HasMaxLength(Invoice.StatusMaxLen).IsRequired();
        builder.Property(e => e.BuyerIdType).HasColumnName("buyer_id_type").HasMaxLength(Invoice.BuyerIdTypeMaxLen).IsRequired();
        builder.Property(e => e.BuyerIdNumber).HasColumnName("buyer_id_number").HasMaxLength(Invoice.BuyerIdNumberMaxLen).IsRequired();
        builder.Property(e => e.BuyerNameSnapshot).HasColumnName("buyer_name_snapshot").HasMaxLength(Invoice.BuyerNameMaxLen).IsRequired();
        builder.Property(e => e.BuyerAddressSnapshot).HasColumnName("buyer_address_snapshot");
        builder.Property(e => e.JournalEntryId).HasColumnName("journal_entry_id");
        builder.Property(e => e.RowVersion).HasColumnName("row_version").IsConcurrencyToken();
        builder.Property(e => e.CreatedAt).HasColumnName("created_at");
        builder.Property(e => e.CreatedBy).HasColumnName("created_by");
        builder.Property(e => e.UpdatedAt).HasColumnName("updated_at");
        builder.Property(e => e.UpdatedBy).HasColumnName("updated_by");

        builder.HasMany(e => e.Lines).WithOne().HasForeignKey(d => d.InvoiceId).OnDelete(DeleteBehavior.Cascade);
        builder.HasMany(e => e.StatusHistory).WithOne().HasForeignKey(h => h.InvoiceId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(e => e.Electronic).WithOne().HasForeignKey<InvoiceElectronic>(e => e.InvoiceId).OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(e => e.PublicId).IsUnique().HasDatabaseName("uq_invoice_public_id");
        builder.HasIndex(e => new { e.SubscriberId, e.EstabCode, e.EmPointCode, e.Sequential }).IsUnique().HasDatabaseName("uq_invoice_seq");
        builder.HasIndex(e => new { e.SubscriberId, e.IssueDate }).HasDatabaseName("ix_invoice_subscriber_issue_date");
        builder.HasIndex(e => new { e.SubscriberId, e.Status }).HasDatabaseName("ix_invoice_subscriber_status");
        builder.HasIndex(e => new { e.SubscriberId, e.BusinessPartnerId }).HasDatabaseName("ix_invoice_subscriber_business_partner");
    }
}

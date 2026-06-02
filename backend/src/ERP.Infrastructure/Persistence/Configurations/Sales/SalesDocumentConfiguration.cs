using ERP.Domain.Modules.Sales.Entities;
using ERP.Infrastructure.Persistence.Converters;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ERP.Infrastructure.Persistence.Configurations.Sales;

public sealed class SalesDocumentConfiguration : IEntityTypeConfiguration<SalesDocument>
{
    public void Configure(EntityTypeBuilder<SalesDocument> builder)
    {
        builder.ToTable("sales_document");

        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasColumnName("id");
        builder.Property(e => e.SubscriberId).HasColumnName("subscriber_id").IsRequired();
        builder.Property(e => e.CompanyId).HasColumnName("company_id");
        builder.Property(e => e.BranchId).HasColumnName("branch_id");
        builder.Property(e => e.BusinessPartnerId).HasColumnName("business_partner_id");
        builder.Property(e => e.WarehouseId).HasColumnName("warehouse_id");
        builder.Property(e => e.SalespersonId).HasColumnName("salesperson_id");
        builder.Property(e => e.DocType)
            .HasColumnName("doc_type")
            .HasMaxLength(20)
            .HasConversion(
                v => SalesDocumentTypeConversions.ToDb(v),
                v => SalesDocumentTypeConversions.FromCode(v))
            .IsRequired();
        builder.Property(e => e.DocNumber).HasColumnName("doc_number").HasMaxLength(30);
        builder.Property(e => e.EstabCode).HasColumnName("estab_code").HasMaxLength(3);
        builder.Property(e => e.EmPointCode).HasColumnName("em_point_code").HasMaxLength(3);
        builder.Property(e => e.Sequential).HasColumnName("sequential").HasMaxLength(9);
        builder.Property(e => e.AccessKey).HasColumnName("access_key").HasMaxLength(49);
        builder.Property(e => e.IssueDate).HasColumnName("issue_date").IsRequired();
        builder.Property(e => e.DueDate).HasColumnName("due_date");
        builder.Property(e => e.DeliveryDate).HasColumnName("delivery_date");
        builder.Property(e => e.ReferenceDocumentId).HasColumnName("reference_document_id");
        builder.Property(e => e.Reason).HasColumnName("reason").HasMaxLength(300);
        builder.Property(e => e.Subtotal).HasColumnName("subtotal").HasPrecision(18, 4);
        builder.Property(e => e.VatTotal).HasColumnName("vat_total").HasPrecision(18, 4);
        builder.Property(e => e.TotalDiscount).HasColumnName("total_discount").HasPrecision(18, 4);
        builder.Property(e => e.Total).HasColumnName("total").HasPrecision(18, 4);
        builder.Property(e => e.TotalNotesApplied).HasColumnName("total_notes_applied").HasPrecision(18, 4);
        builder.Property(e => e.PaymentMethodCode).HasColumnName("payment_method_code").HasMaxLength(5);
        builder.Property(e => e.PaymentDays).HasColumnName("payment_days");
        builder.Property(e => e.Currency).HasColumnName("currency").HasMaxLength(3);
        builder.Property(e => e.Status).HasColumnName("status").HasMaxLength(30);
        builder.Property(e => e.Notes).HasColumnName("notes").HasMaxLength(1000);
        builder.Property(e => e.RemittanceKey).HasColumnName("remittance_key").HasMaxLength(49);
        builder.Property(e => e.GuideDocNum).HasColumnName("guide_doc_num").HasMaxLength(20);
        builder.Property(e => e.JournalEntryId).HasColumnName("journal_entry_id");
        builder.Property(e => e.BusinessPartnerId).HasColumnName("business_partner_id");
        builder.Property(e => e.CreatedAt).HasColumnName("created_at");
        builder.Property(e => e.UpdatedAt).HasColumnName("updated_at");
        builder.Property(e => e.CreatedBy).HasColumnName("created_by");
        builder.Property(e => e.UpdatedBy).HasColumnName("updated_by");

        builder.HasOne<ERP.Domain.MasterData.Entities.BusinessPartner>().WithMany().HasForeignKey(e => e.BusinessPartnerId).IsRequired(false).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(e => e.Warehouse).WithMany().HasForeignKey(e => e.WarehouseId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(e => e.Reference).WithMany().HasForeignKey(e => e.ReferenceDocumentId).OnDelete(DeleteBehavior.Restrict);

        builder.Navigation(e => e.Lines).HasField("_lines");
        builder.Navigation(e => e.Payments).HasField("_payments");
        builder.HasMany(e => e.Lines).WithOne().HasForeignKey(d => d.SalesDocumentId).OnDelete(DeleteBehavior.Cascade);
        builder.HasMany(e => e.Payments).WithOne().HasForeignKey(p => p.SalesDocumentId).OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(e => e.Electronic)
            .WithOne(e => e.Document)
            .HasForeignKey<SalesElectronicDoc>(e => e.SalesDocumentId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(e => new { e.SubscriberId, e.CompanyId })
            .HasDatabaseName("ix_sales_document_subscriber_company");
        builder.HasIndex(e => new { e.SubscriberId, e.IssueDate, e.Status, e.DocType })
            .HasDatabaseName("ix_sales_document_subscriber_date_status_type");
        builder.HasIndex(e => new { e.SubscriberId, e.BusinessPartnerId, e.IssueDate })
            .HasDatabaseName("ix_sales_document_subscriber_customer_date");
        builder.HasIndex(e => new { e.SubscriberId, e.AccessKey })
            .IsUnique()
            .HasFilter("access_key IS NOT NULL")
            .HasDatabaseName("uq_sales_document_subscriber_access_key");
    }
}

using ERP.Domain.Modules.Sales.Entities;
using ERP.Domain.Modules.Inventory.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ERP.Infrastructure.Persistence.Configurations.Sales;

public sealed class SalesBillConfiguration : IEntityTypeConfiguration<SalesBill>
{
    public void Configure(EntityTypeBuilder<SalesBill> builder)
    {
        builder.ToTable("sales_bill");

        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasColumnName("id");
        builder.Property(e => e.SubscriberId).HasColumnName("subscriber_id").IsRequired();
        builder.Property(e => e.CompanyId).HasColumnName("company_id");
        builder.Property(e => e.BranchId).HasColumnName("branch_id").IsRequired();
        builder.Property(e => e.CustomerId).HasColumnName("customer_id").IsRequired();
        builder.Property(e => e.WarehouseId).HasColumnName("warehouse_id").IsRequired();
        builder.Property(e => e.DocType).HasColumnName("doc_type").HasMaxLength(SalesBill.DocTypeMaxLen).IsRequired();
        builder.Property(e => e.EstabCode).HasColumnName("estab_code").HasMaxLength(SalesBill.EstabMaxLen).IsRequired();
        builder.Property(e => e.EmPointCode).HasColumnName("em_point_code").HasMaxLength(SalesBill.EmPointMaxLen).IsRequired();
        builder.Property(e => e.Sequential).HasColumnName("sequential").HasMaxLength(SalesBill.SequentialMaxLen).IsRequired();
        builder.Property(e => e.AccessKey).HasColumnName("access_key").HasMaxLength(SalesBill.AccessKeyMaxLen).IsRequired();
        builder.Property(e => e.IssueDate).HasColumnName("issue_date").IsRequired();
        builder.Property(e => e.Subtotal).HasColumnName("subtotal").HasPrecision(18, 4).IsRequired();
        builder.Property(e => e.VatTotal).HasColumnName("vat_total").HasPrecision(18, 4).IsRequired();
        builder.Property(e => e.Total).HasColumnName("total").HasPrecision(18, 4).IsRequired();
        builder.Property(e => e.TotalDiscount).HasColumnName("total_discount").HasPrecision(18, 4).IsRequired().HasDefaultValue(0m);
        builder.Property(e => e.PaymentMethodCode).HasColumnName("payment_method_code").HasMaxLength(SalesBill.PaymentMethodMaxLen).IsRequired().HasDefaultValue("01");
        builder.Property(e => e.PaymentDays).HasColumnName("payment_days").IsRequired().HasDefaultValue((short)0);
        builder.Property(e => e.Notes).HasColumnName("notes").HasMaxLength(SalesBill.NotesMaxLen);
        builder.Property(e => e.Status).HasColumnName("status").HasMaxLength(SalesBill.StatusMaxLen).IsRequired();
        builder.Property(e => e.XmlSignedPath).HasColumnName("xml_signed_path").HasMaxLength(SalesBill.XmlPathMaxLen);
        builder.Property(e => e.XmlAuthPath).HasColumnName("xml_auth_path").HasMaxLength(SalesBill.XmlPathMaxLen);
        builder.Property(e => e.AuthNumber).HasColumnName("auth_number").HasMaxLength(SalesBill.AccessKeyMaxLen);
        builder.Property(e => e.AuthDate).HasColumnName("auth_date");
        builder.Property(e => e.ErrorMessage).HasColumnName("error_message").HasMaxLength(SalesBill.ErrorMaxLen);
        builder.Property(e => e.JournalEntryId).HasColumnName("journal_entry_id");
        builder.Property(e => e.BusinessPartnerId).HasColumnName("business_partner_id");
        builder.Property(e => e.CreatedAt).HasColumnName("created_at");
        builder.Property(e => e.UpdatedAt).HasColumnName("updated_at");
        builder.Property(e => e.CreatedBy).HasColumnName("created_by");
        builder.Property(e => e.UpdatedBy).HasColumnName("updated_by");

        builder.HasOne(e => e.Cliente).WithMany().HasForeignKey(e => e.CustomerId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(e => e.Warehouse).WithMany().HasForeignKey(e => e.WarehouseId).OnDelete(DeleteBehavior.Restrict);
        builder.HasMany(e => e.Lines).WithOne().HasForeignKey(d => d.SalesBillId).OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(e => new { e.SubscriberId, e.EstabCode, e.EmPointCode, e.Sequential }).IsUnique().HasDatabaseName("uq_sales_bill_seq");
        builder.HasIndex(e => new { e.SubscriberId, e.IssueDate }).HasDatabaseName("ix_sales_bill_subscriber_date");
        builder.HasIndex(e => new { e.SubscriberId, e.CompanyId }).HasDatabaseName("ix_sales_bill_subscriber_company");
    }
}

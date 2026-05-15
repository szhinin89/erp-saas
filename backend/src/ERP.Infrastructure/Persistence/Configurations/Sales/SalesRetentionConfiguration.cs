using ERP.Domain.Modules.Sales.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ERP.Infrastructure.Persistence.Configurations.Sales;

public sealed class SalesRetentionConfiguration : IEntityTypeConfiguration<SalesRetention>
{
    public void Configure(EntityTypeBuilder<SalesRetention> builder)
    {
        builder.ToTable("sales_retention");

        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasColumnName("id");
        builder.Property(e => e.TenantId).HasColumnName("tenant_id").IsRequired();
        builder.Property(e => e.CustomerId).HasColumnName("customer_id").IsRequired();
        builder.Property(e => e.VoucherType).HasColumnName("voucher_type").HasMaxLength(SalesRetention.VoucherTypeMaxLen).IsRequired();
        builder.Property(e => e.AccessKey).HasColumnName("access_key").HasMaxLength(SalesRetention.AccessKeyMaxLen).IsRequired();
        builder.Property(e => e.IssueDate).HasColumnName("issue_date").IsRequired();
        builder.Property(e => e.TotalRetained).HasColumnName("total_retained").HasPrecision(18, 4).IsRequired();
        builder.Property(e => e.Status).HasColumnName("status").HasMaxLength(SalesRetention.StatusMaxLen).IsRequired();
        builder.Property(e => e.SalesBillId).HasColumnName("sales_bill_id");
        builder.Property(e => e.XmlPath).HasColumnName("xml_path").HasMaxLength(SalesRetention.XmlPathMaxLen);
        builder.Property(e => e.JournalEntryId).HasColumnName("journal_entry_id");
        builder.Property(e => e.CreatedAt).HasColumnName("created_at");
        builder.Property(e => e.UpdatedAt).HasColumnName("updated_at");
        builder.Property(e => e.CreatedBy).HasColumnName("created_by");
        builder.Property(e => e.UpdatedBy).HasColumnName("updated_by");

        builder.HasOne(e => e.Customer).WithMany().HasForeignKey(e => e.CustomerId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<SalesBill>().WithMany().HasForeignKey(e => e.SalesBillId).OnDelete(DeleteBehavior.SetNull);
        builder.HasMany(e => e.Lines).WithOne().HasForeignKey(d => d.SalesRetentionId).OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(e => new { e.TenantId, e.AccessKey }).IsUnique().HasDatabaseName("uq_sales_retention_access_key");
    }
}

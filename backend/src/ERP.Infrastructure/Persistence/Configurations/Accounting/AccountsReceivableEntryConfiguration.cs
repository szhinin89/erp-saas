using ERP.Domain.Modules.Accounting.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ERP.Infrastructure.Persistence.Configurations.Accounting;

public sealed class AccountsReceivableEntryConfiguration : IEntityTypeConfiguration<AccountsReceivableEntry>
{
    public void Configure(EntityTypeBuilder<AccountsReceivableEntry> builder)
    {
        builder.ToTable("ar_entries");

        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasColumnName("id");
        builder.Property(e => e.SubscriberId).HasColumnName("subscriber_id").IsRequired();
        builder.Property(e => e.CompanyId).HasColumnName("company_id").IsRequired();
        builder.Property(e => e.BusinessPartnerId).HasColumnName("business_partner_id").IsRequired();
        builder.Property(e => e.BusinessPartnerId).HasColumnName("business_partner_id");
        builder.Property(e => e.SalesBillId).HasColumnName("sales_bill_id");
        builder.Property(e => e.Reference).HasColumnName("reference").HasMaxLength(100).IsRequired();
        builder.Property(e => e.IssueDate).HasColumnName("issue_date").IsRequired();
        builder.Property(e => e.DueDate).HasColumnName("due_date").IsRequired();
        builder.Property(e => e.OriginalAmount).HasColumnName("original_amount").HasPrecision(18, 4).IsRequired();
        builder.Property(e => e.PaidAmount).HasColumnName("paid_amount").HasPrecision(18, 4).IsRequired().HasDefaultValue(0m);
        builder.Property(e => e.Status).HasColumnName("status").HasMaxLength(20).IsRequired();
        builder.Property(e => e.Currency).HasColumnName("currency").HasMaxLength(3).IsRequired();
        builder.Property(e => e.CreatedAt).HasColumnName("created_at");
        builder.Property(e => e.UpdatedAt).HasColumnName("updated_at");
        builder.Property(e => e.CreatedBy).HasColumnName("created_by");
        builder.Property(e => e.UpdatedBy).HasColumnName("updated_by");

        builder.Ignore(e => e.RemainingAmount);
        builder.Ignore(e => e.IsPaid);

        builder.HasIndex(e => new { e.SubscriberId, e.CompanyId, e.Status })
            .HasDatabaseName("ix_ar_entries_subscriber_company_status");

        builder.HasIndex(e => new { e.SubscriberId, e.DueDate, e.Status })
            .HasDatabaseName("ix_ar_entries_subscriber_due_status");

        builder.HasIndex(e => e.SalesBillId)
            .HasFilter("sales_bill_id IS NOT NULL")
            .HasDatabaseName("ix_ar_entries_sales_bill");
    }
}

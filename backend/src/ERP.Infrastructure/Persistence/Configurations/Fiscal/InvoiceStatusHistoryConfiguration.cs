using ERP.Domain.Modules.Fiscal.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ERP.Infrastructure.Persistence.Configurations.Fiscal;

public sealed class InvoiceStatusHistoryConfiguration : IEntityTypeConfiguration<InvoiceStatusHistory>
{
    public void Configure(EntityTypeBuilder<InvoiceStatusHistory> builder)
    {
        builder.ToTable("invoice_status_history");

        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasColumnName("id");
        builder.Property(e => e.InvoiceId).HasColumnName("invoice_id").IsRequired();
        builder.Property(e => e.SubscriberId).HasColumnName("subscriber_id").IsRequired();
        builder.Property(e => e.FromStatus).HasColumnName("from_status").HasMaxLength(InvoiceStatusHistory.StatusMaxLen);
        builder.Property(e => e.ToStatus).HasColumnName("to_status").HasMaxLength(InvoiceStatusHistory.StatusMaxLen).IsRequired();
        builder.Property(e => e.Reason).HasColumnName("reason");
        builder.Property(e => e.ChangedAt).HasColumnName("changed_at").IsRequired();
        builder.Property(e => e.ChangedBy).HasColumnName("changed_by").IsRequired();
        builder.Property(e => e.IssueDate).HasColumnName("issue_date").IsRequired();

        builder.HasIndex(e => new { e.SubscriberId, e.InvoiceId, e.ChangedAt }).HasDatabaseName("ix_invoice_status_history_invoice");
    }
}

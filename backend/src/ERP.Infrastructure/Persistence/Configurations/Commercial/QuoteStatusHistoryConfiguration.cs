using ERP.Domain.Modules.Commercial.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ERP.Infrastructure.Persistence.Configurations.Commercial;

public sealed class QuoteStatusHistoryConfiguration : IEntityTypeConfiguration<QuoteStatusHistory>
{
    public void Configure(EntityTypeBuilder<QuoteStatusHistory> builder)
    {
        builder.ToTable("quote_status_history");

        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasColumnName("id");
        builder.Property(e => e.QuoteId).HasColumnName("quote_id").IsRequired();
        builder.Property(e => e.SubscriberId).HasColumnName("subscriber_id").IsRequired();
        builder.Property(e => e.FromStatus).HasColumnName("from_status").HasMaxLength(QuoteStatusHistory.StatusMaxLen);
        builder.Property(e => e.ToStatus).HasColumnName("to_status").HasMaxLength(QuoteStatusHistory.StatusMaxLen).IsRequired();
        builder.Property(e => e.Reason).HasColumnName("reason");
        builder.Property(e => e.ChangedAt).HasColumnName("changed_at").IsRequired();
        builder.Property(e => e.ChangedBy).HasColumnName("changed_by").IsRequired();
        builder.Property(e => e.IssueDate).HasColumnName("issue_date").IsRequired();

        builder.HasIndex(e => new { e.SubscriberId, e.QuoteId, e.ChangedAt })
            .HasDatabaseName("ix_quote_status_history_quote");
    }
}

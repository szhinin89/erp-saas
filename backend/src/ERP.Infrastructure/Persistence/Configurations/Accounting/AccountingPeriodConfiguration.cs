using ERP.Domain.Modules.Accounting.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ERP.Infrastructure.Persistence.Configurations.Accounting;

public sealed class AccountingPeriodConfiguration : IEntityTypeConfiguration<AccountingPeriod>
{
    public void Configure(EntityTypeBuilder<AccountingPeriod> builder)
    {
        builder.ToTable("accounting_periods");

        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasColumnName("id");
        builder.Property(e => e.SubscriberId).HasColumnName("subscriber_id").IsRequired();
        builder.Property(e => e.Year).HasColumnName("year").IsRequired();
        builder.Property(e => e.Month).HasColumnName("month").IsRequired();
        builder.Property(e => e.Name).HasColumnName("name").HasMaxLength(AccountingPeriod.NameMaxLen).IsRequired();
        builder.Property(e => e.IsClosed).HasColumnName("is_closed").IsRequired().HasDefaultValue(false);
        builder.Property(e => e.ClosedBy).HasColumnName("closed_by");
        builder.Property(e => e.ClosedAt).HasColumnName("closed_at");
        builder.Property(e => e.CreatedAt).HasColumnName("created_at");
        builder.Property(e => e.UpdatedAt).HasColumnName("updated_at");
        builder.Property(e => e.CreatedBy).HasColumnName("created_by");
        builder.Property(e => e.UpdatedBy).HasColumnName("updated_by");

        builder.HasIndex(e => new { e.SubscriberId, e.Year, e.Month })
            .IsUnique()
            .HasDatabaseName("uq_accounting_periods_subscriber_year_month");

        builder.HasIndex(e => new { e.SubscriberId, e.IsClosed })
            .HasDatabaseName("ix_accounting_periods_subscriber_closed");
    }
}

using ERP.Domain.Modules.Accounting.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ERP.Infrastructure.Persistence.Configurations.Accounting;

public sealed class PaymentApplicationConfiguration : IEntityTypeConfiguration<PaymentApplication>
{
    public void Configure(EntityTypeBuilder<PaymentApplication> builder)
    {
        builder.ToTable("payment_applications");

        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasColumnName("id");
        builder.Property(e => e.SubscriberId).HasColumnName("subscriber_id").IsRequired();
        builder.Property(e => e.CompanyId).HasColumnName("company_id").IsRequired();
        builder.Property(e => e.ArEntryId).HasColumnName("ar_entry_id");
        builder.Property(e => e.ApEntryId).HasColumnName("ap_entry_id");
        builder.Property(e => e.Amount).HasColumnName("amount").HasPrecision(18, 4).IsRequired();
        builder.Property(e => e.ApplicationDate).HasColumnName("application_date").IsRequired();
        builder.Property(e => e.PaymentReference).HasColumnName("payment_reference").HasMaxLength(100);
        builder.Property(e => e.Notes).HasColumnName("notes").HasMaxLength(500);
        builder.Property(e => e.CreatedAt).HasColumnName("created_at");
        builder.Property(e => e.UpdatedAt).HasColumnName("updated_at");
        builder.Property(e => e.CreatedBy).HasColumnName("created_by");
        builder.Property(e => e.UpdatedBy).HasColumnName("updated_by");

        builder.HasIndex(e => e.ArEntryId)
            .HasFilter("ar_entry_id IS NOT NULL")
            .HasDatabaseName("ix_payment_applications_ar_entry");

        builder.HasIndex(e => e.ApEntryId)
            .HasFilter("ap_entry_id IS NOT NULL")
            .HasDatabaseName("ix_payment_applications_ap_entry");

        builder.HasIndex(e => new { e.SubscriberId, e.ApplicationDate })
            .HasDatabaseName("ix_payment_applications_subscriber_date");

        builder.HasIndex(e => new { e.SubscriberId, e.CompanyId })
            .HasDatabaseName("ix_payment_applications_company");
    }
}

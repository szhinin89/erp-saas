using ERP.Domain.Modules.Purchases.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ERP.Infrastructure.Persistence.Configurations.Purchases;

public sealed class PurchasePaymentScheduleConfiguration
    : IEntityTypeConfiguration<PurchasePaymentSchedule>
{
    public void Configure(EntityTypeBuilder<PurchasePaymentSchedule> builder)
    {
        builder.ToTable("purchase_payment_schedules");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id").IsRequired();
        builder.Property(x => x.TenantId).HasColumnName("tenant_id").IsRequired();
        builder
            .Property(x => x.PurchaseInvoiceId)
            .HasColumnName("purchase_invoice_id")
            .IsRequired();
        builder.Property(x => x.InstallmentNumber).HasColumnName("installment_number").IsRequired();
        builder.Property(x => x.DueDate).HasColumnName("due_date").IsRequired();
        builder
            .Property(x => x.Amount)
            .HasColumnName("amount")
            .HasColumnType("numeric(18,2)")
            .IsRequired();
        builder
            .Property(x => x.Notes)
            .HasColumnName("notes")
            .HasMaxLength(PurchasePaymentSchedule.NotesMaxLen);
        builder.Property(x => x.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(x => x.UpdatedAt).HasColumnName("updated_at");
        builder.Property(x => x.UpdatedBy).HasColumnName("updated_by");

        builder
            .HasIndex(x => new { x.TenantId, x.PurchaseInvoiceId })
            .HasDatabaseName("ix_purchase_payment_schedules_tenant_invoice");

        builder
            .HasIndex(x => new { x.TenantId, x.DueDate })
            .HasDatabaseName("ix_purchase_payment_schedules_tenant_duedate");

        builder
            .HasIndex(x => new { x.PurchaseInvoiceId, x.InstallmentNumber })
            .IsUnique()
            .HasDatabaseName("uq_purchase_payment_schedules_invoice_number");
    }
}

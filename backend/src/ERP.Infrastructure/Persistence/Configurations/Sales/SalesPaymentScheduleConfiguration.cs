using ERP.Domain.Modules.Sales.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ERP.Infrastructure.Persistence.Configurations.Sales;

/// <summary>ADR-033, Fase 4 — espejo exacto de PurchasePaymentScheduleConfiguration.</summary>
public sealed class SalesPaymentScheduleConfiguration : IEntityTypeConfiguration<SalesPaymentSchedule>
{
    public void Configure(EntityTypeBuilder<SalesPaymentSchedule> builder)
    {
        builder.ToTable("sales_payment_schedules");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id").IsRequired();
        builder.Property(x => x.TenantId).HasColumnName("tenant_id").IsRequired();
        builder.Property(x => x.SalesInvoiceId).HasColumnName("sales_invoice_id").IsRequired();
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
            .HasMaxLength(SalesPaymentSchedule.NotesMaxLen);
        builder.Property(x => x.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(x => x.UpdatedAt).HasColumnName("updated_at");
        builder.Property(x => x.UpdatedBy).HasColumnName("updated_by");

        builder
            .HasIndex(x => new { x.TenantId, x.SalesInvoiceId })
            .HasDatabaseName("ix_sales_payment_schedules_tenant_invoice");

        builder
            .HasIndex(x => new { x.TenantId, x.DueDate })
            .HasDatabaseName("ix_sales_payment_schedules_tenant_duedate");

        builder
            .HasIndex(x => new { x.SalesInvoiceId, x.InstallmentNumber })
            .IsUnique()
            .HasDatabaseName("uq_sales_payment_schedules_invoice_number");
    }
}

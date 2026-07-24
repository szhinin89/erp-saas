using ERP.Domain.Modules.Sales.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ERP.Infrastructure.Persistence.Configurations.Sales;

public sealed class SalesReceivableInstallmentConfiguration : IEntityTypeConfiguration<SalesReceivableInstallment>
{
    public void Configure(EntityTypeBuilder<SalesReceivableInstallment> builder)
    {
        builder.ToTable("sales_receivable_installments");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id").IsRequired();
        builder.Property(x => x.TenantId).HasColumnName("tenant_id").IsRequired();
        builder.Property(x => x.ReceivableId).HasColumnName("receivable_id").IsRequired();

        builder.Property(x => x.InstallmentNumber).HasColumnName("installment_number").IsRequired();
        builder.Property(x => x.DueDate).HasColumnName("due_date").IsRequired();

        builder.Property(x => x.Amount).HasColumnName("amount")
            .HasColumnType("numeric(18,2)").IsRequired();
        builder.Property(x => x.PaidAmount).HasColumnName("paid_amount")
            .HasColumnType("numeric(18,2)").IsRequired();
        builder.Property(x => x.Status).HasColumnName("status")
            .HasMaxLength(SalesReceivableInstallment.StatusMaxLen).IsRequired();

        builder.HasIndex(x => new { x.ReceivableId, x.InstallmentNumber })
            .IsUnique()
            .HasDatabaseName("uq_sales_receivable_installments_number");

        builder.HasIndex(x => new { x.TenantId, x.DueDate })
            .HasDatabaseName("ix_sales_receivable_installments_due_date");
    }
}

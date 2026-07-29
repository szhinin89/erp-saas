using ERP.Domain.Modules.Purchases.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ERP.Infrastructure.Persistence.Configurations.Purchases;

public sealed class PurchasePayableInstallmentConfiguration
    : IEntityTypeConfiguration<PurchasePayableInstallment>
{
    public void Configure(EntityTypeBuilder<PurchasePayableInstallment> builder)
    {
        builder.ToTable("purchase_payable_installments");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id").IsRequired();
        builder.Property(x => x.TenantId).HasColumnName("tenant_id").IsRequired();
        builder.Property(x => x.PayableId).HasColumnName("payable_id").IsRequired();
        builder.Property(x => x.InstallmentNumber).HasColumnName("installment_number").IsRequired();
        builder.Property(x => x.DueDate).HasColumnName("due_date").IsRequired();
        builder
            .Property(x => x.Amount)
            .HasColumnName("amount")
            .HasColumnType("numeric(18,2)")
            .IsRequired();
        builder
            .Property(x => x.PaidAmount)
            .HasColumnName("paid_amount")
            .HasColumnType("numeric(18,2)")
            .IsRequired();
        builder.Property(x => x.Status).HasColumnName("status").HasMaxLength(20).IsRequired();

        builder
            .HasIndex(x => new { x.PayableId, x.InstallmentNumber })
            .IsUnique()
            .HasDatabaseName("uq_payable_installment_number");
    }
}

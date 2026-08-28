using ERP.Domain.Branches.Entities;
using ERP.Domain.MasterData.Entities;
using ERP.Domain.Modules.Company.Entities;
using ERP.Domain.Modules.Payables.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ERP.Infrastructure.Persistence.Configurations.Payables;

public sealed class SupplierPaymentConfiguration : IEntityTypeConfiguration<SupplierPayment>
{
    public void Configure(EntityTypeBuilder<SupplierPayment> builder)
    {
        builder.ToTable("supplier_payments");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id").IsRequired();
        builder.Property(x => x.TenantId).HasColumnName("tenant_id").IsRequired();
        builder.Property(x => x.CompanyId).HasColumnName("company_id").IsRequired();
        builder.Property(x => x.BranchId).HasColumnName("branch_id").IsRequired();
        builder.Property(x => x.SupplierId).HasColumnName("supplier_id").IsRequired();
        builder.Property(x => x.PaymentDate).HasColumnName("payment_date").IsRequired();
        builder
            .Property(x => x.TotalAmount)
            .HasColumnName("total_amount")
            .HasColumnType("numeric(18,2)")
            .IsRequired();
        builder
            .Property(x => x.SystemNumber)
            .HasColumnName("system_number")
            .HasMaxLength(SupplierPayment.SystemNumberMaxLen)
            .IsRequired();
        builder
            .Property(x => x.ReceiptNumber)
            .HasColumnName("receipt_number")
            .HasMaxLength(SupplierPayment.ReceiptNumberMaxLen);
        builder.Property(x => x.Status).HasColumnName("status").HasConversion<int>().IsRequired();
        builder.Property(x => x.ReversedAtUtc).HasColumnName("reversed_at_utc");
        builder.Property(x => x.ReversedBy).HasColumnName("reversed_by");
        builder.Property(x => x.ReverseReason).HasColumnName("reverse_reason");

        builder.Ignore(x => x.DisplayNumber);

        builder.Property(x => x.CreatedAt).HasColumnName("created_at");
        builder.Property(x => x.UpdatedAt).HasColumnName("updated_at");
        builder.Property(x => x.CreatedBy).HasColumnName("created_by");
        builder.Property(x => x.UpdatedBy).HasColumnName("updated_by");

        builder
            .Property<uint>("xmin")
            .HasColumnName("xmin")
            .HasColumnType("xid")
            .IsRequired()
            .ValueGeneratedOnAddOrUpdate()
            .IsConcurrencyToken();

        builder
            .HasMany(x => x.MethodLines)
            .WithOne()
            .HasForeignKey(x => x.SupplierPaymentId)
            .OnDelete(DeleteBehavior.Cascade);
        builder
            .HasMany(x => x.ApplicationLines)
            .WithOne()
            .HasForeignKey(x => x.SupplierPaymentId)
            .OnDelete(DeleteBehavior.Cascade);
        builder
            .HasMany(x => x.AllocationLines)
            .WithOne()
            .HasForeignKey(x => x.SupplierPaymentId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<Company>().WithMany().HasForeignKey(x => x.CompanyId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Branch>().WithMany().HasForeignKey(x => x.BranchId).OnDelete(DeleteBehavior.Restrict);
        builder
            .HasOne<BusinessPartner>()
            .WithMany()
            .HasForeignKey(x => x.SupplierId)
            .OnDelete(DeleteBehavior.Restrict);

        builder
            .HasIndex(x => new { x.TenantId, x.CompanyId, x.SystemNumber })
            .IsUnique()
            .HasDatabaseName("uq_supplier_payments_tenant_company_system_number");

        builder
            .HasIndex(x => new { x.TenantId, x.CompanyId, x.SupplierId, x.PaymentDate })
            .HasDatabaseName("ix_supplier_payments_tenant_company_supplier_date");

        builder
            .HasIndex(x => new { x.TenantId, x.CompanyId, x.Status })
            .HasDatabaseName("ix_supplier_payments_tenant_company_status");

        builder
            .HasIndex(x => new { x.TenantId, x.CompanyId, x.SupplierId, x.ReceiptNumber })
            .IsUnique()
            .HasDatabaseName("uq_supplier_payments_tenant_company_supplier_receipt_number")
            .HasFilter("\"receipt_number\" IS NOT NULL");
    }
}

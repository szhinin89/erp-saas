using ERP.Domain.Branches.Entities;
using ERP.Domain.MasterData.Entities;
using ERP.Domain.Modules.Company.Entities;
using ERP.Domain.Modules.Inventory.Entities;
using ERP.Domain.Modules.Purchases.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ERP.Infrastructure.Persistence.Configurations.Purchases;

public sealed class PurchaseInvoiceConfiguration : IEntityTypeConfiguration<PurchaseInvoice>
{
    public void Configure(EntityTypeBuilder<PurchaseInvoice> builder)
    {
        builder.ToTable("purchase_invoices");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id").IsRequired();
        builder.Property(x => x.TenantId).HasColumnName("tenant_id").IsRequired();
        builder.Property(x => x.CompanyId).HasColumnName("company_id").IsRequired();
        builder.Property(x => x.BranchId).HasColumnName("branch_id").IsRequired();

        builder.Property(x => x.SupplierId).HasColumnName("supplier_id").IsRequired();
        builder
            .Property(x => x.DocTypeCode)
            .HasColumnName("doc_type_code")
            .HasMaxLength(PurchaseInvoice.DocTypeCodeMaxLen)
            .IsRequired();
        builder
            .Property(x => x.InvoiceNumber)
            .HasColumnName("invoice_number")
            .HasMaxLength(PurchaseInvoice.InvoiceNumberMaxLen)
            .IsRequired();
        builder.Property(x => x.IssueDate).HasColumnName("issue_date").IsRequired();
        builder
            .Property(x => x.AccessKey)
            .HasColumnName("access_key")
            .HasMaxLength(PurchaseInvoice.AccessKeyLen);
        builder
            .Property(x => x.AuthorizationNumber)
            .HasColumnName("authorization_number")
            .HasMaxLength(49);
        builder.Property(x => x.AuthorizationDate).HasColumnName("authorization_date");
        builder.Property(x => x.TaxSupportCode).HasColumnName("tax_support_code").HasMaxLength(5);
        builder
            .Property(x => x.SriPaymentMethodCode)
            .HasColumnName("payment_method_code")
            .HasMaxLength(PurchaseInvoice.SriPaymentMethodMaxLen);
        builder
            .Property(x => x.SriPaymentMethodName)
            .HasColumnName("payment_method_name")
            .HasMaxLength(PurchaseInvoice.SriPaymentMethodNameMaxLen);

        builder
            .Property(x => x.SupplierName)
            .HasColumnName("supplier_name")
            .HasMaxLength(PurchaseInvoice.SupplierNameMaxLen)
            .IsRequired();
        builder
            .Property(x => x.SupplierTaxId)
            .HasColumnName("supplier_tax_id")
            .HasMaxLength(PurchaseInvoice.SupplierTaxIdMaxLen)
            .IsRequired();

        builder
            .Property(x => x.CurrencyCode)
            .HasColumnName("currency_code")
            .HasMaxLength(PurchaseInvoice.CurrencyCodeMaxLen)
            .IsRequired();
        builder
            .Property(x => x.ExchangeRate)
            .HasColumnName("exchange_rate")
            .HasColumnType("numeric(18,4)")
            .IsRequired();

        builder.Property(x => x.PurchaseOrderId).HasColumnName("purchase_order_id");
        builder
            .Property(x => x.PurchaseOrderNumber)
            .HasColumnName("purchase_order_number")
            .HasMaxLength(PurchaseInvoice.PurchaseOrderNumMaxLen);

        builder.Property(x => x.GlobalWarehouseId).HasColumnName("global_warehouse_id");
        builder.Property(x => x.PaymentTermId).HasColumnName("payment_term_id").IsRequired();
        builder
            .Property(x => x.PaymentTermName)
            .HasColumnName("payment_term_name")
            .HasMaxLength(PurchaseInvoice.PaymentTermNameMaxLen)
            .IsRequired();
        builder
            .Property(x => x.PaymentTermInstallments)
            .HasColumnName("payment_term_installments")
            .IsRequired();
        builder
            .Property(x => x.PaymentTermDaysBetween)
            .HasColumnName("payment_term_days_between")
            .IsRequired();
        builder.Property(x => x.DueDate).HasColumnName("due_date");
        builder
            .Property(x => x.Notes)
            .HasColumnName("notes")
            .HasMaxLength(PurchaseInvoice.NotesMaxLen);
        builder.Property(x => x.Status).HasColumnName("status").HasConversion<int>().IsRequired();

        builder.Property(x => x.CreatedAt).HasColumnName("created_at");
        builder.Property(x => x.UpdatedAt).HasColumnName("updated_at");
        builder.Property(x => x.CreatedBy).HasColumnName("created_by");
        builder.Property(x => x.UpdatedBy).HasColumnName("updated_by");

        builder
            .Property(x => x.CancelReason)
            .HasColumnName("cancel_reason")
            .HasMaxLength(PurchaseInvoice.CancelReasonMaxLen);
        builder.Property(x => x.CancelledAt).HasColumnName("cancelled_at");
        builder.Property(x => x.CancelledBy).HasColumnName("cancelled_by");

        builder
            .Property(x => x.ConfirmedSubtotal)
            .HasColumnName("confirmed_subtotal")
            .HasColumnType("numeric(18,2)");
        builder
            .Property(x => x.ConfirmedTotalTax)
            .HasColumnName("confirmed_total_tax")
            .HasColumnType("numeric(18,2)");
        builder
            .Property(x => x.ConfirmedTotalDiscount)
            .HasColumnName("confirmed_total_discount")
            .HasColumnType("numeric(18,2)");
        builder
            .Property(x => x.ConfirmedGrandTotal)
            .HasColumnName("confirmed_grand_total")
            .HasColumnType("numeric(18,2)");

        builder
            .Property<uint>("xmin")
            .HasColumnName("xmin")
            .HasColumnType("xid")
            .IsRequired()
            .ValueGeneratedOnAddOrUpdate()
            .IsConcurrencyToken();

        builder.Ignore(x => x.Subtotal);
        builder.Ignore(x => x.TotalDiscount);
        builder.Ignore(x => x.TotalIce);
        builder.Ignore(x => x.TotalVat);
        builder.Ignore(x => x.TotalTax);
        builder.Ignore(x => x.TotalFreight);
        builder.Ignore(x => x.TotalOtherCosts);
        builder.Ignore(x => x.TotalCostValue);
        builder.Ignore(x => x.GrandTotal);
        builder.Ignore(x => x.CreditTermDays);

        builder
            .HasMany(x => x.Lines)
            .WithOne()
            .HasForeignKey(x => x.InvoiceId)
            .OnDelete(DeleteBehavior.Cascade);

        builder
            .HasMany(x => x.PaymentSchedules)
            .WithOne()
            .HasForeignKey(x => x.PurchaseInvoiceId)
            .OnDelete(DeleteBehavior.Cascade);

        builder
            .HasOne<BusinessPartner>()
            .WithMany()
            .HasForeignKey(x => x.SupplierId)
            .OnDelete(DeleteBehavior.Restrict);

        builder
            .HasOne<Company>()
            .WithMany()
            .HasForeignKey(x => x.CompanyId)
            .OnDelete(DeleteBehavior.Restrict);

        builder
            .HasOne<Branch>()
            .WithMany()
            .HasForeignKey(x => x.BranchId)
            .OnDelete(DeleteBehavior.Restrict);

        builder
            .HasOne<PaymentTerm>()
            .WithMany()
            .HasForeignKey(x => x.PaymentTermId)
            .OnDelete(DeleteBehavior.Restrict);

        builder
            .HasOne<Warehouse>()
            .WithMany()
            .HasForeignKey(x => x.GlobalWarehouseId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.SetNull);

        builder
            .HasIndex(x => new { x.TenantId, x.CompanyId })
            .HasDatabaseName("ix_purchase_invoices_tenant_company");

        builder
            .HasIndex(x => new
            {
                x.TenantId,
                x.CompanyId,
                x.SupplierId,
                x.InvoiceNumber,
            })
            .IsUnique()
            .HasDatabaseName("uq_purchase_invoices_tenant_company_supplier_number");

        builder
            .HasIndex(x => new { x.TenantId, x.IssueDate })
            .HasDatabaseName("ix_purchase_invoices_tenant_issue_date");

        builder
            .HasIndex(x => new { x.TenantId, x.Status })
            .HasDatabaseName("ix_purchase_invoices_tenant_status");

        builder
            .HasIndex(x => new { x.TenantId, x.AccessKey })
            .IsUnique()
            .HasDatabaseName("uq_purchase_invoices_tenant_access_key")
            .HasFilter("access_key IS NOT NULL");

        builder
            .HasIndex(x => x.PurchaseOrderId)
            .HasDatabaseName("ix_purchase_invoices_purchase_order")
            .HasFilter("purchase_order_id IS NOT NULL");
    }
}

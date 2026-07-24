using ERP.Domain.Modules.Sales.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ERP.Infrastructure.Persistence.Configurations.Sales;

public sealed class SalesInvoicePaymentConfiguration : IEntityTypeConfiguration<SalesInvoicePayment>
{
    public void Configure(EntityTypeBuilder<SalesInvoicePayment> builder)
    {
        builder.ToTable("sales_invoice_payments");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id").IsRequired();
        builder.Property(x => x.TenantId).HasColumnName("tenant_id").IsRequired();
        builder.Property(x => x.InvoiceId).HasColumnName("sales_invoice_id").IsRequired();

        builder.Property(x => x.PaymentMethodId).HasColumnName("payment_method_id").IsRequired();
        builder.Property(x => x.PaymentMethodCode).HasColumnName("payment_method_code")
            .HasMaxLength(SalesInvoicePayment.CodeMaxLen).IsRequired();
        builder.Property(x => x.PaymentMethodName).HasColumnName("payment_method_name")
            .HasMaxLength(SalesInvoicePayment.NameMaxLen).IsRequired();

        builder.Property(x => x.Amount).HasColumnName("amount")
            .HasColumnType("numeric(18,2)").IsRequired();

        builder.Property(x => x.Reference).HasColumnName("reference")
            .HasMaxLength(SalesInvoicePayment.ReferenceMaxLen);

        builder.Property(x => x.CreatedAt).HasColumnName("created_at").IsRequired();

        // ── Detail 1:1 tables (OwnsOne → separate table) ───────────
        builder.OwnsOne(x => x.CardDetail, cd =>
        {
            cd.ToTable("payment_card_details");
            cd.WithOwner().HasForeignKey("PaymentId");
            cd.Property(c => c.PaymentId).HasColumnName("payment_id").IsRequired();
            cd.Property(c => c.CardBrand).HasColumnName("card_brand").HasMaxLength(PaymentCardDetail.BrandMaxLen);
            cd.Property(c => c.CardLastFour).HasColumnName("card_last_four").HasMaxLength(PaymentCardDetail.LastFourMaxLen);
            cd.Property(c => c.BankName).HasColumnName("bank_name").HasMaxLength(PaymentCardDetail.BankMaxLen);
            cd.Property(c => c.AuthorizationCode).HasColumnName("authorization_code").HasMaxLength(PaymentCardDetail.AuthCodeMaxLen);
            cd.Property(c => c.LotNumber).HasColumnName("lot_number").HasMaxLength(PaymentCardDetail.LotMaxLen);
        });

        builder.OwnsOne(x => x.TransferDetail, td =>
        {
            td.ToTable("payment_transfer_details");
            td.WithOwner().HasForeignKey("PaymentId");
            td.Property(t => t.PaymentId).HasColumnName("payment_id").IsRequired();
            td.Property(t => t.BankName).HasColumnName("bank_name").HasMaxLength(PaymentTransferDetail.BankMaxLen);
            td.Property(t => t.ReceiptNumber).HasColumnName("receipt_number").HasMaxLength(PaymentTransferDetail.ReceiptMaxLen);
            td.Property(t => t.TransferDate).HasColumnName("transfer_date");
        });

        builder.OwnsOne(x => x.ChequeDetail, ch =>
        {
            ch.ToTable("payment_cheque_details");
            ch.WithOwner().HasForeignKey("PaymentId");
            ch.Property(c => c.PaymentId).HasColumnName("payment_id").IsRequired();
            ch.Property(c => c.BankName).HasColumnName("bank_name").HasMaxLength(PaymentChequeDetail.BankMaxLen);
            ch.Property(c => c.ChequeNumber).HasColumnName("cheque_number").HasMaxLength(PaymentChequeDetail.NumberMaxLen);
            ch.Property(c => c.HolderName).HasColumnName("holder_name").HasMaxLength(PaymentChequeDetail.HolderMaxLen);
            ch.Property(c => c.CashDate).HasColumnName("cash_date");
        });

        // ── Indexes ────────────────────────────────────────────────────
        builder.HasIndex(x => new { x.TenantId, x.InvoiceId })
            .HasDatabaseName("ix_sales_invoice_payments_tenant_invoice");

        builder.HasIndex(x => x.PaymentMethodId)
            .HasDatabaseName("ix_sales_invoice_payments_method");
    }
}

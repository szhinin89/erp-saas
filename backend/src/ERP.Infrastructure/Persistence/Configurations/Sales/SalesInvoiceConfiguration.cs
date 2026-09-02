using ERP.Domain.Branches.Entities;
using ERP.Domain.MasterData.Entities;
using ERP.Domain.Modules.Caja.Entities;
using ERP.Domain.Modules.Company.Entities;
using ERP.Domain.Modules.Company.Enums;
using ERP.Domain.Modules.Sales.Entities;
using ERP.Domain.Modules.Sales.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ERP.Infrastructure.Persistence.Configurations.Sales;

public sealed class SalesInvoiceConfiguration : IEntityTypeConfiguration<SalesInvoice>
{
    public void Configure(EntityTypeBuilder<SalesInvoice> builder)
    {
        builder.ToTable("sales_invoices");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id").IsRequired();
        builder.Property(x => x.TenantId).HasColumnName("tenant_id").IsRequired();
        builder.Property(x => x.CompanyId).HasColumnName("company_id").IsRequired();
        builder.Property(x => x.BranchId).HasColumnName("branch_id").IsRequired();
        builder.Property(x => x.CashSessionId).HasColumnName("cash_session_id").IsRequired();

        // ── Documento ───────────────────────────────────────────────
        builder
            .Property(x => x.DocTypeCode)
            .HasColumnName("doc_type_code")
            .HasMaxLength(SalesInvoice.DocTypeCodeMaxLen)
            .IsRequired();
        builder
            .Property(x => x.InvoiceNumber)
            .HasColumnName("invoice_number")
            .HasMaxLength(SalesInvoice.InvoiceNumberMaxLen)
            .IsRequired();
        builder.Property(x => x.IssueDate).HasColumnName("issue_date").IsRequired();
        builder.Property(x => x.EmissionPointId).HasColumnName("emission_point_id");
        builder
            .Property(x => x.EmissionType)
            .HasColumnName("emission_type")
            .HasConversion<short>()
            .IsRequired()
            .HasDefaultValue(EmissionType.Electronic)
            .HasSentinel((EmissionType)0);

        builder
            .Property(x => x.SriPaymentMethodCode)
            .HasColumnName("sri_payment_method_code")
            .HasMaxLength(5);

        // ── Cliente VO (OwnsOne — aplanado) ─────────────────────────
        builder.Property(x => x.CustomerId).HasColumnName("customer_id").IsRequired();
        builder.OwnsOne(
            x => x.Customer,
            cs =>
            {
                cs.Property(c => c.Name)
                    .HasColumnName("customer_name")
                    .HasMaxLength(CustomerSnapshot.NameMaxLen)
                    .IsRequired();
                cs.Property(c => c.TaxId)
                    .HasColumnName("customer_tax_id")
                    .HasMaxLength(CustomerSnapshot.TaxIdMaxLen)
                    .IsRequired();
                cs.Property(c => c.IdentificationType)
                    .HasColumnName("customer_id_type")
                    .HasMaxLength(CustomerSnapshot.IdTypeMaxLen)
                    .IsRequired();
                cs.Property(c => c.Email)
                    .HasColumnName("customer_email")
                    .HasMaxLength(CustomerSnapshot.EmailMaxLen);
                cs.Property(c => c.Address)
                    .HasColumnName("customer_address")
                    .HasMaxLength(CustomerSnapshot.AddressMaxLen);
            }
        );

        // ── Moneda ──────────────────────────────────────────────────
        builder
            .Property(x => x.CurrencyCode)
            .HasColumnName("currency_code")
            .HasMaxLength(SalesInvoice.CurrencyCodeMaxLen)
            .IsRequired();
        builder
            .Property(x => x.ExchangeRate)
            .HasColumnName("exchange_rate")
            .HasColumnType("numeric(18,4)")
            .IsRequired();

        // ── Condición de pago VO (OwnsOne — aplanado) ───────────────
        builder.OwnsOne(
            x => x.PaymentTerm,
            pt =>
            {
                pt.Property(p => p.Id).HasColumnName("payment_term_id").IsRequired();
                pt.Property(p => p.Name)
                    .HasColumnName("payment_term_name")
                    .HasMaxLength(PaymentTermSnapshot.NameMaxLen)
                    .IsRequired();
                pt.Property(p => p.Installments)
                    .HasColumnName("payment_term_installments")
                    .IsRequired();
                pt.Property(p => p.DaysBetween)
                    .HasColumnName("payment_term_days_between")
                    .IsRequired();
                pt.Ignore(p => p.CreditTermDays);
            }
        );

        builder.Property(x => x.DueDate).HasColumnName("due_date");
        builder
            .Property(x => x.Notes)
            .HasColumnName("notes")
            .HasMaxLength(SalesInvoice.NotesMaxLen);

        // ── Estado interno ──────────────────────────────────────────
        builder.Property(x => x.Status).HasColumnName("status").HasConversion<int>().IsRequired();

        // Fase 10: el estado electrónico ya no vive en SalesInvoice — ElectronicDocument es la
        // única fuente de verdad (ver ERP.Domain/Modules/ElectronicDocuments). Columnas
        // electronic_status/access_key/authorization_number/authorization_date eliminadas de
        // esta tabla vía migración.

        // ── Auditoría ───────────────────────────────────────────────
        builder.Property(x => x.CreatedAt).HasColumnName("created_at");
        builder.Property(x => x.UpdatedAt).HasColumnName("updated_at");
        builder.Property(x => x.CreatedBy).HasColumnName("created_by");
        builder.Property(x => x.UpdatedBy).HasColumnName("updated_by");

        // ── Anulación ───────────────────────────────────────────────
        builder
            .Property(x => x.CancelReason)
            .HasColumnName("cancel_reason")
            .HasMaxLength(SalesInvoice.CancelReasonMaxLen);
        builder.Property(x => x.CancelledAt).HasColumnName("cancelled_at");
        builder.Property(x => x.CancelledBy).HasColumnName("cancelled_by");

        // ── Totales snapshot ────────────────────────────────────────
        builder
            .Property(x => x.AuthorizedSubtotal)
            .HasColumnName("authorized_subtotal")
            .HasColumnType("numeric(18,2)");
        builder
            .Property(x => x.AuthorizedTotalTax)
            .HasColumnName("authorized_total_tax")
            .HasColumnType("numeric(18,2)");
        builder
            .Property(x => x.AuthorizedTotalDiscount)
            .HasColumnName("authorized_total_discount")
            .HasColumnType("numeric(18,2)");
        builder
            .Property(x => x.AuthorizedGrandTotal)
            .HasColumnName("authorized_grand_total")
            .HasColumnType("numeric(18,2)");

        // ── Concurrency token (PostgreSQL xid) ──────────────────────
        builder
            .Property<uint>("xmin")
            .HasColumnName("xmin")
            .HasColumnType("xid")
            .IsRequired()
            .ValueGeneratedOnAddOrUpdate()
            .IsConcurrencyToken();

        // ── Computed properties (NOT persisted) ─────────────────────
        builder.Ignore(x => x.Subtotal);
        builder.Ignore(x => x.TotalDiscount);
        builder.Ignore(x => x.TotalIce);
        builder.Ignore(x => x.TotalVat);
        builder.Ignore(x => x.TotalTax);
        builder.Ignore(x => x.GrandTotal);
        builder.Ignore(x => x.CreditTermDays);

        // ── Relationships ───────────────────────────────────────────
        builder
            .HasMany(x => x.Lines)
            .WithOne()
            .HasForeignKey(x => x.InvoiceId)
            .OnDelete(DeleteBehavior.Cascade);

        builder
            .HasMany(x => x.Payments)
            .WithOne()
            .HasForeignKey(x => x.InvoiceId)
            .OnDelete(DeleteBehavior.Cascade);

        builder
            .HasOne<BusinessPartner>()
            .WithMany()
            .HasForeignKey(x => x.CustomerId)
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
            .HasOne<CashSession>()
            .WithMany()
            .HasForeignKey(x => x.CashSessionId)
            .OnDelete(DeleteBehavior.Restrict);

        builder
            .HasOne<EmissionPoint>()
            .WithMany()
            .HasForeignKey(x => x.EmissionPointId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.SetNull);

        // ── Indexes ─────────────────────────────────────────────────
        builder
            .HasIndex(x => new { x.TenantId, x.CompanyId })
            .HasDatabaseName("ix_sales_invoices_tenant_company");

        builder
            .HasIndex(x => new
            {
                x.TenantId,
                x.CompanyId,
                x.InvoiceNumber,
            })
            .IsUnique()
            .HasDatabaseName("uq_sales_invoices_tenant_company_number");

        builder
            .HasIndex(x => new { x.TenantId, x.IssueDate })
            .HasDatabaseName("ix_sales_invoices_tenant_issue_date");

        builder
            .HasIndex(x => new { x.TenantId, x.Status })
            .HasDatabaseName("ix_sales_invoices_tenant_status");

        builder
            .HasIndex(x => new { x.TenantId, x.CustomerId })
            .HasDatabaseName("ix_sales_invoices_tenant_customer");

        builder.HasIndex(x => x.CashSessionId).HasDatabaseName("ix_sales_invoices_cash_session");
    }
}

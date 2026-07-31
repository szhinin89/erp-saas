using ERP.Domain.MasterData.Entities;
using ERP.Domain.Modules.Company.Entities;
using ERP.Domain.Modules.Sales.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ERP.Infrastructure.Persistence.Configurations.Sales;

public sealed class SalesReturnConfiguration : IEntityTypeConfiguration<SalesReturn>
{
    public void Configure(EntityTypeBuilder<SalesReturn> builder)
    {
        builder.ToTable("sales_returns");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id").IsRequired();
        builder.Property(x => x.TenantId).HasColumnName("tenant_id").IsRequired();
        builder.Property(x => x.CompanyId).HasColumnName("company_id").IsRequired();

        builder.Property(x => x.SalesInvoiceId).HasColumnName("sales_invoice_id").IsRequired();
        builder.Property(x => x.CustomerId).HasColumnName("customer_id").IsRequired();

        builder
            .Property(x => x.ReturnNumber)
            .HasColumnName("return_number")
            .HasMaxLength(SalesReturn.ReturnNumberMaxLen)
            .IsRequired();
        builder
            .Property(x => x.Reason)
            .HasColumnName("reason")
            .HasMaxLength(SalesReturn.ReasonMaxLen)
            .IsRequired();

        builder.Property(x => x.Status).HasColumnName("status").HasConversion<int>().IsRequired();

        builder
            .Property(x => x.CreditNoteDocumentNumber)
            .HasColumnName("credit_note_document_number")
            .HasMaxLength(SalesInvoice.InvoiceNumberMaxLen);

        // ── Totales snapshot (congelados al autorizar) ──────────────
        builder
            .Property(x => x.AuthorizedSubtotal)
            .HasColumnName("authorized_subtotal")
            .HasColumnType("numeric(18,2)");
        builder
            .Property(x => x.AuthorizedTotalVat)
            .HasColumnName("authorized_total_vat")
            .HasColumnType("numeric(18,2)");
        builder
            .Property(x => x.AuthorizedTotalIce)
            .HasColumnName("authorized_total_ice")
            .HasColumnType("numeric(18,2)");
        builder
            .Property(x => x.AuthorizedTotalDiscount)
            .HasColumnName("authorized_total_discount")
            .HasColumnType("numeric(18,2)");
        builder
            .Property(x => x.AuthorizedGrandTotal)
            .HasColumnName("authorized_grand_total")
            .HasColumnType("numeric(18,2)");

        // ── Auditoría ───────────────────────────────────────────────
        builder.Property(x => x.CreatedAt).HasColumnName("created_at");
        builder.Property(x => x.UpdatedAt).HasColumnName("updated_at");
        builder.Property(x => x.CreatedBy).HasColumnName("created_by");
        builder.Property(x => x.UpdatedBy).HasColumnName("updated_by");

        // ── Concurrency token (PostgreSQL xid) — mismo patrón que SalesInvoice/CashSession ──
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
        builder.Ignore(x => x.GrandTotal);

        // ── Relationships ───────────────────────────────────────────
        builder
            .HasMany(x => x.Lines)
            .WithOne()
            .HasForeignKey(x => x.ReturnId)
            .OnDelete(DeleteBehavior.Cascade);

        builder
            .HasMany(x => x.RefundAllocations)
            .WithOne()
            .HasForeignKey(x => x.ReturnId)
            .OnDelete(DeleteBehavior.Cascade);

        builder
            .HasOne<SalesInvoice>()
            .WithMany()
            .HasForeignKey(x => x.SalesInvoiceId)
            .OnDelete(DeleteBehavior.Restrict);

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

        // ── Indexes ─────────────────────────────────────────────────
        builder
            .HasIndex(x => new { x.TenantId, x.CompanyId })
            .HasDatabaseName("ix_sales_returns_tenant_company");

        builder
            .HasIndex(x => new
            {
                x.TenantId,
                x.CompanyId,
                x.ReturnNumber,
            })
            .IsUnique()
            .HasDatabaseName("uq_sales_returns_tenant_company_number");

        builder
            .HasIndex(x => new { x.TenantId, x.SalesInvoiceId })
            .HasDatabaseName("ix_sales_returns_tenant_sales_invoice");

        builder
            .HasIndex(x => new { x.TenantId, x.Status })
            .HasDatabaseName("ix_sales_returns_tenant_status");

        builder
            .HasIndex(x => new { x.TenantId, x.CustomerId })
            .HasDatabaseName("ix_sales_returns_tenant_customer");
    }
}

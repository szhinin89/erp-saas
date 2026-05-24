using ERP.Domain.Modules.Purchases.Entities;
using ERP.Domain.Modules.SriCatalogs.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ERP.Infrastructure.Persistence.Configurations.Purchases;

public class PurchaseInvoiceConfiguration : IEntityTypeConfiguration<PurchaseInvoice>
{
    public void Configure(EntityTypeBuilder<PurchaseInvoice> builder)
    {
        builder.ToTable("purchase_invoice");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id").HasDefaultValueSql("gen_random_uuid()");
        builder.Property(x => x.CompanyId).HasColumnName("company_id").IsRequired();
        builder.Property(x => x.BusinessPartnerId).HasColumnName("business_partner_id").IsRequired();
        builder.Property(x => x.InvoiceNumber).HasColumnName("invoice_number").HasMaxLength(50).IsRequired();
        builder.Property(x => x.AccessKey).HasColumnName("access_key").HasMaxLength(49).IsFixedLength();
        builder.Property(x => x.XmlPath).HasColumnName("xml_path").HasMaxLength(500);
        builder.Property(x => x.DocType).HasColumnName("doc_type").HasMaxLength(5).HasDefaultValue("01");
        builder.Property(x => x.InvoiceDate).HasColumnName("invoice_date");
        builder.Property(x => x.DueDate).HasColumnName("due_date");
        builder.Property(x => x.Subtotal).HasColumnName("subtotal").HasPrecision(18, 4).HasDefaultValue(0m);
        builder.Property(x => x.VatTotal).HasColumnName("vat_total").HasPrecision(18, 4).HasDefaultValue(0m);
        builder.Property(x => x.Total).HasColumnName("total").HasPrecision(18, 4).HasDefaultValue(0m);
        builder.Property(x => x.NotesApplied).HasColumnName("notes_applied").HasPrecision(18, 4).HasDefaultValue(0m);
        builder.Property(x => x.PaymentTerms).HasColumnName("payment_terms").HasMaxLength(50);
        builder.Property(x => x.TaxSupportCode).HasColumnName("tax_support_code").HasMaxLength(5);
        builder.Property(x => x.Status).HasColumnName("status").HasMaxLength(20).HasDefaultValue("draft");
        builder.Property(x => x.ValidatedBy).HasColumnName("validated_by");
        builder.Property(x => x.ValidatedAt).HasColumnName("validated_at");
        builder.Property(x => x.ApprovedBy).HasColumnName("approved_by");
        builder.Property(x => x.ApprovedAt).HasColumnName("approved_at");
        builder.Property(x => x.RejectedBy).HasColumnName("rejected_by");
        builder.Property(x => x.RejectedAt).HasColumnName("rejected_at");
        builder.Property(x => x.RejectionReason).HasColumnName("rejection_reason").HasMaxLength(500);
        builder.Property(x => x.JournalEntryId).HasColumnName("journal_entry_id");
        builder.Property(x => x.Notes).HasColumnName("notes").HasMaxLength(1000);
        builder.Property(x => x.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("NOW()");
        builder.Property(x => x.UpdatedAt).HasColumnName("updated_at").HasDefaultValueSql("NOW()");
        builder.Property(x => x.CreatedBy).HasColumnName("created_by");

        // Unicidad: mismo n�mero de factura por Supplier; clave de acceso �nica por empresa
        builder.HasIndex(x => new { x.CompanyId, x.BusinessPartnerId, x.InvoiceNumber })
            .IsUnique().HasDatabaseName("uq_pi_number");
        builder.HasIndex(x => new { x.CompanyId, x.AccessKey })
            .IsUnique().HasFilter("access_key IS NOT NULL").HasDatabaseName("uq_pi_key");
        builder.HasIndex(x => new { x.CompanyId, x.Status }).HasDatabaseName("idx_pi_company");
        builder.HasIndex(x => new { x.CompanyId, x.BusinessPartnerId, x.Status }).HasDatabaseName("idx_pi_supplier");
        builder.HasIndex(x => new { x.CompanyId, x.InvoiceDate }).HasDatabaseName("idx_pi_date");

        builder.HasOne(x => x.TaxSupport)
            .WithMany().HasForeignKey(x => x.TaxSupportCode)
            .IsRequired(false).OnDelete(DeleteBehavior.Restrict);
    }
}

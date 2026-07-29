using ERP.Domain.MasterData.Entities;
using ERP.Domain.Modules.Company.Entities;
using ERP.Domain.Modules.Purchases.Entities;
using ERP.Domain.Modules.Purchases.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ERP.Infrastructure.Persistence.Configurations.Purchases;

public sealed class IssuedWithholdingConfiguration : IEntityTypeConfiguration<IssuedWithholding>
{
    public void Configure(EntityTypeBuilder<IssuedWithholding> builder)
    {
        builder.ToTable("issued_withholdings");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id").IsRequired();
        builder.Property(x => x.TenantId).HasColumnName("tenant_id").IsRequired();
        builder.Property(x => x.CompanyId).HasColumnName("company_id").IsRequired();

        // ── Core ────────────────────────────────────────────────────────
        builder
            .Property(x => x.PurchaseInvoiceId)
            .HasColumnName("purchase_invoice_id")
            .IsRequired();
        builder.Property(x => x.SupplierId).HasColumnName("supplier_id").IsRequired();
        builder.Property(x => x.EmissionPointId).HasColumnName("emission_point_id").IsRequired();
        builder
            .Property(x => x.WithholdingNumber)
            .HasColumnName("withholding_number")
            .HasMaxLength(IssuedWithholding.NumberMaxLen)
            .IsRequired();
        builder.Property(x => x.IssueDate).HasColumnName("issue_date").IsRequired();

        // ── Totals ──────────────────────────────────────────────────────
        builder
            .Property(x => x.TotalRetainedVat)
            .HasColumnName("total_retained_vat")
            .HasColumnType("numeric(18,2)")
            .IsRequired();
        builder
            .Property(x => x.TotalRetainedIncome)
            .HasColumnName("total_retained_income")
            .HasColumnType("numeric(18,2)")
            .IsRequired();
        builder
            .Property(x => x.TotalRetainedIsd)
            .HasColumnName("total_retained_isd")
            .HasColumnType("numeric(18,2)")
            .IsRequired();
        builder
            .Property(x => x.TotalRetained)
            .HasColumnName("total_retained")
            .HasColumnType("numeric(18,2)")
            .IsRequired();

        // ── Status ──────────────────────────────────────────────────────
        builder.Property(x => x.Status).HasColumnName("status").HasConversion<int>().IsRequired();
        builder
            .Property(x => x.CancelReason)
            .HasColumnName("cancel_reason")
            .HasMaxLength(IssuedWithholding.CancelReasonMaxLen);
        builder.Property(x => x.CancelledAt).HasColumnName("cancelled_at");
        builder.Property(x => x.CancelledBy).HasColumnName("cancelled_by");

        // ── SRI Electronic Document ─────────────────────────────────────
        builder
            .Property(x => x.AccessKey)
            .HasColumnName("access_key")
            .HasMaxLength(IssuedWithholding.AccessKeyLen);
        builder
            .Property(x => x.XmlPath)
            .HasColumnName("xml_path")
            .HasMaxLength(IssuedWithholding.PathMaxLen);
        builder
            .Property(x => x.SignedXmlPath)
            .HasColumnName("signed_xml_path")
            .HasMaxLength(IssuedWithholding.PathMaxLen);
        builder
            .Property(x => x.PdfPath)
            .HasColumnName("pdf_path")
            .HasMaxLength(IssuedWithholding.PathMaxLen);
        builder
            .Property(x => x.SriStatus)
            .HasColumnName("sri_status")
            .HasConversion<int>()
            .HasDefaultValue(SriDocumentStatus.None)
            .IsRequired();
        builder
            .Property(x => x.SriReceiptNumber)
            .HasColumnName("sri_receipt_number")
            .HasMaxLength(IssuedWithholding.SriReceiptMaxLen);
        builder
            .Property(x => x.SriAuthorizationNumber)
            .HasColumnName("sri_authorization_number")
            .HasMaxLength(IssuedWithholding.AccessKeyLen);
        builder.Property(x => x.SriAuthorizationDate).HasColumnName("sri_authorization_date");
        builder
            .Property(x => x.SriMessage)
            .HasColumnName("sri_message")
            .HasMaxLength(IssuedWithholding.SriMessageMaxLen);

        // ── Audit ───────────────────────────────────────────────────────
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

        // ── Relationships ───────────────────────────────────────────────
        builder
            .HasMany(x => x.Details)
            .WithOne()
            .HasForeignKey(x => x.WithholdingId)
            .OnDelete(DeleteBehavior.Cascade);

        builder
            .HasOne<PurchaseInvoice>()
            .WithMany()
            .HasForeignKey(x => x.PurchaseInvoiceId)
            .OnDelete(DeleteBehavior.Restrict);
        builder
            .HasOne<BusinessPartner>()
            .WithMany()
            .HasForeignKey(x => x.SupplierId)
            .OnDelete(DeleteBehavior.Restrict);
        builder
            .HasOne<EmissionPoint>()
            .WithMany()
            .HasForeignKey(x => x.EmissionPointId)
            .OnDelete(DeleteBehavior.Restrict);

        // ── Indexes ─────────────────────────────────────────────────────
        builder
            .HasIndex(x => x.PurchaseInvoiceId)
            .IsUnique()
            .HasDatabaseName("uq_issued_withholdings_purchase");
        builder
            .HasIndex(x => new { x.TenantId, x.CompanyId })
            .HasDatabaseName("ix_issued_withholdings_tenant_company");
        builder
            .HasIndex(x => new
            {
                x.TenantId,
                x.CompanyId,
                x.WithholdingNumber,
            })
            .IsUnique()
            .HasDatabaseName("uq_issued_withholdings_number");
    }
}

using ERP.Domain.Branches.Entities;
using ERP.Domain.MasterData.Entities;
using ERP.Domain.Modules.Company.Entities;
using ERP.Domain.Modules.Purchases.Entities;
using ERP.Domain.Modules.Purchases.PurchaseReception.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ERP.Infrastructure.Persistence.Configurations.Purchases;

public sealed class PurchaseReceptionDocumentConfiguration : IEntityTypeConfiguration<PurchaseReceptionDocument>
{
    public void Configure(EntityTypeBuilder<PurchaseReceptionDocument> builder)
    {
        builder.ToTable("purchase_reception_documents");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id").IsRequired();
        builder.Property(x => x.TenantId).HasColumnName("tenant_id").IsRequired();
        builder.Property(x => x.CompanyId).HasColumnName("company_id").IsRequired();
        builder.Property(x => x.BranchId).HasColumnName("branch_id").IsRequired();

        builder.Property(x => x.SourceDocType).HasColumnName("source_doc_type")
            .HasConversion<int>().IsRequired();

        builder.Property(x => x.SupplierRuc).HasColumnName("supplier_ruc")
            .HasMaxLength(PurchaseReceptionDocument.SupplierRucMaxLen).IsRequired();
        builder.Property(x => x.SupplierName).HasColumnName("supplier_name")
            .HasMaxLength(PurchaseReceptionDocument.SupplierNameMaxLen).IsRequired();
        builder.Property(x => x.SupplierId).HasColumnName("supplier_id");

        builder.Property(x => x.AccessKey).HasColumnName("access_key")
            .HasMaxLength(PurchaseReceptionDocument.AccessKeyMaxLen).IsRequired();
        builder.Property(x => x.InvoiceNumber).HasColumnName("invoice_number")
            .HasMaxLength(PurchaseReceptionDocument.InvoiceNumberMaxLen).IsRequired();
        builder.Property(x => x.IssueDate).HasColumnName("issue_date").IsRequired();
        builder.Property(x => x.AuthorizationDate).HasColumnName("authorization_date");
        builder.Property(x => x.AuthorizationNumber).HasColumnName("authorization_number")
            .HasMaxLength(PurchaseReceptionDocument.AuthorizationNumberMaxLen);
        builder.Property(x => x.XmlDownloadedAt).HasColumnName("xml_downloaded_at");

        builder.Property(x => x.Subtotal).HasColumnName("subtotal")
            .HasColumnType("numeric(18,2)").IsRequired();
        builder.Property(x => x.VatAmount).HasColumnName("vat_amount")
            .HasColumnType("numeric(18,2)").IsRequired();
        builder.Property(x => x.TotalAmount).HasColumnName("total_amount")
            .HasColumnType("numeric(18,2)").IsRequired();

        builder.Property(x => x.Status).HasColumnName("status")
            .HasConversion<int>().IsRequired();

        builder.Property(x => x.PurchaseId).HasColumnName("purchase_id");
        builder.Property(x => x.XmlContent).HasColumnName("xml_content")
            .HasColumnType("text");

        builder.Property(x => x.CreatedAt).HasColumnName("created_at");
        builder.Property(x => x.UpdatedAt).HasColumnName("updated_at");
        builder.Property(x => x.CreatedBy).HasColumnName("created_by");
        builder.Property(x => x.UpdatedBy).HasColumnName("updated_by");

        builder.Property<uint>("xmin")
            .HasColumnName("xmin")
            .HasColumnType("xid")
            .IsRequired()
            .ValueGeneratedOnAddOrUpdate()
            .IsConcurrencyToken();

        builder.HasMany(x => x.Lines)
            .WithOne()
            .HasForeignKey(x => x.PurchaseReceptionDocumentId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<Company>()
            .WithMany()
            .HasForeignKey(x => x.CompanyId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Branch>()
            .WithMany()
            .HasForeignKey(x => x.BranchId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<BusinessPartner>()
            .WithMany()
            .HasForeignKey(x => x.SupplierId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.SetNull);

        // No modifica ni depende del ciclo de vida de PurchaseInvoice — solo referencia opcional a su Id.
        builder.HasOne<PurchaseInvoice>()
            .WithMany()
            .HasForeignKey(x => x.PurchaseId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasIndex(x => new { x.TenantId, x.CompanyId })
            .HasDatabaseName("ix_purchase_reception_documents_tenant_company");

        builder.HasIndex(x => new { x.TenantId, x.AccessKey })
            .IsUnique()
            .HasDatabaseName("uq_purchase_reception_documents_tenant_access_key");

        builder.HasIndex(x => new { x.TenantId, x.Status })
            .HasDatabaseName("ix_purchase_reception_documents_tenant_status");
    }
}

using ERP.Domain.Modules.ElectronicDocuments.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ERP.Infrastructure.Persistence.Configurations.ElectronicDocuments;

public class ElectronicDocConfiguration : IEntityTypeConfiguration<ElectronicDoc>
{
    public void Configure(EntityTypeBuilder<ElectronicDoc> builder)
    {
        builder.ToTable("electronic_doc");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id").HasDefaultValueSql("gen_random_uuid()");
        // Multi-tenant scope: subscriber_id backfilled from companies.subscriber_id
        builder.Property(x => x.SubscriberId).HasColumnName("subscriber_id").IsRequired();
        builder.Property(x => x.CompanyId).HasColumnName("company_id").IsRequired();
        builder.Property(x => x.EmissionPointId).HasColumnName("emission_point_id").IsRequired();
        builder.Property(x => x.DocTypeCode).HasColumnName("doc_type_code").HasMaxLength(5).IsRequired();
        builder.Property(x => x.EstablishmentCode).HasColumnName("establishment_code").HasMaxLength(3).IsFixedLength().IsRequired();
        builder.Property(x => x.EmissionPointCode).HasColumnName("emission_point_code").HasMaxLength(3).IsFixedLength().IsRequired();
        builder.Property(x => x.Sequential).HasColumnName("sequential").HasMaxLength(9).IsRequired();
        builder.Property(x => x.AccessKey).HasColumnName("access_key").HasMaxLength(49).IsFixedLength();
        builder.Property(x => x.IssueDate).HasColumnName("issue_date");
        builder.Property(x => x.AuthDate).HasColumnName("auth_date");
        builder.Property(x => x.AuthNumber).HasColumnName("auth_number").HasMaxLength(49);
        builder.Property(x => x.Status).HasColumnName("status").HasMaxLength(20).HasDefaultValue("draft");
        builder.Property(x => x.XmlSignedPath).HasColumnName("xml_signed_path").HasMaxLength(500);
        builder.Property(x => x.XmlAuthPath).HasColumnName("xml_auth_path").HasMaxLength(500);
        builder.Property(x => x.ErrorCode).HasColumnName("error_code").HasMaxLength(10);
        builder.Property(x => x.ErrorMessage).HasColumnName("error_message").HasMaxLength(1000);
        builder.Property(x => x.SubtotalVat0).HasColumnName("subtotal_vat0").HasPrecision(18, 4).HasDefaultValue(0m);
        builder.Property(x => x.SubtotalTaxable).HasColumnName("subtotal_taxable").HasPrecision(18, 4).HasDefaultValue(0m);
        builder.Property(x => x.SubtotalExempt).HasColumnName("subtotal_exempt").HasPrecision(18, 4).HasDefaultValue(0m);
        builder.Property(x => x.SubtotalNoObject).HasColumnName("subtotal_no_object").HasPrecision(18, 4).HasDefaultValue(0m);
        builder.Property(x => x.SubtotalNoVatObj).HasColumnName("subtotal_no_vat_obj").HasPrecision(18, 4).HasDefaultValue(0m);
        builder.Property(x => x.TotalDiscount).HasColumnName("total_discount").HasPrecision(18, 4).HasDefaultValue(0m);
        builder.Property(x => x.TotalVat).HasColumnName("total_vat").HasPrecision(18, 4).HasDefaultValue(0m);
        builder.Property(x => x.TotalIce).HasColumnName("total_ice").HasPrecision(18, 4).HasDefaultValue(0m);
        builder.Property(x => x.TotalOtherTaxes).HasColumnName("total_other_taxes").HasPrecision(18, 4).HasDefaultValue(0m);
        builder.Property(x => x.Total).HasColumnName("total").HasPrecision(18, 4).HasDefaultValue(0m);
        builder.Property(x => x.AdditionalInfo).HasColumnName("additional_info").HasColumnType("jsonb");
        builder.Property(x => x.JournalEntryId).HasColumnName("journal_entry_id");
        builder.Property(x => x.Notes).HasColumnName("notes").HasMaxLength(1000);
        builder.Property(x => x.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("NOW()");
        builder.Property(x => x.UpdatedAt).HasColumnName("updated_at").HasDefaultValueSql("NOW()");
        builder.Property(x => x.CreatedBy).HasColumnName("created_by");
        builder.Property(x => x.UpdatedBy).HasColumnName("updated_by");

        // Índices
        // Multi-tenant query filter index — must match ix_electronic_doc_subscriber_id in migration
        builder.HasIndex(x => x.SubscriberId).HasDatabaseName("ix_electronic_doc_subscriber_id");
        builder.HasIndex(x => x.AccessKey)
            .IsUnique().HasFilter("access_key IS NOT NULL").HasDatabaseName("idx_edoc_access_key");
        builder.HasIndex(x => new { x.CompanyId, x.DocTypeCode, x.EstablishmentCode, x.EmissionPointCode, x.Sequential })
            .IsUnique().HasDatabaseName("uq_edoc_seq");
        builder.HasIndex(x => new { x.CompanyId, x.IssueDate }).HasDatabaseName("idx_edoc_company");
        builder.HasIndex(x => new { x.CompanyId, x.Status, x.DocTypeCode }).HasDatabaseName("idx_edoc_status");

        // FKs
        builder.HasOne(x => x.EmissionPoint).WithMany().HasForeignKey(x => x.EmissionPointId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.DocType).WithMany().HasForeignKey(x => x.DocTypeCode).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.Error).WithMany().HasForeignKey(x => x.ErrorCode).IsRequired(false).OnDelete(DeleteBehavior.Restrict);

        // Extensiones 1:1 (shared primary key — el Id de la extensión ES el FK a ElectronicDoc.Id).
        // .IsRequired(false) en la navegación inverse (ElectronicDoc dentro de la extensión) resuelve
        // el warning de EF Core sobre required navigation to filtered entity.
        // SAFE: la extensión NO puede existir sin su ElectronicDoc padre (cascade delete + PK compartida).
        // El warning es un false positive ya que ambas entidades SIEMPRE tienen el mismo tenant scope.
        builder.HasOne(x => x.SalesInvoice).WithOne(x => x.ElectronicDoc)
            .HasForeignKey<SalesInvoice>(x => x.Id).IsRequired(false).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(x => x.CreditNote).WithOne(x => x.ElectronicDoc)
            .HasForeignKey<CreditNote>(x => x.Id).IsRequired(false).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(x => x.DebitNote).WithOne(x => x.ElectronicDoc)
            .HasForeignKey<DebitNote>(x => x.Id).IsRequired(false).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(x => x.DeliveryGuide).WithOne(x => x.ElectronicDoc)
            .HasForeignKey<DeliveryGuide>(x => x.Id).IsRequired(false).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(x => x.WithholdingCertificate).WithOne(x => x.ElectronicDoc)
            .HasForeignKey<WithholdingCertificate>(x => x.Id).IsRequired(false).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(x => x.PurchaseSettlement).WithOne(x => x.ElectronicDoc)
            .HasForeignKey<PurchaseSettlement>(x => x.Id).IsRequired(false).OnDelete(DeleteBehavior.Cascade);
    }
}

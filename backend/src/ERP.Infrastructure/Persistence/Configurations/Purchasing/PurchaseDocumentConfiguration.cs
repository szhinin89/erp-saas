using ERP.Domain.Modules.Purchasing.Entities;
using ERP.Domain.Modules.Purchasing.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ERP.Infrastructure.Persistence.Configurations.Purchasing;

public sealed class PurchaseDocumentConfiguration : IEntityTypeConfiguration<PurchaseDocument>
{
    public void Configure(EntityTypeBuilder<PurchaseDocument> builder)
    {
        builder.ToTable("purchase_document");

        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasColumnName("id");
        builder.Property(e => e.SubscriberId).HasColumnName("subscriber_id").IsRequired();
        builder.Property(e => e.CompanyId).HasColumnName("company_id");
        builder.Property(e => e.BusinessPartnerId).HasColumnName("business_partner_id");
        builder.Property(e => e.DocType)
            .HasColumnName("doc_type")
            .HasMaxLength(20)
            .HasConversion(
                v => v.ToDbValue(),
                v => PurchaseDocumentTypeExtensions.FromDbValue(v))
            .IsRequired();
        builder.Property(e => e.DocNumber).HasColumnName("doc_number").HasMaxLength(30);
        builder.Property(e => e.AccessKey).HasColumnName("access_key").HasMaxLength(49);
        builder.Property(e => e.IssueDate).HasColumnName("issue_date");
        builder.Property(e => e.DueDate).HasColumnName("due_date");
        builder.Property(e => e.Subtotal).HasColumnName("subtotal").HasPrecision(18, 4);
        builder.Property(e => e.VatTotal).HasColumnName("vat_total").HasPrecision(18, 4);
        builder.Property(e => e.Total).HasColumnName("total").HasPrecision(18, 4);
        builder.Property(e => e.TotalNotesApplied).HasColumnName("total_notes_applied").HasPrecision(18, 4);
        builder.Property(e => e.PaymentTerms).HasColumnName("payment_terms").HasMaxLength(50);
        builder.Property(e => e.Notes).HasColumnName("notes").HasMaxLength(1000);
        builder.Property(e => e.XmlPath).HasColumnName("xml_path").HasMaxLength(500);
        builder.Property(e => e.JournalEntryId).HasColumnName("journal_entry_id");
        builder.Property(e => e.BusinessPartnerId).HasColumnName("business_partner_id");
        builder.Property(e => e.ValidatedBy).HasColumnName("validated_by");
        builder.Property(e => e.ValidatedAt).HasColumnName("validated_at");
        builder.Property(e => e.ApprovedBy).HasColumnName("approved_by");
        builder.Property(e => e.ApprovedAt).HasColumnName("approved_at");
        builder.Property(e => e.RejectedBy).HasColumnName("rejected_by");
        builder.Property(e => e.RejectedAt).HasColumnName("rejected_at");
        builder.Property(e => e.RejectionReason).HasColumnName("rejection_reason").HasMaxLength(500);
        builder.Property(e => e.Status).HasColumnName("status").HasMaxLength(30);
        builder.Property(e => e.CreatedAt).HasColumnName("created_at");
        builder.Property(e => e.UpdatedAt).HasColumnName("updated_at");
        builder.Property(e => e.CreatedBy).HasColumnName("created_by");
        builder.Property(e => e.UpdatedBy).HasColumnName("updated_by");

        builder.HasOne(e => e.Reference)
            .WithMany()
            .HasForeignKey(e => e.ReferenceDocumentId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Navigation(e => e.Lines).HasField("_lines");
        builder.HasMany(e => e.Lines).WithOne().HasForeignKey(d => d.PurchaseDocumentId).OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(e => e.Electronic)
            .WithOne()
            .HasForeignKey<PurchaseElectronicDoc>(e => e.PurchaseDocumentId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

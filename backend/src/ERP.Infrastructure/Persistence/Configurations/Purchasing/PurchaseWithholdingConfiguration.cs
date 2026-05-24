using ERP.Domain.Common;
using ERP.Domain.Modules.Purchasing.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ERP.Infrastructure.Persistence.Configurations.Purchasing;

public sealed class PurchaseWithholdingConfiguration : IEntityTypeConfiguration<PurchaseWithholding>
{
    public void Configure(EntityTypeBuilder<PurchaseWithholding> builder)
    {
        builder.ToTable("purchase_withholding");

        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasColumnName("id");
        builder.Property(e => e.SubscriberId).HasColumnName("subscriber_id").IsRequired();
        builder.Property(e => e.CompanyId).HasColumnName("company_id");
        builder.Property(e => e.BusinessPartnerId).HasColumnName("business_partner_id").IsRequired();
        builder.Property(e => e.Direction)
            .HasColumnName("direction")
            .HasMaxLength(10)
            .HasConversion(
                v => WithholdingDirectionExtensions.ToDbValue(v),
                v => WithholdingDirectionExtensions.FromDbValue(v))
            .IsRequired();
        builder.Property(e => e.PurchaseDocumentId).HasColumnName("purchase_document_id");
        builder.Property(e => e.VoucherType).HasColumnName("voucher_type").HasMaxLength(30).IsRequired();
        builder.Property(e => e.AccessKey).HasColumnName("access_key").HasMaxLength(PurchaseWithholding.AccessKeyMaxLen).IsRequired();
        builder.Property(e => e.IssueDate).HasColumnName("issue_date").IsRequired();
        builder.Property(e => e.EstabCode).HasColumnName("estab_code").HasMaxLength(3);
        builder.Property(e => e.EmPointCode).HasColumnName("em_point_code").HasMaxLength(3);
        builder.Property(e => e.Sequential).HasColumnName("sequential").HasMaxLength(9);
        builder.Property(e => e.TotalRetained).HasColumnName("total_retained").HasPrecision(18, 4).IsRequired();
        builder.Property(e => e.Status).HasColumnName("status").HasMaxLength(PurchaseWithholding.StatusMaxLen).IsRequired();
        builder.Property(e => e.XmlPath).HasColumnName("xml_path").HasMaxLength(PurchaseWithholding.XmlPathMaxLen);
        builder.Property(e => e.XmlSignedPath).HasColumnName("xml_signed_path").HasMaxLength(PurchaseWithholding.XmlPathMaxLen);
        builder.Property(e => e.XmlAuthPath).HasColumnName("xml_auth_path").HasMaxLength(PurchaseWithholding.XmlPathMaxLen);
        builder.Property(e => e.AuthNumber).HasColumnName("auth_number").HasMaxLength(64);
        builder.Property(e => e.AuthDate).HasColumnName("auth_date");
        builder.Property(e => e.ErrorMessage).HasColumnName("error_message").HasMaxLength(PurchaseWithholding.ErrorMaxLen);
        builder.Property(e => e.JournalEntryId).HasColumnName("journal_entry_id");
        builder.Property(e => e.CreatedAt).HasColumnName("created_at");
        builder.Property(e => e.UpdatedAt).HasColumnName("updated_at");
        builder.Property(e => e.CreatedBy).HasColumnName("created_by");
        builder.Property(e => e.UpdatedBy).HasColumnName("updated_by");

        builder.HasOne<ERP.Domain.MasterData.Entities.BusinessPartner>().WithMany().HasForeignKey(e => e.BusinessPartnerId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(e => e.PurchaseDocument).WithMany().HasForeignKey(e => e.PurchaseDocumentId).OnDelete(DeleteBehavior.SetNull);
        builder.HasMany(e => e.Lines).WithOne().HasForeignKey(d => d.PurchaseWithholdingId).OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(e => new { e.SubscriberId, e.AccessKey })
            .IsUnique()
            .HasDatabaseName("uq_purchase_withholding_subscriber_access_key");
    }
}

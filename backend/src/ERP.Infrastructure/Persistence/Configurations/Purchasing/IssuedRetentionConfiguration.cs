using ERP.Domain.Modules.Purchasing.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ERP.Infrastructure.Persistence.Configurations.Purchasing;

public sealed class IssuedRetentionConfiguration : IEntityTypeConfiguration<IssuedRetention>
{
    public void Configure(EntityTypeBuilder<IssuedRetention> builder)
    {
        builder.ToTable("issued_retention");

        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasColumnName("id");
        builder.Property(e => e.SubscriberId).HasColumnName("subscriber_id").IsRequired();
        builder.Property(e => e.BusinessPartnerId).HasColumnName("business_partner_id").IsRequired();
        builder.Property(e => e.PurchBillId).HasColumnName("purch_bill_id");
        builder.Property(e => e.VoucherType).HasColumnName("voucher_type").HasMaxLength(30).IsRequired();
        builder.Property(e => e.AccessKey).HasColumnName("access_key").HasMaxLength(IssuedRetention.AccessKeyLen).IsRequired();
        builder.Property(e => e.IssueDate).HasColumnName("issue_date").IsRequired();
        builder.Property(e => e.EstablishmentCode).HasColumnName("establishment_code").HasMaxLength(IssuedRetention.EstabMaxLen).IsRequired();
        builder.Property(e => e.EmissionPointCode).HasColumnName("emission_point_code").HasMaxLength(IssuedRetention.EmPointMaxLen).IsRequired();
        builder.Property(e => e.Sequential).HasColumnName("sequential").HasMaxLength(IssuedRetention.SequentialMaxLen).IsRequired();
        builder.Property(e => e.Status).HasColumnName("status").HasMaxLength(IssuedRetention.StatusMaxLen).IsRequired();
        builder.Property(e => e.TotalRetained).HasColumnName("total_retained").HasPrecision(18, 4).IsRequired();
        builder.Property(e => e.XmlSignedPath).HasColumnName("xml_signed_path").HasMaxLength(IssuedRetention.XmlPathMaxLen);
        builder.Property(e => e.XmlAuthPath).HasColumnName("xml_auth_path").HasMaxLength(IssuedRetention.XmlPathMaxLen);
        builder.Property(e => e.AuthNumber).HasColumnName("auth_number").HasMaxLength(64);
        builder.Property(e => e.AuthDate).HasColumnName("auth_date");
        builder.Property(e => e.ErrorMessage).HasColumnName("error_message").HasMaxLength(IssuedRetention.ErrorMaxLen);
        builder.Property(e => e.JournalEntryId).HasColumnName("journal_entry_id");
        builder.Property(e => e.CreatedAt).HasColumnName("created_at");
        builder.Property(e => e.UpdatedAt).HasColumnName("updated_at");
        builder.Property(e => e.CreatedBy).HasColumnName("created_by");
        builder.Property(e => e.UpdatedBy).HasColumnName("updated_by");

        builder.HasOne<ERP.Domain.MasterData.Entities.BusinessPartner>().WithMany().HasForeignKey(e => e.BusinessPartnerId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<PurchBill>().WithMany().HasForeignKey(e => e.PurchBillId).OnDelete(DeleteBehavior.SetNull);
        builder.HasMany(e => e.Lines).WithOne().HasForeignKey(d => d.IssuedRetentionId).OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(e => new { e.SubscriberId, e.EstablishmentCode, e.EmissionPointCode, e.Sequential }).IsUnique().HasDatabaseName("uq_issued_retention_seq");
    }
}

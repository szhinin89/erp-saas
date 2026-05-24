using ERP.Domain.Modules.Purchases.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ERP.Infrastructure.Persistence.Configurations.Purchases;

public class ReceivedWithholdingConfiguration : IEntityTypeConfiguration<ReceivedWithholding>
{
    public void Configure(EntityTypeBuilder<ReceivedWithholding> builder)
    {
        builder.ToTable("received_withholding");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id").HasDefaultValueSql("gen_random_uuid()");
        builder.Property(x => x.CompanyId).HasColumnName("company_id").IsRequired();
        builder.Property(x => x.CustomerId).HasColumnName("business_partner_id");
        builder.Property(x => x.AccessKey).HasColumnName("access_key").HasMaxLength(49).IsFixedLength();
        builder.Property(x => x.IssuerRuc).HasColumnName("issuer_ruc").HasMaxLength(13).IsFixedLength().IsRequired();
        builder.Property(x => x.IssuerName).HasColumnName("issuer_name").HasMaxLength(200).IsRequired();
        builder.Property(x => x.IssueDate).HasColumnName("issue_date");
        builder.Property(x => x.SalesDocId).HasColumnName("sales_doc_id");
        builder.Property(x => x.TotalRetained).HasColumnName("total_retained").HasPrecision(18, 4).HasDefaultValue(0m);
        builder.Property(x => x.Status).HasColumnName("status").HasMaxLength(20).HasDefaultValue("active");
        builder.Property(x => x.XmlPath).HasColumnName("xml_path").HasMaxLength(500);
        builder.Property(x => x.JournalEntryId).HasColumnName("journal_entry_id");
        builder.Property(x => x.Notes).HasColumnName("notes").HasMaxLength(500);
        builder.Property(x => x.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("NOW()");
        builder.Property(x => x.UpdatedAt).HasColumnName("updated_at").HasDefaultValueSql("NOW()");
        builder.Property(x => x.CreatedBy).HasColumnName("created_by");

        builder.HasIndex(x => new { x.CompanyId, x.AccessKey })
            .IsUnique().HasFilter("access_key IS NOT NULL").HasDatabaseName("uq_rw_key");
        builder.HasIndex(x => new { x.CompanyId, x.IssueDate }).HasDatabaseName("idx_rw_company");
        builder.HasIndex(x => x.CustomerId).HasDatabaseName("idx_rw_customer");
    }
}

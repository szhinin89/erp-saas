using ERP.Domain.Modules.ElectronicDocuments.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ERP.Infrastructure.Persistence.Configurations.ElectronicDocuments;

public class CreditNoteConfiguration : IEntityTypeConfiguration<CreditNote>
{
    public void Configure(EntityTypeBuilder<CreditNote> builder)
    {
        builder.ToTable("credit_note");
        // PK = FK a electronic_doc.id (shared primary key — sin HasDefaultValueSql)
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.CompanyId).HasColumnName("company_id").IsRequired();
        builder.Property(x => x.OrigDocId).HasColumnName("orig_doc_id");
        builder.Property(x => x.OrigDocType).HasColumnName("orig_doc_type").HasMaxLength(5).HasDefaultValue("01");
        builder.Property(x => x.OrigEstablishment).HasColumnName("orig_establishment").HasMaxLength(3).IsFixedLength().IsRequired();
        builder.Property(x => x.OrigEmissionPoint).HasColumnName("orig_emission_point").HasMaxLength(3).IsFixedLength().IsRequired();
        builder.Property(x => x.OrigSequential).HasColumnName("orig_sequential").HasMaxLength(9).IsRequired();
        builder.Property(x => x.OrigIssueDate).HasColumnName("orig_issue_date");
        builder.Property(x => x.BuyerIdType).HasColumnName("buyer_id_type").HasMaxLength(5);
        builder.Property(x => x.BuyerIdNumber).HasColumnName("buyer_id_number").HasMaxLength(32);
        builder.Property(x => x.BuyerName).HasColumnName("buyer_name").HasMaxLength(200);
        builder.Property(x => x.CustomerId).HasColumnName("customer_id");
        builder.Property(x => x.Reason).HasColumnName("reason").HasMaxLength(300).IsRequired();

        builder.HasIndex(x => x.OrigDocId).HasDatabaseName("idx_cn_orig");

        // La relación 1:1 con ElectronicDoc se configura en ElectronicDocConfiguration
        builder.HasOne(x => x.OrigDoc)
            .WithMany()
            .HasForeignKey(x => x.OrigDocId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

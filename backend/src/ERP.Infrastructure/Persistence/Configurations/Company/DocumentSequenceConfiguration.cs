using ERP.Domain.Modules.Company.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ERP.Infrastructure.Persistence.Configurations.Company;

public class DocumentSequenceConfiguration : IEntityTypeConfiguration<DocumentSequence>
{
    public void Configure(EntityTypeBuilder<DocumentSequence> builder)
    {
        builder.ToTable("document_sequence");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id").HasDefaultValueSql("gen_random_uuid()");
        builder.Property(x => x.CompanyId).HasColumnName("company_id").IsRequired();
        builder.Property(x => x.EmissionPointId).HasColumnName("emission_point_id").IsRequired();
        builder.Property(x => x.DocTypeCode).HasColumnName("doc_type_code").HasMaxLength(5).IsRequired();
        builder.Property(x => x.CurrentSeq).HasColumnName("current_seq").HasDefaultValue(0);
        builder.Property(x => x.UpdatedAt).HasColumnName("updated_at").HasDefaultValueSql("NOW()");

        builder.HasIndex(x => new { x.EmissionPointId, x.DocTypeCode }).IsUnique().HasDatabaseName("uq_doc_seq");
        builder.HasIndex(x => x.CompanyId).HasDatabaseName("idx_docseq_company");

        builder.HasOne(x => x.Company)
            .WithMany().HasForeignKey(x => x.CompanyId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.EmissionPoint)
            .WithMany(x => x.Sequences).HasForeignKey(x => x.EmissionPointId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.DocType)
            .WithMany().HasForeignKey(x => x.DocTypeCode).OnDelete(DeleteBehavior.Restrict);
    }
}

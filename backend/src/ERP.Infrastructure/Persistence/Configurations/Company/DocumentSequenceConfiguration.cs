using ERP.Domain.Modules.Company.Entities;
using ERP.Domain.Modules.SriCatalogs.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ERP.Infrastructure.Persistence.Configurations.CompanyConfig;

public class DocumentSequenceConfiguration : IEntityTypeConfiguration<DocumentSequence>
{
    public void Configure(EntityTypeBuilder<DocumentSequence> builder)
    {
        builder.ToTable("document_sequence");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.SubscriberId).HasColumnName("subscriber_id").IsRequired();
        builder.Property(x => x.CompanyId).HasColumnName("company_id").IsRequired();
        builder.Property(x => x.EmissionPointId).HasColumnName("emission_point_id").IsRequired();
        builder.Property(x => x.DocTypeCode).HasColumnName("doc_type_code").HasMaxLength(5).IsRequired();
        builder.Property(x => x.CurrentSeq).HasColumnName("current_seq").IsRequired();
        builder.Property(x => x.UpdatedAt).HasColumnName("updated_at");

        builder.HasIndex(x => x.SubscriberId).HasDatabaseName("ix_docseq_subscriber_id");
        builder.HasIndex(x => x.CompanyId).HasDatabaseName("idx_docseq_company");
        builder.HasIndex(x => new { x.EmissionPointId, x.DocTypeCode })
            .IsUnique().HasDatabaseName("uq_doc_seq");

        // FK sin navigation en entidad — DocumentSequence es ligero
        builder.HasOne<EmissionPoint>()
            .WithMany(x => x.Sequences)
            .HasForeignKey(x => x.EmissionPointId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<SriDocType>()
            .WithMany()
            .HasForeignKey(x => x.DocTypeCode)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

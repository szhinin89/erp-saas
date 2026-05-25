using ERP.Domain.Modules.Integration.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ERP.Infrastructure.Persistence.Configurations.Integration;

public sealed class DocumentRelationConfiguration : IEntityTypeConfiguration<DocumentRelation>
{
    public void Configure(EntityTypeBuilder<DocumentRelation> builder)
    {
        builder.ToTable("document_relation");

        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasColumnName("id");
        builder.Property(e => e.PublicId).HasColumnName("public_id").IsRequired();
        builder.Property(e => e.SubscriberId).HasColumnName("subscriber_id").IsRequired();
        builder.Property(e => e.BranchId).HasColumnName("branch_id").IsRequired();
        builder.Property(e => e.SourceModule).HasColumnName("source_module").HasMaxLength(DocumentRelation.ModuleMaxLen).IsRequired();
        builder.Property(e => e.SourceId).HasColumnName("source_id").IsRequired();
        builder.Property(e => e.TargetModule).HasColumnName("target_module").HasMaxLength(DocumentRelation.ModuleMaxLen).IsRequired();
        builder.Property(e => e.TargetId).HasColumnName("target_id");
        builder.Property(e => e.RelationType).HasColumnName("relation_type").HasMaxLength(DocumentRelation.RelationTypeMaxLen).IsRequired();
        builder.Property(e => e.RowVersion).HasColumnName("row_version").IsConcurrencyToken();
        builder.Property(e => e.CreatedAt).HasColumnName("created_at");
        builder.Property(e => e.CreatedBy).HasColumnName("created_by");
        builder.Property(e => e.UpdatedAt).HasColumnName("updated_at");
        builder.Property(e => e.UpdatedBy).HasColumnName("updated_by");

        builder.HasIndex(e => new { e.SubscriberId, e.SourceModule, e.SourceId, e.RelationType })
            .IsUnique()
            .HasDatabaseName("uq_document_relation_source");

        builder.HasIndex(e => new { e.SubscriberId, e.TargetModule, e.TargetId })
            .HasDatabaseName("ix_document_relation_target");
    }
}

using ERP.Domain.Modules.Company.Entities;
using ERP.Domain.Modules.SriCatalogs.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ERP.Infrastructure.Persistence.Configurations.CompanyConfig;

public class DocumentSequenceConfiguration : IEntityTypeConfiguration<DocumentSequence>
{
    public void Configure(EntityTypeBuilder<DocumentSequence> builder)
    {
        // CHECK se define en el overload de ToTable para que quede ligado a la tabla.
        builder.ToTable("document_sequence", t =>
            t.HasCheckConstraint("chk_doc_seq_positive", "current_seq >= 1"));

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.TenantId).HasColumnName("tenant_id").IsRequired();
        builder.Property(x => x.CompanyId).HasColumnName("company_id").IsRequired();
        builder.Property(x => x.EmissionPointId).HasColumnName("emission_point_id").IsRequired();
        builder.Property(x => x.DocTypeCode).HasColumnName("doc_type_code").HasMaxLength(5).IsRequired();
        builder.Property(x => x.CurrentSeq).HasColumnName("current_seq").IsRequired();
        builder.Property(x => x.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(x => x.UpdatedAt).HasColumnName("updated_at").IsRequired();

        // Unique compuesto que hace explícita la isolación multi-tenant en la BD.
        // Reemplaza al anterior uq_doc_seq (emission_point_id, doc_type_code):
        //   — emission_point_id es UUID globalmente único, por lo que la restricción
        //     funcional no cambia, pero ahora la BD la hace auto-explicativa.
        //   — El prefijo (tenant_id, company_id) hace redundante el índice simple
        //     ix_docseq_tenant_id que se elimina en la migración.
        builder.HasIndex(x => new { x.TenantId, x.CompanyId, x.EmissionPointId, x.DocTypeCode })
            .IsUnique()
            .HasDatabaseName("uq_doc_seq");

        // Índice para consultas por empresa (company_id no es prefijo del unique compuesto).
        builder.HasIndex(x => x.CompanyId).HasDatabaseName("idx_docseq_company");

        // FK sin navigation en entidad — DocumentSequence es ligero.
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

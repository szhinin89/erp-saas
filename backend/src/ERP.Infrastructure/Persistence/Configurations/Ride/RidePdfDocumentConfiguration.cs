using ERP.Domain.Modules.Ride.Entities;
using ERP.Domain.Modules.Ride.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ERP.Infrastructure.Persistence.Configurations.Ride;

public sealed class RidePdfDocumentConfiguration : IEntityTypeConfiguration<RidePdfDocument>
{
    public void Configure(EntityTypeBuilder<RidePdfDocument> builder)
    {
        builder.ToTable("ride_pdf_document");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.TenantId).HasColumnName("tenant_id").IsRequired();
        builder.Property(x => x.CompanyId).HasColumnName("company_id").IsRequired();
        builder
            .Property(x => x.ElectronicDocumentId)
            .HasColumnName("electronic_document_id")
            .IsRequired();

        builder
            .Property(x => x.DocumentType)
            .HasColumnName("document_type")
            .HasConversion<int>()
            .IsRequired();

        builder
            .Property(x => x.SourceXmlHash)
            .HasColumnName("source_xml_hash")
            .HasMaxLength(RideContentHash.Length)
            .HasConversion(v => v.Value, v => RideContentHash.Create(v))
            .IsRequired();

        builder
            .Property(x => x.TemplateId)
            .HasColumnName("template_id")
            .HasMaxLength(RidePdfDocument.TemplateIdMaxLen)
            .IsRequired();
        builder
            .Property(x => x.TemplateVersion)
            .HasColumnName("template_version")
            .HasMaxLength(RidePdfDocument.VersionMaxLen)
            .IsRequired();
        builder
            .Property(x => x.BrandingVersion)
            .HasColumnName("branding_version")
            .HasMaxLength(RidePdfDocument.VersionMaxLen)
            .IsRequired();
        builder
            .Property(x => x.RendererVersion)
            .HasColumnName("renderer_version")
            .HasMaxLength(RidePdfDocument.VersionMaxLen)
            .IsRequired();
        builder
            .Property(x => x.RideSpecificationVersion)
            .HasColumnName("ride_specification_version")
            .HasMaxLength(RidePdfDocument.VersionMaxLen)
            .IsRequired();

        builder.Property(x => x.State).HasColumnName("state").HasConversion<int>().IsRequired();

        builder
            .Property(x => x.StoragePath)
            .HasColumnName("storage_path")
            .HasMaxLength(RidePdfDocument.PathMaxLen);
        builder
            .Property(x => x.LastError)
            .HasColumnName("last_error")
            .HasMaxLength(RidePdfDocument.ReasonMaxLen);
        builder.Property(x => x.GeneratedAtUtc).HasColumnName("generated_at_utc");
        builder.Property(x => x.RetryCount).HasColumnName("retry_count").IsRequired();

        builder.Property(x => x.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(x => x.UpdatedAt).HasColumnName("updated_at");
        builder.Property(x => x.CreatedBy).HasColumnName("created_by").IsRequired();
        builder.Property(x => x.UpdatedBy).HasColumnName("updated_by");

        // Mismo patrón que ElectronicDocumentConfiguration (PERS-01): protege contra que un
        // reintento automático y una regeneración manual pisen el mismo registro sin detección.
        builder
            .Property<uint>("xmin")
            .HasColumnName("xmin")
            .HasColumnType("xid")
            .IsRequired()
            .ValueGeneratedOnAddOrUpdate()
            .IsConcurrencyToken();

        // Índice único de huella completa (ADR-025 §14) — ampliado respecto a la redacción
        // literal de H4 (4 columnas) para cubrir los 5 valores que la propia ADR define como
        // determinantes de la validez del cache; documentado en la auditoría de la Fase 4.
        builder
            .HasIndex(x => new
            {
                x.TenantId,
                x.ElectronicDocumentId,
                x.SourceXmlHash,
                x.TemplateVersion,
                x.BrandingVersion,
                x.RendererVersion,
                x.RideSpecificationVersion,
            })
            .IsUnique()
            .HasDatabaseName("uq_ride_pdf_document_fingerprint");

        builder
            .HasIndex(x => new { x.TenantId, x.CompanyId })
            .HasDatabaseName("idx_ride_pdf_document_company");
    }
}

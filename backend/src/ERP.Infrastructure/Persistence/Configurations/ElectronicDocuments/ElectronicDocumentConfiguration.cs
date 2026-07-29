using ERP.Domain.Modules.ElectronicDocuments.Entities;
using ERP.Domain.Modules.ElectronicDocuments.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ERP.Infrastructure.Persistence.Configurations.ElectronicDocuments;

public sealed class ElectronicDocumentConfiguration : IEntityTypeConfiguration<ElectronicDocument>
{
    public void Configure(EntityTypeBuilder<ElectronicDocument> builder)
    {
        builder.ToTable("electronic_documents");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.TenantId).HasColumnName("tenant_id").IsRequired();
        builder.Property(x => x.CompanyId).HasColumnName("company_id").IsRequired();

        builder
            .Property(x => x.DocumentType)
            .HasColumnName("document_type")
            .HasConversion<int>()
            .IsRequired();

        builder
            .Property(x => x.SourceModule)
            .HasColumnName("source_module")
            .HasMaxLength(ElectronicDocument.SourceModuleMaxLen)
            .IsRequired();
        builder.Property(x => x.SourceEntityId).HasColumnName("source_entity_id").IsRequired();

        builder
            .Property(x => x.CurrentState)
            .HasColumnName("current_state")
            .HasConversion<int>()
            .IsRequired();

        builder.Property(x => x.Environment).HasColumnName("environment").HasMaxLength(10);

        builder
            .Property(x => x.AccessKey)
            .HasColumnName("access_key")
            .HasMaxLength(AccessKey.Length)
            .HasConversion(
                v => v == null ? null : v.Value,
                v => v == null ? null : AccessKey.Create(v)
            );

        builder
            .Property(x => x.AuthorizationNumber)
            .HasColumnName("authorization_number")
            .HasMaxLength(AuthorizationNumber.MaxLength)
            .HasConversion(
                v => v == null ? null : v.Value,
                v => v == null ? null : AuthorizationNumber.Create(v)
            );

        builder.Property(x => x.AuthorizationDate).HasColumnName("authorization_date");

        builder
            .Property(x => x.XmlVersion)
            .HasColumnName("xml_version")
            .HasMaxLength(ElectronicDocument.VersionMaxLen);
        builder
            .Property(x => x.SchemaVersion)
            .HasColumnName("schema_version")
            .HasMaxLength(ElectronicDocument.VersionMaxLen);

        builder
            .Property(x => x.XmlDraftPath)
            .HasColumnName("xml_draft_path")
            .HasMaxLength(ElectronicDocument.PathMaxLen);
        builder
            .Property(x => x.SignedXmlPath)
            .HasColumnName("signed_xml_path")
            .HasMaxLength(ElectronicDocument.PathMaxLen);
        builder
            .Property(x => x.AuthorizedXmlPath)
            .HasColumnName("authorized_xml_path")
            .HasMaxLength(ElectronicDocument.PathMaxLen);

        builder.Property(x => x.RetryCount).HasColumnName("retry_count").IsRequired();
        builder.Property(x => x.LastAttemptUtc).HasColumnName("last_attempt_utc");
        builder
            .Property(x => x.PreDeadLetterState)
            .HasColumnName("pre_dead_letter_state")
            .HasConversion<int?>();
        builder.Property(x => x.LastError).HasColumnName("last_error");

        builder.Property(x => x.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(x => x.UpdatedAt).HasColumnName("updated_at");
        builder.Property(x => x.CreatedBy).HasColumnName("created_by").IsRequired();
        builder.Property(x => x.UpdatedBy).HasColumnName("updated_by");

        // ── Concurrency token (PostgreSQL xid) ──────────────────────
        // PERS-01 (auditoría SRI): sin esto, el job de reintento automático y una acción manual
        // desde el Monitor pueden pisar el estado del mismo documento sin detección — mismo
        // patrón ya usado en SalesInvoice/StockMovement/CurrentStock/PurchaseInvoice/CashSession.
        builder
            .Property<uint>("xmin")
            .HasColumnName("xmin")
            .HasColumnType("xid")
            .IsRequired()
            .ValueGeneratedOnAddOrUpdate()
            .IsConcurrencyToken();

        // Un documento de origen tiene a lo sumo un ElectronicDocument — misma garantía
        // que hoy exige el mecanismo de idempotencia de ElectronicDocumentIssuer.
        builder
            .HasIndex(x => new
            {
                x.TenantId,
                x.SourceModule,
                x.SourceEntityId,
            })
            .IsUnique()
            .HasDatabaseName("uq_electronic_document_source");

        builder
            .HasIndex(x => new { x.TenantId, x.CompanyId })
            .HasDatabaseName("idx_electronic_document_company");

        // BD-01 (auditoría SRI): el SRI rechaza con el código de error 45 "Secuencial
        // registrado" / 43 "Clave acceso registrada" cualquier clave de acceso duplicada — es
        // una regla de unicidad de negocio que hasta ahora no tenía respaldo en base de datos.
        // Índice único parcial (NULL permitido en Draft/XmlGenerated, antes de que el
        // documento tenga clave de acceso calculada) — mismo patrón ya usado en
        // purchase_invoices (ver migración AddPurchaseFKsAndAccessKeyUnique).
        builder
            .HasIndex(x => new { x.TenantId, x.AccessKey })
            .IsUnique()
            .HasDatabaseName("uq_electronic_document_access_key")
            .HasFilter("access_key IS NOT NULL");
    }
}

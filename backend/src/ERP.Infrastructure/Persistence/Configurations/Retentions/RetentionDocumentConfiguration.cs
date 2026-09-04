using ERP.Domain.Modules.Retentions.Entities;
using ERP.Domain.Modules.Retentions.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ERP.Infrastructure.Persistence.Configurations.Retentions;

/// <summary>
/// Fase <c>RETENTIONS-PERSISTENCE-01B</c>. Configuración EF Core de <see cref="RetentionDocument"/>
/// — mismo patrón que <c>ExpenseDocumentConfiguration</c>/<c>AccountsPayableConfiguration</c>. No se
/// crea FK hacia <c>ExpenseDocument</c>/<c>PurchaseInvoice</c>: <see cref="RetentionDocument.SourceDocumentType"/>
/// + <see cref="RetentionDocument.SourceDocumentId"/> es una relación genérica sin FK física, mismo
/// principio ya usado por <c>AccountsPayable.OriginType</c>/<c>OriginId</c>.
/// </summary>
public sealed class RetentionDocumentConfiguration : IEntityTypeConfiguration<RetentionDocument>
{
    public void Configure(EntityTypeBuilder<RetentionDocument> builder)
    {
        builder.ToTable("retention_documents");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id").IsRequired();
        builder.Property(x => x.TenantId).HasColumnName("tenant_id").IsRequired();
        builder.Property(x => x.CompanyId).HasColumnName("company_id").IsRequired();
        builder.Property(x => x.BranchId).HasColumnName("branch_id").IsRequired();
        builder
            .Property(x => x.SourceDocumentType)
            .HasColumnName("source_document_type")
            .HasConversion<int>()
            .IsRequired();
        builder.Property(x => x.SourceDocumentId).HasColumnName("source_document_id").IsRequired();
        builder
            .Property(x => x.SubjectBusinessPartnerId)
            .HasColumnName("subject_business_partner_id")
            .IsRequired();
        builder.Property(x => x.EmissionPointId).HasColumnName("emission_point_id").IsRequired();
        builder
            .Property(x => x.RetentionNumber)
            .HasColumnName("retention_number")
            .HasMaxLength(RetentionDocument.RetentionNumberMaxLen);
        builder.Property(x => x.IssueDate).HasColumnName("issue_date");
        builder.Property(x => x.Status).HasColumnName("status").HasConversion<int>().IsRequired();

        // RETENTIONS-TAX-COMPONENT-MODEL-02B — periodo fiscal (derivado de IssueDate en Issue(),
        // ver comentario de la entidad) y snapshot del documento sustento. Todas las columnas
        // nuevas son NULLABLE: documentos emitidos ANTES de esta fase quedan con estos campos en
        // NULL, sin backfill retroactivo (no hay forma de reconstruir un snapshot histórico que
        // nunca se capturó) — documentos nuevos siempre los completan porque Issue()/Create()
        // los derivan/reciben de forma obligatoria en el flujo real (RetentionIssuer).
        builder.Property(x => x.FiscalPeriodMonth).HasColumnName("fiscal_period_month");
        builder.Property(x => x.FiscalPeriodYear).HasColumnName("fiscal_period_year");

        builder
            .Property(x => x.SourceDocumentSriTypeCode)
            .HasColumnName("source_document_sri_type_code")
            .HasMaxLength(5);
        builder
            .Property(x => x.SourceDocumentNumber)
            .HasColumnName("source_document_number")
            .HasMaxLength(30);
        builder.Property(x => x.SourceDocumentIssueDate).HasColumnName("source_document_issue_date");
        builder
            .Property(x => x.SourceDocumentAuthorizationNumber)
            .HasColumnName("source_document_authorization_number")
            .HasMaxLength(49);
        builder
            .Property(x => x.SourceDocumentTaxSupportCode)
            .HasColumnName("source_document_tax_support_code")
            .HasMaxLength(2);
        builder
            .Property(x => x.SourceDocumentSubtotal)
            .HasColumnName("source_document_subtotal")
            .HasColumnType("numeric(18,2)");
        builder
            .Property(x => x.SourceDocumentTotal)
            .HasColumnName("source_document_total")
            .HasColumnType("numeric(18,2)");

        builder
            .Property(x => x.TotalRetainedVat)
            .HasColumnName("total_retained_vat")
            .HasColumnType("numeric(18,2)")
            .IsRequired();
        builder
            .Property(x => x.TotalRetainedIncome)
            .HasColumnName("total_retained_income")
            .HasColumnType("numeric(18,2)")
            .IsRequired();
        builder
            .Property(x => x.TotalRetained)
            .HasColumnName("total_retained")
            .HasColumnType("numeric(18,2)")
            .IsRequired();

        builder
            .Property(x => x.CancelReason)
            .HasColumnName("cancel_reason")
            .HasMaxLength(RetentionDocument.CancelReasonMaxLen);
        builder.Property(x => x.CancelledAt).HasColumnName("cancelled_at");
        builder.Property(x => x.CancelledBy).HasColumnName("cancelled_by");

        builder.Property(x => x.CreatedAt).HasColumnName("created_at");
        builder.Property(x => x.UpdatedAt).HasColumnName("updated_at");
        builder.Property(x => x.CreatedBy).HasColumnName("created_by");
        builder.Property(x => x.UpdatedBy).HasColumnName("updated_by");

        builder
            .Property<uint>("xmin")
            .HasColumnName("xmin")
            .HasColumnType("xid")
            .IsRequired()
            .ValueGeneratedOnAddOrUpdate()
            .IsConcurrencyToken();

        builder
            .HasMany(x => x.Lines)
            .WithOne()
            .HasForeignKey(x => x.RetentionDocumentId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(x => x.TenantId).HasDatabaseName("ix_retention_documents_tenant");
        builder.HasIndex(x => x.CompanyId).HasDatabaseName("ix_retention_documents_company");
        builder.HasIndex(x => x.BranchId).HasDatabaseName("ix_retention_documents_branch");
        builder
            .HasIndex(x => new { x.SourceDocumentType, x.SourceDocumentId })
            .HasDatabaseName("ix_retention_documents_source");
        builder.HasIndex(x => x.Status).HasDatabaseName("ix_retention_documents_status");
        builder.HasIndex(x => x.IssueDate).HasDatabaseName("ix_retention_documents_issue_date");
        builder
            .HasIndex(x => x.RetentionNumber)
            .HasDatabaseName("ix_retention_documents_number")
            .HasFilter("retention_number IS NOT NULL");

        // Unicidad por origen (RETENTIONS-MODULE-DESIGN-01.md § "Agregado raíz"): nunca más de una
        // retención "activa" (Draft o Issued, es decir Status != Cancelled = 2) sobre el mismo
        // origen — mismo criterio ya implementado en aplicación por
        // IRetentionDocumentRepository.ExistsActiveBySourceAsync, reforzado aquí a nivel de BD
        // (doble mecanismo, mismo patrón que AccountsPayable usa para su propio origen). El filtro
        // usa el valor entero de RetentionStatus.Cancelled (2) porque Status se mapea como int
        // (HasConversion<int>()), no como string.
        builder
            .HasIndex(x => new
            {
                x.TenantId,
                x.CompanyId,
                x.SourceDocumentType,
                x.SourceDocumentId,
            })
            .IsUnique()
            .HasDatabaseName("uq_retention_documents_active_source")
            .HasFilter($"status <> {(int)RetentionStatus.Cancelled}");
    }
}

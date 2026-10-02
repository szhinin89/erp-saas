using ERP.Domain.Modules.Retentions.Entities;
using ERP.Infrastructure.Audit;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ERP.Infrastructure.Persistence.Configurations.Retentions;

/// <summary>ZH-RETENTION-SRI-ANNULMENT-01 — auditoría de entidad (ADR-022) de las solicitudes de anulación.</summary>
public sealed class RetentionAnnulmentRequestAuditConfiguration : IEntityTypeConfiguration<RetentionAnnulmentRequestAudit>
{
    public void Configure(EntityTypeBuilder<RetentionAnnulmentRequestAudit> builder)
    {
        builder.ConfigureAuditBase("retention_annulment_request_audit");

        // Puede llevar el error real de una finalización fallida: sin límite (mismo criterio que la auditoría SRI).
        builder.Property(x => x.Reason).Metadata.SetMaxLength(null);

        builder.Property(x => x.CompanyId).HasColumnName("company_id").IsRequired();
        builder.Property(x => x.RetentionDocumentId).HasColumnName("retention_document_id").IsRequired();
        builder.Property(x => x.FromStatus).HasColumnName("from_status").HasConversion<int?>();
        builder.Property(x => x.ToStatus).HasColumnName("to_status").HasConversion<int>().IsRequired();

        builder
            .HasIndex(x => new { x.TenantId, x.CompanyId, x.OccurredAtUtc })
            .HasDatabaseName("ix_retention_annulment_request_audit_company_occurred_at");
    }
}

using ERP.Domain.Modules.Retentions.Entities;
using ERP.Domain.Modules.Retentions.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ERP.Infrastructure.Persistence.Configurations.Retentions;

/// <summary>ZH-RETENTION-SRI-ANNULMENT-01 — mapeo de <see cref="RetentionAnnulmentRequest"/>.</summary>
public sealed class RetentionAnnulmentRequestConfiguration
    : IEntityTypeConfiguration<RetentionAnnulmentRequest>
{
    public void Configure(EntityTypeBuilder<RetentionAnnulmentRequest> builder)
    {
        builder.ToTable("retention_annulment_requests");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id").ValueGeneratedNever();

        builder.Property(x => x.TenantId).HasColumnName("tenant_id").IsRequired();
        builder.Property(x => x.CompanyId).HasColumnName("company_id").IsRequired();
        builder
            .Property(x => x.RetentionDocumentId)
            .HasColumnName("retention_document_id")
            .IsRequired();
        builder
            .Property(x => x.ElectronicDocumentId)
            .HasColumnName("electronic_document_id")
            .IsRequired();
        builder
            .Property(x => x.SourceDocumentType)
            .HasColumnName("source_document_type")
            .HasConversion<int>()
            .IsRequired();
        builder.Property(x => x.SourceDocumentId).HasColumnName("source_document_id").IsRequired();
        builder
            .Property(x => x.Reason)
            .HasColumnName("reason")
            .HasMaxLength(RetentionAnnulmentRequest.ReasonMaxLen)
            .IsRequired();
        builder.Property(x => x.RequestedBy).HasColumnName("requested_by").IsRequired();
        builder.Property(x => x.RequestedAtUtc).HasColumnName("requested_at_utc").IsRequired();
        builder.Property(x => x.Status).HasColumnName("status").HasConversion<int>().IsRequired();

        builder
            .Property(x => x.AccessKey)
            .HasColumnName("access_key")
            .HasMaxLength(49)
            .IsRequired();
        builder
            .Property(x => x.RetentionNumber)
            .HasColumnName("retention_number")
            .HasMaxLength(20)
            .IsRequired();
        builder
            .Property(x => x.RetentionIssueDate)
            .HasColumnName("retention_issue_date")
            .IsRequired();
        builder
            .Property(x => x.ReceptorIdentification)
            .HasColumnName("receptor_identification")
            .HasMaxLength(20)
            .IsRequired();
        builder
            .Property(x => x.ReceptorName)
            .HasColumnName("receptor_name")
            .HasMaxLength(300)
            .IsRequired();
        builder.Property(x => x.OrdinaryDeadline).HasColumnName("ordinary_deadline").IsRequired();

        builder.Property(x => x.SubmittedOn).HasColumnName("submitted_on");
        builder.Property(x => x.SubmittedAtUtc).HasColumnName("submitted_at_utc");
        builder.Property(x => x.SubmittedBy).HasColumnName("submitted_by");
        builder
            .Property(x => x.SubmissionReference)
            .HasColumnName("submission_reference")
            .HasMaxLength(RetentionAnnulmentRequest.ReferenceMaxLen);

        builder.Property(x => x.ResolvedOn).HasColumnName("resolved_on");
        builder.Property(x => x.ResolvedAtUtc).HasColumnName("resolved_at_utc");
        builder.Property(x => x.ResolutionBy).HasColumnName("resolution_by");
        builder
            .Property(x => x.EvidenceReference)
            .HasColumnName("evidence_reference")
            .HasMaxLength(RetentionAnnulmentRequest.ReferenceMaxLen);
        builder
            .Property(x => x.Notes)
            .HasColumnName("notes")
            .HasMaxLength(RetentionAnnulmentRequest.NotesMaxLen);

        // 01B — verificación automática en ConsultaComprobante.
        builder.Property(x => x.LastSriCheckAtUtc).HasColumnName("last_sri_check_at_utc");
        builder
            .Property(x => x.LastSriQueryOutcome)
            .HasColumnName("last_sri_query_outcome")
            .HasConversion<int?>();
        builder
            .Property(x => x.LastSriFiscalStatus)
            .HasColumnName("last_sri_fiscal_status")
            .HasConversion<int?>();
        builder
            .Property(x => x.LastSriRawStatus)
            .HasColumnName("last_sri_raw_status")
            .HasMaxLength(RetentionAnnulmentRequest.RawSriStatusMaxLen);
        builder.Property(x => x.SriCheckCount).HasColumnName("sri_check_count").IsRequired();
        builder
            .Property(x => x.SriAnnulmentEvidence)
            .HasColumnName("sri_annulment_evidence")
            .HasMaxLength(RetentionAnnulmentRequest.SriEvidenceMaxLen);

        builder.Property(x => x.FinalizedAtUtc).HasColumnName("finalized_at_utc");
        builder
            .Property(x => x.FinalizationAttempts)
            .HasColumnName("finalization_attempts")
            .IsRequired();
        builder
            .Property(x => x.LastFinalizationError)
            .HasColumnName("last_finalization_error")
            .HasMaxLength(RetentionAnnulmentRequest.ErrorMaxLen);

        builder.Property(x => x.CreatedAt).HasColumnName("created_at");
        builder.Property(x => x.UpdatedAt).HasColumnName("updated_at");
        builder.Property(x => x.CreatedBy).HasColumnName("created_by");
        builder.Property(x => x.UpdatedBy).HasColumnName("updated_by");

        builder.Ignore(x => x.IsOpen);
        builder.Ignore(x => x.RequiresFinalization);
        builder.Ignore(x => x.SriReportsAuthorized);
        builder.Ignore(x => x.CanBeAbandoned);

        // Concurrencia optimista: dos resoluciones/finalizaciones sobre la misma solicitud se serializan.
        builder
            .Property<uint>("xmin")
            .HasColumnName("xmin")
            .HasColumnType("xid")
            .IsRequired()
            .ValueGeneratedOnAddOrUpdate()
            .IsConcurrencyToken();

        builder
            .HasOne<RetentionDocument>()
            .WithMany()
            .HasForeignKey(x => x.RetentionDocumentId)
            .OnDelete(DeleteBehavior.Restrict);

        // A lo sumo UNA solicitud abierta por retención (barrera atómica ante dos solicitudes simultáneas).
        builder
            .HasIndex(x => new { x.TenantId, x.RetentionDocumentId })
            .IsUnique()
            .HasFilter(
                $"status IN ({(int)RetentionAnnulmentStatus.PendingSubmission}, {(int)RetentionAnnulmentStatus.PendingSriResolution})"
            )
            .HasDatabaseName("uq_retention_annulment_requests_open");

        builder
            .HasIndex(x => new
            {
                x.TenantId,
                x.CompanyId,
                x.RetentionDocumentId,
            })
            .HasDatabaseName("ix_retention_annulment_requests_retention");

        builder
            .HasIndex(x => x.Status)
            .HasFilter(
                $"status = {(int)RetentionAnnulmentStatus.Accepted} AND finalized_at_utc IS NULL"
            )
            .HasDatabaseName("ix_retention_annulment_requests_pending_finalization");

        // 01B — polling de solicitudes presentadas al SRI (job): las menos recientemente verificadas primero.
        builder
            .HasIndex(x => x.LastSriCheckAtUtc)
            .HasFilter($"status = {(int)RetentionAnnulmentStatus.PendingSriResolution}")
            .HasDatabaseName("ix_retention_annulment_requests_sri_verification");
    }
}

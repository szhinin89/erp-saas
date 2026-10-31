using ERP.Domain.Modules.ElectronicDocuments.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ERP.Infrastructure.Persistence.Configurations.ElectronicDocuments;

public sealed class ElectronicDocumentTransmissionAttemptConfiguration
    : IEntityTypeConfiguration<ElectronicDocumentTransmissionAttempt>
{
    public void Configure(EntityTypeBuilder<ElectronicDocumentTransmissionAttempt> builder)
    {
        builder.ToTable("electronic_document_transmission_attempts");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.TenantId).HasColumnName("tenant_id").IsRequired();
        builder.Property(x => x.CompanyId).HasColumnName("company_id").IsRequired();
        builder
            .Property(x => x.ElectronicDocumentId)
            .HasColumnName("electronic_document_id")
            .IsRequired();
        builder.Property(x => x.AttemptNumber).HasColumnName("attempt_number").IsRequired();
        builder.Property(x => x.IdempotencyKey).HasColumnName("idempotency_key").IsRequired();
        builder
            .Property(x => x.Reason)
            .HasColumnName("reason")
            .HasMaxLength(ElectronicDocumentTransmissionAttempt.ReasonMaxLen)
            .IsRequired();
        builder.Property(x => x.RequestedBy).HasColumnName("requested_by").IsRequired();
        builder.Property(x => x.RequestedAtUtc).HasColumnName("requested_at_utc").IsRequired();
        builder
            .Property(x => x.RejectionStage)
            .HasColumnName("rejection_stage")
            .HasMaxLength(ElectronicDocumentTransmissionAttempt.RejectionStageMaxLen)
            .IsRequired();
        builder
            .Property(x => x.RejectionCodes)
            .HasColumnName("rejection_codes")
            .HasMaxLength(ElectronicDocumentTransmissionAttempt.RejectionCodesMaxLen)
            .IsRequired();
        builder
            .Property(x => x.SupersededSriDecisionUtc)
            .HasColumnName("superseded_sri_decision_utc");

        foreach (var (path, column) in new[]
        {
            (nameof(ElectronicDocumentTransmissionAttempt.PreviousSignedXmlPath), "previous_signed_xml_path"),
            (nameof(ElectronicDocumentTransmissionAttempt.DraftXmlPath), "draft_xml_path"),
            (nameof(ElectronicDocumentTransmissionAttempt.SignedXmlPath), "signed_xml_path"),
        })
            builder
                .Property<string>(path)
                .HasColumnName(column)
                .HasMaxLength(ElectronicDocument.PathMaxLen)
                .IsRequired();

        foreach (var (hash, column) in new[]
        {
            (nameof(ElectronicDocumentTransmissionAttempt.PreviousSignedXmlSha256), "previous_signed_xml_sha256"),
            (nameof(ElectronicDocumentTransmissionAttempt.DraftXmlSha256), "draft_xml_sha256"),
            (nameof(ElectronicDocumentTransmissionAttempt.SignedXmlSha256), "signed_xml_sha256"),
        })
            builder
                .Property<string>(hash)
                .HasColumnName(column)
                .HasMaxLength(ElectronicDocumentTransmissionAttempt.Sha256HexLen)
                .IsFixedLength()
                .IsRequired();

        builder
            .Property(x => x.CorrectionSummary)
            .HasColumnName("correction_summary")
            .HasMaxLength(ElectronicDocumentTransmissionAttempt.CorrectionSummaryMaxLen)
            .IsRequired();

        builder
            .HasOne<ElectronicDocument>()
            .WithMany()
            .HasForeignKey(x => x.ElectronicDocumentId)
            .OnDelete(DeleteBehavior.Restrict);

        // A lo sumo un reclamo por intento: dos solicitudes concurrentes con distinta clave de
        // idempotencia nunca registran el mismo intento, aunque el lock del origen fallara.
        builder
            .HasIndex(x => new { x.TenantId, x.ElectronicDocumentId, x.AttemptNumber })
            .IsUnique()
            .HasDatabaseName("uq_electronic_document_transmission_attempt_number");

        // Una misma solicitud (clave de idempotencia) reclama a lo sumo una vez.
        builder
            .HasIndex(x => new { x.TenantId, x.ElectronicDocumentId, x.IdempotencyKey })
            .IsUnique()
            .HasDatabaseName("uq_electronic_document_transmission_attempt_idempotency");

        builder.ToTable(t =>
            t.HasCheckConstraint(
                "ck_electronic_document_transmission_attempt_number",
                "attempt_number >= 2"
            )
        );
    }
}

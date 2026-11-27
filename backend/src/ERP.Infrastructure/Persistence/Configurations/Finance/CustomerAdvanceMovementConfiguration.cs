using ERP.Domain.Branches.Entities;
using ERP.Domain.Modules.Caja.Entities;
using ERP.Domain.Modules.Finance.Entities;
using ERP.Domain.Modules.Sales.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ERP.Infrastructure.Persistence.Configurations.Finance;

/// <summary>
/// Mapeo EF de <see cref="CustomerAdvanceMovement"/> — ADR-052 §4/§5. Los CHECK reflejan por tipo
/// los mismos invariantes del dominio. Tipos: 1=Received, 2=Application, 3=ReversalOfApplication,
/// 4=Refund, 5=ReversalOfRefund, 6=SourceCancelled, 7=IncomeReversed.
/// </summary>
public sealed class CustomerAdvanceMovementConfiguration
    : IEntityTypeConfiguration<CustomerAdvanceMovement>
{
    public void Configure(EntityTypeBuilder<CustomerAdvanceMovement> builder)
    {
        builder.ToTable(
            "customer_advance_movements",
            t =>
            {
                t.HasCheckConstraint(
                    "chk_customer_advance_movements_type",
                    "\"movement_type\" BETWEEN 1 AND 7"
                );
                t.HasCheckConstraint(
                    "chk_customer_advance_movements_amount_positive",
                    "\"amount\" > 0"
                );
                t.HasCheckConstraint(
                    "chk_customer_advance_movements_line_number",
                    "(\"movement_type\" IN (2, 3) AND \"line_number\" >= 1) "
                        + "OR (\"movement_type\" NOT IN (2, 3) AND \"line_number\" = 0)"
                );
                t.HasCheckConstraint(
                    "chk_customer_advance_movements_receivable",
                    "(\"movement_type\" IN (2, 3) AND \"receivable_id\" IS NOT NULL) "
                        + "OR (\"movement_type\" NOT IN (2, 3) AND \"receivable_id\" IS NULL AND \"installment_id\" IS NULL)"
                );
                t.HasCheckConstraint(
                    "chk_customer_advance_movements_reversal_ref",
                    "(\"movement_type\" IN (3, 5, 6, 7) AND \"reversal_of_movement_id\" IS NOT NULL) "
                        + "OR (\"movement_type\" NOT IN (3, 5, 6, 7) AND \"reversal_of_movement_id\" IS NULL)"
                );
                t.HasCheckConstraint(
                    "chk_customer_advance_movements_compensation_data",
                    "\"movement_type\" IN (1, 2) OR (\"reason\" IS NOT NULL AND \"reference\" IS NOT NULL)"
                );
                t.HasCheckConstraint(
                    "chk_customer_advance_movements_evidence_date",
                    "\"movement_type\" NOT IN (5, 7) OR \"evidence_date\" IS NOT NULL"
                );
                t.HasCheckConstraint(
                    "chk_customer_advance_movements_cash_link",
                    "(\"cash_session_id\" IS NULL AND \"cash_movement_id\" IS NULL) "
                        + "OR (\"movement_type\" IN (1, 7) AND \"cash_session_id\" IS NOT NULL AND \"cash_movement_id\" IS NOT NULL)"
                );
            }
        );

        builder.HasKey(x => x.Id);
        // Destino de las FK compuestas: una reversa o una transacción solo puede referenciar un
        // movimiento del mismo anticipo, empresa y tenant.
        builder
            .HasAlternateKey(x => new
            {
                x.Id,
                x.CustomerAdvanceId,
                x.TenantId,
                x.CompanyId,
            })
            .HasName("ak_customer_advance_movements_id_scope");

        builder.Property(x => x.Id).HasColumnName("id").IsRequired();
        Immutable(builder.Property(x => x.TenantId).HasColumnName("tenant_id").IsRequired());
        Immutable(builder.Property(x => x.CompanyId).HasColumnName("company_id").IsRequired());
        Immutable(
            builder
                .Property(x => x.CustomerAdvanceId)
                .HasColumnName("customer_advance_id")
                .IsRequired()
        );
        Immutable(
            builder
                .Property(x => x.MovementType)
                .HasColumnName("movement_type")
                .HasConversion<int>()
                .IsRequired()
        );
        Immutable(
            builder
                .Property(x => x.Amount)
                .HasColumnName("amount")
                .HasColumnType("numeric(18,2)")
                .IsRequired()
        );

        // ── Idempotencia (ADR-052 §5) ────────────────────────────────────────
        Immutable(
            builder.Property(x => x.ClientRequestId).HasColumnName("client_request_id").IsRequired()
        );
        Immutable(builder.Property(x => x.LineNumber).HasColumnName("line_number").IsRequired());
        Immutable(
            builder
                .Property(x => x.RequestPayloadHash)
                .HasColumnName("request_payload_hash")
                .HasMaxLength(ERP.Domain.Common.ClientRequestKey.PayloadHashLength)
                .IsRequired()
        );

        Immutable(builder.Property(x => x.BranchId).HasColumnName("branch_id").IsRequired());
        Immutable(
            builder
                .Property(x => x.EffectiveDate)
                .HasColumnName("effective_date")
                .HasColumnType("date")
                .IsRequired()
        );
        Immutable(
            builder
                .Property(x => x.Reason)
                .HasColumnName("reason")
                .HasMaxLength(CustomerAdvanceMovement.ReasonMaxLen)
        );
        Immutable(
            builder
                .Property(x => x.Reference)
                .HasColumnName("reference")
                .HasMaxLength(CustomerAdvanceMovement.ReferenceMaxLen)
        );
        Immutable(
            builder
                .Property(x => x.EvidenceDate)
                .HasColumnName("evidence_date")
                .HasColumnType("date")
        );
        Immutable(builder.Property(x => x.ReceivableId).HasColumnName("receivable_id"));
        Immutable(builder.Property(x => x.InstallmentId).HasColumnName("installment_id"));
        Immutable(
            builder.Property(x => x.ReversalOfMovementId).HasColumnName("reversal_of_movement_id")
        );
        // Únicos datos posteriores al alta (vínculo con caja, una sola vez).
        builder.Property(x => x.CashSessionId).HasColumnName("cash_session_id");
        builder.Property(x => x.CashMovementId).HasColumnName("cash_movement_id");
        Immutable(
            builder
                .Property(x => x.CreatedByUserId)
                .HasColumnName("created_by_user_id")
                .IsRequired()
        );
        Immutable(
            builder.Property(x => x.CreatedAtUtc).HasColumnName("created_at_utc").IsRequired()
        );

        // ── Relationships (la FK al anticipo vive en CustomerAdvanceConfiguration) ──
        builder
            .HasOne<CustomerAdvanceMovement>()
            .WithMany()
            .HasForeignKey(x => new
            {
                x.ReversalOfMovementId,
                x.CustomerAdvanceId,
                x.TenantId,
                x.CompanyId,
            })
            .HasPrincipalKey(x => new
            {
                x.Id,
                x.CustomerAdvanceId,
                x.TenantId,
                x.CompanyId,
            })
            .IsRequired(false)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_customer_advance_movements_reversal_scope");
        builder
            .HasOne<SalesReceivable>()
            .WithMany()
            .HasForeignKey(x => x.ReceivableId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.Restrict);
        builder
            .HasOne<Branch>()
            .WithMany()
            .HasForeignKey(x => x.BranchId)
            .OnDelete(DeleteBehavior.Restrict);
        builder
            .HasOne<CashSession>()
            .WithMany()
            .HasForeignKey(x => x.CashSessionId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.Restrict);
        builder
            .HasOne<CashMovement>()
            .WithMany()
            .HasForeignKey(x => x.CashMovementId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.Restrict);

        // ── Indexes ──────────────────────────────────────────────────────────
        builder
            .HasIndex(x => new { x.TenantId, x.CustomerAdvanceId })
            .HasDatabaseName("ix_customer_advance_movements_tenant_advance");
        builder
            .HasIndex(x => new
            {
                x.TenantId,
                x.ClientRequestId,
                x.LineNumber,
            })
            .IsUnique()
            .HasDatabaseName("uq_customer_advance_movements_tenant_request_line");
        // Toda operación escribe exactamente una línea inicial (0 o 1): una misma clave no puede
        // reutilizarse en otra operación con distintos números de línea.
        builder
            .HasIndex(x => new { x.TenantId, x.ClientRequestId })
            .IsUnique()
            .HasFilter("\"line_number\" <= 1")
            .HasDatabaseName("uq_customer_advance_movements_tenant_request_operation");
        builder
            .HasIndex(x => x.ReversalOfMovementId)
            .IsUnique()
            .HasFilter("\"reversal_of_movement_id\" IS NOT NULL")
            .HasDatabaseName("uq_customer_advance_movements_reversal_of_movement");
        builder
            .HasIndex(x => x.CustomerAdvanceId, "uq_customer_advance_movements_received")
            .IsUnique()
            .HasFilter("\"movement_type\" = 1")
            .HasDatabaseName("uq_customer_advance_movements_received");
        builder
            .HasIndex(x => x.CustomerAdvanceId, "uq_customer_advance_movements_termination")
            .IsUnique()
            .HasFilter("\"movement_type\" IN (6, 7)")
            .HasDatabaseName("uq_customer_advance_movements_termination");
    }

    private static void Immutable<TProperty>(PropertyBuilder<TProperty> property) =>
        property.Metadata.SetAfterSaveBehavior(PropertySaveBehavior.Throw);
}

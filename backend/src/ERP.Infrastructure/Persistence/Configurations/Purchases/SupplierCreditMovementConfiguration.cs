using ERP.Domain.Modules.Purchases.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ERP.Infrastructure.Persistence.Configurations.Purchases;

/// <summary>
/// Mapeo EF de <see cref="SupplierCreditMovement"/> — diseño P0-02 §7.5/§13.3, Fase 2. Los CHECK
/// combinados se expresan como equivalencia (⇔) para reforzar exactamente el mismo invariante que
/// ya validan los guards de <c>SupplierCreditMovement.Create</c> en ambas direcciones — nunca solo
/// la implicación exigida en un sentido.
/// </summary>
public sealed class SupplierCreditMovementConfiguration
    : IEntityTypeConfiguration<SupplierCreditMovement>
{
    public void Configure(EntityTypeBuilder<SupplierCreditMovement> builder)
    {
        builder.ToTable(
            "supplier_credit_movements",
            t =>
            {
                t.HasCheckConstraint(
                    "chk_supplier_credit_movement_amount_positive",
                    "\"amount\" > 0"
                );
                // MovementType 1=Application, 3=ReversalOfApplication ⇔ target_purchase_payable_id NOT NULL.
                t.HasCheckConstraint(
                    "chk_supplier_credit_movement_target_payable",
                    "(\"movement_type\" IN (1, 3) AND \"target_purchase_payable_id\" IS NOT NULL) "
                        + "OR (\"movement_type\" NOT IN (1, 3) AND \"target_purchase_payable_id\" IS NULL)"
                );
                // MovementType 3=ReversalOfApplication, 4=ReversalOfRefund ⇔ reversal_of_movement_id NOT NULL.
                t.HasCheckConstraint(
                    "chk_supplier_credit_movement_reversal_ref",
                    "(\"movement_type\" IN (3, 4) AND \"reversal_of_movement_id\" IS NOT NULL) "
                        + "OR (\"movement_type\" NOT IN (3, 4) AND \"reversal_of_movement_id\" IS NULL)"
                );
            }
        );

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id").IsRequired();
        builder.Property(x => x.TenantId).HasColumnName("tenant_id").IsRequired();
        builder.Property(x => x.SupplierCreditId).HasColumnName("supplier_credit_id").IsRequired();
        builder
            .Property(x => x.MovementType)
            .HasColumnName("movement_type")
            .HasConversion<int>()
            .IsRequired();
        builder
            .Property(x => x.Amount)
            .HasColumnName("amount")
            .HasColumnType("numeric(18,2)")
            .IsRequired();
        builder
            .Property(x => x.TargetPurchasePayableId)
            .HasColumnName("target_purchase_payable_id");
        builder.Property(x => x.ReversalOfMovementId).HasColumnName("reversal_of_movement_id");

        // ── Idempotencia (§7.5, §16.2) ──────────────────────────────────────
        builder.Property(x => x.ClientRequestId).HasColumnName("client_request_id").IsRequired();
        builder
            .Property(x => x.RequestPayloadHash)
            .HasColumnName("request_payload_hash")
            .HasMaxLength(128)
            .IsRequired();

        builder.Property(x => x.CreatedAtUtc).HasColumnName("created_at_utc").IsRequired();
        builder.Property(x => x.CreatedByUserId).HasColumnName("created_by_user_id").IsRequired();

        // ── Relationships ────────────────────────────────────────────────────
        builder
            .HasOne<PurchasePayable>()
            .WithMany()
            .HasForeignKey(x => x.TargetPurchasePayableId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.Restrict);

        builder
            .HasOne<SupplierCreditMovement>()
            .WithMany()
            .HasForeignKey(x => x.ReversalOfMovementId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.Restrict);

        // ── Indexes ──────────────────────────────────────────────────────────
        builder
            .HasIndex(x => new { x.TenantId, x.SupplierCreditId })
            .HasDatabaseName("ix_supplier_credit_movements_tenant_supplier_credit");

        builder
            .HasIndex(x => x.ReversalOfMovementId)
            .IsUnique()
            .HasFilter("\"reversal_of_movement_id\" IS NOT NULL")
            .HasDatabaseName("uq_supplier_credit_movements_reversal_of_movement");

        builder
            .HasIndex(x => new { x.TenantId, x.ClientRequestId })
            .IsUnique()
            .HasDatabaseName("uq_supplier_credit_movements_tenant_client_request_id");
    }
}

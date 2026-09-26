using ERP.Domain.Modules.Caja.Entities;
using ERP.Domain.Modules.Company.Entities;
using ERP.Domain.Modules.Payables.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ERP.Infrastructure.Persistence.Configurations.Caja;

/// <summary>
/// ZH-CASH-FUNDING-REQUEST-FOUNDATION-02E-B — mapeo EF de <see cref="CashFundingRequest"/>.
/// Invariantes de estado reforzadas en BD (defensa en profundidad del dominio): efectivo > 0,
/// total ≥ efectivo, SupplierPaymentId solo en Fulfilled, datos de resolución solo en estados
/// terminales. PaymentPayload = jsonb (snapshot versionado). Concurrencia optimista vía xmin.
/// </summary>
public sealed class CashFundingRequestConfiguration : IEntityTypeConfiguration<CashFundingRequest>
{
    public void Configure(EntityTypeBuilder<CashFundingRequest> builder)
    {
        builder.ToTable(
            "cash_funding_requests",
            t =>
            {
                t.HasCheckConstraint("chk_cash_funding_requests_cash_amount_positive", "\"cash_amount\" > 0");
                t.HasCheckConstraint(
                    "chk_cash_funding_requests_total_covers_cash",
                    "\"total_amount\" >= \"cash_amount\""
                );
                // SupplierPaymentId existe si y solo si la solicitud está Fulfilled (2).
                t.HasCheckConstraint(
                    "chk_cash_funding_requests_payment_only_when_fulfilled",
                    "(\"status\" = 2) = (\"supplier_payment_id\" IS NOT NULL)"
                );
                // Pending (1) sin datos de resolución; terminales siempre con quién y cuándo.
                t.HasCheckConstraint(
                    "chk_cash_funding_requests_resolution_consistency",
                    "(\"status\" = 1 AND \"resolved_by_user_id\" IS NULL AND \"resolved_at_utc\" IS NULL) "
                        + "OR (\"status\" <> 1 AND \"resolved_by_user_id\" IS NOT NULL AND \"resolved_at_utc\" IS NOT NULL)"
                );
                t.HasCheckConstraint("chk_cash_funding_requests_payload_version", "\"payload_version\" >= 1");
            }
        );

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id").IsRequired();
        builder.Property(x => x.TenantId).HasColumnName("tenant_id").IsRequired();
        builder.Property(x => x.CompanyId).HasColumnName("company_id").IsRequired();
        builder.Property(x => x.BranchId).HasColumnName("branch_id").IsRequired();
        builder.Property(x => x.CashRegisterId).HasColumnName("cash_register_id").IsRequired();
        builder.Property(x => x.CashSessionId).HasColumnName("cash_session_id").IsRequired();
        builder.Property(x => x.SupplierId).HasColumnName("supplier_id").IsRequired();
        builder.Property(x => x.TotalAmount).HasColumnName("total_amount").HasPrecision(18, 2).IsRequired();
        builder.Property(x => x.CashAmount).HasColumnName("cash_amount").HasPrecision(18, 2).IsRequired();
        builder.Property(x => x.RequestedByUserId).HasColumnName("requested_by_user_id").IsRequired();
        builder.Property(x => x.RequestedAtUtc).HasColumnName("requested_at_utc").IsRequired();
        builder.Property(x => x.Status).HasColumnName("status").HasConversion<int>().IsRequired();
        builder.Property(x => x.ResolvedByUserId).HasColumnName("resolved_by_user_id");
        builder.Property(x => x.ResolvedAtUtc).HasColumnName("resolved_at_utc");
        builder
            .Property(x => x.ResolutionReason)
            .HasColumnName("resolution_reason")
            .HasMaxLength(CashFundingRequest.ResolutionReasonMaxLen);
        builder.Property(x => x.SupplierPaymentId).HasColumnName("supplier_payment_id");
        builder.Property(x => x.PaymentPayload).HasColumnName("payment_payload").HasColumnType("jsonb").IsRequired();
        builder.Property(x => x.PayloadVersion).HasColumnName("payload_version").IsRequired();
        builder
            .Property(x => x.PayloadHash)
            .HasColumnName("payload_hash")
            .HasMaxLength(CashFundingRequest.PayloadHashMaxLen)
            .IsRequired();
        builder.Property(x => x.ClientRequestId).HasColumnName("client_request_id").IsRequired();
        builder.Ignore(x => x.IsPending);

        builder
            .Property<uint>("xmin")
            .HasColumnName("xmin")
            .HasColumnType("xid")
            .IsRequired()
            .ValueGeneratedOnAddOrUpdate()
            .IsConcurrencyToken();

        builder.HasOne<Company>().WithMany().HasForeignKey(x => x.CompanyId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<CashRegister>().WithMany().HasForeignKey(x => x.CashRegisterId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<CashSession>().WithMany().HasForeignKey(x => x.CashSessionId).OnDelete(DeleteBehavior.Restrict);
        builder
            .HasOne<SupplierPayment>()
            .WithMany()
            .HasForeignKey(x => x.SupplierPaymentId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.Restrict);

        // ── Indexes ──────────────────────────────────────────────────────
        // Bandeja del cajero: solicitudes de una sesión por estado.
        builder
            .HasIndex(x => new { x.TenantId, x.CompanyId, x.CashSessionId, x.Status })
            .HasDatabaseName("ix_cash_funding_requests_tenant_company_session_status");

        // "Mis solicitudes": por solicitante y estado.
        builder
            .HasIndex(x => new { x.TenantId, x.CompanyId, x.RequestedByUserId, x.Status })
            .HasDatabaseName("ix_cash_funding_requests_tenant_company_requester_status");

        builder
            .HasIndex(x => new { x.TenantId, x.ClientRequestId })
            .IsUnique()
            .HasDatabaseName("uq_cash_funding_requests_tenant_client_request_id");

        // Una solicitud nunca puede consumirse por más de un pago, ni un pago consumir dos solicitudes.
        builder
            .HasIndex(x => x.SupplierPaymentId)
            .IsUnique()
            .HasFilter("\"supplier_payment_id\" IS NOT NULL")
            .HasDatabaseName("uq_cash_funding_requests_supplier_payment");
    }
}

using ERP.Domain.Branches.Entities;
using ERP.Domain.MasterData.Entities;
using ERP.Domain.Modules.Company.Entities;
using ERP.Domain.Modules.Purchases.Entities;
using ERP.Domain.Modules.Purchases.PurchaseReception.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ERP.Infrastructure.Persistence.Configurations.Purchases;

/// <summary>
/// Mapeo EF de <see cref="PurchaseReturn"/> — diseño P0-02 §7.1, Fase 2. Índices únicos de
/// idempotencia (§16.2) sobre las 4 parejas ClientRequestId, además de los índices de negocio
/// (número de devolución, vínculo de NC).
/// </summary>
public sealed class PurchaseReturnConfiguration : IEntityTypeConfiguration<PurchaseReturn>
{
    public void Configure(EntityTypeBuilder<PurchaseReturn> builder)
    {
        builder.ToTable("purchase_returns");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id").IsRequired();
        builder.Property(x => x.TenantId).HasColumnName("tenant_id").IsRequired();
        builder.Property(x => x.CompanyId).HasColumnName("company_id").IsRequired();

        // Branch Ownership Rule (§5.2) — obligatorio, nunca sustituible tras CreateDraft().
        builder.Property(x => x.BranchId).HasColumnName("branch_id").IsRequired();

        builder.Property(x => x.PurchaseInvoiceId).HasColumnName("purchase_invoice_id").IsRequired();
        builder.Property(x => x.SupplierId).HasColumnName("supplier_id").IsRequired();

        builder
            .Property(x => x.ReturnNumber)
            .HasColumnName("return_number")
            .HasMaxLength(PurchaseReturn.ReturnNumberMaxLen);
        builder
            .Property(x => x.Reason)
            .HasColumnName("reason")
            .HasMaxLength(PurchaseReturn.ReasonMaxLen)
            .IsRequired();

        builder.Property(x => x.Status).HasColumnName("status").HasConversion<int>().IsRequired();
        builder
            .Property(x => x.FiscalStatus)
            .HasColumnName("fiscal_status")
            .HasConversion<int>()
            .IsRequired();

        builder.Property(x => x.SupplierCreditNoteDocumentId).HasColumnName("supplier_credit_note_document_id");

        // ── Totales snapshot (congelados al autorizar, §11.1/§19.1bis) ──────
        builder
            .Property(x => x.AuthorizedSubtotal)
            .HasColumnName("authorized_subtotal")
            .HasColumnType("numeric(18,2)");
        builder
            .Property(x => x.AuthorizedVatTotal)
            .HasColumnName("authorized_vat_total")
            .HasColumnType("numeric(18,2)");
        builder
            .Property(x => x.AuthorizedIceTotal)
            .HasColumnName("authorized_ice_total")
            .HasColumnType("numeric(18,2)");
        builder
            .Property(x => x.AuthorizedDiscountTotal)
            .HasColumnName("authorized_discount_total")
            .HasColumnType("numeric(18,2)");
        builder
            .Property(x => x.AuthorizedGrandTotal)
            .HasColumnName("authorized_grand_total")
            .HasColumnType("numeric(18,2)");
        builder
            .Property(x => x.HistoricalCostTotal)
            .HasColumnName("historical_cost_total")
            .HasColumnType("numeric(18,2)");
        builder
            .Property(x => x.CostVarianceTotal)
            .HasColumnName("cost_variance_total")
            .HasColumnType("numeric(18,2)");
        builder
            .Property(x => x.AppliedToPayableAmount)
            .HasColumnName("applied_to_payable_amount")
            .HasColumnType("numeric(18,2)");
        builder
            .Property(x => x.SupplierCreditAmount)
            .HasColumnName("supplier_credit_amount")
            .HasColumnType("numeric(18,2)");

        builder.Property(x => x.AuthorizedAtUtc).HasColumnName("authorized_at_utc");
        builder.Property(x => x.AuthorizedByUserId).HasColumnName("authorized_by_user_id");
        builder.Property(x => x.CancelledAtUtc).HasColumnName("cancelled_at_utc");
        builder.Property(x => x.CancelledByUserId).HasColumnName("cancelled_by_user_id");
        builder
            .Property(x => x.CancellationReason)
            .HasColumnName("cancellation_reason")
            .HasMaxLength(PurchaseReturn.CancellationReasonMaxLen);

        // ── Idempotencia (§7.1, §16.2 — Enmienda P0-02_PURCHASE_RETURN_DOMAIN_AMENDMENT_01) ──
        builder
            .Property(x => x.CreateClientRequestId)
            .HasColumnName("create_client_request_id")
            .IsRequired();
        builder
            .Property(x => x.CreateRequestPayloadHash)
            .HasColumnName("create_request_payload_hash")
            .HasMaxLength(128)
            .IsRequired();
        builder.Property(x => x.AuthorizeClientRequestId).HasColumnName("authorize_client_request_id");
        builder
            .Property(x => x.AuthorizeRequestPayloadHash)
            .HasColumnName("authorize_request_payload_hash")
            .HasMaxLength(128);
        builder.Property(x => x.CancelClientRequestId).HasColumnName("cancel_client_request_id");
        builder
            .Property(x => x.CancelRequestPayloadHash)
            .HasColumnName("cancel_request_payload_hash")
            .HasMaxLength(128);
        builder
            .Property(x => x.LinkCreditNoteClientRequestId)
            .HasColumnName("link_credit_note_client_request_id");
        builder
            .Property(x => x.LinkCreditNoteRequestPayloadHash)
            .HasColumnName("link_credit_note_request_payload_hash")
            .HasMaxLength(128);

        // ── Auditoría embebida ───────────────────────────────────────────────
        builder.Property(x => x.CreatedAt).HasColumnName("created_at");
        builder.Property(x => x.UpdatedAt).HasColumnName("updated_at");
        builder.Property(x => x.CreatedBy).HasColumnName("created_by");
        builder.Property(x => x.UpdatedBy).HasColumnName("updated_by");

        // ── Concurrency token (PostgreSQL xid) ──────────────────────────────
        builder
            .Property<uint>("xmin")
            .HasColumnName("xmin")
            .HasColumnType("xid")
            .IsRequired()
            .ValueGeneratedOnAddOrUpdate()
            .IsConcurrencyToken();

        // ── Computed properties (NOT persisted) ─────────────────────────────
        builder.Ignore(x => x.GrandTotal);

        // ── Relationships ────────────────────────────────────────────────────
        builder
            .HasMany(x => x.Lines)
            .WithOne()
            .HasForeignKey(x => x.PurchaseReturnId)
            .OnDelete(DeleteBehavior.Cascade);

        builder
            .HasOne<PurchaseInvoice>()
            .WithMany()
            .HasForeignKey(x => x.PurchaseInvoiceId)
            .OnDelete(DeleteBehavior.Restrict);

        builder
            .HasOne<BusinessPartner>()
            .WithMany()
            .HasForeignKey(x => x.SupplierId)
            .OnDelete(DeleteBehavior.Restrict);

        builder
            .HasOne<Company>()
            .WithMany()
            .HasForeignKey(x => x.CompanyId)
            .OnDelete(DeleteBehavior.Restrict);

        builder
            .HasOne<Branch>()
            .WithMany()
            .HasForeignKey(x => x.BranchId)
            .OnDelete(DeleteBehavior.Restrict);

        // Referencia (no copia) al documento de NC vinculado (§18) — 1:1 vía índice único abajo.
        builder
            .HasOne<PurchaseReceptionDocument>()
            .WithMany()
            .HasForeignKey(x => x.SupplierCreditNoteDocumentId)
            .OnDelete(DeleteBehavior.Restrict);

        // ── Indexes ──────────────────────────────────────────────────────────
        builder
            .HasIndex(x => new { x.TenantId, x.CompanyId, x.BranchId })
            .HasDatabaseName("ix_purchase_returns_tenant_company_branch");

        builder
            .HasIndex(x => new { x.TenantId, x.PurchaseInvoiceId })
            .HasDatabaseName("ix_purchase_returns_tenant_purchase_invoice");

        builder
            .HasIndex(x => new { x.TenantId, x.Status })
            .HasDatabaseName("ix_purchase_returns_tenant_status");

        builder
            .HasIndex(x => new
            {
                x.TenantId,
                x.CompanyId,
                x.ReturnNumber,
            })
            .IsUnique()
            .HasFilter("\"return_number\" IS NOT NULL")
            .HasDatabaseName("uq_purchase_returns_tenant_company_return_number");

        builder
            .HasIndex(x => new { x.TenantId, x.CreateClientRequestId })
            .IsUnique()
            .HasDatabaseName("uq_purchase_returns_tenant_create_client_request_id");

        builder
            .HasIndex(x => new { x.TenantId, x.AuthorizeClientRequestId })
            .IsUnique()
            .HasFilter("\"authorize_client_request_id\" IS NOT NULL")
            .HasDatabaseName("uq_purchase_returns_tenant_authorize_client_request_id");

        builder
            .HasIndex(x => new { x.TenantId, x.CancelClientRequestId })
            .IsUnique()
            .HasFilter("\"cancel_client_request_id\" IS NOT NULL")
            .HasDatabaseName("uq_purchase_returns_tenant_cancel_client_request_id");

        builder
            .HasIndex(x => new { x.TenantId, x.LinkCreditNoteClientRequestId })
            .IsUnique()
            .HasFilter("\"link_credit_note_client_request_id\" IS NOT NULL")
            .HasDatabaseName("uq_purchase_returns_tenant_link_credit_note_client_request_id");

        builder
            .HasIndex(x => new { x.TenantId, x.SupplierCreditNoteDocumentId })
            .IsUnique()
            .HasFilter("\"supplier_credit_note_document_id\" IS NOT NULL")
            .HasDatabaseName("uq_purchase_returns_tenant_supplier_credit_note_document_id");
    }
}

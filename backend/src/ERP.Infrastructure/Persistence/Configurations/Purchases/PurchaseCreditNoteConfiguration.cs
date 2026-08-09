using ERP.Domain.Branches.Entities;
using ERP.Domain.MasterData.Entities;
using ERP.Domain.Modules.Company.Entities;
using ERP.Domain.Modules.Purchases.Entities;
using ERP.Domain.Modules.Purchases.PurchaseReception.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ERP.Infrastructure.Persistence.Configurations.Purchases;

/// <summary>
/// Mapeo EF de <see cref="PurchaseCreditNote"/> — diseño FLOW-READY-02C §5.2. Índices únicos de
/// idempotencia sobre las 3 parejas ClientRequestId, además de los índices de negocio
/// (ReceptionDocumentId, AccessKey, SupplierId+CreditNoteNumber — §5.2 del diseño).
/// </summary>
public sealed class PurchaseCreditNoteConfiguration : IEntityTypeConfiguration<PurchaseCreditNote>
{
    public void Configure(EntityTypeBuilder<PurchaseCreditNote> builder)
    {
        builder.ToTable("purchase_credit_notes");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id").IsRequired();
        builder.Property(x => x.TenantId).HasColumnName("tenant_id").IsRequired();
        builder.Property(x => x.CompanyId).HasColumnName("company_id").IsRequired();

        // Branch Ownership Rule — obligatorio, nunca sustituible tras CreateDraft().
        builder.Property(x => x.BranchId).HasColumnName("branch_id").IsRequired();

        builder.Property(x => x.SupplierId).HasColumnName("supplier_id").IsRequired();
        builder
            .Property(x => x.PurchaseInvoiceId)
            .HasColumnName("purchase_invoice_id")
            .IsRequired();
        builder
            .Property(x => x.ReceptionDocumentId)
            .HasColumnName("reception_document_id");

        // FLOW-READY-02C-R1.1 — obligatorio, inmutable tras CreateDraft().
        builder
            .Property(x => x.ApplicationType)
            .HasColumnName("application_type")
            .HasConversion<int>()
            .IsRequired();

        // FLOW-READY-02C-R1.1 — set-once vía LinkPurchaseReturn(), solo para ApplicationType.Return.
        builder
            .Property(x => x.LinkedPurchaseReturnId)
            .HasColumnName("linked_purchase_return_id");

        builder.Property(x => x.Status).HasColumnName("status").HasConversion<int>().IsRequired();

        builder
            .Property(x => x.CreditNoteNumber)
            .HasColumnName("credit_note_number")
            .HasMaxLength(PurchaseCreditNote.CreditNoteNumberMaxLen)
            .IsRequired();
        builder
            .Property(x => x.AccessKey)
            .HasColumnName("access_key")
            .HasMaxLength(PurchaseCreditNote.AccessKeyMaxLen);
        builder
            .Property(x => x.AuthorizationNumber)
            .HasColumnName("authorization_number")
            .HasMaxLength(PurchaseCreditNote.AuthorizationNumberMaxLen);
        builder.Property(x => x.AuthorizationDate).HasColumnName("authorization_date");
        builder.Property(x => x.IssueDate).HasColumnName("issue_date").IsRequired();

        builder
            .Property(x => x.Reason)
            .HasColumnName("reason")
            .HasMaxLength(PurchaseCreditNote.ReasonMaxLen)
            .IsRequired();

        // ── Snapshot financiero (§5.3) ───────────────────────────────────────
        builder
            .Property(x => x.Subtotal)
            .HasColumnName("subtotal")
            .HasColumnType("numeric(18,2)")
            .IsRequired();
        // FLOW-READY-02C-R1.2 — suma de PurchaseCreditNoteTaxSummary.IceAmount (líneas libres nunca tienen ICE).
        builder
            .Property(x => x.IceAmount)
            .HasColumnName("ice_amount")
            .HasColumnType("numeric(18,2)")
            .IsRequired();
        builder
            .Property(x => x.VatAmount)
            .HasColumnName("vat_amount")
            .HasColumnType("numeric(18,2)")
            .IsRequired();
        builder
            .Property(x => x.TotalAmount)
            .HasColumnName("total_amount")
            .HasColumnType("numeric(18,2)")
            .IsRequired();
        builder
            .Property(x => x.AppliedToPayableAmount)
            .HasColumnName("applied_to_payable_amount")
            .HasColumnType("numeric(18,2)");

        builder.Property(x => x.AuthorizedAtUtc).HasColumnName("authorized_at_utc");
        builder.Property(x => x.AuthorizedByUserId).HasColumnName("authorized_by_user_id");
        builder.Property(x => x.CancelledAtUtc).HasColumnName("cancelled_at_utc");
        builder.Property(x => x.CancelledByUserId).HasColumnName("cancelled_by_user_id");
        builder
            .Property(x => x.CancellationReason)
            .HasColumnName("cancellation_reason")
            .HasMaxLength(PurchaseCreditNote.CancellationReasonMaxLen);

        // ── Idempotencia ──────────────────────────────────────────────────
        builder
            .Property(x => x.CreateClientRequestId)
            .HasColumnName("create_client_request_id")
            .IsRequired();
        builder
            .Property(x => x.CreateRequestPayloadHash)
            .HasColumnName("create_request_payload_hash")
            .HasMaxLength(128)
            .IsRequired();
        builder
            .Property(x => x.AuthorizeClientRequestId)
            .HasColumnName("authorize_client_request_id");
        builder
            .Property(x => x.AuthorizeRequestPayloadHash)
            .HasColumnName("authorize_request_payload_hash")
            .HasMaxLength(128);
        builder.Property(x => x.CancelClientRequestId).HasColumnName("cancel_client_request_id");
        builder
            .Property(x => x.CancelRequestPayloadHash)
            .HasColumnName("cancel_request_payload_hash")
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

        // ── Relationships ────────────────────────────────────────────────────
        builder
            .HasMany(x => x.Lines)
            .WithOne()
            .HasForeignKey(x => x.PurchaseCreditNoteId)
            .OnDelete(DeleteBehavior.Cascade);

        // FLOW-READY-02C-R1.2 — flujo principal de Discount, mismo criterio cascade que Lines.
        builder
            .HasMany(x => x.TaxSummaries)
            .WithOne()
            .HasForeignKey(x => x.PurchaseCreditNoteId)
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

        // Referencia (no copia) al documento de recepción (NC) vinculado — 1:1 vía índice único abajo.
        builder
            .HasOne<PurchaseReceptionDocument>()
            .WithMany()
            .HasForeignKey(x => x.ReceptionDocumentId)
            .OnDelete(DeleteBehavior.Restrict);

        // FLOW-READY-02C-R1.1 — FK restrictiva hacia purchase_returns, referencia (no copia), 1:1
        // vía índice único filtrado abajo. Nunca se elimina un PurchaseReturn en cascada por esto.
        builder
            .HasOne<PurchaseReturn>()
            .WithMany()
            .HasForeignKey(x => x.LinkedPurchaseReturnId)
            .OnDelete(DeleteBehavior.Restrict);

        // ── Indexes ──────────────────────────────────────────────────────────
        builder
            .HasIndex(x => new
            {
                x.TenantId,
                x.CompanyId,
                x.BranchId,
            })
            .HasDatabaseName("ix_purchase_credit_notes_tenant_company_branch");

        builder
            .HasIndex(x => new { x.TenantId, x.PurchaseInvoiceId })
            .HasDatabaseName("ix_purchase_credit_notes_tenant_purchase_invoice");

        builder
            .HasIndex(x => new { x.TenantId, x.Status })
            .HasDatabaseName("ix_purchase_credit_notes_tenant_status");

        // Regla de duplicados §5.2 — 1 documento de recepción → máx. 1 PurchaseCreditNote.
        builder
            .HasIndex(x => new { x.TenantId, x.ReceptionDocumentId })
            .IsUnique()
            .HasFilter("\"reception_document_id\" IS NOT NULL")
            .HasDatabaseName("uq_purchase_credit_notes_tenant_reception_document_id");

        // Regla de duplicados §5.2 — mirror de uq_purchase_invoices_tenant_access_key.
        builder
            .HasIndex(x => new { x.TenantId, x.AccessKey })
            .IsUnique()
            .HasFilter("\"access_key\" IS NOT NULL")
            .HasDatabaseName("uq_purchase_credit_notes_tenant_access_key");

        // Regla de duplicados §5.2 — mirror de uq_purchase_invoices_tenant_company_supplier_number.
        builder
            .HasIndex(x => new
            {
                x.TenantId,
                x.CompanyId,
                x.SupplierId,
                x.CreditNoteNumber,
            })
            .IsUnique()
            .HasDatabaseName("uq_purchase_credit_notes_tenant_company_supplier_number");

        builder
            .HasIndex(x => new { x.TenantId, x.CreateClientRequestId })
            .IsUnique()
            .HasDatabaseName("uq_purchase_credit_notes_tenant_create_client_request_id");

        builder
            .HasIndex(x => new { x.TenantId, x.AuthorizeClientRequestId })
            .IsUnique()
            .HasFilter("\"authorize_client_request_id\" IS NOT NULL")
            .HasDatabaseName("uq_purchase_credit_notes_tenant_authorize_client_request_id");

        builder
            .HasIndex(x => new { x.TenantId, x.CancelClientRequestId })
            .IsUnique()
            .HasFilter("\"cancel_client_request_id\" IS NOT NULL")
            .HasDatabaseName("uq_purchase_credit_notes_tenant_cancel_client_request_id");

        // FLOW-READY-02C-R1.1 — 1 PurchaseReturn → máx. 1 PurchaseCreditNote vinculada.
        builder
            .HasIndex(x => new { x.TenantId, x.LinkedPurchaseReturnId })
            .IsUnique()
            .HasFilter("\"linked_purchase_return_id\" IS NOT NULL")
            .HasDatabaseName("uq_purchase_credit_notes_tenant_linked_purchase_return_id");
    }
}

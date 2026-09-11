using ERP.Domain.Modules.Purchases.Entities;

namespace ERP.Domain.Modules.Purchases.Interfaces;

/// <summary>
/// Contrato de persistencia de <see cref="PurchaseCreditNote"/> — diseño FLOW-READY-02C, fase
/// Application/API (.2). Mismo patrón que <see cref="IPurchaseReturnRepository"/>.
/// </summary>
public interface IPurchaseCreditNoteRepository
{
    Task<PurchaseCreditNote?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct = default);

    Task<PurchaseCreditNote?> GetByLinkedPurchaseReturnIdAsync(Guid tenantId, Guid returnId, CancellationToken ct = default);

    Task AddAsync(PurchaseCreditNote creditNote, CancellationToken ct = default);
    Task SaveChangesAsync(CancellationToken ct = default);

    /// <summary>Mecanismo de idempotencia obligatorio de <c>CreateDraft</c> (mismo criterio que <see cref="IPurchaseReturnRepository.GetByCreateClientRequestIdAsync"/>).</summary>
    Task<PurchaseCreditNote?> GetByCreateClientRequestIdAsync(
        Guid tenantId,
        Guid createClientRequestId,
        CancellationToken ct = default
    );

    /// <summary>
    /// Descubrimiento mínimo, sin tracking, del <c>PurchaseInvoiceId</c> dueño de un
    /// <c>PurchaseCreditNote</c> — usado para determinar qué Lock A (<c>IPurchaseReturnRepository.
    /// AcquireFinancialLockAsync</c>, namespace "PurchaseInvoice.FinancialLock") adquirir ANTES de
    /// la recarga autoritativa, mismo patrón exacto que <see cref="IPurchaseReturnRepository.GetPurchaseInvoiceIdAsync"/>.
    /// </summary>
    Task<Guid?> GetPurchaseInvoiceIdAsync(
        Guid tenantId,
        Guid purchaseCreditNoteId,
        CancellationToken ct = default
    );

    Task<bool> ExistsByReceptionDocumentIdAsync(
        Guid tenantId,
        Guid receptionDocumentId,
        CancellationToken ct = default
    );

    /// <summary>
    /// PURCHASE-CREDIT-NOTE-RECEPTION-IDEMPOTENCY-UI-01 — descubrimiento mínimo, sin tracking, del
    /// Id de la <see cref="Entities.PurchaseCreditNote"/> ACTIVA (Draft/Authorized) ya vinculada a
    /// esta recepción, si existe — una NC <c>Cancelled</c> nunca cuenta
    /// (PURCHASE-RECEPTION-CREDIT-NOTE-CANCELLED-REPROCESS-01). Usado por el listado de Recepción
    /// para ofrecer "Ver NC existente" en vez de reabrir el formulario de creación — nunca carga el
    /// agregado completo, mismo criterio que <see cref="GetPurchaseInvoiceIdAsync"/>.
    /// </summary>
    Task<Guid?> GetIdByReceptionDocumentIdAsync(
        Guid tenantId,
        Guid receptionDocumentId,
        CancellationToken ct = default
    );

    /// <summary>
    /// PURCHASE-RECEPTION-CREDIT-NOTE-CANCELLED-REPROCESS-01 — Id de la NC <c>Cancelled</c> más
    /// reciente para esta recepción, si existe (nunca la activa) — para que la UI de Recepción
    /// ofrezca "Ver NC anulada" (historial) mientras permite "Procesar nuevamente".
    /// </summary>
    Task<Guid?> GetLatestCancelledIdByReceptionDocumentIdAsync(
        Guid tenantId,
        Guid receptionDocumentId,
        CancellationToken ct = default
    );

    /// <summary>
    /// PURCHASE-CREDIT-NOTE-CANCELLED-ACCESSKEY-REPROCESS-01 — solo cuenta como duplicado una NC
    /// ACTIVA (Draft/Authorized) con este AccessKey; una NC <c>Cancelled</c> es historial, nunca
    /// bloquea reprocesar la misma clave de acceso (mismo criterio que
    /// <see cref="GetIdByReceptionDocumentIdAsync"/>).
    /// </summary>
    Task<bool> ExistsByAccessKeyAsync(
        Guid tenantId,
        string accessKey,
        CancellationToken ct = default
    );

    /// <summary>
    /// PURCHASE-CREDIT-NOTE-CANCELLED-NUMBER-REPROCESS-01 — solo cuenta como duplicado una NC
    /// ACTIVA (Draft/Authorized) con este supplier+creditNoteNumber; una NC <c>Cancelled</c> es
    /// historial, nunca bloquea reutilizar el mismo número (mismo criterio que
    /// <see cref="ExistsByAccessKeyAsync"/>/<see cref="GetIdByReceptionDocumentIdAsync"/>).
    /// </summary>
    Task<bool> ExistsBySupplierAndCreditNoteNumberAsync(
        Guid tenantId,
        Guid companyId,
        Guid supplierId,
        string creditNoteNumber,
        CancellationToken ct = default
    );

    /// <summary>
    /// FLOW-READY-02C-R1.1 — indica si otra <see cref="Entities.PurchaseCreditNote"/> (distinta de
    /// <paramref name="excludePurchaseCreditNoteId"/>) ya está vinculada a este
    /// <paramref name="purchaseReturnId"/> — 1:1 aplicado también vía índice único filtrado.
    /// </summary>
    Task<bool> ExistsByLinkedPurchaseReturnIdAsync(
        Guid tenantId,
        Guid purchaseReturnId,
        Guid? excludePurchaseCreditNoteId = null,
        CancellationToken ct = default
    );

    Task<(IReadOnlyList<PurchaseCreditNote> Items, int Total)> GetPagedAsync(
        Guid tenantId,
        string? status,
        Guid? supplierId,
        Guid? purchaseInvoiceId,
        DateOnly? dateFrom,
        DateOnly? dateTo,
        int page,
        int pageSize,
        CancellationToken ct = default
    );

    /// <summary>
    /// FLOW-READY-02C-R1.2 — suma <c>PurchaseCreditNoteTaxSummary.TaxableBase</c> ya acreditada por
    /// NC de compra no canceladas (Draft + Authorized cuentan, para evitar doble aplicación entre
    /// borradores concurrentes), agrupada por <c>SourcePurchaseInvoiceTaxSummaryId</c>. Claves
    /// ausentes en el resultado implican acreditado cero. <paramref name="excludePurchaseCreditNoteId"/>
    /// excluye la propia NC al recalcular en <c>UpdateDraft</c> — nunca se cuenta dos veces a sí misma.
    /// </summary>
    Task<IReadOnlyDictionary<Guid, decimal>> GetCreditedTaxableBaseByPurchaseTaxSummaryIdsAsync(
        Guid tenantId,
        IReadOnlyCollection<Guid> sourcePurchaseInvoiceTaxSummaryIds,
        Guid? excludePurchaseCreditNoteId = null,
        CancellationToken ct = default
    );

    /// <summary>
    /// ACCOUNTING-CREDIT-NOTES-POSTING-08: proyección liviana para resolver el origen documental
    /// humano de un JournalEntry — mismo criterio que
    /// <c>ISalesInvoiceRepository.GetJournalSourceSummariesByIdsAsync</c>/
    /// <c>IPurchaseInvoiceRepository.GetJournalSourceSummariesByIdsAsync</c>. Sin Include de
    /// líneas/resúmenes fiscales. <c>SupplierId</c> se resuelve a nombre en Application (mismo
    /// repositorio de BusinessPartner ya usado por Finance), no aquí.
    /// </summary>
    Task<
        IReadOnlyDictionary<
            Guid,
            (Guid SupplierId, string CreditNoteNumber, string Status, DateOnly IssueDate)
        >
    > GetJournalSourceSummariesByIdsAsync(
        Guid tenantId,
        IReadOnlyCollection<Guid> ids,
        CancellationToken ct = default
    );
}

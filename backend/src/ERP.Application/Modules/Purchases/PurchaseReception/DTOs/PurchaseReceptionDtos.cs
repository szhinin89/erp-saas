namespace ERP.Application.Modules.Purchases.PurchaseReception.DTOs;

public sealed record PurchaseReceptionItemDto(
    string SupplierRuc,
    string SupplierName,
    string SourceDocType,
    string InvoiceNumber,
    // Solo aplica a notas de crédito/débito — la factura que modifican. Null en Factura.
    string? ModifiedDocumentNumber,
    string AccessKey,
    DateOnly IssueDate,
    DateTime AuthorizationDate,
    // Subtotal/VatAmount: ya venían en el TXT SRI y se persisten en PurchaseReceptionDocument
    // (Create) desde siempre — solo no se exponían todavía en este DTO.
    decimal Subtotal,
    decimal VatAmount,
    decimal Total,
    bool SupplierExists,
    Guid? SupplierId,
    bool? SupplierIsActive,
    bool PurchaseExists,
    Guid? PurchaseId,
    // Solo aplica a notas de crédito: si la factura afectada (ModifiedDocumentNumber) ya está
    // ingresada como compra del mismo proveedor. False/null en Factura.
    bool AffectedPurchaseExists,
    Guid? AffectedPurchaseId,
    string Status,
    Guid DocumentId,
    string DocumentStatus,
    string ProcessingStatus,
    string? ProcessingNotes,
    // EXPENSES-FROM-RECEPTION-01 — si ya existe un Gasto con esta clave de acceso SRI (bloquea
    // "Crear compra" en la UI, mirror de PurchaseExists bloqueando "Crear gasto"). Solo aplica a
    // Factura — false en NC/ND.
    bool ExpenseExists,
    // PURCHASE-CREDIT-NOTE-RECEPTION-IDEMPOTENCY-UI-01 — si esta recepción (DocumentId) ya está
    // vinculada a un PurchaseCreditNote (1:1, ReceptionDocumentId único). Solo aplica a notas de
    // crédito — false/null en Factura/ND. Bloquea "Procesar NC" en la UI (el backend ya lo rechaza
    // por constraint único; esto evita que el usuario llegue a intentarlo).
    bool CreditNoteExists = false,
    Guid? CreditNoteId = null
);

public sealed record PurchaseReceptionImportResultDto(
    IReadOnlyList<PurchaseReceptionItemDto> Items,
    int TotalParsed,
    int ParseErrorCount,
    int SkippedUnsupportedCount
);

/// <summary>
/// EXPENSES-FROM-RECEPTION-01 — cabecera para precargar el formulario de Nuevo Gasto desde una
/// factura de recepción ya verificada. Sin líneas: la subcategoría de gasto por línea (obligatoria,
/// <c>ExpenseSubcategoryId</c>) es un criterio contable que solo la persona usuaria puede asignar —
/// no existe forma de inferirla del XML sin inventar una regla, así que el usuario completa el
/// detalle en el formulario con <see cref="Subtotal"/>/<see cref="VatAmount"/>/<see cref="Total"/>
/// como referencia de cuadre.
/// </summary>
public sealed record ExpenseReceptionDraftDto(
    Guid ReceptionDocumentId,
    string AccessKey,
    Guid SupplierId,
    string SupplierName,
    string SupplierTaxId,
    DateOnly IssueDate,
    string DocumentType,
    string DocumentNumber,
    string? AuthorizationNumber,
    DateTime? AuthorizationDate,
    decimal Subtotal,
    decimal VatAmount,
    decimal Total
);

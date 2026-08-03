namespace ERP.Application.Modules.Purchases.DTOs;

/// <summary>
/// P0-02 Fase 5 — línea devolvible de una <c>PurchaseInvoice</c> (diseño §10.2, §14.2):
/// cantidad original, ya devuelta (solo devoluciones <c>Authorized</c>), remanente, y
/// <c>WarehouseId</c> congelado de la línea original — solo lectura, nunca seleccionable.
/// </summary>
public sealed record ReturnableLineDto(
    Guid InvoiceDetailId,
    Guid ItemId,
    string Description,
    decimal OriginalQuantity,
    decimal ReturnedQuantity,
    decimal RemainingQuantity,
    Guid WarehouseId
);

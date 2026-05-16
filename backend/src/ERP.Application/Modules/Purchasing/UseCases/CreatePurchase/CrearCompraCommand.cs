using MediatR;
using ERP.Application.Common;
using ERP.Application.Modules.Purchasing.DTOs;

namespace ERP.Application.Modules.Purchasing.UseCases.CrearCompra;

public enum PurchaseCreationMode { Xml = 1, Manual = 2 }

/// <summary>Datos de una línea de detalle en modo Manual.</summary>
public sealed record PurchaseLineInput(
    string  Description,
    string? ProductCode,
    Guid?   ProductId,
    decimal Quantity,
    decimal UnitPrice,
    decimal DiscountPct,
    decimal VatPct);

/// <summary>
/// Distribución de la cantidad de un detalle (<see cref="ItemIndex"/>) hacia una Warehouse.
/// <see cref="ProductoId"/> opcional: si viene, enlaza inventario aunque la línea (p. ej. XML) no tenga producto.
/// </summary>
public sealed record WarehouseAllocationRequest(int ItemIndex, Guid    WarehouseId, decimal Quantity, Guid?     ProductId = null);

/// <summary>Crea una factura de compra. <c>WarehouseAllocations</c> es opcional; si viene, la suma por ítem debe igualar la cantidad de cada detalle.</summary>
[RequireFeature(SubscriptionFeatureCodes.Inventory)]
public sealed record CreatePurchaseCommand(
    PurchaseCreationMode Modo,

    // ── XML ──────────────────────────────────────────────────────────────
    byte[]? XmlContent,
    string? XmlFileName,

    // ── Manual ───────────────────────────────────────────────────────────
    Guid?     SupplierId,
    string?   InvoiceNumber,
    DateTime? InvoiceDate,
    DateTime? DueDate,
    string?   PaymentTerms,
    string? Notes,
    IReadOnlyList<PurchaseLineInput>? Lines,
    IReadOnlyList<WarehouseAllocationRequest>? WarehouseAllocations
) : IRequest<Result<PurchBillDto>>;

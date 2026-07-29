using ERP.Application.Common;
using ERP.Application.Items.DTOs;
using MediatR;

namespace ERP.Application.Items.UseCases.CreateItem;

public sealed record CreateItemBarcodeDto(string Code, string BarcodeType, bool IsPrimary);

/// <summary>SupplierId es obligatorio — no existe "código de proveedor" sin proveedor (Fase 2).</summary>
public sealed record CreateItemSupplierCodeDto(Guid SupplierId, string Code, bool IsPrimary);

public sealed record CreateItemCommand(
    // ── Identity ──────────────────────────────────────────────────────────
    string SKU,
    string ShortName,
    string Description,
    Guid ItemTypeId,
    string DefaultUomCode,
    // ── Clasificación (obligatoria en creación) ─────────────────────────────
    Guid? CategoryNodeId,
    Guid? BrandId,
    // ── Identificación ────────────────────────────────────────────────────
    IReadOnlyList<CreateItemBarcodeDto> Barcodes,
    // ── Tax config (código = fuente única de verdad; nulo = no aplica) ──────
    string? SaleVatCode = null,
    string? PurchaseVatCode = null,
    string? ExciseTaxCode = null,
    // ── Optionals ─────────────────────────────────────────────────────────
    string? Observations = null,
    IReadOnlyList<CreateItemSupplierCodeDto>? SupplierCodes = null,
    // ── Precio base (SSOT, Motor de Pricing v2) ─────────────────────────
    decimal? BaseSalePrice = null,
    // ── Sale config ───────────────────────────────────────────────────────
    bool IsForSale = true,
    decimal? MaxDiscountPercent = null,
    bool IsAvailableOnWeb = false,
    bool IsAvailableOnPOS = false,
    bool IsAvailableOnMobile = false,
    bool IsEcommerceActive = false,
    bool IsFavorite = false,
    // ── Stock config ──────────────────────────────────────────────────────
    bool TracksStock = true,
    bool TracksLot = false,
    bool TracksSeries = false,
    bool AllowDecimalQty = false,
    bool AllowDecimalSale = false,
    decimal? MinStockQty = null,
    decimal? MaxStockQty = null
) : IRequest<Result<ItemDto>>, ICompanyScopedRequest;

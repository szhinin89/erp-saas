using MediatR;
using ERP.Application.Common;
using ERP.Application.Items.DTOs;
using ERP.Domain.Modules.Items.Enums;

namespace ERP.Application.Items.UseCases.CreateItem;

[RequireFeature(SubscriptionFeatureCodes.Inventory)]
public sealed record CreateItemCommand(
    // ── Identity ──────────────────────────────────────────────────────────
    string   SKU,
    string   ShortName,
    string   Description,
    ItemType ItemType,
    string   DefaultUomCode,

    // ── Tax config ────────────────────────────────────────────────────────
    bool     AppliesVatOnSale,
    string?  SaleVatCode,
    Guid?    VatAccountId,
    bool     AppliesVatOnPurchase,
    string?  PurchaseVatCode,
    Guid?    PurchaseVatAccountId,

    // ── Optionals ─────────────────────────────────────────────────────────
    string?  PurchaseCode          = null,
    string?  Observations          = null,
    Guid?    CategoryNodeId        = null,
    Guid?    BrandId               = null,
    bool     AppliesExciseTax      = false,
    string?  ExciseTaxCode         = null,
    Guid?    ExciseAccountId       = null,
    string?  SriServiceCode        = null,

    // ── Sale config ───────────────────────────────────────────────────────
    bool     IsForSale             = true,
    decimal? MaxDiscountPercent    = null,
    bool     IsAvailableOnWeb      = false,
    bool     IsAvailableOnPOS      = false,
    bool     IsAvailableOnMobile   = false,
    bool     IsEcommerceActive     = false,

    // ── Stock config ──────────────────────────────────────────────────────
    bool     TracksStock           = true,
    bool     TracksLot             = false,
    bool     TracksSeries          = false,
    bool     AllowDecimalQty       = false,
    bool     AllowDecimalSale      = false,
    decimal? MinStockQty           = null,
    decimal? MaxStockQty           = null
) : IRequest<Result<ItemDto>>, ICompanyScopedRequest;

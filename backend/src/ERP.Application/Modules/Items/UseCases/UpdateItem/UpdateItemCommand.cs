using MediatR;
using ERP.Application.Common;
using ERP.Application.Items.DTOs;

namespace ERP.Application.Items.UseCases.UpdateItem;

public sealed record UpdateItemCommand(
    Guid     Id,
    string   ShortName,
    string   Description,
    string   DefaultUomCode,

    bool     AppliesVatOnSale,
    string?  SaleVatCode,
    Guid?    VatAccountId,
    bool     AppliesVatOnPurchase,
    string?  PurchaseVatCode,
    Guid?    PurchaseVatAccountId,

    string?  PurchaseCode        = null,
    string?  Observations        = null,
    Guid?    CategoryNodeId      = null,
    Guid?    BrandId             = null,
    bool     AppliesExciseTax    = false,
    string?  ExciseTaxCode       = null,
    Guid?    ExciseAccountId     = null,
    string?  SriServiceCode      = null,

    bool     IsForSale           = true,
    decimal? MaxDiscountPercent  = null,
    bool     IsAvailableOnWeb    = false,
    bool     IsAvailableOnPOS    = false,
    bool     IsAvailableOnMobile = false,
    bool     IsEcommerceActive   = false,

    bool     TracksStock         = true,
    bool     TracksLot           = false,
    bool     TracksSeries        = false,
    bool     AllowDecimalQty     = false,
    bool     AllowDecimalSale    = false,
    decimal? MinStockQty         = null,
    decimal? MaxStockQty         = null
) : IRequest<Result<ItemDto>>, ICompanyScopedRequest;

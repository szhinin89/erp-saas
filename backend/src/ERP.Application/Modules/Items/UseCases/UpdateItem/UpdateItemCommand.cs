using ERP.Application.Common;
using ERP.Application.Items.DTOs;
using MediatR;

namespace ERP.Application.Items.UseCases.UpdateItem;

public sealed record UpdateItemCommand(
    Guid Id,
    string SKU,
    string ShortName,
    string Description,
    string DefaultUomCode,
    string? SaleVatCode = null,
    string? PurchaseVatCode = null,
    string? Observations = null,
    Guid? CategoryNodeId = null,
    Guid? BrandId = null,
    decimal? BaseSalePrice = null,
    string? ExciseTaxCode = null,
    bool IsForSale = true,
    decimal? MaxDiscountPercent = null,
    bool IsAvailableOnWeb = false,
    bool IsAvailableOnPOS = false,
    bool IsAvailableOnMobile = false,
    bool IsEcommerceActive = false,
    // null = "no viene en el payload" → se preserva el valor existente del agregado
    // (no hay UI hoy que lo setee; ver UpdateItemCommandHandler).
    bool? IsFavorite = null,
    bool TracksStock = true,
    bool TracksLot = false,
    bool TracksSeries = false,
    bool AllowDecimalQty = false,
    bool AllowDecimalSale = false,
    decimal? MinStockQty = null,
    decimal? MaxStockQty = null
) : IRequest<Result<ItemDto>>, ICompanyScopedRequest;

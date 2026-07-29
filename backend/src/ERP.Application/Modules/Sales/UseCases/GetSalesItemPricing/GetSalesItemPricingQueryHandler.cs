using ERP.Application.Common;
using ERP.Application.Common.Services;
using ERP.Application.Modules.Pricing.Services;
using ERP.Application.Modules.Sales.DTOs;
using ERP.Domain.Modules.Items.Interfaces;
using MediatR;

namespace ERP.Application.Modules.Sales.UseCases.GetSalesItemPricing;

public sealed class GetSalesItemPricingQueryHandler
    : IRequestHandler<GetSalesItemPricingQuery, Result<SalesItemPricingDto>>
{
    private readonly IItemRepository _itemRepo;
    private readonly IPricingResolver _pricingResolver;
    private readonly ISriTaxResolver _taxResolver;
    private readonly ICurrentTenant _tenant;

    public GetSalesItemPricingQueryHandler(
        IItemRepository itemRepo, IPricingResolver pricingResolver,
        ISriTaxResolver taxResolver, ICurrentTenant tenant)
    {
        _itemRepo = itemRepo;
        _pricingResolver = pricingResolver;
        _taxResolver = taxResolver;
        _tenant = tenant;
    }

    public async Task<Result<SalesItemPricingDto>> Handle(
        GetSalesItemPricingQuery request, CancellationToken ct)
    {
        var item = await _itemRepo.GetByIdLightAsync(request.ItemId, _tenant.TenantId, ct);
        if (item is null)
            return Result<SalesItemPricingDto>.NotFound("Ítem no encontrado.");

        if (!item.IsActive || !item.SaleConfig.IsForSale)
            return Result<SalesItemPricingDto>.ValidationFailure(
                $"El producto '{item.Code.Description}' está inactivo o no está habilitado para venta.");

        var pricingResult = await _pricingResolver.ResolveAsync(request.ItemId, ct: ct);
        if (!pricingResult.IsSuccess)
            return Result<SalesItemPricingDto>.ValidationFailure(pricingResult.Error!);

        var vatCode = item.TaxConfig.SaleVatCode;
        string? vatName = null;
        if (!string.IsNullOrWhiteSpace(vatCode))
        {
            var vatResult = await _taxResolver.GetVatRateWithNameAsync(vatCode, ct);
            vatName = vatResult?.Name;
        }

        var iceCode = item.TaxConfig.ExciseTaxCode;
        string? iceName = null;
        if (!string.IsNullOrWhiteSpace(iceCode))
        {
            var iceResult = await _taxResolver.GetIceRateWithNameAsync(iceCode, ct);
            iceName = iceResult?.Name;
        }

        return Result<SalesItemPricingDto>.Success(new SalesItemPricingDto(
            item.Id, pricingResult.Value!.UnitPrice,
            vatCode, vatName, iceCode, iceName,
            item.SaleConfig.MaxDiscountPercent,
            pricingResult.Value!.PriceListCode));
    }
}

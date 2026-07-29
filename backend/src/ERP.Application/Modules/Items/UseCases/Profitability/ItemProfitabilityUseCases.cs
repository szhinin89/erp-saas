using ERP.Application.Common;
using ERP.Application.Modules.Companies.UseCases.DecimalConfig;
using ERP.Application.Modules.Pricing.Services;
using ERP.Domain.Modules.Inventory.Interfaces;
using ERP.Domain.Modules.Items.Interfaces;
using MediatR;

namespace ERP.Application.Modules.Items.UseCases.Profitability;

// ── DTOs ────────────────────────────────────────────────────────────────

public sealed record ItemProfitabilityDto(
    Guid ItemId,
    string Sku,
    string ItemName,
    decimal TotalStockQuantity,
    decimal TotalStockValue,
    decimal AverageCost,
    decimal CurrentSalePrice,
    string? PriceListName,
    string? CurrencyCode,
    decimal MarginAmount,
    decimal MarginPercent,
    string MarginStatus);

public sealed record PriceSimulationDto(
    Guid ItemId,
    decimal AverageCost,
    decimal CurrentSalePrice,
    string? CurrencyCode,
    decimal CurrentMarginAmount,
    decimal CurrentMarginPercent,
    decimal SimulatedPrice,
    decimal SimulatedMarginAmount,
    decimal SimulatedMarginPercent,
    decimal MarginDifference,
    string SimulatedMarginStatus);

// ── Queries ─────────────────────────────────────────────────────────────

public sealed record GetItemProfitabilityQuery(Guid ItemId)
    : IRequest<Result<ItemProfitabilityDto>>, ICompanyScopedRequest;

public sealed record SimulateItemPricingQuery(Guid ItemId, decimal NewPvp)
    : IRequest<Result<PriceSimulationDto>>, ICompanyScopedRequest;

// ── Handlers ────────────────────────────────────────────────────────────

public sealed class GetItemProfitabilityHandler
    : IRequestHandler<GetItemProfitabilityQuery, Result<ItemProfitabilityDto>>
{
    private readonly IItemRepository _itemRepo;
    private readonly IStockRepository _stockRepo;
    private readonly IPricingResolver _pricingResolver;
    private readonly ICurrentTenant _t;
    private readonly ICurrentCompany _c;
    private readonly IDecimalConfigRepository _decimalConfigRepo;

    public GetItemProfitabilityHandler(
        IItemRepository itemRepo, IStockRepository stockRepo,
        IPricingResolver pricingResolver,
        ICurrentTenant t, ICurrentCompany c, IDecimalConfigRepository decimalConfigRepo)
    {
        _itemRepo = itemRepo; _stockRepo = stockRepo;
        _pricingResolver = pricingResolver; _t = t;
        _c = c; _decimalConfigRepo = decimalConfigRepo;
    }

    public async Task<Result<ItemProfitabilityDto>> Handle(GetItemProfitabilityQuery q, CancellationToken ct)
    {
        var tid = _t.TenantId;
        var item = await _itemRepo.GetByIdLightAsync(q.ItemId, tid, ct);
        if (item is null) return Result<ItemProfitabilityDto>.NotFound("Producto no encontrado.");

        var decimalConfig = await _decimalConfigRepo.GetAsync(tid, _c.CompanyId, ct);
        var (totalQty, totalVal) = await _stockRepo.GetAggregatedStockAsync(tid, q.ItemId, ct);
        var avgCost = totalQty > 0
            ? Math.Round(totalVal / totalQty, decimalConfig.PurchaseUnitPrice, MidpointRounding.AwayFromZero)
            : 0m;

        var (salePrice, listName, currencyCode) = await ResolveDefaultPriceAsync(q.ItemId, ct);
        var (marginAmt, marginPct, status) = CalcMargin(avgCost, salePrice, decimalConfig.TotalAmount, decimalConfig.Percentage);

        return Result<ItemProfitabilityDto>.Success(new(
            q.ItemId, item.Code.SKU, item.Code.Description,
            totalQty, totalVal, avgCost,
            salePrice, listName, currencyCode,
            marginAmt, marginPct, status));
    }

    /// <summary>SSOT vía PricingResolver. Sin lista predeterminada o sin precio base configurado, no hay precio que mostrar.</summary>
    internal async Task<(decimal Price, string? ListName, string? CurrencyCode)> ResolveDefaultPriceAsync(Guid itemId, CancellationToken ct)
    {
        var result = await _pricingResolver.ResolveAsync(itemId, ct: ct);
        return result.IsSuccess
            ? (result.Value!.UnitPrice, result.Value!.PriceListName, result.Value!.CurrencyCode)
            : (0m, null, null);
    }

    internal static (decimal Amount, decimal Percent, string Status) CalcMargin(
        decimal cost, decimal price, int amountDecimals, int percentDecimals)
    {
        if (price <= 0) return (0m, 0m, "SIN_PRECIO");
        var amount = Math.Round(price - cost, amountDecimals, MidpointRounding.AwayFromZero);
        var percent = Math.Round(amount / price * 100m, percentDecimals, MidpointRounding.AwayFromZero);
        var status = amount < 0 ? "NEGATIVO" : amount == 0 ? "CERO" : percent < 10 ? "BAJO" : "SALUDABLE";
        return (amount, percent, status);
    }
}

public sealed class SimulateItemPricingHandler
    : IRequestHandler<SimulateItemPricingQuery, Result<PriceSimulationDto>>
{
    private readonly IItemRepository _itemRepo;
    private readonly IStockRepository _stockRepo;
    private readonly IPricingResolver _pricingResolver;
    private readonly ICurrentTenant _t;
    private readonly ICurrentCompany _c;
    private readonly IDecimalConfigRepository _decimalConfigRepo;

    public SimulateItemPricingHandler(
        IItemRepository itemRepo, IStockRepository stockRepo,
        IPricingResolver pricingResolver,
        ICurrentTenant t, ICurrentCompany c, IDecimalConfigRepository decimalConfigRepo)
    {
        _itemRepo = itemRepo; _stockRepo = stockRepo;
        _pricingResolver = pricingResolver; _t = t;
        _c = c; _decimalConfigRepo = decimalConfigRepo;
    }

    public async Task<Result<PriceSimulationDto>> Handle(SimulateItemPricingQuery q, CancellationToken ct)
    {
        if (q.NewPvp < 0)
            return Result<PriceSimulationDto>.ValidationFailure("El precio simulado no puede ser negativo.");

        var tid = _t.TenantId;
        var item = await _itemRepo.GetByIdLightAsync(q.ItemId, tid, ct);
        if (item is null) return Result<PriceSimulationDto>.NotFound("Producto no encontrado.");

        var decimalConfig = await _decimalConfigRepo.GetAsync(tid, _c.CompanyId, ct);
        var (totalQty, totalVal) = await _stockRepo.GetAggregatedStockAsync(tid, q.ItemId, ct);
        var avgCost = totalQty > 0
            ? Math.Round(totalVal / totalQty, decimalConfig.PurchaseUnitPrice, MidpointRounding.AwayFromZero)
            : 0m;

        var pricingResult = await _pricingResolver.ResolveAsync(q.ItemId, ct: ct);
        var currentPrice = pricingResult.IsSuccess ? pricingResult.Value!.UnitPrice : 0m;
        var currencyCode = pricingResult.IsSuccess ? pricingResult.Value!.CurrencyCode : null;

        var (curAmt, curPct, _) = GetItemProfitabilityHandler.CalcMargin(
            avgCost, currentPrice, decimalConfig.TotalAmount, decimalConfig.Percentage);
        var (simAmt, simPct, simStatus) = GetItemProfitabilityHandler.CalcMargin(
            avgCost, q.NewPvp, decimalConfig.TotalAmount, decimalConfig.Percentage);

        return Result<PriceSimulationDto>.Success(new(
            q.ItemId, avgCost,
            currentPrice, currencyCode, curAmt, curPct,
            q.NewPvp, simAmt, simPct,
            Math.Round(simAmt - curAmt, decimalConfig.TotalAmount, MidpointRounding.AwayFromZero),
            simStatus));
    }
}

using ERP.Application.Common;
using ERP.Application.Common.Services;
using ERP.Application.Modules.Pricing.Services;
using ERP.Application.Modules.Sales.DTOs;
using ERP.Domain.Modules.Items.Interfaces;
using ERP.Domain.Modules.Purchases;
using MediatR;

namespace ERP.Application.Modules.Sales.UseCases.GetSalesItemPricing;

public sealed class GetSalesItemPricingQueryHandler
    : IRequestHandler<GetSalesItemPricingQuery, Result<SalesItemPricingDto>>
{
    private readonly IItemRepository _itemRepo;
    private readonly IPricingResolver _pricingResolver;
    private readonly ISriTaxResolver _taxResolver;
    private readonly ERP.Domain.Modules.Company.Interfaces.ICompanySpecialTaxResponsibilityRepository _companyTaxRepo;
    private readonly ICurrentTenant _tenant;
    private readonly ICurrentCompany _company;

    public GetSalesItemPricingQueryHandler(
        IItemRepository itemRepo,
        IPricingResolver pricingResolver,
        ISriTaxResolver taxResolver,
        ERP.Domain.Modules.Company.Interfaces.ICompanySpecialTaxResponsibilityRepository companyTaxRepo,
        ICurrentTenant tenant,
        ICurrentCompany company
    )
    {
        _itemRepo = itemRepo;
        _pricingResolver = pricingResolver;
        _taxResolver = taxResolver;
        _companyTaxRepo = companyTaxRepo;
        _tenant = tenant;
        _company = company;
    }

    public async Task<Result<SalesItemPricingDto>> Handle(
        GetSalesItemPricingQuery request,
        CancellationToken ct
    )
    {
        // TAX-LINE-SSOT-ICE-IRBPNR-01 (ADR-032 §3.2/Fase 3) — GetByIdAsync (no Light): se necesita
        // SpecialTaxConfigurations para resolver ICE, ya no disponible vía TaxConfig.ExciseTaxCode
        // (legacy compatibility mirror, ya no se lee para decisiones nuevas).
        var item = await _itemRepo.GetByIdAsync(request.ItemId, _tenant.TenantId, ct);
        if (item is null)
            return Result<SalesItemPricingDto>.NotFound("Ítem no encontrado.");

        if (!item.IsActive || !item.SaleConfig.IsForSale)
            return Result<SalesItemPricingDto>.ValidationFailure(
                $"El producto '{item.Code.Description}' está inactivo o no está habilitado para venta."
            );

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

        // TAX-LINE-SSOT-ICE-IRBPNR-01 (ADR-032 §3.4/§5.1) — ICE se calcula SOLO si el ítem tiene
        // ItemSpecialTaxConfiguration activa Y la empresa está marcada responsable de aplicarlo en
        // ventas — nunca desde TaxConfig.ExciseTaxCode.
        var iceConfig = item.SpecialTaxConfigurations.FirstOrDefault(c =>
            c.IsActive && c.SriTaxCategoryCode == SriTaxCategoryCodes.Ice
        );
        var companyResponsibleCodes = await _companyTaxRepo.GetResponsibleSriTaxCategoryCodesAsync(
            _company.CompanyId,
            _tenant.TenantId,
            ct
        );
        var iceCode =
            iceConfig is not null && companyResponsibleCodes.Contains(SriTaxCategoryCodes.Ice)
                ? iceConfig.TaxCatalogCode
                : null;
        string? iceName = null;
        if (!string.IsNullOrWhiteSpace(iceCode))
        {
            var iceResult = await _taxResolver.GetIceRateWithNameAsync(iceCode, ct);
            iceName = iceResult?.Name;
        }

        return Result<SalesItemPricingDto>.Success(
            new SalesItemPricingDto(
                item.Id,
                pricingResult.Value!.UnitPrice,
                vatCode,
                vatName,
                iceCode,
                iceName,
                item.SaleConfig.MaxDiscountPercent,
                pricingResult.Value!.PriceListCode
            )
        );
    }
}

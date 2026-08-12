using ERP.Application.Common;
using ERP.Application.Items.DTOs;
using ERP.Domain.MasterData.Interfaces;
using ERP.Domain.Modules.Items.Interfaces;
using MediatR;

namespace ERP.Application.Items.UseCases.GetItemReport;

/// <summary>
/// Full detail report for a single item.
/// Includes all collections, brand name lookup, and SRI catalog name resolution.
/// </summary>
public sealed record GetItemFullReportQuery(Guid Id)
    : IRequest<Result<ItemFullReportDto>>,
        ICompanyScopedRequest;

public sealed class GetItemFullReportQueryHandler
    : IRequestHandler<GetItemFullReportQuery, Result<ItemFullReportDto>>
{
    private readonly IItemRepository _repository;
    private readonly IItemCatalogRepository _catalog;
    private readonly ICurrentTenant _currentTenant;
    private readonly ISriCatalogResolver _sri;
    private readonly IItemTypeRepository _itemTypeRepo;
    private readonly IBusinessPartnerRepository _businessPartnerRepo;

    public GetItemFullReportQueryHandler(
        IItemRepository repository,
        IItemCatalogRepository catalog,
        ICurrentTenant tenant,
        ISriCatalogResolver sri,
        IItemTypeRepository itemTypeRepo,
        IBusinessPartnerRepository businessPartnerRepo
    )
    {
        _repository = repository;
        _catalog = catalog;
        _currentTenant = tenant;
        _sri = sri;
        _itemTypeRepo = itemTypeRepo;
        _businessPartnerRepo = businessPartnerRepo;
    }

    public async Task<Result<ItemFullReportDto>> Handle(
        GetItemFullReportQuery query,
        CancellationToken cancellationToken
    )
    {
        var item = await _repository.GetByIdAsync(
            query.Id,
            _currentTenant.TenantId,
            cancellationToken
        );
        if (item is null)
            return Result<ItemFullReportDto>.NotFound("Ítem no encontrado.");

        var uomCodes = item
            .UnitConversions.SelectMany(c => new[] { c.FromUomCode, c.ToUomCode })
            .Append(item.DefaultUomCode)
            .Concat(item.PackagingLevels.Select(p => p.UomCode));

        var vatCodes = new[] { item.TaxConfig.SaleVatCode, item.TaxConfig.PurchaseVatCode }
            .Where(c => !string.IsNullOrWhiteSpace(c))
            .Cast<string>();

        var iceCodes = !string.IsNullOrWhiteSpace(item.TaxConfig.ExciseTaxCode)
            ? new[] { item.TaxConfig.ExciseTaxCode }
            : [];

        var brand = item.BrandId.HasValue
            ? await _catalog.GetBrandByIdAsync(item.BrandId.Value, cancellationToken)
            : null;

        var uomMap = await _sri.ResolveUomsAsync(uomCodes, cancellationToken);
        var vatMap = await _sri.ResolveVatRatesAsync(vatCodes, cancellationToken);
        var iceMap = await _sri.ResolveIceRatesAsync(iceCodes, cancellationToken);

        var itemType = await _itemTypeRepo.GetByIdAsync(
            _currentTenant.TenantId,
            item.ItemTypeId,
            cancellationToken
        );
        var itemTypeNames = itemType is null
            ? new Dictionary<Guid, string>()
            : new Dictionary<Guid, string> { [itemType.Id] = itemType.Name };
        var supplierInfo = await _businessPartnerRepo.GetDisplayInfoByIdsAsync(
            item.SupplierCodes.Where(s => s.IsActive).Select(s => s.SupplierId),
            cancellationToken
        );

        var detail = ItemMappingService.ToDetailDto(
            item,
            uomMap,
            vatMap,
            iceMap,
            itemTypeNames,
            supplierInfo
        );

        uomMap.TryGetValue(item.DefaultUomCode, out var uomInfo);

        var report = new ItemFullReportDto(
            item.Id,
            item.Code.SKU,
            item.Code.ShortName,
            item.Code.Description,
            item.Observations,
            item.ItemTypeId,
            detail.ItemTypeName,
            item.CategoryNodeId,
            item.BrandId,
            brand?.Name,
            item.DefaultUomCode,
            uomInfo?.Abbrev ?? item.DefaultUomCode,
            uomInfo?.Name ?? item.DefaultUomCode,
            detail.TaxConfig,
            detail.SaleConfig,
            detail.StockConfig,
            detail.Variants,
            detail.Images,
            detail.UnitConversions,
            detail.Substitutes,
            detail.PackagingLevels,
            item.IsActive,
            item.CreatedAt,
            item.UpdatedAt
        );

        return Result<ItemFullReportDto>.Success(report);
    }
}

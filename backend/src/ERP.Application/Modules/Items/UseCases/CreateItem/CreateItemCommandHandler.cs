using ERP.Application.Common;
using ERP.Application.Common.Persistence;
using ERP.Application.Items.DTOs;
using ERP.Domain.Modules.Items.Entities;
using ERP.Domain.Modules.Items.Interfaces;
using ERP.Domain.Modules.Items.ValueObjects;
using MediatR;

namespace ERP.Application.Items.UseCases.CreateItem;

public sealed class CreateItemCommandHandler : IRequestHandler<CreateItemCommand, Result<ItemDto>>
{
    private readonly IItemRepository _repository;
    private readonly ICategoryNodeRepository _categoryNodeRepo;
    private readonly IItemCatalogRepository _catalogRepo;
    private readonly IItemTypeRepository _itemTypeRepo;
    private readonly ICurrentTenant _currentTenant;
    private readonly ICurrentUser _user;
    private readonly IDatabaseExceptionTranslator _dbEx;
    private readonly ISriCatalogResolver _sri;

    public CreateItemCommandHandler(
        IItemRepository repository,
        ICategoryNodeRepository categoryNodeRepo,
        IItemCatalogRepository catalogRepo,
        IItemTypeRepository itemTypeRepo,
        ICurrentTenant tenant,
        ICurrentUser user,
        IDatabaseExceptionTranslator dbEx,
        ISriCatalogResolver sri
    )
    {
        _repository = repository;
        _categoryNodeRepo = categoryNodeRepo;
        _catalogRepo = catalogRepo;
        _itemTypeRepo = itemTypeRepo;
        _currentTenant = tenant;
        _user = user;
        _dbEx = dbEx;
        _sri = sri;
    }

    public async Task<Result<ItemDto>> Handle(
        CreateItemCommand cmd,
        CancellationToken cancellationToken
    )
    {
        var tenantId = _currentTenant.TenantId;
        var userId = _user.UserId;

        if (
            await _repository.ExistsBySkuAsync(
                cmd.SKU,
                tenantId,
                cancellationToken: cancellationToken
            )
        )
            return Result<ItemDto>.Conflict(
                $"Ya existe un ítem con SKU '{cmd.SKU}'.",
                "SKU_DUPLICATE"
            );

        var itemTypeDef = await _itemTypeRepo.GetByIdAsync(
            tenantId,
            cmd.ItemTypeId,
            cancellationToken
        );
        if (itemTypeDef is null)
            return Result<ItemDto>.ValidationFailure(
                "El tipo de ítem seleccionado no existe. Verifique el catálogo de tipos de ítem."
            );
        if (!itemTypeDef.IsActive)
            return Result<ItemDto>.ValidationFailure(
                $"El tipo de ítem '{itemTypeDef.Name}' se encuentra deshabilitado."
            );

        foreach (var barcode in cmd.Barcodes)
        {
            if (
                !await _catalogRepo.BarcodeTypeExistsAndActiveAsync(
                    barcode.BarcodeType,
                    cancellationToken
                )
            )
                return Result<ItemDto>.ValidationFailure(
                    $"El tipo de código de barras '{barcode.BarcodeType}' no es válido."
                );
        }

        if (cmd.CategoryNodeId.HasValue)
        {
            var catValidation = await ValidateCategoryNodeAsync(
                cmd.CategoryNodeId.Value,
                cancellationToken
            );
            if (catValidation is not null)
                return catValidation;
        }

        if (cmd.BrandId.HasValue)
        {
            var brandValidation = await ValidateBrandAsync(cmd.BrandId.Value, cancellationToken);
            if (brandValidation is not null)
                return brandValidation;
        }

        foreach (var barcode in cmd.Barcodes)
        {
            if (
                await _repository.BarcodeExistsAsync(
                    barcode.Code.Trim(),
                    tenantId,
                    cancellationToken
                )
            )
                return Result<ItemDto>.Conflict(
                    $"El código de barras '{barcode.Code}' ya está asignado a otro ítem.",
                    "BARCODE_DUPLICATE"
                );
        }

        foreach (var supplierCode in cmd.SupplierCodes ?? [])
        {
            if (
                await _repository.SupplierCodeExistsAsync(
                    supplierCode.SupplierId,
                    supplierCode.Code.Trim(),
                    tenantId,
                    cancellationToken
                )
            )
                return Result<ItemDto>.Conflict(
                    $"El código de proveedor '{supplierCode.Code}' ya está asignado a otro ítem para este proveedor.",
                    "SUPPLIER_CODE_DUPLICATE"
                );
        }

        Item item;
        try
        {
            item = Item.Create(
                tenantId,
                cmd.SKU,
                cmd.ShortName,
                cmd.Description,
                cmd.ItemTypeId,
                cmd.DefaultUomCode,
                ItemTaxConfig.Create(cmd.SaleVatCode, cmd.PurchaseVatCode, cmd.ExciseTaxCode),
                ItemSaleConfig.Create(
                    cmd.IsForSale,
                    cmd.MaxDiscountPercent,
                    cmd.IsAvailableOnWeb,
                    cmd.IsAvailableOnPOS,
                    cmd.IsAvailableOnMobile,
                    cmd.IsEcommerceActive,
                    cmd.IsFavorite
                ),
                ItemStockConfig.Create(
                    cmd.TracksStock,
                    cmd.TracksLot,
                    cmd.TracksSeries,
                    cmd.AllowDecimalQty,
                    cmd.AllowDecimalSale,
                    cmd.MinStockQty,
                    cmd.MaxStockQty
                ),
                userId,
                cmd.CategoryNodeId,
                cmd.BrandId,
                cmd.Observations,
                cmd.BaseSalePrice
            );
        }
        catch (ArgumentException ex)
        {
            return Result<ItemDto>.ValidationFailure(ex.Message);
        }

        var variant = item.AddVariant(
            axisAttributes: Array.Empty<(Guid, string)>(),
            skuOverride: null,
            sortOrder: 0,
            updatedBy: userId
        );

        foreach (var barcode in cmd.Barcodes)
            variant.AddBarcode(
                barcode.Code,
                barcode.BarcodeType,
                tenantId,
                userId,
                barcode.IsPrimary
            );

        foreach (var supplierCode in cmd.SupplierCodes ?? [])
            item.AddSupplierCode(
                supplierCode.Code,
                supplierCode.IsPrimary,
                supplierCode.SupplierId,
                userId
            );

        await _repository.AddAsync(item, cancellationToken);

        // La auditoría la registra ItemAuditHandler al reaccionar a ItemCreatedEvent
        // (levantado por Item.Create) — ya no se escribe manualmente aquí.
        try
        {
            await _repository.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex) when (_dbEx.TryGetUniqueViolation(ex, out var info))
        {
            return Result<ItemDto>.Conflict(
                $"Conflicto de unicidad en '{info.ConstraintName ?? "items"}'.",
                "SKU_DUPLICATE"
            );
        }

        var uomMap = await _sri.ResolveUomsAsync([item.DefaultUomCode], cancellationToken);
        var itemTypeNames = new Dictionary<Guid, string> { [itemTypeDef.Id] = itemTypeDef.Name };
        return Result<ItemDto>.Success(ItemMappingService.ToDto(item, uomMap, itemTypeNames));
    }

    private async Task<Result<ItemDto>?> ValidateCategoryNodeAsync(
        Guid nodeId,
        CancellationToken ct
    )
    {
        var node = await _categoryNodeRepo.GetByIdAsync(nodeId, ct);
        if (node is null)
            return Result<ItemDto>.NotFound("La categoría seleccionada no existe.");
        if (!node.IsActive)
            return Result<ItemDto>.ValidationFailure(
                "La categoría seleccionada se encuentra deshabilitada."
            );
        if (await _categoryNodeRepo.HasActiveChildrenAsync(nodeId, ct))
            return Result<ItemDto>.ValidationFailure(
                "La categoría seleccionada no es un nodo hoja. Seleccione una categoría final sin hijos activos."
            );
        if (await _categoryNodeRepo.AnyAncestorDisabledAsync(nodeId, ct))
            return Result<ItemDto>.ValidationFailure(
                "La categoría seleccionada pertenece a una rama deshabilitada."
            );
        return null;
    }

    private async Task<Result<ItemDto>?> ValidateBrandAsync(Guid brandId, CancellationToken ct)
    {
        var brand = await _catalogRepo.GetBrandByIdAsync(brandId, ct);
        if (brand is null)
            return Result<ItemDto>.NotFound("La marca seleccionada no existe.");
        if (!brand.IsActive)
            return Result<ItemDto>.ValidationFailure(
                "La marca seleccionada se encuentra deshabilitada."
            );
        return null;
    }
}

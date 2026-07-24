using MediatR;
using ERP.Application.Common;
using ERP.Application.Common.Persistence;
using ERP.Application.Items;
using ERP.Application.Items.DTOs;
using ERP.Domain.Modules.Items.Interfaces;
using ERP.Domain.Modules.Items.ValueObjects;

namespace ERP.Application.Items.UseCases.UpdateItem;

public sealed class UpdateItemCommandHandler
    : IRequestHandler<UpdateItemCommand, Result<ItemDto>>
{
    private readonly IItemRepository _repository;
    private readonly ICategoryNodeRepository _categoryNodeRepo;
    private readonly IItemTypeRepository _itemTypeRepo;
    private readonly ICurrentTenant _currentTenant;
    private readonly ICurrentUser _user;
    private readonly ISriCatalogResolver _sri;
    private readonly IDatabaseExceptionTranslator _dbEx;

    public UpdateItemCommandHandler(
        IItemRepository repository,
        ICategoryNodeRepository categoryNodeRepo,
        IItemTypeRepository itemTypeRepo,
        ICurrentTenant tenant,
        ICurrentUser user,
        ISriCatalogResolver sri,
        IDatabaseExceptionTranslator dbEx)
    {
        _repository = repository;
        _categoryNodeRepo = categoryNodeRepo;
        _itemTypeRepo = itemTypeRepo;
        _currentTenant = tenant;
        _user       = user;
        _sri        = sri;
        _dbEx       = dbEx;
    }

    public async Task<Result<ItemDto>> Handle(UpdateItemCommand cmd, CancellationToken cancellationToken)
    {
        var tenantId = _currentTenant.TenantId;
        var userId       = _user.UserId;

        var item = await _repository.GetByIdLightAsync(cmd.Id, tenantId, cancellationToken);
        if (item is null)
            return Result<ItemDto>.NotFound("Ítem no encontrado.");

        if (await _repository.ExistsBySkuAsync(cmd.SKU, tenantId, cmd.Id, cancellationToken))
            return Result<ItemDto>.Conflict($"Ya existe un ítem con SKU '{cmd.SKU}'.", "SKU_DUPLICATE");

        if (cmd.CategoryNodeId.HasValue)
        {
            var catValidation = await ValidateCategoryNodeAsync(cmd.CategoryNodeId.Value, cancellationToken);
            if (catValidation is not null) return catValidation;
        }

        try
        {
            item.UpdateSku(cmd.SKU, userId);
            item.UpdateIdentity(cmd.ShortName, cmd.Description, cmd.Observations, userId);
            item.UpdateClassification(cmd.CategoryNodeId, cmd.BrandId, userId);
            item.UpdateDefaultUom(cmd.DefaultUomCode, userId);

            item.UpdateTaxConfig(ItemTaxConfig.Create(
                cmd.SaleVatCode, cmd.PurchaseVatCode, cmd.ExciseTaxCode), userId);

            // IsFavorite: sin UI hoy — cmd.IsFavorite llega null y se preserva el valor
            // existente. Merge incremental, nunca reconstrucción destructiva del VO.
            item.UpdateSaleConfig(ItemSaleConfig.Create(
                cmd.IsForSale, cmd.MaxDiscountPercent,
                cmd.IsAvailableOnWeb, cmd.IsAvailableOnPOS, cmd.IsAvailableOnMobile,
                cmd.IsEcommerceActive, cmd.IsFavorite ?? item.SaleConfig.IsFavorite), userId);

            item.UpdateStockConfig(ItemStockConfig.Create(
                cmd.TracksStock, cmd.TracksLot, cmd.TracksSeries,
                cmd.AllowDecimalQty, cmd.AllowDecimalSale,
                cmd.MinStockQty, cmd.MaxStockQty), userId);

            // El validator exige NotNull, pero el guard queda como defensa adicional:
            // un BaseSalePrice ausente jamás debe destruir el precio ya persistido.
            if (cmd.BaseSalePrice.HasValue)
                item.UpdateBaseSalePrice(cmd.BaseSalePrice, userId);
        }
        catch (ArgumentException ex)
        {
            return Result<ItemDto>.ValidationFailure(ex.Message);
        }

        try
        {
            await _repository.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex) when (_dbEx.TryGetUniqueViolation(ex, out var info))
        {
            return Result<ItemDto>.Conflict(
                $"Conflicto de unicidad en '{info.ConstraintName ?? "items"}'.",
                "SKU_DUPLICATE");
        }

        var uomMap = await _sri.ResolveUomsAsync([item.DefaultUomCode], cancellationToken);
        var itemType = await _itemTypeRepo.GetByIdAsync(tenantId, item.ItemTypeId, cancellationToken);
        var itemTypeNames = itemType is null
            ? new Dictionary<Guid, string>()
            : new Dictionary<Guid, string> { [itemType.Id] = itemType.Name };
        return Result<ItemDto>.Success(ItemMappingService.ToDto(item, uomMap, itemTypeNames));
    }

    private async Task<Result<ItemDto>?> ValidateCategoryNodeAsync(Guid nodeId, CancellationToken ct)
    {
        var node = await _categoryNodeRepo.GetByIdAsync(nodeId, ct);
        if (node is null)
            return Result<ItemDto>.NotFound("La categoría seleccionada no existe.");
        if (!node.IsActive)
            return Result<ItemDto>.ValidationFailure("La categoría seleccionada se encuentra deshabilitada.");
        if (await _categoryNodeRepo.HasActiveChildrenAsync(nodeId, ct))
            return Result<ItemDto>.ValidationFailure("La categoría seleccionada no es un nodo hoja. Seleccione una categoría final sin hijos activos.");
        if (await _categoryNodeRepo.AnyAncestorDisabledAsync(nodeId, ct))
            return Result<ItemDto>.ValidationFailure("La categoría seleccionada pertenece a una rama deshabilitada.");
        return null;
    }
}

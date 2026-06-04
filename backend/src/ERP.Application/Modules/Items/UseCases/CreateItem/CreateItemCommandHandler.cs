using MediatR;
using ERP.Application.Common;
using ERP.Application.Items.DTOs;
using ERP.Application.Items;
using ERP.Domain.Audit.Entities;
using ERP.Domain.Audit.Interfaces;
using ERP.Domain.Modules.Items.Entities;
using ERP.Domain.Modules.Items.Interfaces;
using ERP.Application.Common.Persistence;
using ERP.Domain.Modules.Items.ValueObjects;

namespace ERP.Application.Items.UseCases.CreateItem;

public sealed class CreateItemCommandHandler
    : IRequestHandler<CreateItemCommand, Result<ItemDto>>
{
    private readonly IItemRepository _repository;
    private readonly IUserActivityRepository _activity;
    private readonly ICurrentSubscriber _subscriber;
    private readonly ICurrentUser _user;
    private readonly IDatabaseExceptionTranslator _dbEx;

    public CreateItemCommandHandler(
        IItemRepository repository,
        IUserActivityRepository activity,
        ICurrentSubscriber subscriber,
        ICurrentUser user,
        IDatabaseExceptionTranslator dbEx)
    {
        _repository = repository;
        _activity   = activity;
        _subscriber = subscriber;
        _user       = user;
        _dbEx       = dbEx;
    }

    public async Task<Result<ItemDto>> Handle(CreateItemCommand cmd, CancellationToken ct)
    {
        var subscriberId = _subscriber.SubscriberId;
        var userId       = _user.UserId;

        if (await _repository.ExistsBySkuAsync(cmd.SKU, subscriberId, ct))
            return Result<ItemDto>.Conflict($"Ya existe un ítem con SKU '{cmd.SKU}'.", "SKU_DUPLICATE");

        Item item;
        try
        {
            item = Item.Create(
                subscriberId,
                cmd.SKU,
                cmd.ShortName,
                cmd.Description,
                cmd.ItemType,
                cmd.DefaultUomCode,
                ItemTaxConfig.Create(
                    cmd.AppliesVatOnSale, cmd.SaleVatCode, cmd.VatAccountId,
                    cmd.AppliesVatOnPurchase, cmd.PurchaseVatCode, cmd.PurchaseVatAccountId,
                    cmd.AppliesExciseTax, cmd.ExciseTaxCode, cmd.ExciseAccountId, cmd.SriServiceCode),
                ItemSaleConfig.Create(
                    cmd.IsForSale, cmd.MaxDiscountPercent,
                    cmd.IsAvailableOnWeb, cmd.IsAvailableOnPOS, cmd.IsAvailableOnMobile,
                    cmd.IsEcommerceActive),
                ItemStockConfig.Create(
                    cmd.TracksStock, cmd.TracksLot, cmd.TracksSeries,
                    cmd.AllowDecimalQty, cmd.AllowDecimalSale,
                    cmd.MinStockQty, cmd.MaxStockQty),
                userId,
                cmd.CategoryNodeId,
                cmd.BrandId,
                cmd.Observations);
        }
        catch (ArgumentException ex)
        {
            return Result<ItemDto>.ValidationFailure(ex.Message);
        }

        if (!string.IsNullOrWhiteSpace(cmd.PurchaseCode))
            item.UpdatePurchaseCode(cmd.PurchaseCode, userId);

        await _repository.AddAsync(item, ct);

        await _activity.AddAsync(UserActivity.Create(
            subscriberId, userId,
            _user.Email, _user.FullName,
            module: "items",
            action: "item.create",
            entityType: "Item",
            entityId: item.Id,
            description: $"{item.Code.SKU} – {item.Code.ShortName}"), ct);

        try
        {
            await _repository.SaveChangesAsync(ct);
        }
        catch (Exception ex) when (_dbEx.TryGetUniqueViolation(ex, out var info))
        {
            return Result<ItemDto>.Conflict(
                $"Conflicto de unicidad en '{info.ConstraintName ?? "items"}'.",
                "SKU_DUPLICATE");
        }

        return Result<ItemDto>.Success(ItemMappingService.ToDto(item));
    }
}

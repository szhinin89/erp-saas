using MediatR;
using ERP.Application.Common;
using ERP.Application.Items.DTOs;
using ERP.Application.Items;
using ERP.Domain.Modules.Items.Interfaces;
using ERP.Domain.Modules.Items.ValueObjects;

namespace ERP.Application.Items.UseCases.UpdateItem;

public sealed class UpdateItemCommandHandler
    : IRequestHandler<UpdateItemCommand, Result<ItemDto>>
{
    private readonly IItemRepository _repository;
    private readonly ICurrentSubscriber _subscriber;
    private readonly ICurrentUser _user;

    public UpdateItemCommandHandler(
        IItemRepository repository,
        ICurrentSubscriber subscriber,
        ICurrentUser user)
    {
        _repository = repository;
        _subscriber = subscriber;
        _user       = user;
    }

    public async Task<Result<ItemDto>> Handle(UpdateItemCommand cmd, CancellationToken ct)
    {
        var subscriberId = _subscriber.SubscriberId;
        var userId       = _user.UserId;

        var item = await _repository.GetByIdLightAsync(cmd.Id, subscriberId, ct);
        if (item is null)
            return Result<ItemDto>.NotFound("Ítem no encontrado.");

        try
        {
            item.UpdateIdentity(cmd.ShortName, cmd.Description, cmd.Observations, userId);
            item.UpdatePurchaseCode(cmd.PurchaseCode, userId);
            item.UpdateClassification(cmd.CategoryNodeId, cmd.BrandId, userId);
            item.UpdateDefaultUom(cmd.DefaultUomCode, userId);

            item.UpdateTaxConfig(ItemTaxConfig.Create(
                cmd.AppliesVatOnSale, cmd.SaleVatCode, cmd.VatAccountId,
                cmd.AppliesVatOnPurchase, cmd.PurchaseVatCode, cmd.PurchaseVatAccountId,
                cmd.AppliesExciseTax, cmd.ExciseTaxCode, cmd.ExciseAccountId, cmd.SriServiceCode), userId);

            item.UpdateSaleConfig(ItemSaleConfig.Create(
                cmd.IsForSale, cmd.MaxDiscountPercent,
                cmd.IsAvailableOnWeb, cmd.IsAvailableOnPOS, cmd.IsAvailableOnMobile,
                cmd.IsEcommerceActive), userId);

            item.UpdateStockConfig(ItemStockConfig.Create(
                cmd.TracksStock, cmd.TracksLot, cmd.TracksSeries,
                cmd.AllowDecimalQty, cmd.AllowDecimalSale,
                cmd.MinStockQty, cmd.MaxStockQty), userId);
        }
        catch (ArgumentException ex)
        {
            return Result<ItemDto>.ValidationFailure(ex.Message);
        }

        await _repository.SaveChangesAsync(ct);
        return Result<ItemDto>.Success(ItemMappingService.ToDto(item));
    }
}

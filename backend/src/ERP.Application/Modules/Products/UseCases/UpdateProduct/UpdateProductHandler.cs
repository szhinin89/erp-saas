using MediatR;
using ERP.Application.Common;
using ERP.Application.Products.DTOs;
using ERP.Domain.Products.Interfaces;

namespace ERP.Application.Products.UseCases.UpdateProduct;

public class UpdateProductHandler : IRequestHandler<UpdateProductCommand, Result<ProductDto>>
{
    private readonly IProductRepository _repository;
    private readonly ICurrentSubscriber _currentSubscriber;
    private readonly ICurrentUser _currentUser;

    public UpdateProductHandler(
        IProductRepository repository,
        ICurrentSubscriber currentSubscriber,
        ICurrentUser currentUser)
    {
        _repository    = repository;
        _currentSubscriber = currentSubscriber;
        _currentUser   = currentUser;
    }

    public async Task<Result<ProductDto>> Handle(UpdateProductCommand command, CancellationToken ct)
    {
        var subscriberId = _currentSubscriber.SubscriberId;
        var userId   = _currentUser.UserId;

        var product = await _repository.GetByIdAsync(command.Id, subscriberId, ct);
        if (product is null)
            return Result<ProductDto>.Failure("Producto no encontrado.");

        ApplyScalarUpdates(product, command, userId);
        var collectionError = ProductCommandMutationHelper.ApplyUpdateChildCollections(product, command, userId);
        if (collectionError is not null)
            return collectionError;

        await _repository.UpdateAsync(product, ct);
        await _repository.SaveChangesAsync(ct);

        return Result<ProductDto>.Success(ProductCommandMutationHelper.MapToDto(product));
    }

    private static void ApplyScalarUpdates(
        ERP.Domain.Products.Entities.Product product,
        UpdateProductCommand command,
        Guid userId)
    {
        product.Update(
            command.ShortName,
            command.Description,
            command.Observations,
            command.LineId,
            command.CategoryId,
            command.SubcategoryId,
            command.UomCode,
            command.BrandId,
            command.ProductTypeId,
            command.AppliesVatOnSale,
            command.SaleVatCode,
            command.SaleVatAccountId,
            command.AppliesVatOnPurchase,
            command.PurchaseVatCode,
            command.PurchaseVatAccountId,
            command.AppliesExciseTax,
            command.IceCode,
            command.ExciseAccountId,
            command.TracksStock,
            command.IsService,
            command.TracksLot,
            command.TracksSeries,
            command.HasRecipe,
            command.RecipeId,
            command.StockWithDecimal,
            command.SaleWithDecimal,
            command.MaxItemDiscountPercent,
            userId);

        product.UpdateChannels(command.AvailableOnWeb, command.AvailableOnMobile, command.IsEcommerceActive, command.IsForSale, userId);
    }
}

using MediatR;
using ERP.Application.Common;
using ERP.Application.Products.DTOs;
using ERP.Domain.Audit.Entities;
using ERP.Domain.Audit.Interfaces;
using ERP.Domain.Products.Interfaces;

namespace ERP.Application.Products.UseCases.CreateProduct;

public sealed class CreateProductCommandHandler : IRequestHandler<CreateProductCommand, Result<ProductDto>>
{
    private readonly IProductRepository _repository;
    private readonly IUserActivityRepository _activity;
    private readonly ICurrentSubscriber _currentSubscriber;
    private readonly ICurrentCompany _currentCompany;
    private readonly ICurrentUser _currentUser;

    public CreateProductCommandHandler(
        IProductRepository repository,
        IUserActivityRepository activity,
        ICurrentSubscriber currentSubscriber,
        ICurrentCompany currentCompany,
        ICurrentUser currentUser)
    {
        _repository    = repository;
        _activity      = activity;
        _currentSubscriber = currentSubscriber;
        _currentCompany = currentCompany;
        _currentUser   = currentUser;
    }

    public async Task<Result<ProductDto>> Handle(
        CreateProductCommand command,
        CancellationToken ct)
    {
        var subscriberId = _currentSubscriber.SubscriberId;
        var userId   = _currentUser.UserId;

        var product = BuildProduct(command, subscriberId, userId);
        var collectionError = ProductCommandMutationHelper.ApplyCreateChildCollections(product, command, userId);
        if (collectionError is not null)
            return collectionError;

        await _repository.AddAsync(product, ct);
        await _activity.AddAsync(UserActivity.Create(
            subscriberId,
            userId,
            _currentUser.Email,
            _currentUser.FullName,
            module: "inventario",
            action: "product.create",
            entityType: "Product",
            entityId: product.Id,
            description: $"{product.SaleCode} — {product.ShortName}"), ct);
        await _repository.SaveChangesAsync(ct);

        return Result<ProductDto>.Success(ProductCommandMutationHelper.MapToDto(product));
    }

    private ERP.Domain.Products.Entities.Product BuildProduct(CreateProductCommand command, Guid subscriberId, Guid userId) =>
        ERP.Domain.Products.Entities.Product.Create(
            subscriberId,
            command.SaleCode,
            command.ShortName,
            command.Description,
            command.LineId,
            command.CategoryId,
            command.SubcategoryId,
            command.UomCode,
            command.BrandId,
            command.ProductTypeId,
            command.TariffId,
            command.AppliesVatOnSale,
            command.SaleVatCode,
            command.SaleVatAccountId,
            command.AppliesVatOnPurchase,
            command.PurchaseVatCode,
            command.PurchaseVatAccountId,
            userId,
            command.PurchaseCode,
            command.AppliesExciseTax,
            command.IceCode,
            command.ExciseAccountId,
            command.IsService,
            command.TracksStock,
            command.TracksLot,
            command.TracksSeries,
            command.HasRecipe,
            command.RecipeId,
            command.StockWithDecimal,
            command.SaleWithDecimal,
            command.MaxItemDiscountPercent,
            command.AvailableOnWeb,
            command.AvailableOnMobile,
            command.IsEcommerceActive,
            command.BaseColor,
            command.HasMultipleColors,
            command.HasSizes,
            command.HandlesTariff,
            command.IsForSale,
            companyId: _currentCompany.HasCompanyContext ? _currentCompany.CompanyId : null);
}

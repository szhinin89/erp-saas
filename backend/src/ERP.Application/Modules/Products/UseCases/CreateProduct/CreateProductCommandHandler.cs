using MediatR;
using ERP.Application.Common;
using ERP.Application.Products.DTOs;
using ERP.Domain.Audit.Entities;
using ERP.Domain.Audit.Interfaces;
using ERP.Domain.Products.Entities;
using ERP.Domain.Products.Interfaces;

namespace ERP.Application.Products.UseCases.CreateProduct;

public sealed class CreateProductCommandHandler : IRequestHandler<CreateProductCommand, Result<ProductDto>>
{
    private readonly IProductRepository _repository;
    private readonly ITaxRateRepository _taxRates;
    private readonly IUserActivityRepository _activity;
    private readonly ICurrentSubscriber _currentSubscriber;
    private readonly ICurrentCompany _currentCompany;
    private readonly ICurrentUser _currentUser;

    public CreateProductCommandHandler(
        IProductRepository repository,
        ITaxRateRepository taxRates,
        IUserActivityRepository activity,
        ICurrentSubscriber currentSubscriber,
        ICurrentCompany currentCompany,
        ICurrentUser currentUser)
    {
        _repository    = repository;
        _taxRates      = taxRates;
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

        var taxError = await ProductCommandMutationHelper.ValidateTaxRatesAsync(
            _taxRates, subscriberId,
            command.AppliesVatOnSale, command.SaleTaxId,
            command.AppliesVatOnPurchase, command.PurchaseTaxId,
            command.AppliesExciseTax, command.ExciseTaxId, ct);
        if (taxError is not null)
            return taxError;

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

    private Product BuildProduct(CreateProductCommand command, Guid subscriberId, Guid userId) =>
        Product.Create(
            subscriberId,
            command.SaleCode,
            command.ShortName,
            command.Description,
            command.LineId,
            command.CategoryId,
            command.SubcategoryId,
            command.UnitOfMeasureId,
            command.BrandId,
            command.ProductTypeId,
            command.TariffId,
            command.AppliesVatOnSale,
            command.SaleTaxId,
            command.SaleVatAccountId,
            command.AppliesVatOnPurchase,
            command.PurchaseTaxId,
            command.PurchaseVatAccountId,
            userId,
            command.PurchaseCode,
            command.AppliesExciseTax,
            command.ExciseTaxId,
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

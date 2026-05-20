using ERP.Application.Common;
using ERP.Application.Products.DTOs;
using ERP.Domain.Audit.Entities;
using ERP.Domain.Audit.Interfaces;
using ERP.Domain.Products.Interfaces;

using MediatR;

namespace ERP.Application.Products.UseCases.EnableProduct;

public class EnableProductHandler : IRequestHandler<EnableProductCommand, Result<ProductDto>>
{
    private readonly IProductRepository _repository;
    private readonly IUserActivityRepository _activity;
    private readonly ICurrentSubscriber _currentSubscriber;
    private readonly ICurrentUser _currentUser;

    public EnableProductHandler(
        IProductRepository repository,
        IUserActivityRepository activity,
        ICurrentSubscriber currentSubscriber,
        ICurrentUser currentUser)
    {
        _repository    = repository;
        _activity      = activity;
        _currentSubscriber = currentSubscriber;
        _currentUser   = currentUser;
    }

    public async Task<Result<ProductDto>> Handle(EnableProductCommand command, CancellationToken ct)
    {
        var subscriberId = _currentSubscriber.SubscriberId;
        var userId   = _currentUser.UserId;

        var product = await _repository.GetByIdAsync(command.Id, subscriberId, ct);
        if (product is null)
            return Result<ProductDto>.Failure("Producto no encontrado.");

        if (product.IsActive)
            return Result<ProductDto>.Failure("El producto ya está activo.");

        product.Enable(userId);

        await _activity.AddAsync(UserActivity.Create(
            subscriberId,
            userId,
            _currentUser.Email,
            _currentUser.FullName,
            module: "inventario",
            action: "product.enable",
            entityType: "Product",
            entityId: product.Id,
            description: $"{product.SaleCode} — {product.ShortName}"), ct);

        await _repository.SaveChangesAsync(ct);

        return Result<ProductDto>.Success(new ProductDto(
            product.Id,
            product.SaleCode,
            product.PurchaseCode,
            product.ShortName,
            product.Description,
            product.LineId,
            product.CategoryId,
            product.SubcategoryId,
            product.UnitOfMeasureId,
            product.BrandId,
            product.ProductTypeId,
            product.TariffId,
            product.AppliesVatOnSale,
            product.SaleTaxId,
            product.AppliesVatOnPurchase,
            product.PurchaseTaxId,
            product.AppliesExciseTax,
            product.ExciseTaxId,
            product.IsService,
            product.TracksStock,
            product.IsActive,
            product.AvailableOnWeb,
            product.AvailableOnMobile,
            product.IsEcommerceActive,
            product.IsForSale,
            product.CreatedAt));
    }
}

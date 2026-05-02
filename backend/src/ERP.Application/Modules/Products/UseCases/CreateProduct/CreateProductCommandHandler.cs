using MediatR;
using ERP.Application.Common;
using ERP.Application.Products.DTOs;
using ERP.Domain.Audit.Entities;
using ERP.Domain.Audit.Interfaces;
using ERP.Domain.Products.Entities;
using ERP.Domain.Products.Enums;
using ERP.Domain.Products.Interfaces;

namespace ERP.Application.Products.UseCases.CreateProduct;

public sealed class CreateProductCommandHandler : IRequestHandler<CreateProductCommand, Result<ProductDto>>
{
    private readonly IProductRepository _repository;
    private readonly ITaxRateRepository _taxRates;
    private readonly IUserActivityRepository _activity;
    private readonly ICurrentTenant _currentTenant;
    private readonly ICurrentUser _currentUser;

    public CreateProductCommandHandler(
        IProductRepository repository,
        ITaxRateRepository taxRates,
        IUserActivityRepository activity,
        ICurrentTenant currentTenant,
        ICurrentUser currentUser)
    {
        _repository    = repository;
        _taxRates      = taxRates;
        _activity      = activity;
        _currentTenant = currentTenant;
        _currentUser   = currentUser;
    }

    public async Task<Result<ProductDto>> Handle(
        CreateProductCommand command,
        CancellationToken ct)
    {
        var tenantId = _currentTenant.TenantId;
        var userId   = _currentUser.UserId;

        if (command.AppliesVatOnSale && command.SaleTaxId is not null)
        {
            var tax = await _taxRates.GetByIdAsync(command.SaleTaxId.Value, tenantId, ct);
            if (tax is null || !tax.IsActive || tax.Type != TaxRateType.VAT)
                return Result<ProductDto>.Failure("La tarifa de IVA (venta) no es válida o no está vigente.");
        }

        if (command.AppliesVatOnPurchase && command.PurchaseTaxId is not null)
        {
            var tax = await _taxRates.GetByIdAsync(command.PurchaseTaxId.Value, tenantId, ct);
            if (tax is null || !tax.IsActive || tax.Type != TaxRateType.VAT)
                return Result<ProductDto>.Failure("La tarifa de IVA (compra) no es válida o no está vigente.");
        }

        if (command.AppliesExciseTax && command.ExciseTaxId is not null)
        {
            var tax = await _taxRates.GetByIdAsync(command.ExciseTaxId.Value, tenantId, ct);
            if (tax is null || !tax.IsActive || tax.Type != TaxRateType.Excise)
                return Result<ProductDto>.Failure("La tarifa de ICE no es válida o no está vigente.");
        }

        var product = Product.Create(
            tenantId,
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
            command.IsForSale);

        if (command.Barcodes is { Count: > 0 })
        {
            foreach (var b in command.Barcodes)
            {
                if (!Enum.IsDefined(typeof(BarcodeType), b.Type))
                    return Result<ProductDto>.Failure($"Tipo de código de barras inválido: {b.Type}");
                product.AddBarcode(b.Code, (BarcodeType)b.Type, userId);
            }
        }

        if (command.SupplierCodes is { Count: > 0 })
            product.ReplaceSupplierCodes(
                command.SupplierCodes.Select(s => (s.SupplierId, s.Code, s.IsDefault)),
                userId);

        if (command.UnitConversions is { Count: > 0 })
            product.ReplaceUnitConversions(
                command.UnitConversions.Select(u => (u.AlternateUnitId, u.ConversionFactor)),
                userId);

        if (command.Colors is { Count: > 0 })
            product.ReplaceColors(
                command.Colors.Select(c => (c.Name, c.HexCode)),
                userId);

        if (command.Sizes is { Count: > 0 })
            product.ReplaceSizes(
                command.Sizes.Select(s => (s.Name, s.SortOrder)),
                userId);

        if (command.Dimensions is { Count: > 0 })
            product.ReplaceDimensions(
                command.Dimensions.Select(d => (d.Name, d.Value, d.Unit)),
                userId);

        if (command.Images is { Count: > 0 })
            product.ReplaceImages(
                command.Images.Select(i => (i.Url, i.AltText, i.IsMain, i.IsEcommerce, i.SortOrder)),
                userId);

        if (command.Features is { Count: > 0 })
            product.ReplaceFeatures(
                command.Features.Select(f => (f.Name, f.Value)),
                userId);

        if (command.TariffDetails is { Count: > 0 })
            product.ReplaceTariffDetails(
                command.TariffDetails.Select(t => (t.OriginCountry, t.TariffCode, t.Percentage)),
                userId);

        if (command.Substitutes is { Count: > 0 })
            product.ReplaceSubstitutes(
                command.Substitutes.Select(s => (s.SubstituteProductId, s.Note)),
                userId);

        if (command.CustomFields is { Count: > 0 })
        {
            foreach (var c in command.CustomFields)
            {
                if (!Enum.IsDefined(typeof(CustomFieldType), c.FieldType))
                    return Result<ProductDto>.Failure($"Tipo de campo personalizado inválido: {c.FieldType}");
            }
            product.ReplaceCustomFields(
                command.CustomFields.Select(c => (c.FieldName, (CustomFieldType)c.FieldType, c.FieldValue)),
                userId);
        }

        await _repository.AddAsync(product, ct);
        await _activity.AddAsync(UserActivity.Create(
            tenantId,
            userId,
            _currentUser.Email,
            _currentUser.FullName,
            module: "catalog",
            action: "product.create",
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

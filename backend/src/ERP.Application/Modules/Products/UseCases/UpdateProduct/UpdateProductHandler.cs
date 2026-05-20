using MediatR;
using ERP.Application.Common;
using ERP.Application.Products.DTOs;
using ERP.Domain.Products.Entities;
using ERP.Domain.Products.Enums;
using ERP.Domain.Products.Interfaces;

namespace ERP.Application.Products.UseCases.UpdateProduct;

public class UpdateProductHandler : IRequestHandler<UpdateProductCommand, Result<ProductDto>>
{
    private readonly IProductRepository _repository;
    private readonly ITaxRateRepository _taxRates;
    private readonly ICurrentSubscriber _currentSubscriber;
    private readonly ICurrentUser _currentUser;

    public UpdateProductHandler(
        IProductRepository repository,
        ITaxRateRepository taxRates,
        ICurrentSubscriber currentSubscriber,
        ICurrentUser currentUser)
    {
        _repository    = repository;
        _taxRates      = taxRates;
        _currentSubscriber = currentSubscriber;
        _currentUser   = currentUser;
    }

    public async Task<Result<ProductDto>> Handle(UpdateProductCommand command, CancellationToken ct)
    {
        var subscriberId = _currentSubscriber.SubscriberId;
        var userId   = _currentUser.UserId;

        if (command.AppliesVatOnSale && command.SaleTaxId is not null)
        {
            var tax = await _taxRates.GetByIdAsync(command.SaleTaxId.Value, subscriberId, ct);
            if (tax is null || !tax.IsActive || tax.Type != TaxRateType.VAT)
                return Result<ProductDto>.Failure("La tarifa de IVA (venta) no es válida o no está vigente.");
        }

        if (command.AppliesVatOnPurchase && command.PurchaseTaxId is not null)
        {
            var tax = await _taxRates.GetByIdAsync(command.PurchaseTaxId.Value, subscriberId, ct);
            if (tax is null || !tax.IsActive || tax.Type != TaxRateType.VAT)
                return Result<ProductDto>.Failure("La tarifa de IVA (compra) no es válida o no está vigente.");
        }

        if (command.AppliesExciseTax && command.ExciseTaxId is not null)
        {
            var tax = await _taxRates.GetByIdAsync(command.ExciseTaxId.Value, subscriberId, ct);
            if (tax is null || !tax.IsActive || tax.Type != TaxRateType.Excise)
                return Result<ProductDto>.Failure("La tarifa de ICE no es válida o no está vigente.");
        }

        var product = await _repository.GetByIdAsync(command.Id, subscriberId, ct);
        if (product is null)
            return Result<ProductDto>.Failure("Producto no encontrado.");

        product.Update(
            command.ShortName,
            command.Description,
            command.Observations,
            command.LineId,
            command.CategoryId,
            command.SubcategoryId,
            command.UnitOfMeasureId,
            command.BrandId,
            command.ProductTypeId,
            command.AppliesVatOnSale,
            command.SaleTaxId,
            command.SaleVatAccountId,
            command.AppliesVatOnPurchase,
            command.PurchaseTaxId,
            command.PurchaseVatAccountId,
            command.AppliesExciseTax,
            command.ExciseTaxId,
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
            command.BaseColor,
            command.HasMultipleColors,
            command.HasSizes,
            command.HandlesTariff,
            userId);

        product.UpdateChannels(command.AvailableOnWeb, command.AvailableOnMobile, command.IsEcommerceActive, command.IsForSale, userId);

        if (product.TariffId != command.TariffId)
            product.UpdateTariff(command.TariffId, userId);

        if (command.Barcodes is not null)
        {
            foreach (var b in command.Barcodes)
            {
                if (!Enum.IsDefined(typeof(BarcodeType), b.Type))
                    return Result<ProductDto>.Failure($"Tipo de código de barras inválido: {b.Type}");
            }

            // Reemplazo simple: elimina/añade desde el agregado
            foreach (var existing in product.Barcodes.ToList())
                product.RemoveBarcode(existing.Id, userId);
            foreach (var b in command.Barcodes)
                product.AddBarcode(b.Code, (BarcodeType)b.Type, userId);
        }

        if (command.SupplierCodes is not null)
            product.ReplaceSupplierCodes(
                command.SupplierCodes.Select(s => (s.SupplierId, s.Code, s.IsDefault)),
                userId);

        if (command.UnitConversions is not null)
            product.ReplaceUnitConversions(
                command.UnitConversions.Select(u => (u.AlternateUnitId, u.ConversionFactor)),
                userId);

        if (command.Colors is not null)
            product.ReplaceColors(
                command.Colors.Select(c => (c.Name, c.HexCode)),
                userId);

        if (command.Sizes is not null)
            product.ReplaceSizes(
                command.Sizes.Select(s => (s.Name, s.SortOrder)),
                userId);

        if (command.Dimensions is not null)
            product.ReplaceDimensions(
                command.Dimensions.Select(d => (d.Name, d.Value, d.Unit)),
                userId);

        if (command.Images is not null)
            product.ReplaceImages(
                command.Images.Select(i => (i.Url, i.AltText, i.IsMain, i.IsEcommerce, i.SortOrder)),
                userId);

        if (command.Features is not null)
            product.ReplaceFeatures(
                command.Features.Select(f => (f.Name, f.Value)),
                userId);

        if (command.TariffDetails is not null)
            product.ReplaceTariffDetails(
                command.TariffDetails.Select(t => (t.OriginCountry, t.TariffCode, t.Percentage)),
                userId);

        if (command.Substitutes is not null)
            product.ReplaceSubstitutes(
                command.Substitutes.Select(s => (s.SubstituteProductId, s.Note)),
                userId);

        if (command.CustomFields is not null)
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

        await _repository.UpdateAsync(product, ct);
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


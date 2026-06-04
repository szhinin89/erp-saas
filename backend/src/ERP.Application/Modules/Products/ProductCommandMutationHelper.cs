using ERP.Application.Common;
using ERP.Application.Products.DTOs;
using ERP.Application.Products.UseCases.CreateProduct;
using ERP.Application.Products.UseCases.UpdateProduct;
using ERP.Domain.Products.Entities;

namespace ERP.Application.Products;

internal static class ProductCommandMutationHelper
{
    public static Result<ProductDto>? ApplyCreateChildCollections(Product product, CreateProductCommand command, Guid userId)
    {
        if (command.Barcodes is { Count: > 0 })
        {
            foreach (var b in command.Barcodes)
            {
                if (!Enum.IsDefined(typeof(BarcodeType), b.Type))
                    return Result<ProductDto>.Failure($"Tipo de código de barras inválido: {b.Type}");
                product.AddBarcode(b.Code, (BarcodeType)b.Type, userId);
            }
        }

        if (command.UnitConversions is { Count: > 0 })
            product.ReplaceUnitConversions(
                command.UnitConversions.Select(u => (u.AlternateUomCode, u.ConversionFactor)),
                userId);

        if (command.Images is { Count: > 0 })
            product.ReplaceImages(
                command.Images.Select(i => (i.Url, i.AltText, i.IsMain, i.IsEcommerce, i.SortOrder)),
                userId);

        if (command.Substitutes is { Count: > 0 })
            product.ReplaceSubstitutes(
                command.Substitutes.Select(s => (s.SubstituteProductId, s.Note)),
                userId);

        return null;
    }

    public static Result<ProductDto>? ApplyUpdateChildCollections(Product product, UpdateProductCommand command, Guid userId)
    {
        if (command.Barcodes is not null)
        {
            foreach (var b in command.Barcodes)
            {
                if (!Enum.IsDefined(typeof(BarcodeType), b.Type))
                    return Result<ProductDto>.Failure($"Tipo de código de barras inválido: {b.Type}");
            }

            foreach (var existing in product.Barcodes.ToList())
                product.RemoveBarcode(existing.Id, userId);
            foreach (var b in command.Barcodes)
                product.AddBarcode(b.Code, (BarcodeType)b.Type, userId);
        }

        if (command.UnitConversions is not null)
            product.ReplaceUnitConversions(
                command.UnitConversions.Select(u => (u.AlternateUomCode, u.ConversionFactor)),
                userId);

        if (command.Images is not null)
            product.ReplaceImages(
                command.Images.Select(i => (i.Url, i.AltText, i.IsMain, i.IsEcommerce, i.SortOrder)),
                userId);

        if (command.Substitutes is not null)
            product.ReplaceSubstitutes(
                command.Substitutes.Select(s => (s.SubstituteProductId, s.Note)),
                userId);

        return null;
    }

    public static ProductDto MapToDto(Product product) => new(
        product.Id,
        product.SaleCode,
        product.PurchaseCode,
        product.ShortName,
        product.Description,
        product.LineId,
        product.CategoryId,
        product.SubcategoryId,
        product.UomCode,
        product.BrandId,
        product.ProductTypeId,
        product.TariffId,
        product.AppliesVatOnSale,
        product.SaleVatCode,
        product.AppliesVatOnPurchase,
        product.PurchaseVatCode,
        product.AppliesExciseTax,
        product.IceCode,
        product.IsService,
        product.TracksStock,
        product.IsActive,
        product.AvailableOnWeb,
        product.AvailableOnMobile,
        product.IsEcommerceActive,
        product.IsForSale,
        product.CreatedAt);
}

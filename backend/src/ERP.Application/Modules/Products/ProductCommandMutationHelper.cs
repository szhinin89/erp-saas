using ERP.Application.Common;
using ERP.Application.Products.DTOs;
using ERP.Application.Products.UseCases.CreateProduct;
using ERP.Application.Products.UseCases.UpdateProduct;
using ERP.Domain.Products.Entities;
using ERP.Domain.Products.Enums;

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

        if (command.SupplierCodes is { Count: > 0 })
            product.ReplaceSupplierCodes(
                command.SupplierCodes.Select(s => (s.BusinessPartnerId, s.Code, s.IsDefault)),
                userId);

        if (command.UnitConversions is { Count: > 0 })
            product.ReplaceUnitConversions(
                command.UnitConversions.Select(u => (u.AlternateUomCode, u.ConversionFactor)),
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

        if (command.SupplierCodes is not null)
            product.ReplaceSupplierCodes(
                command.SupplierCodes.Select(s => (s.BusinessPartnerId, s.Code, s.IsDefault)),
                userId);

        if (command.UnitConversions is not null)
            product.ReplaceUnitConversions(
                command.UnitConversions.Select(u => (u.AlternateUomCode, u.ConversionFactor)),
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

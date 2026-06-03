using ERP.Domain.Products.Entities;

namespace ERP.Application.Products.DTOs;

/// <summary>Porcentaje derivado de global.sri_vat_rate o global.sri_ice_rate.</summary>
public record TaxRateDto(Guid Id, string Code, string Name, TaxRateType Type, string? SriVatCode, string? SriIceCode, bool IsActive);
public record BrandDto(Guid Id, string Code, string Name, bool IsActive, string? Manufacturer, string? CountryOfOrigin);
public record ProductTypeDto(Guid Id, string Code, string Name, bool IsActive);
public record UnitOfMeasureDto(Guid Id, string Code, string Name, string? Symbol, bool IsActive);
public record TariffDto(Guid Id, string Code, string Description, bool IsActive);
public record ProductLineDto(Guid Id, string Code, string Name, bool IsActive);
public record ProductCategoryDto(Guid Id, string Code, string Name, Guid LineId, bool IsActive);
public record ProductSubcategoryDto(Guid Id, string Code, string Name, Guid CategoryId, bool IsActive);

public record ProductCategoryListItemDto(
    Guid Id, string Code, string Name, Guid LineId, string LineCode, string LineName, bool IsActive);

public record ProductSubcategoryListItemDto(
    Guid Id,
    string Code,
    string Name,
    Guid CategoryId,
    Guid LineId,
    string LineCode,
    string LineName,
    string CategoryCode,
    string CategoryName,
    bool IsActive);


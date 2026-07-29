namespace ERP.Application.Items.DTOs;

/// <summary>
/// Full item report: all scalar fields, all collections, and catalog label names.
/// Used for administrative reports, export to PDF/Excel, and integrations.
/// </summary>
public record ItemFullReportDto(
    // ── Identity ──────────────────────────────────────────────────────────
    Guid Id,
    string SKU,
    string ShortName,
    string Description,
    string? Observations,
    Guid ItemTypeId,
    string ItemTypeName,
    // ── Classification (with names for display) ────────────────────────
    Guid? CategoryNodeId,
    Guid? BrandId,
    string? BrandName,
    string DefaultUomCode,
    string DefaultUomAbbrev,
    string DefaultUomName,
    // ── Config sections ────────────────────────────────────────────────
    ItemTaxConfigDto TaxConfig,
    ItemSaleConfigDto SaleConfig,
    ItemStockConfigDto StockConfig,
    // ── Collections ────────────────────────────────────────────────────
    IReadOnlyList<ItemVariantDto> Variants,
    IReadOnlyList<ItemImageDto> Images,
    IReadOnlyList<ItemUnitConversionDto> UnitConversions,
    IReadOnlyList<ItemSubstituteDto> Substitutes,
    IReadOnlyList<ItemPackagingLevelDto> PackagingLevels,
    // ── Audit ──────────────────────────────────────────────────────────
    bool IsActive,
    DateTime CreatedAt,
    DateTime? UpdatedAt
);

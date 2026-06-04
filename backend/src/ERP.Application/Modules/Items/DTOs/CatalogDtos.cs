namespace ERP.Application.Items.DTOs;

// ── Catalog hierarchy ─────────────────────────────────────────────────────

public record ItemFamilyDto(
    Guid     Id,
    string   Code,
    string   Name,
    bool     IsActive,
    DateTime CreatedAt,
    DateTime? UpdatedAt
);

public record ItemCategoryDto(
    Guid     Id,
    Guid     FamilyId,
    string   Code,
    string   Name,
    bool     IsActive,
    DateTime CreatedAt,
    DateTime? UpdatedAt
);

public record ItemSubcategoryDto(
    Guid     Id,
    Guid     CategoryId,
    string   Code,
    string   Name,
    bool     IsActive,
    DateTime CreatedAt,
    DateTime? UpdatedAt
);

public record BrandDto(
    Guid     Id,
    string   Code,
    string   Name,
    string?  Manufacturer,
    string?  CountryOfOrigin,
    bool     IsActive,
    DateTime CreatedAt,
    DateTime? UpdatedAt
);

// ── Attribute system ──────────────────────────────────────────────────────

public record AttributeGroupDto(
    Guid   Id,
    string Code,
    string Name,
    int    SortOrder,
    bool   IsActive
);

public record AttributeDefinitionDto(
    Guid    Id,
    Guid    GroupId,
    string  Code,
    string  Name,
    string  DataType,
    bool    IsVariantAxis,
    string? AllowedValues,
    bool    IsRequired,
    int     SortOrder,
    bool    IsActive
);

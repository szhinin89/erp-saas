namespace ERP.Application.Items.DTOs;

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

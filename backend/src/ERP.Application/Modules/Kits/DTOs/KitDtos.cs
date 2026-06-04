namespace ERP.Application.Kits.DTOs;

public record KitDto(
    Guid                       Id,
    Guid                       ItemId,
    string                     KitType,
    bool                       IsActive,
    IReadOnlyList<KitLineDto>  Lines
);

public record KitLineDto(
    Guid    Id,
    Guid    ComponentItemId,
    Guid?   ComponentVariantId,
    decimal Quantity,
    string  UomCode,
    bool    IsOptional,
    int     SortOrder,
    bool    IsActive
);

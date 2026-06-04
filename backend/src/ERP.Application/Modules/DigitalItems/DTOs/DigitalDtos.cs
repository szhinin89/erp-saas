namespace ERP.Application.DigitalItems.DTOs;

public record DigitalItemDto(
    Guid                              Id,
    Guid                              ItemId,
    string                            LicenseType,
    string                            DeliveryMethod,
    int?                              MaxDownloads,
    int?                              ValidityDays,
    bool                              IsActive,
    IReadOnlyList<DeliverableDto>     Deliverables
);

public record DeliverableDto(
    Guid    Id,
    string  Name,
    Guid    StorageObjectId,
    string? Version,
    bool    IsActive
);

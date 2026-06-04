namespace ERP.Application.Items.DTOs;

public record LotDto(
    Guid      Id,
    string    LotNumber,
    Guid      ItemId,
    Guid?     VariantId,
    Guid      WarehouseId,
    DateOnly? ManufactureDate,
    DateOnly? ExpirationDate,
    decimal   InitialQty,
    decimal   CurrentQty,
    string    Status,
    string?   Notes,
    DateTime  CreatedAt
);

public record SerialDto(
    Guid      Id,
    string    Serial,
    Guid      ItemId,
    Guid?     VariantId,
    Guid      WarehouseId,
    Guid?     LotId,
    string    Status,
    DateTime  AcquiredAt,
    DateTime? SoldAt,
    string?   DocumentRef
);

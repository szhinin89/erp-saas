namespace ERP.API.Contracts.Inventory;

public sealed record RecalcularSnapshotsBody(
    Guid? ProductId = null,
    Guid? WarehouseId = null,
    DateTime? Until = null);

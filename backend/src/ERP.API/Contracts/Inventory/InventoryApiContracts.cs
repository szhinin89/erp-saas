namespace ERP.API.Contracts.Inventory;

public sealed record RecalculateSnapshotsBody(
    Guid? ProductId = null,
    Guid? WarehouseId = null,
    DateTime? Until = null);

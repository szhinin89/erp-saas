namespace ERP.Application.Common.Inventory;

public sealed record InventoryPostingLine(Guid ProductId, decimal Quantity);

/// <summary>Única puerta de entrada para movimientos de stock desde ventas/notas.</summary>
public sealed record InventoryPostingRequest(
    Guid TenantId,
    Guid CompanyId,
    Guid WarehouseId,
    IReadOnlyList<InventoryPostingLine> Lines,
    string Reference,
    Guid SourceDocId,
    string SourceDocType,
    Guid UserId
);

public interface IInventoryPostingService
{
    Task<Result<bool>> PostSaleExitAsync(
        InventoryPostingRequest request,
        CancellationToken cancellationToken = default
    );

    Task<Result<bool>> PostSaleReturnAsync(
        InventoryPostingRequest request,
        CancellationToken cancellationToken = default
    );
}

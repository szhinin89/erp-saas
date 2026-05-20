using ERP.Application.Common;
using ERP.Application.Common.Inventory;
using ERP.Domain.Modules.Inventory.Entities;
using ERP.Domain.Modules.Inventory.Enums;
using ERP.Domain.Modules.Inventory.Interfaces;

namespace ERP.Application.Modules.Inventory.Services;

public sealed class InventoryPostingService : IInventoryPostingService
{
    private readonly IStockRepository _stock;

    public InventoryPostingService(IStockRepository stock) => _stock = stock;

    public async Task<Result<bool>> PostSaleExitAsync(InventoryPostingRequest request, CancellationToken ct = default)
    {
        foreach (var line in request.Lines)
        {
            var stock = await _stock.GetStockAsync(
                request.SubscriberId, request.WarehouseId, line.ProductId, ct);

            if (stock is null)
                return Result<bool>.Failure(
                    $"Sin stock registrado para el producto {line.ProductId} en la bodega indicada.");

            if (stock.CompanyId.HasValue && stock.CompanyId != request.CompanyId)
                return Result<bool>.Failure("El stock no pertenece a la empresa operativa activa.");

            if (stock.AvailableQuantity < line.Quantity)
                return Result<bool>.Failure(
                    $"Stock insuficiente. Disponible: {stock.AvailableQuantity}, solicitado: {line.Quantity}.");

            var previous = stock.Quantity;
            var unitCost = stock.AverageCost;
            stock.ApplyMovement(-line.Quantity, request.UserId, unitCost);

            var movement = StockMovement.Create(
                request.SubscriberId,
                line.ProductId,
                request.WarehouseId,
                StockMovementType.SaleExit,
                quantity: -line.Quantity,
                previousQuantity: previous,
                reference: request.Reference,
                sourceDocId: request.SourceDocId,
                sourceDocType: request.SourceDocType,
                createdBy: request.UserId,
                unitCost: unitCost,
                companyId: request.CompanyId);

            await _stock.AddMovementAsync(movement, ct);
        }

        return Result<bool>.Success(true);
    }

    public async Task<Result<bool>> PostSaleReturnAsync(InventoryPostingRequest request, CancellationToken ct = default)
    {
        foreach (var line in request.Lines)
        {
            var stock = await _stock.GetStockAsync(
                request.SubscriberId, request.WarehouseId, line.ProductId, ct);

            if (stock is null)
            {
                stock = CurrentStock.Create(
                    request.SubscriberId,
                    line.ProductId,
                    request.WarehouseId,
                    request.UserId,
                    companyId: request.CompanyId);
                await _stock.AddCurrentStockAsync(stock, ct);
            }

            var previous = stock.Quantity;
            var unitCost = stock.AverageCost;
            stock.ApplyMovement(line.Quantity, request.UserId, unitCost);

            var movement = StockMovement.Create(
                request.SubscriberId,
                line.ProductId,
                request.WarehouseId,
                StockMovementType.SaleReturn,
                quantity: line.Quantity,
                previousQuantity: previous,
                reference: request.Reference,
                sourceDocId: request.SourceDocId,
                sourceDocType: request.SourceDocType,
                createdBy: request.UserId,
                unitCost: unitCost,
                companyId: request.CompanyId);

            await _stock.AddMovementAsync(movement, ct);
        }

        return Result<bool>.Success(true);
    }
}

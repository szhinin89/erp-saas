using ERP.Application.Common;
using ERP.Application.Modules.Inventory.Stock.DTOs;
using ERP.Domain.Access.Interfaces;
using ERP.Domain.Modules.Inventory.Entities;
using ERP.Domain.Modules.Inventory.Interfaces;
using MediatR;

namespace ERP.Application.Modules.Inventory.Stock.UseCases.GetStockMovements;

public sealed class GetStockMovementsQueryHandler
    : IRequestHandler<GetStockMovementsQuery, Result<IReadOnlyList<StockMovementDto>>>
{
    private readonly IStockRepository _repo;
    private readonly IWarehouseRepository _warehouseRepo;
    private readonly ICurrentTenant _tenant;
    private readonly ICurrentBranch _branch;

    public GetStockMovementsQueryHandler(
        IStockRepository repo,
        IWarehouseRepository warehouseRepo,
        ICurrentTenant tenant,
        ICurrentBranch branch
    )
    {
        _repo = repo;
        _warehouseRepo = warehouseRepo;
        _tenant = tenant;
        _branch = branch;
    }

    public async Task<Result<IReadOnlyList<StockMovementDto>>> Handle(
        GetStockMovementsQuery request,
        CancellationToken ct
    )
    {
        var warehouse = await _warehouseRepo.GetByIdAsync(
            _tenant.TenantId,
            request.WarehouseId,
            ct
        );
        if (warehouse is null || warehouse.BranchId != _branch.BranchId)
            return Result<IReadOnlyList<StockMovementDto>>.ValidationFailure(
                "La bodega seleccionada no pertenece a la sucursal activa."
            );

        var movements = await _repo.GetMovementsAsync(
            _tenant.TenantId,
            request.ItemId,
            request.WarehouseId,
            request.From,
            request.To,
            ct
        );

        var dtos = movements.Select(ToDto).ToList();
        return Result<IReadOnlyList<StockMovementDto>>.Success(dtos);
    }

    internal static StockMovementDto ToDto(StockMovement m) =>
        new(
            m.Id,
            m.ProductId,
            m.WarehouseId,
            (int)m.MovementType,
            m.MovementType.ToString(),
            m.Quantity,
            m.UomCode,
            m.PreviousQuantity,
            m.ResultQuantity,
            m.SequenceNumber,
            m.UnitCost,
            m.TotalCost,
            m.RunningAverageCost,
            m.RunningStockValue,
            m.EffectiveDate,
            m.Reference,
            m.SourceDocId,
            m.SourceDocType,
            m.CreatedBy,
            m.CreatedAt
        );

    /// <summary>
    /// Resuelve en un solo lote los nombres de los usuarios autores de una lista de movimientos,
    /// evitando N+1 al componer el reporte de Kardex (por producto o por documento).
    /// </summary>
    internal static async Task<IReadOnlyDictionary<Guid, string>> ResolveActorNamesAsync(
        IAccessRepository accessRepo,
        IEnumerable<StockMovement> movements,
        CancellationToken ct
    )
    {
        var userIds = movements.Select(m => m.CreatedBy).Distinct().ToList();
        var users = await accessRepo.GetUsersByIdsAsync(userIds, ct);
        return users.ToDictionary(u => u.Id, u => $"{u.FirstName} {u.LastName}".Trim());
    }
}

using ERP.Application.Common;
using ERP.Application.Modules.Branches;
using ERP.Application.Modules.Inventory.Stock.DTOs;
using ERP.Domain.Modules.Inventory.Entities;
using ERP.Domain.Modules.Inventory.Interfaces;
using MediatR;

namespace ERP.Application.Modules.Inventory.Stock.UseCases.CreateStockTransfer;

public sealed class CreateStockTransferCommandHandler
    : IRequestHandler<CreateStockTransferCommand, Result<StockTransferDto>>
{
    private readonly IStockTransferRepository _repo;
    private readonly IInterBranchAccessGuard _interBranchGuard;
    private readonly ICurrentTenant _tenant;
    private readonly ICurrentUser _user;

    public CreateStockTransferCommandHandler(
        IStockTransferRepository repo,
        IInterBranchAccessGuard interBranchGuard,
        ICurrentTenant tenant,
        ICurrentUser user
    )
    {
        _repo = repo;
        _interBranchGuard = interBranchGuard;
        _tenant = tenant;
        _user = user;
    }

    public async Task<Result<StockTransferDto>> Handle(
        CreateStockTransferCommand request,
        CancellationToken ct
    )
    {
        if (request.SourceWarehouseId == request.TargetWarehouseId)
            return Result<StockTransferDto>.Failure(
                "Bodega origen y destino no pueden ser la misma."
            );

        if (request.Lines.Count == 0)
            return Result<StockTransferDto>.Failure("Debe incluir al menos una línea.");

        var access = await _interBranchGuard.RequireInterBranchAccessAsync(
            request.SourceWarehouseId,
            request.TargetWarehouseId,
            ct
        );
        if (!access.IsSuccess)
            return Result<StockTransferDto>.Failure(access.Error!);

        var ctx = access.Value!;
        var seq = await _repo.GetNextSequentialAsync(ctx.TenantId, ctx.CompanyId, ct);
        var uid = _user.UserId;

        var transfer = StockTransfer.Create(
            _tenant.TenantId,
            seq,
            ctx.OperationBranchId,
            request.SourceWarehouseId,
            request.TargetWarehouseId,
            request.Reason,
            request.Notes,
            uid,
            ctx.CompanyId
        );

        foreach (var line in request.Lines)
        {
            var tl = StockTransferLine.Create(
                _tenant.TenantId,
                transfer.Id,
                line.ProductId,
                line.Quantity,
                line.Description,
                uid
            );
            transfer.AddLine(tl);
        }

        await _repo.AddAsync(transfer, ct);
        await _repo.SaveChangesAsync(ct);

        return Result<StockTransferDto>.Success(ToDto(transfer));
    }

    internal static StockTransferDto ToDto(StockTransfer t) =>
        new(
            t.Id,
            t.TransferNumber,
            t.SourceWarehouseId,
            t.TargetWarehouseId,
            t.TransferDate,
            t.Status,
            t.Reason,
            t.Notes,
            t.ConfirmedAt,
            t.Lines.Select(l => new StockTransferLineDto(
                    l.Id,
                    l.ProductId,
                    l.Quantity,
                    l.Description
                ))
                .ToList()
        );
}

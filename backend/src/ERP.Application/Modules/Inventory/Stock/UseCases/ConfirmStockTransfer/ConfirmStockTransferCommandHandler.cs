using ERP.Application.Common;
using ERP.Application.Common.Services;
using ERP.Application.Modules.Branches;
using ERP.Application.Modules.Inventory.Stock.DTOs;
using ERP.Domain.Modules.Inventory.Enums;
using ERP.Domain.Modules.Inventory.Interfaces;
using MediatR;

namespace ERP.Application.Modules.Inventory.Stock.UseCases.ConfirmStockTransfer;

public sealed class ConfirmStockTransferCommandHandler
    : IRequestHandler<ConfirmStockTransferCommand, Result<StockTransferDto>>
{
    private readonly IStockTransferRepository _repo;
    private readonly IStockRepository _stockRepo;
    private readonly IInterBranchAccessGuard _interBranchGuard;
    private readonly ICurrentTenant _tenant;
    private readonly ICurrentCompany _company;
    private readonly ICurrentUser _user;
    private readonly ICompanyClock _companyClock;
    private readonly IUnitOfWork _unitOfWork;

    public ConfirmStockTransferCommandHandler(
        IStockTransferRepository repo,
        IStockRepository stockRepo,
        IInterBranchAccessGuard interBranchGuard,
        ICurrentTenant tenant,
        ICurrentCompany company,
        ICurrentUser user,
        ICompanyClock companyClock,
        IUnitOfWork unitOfWork
    )
    {
        _unitOfWork = unitOfWork;
        _repo = repo;
        _stockRepo = stockRepo;
        _interBranchGuard = interBranchGuard;
        _tenant = tenant;
        _company = company;
        _user = user;
        _companyClock = companyClock;
    }

    /// <summary>
    /// INVENTORY-V1-B2 — decisión y efectos en UNA transacción con el documento bloqueado
    /// (FOR UPDATE) antes de validar su estado: una solicitud concurrente espera y ve el estado ya
    /// aplicado (mismo patrón que CancelSalesInvoiceHandler). Orden de locks: documento → Kardex.
    /// </summary>
    public async Task<Result<StockTransferDto>> Handle(
        ConfirmStockTransferCommand request,
        CancellationToken ct
    )
    {
        await _unitOfWork.BeginTransactionAsync(ct);
        try
        {
            var result = await ConfirmAsync(request, ct);
            if (result.IsSuccess)
                await _unitOfWork.CommitAsync(ct);
            else
                await _unitOfWork.RollbackAsync(ct);
            return result;
        }
        catch
        {
            await _unitOfWork.RollbackAsync(ct);
            throw;
        }
    }

    private async Task<Result<StockTransferDto>> ConfirmAsync(
        ConfirmStockTransferCommand request,
        CancellationToken ct
    )
    {
        var tid = _tenant.TenantId;
        var cid = _company.CompanyId;
        var uid = _user.UserId;

        var transfer = await _repo.GetByIdForUpdateAsync(tid, cid, request.Id, ct);
        if (transfer is null)
            return Result<StockTransferDto>.NotFound("Transferencia no encontrada.");

        if (transfer.Status != "Draft")
            return Result<StockTransferDto>.Failure(
                $"Solo transferencias en Draft pueden confirmarse (actual: {transfer.Status})."
            );

        var access = await _interBranchGuard.RequireInterBranchAccessAsync(
            transfer.SourceWarehouseId,
            transfer.TargetWarehouseId,
            ct
        );
        if (!access.IsSuccess)
            return Result<StockTransferDto>.Failure(access.Error!, access.Code);

        var effectiveDate = await _companyClock.TodayAsync(cid, tid, ct);

        foreach (var line in transfer.Lines)
        {
            // TransferExit — decrementar origen. Sin costo explícito: se resuelve con el
            // RunningAverageCost vigente del Kardex de origen (ver IStockRepository.AppendMovementAsync).
            var exitMovement = await _stockRepo.AppendMovementAsync(
                tid,
                transfer.CompanyId,
                line.ProductId,
                transfer.SourceWarehouseId,
                StockMovementType.TransferExit,
                -line.Quantity,
                "UNIT",
                effectiveDate,
                transfer.TransferNumber,
                transfer.Id,
                "StockTransfer",
                uid,
                cancellationToken: ct
            );

            // TransferEntry — incrementar destino, con el costo que traía la mercadería de origen
            // (una salida por promedio ponderado no cambia el costo promedio, por eso
            // exitMovement.RunningAverageCost sigue representando el costo de esas unidades).
            // INVENTORY-V1-B3: el importe es exactamente el de la salida (incluido el residuo que
            // absorbe al vaciar el origen), así el valor se conserva entre bodegas.
            await _stockRepo.AppendMovementAsync(
                tid,
                transfer.CompanyId,
                line.ProductId,
                transfer.TargetWarehouseId,
                StockMovementType.TransferEntry,
                line.Quantity,
                "UNIT",
                effectiveDate,
                transfer.TransferNumber,
                transfer.Id,
                "StockTransfer",
                uid,
                exitMovement.RunningAverageCost,
                cancellationToken: ct,
                totalCost: exitMovement.TotalCost
            );
        }

        transfer.Confirm(uid);
        await _stockRepo.SaveChangesWithSequenceRetryAsync(ct);

        return Result<StockTransferDto>.Success(
            CreateStockTransfer.CreateStockTransferCommandHandler.ToDto(transfer)
        );
    }
}

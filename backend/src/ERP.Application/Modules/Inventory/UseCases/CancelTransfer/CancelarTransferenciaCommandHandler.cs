using MediatR;
using Microsoft.Extensions.Logging;
using ERP.Application.Common;
using ERP.Application.Inventory.DTOs;
using ERP.Domain.Audit.Entities;
using ERP.Domain.Audit.Interfaces;
using ERP.Domain.Modules.Inventory.Interfaces;

namespace ERP.Application.Inventory.UseCases.CancelarTransferencia;

public sealed class CancelTransferCommandHandler
    : IRequestHandler<CancelTransferCommand, Result<TransferDto>>
{
    private readonly IStockTransferRepository _transferenciaRepo;
    private readonly IUserActivityRepository  _activity;
    private readonly ICurrentSubscriber           _currentSubscriber;
    private readonly ICurrentUser             _currentUser;
    private readonly IUnitOfWork              _unitOfWork;
    private readonly ILogger<CancelTransferCommandHandler> _logger;

    public CancelTransferCommandHandler(
        IStockTransferRepository transferenciaRepo,
        IUserActivityRepository activity,
        ICurrentSubscriber currentSubscriber,
        ICurrentUser currentUser,
        IUnitOfWork unitOfWork,
        ILogger<CancelTransferCommandHandler> logger)
    {
        _transferenciaRepo = transferenciaRepo;
        _activity          = activity;
        _currentSubscriber     = currentSubscriber;
        _currentUser       = currentUser;
        _unitOfWork        = unitOfWork;
        _logger            = logger;
    }

    public async Task<Result<TransferDto>> Handle(
        CancelTransferCommand command, CancellationToken ct)
    {
        var subscriberId = _currentSubscriber.SubscriberId;
        var userId   = _currentUser.UserId;

        var transfer = await _transferenciaRepo.GetByIdAsync(subscriberId, command.TransferId, ct);
        if (transfer is null)
            return Result<TransferDto>.Failure("transfer no encontrada.");

        if (transfer.Status != "Draft")
            return Result<TransferDto>.Failure(
                $"Solo se puede cancelar una transfer en Draft (estado actual: {transfer.Status}).");

        transfer.Cancel(userId);

        await _unitOfWork.BeginTransactionAsync(ct);
        try
        {
            await _activity.AddAsync(UserActivity.Create(
                subscriberId, userId, _currentUser.Email, _currentUser.FullName,
                module: "inventario", action: "transfer.cancelar",
                entityType: "transfer", entityId: transfer.Id,
                description: transfer.TransferNumber), ct);

            await _unitOfWork.SaveChangesAsync(ct);
            await _unitOfWork.CommitAsync(ct);

            _logger.LogInformation("transfer cancelada: {Numero}", transfer.TransferNumber);

            return Result<TransferDto>.Success(new TransferDto(
            transfer.Id, transfer.TransferNumber,
            transfer.SourceWarehouseId,
            transfer.SourceWarehouse?.Name ?? transfer.SourceWarehouseId.ToString(),
            transfer.TargetWarehouseId,
            transfer.TargetWarehouse?.Name ?? transfer.TargetWarehouseId.ToString(),
            transfer.TransferDate, transfer.Status,
            transfer.Reason, transfer.Notes,
            transfer.ConfirmedAt, transfer.ConfirmedBy,
            transfer.CreatedAt));
        }
        catch (Exception ex)
        {
            await _unitOfWork.RollbackAsync(ct);
            _logger.LogError(ex, "Error al cancelar transfer {Id}", command.TransferId);
            return Result<TransferDto>.Failure($"No se pudo cancelar la transfer: {ex.Message}");
        }
    }
}

using MediatR;
using Microsoft.Extensions.Logging;
using ERP.Application.Common;
using ERP.Application.Inventory.DTOs;
using ERP.Domain.Audit.Entities;
using ERP.Domain.Audit.Interfaces;
using ERP.Domain.Modules.Inventory.Interfaces;

namespace ERP.Application.Inventory.UseCases.CancelarTransferencia;

public sealed class CancelarTransferenciaCommandHandler
    : IRequestHandler<CancelarTransferenciaCommand, Result<TransferenciaDto>>
{
    private readonly IStockTransferRepository _transferenciaRepo;
    private readonly IUserActivityRepository  _activity;
    private readonly ICurrentTenant           _currentTenant;
    private readonly ICurrentUser             _currentUser;
    private readonly IUnitOfWork              _unitOfWork;
    private readonly ILogger<CancelarTransferenciaCommandHandler> _logger;

    public CancelarTransferenciaCommandHandler(
        IStockTransferRepository transferenciaRepo,
        IUserActivityRepository activity,
        ICurrentTenant currentTenant,
        ICurrentUser currentUser,
        IUnitOfWork unitOfWork,
        ILogger<CancelarTransferenciaCommandHandler> logger)
    {
        _transferenciaRepo = transferenciaRepo;
        _activity          = activity;
        _currentTenant     = currentTenant;
        _currentUser       = currentUser;
        _unitOfWork        = unitOfWork;
        _logger            = logger;
    }

    public async Task<Result<TransferenciaDto>> Handle(
        CancelarTransferenciaCommand command, CancellationToken ct)
    {
        var tenantId = _currentTenant.TenantId;
        var userId   = _currentUser.UserId;

        var transfer = await _transferenciaRepo.GetByIdAsync(tenantId, command.TransferenciaId, ct);
        if (transfer is null)
            return Result<TransferenciaDto>.Failure("transfer no encontrada.");

        if (transfer.Status != "Borrador")
            return Result<TransferenciaDto>.Failure(
                $"Solo se puede cancelar una transfer en Borrador (estado actual: {transfer.Status}).");

        transfer.Cancel(userId);

        await _unitOfWork.BeginTransactionAsync(ct);
        try
        {
            await _activity.AddAsync(UserActivity.Create(
                tenantId, userId, _currentUser.Email, _currentUser.FullName,
                module: "inventario", action: "transfer.cancelar",
                entityType: "transfer", entityId: transfer.Id,
                description: transfer.TransferNumber), ct);

            await _unitOfWork.SaveChangesAsync(ct);
            await _unitOfWork.CommitAsync(ct);

            _logger.LogInformation("transfer cancelada: {Numero}", transfer.TransferNumber);

            return Result<TransferenciaDto>.Success(new TransferenciaDto(
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
            _logger.LogError(ex, "Error al cancelar transfer {Id}", command.TransferenciaId);
            return Result<TransferenciaDto>.Failure($"No se pudo cancelar la transfer: {ex.Message}");
        }
    }
}

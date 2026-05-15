using MediatR;
using Microsoft.Extensions.Logging;
using ERP.Application.Common;
using ERP.Application.Inventory.DTOs;
using ERP.Domain.Audit.Entities;
using ERP.Domain.Audit.Interfaces;
using ERP.Domain.Modules.Inventory.Entities;
using ERP.Domain.Modules.Inventory.Interfaces;

namespace ERP.Application.Inventory.UseCases.CancelarAjuste;

public sealed class CancelStockAdjustmentCommandHandler
    : IRequestHandler<CancelStockAdjustmentCommand, Result<StockAdjustmentDto>>
{
    private readonly IStockAdjustmentRepository _ajusteRepo;
    private readonly IUserActivityRepository     _activity;
    private readonly ICurrentTenant              _currentTenant;
    private readonly ICurrentUser                _currentUser;
    private readonly IUnitOfWork                 _unitOfWork;
    private readonly ILogger<CancelStockAdjustmentCommandHandler> _logger;

    public CancelStockAdjustmentCommandHandler(
        IStockAdjustmentRepository ajusteRepo,
        IUserActivityRepository activity,
        ICurrentTenant currentTenant,
        ICurrentUser currentUser,
        IUnitOfWork unitOfWork,
        ILogger<CancelStockAdjustmentCommandHandler> logger)
    {
        _ajusteRepo    = ajusteRepo;
        _activity      = activity;
        _currentTenant = currentTenant;
        _currentUser   = currentUser;
        _unitOfWork    = unitOfWork;
        _logger        = logger;
    }

    public async Task<Result<StockAdjustmentDto>> Handle(
        CancelStockAdjustmentCommand command, CancellationToken ct)
    {
        var tenantId = _currentTenant.TenantId;
        var userId   = _currentUser.UserId;

        var ajuste = await _ajusteRepo.GetByIdAsync(tenantId, command.AdjustmentId, ct);
        if (ajuste is null)
            return Result<StockAdjustmentDto>.Failure("Ajuste no encontrado.");

        if (ajuste.Status != "Borrador")
            return Result<StockAdjustmentDto>.Failure(
                $"Solo se puede cancelar un ajuste en Borrador (estado actual: {ajuste.Status}).");

        ajuste.Cancel(userId);

        await _unitOfWork.BeginTransactionAsync(ct);
        try
        {
            await _activity.AddAsync(UserActivity.Create(
                tenantId, userId, _currentUser.Email, _currentUser.FullName,
                module: "inventario", action: "ajuste.cancelar",
                entityType: "StockAdjustment", entityId: ajuste.Id,
                description: ajuste.AdjustmentNumber), ct);

            await _unitOfWork.SaveChangesAsync(ct);
            await _unitOfWork.CommitAsync(ct);

            _logger.LogInformation("Ajuste cancelado: {Numero}", ajuste.AdjustmentNumber);

            return Result<StockAdjustmentDto>.Success(new(
            ajuste.Id, ajuste.AdjustmentNumber,
            ajuste.WarehouseId,   ajuste.WarehouseName,
            ajuste.ProductId, ajuste.ProductName,
            ajuste.AdjustmentQty, ajuste.AdjustmentType,
            ajuste.Reason, ajuste.Notes,
            ajuste.AdjustmentDate, ajuste.Status,
            ajuste.ExecutedAt, ajuste.ExecutedBy,
            ajuste.CreatedAt));
        }
        catch (Exception ex)
        {
            await _unitOfWork.RollbackAsync(ct);
            _logger.LogError(ex, "Error al cancelar ajuste {Id}", command.AdjustmentId);
            return Result<StockAdjustmentDto>.Failure($"No se pudo cancelar el ajuste: {ex.Message}");
        }
    }
}

using MediatR;
using Microsoft.Extensions.Logging;
using ERP.Application.Common;
using ERP.Application.Inventario.DTOs;
using ERP.Domain.Audit.Entities;
using ERP.Domain.Audit.Interfaces;
using ERP.Domain.Inventario.Interfaces;

namespace ERP.Application.Inventario.UseCases.CancelarTransferencia;

public sealed class CancelarTransferenciaCommandHandler
    : IRequestHandler<CancelarTransferenciaCommand, Result<TransferenciaDto>>
{
    private readonly ITransferenciaRepository _transferenciaRepo;
    private readonly IUserActivityRepository  _activity;
    private readonly ICurrentTenant           _currentTenant;
    private readonly ICurrentUser             _currentUser;
    private readonly ILogger<CancelarTransferenciaCommandHandler> _logger;

    public CancelarTransferenciaCommandHandler(
        ITransferenciaRepository transferenciaRepo,
        IUserActivityRepository activity,
        ICurrentTenant currentTenant,
        ICurrentUser currentUser,
        ILogger<CancelarTransferenciaCommandHandler> logger)
    {
        _transferenciaRepo = transferenciaRepo;
        _activity          = activity;
        _currentTenant     = currentTenant;
        _currentUser       = currentUser;
        _logger            = logger;
    }

    public async Task<Result<TransferenciaDto>> Handle(
        CancelarTransferenciaCommand command, CancellationToken ct)
    {
        var tenantId = _currentTenant.TenantId;
        var userId   = _currentUser.UserId;

        var transferencia = await _transferenciaRepo.GetByIdAsync(tenantId, command.TransferenciaId, ct);
        if (transferencia is null)
            return Result<TransferenciaDto>.Failure("Transferencia no encontrada.");

        if (transferencia.Estado != "Borrador")
            return Result<TransferenciaDto>.Failure(
                $"Solo se puede cancelar una transferencia en Borrador (estado actual: {transferencia.Estado}).");

        transferencia.Cancelar(userId);

        await _activity.AddAsync(UserActivity.Create(
            tenantId, userId, _currentUser.Email, _currentUser.FullName,
            module: "inventario", action: "transferencia.cancelar",
            entityType: "Transferencia", entityId: transferencia.Id,
            description: transferencia.NumeroTransferencia), ct);

        await _transferenciaRepo.SaveChangesAsync(ct);

        _logger.LogInformation("Transferencia cancelada: {Numero}", transferencia.NumeroTransferencia);

        return Result<TransferenciaDto>.Success(new TransferenciaDto(
            transferencia.Id, transferencia.NumeroTransferencia,
            transferencia.BodegaOrigenId,
            transferencia.BodegaOrigen?.Nombre ?? transferencia.BodegaOrigenId.ToString(),
            transferencia.BodegaDestinoId,
            transferencia.BodegaDestino?.Nombre ?? transferencia.BodegaDestinoId.ToString(),
            transferencia.FechaTransferencia, transferencia.Estado,
            transferencia.Motivo, transferencia.Observaciones,
            transferencia.FechaConfirmacion, transferencia.ConfirmadoPor,
            transferencia.CreatedAt));
    }
}

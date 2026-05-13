using MediatR;
using Microsoft.Extensions.Logging;
using ERP.Application.Common;
using ERP.Application.Inventario.DTOs;
using ERP.Domain.Audit.Entities;
using ERP.Domain.Audit.Interfaces;
using ERP.Domain.Modules.Inventario.Entities;
using ERP.Domain.Modules.Inventario.Interfaces;

namespace ERP.Application.Inventario.UseCases.CancelarAjuste;

public sealed class CancelarAjusteCommandHandler
    : IRequestHandler<CancelarAjusteCommand, Result<AjusteInventarioDto>>
{
    private readonly IAjusteInventarioRepository _ajusteRepo;
    private readonly IUserActivityRepository     _activity;
    private readonly ICurrentTenant              _currentTenant;
    private readonly ICurrentUser                _currentUser;
    private readonly IUnitOfWork                 _unitOfWork;
    private readonly ILogger<CancelarAjusteCommandHandler> _logger;

    public CancelarAjusteCommandHandler(
        IAjusteInventarioRepository ajusteRepo,
        IUserActivityRepository activity,
        ICurrentTenant currentTenant,
        ICurrentUser currentUser,
        IUnitOfWork unitOfWork,
        ILogger<CancelarAjusteCommandHandler> logger)
    {
        _ajusteRepo    = ajusteRepo;
        _activity      = activity;
        _currentTenant = currentTenant;
        _currentUser   = currentUser;
        _unitOfWork    = unitOfWork;
        _logger        = logger;
    }

    public async Task<Result<AjusteInventarioDto>> Handle(
        CancelarAjusteCommand command, CancellationToken ct)
    {
        var tenantId = _currentTenant.TenantId;
        var userId   = _currentUser.UserId;

        var ajuste = await _ajusteRepo.GetByIdAsync(tenantId, command.AjusteId, ct);
        if (ajuste is null)
            return Result<AjusteInventarioDto>.Failure("Ajuste no encontrado.");

        if (ajuste.Estado != "Borrador")
            return Result<AjusteInventarioDto>.Failure(
                $"Solo se puede cancelar un ajuste en Borrador (estado actual: {ajuste.Estado}).");

        ajuste.Cancelar(userId);

        await _unitOfWork.BeginTransactionAsync(ct);
        try
        {
            await _activity.AddAsync(UserActivity.Create(
                tenantId, userId, _currentUser.Email, _currentUser.FullName,
                module: "inventario", action: "ajuste.cancelar",
                entityType: "AjusteInventario", entityId: ajuste.Id,
                description: ajuste.NumeroAjuste), ct);

            await _unitOfWork.SaveChangesAsync(ct);
            await _unitOfWork.CommitAsync(ct);

            _logger.LogInformation("Ajuste cancelado: {Numero}", ajuste.NumeroAjuste);

            return Result<AjusteInventarioDto>.Success(new(
            ajuste.Id, ajuste.NumeroAjuste,
            ajuste.BodegaId,   ajuste.BodegaNombre,
            ajuste.ProductoId, ajuste.ProductoNombre,
            ajuste.CantidadAjuste, ajuste.TipoAjuste,
            ajuste.Motivo, ajuste.Observaciones,
            ajuste.FechaAjuste, ajuste.Estado,
            ajuste.FechaEjecucion, ajuste.EjecutadoPor,
            ajuste.CreatedAt));
        }
        catch (Exception ex)
        {
            await _unitOfWork.RollbackAsync(ct);
            _logger.LogError(ex, "Error al cancelar ajuste {Id}", command.AjusteId);
            return Result<AjusteInventarioDto>.Failure($"No se pudo cancelar el ajuste: {ex.Message}");
        }
    }
}

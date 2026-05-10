using MediatR;
using Microsoft.Extensions.Logging;
using ERP.Application.Common;
using ERP.Application.Inventario.DTOs;
using ERP.Domain.Audit.Entities;
using ERP.Domain.Audit.Interfaces;
using ERP.Domain.Inventario.Entities;
using ERP.Domain.Inventario.Enums;
using ERP.Domain.Inventario.Interfaces;

namespace ERP.Application.Inventario.UseCases.EjecutarAjuste;

public sealed class EjecutarAjusteCommandHandler
    : IRequestHandler<EjecutarAjusteCommand, Result<AjusteInventarioDto>>
{
    private readonly IAjusteInventarioRepository _ajusteRepo;
    private readonly IInventarioStockRepository  _inventario;
    private readonly IUserActivityRepository     _activity;
    private readonly IUnitOfWork                 _unitOfWork;
    private readonly ICurrentTenant              _currentTenant;
    private readonly ICurrentUser                _currentUser;
    private readonly ILogger<EjecutarAjusteCommandHandler> _logger;

    public EjecutarAjusteCommandHandler(
        IAjusteInventarioRepository ajusteRepo,
        IInventarioStockRepository inventario,
        IUserActivityRepository activity,
        IUnitOfWork unitOfWork,
        ICurrentTenant currentTenant,
        ICurrentUser currentUser,
        ILogger<EjecutarAjusteCommandHandler> logger)
    {
        _ajusteRepo    = ajusteRepo;
        _inventario    = inventario;
        _activity      = activity;
        _unitOfWork    = unitOfWork;
        _currentTenant = currentTenant;
        _currentUser   = currentUser;
        _logger        = logger;
    }

    public async Task<Result<AjusteInventarioDto>> Handle(
        EjecutarAjusteCommand command, CancellationToken ct)
    {
        var tenantId = _currentTenant.TenantId;
        var userId   = _currentUser.UserId;

        var ajuste = await _ajusteRepo.GetByIdAsync(tenantId, command.AjusteId, ct);
        if (ajuste is null)
            return Result<AjusteInventarioDto>.Failure("Ajuste no encontrado.");

        if (ajuste.Estado != "Borrador")
            return Result<AjusteInventarioDto>.Failure(
                $"Solo se puede ejecutar un ajuste en Borrador (estado actual: {ajuste.Estado}).");

        _logger.LogInformation(
            "Ejecutando ajuste {Numero} ({Id}): {Cantidad} en bodega {Bodega}",
            ajuste.NumeroAjuste, ajuste.Id, ajuste.CantidadAjuste, ajuste.BodegaNombre);

        await _unitOfWork.BeginTransactionAsync(ct);
        try
        {
            decimal cantidadAnterior;
            TipoMovimientoInventario tipoMovimiento;

            if (ajuste.CantidadAjuste > 0)
            {
                // Incremento: UPSERT atómico — siempre tiene éxito
                cantidadAnterior = await _inventario.IncrementarStockAtomicoAsync(
                    tenantId, ajuste.BodegaId, ajuste.ProductoId,
                    ajuste.CantidadAjuste, userId, ct);
                tipoMovimiento = TipoMovimientoInventario.AjustePositivo;
            }
            else
            {
                // Disminución: UPDATE WHERE disponible >= |delta| — puede fallar por stock
                var cantAnteriorNullable = await _inventario.DecrementarStockAtomicoAsync(
                    tenantId, ajuste.BodegaId, ajuste.ProductoId,
                    Math.Abs(ajuste.CantidadAjuste), userId, ct);

                if (cantAnteriorNullable is null)
                {
                    await _unitOfWork.RollbackAsync(ct);
                    _logger.LogWarning(
                        "Stock insuficiente al ejecutar ajuste {Numero}: solicitado={S}",
                        ajuste.NumeroAjuste, ajuste.CantidadAjuste);
                    return Result<AjusteInventarioDto>.Failure(
                        $"Stock insuficiente en '{ajuste.BodegaNombre}' para disminuir " +
                        $"{Math.Abs(ajuste.CantidadAjuste)} unidades de '{ajuste.ProductoNombre}'.");
                }

                cantidadAnterior = cantAnteriorNullable.Value;
                tipoMovimiento   = TipoMovimientoInventario.AjusteNegativo;
            }

            await _inventario.AddMovimientoAsync(
                InventarioMovimiento.Create(
                    tenantId, ajuste.ProductoId, ajuste.BodegaId,
                    tipoMovimiento,
                    cantidad:            ajuste.CantidadAjuste,
                    cantidadAnterior:    cantidadAnterior,
                    referencia:          ajuste.NumeroAjuste,
                    documentoOrigenId:   ajuste.Id,
                    documentoOrigenTipo: "AjusteInventario",
                    createdBy:           userId),
                ct);

            ajuste.Ejecutar(userId);

            await _activity.AddAsync(UserActivity.Create(
                tenantId, userId, _currentUser.Email, _currentUser.FullName,
                module: "inventario", action: "ajuste.ejecutar",
                entityType: "AjusteInventario", entityId: ajuste.Id,
                description: ajuste.NumeroAjuste), ct);

            await _unitOfWork.SaveChangesAsync(ct);
            await _unitOfWork.CommitAsync(ct);

            _logger.LogInformation(
                "Ajuste ejecutado: {Numero} ({Id})", ajuste.NumeroAjuste, ajuste.Id);

            return Result<AjusteInventarioDto>.Success(ToDto(ajuste));
        }
        catch (InvalidOperationException ex)
        {
            await _unitOfWork.RollbackAsync(ct);
            _logger.LogWarning(ex, "Error de negocio al ejecutar ajuste {Id}", command.AjusteId);
            return Result<AjusteInventarioDto>.Failure(ex.Message);
        }
        catch (Exception ex)
        {
            await _unitOfWork.RollbackAsync(ct);
            _logger.LogError(ex, "Error inesperado al ejecutar ajuste {Id}", command.AjusteId);
            return Result<AjusteInventarioDto>.Failure($"Error al ejecutar el ajuste: {ex.Message}");
        }
    }

    private static AjusteInventarioDto ToDto(AjusteInventario a) => new(
        a.Id, a.NumeroAjuste,
        a.BodegaId,   a.BodegaNombre,
        a.ProductoId, a.ProductoNombre,
        a.CantidadAjuste, a.TipoAjuste,
        a.Motivo, a.Observaciones,
        a.FechaAjuste, a.Estado,
        a.FechaEjecucion, a.EjecutadoPor,
        a.CreatedAt);
}

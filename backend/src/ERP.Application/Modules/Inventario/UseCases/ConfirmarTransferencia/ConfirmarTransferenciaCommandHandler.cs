using MediatR;
using Microsoft.Extensions.Logging;
using ERP.Application.Common;
using ERP.Application.Common.Interfaces;
using ERP.Application.Inventario.DTOs;
using ERP.Domain.Audit.Entities;
using ERP.Domain.Audit.Interfaces;
using ERP.Domain.Modules.Inventario.Entities;
using ERP.Domain.Modules.Inventario.Enums;
using ERP.Domain.Modules.Inventario.Interfaces;
using ERP.Domain.Products.Interfaces;

namespace ERP.Application.Inventario.UseCases.ConfirmarTransferencia;

public sealed class ConfirmarTransferenciaCommandHandler
    : IRequestHandler<ConfirmarTransferenciaCommand, Result<TransferenciaDto>>
{
    private readonly ITransferenciaRepository   _transferenciaRepo;
    private readonly IInventarioStockRepository _inventario;
    private readonly ICostoPromedioService      _costoServicio;
    private readonly IProductRepository         _productRepo;
    private readonly IUserActivityRepository    _activity;
    private readonly IUnitOfWork                _unitOfWork;
    private readonly ICurrentTenant             _currentTenant;
    private readonly ICurrentUser               _currentUser;
    private readonly ILogger<ConfirmarTransferenciaCommandHandler> _logger;

    public ConfirmarTransferenciaCommandHandler(
        ITransferenciaRepository transferenciaRepo,
        IInventarioStockRepository inventario,
        ICostoPromedioService costoServicio,
        IProductRepository productRepo,
        IUserActivityRepository activity,
        IUnitOfWork unitOfWork,
        ICurrentTenant currentTenant,
        ICurrentUser currentUser,
        ILogger<ConfirmarTransferenciaCommandHandler> logger)
    {
        _transferenciaRepo = transferenciaRepo;
        _inventario        = inventario;
        _costoServicio     = costoServicio;
        _productRepo       = productRepo;
        _activity          = activity;
        _unitOfWork        = unitOfWork;
        _currentTenant     = currentTenant;
        _currentUser       = currentUser;
        _logger            = logger;
    }

    public async Task<Result<TransferenciaDto>> Handle(
        ConfirmarTransferenciaCommand command, CancellationToken ct)
    {
        var tenantId = _currentTenant.TenantId;
        var userId   = _currentUser.UserId;

        var transferencia = await _transferenciaRepo.GetByIdAsync(tenantId, command.TransferenciaId, ct);
        if (transferencia is null)
            return Result<TransferenciaDto>.Failure("Transferencia no encontrada.");

        if (transferencia.Estado != "Borrador")
            return Result<TransferenciaDto>.Failure(
                $"Solo se puede confirmar una transferencia en Borrador (estado actual: {transferencia.Estado}).");

        if (transferencia.Detalles.Count == 0)
            return Result<TransferenciaDto>.Failure("La transferencia no tiene ítems.");

        _logger.LogInformation(
            "Confirmando transferencia {Numero} ({Id}): {Count} ítems",
            transferencia.NumeroTransferencia, transferencia.Id, transferencia.Detalles.Count);

        await _unitOfWork.BeginTransactionAsync(ct);
        try
        {
            foreach (var detalle in transferencia.Detalles)
            {
                var producto = await _productRepo.GetByIdAsync(detalle.ProductoId, tenantId, ct);
                if (producto is not null && (producto.IsService || !producto.TracksStock))
                    continue;

                // Obtener el costo promedio actual de la bodega ORIGEN antes del decremento.
                // Se usa para valorizar la salida y la entrada en destino con el mismo costo.
                var costoPromedio = await _costoServicio.ObtenerCostoPromedioAsync(
                    tenantId, detalle.ProductoId, transferencia.BodegaOrigenId, ct);

                // ── Decremento atómico en bodega ORIGEN ───────────────────────
                var cantAnteriorOrigen = await _inventario.DecrementarStockAtomicoAsync(
                    tenantId, transferencia.BodegaOrigenId, detalle.ProductoId,
                    detalle.Cantidad, userId, ct, costoPromedio);

                if (cantAnteriorOrigen is null)
                {
                    await _unitOfWork.RollbackAsync(ct);
                    _logger.LogWarning(
                        "Stock insuficiente (posiblemente por concurrencia): producto={Pid}, solicitado={S}",
                        detalle.ProductoId, detalle.Cantidad);
                    return Result<TransferenciaDto>.Failure(
                        $"Stock insuficiente para '{detalle.Descripcion}' en la bodega origen. " +
                        $"El saldo pudo haber sido modificado por otra operación concurrente.");
                }

                await _inventario.AddMovimientoAsync(
                    InventarioMovimiento.Create(
                        tenantId, detalle.ProductoId, transferencia.BodegaOrigenId,
                        TipoMovimientoInventario.TransferenciaSalida,
                        cantidad:            -detalle.Cantidad,
                        cantidadAnterior:    cantAnteriorOrigen.Value,
                        referencia:          transferencia.NumeroTransferencia,
                        documentoOrigenId:   transferencia.Id,
                        documentoOrigenTipo: "Transferencia",
                        createdBy:           userId,
                        costoUnitario:       costoPromedio),
                    ct);

                // ── Incremento atómico en bodega DESTINO ──────────────────────
                var cantAnteriorDestino = await _inventario.IncrementarStockAtomicoAsync(
                    tenantId, transferencia.BodegaDestinoId, detalle.ProductoId,
                    detalle.Cantidad, userId, ct, costoPromedio);

                await _inventario.AddMovimientoAsync(
                    InventarioMovimiento.Create(
                        tenantId, detalle.ProductoId, transferencia.BodegaDestinoId,
                        TipoMovimientoInventario.TransferenciaEntrada,
                        cantidad:            +detalle.Cantidad,
                        cantidadAnterior:    cantAnteriorDestino,
                        referencia:          transferencia.NumeroTransferencia,
                        documentoOrigenId:   transferencia.Id,
                        documentoOrigenTipo: "Transferencia",
                        createdBy:           userId,
                        costoUnitario:       costoPromedio),
                    ct);
            }

            transferencia.Confirmar(userId);

            await _activity.AddAsync(UserActivity.Create(
                tenantId, userId, _currentUser.Email, _currentUser.FullName,
                module: "inventario", action: "transferencia.confirmar",
                entityType: "Transferencia", entityId: transferencia.Id,
                description: transferencia.NumeroTransferencia), ct);

            await _unitOfWork.SaveChangesAsync(ct);
            await _unitOfWork.CommitAsync(ct);

            _logger.LogInformation(
                "Transferencia confirmada: {Numero} ({Id})",
                transferencia.NumeroTransferencia, transferencia.Id);

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
        catch (InvalidOperationException ex)
        {
            await _unitOfWork.RollbackAsync(ct);
            _logger.LogWarning(ex, "Error de negocio al confirmar transferencia {Id}", command.TransferenciaId);
            return Result<TransferenciaDto>.Failure(ex.Message);
        }
        catch (Exception ex)
        {
            await _unitOfWork.RollbackAsync(ct);
            _logger.LogError(ex, "Error inesperado al confirmar transferencia {Id}", command.TransferenciaId);
            return Result<TransferenciaDto>.Failure($"Error al confirmar la transferencia: {ex.Message}");
        }
    }
}

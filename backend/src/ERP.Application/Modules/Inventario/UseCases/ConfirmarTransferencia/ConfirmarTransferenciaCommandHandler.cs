using MediatR;
using Microsoft.Extensions.Logging;
using ERP.Application.Common;
using ERP.Application.Inventario.DTOs;
using ERP.Domain.Audit.Entities;
using ERP.Domain.Audit.Interfaces;
using ERP.Domain.Inventario.Entities;
using ERP.Domain.Inventario.Enums;
using ERP.Domain.Inventario.Interfaces;
using ERP.Domain.Products.Interfaces;

namespace ERP.Application.Inventario.UseCases.ConfirmarTransferencia;

public sealed class ConfirmarTransferenciaCommandHandler
    : IRequestHandler<ConfirmarTransferenciaCommand, Result<TransferenciaDto>>
{
    private readonly ITransferenciaRepository   _transferenciaRepo;
    private readonly IInventarioStockRepository _inventario;
    private readonly IProductRepository         _productRepo;
    private readonly IUserActivityRepository    _activity;
    private readonly IUnitOfWork                _unitOfWork;
    private readonly ICurrentTenant             _currentTenant;
    private readonly ICurrentUser               _currentUser;
    private readonly ILogger<ConfirmarTransferenciaCommandHandler> _logger;

    public ConfirmarTransferenciaCommandHandler(
        ITransferenciaRepository transferenciaRepo,
        IInventarioStockRepository inventario,
        IProductRepository productRepo,
        IUserActivityRepository activity,
        IUnitOfWork unitOfWork,
        ICurrentTenant currentTenant,
        ICurrentUser currentUser,
        ILogger<ConfirmarTransferenciaCommandHandler> logger)
    {
        _transferenciaRepo = transferenciaRepo;
        _inventario        = inventario;
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
                    continue; // los servicios no manejan stock físico

                // ── Stock en bodega ORIGEN ────────────────────────────────
                var stockOrigen = await _inventario.GetStockByTenantBodegaProductAsync(
                    tenantId, transferencia.BodegaOrigenId, detalle.ProductoId, ct);

                if (stockOrigen is null || stockOrigen.CantidadDisponible < detalle.Cantidad)
                {
                    await _unitOfWork.RollbackAsync(ct);
                    var disponible = stockOrigen?.CantidadDisponible ?? 0;
                    _logger.LogWarning(
                        "Stock insuficiente al confirmar: producto={ProductoId}, disponible={D}, solicitado={S}",
                        detalle.ProductoId, disponible, detalle.Cantidad);
                    return Result<TransferenciaDto>.Failure(
                        $"Stock insuficiente para el producto '{detalle.Descripcion}' en la bodega origen. " +
                        $"Disponible: {disponible}, Requerido: {detalle.Cantidad}");
                }

                var cantAnteriorOrigen = stockOrigen.Cantidad;
                stockOrigen.AplicarMovimiento(-detalle.Cantidad, userId);

                var movSalida = InventarioMovimiento.Create(
                    tenantId, detalle.ProductoId, transferencia.BodegaOrigenId,
                    TipoMovimientoInventario.TransferenciaSalida,
                    cantidad:            -detalle.Cantidad,
                    cantidadAnterior:    cantAnteriorOrigen,
                    referencia:          transferencia.NumeroTransferencia,
                    documentoOrigenId:   transferencia.Id,
                    documentoOrigenTipo: "Transferencia",
                    createdBy:           userId);
                await _inventario.AddMovimientoAsync(movSalida, ct);

                // ── Stock en bodega DESTINO ───────────────────────────────
                var stockDestino = await _inventario.GetStockByTenantBodegaProductAsync(
                    tenantId, transferencia.BodegaDestinoId, detalle.ProductoId, ct);

                if (stockDestino is null)
                {
                    stockDestino = StockActual.Create(
                        tenantId, detalle.ProductoId, transferencia.BodegaDestinoId, userId);
                    await _inventario.AddStockActualAsync(stockDestino, ct);
                }

                var cantAnteriorDestino = stockDestino.Cantidad;
                stockDestino.AplicarMovimiento(+detalle.Cantidad, userId);

                var movEntrada = InventarioMovimiento.Create(
                    tenantId, detalle.ProductoId, transferencia.BodegaDestinoId,
                    TipoMovimientoInventario.TransferenciaEntrada,
                    cantidad:            +detalle.Cantidad,
                    cantidadAnterior:    cantAnteriorDestino,
                    referencia:          transferencia.NumeroTransferencia,
                    documentoOrigenId:   transferencia.Id,
                    documentoOrigenTipo: "Transferencia",
                    createdBy:           userId);
                await _inventario.AddMovimientoAsync(movEntrada, ct);
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

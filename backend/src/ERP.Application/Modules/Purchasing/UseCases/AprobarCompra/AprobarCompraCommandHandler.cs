using MediatR;
using Microsoft.Extensions.Logging;
using ERP.Application.Common;
using ERP.Application.Common.Interfaces;
using ERP.Application.Modules.Purchasing.DTOs;
using ERP.Domain.Audit.Entities;
using ERP.Domain.Audit.Interfaces;
using ERP.Domain.Modules.Purchasing.Enums;
using ERP.Domain.Modules.Purchasing.Events;
using ERP.Domain.Modules.Purchasing.Interfaces;

namespace ERP.Application.Modules.Purchasing.UseCases.AprobarCompra;

public sealed class AprobarCompraCommandHandler
    : IRequestHandler<AprobarCompraCommand, Result<CompraFacturaDto>>
{
    private readonly ICompraRepository          _repo;
    private readonly IAccountingService         _accounting;
    private readonly IUserActivityRepository    _activity;
    private readonly ICurrentTenant             _tenant;
    private readonly ICurrentUser               _user;
    private readonly IUnitOfWork                _unitOfWork;
    private readonly ILogger<AprobarCompraCommandHandler> _logger;

    public AprobarCompraCommandHandler(
        ICompraRepository repo,
        IAccountingService accounting,
        IUserActivityRepository activity,
        ICurrentTenant tenant,
        ICurrentUser user,
        IUnitOfWork unitOfWork,
        ILogger<AprobarCompraCommandHandler> logger)
    {
        _repo       = repo;
        _accounting = accounting;
        _activity   = activity;
        _tenant     = tenant;
        _user       = user;
        _unitOfWork = unitOfWork;
        _logger     = logger;
    }

    public async Task<Result<CompraFacturaDto>> Handle(
        AprobarCompraCommand command,
        CancellationToken ct)
    {
        var tenantId = _tenant.TenantId;
        var userId   = _user.UserId;

        var compra = await _repo.GetByIdWithDetailsAsync(tenantId, command.CompraFacturaId, ct);
        if (compra is null)
            return Result<CompraFacturaDto>.Failure("Compra no encontrada.");

        if (compra.Estado != EstadoCompra.Validado)
            return Result<CompraFacturaDto>.Failure(
                $"Solo se puede aprobar una compra Validada (estado actual: {compra.Estado}).");

        var asignaciones =
            await _repo.GetBodegaAsignacionesByCompraFacturaIdAsync(tenantId, command.CompraFacturaId, ct);

        var stockLines = new List<CompraAprobadaStockLine>();
        foreach (var asig in asignaciones)
        {
            var detalle = compra.Detalles.FirstOrDefault(d => d.Id == asig.CompraDetalleId);
            if (detalle is null)
            {
                _logger.LogWarning(
                    "Compra {CompraId}: asignación huérfana (detalle {DetalleId} no encontrado en la factura).",
                    compra.Id, asig.CompraDetalleId);
                continue;
            }

            if (!asig.ProductoId.HasValue)
            {
                _logger.LogWarning(
                    "Compra {CompraId}: línea sin producto enlazado; no se actualiza inventario físico (detalle {DetalleId}, bodega {BodegaId}, cantidad {Cantidad}).",
                    compra.Id, asig.CompraDetalleId, asig.BodegaId, asig.Cantidad);
                continue;
            }

            var costoUnitario = detalle.Cantidad > 0
                ? detalle.PrecioUnitario * (1 - detalle.DescuentoPorcentaje / 100m)
                : 0m;

            stockLines.Add(new CompraAprobadaStockLine(
                asig.CompraDetalleId,
                asig.ProductoId,
                asig.BodegaId,
                asig.Cantidad,
                costoUnitario));
        }

        await _unitOfWork.BeginTransactionAsync(ct);
        try
        {
            var asientoResult = await _accounting.CrearAsientoCompraAsync(
                compra.Id,
                referencia:  compra.NumeroFactura,
                fecha:       compra.FechaFactura,
                subtotal:    compra.Subtotal,
                iva:         compra.IvaTotal,
                total:       compra.Total,
                descripcion: $"Compra {compra.NumeroFactura} — {compra.ProveedorId}",
                ct);

            if (!asientoResult.IsSuccess)
            {
                await _unitOfWork.RollbackAsync(ct);
                return Result<CompraFacturaDto>.Failure(
                    asientoResult.Error ?? "No se pudo registrar el asiento contable de la compra.");
            }

            var asientoId = asientoResult.Value;

            compra.Aprobar(userId, asientoId, stockLines);

            await _activity.AddAsync(UserActivity.Create(
                tenantId, userId, _user.Email, _user.FullName,
                module: "compras", action: "compra.aprobar",
                entityType: "CompraFactura", entityId: compra.Id,
                description: $"{compra.NumeroFactura} — asiento: {asientoId}"), ct);

            await _unitOfWork.SaveChangesAsync(ct);
            await _unitOfWork.CommitAsync(ct);

            _logger.LogInformation(
                "Compra aprobada: id {CompraId}, tenant {TenantId}, usuario {UserId}, asiento {AsientoId}.",
                compra.Id, tenantId, userId, asientoId);

            return Result<CompraFacturaDto>.Success(ToDto(compra));
        }
        catch (Exception ex)
        {
            await _unitOfWork.RollbackAsync(ct);
            _logger.LogError(ex, "Error al aprobar compra {CompraId}", command.CompraFacturaId);
            return Result<CompraFacturaDto>.Failure($"No se pudo aprobar la compra: {ex.Message}");
        }
    }

    private static CompraFacturaDto ToDto(ERP.Domain.Modules.Purchasing.Entities.CompraFactura c) => new(
        c.Id, c.ProveedorId, c.NumeroFactura, c.ClaveAcceso, c.XmlPath,
        c.FechaFactura, c.FechaVencimiento, c.Estado, c.CondicionPago,
        c.Subtotal, c.IvaTotal, c.Total, c.Observaciones, c.AsientoContableId, c.CreatedAt);
}

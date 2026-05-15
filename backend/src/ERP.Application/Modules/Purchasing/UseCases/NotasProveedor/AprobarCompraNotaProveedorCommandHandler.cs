using MediatR;
using Microsoft.Extensions.Logging;
using ERP.Application.Common;
using ERP.Application.Common.Interfaces;
using ERP.Application.Modules.Purchasing.DTOs;
using ERP.Domain.Audit.Entities;
using ERP.Domain.Audit.Interfaces;
using ERP.Domain.Modules.Purchasing.Entities;
using ERP.Domain.Modules.Purchasing.Enums;
using ERP.Domain.Modules.Purchasing.Events;
using ERP.Domain.Modules.Purchasing.Interfaces;
using ERP.Domain.Modules.Expenses.Enums;
using ERP.Domain.Modules.Expenses.Interfaces;

namespace ERP.Application.Modules.Purchasing.UseCases.NotasProveedor;

public sealed class AprobarCompraNotaProveedorCommandHandler
    : IRequestHandler<AprobarCompraNotaProveedorCommand, Result<CompraNotaProveedorDto>>
{
    private readonly ICompraRepository       _compraRepo;
    private readonly IGastoFacturaRepository _gastoRepo;
    private readonly IAccountingService     _accounting;
    private readonly IUserActivityRepository _activity;
    private readonly ICurrentTenant          _tenant;
    private readonly ICurrentUser            _user;
    private readonly IUnitOfWork           _unitOfWork;
    private readonly ILogger<AprobarCompraNotaProveedorCommandHandler> _logger;

    public AprobarCompraNotaProveedorCommandHandler(
        ICompraRepository compraRepo,
        IGastoFacturaRepository gastoRepo,
        IAccountingService accounting,
        IUserActivityRepository activity,
        ICurrentTenant tenant,
        ICurrentUser user,
        IUnitOfWork unitOfWork,
        ILogger<AprobarCompraNotaProveedorCommandHandler> logger)
    {
        _compraRepo   = compraRepo;
        _gastoRepo    = gastoRepo;
        _accounting   = accounting;
        _activity     = activity;
        _tenant       = tenant;
        _user         = user;
        _unitOfWork   = unitOfWork;
        _logger       = logger;
    }

    public async Task<Result<CompraNotaProveedorDto>> Handle(
        AprobarCompraNotaProveedorCommand command,
        CancellationToken ct)
    {
        var tenantId = _tenant.TenantId;
        var userId   = _user.UserId;

        var nota = await _compraRepo.GetNotaProveedorByIdWithDetailsAsync(tenantId, command.NotaId, ct);
        if (nota is null)
            return Result<CompraNotaProveedorDto>.Failure("Nota de proveedor no encontrada.");

        if (nota.Estado != "Borrador")
            return Result<CompraNotaProveedorDto>.Failure(
                $"Solo se puede aprobar una nota en Borrador (estado: {nota.Estado}).");

        if (!nota.CompraFacturaId.HasValue && !nota.GastoFacturaId.HasValue)
            return Result<CompraNotaProveedorDto>.Failure(
                "Vincule la nota a una factura de compra o de gasto antes de aprobar.");

        var numeroNota = $"{nota.Establecimiento}-{nota.PuntoEmision}-{nota.Secuencial}";
        var descripcionBase = $"Nota {nota.TipoNota} proveedor {numeroNota} (clave {nota.ClaveAcceso})";

        await _unitOfWork.BeginTransactionAsync(ct);
        try
        {
            Result<Guid> asientoResult;
            if (nota.CompraFacturaId.HasValue)
            {
                var compra = await _compraRepo.GetByIdAsync(tenantId, nota.CompraFacturaId.Value, ct);
                if (compra is null || compra.Estado != EstadoCompra.Aprobado)
                {
                    await _unitOfWork.RollbackAsync(ct);
                    return Result<CompraNotaProveedorDto>.Failure(
                        "La factura de compra vinculada no existe o no está aprobada.");
                }

                compra.RegistrarNotaProveedorAplicada(nota.TipoNota, nota.Total, userId);

                asientoResult = nota.TipoNota == "CREDITO"
                    ? await _accounting.CrearAsientoNotaCreditoCompraProveedorAsync(
                        nota.Id,
                        referencia:  numeroNota,
                        fecha:       nota.FechaEmision,
                        subtotal:    nota.Subtotal,
                        impuesto:    nota.Impuesto,
                        total:       nota.Total,
                        descripcion: $"{descripcionBase} — compra {compra.NumeroFactura}",
                        ct)
                    : await _accounting.CrearAsientoNotaDebitoCompraProveedorAsync(
                        nota.Id,
                        referencia:  numeroNota,
                        fecha:       nota.FechaEmision,
                        subtotal:    nota.Subtotal,
                        impuesto:    nota.Impuesto,
                        total:       nota.Total,
                        descripcion: $"{descripcionBase} — compra {compra.NumeroFactura}",
                        ct);
            }
            else
            {
                var gasto = await _gastoRepo.GetByIdAsync(tenantId, nota.GastoFacturaId!.Value, ct);
                if (gasto is null || gasto.Estado != EstadoGasto.Aprobado)
                {
                    await _unitOfWork.RollbackAsync(ct);
                    return Result<CompraNotaProveedorDto>.Failure(
                        "La factura de gasto vinculada no existe o no está aprobada.");
                }

                gasto.RegistrarNotaProveedorAplicada(nota.TipoNota, nota.Total, userId);

                asientoResult = nota.TipoNota == "CREDITO"
                    ? await _accounting.CrearAsientoNotaCreditoGastoProveedorAsync(
                        nota.Id,
                        referencia:     numeroNota,
                        fecha:          nota.FechaEmision,
                        total:          nota.Total,
                        categoriaGasto: gasto.CategoriaGasto,
                        descripcion:    $"{descripcionBase} — gasto {gasto.Concepto}",
                        ct)
                    : await _accounting.CrearAsientoNotaDebitoGastoProveedorAsync(
                        nota.Id,
                        referencia:     numeroNota,
                        fecha:          nota.FechaEmision,
                        total:          nota.Total,
                        categoriaGasto: gasto.CategoriaGasto,
                        descripcion:    $"{descripcionBase} — gasto {gasto.Concepto}",
                        ct);
            }

            if (!asientoResult.IsSuccess)
            {
                await _unitOfWork.RollbackAsync(ct);
                return Result<CompraNotaProveedorDto>.Failure(
                    asientoResult.Error ?? "No se pudo registrar el asiento contable de la nota.");
            }

            var asientoId = asientoResult.Value;
            IReadOnlyList<CompraNotaProveedorStockLine>? stockLines = null;
            if (nota.CompraFacturaId.HasValue)
            {
                var compraFull = await _compraRepo.GetByIdWithDetailsAsync(tenantId, nota.CompraFacturaId.Value, ct);
                var asigs =
                    await _compraRepo.GetBodegaAsignacionesByCompraFacturaIdAsync(
                        tenantId, nota.CompraFacturaId.Value, ct);
                if (compraFull is not null)
                    stockLines = BuildStockLines(nota, compraFull, asigs);
            }

            nota.Aprobar(
                userId,
                asientoId,
                command.NumeroAutorizacion,
                command.FechaAutorizacion,
                stockLines);

            await _activity.AddAsync(UserActivity.Create(
                tenantId, userId, _user.Email, _user.FullName,
                module: "compras", action: "notas-proveedor.aprobar",
                entityType: "CompraNotaProveedor", entityId: nota.Id,
                description: $"{numeroNota} — asiento {asientoId}"), ct);

            await _unitOfWork.SaveChangesAsync(ct);
            await _unitOfWork.CommitAsync(ct);

            _logger.LogInformation("Nota proveedor aprobada: {NotaId}, asiento {AsientoId}", nota.Id, asientoId);
            return Result<CompraNotaProveedorDto>.Success(ToDto(nota));
        }
        catch (Exception ex)
        {
            await _unitOfWork.RollbackAsync(ct);
            _logger.LogError(ex, "Error al aprobar nota proveedor {NotaId}", command.NotaId);
            return Result<CompraNotaProveedorDto>.Failure($"No se pudo aprobar la nota: {ex.Message}");
        }
    }

    private static IReadOnlyList<CompraNotaProveedorStockLine> BuildStockLines(
        CompraNotaProveedor nota,
        CompraFactura compra,
        IReadOnlyList<CompraBodegaAsignacion> asignaciones)
    {
        var lines = new List<CompraNotaProveedorStockLine>();
        foreach (var nd in nota.Detalles)
        {
            if (!nd.ProductoId.HasValue)
                continue;

            var compraDet = compra.Detalles.FirstOrDefault(d =>
                (!string.IsNullOrEmpty(nd.CodigoPrincipalProveedor) &&
                 string.Equals(d.CodigoPrincipalProveedor, nd.CodigoPrincipalProveedor, StringComparison.OrdinalIgnoreCase))
                || (d.ProductoId == nd.ProductoId));

            if (compraDet is null)
                continue;

            var asigs = asignaciones
                .Where(a => a.CompraDetalleId == compraDet.Id && a.ProductoId == nd.ProductoId)
                .ToList();
            if (asigs.Count == 0)
                continue;

            var detCant = compraDet.Cantidad;
            var sign    = nota.TipoNota == "CREDITO" ? 1m : -1m;
            var costo   = compraDet.Cantidad > 0
                ? compraDet.PrecioUnitario * (1 - compraDet.DescuentoPorcentaje / 100m)
                : 0m;

            foreach (var a in asigs)
            {
                var frac = detCant > 0 ? a.Cantidad / detCant : 1m / asigs.Count;
                var qty  = nd.Cantidad * frac * sign;
                if (qty == 0)
                    continue;
                lines.Add(new CompraNotaProveedorStockLine(
                    nd.ProductoId.Value,
                    a.BodegaId,
                    qty,
                    costo));
            }
        }

        return lines;
    }

    private static CompraNotaProveedorDto ToDto(CompraNotaProveedor n) => new(
        n.Id,
        n.ProveedorId,
        n.CompraFacturaId,
        n.GastoFacturaId,
        n.TipoNota,
        n.Motivo,
        n.ClaveAcceso,
        n.FechaEmision,
        n.Establecimiento,
        n.PuntoEmision,
        n.Secuencial,
        n.Subtotal,
        n.Impuesto,
        n.Total,
        n.Estado,
        n.XmlPath,
        n.AsientoContableId,
        n.CreatedAt);
}

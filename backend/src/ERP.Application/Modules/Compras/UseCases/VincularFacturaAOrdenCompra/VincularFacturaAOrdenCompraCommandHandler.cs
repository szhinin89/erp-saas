using MediatR;
using Microsoft.Extensions.Logging;
using ERP.Application.Common;
using ERP.Application.Modules.Compras.DTOs;
using ERP.Application.Modules.Compras.UseCases.CrearOrdenCompra;
using ERP.Domain.Audit.Entities;
using ERP.Domain.Audit.Interfaces;
using ERP.Domain.Compras.Entities;
using ERP.Domain.Compras.Enums;
using ERP.Domain.Compras.Interfaces;
using ERP.Domain.Proveedores.Interfaces;

namespace ERP.Application.Modules.Compras.UseCases.VincularFacturaAOrdenCompra;

public sealed class VincularFacturaAOrdenCompraCommandHandler
    : IRequestHandler<VincularFacturaAOrdenCompraCommand, Result<OrdenCompraDto>>
{
    private readonly IOrdenCompraRepository  _ordenRepo;
    private readonly ICompraRepository       _compraRepo;
    private readonly IProveedorRepository    _proveedorRepo;
    private readonly IUserActivityRepository _activity;
    private readonly ICurrentTenant          _currentTenant;
    private readonly ICurrentUser            _currentUser;
    private readonly ILogger<VincularFacturaAOrdenCompraCommandHandler> _logger;

    public VincularFacturaAOrdenCompraCommandHandler(
        IOrdenCompraRepository ordenRepo,
        ICompraRepository compraRepo,
        IProveedorRepository proveedorRepo,
        IUserActivityRepository activity,
        ICurrentTenant currentTenant,
        ICurrentUser currentUser,
        ILogger<VincularFacturaAOrdenCompraCommandHandler> logger)
    {
        _ordenRepo     = ordenRepo;
        _compraRepo    = compraRepo;
        _proveedorRepo = proveedorRepo;
        _activity      = activity;
        _currentTenant = currentTenant;
        _currentUser   = currentUser;
        _logger        = logger;
    }

    public async Task<Result<OrdenCompraDto>> Handle(
        VincularFacturaAOrdenCompraCommand command, CancellationToken ct)
    {
        var tenantId = _currentTenant.TenantId;
        var userId   = _currentUser.UserId;

        // 1. Cargar la OC con sus detalles
        var orden = await _ordenRepo.GetByIdWithDetallesAsync(tenantId, command.OrdenCompraId, ct);
        if (orden is null)
            return Result<OrdenCompraDto>.Failure("Orden de compra no encontrada.");

        if (orden.Estado is not ("Aprobada" or "RecibidaParcial"))
            return Result<OrdenCompraDto>.Failure(
                $"Solo se puede vincular una factura a OC en Aprobada o RecibidaParcial (estado: {orden.Estado}).");

        // 2. Cargar la factura con sus detalles
        var factura = await _compraRepo.GetByIdWithDetailsAsync(tenantId, command.CompraFacturaId, ct);
        if (factura is null)
            return Result<OrdenCompraDto>.Failure("Factura de compra no encontrada.");

        if (factura.Estado != EstadoCompra.Aprobado)
            return Result<OrdenCompraDto>.Failure(
                "Solo se pueden vincular facturas en estado Aprobado.");

        // 3. Verificar que no esté ya vinculada a esta OC
        var yaVinculada = await _ordenRepo.FacturaYaVinculadaAsync(
            tenantId, command.OrdenCompraId, command.CompraFacturaId, ct);
        if (yaVinculada)
            return Result<OrdenCompraDto>.Failure("Esta factura ya está vinculada a la orden de compra.");

        // 4. Matching por ProductoId y actualización de CantidadFacturada
        foreach (var detalleFactura in factura.Detalles)
        {
            // Saltear líneas sin producto en catálogo (servicios, fletes, etc.)
            if (detalleFactura.ProductoId is null) continue;

            var detalleOrden = orden.Detalles
                .FirstOrDefault(d => d.ProductoId == detalleFactura.ProductoId.Value);

            if (detalleOrden is null)
                return Result<OrdenCompraDto>.Failure(
                    $"El producto '{detalleFactura.Descripcion}' de la factura no está incluido en esta orden de compra.");

            var nuevoFacturado = detalleOrden.CantidadFacturada + detalleFactura.Cantidad;
            if (nuevoFacturado > detalleOrden.CantidadPedida)
                return Result<OrdenCompraDto>.Failure(
                    $"La cantidad a facturar ({nuevoFacturado:F3}) excede la cantidad pedida " +
                    $"({detalleOrden.CantidadPedida:F3}) para '{detalleFactura.Descripcion}'. " +
                    $"Pendiente por facturar: {detalleOrden.PendienteFacturar:F3}.");

            detalleOrden.AgregarCantidadFacturada(detalleFactura.Cantidad, userId);
            detalleFactura.SetOrdenCompraDetalleId(detalleOrden.Id, userId);
        }

        // 5. Crear la vinculación
        var vinculo = OrdenCompraFactura.Create(tenantId, orden.Id, factura.Id, userId);
        await _ordenRepo.AddOrdenCompraFacturaAsync(vinculo, ct);

        // 6. Actualizar estado de la OC según cobertura
        var todoFacturado = orden.Detalles.All(d => d.CantidadFacturada >= d.CantidadPedida);
        if (todoFacturado)
            orden.Cerrar(userId);
        else if (orden.Estado == "Aprobada")
            orden.MarcarRecibidaParcial(userId);

        await _activity.AddAsync(UserActivity.Create(
            tenantId, userId, _currentUser.Email, _currentUser.FullName,
            module: "compras", action: "orden-compra.vincular-factura",
            entityType: "OrdenCompra", entityId: orden.Id,
            description: $"{orden.NumeroOrden} ← {factura.NumeroFactura}"), ct);

        await _ordenRepo.SaveChangesAsync(ct);

        _logger.LogInformation(
            "Factura {Factura} vinculada a OC {OC}. Estado: {Estado}",
            factura.NumeroFactura, orden.NumeroOrden, orden.Estado);

        var proveedor = await _proveedorRepo.GetByIdAsync(tenantId, orden.ProveedorId, ct);
        return Result<OrdenCompraDto>.Success(
            CrearOrdenCompraCommandHandler.ToDto(orden, proveedor?.RazonSocial ?? orden.ProveedorId.ToString()));
    }
}

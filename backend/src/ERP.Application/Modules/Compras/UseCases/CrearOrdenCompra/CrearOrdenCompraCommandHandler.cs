using MediatR;
using Microsoft.Extensions.Logging;
using ERP.Application.Common;
using ERP.Application.Modules.Compras.DTOs;
using ERP.Domain.Audit.Entities;
using ERP.Domain.Audit.Interfaces;
using ERP.Domain.Modules.Compras.Entities;
using ERP.Domain.Modules.Compras.Interfaces;
using ERP.Domain.Products.Interfaces;
using ERP.Domain.Modules.Compras.Interfaces;

namespace ERP.Application.Modules.Compras.UseCases.CrearOrdenCompra;

public sealed class CrearOrdenCompraCommandHandler
    : IRequestHandler<CrearOrdenCompraCommand, Result<OrdenCompraDto>>
{
    private readonly IOrdenCompraRepository  _ordenRepo;
    private readonly IProveedorRepository    _proveedorRepo;
    private readonly IProductRepository      _productRepo;
    private readonly IUserActivityRepository _activity;
    private readonly ICurrentTenant          _currentTenant;
    private readonly ICurrentUser            _currentUser;
    private readonly ILogger<CrearOrdenCompraCommandHandler> _logger;

    public CrearOrdenCompraCommandHandler(
        IOrdenCompraRepository ordenRepo,
        IProveedorRepository proveedorRepo,
        IProductRepository productRepo,
        IUserActivityRepository activity,
        ICurrentTenant currentTenant,
        ICurrentUser currentUser,
        ILogger<CrearOrdenCompraCommandHandler> logger)
    {
        _ordenRepo     = ordenRepo;
        _proveedorRepo = proveedorRepo;
        _productRepo   = productRepo;
        _activity      = activity;
        _currentTenant = currentTenant;
        _currentUser   = currentUser;
        _logger        = logger;
    }

    public async Task<Result<OrdenCompraDto>> Handle(
        CrearOrdenCompraCommand command, CancellationToken ct)
    {
        var tenantId = _currentTenant.TenantId;
        var userId   = _currentUser.UserId;

        var proveedor = await _proveedorRepo.GetByIdAsync(tenantId, command.ProveedorId, ct);
        if (proveedor is null || !proveedor.IsActive)
            return Result<OrdenCompraDto>.Failure("El proveedor no existe o no está activo.");

        // Validar productos y construir detalles
        var productosValidados = new Dictionary<Guid, ERP.Domain.Products.Entities.Product>();
        foreach (var item in command.Items)
        {
            if (productosValidados.ContainsKey(item.ProductoId)) continue;
            var producto = await _productRepo.GetByIdAsync(item.ProductoId, tenantId, ct);
            if (producto is null || !producto.IsActive)
                return Result<OrdenCompraDto>.Failure(
                    $"El producto {item.ProductoId} no existe o no está activo.");
            productosValidados[item.ProductoId] = producto;
        }

        var secuencial = await _ordenRepo.GetNextSecuencialAsync(tenantId, ct);

        var orden = OrdenCompra.Create(
            tenantId, secuencial,
            command.ProveedorId, command.FechaRequerida,
            command.BodegaDestinoId, command.DireccionEntrega, command.Observaciones,
            userId);

        foreach (var item in command.Items)
        {
            var producto = productosValidados[item.ProductoId];
            var detalle  = OrdenCompraDetalle.Create(
                tenantId, orden.Id, item.ProductoId,
                producto.ShortName,
                item.Cantidad, item.PrecioUnitario, item.IvaPorcentaje,
                userId);
            orden.AgregarDetalle(detalle);
        }

        await _ordenRepo.AddAsync(orden, ct);
        await _ordenRepo.SaveChangesAsync(ct);

        await _activity.AddAsync(UserActivity.Create(
            tenantId, userId, _currentUser.Email, _currentUser.FullName,
            module: "compras", action: "orden-compra.crear",
            entityType: "OrdenCompra", entityId: orden.Id,
            description: orden.NumeroOrden), ct);

        _logger.LogInformation("OC creada: {Numero} ({Id})", orden.NumeroOrden, orden.Id);

        return Result<OrdenCompraDto>.Success(ToDto(orden, proveedor.RazonSocial));
    }

    internal static OrdenCompraDto ToDto(
        OrdenCompra o, string proveedorNombre,
        IReadOnlyList<string>? advertencias = null) => new(
        o.Id, o.NumeroOrden,
        o.ProveedorId, proveedorNombre,
        o.FechaEmision, o.FechaRequerida,
        o.Estado, o.Moneda,
        o.Subtotal, o.Impuesto, o.Total,
        o.Observaciones, o.DireccionEntrega,
        o.BodegaDestinoId,
        o.FechaEnvio, o.FechaAprobacion, o.AprobadoPor, o.FechaCierre,
        o.CreatedAt)
    {
        Advertencias = advertencias is { Count: > 0 } ? advertencias : null,
    };
}

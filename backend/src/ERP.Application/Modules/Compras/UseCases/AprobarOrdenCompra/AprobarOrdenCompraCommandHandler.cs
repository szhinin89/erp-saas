using MediatR;
using Microsoft.Extensions.Logging;
using ERP.Application.Common;
using ERP.Application.Modules.Compras.DTOs;
using ERP.Application.Modules.Compras.UseCases.CrearOrdenCompra;
using ERP.Domain.Audit.Entities;
using ERP.Domain.Audit.Interfaces;
using ERP.Domain.Compras.Interfaces;
using ERP.Domain.Proveedores.Interfaces;

namespace ERP.Application.Modules.Compras.UseCases.AprobarOrdenCompra;

public sealed class AprobarOrdenCompraCommandHandler
    : IRequestHandler<AprobarOrdenCompraCommand, Result<OrdenCompraDto>>
{
    private readonly IOrdenCompraRepository  _ordenRepo;
    private readonly IProveedorRepository    _proveedorRepo;
    private readonly IUserActivityRepository _activity;
    private readonly ICurrentTenant          _currentTenant;
    private readonly ICurrentUser            _currentUser;
    private readonly ILogger<AprobarOrdenCompraCommandHandler> _logger;

    public AprobarOrdenCompraCommandHandler(
        IOrdenCompraRepository ordenRepo,
        IProveedorRepository proveedorRepo,
        IUserActivityRepository activity,
        ICurrentTenant currentTenant,
        ICurrentUser currentUser,
        ILogger<AprobarOrdenCompraCommandHandler> logger)
    {
        _ordenRepo     = ordenRepo;
        _proveedorRepo = proveedorRepo;
        _activity      = activity;
        _currentTenant = currentTenant;
        _currentUser   = currentUser;
        _logger        = logger;
    }

    public async Task<Result<OrdenCompraDto>> Handle(
        AprobarOrdenCompraCommand command, CancellationToken ct)
    {
        var tenantId = _currentTenant.TenantId;
        var userId   = _currentUser.UserId;

        var orden = await _ordenRepo.GetByIdAsync(tenantId, command.OrdenId, ct);
        if (orden is null)
            return Result<OrdenCompraDto>.Failure("Orden de compra no encontrada.");

        if (orden.Estado is not ("Borrador" or "Enviada"))
            return Result<OrdenCompraDto>.Failure(
                $"Solo se puede aprobar una OC en Borrador o Enviada (estado actual: {orden.Estado}).");

        orden.Aprobar(userId);

        await _activity.AddAsync(UserActivity.Create(
            tenantId, userId, _currentUser.Email, _currentUser.FullName,
            module: "compras", action: "orden-compra.aprobar",
            entityType: "OrdenCompra", entityId: orden.Id,
            description: orden.NumeroOrden), ct);

        await _ordenRepo.SaveChangesAsync(ct);
        _logger.LogInformation("OC aprobada: {Numero}", orden.NumeroOrden);

        var proveedor = await _proveedorRepo.GetByIdAsync(tenantId, orden.ProveedorId, ct);
        return Result<OrdenCompraDto>.Success(
            CrearOrdenCompraCommandHandler.ToDto(orden, proveedor?.RazonSocial ?? orden.ProveedorId.ToString()));
    }
}

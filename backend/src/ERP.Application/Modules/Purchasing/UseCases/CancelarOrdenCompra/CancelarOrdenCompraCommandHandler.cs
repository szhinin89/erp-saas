using MediatR;
using Microsoft.Extensions.Logging;
using ERP.Application.Common;
using ERP.Application.Modules.Purchasing.DTOs;
using ERP.Application.Modules.Purchasing.UseCases.CrearOrdenCompra;
using ERP.Domain.Audit.Entities;
using ERP.Domain.Audit.Interfaces;
using ERP.Domain.Modules.Purchasing.Interfaces;
using ERP.Domain.Modules.Purchasing.Interfaces;

namespace ERP.Application.Modules.Purchasing.UseCases.CancelarOrdenCompra;

public sealed class CancelarOrdenCompraCommandHandler
    : IRequestHandler<CancelarOrdenCompraCommand, Result<OrdenCompraDto>>
{
    private readonly IPurchaseOrderRepository  _ordenRepo;
    private readonly ISupplierRepository    _proveedorRepo;
    private readonly IUserActivityRepository _activity;
    private readonly ICurrentTenant          _currentTenant;
    private readonly ICurrentUser            _currentUser;
    private readonly ILogger<CancelarOrdenCompraCommandHandler> _logger;

    public CancelarOrdenCompraCommandHandler(
        IPurchaseOrderRepository ordenRepo,
        ISupplierRepository proveedorRepo,
        IUserActivityRepository activity,
        ICurrentTenant currentTenant,
        ICurrentUser currentUser,
        ILogger<CancelarOrdenCompraCommandHandler> logger)
    {
        _ordenRepo     = ordenRepo;
        _proveedorRepo = proveedorRepo;
        _activity      = activity;
        _currentTenant = currentTenant;
        _currentUser   = currentUser;
        _logger        = logger;
    }

    public async Task<Result<OrdenCompraDto>> Handle(
        CancelarOrdenCompraCommand command, CancellationToken ct)
    {
        var tenantId = _currentTenant.TenantId;
        var userId   = _currentUser.UserId;

        var orden = await _ordenRepo.GetByIdAsync(tenantId, command.OrdenId, ct);
        if (orden is null)
            return Result<OrdenCompraDto>.Failure("Orden de compra no encontrada.");

        if (orden.Status is "Cerrada" or "Cancelada")
            return Result<OrdenCompraDto>.Failure(
                $"No se puede cancelar una OC en estado {orden.Status}.");

        orden.Cancel(userId);

        await _activity.AddAsync(UserActivity.Create(
            tenantId, userId, _currentUser.Email, _currentUser.FullName,
            module: "compras", action: "orden-compra.cancelar",
            entityType: "PurchaseOrder", entityId: orden.Id,
            description: orden.OrderNumber), ct);

        await _ordenRepo.SaveChangesAsync(ct);
        _logger.LogInformation("OC cancelada: {Numero}", orden.OrderNumber);

        var Supplier = await _proveedorRepo.GetByIdAsync(tenantId, orden.SupplierId, ct);
        return Result<OrdenCompraDto>.Success(
            CrearOrdenCompraCommandHandler.ToDto(orden, Supplier?.LegalName ?? orden.SupplierId.ToString()));
    }
}

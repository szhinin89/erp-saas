using MediatR;
using Microsoft.Extensions.Logging;
using ERP.Application.Common;
using ERP.Application.Modules.Purchasing.DTOs;
using ERP.Application.Modules.Purchasing.UseCases.CrearOrdenCompra;
using ERP.Domain.Audit.Entities;
using ERP.Domain.Audit.Interfaces;
using ERP.Domain.Modules.Purchasing.Interfaces;
using ERP.Domain.Modules.Purchasing.Interfaces;

namespace ERP.Application.Modules.Purchasing.UseCases.AprobarOrdenCompra;

public sealed class AprobarOrdenCompraCommandHandler
    : IRequestHandler<AprobarOrdenCompraCommand, Result<OrdenCompraDto>>
{
    private readonly IPurchaseOrderRepository  _ordenRepo;
    private readonly ISupplierRepository    _proveedorRepo;
    private readonly IUserActivityRepository _activity;
    private readonly ICurrentTenant          _currentTenant;
    private readonly ICurrentUser            _currentUser;
    private readonly ILogger<AprobarOrdenCompraCommandHandler> _logger;

    public AprobarOrdenCompraCommandHandler(
        IPurchaseOrderRepository ordenRepo,
        ISupplierRepository proveedorRepo,
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

        if (orden.Status is not ("Borrador" or "Enviada"))
            return Result<OrdenCompraDto>.Failure(
                $"Solo se puede aprobar una OC en Borrador o Enviada (estado actual: {orden.Status}).");

        orden.Approve(userId);

        await _activity.AddAsync(UserActivity.Create(
            tenantId, userId, _currentUser.Email, _currentUser.FullName,
            module: "compras", action: "orden-compra.aprobar",
            entityType: "PurchaseOrder", entityId: orden.Id,
            description: orden.OrderNumber), ct);

        await _ordenRepo.SaveChangesAsync(ct);
        _logger.LogInformation("OC aprobada: {Numero}", orden.OrderNumber);

        var Supplier = await _proveedorRepo.GetByIdAsync(tenantId, orden.SupplierId, ct);
        return Result<OrdenCompraDto>.Success(
            CrearOrdenCompraCommandHandler.ToDto(orden, Supplier?.LegalName ?? orden.SupplierId.ToString()));
    }
}

using MediatR;
using Microsoft.Extensions.Logging;
using ERP.Application.Common;
using ERP.Application.Modules.Purchasing.DTOs;
using ERP.Application.Modules.Purchasing.UseCases.CrearOrdenCompra;
using ERP.Domain.Audit.Entities;
using ERP.Domain.Audit.Interfaces;
using ERP.Domain.Modules.Purchasing.Interfaces;
using ERP.Domain.Modules.Purchasing.Interfaces;

namespace ERP.Application.Modules.Purchasing.UseCases.EnviarOrdenCompra;

public sealed class EnviarOrderPurchaseCommandHandler
    : IRequestHandler<EnviarOrderPurchaseCommand, Result<PurchaseOrderDto>>
{
    private readonly IPurchaseOrderRepository  _ordenRepo;
    private readonly ISupplierRepository    _proveedorRepo;
    private readonly IUserActivityRepository _activity;
    private readonly ICurrentTenant          _currentTenant;
    private readonly ICurrentUser            _currentUser;
    private readonly ILogger<EnviarOrderPurchaseCommandHandler> _logger;

    public EnviarOrderPurchaseCommandHandler(
        IPurchaseOrderRepository ordenRepo,
        ISupplierRepository proveedorRepo,
        IUserActivityRepository activity,
        ICurrentTenant currentTenant,
        ICurrentUser currentUser,
        ILogger<EnviarOrderPurchaseCommandHandler> logger)
    {
        _ordenRepo     = ordenRepo;
        _proveedorRepo = proveedorRepo;
        _activity      = activity;
        _currentTenant = currentTenant;
        _currentUser   = currentUser;
        _logger        = logger;
    }

    public async Task<Result<PurchaseOrderDto>> Handle(
        EnviarOrderPurchaseCommand command, CancellationToken ct)
    {
        var tenantId = _currentTenant.TenantId;
        var userId   = _currentUser.UserId;

        var orden = await _ordenRepo.GetByIdAsync(tenantId, command.OrdenId, ct);
        if (orden is null)
            return Result<PurchaseOrderDto>.Failure("Orden de compra no encontrada.");

        if (orden.Status != "Borrador")
            return Result<PurchaseOrderDto>.Failure(
                $"Solo se puede enviar una OC en Borrador (estado actual: {orden.Status}).");

        orden.Send(userId);

        await _activity.AddAsync(UserActivity.Create(
            tenantId, userId, _currentUser.Email, _currentUser.FullName,
            module: "compras", action: "orden-compra.enviar",
            entityType: "PurchaseOrder", entityId: orden.Id,
            description: orden.OrderNumber), ct);

        await _ordenRepo.SaveChangesAsync(ct);
        _logger.LogInformation("OC enviada: {Numero}", orden.OrderNumber);

        var Supplier = await _proveedorRepo.GetByIdAsync(tenantId, orden.SupplierId, ct);
        return Result<PurchaseOrderDto>.Success(
            CrearOrderPurchaseCommandHandler.ToDto(orden, Supplier?.LegalName ?? orden.SupplierId.ToString()));
    }
}

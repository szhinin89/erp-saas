using MediatR;
using Microsoft.Extensions.Logging;
using ERP.Application.Common;
using ERP.Application.Modules.Purchasing.DTOs;
using ERP.Application.Modules.Purchasing.UseCases.CreatePurchaseOrder;
using ERP.Domain.Audit.Entities;
using ERP.Domain.Audit.Interfaces;
using ERP.Domain.Modules.Purchasing.Interfaces;
using ERP.Domain.MasterData.Interfaces;
using ERP.Domain.MasterData.Interfaces;

namespace ERP.Application.Modules.Purchasing.UseCases.CancelPurchaseOrder;

public sealed class CancelOrderPurchaseCommandHandler
    : IRequestHandler<CancelOrderPurchaseCommand, Result<PurchaseOrderDto>>
{
    private readonly IPurchaseOrderRepository  _ordenRepo;
    private readonly IBusinessPartnerRepository _bpRepo;
    private readonly IUserActivityRepository _activity;
    private readonly ICurrentSubscriber          _currentSubscriber;
    private readonly ICurrentUser            _currentUser;
    private readonly ILogger<CancelOrderPurchaseCommandHandler> _logger;

    public CancelOrderPurchaseCommandHandler(
        IPurchaseOrderRepository ordenRepo,
        IBusinessPartnerRepository bpRepo,
        IUserActivityRepository activity,
        ICurrentSubscriber currentSubscriber,
        ICurrentUser currentUser,
        ILogger<CancelOrderPurchaseCommandHandler> logger)
    {
        _ordenRepo     = ordenRepo;
        _bpRepo = bpRepo;
        _activity      = activity;
        _currentSubscriber = currentSubscriber;
        _currentUser   = currentUser;
        _logger        = logger;
    }

    public async Task<Result<PurchaseOrderDto>> Handle(
        CancelOrderPurchaseCommand command, CancellationToken ct)
    {
        var subscriberId = _currentSubscriber.SubscriberId;
        var userId   = _currentUser.UserId;

        var orden = await _ordenRepo.GetByIdAsync(subscriberId, command.OrdenId, ct);
        if (orden is null)
            return Result<PurchaseOrderDto>.Failure("Orden de compra no encontrada.");

        if (orden.Status is "Closed" or "Cancelled")
            return Result<PurchaseOrderDto>.Failure(
                $"No se puede cancelar una OC en estado {orden.Status}.");

        orden.Cancel(userId);

        await _activity.AddAsync(UserActivity.Create(
            subscriberId, userId, _currentUser.Email, _currentUser.FullName,
            module: "compras", action: "orden-compra.cancelar",
            entityType: "PurchaseOrder", entityId: orden.Id,
            description: orden.OrderNumber), ct);

        await _ordenRepo.SaveChangesAsync(ct);
        _logger.LogInformation("OC cancelada: {Numero}", orden.OrderNumber);

        var bp = await _bpRepo.GetByIdAsync(orden.BusinessPartnerId, ct);
        return Result<PurchaseOrderDto>.Success(
            CreatePurchaseOrderCommandHandler.ToDto(orden, bp?.LegalName ?? orden.BusinessPartnerId.ToString()));
    }
}

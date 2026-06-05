using MediatR;
using Microsoft.Extensions.Logging;
using ERP.Application.Common;
using ERP.Application.Modules.Purchasing.DTOs;
using ERP.Application.Modules.Purchasing.UseCases.CreatePurchaseOrder;
using ERP.Domain.Audit.Entities;
using ERP.Domain.Audit.Interfaces;
using ERP.Domain.MasterData.Interfaces;
using ERP.Domain.Modules.Purchasing.Interfaces;

namespace ERP.Application.Modules.Purchasing.UseCases.ApprovePurchaseOrder;

public sealed class ApproveOrderPurchaseCommandHandler
    : IRequestHandler<ApprovePurchaseOrderCommand, Result<PurchaseOrderDto>>
{
    private readonly IPurchaseOrderRepository  _ordenRepo;
    private readonly IBusinessPartnerRepository _bpRepo;
    private readonly IUserActivityRepository _activity;
    private readonly ICurrentSubscriber          _currentSubscriber;
    private readonly ICurrentUser            _currentUser;
    private readonly ILogger<ApproveOrderPurchaseCommandHandler> _logger;

    public ApproveOrderPurchaseCommandHandler(
        IPurchaseOrderRepository ordenRepo,
        IBusinessPartnerRepository bpRepo,
        IUserActivityRepository activity,
        ICurrentSubscriber currentSubscriber,
        ICurrentUser currentUser,
        ILogger<ApproveOrderPurchaseCommandHandler> logger)
    {
        _ordenRepo = ordenRepo;
        _bpRepo    = bpRepo;
        _activity      = activity;
        _currentSubscriber = currentSubscriber;
        _currentUser   = currentUser;
        _logger        = logger;
    }

    public async Task<Result<PurchaseOrderDto>> Handle(
        ApprovePurchaseOrderCommand command, CancellationToken ct)
    {
        var subscriberId = _currentSubscriber.SubscriberId;
        var userId   = _currentUser.UserId;

        var orden = await _ordenRepo.GetByIdAsync(subscriberId, command.OrderId, ct);
        if (orden is null)
            return Result<PurchaseOrderDto>.Failure("Orden de compra no encontrada.");

        if (orden.Status is not ("Draft" or "Sent"))
            return Result<PurchaseOrderDto>.Failure(
                $"Solo se puede aprobar una OC en Draft o Sent (estado actual: {orden.Status}).");

        orden.Approve(userId);

        await _activity.AddAsync(UserActivity.Create(
            subscriberId, userId, _currentUser.Email, _currentUser.FullName,
            module: "compras", action: "orden-compra.aprobar",
            entityType: "PurchaseOrder", entityId: orden.Id,
            description: orden.OrderNumber), ct);

        await _ordenRepo.SaveChangesAsync(ct);
        _logger.LogInformation("OC aprobada: {Numero}", orden.OrderNumber);

        var bp = await _bpRepo.GetByIdAsync(orden.BusinessPartnerId, ct);
        return Result<PurchaseOrderDto>.Success(
            CreatePurchaseOrderCommandHandler.ToDto(orden, bp?.Name.LegalName ?? orden.BusinessPartnerId.ToString()));
    }
}


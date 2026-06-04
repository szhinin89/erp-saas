using MediatR;
using ERP.Application.Common;
using ERP.Application.Modules.Purchasing.DTOs;
using ERP.Application.Modules.Purchasing.UseCases.CreatePurchaseOrder;
using ERP.Domain.Modules.Purchasing.Interfaces;
using ERP.Domain.MasterData.Interfaces;
using ERP.Domain.MasterData.Interfaces;

namespace ERP.Application.Modules.Purchasing.UseCases.GetOrdersPendingBilling;

public sealed class GetOrdersPendingBillingQueryHandler
    : IRequestHandler<GetOrdersPendingBillingQuery, Result<IReadOnlyList<PurchaseOrderDto>>>
{
    private readonly IPurchaseOrderRepository _repo;
    private readonly IBusinessPartnerRepository _bpRepo;
    private readonly ICurrentSubscriber         _currentSubscriber;

    public GetOrdersPendingBillingQueryHandler(
        IPurchaseOrderRepository repo,
        IBusinessPartnerRepository bpRepo,
        ICurrentSubscriber currentSubscriber)
    {
        _repo          = repo;
        _bpRepo = bpRepo;
        _currentSubscriber = currentSubscriber;
    }

    public async Task<Result<IReadOnlyList<PurchaseOrderDto>>> Handle(
        GetOrdersPendingBillingQuery query, CancellationToken ct)
    {
        var subscriberId = _currentSubscriber.SubscriberId;
        var ordenes  = await _repo.GetPendingToInvoiceAsync(subscriberId, ct);

        var proveedorIds = ordenes.Select(o => o.BusinessPartnerId).Distinct().ToList();
        var proveedores  = new Dictionary<Guid, string>();
        foreach (var pid in proveedorIds)
        {
            var p = await _bpRepo.GetByIdAsync(pid, ct);
            proveedores[pid] = p?.Name.LegalName ?? pid.ToString();
        }

        var dtos = ordenes
            .Select(o => CreatePurchaseOrderCommandHandler.ToDto(o, proveedores.GetValueOrDefault(o.BusinessPartnerId, "")))
            .ToList();

        return Result<IReadOnlyList<PurchaseOrderDto>>.Success(dtos);
    }
}


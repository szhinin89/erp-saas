using MediatR;
using ERP.Application.Common;
using ERP.Application.Modules.Purchasing.DTOs;
using ERP.Application.Modules.Purchasing.UseCases.CrearOrdenCompra;
using ERP.Domain.Modules.Purchasing.Interfaces;
using ERP.Domain.MasterData.Interfaces;
using ERP.Domain.MasterData.Interfaces;

namespace ERP.Application.Modules.Purchasing.UseCases.GetOrdenesCompraList;

public sealed class GetPurchaseOrdersListQueryHandler
    : IRequestHandler<GetPurchaseOrdersListQuery, Result<PurchaseOrdersPagedResult>>
{
    private readonly IPurchaseOrderRepository _repo;
    private readonly IBusinessPartnerRepository _bpRepo;
    private readonly ICurrentSubscriber         _currentSubscriber;

    public GetPurchaseOrdersListQueryHandler(
        IPurchaseOrderRepository repo,
        IBusinessPartnerRepository bpRepo,
        ICurrentSubscriber currentSubscriber)
    {
        _repo          = repo;
        _bpRepo = bpRepo;
        _currentSubscriber = currentSubscriber;
    }

    public async Task<Result<PurchaseOrdersPagedResult>> Handle(
        GetPurchaseOrdersListQuery query, CancellationToken ct)
    {
        var subscriberId = _currentSubscriber.SubscriberId;

        var (items, total) = await _repo.GetPagedAsync(
            subscriberId, query.PageNumber, query.PageSize,
            query.BusinessPartnerId, query.Status, query.DateFrom, query.DateTo, ct);

        // Batch de proveedores Ãºnicos para evitar N+1
        var proveedorIds = items.Select(o => o.BusinessPartnerId).Distinct().ToList();
        var proveedores  = new Dictionary<Guid, string>();
        foreach (var pid in proveedorIds)
        {
            var p = await _bpRepo.GetByIdAsync(pid, ct);
            proveedores[pid] = p?.LegalName ?? pid.ToString();
        }

        var dtos = items.Select(o =>
            CreatePurchaseOrderCommandHandler.ToDto(o, proveedores.GetValueOrDefault(o.BusinessPartnerId, ""))).ToList();

        return Result<PurchaseOrdersPagedResult>.Success(
            new PurchaseOrdersPagedResult(dtos, total, query.PageNumber, query.PageSize));
    }
}

using MediatR;
using ERP.Application.Common;
using ERP.Application.Modules.Purchasing.DTOs;
using ERP.Application.Modules.Purchasing.UseCases.CrearOrdenCompra;
using ERP.Domain.Modules.Purchasing.Interfaces;
using ERP.Domain.Modules.Purchasing.Interfaces;

namespace ERP.Application.Modules.Purchasing.UseCases.GetOrdenesPendientesPorFacturar;

public sealed class GetOrdersPendingBillingQueryHandler
    : IRequestHandler<GetOrdersPendingBillingQuery, Result<IReadOnlyList<PurchaseOrderDto>>>
{
    private readonly IPurchaseOrderRepository _repo;
    private readonly ISupplierRepository   _proveedorRepo;
    private readonly ICurrentSubscriber         _currentSubscriber;

    public GetOrdersPendingBillingQueryHandler(
        IPurchaseOrderRepository repo,
        ISupplierRepository proveedorRepo,
        ICurrentSubscriber currentSubscriber)
    {
        _repo          = repo;
        _proveedorRepo = proveedorRepo;
        _currentSubscriber = currentSubscriber;
    }

    public async Task<Result<IReadOnlyList<PurchaseOrderDto>>> Handle(
        GetOrdersPendingBillingQuery query, CancellationToken ct)
    {
        var subscriberId = _currentSubscriber.SubscriberId;
        var ordenes  = await _repo.GetPendingToInvoiceAsync(subscriberId, ct);

        var proveedorIds = ordenes.Select(o => o.SupplierId).Distinct().ToList();
        var proveedores  = new Dictionary<Guid, string>();
        foreach (var pid in proveedorIds)
        {
            var p = await _proveedorRepo.GetByIdAsync(subscriberId, pid, ct);
            proveedores[pid] = p?.LegalName ?? pid.ToString();
        }

        var dtos = ordenes
            .Select(o => CreatePurchaseOrderCommandHandler.ToDto(o, proveedores.GetValueOrDefault(o.SupplierId, "")))
            .ToList();

        return Result<IReadOnlyList<PurchaseOrderDto>>.Success(dtos);
    }
}

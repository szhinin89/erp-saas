using MediatR;
using ERP.Application.Common;
using ERP.Application.Modules.Purchasing.DTOs;
using ERP.Application.Modules.Purchasing.UseCases.CreatePurchaseOrder;
using ERP.Domain.Modules.Purchasing.Interfaces;
using ERP.Domain.MasterData.Interfaces;
using ERP.Domain.MasterData.Interfaces;

namespace ERP.Application.Modules.Purchasing.UseCases.GetPurchaseOrderById;

public sealed class GetPurchaseOrderByIdQueryHandler
    : IRequestHandler<GetPurchaseOrderByIdQuery, Result<PurchaseOrderDetailDto?>>
{
    private readonly IPurchaseOrderRepository _repo;
    private readonly IBusinessPartnerRepository _bpRepo;
    private readonly ICurrentSubscriber         _currentSubscriber;

    public GetPurchaseOrderByIdQueryHandler(
        IPurchaseOrderRepository repo,
        IBusinessPartnerRepository bpRepo,
        ICurrentSubscriber currentSubscriber)
    {
        _repo          = repo;
        _bpRepo = bpRepo;
        _currentSubscriber = currentSubscriber;
    }

    public async Task<Result<PurchaseOrderDetailDto?>> Handle(
        GetPurchaseOrderByIdQuery query, CancellationToken ct)
    {
        var subscriberId = _currentSubscriber.SubscriberId;

        var orden = await _repo.GetByIdAsync(subscriberId, query.OrderId, ct);
        if (orden is null)
            return Result<PurchaseOrderDetailDto?>.Success(null);

        var bp = await _bpRepo.GetByIdAsync(orden.BusinessPartnerId, ct);
        var vinculadas  = await _repo.GetBillLinksAsync(subscriberId, orden.Id, ct);

        var lines = orden.Lines.Select(d => new PurchaseOrderLineDto(
            d.Id, d.ProductId, d.Description,
            d.OrderedQty, d.InvoicedQty, d.PendingToInvoice,
            d.UnitCost, d.Subtotal, d.TaxAmount, d.Total)).ToList();

        var linkedBills = vinculadas
            .Select(v => new LinkedPurchaseBillDto(v.PurchBillId, v.InvoiceNumber, v.LinkedAt))
            .ToList();

        return Result<PurchaseOrderDetailDto?>.Success(new PurchaseOrderDetailDto(
            orden.Id, orden.OrderNumber,
            orden.BusinessPartnerId, bp?.Name.LegalName ?? orden.BusinessPartnerId.ToString(),
            orden.IssueDate, orden.RequiredDate,
            orden.Status, orden.Currency,
            orden.Subtotal, orden.TaxTotal, orden.Total,
            orden.Notes, orden.DeliveryAddress,
            orden.TargetWarehouseId,
            orden.SentAt, orden.ApprovedAt, orden.ApprovedBy, orden.ClosedAt,
            orden.CreatedAt,
            lines, linkedBills));
    }
}




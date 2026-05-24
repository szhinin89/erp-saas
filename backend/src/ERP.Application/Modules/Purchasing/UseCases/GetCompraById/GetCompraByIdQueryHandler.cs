using MediatR;
using ERP.Application.Common;
using ERP.Application.Modules.Purchasing.DTOs;
using ERP.Domain.Modules.Purchasing.Interfaces;

namespace ERP.Application.Modules.Purchasing.UseCases.GetCompraById;

public sealed class GetPurchaseByIdQueryHandler
    : IRequestHandler<GetPurchaseByIdQuery, Result<PurchBillDetailDto?>>
{
    private readonly IPurchBillRepository _repo;
    private readonly ICurrentSubscriber    _tenant;

    public GetPurchaseByIdQueryHandler(IPurchBillRepository repo, ICurrentSubscriber tenant)
    {
        _repo   = repo;
        _tenant = tenant;
    }

    public async Task<Result<PurchBillDetailDto?>> Handle(
        GetPurchaseByIdQuery query, CancellationToken ct)
    {
        var c = await _repo.GetByIdAsync(_tenant.SubscriberId, query.Id, ct);
        if (c is null) return Result<PurchBillDetailDto?>.Success(null);

        var detalles = c.Lines.Select(d => new PurchaseBillLineDto(
            d.Id, d.ProductId, d.Description, d.SupplierProductCode,
            d.Quantity, d.UnitPrice, d.DiscountPct,
            d.Subtotal, d.VatPct, d.VatAmount, d.Total)).ToList();

        return Result<PurchBillDetailDto?>.Success(new PurchBillDetailDto(
            c.Id, c.BusinessPartnerId, c.InvoiceNumber, c.AccessKey, c.XmlPath,
            c.InvoiceDate, c.DueDate, c.Status, c.PaymentTerms,
            c.Subtotal, c.VatTotal, c.Total, c.Notes,
            c.ValidatedBy, c.ValidatedAt, c.ApprovedBy, c.ApprovedAt,
            c.RejectedBy, c.RejectedAt, c.RejectionReason, c.JournalEntryId,
            c.CreatedAt, detalles));
    }
}

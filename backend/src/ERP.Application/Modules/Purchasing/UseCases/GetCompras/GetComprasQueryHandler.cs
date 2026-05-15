using MediatR;
using ERP.Application.Common;
using ERP.Application.Modules.Purchasing.DTOs;
using ERP.Domain.Modules.Purchasing.Interfaces;

namespace ERP.Application.Modules.Purchasing.UseCases.GetCompras;

public sealed class GetPurchasesQueryHandler
    : IRequestHandler<GetPurchasesQuery, Result<IReadOnlyList<PurchBillDto>>>
{
    private readonly IPurchBillRepository _repo;
    private readonly ICurrentTenant    _tenant;

    public GetPurchasesQueryHandler(IPurchBillRepository repo, ICurrentTenant tenant)
    {
        _repo   = repo;
        _tenant = tenant;
    }

    public async Task<Result<IReadOnlyList<PurchBillDto>>> Handle(
        GetPurchasesQuery query, CancellationToken ct)
    {
        var list = await _repo.GetAsync(
            _tenant.TenantId, query.Status, query.SupplierId,
            query.DateFrom, query.DateTo, query.Search, ct);

        var dtos = list.Select(ToDto).ToList();
        return Result<IReadOnlyList<PurchBillDto>>.Success(dtos);
    }

    private static PurchBillDto ToDto(ERP.Domain.Modules.Purchasing.Entities.PurchBill c) => new(
        c.Id, c.SupplierId, c.InvoiceNumber, c.AccessKey, c.XmlPath,
        c.InvoiceDate, c.DueDate, c.Status, c.PaymentTerms,
        c.Subtotal, c.VatTotal, c.Total, c.Notes, c.JournalEntryId, c.CreatedAt);
}

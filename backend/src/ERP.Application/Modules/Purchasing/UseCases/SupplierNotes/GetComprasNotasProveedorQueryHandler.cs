using MediatR;
using ERP.Application.Common;
using ERP.Application.Common.Interfaces;
using ERP.Application.Modules.Purchasing.DTOs;
using ERP.Domain.Modules.Purchasing.Interfaces;

namespace ERP.Application.Modules.Purchasing.UseCases.NotasProveedor;

public sealed class GetPurchasesNotesSupplierQueryHandler
    : IRequestHandler<GetPurchaseSupplierNotesQuery, Result<IReadOnlyList<SupplierPurchaseNoteDto>>>
{
    private readonly IPurchBillRepository _repo;
    private readonly ICurrentTenant    _tenant;

    public GetPurchasesNotesSupplierQueryHandler(IPurchBillRepository repo, ICurrentTenant tenant)
    {
        _repo   = repo;
        _tenant = tenant;
    }

    public async Task<Result<IReadOnlyList<SupplierPurchaseNoteDto>>> Handle(
        GetPurchaseSupplierNotesQuery query,
        CancellationToken ct)
    {
        var list = await _repo.GetPurchNotesAsync(
            _tenant.TenantId,
            query.SupplierId,
            query.PurchBillId,
            query.ExpenseInvoiceId,
            query.Status,
            ct);

        IReadOnlyList<SupplierPurchaseNoteDto> dtos = list.Select(n => new SupplierPurchaseNoteDto(
            n.Id,
            n.SupplierId,
            n.PurchBillId,
            n.ExpenseInvoiceId,
            n.NoteType,
            n.Reason,
            n.AccessKey,
            n.IssueDate,
            n.EstabCode,
            n.EmPointCode,
            n.Sequential,
            n.Subtotal,
            n.VatTotal,
            n.Total,
            n.Status,
            n.XmlPath,
            n.JournalEntryId,
            n.CreatedAt)).ToList();

        return Result<IReadOnlyList<SupplierPurchaseNoteDto>>.Success(dtos);
    }
}

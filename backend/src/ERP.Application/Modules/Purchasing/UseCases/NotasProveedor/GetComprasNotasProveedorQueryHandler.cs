using MediatR;
using ERP.Application.Common;
using ERP.Application.Common.Interfaces;
using ERP.Application.Modules.Purchasing.DTOs;
using ERP.Domain.Modules.Purchasing.Interfaces;

namespace ERP.Application.Modules.Purchasing.UseCases.NotasProveedor;

public sealed class GetComprasNotasProveedorQueryHandler
    : IRequestHandler<GetComprasNotasProveedorQuery, Result<IReadOnlyList<CompraNotaProveedorDto>>>
{
    private readonly IPurchBillRepository _repo;
    private readonly ICurrentTenant    _tenant;

    public GetComprasNotasProveedorQueryHandler(IPurchBillRepository repo, ICurrentTenant tenant)
    {
        _repo   = repo;
        _tenant = tenant;
    }

    public async Task<Result<IReadOnlyList<CompraNotaProveedorDto>>> Handle(
        GetComprasNotasProveedorQuery query,
        CancellationToken ct)
    {
        var list = await _repo.GetPurchNotesAsync(
            _tenant.TenantId,
            query.SupplierId,
            query.PurchBillId,
            query.ExpenseInvoiceId,
            query.Status,
            ct);

        IReadOnlyList<CompraNotaProveedorDto> dtos = list.Select(n => new CompraNotaProveedorDto(
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

        return Result<IReadOnlyList<CompraNotaProveedorDto>>.Success(dtos);
    }
}

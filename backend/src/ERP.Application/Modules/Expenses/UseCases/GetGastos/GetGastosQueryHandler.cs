using MediatR;
using ERP.Application.Common;
using ERP.Application.Modules.Expenses.DTOs;
using ERP.Domain.Modules.Expenses.Entities;
using ERP.Domain.Modules.Expenses.Interfaces;

namespace ERP.Application.Modules.Expenses.UseCases.GetGastos;

public sealed class GetGastosQueryHandler
    : IRequestHandler<GetGastosQuery, Result<IReadOnlyList<ExpenseInvoiceDto>>>
{
    private readonly IExpenseInvoiceRepository _repo;
    private readonly ICurrentTenant        _tenant;

    public GetGastosQueryHandler(IExpenseInvoiceRepository repo, ICurrentTenant tenant)
    {
        _repo   = repo;
        _tenant = tenant;
    }

    public async Task<Result<IReadOnlyList<ExpenseInvoiceDto>>> Handle(
        GetGastosQuery query,
        CancellationToken ct)
    {
        var list = await _repo.GetAsync(
            _tenant.TenantId,
            query.Status,
            query.SupplierId,
            query.DateFrom,
            query.DateTo,
            query.Search,
            ct);

        var dtos = list.Select(ToDto).ToList();
        return Result<IReadOnlyList<ExpenseInvoiceDto>>.Success(dtos);
    }

    private static ExpenseInvoiceDto ToDto(ExpenseInvoice g) => new(
        g.Id,
        g.AccessKey,
        g.IssueDate,
        g.SupplierId,
        g.InvoiceNumber,
        g.Concept,
        g.Category,
        g.Subtotal,
        g.TaxTotal,
        g.Total,
        g.Status,
        g.XmlPath,
        g.Notes,
        g.JournalEntryId,
        g.CreatedAt);
}

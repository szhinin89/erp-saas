using MediatR;
using ERP.Application.Common;
using ERP.Application.Modules.Expenses.DTOs;
using ERP.Domain.Modules.Expenses.Entities;
using ERP.Domain.Modules.Expenses.Interfaces;

namespace ERP.Application.Modules.Expenses.UseCases.GetGastos;

public sealed class GetExpensesQueryHandler
    : IRequestHandler<GetExpensesQuery, Result<IReadOnlyList<ExpenseInvoiceDto>>>
{
    private readonly IExpenseInvoiceRepository _repo;
    private readonly ICurrentSubscriber        _subscriber;

    public GetExpensesQueryHandler(IExpenseInvoiceRepository repo, ICurrentSubscriber subscriber)
    {
        _repo   = repo;
        _subscriber = subscriber;
    }

    public async Task<Result<IReadOnlyList<ExpenseInvoiceDto>>> Handle(
        GetExpensesQuery query,
        CancellationToken ct)
    {
        var list = await _repo.GetAsync(
            _subscriber.SubscriberId,
            query.Status,
            query.BusinessPartnerId,
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
        g.BusinessPartnerId,
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

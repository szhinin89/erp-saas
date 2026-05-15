using MediatR;
using ERP.Application.Common;
using ERP.Application.Modules.Expenses.DTOs;
using ERP.Domain.Modules.Expenses.Entities;
using ERP.Domain.Modules.Expenses.Interfaces;

namespace ERP.Application.Modules.Expenses.UseCases.GetGastoById;

public sealed class GetGastoByIdQueryHandler
    : IRequestHandler<GetGastoByIdQuery, Result<ExpenseInvoiceDto?>>
{
    private readonly IExpenseInvoiceRepository _repo;
    private readonly ICurrentTenant        _tenant;

    public GetGastoByIdQueryHandler(IExpenseInvoiceRepository repo, ICurrentTenant tenant)
    {
        _repo   = repo;
        _tenant = tenant;
    }

    public async Task<Result<ExpenseInvoiceDto?>> Handle(GetGastoByIdQuery query, CancellationToken ct)
    {
        var g = await _repo.GetByIdAsync(_tenant.TenantId, query.Id, ct);
        if (g is null)
            return Result<ExpenseInvoiceDto?>.Success(null);

        return Result<ExpenseInvoiceDto?>.Success(ToDto(g));
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

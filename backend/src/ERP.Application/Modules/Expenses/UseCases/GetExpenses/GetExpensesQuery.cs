using MediatR;
using ERP.Application.Common;

using ERP.Application.Modules.Expenses.DTOs;
using ERP.Domain.Modules.Expenses.Enums;

namespace ERP.Application.Modules.Expenses.UseCases.GetExpenses;

[RequireFeature(SubscriptionFeatureCodes.Expenses)]
public record GetExpensesQuery(
    ExpenseStatus? Status,
    Guid?        BusinessPartnerId,
    DateTime?    DateFrom,
    DateTime?    DateTo,
    string?      Search
) : IRequest<Result<IReadOnlyList<ExpenseInvoiceDto>>>, ICompanyScopedRequest;

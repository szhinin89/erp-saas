using MediatR;
using ERP.Application.Common;

using ERP.Application.Modules.Expenses.DTOs;

namespace ERP.Application.Modules.Expenses.UseCases.ValidateExpense;

[RequireFeature(SubscriptionFeatureCodes.Gastos)]
public sealed record ValidateExpenseCommand(Guid ExpenseInvoiceId)
    : IRequest<Result<ExpenseInvoiceDto>>, ICompanyScopedRequest;

using MediatR;
using ERP.Application.Common;

using ERP.Application.Modules.Expenses.DTOs;

namespace ERP.Application.Modules.Expenses.UseCases.RejectExpense;

[RequireFeature(SubscriptionFeatureCodes.Expenses)]
public sealed record RejectExpenseCommand(Guid ExpenseInvoiceId, string  Reason)
    : IRequest<Result<ExpenseInvoiceDto>>, ICompanyScopedRequest;

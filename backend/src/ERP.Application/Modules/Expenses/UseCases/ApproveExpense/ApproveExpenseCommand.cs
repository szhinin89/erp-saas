using MediatR;
using ERP.Application.Common;

using ERP.Application.Modules.Expenses.DTOs;

namespace ERP.Application.Modules.Expenses.UseCases.ApproveExpense;

[RequireFeature(SubscriptionFeatureCodes.Expenses)]
public sealed record ApproveExpenseCommand(Guid ExpenseInvoiceId)
    : IRequest<Result<ExpenseInvoiceDto>>, ICompanyScopedRequest;

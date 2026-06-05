using MediatR;
using ERP.Application.Common;

using ERP.Application.Modules.Expenses.DTOs;

namespace ERP.Application.Modules.Expenses.UseCases.GetExpenseById;

[RequireFeature(SubscriptionFeatureCodes.Expenses)]
public sealed record GetExpenseByIdQuery(Guid Id)
    : IRequest<Result<ExpenseInvoiceDto?>>, ICompanyScopedRequest;

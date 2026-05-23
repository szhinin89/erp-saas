using MediatR;
using ERP.Application.Common;

using ERP.Application.Modules.Expenses.DTOs;

namespace ERP.Application.Modules.Expenses.UseCases.GetGastoById;

[RequireFeature(SubscriptionFeatureCodes.Gastos)]
public sealed record GetExpenseByIdQuery(Guid Id)
    : IRequest<Result<ExpenseInvoiceDto?>>, ICompanyScopedRequest;

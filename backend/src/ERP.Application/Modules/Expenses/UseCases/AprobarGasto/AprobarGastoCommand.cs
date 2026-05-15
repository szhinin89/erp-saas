using MediatR;
using ERP.Application.Common;

using ERP.Application.Modules.Expenses.DTOs;

namespace ERP.Application.Modules.Expenses.UseCases.AprobarGasto;

[RequireFeature(SubscriptionFeatureCodes.Gastos)]
public sealed record AprobarGastoCommand(Guid ExpenseInvoiceId)
    : IRequest<Result<ExpenseInvoiceDto>>;

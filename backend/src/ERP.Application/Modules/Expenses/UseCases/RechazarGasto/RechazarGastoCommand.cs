using MediatR;
using ERP.Application.Common;

using ERP.Application.Modules.Expenses.DTOs;

namespace ERP.Application.Modules.Expenses.UseCases.RechazarGasto;

[RequireFeature(SubscriptionFeatureCodes.Gastos)]
public sealed record RechazarGastoCommand(Guid ExpenseInvoiceId, string  Reason)
    : IRequest<Result<ExpenseInvoiceDto>>;

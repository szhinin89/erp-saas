using ERP.Application.Billing.DTOs;
using ERP.Application.Common;
using MediatR;

namespace ERP.Application.Billing.UseCases.ListBillingEvents;

public sealed record ListBillingEventsQuery(int Take = 100) : IRequest<Result<IReadOnlyList<BillingEventDto>>>;

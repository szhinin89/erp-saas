using ERP.Application.Billing.DTOs;
using ERP.Application.Common;
using MediatR;

namespace ERP.Application.Billing.UseCases.GetBillingAccount;

public sealed record GetBillingAccountQuery : IRequest<Result<SubscriberBillingAccountDto>>;

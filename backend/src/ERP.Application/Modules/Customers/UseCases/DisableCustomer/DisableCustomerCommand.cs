using MediatR;
using ERP.Application.Common;
using ERP.Application.Modules.Customers.DTOs;

namespace ERP.Application.Modules.Customers.UseCases.DisableCustomer;

[RequireFeature(SubscriptionFeatureCodes.Customers)]
public sealed record DisableCustomerCommand(Guid Id) : IRequest<Result<CustomerDto>>;

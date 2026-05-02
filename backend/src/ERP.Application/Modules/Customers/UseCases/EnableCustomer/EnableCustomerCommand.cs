using MediatR;
using ERP.Application.Common;
using ERP.Application.Modules.Customers.DTOs;

namespace ERP.Application.Modules.Customers.UseCases.EnableCustomer;

[RequireFeature(SubscriptionFeatureCodes.Customers)]
public sealed record EnableCustomerCommand(Guid Id) : IRequest<Result<CustomerDto>>;

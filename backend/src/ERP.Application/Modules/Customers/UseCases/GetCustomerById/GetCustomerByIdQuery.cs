using MediatR;
using ERP.Application.Common;
using ERP.Application.Modules.Customers.DTOs;

namespace ERP.Application.Modules.Customers.UseCases.GetCustomerById;

[RequireFeature(SubscriptionFeatureCodes.Customers)]
public sealed record GetCustomerByIdQuery(Guid Id) : IRequest<Result<CustomerDetailDto>>;

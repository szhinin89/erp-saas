using MediatR;
using ERP.Application.Common;
using ERP.Application.Modules.Customers.DTOs;

namespace ERP.Application.Modules.Customers.UseCases.GetCustomers;

[RequireFeature(SubscriptionFeatureCodes.Customers)]
public sealed record GetCustomersQuery(bool? ActiveFilter, string? Search)
    : IRequest<Result<IReadOnlyList<CustomerDto>>>;

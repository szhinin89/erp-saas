using MediatR;
using ERP.Application.Common;
using ERP.Application.Modules.Ventas.DTOs;

namespace ERP.Application.Modules.Ventas.UseCases.ListarClientes;

[RequireFeature(SubscriptionFeatureCodes.Customers)]
public sealed record GetCustomersQuery(bool? ActiveFilter, string? Search)
    : IRequest<Result<IReadOnlyList<CustomerDto>>>;

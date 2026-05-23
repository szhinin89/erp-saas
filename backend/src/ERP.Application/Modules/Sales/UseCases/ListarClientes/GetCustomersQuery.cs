using MediatR;
using ERP.Application.Common;
using ERP.Application.Modules.Sales.DTOs;

namespace ERP.Application.Modules.Sales.UseCases.ListarClientes;

[RequireFeature(SubscriptionFeatureCodes.Sales)]
public sealed record GetCustomersQuery(bool? ActiveFilter, string? Search)
    : IRequest<Result<IReadOnlyList<CustomerDto>>>, ICompanyScopedRequest;

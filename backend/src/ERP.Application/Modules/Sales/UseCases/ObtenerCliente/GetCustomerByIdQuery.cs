using MediatR;
using ERP.Application.Common;
using ERP.Application.Modules.Sales.DTOs;

namespace ERP.Application.Modules.Sales.UseCases.ObtenerCliente;

[RequireFeature(SubscriptionFeatureCodes.Customers)]
public sealed record GetCustomerByIdQuery(Guid Id) : IRequest<Result<CustomerDetailDto>>;

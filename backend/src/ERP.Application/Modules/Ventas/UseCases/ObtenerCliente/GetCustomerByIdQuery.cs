using MediatR;
using ERP.Application.Common;
using ERP.Application.Modules.Ventas.DTOs;

namespace ERP.Application.Modules.Ventas.UseCases.ObtenerCliente;

[RequireFeature(SubscriptionFeatureCodes.Customers)]
public sealed record GetCustomerByIdQuery(Guid Id) : IRequest<Result<CustomerDetailDto>>;

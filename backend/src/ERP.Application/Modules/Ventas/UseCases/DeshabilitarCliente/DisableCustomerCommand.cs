using MediatR;
using ERP.Application.Common;
using ERP.Application.Modules.Ventas.DTOs;

namespace ERP.Application.Modules.Ventas.UseCases.DeshabilitarCliente;

[RequireFeature(SubscriptionFeatureCodes.Customers)]
public sealed record DisableCustomerCommand(Guid Id) : IRequest<Result<CustomerDto>>;

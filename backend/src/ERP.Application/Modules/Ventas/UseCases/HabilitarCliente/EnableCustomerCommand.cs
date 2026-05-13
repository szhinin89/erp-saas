using MediatR;
using ERP.Application.Common;
using ERP.Application.Modules.Ventas.DTOs;

namespace ERP.Application.Modules.Ventas.UseCases.HabilitarCliente;

[RequireFeature(SubscriptionFeatureCodes.Customers)]
public sealed record EnableCustomerCommand(Guid Id) : IRequest<Result<CustomerDto>>;

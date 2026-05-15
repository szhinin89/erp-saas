using MediatR;
using ERP.Application.Common;
using ERP.Application.Modules.Sales.DTOs;

namespace ERP.Application.Modules.Sales.UseCases.HabilitarCliente;

[RequireFeature(SubscriptionFeatureCodes.Customers)]
public sealed record EnableCustomerCommand(Guid Id) : IRequest<Result<CustomerDto>>;

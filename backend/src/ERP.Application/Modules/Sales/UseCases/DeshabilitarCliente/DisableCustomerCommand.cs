using MediatR;
using ERP.Application.Common;
using ERP.Application.Modules.Sales.DTOs;

namespace ERP.Application.Modules.Sales.UseCases.DeshabilitarCliente;

[RequireFeature(SubscriptionFeatureCodes.Sales)]
public sealed record DisableCustomerCommand(Guid Id) : IRequest<Result<CustomerDto>>;

using MediatR;
using ERP.Application.Common;
using ERP.Application.Modules.Purchasing.DTOs;

namespace ERP.Application.Modules.Purchasing.UseCases.DeshabilitarProveedor;

[RequireFeature(SubscriptionFeatureCodes.Inventory)]
public sealed record DisableProveedorCommand(Guid Id)
    : IRequest<Result<ProveedorDto>>;

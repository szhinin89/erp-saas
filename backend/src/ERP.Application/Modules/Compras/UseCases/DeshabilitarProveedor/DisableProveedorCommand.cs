using MediatR;
using ERP.Application.Common;
using ERP.Application.Modules.Compras.DTOs;

namespace ERP.Application.Modules.Compras.UseCases.DeshabilitarProveedor;

[RequireFeature(SubscriptionFeatureCodes.Inventory)]
public sealed record DisableProveedorCommand(Guid Id)
    : IRequest<Result<ProveedorDto>>;

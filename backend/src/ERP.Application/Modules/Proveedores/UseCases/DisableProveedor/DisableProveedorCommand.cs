using MediatR;
using ERP.Application.Common;
using ERP.Application.Modules.Proveedores.DTOs;

namespace ERP.Application.Modules.Proveedores.UseCases.DisableProveedor;

[RequireFeature(SubscriptionFeatureCodes.Inventory)]
public sealed record DisableProveedorCommand(Guid Id)
    : IRequest<Result<ProveedorDto>>;

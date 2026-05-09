using MediatR;
using ERP.Application.Common;
using ERP.Application.Modules.Proveedores.DTOs;

namespace ERP.Application.Modules.Proveedores.UseCases.EnableProveedor;

[RequireFeature(SubscriptionFeatureCodes.Inventory)]
public sealed record EnableProveedorCommand(Guid Id)
    : IRequest<Result<ProveedorDto>>;

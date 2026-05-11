using MediatR;
using ERP.Application.Common;
using ERP.Application.Modules.Compras.DTOs;

namespace ERP.Application.Modules.Compras.UseCases.HabilitarProveedor;

[RequireFeature(SubscriptionFeatureCodes.Inventory)]
public sealed record EnableProveedorCommand(Guid Id)
    : IRequest<Result<ProveedorDto>>;

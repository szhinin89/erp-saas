using MediatR;
using ERP.Application.Common;
using ERP.Application.Modules.Compras.DTOs;

namespace ERP.Application.Modules.Compras.UseCases.ActualizarProveedor;

[RequireFeature(SubscriptionFeatureCodes.Inventory)]
public sealed record UpdateProveedorCommand(
    Guid    Id,
    string  TipoPersona,
    string  RazonSocial,
    string  Ruc,
    string? Correo,
    string? Telefono,
    string? Direccion,
    string  CondicionPago
) : IRequest<Result<ProveedorDto>>;

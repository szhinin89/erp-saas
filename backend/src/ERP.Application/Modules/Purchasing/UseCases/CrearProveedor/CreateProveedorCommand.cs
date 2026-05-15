using MediatR;
using ERP.Application.Common;
using ERP.Application.Modules.Purchasing.DTOs;

namespace ERP.Application.Modules.Purchasing.UseCases.CrearProveedor;

[RequireFeature(SubscriptionFeatureCodes.Inventory)]
public sealed record CreateProveedorCommand(
    string  TipoPersona,
    string  RazonSocial,
    string  Ruc,
    string? Correo,
    string? Telefono,
    string? Direccion,
    string  CondicionPago
) : IRequest<Result<ProveedorDto>>;

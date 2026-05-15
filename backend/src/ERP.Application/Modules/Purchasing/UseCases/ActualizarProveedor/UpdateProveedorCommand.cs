using MediatR;
using ERP.Application.Common;
using ERP.Application.Modules.Purchasing.DTOs;

namespace ERP.Application.Modules.Purchasing.UseCases.ActualizarProveedor;

[RequireFeature(SubscriptionFeatureCodes.Inventory)]
public sealed record UpdateProveedorCommand(
    Guid    Id,
    string  PersonType,
    string  LegalName,
    string  Ruc,
    string? Email,
    string? Phone,
    string? Address,
    string  PaymentTerms
) : IRequest<Result<ProveedorDto>>;

using MediatR;
using ERP.Application.Common;
using ERP.Application.Modules.Ventas.DTOs;

namespace ERP.Application.Modules.Ventas.UseCases.CrearCliente;

/// <summary>Creación de cliente (catálogo). Gobernado por suscripción SaaS vía atributos + MediatR pipeline.</summary>
[RequireFeature(SubscriptionFeatureCodes.Customers)]
[ConsumeSubscriptionUnits(SubscriptionFeatureCodes.Customers, 1)]
public sealed record CreateCustomerCommand(
    string IdentificationType,
    string IdentificationNumber,
    string LegalName,
    string? TradeName,
    string? AddressLine,
    string? Phone,
    string? Email,
    string? Notes,
    bool IsActive) : IRequest<Result<CustomerDto>>;

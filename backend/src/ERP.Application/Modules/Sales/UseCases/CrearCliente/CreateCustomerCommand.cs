using MediatR;
using ERP.Application.Common;
using ERP.Application.Modules.Sales.DTOs;

namespace ERP.Application.Modules.Sales.UseCases.CrearCliente;

/// <summary>Creación de cliente (catálogo). Gobernado por suscripción SaaS vía atributos + MediatR pipeline.</summary>
[RequireFeature(SubscriptionFeatureCodes.Sales)]
public sealed record CreateCustomerCommand(
    string IdentificationType,
    string IdentificationNumber,
    string LegalName,
    string? TradeName,
    string? AddressLine,
    string? Phone,
    string? Email,
    string? Notes,
    bool IsActive) : IRequest<Result<CustomerDto>>, ICompanyScopedRequest;

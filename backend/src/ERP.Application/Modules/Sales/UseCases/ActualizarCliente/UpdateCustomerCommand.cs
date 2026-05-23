using MediatR;
using ERP.Application.Common;
using ERP.Application.Modules.Sales.DTOs;

namespace ERP.Application.Modules.Sales.UseCases.ActualizarCliente;

[RequireFeature(SubscriptionFeatureCodes.Sales)]
public sealed record UpdateCustomerCommand(
    Guid Id,
    string IdentificationType,
    string IdentificationNumber,
    string LegalName,
    string? TradeName,
    string? AddressLine,
    string? Phone,
    string? Email,
    string? Notes,
    bool IsActive) : IRequest<Result<CustomerDto>>, ICompanyScopedRequest;

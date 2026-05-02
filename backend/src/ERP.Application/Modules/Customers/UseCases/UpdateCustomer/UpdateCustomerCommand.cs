using MediatR;
using ERP.Application.Common;
using ERP.Application.Modules.Customers.DTOs;

namespace ERP.Application.Modules.Customers.UseCases.UpdateCustomer;

[RequireFeature(SubscriptionFeatureCodes.Customers)]
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
    bool IsActive) : IRequest<Result<CustomerDto>>;

using ERP.Application.Common;
using ERP.Application.MasterData.DTOs;
using MediatR;

namespace ERP.Application.MasterData.UseCases.CreateBusinessPartner;

/// <summary>
/// ISubscriberOnlyRequest: BusinessPartner es subscriber-scoped — no requiere company_id en el JWT.
/// El CompanyScopeBehavior permite el paso sin validar empresa activa.
/// </summary>
public sealed record CreateBusinessPartnerCommand(
    string  IdentificationType,
    string  IdentificationNumber,
    string  LegalName,
    string? TradeName,
    string? Email,
    string? Phone,
    string? CountryCode,
    bool    AsCustomer,
    bool    AsSupplier) : IRequest<Result<BusinessPartnerDto>>, ISubscriberOnlyRequest;

using ERP.Application.Common;
using ERP.Application.MasterData.DTOs;
using ERP.Domain.MasterData.Enums;
using MediatR;

namespace ERP.Application.MasterData.UseCases.UpdateBusinessPartner;

/// <summary>
/// Actualiza perfil de identidad del BusinessPartner.
/// No modifica la identificación fiscal — usar UpdateBusinessPartnerIdentificationCommand para eso.
/// </summary>
public sealed record UpdateBusinessPartnerCommand(
    Guid Id,
    string LegalName,
    PersonType PersonType,
    string? TradeName = null,
    string? CountryCode = null
) : IRequest<Result<BusinessPartnerSummaryDto>>, ITenantScopedRequest;

/// <summary>
/// Cambia la identificación fiscal. Operación de alto impacto — emite domain event de auditoría.
/// Validación algoritmo RUC/CI delegada al dominio.
/// </summary>
public sealed record UpdateBusinessPartnerIdentificationCommand(
    Guid Id,
    string IdentificationType,
    string IdentificationNumber
) : IRequest<Result<BusinessPartnerSummaryDto>>, ITenantScopedRequest;

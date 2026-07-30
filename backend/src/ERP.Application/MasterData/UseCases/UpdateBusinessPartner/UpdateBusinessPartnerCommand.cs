using ERP.Application.Common;
using ERP.Application.MasterData.DTOs;
using MediatR;

namespace ERP.Application.MasterData.UseCases.UpdateBusinessPartner;

/// <summary>
/// Actualiza perfil de identidad del BusinessPartner.
/// No modifica la identificación fiscal — usar UpdateBusinessPartnerIdentificationCommand para eso.
///
/// LegalEntityTypeCode: mismas reglas de inferencia/obligatoriedad condicional que en creación
/// (ver <see cref="ERP.Domain.MasterData.ValueObjects.TaxIdentification.ResolveLegalEntityTypeCode"/>).
/// Cuando la identificación actual permite inferirlo, no puede modificarse de forma independiente:
/// un valor que contradiga la inferencia es rechazado.
/// </summary>
public sealed record UpdateBusinessPartnerCommand(
    Guid Id,
    string LegalName,
    int? LegalEntityTypeCode,
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

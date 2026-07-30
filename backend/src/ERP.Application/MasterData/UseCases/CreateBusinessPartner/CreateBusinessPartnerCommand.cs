using ERP.Application.Common;
using ERP.Application.MasterData.DTOs;
using MediatR;

namespace ERP.Application.MasterData.UseCases.CreateBusinessPartner;

/// <summary>
/// Crea la identidad fiscal de un tercero en el tenant.
/// Tenant-scoped: no requiere company en el JWT.
///
/// LegalEntityTypeCode es opcional: cuando la identificación permite inferir la naturaleza
/// jurídica (RUC/CI), el dominio la infiere automáticamente y valida que el valor enviado —
/// si lo hay — coincida. Cuando no puede inferirse (Pasaporte/ConsumidorFinal/Exterior/Placa)
/// es obligatorio. Ver <see cref="ERP.Domain.MasterData.ValueObjects.TaxIdentification.ResolveLegalEntityTypeCode"/>.
///
/// ELIMINADO: AsCustomer, AsSupplier → usar AssignBusinessPartnerRoleCommand después de crear.
/// ELIMINADO: LegalRepresentativeName, Email, Phone → usar CreateBpContactCommand después de crear.
/// </summary>
public sealed record CreateBusinessPartnerCommand(
    string IdentificationType,
    string IdentificationNumber,
    int? LegalEntityTypeCode,
    string LegalName,
    string? TradeName = null,
    string? CountryCode = null
) : IRequest<Result<BusinessPartnerSummaryDto>>, ITenantScopedRequest;

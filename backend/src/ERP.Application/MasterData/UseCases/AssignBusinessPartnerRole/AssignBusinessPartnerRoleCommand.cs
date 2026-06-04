using ERP.Application.Common;
using ERP.Application.MasterData.DTOs;
using ERP.Domain.MasterData.Enums;
using ERP.Domain.MasterData.ValueObjects;
using MediatR;

namespace ERP.Application.MasterData.UseCases.AssignBusinessPartnerRole;

/// <summary>
/// Asigna un rol a un BusinessPartner. Semántica UPSERT (ADR-BP-12):
///   - Si el rol no existe → crea nuevo (BusinessPartnerRole.Create)
///   - Si el rol existe revocado → reactiva (BusinessPartnerRole.Reactivate)
///   - Si el rol existe activo → error descriptivo (no duplica)
///
/// Las configs son opcionales. Se pueden actualizar después con UpdateRoleConfigCommand.
/// </summary>
public sealed record AssignBusinessPartnerRoleCommand(
    Guid                BusinessPartnerId,
    RoleType            RoleType,
    SupplierRoleConfig?  SupplierConfig  = null,
    CarrierRoleConfig?   CarrierConfig   = null,
    CustomerRoleConfig?  CustomerConfig  = null)
    : IRequest<Result<BusinessPartnerRoleDto>>, ISubscriberScopedRequest;

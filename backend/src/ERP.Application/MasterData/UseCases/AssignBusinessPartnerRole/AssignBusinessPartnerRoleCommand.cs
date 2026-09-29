using ERP.Application.Common;
using ERP.Application.MasterData.DTOs;
using ERP.Domain.MasterData.Enums;
using MediatR;

namespace ERP.Application.MasterData.UseCases.AssignBusinessPartnerRole;

/// <summary>
/// Asigna un rol a un BusinessPartner. Semántica UPSERT (ADR-BP-12):
///   - Si el rol no existe → crea nuevo (BusinessPartnerRole.Create)
///   - Si el rol existe revocado → reactiva (BusinessPartnerRole.Reactivate)
///   - Si el rol existe activo → error descriptivo (no duplica)
///
/// Las configs son opcionales. Se pueden actualizar después con UpdateRoleConfigCommand.
/// Llegan como datos primitivos (ZH-API-THIN-BP-ROLES-01); el handler construye los value objects
/// de Domain vía RoleConfigFactory.
/// </summary>
public sealed record AssignBusinessPartnerRoleCommand(
    Guid BusinessPartnerId,
    RoleType RoleType,
    SupplierRoleConfigDto? SupplierConfig = null,
    CarrierRoleConfigDto? CarrierConfig = null,
    CustomerRoleConfigDto? CustomerConfig = null
) : IRequest<Result<BusinessPartnerRoleDto>>, ITenantScopedRequest;

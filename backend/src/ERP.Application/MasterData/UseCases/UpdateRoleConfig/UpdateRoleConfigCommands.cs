using ERP.Application.Common;
using ERP.Application.MasterData.DTOs;
using MediatR;

namespace ERP.Application.MasterData.UseCases.UpdateRoleConfig;

// ZH-API-THIN-BP-ROLES-01: Config llega como datos primitivos (DTO de Application); el
// handler construye el value object de Domain vía RoleConfigFactory.

/// <summary>
/// Actualiza la config SRI operativa del rol Supplier.
/// Incluye (S3-A): DefaultTaxSupportCode, RetentionCodes, DefaultPaymentMethodCode, IsRetentionExempt.
/// </summary>
public sealed record UpdateSupplierRoleConfigCommand(
    Guid BusinessPartnerId,
    Guid RoleId,
    SupplierRoleConfigDto Config
)
    : IRequest<Result<BusinessPartnerRoleDto>>,
        ITenantScopedRequest;

/// <summary>Actualiza la config del rol Carrier (número autorización transporte, capacidad).</summary>
public sealed record UpdateCarrierRoleConfigCommand(
    Guid BusinessPartnerId,
    Guid RoleId,
    CarrierRoleConfigDto Config
)
    : IRequest<Result<BusinessPartnerRoleDto>>,
        ITenantScopedRequest;

/// <summary>Actualiza la config del rol Customer (CRM fields).</summary>
public sealed record UpdateCustomerRoleConfigCommand(
    Guid BusinessPartnerId,
    Guid RoleId,
    CustomerRoleConfigDto Config
)
    : IRequest<Result<BusinessPartnerRoleDto>>,
        ITenantScopedRequest;

/// <summary>Actualiza las notas internas de cualquier rol.</summary>
public sealed record UpdateRoleNotesCommand(Guid BusinessPartnerId, Guid RoleId, string? Notes)
    : IRequest<Result<bool>>,
        ITenantScopedRequest;

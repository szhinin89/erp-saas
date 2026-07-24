using ERP.Application.Common;
using ERP.Application.MasterData.DTOs;
using ERP.Domain.MasterData.ValueObjects;
using MediatR;

namespace ERP.Application.MasterData.UseCases.UpdateRoleConfig;

/// <summary>
/// Actualiza la config SRI operativa del rol Supplier.
/// Incluye (S3-A): DefaultTaxSupportCode, RetentionCodes, DefaultPaymentMethodCode, IsRetentionExempt.
/// </summary>
public sealed record UpdateSupplierRoleConfigCommand(
    Guid               RoleId,
    SupplierRoleConfig Config)
    : IRequest<Result<BusinessPartnerRoleDto>>, ITenantScopedRequest;

/// <summary>
/// Actualiza la clasificación estratégica del rol Supplier (S3-B).
/// Categoría, tipo, riesgo, rating, tipo de bien, segmento, preferencia de pago.
/// Simétrico a UpdateCustomerRoleConfigCommand.
/// </summary>
public sealed record UpdateSupplierClassificationConfigCommand(
    Guid                         RoleId,
    SupplierClassificationConfig Config)
    : IRequest<Result<BusinessPartnerRoleDto>>, ITenantScopedRequest;

/// <summary>Actualiza la config del rol Carrier (número autorización transporte, capacidad).</summary>
public sealed record UpdateCarrierRoleConfigCommand(
    Guid              RoleId,
    CarrierRoleConfig Config)
    : IRequest<Result<BusinessPartnerRoleDto>>, ITenantScopedRequest;

/// <summary>Actualiza la config del rol Customer (CRM fields).</summary>
public sealed record UpdateCustomerRoleConfigCommand(
    Guid               RoleId,
    CustomerRoleConfig Config)
    : IRequest<Result<BusinessPartnerRoleDto>>, ITenantScopedRequest;

/// <summary>Actualiza las notas internas de cualquier rol.</summary>
public sealed record UpdateRoleNotesCommand(
    Guid    RoleId,
    string? Notes)
    : IRequest<Result<bool>>, ITenantScopedRequest;

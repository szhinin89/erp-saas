using ERP.Application.Common;
using ERP.Application.MasterData.DTOs;
using ERP.Domain.MasterData.ValueObjects;
using MediatR;

namespace ERP.Application.MasterData.UseCases.UpdateRoleConfig;

/// <summary>Actualiza la config del rol Supplier (DefaultTaxSupportCode, retenciones, PaymentTerms).</summary>
public sealed record UpdateSupplierRoleConfigCommand(
    Guid               RoleId,
    SupplierRoleConfig Config)
    : IRequest<Result<BusinessPartnerRoleDto>>, ISubscriberScopedRequest;

/// <summary>Actualiza la config del rol Carrier (número autorización transporte, capacidad).</summary>
public sealed record UpdateCarrierRoleConfigCommand(
    Guid              RoleId,
    CarrierRoleConfig Config)
    : IRequest<Result<BusinessPartnerRoleDto>>, ISubscriberScopedRequest;

/// <summary>
/// Actualiza la config del rol Customer (categoría, segmento, zona, rating, fidelización, etc.).
/// CRM-ready: soporta segmentación, scoring y automatización documental.
/// </summary>
public sealed record UpdateCustomerRoleConfigCommand(
    Guid               RoleId,
    CustomerRoleConfig Config)
    : IRequest<Result<BusinessPartnerRoleDto>>, ISubscriberScopedRequest;

/// <summary>Actualiza las notas internas de cualquier rol.</summary>
public sealed record UpdateRoleNotesCommand(
    Guid    RoleId,
    string? Notes)
    : IRequest<Result<bool>>, ISubscriberScopedRequest;

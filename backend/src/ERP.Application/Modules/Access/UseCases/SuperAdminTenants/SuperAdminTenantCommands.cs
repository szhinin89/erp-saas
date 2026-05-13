using MediatR;
using ERP.Application.Common;
using ERP.Application.Access.DTOs;
using ERP.Domain.Tenants.Entities;

namespace ERP.Application.Access.UseCases.SuperAdminTenants;

public record SuperAdminCreateTenantWithAdminCommand(
    string TenantName,
    string TenantSlug,
    string AdminFirstName,
    string AdminLastName,
    string AdminEmail,
    string AdminPassword,
    PasswordResetMode PasswordResetMode = PasswordResetMode.Disabled,
    string? Ruc = null,
    string? ShortName = null,
    string? TradeName = null,
    string? Dinardap = null,
    string? LogoUrl = null,
    int DisplayOrder = 0,
    int Priority = 0,
    bool LinkExistingAdmin = false,
    /// <summary>Código de plan SaaS (p. ej. starter). Null/vacío = sin plan explícito.</summary>
    string? PlanCode = null,
    /// <summary>Si null o vacío, el tenant no restringe módulos (todos los habilitados según catálogo).</summary>
    List<string>? EnabledModules = null
) : IRequest<Result<SessionResponseDto>>;

public record GetSuperAdminTenantsQuery : IRequest<Result<IReadOnlyList<SuperAdminTenantItemDto>>>;


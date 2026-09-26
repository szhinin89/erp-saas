using ERP.Application.Modules.Companies.DTOs;

namespace ERP.Application.Modules.Session.DTOs;

/// <summary>
/// Snapshot de sesión del usuario autenticado: identidad, empresa activa, roles/permisos
/// y preferencias. Siempre derivado server-side desde JWT + contexto resuelto
/// (nunca acepta tenantId/companyId/roles/permisos desde el cliente).
/// </summary>
public sealed record SessionContextDto(
    SessionIdentityDto Identity,
    SessionTenantDto Tenant,
    SessionAuthorizationDto Authorization,
    SessionPreferencesDto Preferences,
    SessionBranchDto? Branch
);

public sealed record SessionIdentityDto(
    Guid UserId,
    string FullName,
    string Username,
    string? Email
);

/// <summary>
/// DisplayName/Logo provienen de la empresa operativa activa (TradeName ?? LegalName,
/// logo activo en Media); si no hay empresa resuelta, DisplayName cae a Tenant.Name.
/// ZH-TEMPORAL-CONTRACT-02: <c>Timezone</c> = <c>Company.Timezone</c> (IANA) de la empresa operativa
/// activa — SSOT de la zona con la que el frontend presenta instantes UTC y convierte horas locales
/// ingresadas por el usuario. Sin empresa resuelta: zona fiscal nacional por defecto.
/// </summary>
public sealed record SessionTenantDto(
    Guid Id,
    string DisplayName,
    CompanyLogoDto? Logo,
    string Timezone
);

public sealed record SessionAuthorizationDto(
    IReadOnlyList<string> Roles,
    IReadOnlyList<string> Permissions
);

public sealed record SessionPreferencesDto(string Language);

/// <summary>
/// Sucursal activa inicial de la sesión (Fase I-1 — bootstrap). Null cuando no hay empresa
/// operativa resuelta o cuando no se pudo determinar una sucursal única (misma limitación
/// interina que LoginHandler/SwitchCompanyHandler: sin selección real de sucursal todavía).
/// </summary>
public sealed record SessionBranchDto(Guid Id, string Name, bool IsMainBranch);

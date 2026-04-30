using ERP.Domain.Tenants.Entities;

namespace ERP.Application.Access.UseCases.RegisterTenantWithAdmin;

public record RegisterTenantWithAdminCommand(
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
    int Priority = 0
);


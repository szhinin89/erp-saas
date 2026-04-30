namespace ERP.Application.Tenants.DTOs;

public record TenantPublicSettingsDto(
    Guid TenantId,
    int PasswordResetMode
);


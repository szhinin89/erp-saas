namespace ERP.Application.Tenants.UseCases.UpdatePasswordResetMode;

public record UpdateTenantPasswordResetModeCommand(
    Guid TenantId,
    ERP.Domain.Tenants.Entities.PasswordResetMode PasswordResetMode
);


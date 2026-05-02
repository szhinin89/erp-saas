namespace ERP.Application.Tenants.UseCases.CreateTenant;

public record CreateTenantCommand(
    string Name,
    string Slug,
    ERP.Domain.Tenants.Entities.PasswordResetMode PasswordResetMode = ERP.Domain.Tenants.Entities.PasswordResetMode.Disabled,
    string? Ruc = null,
    string? ShortName = null,
    string? TradeName = null,
    string? Dinardap = null,
    string? LogoUrl = null,
    int DisplayOrder = 0,
    int Priority = 0,
    string? PlanCode = null,
    IReadOnlyList<string>? EnabledModules = null
);

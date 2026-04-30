namespace ERP.Application.Access.UseCases.SwitchTenant;

public record SwitchTenantCommand(
    Guid TenantId
);


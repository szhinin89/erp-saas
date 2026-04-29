namespace ERP.Application.Tenants.UseCases.CreateTenant;

public record CreateTenantCommand(
    string Name,
    string Slug
);

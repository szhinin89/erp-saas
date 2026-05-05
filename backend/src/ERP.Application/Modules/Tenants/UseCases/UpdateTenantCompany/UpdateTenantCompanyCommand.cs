namespace ERP.Application.Tenants.UseCases.UpdateTenantCompany;

public record UpdateTenantCompanyCommand(
    Guid TenantId,
    string Name,
    string Slug,
    string? Ruc,
    string? ShortName,
    string? TradeName,
    string? Dinardap,
    string? LogoUrl,
    int DisplayOrder,
    int Priority);

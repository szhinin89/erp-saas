namespace ERP.Application.Tenants.DTOs;

public record TenantDto(
    Guid Id,
    string Name,
    string Slug,
    bool IsActive,
    DateTime CreatedAt,
    string? Ruc,
    string? ShortName,
    string? TradeName,
    string? Dinardap,
    string? LogoUrl,
    int DisplayOrder,
    int Priority
);

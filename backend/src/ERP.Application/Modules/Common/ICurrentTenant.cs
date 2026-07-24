namespace ERP.Application.Common;

public interface ICurrentTenant
{
    Guid TenantId { get; }
    string? Slug { get; }
}

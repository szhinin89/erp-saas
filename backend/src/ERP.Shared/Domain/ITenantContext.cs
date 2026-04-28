namespace ERP.Shared.Domain;

public interface ITenantContext
{
    Guid TenantId { get; }
}

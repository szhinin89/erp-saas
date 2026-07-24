namespace ERP.Domain.Common;

public interface ITenantScopedEntity
{
    Guid TenantId { get; }
}

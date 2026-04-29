namespace ERP.Application.Common;

public interface ICurrentTenant
{
    Guid TenantId { get; }
    bool IsAuthenticated { get; }
}

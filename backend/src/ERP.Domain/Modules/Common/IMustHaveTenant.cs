namespace ERP.Domain.Common;

public interface IMustHaveTenant
{
    Guid TenantId { get; }
}


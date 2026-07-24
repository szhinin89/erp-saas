namespace ERP.Infrastructure.Services;

public static class JobTenantContext
{
    private static readonly AsyncLocal<Guid> _tenantId = new();

    public static Guid Current
    {
        get => _tenantId.Value;
        set => _tenantId.Value = value;
    }
}

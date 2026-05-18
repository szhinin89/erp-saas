namespace ERP.Infrastructure.Services;

/// <summary>
/// Almacena el TenantId del job activo usando AsyncLocal, que fluye a través de await.
/// Usado por <see cref="CurrentTenantService"/> como fallback cuando no hay HttpContext
/// (jobs de Hangfire, background services).
/// </summary>
public static class JobTenantContext
{
    private static readonly AsyncLocal<Guid> _tenantId = new();

    public static Guid Current
    {
        get => _tenantId.Value;
        set => _tenantId.Value = value;
    }
}

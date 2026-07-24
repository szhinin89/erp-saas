namespace ERP.Infrastructure.Services;

/// <summary>
/// <see cref="ICurrentCompany"/> para jobs sin HttpContext (Hangfire, background).
/// </summary>
public static class JobCompanyContext
{
    private static readonly AsyncLocal<Guid> _companyId = new();

    public static Guid Current
    {
        get => _companyId.Value;
        set => _companyId.Value = value;
    }
}

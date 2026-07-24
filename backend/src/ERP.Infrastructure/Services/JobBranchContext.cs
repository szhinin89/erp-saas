namespace ERP.Infrastructure.Services;

/// <summary>
/// <see cref="ICurrentBranch"/> para jobs sin HttpContext (Hangfire, background) — mismo
/// criterio que <see cref="JobCompanyContext"/>.
/// </summary>
public static class JobBranchContext
{
    private static readonly AsyncLocal<Guid> _branchId = new();

    public static Guid Current
    {
        get => _branchId.Value;
        set => _branchId.Value = value;
    }
}

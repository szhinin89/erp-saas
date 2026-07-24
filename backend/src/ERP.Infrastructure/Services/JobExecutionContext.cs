namespace ERP.Infrastructure.Services;

/// <summary>
/// Bootstrap y reset explícito de contexto tenant/empresa para jobs Hangfire y background.
/// Evita heredar AsyncLocal de requests HTTP previos.
/// </summary>
public sealed class JobExecutionContext : IDisposable
{
    private readonly Guid _previousTenant;
    private readonly Guid _previousCompany;
    private readonly bool _hadTenant;
    private readonly bool _hadCompany;

    private JobExecutionContext(Guid tenantId, Guid? companyId)
    {
        _hadTenant = JobTenantContext.Current != Guid.Empty;
        _previousTenant = JobTenantContext.Current;
        _hadCompany = JobCompanyContext.Current != Guid.Empty;
        _previousCompany = JobCompanyContext.Current;

        JobTenantContext.Current = tenantId;
        JobCompanyContext.Current = companyId ?? Guid.Empty;
    }

    public static JobExecutionContext Begin(Guid tenantId, Guid? companyId = null)
    {
        if (tenantId == Guid.Empty)
            throw new InvalidOperationException("JobExecutionContext requiere tenantId explícito.");

        return new JobExecutionContext(tenantId, companyId);
    }

    public void Dispose()
    {
        JobTenantContext.Current = _hadTenant ? _previousTenant : Guid.Empty;
        JobCompanyContext.Current = _hadCompany ? _previousCompany : Guid.Empty;
    }
}

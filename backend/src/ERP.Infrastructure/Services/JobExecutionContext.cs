namespace ERP.Infrastructure.Services;

/// <summary>
/// Bootstrap y reset explícito de contexto tenant/empresa para jobs Hangfire y background.
/// Evita heredar AsyncLocal de requests HTTP previos.
/// </summary>
public sealed class JobExecutionContext : IDisposable
{
    private readonly Guid _previousSubscriber;
    private readonly Guid _previousCompany;
    private readonly bool _hadSubscriber;
    private readonly bool _hadCompany;

    private JobExecutionContext(Guid subscriberId, Guid? companyId)
    {
        _hadSubscriber = JobSubscriberContext.Current != Guid.Empty;
        _previousSubscriber = JobSubscriberContext.Current;
        _hadCompany = JobCompanyContext.Current != Guid.Empty;
        _previousCompany = JobCompanyContext.Current;

        JobSubscriberContext.Current = subscriberId;
        JobCompanyContext.Current = companyId ?? Guid.Empty;
    }

    public static JobExecutionContext Begin(Guid subscriberId, Guid? companyId = null)
    {
        if (subscriberId == Guid.Empty)
            throw new InvalidOperationException("JobExecutionContext requiere SubscriberId explícito.");

        return new JobExecutionContext(subscriberId, companyId);
    }

    public void Dispose()
    {
        JobSubscriberContext.Current = _hadSubscriber ? _previousSubscriber : Guid.Empty;
        JobCompanyContext.Current = _hadCompany ? _previousCompany : Guid.Empty;
    }
}

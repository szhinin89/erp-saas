using ERP.Infrastructure.Services;
using Hangfire.Client;
using Hangfire.Server;

namespace ERP.API.Hangfire;

/// <summary>
/// Limpia AsyncLocal de contexto al crear/ejecutar jobs Hangfire (defense-in-depth).
/// </summary>
public sealed class HangfireSubscriberContextFilter : IClientFilter, IServerFilter
{
    public void OnCreating(CreatingContext context) => ResetContext();

    public void OnCreated(CreatedContext context) { }

    public void OnPerforming(PerformingContext context) => ResetContext();

    public void OnPerformed(PerformedContext context) => ResetContext();

    private static void ResetContext()
    {
        JobSubscriberContext.Current = Guid.Empty;
        JobCompanyContext.Current = Guid.Empty;
    }
}

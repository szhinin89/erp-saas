using System.Data.Common;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace ERP.Infrastructure.Setup;

/// <summary>An externally opened first-run session may never be reopened by EF, on any connection.</summary>
public sealed class FirstRunConnectionGuard(FirstRunOwnershipCoordinator coordinator) : DbConnectionInterceptor
{
    public override InterceptionResult ConnectionOpening(DbConnection connection, ConnectionEventData eventData,
        InterceptionResult result)
    {
        RejectReopen(eventData);
        return result;
    }

    public override ValueTask<InterceptionResult> ConnectionOpeningAsync(DbConnection connection,
        ConnectionEventData eventData, InterceptionResult result, CancellationToken cancellationToken = default)
    {
        RejectReopen(eventData);
        return ValueTask.FromResult(result);
    }

    private void RejectReopen(ConnectionEventData eventData)
    {
        if (coordinator.Protects(eventData.Context))
            throw new InvalidOperationException("EF cannot reopen a protected first-run session. Restart the API.");
    }
}

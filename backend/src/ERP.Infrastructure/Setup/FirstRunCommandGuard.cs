using System.Data.Common;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace ERP.Infrastructure.Setup;

/// <summary>Also rejects swapping a protected context to another already-open connection.</summary>
public sealed class FirstRunCommandGuard(FirstRunOwnershipCoordinator coordinator) : DbCommandInterceptor
{
    public override InterceptionResult<DbDataReader> ReaderExecuting(DbCommand command, CommandEventData eventData,
        InterceptionResult<DbDataReader> result)
    {
        coordinator.AssertProtectedConnection(eventData.Context, command.Connection);
        return result;
    }

    public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command,
        CommandEventData eventData, InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default)
    {
        coordinator.AssertProtectedConnection(eventData.Context, command.Connection);
        return ValueTask.FromResult(result);
    }

    public override InterceptionResult<int> NonQueryExecuting(DbCommand command, CommandEventData eventData,
        InterceptionResult<int> result)
    {
        coordinator.AssertProtectedConnection(eventData.Context, command.Connection);
        return result;
    }

    public override ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(DbCommand command,
        CommandEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        coordinator.AssertProtectedConnection(eventData.Context, command.Connection);
        return ValueTask.FromResult(result);
    }

    public override InterceptionResult<object> ScalarExecuting(DbCommand command, CommandEventData eventData,
        InterceptionResult<object> result)
    {
        coordinator.AssertProtectedConnection(eventData.Context, command.Connection);
        return result;
    }

    public override ValueTask<InterceptionResult<object>> ScalarExecutingAsync(DbCommand command,
        CommandEventData eventData, InterceptionResult<object> result, CancellationToken cancellationToken = default)
    {
        coordinator.AssertProtectedConnection(eventData.Context, command.Connection);
        return ValueTask.FromResult(result);
    }
}

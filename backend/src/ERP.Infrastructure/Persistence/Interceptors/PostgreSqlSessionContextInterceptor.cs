using ERP.Application.Common;
using Microsoft.EntityFrameworkCore.Diagnostics;
using System.Data.Common;

namespace ERP.Infrastructure.Persistence.Interceptors;

/// <summary>
/// Establece variables de sesión PostgreSQL para RLS futuro. No activa políticas RLS.
/// </summary>
public sealed class PostgreSqlSessionContextInterceptor : DbConnectionInterceptor
{
    private readonly IDbSessionContextApplicator _applicator;

    public PostgreSqlSessionContextInterceptor(IDbSessionContextApplicator applicator)
    {
        _applicator = applicator;
    }

    public override async Task ConnectionOpenedAsync(
        DbConnection connection,
        ConnectionEndEventData eventData,
        CancellationToken cancellationToken = default
    )
    {
        await _applicator.ApplyAsync(connection, cancellationToken);
        await base.ConnectionOpenedAsync(connection, eventData, cancellationToken);
    }
}

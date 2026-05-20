using System.Data.Common;
using ERP.Application.Common;

namespace ERP.Infrastructure.Persistence;

public sealed class DbSessionContextApplicator : IDbSessionContextApplicator
{
    private readonly ISessionContext _session;

    public DbSessionContextApplicator(ISessionContext session) => _session = session;

    public async Task ApplyAsync(DbConnection connection, CancellationToken ct = default)
    {
        if (connection is not DbConnection dbConn)
            return;

        if (_session.HasSubscriberContext)
            await SetConfigAsync(dbConn, "app.subscriber_id", _session.SubscriberId.ToString("D"), ct);

        if (_session.HasCompanyContext)
            await SetConfigAsync(dbConn, "app.company_id", _session.CompanyId.ToString("D"), ct);

        if (_session.IsPlatformAdmin)
            await SetConfigAsync(dbConn, "app.is_platform_admin", "true", ct);
    }

    private static async Task SetConfigAsync(DbConnection connection, string key, string value, CancellationToken ct)
    {
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT set_config(@key, @value, true)";
        var pKey = cmd.CreateParameter();
        pKey.ParameterName = "key";
        pKey.Value = key;
        cmd.Parameters.Add(pKey);
        var pVal = cmd.CreateParameter();
        pVal.ParameterName = "value";
        pVal.Value = value;
        cmd.Parameters.Add(pVal);
        if (connection.State != System.Data.ConnectionState.Open)
            await connection.OpenAsync(ct);
        await cmd.ExecuteNonQueryAsync(ct);
    }
}

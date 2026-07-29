using System.Data.Common;
using ERP.Application.Common;

namespace ERP.Infrastructure.Persistence;

public sealed class DbSessionContextApplicator : IDbSessionContextApplicator
{
    private readonly ISessionContext _session;

    public DbSessionContextApplicator(ISessionContext session) => _session = session;

    public async Task ApplyAsync(
        DbConnection connection,
        CancellationToken cancellationToken = default
    )
    {
        if (connection is not DbConnection dbConn)
            return;

        if (_session.HasTenantContext)
            await SetConfigAsync(
                dbConn,
                "app.tenant_id",
                _session.TenantId.ToString("D"),
                cancellationToken
            );

        if (_session.HasCompanyContext)
            await SetConfigAsync(
                dbConn,
                "app.company_id",
                _session.CompanyId.ToString("D"),
                cancellationToken
            );
    }

    private static async Task SetConfigAsync(
        DbConnection connection,
        string key,
        string value,
        CancellationToken cancellationToken
    )
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
            await connection.OpenAsync(cancellationToken);
        await cmd.ExecuteNonQueryAsync(cancellationToken);
    }
}

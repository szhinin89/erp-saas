using System.Data.Common;

namespace ERP.Application.Common;

public interface IDbSessionContextApplicator
{
    Task ApplyAsync(DbConnection connection, CancellationToken cancellationToken = default);
}

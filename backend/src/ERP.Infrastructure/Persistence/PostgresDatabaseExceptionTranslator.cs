using ERP.Application.Common.Persistence;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace ERP.Infrastructure.Persistence;

public sealed class PostgresDatabaseExceptionTranslator : IDatabaseExceptionTranslator
{
    public bool TryGetUniqueViolation(Exception exception, out DatabaseUniqueViolationInfo info)
    {
        info = null!;
        var pg = FindPostgresException(exception);
        if (pg is null || pg.SqlState != PostgresErrorCodes.UniqueViolation)
            return false;

        info = new DatabaseUniqueViolationInfo(
            pg.SqlState,
            pg.ConstraintName,
            pg.TableName,
            pg.Detail
        );
        return true;
    }

    private static PostgresException? FindPostgresException(Exception exception)
    {
        for (var ex = exception; ex is not null; ex = ex.InnerException!)
        {
            if (ex is PostgresException pg)
                return pg;
            if (ex is DbUpdateException)
                continue;
        }

        return null;
    }
}

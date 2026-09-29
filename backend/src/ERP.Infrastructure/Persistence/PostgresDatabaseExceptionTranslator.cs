using System.Data.Common;
using System.Net.Sockets;
using ERP.Application.Common;
using ERP.Application.Common.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
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

    public string? ClassifyFailureCode(Exception exception)
    {
        var chain = Chain(exception).ToList();

        if (chain.Any(e => e is DbUpdateConcurrencyException))
            return ApiResponseCodes.Common.ConcurrencyConflict;

        // El servidor respondió con un error SQL: el SQLSTATE decide.
        var pg = FindPostgresException(exception);
        if (pg is not null)
            return ClassifySqlState(pg.SqlState);

        var fromDatabase = chain.Any(e =>
            e is DbException or DbUpdateException or RetryLimitExceededException
        );
        if (!fromDatabase)
            return null;

        // Sin respuesta del servidor: conexión rechazada/caída, timeout, pool agotado, reintentos agotados.
        return chain.Any(e =>
            e is NpgsqlException { IsTransient: true }
                or RetryLimitExceededException
                or TimeoutException
                or SocketException
                or IOException
        )
            ? ApiResponseCodes.Common.DatabaseUnavailable
            : ApiResponseCodes.Common.InternalError;
    }

    private static string ClassifySqlState(string sqlState) =>
        sqlState switch
        {
            PostgresErrorCodes.UniqueViolation => ApiResponseCodes.Common.UniqueViolation,
            // Clase 23 (integrity constraint violation): FK, CHECK, NOT NULL, exclusión.
            _ when sqlState.StartsWith("23", StringComparison.Ordinal) =>
                ApiResponseCodes.Common.Conflict,
            PostgresErrorCodes.SerializationFailure or PostgresErrorCodes.DeadlockDetected =>
                ApiResponseCodes.Common.ConcurrencyConflict,
            // Clase 08 (conexión), 53 (recursos insuficientes), apagado/arranque del servidor y
            // statement timeout.
            _ when sqlState.StartsWith("08", StringComparison.Ordinal)
                || sqlState.StartsWith("53", StringComparison.Ordinal) =>
                ApiResponseCodes.Common.DatabaseUnavailable,
            PostgresErrorCodes.AdminShutdown
            or PostgresErrorCodes.CrashShutdown
            or PostgresErrorCodes.CannotConnectNow
            or PostgresErrorCodes.QueryCanceled => ApiResponseCodes.Common.DatabaseUnavailable,
            // Sintaxis, objeto inexistente, tipo de dato, error interno del servidor…: defecto nuestro.
            _ => ApiResponseCodes.Common.InternalError,
        };

    private static IEnumerable<Exception> Chain(Exception exception)
    {
        for (var ex = exception; ex is not null; ex = ex.InnerException)
            yield return ex;
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

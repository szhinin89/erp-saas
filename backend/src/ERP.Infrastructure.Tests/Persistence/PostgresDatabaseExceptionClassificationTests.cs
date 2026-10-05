using System.Net.Sockets;
using ERP.Application.Common;
using ERP.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Npgsql;

namespace ERP.Infrastructure.Tests.Persistence;

/// <summary>
/// ZH-BACKEND-SECURITY-ERROR-FINAL-HARDENING-01 — <see cref="PostgresDatabaseExceptionTranslator.ClassifyFailureCode"/>
/// es el único punto de clasificación técnica de fallos de base de datos (lo consume ExceptionMiddleware).
/// </summary>
public sealed class PostgresDatabaseExceptionClassificationTests
{
    private readonly PostgresDatabaseExceptionTranslator _translator = new();

    private static PostgresException Pg(string sqlState) =>
        new("detalle", "ERROR", "ERROR", sqlState);

    public static TheoryData<string, Exception, string?> Cases =>
        new()
        {
            // No disponible → 503
            {
                "socket rechazado",
                new NpgsqlException("x", new SocketException(10061)),
                ApiResponseCodes.Common.DatabaseUnavailable
            },
            {
                "timeout envuelto por EF",
                new DbUpdateException("x", new NpgsqlException("x", new TimeoutException())),
                ApiResponseCodes.Common.DatabaseUnavailable
            },
            {
                "reintentos agotados",
                new RetryLimitExceededException(
                    "x",
                    new NpgsqlException("x", new SocketException(10060))
                ),
                ApiResponseCodes.Common.DatabaseUnavailable
            },
            {
                "08006",
                Pg(PostgresErrorCodes.ConnectionFailure),
                ApiResponseCodes.Common.DatabaseUnavailable
            },
            {
                "53300",
                Pg(PostgresErrorCodes.TooManyConnections),
                ApiResponseCodes.Common.DatabaseUnavailable
            },
            {
                "57P01",
                Pg(PostgresErrorCodes.AdminShutdown),
                ApiResponseCodes.Common.DatabaseUnavailable
            },
            {
                "57014 statement timeout",
                Pg(PostgresErrorCodes.QueryCanceled),
                ApiResponseCodes.Common.DatabaseUnavailable
            },
            // Integridad / concurrencia conocidas → 409
            {
                "23505",
                new DbUpdateException("x", Pg(PostgresErrorCodes.UniqueViolation)),
                ApiResponseCodes.Common.UniqueViolation
            },
            {
                "23503",
                new DbUpdateException("x", Pg(PostgresErrorCodes.ForeignKeyViolation)),
                ApiResponseCodes.Common.Conflict
            },
            {
                "23502",
                new DbUpdateException("x", Pg(PostgresErrorCodes.NotNullViolation)),
                ApiResponseCodes.Common.Conflict
            },
            {
                "40P01",
                Pg(PostgresErrorCodes.DeadlockDetected),
                ApiResponseCodes.Common.ConcurrencyConflict
            },
            {
                "concurrencia optimista",
                new DbUpdateConcurrencyException("x"),
                ApiResponseCodes.Common.ConcurrencyConflict
            },
            // SQL inesperado / sin causa identificable → 500
            {
                "42P01",
                Pg(PostgresErrorCodes.UndefinedTable),
                ApiResponseCodes.Common.InternalError
            },
            {
                "22P02",
                new DbUpdateException("x", Pg(PostgresErrorCodes.InvalidTextRepresentation)),
                ApiResponseCodes.Common.InternalError
            },
            {
                "XX000",
                Pg(PostgresErrorCodes.InternalError),
                ApiResponseCodes.Common.InternalError
            },
            {
                "DbUpdateException sin causa",
                new DbUpdateException("x", new InvalidCastException()),
                ApiResponseCodes.Common.InternalError
            },
            {
                "Npgsql no transitorio",
                new NpgsqlException("protocolo"),
                ApiResponseCodes.Common.InternalError
            },
            // No es de base de datos → null (lo clasifica el resto del middleware)
            { "timeout ajeno a la BD (SRI/HTTP)", new TimeoutException("x"), null },
            { "regla de negocio", new InvalidOperationException("x"), null },
        };

    [Theory]
    [MemberData(nameof(Cases))]
    public void Clasifica_por_la_causa_real(
        string scenario,
        Exception exception,
        string? expected
    ) => _translator.ClassifyFailureCode(exception).Should().Be(expected, scenario);

    [Fact]
    public void TryGetUniqueViolation_sigue_igual()
    {
        _translator
            .TryGetUniqueViolation(
                new DbUpdateException("x", Pg(PostgresErrorCodes.UniqueViolation)),
                out var info
            )
            .Should()
            .BeTrue();
        info.SqlState.Should().Be(PostgresErrorCodes.UniqueViolation);
    }
}

using System.Net.Sockets;
using System.Text.Json;
using System.Text.Json.Nodes;
using ERP.Application.Common;
using ERP.API.Middleware;
using ERP.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;

namespace ERP.API.Tests.Extensions;

/// <summary>
/// ZH-BACKEND-SECURITY-ERROR-FINAL-HARDENING-01 — clasificación de excepciones en
/// <see cref="ExceptionMiddleware"/> con el traductor real de base de datos:
/// <list type="bullet">
/// <item>InvalidOperationException/ArgumentException: el texto solo viaja si el <c>throw</c> está en
/// ERP.Domain o ERP.Application (regla curada); framework / EF / Infrastructure → defecto interno.</item>
/// <item>Base de datos: no disponible → 503, integridad/concurrencia → 409, SQL inesperado → 500.</item>
/// <item>Ningún 500/503 (ni 409 de BD) expone SQL, conexión, host, inner exception ni stack.</item>
/// </list>
/// </summary>
public sealed class ExceptionClassificationTests
{
    private const string Sensitive =
        "Host=pg-prod.internal;Port=5432;Password=s3cr3t · SELECT * FROM identity_users · Key (ruc)=(1790012345001)";

    private sealed class Env(string name) : IWebHostEnvironment
    {
        public string EnvironmentName { get; set; } = name;
        public string ApplicationName { get; set; } = "ERP.API.Tests";
        public string WebRootPath { get; set; } = "";
        public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
        public string ContentRootPath { get; set; } = "";
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }

    private static async Task<(int Status, string Code, string Body)> Run(
        Exception exception,
        string environment = "Development"
    )
    {
        var context = new DefaultHttpContext();
        context.TraceIdentifier = "a5432b-opaque-request";
        context.Response.Body = new MemoryStream();
        await new ExceptionMiddleware(
            _ => Rethrow(exception),
            NullLogger<ExceptionMiddleware>.Instance,
            new Env(environment),
            new PostgresDatabaseExceptionTranslator()
        ).InvokeAsync(context);

        context.Response.Body.Position = 0;
        var body = await new StreamReader(context.Response.Body).ReadToEndAsync();
        using var doc = JsonDocument.Parse(body);
        return (
            context.Response.StatusCode,
            doc.RootElement.GetProperty("code").GetString()!,
            body
        );
    }

    private static Exception Capture(Action action)
    {
        try
        {
            action();
        }
        catch (Exception ex)
        {
            return ex;
        }
        throw new InvalidOperationException("Se esperaba una excepción.");
    }

    /// <summary>
    /// Propaga la excepción como lo hace el pipeline real (await → ExceptionDispatchInfo): conserva
    /// el stack y el frame de origen. Un <c>throw exception</c> los reiniciaría al lambda del test.
    /// </summary>
    internal static Task Rethrow(Exception exception)
    {
        System.Runtime.ExceptionServices.ExceptionDispatchInfo.Throw(exception);
        return Task.CompletedTask;
    }

    private static PostgresException Pg(string sqlState) =>
        new(
            Sensitive,
            "ERROR",
            "ERROR",
            sqlState,
            detail: Sensitive,
            tableName: "identity_users",
            constraintName: "uq_secret"
        );

    // ── InvalidOperationException / ArgumentException ──

    [Fact]
    public async Task Regla_del_dominio_es_422_DOMAIN_RULE_VIOLATION_con_su_mensaje_curado()
    {
        var (status, code, body) = await Run(ApiErrorContractTests.DomainRuleException());

        (status, code).Should().Be((422, "DOMAIN_RULE_VIOLATION"));
        using var doc = JsonDocument.Parse(body);
        doc.RootElement.GetProperty("data")
            .GetProperty("errors")[0]
            .GetString()
            .Should()
            .Be("El registro ya está deshabilitado.");
    }

    [Fact]
    public async Task Regla_del_dominio_a_traves_de_un_await_real_del_pipeline_es_422()
    {
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();
        var middleware = new ExceptionMiddleware(
            async _ =>
            {
                await Task.Yield();
                var warehouse = ERP.Domain.Modules.Inventory.Entities.Warehouse.Create(
                    Guid.NewGuid(),
                    Guid.NewGuid(),
                    "Bodega",
                    "B1",
                    null,
                    null,
                    null,
                    null,
                    null,
                    null,
                    null,
                    null,
                    null,
                    Guid.NewGuid(),
                    Guid.NewGuid()
                );
                warehouse.Disable(Guid.NewGuid());
                await Task.Yield();
                warehouse.Disable(Guid.NewGuid());
            },
            NullLogger<ExceptionMiddleware>.Instance,
            new Env("Production"),
            new PostgresDatabaseExceptionTranslator()
        );

        await middleware.InvokeAsync(context);

        context.Response.StatusCode.Should().Be(422);
    }

    public static TheoryData<string, Exception> InternalInvalidOperations =>
        new()
        {
            // ZH-DOMAIN-RULE-ERROR-SSOT-01: una invariante interna del DOMINIO también es técnica
            // (InvalidOperationException ya no significa regla de negocio en ninguna capa).
            {
                "Domain: invariante interna",
                Capture(() =>
                    ERP.Domain.Modules.Inventory.Entities.StockMovement.Create(
                        Guid.NewGuid(),
                        Guid.NewGuid(),
                        Guid.NewGuid(),
                        Guid.NewGuid(),
                        ERP.Domain.Modules.Inventory.Enums.StockMovementType.SaleExit,
                        1m,
                        "UND",
                        0m,
                        sequenceNumber: 0,
                        0m,
                        0m,
                        new DateOnly(2026, 1, 1),
                        null,
                        null,
                        null,
                        Guid.NewGuid(),
                        Guid.NewGuid()
                    )
                )
            },
            // LINQ, Nullable y la infraestructura propia: errores de programación / estado interno.
            { "LINQ Single()", Capture(() => Enumerable.Empty<int>().Single()) },
            { "Nullable.Value", Capture(() => _ = ((int?)null).Value) },
            {
                "Infrastructure",
                Capture(() => ERP.Infrastructure.Services.JobExecutionContext.Begin(Guid.Empty))
            },
            {
                "EF sin causa de BD",
                new InvalidOperationException(
                    "The instance of entity type 'IdentityUser' cannot be tracked " + Sensitive
                )
            },
        };

    [Theory]
    [MemberData(nameof(InternalInvalidOperations))]
    public async Task IOE_fuera_de_Domain_Application_es_500_sin_texto(
        string origin,
        Exception exception
    )
    {
        var (status, code, body) = await Run(exception);

        (status, code).Should().Be((500, "INTERNAL_ERROR"), origin);
        body.Should()
            .NotContain(exception.Message)
            .And.NotContain("Sequence")
            .And.NotContain("tenantId")
            .And.NotContain("Invariante");
    }

    [Fact]
    public async Task ArgumentException_conserva_400_pero_solo_expone_mensajes_curados()
    {
        var domain = Capture(() =>
            ERP.Domain.Modules.Inventory.Entities.Warehouse.Create(
                Guid.NewGuid(),
                Guid.NewGuid(),
                "",
                "B1",
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                Guid.NewGuid(),
                Guid.NewGuid()
            )
        );
        var framework = Capture(() => Enum.Parse<DayOfWeek>("NoExiste"));

        var fromDomain = await Run(domain);
        var fromFramework = await Run(framework);

        (fromDomain.Status, fromDomain.Code).Should().Be((400, "BAD_REQUEST"));
        fromDomain.Body.Should().Contain("Warehouse name is required.");
        (fromFramework.Status, fromFramework.Code).Should().Be((400, "BAD_REQUEST"));
        fromFramework.Body.Should().NotContain("NoExiste");
    }

    // ── Base de datos ──

    public static TheoryData<string, Exception, int, string> DatabaseFailures =>
        new()
        {
            {
                "conexión rechazada",
                new NpgsqlException(Sensitive, new SocketException(10061)),
                503,
                "DATABASE_UNAVAILABLE"
            },
            {
                "timeout",
                new DbUpdateException(
                    Sensitive,
                    new NpgsqlException(Sensitive, new TimeoutException(Sensitive))
                ),
                503,
                "DATABASE_UNAVAILABLE"
            },
            {
                "servidor arrancando (57P03)",
                Pg(PostgresErrorCodes.CannotConnectNow),
                503,
                "DATABASE_UNAVAILABLE"
            },
            {
                "conexión caída (08006)",
                Pg(PostgresErrorCodes.ConnectionFailure),
                503,
                "DATABASE_UNAVAILABLE"
            },
            {
                "demasiadas conexiones (53300)",
                Pg(PostgresErrorCodes.TooManyConnections),
                503,
                "DATABASE_UNAVAILABLE"
            },
            {
                "EF: fallo transitorio envuelto",
                new InvalidOperationException(
                    Sensitive,
                    new NpgsqlException(Sensitive, new SocketException(10060))
                ),
                503,
                "DATABASE_UNAVAILABLE"
            },
            {
                "UNIQUE (23505)",
                new DbUpdateException(Sensitive, Pg(PostgresErrorCodes.UniqueViolation)),
                409,
                "UNIQUE_VIOLATION"
            },
            {
                "FK (23503)",
                new DbUpdateException(Sensitive, Pg(PostgresErrorCodes.ForeignKeyViolation)),
                409,
                "CONFLICT"
            },
            {
                "CHECK (23514)",
                new DbUpdateException(Sensitive, Pg(PostgresErrorCodes.CheckViolation)),
                409,
                "CONFLICT"
            },
            {
                "serialización (40001)",
                new DbUpdateException(Sensitive, Pg(PostgresErrorCodes.SerializationFailure)),
                409,
                "CONCURRENCY_CONFLICT"
            },
            {
                "concurrencia optimista",
                new DbUpdateConcurrencyException(Sensitive),
                409,
                "CONCURRENCY_CONFLICT"
            },
            {
                "tabla inexistente (42P01)",
                Pg(PostgresErrorCodes.UndefinedTable),
                500,
                "INTERNAL_ERROR"
            },
            {
                "sintaxis SQL (42601)",
                new DbUpdateException(Sensitive, Pg(PostgresErrorCodes.SyntaxError)),
                500,
                "INTERNAL_ERROR"
            },
            {
                "DbUpdateException sin causa de BD",
                new DbUpdateException(Sensitive, new InvalidCastException(Sensitive)),
                500,
                "INTERNAL_ERROR"
            },
        };

    [Theory]
    [MemberData(nameof(DatabaseFailures))]
    public async Task Fallo_de_BD_se_clasifica_por_su_causa_y_nunca_expone_detalle(
        string scenario,
        Exception exception,
        int expectedStatus,
        string expectedCode
    )
    {
        foreach (var environment in new[] { "Production", "Development" })
        {
            var (status, code, body) = await Run(exception, environment);

            (status, code)
                .Should()
                .Be((expectedStatus, expectedCode), $"{scenario} ({environment})");
            AssertSafeDatabaseResponse(body, expectedCode, environment);
        }
    }

    private static void AssertSafeDatabaseResponse(string body, string code, string environment)
    {
        using var doc = JsonDocument.Parse(body);
        var root = doc.RootElement;
        root.EnumerateObject().Select(p => p.Name).Should()
            .BeEquivalentTo("code", "severity", "message", "data", "meta");
        root.GetProperty("code").GetString().Should().Be(code);
        var catalog = MessageCatalog.Resolve(code);
        root.GetProperty("severity").GetString().Should().Be(catalog.Severity);
        var message = root.GetProperty("message");
        message.EnumerateObject().Select(p => p.Name).Should().BeEquivalentTo("user", "dev");
        message.GetProperty("user").GetString().Should().Be(catalog.User);
        message.GetProperty("dev").GetString().Should()
            .Be(environment == "Development" ? catalog.Dev : null);
        // Solo el contenido del mensaje se inspecciona como texto técnico. Los metadatos
        // opacos y temporales pueden contener por casualidad secuencias como 5432.
        foreach (var field in message.EnumerateObject())
        {
            foreach (var sensitive in new[]
            {
                "pg-prod", "5432", "Password", "s3cr3t", "SELECT", "identity_users",
                "1790012345001", "uq_secret", "Exception", " at "
            })
                (field.Value.GetString() ?? "").Should().NotContain(sensitive, field.Name);
        }
        root.GetProperty("data").ValueKind.Should().Be(JsonValueKind.Null);
        var meta = root.GetProperty("meta");
        meta.EnumerateObject().Select(p => p.Name).Should()
            .BeEquivalentTo("correlationId", "timestamp", "traceId");
        meta.GetProperty("correlationId").GetString().Should().Be("a5432b-opaque-request");
        meta.GetProperty("timestamp").GetDateTime().Kind.Should().Be(DateTimeKind.Utc);
        meta.GetProperty("traceId").ValueKind.Should().Be(JsonValueKind.Null);
    }

    [Fact]
    public async Task Metadatos_con_5432_no_son_detalle_de_BD()
    {
        var (_, code, body) = await Run(Pg(PostgresErrorCodes.SyntaxError));
        var response = JsonNode.Parse(body)!;
        response["meta"]!["timestamp"] = "2026-11-08T12:00:00.5432Z";

        AssertSafeDatabaseResponse(response.ToJsonString(), code, "Development");
    }

    [Theory]
    [InlineData("5432")]
    [InlineData("Host=pg-prod.internal;Port=5432;Password=s3cr3t")]
    [InlineData("SELECT * FROM identity_users")]
    [InlineData("Key (ruc)=(1790012345001); constraint uq_secret")]
    [InlineData("NpgsqlException: inner exception at Database.Save()")]
    public async Task Asercion_rechaza_filtraciones_reales_en_campos_de_contenido(string sensitive)
    {
        foreach (var environment in new[] { "Production", "Development" })
        {
            var (_, code, body) = await Run(Pg(PostgresErrorCodes.SyntaxError), environment);
            foreach (var field in new[] { "user", "dev", "data", "extra" })
            {
                var response = JsonNode.Parse(body)!;
                if (field is "user" or "dev")
                    response["message"]![field] = sensitive;
                else if (field == "data")
                    response["data"] = new JsonObject { ["errors"] = new JsonArray(sensitive) };
                else
                    response["detail"] = sensitive;

                Action validate = () => AssertSafeDatabaseResponse(response.ToJsonString(), code, environment);
                validate.Should().Throw<Xunit.Sdk.XunitException>($"{field} ({environment})");
            }
        }
    }

    [Fact]
    public async Task RUC_duplicado_sigue_siendo_su_codigo_semantico_aunque_envuelva_la_violacion_UNIQUE()
    {
        var (status, code, _) = await Run(
            new ERP.Domain.Exceptions.CompanyRucAlreadyExistsException("1790012345001")
        );

        (status, code).Should().Be((409, "COMPANY_RUC_ALREADY_EXISTS"));
    }
}

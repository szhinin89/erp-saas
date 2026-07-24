using System.Net;
using System.Text.Json;
using ERP.API.Extensions;
using ERP.Application.Common;
using ERP.Application.Common.Exceptions;
using ERP.Domain.Exceptions;
using ERP.API.Middleware;
using FluentAssertions;
using FluentValidation;
using FluentValidation.Results;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Logging.Abstractions;
using System.Reflection;

namespace ERP.API.Tests;

/// <summary>
/// Fase 3 (gobernanza del contrato de respuesta): garantiza el pipeline
/// <c>code → MessageCatalog → severity/message → ApiResponse</c> y que
/// <see cref="ExceptionMiddleware"/> nunca filtra detalles internos.
/// </summary>
public sealed class ResponseFactoryConsistencyTests
{
    private sealed class FakeWebHostEnvironment : IWebHostEnvironment
    {
        public string EnvironmentName { get; set; } = "Production";
        public string ApplicationName { get; set; } = "ERP.API.Tests";
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
        public string WebRootPath { get; set; } = AppContext.BaseDirectory;
        public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
    }

    /// <summary>
    /// Recorre <see cref="ApiResponseCodes"/> y sus clases anidadas por dominio
    /// (<c>Common</c>, futuros <c>Inventory</c>/<c>Sales</c>/...) para cubrir
    /// automáticamente cualquier código nuevo agregado al catálogo.
    /// </summary>
    private static IEnumerable<string> CollectResponseCodes(Type type)
    {
        var fields = type
            .GetFields(BindingFlags.Public | BindingFlags.Static | BindingFlags.GetField)
            .Where(f => f.IsLiteral && f.FieldType == typeof(string))
            .Select(f => (string)f.GetRawConstantValue()!);

        var nested = type
            .GetNestedTypes(BindingFlags.Public | BindingFlags.Static)
            .Where(t => t.IsAbstract && t.IsSealed)
            .SelectMany(CollectResponseCodes);

        return fields.Concat(nested);
    }

    public static IEnumerable<object[]> AllCodes()
        => CollectResponseCodes(typeof(ApiResponseCodes))
            .Select(code => new object[] { code });

    [Theory]
    [MemberData(nameof(AllCodes))]
    public void Success_pipeline_resolves_severity_and_user_message_from_catalog(string code)
    {
        var entry = MessageCatalog.Resolve(code);
        var context = new DefaultHttpContext();
        var environment = new FakeWebHostEnvironment { EnvironmentName = "Production" };

        var response = ResponseFactory.Success(context, environment, code, new { });

        response.Code.Should().Be(code);
        response.Severity.Should().Be(entry.Severity);
        response.Message.User.Should().Be(entry.User);
        response.Message.Dev.Should().BeNull("en Production no se expone el mensaje de desarrollador");
    }

    [Theory]
    [MemberData(nameof(AllCodes))]
    public void Error_pipeline_resolves_severity_and_user_message_from_catalog(string code)
    {
        var entry = MessageCatalog.Resolve(code);
        var context = new DefaultHttpContext();
        var environment = new FakeWebHostEnvironment { EnvironmentName = "Production" };

        var response = ResponseFactory.Error(context, environment, code);

        response.Code.Should().Be(code);
        response.Severity.Should().Be(entry.Severity);
        response.Message.User.Should().Be(entry.User);
        response.Message.Dev.Should().BeNull("en Production no se expone el mensaje de desarrollador");
    }

    [Fact]
    public void Development_environment_exposes_dev_message()
    {
        var entry = MessageCatalog.Resolve(ApiResponseCodes.Common.InternalError);
        var context = new DefaultHttpContext();
        var environment = new FakeWebHostEnvironment { EnvironmentName = "Development" };

        var response = ResponseFactory.Error(context, environment, ApiResponseCodes.Common.InternalError);

        response.Message.Dev.Should().Be(entry.Dev);
    }

    [Fact]
    public void Meta_includes_correlationId_and_recent_timestamp()
    {
        var context = new DefaultHttpContext { TraceIdentifier = "trace-123" };
        var environment = new FakeWebHostEnvironment();

        var response = ResponseFactory.Success(context, environment, ApiResponseCodes.Common.Ok, new { });

        response.Meta.CorrelationId.Should().Be("trace-123");
        response.Meta.Timestamp.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromSeconds(5));
    }

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private static async Task<(JsonDocument Doc, int StatusCode)> RunMiddlewareAsync(Exception exceptionToThrow, string environmentName = "Production")
    {
        RequestDelegate next = _ => throw exceptionToThrow;

        var environment = new FakeWebHostEnvironment { EnvironmentName = environmentName };
        var middleware = new ExceptionMiddleware(next, NullLogger<ExceptionMiddleware>.Instance, environment);
        var context = new DefaultHttpContext { TraceIdentifier = "trace-abc" };
        context.Response.Body = new MemoryStream();

        await middleware.InvokeAsync(context);

        context.Response.Body.Position = 0;
        using var reader = new StreamReader(context.Response.Body);
        var json = await reader.ReadToEndAsync();
        return (JsonDocument.Parse(json), context.Response.StatusCode);
    }

    public static IEnumerable<object[]> ExceptionToStatusAndCode()
    {
        yield return new object[]
        {
            new ValidationException(new[] { new ValidationFailure("Campo", "Inválido") }),
            (int)HttpStatusCode.UnprocessableEntity,
            ApiResponseCodes.Common.ValidationError,
        };
        yield return new object[]
        {
            new ArgumentException("Argumento inválido"),
            (int)HttpStatusCode.BadRequest,
            ApiResponseCodes.Common.BadRequest,
        };
        yield return new object[]
        {
            new InvalidOperationException("Regla de negocio violada"),
            (int)HttpStatusCode.UnprocessableEntity,
            ApiResponseCodes.Common.DomainRuleViolation,
        };
        yield return new object[]
        {
            new SriCommunicationException("Timeout SRI"),
            (int)HttpStatusCode.BadGateway,
            ApiResponseCodes.Common.SriCommunicationError,
        };
        yield return new object[]
        {
            CompanyScopeException.AccessDenied(),
            (int)HttpStatusCode.Forbidden,
            ApiResponseCodes.Common.CompanyScopeForbidden,
        };
        yield return new object[]
        {
            new CompanyRucAlreadyExistsException("1790012345001"),
            (int)HttpStatusCode.Conflict,
            ApiResponseCodes.Common.CompanyRucAlreadyExists,
        };
        yield return new object[]
        {
            new UnauthorizedAccessException("Sin permiso"),
            (int)HttpStatusCode.Unauthorized,
            ApiResponseCodes.Common.Unauthorized,
        };
        yield return new object[]
        {
            new InvalidCastException("boom"),
            (int)HttpStatusCode.InternalServerError,
            ApiResponseCodes.Common.InternalError,
        };
    }

    [Theory]
    [MemberData(nameof(ExceptionToStatusAndCode))]
    public async Task ExceptionMiddleware_maps_exception_to_status_and_code(Exception exception, int expectedStatus, string expectedCode)
    {
        var (doc, statusCode) = await RunMiddlewareAsync(exception);
        using var _doc = doc;

        statusCode.Should().Be(expectedStatus);

        var root = doc.RootElement;
        root.GetProperty("code").GetString().Should().Be(expectedCode);

        var entry = MessageCatalog.Resolve(expectedCode);
        root.GetProperty("severity").GetString().Should().Be(entry.Severity);
        root.GetProperty("message").GetProperty("user").GetString().Should().Be(entry.User);

        var meta = root.GetProperty("meta");
        meta.GetProperty("correlationId").GetString().Should().Be("trace-abc");
        meta.TryGetProperty("timestamp", out _).Should().BeTrue();
    }

    [Theory]
    [MemberData(nameof(ExceptionToStatusAndCode))]
    public async Task ExceptionMiddleware_never_leaks_stack_trace_or_dev_message_in_production(Exception exception, int expectedStatus, string expectedCode)
    {
        var (doc, statusCode) = await RunMiddlewareAsync(exception, environmentName: "Production");
        using var _doc = doc;

        statusCode.Should().Be(expectedStatus);

        var root = doc.RootElement;
        root.GetProperty("code").GetString().Should().Be(expectedCode);

        var rawJson = root.GetRawText();
        rawJson.Should().NotContain("StackTrace");
        rawJson.Should().NotContain(" at ERP.");
        rawJson.Should().NotContain("System.Exception");

        root.GetProperty("message").GetProperty("dev").ValueKind.Should().Be(JsonValueKind.Null);
    }

    [Fact]
    public async Task ExceptionMiddleware_exposes_dev_message_only_in_development()
    {
        var (doc, _) = await RunMiddlewareAsync(new InvalidCastException("boom"), environmentName: "Development");
        using var _doc = doc;

        var entry = MessageCatalog.Resolve(ApiResponseCodes.Common.InternalError);
        doc.RootElement.GetProperty("message").GetProperty("dev").GetString().Should().Be(entry.Dev);
    }

    [Fact]
    public async Task ExceptionMiddleware_response_uses_camelCase_envelope_keys()
    {
        var (doc, _) = await RunMiddlewareAsync(new InvalidOperationException("boom"));
        using var _doc = doc;

        var root = doc.RootElement;
        foreach (var expected in new[] { "code", "severity", "message", "data", "meta" })
        {
            root.TryGetProperty(expected, out _).Should().BeTrue($"el envelope debe exponer '{expected}' en camelCase");
        }

        var message = root.GetProperty("message");
        message.TryGetProperty("user", out _).Should().BeTrue();
        message.TryGetProperty("dev", out _).Should().BeTrue();

        var meta = root.GetProperty("meta");
        meta.TryGetProperty("correlationId", out _).Should().BeTrue();
        meta.TryGetProperty("timestamp", out _).Should().BeTrue();
    }
}

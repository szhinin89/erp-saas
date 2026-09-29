using System.Reflection;
using System.Text.Json;
using ERP.API.Extensions;
using ERP.API.Middleware;
using ERP.Application.Common;
using ERP.Application.Common.Exceptions;
using ERP.Domain.Exceptions;
using FluentAssertions;
using FluentValidation;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Logging.Abstractions;

namespace ERP.API.Tests.Extensions;

/// <summary>
/// ZH-API-ERROR-CONTRACT-HARDENING-01 — contrato único Error → HTTP del ERP (ADR-027 §8-9).
/// Un mismo código produce el mismo status y la misma forma de envelope venga de un fallo de
/// <c>Result&lt;T&gt;</c> (<see cref="ApiResultExtensions"/>) o de una excepción
/// (<see cref="ExceptionMiddleware"/>): ambos consultan <see cref="ApiErrorStatus"/>.
/// </summary>
public sealed class ApiErrorContractTests
{
    private sealed class StubWebHostEnvironment : IWebHostEnvironment
    {
        public string EnvironmentName { get; set; } = "Production";
        public string ApplicationName { get; set; } = "ERP.API.Tests";
        public string WebRootPath { get; set; } = "";
        public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
        public string ContentRootPath { get; set; } = "";
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }

    private sealed class TestController : ControllerBase;

    /// <summary>
    /// Matriz canónica. Exhaustiva a propósito: un código nuevo en <see cref="ApiResponseCodes"/>
    /// rompe <see cref="La_matriz_cubre_todos_los_codigos_de_error"/> hasta que se decida aquí su status.
    /// </summary>
    private static readonly Dictionary<string, int> Matrix = new()
    {
        [ApiResponseCodes.Common.ValidationError] = 422,
        [ApiResponseCodes.Common.DomainRuleViolation] = 422,
        [ApiResponseCodes.Common.BadRequest] = 400,
        [ApiResponseCodes.Common.NotFound] = 404,
        [ApiResponseCodes.Common.Conflict] = 409,
        [ApiResponseCodes.Common.UniqueViolation] = 409,
        [ApiResponseCodes.Common.CompanyRucAlreadyExists] = 409,
        [ApiResponseCodes.Common.ConcurrencyConflict] = 409,
        [ApiResponseCodes.Common.Unauthorized] = 401,
        [ApiResponseCodes.Common.Forbidden] = 403,
        [ApiResponseCodes.Common.CompanyScopeForbidden] = 403,
        [ApiResponseCodes.Common.BranchScopeForbidden] = 403,
        [ApiResponseCodes.Common.DatabaseUnavailable] = 503,
        [ApiResponseCodes.Common.SriCommunicationError] = 502,
        [ApiResponseCodes.Common.RateLimited] = 429,
        [ApiResponseCodes.Common.InternalError] = 500,
        [ApiResponseCodes.Common.InvalidDateTimeKind] = 500,
    };

    private static readonly string[] SuccessCodes =
    [
        ApiResponseCodes.Common.Ok,
        ApiResponseCodes.Common.Created,
    ];

    private static IEnumerable<string> DeclaredCodes(Type type) =>
        type.GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(f => f.IsLiteral && f.FieldType == typeof(string))
            .Select(f => (string)f.GetRawConstantValue()!)
            .Concat(type.GetNestedTypes(BindingFlags.Public).SelectMany(DeclaredCodes));

    public static TheoryData<string, int> MatrixRows()
    {
        var data = new TheoryData<string, int>();
        foreach (var (code, status) in Matrix)
            data.Add(code, status);
        return data;
    }

    [Fact]
    public void La_matriz_cubre_todos_los_codigos_de_error()
    {
        var errorCodes = DeclaredCodes(typeof(ApiResponseCodes)).Except(SuccessCodes);

        errorCodes.Should().BeEquivalentTo(Matrix.Keys);
    }

    [Fact]
    public void Todo_codigo_de_error_declara_categoria_y_los_de_exito_no()
    {
        foreach (var code in DeclaredCodes(typeof(ApiResponseCodes)))
        {
            var category = MessageCatalog.Resolve(code).Category;
            if (SuccessCodes.Contains(code))
                category.Should().BeNull(code);
            else
                category.Should().NotBeNull(code);
        }
    }

    [Theory]
    [MemberData(nameof(MatrixRows))]
    public void Cada_codigo_tiene_un_solo_status(string code, int status) =>
        ApiErrorStatus.For(code).Should().Be(status);

    [Theory]
    [MemberData(nameof(MatrixRows))]
    public void Fallo_de_Result_responde_status_code_y_data_errors(string code, int status)
    {
        var result = Controller().ToOkOrBadRequest(Result<string>.Failure("detalle", code));

        var obj = result.Should().BeAssignableTo<ObjectResult>().Subject;
        obj.StatusCode.Should().Be(status);
        var json = JsonSerializer.SerializeToElement(obj.Value);
        json.GetProperty("Code").GetString().Should().Be(code);
        if (ApiErrorStatus.ExposesDetail(code))
            json.GetProperty("Data").GetProperty("errors")[0].GetString().Should().Be("detalle");
        else
            json.GetProperty("Data").ValueKind.Should().Be(JsonValueKind.Null, "500/503 no exponen detalle");
    }

    /// <summary>Misma condición lógica por excepción o por Result → mismo status y mismo code.</summary>
    public static TheoryData<Exception, string> ExceptionCodes =>
        new()
        {
            { new ValidationException("x"), ApiResponseCodes.Common.ValidationError },
            { new DbUpdateConcurrencyException("x"), ApiResponseCodes.Common.ConcurrencyConflict },
            { new UnspecifiedDateTimeKindException("Entity", "At", DateTimeKind.Local), ApiResponseCodes.Common.InvalidDateTimeKind },
            { new DbUpdateException("x"), ApiResponseCodes.Common.DatabaseUnavailable },
            { new ArgumentException("x"), ApiResponseCodes.Common.BadRequest },
            { new InvalidOperationException("x"), ApiResponseCodes.Common.DomainRuleViolation },
            { new SriCommunicationException("x"), ApiResponseCodes.Common.SriCommunicationError },
            { CompanyScopeException.AccessDenied(), ApiResponseCodes.Common.CompanyScopeForbidden },
            { BranchScopeException.AccessDenied(), ApiResponseCodes.Common.BranchScopeForbidden },
            { new CompanyRucAlreadyExistsException("x"), ApiResponseCodes.Common.CompanyRucAlreadyExists },
            { new UnauthorizedAccessException("x"), ApiResponseCodes.Common.Unauthorized },
            { new NotSupportedException("x"), ApiResponseCodes.Common.InternalError },
        };

    [Theory]
    [MemberData(nameof(ExceptionCodes))]
    public async Task Excepcion_y_Result_producen_el_mismo_status_y_code(Exception exception, string code)
    {
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();
        var middleware = new ExceptionMiddleware(
            _ => throw exception,
            NullLogger<ExceptionMiddleware>.Instance,
            new StubWebHostEnvironment()
        );

        await middleware.InvokeAsync(context);

        context.Response.Body.Position = 0;
        using var doc = await JsonDocument.ParseAsync(context.Response.Body);
        doc.RootElement.GetProperty("code").GetString().Should().Be(code);
        context.Response.StatusCode.Should().Be(Matrix[code]);

        var viaResult = (ObjectResult)Controller().ToOkOrBadRequest(Result<string>.Failure("x", code));
        viaResult.StatusCode.Should().Be(context.Response.StatusCode);
    }

    // ── ZH-SCOPE-ERROR-SEMANTICS-01: 500/503 nunca exponen detalle técnico ──

    private const string Secret =
        "Npgsql: Host=db.internal;Password=s3cr3t; SELECT * FROM identity_users WHERE id = @p0";

    [Theory]
    [InlineData(ApiResponseCodes.Common.InternalError, 500, "Production")]
    [InlineData(ApiResponseCodes.Common.InternalError, 500, "Development")]
    [InlineData(ApiResponseCodes.Common.DatabaseUnavailable, 503, "Production")]
    [InlineData(ApiResponseCodes.Common.DatabaseUnavailable, 503, "Development")]
    [InlineData(ApiResponseCodes.Common.InvalidDateTimeKind, 500, "Development")]
    public void Result_500_503_no_filtra_el_detalle_tecnico(string code, int status, string environment)
    {
        var result = Controller(environment).ApiFailure(Result<string>.Failure(Secret, code));

        var obj = result.Should().BeAssignableTo<ObjectResult>().Subject;
        obj.StatusCode.Should().Be(status);
        var json = JsonSerializer.Serialize(obj.Value);
        json.Should().Contain(code);
        json.Should().NotContain("Password").And.NotContain("SELECT").And.NotContain("db.internal");
    }

    [Fact]
    public void Solo_InternalError_e_Infrastructure_ocultan_el_detalle()
    {
        foreach (var (code, _) in Matrix)
            ApiErrorStatus.ExposesDetail(code).Should().Be(
                code is not (ApiResponseCodes.Common.InternalError
                    or ApiResponseCodes.Common.InvalidDateTimeKind
                    or ApiResponseCodes.Common.DatabaseUnavailable),
                code
            );
    }

    public static TheoryData<Exception, int, string> TechnicalExceptions =>
        new()
        {
            { new Exception(Secret), 500, "Production" },
            { new Exception(Secret), 500, "Development" },
            { new TimeoutException(Secret), 500, "Development" },
            { new DbUpdateException("An error occurred while saving the entity changes.", new Exception(Secret)), 503, "Production" },
            { new DbUpdateException(Secret), 503, "Development" },
        };

    [Theory]
    [MemberData(nameof(TechnicalExceptions))]
    public async Task Excepcion_500_503_no_filtra_detalle_ni_stack_trace(Exception exception, int status, string environment)
    {
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();
        var middleware = new ExceptionMiddleware(
            _ => throw exception,
            NullLogger<ExceptionMiddleware>.Instance,
            new StubWebHostEnvironment { EnvironmentName = environment }
        );

        await middleware.InvokeAsync(context);

        context.Response.StatusCode.Should().Be(status);
        context.Response.Body.Position = 0;
        var body = await new StreamReader(context.Response.Body).ReadToEndAsync();
        body.Should().NotContain("Password").And.NotContain("SELECT").And.NotContain("db.internal")
            .And.NotContain(" at ").And.NotContain("Exception");
        using var doc = JsonDocument.Parse(body);
        doc.RootElement.TryGetProperty("data", out var data).Should().BeTrue();
        data.ValueKind.Should().Be(JsonValueKind.Null);
    }

    private static TestController Controller(string environment = "Production")
    {
        var services = new ServiceCollection();
        services.AddSingleton<IWebHostEnvironment>(new StubWebHostEnvironment { EnvironmentName = environment });
        return new TestController
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    RequestServices = services.BuildServiceProvider(),
                },
            },
        };
    }
}

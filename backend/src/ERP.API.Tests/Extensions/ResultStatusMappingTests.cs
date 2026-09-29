using System.Text.Json;
using ERP.API.Extensions;
using ERP.Application.Common;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;

namespace ERP.API.Tests.Extensions;

/// <summary>
/// ZH-API-RESULT-STATUS-MAPPING-01 — mapeo Result → HTTP de <see cref="ApiResultExtensions"/>.
/// Tabla única (MapFailure) compartida por ToOkOrBadRequest / ToCreatedOrBadRequest / ToOkOrNotFound /
/// ToFileOrNotFound para todo fallo con Code; ToOkOrNotFound/ToFileOrNotFound conservan 404 solo para
/// fallos sin Code (contrato histórico de "no encontrado"). Antes ToOkOrNotFound devolvía 404 para
/// cualquier fallo (FORBIDDEN, VALIDATION_ERROR, CONFLICT…).
/// </summary>
public sealed class ResultStatusMappingTests
{
    private sealed class StubWebHostEnvironment : IWebHostEnvironment
    {
        public string EnvironmentName { get; set; } = "Production";
        public string ApplicationName { get; set; } = "ERP.API.Tests";
        public string WebRootPath { get; set; } = "";
        public Microsoft.Extensions.FileProviders.IFileProvider WebRootFileProvider { get; set; } = null!;
        public string ContentRootPath { get; set; } = "";
        public Microsoft.Extensions.FileProviders.IFileProvider ContentRootFileProvider { get; set; } = null!;
    }

    private sealed class TestController : ControllerBase;

    private static TestController Controller()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IWebHostEnvironment>(new StubWebHostEnvironment());
        return new TestController
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { RequestServices = services.BuildServiceProvider() },
            },
        };
    }

    private static (int Status, string? Code, string[] Errors) Read(IActionResult result)
    {
        var obj = result.Should().BeAssignableTo<ObjectResult>().Subject;
        var json = JsonSerializer.SerializeToElement(obj.Value);
        var errors = json.TryGetProperty("Data", out var data) && data.ValueKind == JsonValueKind.Object
            && data.TryGetProperty("errors", out var e)
            ? e.EnumerateArray().Select(x => x.GetString()!).ToArray()
            : [];
        return (obj.StatusCode ?? 200, json.GetProperty("Code").GetString(), errors);
    }

    /// <summary>Códigos canónicos usados hoy en Result → status esperado (tabla única).</summary>
    public static TheoryData<string, int> CodedFailures =>
        new()
        {
            { ApiResponseCodes.Common.NotFound, 404 },
            { ApiResponseCodes.Common.Forbidden, 403 },
            { ApiResponseCodes.Common.Unauthorized, 401 },
            { ApiResponseCodes.Common.ValidationError, 422 },
            { ApiResponseCodes.Common.Conflict, 409 },
            { ApiResponseCodes.Common.UniqueViolation, 409 },
            { ApiResponseCodes.Common.CompanyRucAlreadyExists, 409 },
            { ApiResponseCodes.Common.BadRequest, 400 },
            // ZH-API-ERROR-CONTRACT-HARDENING-01: antes caían al default 400 en Result.
            { ApiResponseCodes.Common.RateLimited, 429 },
            { ApiResponseCodes.Common.SriCommunicationError, 502 },
            { ApiResponseCodes.Common.ConcurrencyConflict, 409 },
            { ApiResponseCodes.Common.CompanyScopeForbidden, 403 },
            { ApiResponseCodes.Common.BranchScopeForbidden, 403 },
            { ApiResponseCodes.Common.DomainRuleViolation, 422 },
            { ApiResponseCodes.Common.DatabaseUnavailable, 503 },
            { ApiResponseCodes.Common.InternalError, 500 },
            { ApiResponseCodes.Common.InvalidDateTimeKind, 500 },
        };

    [Theory]
    [MemberData(nameof(CodedFailures))]
    public void ToOkOrBadRequest_y_ToCreatedOrBadRequest_usan_la_misma_tabla(string code, int status)
    {
        var failure = Result<string>.Failure("mensaje de dominio", code);

        Read(Controller().ToOkOrBadRequest(failure)).Should().BeEquivalentTo((status, code, new[] { "mensaje de dominio" }));
        Read(Controller().ToCreatedOrBadRequest(failure)).Should().BeEquivalentTo((status, code, new[] { "mensaje de dominio" }));
        Read(Controller().ApiFailure(failure)).Should().BeEquivalentTo((status, code, new[] { "mensaje de dominio" }));
    }

    [Fact]
    public void ApiFailure_sin_codigo_usa_el_fallback_del_endpoint_y_con_codigo_lo_ignora()
    {
        var uncoded = Result<string>.Failure("Refresh token inválido.");
        var rateLimited = Result<string>.Failure("Demasiados intentos.", ApiResponseCodes.Common.RateLimited);

        Read(Controller().ApiFailure(uncoded)).Should().BeEquivalentTo((400, ApiResponseCodes.Common.BadRequest, new[] { "Refresh token inválido." }));
        Read(Controller().ApiFailure(uncoded, ApiResponseCodes.Common.Unauthorized)).Should().BeEquivalentTo((401, ApiResponseCodes.Common.Unauthorized, new[] { "Refresh token inválido." }));
        Read(Controller().ApiFailure(rateLimited, ApiResponseCodes.Common.Unauthorized)).Should().BeEquivalentTo((429, ApiResponseCodes.Common.RateLimited, new[] { "Demasiados intentos." }));
    }

    [Fact]
    public void Codigo_no_catalogado_conserva_400_con_su_propio_code()
    {
        // Deuda ADR-027 Fase 1 (códigos de módulo aún literales): documentada, no silenciosa.
        var failure = Result<string>.ValidationFailure("Período cerrado.", "PERIOD_NOT_OPEN");

        Read(Controller().ToOkOrBadRequest(failure)).Should().BeEquivalentTo((400, "PERIOD_NOT_OPEN", new[] { "Período cerrado." }));
    }

    [Theory]
    [MemberData(nameof(CodedFailures))]
    public void ToOkOrNotFound_traduce_cada_codigo_con_la_tabla_unica(string code, int status)
    {
        var result = Controller().ToOkOrNotFound(Result<string>.Failure("mensaje de dominio", code));

        Read(result).Should().BeEquivalentTo((status, code, new[] { "mensaje de dominio" }));
    }

    [Theory]
    [MemberData(nameof(CodedFailures))]
    public void ToFileOrNotFound_usa_la_misma_regla_para_fallos(string code, int status)
    {
        var result = Controller().ToFileOrNotFound(Result<string>.Failure("mensaje", code), _ => new EmptyResult());

        Read(result).Should().BeEquivalentTo((status, code, new[] { "mensaje" }));
    }

    [Fact]
    public void Todo_fallo_con_codigo_se_responde_igual_que_ToOkOrBadRequest()
    {
        var codes = typeof(ApiResponseCodes.Common).GetFields()
            .Where(f => f.IsLiteral)
            .Select(f => (string)f.GetRawConstantValue()!)
            .Where(c => c is not ApiResponseCodes.Common.Ok and not ApiResponseCodes.Common.Created);

        foreach (var code in codes)
        {
            var failure = Result<string>.Failure("x", code);
            Read(Controller().ToOkOrNotFound(failure)).Should().BeEquivalentTo(Read(Controller().ToOkOrBadRequest(failure)), code);
            Read(Controller().ToFileOrNotFound(failure, _ => new EmptyResult())).Should().BeEquivalentTo(Read(Controller().ToOkOrBadRequest(failure)), code);
        }
    }

    [Fact]
    public void Fallo_sin_codigo_conserva_el_404_NOT_FOUND_historico()
    {
        var failure = Result<string>.Failure("Sucursal no encontrada.");

        Read(Controller().ToOkOrNotFound(failure)).Should().BeEquivalentTo((404, ApiResponseCodes.Common.NotFound, new[] { "Sucursal no encontrada." }));
        Read(Controller().ToFileOrNotFound(failure, _ => new EmptyResult())).Should().BeEquivalentTo((404, ApiResponseCodes.Common.NotFound, new[] { "Sucursal no encontrada." }));
    }

    [Fact]
    public void Exito_no_cambia()
    {
        Read(Controller().ToOkOrNotFound(Result<string>.Success("valor"))).Should().BeEquivalentTo((200, ApiResponseCodes.Common.Ok, Array.Empty<string>()));
        Controller().ToFileOrNotFound(Result<string>.Success("valor"), v => new ContentResult { Content = v })
            .Should().BeOfType<ContentResult>().Which.Content.Should().Be("valor");
    }
}

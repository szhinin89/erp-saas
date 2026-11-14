using System.Reflection;
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
/// ZH-B-MSG (API-DOC F-10 / F-33) — los rechazos de negocio de Contabilidad y Carga Inicial creados con
/// <c>Result.ValidationFailure(msg, code)</c> estaban fuera de <see cref="MessageCatalog"/>: caían en el
/// fallback (400 + «Ocurrió un error inesperado»). Ahora cada código está catalogado como Validation
/// (422) con mensaje propio, conservando el valor público del <c>code</c>.
/// </summary>
public sealed class AccountingInitialLoadValidationCodesTests
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

    /// <summary>
    /// Contrato público (valores del campo <c>code</c>) de los códigos de F-10/F-33: se fija el literal
    /// para que catalogar no pueda renombrar ninguno (el frontend lee, p. ej., <c>PERIOD_NOT_OPEN</c>).
    /// </summary>
    private static readonly string[] PublicCodes =
    [
        "PERIOD_NOT_OPEN", "MANUAL_FUTURE_DATE", "ACCOUNTING_NOT_ENABLED",
        "OPENING_REVERSAL_REQUIRES_CORRECTION_FLOW",
        "FISCAL_YEAR_PERIODS_INCOMPLETE", "FISCAL_YEAR_LAST_PERIOD_NOT_OPEN", "FISCAL_YEAR_PREVIOUS_PERIODS_OPEN",
        "FISCAL_YEAR_NOT_ENDED", "FISCAL_YEAR_ACCOUNTING_INCOMPLETE", "FISCAL_YEAR_DRAFT_ENTRIES",
        "YEAR_END_RESULT_ACCOUNT_INVALID", "YEAR_END_CLOSING_DRAFT_EXISTS", "YEAR_END_CLOSING_ALREADY_POSTED",
        "YEAR_END_CLOSING_NOTHING_TO_CLOSE", "YEAR_END_CLOSING_OUTDATED", "YEAR_END_CLOSING_NOT_EDITABLE",
        "YEAR_END_CLOSING_REVERSAL_DATE",
        "INITIAL_LOAD_CLOSED", "INITIAL_LOAD_CLOSE_BLOCKED", "OPENING_SOURCE_CHANGED", "OPENING_DATE_MISSING",
        "OPENING_DATE_MISMATCH", "OPENING_ACCOUNTING_NOT_APPLICABLE", "OPENING_ASI_RULE_HAS_FIXED_LINES",
        "OPENING_CONTROL_RULE_MISSING", "OPENING_BRIDGE_AMBIGUOUS", "OPENING_CONTROL_ACCOUNT_BLOCKED",
        "OPENING_BRIDGE_NOT_CLEARED", "OPENING_ASI_NOT_CURRENT", "OPENING_ASI_NOT_POSTED",
        "OPENING_ASI_JOURNAL_MISMATCH", "BATCH_NOT_ACCOUNTABLE", "BATCH_NOT_COMPLETED", "ALREADY_POSTED",
        "SOURCE_EMPTY", "SOURCE_COST_MISSING", "AMOUNT_NOT_POSITIVE",
    ];

    /// <summary>
    /// Excepción deliberada: nace de un <c>catch (Exception)</c> técnico al publicar (no de una regla de
    /// negocio), así que es InternalError (500) y no Validation. Su valor se conserva porque queda persistido
    /// en el intento y se reproduce en los reintentos idempotentes.
    /// </summary>
    private const string ManualPostInternal = "MANUAL_POST_INTERNAL";

    private static IEnumerable<string> DeclaredCodes(Type holder) =>
        holder
            .GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(f => f.IsLiteral && f.FieldType == typeof(string))
            .Select(f => (string)f.GetRawConstantValue()!);

    public static TheoryData<string> Codes()
    {
        var data = new TheoryData<string>();
        foreach (var code in PublicCodes)
            data.Add(code);
        return data;
    }

    [Fact]
    public void Los_codigos_declarados_son_exactamente_el_contrato_publico_fijado()
    {
        var declared = DeclaredCodes(typeof(ApiResponseCodes.Accounting))
            .Concat(DeclaredCodes(typeof(ApiResponseCodes.InitialLoad)));

        declared.Should().BeEquivalentTo(PublicCodes.Append(ManualPostInternal));
    }

    [Fact]
    public void MANUAL_POST_INTERNAL_es_error_interno_500_con_mensaje_propio_y_sin_detalle_tecnico()
    {
        ApiResponseCodes.Accounting.ManualPostInternal.Should().Be(ManualPostInternal);
        var entry = MessageCatalog.Resolve(ManualPostInternal);
        entry.Category.Should().Be(ApiErrorCategory.InternalError);
        entry.User.Should().Be("No se pudo completar la publicación. Consulte el intento antes de volver a publicar.");
        ApiErrorStatus.For(ManualPostInternal).Should().Be(500);
        ApiErrorStatus.ExposesDetail(ManualPostInternal).Should().BeFalse();

        var result = Controller().ToOkOrBadRequest(Result<string>.Failure("detalle técnico", ManualPostInternal));

        var obj = result.Should().BeAssignableTo<ObjectResult>().Subject;
        obj.StatusCode.Should().Be(500);
        var json = JsonSerializer.SerializeToElement(obj.Value);
        json.GetProperty("Code").GetString().Should().Be(ManualPostInternal);
        json.GetProperty("Message").GetProperty("User").GetString().Should().Be(entry.User);
        json.GetProperty("Data").ValueKind.Should().Be(JsonValueKind.Null);
    }

    [Theory]
    [MemberData(nameof(Codes))]
    public void Cada_codigo_esta_catalogado_como_Validation_con_mensaje_propio(string code)
    {
        MessageCatalog.RegisteredCodes.Should().Contain(code);

        var entry = MessageCatalog.Resolve(code);
        entry.Category.Should().Be(ApiErrorCategory.Validation);
        entry.User.Should().NotBe("Ocurrió un error inesperado.");
        entry.Dev.Should().NotBe("Unmapped response code.");
        ApiErrorStatus.For(code).Should().Be(422);
    }

    [Theory]
    [MemberData(nameof(Codes))]
    public void ValidationFailure_responde_422_conserva_el_code_y_el_detalle_especifico(string code)
    {
        const string detail = "Detalle específico del rechazo.";
        var failure = Result<string>.ValidationFailure(detail, code);

        foreach (var result in new[] { Controller().ToOkOrBadRequest(failure), Controller().ApiFailure(failure) })
        {
            var obj = result.Should().BeAssignableTo<ObjectResult>().Subject;
            obj.StatusCode.Should().Be(422);

            var json = JsonSerializer.SerializeToElement(obj.Value);
            json.GetProperty("Code").GetString().Should().Be(code);
            json.GetProperty("Message").GetProperty("User").GetString()
                .Should().Be(MessageCatalog.Resolve(code).User);
            json.GetProperty("Data").GetProperty("errors").EnumerateArray()
                .Select(e => e.GetString()).Should().Equal(detail);
        }
    }

    [Fact]
    public void Un_codigo_ajeno_no_catalogado_conserva_el_fallback_400()
    {
        // F-10/F-33 no aplican reglas globales: los demás códigos literales no cambian de status.
        MessageCatalog.RegisteredCodes.Should().NotContain("SKU_DUPLICATE");
        ApiErrorStatus.For("SKU_DUPLICATE").Should().Be(400);
    }
}

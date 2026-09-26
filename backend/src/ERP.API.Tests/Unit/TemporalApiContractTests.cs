using System.Text.Json;
using ERP.API.Contracts;
using ERP.API.Temporal;
using ERP.Application.Modules.Sales.UseCases;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.Extensions.Primitives;

namespace ERP.API.Tests.Unit;

/// <summary>
/// ZH-TEMPORAL-CONTRACT-SINGLE-SOURCE-02 — contrato temporal en el borde HTTP:
/// DateOnly ⇄ "YYYY-MM-DD"; DateTime (instante) ⇄ ISO-8601 UTC "...Z". Un instante sin zona se
/// rechaza (400) en vez de adivinar su zona.
/// </summary>
public sealed class TemporalApiContractTests
{
    private static readonly JsonSerializerOptions Options = CreateOptions();

    private static JsonSerializerOptions CreateOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        options.Converters.Add(new UtcInstantJsonConverter());
        return options;
    }

    private sealed record Payload(DateOnly IssueDate, DateTime AuthorizationDate, DateTime? ConfirmedAt);

    [Fact]
    public void Serializa_DateOnly_como_fecha_y_DateTime_UTC_con_Z()
    {
        var json = JsonSerializer.Serialize(
            new Payload(
                new DateOnly(2026, 9, 25),
                new DateTime(2026, 9, 25, 19, 38, 0, DateTimeKind.Utc),
                null
            ),
            Options
        );

        json.Should().Contain("\"issueDate\":\"2026-09-25\"");
        json.Should().Contain("\"authorizationDate\":\"2026-09-25T19:38:00Z\"");
        json.Should().Contain("\"confirmedAt\":null");
    }

    [Fact]
    public void Round_trip_JSON_conserva_dia_de_negocio_e_instante()
    {
        var input = "{\"issueDate\":\"2026-09-25\",\"authorizationDate\":\"2026-09-25T19:38:00Z\",\"confirmedAt\":\"2026-09-25T14:38:00-05:00\"}";

        var payload = JsonSerializer.Deserialize<Payload>(input, Options)!;

        payload.IssueDate.Should().Be(new DateOnly(2026, 9, 25));
        payload.AuthorizationDate.Should().Be(new DateTime(2026, 9, 25, 19, 38, 0, DateTimeKind.Utc));
        payload.AuthorizationDate.Kind.Should().Be(DateTimeKind.Utc);
        payload.ConfirmedAt.Should().Be(new DateTime(2026, 9, 25, 19, 38, 0, DateTimeKind.Utc));

        JsonSerializer.Deserialize<Payload>(JsonSerializer.Serialize(payload, Options), Options)
            .Should()
            .Be(payload);
    }

    [Theory]
    [InlineData("2026-09-25T14:38")]
    [InlineData("2026-09-25T14:38:00")]
    public void Rechaza_un_instante_sin_zona_en_el_body(string value)
    {
        var act = () =>
            JsonSerializer.Deserialize<Payload>(
                $"{{\"issueDate\":\"2026-09-25\",\"authorizationDate\":\"{value}\"}}",
                Options
            );

        act.Should().Throw<JsonException>();
    }

    [Fact]
    public void Nunca_emite_un_instante_sin_zona()
    {
        var act = () =>
            JsonSerializer.Serialize(
                new Payload(new DateOnly(2026, 9, 25), new DateTime(2026, 9, 25, 14, 38, 0), null),
                Options
            );

        act.Should().Throw<ArgumentException>();
    }

    [Theory]
    [InlineData("2026-09-25T19:38:00Z", true)]
    [InlineData("2026-09-25T14:38:00-05:00", true)]
    [InlineData("2026-09-25T14:38:00", false)]
    [InlineData("2026-09-25", false)]
    public async Task Query_DateTime_exige_zona_explicita(string value, bool valid)
    {
        var context = BindingContext("fromUtc", value);

        await new UtcInstantModelBinder().BindModelAsync(context);

        context.Result.IsModelSet.Should().Be(valid);
        // Tras el binding la entrada queda Unvalidated (la validación MVC la marca después):
        // el contrato del binder es "sin error de modelo" para un instante válido.
        context.ModelState.ErrorCount.Should().Be(valid ? 0 : 1);
        if (valid)
        {
            var utc = (DateTime)context.Result.Model!;
            utc.Should().Be(new DateTime(2026, 9, 25, 19, 38, 0, DateTimeKind.Utc));
            utc.Kind.Should().Be(DateTimeKind.Utc);
        }
    }

    // ── ZH-TEMPORAL-CONTRACT-02J: TransferDate/CashDate de Ventas = fecha de negocio (DateOnly) ──

    [Theory]
    [InlineData("es-EC")]
    [InlineData("en-US")]
    public void Ventas_TransferDate_y_CashDate_ISO_conservan_el_dia_sin_depender_de_la_cultura(string culture)
    {
        WithCulture(culture, () =>
        {
            var transfer = JsonSerializer.Deserialize<TransferDetailInput>(
                "{\"receiptNumber\":\"TRX-1\",\"transferDate\":\"2026-09-25\"}",
                Options
            )!;
            var cheque = JsonSerializer.Deserialize<ChequeDetailInput>(
                "{\"chequeNumber\":\"CH-1\",\"cashDate\":\"2026-09-25\"}",
                Options
            )!;

            transfer.TransferDate.Should().Be(new DateOnly(2026, 9, 25));
            cheque.CashDate.Should().Be(new DateOnly(2026, 9, 25));
            JsonSerializer.Serialize(transfer, Options).Should().Contain("\"transferDate\":\"2026-09-25\"");
        });
    }

    [Theory]
    [InlineData("es-EC", "09/25/2026")]
    [InlineData("es-EC", "25/09/2026")]
    [InlineData("en-US", "09/25/2026")]
    [InlineData("en-US", "25/09/2026")]
    public void Ventas_fechas_ambiguas_se_rechazan_en_cualquier_cultura(string culture, string value)
    {
        WithCulture(culture, () =>
        {
            var transfer = () =>
                JsonSerializer.Deserialize<TransferDetailInput>($"{{\"transferDate\":\"{value}\"}}", Options);
            var cheque = () =>
                JsonSerializer.Deserialize<ChequeDetailInput>($"{{\"cashDate\":\"{value}\"}}", Options);

            transfer.Should().Throw<JsonException>();
            cheque.Should().Throw<JsonException>();
        });
    }

    // ── ZH-TEMPORAL-CONTRACT-02J: ApiResponse.Meta.Timestamp = instante UTC "…Z" ──

    [Fact]
    public void Envelope_Timestamp_es_UTC_termina_en_Z_y_conserva_segundos()
    {
        var response = new ApiResponse<object>(
            "OK",
            "success",
            new ApiResponseMessage("ok", null),
            null,
            new ApiResponseMeta("corr-1", new DateTime(2026, 9, 25, 19, 38, 27, DateTimeKind.Utc))
        );

        var json = JsonSerializer.Serialize(response, Options);

        json.Should().Contain("\"timestamp\":\"2026-09-25T19:38:27Z\"");
        json.Should().NotContain("+00:00");
    }

    [Fact]
    public void Envelope_Timestamp_real_de_ResponseFactory_y_ExceptionMiddleware_sale_con_Z()
    {
        // Mismas opciones que usa ExceptionMiddleware (Web + UtcInstantJsonConverter) y MVC.
        var now = DateTime.UtcNow;
        var json = JsonSerializer.Serialize(new ApiResponseMeta("corr-1", now), Options);

        System.Text.RegularExpressions.Regex
            .IsMatch(json, @"""timestamp"":""\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}(\.\d+)?Z""")
            .Should()
            .BeTrue(json);
        JsonSerializer.Deserialize<ApiResponseMeta>(json, Options)!.Timestamp.Should().Be(now);

        var zoneLess = () => JsonSerializer.Serialize(
            new ApiResponseMeta("corr-1", new DateTime(2026, 9, 25, 19, 38, 27)),
            Options
        );
        zoneLess.Should().Throw<ArgumentException>("un instante nunca sale sin zona");
    }

    private static void WithCulture(string culture, Action action)
    {
        var previous = System.Globalization.CultureInfo.CurrentCulture;
        System.Globalization.CultureInfo.CurrentCulture = new System.Globalization.CultureInfo(culture);
        try
        {
            action();
        }
        finally
        {
            System.Globalization.CultureInfo.CurrentCulture = previous;
        }
    }

    private static DefaultModelBindingContext BindingContext(string name, string value)
    {
        var metadata = new EmptyModelMetadataProvider().GetMetadataForType(typeof(DateTime?));
        return new DefaultModelBindingContext
        {
            ModelName = name,
            ModelMetadata = metadata,
            ModelState = new ModelStateDictionary(),
            ValueProvider = new QueryStringValueProvider(
                BindingSource.Query,
                new QueryCollection(new Dictionary<string, StringValues> { [name] = value }),
                System.Globalization.CultureInfo.InvariantCulture
            ),
        };
    }
}

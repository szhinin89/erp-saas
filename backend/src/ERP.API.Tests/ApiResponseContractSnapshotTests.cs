using ERP.API.Extensions;
using ERP.Application.Common;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.FileProviders;
using System.Text.Json;

namespace ERP.API.Tests;

/// <summary>
/// Fase 2 (hardening del contrato de respuesta): congela la forma exacta del JSON
/// serializado de <c>ApiResponse&lt;T&gt;</c>. Cualquier cambio de nombre de propiedad,
/// reaparición de un campo legacy (<c>success</c>, <c>status</c>, <c>responseObject</c>,
/// <c>userMessage</c>, <c>developerMessage</c>) o alteración del envelope debe romper
/// estos tests.
/// </summary>
public sealed class ApiResponseContractSnapshotTests
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

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private static readonly string[] RootKeys = ["code", "severity", "message", "data", "meta"];
    private static readonly string[] MessageKeys = ["user", "dev"];
    private static readonly string[] MetaKeys = ["correlationId", "timestamp", "traceId"];

    // Fragmentos de NOMBRE DE PROPIEDAD (con ':' final) de envelopes anteriores.
    // "severity":"success" es válido; "success": no debe existir como clave.
    private static readonly string[] ForbiddenFragments =
    [
        "\"success\":",
        "\"status\":",
        "\"responseObject\":",
        "\"userMessage\":",
        "\"developerMessage\":",
        "\"errorCode\":",
        "\"ErrorCode\":",
        "\"warnings\":",
        "\"Warnings\":",
    ];

    [Fact]
    public void Success_response_serializes_to_exact_envelope_shape()
    {
        var context = new DefaultHttpContext { TraceIdentifier = "trace-success" };
        var environment = new FakeWebHostEnvironment();

        var response = ResponseFactory.Success(
            context,
            environment,
            ApiResponseCodes.Common.Ok,
            new { id = 1 }
        );
        var json = JsonSerializer.Serialize(response, JsonOptions);
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        PropertyNames(root).Should().BeEquivalentTo(RootKeys);
        PropertyNames(root.GetProperty("message")).Should().BeEquivalentTo(MessageKeys);
        PropertyNames(root.GetProperty("meta")).Should().BeEquivalentTo(MetaKeys);

        root.GetProperty("code").GetString().Should().Be(ApiResponseCodes.Common.Ok);
        root.GetProperty("severity").GetString().Should().Be(ApiSeverity.Success);
        root.GetProperty("data").GetProperty("id").GetInt32().Should().Be(1);
        root.GetProperty("meta")
            .GetProperty("correlationId")
            .GetString()
            .Should()
            .Be("trace-success");
    }

    [Fact]
    public void Error_response_serializes_to_exact_envelope_shape()
    {
        var context = new DefaultHttpContext { TraceIdentifier = "trace-error" };
        var environment = new FakeWebHostEnvironment();

        var response = ResponseFactory.Error(
            context,
            environment,
            ApiResponseCodes.Common.NotFound,
            ["El item 42 no existe."]
        );
        var json = JsonSerializer.Serialize(response, JsonOptions);
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        PropertyNames(root).Should().BeEquivalentTo(RootKeys);
        PropertyNames(root.GetProperty("message")).Should().BeEquivalentTo(MessageKeys);
        PropertyNames(root.GetProperty("meta")).Should().BeEquivalentTo(MetaKeys);

        root.GetProperty("code").GetString().Should().Be(ApiResponseCodes.Common.NotFound);
        root.GetProperty("severity").GetString().Should().Be(ApiSeverity.Error);

        var data = root.GetProperty("data");
        PropertyNames(data).Should().BeEquivalentTo(["errors"]);
        data.GetProperty("errors")[0].GetString().Should().Be("El item 42 no existe.");
    }

    [Fact]
    public void Error_response_without_dynamic_detail_has_null_data()
    {
        var context = new DefaultHttpContext();
        var environment = new FakeWebHostEnvironment();

        var response = ResponseFactory.Error(
            context,
            environment,
            ApiResponseCodes.Common.InternalError
        );
        var json = JsonSerializer.Serialize(response, JsonOptions);
        using var doc = JsonDocument.Parse(json);

        doc.RootElement.GetProperty("data").ValueKind.Should().Be(JsonValueKind.Null);
    }

    [Theory]
    [InlineData("Development")]
    [InlineData("Production")]
    public void Message_dev_visibility_follows_environment(string environmentName)
    {
        var context = new DefaultHttpContext();
        var environment = new FakeWebHostEnvironment { EnvironmentName = environmentName };

        var response = ResponseFactory.Error(
            context,
            environment,
            ApiResponseCodes.Common.InternalError
        );
        var json = JsonSerializer.Serialize(response, JsonOptions);
        using var doc = JsonDocument.Parse(json);

        var dev = doc.RootElement.GetProperty("message").GetProperty("dev");

        if (environmentName == "Development")
            dev.ValueKind.Should().Be(JsonValueKind.String);
        else
            dev.ValueKind.Should().Be(JsonValueKind.Null);
    }

    [Fact]
    public void Envelope_json_never_reintroduces_legacy_fields()
    {
        var context = new DefaultHttpContext();
        var environment = new FakeWebHostEnvironment { EnvironmentName = "Development" };

        var success = ResponseFactory.Success(
            context,
            environment,
            ApiResponseCodes.Common.Created,
            new { id = 1, name = "x" }
        );
        var error = ResponseFactory.Error(
            context,
            environment,
            ApiResponseCodes.Common.ValidationError,
            ["Campo: inválido"]
        );

        var successJson = JsonSerializer.Serialize(success, JsonOptions);
        var errorJson = JsonSerializer.Serialize(error, JsonOptions);

        foreach (var fragment in ForbiddenFragments)
        {
            successJson.Should().NotContain(fragment);
            errorJson.Should().NotContain(fragment);
        }
    }

    [Fact]
    public void Envelope_json_property_names_are_camelCase()
    {
        var context = new DefaultHttpContext();
        var environment = new FakeWebHostEnvironment { EnvironmentName = "Development" };

        var response = ResponseFactory.Success(
            context,
            environment,
            ApiResponseCodes.Common.Ok,
            new { itemId = 1 }
        );
        var json = JsonSerializer.Serialize(response, JsonOptions);
        using var doc = JsonDocument.Parse(json);

        AssertCamelCaseRecursively(doc.RootElement);
    }

    private static void AssertCamelCaseRecursively(JsonElement element)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                foreach (var property in element.EnumerateObject())
                {
                    var name = property.Name;
                    name.Should().NotBeEmpty();
                    char.IsUpper(name[0]).Should().BeFalse($"'{name}' debe estar en camelCase");
                    AssertCamelCaseRecursively(property.Value);
                }
                break;
            case JsonValueKind.Array:
                foreach (var item in element.EnumerateArray())
                    AssertCamelCaseRecursively(item);
                break;
        }
    }

    private static IEnumerable<string> PropertyNames(JsonElement element) =>
        element.EnumerateObject().Select(p => p.Name);
}

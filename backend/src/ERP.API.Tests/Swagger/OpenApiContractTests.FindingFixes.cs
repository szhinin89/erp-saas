using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using ERP.API.Tests.Support;
using ERP.Application.Access.Authorization;
using FluentAssertions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.ApiExplorer;
using Microsoft.AspNetCore.Mvc.Formatters;
using Microsoft.Extensions.DependencyInjection;

namespace ERP.API.Tests.Swagger;

/// <summary>
/// API-DOC-V1 — correcciones de los hallazgos F-01..F-52 que afectaban al contrato publicado (F-07, F-16, F-46,
/// F-47, F-48, F-50). Solo documento: cada prueba fija el contrato corregido y lo contrasta con el comportamiento
/// HTTP real, sin cambiar controladores en ejecución.
/// </summary>
public sealed partial class OpenApiContractTests
{
    /// <summary>
    /// Operaciones públicas (<c>security: [{}]</c>) que responden 401 con envelope por credenciales o token del caso de
    /// uso, no por falta de JWT. Cualquier otra operación pública no debe declarar 401.
    /// </summary>
    private static readonly string[] PublicOperationsThatReturn401 =
    [
        "Auth_CompletePasswordReset",
        "Auth_GlobalLogin",
        "Auth_Login",
        "Auth_Reauthenticate",
        "Auth_Refresh",
        "Setup_CreateInitialAdmin",
    ];

    private static bool IsPublic(JsonObject op) =>
        op["security"]!.AsArray().All(r => r!.AsObject().Count == 0);

    // ── F-46: 401 en operaciones públicas ───────────────────────────────────────────────────────

    [Fact]
    public void F46_las_operaciones_publicas_declaran_401_solo_si_el_caso_de_uso_lo_emite_y_no_exigen_Bearer()
    {
        var publicWith401 = Operations()
            .Where(o => IsPublic(o.Op) && o.Op["responses"]!.AsObject().ContainsKey("401"))
            .Select(o => o.Op["operationId"]!.GetValue<string>())
            .Order(StringComparer.Ordinal)
            .ToList();
        publicWith401.Should().Equal(PublicOperationsThatReturn401);

        foreach (var id in PublicOperationsThatReturn401)
        {
            var op = OperationById(id);
            // La seguridad real no cambia: sigue sin exigir Bearer ni permisos.
            op["security"]!.AsArray().Should().ContainSingle().Which!.AsObject().Should().BeEmpty(id);
            op.ContainsKey("x-required-permissions").Should().BeFalse(id);
            // El 401 de un caso de uso viaja en el envelope (a diferencia del rechazo del middleware JWT, sin cuerpo).
            op["responses"]!["401"]!["content"]!["application/json"]!["schema"]!["$ref"]!
                .GetValue<string>()
                .Should()
                .EndWith("/ApiResponseOfObject", id);
            op["responses"]!["401"]!["description"]!.GetValue<string>().Should().Contain("`UNAUTHORIZED`", id);
        }
    }

    [Fact]
    public void F46_cada_401_publico_corresponde_a_una_declaracion_explicita_de_la_accion()
    {
        // El 401 público nunca lo inventa el filtro común: viene de [ProducesResponseType(401)] de la acción.
        foreach (var description in ApiExplorerOperations())
        {
            var path = "/" + description.RelativePath!.Split('?')[0];
            var op = FindOperation(path, description.HttpMethod!);
            if (!IsPublic(op))
                continue;

            var declares401 = description
                .ActionDescriptor.EndpointMetadata.OfType<IApiResponseMetadataProvider>()
                .Any(p => p.StatusCode == 401);
            op["responses"]!.AsObject().ContainsKey("401").Should().Be(declares401, $"{description.HttpMethod} {path}");
        }
    }

    [Fact]
    public async Task F46_refresh_y_reauthenticate_sin_credenciales_responden_401_con_envelope_UNAUTHORIZED()
    {
        var client = _factory.CreateClient();

        foreach (var path in new[] { "/api/v1/auth/refresh", "/api/v1/auth/reauthenticate" })
        {
            var response = await client.PostAsJsonAsync(path, new { password = "no-es-una-credencial" });

            response.StatusCode.Should().Be(HttpStatusCode.Unauthorized, path);
            response.Content.Headers.ContentType!.MediaType.Should().Be("application/json", path);
            var body = (await response.Content.ReadFromJsonAsync<JsonObject>())!;
            body["code"]!.GetValue<string>().Should().Be("UNAUTHORIZED", path);
        }
    }

    // ── F-50: parámetros de consulta de Warehouses.GetAll ───────────────────────────────────────

    [Fact]
    public void F50_Warehouses_GetAll_publica_sus_cuatro_parametros_opcionales_sin_422()
    {
        var op = OperationById("Warehouses_GetAll");
        var parameters = op["parameters"]!.AsArray().Select(p => p!.AsObject()).ToList();

        parameters.Select(p => p["name"]!.GetValue<string>()).Should().Equal("activeStatus", "onlyActive", "search", "sucursalId");
        parameters.Should().OnlyContain(p => p["in"]!.GetValue<string>() == "query");
        parameters.Should().OnlyContain(p => p["required"] == null || p["required"]!.GetValue<bool>() == false);
        parameters.Should().OnlyContain(p => !string.IsNullOrWhiteSpace(p["description"]!.GetValue<string>()));

        var status = parameters[0]["schema"]!;
        status["type"]!.GetValue<string>().Should().Be("string");
        status["enum"]!.AsArray().Select(v => v!.GetValue<string>()).Should().Equal("all", "active", "inactive");
        status["default"]!.GetValue<string>().Should().Be("active");
        parameters[1]["schema"]!["type"]!.GetValue<string>().Should().Be("boolean");
        parameters[2]["schema"]!["type"]!.GetValue<string>().Should().Be("string");
        parameters[3]["schema"]!["format"]!.GetValue<string>().Should().Be("uuid");

        // Los valores no se enlazan al modelo: ningún valor es rechazado, así que el contrato no promete 422.
        op["responses"]!.AsObject().ContainsKey("422").Should().BeFalse();
    }

    [Fact]
    public async Task F50_Warehouses_GetAll_tolera_valores_no_reconocidos_sin_422()
    {
        await using var app = _factory.WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
                services.AddScoped<IRuntimePermissionAuthorizer, AllowAllPermissionAuthorizer>()
            )
        );
        var client = app.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            TestJwtFactory.CreateSessionJwt(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "Admin")
        );

        var response = await client.GetAsync(
            "/api/v1/inventory/warehouses?activeStatus=desconocido&onlyActive=no-es-bool&sucursalId=no-es-guid&search=%20"
        );

        response.StatusCode.Should().NotBe(HttpStatusCode.UnprocessableEntity, await response.Content.ReadAsStringAsync());
    }

    // ── F-07 / F-47 / F-48: id de ruta distinto al del cuerpo ───────────────────────────────────

    [Fact]
    public void F07_F47_F48_los_cuatro_Update_con_comprobacion_de_id_declaran_el_400_y_su_cuerpo_real()
    {
        foreach (var id in new[]
                 {
                     "CashMovementReasons_Update",
                     "InventoryAdjustmentReasons_Update",
                     "PaymentTerms_Update",
                     "StockAdjustments_UpdateAdjustment",
                 })
        {
            var response = OperationById(id)["responses"]!["400"];
            response.Should().NotBeNull($"{id}: el id distinto responde 400");
            var description = response!["description"]!.GetValue<string>();
            description.Should().Contain("id de la ruta", id).And.Contain("sin envelope", id);
        }

        OperationById("PaymentTerms_Update")["responses"]!["400"]!["description"]!
            .GetValue<string>()
            .Should()
            .Contain("Cadena JSON").And.NotContain("texto plano");
        foreach (var id in new[] { "CashMovementReasons_Update", "InventoryAdjustmentReasons_Update", "StockAdjustments_UpdateAdjustment" })
            OperationById(id)["responses"]!["400"]!["description"]!.GetValue<string>().Should().Contain("ProblemDetails", id).And.NotContain("cuerpo vacío", id);
    }

    [Fact]
    public async Task F07_F47_F48_el_id_distinto_responde_400_sin_envelope_con_el_cuerpo_documentado()
    {
        await using var app = _factory.WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
                services.AddScoped<IRuntimePermissionAuthorizer, AllowAllPermissionAuthorizer>()
            )
        );
        var client = app.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            TestJwtFactory.CreateSessionJwt(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "Admin")
        );
        var routeId = Guid.NewGuid();
        var bodyId = Guid.NewGuid();

        var cases = new (string Url, object Body, string? PlainText)[]
        {
            ($"/api/v1/cash-movement-reasons/{routeId}", new { id = bodyId, name = "Motivo", sortOrder = 1 }, null),
            (
                $"/api/v1/inventory/adjustment-reasons/{routeId}",
                new { id = bodyId, name = "Motivo", allowedMovementType = "In", requiresNotes = false, sortOrder = 1 },
                null
            ),
            (
                $"/api/v1/inventory/stock/adjustments/{routeId}",
                new
                {
                    id = bodyId,
                    warehouseId = Guid.NewGuid(),
                    warehouseName = "Bodega",
                    movementType = "In",
                    reasonId = Guid.NewGuid(),
                    notes = (string?)null,
                    lines = Array.Empty<object>(),
                },
                null
            ),
            ($"/api/v1/master/payment-terms/{routeId}", new { id = bodyId, name = "Contado", installments = 1, daysBetweenInstallments = 0 }, "ID mismatch."),
        };

        foreach (var (url, body, plainText) in cases)
        {
            var response = await client.PutAsJsonAsync(url, body);
            var text = await response.Content.ReadAsStringAsync();

            response.StatusCode.Should().Be(HttpStatusCode.BadRequest, $"{url}: {text}");
            if (plainText is null)
            {
                // `BadRequest()` sin valor dentro de un [ApiController] lo convierte ASP.NET Core en ProblemDetails
                // (RFC 9110), no en cuerpo vacío ni en el envelope de la API.
                response.Content.Headers.ContentType!.MediaType.Should().EndWith("json", url);
                var problem = JsonNode.Parse(text)!.AsObject();
                problem["status"]!.GetValue<int>().Should().Be(400, url);
                problem["title"]!.GetValue<string>().Should().Be("Bad Request", url);
                problem.ContainsKey("traceId").Should().BeTrue(url);
                problem.ContainsKey("code").Should().BeFalse($"{url}: no es el envelope de la API");
                // Lo observado cabe en el esquema publicado: sus propiedades más `traceId` (extensión adicional).
                var published = _doc["components"]!["schemas"]!["ProblemDetails"]!["properties"]!.AsObject().Select(p => p.Key).Append("traceId");
                problem.Select(p => p.Key).Should().BeSubsetOf(published, url);
            }
            else
            {
                // `BadRequest("...")` serializa la cadena como JSON (application/json), no como text/plain.
                response.Content.Headers.ContentType!.MediaType.Should().Be("application/json", url);
                JsonNode.Parse(text)!.GetValue<string>().Should().Be(plainText, url);
            }
        }
    }

    // ── F-16 y archivos: tipos de medio publicados ──────────────────────────────────────────────

    [Fact]
    public void F16_ninguna_respuesta_publica_text_plain_ni_text_json()
    {
        // La API solo emite application/json (envelope) o el archivo declarado: text/plain y text/json salían de
        // controladores sin [Produces] (ManualJournal, Retentions).
        var offenders = Operations()
            .SelectMany(o =>
                o.Op["responses"]!.AsObject()
                    .SelectMany(r =>
                        (r.Value!["content"]?.AsObject() ?? [])
                            .Where(c => c.Key is "text/plain" or "text/json")
                            .Select(c => $"{o.Method.ToUpperInvariant()} {o.Path} {r.Key} {c.Key}")
                    )
            )
            .ToList();

        offenders.Should().BeEmpty();
    }

    [Fact]
    public void Las_respuestas_2xx_con_tipo_de_medio_explicito_publican_exactamente_ese_tipo()
    {
        // [ProducesResponseType(typeof(byte[]), 200, "application/pdf")] debe publicarse como application/pdf aunque el
        // [Produces("application/json")] de clase lo reemplace en ApiExplorer (Ride, plantilla de carga inicial, logos).
        var checkedOperations = new List<string>();
        foreach (var description in ApiExplorerOperations())
        {
            var path = "/" + description.RelativePath!.Split('?')[0];
            foreach (var provider in description
                         .ActionDescriptor.EndpointMetadata.OfType<IApiResponseMetadataProvider>()
                         .Where(p => p.StatusCode is >= 200 and < 300 && p.Type is not null && p.Type != typeof(void)))
            {
                var explicitTypes = new MediaTypeCollection();
                provider.SetContentTypes(explicitTypes);
                if (explicitTypes.Count == 0)
                    continue;

                var op = FindOperation(path, description.HttpMethod!);
                var content = op["responses"]![provider.StatusCode.ToString()]!["content"]!.AsObject();
                content
                    .Select(c => c.Key)
                    .Order(StringComparer.Ordinal)
                    .Should()
                    .Equal(explicitTypes.Order(StringComparer.Ordinal), $"{description.HttpMethod} {path}");
                checkedOperations.Add(op["operationId"]!.GetValue<string>());
            }
        }

        checkedOperations
            .Order(StringComparer.Ordinal)
            .Should()
            .Equal(
                "CompanyProfile_GetLogoAltContent",
                "CompanyProfile_GetLogoContent",
                "InitialLoad_DownloadTemplate",
                "Retentions_GetElectronicXml",
                "Retentions_GetRidePdf",
                "Ride_GetContent"
            );
    }

    // ── Los filtros nuevos no quitan ni alteran nada ajeno a su objeto ──────────────────────────

    [Fact]
    public void Los_filtros_solo_quitan_text_plain_y_text_json_y_conservan_los_demas_tipos_de_ApiExplorer()
    {
        // Todo tipo de medio que ApiExplorer anuncia para una respuesta, salvo text/plain y text/json, sigue en el
        // documento; las únicas respuestas cuyo tipo cambia son las 2xx con tipo explícito (archivos).
        foreach (var description in ApiExplorerOperations())
        {
            var path = "/" + description.RelativePath!.Split('?')[0];
            var op = FindOperation(path, description.HttpMethod!);
            var explicitStatuses = description
                .ActionDescriptor.EndpointMetadata.OfType<IApiResponseMetadataProvider>()
                .Where(p =>
                {
                    var types = new MediaTypeCollection();
                    p.SetContentTypes(types);
                    return p.StatusCode is >= 200 and < 300 && types.Count > 0;
                })
                .Select(p => p.StatusCode)
                .ToHashSet();

            foreach (var responseType in description.SupportedResponseTypes.Where(r => r.StatusCode > 0))
            {
                if (explicitStatuses.Contains(responseType.StatusCode))
                    continue;
                var published = op["responses"]![responseType.StatusCode.ToString()]?["content"]?.AsObject();
                foreach (var format in responseType.ApiResponseFormats.Select(f => f.MediaType).Where(m => m is not ("text/plain" or "text/json")))
                {
                    published.Should().NotBeNull($"{description.HttpMethod} {path} {responseType.StatusCode}");
                    published!.ContainsKey(format).Should().BeTrue($"{description.HttpMethod} {path} {responseType.StatusCode} {format}");
                }
            }
        }
    }

    [Fact]
    public void Los_filtros_no_alteran_los_parametros_de_ruta_ni_de_consulta_fuera_de_Warehouses_GetAll()
    {
        foreach (var description in ApiExplorerOperations())
        {
            var path = "/" + description.RelativePath!.Split('?')[0];
            var op = FindOperation(path, description.HttpMethod!);
            if (op["operationId"]!.GetValue<string>() == "Warehouses_GetAll")
                continue;

            var expected = description
                .ParameterDescriptions.Where(p => p.Source.Id is "Path" or "Query")
                .Select(p => p.Name)
                .Order(StringComparer.Ordinal)
                .ToList();
            var published = (op["parameters"]?.AsArray() ?? [])
                .Select(p => p!["name"]!.GetValue<string>())
                .Order(StringComparer.Ordinal)
                .ToList();

            published.Should().Equal(expected, $"{description.HttpMethod} {path}");
        }
    }

    [Fact]
    public async Task ManualJournal_responde_application_json_y_su_422_solo_publica_application_json()
    {
        await using var app = _factory.WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
                services.AddScoped<IRuntimePermissionAuthorizer, AllowAllPermissionAuthorizer>()
            )
        );
        var client = app.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            TestJwtFactory.CreateSessionJwt(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "Admin")
        );

        var response = await client.PostAsync(
            "/api/v1/accounting/manual-journal-entries",
            new StringContent("{", System.Text.Encoding.UTF8, "application/json")
        );

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/json");
        FindOperation("/api/v1/accounting/manual-journal-entries", "POST")["responses"]!["422"]!["content"]!
            .AsObject()
            .Select(c => c.Key)
            .Should()
            .Equal("application/json");
    }

    [Fact]
    public void El_400_por_id_distinto_publica_un_unico_esquema_por_operacion_sin_oneOf()
    {
        // Único 400 alcanzable de estas operaciones: los handlers solo responden 404/409/422 y los validadores (422)
        // cubren las reglas de los ArgumentException del dominio, así que el envelope no se declara bajo el 400 y un
        // `oneOf` publicaría un cuerpo inexistente. Cuerpos reales: ver el test de ejecución anterior.
        foreach (var id in ERP.API.Swagger.OpenApiRouteIdMismatchResponseFilter.ProblemDetailsOperations)
        {
            var response = OperationById(id)["responses"]!["400"]!.AsObject();
            response["content"]!.AsObject().Select(c => c.Key).Should().Equal("application/json");
            var schema = response["content"]!["application/json"]!["schema"]!.AsObject();
            schema["$ref"]!.GetValue<string>().Should().EndWith("/ProblemDetails", id);
            schema.ContainsKey("oneOf").Should().BeFalse(id);
            response["description"]!.GetValue<string>().Should().Contain("único 400", id);
        }

        foreach (var id in ERP.API.Swagger.OpenApiRouteIdMismatchResponseFilter.JsonStringOperations)
        {
            var schema = OperationById(id)["responses"]!["400"]!["content"]!["application/json"]!["schema"]!.AsObject();
            schema["type"]!.GetValue<string>().Should().Be("string", id);
            schema["$ref"].Should().BeNull(id);
            schema.ContainsKey("oneOf").Should().BeFalse(id);
        }

        // ProblemDetails solo se referencia desde esas tres respuestas; ningún otro 400 cambia de esquema.
        System.Text.RegularExpressions.Regex.Matches(_doc.ToJsonString(), "#/components/schemas/ProblemDetails")
            .Should()
            .HaveCount(ERP.API.Swagger.OpenApiRouteIdMismatchResponseFilter.ProblemDetailsOperations.Length);
        _doc["components"]!["schemas"]!["ProblemDetails"]!["properties"]!.AsObject()
            .Select(p => p.Key)
            .Should()
            .Contain(["type", "title", "status"]);
    }
}

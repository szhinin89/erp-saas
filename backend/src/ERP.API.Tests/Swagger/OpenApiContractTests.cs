using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using ERP.API.Tests.Support;
using ERP.Application.Access.Authorization;
using FluentAssertions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.ApiExplorer;
using Microsoft.Extensions.DependencyInjection;

namespace ERP.API.Tests.Swagger;

/// <summary>
/// API-DOC-V1 / Fase 2A — invariantes del documento OpenAPI global y su correspondencia con el
/// comportamiento HTTP real (host real + PostgreSQL). No fija el documento completo: fija las reglas
/// que los filtros centrales deben garantizar para toda operación, presente o futura.
/// </summary>
[Trait("Category", "PostgreSql")]
public sealed partial class OpenApiContractTests : IAsyncLifetime
{
    private static readonly string[] Methods = ["get", "post", "put", "patch", "delete"];

    private readonly PostgreSqlTestWebAppFactory _factory = new();
    private JsonNode _doc = null!;

    public async Task InitializeAsync()
    {
        // JwtExtensions lee la clave al registrar servicios (antes de la configuración en memoria del
        // factory): mismo patrón que AdminCoreAuthorizationHttpFixture.
        Environment.SetEnvironmentVariable("JWT__SECRETKEY", IntegrationTestConstants.JwtSecretKey);
        Environment.SetEnvironmentVariable("JWT__ISSUER", "ZHTechnologies");
        Environment.SetEnvironmentVariable("JWT__AUDIENCE", "ERPUsers");

        await _factory.InitializeAsync();
        await _factory.MigrateAsync();
        _doc = JsonNode.Parse(
            await _factory.CreateClient().GetStringAsync("/swagger/v1/swagger.json")
        )!;
    }

    public async Task DisposeAsync() => await _factory.DisposeAsync();

    [GeneratedRegex("^[A-Za-z0-9._-]+$")]
    private static partial Regex ComponentName();

    [GeneratedRegex("^[A-Za-z0-9_]+$")]
    private static partial Regex OperationName();

    private IEnumerable<(string Path, string Method, JsonObject Op)> Operations() =>
        _doc["paths"]!
            .AsObject()
            .SelectMany(p =>
                p.Value!.AsObject()
                    .Where(m => Methods.Contains(m.Key))
                    .Select(m => (p.Key, m.Key, m.Value!.AsObject()))
            );

    private IReadOnlyList<ApiDescription> ApiExplorerOperations() =>
        _factory
            .Services.GetRequiredService<IApiDescriptionGroupCollectionProvider>()
            .ApiDescriptionGroups.Items.SelectMany(g => g.Items)
            .Where(d => d.HttpMethod is not null)
            .ToList();

    [Fact]
    public void El_documento_expone_exactamente_las_operaciones_del_ApiExplorer_sin_las_ocultas()
    {
        var expected = ApiExplorerOperations();

        Operations().Count().Should().Be(expected.Count);
        Operations().Select(o => o.Path).Should().NotContain(p => p.Contains("/api/dev/"));
    }

    [Fact]
    public void operationId_existe_es_unico_y_portable()
    {
        var ids = Operations().Select(o => o.Op["operationId"]?.GetValue<string>()).ToList();

        ids.Should().OnlyContain(i => !string.IsNullOrEmpty(i));
        ids.Should().OnlyContain(i => OperationName().IsMatch(i!));
        ids.Select(i => i!.ToLowerInvariant()).Should().OnlyHaveUniqueItems();
    }

    [Fact]
    public void Ids_de_esquema_son_portables_sin_backticks_ni_assembly()
    {
        var names = _doc["components"]!["schemas"]!.AsObject().Select(s => s.Key).ToList();

        names.Should().OnlyContain(n => ComponentName().IsMatch(n));
        names.Should().NotContain(n => n.Contains("PublicKeyToken") || n.Contains('`'));
    }

    [Fact]
    public void Todas_las_referencias_a_esquemas_se_resuelven_y_no_hay_esquemas_huerfanos()
    {
        var text = _doc.ToJsonString();
        var referenced = Regex
            .Matches(text, "\"#/components/schemas/([^\"]+)\"")
            .Select(m => m.Groups[1].Value)
            .ToHashSet();
        var defined = _doc["components"]!["schemas"]!.AsObject().Select(s => s.Key).ToHashSet();

        referenced.Except(defined).Should().BeEmpty("no puede haber $ref colgantes");
        defined.Except(referenced).Should().BeEmpty("no debe quedar ningún esquema sin referenciar");
    }

    [Fact]
    public void La_exigencia_de_Bearer_es_por_operacion_y_los_endpoints_publicos_no_la_exigen()
    {
        _doc["security"].Should().BeNull("el requisito global haría parecer protegidos a los anónimos");

        foreach (var description in ApiExplorerOperations())
        {
            var metadata = description.ActionDescriptor.EndpointMetadata;
            var isPublic =
                metadata.OfType<IAllowAnonymous>().Any()
                || !metadata.OfType<IAuthorizeData>().Any();
            var path = "/" + description.RelativePath!.Split('?')[0];
            var op = FindOperation(path, description.HttpMethod!);
            var security = op["security"]!.AsArray();

            if (isPublic)
                security
                    .Should()
                    .ContainSingle($"{description.HttpMethod} {path} es público")
                    .Which!.AsObject()
                    .Should()
                    .BeEmpty();
            else
                security[0]!.AsObject().ContainsKey("Bearer").Should().BeTrue($"{description.HttpMethod} {path}");
        }
    }

    private JsonObject FindOperation(string apiExplorerPath, string method)
    {
        // El ApiExplorer conserva las restricciones de ruta ({id:guid}); el documento las elimina.
        var normalized = Regex.Replace(apiExplorerPath, @"\{([^}:?]+)(:[^}]*)?\??\}", "{$1}");
        var item = _doc["paths"]![normalized];
        item.Should().NotBeNull($"falta la ruta {normalized}");
        return item![method.ToLowerInvariant()]!.AsObject();
    }

    [Fact]
    public void Ninguna_respuesta_de_error_inferida_expone_ProblemDetails()
    {
        // La API no emite ProblemDetails: todo error usa el envelope (ver error-handling.md).
        var offenders = Operations()
            .SelectMany(o =>
                o.Op["responses"]!.AsObject()
                    .Where(r => r.Value!.ToJsonString().Contains("ProblemDetails"))
                    .Select(r => $"{o.Method.ToUpperInvariant()} {o.Path} {r.Key}")
            )
            .ToList();

        offenders.Should().BeEmpty();
    }

    [Fact]
    public void Respuestas_comunes_se_documentan_solo_cuando_el_pipeline_las_garantiza()
    {
        foreach (var (path, method, op) in Operations())
        {
            var responses = op["responses"]!.AsObject();
            var secured = op["security"]!.AsArray().Any(r => r!.AsObject().Count > 0);
            var label = $"{method.ToUpperInvariant()} {path}";

            responses.ContainsKey("500").Should().BeTrue($"{label}: ExceptionMiddleware envuelve el pipeline");
            responses.ContainsKey("401").Should().Be(secured, $"{label}: 401 solo si exige token");
            responses.ContainsKey("403").Should().Be(secured, $"{label}: 403 solo si exige política");
        }
    }

    [Fact]
    public void Los_permisos_del_contrato_provienen_de_las_politicas_reales()
    {
        foreach (var description in ApiExplorerOperations())
        {
            var expected = description
                .ActionDescriptor.EndpointMetadata.OfType<IAuthorizeData>()
                .Select(a => a.Policy)
                .Where(p => p is not null && p.StartsWith("perm:", StringComparison.OrdinalIgnoreCase))
                .Select(p => p![5..].Trim())
                .Distinct()
                .OrderBy(p => p, StringComparer.Ordinal)
                .ToList();
            var path = "/" + description.RelativePath!.Split('?')[0];
            var extension = FindOperation(path, description.HttpMethod!)["x-required-permissions"];

            if (expected.Count == 0)
                extension.Should().BeNull($"{description.HttpMethod} {path}");
            else
                extension!
                    .AsArray()
                    .Select(n => n!.GetValue<string>())
                    .Should()
                    .Equal(expected, $"{description.HttpMethod} {path}");
        }
    }

    [Fact]
    public void Secretos_de_entrada_son_writeOnly_y_los_de_salida_no_se_ocultan()
    {
        var schemas = _doc["components"]!["schemas"]!;

        schemas["LoginCommand"]!["properties"]!["password"]!["writeOnly"]!.GetValue<bool>().Should().BeTrue();
        schemas["LoginCommand"]!["properties"]!["password"]!["format"]!.GetValue<string>().Should().Be("password");
        // El token de sesión es parte legítima de la respuesta de login.
        schemas["AuthResponseDto"]!["properties"]!["token"]!["writeOnly"].Should().BeNull();
    }

    [Fact]
    public void Esquemas_solo_de_respuesta_marcan_required_las_propiedades_no_nulas()
    {
        var envelope = _doc["components"]!["schemas"]!["ApiResponseOfObject"]!;

        envelope["required"]!
            .AsArray()
            .Select(n => n!.GetValue<string>())
            .Should()
            .Contain(["code", "severity", "message", "meta"])
            .And.NotContain("data", "Data es T? y puede ser null");
    }

    // ── Comportamiento real que respalda lo documentado ─────────────────────────────────────────

    [Fact]
    public async Task Endpoints_protegidos_sin_token_responden_401_sin_cuerpo()
    {
        var client = _factory.CreateClient();
        var targets = Operations()
            .Where(o =>
                o.Method == "get"
                && !o.Path.Contains('{')
                && o.Op["security"]!.AsArray().Any(r => r!.AsObject().Count > 0)
            )
            .OrderBy(o => o.Path, StringComparer.Ordinal)
            .Take(5)
            .ToList();
        targets.Should().NotBeEmpty();

        foreach (var (path, _, _) in targets)
        {
            var response = await client.GetAsync(path);

            response.StatusCode.Should().Be(HttpStatusCode.Unauthorized, path);
            (await response.Content.ReadAsStringAsync()).Should().BeEmpty(path);
        }
    }

    [Fact]
    public async Task Endpoint_con_politica_y_token_sin_permiso_responde_403_sin_cuerpo()
    {
        var target = Operations()
            .Where(o =>
                o.Method == "get"
                && !o.Path.Contains('{')
                && o.Op.ContainsKey("x-required-permissions")
            )
            .OrderBy(o => o.Path, StringComparer.Ordinal)
            .First();
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            TestJwtFactory.CreateSessionJwt(Guid.NewGuid(), Guid.NewGuid())
        );

        var response = await client.GetAsync(target.Path);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden, target.Path);
        (await response.Content.ReadAsStringAsync()).Should().BeEmpty();
    }

    [Fact]
    public async Task Endpoints_publicos_no_exigen_token()
    {
        var client = _factory.CreateClient();

        foreach (var path in new[] { "/api/v1/public/deployment", "/api/v1/setup/status" })
        {
            var response = await client.GetAsync(path);
            response.StatusCode.Should().NotBe(HttpStatusCode.Unauthorized, path);
            response.StatusCode.Should().NotBe(HttpStatusCode.Forbidden, path);
        }
    }

    [Fact]
    public async Task Entrada_no_enlazable_responde_422_con_envelope_VALIDATION_ERROR()
    {
        // /auth/login es público (sin token) y enlaza cuerpo: llega hasta ModelState.
        var response = await _factory
            .CreateClient()
            .PostAsync(
                "/api/v1/auth/login",
                new StringContent("{", System.Text.Encoding.UTF8, "application/json")
            );

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        var body = (await response.Content.ReadFromJsonAsync<JsonObject>())!;
        body["code"]!.GetValue<string>().Should().Be("VALIDATION_ERROR");
        body.ContainsKey("meta").Should().BeTrue();

        var declared = FindOperation("/api/v1/auth/login", "POST")["responses"]!["422"]!;
        declared["content"]!["application/json"]!["schema"]!["$ref"]!
            .GetValue<string>()
            .Should()
            .EndWith("/ApiResponseOfObject");
    }

    [Fact]
    public void Auth_login_es_publico_por_su_configuracion_actual_y_asi_se_documenta()
    {
        // POST /auth/login no declara [Authorize] ni [AllowAnonymous]; sin política de respaldo es
        // accesible sin token. El contrato lo refleja sin modificar la autorización real.
        var login = FindOperation("/api/v1/auth/login", "POST");

        login["security"]!.AsArray().Should().ContainSingle().Which!.AsObject().Should().BeEmpty();
        login["description"]!.GetValue<string>().Should().Contain("no declara `[Authorize]`");
    }

    [Fact]
    public async Task Query_no_enlazable_en_endpoint_autenticado_responde_422_con_envelope()
    {
        // Respalda la regla de 422 para operaciones con solo parámetros query: ModelState rechaza
        // el valor antes de ejecutar la acción. Permisos abiertos para llegar al enlace de modelo.
        var tenantId = Guid.NewGuid();
        await using var app = _factory.WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
                services.AddScoped<IRuntimePermissionAuthorizer, AllowAllPermissionAuthorizer>()
            )
        );
        var client = app.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            TestJwtFactory.CreateSessionJwt(tenantId, Guid.NewGuid(), Guid.NewGuid(), "Admin")
        );

        var response = await client.GetAsync("/api/v1/accounting/journal-entries?pageNumber=abc");

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        var body = (await response.Content.ReadFromJsonAsync<JsonObject>())!;
        body["code"]!.GetValue<string>().Should().Be("VALIDATION_ERROR");

        var op = FindOperation("/api/v1/accounting/journal-entries", "GET");
        op["requestBody"].Should().BeNull("la operación solo enlaza query");
        op["responses"]!.AsObject().ContainsKey("422").Should().BeTrue();
    }

    // ── API-DOC-V1 / Fase 2B-1: Integraciones, Caja y Finanzas/Tesorería ───────────────────────

    /// <summary>Prefijo de <c>operationId</c> (= controller) de los tres módulos documentados.</summary>
    private static readonly string[] Phase2B1Controllers =
    [
        "Integration",
        "CashRegister",
        "CashSession",
        "CashFundingRequests",
        "CashMovementReasons",
        "CreditTerms",
        "SupplierCredit",
        "FinancePayments",
        "CompanyBankAccount",
        "PaymentMethods",
        "SupplierPayments",
        "Payables",
    ];

    private IEnumerable<(string Path, string Method, JsonObject Op)> Phase2B1Operations() =>
        Operations()
            .Where(o =>
                Phase2B1Controllers.Contains(o.Op["operationId"]!.GetValue<string>().Split('_')[0])
            );

    [Fact]
    public void Las_71_operaciones_de_Integraciones_Caja_y_Finanzas_estan_inventariadas()
    {
        Phase2B1Operations().Should().HaveCount(71);
    }

    [Fact]
    public void Remarks_y_seccion_de_autorizacion_conviven_en_la_descripcion()
    {
        // El filtro XML asigna Description (<remarks>): la sección generada por Fase 2A debe seguir
        // presente en TODAS las operaciones, también en las que tienen remarks.
        Operations()
            .Select(o => o.Op["description"]?.GetValue<string>() ?? "")
            .Should()
            .OnlyContain(d => d.Contains("**Autorización:**"));

        var close = FindOperation("/api/v1/cash-sessions/{id}/close", "POST")["description"]!
            .GetValue<string>();
        close.Should().Contain("una única transacción").And.Contain("**Autorización:**");
    }

    [Fact]
    public void Operaciones_de_los_tres_modulos_tienen_summary_contrato_2xx_tipado_y_respuestas_descritas()
    {
        foreach (var (path, method, op) in Phase2B1Operations())
        {
            var label = $"{method.ToUpperInvariant()} {path}";
            op["summary"]?.GetValue<string>().Should().NotBeNullOrWhiteSpace(label);

            var success = op["responses"]!
                .AsObject()
                .Where(r => r.Key.StartsWith('2'))
                .ToList();
            success.Should().ContainSingle(label);
            success[0]
                .Value!["content"]!["application/json"]!["schema"]!["$ref"]!.GetValue<string>()
                .Should()
                .Contain("/ApiResponseOf", label);

            foreach (var (code, response) in op["responses"]!.AsObject())
                response!["description"]!.GetValue<string>()
                    .Should()
                    .NotBeNullOrWhiteSpace($"{label} {code}");

            foreach (var parameter in op["parameters"]?.AsArray() ?? [])
                parameter!["description"]
                    ?.GetValue<string>()
                    .Should()
                    .NotBeNullOrWhiteSpace($"{label} parámetro {parameter["name"]}");
        }
    }

    [Theory]
    [InlineData("/api/integration/v1/tenants", "POST", "201", new[] { "409", "422" })]
    [InlineData("/api/integration/v1/tenants/{id}/status", "GET", "200", new[] { "404" })]
    [InlineData("/api/integration/v1/tenants/{id}/activate", "PUT", "200", new[] { "404" })]
    [InlineData("/api/integration/v1/tenants/{id}/suspend", "PUT", "200", new[] { "404" })]
    [InlineData("/api/integration/v1/companies", "POST", "201", new[] { "409" })]
    [InlineData("/api/integration/v1/companies/{id}/status", "GET", "200", new[] { "404" })]
    [InlineData("/api/integration/v1/companies/{id}/activate", "PUT", "200", new[] { "404" })]
    [InlineData("/api/integration/v1/companies/{id}/suspend", "PUT", "200", new[] { "404" })]
    [InlineData("/api/v1/cash-sessions/{id}/close", "POST", "200", new[] { "404", "422" })]
    [InlineData("/api/v1/cash-sessions/open", "POST", "201", new[] { "404", "409", "422" })]
    [InlineData("/api/v1/cash-funding-requests/{id}/cancel", "POST", "200", new[] { "403", "404", "422" })]
    [InlineData("/api/v1/supplier-payments", "POST", "201", new[] { "404", "409", "422" })]
    [InlineData("/api/v1/finance/collections", "POST", "201", new[] { "404", "409", "422" })]
    public void Contratos_representativos_declaran_exito_y_errores_especificos(
        string path,
        string method,
        string success,
        string[] errors
    )
    {
        var responses = FindOperation(path, method)["responses"]!.AsObject();

        responses.ContainsKey(success).Should().BeTrue($"{method} {path} éxito {success}");
        foreach (var code in errors)
            responses.ContainsKey(code).Should().BeTrue($"{method} {path} error {code}");
        // Los errores específicos usan el envelope oficial, nunca ProblemDetails.
        foreach (var code in errors.Where(c => c != "403"))
            responses[code]!["content"]!["application/json"]!["schema"]!["$ref"]!
                .GetValue<string>()
                .Should()
                .EndWith("/ApiResponseOfObject");
    }

    [Fact]
    public void Todo_error_declarado_distinto_de_401_y_403_lleva_el_envelope()
    {
        // Incluye los declarados solo con <response code="..."> en XML (sin [ProducesResponseType]).
        var offenders = Operations()
            .SelectMany(o =>
                o.Op["responses"]!.AsObject()
                    .Where(r =>
                        r.Key.Length == 3
                        && r.Key[0] is '4' or '5'
                        && r.Key is not ("401" or "403")
                        && r.Value!["content"] is null
                    )
                    .Select(r => $"{o.Method.ToUpperInvariant()} {o.Path} {r.Key}")
            )
            .ToList();

        offenders.Should().BeEmpty();
    }
}

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

    /// <summary>API-DOC-V1 / Fase 2B-2: Contabilidad (42) y Ventas (23).</summary>
    private static readonly string[] Phase2B2Controllers =
    [
        "Accounting",
        "AccountingReports",
        "ManualJournal",
        "Sales",
        "SalesReceivables",
        "SalesReturn",
    ];

    private IEnumerable<(string Path, string Method, JsonObject Op)> OperationsOf(
        string[] controllers
    ) =>
        Operations()
            .Where(o => controllers.Contains(o.Op["operationId"]!.GetValue<string>().Split('_')[0]));

    private IEnumerable<(string Path, string Method, JsonObject Op)> Phase2B1Operations() =>
        OperationsOf(Phase2B1Controllers);

    /// <summary>
    /// API-DOC-V1 / Fase 2B-3: Configuración, Administración e Importación inicial (107 operaciones).
    /// <c>OrgConfig</c> (4 operaciones) queda fuera: figura en «API pública congelada» y ya traía
    /// summary y 2xx tipado.
    /// </summary>
    private static readonly string[] Phase2B3Controllers =
    [
        "AccessProfiles",
        "AccessSession",
        "Activity",
        "AdminCore",
        "AdminPermissions",
        "AdminUserSession",
        "CompanyUserBranches",
        "CompanyUserMemberships",
        "CompanyUserPreferences",
        "IdentityUsers",
        "SystemProviderSettings",
        "Banks",
        "Branches",
        "CommunicationsEmailSettings",
        "Companies",
        "CompanyPrecisionPolicy",
        "CompanyProfile",
        "ConfigurationChangeLog",
        "Dashboard",
        "DocumentFlowPolicies",
        "DocumentSequences",
        "EmissionPoints",
        "Establishments",
        "Geography",
        "OperationalPreferences",
        "PhysicalDocumentAuthorizations",
        "InitialLoad",
    ];

    /// <summary>
    /// Operaciones documentadas cuyo éxito NO es un envelope: lista cerrada y verificada contra el
    /// controller (`Ok(arreglo)` crudo en Banks.List; `File(...)` en los logos, la plantilla de carga y el XML/PDF de retenciones).
    /// Cualquier otra operación documentada debe responder con <c>ApiResponse&lt;T&gt;</c>.
    /// </summary>
    private static readonly string[] NonEnvelopeSuccess =
    [
        "Banks_List",
        "CompanyProfile_GetLogoContent",
        "CompanyProfile_GetLogoAltContent",
        "InitialLoad_DownloadTemplate",
        "Retentions_GetElectronicXml",
        "Retentions_GetRidePdf",
        "PaymentTerms_List",
        "Ride_GetContent",
    ];

    /// <summary>API-DOC-V1 / Fase 2B-4: Gastos (18), Precios (17) y Retenciones (7).</summary>
    private static readonly string[] Phase2B4Controllers =
    [
        "Expenses",
        "ExpenseCategories",
        "Pricing",
        "Retentions",
    ];

    /// <summary>API-DOC-V1 / Fase 2B-5, lote A: Auth, Sesión, Público y Condiciones de pago (25).</summary>
    private static readonly string[] Phase2B5LotA =
    [
        "Auth",
        "GlobalAuth",
        "Session",
        "Me",
        "Public",
        "Setup",
        "PaymentTerms",
    ];

    /// <summary>API-DOC-V1 / Fase 2B-5, lote B: Catálogo e Ítems (78).</summary>
    private static readonly string[] Phase2B5LotB =
    [
        "Catalog",
        "CatalogBrands",
        "CatalogCategoryNodes",
        "CatalogClassifications",
        "CatalogAttributeGroups",
        "CatalogAttributeDefinitions",
        "Items",
        "ItemImages",
        "ItemPackagingLevels",
        "ItemPricing",
        "ItemSubstitutes",
        "ItemSupplierCodes",
        "ItemTypes",
        "ItemUnitConversions",
        "ItemVariants",
    ];

    /// <summary>API-DOC-V1 / Fase 2B-5, lote C: Business Partners e Inventario (63).</summary>
    private static readonly string[] Phase2B5LotC =
    [
        "BusinessPartners",
        "BusinessPartnerContacts",
        "BusinessPartnerLocations",
        "BusinessPartnerRoles",
        "CompanyBpPurchaseSettings",
        "CompanyBpSalesSettings",
        "SupplierRetentionDefaults",
        "Warehouses",
        "Stock",
        "StockAdjustments",
        "StockTransfers",
        "Kardex",
        "InventoryAdjustmentReasons",
    ];

    /// <summary>API-DOC-V1 / Fase 2B-5, lote D1: Compras (43).</summary>
    private static readonly string[] Phase2B5LotD1 =
    [
        "Purchases",
        "PurchaseReception",
        "PurchaseCreditNote",
        "PurchaseReturn",
    ];

    /// <summary>
    /// API-DOC-V1 / Fase 2B-5, lote D2: Facturación electrónica (20). Excepción documental autorizada por el
    /// propietario (ADR-023, addendum 2026-11-12): solo XML, <c>[ProducesResponseType]</c> y <c>using</c>.
    /// </summary>
    private static readonly string[] Phase2B5LotD2 =
    [
        "ElectronicDocuments",
        "ElectronicInvoicing",
        "SriServiceHealth",
        "Ride",
    ];

    /// <summary>
    /// Único controlador sin metadatos añadidos: <c>OrgConfig</c> (API pública congelada, ya cumplía).
    /// </summary>
    private static readonly string[] NotDocumentedControllers = ["OrgConfig"];

    /// <summary>Todas las operaciones con documentación funcional completa (2B-1 a 2B-5).</summary>
    private IEnumerable<(string Path, string Method, JsonObject Op)> DocumentedOperations() =>
        OperationsOf(
            [
                .. Phase2B1Controllers,
                .. Phase2B2Controllers,
                .. Phase2B3Controllers,
                .. Phase2B4Controllers,
                .. Phase2B5LotA,
                .. Phase2B5LotB,
                .. Phase2B5LotC,
                .. Phase2B5LotD1,
                .. Phase2B5LotD2,
            ]
        );

    [Fact]
    public void Las_71_operaciones_de_Integraciones_Caja_y_Finanzas_estan_inventariadas()
    {
        Phase2B1Operations().Should().HaveCount(71);
    }

    [Fact]
    public void Las_65_operaciones_de_Contabilidad_y_Ventas_estan_inventariadas()
    {
        OperationsOf(Phase2B2Controllers).Should().HaveCount(65);
        OperationsOf(["Accounting", "AccountingReports", "ManualJournal"]).Should().HaveCount(42);
        OperationsOf(["Sales", "SalesReceivables", "SalesReturn"]).Should().HaveCount(23);
    }

    [Fact]
    public void Las_107_operaciones_de_Configuracion_Administracion_e_Importacion_estan_inventariadas()
    {
        OperationsOf(Phase2B3Controllers).Should().HaveCount(107);
        // OrgConfig (API pública congelada) no forma parte del lote documentado.
        OperationsOf(["OrgConfig"]).Should().HaveCount(4);
        OperationsOf([.. Phase2B1Controllers, .. Phase2B2Controllers])
            .Select(o => o.Op["operationId"]!.GetValue<string>())
            .Intersect(OperationsOf(Phase2B3Controllers).Select(o => o.Op["operationId"]!.GetValue<string>()))
            .Should()
            .BeEmpty("no hay solapamiento con 2B-1 ni 2B-2");
    }

    [Fact]
    public void Las_42_operaciones_de_Gastos_Precios_y_Retenciones_estan_inventariadas()
    {
        OperationsOf(Phase2B4Controllers).Should().HaveCount(42);
        OperationsOf(["Expenses", "ExpenseCategories"]).Should().HaveCount(18);
        OperationsOf(["Pricing"]).Should().HaveCount(17);
        OperationsOf(["Retentions"]).Should().HaveCount(7);
        OperationsOf([.. Phase2B1Controllers, .. Phase2B2Controllers, .. Phase2B3Controllers])
            .Select(o => o.Op["operationId"]!.GetValue<string>())
            .Intersect(OperationsOf(Phase2B4Controllers).Select(o => o.Op["operationId"]!.GetValue<string>()))
            .Should()
            .BeEmpty("no hay solapamiento con 2B-1, 2B-2 ni 2B-3");
    }

    [Theory]
    [InlineData("/api/v1/retentions/{id}/electronic/xml", "GET", "application/xml")]
    [InlineData("/api/v1/retentions/{id}/ride/pdf", "GET", "application/pdf")]
    public void Los_archivos_de_retenciones_publican_su_tipo_de_medio_real(
        string path,
        string method,
        string mediaType
    )
    {
        // RetentionsController no tiene [Produces] de clase: el tipo de medio declarado en el atributo se respeta.
        var content = FindOperation(path, method)["responses"]!["200"]!["content"]!.AsObject();

        content.Select(c => c.Key).Should().Equal(mediaType);
        content[mediaType]!["schema"]!["$ref"].Should().BeNull("el cuerpo es el archivo, no un envelope");
    }

    [Fact]
    public void Solo_las_operaciones_de_la_lista_cerrada_responden_sin_envelope()
    {
        var withoutEnvelope = DocumentedOperations()
            .Where(o =>
                !o.Op["responses"]!
                    .AsObject()
                    .Where(r => r.Key.StartsWith('2'))
                    .All(r =>
                        r.Value!["content"]!.AsObject().First().Value!["schema"]!["$ref"]
                            ?.GetValue<string>()
                            .Contains("/ApiResponseOf") == true
                    )
            )
            .Select(o => o.Op["operationId"]!.GetValue<string>())
            .Order()
            .ToList();

        withoutEnvelope.Should().Equal(NonEnvelopeSuccess.Order());
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
    public void Operaciones_documentadas_tienen_summary_contrato_2xx_tipado_y_respuestas_descritas()
    {
        foreach (var (path, method, op) in DocumentedOperations())
        {
            var label = $"{method.ToUpperInvariant()} {path}";
            op["summary"]?.GetValue<string>().Should().NotBeNullOrWhiteSpace(label);

            var success = op["responses"]!
                .AsObject()
                .Where(r => r.Key.StartsWith('2'))
                .ToList();
            success.Should().ContainSingle(label);
            var schema = success[0].Value!["content"]!.AsObject().First().Value!["schema"]!;
            if (NonEnvelopeSuccess.Contains(op["operationId"]!.GetValue<string>()))
                schema["$ref"]?.GetValue<string>().Should().NotContain("/ApiResponseOf", label);
            else
                schema["$ref"]!.GetValue<string>().Should().Contain("/ApiResponseOf", label);

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

    [Fact]
    public void Las_229_operaciones_de_2B_5_estan_inventariadas()
    {
        OperationsOf(Phase2B5LotD2).Should().HaveCount(20);
        OperationsOf(["ElectronicDocuments"]).Should().HaveCount(10);
        OperationsOf(["ElectronicInvoicing"]).Should().HaveCount(6);
        OperationsOf(["SriServiceHealth"]).Should().HaveCount(1);
        OperationsOf(["Ride"]).Should().HaveCount(3);
        OperationsOf([.. Phase2B5LotA, .. Phase2B5LotB, .. Phase2B5LotC, .. Phase2B5LotD1, .. Phase2B5LotD2])
            .Should()
            .HaveCount(229);

        OperationsOf(Phase2B5LotA).Should().HaveCount(25);
        OperationsOf(Phase2B5LotB).Should().HaveCount(78);
        OperationsOf(Phase2B5LotC).Should().HaveCount(63);
        OperationsOf(Phase2B5LotD1).Should().HaveCount(43);

        var previous = OperationsOf(
                [.. Phase2B1Controllers, .. Phase2B2Controllers, .. Phase2B3Controllers, .. Phase2B4Controllers]
            )
            .Select(o => o.Op["operationId"]!.GetValue<string>())
            .ToHashSet();
        OperationsOf([.. Phase2B5LotA, .. Phase2B5LotB, .. Phase2B5LotC, .. Phase2B5LotD1, .. Phase2B5LotD2])
            .Select(o => o.Op["operationId"]!.GetValue<string>())
            .Should()
            .NotIntersectWith(previous, "no hay solapamiento con 2B-1 a 2B-4");
    }

    [Fact]
    public void El_inventario_total_no_omite_ni_duplica_operaciones()
    {
        var all = Operations().ToList();
        all.Should().HaveCount(518);
        all.Select(o => o.Op["operationId"]!.GetValue<string>()).Should().OnlyHaveUniqueItems();
        all.Select(o => $"{o.Method} {o.Path}").Should().OnlyHaveUniqueItems();

        DocumentedOperations().Should().HaveCount(514);
        OperationsOf(NotDocumentedControllers).Should().HaveCount(4);

        // Cada operación está documentada o figura en la lista explícita de excepciones: nada queda sin dueño.
        var documented = DocumentedOperations().Select(o => o.Op["operationId"]!.GetValue<string>()).ToHashSet();
        var excepted = OperationsOf(NotDocumentedControllers).Select(o => o.Op["operationId"]!.GetValue<string>()).ToHashSet();
        documented.Should().NotIntersectWith(excepted);
        (documented.Count + excepted.Count).Should().Be(all.Count);
    }

    [Fact]
    public void Operaciones_que_leen_parametros_fuera_del_modelo_lo_declaran_en_su_descripcion()
    {
        // Warehouses.GetAll lee activeStatus/onlyActive/search/sucursalId directamente de la cadena de consulta:
        // no son parámetros del contrato, así que la descripción debe nombrarlos.
        var op = FindOperation("/api/v1/inventory/warehouses", "GET");
        var description = op["description"]!.GetValue<string>();
        foreach (var name in new[] { "activeStatus", "onlyActive", "search", "sucursalId" })
            description.Should().Contain(name);
    }

    [Fact]
    public void Facturacion_electronica_documenta_archivos_y_multipart_con_su_contrato_real()
    {
        var cert = FindOperation("/api/v1/electronic-invoicing/sri-configuration/certificate", "POST");
        cert["requestBody"]!["content"]!.AsObject().ContainsKey("multipart/form-data").Should().BeTrue();
        var description = cert["description"]!.GetValue<string>();
        description.Should().Contain(".p12").And.Contain(".pfx").And.Contain("5 MB");

        var content = FindOperation("/api/v1/ride/content", "GET");
        content["description"]!.GetValue<string>().Should().Contain("application/pdf");
        var schema = content["responses"]!["200"]!["content"]!.AsObject().First().Value!["schema"]!;
        schema["$ref"]?.GetValue<string>().Should().NotContain("/ApiResponseOf");
        content["responses"]!.AsObject().ContainsKey("404").Should().BeTrue();
    }

    [Fact]
    public void Carga_de_archivo_de_recepcion_documenta_multipart()
    {
        var import = FindOperation("/api/v1/purchases/reception/import", "POST");
        import["requestBody"]!["content"]!.AsObject().ContainsKey("multipart/form-data").Should().BeTrue();
        import["description"]!.GetValue<string>().Should().Contain("multipart/form-data");
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
    [InlineData("/api/v1/sales", "POST", "201", new[] { "404", "422" })]
    [InlineData("/api/v1/sales/{id}/authorize", "POST", "200", new[] { "404", "422" })]
    [InlineData("/api/v1/sales/{id}/cancel", "POST", "200", new[] { "400", "404", "422" })]
    [InlineData("/api/v1/sales/{id}", "PUT", "200", new[] { "400", "404", "422" })]
    [InlineData("/api/v1/accounting/accounts", "POST", "201", new[] { "409", "422" })]
    [InlineData("/api/v1/accounting/accounts/{id}/disable", "PATCH", "200", new[] { "404", "409" })]
    [InlineData("/api/v1/accounting/accounting-periods/{id}/close", "PATCH", "200", new[] { "404", "422" })]
    [InlineData("/api/v1/accounting/reports/trial-balance", "GET", "200", new[] { "422" })]
    [InlineData("/api/v1/settings/catalogs/banks", "POST", "201", new[] { "409", "422" })]
    [InlineData("/api/v1/settings/catalogs/banks/{id}", "PUT", "200", new[] { "400", "404", "422" })]
    [InlineData("/api/v1/config/precision-policy", "PUT", "200", new[] { "404", "409", "422" })]
    [InlineData("/api/v1/settings/document-sequences/configure", "PUT", "200", new[] { "404", "409", "422" })]
    [InlineData("/api/v1/settings/physical-document-authorizations", "POST", "201", new[] { "404", "409", "422" })]
    [InlineData("/api/v1/settings/branches", "POST", "201", new[] { "400", "422" })]
    [InlineData("/api/v1/initial-load/batches", "POST", "201", new[] { "400", "422" })]
    [InlineData("/api/v1/initial-load/batches/{id}/confirm", "POST", "200", new[] { "400", "404", "422" })]
    [InlineData("/api/v1/admin/iam/profiles", "POST", "201", new[] { "400", "409" })]
    [InlineData("/api/v1/admin/iam/users", "POST", "200", new[] { "404", "409", "422" })]
    [InlineData("/api/v1/expenses/documents", "POST", "201", new[] { "404", "409", "422" })]
    [InlineData("/api/v1/expenses/documents/{id}/confirm", "POST", "200", new[] { "404", "409", "422" })]
    [InlineData("/api/v1/expenses/documents/{id}/cancel", "POST", "200", new[] { "404", "422" })]
    [InlineData("/api/v1/expenses/categories", "POST", "201", new[] { "404", "409", "422" })]
    [InlineData("/api/v1/pricing/price-lists", "POST", "201", new[] { "409", "422" })]
    [InlineData("/api/v1/pricing/price-lists/{id}", "PUT", "200", new[] { "400", "404", "422" })]
    [InlineData("/api/v1/pricing/pricing-rules", "POST", "201", new[] { "404", "422" })]
    [InlineData("/api/v1/pricing/pricing-rules/{id}", "DELETE", "200", new[] { "404", "422" })]
    [InlineData("/api/v1/retentions/{id}/electronic/register", "POST", "200", new[] { "400", "404", "409", "422" })]
    [InlineData("/api/v1/retentions/annulments/{requestId}/submission", "POST", "200", new[] { "404", "422" })]
    [InlineData("/api/v1/cash-sessions/{id}/close", "POST", "200", new[] { "404", "422" })]
    [InlineData("/api/v1/cash-sessions/open", "POST", "201", new[] { "404", "409", "422" })]
    [InlineData("/api/v1/cash-funding-requests/{id}/cancel", "POST", "200", new[] { "403", "404", "422" })]
    [InlineData("/api/v1/supplier-payments", "POST", "201", new[] { "404", "409", "422" })]
    [InlineData("/api/v1/finance/collections", "POST", "201", new[] { "404", "409", "422" })]
    [InlineData("/api/v1/auth/login", "POST", "200", new[] { "409", "429", "503" })]
    [InlineData("/api/v1/setup/admin", "POST", "200", new[] { "409" })]
    [InlineData("/api/v1/master/payment-terms", "POST", "201", new[] { "409", "422" })]
    [InlineData("/api/v1/catalog/brands", "POST", "201", new[] { "409", "422" })]
    [InlineData("/api/v1/items", "POST", "201", new[] { "404", "409", "422" })]
    [InlineData("/api/v1/items/{id}", "GET", "200", new[] { "404" })]
    [InlineData("/api/v1/master/business-partners", "POST", "201", new[] { "400", "409", "422" })]
    [InlineData("/api/v1/master/business-partners/{bpId}/roles", "POST", "201", new[] { "404", "409", "422" })]
    [InlineData("/api/v1/inventory/warehouses", "POST", "201", new[] { "409", "422" })]
    [InlineData("/api/v1/inventory/stock/adjustments/{id}/execute", "POST", "200", new[] { "404" })]
    [InlineData("/api/v1/purchases/{id}/confirm", "POST", "200", new[] { "400", "404" })]
    [InlineData("/api/v1/purchases/reception/import", "POST", "200", new[] { "400", "409", "422" })]
    [InlineData("/api/v1/purchases/credit-notes/{id}/authorize", "POST", "200", new[] { "404", "422" })]
    [InlineData("/api/v1/electronic-documents/register", "POST", "200", new[] { "409", "422" })]
    [InlineData("/api/v1/electronic-documents/{id}", "GET", "200", new[] { "404" })]
    [InlineData("/api/v1/electronic-documents/{id}/retry", "POST", "200", new[] { "400", "404", "409" })]
    [InlineData("/api/v1/electronic-invoicing/sri-configuration/certificate", "POST", "200", new[] { "400", "422" })]
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

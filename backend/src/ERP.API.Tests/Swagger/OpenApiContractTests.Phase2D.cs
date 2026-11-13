using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using ERP.API.Swagger;
using FluentAssertions;

namespace ERP.API.Tests.Swagger;

/// <summary>
/// API-DOC-V1 / Fase 2D — ejemplos OpenAPI, descripciones de cuerpos multipart y excepciones conocidas.
/// Solo comprueba metadato del documento: ninguna prueba cambia ni asume comportamiento funcional.
/// </summary>
public sealed partial class OpenApiContractTests
{
    // ── Ejemplos ─────────────────────────────────────────────────────────────────────────────────

    private static readonly string[] JsonMediaTypes = ["application/json", "text/json", "application/*+json"];

    private static readonly string[] ExampleMediaTypes = ["application/json", "text/json"];

    private JsonObject OperationById(string operationId)
    {
        var matches = Operations().Where(o => o.Op["operationId"]!.GetValue<string>() == operationId).ToList();
        matches.Should().ContainSingle($"el operationId {operationId} debe existir y ser único (sin sufijos de desambiguación)");
        return matches[0].Op;
    }

    private IEnumerable<(OpenApiExampleEntry Entry, JsonObject Op)> CatalogEntries() =>
        OpenApiExampleCatalog.All.Select(e => (e, OperationById(e.OperationId)));

    private IEnumerable<(OpenApiExampleEntry Entry, string MediaType, JsonNode Media)> ExampleSlots()
    {
        foreach (var (entry, op) in CatalogEntries())
        {
            var content =
                entry.Target == OpenApiExampleEntry.Request
                    ? op["requestBody"]!["content"]!.AsObject()
                    : op["responses"]![entry.Target]!["content"]!.AsObject();
            foreach (var (mediaType, media) in content)
                if (ExampleMediaTypes.Contains(mediaType) || (entry.Target == OpenApiExampleEntry.Request && JsonMediaTypes.Contains(mediaType)))
                    yield return (entry, mediaType, media!);
        }
    }

    [Fact]
    public void El_catalogo_de_ejemplos_es_minimo_y_cubre_los_dominios_priorizados()
    {
        var entries = OpenApiExampleCatalog.All;

        entries.Count.Should().BeInRange(8, 12);
        entries.Select(e => (e.OperationId, e.Target)).Should().OnlyHaveUniqueItems();

        var operations = entries.Select(e => e.OperationId).Distinct().ToList();
        foreach (var controller in new[] { "Auth", "Sales", "Purchases", "ManualJournal", "StockAdjustments", "Kardex", "ElectronicDocuments" })
            operations.Should().Contain(o => o.StartsWith(controller + "_", StringComparison.Ordinal), controller);
        entries.Should().Contain(e => e.Target == "422", "debe haber un ejemplo del envelope de validación");
    }

    [Fact]
    public void Cada_ejemplo_esta_adjunto_en_el_documento_con_el_contenido_del_catalogo()
    {
        foreach (var (entry, mediaType, media) in ExampleSlots())
        {
            var label = $"{entry.OperationId} {entry.Target} {mediaType}";
            media["example"].Should().NotBeNull(label);
            JsonNode.DeepEquals(media["example"], JsonNode.Parse(entry.Json)).Should().BeTrue(label);
        }
    }

    [Fact]
    public void Los_ejemplos_solo_estan_en_las_operaciones_del_catalogo()
    {
        var expected = OpenApiExampleCatalog.All.Select(e => e.OperationId).ToHashSet();

        var withExample = Operations()
            .Where(o => o.Op.ToJsonString().Contains("\"example\":"))
            .Select(o => o.Op["operationId"]!.GetValue<string>())
            .ToHashSet();

        withExample.Should().BeEquivalentTo(expected);
        // Los componentes (DTO) no llevan ejemplos: el mecanismo es solo de operación.
        _doc["components"]!["schemas"]!.ToJsonString().Should().NotContain("\"example\":");
    }

    [Fact]
    public void Cada_ejemplo_es_valido_contra_el_esquema_declarado_sin_inventar_propiedades()
    {
        var errors = new List<string>();
        foreach (var (entry, mediaType, media) in ExampleSlots())
            errors.AddRange(
                ValidateAgainstSchema(media["example"], media["schema"]!, "$").Select(e => $"{entry.OperationId} {entry.Target} {mediaType}: {e}")
            );

        errors.Should().BeEmpty();
    }

    [Fact]
    public void El_422_de_validacion_solo_se_adjunta_al_envelope_comun()
    {
        // Auth_Login declara el 422 común (ModelState): lleva el ejemplo. Sales_CreateDraft declara un 422 propio
        // (regla de negocio): no debe recibir un ejemplo de validación de formulario.
        var login422 = OperationById("Auth_Login")["responses"]!["422"]!;
        login422["description"]!.GetValue<string>().Should().StartWith(OpenApiExamplesOperationFilter.CommonValidationDescriptionPrefix);
        login422["content"]!["application/json"]!["example"]!["code"]!.GetValue<string>().Should().Be("VALIDATION_ERROR");

        var sales422 = OperationById("Sales_CreateDraft")["responses"]!["422"]!;
        sales422["content"]!["application/json"]!.AsObject().ContainsKey("example").Should().BeFalse();
    }

    [Fact]
    public void Los_ejemplos_son_sinteticos_y_no_contienen_secretos()
    {
        var offenders = new List<string>();
        foreach (var entry in OpenApiExampleCatalog.All)
            Walk(JsonNode.Parse(entry.Json), "$", $"{entry.OperationId} {entry.Target}", offenders);

        offenders.Should().BeEmpty();

        static void Walk(JsonNode? node, string path, string label, List<string> offenders)
        {
            switch (node)
            {
                case JsonObject obj:
                    foreach (var (key, value) in obj)
                        Walk(value, $"{path}.{key}", label, offenders);
                    break;
                case JsonArray array:
                    for (var i = 0; i < array.Count; i++)
                        Walk(array[i], $"{path}[{i}]", label, offenders);
                    break;
                case JsonValue value when value.TryGetValue<string>(out var text):
                    var name = path[(path.LastIndexOf('.') + 1)..];
                    var secretKey = Regex.IsMatch(name, "password|secret|token|certificate|passphrase|privatekey", RegexOptions.IgnoreCase);
                    if (secretKey && !Regex.IsMatch(text, "^<[a-z-]+>$"))
                        offenders.Add($"{label} {path}: un campo sensible debe ser un marcador <...>");
                    if (Regex.IsMatch(text, @"eyJ[A-Za-z0-9_-]{8,}|BEGIN [A-Z ]*(KEY|CERTIFICATE)|^Bearer\s", RegexOptions.IgnoreCase))
                        offenders.Add($"{label} {path}: forma de JWT, clave o cabecera de autorización");
                    if (Guid.TryParse(text, out _) && !text.StartsWith("00000000-0000-4000-8000-", StringComparison.Ordinal))
                        offenders.Add($"{label} {path}: identificador no sintético");
                    if (text.Contains('@') && !text.EndsWith("@example.com", StringComparison.Ordinal))
                        offenders.Add($"{label} {path}: correo fuera de example.com");
                    if (Regex.IsMatch(text, @"^\d{10}(\d{3})?$"))
                        offenders.Add($"{label} {path}: forma de cédula o RUC");
                    break;
            }
        }
    }

    [Fact]
    public async Task El_ejemplo_de_deployment_publica_exactamente_los_campos_del_endpoint_real()
    {
        var response = await _factory.CreateClient().GetAsync("/api/v1/public/deployment");
        response.EnsureSuccessStatusCode();
        var actual = (await response.Content.ReadFromJsonAsync<JsonObject>())!;
        var example = JsonNode.Parse(OpenApiExampleCatalog.All.Single(e => e.OperationId == "Public_GetDeploymentInfo").Json)!.AsObject();

        actual.Select(p => p.Key).Should().BeEquivalentTo(example.Select(p => p.Key));
        actual["data"]!.AsObject().Select(p => p.Key).Should().BeEquivalentTo(example["data"]!.AsObject().Select(p => p.Key));
        foreach (var (key, value) in actual["data"]!.AsObject())
            (value?.GetValueKind() ?? JsonValueKind.Null)
                .Should()
                .Be(
                    example["data"]![key]?.GetValueKind() ?? JsonValueKind.Null,
                    $"data.{key}: el tipo del ejemplo debe coincidir con el real"
                );
    }

    // ── Cuerpos multipart y excepciones conocidas ────────────────────────────────────────────────

    private static readonly string[] MultipartOperationIds =
    [
        "CompanyProfile_UploadLogo",
        "CompanyProfile_UploadLogoAlt",
        "ElectronicInvoicing_UploadCertificate",
        "InitialLoad_Upload",
        "PurchaseReception_Import",
    ];

    /// <summary>
    /// Excepciones conocidas de cobertura (API pública congelada, <c>OrgConfig</c>): no se tocan en API-DOC-V1.
    /// No es cobertura perfecta: es una lista cerrada y verificada; si cambia, la decisión es del propietario.
    /// </summary>
    private static readonly string[] OrgConfigParametersWithoutDescription =
    [
        "OrgConfig_GetBranchInvoiceDefaults:branchId",
        "OrgConfig_UpsertBranchInvoiceDefaults:branchId",
    ];

    private static readonly string[] OrgConfigBodiesWithoutDescription =
    [
        "OrgConfig_UpsertBranchInvoiceDefaults",
        "OrgConfig_UpsertCompanyInvoiceDefaults",
    ];

    [Fact]
    public void Los_cinco_cuerpos_multipart_estan_descritos_y_son_los_unicos_multipart()
    {
        var multipart = Operations()
            .Where(o => o.Op["requestBody"]?["content"]?.AsObject().ContainsKey("multipart/form-data") == true)
            .Select(o => o.Op["operationId"]!.GetValue<string>())
            .Order(StringComparer.Ordinal)
            .ToList();
        multipart.Should().Equal(MultipartOperationIds.Order(StringComparer.Ordinal));
        OpenApiMultipartBodyDescriptionFilter.Descriptions.Keys.Should().BeEquivalentTo(MultipartOperationIds);

        foreach (var id in MultipartOperationIds)
        {
            var body = OperationById(id)["requestBody"]!;
            body["description"]!.GetValue<string>().Should().Contain("multipart/form-data").And.Contain("`file`", id);
            var file = body["content"]!["multipart/form-data"]!["schema"]!["properties"]!["file"]!;
            file["description"]!.GetValue<string>().Should().NotBeNullOrWhiteSpace(id);
            file["type"]!.GetValue<string>().Should().Be("string");
            file["format"]!.GetValue<string>().Should().Be("binary");
        }
    }

    [Fact]
    public void Las_excepciones_de_cobertura_son_solo_las_cuatro_de_OrgConfig()
    {
        var parametersWithout = Operations()
            .SelectMany(o =>
                (o.Op["parameters"]?.AsArray() ?? [])
                    .Where(p => string.IsNullOrWhiteSpace(p!["description"]?.GetValue<string>()))
                    .Select(p => $"{o.Op["operationId"]!.GetValue<string>()}:{p!["name"]!.GetValue<string>()}")
            )
            .Order(StringComparer.Ordinal)
            .ToList();
        parametersWithout.Should().Equal(OrgConfigParametersWithoutDescription);

        var bodiesWithout = Operations()
            .Where(o => o.Op["requestBody"] is not null && string.IsNullOrWhiteSpace(o.Op["requestBody"]!["description"]?.GetValue<string>()))
            .Select(o => o.Op["operationId"]!.GetValue<string>())
            .Order(StringComparer.Ordinal)
            .ToList();
        bodiesWithout.Should().Equal(OrgConfigBodiesWithoutDescription);

        // Las cuatro excepciones pertenecen al controlador congelado y a ningún otro.
        parametersWithout.Concat(bodiesWithout).Select(x => x.Split(':')[0].Split('_')[0]).Distinct().Should().Equal("OrgConfig");
    }

    [Fact]
    public void Toda_operacion_tiene_summary_y_description_y_la_deuda_de_required_de_entrada_esta_fijada()
    {
        foreach (var (path, method, op) in Operations())
        {
            var label = $"{method.ToUpperInvariant()} {path}";
            op["summary"]!.GetValue<string>().Should().NotBeNullOrWhiteSpace(label);
            op["description"]!.GetValue<string>().Should().NotBeNullOrWhiteSpace(label);
        }

        // Deuda contractual registrada (no se resuelve en API-DOC-V1): los esquemas de entrada casi no declaran
        // `required`. La prueba fija el estado actual para que un cambio sea una decisión explícita.
        var schemas = _doc["components"]!["schemas"]!.AsObject();
        var requestSchemas = Operations()
            .SelectMany(o => (o.Op["requestBody"]?["content"]?.AsObject() ?? []).Where(c => JsonMediaTypes.Contains(c.Key)))
            .Select(c => c.Value!["schema"]?["$ref"]?.GetValue<string>()?.Split('/').Last())
            .Where(n => n is not null)
            .Distinct()
            .ToList();
        requestSchemas.Where(n => schemas[n!]!["required"] is not null).Should().HaveCount(1);
    }

    // ── Respuestas genéricas y conciliación de métricas ──────────────────────────────────────────

    /// <summary>
    /// Las cinco operaciones cuyo 2xx publica <c>ApiResponse&lt;object&gt;</c>. Análisis (Fase 2D) contra el código:
    /// <c>Public.GetDeploymentInfo</c> devuelve un objeto anónimo sin DTO (F-52);
    /// <c>CompanyUserMemberships.Upsert/Revoke</c> devuelven <c>Result&lt;object&gt;</c> con <c>{}</c> de respaldo;
    /// <c>Auth.ForgotPassword/ResetPasswordWithToken</c> devuelven <c>Result&lt;bool&gt;</c> aunque el controlador declare
    /// <c>object</c>. Cambiar el tipo exige tocar el controlador: queda como está, documentado.
    /// </summary>
    private static readonly string[] GenericObjectSuccess =
    [
        "Auth_ForgotPassword",
        "Auth_ResetPasswordWithToken",
        "CompanyUserMemberships_Revoke",
        "CompanyUserMemberships_Upsert",
        "Public_GetDeploymentInfo",
    ];

    [Fact]
    public void Las_cinco_respuestas_genericas_son_las_conocidas()
    {
        var generic = Operations()
            .Where(o =>
                o.Op["responses"]!.AsObject()
                    .Where(r => r.Key.StartsWith('2'))
                    .Any(r =>
                        r.Value!["content"]?.AsObject().Any(c => c.Value!["schema"]?["$ref"]?.GetValue<string>().EndsWith("/ApiResponseOfObject", StringComparison.Ordinal) == true) == true
                    )
            )
            .Select(o => o.Op["operationId"]!.GetValue<string>())
            .Order(StringComparer.Ordinal)
            .ToList();

        generic.Should().Equal(GenericObjectSuccess);
    }

    [Fact]
    public void Las_cifras_de_respuestas_2xx_concilian_por_respuesta_y_no_por_tipo_de_medio()
    {
        // Una respuesta 2xx por operación (518). Las de datos tipados repiten el esquema en tres tipos de medio
        // (application/json, text/plain, text/json): contar por tipo de medio da más entradas que respuestas.
        var success = Operations()
            .SelectMany(o => o.Op["responses"]!.AsObject().Where(r => r.Key.StartsWith('2')).Select(r => (Id: o.Op["operationId"]!.GetValue<string>(), Response: r.Value!)))
            .ToList();

        success.Should().HaveCount(518);
        success.Select(s => s.Id).Should().OnlyHaveUniqueItems("exactamente una respuesta 2xx por operación");

        var withoutEnvelope = success
            .Where(s => !s.Response["content"]!.AsObject().All(c => c.Value!["schema"]!["$ref"]?.GetValue<string>().Contains("/ApiResponseOf") == true))
            .Select(s => s.Id)
            .Order(StringComparer.Ordinal)
            .ToList();

        withoutEnvelope.Should().Equal(NonEnvelopeSuccess.Order(StringComparer.Ordinal));
        (success.Count - withoutEnvelope.Count).Should().Be(510);
        withoutEnvelope.Should().HaveCount(8);
    }

    // ── Validador mínimo de ejemplos contra el esquema OpenAPI generado ─────────────────────────

    private IEnumerable<string> ValidateAgainstSchema(JsonNode? value, JsonNode schema, string path)
    {
        schema = Resolve(schema);

        if (value is null)
        {
            if (schema["nullable"]?.GetValue<bool>() != true && !IsNullableByAllOf(schema))
                yield return $"{path}: null no permitido (el esquema no es nullable)";
            yield break;
        }

        if (schema["allOf"] is JsonArray allOf)
        {
            foreach (var part in allOf)
            foreach (var error in ValidateAgainstSchema(value, part!, path))
                yield return error;
            yield break;
        }

        var type = schema["type"]?.GetValue<string>();
        var kind = value.GetValueKind();
        switch (type)
        {
            case "string":
                if (kind != JsonValueKind.String)
                {
                    yield return $"{path}: se esperaba string y es {kind}";
                    yield break;
                }

                var text = value.GetValue<string>();
                var format = schema["format"]?.GetValue<string>();
                if (format == "uuid" && !Guid.TryParse(text, out _))
                    yield return $"{path}: uuid inválido";
                if (format == "date" && !DateOnly.TryParseExact(text, "yyyy-MM-dd", out _))
                    yield return $"{path}: date debe ser YYYY-MM-DD";
                if (format == "date-time" && !Regex.IsMatch(text, @"^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}(\.\d+)?Z$"))
                    yield return $"{path}: date-time debe ser un instante UTC terminado en Z";
                if (schema["enum"] is JsonArray values && !values.Any(v => v!.GetValue<string>() == text))
                    yield return $"{path}: '{text}' no está en el enum";
                break;
            case "integer":
                if (kind != JsonValueKind.Number || value.GetValue<double>() % 1 != 0)
                    yield return $"{path}: se esperaba integer";
                break;
            case "number":
                if (kind != JsonValueKind.Number)
                    yield return $"{path}: se esperaba number y es {kind}";
                break;
            case "boolean":
                if (kind != JsonValueKind.True && kind != JsonValueKind.False)
                    yield return $"{path}: se esperaba boolean";
                break;
            case "array":
                if (value is not JsonArray array)
                {
                    yield return $"{path}: se esperaba array";
                    yield break;
                }

                for (var i = 0; i < array.Count; i++)
                    foreach (var error in ValidateAgainstSchema(array[i], schema["items"]!, $"{path}[{i}]"))
                        yield return error;
                break;
            default:
                if (value is JsonObject obj && (type == "object" || schema["properties"] is not null))
                {
                    var properties = schema["properties"]?.AsObject();
                    var additional = schema["additionalProperties"];
                    foreach (var (key, child) in obj)
                    {
                        if (properties is not null && properties.TryGetPropertyValue(key, out var childSchema))
                        {
                            foreach (var error in ValidateAgainstSchema(child, childSchema!, $"{path}.{key}"))
                                yield return error;
                        }
                        else if (additional is JsonObject additionalSchema && additionalSchema.Count > 0)
                        {
                            foreach (var error in ValidateAgainstSchema(child, additionalSchema, $"{path}.{key}"))
                                yield return error;
                        }
                        else if (properties is not null && additional?.GetValueKind() != JsonValueKind.True)
                        {
                            yield return $"{path}.{key}: propiedad que el esquema no declara";
                        }
                    }

                    foreach (var required in (schema["required"]?.AsArray() ?? []).Select(r => r!.GetValue<string>()))
                        if (!obj.ContainsKey(required))
                            yield return $"{path}.{required}: propiedad obligatoria ausente";
                }
                else if (type == "object" && value is not JsonObject)
                {
                    yield return $"{path}: se esperaba object";
                }

                break;
        }
    }

    private JsonNode Resolve(JsonNode schema)
    {
        while (schema["$ref"]?.GetValue<string>() is { } reference)
            schema = _doc["components"]!["schemas"]![reference.Split('/').Last()]!;
        return schema;
    }

    private bool IsNullableByAllOf(JsonNode schema) =>
        schema["allOf"] is JsonArray allOf && allOf.Any(part => Resolve(part!)["nullable"]?.GetValue<bool>() == true);
}

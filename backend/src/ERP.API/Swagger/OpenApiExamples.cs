using System.Text.Json;
using Microsoft.OpenApi.Any;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace ERP.API.Swagger;

/// <summary>
/// Dónde se adjunta un ejemplo dentro de una operación (API-DOC-V1 / Fase 2D).
/// </summary>
/// <param name="OperationId">
/// <c>operationId</c> base (<c>{Controller}_{Action}</c>). Solo se admiten operaciones cuyo id no se
/// desambigua en <see cref="OpenApiOperationIdsDocumentFilter"/>: la prueba de contrato lo comprueba.
/// </param>
/// <param name="Target"><c>request</c> (cuerpo JSON de entrada) o el código HTTP de la respuesta (<c>200</c>, <c>201</c>, <c>422</c>…).</param>
/// <param name="Json">Cuerpo del ejemplo como JSON literal.</param>
public sealed record OpenApiExampleEntry(string OperationId, string Target, string Json)
{
    public const string Request = "request";
}

/// <summary>
/// Ejemplos OpenAPI mínimos y centralizados (API-DOC-V1 / Fase 2D). Todo el contenido es sintético:
/// identificadores de forma <c>00000000-0000-4000-8000-…</c>, dominio <c>example.com</c>, token
/// <c>&lt;jwt&gt;</c> y contraseña <c>&lt;password&gt;</c>. No se escribe ningún ejemplo en los DTO, así
/// que ni los contratos ni los controladores cambian: el ejemplo es solo metadato del documento.
/// <para>
/// Cada ejemplo repite exactamente las propiedades del esquema declarado (nombres y tipos) y no declara
/// <c>required</c> alguno. <c>OpenApiContractTests</c> valida cada ejemplo contra el esquema del
/// documento generado y rechaza cualquier valor con forma de secreto. Los valores de catálogo que cada
/// ejemplo usa (<c>docTypeCode</c> <c>01</c>, <c>Sales</c>, <c>Ingreso</c>) son los que acepta el
/// código. <c>vatCode</c> <c>0</c> se verificó contra el catálogo real: <c>SriVatRateConfiguration</c> lo siembra
/// (<c>HasData</c>, migración base) como «0% IVA», 0.00, activo y sin fin de vigencia, y
/// <c>SriTaxResolver.GetVatRateWithNameAsync</c> —que usan Ventas y Compras— resuelve por <c>Code</c> activo. El
/// resto de códigos vienen de los catálogos de la propia API.
/// </para>
/// </summary>
public static class OpenApiExampleCatalog
{
    private const string Meta = """
        "meta": { "correlationId": "example-correlation-id", "timestamp": "2026-01-15T14:30:00Z", "traceId": null }
        """;

    public static readonly IReadOnlyList<OpenApiExampleEntry> All =
    [
        // ── Autenticación ───────────────────────────────────────────────────────────────────────
        new(
            "Auth_Login",
            OpenApiExampleEntry.Request,
            """
            { "username": "demo.usuario", "password": "<password>" }
            """
        ),
        new(
            "Auth_Login",
            "200",
            $$"""
            {
              "code": "OK",
              "severity": "success",
              "message": { "user": "Operación realizada correctamente.", "dev": null },
              "data": {
                "userId": "00000000-0000-4000-8000-000000000001",
                "fullName": "Usuario Demo",
                "username": "demo.usuario",
                "email": "demo.usuario@example.com",
                "role": "Admin",
                "tenantId": "00000000-0000-4000-8000-000000000002",
                "token": "<jwt>",
                "companyId": "00000000-0000-4000-8000-000000000003",
                "requiresCompanySelection": false,
                "refreshToken": null,
                "refreshTokenExpiry": null,
                "onboardingCompleted": true,
                "operationalStatus": "Operational",
                "requiresPasswordReset": false,
                "passwordResetToken": null,
                "passwordResetTokenExpiresIn": null,
                "operatorMode": false,
                "globalAdminUserId": null
              },
              {{Meta}}
            }
            """
        ),
        // Envelope de entrada no enlazable (ModelState): lo emite InvalidModelStateResponseFactory para
        // toda operación que enlaza cuerpo o query; el filtro solo lo adjunta si el 422 es el común.
        new(
            "Auth_Login",
            "422",
            $$"""
            {
              "code": "VALIDATION_ERROR",
              "severity": "error",
              "message": { "user": "Datos inválidos. Revisa el formulario.", "dev": null },
              "data": { "errors": { "username": ["Datos inválidos. Revisa el formulario."] } },
              {{Meta}}
            }
            """
        ),
        // ── Ventas ──────────────────────────────────────────────────────────────────────────────
        new(
            "Sales_CreateDraft",
            OpenApiExampleEntry.Request,
            """
            {
              "customerId": "00000000-0000-4000-8000-000000000010",
              "issueDate": "2026-01-15",
              "docTypeCode": "01",
              "paymentTermId": "00000000-0000-4000-8000-000000000011",
              "notes": "Venta de ejemplo",
              "lines": [
                {
                  "itemId": "00000000-0000-4000-8000-000000000012",
                  "description": "Producto de ejemplo",
                  "quantity": 2,
                  "unitPrice": 5.5,
                  "vatCode": "0",
                  "discountPct": 0,
                  "warehouseId": "00000000-0000-4000-8000-000000000013"
                }
              ],
              "payments": [
                {
                  "paymentMethodId": "00000000-0000-4000-8000-000000000014",
                  "amount": 11,
                  "tenderedAmount": 20
                }
              ]
            }
            """
        ),
        // ── Compras ─────────────────────────────────────────────────────────────────────────────
        new(
            "Purchases_CreateDraft",
            OpenApiExampleEntry.Request,
            """
            {
              "supplierId": "00000000-0000-4000-8000-000000000020",
              "docTypeCode": "01",
              "invoiceNumber": "001-001-000000001",
              "issueDate": "2026-01-15",
              "globalWarehouseId": "00000000-0000-4000-8000-000000000021",
              "paymentTermId": "00000000-0000-4000-8000-000000000022",
              "notes": "Compra de ejemplo",
              "lines": [
                {
                  "itemId": "00000000-0000-4000-8000-000000000023",
                  "description": "Producto de ejemplo",
                  "quantity": 10,
                  "unitPrice": 3.25,
                  "vatCode": "0",
                  "discountPct": 0
                }
              ]
            }
            """
        ),
        // ── Contabilidad ────────────────────────────────────────────────────────────────────────
        new(
            "ManualJournal_Create",
            OpenApiExampleEntry.Request,
            """
            {
              "entryDate": "2026-01-15",
              "concept": "Asiento manual de ejemplo",
              "reference": "REF-0001",
              "idempotencyKey": "00000000-0000-4000-8000-000000000030",
              "lines": [
                { "accountId": "00000000-0000-4000-8000-000000000031", "description": "Débito", "debit": 100, "credit": 0 },
                { "accountId": "00000000-0000-4000-8000-000000000032", "description": "Crédito", "debit": 0, "credit": 100 }
              ]
            }
            """
        ),
        new(
            "ManualJournal_Create",
            "201",
            $$"""
            {
              "code": "CREATED",
              "severity": "success",
              "message": { "user": "Recurso creado correctamente.", "dev": null },
              "data": {
                "id": "00000000-0000-4000-8000-000000000033",
                "status": "Draft",
                "version": 1,
                "entryNumber": null
              },
              {{Meta}}
            }
            """
        ),
        // ── Inventario y Kardex ─────────────────────────────────────────────────────────────────
        new(
            "StockAdjustments_CreateAdjustment",
            OpenApiExampleEntry.Request,
            """
            {
              "warehouseId": "00000000-0000-4000-8000-000000000040",
              "warehouseName": "Bodega principal",
              "movementType": "Ingreso",
              "reasonId": "00000000-0000-4000-8000-000000000041",
              "notes": "Ajuste de ejemplo",
              "lines": [
                {
                  "itemId": "00000000-0000-4000-8000-000000000042",
                  "itemName": "Producto de ejemplo",
                  "quantity": 5,
                  "unitCostBase": 2.5
                }
              ]
            }
            """
        ),
        new(
            "Kardex_GetKardexByProduct",
            "200",
            $$"""
            {
              "code": "OK",
              "severity": "success",
              "message": { "user": "Operación realizada correctamente.", "dev": null },
              "data": [
                {
                  "id": "00000000-0000-4000-8000-000000000043",
                  "itemId": "00000000-0000-4000-8000-000000000042",
                  "warehouseId": "00000000-0000-4000-8000-000000000040",
                  "movementType": 1,
                  "movementTypeName": "PurchaseEntry",
                  "quantity": 10,
                  "uomCode": "UND",
                  "previousQuantity": 0,
                  "resultQuantity": 10,
                  "sequenceNumber": 1,
                  "unitCost": 3.25,
                  "totalCost": 32.5,
                  "runningAverageCost": 3.25,
                  "runningStockValue": 32.5,
                  "effectiveDate": "2026-01-15",
                  "reference": "001-001-000000001",
                  "sourceDocId": "00000000-0000-4000-8000-000000000044",
                  "sourceDocType": "PurchaseInvoice",
                  "createdBy": "00000000-0000-4000-8000-000000000001",
                  "createdAt": "2026-01-15T14:30:00Z",
                  "createdByName": "Usuario Demo",
                  "costBasis": 3.25,
                  "costPending": false
                }
              ],
              {{Meta}}
            }
            """
        ),
        // ── Facturación electrónica ─────────────────────────────────────────────────────────────
        new(
            "ElectronicDocuments_Register",
            OpenApiExampleEntry.Request,
            """
            { "documentType": "Invoice", "sourceModule": "Sales", "sourceEntityId": "00000000-0000-4000-8000-000000000050" }
            """
        ),
        // ── Público ─────────────────────────────────────────────────────────────────────────────
        // Public.GetDeploymentInfo responde un objeto anónimo (API-DOC-F-52): su esquema 2xx es
        // ApiResponse<object>; este ejemplo fija los campos reales y una prueba los compara contra el
        // endpoint en ejecución.
        new(
            "Public_GetDeploymentInfo",
            "200",
            $$"""
            {
              "code": "OK",
              "severity": "success",
              "message": { "user": "Operación realizada correctamente.", "dev": null },
              "data": {
                "platformPanelEnabled": false,
                "dedicatedSingleClientInstance": true,
                "maxActiveSubscribers": null,
                "maxIdentityUsers": null,
                "maxUsersPerSubscriber": null
              },
              {{Meta}}
            }
            """
        ),
    ];

    internal static IEnumerable<OpenApiExampleEntry> For(string operationId) =>
        All.Where(e => string.Equals(e.OperationId, operationId, StringComparison.Ordinal));
}

/// <summary>
/// Adjunta los ejemplos de <see cref="OpenApiExampleCatalog"/> (API-DOC-V1 / Fase 2D). Debe registrarse
/// después de los filtros de comentarios XML y de errores: el 422 común solo existe cuando ese filtro lo
/// ha añadido. Nunca crea ni modifica respuestas, cuerpos ni esquemas: solo asigna <c>Example</c>.
/// </summary>
public sealed class OpenApiExamplesOperationFilter : IOperationFilter
{
    public const string CommonValidationDescriptionPrefix = "Entrada inválida o no enlazable";

    private static readonly string[] JsonMediaTypes = ["application/json", "text/json", "application/*+json"];

    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        var id = OpenApiOperationIds.Base(context.ApiDescription);
        if (id is null)
            return;

        foreach (var entry in OpenApiExampleCatalog.For(id))
        {
            var example = OpenApiExampleJson.Parse(entry.Json);
            if (entry.Target == OpenApiExampleEntry.Request)
            {
                Assign(operation.RequestBody?.Content, example);
                continue;
            }

            if (!operation.Responses.TryGetValue(entry.Target, out var response))
                continue;
            // Un 422 propio de la operación (regla de negocio con otro código) no es el envelope común.
            if (
                entry.Target == "422"
                && response.Description?.StartsWith(CommonValidationDescriptionPrefix, StringComparison.Ordinal) != true
            )
                continue;
            Assign(response.Content, example);
        }
    }

    private static void Assign(IDictionary<string, OpenApiMediaType>? content, IOpenApiAny example)
    {
        if (content is null)
            return;
        foreach (var mediaType in JsonMediaTypes)
            if (content.TryGetValue(mediaType, out var media))
                media.Example = example;
    }
}

/// <summary>Convierte JSON literal en los nodos de <c>Microsoft.OpenApi.Any</c>.</summary>
public static class OpenApiExampleJson
{
    public static IOpenApiAny Parse(string json)
    {
        using var document = JsonDocument.Parse(json);
        return Convert(document.RootElement);
    }

    private static IOpenApiAny Convert(JsonElement element)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                var obj = new OpenApiObject();
                foreach (var property in element.EnumerateObject())
                    obj[property.Name] = Convert(property.Value);
                return obj;
            case JsonValueKind.Array:
                var array = new OpenApiArray();
                foreach (var item in element.EnumerateArray())
                    array.Add(Convert(item));
                return array;
            case JsonValueKind.String:
                return new OpenApiString(element.GetString());
            case JsonValueKind.True:
                return new OpenApiBoolean(true);
            case JsonValueKind.False:
                return new OpenApiBoolean(false);
            case JsonValueKind.Number:
                if (element.TryGetInt32(out var i))
                    return new OpenApiInteger(i);
                if (element.TryGetInt64(out var l))
                    return new OpenApiLong(l);
                return new OpenApiDouble(element.GetDouble());
            default:
                return new OpenApiNull();
        }
    }
}

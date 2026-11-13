using Microsoft.AspNetCore.Mvc.ApiExplorer;
using Microsoft.AspNetCore.Mvc.Formatters;
using Microsoft.Net.Http.Headers;
using Microsoft.OpenApi.Any;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace ERP.API.Swagger;

/// <summary>Tipo JSON del parámetro de consulta documentado con <see cref="OpenApiQueryParameterAttribute"/>.</summary>
public enum OpenApiQueryParameterType
{
    String,
    Boolean,
    Guid,
}

/// <summary>
/// API-DOC-V1: declara en el contrato OpenAPI un parámetro de consulta que la acción lee directamente de
/// <c>Request.Query</c> (por tanto invisible para ApiExplorer). Es solo metadato del documento: no enlaza,
/// valida ni cambia nada en ejecución, y no añade el 422 de ModelState (el valor nunca se enlaza al modelo).
/// Si la acción pasa a enlazar el parámetro (<c>[FromQuery]</c>), el filtro no lo duplica.
/// </summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = true, Inherited = false)]
public sealed class OpenApiQueryParameterAttribute(
    string name,
    OpenApiQueryParameterType type,
    string description
) : Attribute
{
    public string Name { get; } = name;
    public OpenApiQueryParameterType Type { get; } = type;
    public string Description { get; } = description;

    /// <summary>Valores admitidos (enum del esquema); vacío si el parámetro es libre.</summary>
    public string[] AllowedValues { get; init; } = [];

    /// <summary>Valor que rige cuando el parámetro se omite; null si no hay uno explícito.</summary>
    public string? Default { get; init; }
}

/// <summary>
/// Publica los <see cref="OpenApiQueryParameterAttribute"/> de la acción como parámetros <c>in: query</c>
/// opcionales. Debe registrarse DESPUÉS de <see cref="OpenApiErrorResponsesOperationFilter"/>: ese filtro
/// añade un 422 a toda operación con parámetros de consulta enlazados, y estos no lo están.
/// </summary>
public sealed class OpenApiQueryParametersOperationFilter : IOperationFilter
{
    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        var declared = context
            .MethodInfo.GetCustomAttributes(typeof(OpenApiQueryParameterAttribute), inherit: false)
            .Cast<OpenApiQueryParameterAttribute>()
            .ToList();
        if (declared.Count == 0)
            return;

        foreach (var attribute in declared)
        {
            if (
                operation.Parameters.Any(p =>
                    p.In == ParameterLocation.Query
                    && string.Equals(p.Name, attribute.Name, StringComparison.Ordinal)
                )
            )
                continue;

            var schema = attribute.Type switch
            {
                OpenApiQueryParameterType.Boolean => new OpenApiSchema { Type = "boolean" },
                OpenApiQueryParameterType.Guid => new OpenApiSchema { Type = "string", Format = "uuid" },
                _ => new OpenApiSchema { Type = "string" },
            };
            if (attribute.AllowedValues.Length > 0)
                schema.Enum = attribute
                    .AllowedValues.Select(v => (IOpenApiAny)new OpenApiString(v))
                    .ToList();
            if (attribute.Default is not null)
                schema.Default = attribute.Type == OpenApiQueryParameterType.Boolean
                    ? new OpenApiBoolean(bool.Parse(attribute.Default))
                    : new OpenApiString(attribute.Default);

            operation.Parameters.Add(
                new OpenApiParameter
                {
                    Name = attribute.Name,
                    In = ParameterLocation.Query,
                    Required = false,
                    Description = attribute.Description,
                    Schema = schema,
                }
            );
        }
    }
}

/// <summary>
/// API-DOC-V1: tipos de medio publicados que no son los que la API emite. Solo metadato del documento.
/// <list type="bullet">
/// <item>F-16: un controlador sin <c>[Produces]</c> hace que ApiExplorer publique cada respuesta tipada también
/// como <c>text/plain</c> y <c>text/json</c>; la API solo emite <c>application/json</c> (envelope). Se quitan esas
/// dos entradas cuando la respuesta ya publica <c>application/json</c>.</item>
/// <item>Archivos: una respuesta 2xx declarada con tipos de medio explícitos
/// (<c>[ProducesResponseType(typeof(byte[]), 200, "application/pdf")]</c>) se publica con esos tipos, aunque el
/// <c>[Produces("application/json")]</c> de clase los reemplace en ApiExplorer. El esquema es el que ya generó
/// Swashbuckle para el tipo declarado.</item>
/// </list>
/// </summary>
public sealed class OpenApiResponseMediaTypesOperationFilter : IOperationFilter
{
    private static readonly string[] Spurious = ["text/plain", "text/json"];

    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        foreach (var response in operation.Responses.Values)
        {
            if (!response.Content.ContainsKey("application/json"))
                continue;
            foreach (var mediaType in Spurious)
                response.Content.Remove(mediaType);
        }

        var providers = context
            .ApiDescription.ActionDescriptor.EndpointMetadata.OfType<IApiResponseMetadataProvider>()
            .Where(p => p.StatusCode is >= 200 and < 300 && p.Type is not null && p.Type != typeof(void));
        foreach (var provider in providers)
        {
            var explicitTypes = new MediaTypeCollection();
            provider.SetContentTypes(explicitTypes);
            if (explicitTypes.Count == 0)
                continue;
            if (
                !operation.Responses.TryGetValue(
                    provider.StatusCode.ToString(System.Globalization.CultureInfo.InvariantCulture),
                    out var response
                )
                || response.Content.Count == 0
            )
                continue;

            var wanted = explicitTypes.Select(t => MediaTypeHeaderValue.Parse(t).MediaType.ToString()).ToList();
            if (response.Content.Keys.OrderBy(k => k, StringComparer.Ordinal).SequenceEqual(wanted.OrderBy(k => k, StringComparer.Ordinal)))
                continue;

            var schema = response.Content.Values.First().Schema;
            response.Content = wanted.Distinct(StringComparer.OrdinalIgnoreCase)
                .ToDictionary(t => t, _ => new OpenApiMediaType { Schema = schema }, StringComparer.Ordinal);
        }
    }
}

/// <summary>
/// API-DOC-V1 (F-07, F-47, F-48): el 400 de los cuatro <c>Update</c> que comparan el id de la ruta con el del cuerpo
/// (<c>return BadRequest(...)</c> antes de llamar al caso de uso). Solo metadato del documento.
/// <para>
/// El único 400 alcanzable en estas operaciones es ese: los handlers solo responden 404/409/422 y los validadores
/// (422) cubren las mismas reglas que los <c>ArgumentException</c> del dominio, de modo que el envelope de error no es
/// alcanzable con 400 y no se declara (un <c>oneOf</c> con el envelope publicaría un cuerpo que no existe).
/// Cuerpos reales, medidos en ejecución (ver <c>OpenApiContractTests.FindingFixes.cs</c>):
/// <c>BadRequest()</c> sin valor → <c>ProblemDetails</c> de ASP.NET Core en <c>application/json</c>;
/// <c>BadRequest("ID mismatch.")</c> → cadena JSON en <c>application/json</c>.
/// </para>
/// </summary>
public sealed class OpenApiRouteIdMismatchResponseFilter : IOperationFilter
{
    /// <summary>Operaciones cuyo 400 es un <c>ProblemDetails</c> (<c>BadRequest()</c>).</summary>
    public static readonly string[] ProblemDetailsOperations =
    [
        "CashMovementReasons_Update",
        "InventoryAdjustmentReasons_Update",
        "StockAdjustments_UpdateAdjustment",
    ];

    /// <summary>Operaciones cuyo 400 es una cadena JSON (<c>BadRequest("...")</c>).</summary>
    public static readonly string[] JsonStringOperations = ["PaymentTerms_Update"];

    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        var id = operation.OperationId;
        if (id is null)
            return;

        OpenApiSchema schema;
        string description;
        if (ProblemDetailsOperations.Contains(id, StringComparer.Ordinal))
        {
            schema = context.SchemaGenerator.GenerateSchema(
                typeof(Microsoft.AspNetCore.Mvc.ProblemDetails),
                context.SchemaRepository
            );
            description =
                "El id de la ruta no coincide con el del cuerpo. `ProblemDetails` estándar de ASP.NET Core (`type`, `title`, `status` y `traceId` como propiedad adicional), sin envelope. Es el único 400 de la operación: las reglas de entrada responden 422.";
        }
        else if (JsonStringOperations.Contains(id, StringComparer.Ordinal))
        {
            schema = new OpenApiSchema { Type = "string" };
            description =
                "El id de la ruta no coincide con el del cuerpo. Cadena JSON (`\"ID mismatch.\"`), sin envelope. Es el único 400 de la operación: las reglas de entrada responden 422.";
        }
        else
            return;

        operation.Responses["400"] = new OpenApiResponse
        {
            Description = description,
            Content = new Dictionary<string, OpenApiMediaType>
            {
                ["application/json"] = new() { Schema = schema },
            },
        };
    }
}

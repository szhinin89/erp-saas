using System.Reflection;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace ERP.API.Swagger;

/// <summary>
/// Recuerda qué tipo CLR originó cada esquema (por identidad de objeto) para que
/// <see cref="OpenApiSchemaContractDocumentFilter"/> pueda evaluar nulabilidad y serialización con
/// reflexión en vez de adivinar por nombre.
/// </summary>
public sealed class OpenApiSchemaTypeRegistry
{
    private readonly Dictionary<OpenApiSchema, Type> _types = new(ReferenceEqualityComparer.Instance);

    internal void Record(OpenApiSchema schema, Type type) => _types[schema] = type;

    internal bool TryGet(OpenApiSchema schema, out Type type) => _types.TryGetValue(schema, out type!);
}

public sealed class OpenApiSchemaTypeRegistryFilter : ISchemaFilter
{
    private readonly OpenApiSchemaTypeRegistry _registry;

    public OpenApiSchemaTypeRegistryFilter(OpenApiSchemaTypeRegistry registry) => _registry = registry;

    public void Apply(OpenApiSchema schema, SchemaFilterContext context)
    {
        if (schema.Reference is null && schema.Properties is { Count: > 0 })
            _registry.Record(schema, context.Type);
    }
}

/// <summary>
/// Obligatoriedad y campos sensibles del contrato (API-DOC-V1 / Fase 2A). No marca propiedades como
/// <c>required</c> indiscriminadamente:
/// <list type="bullet">
/// <item><b>Esquemas de entrada</b>: se respeta lo que ya declara el modelo
/// (<c>[Required]</c>/<c>required</c>); la obligatoriedad real la deciden los validadores del caso de
/// uso, que no son introspectables, así que no se inventa.</item>
/// <item><b>Esquemas solo de respuesta</b> (alcanzables únicamente desde respuestas): la API siempre
/// serializa la propiedad (no hay <c>DefaultIgnoreCondition</c>), por lo que es <c>required</c> cuando
/// el tipo CLR es no nulo (valor no <c>Nullable&lt;&gt;</c> o referencia con anotación NRT no nula) y no
/// lleva <c>[JsonIgnore]</c>.</item>
/// <item><b>Secretos de entrada</b> (<c>*password</c>, <c>*secret</c>, <c>*token</c>) en esquemas solo de
/// entrada: <c>writeOnly</c> y, para contraseñas, <c>format: password</c>. Los secretos que la API
/// devuelve a propósito (tokens de login, contraseña temporal) no se tocan.</item>
/// </list>
/// También elimina esquemas que dejaron de referenciarse (p. ej. <c>ProblemDetails</c> inferido).
/// </summary>
public sealed partial class OpenApiSchemaContractDocumentFilter : IDocumentFilter
{
    private readonly OpenApiSchemaTypeRegistry _registry;

    public OpenApiSchemaContractDocumentFilter(OpenApiSchemaTypeRegistry registry) =>
        _registry = registry;

    [GeneratedRegex("(password|secret|token)$", RegexOptions.IgnoreCase)]
    private static partial Regex SecretName();

    public void Apply(OpenApiDocument doc, DocumentFilterContext context)
    {
        var schemas = doc.Components.Schemas;

        var request = new HashSet<string>(StringComparer.Ordinal);
        var response = new HashSet<string>(StringComparer.Ordinal);
        foreach (var operation in doc.Paths.Values.SelectMany(p => p.Operations.Values))
        {
            if (operation.RequestBody is not null)
                foreach (var media in operation.RequestBody.Content.Values)
                    Walk(media.Schema, schemas, request);
            foreach (var parameter in operation.Parameters)
                Walk(parameter.Schema, schemas, request);
            foreach (var r in operation.Responses.Values)
                foreach (var media in r.Content.Values)
                    Walk(media.Schema, schemas, response);
        }

        foreach (var (id, schema) in schemas)
        {
            if (!_registry.TryGet(schema, out var type))
                continue;
            var inRequest = request.Contains(id);
            var inResponse = response.Contains(id);

            if (inResponse && !inRequest)
                MarkRequiredFromSerialization(schema, type);
            else if (inRequest && !inResponse)
                MarkWriteOnlySecrets(schema);
        }

        Prune(doc);
    }

    private static void MarkRequiredFromSerialization(OpenApiSchema schema, Type type)
    {
        var nullability = new NullabilityInfoContext();
        foreach (var (name, property) in schema.Properties)
        {
            if (property.Nullable || schema.Required.Contains(name))
                continue;
            var member = FindProperty(type, name);
            if (member is null || member.GetCustomAttribute<JsonIgnoreAttribute>() is not null)
                continue;
            if (!member.CanRead || member.GetMethod?.IsPublic != true)
                continue;
            if (!IsNonNull(member, nullability))
                continue;
            schema.Required.Add(name);
        }
    }

    private static bool IsNonNull(PropertyInfo member, NullabilityInfoContext nullability)
    {
        var t = member.PropertyType;
        if (t.IsValueType)
            return Nullable.GetUnderlyingType(t) is null;
        return nullability.Create(member).ReadState == NullabilityState.NotNull;
    }

    private static PropertyInfo? FindProperty(Type type, string jsonName)
    {
        PropertyInfo? match = null;
        foreach (var p in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            var explicitName = p.GetCustomAttribute<JsonPropertyNameAttribute>()?.Name;
            if (explicitName is not null)
            {
                if (string.Equals(explicitName, jsonName, StringComparison.Ordinal))
                    return p;
                continue;
            }

            if (string.Equals(p.Name, jsonName, StringComparison.OrdinalIgnoreCase))
            {
                if (match is not null)
                    return null; // ambiguo: no se adivina
                match = p;
            }
        }

        return match;
    }

    private static void MarkWriteOnlySecrets(OpenApiSchema schema)
    {
        foreach (var (name, property) in schema.Properties)
        {
            if (!SecretName().IsMatch(name) || !string.Equals(property.Type, "string", StringComparison.Ordinal))
                continue;
            property.WriteOnly = true;
            if (name.EndsWith("password", StringComparison.OrdinalIgnoreCase) && property.Format is null)
                property.Format = "password";
        }
    }

    private static void Walk(
        OpenApiSchema? schema,
        IDictionary<string, OpenApiSchema> components,
        HashSet<string> seen
    )
    {
        if (schema is null)
            return;
        if (schema.Reference is { Id: { } id })
        {
            if (!seen.Add(id))
                return;
            if (components.TryGetValue(id, out var target))
                Walk(target, components, seen);
            return;
        }

        foreach (var p in schema.Properties.Values)
            Walk(p, components, seen);
        Walk(schema.Items, components, seen);
        Walk(schema.AdditionalProperties, components, seen);
        foreach (var s in schema.AllOf.Concat(schema.OneOf).Concat(schema.AnyOf))
            Walk(s, components, seen);
    }

    private static void Prune(OpenApiDocument doc)
    {
        var schemas = doc.Components.Schemas;
        var reachable = new HashSet<string>(StringComparer.Ordinal);
        foreach (var operation in doc.Paths.Values.SelectMany(p => p.Operations.Values))
        {
            foreach (var media in operation.RequestBody?.Content.Values ?? [])
                Walk(media.Schema, schemas, reachable);
            foreach (var parameter in operation.Parameters)
                Walk(parameter.Schema, schemas, reachable);
            foreach (var r in operation.Responses.Values)
                foreach (var media in r.Content.Values)
                    Walk(media.Schema, schemas, reachable);
        }

        foreach (var id in schemas.Keys.Where(k => !reachable.Contains(k)).ToList())
            schemas.Remove(id);
    }
}

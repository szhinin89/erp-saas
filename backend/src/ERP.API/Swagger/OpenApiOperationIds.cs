using System.Text;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.ApiExplorer;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace ERP.API.Swagger;

/// <summary>
/// <c>operationId</c> determinista (API-DOC-V1 / Fase 2A): <c>{Controller}_{Action}</c>, derivado del
/// nombre del controller y del método (o <c>[ActionName]</c>). No depende del orden ni de la ruta, así
/// que solo cambia si se renombra el método. Las sobrecargas o nombres repetidos se desambiguan en
/// <see cref="OpenApiOperationIdsDocumentFilter"/> con método HTTP y, si hace falta, ruta.
/// </summary>
public static class OpenApiOperationIds
{
    public static string? Base(ApiDescription api)
    {
        if (api.ActionDescriptor is not ControllerActionDescriptor action)
            return null;
        return OpenApiSchemaIds.Sanitize($"{action.ControllerName}_{action.ActionName}");
    }

    internal static string PathToken(string path)
    {
        var sb = new StringBuilder();
        foreach (var segment in path.Split('/', StringSplitOptions.RemoveEmptyEntries))
        {
            if (segment.Equals("api", StringComparison.OrdinalIgnoreCase))
                continue;
            var token = segment.Trim('{', '}');
            var colon = token.IndexOf(':');
            if (colon >= 0)
                token = token[..colon];
            if (token.Length == 0)
                continue;
            var isParam = segment.StartsWith('{');
            if (sb.Length > 0)
                sb.Append('_');
            if (isParam)
                sb.Append("By");
            sb.Append(char.ToUpperInvariant(token[0])).Append(token.AsSpan(1));
        }

        return OpenApiSchemaIds.Sanitize(sb.ToString());
    }
}

/// <summary>Garantiza <c>operationId</c> únicos (sin distinguir mayúsculas) en todo el documento.</summary>
public sealed class OpenApiOperationIdsDocumentFilter : IDocumentFilter
{
    public void Apply(OpenApiDocument swaggerDoc, DocumentFilterContext context)
    {
        var all = swaggerDoc
            .Paths.SelectMany(p =>
                p.Value.Operations.Select(o => (Path: p.Key, Method: o.Key, Operation: o.Value))
            )
            .Where(x => !string.IsNullOrEmpty(x.Operation.OperationId))
            .OrderBy(x => x.Path, StringComparer.Ordinal)
            .ThenBy(x => x.Method.ToString(), StringComparer.Ordinal)
            .ToList();

        var duplicated = all
            .GroupBy(x => x.Operation.OperationId, StringComparer.OrdinalIgnoreCase)
            .Where(g => g.Count() > 1)
            .ToList();

        foreach (var group in duplicated)
        {
            foreach (var item in group)
                item.Operation.OperationId = $"{item.Operation.OperationId}_{item.Method}";
        }

        // Si aun así hay repetidos (misma acción, mismo verbo, rutas distintas) se añade la ruta.
        var stillDuplicated = all
            .GroupBy(x => x.Operation.OperationId, StringComparer.OrdinalIgnoreCase)
            .Where(g => g.Count() > 1);
        foreach (var group in stillDuplicated)
        {
            foreach (var item in group)
                item.Operation.OperationId =
                    $"{item.Operation.OperationId}_{OpenApiOperationIds.PathToken(item.Path)}";
        }
    }
}

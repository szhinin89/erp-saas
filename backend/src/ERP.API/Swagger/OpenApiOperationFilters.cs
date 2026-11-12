using ERP.API.Contracts;
using ERP.Application.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ApiExplorer;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.OpenApi.Any;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace ERP.API.Swagger;

/// <summary>
/// Seguridad y autorización del contrato (API-DOC-V1 / Fase 2A), derivadas de los metadatos reales
/// del endpoint (<c>[Authorize]</c>, <c>[AllowAnonymous]</c>): nunca de una lista escrita a mano.
/// <list type="bullet">
/// <item><c>[AllowAnonymous]</c> → <c>security: []</c> (no exige Bearer; no cambia la autorización real).</item>
/// <item><c>[Authorize]</c> → requisito Bearer + sección «Autorización» en la descripción y
/// <c>x-required-permissions</c> con los permisos <c>perm:*</c> exigidos.</item>
/// </list>
/// </summary>
public sealed class OpenApiAuthorizationOperationFilter : IOperationFilter
{
    public const string BearerSchemeId = "Bearer";
    public const string PermissionExtension = "x-required-permissions";
    public const string PermissionPolicyPrefix = "perm:";

    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        var metadata = context.ApiDescription.ActionDescriptor.EndpointMetadata;

        var authorize = metadata.OfType<IAuthorizeData>().ToList();
        var anonymous = metadata.OfType<IAllowAnonymous>().Any();
        if (anonymous || authorize.Count == 0)
        {
            // `security: [{}]` = «sin requisito». Una lista vacía no se serializa (Microsoft.OpenApi la
            // omite), y omitirla dejaría el endpoint a merced de cualquier requisito global futuro.
            operation.Security = [new OpenApiSecurityRequirement()];
            Append(
                operation,
                anonymous
                    ? "**Autorización:** endpoint público (`AllowAnonymous`); no requiere token."
                    : "**Autorización:** el endpoint no declara `[Authorize]`; es accesible sin token."
            );
            return;
        }

        operation.Security =
        [
            new OpenApiSecurityRequirement
            {
                [
                    new OpenApiSecurityScheme
                    {
                        Reference = new OpenApiReference
                        {
                            Type = ReferenceType.SecurityScheme,
                            Id = BearerSchemeId,
                        },
                    }
                ] = Array.Empty<string>(),
            },
        ];

        var policies = authorize
            .Select(a => a.Policy)
            .Where(p => !string.IsNullOrWhiteSpace(p))
            .Select(p => p!)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(p => p, StringComparer.Ordinal)
            .ToList();
        var permissions = policies
            .Where(p => p.StartsWith(PermissionPolicyPrefix, StringComparison.OrdinalIgnoreCase))
            .Select(p => p[PermissionPolicyPrefix.Length..].Trim())
            .ToList();
        var otherPolicies = policies
            .Where(p => !p.StartsWith(PermissionPolicyPrefix, StringComparison.OrdinalIgnoreCase))
            .ToList();
        var roles = authorize
            .Select(a => a.Roles)
            .Where(r => !string.IsNullOrWhiteSpace(r))
            .Distinct(StringComparer.Ordinal)
            .ToList();

        var parts = new List<string> { "requiere JWT Bearer" };
        if (policies.Count == 0 && roles.Count == 0)
            parts.Add("política por defecto `Session` (sesión con tenant)");
        if (permissions.Count > 0)
            parts.Add(
                "permiso" + (permissions.Count > 1 ? "s (todos)" : "") + ": "
                    + string.Join(", ", permissions.Select(p => $"`{p}`"))
            );
        if (otherPolicies.Count > 0)
            parts.Add(
                "política" + (otherPolicies.Count > 1 ? "s" : "") + ": "
                    + string.Join(", ", otherPolicies.Select(p => $"`{p}`"))
            );
        if (roles.Count > 0)
            parts.Add("rol: " + string.Join(", ", roles.Select(r => $"`{r}`")));

        Append(operation, "**Autorización:** " + string.Join("; ", parts) + ".");

        if (permissions.Count > 0)
        {
            var array = new OpenApiArray();
            array.AddRange(permissions.Select(p => new OpenApiString(p)));
            operation.Extensions[PermissionExtension] = array;
        }
    }

    internal static void Append(OpenApiOperation operation, string text) =>
        operation.Description = string.IsNullOrWhiteSpace(operation.Description)
            ? text
            : operation.Description.TrimEnd() + "\n\n" + text;
}

/// <summary>
/// Contrato de errores común (API-DOC-V1 / Fase 2A). Solo documenta lo que el pipeline garantiza para
/// la operación; no inventa 400/404/409 (eso es específico de cada caso de uso y se declara con
/// <c>[ProducesResponseType]</c> en la fase por módulos). Nunca sobrescribe una respuesta ya declarada.
/// <list type="bullet">
/// <item>401 y 403 si hay <c>[Authorize]</c> (siempre rige una política: la indicada o la por defecto <c>Session</c>; rechazo del middleware,
/// sin cuerpo, comprobado en <c>OpenApiContractTests</c>; o envelope de alcance empresa/sucursal).</item>
/// <item>422 si la operación enlaza cuerpo o query (rechazo de <c>ModelState</c> en
/// <c>InvalidModelStateResponseFactory</c>, envelope <c>VALIDATION_ERROR</c>).</item>
/// <item>429 si el endpoint tiene <c>[EnableRateLimiting]</c> (envelope <c>RATE_LIMITED</c>).</item>
/// <item>500 siempre: <c>ExceptionMiddleware</c> envuelve el pipeline completo (envelope <c>INTERNAL_ERROR</c>).</item>
/// <item>Corrige los <c>ProblemDetails</c> que ApiExplorer infiere para <c>[ProducesResponseType(status)]</c>
/// sin tipo: la API no emite <c>ProblemDetails</c>, emite el envelope. Un
/// <c>typeof(ProblemDetails)</c> explícito se respeta.</item>
/// </list>
/// </summary>
public sealed class OpenApiErrorResponsesOperationFilter : IOperationFilter
{
    private readonly OpenApiSchemaIds _schemaIds;

    public OpenApiErrorResponsesOperationFilter(OpenApiSchemaIds schemaIds) => _schemaIds = schemaIds;

    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        var metadata = context.ApiDescription.ActionDescriptor.EndpointMetadata;
        var envelope = context.SchemaGenerator.GenerateSchema(
            typeof(ApiResponse<object>),
            context.SchemaRepository
        );

        FixInferredProblemDetails(operation, context, envelope);

        var anonymous = metadata.OfType<IAllowAnonymous>().Any();
        var authorize = anonymous
            ? new List<IAuthorizeData>()
            : metadata.OfType<IAuthorizeData>().ToList();

        if (authorize.Count > 0)
        {
            Add(
                operation,
                "401",
                $"No autenticado: token ausente, inválido o vencido (rechazo del middleware JWT, sin cuerpo) o rechazo del caso de uso con envelope `code` `{ApiResponseCodes.Common.Unauthorized}`."
            );
            Add(
                operation,
                "403",
                $"Autorización denegada: política del endpoint (rechazo del middleware de autorización, sin cuerpo) o alcance de empresa/sucursal (envelope `code` `{ApiResponseCodes.Common.CompanyScopeForbidden}`/`{ApiResponseCodes.Common.BranchScopeForbidden}`)."
            );
        }

        var bindsInput =
            operation.RequestBody is not null
            || operation.Parameters.Any(p => p.In == ParameterLocation.Query);
        if (bindsInput)
            Add(
                operation,
                "422",
                $"Entrada inválida o no enlazable (ModelState). Envelope con `code` `{ApiResponseCodes.Common.ValidationError}`.",
                envelope
            );

        var limiter = metadata.OfType<EnableRateLimitingAttribute>().FirstOrDefault();
        if (limiter is not null)
            Add(
                operation,
                "429",
                $"Límite de solicitudes excedido (política `{limiter.PolicyName}`). Envelope con `code` `{ApiResponseCodes.Common.RateLimited}`.",
                envelope
            );

        Add(
            operation,
            "500",
            $"Error interno no controlado (ExceptionMiddleware). Envelope con `code` `{ApiResponseCodes.Common.InternalError}`.",
            envelope
        );
    }

    private void FixInferredProblemDetails(
        OpenApiOperation operation,
        OperationFilterContext context,
        OpenApiSchema envelope
    )
    {
        var problemId = _schemaIds.For(typeof(ProblemDetails));
        var explicitByStatus = context
            .ApiDescription.ActionDescriptor.EndpointMetadata.OfType<IApiResponseMetadataProvider>()
            .Where(p => p.Type is not null && p.Type != typeof(void))
            .Select(p => p.StatusCode.ToString())
            .ToHashSet(StringComparer.Ordinal);

        foreach (var (code, response) in operation.Responses.ToList())
        {
            if (code.Length != 3 || code[0] is not ('4' or '5'))
                continue;
            if (explicitByStatus.Contains(code))
                continue; // typeof(ProblemDetails) declarado a propósito: se respeta.
            var usesProblem = response.Content.Values.Any(c =>
                c.Schema?.Reference?.Id == problemId
            );
            if (!usesProblem)
                continue;

            response.Content = new Dictionary<string, OpenApiMediaType>
            {
                ["application/json"] = new() { Schema = envelope },
            };
        }
    }

    private static void Add(
        OpenApiOperation operation,
        string status,
        string description,
        OpenApiSchema? body = null
    )
    {
        if (operation.Responses.ContainsKey(status))
            return;
        var response = new OpenApiResponse { Description = description };
        if (body is not null)
            response.Content["application/json"] = new OpenApiMediaType { Schema = body };
        operation.Responses[status] = response;
    }
}

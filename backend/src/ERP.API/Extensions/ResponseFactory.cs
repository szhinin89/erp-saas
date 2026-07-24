using ERP.API.Contracts;
using ERP.API.Middleware;
using ERP.Application.Common;

namespace ERP.API.Extensions;

/// <summary>
/// Única fábrica del envelope <see cref="ApiResponse{T}"/>. <c>code</c> es la fuente
/// única de verdad: <see cref="MessageCatalog"/> resuelve <c>severity</c> y los
/// mensajes de usuario/desarrollador. Ningún controller, handler o middleware debe
/// construir <see cref="ApiResponse{T}"/> manualmente.
/// </summary>
public static class ResponseFactory
{
    public static ApiResponse<T> Success<T>(HttpContext context, IWebHostEnvironment env, string code, T data)
    {
        var entry = MessageCatalog.Resolve(code);
        return new ApiResponse<T>(code, entry.Severity, BuildMessage(entry, env), data, BuildMeta(context));
    }

    /// <summary>Respuesta de error genérico. <paramref name="errors"/> (mensajes planos) viaja en <c>data.errors</c>.</summary>
    public static ApiResponse<object> Error(HttpContext context, IWebHostEnvironment env, string code, IReadOnlyList<string>? errors = null)
    {
        var entry = MessageCatalog.Resolve(code);
        object? data = errors is { Count: > 0 } ? new { errors } : null;
        return new ApiResponse<object>(code, entry.Severity, BuildMessage(entry, env), data, BuildMeta(context));
    }

    /// <summary>Respuesta de error de validación. <paramref name="fieldErrors"/> mapa campo→mensajes en <c>data.errors</c>.</summary>
    public static ApiResponse<object> ValidationError(HttpContext context, IWebHostEnvironment env, string code, IReadOnlyDictionary<string, string[]> fieldErrors)
    {
        var entry = MessageCatalog.Resolve(code);
        object? data = fieldErrors.Count > 0 ? new { errors = fieldErrors } : null;
        return new ApiResponse<object>(code, entry.Severity, BuildMessage(entry, env), data, BuildMeta(context));
    }

    private static ApiResponseMessage BuildMessage(CatalogMessage entry, IWebHostEnvironment env)
        => new(entry.User, env.IsDevelopment() ? entry.Dev : null);

    private static ApiResponseMeta BuildMeta(HttpContext context)
        => new(RequestCorrelationMiddleware.Resolve(context), DateTimeOffset.UtcNow);
}

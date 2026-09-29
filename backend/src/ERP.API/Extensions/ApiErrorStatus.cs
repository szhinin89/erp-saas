using ERP.Application.Common;

namespace ERP.API.Extensions;

/// <summary>
/// Contrato único Error → HTTP del ERP (ADR-027 §9, ZH-API-ERROR-CONTRACT-HARDENING-01).
/// <c>code</c> → <see cref="ApiErrorCategory"/> (declarada en <see cref="MessageCatalog"/>) → HTTP.
/// Es la ÚNICA tabla de status de error: la consultan <see cref="ApiResultExtensions"/> (fallos de
/// <c>Result&lt;T&gt;</c>) y <c>ExceptionMiddleware</c> (excepciones), que solo deciden el
/// <c>code</c>. Ningún controller, helper ni módulo mapea status por su cuenta.
/// </summary>
public static class ApiErrorStatus
{
    /// <summary>
    /// Status HTTP de un código de error. Un código no catalogado usa la categoría del fallback de
    /// <see cref="MessageCatalog"/> (BusinessRule → 400).
    /// </summary>
    public static int For(string code) =>
        For(MessageCatalog.Resolve(code).Category ?? ApiErrorCategory.BusinessRule);

    /// <summary>
    /// ¿Puede el detalle del error viajar al cliente en <c>data.errors</c>? No para InternalError e
    /// Infrastructure (500/503): ahí el detalle es técnico (excepción, SQL, conexión) y se queda en
    /// logs; el cliente recibe solo el mensaje canónico del catálogo.
    /// </summary>
    public static bool ExposesDetail(string code) =>
        (MessageCatalog.Resolve(code).Category ?? ApiErrorCategory.BusinessRule)
            is not (ApiErrorCategory.InternalError or ApiErrorCategory.Infrastructure);

    public static int For(ApiErrorCategory category) =>
        category switch
        {
            ApiErrorCategory.Validation => StatusCodes.Status422UnprocessableEntity,
            ApiErrorCategory.BusinessRule => StatusCodes.Status400BadRequest,
            ApiErrorCategory.Duplicate => StatusCodes.Status409Conflict,
            ApiErrorCategory.NotFound => StatusCodes.Status404NotFound,
            ApiErrorCategory.Authentication => StatusCodes.Status401Unauthorized,
            ApiErrorCategory.Authorization => StatusCodes.Status403Forbidden,
            ApiErrorCategory.Infrastructure => StatusCodes.Status503ServiceUnavailable,
            ApiErrorCategory.Integration => StatusCodes.Status502BadGateway,
            ApiErrorCategory.RateLimit => StatusCodes.Status429TooManyRequests,
            ApiErrorCategory.InternalError => StatusCodes.Status500InternalServerError,
            _ => throw new ArgumentOutOfRangeException(nameof(category), category, null),
        };
}

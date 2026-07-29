namespace ERP.Application.Common;

/// <summary>Entrada del catálogo: severidad y mensajes (usuario/desarrollador) asociados a un <c>code</c>.</summary>
public sealed record CatalogMessage(string Severity, string User, string Dev);

/// <summary>
/// Fuente única de mensajes del sistema. Cada <c>code</c> de <see cref="ApiResponseCodes"/>
/// mapea a una severidad y a un mensaje estable para usuario y desarrollador.
/// <c>ERP.API.Extensions.ResponseFactory</c> es el único consumidor: ningún
/// controller, handler o middleware debe escribir mensajes de usuario a mano.
/// </summary>
public static class MessageCatalog
{
    private static readonly Dictionary<string, CatalogMessage> Entries = new()
    {
        [ApiResponseCodes.Common.Ok] = new(
            ApiSeverity.Success,
            "Operación realizada correctamente.",
            "Request completed successfully."
        ),
        [ApiResponseCodes.Common.Created] = new(
            ApiSeverity.Success,
            "Recurso creado correctamente.",
            "Resource created successfully."
        ),
        [ApiResponseCodes.Common.ValidationError] = new(
            ApiSeverity.Error,
            "Datos inválidos. Revisa el formulario.",
            "Validation failed."
        ),
        [ApiResponseCodes.Common.NotFound] = new(
            ApiSeverity.Error,
            "El recurso solicitado no existe.",
            "Entity not found."
        ),
        [ApiResponseCodes.Common.Conflict] = new(
            ApiSeverity.Error,
            "La operación no se puede completar por un conflicto con el estado actual.",
            "Conflict with current state."
        ),
        [ApiResponseCodes.Common.UniqueViolation] = new(
            ApiSeverity.Error,
            "Ya existe un registro con esos datos.",
            "Unique constraint violation."
        ),
        [ApiResponseCodes.Common.Forbidden] = new(
            ApiSeverity.Error,
            "No tiene permisos para realizar esta acción.",
            "Forbidden."
        ),
        [ApiResponseCodes.Common.Unauthorized] = new(
            ApiSeverity.Error,
            "No autorizado.",
            "Unauthorized."
        ),
        [ApiResponseCodes.Common.DomainRuleViolation] = new(
            ApiSeverity.Error,
            "No se puede completar la operación.",
            "Domain rule violation."
        ),
        [ApiResponseCodes.Common.ConcurrencyConflict] = new(
            ApiSeverity.Error,
            "El recurso fue modificado por otro proceso. Reintente la operación.",
            "Optimistic concurrency violation."
        ),
        [ApiResponseCodes.Common.DatabaseUnavailable] = new(
            ApiSeverity.Error,
            "Error temporal de base de datos. Reintente en unos segundos.",
            "Database update exception."
        ),
        [ApiResponseCodes.Common.SriCommunicationError] = new(
            ApiSeverity.Error,
            "Error de comunicación con el SRI. La operación quedó pendiente para reintentar.",
            "SRI communication exception."
        ),
        [ApiResponseCodes.Common.CompanyScopeForbidden] = new(
            ApiSeverity.Error,
            "Acceso denegado por contexto de empresa.",
            "Company scope exception."
        ),
        [ApiResponseCodes.Common.BranchScopeForbidden] = new(
            ApiSeverity.Error,
            "Acceso denegado por contexto de sucursal.",
            "Branch scope exception."
        ),
        [ApiResponseCodes.Common.CompanyRucAlreadyExists] = new(
            ApiSeverity.Error,
            "El RUC ya está registrado en el sistema.",
            "Unique RUC violation."
        ),
        [ApiResponseCodes.Common.BadRequest] = new(
            ApiSeverity.Error,
            "Solicitud inválida.",
            "Bad request."
        ),
        [ApiResponseCodes.Common.InternalError] = new(
            ApiSeverity.Error,
            "Error interno del servidor.",
            "Unhandled exception."
        ),
        [ApiResponseCodes.Common.RateLimited] = new(
            ApiSeverity.Error,
            "Demasiados intentos. Intente más tarde.",
            "Rate limit exceeded."
        ),
    };

    private static readonly CatalogMessage Fallback = new(
        ApiSeverity.Error,
        "Ocurrió un error inesperado.",
        "Unmapped response code."
    );

    /// <summary>Resuelve la severidad y mensajes para un <c>code</c>. Devuelve un fallback genérico si no está catalogado.</summary>
    public static CatalogMessage Resolve(string code) => Entries.GetValueOrDefault(code, Fallback);

    /// <summary>Códigos con entrada propia en el catálogo (sin contar el fallback genérico). Uso: tests de consistencia.</summary>
    public static IReadOnlyCollection<string> RegisteredCodes => Entries.Keys;
}

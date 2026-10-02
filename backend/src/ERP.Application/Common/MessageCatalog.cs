namespace ERP.Application.Common;

/// <summary>
/// Entrada del catálogo: severidad, mensajes (usuario/desarrollador) y, para códigos de error, su
/// <see cref="ApiErrorCategory"/> (ADR-027 §8) — de la que ERP.API deriva el HTTP status. Los
/// códigos de éxito (<c>OK</c>/<c>CREATED</c>) no tienen categoría.
/// </summary>
public sealed record CatalogMessage(
    string Severity,
    string User,
    string Dev,
    ApiErrorCategory? Category = null
);

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
            "Validation failed.",
            ApiErrorCategory.Validation
        ),
        [ApiResponseCodes.Common.NotFound] = new(
            ApiSeverity.Error,
            "El recurso solicitado no existe.",
            "Entity not found.",
            ApiErrorCategory.NotFound
        ),
        [ApiResponseCodes.Common.Conflict] = new(
            ApiSeverity.Error,
            "La operación no se puede completar por un conflicto con el estado actual.",
            "Conflict with current state.",
            ApiErrorCategory.Duplicate
        ),
        [ApiResponseCodes.Common.UniqueViolation] = new(
            ApiSeverity.Error,
            "Ya existe un registro con esos datos.",
            "Unique constraint violation.",
            ApiErrorCategory.Duplicate
        ),
        [ApiResponseCodes.Common.Forbidden] = new(
            ApiSeverity.Error,
            "No tiene permisos para realizar esta acción.",
            "Forbidden.",
            ApiErrorCategory.Authorization
        ),
        [ApiResponseCodes.Common.Unauthorized] = new(
            ApiSeverity.Error,
            "No autorizado.",
            "Unauthorized.",
            ApiErrorCategory.Authentication
        ),
        [ApiResponseCodes.Common.DomainRuleViolation] = new(
            ApiSeverity.Error,
            "No se puede completar la operación.",
            "Domain rule violation.",
            ApiErrorCategory.Validation
        ),
        [ApiResponseCodes.Common.ConcurrencyConflict] = new(
            ApiSeverity.Error,
            "El recurso fue modificado por otro proceso. Reintente la operación.",
            "Optimistic concurrency violation.",
            ApiErrorCategory.Duplicate
        ),
        [ApiResponseCodes.Common.DatabaseUnavailable] = new(
            ApiSeverity.Error,
            "Error temporal de base de datos. Reintente en unos segundos.",
            "Database update exception.",
            ApiErrorCategory.Infrastructure
        ),
        [ApiResponseCodes.Common.InvalidDateTimeKind] = new(
            ApiSeverity.Error,
            "Error interno del servidor.",
            "Unspecified/Local DateTimeKind reached SaveChanges — invariant violation, not a database outage.",
            ApiErrorCategory.InternalError
        ),
        [ApiResponseCodes.Common.SriCommunicationError] = new(
            ApiSeverity.Error,
            "Error de comunicación con el SRI. La operación quedó pendiente para reintentar.",
            "SRI communication exception.",
            ApiErrorCategory.Integration
        ),
        [ApiResponseCodes.Common.CompanyScopeForbidden] = new(
            ApiSeverity.Error,
            "Acceso denegado por contexto de empresa.",
            "Company scope exception.",
            ApiErrorCategory.Authorization
        ),
        [ApiResponseCodes.Common.BranchScopeForbidden] = new(
            ApiSeverity.Error,
            "Acceso denegado por contexto de sucursal.",
            "Branch scope exception.",
            ApiErrorCategory.Authorization
        ),
        [ApiResponseCodes.Common.CompanyRucAlreadyExists] = new(
            ApiSeverity.Error,
            "El RUC ya está registrado en el sistema.",
            "Unique RUC violation.",
            ApiErrorCategory.Duplicate
        ),
        [ApiResponseCodes.Common.BadRequest] = new(
            ApiSeverity.Error,
            "Solicitud inválida.",
            "Bad request.",
            ApiErrorCategory.BusinessRule
        ),
        [ApiResponseCodes.Common.InternalError] = new(
            ApiSeverity.Error,
            "Error interno del servidor.",
            "Unhandled exception.",
            ApiErrorCategory.InternalError
        ),
        [ApiResponseCodes.Common.RateLimited] = new(
            ApiSeverity.Error,
            "Demasiados intentos. Intente más tarde.",
            "Rate limit exceeded.",
            ApiErrorCategory.RateLimit
        ),
        [ApiResponseCodes.ElectronicDocuments.SourceNotProcessable] = new(
            ApiSeverity.Error,
            "El documento de origen ya no permite procesamiento electrónico.",
            "Electronic document source no longer allows processing.",
            ApiErrorCategory.Validation
        ),
        [ApiResponseCodes.ElectronicDocuments.SourceCancellationInProcess] = new(
            ApiSeverity.Error,
            "El comprobante electrónico está en proceso ante el SRI. Primero debe resolverse su estado.",
            "Electronic document outcome at SRI is not final; source cancellation blocked.",
            ApiErrorCategory.Validation
        ),
        [ApiResponseCodes.ElectronicDocuments.AnnulmentPending] = new(
            ApiSeverity.Error,
            "El comprobante electrónico tiene una anulación en trámite ante el SRI.",
            "Electronic document annulment pending at SRI.",
            ApiErrorCategory.Validation
        ),
        [ApiResponseCodes.ElectronicDocuments.FiscalCatalogConfigurationError] = new(
            ApiSeverity.Error,
            "El catálogo fiscal del SRI no permite generar el comprobante electrónico. Revise el código de retención usado o contacte a soporte.",
            "SRI fiscal catalog cannot resolve a single official representation; electronic document not generated.",
            ApiErrorCategory.Validation
        ),
        [ApiResponseCodes.Retentions.AnnulmentPending] = new(
            ApiSeverity.Error,
            "La retención tiene una anulación en trámite ante el SRI: la cuenta por pagar no admite pagos, créditos ni ajustes.",
            "Accounts payable on hold by a pending SRI retention annulment.",
            ApiErrorCategory.Validation
        ),
        [ApiResponseCodes.Retentions.AnnulmentRequested] = new(
            ApiSeverity.Success,
            "Se registró la solicitud de anulación ante el SRI. El documento sigue vigente hasta que el SRI confirme ANULADO.",
            "SRI annulment requested; source document remains confirmed."
        ),
        [ApiResponseCodes.Retentions.AnnulmentFinalized] = new(
            ApiSeverity.Success,
            "El SRI confirmó la anulación y el documento quedó anulado.",
            "SRI annulment confirmed; source document cancelled."
        ),
        [ApiResponseCodes.Retentions.AnnulmentFinalizationPending] = new(
            ApiSeverity.Warning,
            "Se registró la anulación del SRI, pero la anulación del documento quedó pendiente y se reintentará automáticamente.",
            "SRI annulment recorded; source cancellation pending retry."
        ),
        [ApiResponseCodes.Retentions.SriStillAuthorized] = new(
            ApiSeverity.Info,
            "El SRI todavía mantiene vigente el comprobante.",
            "SRI reports the document as AUTORIZADO; nothing changed."
        ),
        [ApiResponseCodes.Retentions.SriAnnulmentPending] = new(
            ApiSeverity.Info,
            "Pendiente de anulación en SRI.",
            "SRI reports PENDIENTE DE ANULAR; will be checked again."
        ),
        [ApiResponseCodes.Retentions.SriNotAuthorized] = new(
            ApiSeverity.Warning,
            "El SRI informa NO AUTORIZADO para el comprobante. No se realizó ningún cambio; requiere revisión.",
            "SRI reports NO AUTORIZADO; no automatic action (decision required)."
        ),
        [ApiResponseCodes.Retentions.SriVerificationFailed] = new(
            ApiSeverity.Warning,
            "No fue posible verificar el estado en SRI.",
            "SRI status query failed; no fiscal state change."
        ),
        [ApiResponseCodes.ElectronicDocuments.SourceCancellationRequiresSriAnnulment] = new(
            ApiSeverity.Error,
            "El comprobante electrónico ya está autorizado. Requiere el proceso de anulación electrónica ante el SRI.",
            "Electronic document is authorized; source cancellation requires SRI annulment.",
            ApiErrorCategory.Validation
        ),
    };

    // Código no catalogado (deuda ADR-027 Fase 1: códigos de módulo aún como literales, p. ej.
    // SKU_DUPLICATE, PERIOD_NOT_OPEN): BusinessRule → 400, el mismo status que siempre recibió.
    private static readonly CatalogMessage Fallback = new(
        ApiSeverity.Error,
        "Ocurrió un error inesperado.",
        "Unmapped response code.",
        ApiErrorCategory.BusinessRule
    );

    /// <summary>Resuelve la severidad y mensajes para un <c>code</c>. Devuelve un fallback genérico si no está catalogado.</summary>
    public static CatalogMessage Resolve(string code) => Entries.GetValueOrDefault(code, Fallback);

    /// <summary>Códigos con entrada propia en el catálogo (sin contar el fallback genérico). Uso: tests de consistencia.</summary>
    public static IReadOnlyCollection<string> RegisteredCodes => Entries.Keys;
}

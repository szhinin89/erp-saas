namespace ERP.Application.Common;

/// <summary>
/// Códigos públicos y estables del campo <c>code</c> del envelope de respuesta
/// (<c>ApiResponse&lt;T&gt;</c>). Formato SCREAMING_SNAKE_CASE. Fuente única de
/// verdad: <see cref="MessageCatalog"/> deriva <c>severity</c> y los mensajes
/// (<c>message.user</c> / <c>message.dev</c>) a partir de estos valores.
/// </summary>
/// <remarks>
/// Organización por dominio: los códigos transversales viven en
/// <see cref="Common"/>. Nuevos módulos del ERP (Items, Clientes, Proveedores,
/// Inventario, Ventas, Compras, Contabilidad, ...) agregan su propia clase
/// estática anidada (p. ej. <c>ApiResponseCodes.Inventory</c>) con sus
/// constantes <c>SCREAMING_SNAKE_CASE</c>. Cada código nuevo requiere una
/// entrada en <see cref="MessageCatalog"/>: la regla de arquitectura
/// <c>MessageCatalog_has_exactly_one_entry_per_ApiResponseCode</c> recorre
/// <see cref="ApiResponseCodes"/> y todas sus clases anidadas, por lo que la
/// cobertura se exige automáticamente sin tocar el test.
/// </remarks>
public static class ApiResponseCodes
{
    /// <summary>Códigos transversales (éxito genérico, errores HTTP comunes, infraestructura).</summary>
    public static class Common
    {
        public const string Ok = "OK";
        public const string Created = "CREATED";
        public const string ValidationError = "VALIDATION_ERROR";
        public const string NotFound = "NOT_FOUND";
        public const string Conflict = "CONFLICT";
        public const string UniqueViolation = "UNIQUE_VIOLATION";
        public const string Forbidden = "FORBIDDEN";
        public const string Unauthorized = "UNAUTHORIZED";
        public const string DomainRuleViolation = "DOMAIN_RULE_VIOLATION";
        public const string ConcurrencyConflict = "CONCURRENCY_CONFLICT";
        public const string DatabaseUnavailable = "DATABASE_UNAVAILABLE";
        public const string InvalidDateTimeKind = "INVALID_DATETIME_KIND";
        public const string SriCommunicationError = "SRI_COMMUNICATION_ERROR";
        public const string CompanyScopeForbidden = "COMPANY_SCOPE_FORBIDDEN";
        public const string BranchScopeForbidden = "BRANCH_SCOPE_FORBIDDEN";
        public const string CompanyRucAlreadyExists = "COMPANY_RUC_ALREADY_EXISTS";
        public const string BadRequest = "BAD_REQUEST";
        public const string InternalError = "INTERNAL_ERROR";
        public const string RateLimited = "RATE_LIMITED";
    }

    /// <summary>
    /// ADR-036 — ciclo de vida electrónico frente al documento de origen. Estables: el frontend
    /// puede distinguirlos sin interpretar el texto del mensaje.
    /// </summary>
    public static class ElectronicDocuments
    {
        /// <summary>El documento de origen ya no permite procesamiento electrónico (p.ej. retención anulada).</summary>
        public const string SourceNotProcessable = "ELECTRONIC_DOCUMENT_SOURCE_NOT_PROCESSABLE";

        /// <summary>El comprobante salió o pudo salir al SRI y su resultado no es definitivo: el origen no puede anularse.</summary>
        public const string SourceCancellationInProcess = "ELECTRONIC_DOCUMENT_IN_PROCESS";

        /// <summary>El comprobante ya está autorizado: anular el origen requiere la anulación electrónica ante el SRI.</summary>
        public const string SourceCancellationRequiresSriAnnulment =
            "ELECTRONIC_DOCUMENT_REQUIRES_SRI_ANNULMENT";

        /// <summary>ZH-RETENTION-SRI-ANNULMENT-01 — el comprobante tiene una anulación en trámite ante el SRI.</summary>
        public const string AnnulmentPending = "ELECTRONIC_DOCUMENT_ANNULMENT_PENDING";

        /// <summary>
        /// ZH-SRI-RETENTION-CATALOG-SSOT-01 (ADR-037 D7) — el catálogo global SRI no permite resolver una
        /// representación oficial única y coherente (sin versión vigente, ambigua, sin código XML o tasa
        /// distinta). Error de configuración fiscal: el XML no se genera (fail-closed).
        /// </summary>
        public const string FiscalCatalogConfigurationError = "SRI_FISCAL_CATALOG_CONFIGURATION_ERROR";
    }

    /// <summary>ZH-RETENTION-SRI-ANNULMENT-01 — anulación ante el SRI de retenciones autorizadas.</summary>
    public static class Retentions
    {
        /// <summary>Éxito: se registró la solicitud de anulación ante el SRI; el documento origen sigue confirmado.</summary>
        public const string AnnulmentRequested = "RETENTION_ANNULMENT_REQUESTED";

        /// <summary>Éxito: el SRI confirmó ANULADO y el documento origen quedó anulado.</summary>
        public const string AnnulmentFinalized = "RETENTION_ANNULMENT_FINALIZED";

        /// <summary>Éxito parcial: ANULADO registrado, pero la anulación del origen quedó pendiente de reintento.</summary>
        public const string AnnulmentFinalizationPending = "RETENTION_ANNULMENT_FINALIZATION_PENDING";

        // ZH-RETENTION-SRI-ANNULMENT-01B — resultado de la verificación en ConsultaComprobante (200: la
        // consulta se ejecutó; el estado fiscal lo informa el SRI, nunca el usuario).

        /// <summary>El SRI informa AUTORIZADO: el comprobante sigue vigente; nada cambia.</summary>
        public const string SriStillAuthorized = "RETENTION_ANNULMENT_SRI_STILL_AUTHORIZED";

        /// <summary>El SRI informa PENDIENTE DE ANULAR: se vuelve a consultar más tarde.</summary>
        public const string SriAnnulmentPending = "RETENTION_ANNULMENT_SRI_PENDING";

        /// <summary>El SRI informa NO AUTORIZADO: sin acción automática (ADR-036 §24, Decision Required).</summary>
        public const string SriNotAuthorized = "RETENTION_ANNULMENT_SRI_NOT_AUTHORIZED";

        /// <summary>La consulta no produjo un estado fiscal (rechazada/99, timeout, red, respuesta no reconocida).</summary>
        public const string SriVerificationFailed = "RETENTION_ANNULMENT_SRI_VERIFICATION_FAILED";

        /// <summary>La CxP del origen está retenida por una anulación en trámite (pagos/créditos/ajustes bloqueados).</summary>
        public const string AnnulmentPending =
            ERP.Domain.Modules.Payables.Exceptions.RetentionAnnulmentPendingException.ErrorCode;
    }
}

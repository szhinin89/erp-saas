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
    public static class Purchases
    {
        public const string CancellationSubsequentInventoryExit = "PURCHASE_CANCELLATION_SUBSEQUENT_INVENTORY_EXIT";

        /// <summary>Revisión obsoleta de una distribución de costos: el cliente recarga la compra.</summary>
        public const string CostDistributionStale = ERP.Domain
            .Modules
            .Purchases
            .Exceptions
            .PurchaseCostDistributionStaleException
            .ErrorCode;
    }

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
    /// SEC-A01 — Redis de los límites de login configurado pero no disponible: el login se rechaza
    /// (fail-closed) sin verificar la contraseña.
    /// </summary>
    public static class Auth
    {
        public const string LoginThrottleUnavailable = "LOGIN_THROTTLE_UNAVAILABLE";
    }

    /// <summary>
    /// SEC-06 — invalidación de la caché de permisos efectivos (fail-closed). Estables: el cliente
    /// distingue "no se aplicó nada" de "se guardó con propagación en modo protegido".
    /// </summary>
    public static class Access
    {
        public const string PermissionsInvalidationUnavailable =
            "PERMISSIONS_INVALIDATION_UNAVAILABLE";
        public const string PermissionsInvalidationIncomplete =
            "PERMISSIONS_INVALIDATION_INCOMPLETE";
    }

    /// <summary>
    /// ADR-036 — ciclo de vida electrónico frente al documento de origen. Estables: el frontend
    /// puede distinguirlos sin interpretar el texto del mensaje.
    /// </summary>
    public static class ElectronicDocuments
    {
        /// <summary>El documento de origen ya no permite procesamiento electrónico (p.ej. retención anulada).</summary>
        public const string SourceNotProcessable = "ELECTRONIC_DOCUMENT_SOURCE_NOT_PROCESSABLE";

        /// <summary>SRI-TOGGLE-02 — la empresa tiene deshabilitada la emisión electrónica (o el pendiente requiere reanudación explícita).</summary>
        public const string EmissionDisabled = "ELECTRONIC_DOCUMENT_EMISSION_DISABLED";

        /// <summary>El comprobante salió o pudo salir al SRI y su resultado no es definitivo: el origen no puede anularse.</summary>
        public const string SourceCancellationInProcess = "ELECTRONIC_DOCUMENT_IN_PROCESS";

        /// <summary>El comprobante ya está autorizado: anular el origen requiere la anulación electrónica ante el SRI.</summary>
        public const string SourceCancellationRequiresSriAnnulment =
            "ELECTRONIC_DOCUMENT_REQUIRES_SRI_ANNULMENT";

        /// <summary>ZH-RETENTION-SRI-ANNULMENT-01 — el comprobante tiene una anulación en trámite ante el SRI.</summary>
        public const string AnnulmentPending = "ELECTRONIC_DOCUMENT_ANNULMENT_PENDING";

        /// <summary>R12 Fase B — el comprobante rechazado no cumple las condiciones de retransmisión corregida.</summary>
        public const string RetransmissionNotAllowed = "ELECTRONIC_DOCUMENT_RETRANSMISSION_NOT_ALLOWED";

        /// <summary>R12 Fase B — el XML regenerado no demuestra una corrección compatible con la misma clave.</summary>
        public const string RetransmissionWithoutCorrection =
            "ELECTRONIC_DOCUMENT_RETRANSMISSION_WITHOUT_CORRECTION";

        /// <summary>R12 Fase B — el historial del SRI para la clave es incierto, inconsistente o ya autorizado.</summary>
        public const string RetransmissionSriHistoryBlocked =
            "ELECTRONIC_DOCUMENT_RETRANSMISSION_SRI_HISTORY_BLOCKED";

        /// <summary>
        /// ZH-SRI-RETENTION-CATALOG-SSOT-01 (ADR-037 D7) — el catálogo global SRI no permite resolver una
        /// representación oficial única y coherente (sin versión vigente, ambigua, sin código XML o tasa
        /// distinta). Error de configuración fiscal: el XML no se genera (fail-closed).
        /// </summary>
        public const string FiscalCatalogConfigurationError =
            "SRI_FISCAL_CATALOG_CONFIGURATION_ERROR";

        /// <summary>
        /// ZH-SRI-ANEXO26-PROVIDER-RUC-01 (ADR-038 D6) — la información adicional compuesta viola una
        /// regla del SRI (nombre o valor vacío o &gt; 300, nombre duplicado o reservado, más de 15
        /// campos). El XML no se genera (fail-closed).
        /// </summary>
        public const string AdditionalInfoInvalid = "ELECTRONIC_DOCUMENT_ADDITIONAL_INFO_INVALID";

        /// <summary>
        /// ZH-SRI-ANEXO26-PROVIDER-RUC-01 (ADR-038 D7) — el RUC Proveedor (Ficha 2.34 Anexo 26) es
        /// exigible para el comprobante y la configuración global del proveedor está incompleta
        /// (deshabilitada, sin fecha de vigencia o sin RUC válido). El XML no se genera (fail-closed).
        /// </summary>
        public const string SystemProviderRucNotConfigured =
            "SRI_SYSTEM_PROVIDER_RUC_NOT_CONFIGURED";

        /// <summary>
        /// READINESS-PREVENTIVE-1B — gate fiscal preventivo (01/04/07): faltan requisitos locales del
        /// emisor para iniciar la emisión electrónica. Se devuelve antes de capturar secuencial o
        /// generar efectos comerciales; el detalle lista cada requisito como <c>[código] mensaje</c>
        /// (<c>ElectronicEmissionRequirementCodes</c>).
        /// </summary>
        public const string EmissionRequirementsMissing = "SRI_EMISSION_REQUIREMENTS_MISSING";
    }

    /// <summary>
    /// PHYSICAL-FISCAL-F1..F5 — gate fiscal preventivo de la emisión física: faltan requisitos
    /// verificables (RUC oficial, régimen, autorización de impresión vigente, rango). Se devuelve
    /// antes de capturar secuencial o generar efectos; el detalle lista cada requisito como
    /// <c>[código] mensaje</c> (<c>PhysicalEmissionRequirementCodes</c>).
    /// </summary>
    public static class PhysicalEmission
    {
        public const string RequirementsMissing = "PHYSICAL_EMISSION_REQUIREMENTS_MISSING";
    }

    /// <summary>
    /// ZH-COMMUNICATIONS-TEMPLATES-01 (ADR-039 D12/D13) — fallos de template al encolar: configuración
    /// o validación, nunca SMTP. Ocurren antes de persistir la comunicación.
    /// </summary>
    public static class Communications
    {
        /// <summary>No existe template (default ni override) para la TemplateKey.</summary>
        public const string TemplateNotFound = "COMMUNICATION_TEMPLATE_NOT_FOUND";

        /// <summary>El template (p. ej. un override de empresa) no cumple el contrato: no se usa ni se reemplaza en silencio.</summary>
        public const string TemplateInvalid = "COMMUNICATION_TEMPLATE_INVALID";

        /// <summary>Las variables no cumplen el contrato del template (faltante, no declarada o resultado fuera de límites).</summary>
        public const string TemplateRenderFailed = "COMMUNICATION_TEMPLATE_RENDER_FAILED";

        /// <summary>ZH-EDOC-COMMUNICATIONS-01 — el destinatario no tiene correo válido: evidencia Failed/Permanent, no se envía.</summary>
        public const string RecipientMissing = "COMMUNICATION_RECIPIENT_MISSING";

        /// <summary>Ningún contributor soporta el par módulo origen / tipo de documento.</summary>
        public const string SourceNotSupported = "COMMUNICATION_SOURCE_NOT_SUPPORTED";

        /// <summary>El documento de origen no existe (o no es visible) en su módulo dueño.</summary>
        public const string SourceNotFound = "COMMUNICATION_SOURCE_NOT_FOUND";

        /// <summary>El documento de origen existe pero su estado de negocio no admite la comunicación (p. ej. factura no autorizada).</summary>
        public const string SourceNotEligible = "COMMUNICATION_SOURCE_NOT_ELIGIBLE";

        /// <summary>Un adjunto por referencia no pudo resolverse en su módulo dueño al enviar.</summary>
        public const string AttachmentUnavailable = "COMMUNICATION_ATTACHMENT_UNAVAILABLE";
    }

    /// <summary>
    /// ZH-B-MSG (API-DOC F-10): rechazos de negocio de Contabilidad (asientos manuales, reverso, cierre
    /// anual) que viajan como <c>Result.ValidationFailure(msg, code)</c>. Los valores son el contrato
    /// público histórico del campo <c>code</c>; solo se catalogan (categoría Validation → 422).
    /// </summary>
    public static class Accounting
    {
        /// <summary>No existe período abierto para la fecha contable (o el período está cerrado/bloqueado).</summary>
        public const string PeriodNotOpen = "PERIOD_NOT_OPEN";

        /// <summary>La fecha contable de un asiento manual es futura según la fecha de la empresa.</summary>
        public const string ManualFutureDate = "MANUAL_FUTURE_DATE";

        /// <summary>Contabilidad no está habilitada en la fecha contable.</summary>
        public const string NotEnabled = "ACCOUNTING_NOT_ENABLED";

        /// <summary>
        /// La publicación manual falló por una excepción técnica inesperada (no una regla de negocio): categoría
        /// InternalError (500); el intento queda registrado con este código y se reproduce en los reintentos.
        /// </summary>
        public const string ManualPostInternal = "MANUAL_POST_INTERNAL";

        /// <summary>El asiento de apertura de la Carga Inicial no se reversa por el flujo genérico.</summary>
        public const string OpeningReversalRequiresCorrectionFlow = "OPENING_REVERSAL_REQUIRES_CORRECTION_FLOW";

        public const string FiscalYearPeriodsIncomplete = "FISCAL_YEAR_PERIODS_INCOMPLETE";
        public const string FiscalYearLastPeriodNotOpen = "FISCAL_YEAR_LAST_PERIOD_NOT_OPEN";
        public const string FiscalYearPreviousPeriodsOpen = "FISCAL_YEAR_PREVIOUS_PERIODS_OPEN";
        public const string FiscalYearNotEnded = "FISCAL_YEAR_NOT_ENDED";
        public const string FiscalYearAccountingIncomplete = "FISCAL_YEAR_ACCOUNTING_INCOMPLETE";
        public const string FiscalYearDraftEntries = "FISCAL_YEAR_DRAFT_ENTRIES";
        public const string YearEndResultAccountInvalid = "YEAR_END_RESULT_ACCOUNT_INVALID";
        public const string YearEndClosingDraftExists = "YEAR_END_CLOSING_DRAFT_EXISTS";
        public const string YearEndClosingAlreadyPosted = "YEAR_END_CLOSING_ALREADY_POSTED";
        public const string YearEndClosingNothingToClose = "YEAR_END_CLOSING_NOTHING_TO_CLOSE";
        public const string YearEndClosingOutdated = "YEAR_END_CLOSING_OUTDATED";
        public const string YearEndClosingNotEditable = "YEAR_END_CLOSING_NOT_EDITABLE";
        public const string YearEndClosingReversalDate = "YEAR_END_CLOSING_REVERSAL_DATE";
    }

    /// <summary>
    /// ZH-B-MSG (API-DOC F-33): rechazos de negocio de la Carga Inicial (cierre, contabilización y
    /// reverso del asiento de apertura, preflight por lote) que viajan como
    /// <c>Result.ValidationFailure(msg, code)</c>. Valores = contrato público histórico.
    /// </summary>
    public static class InitialLoad
    {
        public const string Closed = "INITIAL_LOAD_CLOSED";
        public const string CloseBlocked = "INITIAL_LOAD_CLOSE_BLOCKED";
        public const string OpeningSourceChanged = "OPENING_SOURCE_CHANGED";
        public const string OpeningDateMissing = "OPENING_DATE_MISSING";
        public const string OpeningDateMismatch = "OPENING_DATE_MISMATCH";
        public const string OpeningAccountingNotApplicable = "OPENING_ACCOUNTING_NOT_APPLICABLE";
        public const string OpeningAsiRuleHasFixedLines = "OPENING_ASI_RULE_HAS_FIXED_LINES";
        public const string OpeningControlRuleMissing = "OPENING_CONTROL_RULE_MISSING";
        public const string OpeningBridgeAmbiguous = "OPENING_BRIDGE_AMBIGUOUS";
        public const string OpeningControlAccountBlocked = "OPENING_CONTROL_ACCOUNT_BLOCKED";
        public const string OpeningBridgeNotCleared = "OPENING_BRIDGE_NOT_CLEARED";
        public const string OpeningAsiNotCurrent = "OPENING_ASI_NOT_CURRENT";
        public const string OpeningAsiNotPosted = "OPENING_ASI_NOT_POSTED";
        public const string OpeningAsiJournalMismatch = "OPENING_ASI_JOURNAL_MISMATCH";

        // Preflight de contabilización por lote (IL-7B).
        public const string BatchNotAccountable = "BATCH_NOT_ACCOUNTABLE";
        public const string BatchNotCompleted = "BATCH_NOT_COMPLETED";
        public const string AlreadyPosted = "ALREADY_POSTED";
        public const string SourceEmpty = "SOURCE_EMPTY";
        public const string SourceCostMissing = "SOURCE_COST_MISSING";
        public const string AmountNotPositive = "AMOUNT_NOT_POSITIVE";
    }

    /// <summary>ZH-RETENTION-SRI-ANNULMENT-01 — anulación ante el SRI de retenciones autorizadas.</summary>
    public static class Retentions
    {
        /// <summary>Éxito: se registró la solicitud de anulación ante el SRI; el documento origen sigue confirmado.</summary>
        public const string AnnulmentRequested = "RETENTION_ANNULMENT_REQUESTED";

        /// <summary>Éxito: el SRI confirmó ANULADO y el documento origen quedó anulado.</summary>
        public const string AnnulmentFinalized = "RETENTION_ANNULMENT_FINALIZED";

        /// <summary>Éxito parcial: ANULADO registrado, pero la anulación del origen quedó pendiente de reintento.</summary>
        public const string AnnulmentFinalizationPending =
            "RETENTION_ANNULMENT_FINALIZATION_PENDING";

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
        public const string AnnulmentPending = ERP.Domain
            .Modules
            .Payables
            .Exceptions
            .RetentionAnnulmentPendingException
            .ErrorCode;
    }
}

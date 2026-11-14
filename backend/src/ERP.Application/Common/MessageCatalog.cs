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
        [ApiResponseCodes.Purchases.CancellationSubsequentInventoryExit] = new(
            ApiSeverity.Error,
            "La compra no puede anularse porque existen movimientos posteriores de salida de inventario. Utilice devolución o nota de crédito según corresponda.",
            "Purchase cancellation blocked by a subsequent effective inventory exit; a reversal does not restore eligibility.",
            ApiErrorCategory.BusinessRule
        ),
        [ApiResponseCodes.Purchases.CostDistributionStale] = new(
            ApiSeverity.Error,
            "La distribución de costos cambió. Recargue la compra antes de modificarla.",
            "Stale purchase cost distribution revision.",
            ApiErrorCategory.Duplicate
        ),
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
        [ApiResponseCodes.Auth.LoginThrottleUnavailable] = new(
            ApiSeverity.Error,
            "El inicio de sesión no está disponible temporalmente. Reintente en unos segundos.",
            "Login throttle store unavailable; credentials not verified.",
            ApiErrorCategory.Infrastructure
        ),
        [ApiResponseCodes.Access.PermissionsInvalidationUnavailable] = new(
            ApiSeverity.Error,
            "No se pudo garantizar la actualización de permisos. No se aplicó ningún cambio; reintente en unos segundos.",
            "Permissions cache invalidation could not be started; mutation not persisted.",
            ApiErrorCategory.Infrastructure
        ),
        [ApiResponseCodes.Access.PermissionsInvalidationIncomplete] = new(
            ApiSeverity.Error,
            "El cambio se guardó, pero la propagación de permisos no pudo confirmarse. Los permisos afectados se evalúan desde la base de datos hasta normalizarse.",
            "Mutation persisted; permissions cache invalidation incomplete, scope kept in protected (cache-bypass) mode.",
            ApiErrorCategory.Infrastructure
        ),
        [ApiResponseCodes.ElectronicDocuments.SourceNotProcessable] = new(
            ApiSeverity.Error,
            "El documento de origen ya no permite procesamiento electrónico.",
            "Electronic document source no longer allows processing.",
            ApiErrorCategory.Validation
        ),
        [ApiResponseCodes.ElectronicDocuments.EmissionDisabled] = new(
            ApiSeverity.Error,
            "La emisión electrónica está deshabilitada para esta empresa (no se envía ni consulta nada en el SRI) o el pendiente requiere reanudación explícita.",
            "Company electronic emission paused (no SRI transmission or query), or the pending item requires explicit resume.",
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
        [ApiResponseCodes.ElectronicDocuments.RetransmissionNotAllowed] = new(
            ApiSeverity.Error,
            "El comprobante rechazado no cumple las condiciones para corregirse y retransmitirse. Revise el detalle indicado.",
            "Rejected electronic document is not eligible for corrected retransmission.",
            ApiErrorCategory.Validation
        ),
        [ApiResponseCodes.ElectronicDocuments.RetransmissionWithoutCorrection] = new(
            ApiSeverity.Error,
            "El comprobante regenerado no demuestra una corrección compatible con la misma clave de acceso; no se retransmitió.",
            "Regenerated XML shows no correction compatible with the same access key; nothing was transmitted.",
            ApiErrorCategory.Validation
        ),
        [ApiResponseCodes.ElectronicDocuments.RetransmissionSriHistoryBlocked] = new(
            ApiSeverity.Error,
            "El historial del comprobante en el SRI no permite retransmitirlo (autorizado, en proceso, incierto o inconsistente).",
            "SRI history for the access key blocks retransmission (authorized, pending, uncertain or inconsistent).",
            ApiErrorCategory.Validation
        ),
        [ApiResponseCodes.ElectronicDocuments.FiscalCatalogConfigurationError] = new(
            ApiSeverity.Error,
            "El catálogo fiscal del SRI no permite generar el comprobante electrónico. Revise el código de retención usado o contacte a soporte.",
            "SRI fiscal catalog cannot resolve a single official representation; electronic document not generated.",
            ApiErrorCategory.Validation
        ),
        [ApiResponseCodes.ElectronicDocuments.AdditionalInfoInvalid] = new(
            ApiSeverity.Error,
            "La información adicional del comprobante electrónico no cumple las reglas del SRI. Revise las observaciones del documento.",
            "Electronic document additional info violates SRI rules; electronic document not generated.",
            ApiErrorCategory.Validation
        ),
        [ApiResponseCodes.ElectronicDocuments.SystemProviderRucNotConfigured] = new(
            ApiSeverity.Error,
            "El RUC del proveedor tecnológico es obligatorio para este comprobante y su configuración global está incompleta. Contacte a soporte.",
            "SRI system provider RUC (Annex 26) is required and its global configuration is incomplete; electronic document not generated.",
            ApiErrorCategory.Validation
        ),
        [ApiResponseCodes.ElectronicDocuments.EmissionRequirementsMissing] = new(
            ApiSeverity.Error,
            "No se puede emitir el comprobante electrónico porque la empresa no cumple los requisitos de emisión electrónica. Revise el detalle indicado.",
            "Local issuer requirements for electronic emission are missing; operation rejected before any sequence or commercial effect.",
            ApiErrorCategory.Validation
        ),
        [ApiResponseCodes.PhysicalEmission.RequirementsMissing] = new(
            ApiSeverity.Error,
            "No se puede emitir el comprobante físico porque faltan requisitos fiscales de emisión física. Revise el detalle y complete la configuración indicada.",
            "Verifiable requirements for physical emission are missing; operation rejected before any sequence or commercial effect.",
            ApiErrorCategory.Validation
        ),
        [ApiResponseCodes.Communications.TemplateNotFound] = new(
            ApiSeverity.Error,
            "No existe una plantilla de correo para esta comunicación.",
            "Communication template not found for the template key.",
            ApiErrorCategory.Validation
        ),
        [ApiResponseCodes.Communications.TemplateInvalid] = new(
            ApiSeverity.Error,
            "La plantilla de correo configurada no es válida. Revise la plantilla personalizada de la empresa.",
            "Communication template violates its contract; not used and not silently replaced.",
            ApiErrorCategory.Validation
        ),
        [ApiResponseCodes.Communications.TemplateRenderFailed] = new(
            ApiSeverity.Error,
            "No se pudo generar el contenido del correo con los datos disponibles.",
            "Communication template variables do not satisfy the template contract.",
            ApiErrorCategory.Validation
        ),
        [ApiResponseCodes.Communications.RecipientMissing] = new(
            ApiSeverity.Error,
            "El destinatario no tiene un correo electrónico válido registrado; el correo no se envió.",
            "Communication recipient has no valid email address; recorded as failed, not sent.",
            ApiErrorCategory.Validation
        ),
        [ApiResponseCodes.Communications.SourceNotSupported] = new(
            ApiSeverity.Error,
            "Este tipo de documento no tiene una comunicación configurada.",
            "No communication contributor supports this source module/document type.",
            ApiErrorCategory.Validation
        ),
        [ApiResponseCodes.Communications.SourceNotFound] = new(
            ApiSeverity.Error,
            "No se encontró el documento de origen de la comunicación.",
            "Communication source document not found in its owner module.",
            ApiErrorCategory.Validation
        ),
        [ApiResponseCodes.Communications.SourceNotEligible] = new(
            ApiSeverity.Error,
            "El estado del documento de origen no permite enviar esta comunicación.",
            "Communication source document state does not allow this communication.",
            ApiErrorCategory.Validation
        ),
        [ApiResponseCodes.Communications.AttachmentUnavailable] = new(
            ApiSeverity.Error,
            "No se pudo obtener un adjunto del correo desde su módulo de origen.",
            "Communication attachment could not be resolved by its owner module.",
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

        // ZH-B-MSG (API-DOC F-10 / F-33): rechazos de Contabilidad y Carga Inicial creados con
        // Result.ValidationFailure(msg, code). Antes caían en el fallback (400, «Ocurrió un error
        // inesperado»); ahora son Validation → 422 con mensaje propio. El detalle específico sigue
        // viajando en data.errors.
        [ApiResponseCodes.Accounting.PeriodNotOpen] = Rejection(
            "No existe un período contable abierto para la fecha indicada.",
            "No open accounting period for the accounting date."
        ),
        [ApiResponseCodes.Accounting.ManualFutureDate] = Rejection(
            "La fecha contable no puede ser futura.",
            "Manual journal accounting date is in the future."
        ),
        [ApiResponseCodes.Accounting.NotEnabled] = Rejection(
            "Contabilidad no está habilitada en la fecha indicada.",
            "Accounting is not enabled on the accounting date."
        ),
        // Origen: excepción técnica inesperada (flush/commit) al publicar un asiento manual; no es una regla
        // de negocio recuperable. Va como error interno (500, detalle oculto) con su mensaje propio; el código
        // se conserva porque queda persistido en el intento y se reproduce en los reintentos idempotentes.
        [ApiResponseCodes.Accounting.ManualPostInternal] = new(
            ApiSeverity.Error,
            "No se pudo completar la publicación. Consulte el intento antes de volver a publicar.",
            "Manual journal publication failed with an unexpected technical error; the attempt was recorded.",
            ApiErrorCategory.InternalError
        ),
        [ApiResponseCodes.Accounting.OpeningReversalRequiresCorrectionFlow] = Rejection(
            "El asiento de apertura de la Carga inicial no puede reversarse por este flujo; use la corrección de apertura.",
            "Opening balance journal entries cannot be reversed through the generic reversal."
        ),
        [ApiResponseCodes.Accounting.FiscalYearPeriodsIncomplete] = Rejection(
            "El ejercicio no tiene períodos que cubran todo el año.",
            "Fiscal year periods do not cover the whole year."
        ),
        [ApiResponseCodes.Accounting.FiscalYearLastPeriodNotOpen] = Rejection(
            "El último período del ejercicio no está abierto: el cierre anual ya no puede registrarse.",
            "Last period of the fiscal year is not open."
        ),
        [ApiResponseCodes.Accounting.FiscalYearPreviousPeriodsOpen] = Rejection(
            "Cierre primero los períodos anteriores del ejercicio.",
            "Previous periods of the fiscal year are still open."
        ),
        [ApiResponseCodes.Accounting.FiscalYearNotEnded] = Rejection(
            "El ejercicio aún no ha terminado: el cierre anual se genera a partir del día siguiente a su fin.",
            "Fiscal year has not ended according to the company date."
        ),
        [ApiResponseCodes.Accounting.FiscalYearAccountingIncomplete] = Rejection(
            "El ejercicio tiene información contable incompleta. Resuelva los pendientes antes del cierre anual.",
            "Fiscal year accounting is incomplete."
        ),
        [ApiResponseCodes.Accounting.FiscalYearDraftEntries] = Rejection(
            "El último período del ejercicio tiene asientos sin publicar: publíquelos o descártelos antes del cierre anual.",
            "Last period has draft or non-final journal entries."
        ),
        [ApiResponseCodes.Accounting.YearEndResultAccountInvalid] = Rejection(
            "La cuenta de Resultado del ejercicio debe existir, estar activa, admitir movimientos y ser de Patrimonio.",
            "Year-end result account is missing or invalid."
        ),
        [ApiResponseCodes.Accounting.YearEndClosingDraftExists] = Rejection(
            "Ya existe un asiento de cierre anual en borrador: publíquelo o descártelo.",
            "A year-end closing draft already exists."
        ),
        [ApiResponseCodes.Accounting.YearEndClosingAlreadyPosted] = Rejection(
            "El resultado del ejercicio ya fue cerrado con un asiento contabilizado.",
            "Year-end closing entry already posted."
        ),
        [ApiResponseCodes.Accounting.YearEndClosingNothingToClose] = Rejection(
            "Las cuentas de resultados del ejercicio no tienen saldo: no hay resultado que cerrar.",
            "Nominal accounts have no balance; nothing to close."
        ),
        [ApiResponseCodes.Accounting.YearEndClosingOutdated] = Rejection(
            "El asiento de cierre anual quedó desactualizado: descártelo y vuelva a generarlo.",
            "Year-end closing entry is outdated relative to current balances."
        ),
        [ApiResponseCodes.Accounting.YearEndClosingNotEditable] = Rejection(
            "El asiento de cierre anual se calcula desde Períodos y no se edita: descártelo y vuelva a generarlo.",
            "Year-end closing entries are computed and not editable."
        ),
        [ApiResponseCodes.Accounting.YearEndClosingReversalDate] = Rejection(
            "La fecha de reverso del asiento de cierre anual no es válida.",
            "Invalid reversal date for a year-end closing entry."
        ),
        [ApiResponseCodes.InitialLoad.Closed] = Rejection(
            "La Carga Inicial está cerrada: ya no se pueden modificar los saldos de apertura.",
            "Initial load is closed; opening balances are immutable."
        ),
        [ApiResponseCodes.InitialLoad.CloseBlocked] = Rejection(
            "No se puede cerrar la Carga Inicial: hay bloqueos pendientes. Revise el detalle indicado.",
            "Initial load closing is blocked by pending reconciliation items."
        ),
        [ApiResponseCodes.InitialLoad.OpeningSourceChanged] = Rejection(
            "El monto o la fecha del lote ya no coinciden con los datos confirmados.",
            "Opening posting source changed after it was registered."
        ),
        [ApiResponseCodes.InitialLoad.OpeningDateMissing] = Rejection(
            "La empresa no tiene definida su fecha de apertura de saldos.",
            "Company opening balance date is not defined."
        ),
        [ApiResponseCodes.InitialLoad.OpeningDateMismatch] = Rejection(
            "El corte del lote no coincide con la fecha de apertura de la empresa.",
            "Batch cutoff date differs from the company opening balance date."
        ),
        [ApiResponseCodes.InitialLoad.OpeningAccountingNotApplicable] = Rejection(
            "Contabilidad no aplica en la fecha de apertura de la empresa: la apertura no genera asientos.",
            "Accounting is not applicable on the opening date."
        ),
        [ApiResponseCodes.InitialLoad.OpeningAsiRuleHasFixedLines] = Rejection(
            "La regla de contabilización del asiento de apertura no admite líneas fijas.",
            "Opening journal posting rule has fixed lines."
        ),
        [ApiResponseCodes.InitialLoad.OpeningControlRuleMissing] = Rejection(
            "Falta la regla de contabilización de la cuenta de control de la apertura.",
            "Opening control account posting rule is missing."
        ),
        [ApiResponseCodes.InitialLoad.OpeningBridgeAmbiguous] = Rejection(
            "La cuenta puente de la apertura es ambigua: revise la configuración contable.",
            "Opening bridge account resolution is ambiguous."
        ),
        [ApiResponseCodes.InitialLoad.OpeningControlAccountBlocked] = Rejection(
            "Una cuenta de control de la apertura no admite movimientos. Revise el detalle indicado.",
            "Opening control account is not postable."
        ),
        [ApiResponseCodes.InitialLoad.OpeningBridgeNotCleared] = Rejection(
            "La cuenta puente de la apertura no quedaría en cero. Revise las líneas del asiento.",
            "Opening bridge account would not clear to zero."
        ),
        [ApiResponseCodes.InitialLoad.OpeningAsiNotCurrent] = Rejection(
            "El asiento de apertura indicado no es el vigente.",
            "Opening journal entry is not the current one."
        ),
        [ApiResponseCodes.InitialLoad.OpeningAsiNotPosted] = Rejection(
            "El asiento de apertura aún no está contabilizado.",
            "Opening journal entry is not posted."
        ),
        [ApiResponseCodes.InitialLoad.OpeningAsiJournalMismatch] = Rejection(
            "El asiento indicado no corresponde a la publicación de apertura.",
            "Journal entry does not match the opening posting."
        ),
        [ApiResponseCodes.InitialLoad.BatchNotAccountable] = Rejection(
            "Este tipo de carga inicial no genera asiento de apertura.",
            "Import type does not generate an opening journal entry."
        ),
        [ApiResponseCodes.InitialLoad.BatchNotCompleted] = Rejection(
            "Solo un lote confirmado por completo se contabiliza.",
            "Only a fully confirmed batch can be posted."
        ),
        [ApiResponseCodes.InitialLoad.AlreadyPosted] = Rejection(
            "La apertura del lote ya está contabilizada.",
            "Batch opening is already posted."
        ),
        [ApiResponseCodes.InitialLoad.SourceEmpty] = Rejection(
            "El lote no tiene saldos confirmados que contabilizar.",
            "Batch has no confirmed balances to post."
        ),
        [ApiResponseCodes.InitialLoad.SourceCostMissing] = Rejection(
            "Hay movimientos de inventario inicial sin costo total.",
            "Initial stock movements without total cost."
        ),
        [ApiResponseCodes.InitialLoad.AmountNotPositive] = Rejection(
            "El monto de apertura del lote debe ser mayor a cero.",
            "Opening amount must be greater than zero."
        ),
    };

    /// <summary>Rechazo de negocio con categoría Validation (422), mismo contrato que el resto de <c>ValidationFailure</c> catalogados.</summary>
    private static CatalogMessage Rejection(string user, string dev) =>
        new(ApiSeverity.Error, user, dev, ApiErrorCategory.Validation);

    // Código no catalogado (deuda ADR-027 Fase 1: códigos de módulo aún como literales, p. ej.
    // SKU_DUPLICATE; Contabilidad/Carga Inicial ya catalogados, F-10/F-33): BusinessRule → 400, el mismo status que siempre recibió.
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

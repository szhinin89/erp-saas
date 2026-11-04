using ERP.Domain.Modules.ElectronicDocuments.Entities;
using ERP.Domain.Modules.ElectronicDocuments.Enums;

namespace ERP.Domain.Modules.ElectronicDocuments.Interfaces;

public interface IElectronicDocumentRepository
{
    Task<string?> GetRetryMaximumSettingAsync(Guid tenantId, Guid companyId, CancellationToken ct = default);
    Task<ElectronicDocument?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct = default);

    /// <summary>
    /// R11 — instante de la primera transición a Received registrada en el timeline del comprobante
    /// (<see cref="ElectronicDocumentAudit"/>, persistido en la misma transacción que el estado): el ERP
    /// recibió RECIBIDA (o 70/43/45, envío anterior ya registrado) y desde ahí corre el plazo del SRI.
    /// <c>null</c> si no hay evidencia de recepción (p. ej. <c>Signed</c> tras un fallo de transporte).
    /// R12 Fase B: con <paramref name="sinceUtc"/> (inicio del intento vigente) solo cuenta la RECIBIDA
    /// de ese intento — el plazo corre por intento. Sin retransmisión es <c>null</c> (sin cambios).
    /// </summary>
    Task<DateTime?> GetFirstReceivedAtUtcAsync(
        Guid tenantId,
        Guid id,
        DateTime? sinceUtc = null,
        CancellationToken ct = default
    );

    /// <summary>
    /// R12 Fase B — evidencia persistida del rechazo del intento vigente (desde <paramref name="sinceUtc"/>,
    /// o todo el historial en el intento 1): la última transición a Rejected del timeline (su estado de
    /// origen distingue DEVUELTA de NO AUTORIZADO) y los mensajes SRI estructurados registrados.
    /// <c>null</c> si el timeline no registra ese rechazo.
    /// </summary>
    Task<ElectronicDocumentRejectionEvidence?> GetRejectionEvidenceAsync(
        Guid tenantId,
        Guid id,
        DateTime? sinceUtc,
        CancellationToken ct = default
    );

    /// <summary>R12 Fase B — reclamo ya registrado para esa solicitud (clave de idempotencia), o null.</summary>
    Task<ElectronicDocumentTransmissionAttempt?> GetTransmissionAttemptByIdempotencyKeyAsync(
        Guid tenantId,
        Guid electronicDocumentId,
        Guid idempotencyKey,
        CancellationToken ct = default
    );

    /// <summary>R12 Fase B — último reclamo de retransmisión registrado del documento, o null.</summary>
    Task<ElectronicDocumentTransmissionAttempt?> GetLatestTransmissionAttemptAsync(
        Guid tenantId,
        Guid electronicDocumentId,
        CancellationToken ct = default
    );

    Task AddTransmissionAttemptAsync(
        ElectronicDocumentTransmissionAttempt attempt,
        CancellationToken ct = default
    );

    /// <summary>Deja de trackear un reclamo que no llegó a persistirse (rollback del reclamo).</summary>
    void DiscardTransmissionAttempt(ElectronicDocumentTransmissionAttempt attempt);

    /// <summary>
    /// Busca el documento electrónico registrado para un documento de origen específico —
    /// es el punto de acceso que usarán los módulos consumidores (conocen su propio
    /// SourceModule/SourceEntityId, no el Id interno de ElectronicDocument).
    /// </summary>
    Task<ElectronicDocument?> GetBySourceAsync(
        Guid tenantId,
        string sourceModule,
        Guid sourceEntityId,
        CancellationToken ct = default
    );

    /// <summary>
    /// SALES-ELECTRONIC-STATUS-CLARITY-01 — lectura en lote (una sola consulta, sin tracking)
    /// del estado real de los documentos electrónicos de una página de documentos de origen.
    /// Los orígenes sin documento electrónico registrado no aparecen en el diccionario; el
    /// consumidor decide cómo presentarlos (nunca se infiere un estado).
    /// </summary>
    Task<IReadOnlyDictionary<Guid, ElectronicDocumentState>> GetCurrentStatesBySourcesAsync(
        Guid tenantId,
        string sourceModule,
        IReadOnlyCollection<Guid> sourceEntityIds,
        CancellationToken ct = default
    );

    Task AddAsync(ElectronicDocument document, CancellationToken ct = default);

    /// <summary>Relee de la BD los valores (y el <c>xmin</c>) de una instancia ya trackeada, descartando cambios en memoria.</summary>
    Task ReloadAsync(ElectronicDocument document, CancellationToken ct = default);
    Task SaveChangesAsync(CancellationToken ct = default);

    /// <summary>
    /// Lectura paginada para el Monitor de Documentos Electrónicos (Fase 8) — nunca trackeada
    /// (AsNoTracking), siempre acotada a <paramref name="tenantId"/> y, si se especifica,
    /// a <paramref name="companyId"/>. El texto libre busca sobre clave de acceso, número
    /// de autorización y (si el módulo de origen lo permite resolverlo) número de documento
    /// / nombre del cliente-proveedor de origen.
    /// </summary>
    Task<(IReadOnlyList<ElectronicDocument> Items, int Total)> GetPagedAsync(
        Guid tenantId,
        Guid? companyId,
        DateTime? dateFromUtc,
        DateTime? dateToUtcExclusive,
        IReadOnlyList<ElectronicDocumentState>? states,
        ElectronicDocumentType? documentType,
        string? environment,
        string? search,
        int page,
        int pageSize,
        CancellationToken ct = default
    );

    /// <summary>Conteo de documentos por estado — base del dashboard del Monitor. Nunca hardcodeado.</summary>
    Task<IReadOnlyDictionary<ElectronicDocumentState, int>> GetStateCountsAsync(
        Guid tenantId,
        Guid? companyId,
        CancellationToken ct = default
    );

    /// <summary>Documentos autorizados hoy en el día calendario de la empresa — para la tarjeta "Autorizados hoy" del dashboard.</summary>
    Task<int> CountAuthorizedTodayAsync(
        Guid tenantId,
        Guid? companyId,
        CancellationToken ct = default
    );

    /// <summary>Documentos que transicionaron a un estado de error hoy en el día calendario de la empresa.</summary>
    Task<int> CountErrorsTodayAsync(Guid tenantId, Guid? companyId, CancellationToken ct = default);

    /// <summary>Promedio real (en minutos) entre creación y autorización — null si ningún documento fue autorizado todavía.</summary>
    Task<double?> GetAverageAuthorizationMinutesAsync(
        Guid tenantId,
        Guid? companyId,
        CancellationToken ct = default
    );

    /// <summary>
    /// "Reintentos pendientes": documentos en un estado que el job automático de reintentos
    /// atiende (ElectronicDocumentRetryPolicy.AutomaticRetryStates), con al menos un reintento
    /// registrado (RetryCount &gt; 0) y que todavía no agotaron el máximo efectivo de empresa (o con resultado
    /// SRI pendiente, que sigue en consulta hasta la ventana de 24 h — R11). Un documento que ya
    /// salió de esos estados (p.ej. Authorized) no cuenta aunque conserve RetryCount histórico.
    /// </summary>
    Task<int> CountPendingRetriesAsync(
        Guid tenantId,
        Guid? companyId,
        CancellationToken ct = default
    );

    /// <summary>Documentos creados hoy en el día calendario de la empresa, sin importar su estado — "Total emitidos hoy" del dashboard.</summary>
    Task<int> CountCreatedTodayAsync(
        Guid tenantId,
        Guid? companyId,
        CancellationToken ct = default
    );

    /// <summary>
    /// Candidatos a reintento automático, cross-tenant — usado exclusivamente por el job Hangfire
    /// de reintentos, que corre sin contexto de tenant. Filtra antes del lote con la política
    /// (estados, intentos y backoff) y la preferencia de empresa: todo candidato devuelto es
    /// elegible. ZH-ELECTRONIC-RETRY-TENANT-CONTEXT-01: devuelve solo identificadores (sin
    /// tracking); cada candidato se procesa después bajo el contexto de su propio tenant/empresa,
    /// donde el documento se vuelve a leer con los filtros fail-closed.
    /// </summary>
    Task<IReadOnlyList<ElectronicDocumentRetryCandidate>> GetRetryCandidatesAsync(
        int batchSize, CancellationToken ct = default
    );
}

/// <summary>
/// R12 Fase B — rechazo registrado en el timeline: <see cref="FromState"/> es el estado desde el que se
/// rechazó (Sent = DEVUELTA de Recepción; Signed/Dispatching/Received = NO AUTORIZADO de Autorización).
/// </summary>
public sealed record ElectronicDocumentRejectionEvidence(
    ElectronicDocumentState? FromState,
    DateTime RejectedAtUtc,
    IReadOnlyList<ERP.Domain.Modules.ElectronicDocuments.ValueObjects.SriMessage> Messages
);

/// <summary>Identidad de un candidato a reintento automático ya elegible.</summary>
public sealed record ElectronicDocumentRetryCandidate(
    Guid TenantId,
    Guid CompanyId,
    Guid ElectronicDocumentId
);

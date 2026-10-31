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
    /// </summary>
    Task<DateTime?> GetFirstReceivedAtUtcAsync(Guid tenantId, Guid id, CancellationToken ct = default);

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

/// <summary>Identidad de un candidato a reintento automático ya elegible.</summary>
public sealed record ElectronicDocumentRetryCandidate(
    Guid TenantId,
    Guid CompanyId,
    Guid ElectronicDocumentId
);

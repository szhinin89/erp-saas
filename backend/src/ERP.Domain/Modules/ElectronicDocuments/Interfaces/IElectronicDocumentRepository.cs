using ERP.Domain.Modules.ElectronicDocuments.Entities;
using ERP.Domain.Modules.ElectronicDocuments.Enums;

namespace ERP.Domain.Modules.ElectronicDocuments.Interfaces;

public interface IElectronicDocumentRepository
{
    Task<ElectronicDocument?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct = default);

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

    /// <summary>Documentos con al menos un reintento registrado (RetryCount &gt; 0).</summary>
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
    /// Candidatos a reintento automático (Draft/Failed/Signed/Received), cross-tenant — usado
    /// exclusivamente por el job Hangfire de reintentos, que corre sin contexto de tenant.
    /// ZH-ELECTRONIC-RETRY-TENANT-CONTEXT-01: devuelve solo identificadores y los datos del
    /// backoff (sin tracking); cada candidato se procesa después bajo el contexto de su propio
    /// tenant/empresa, donde el documento se vuelve a leer con los filtros fail-closed.
    /// </summary>
    Task<IReadOnlyList<ElectronicDocumentRetryCandidate>> GetRetryCandidatesAsync(
        CancellationToken ct = default
    );
}

/// <summary>Identidad de un candidato a reintento automático y los datos de su backoff.</summary>
public sealed record ElectronicDocumentRetryCandidate(
    Guid TenantId,
    Guid CompanyId,
    Guid ElectronicDocumentId,
    int RetryCount,
    DateTime? LastAttemptUtc
);

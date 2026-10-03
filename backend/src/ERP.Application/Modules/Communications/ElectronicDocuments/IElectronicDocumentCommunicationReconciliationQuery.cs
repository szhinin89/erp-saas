namespace ERP.Application.Modules.Communications.ElectronicDocuments;

/// <summary>Comprobante autorizado sin comunicación (solo identificadores: cross-tenant).</summary>
public sealed record ElectronicDocumentCommunicationCandidate(
    Guid TenantId,
    Guid CompanyId,
    Guid ElectronicDocumentId,
    DateTime CreatedAtUtc
);

/// <summary>
/// ZH-EDOC-COMMUNICATIONS-01 — fuente durable de lo pendiente para la reconciliación: comprobantes
/// <c>Authorized</c> de una ruta soportada sin fila en <c>communication_outbox</c> para (tenant, empresa,
/// SourceModule, SourceType, SourceId, Purpose). Sin tabla nueva: la outbox es la evidencia (una fila
/// Failed también cuenta — no se reencola un fallo).
/// <para>
/// Sin horizonte de antigüedad (verificación final): un faltante nunca queda fuera solo por viejo. Orden
/// determinístico y global, más antiguos primero: (<c>created_at</c>, <c>id</c>), paginado por keyset.
/// </para>
/// </summary>
public interface IElectronicDocumentCommunicationReconciliationQuery
{
    /// <param name="lastChangeBeforeUtc">Antigüedad mínima: no compite con el evento en curso.</param>
    /// <param name="after">Keyset: devuelve candidatos estrictamente posteriores a este (null = desde el más antiguo).</param>
    /// <param name="excludedCompanyIds">Empresas sin comunicación por política (preferencia desactivada): no consumen el lote.</param>
    Task<IReadOnlyList<ElectronicDocumentCommunicationCandidate>> GetMissingAsync(
        IReadOnlyCollection<ElectronicDocumentCommunicationRoute> routes,
        DateTime lastChangeBeforeUtc,
        ElectronicDocumentCommunicationCandidate? after,
        IReadOnlyCollection<Guid> excludedCompanyIds,
        int limit,
        CancellationToken ct = default
    );
}

using ERP.Domain.Audit;

namespace ERP.Application.Audit;

/// <summary>
/// Consultas comunes sobre una tabla de auditoría de dominio (entidad, usuario, fecha).
/// Cada dominio puede componer lecturas adicionales propias (por producto, cliente,
/// cuenta, documento, etc.) sobre su propio repositorio sin duplicar estos filtros base.
/// </summary>
public interface IAuditReader<TAudit>
    where TAudit : AuditRecordBase
{
    Task<IReadOnlyList<TAudit>> GetByEntityAsync(
        Guid tenantId,
        Guid entityId,
        int take = 20,
        CancellationToken ct = default
    );

    /// <summary>
    /// Última auditoría por cada Id en <paramref name="entityIds"/>, en una sola consulta
    /// batch — evita el patrón N+1 de resolver "última modificación" fila por fila.
    /// </summary>
    Task<IReadOnlyDictionary<Guid, TAudit>> GetLastByEntityIdsAsync(
        Guid tenantId,
        IReadOnlyCollection<Guid> entityIds,
        CancellationToken ct = default
    );

    Task<IReadOnlyList<TAudit>> GetByUserAsync(
        Guid tenantId,
        Guid userId,
        DateTime? fromUtc = null,
        DateTime? toUtc = null,
        int take = 50,
        CancellationToken ct = default
    );
}

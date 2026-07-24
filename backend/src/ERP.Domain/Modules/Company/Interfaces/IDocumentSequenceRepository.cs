using ERP.Domain.Modules.Company.Entities;

namespace ERP.Domain.Modules.Company.Interfaces;

public interface IDocumentSequenceRepository
{
    /// <summary>
    /// Captura y reserva atómicamente el siguiente número secuencial para el punto de emisión y
    /// tipo documental indicados. Internamente utiliza un advisory lock de PostgreSQL y una
    /// transacción explícita, garantizando unicidad incluso bajo alta concurrencia.
    /// Crea el registro de secuencia on-demand si todavía no existe.
    /// </summary>
    /// <returns>El número formateado en 9 dígitos, ej. "000000001".</returns>
    Task<string> CaptureNextAsync(
        Guid tenantId, Guid companyId, Guid emissionPointId,
        string docTypeCode, CancellationToken ct = default);

    /// <summary>
    /// Obtiene el DocumentSequence con bloqueo pesimista (SELECT FOR UPDATE).
    /// Llamar siempre dentro de una transacción activa. Preferir <see cref="CaptureNextAsync"/>.
    /// </summary>
    Task<DocumentSequence?> GetForUpdateAsync(Guid emissionPointId, string docTypeCode, CancellationToken cancellationToken = default);

    Task<bool> ExistsAsync(Guid emissionPointId, string docTypeCode, CancellationToken cancellationToken = default);
    Task AddAsync(DocumentSequence sequence, CancellationToken cancellationToken = default);
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}

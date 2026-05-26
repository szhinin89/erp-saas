using ERP.Domain.Modules.Company.Entities;

namespace ERP.Domain.Modules.Company.Interfaces;

public interface IDocumentSequenceRepository
{
    /// <summary>
    /// Obtiene el DocumentSequence con bloqueo pesimista (SELECT FOR UPDATE).
    /// Llamar siempre dentro de una transacción activa.
    /// </summary>
    Task<DocumentSequence?> GetForUpdateAsync(Guid emissionPointId, string docTypeCode, CancellationToken ct = default);

    Task<bool> ExistsAsync(Guid emissionPointId, string docTypeCode, CancellationToken ct = default);
    Task AddAsync(DocumentSequence sequence, CancellationToken ct = default);
    Task SaveChangesAsync(CancellationToken ct = default);
}

using ERP.Domain.Modules.Company.Entities;

namespace ERP.Domain.Modules.Company.Interfaces;

public interface IEmissionPointRepository
{
    /// <summary>
    /// Devuelve el punto de emisión predeterminado del establecimiento asociado a la sucursal,
    /// incluyendo el establecimiento cargado (para acceder a <c>Establishment.Code</c>).
    /// </summary>
    Task<EmissionPoint?> GetDefaultForBranchAsync(Guid subscriberId, Guid branchId, CancellationToken ct = default);

    /// <summary>
    /// Devuelve el punto de emisión predeterminado del establecimiento principal de la empresa,
    /// incluyendo el establecimiento cargado. Útil cuando no hay BranchId disponible.
    /// </summary>
    Task<EmissionPoint?> GetDefaultForCompanyAsync(Guid subscriberId, Guid companyId, CancellationToken ct = default);

    Task<bool> ExistsAsync(Guid subscriberId, Guid establishmentId, string code, CancellationToken ct = default);
    Task AddAsync(EmissionPoint emissionPoint, CancellationToken ct = default);
    Task SaveChangesAsync(CancellationToken ct = default);
}

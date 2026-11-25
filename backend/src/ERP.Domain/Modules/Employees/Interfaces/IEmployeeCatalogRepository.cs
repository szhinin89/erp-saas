using ERP.Domain.Modules.Employees.Entities;
using ERP.Domain.Modules.Employees.Enums;

namespace ERP.Domain.Modules.Employees.Interfaces;

/// <summary>
/// Persistencia de los tres catálogos laborales (cargo, profesión, especialidad). Todas las
/// consultas aplican tenant y empresa explícitos (observación B).
/// </summary>
/// <summary>Registro de catálogo leído sin seguimiento, con su token de concurrencia.</summary>
public sealed record EmployeeCatalogRow(EmployeeCatalogEntry Entry, uint RowVersion);

public interface IEmployeeCatalogRepository
{
    Task<EmployeeCatalogEntry?> GetByIdAsync(
        EmployeeCatalogType type,
        Guid tenantId,
        Guid companyId,
        Guid id,
        CancellationToken ct = default
    );

    Task<(IReadOnlyList<EmployeeCatalogRow> Items, int Total)> ListAsync(
        EmployeeCatalogType type,
        Guid tenantId,
        Guid companyId,
        bool? isActive,
        string? search,
        int skip,
        int take,
        CancellationToken ct = default
    );

    /// <summary>¿Existe otro registro con el mismo nombre normalizado en la empresa (UX-CAT-01)?</summary>
    Task<bool> ExistsByNormalizedNameAsync(
        EmployeeCatalogType type,
        Guid tenantId,
        Guid companyId,
        string normalizedName,
        Guid? excludeId,
        CancellationToken ct = default
    );

    Task AddAsync(EmployeeCatalogEntry entry, CancellationToken ct = default);

    /// <summary>
    /// Resuelve, para asignarlos, los registros indicados dentro del tenant y la empresa. La clave
    /// ausente significa «inexistente o fuera de ámbito» (indistinguible); el valor indica si está
    /// activo. Los activos quedan bloqueados <c>FOR SHARE</c> hasta el fin de la transacción: una
    /// inactivación concurrente espera, y una ya confirmada se ve como inactiva. Requiere transacción.
    /// </summary>
    Task<IReadOnlyDictionary<Guid, bool>> ResolveForAssignmentAsync(
        EmployeeCatalogType type,
        Guid tenantId,
        Guid companyId,
        IReadOnlyCollection<Guid> ids,
        CancellationToken ct = default
    );

    /// <summary>Token de concurrencia (<c>xmin</c>) vigente de un registro leído con seguimiento.</summary>
    uint GetRowVersion(EmployeeCatalogEntry entry);

    /// <summary>Fija el token recibido del cliente como valor original; la escritura lo comprueba.</summary>
    void SetExpectedRowVersion(EmployeeCatalogEntry entry, uint rowVersion);

    /// <summary>
    /// Persiste los cambios. <c>false</c> si la versión del registro está obsoleta (concurrencia
    /// optimista); en ese caso nada se confirma (ni siquiera la auditoría del cambio).
    /// </summary>
    Task<bool> SaveChangesAsync(CancellationToken ct = default);
}

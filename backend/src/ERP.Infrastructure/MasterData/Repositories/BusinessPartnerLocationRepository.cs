using ERP.Domain.MasterData.Entities;
using ERP.Domain.MasterData.Enums;
using ERP.Domain.MasterData.Interfaces;
using ERP.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ERP.Infrastructure.MasterData.Repositories;

/// <summary>
/// Repositorio del Aggregate Root BusinessPartnerLocation.
/// Scope: ITenantScopedEntity — el global query filter agrega tenant_id en cada query.
/// CompanyId eliminado de todos los métodos (scope cambiado en Fase 4).
///
/// INVARIANTES DEL AR garantizadas en colaboración con repositorio + BD:
///   1. Solo una ubicación primaria activa por BP:
///      - ClearPrimaryAsync() quita el flag antes de SetPrimary()
///      - Índice UNIQUE parcial uq_bpl_primary en BD como segunda línea de defensa
///      - Concurrencia: dos requests simultáneos pueden pasar ClearPrimary y ambos intentar
///        SetPrimary — el índice UNIQUE detiene al segundo y IDatabaseExceptionTranslator
///        convierte la violación en error descriptivo.
///   2. No desactivar ubicación primaria:
///      - BusinessPartnerLocation.Deactivate() lanza si IsPrimary == true (dominio).
///   3. No desactivar si hay contactos activos referenciando la ubicación:
///      - HasActiveContactsAsync() debe llamarse en el handler ANTES de Deactivate().
///      - El FK RESTRICT es la barrera para eliminaciones físicas (soft delete no activa FKs).
///
/// note sobre ClearPrimaryAsync y transaccionalidad:
///   ExecuteUpdateAsync se ejecuta inmediatamente vía UPDATE SQL, fuera del SaveChanges.
///   Si SaveChanges falla después de ClearPrimaryAsync, el BP queda sin ubicación primaria.
///   Esto es aceptable: el estado "sin primaria" es válido y transitorio.
///   El usuario deberá re-ejecutar la operación de set-primary. No hay pérdida de datos.
/// </summary>
public sealed class BusinessPartnerLocationRepository : IBusinessPartnerLocationRepository
{
    private readonly ErpDbContext _db;

    public BusinessPartnerLocationRepository(ErpDbContext db) => _db = db;

    /// <summary>Devuelve entidad tracked — necesaria para Deactivate, Update, SetPrimary.</summary>
    public Task<BusinessPartnerLocation?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => _db.BusinessPartnerLocations
              .FirstOrDefaultAsync(l => l.Id == id, cancellationToken);

    public async Task<IReadOnlyList<BusinessPartnerLocation>> GetByBusinessPartnerAsync(
        Guid  businessPartnerId,
        bool? onlyActive = true,
        CancellationToken cancellationToken = default)
    {
        var q = _db.BusinessPartnerLocations
                   .AsNoTracking()
                   .Where(l => l.BusinessPartnerId == businessPartnerId);

        if (onlyActive.HasValue)
            q = q.Where(l => l.IsActive == onlyActive.Value);

        return await q
            .OrderByDescending(l => l.IsPrimary)
            .ThenBy(l => l.Name)
            .ToListAsync(cancellationToken);
    }

    /// <summary>
    /// Busca ubicaciones que tienen al menos uno de los propósitos indicados (bitmask).
    /// Genera: WHERE (location_purpose & @purpose) = @purpose
    /// EF Core + Npgsql traduce el operador & correctamente a SQL de PostgreSQL.
    /// </summary>
    public async Task<IReadOnlyList<BusinessPartnerLocation>> GetByPurposeAsync(
        Guid            businessPartnerId,
        LocationPurpose purpose,
        CancellationToken cancellationToken = default)
    {
        return await _db.BusinessPartnerLocations
            .AsNoTracking()
            .Where(l => l.BusinessPartnerId == businessPartnerId
                     && l.IsActive
                     && (l.Purpose & purpose) == purpose)
            .OrderByDescending(l => l.IsPrimary)
            .ThenBy(l => l.Name)
            .ToListAsync(cancellationToken);
    }

    public Task<BusinessPartnerLocation?> GetPrimaryAsync(
        Guid businessPartnerId,
        CancellationToken cancellationToken = default)
        => _db.BusinessPartnerLocations
              .AsNoTracking()
              .FirstOrDefaultAsync(l =>
                  l.BusinessPartnerId == businessPartnerId &&
                  l.IsPrimary && l.IsActive, cancellationToken);

    /// <summary>
    /// Verifica si algún contacto activo del tenant referencia esta ubicación.
    /// DEBE llamarse en DeactivateBpLocationHandler ANTES de llamar Deactivate().
    /// El global query filter en BusinessPartnerContacts filtra por tenant_id.
    /// </summary>
    public Task<bool> HasActiveContactsAsync(Guid locationId, CancellationToken cancellationToken = default)
        => _db.BusinessPartnerContacts
              .AnyAsync(c => c.LocationId == locationId && c.IsActive, cancellationToken);

    /// <summary>
    /// Quita IsPrimary de TODAS las ubicaciones del BP (bulk UPDATE directo a BD).
    /// Llamar ANTES de SetPrimary() en el handler para mantener la invariante.
    /// El global query filter añade WHERE tenant_id = @tenant automáticamente.
    /// </summary>
    public async Task ClearPrimaryAsync(Guid businessPartnerId, CancellationToken cancellationToken = default)
    {
        await _db.BusinessPartnerLocations
            .Where(l => l.BusinessPartnerId == businessPartnerId && l.IsPrimary)
            .ExecuteUpdateAsync(s => s.SetProperty(l => l.IsPrimary, false), cancellationToken);
    }

    public Task AddAsync(BusinessPartnerLocation location, CancellationToken cancellationToken = default)
        => _db.BusinessPartnerLocations.AddAsync(location, cancellationToken).AsTask();

    public Task SaveChangesAsync(CancellationToken cancellationToken = default)
        => _db.SaveChangesAsync(cancellationToken);
}

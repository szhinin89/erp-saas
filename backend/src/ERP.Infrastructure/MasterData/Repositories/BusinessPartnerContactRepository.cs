using ERP.Domain.MasterData.Entities;
using ERP.Domain.MasterData.Enums;
using ERP.Domain.MasterData.Interfaces;
using ERP.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ERP.Infrastructure.MasterData.Repositories;

/// <summary>
/// Repositorio del Aggregate Root BusinessPartnerContact.
/// Scope: ITenantScopedEntity — global query filter aplica tenant_id en cada query.
/// CompanyId eliminado de todos los métodos (scope cambiado en Fase 4).
///
/// INVARIANTES DEL AR garantizadas en colaboración con repositorio + BD:
///   1. Solo un contacto primario activo por BP:
///      - ClearPrimaryAsync() + SetPrimary() + índice UNIQUE parcial uq_bpc_primary.
///      - Misma semántica de concurrencia que BusinessPartnerLocationRepository.
///   2. No marcar primario a un contacto inactivo:
///      - BusinessPartnerContact.SetPrimary() lanza si !IsActive (dominio).
///   3. ContactRole = Other requiere OtherDescription:
///      - BusinessPartnerContact.Create() y Update() validan en dominio.
///
/// ContactInfo (VO): phone, mobile, email son propiedades del VO embebido.
/// EF Core accede via Contact.Phone, Contact.Mobile, Contact.Email en queries.
///
/// note sobre ClearPrimaryAsync: misma semántica transaccional que LocationRepository.
/// </summary>
public sealed class BusinessPartnerContactRepository : IBusinessPartnerContactRepository
{
    private readonly ErpDbContext _db;

    public BusinessPartnerContactRepository(ErpDbContext db) => _db = db;

    /// <summary>Devuelve entidad tracked — necesaria para Update, Deactivate, SetPrimary.</summary>
    public Task<BusinessPartnerContact?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => _db.BusinessPartnerContacts
              .FirstOrDefaultAsync(c => c.Id == id, cancellationToken);

    public async Task<IReadOnlyList<BusinessPartnerContact>> GetByBusinessPartnerAsync(
        Guid businessPartnerId,
        bool? onlyActive = true,
        CancellationToken cancellationToken = default)
    {
        var q = _db.BusinessPartnerContacts
                   .AsNoTracking()
                   .Where(c => c.BusinessPartnerId == businessPartnerId);

        if (onlyActive.HasValue)
            q = q.Where(c => c.IsActive == onlyActive.Value);

        return await q
            .OrderByDescending(c => c.IsPrimary)
            .ThenBy(c => c.FirstName)
            .ToListAsync(cancellationToken);
    }

    /// <summary>
    /// Busca contactos por rol (ej: todos los contactos Legal, todos los de Facturación).
    /// Usa el índice ix_bpc_subscriber_bp_role para eficiencia.
    /// </summary>
    public async Task<IReadOnlyList<BusinessPartnerContact>> GetByRoleAsync(
        Guid businessPartnerId,
        ContactRole role,
        CancellationToken cancellationToken = default)
    {
        return await _db.BusinessPartnerContacts
            .AsNoTracking()
            .Where(c => c.BusinessPartnerId == businessPartnerId
                     && c.Role == role
                     && c.IsActive)
            .OrderByDescending(c => c.IsPrimary)
            .ThenBy(c => c.FirstName)
            .ToListAsync(cancellationToken);
    }

    public Task<BusinessPartnerContact?> GetPrimaryAsync(
        Guid businessPartnerId,
        CancellationToken cancellationToken = default)
        => _db.BusinessPartnerContacts
              .AsNoTracking()
              .FirstOrDefaultAsync(c =>
                  c.BusinessPartnerId == businessPartnerId &&
                  c.IsPrimary && c.IsActive, cancellationToken);

    /// <summary>
    /// Quita IsPrimary de TODOS los contactos del BP (bulk UPDATE directo a BD).
    /// Llamar ANTES de SetPrimary() en el handler.
    /// El global query filter añade WHERE tenant_id = @tenant automáticamente.
    /// </summary>
    public async Task ClearPrimaryAsync(Guid businessPartnerId, CancellationToken cancellationToken = default)
    {
        await _db.BusinessPartnerContacts
            .Where(c => c.BusinessPartnerId == businessPartnerId && c.IsPrimary)
            .ExecuteUpdateAsync(s => s.SetProperty(c => c.IsPrimary, false), cancellationToken);
    }

    public Task AddAsync(BusinessPartnerContact contact, CancellationToken cancellationToken = default)
        => _db.BusinessPartnerContacts.AddAsync(contact, cancellationToken).AsTask();

    public Task SaveChangesAsync(CancellationToken cancellationToken = default)
        => _db.SaveChangesAsync(cancellationToken);
}

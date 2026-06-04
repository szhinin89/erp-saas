using ERP.Domain.MasterData.Entities;
using ERP.Domain.MasterData.Enums;
using ERP.Domain.MasterData.Interfaces;
using ERP.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ERP.Infrastructure.MasterData.Repositories;

/// <summary>
/// Repositorio de identidad fiscal. Solo accede a master_business_partners.
/// Los roles (master_bp_roles) son consultados indirectamente via EXISTS en SearchAsync.
///
/// IMPORTANTE — invariantes del Aggregate Root:
///   El AR BusinessPartner controla sus propias invariantes mediante métodos con validación
///   (Create, UpdateProfile, Deactivate). El repositorio NO valida nada de negocio.
///   La unicidad de identificación está garantizada por:
///     1. Índice UNIQUE incondicional en BD (uq_mbp_identification)
///     2. IDatabaseExceptionTranslator convierte la violación en error de dominio descriptivo
///   El check previo GetByIdentificationAsync en el handler es una soft-check por UX,
///   no la barrera de seguridad (la BD lo es).
///
/// Unit of Work: ErpDbContext inyectado como Scoped. SaveChangesAsync() compromete
///   todas las entidades tracked en el contexto compartido de la request.
/// </summary>
public sealed class BusinessPartnerRepository : IBusinessPartnerRepository
{
    private readonly ErpDbContext _db;

    public BusinessPartnerRepository(ErpDbContext db) => _db = db;

    /// <summary>Devuelve entidad tracked — usar en command handlers (necesita tracking para writes).</summary>
    public Task<BusinessPartner?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => _db.BusinessPartners
              .FirstOrDefaultAsync(x => x.Id == id, ct);

    public Task<BusinessPartner?> GetByIdentificationAsync(
        string identificationType,
        string identificationNumber,
        CancellationToken ct = default)
        => _db.BusinessPartners
              .AsNoTracking()
              .FirstOrDefaultAsync(x =>
                  x.Identification.Type   == identificationType &&
                  x.Identification.Number == identificationNumber, ct);

    /// <summary>
    /// Búsqueda paginada. Filtra por roles via correlated EXISTS (JOIN lógico a master_bp_roles).
    /// Reemplaza el patrón obsoleto isCustomer/isSupplier con RoleType[] extensible.
    /// </summary>
    public async Task<IReadOnlyList<BusinessPartner>> SearchAsync(
        string?     query    = null,
        bool?       isActive = true,
        RoleType[]? roles    = null,
        int         skip     = 0,
        int         take     = 50,
        CancellationToken ct = default)
    {
        return await BuildQuery(query, isActive, roles)
            .OrderBy(x => x.Name.LegalName)
            .Skip(skip)
            .Take(Math.Clamp(take, 1, 200))
            .ToListAsync(ct);
    }

    public Task<int> CountAsync(
        string?     query    = null,
        bool?       isActive = true,
        RoleType[]? roles    = null,
        CancellationToken ct = default)
        => BuildQuery(query, isActive, roles).CountAsync(ct);

    public async Task AddAsync(BusinessPartner businessPartner, CancellationToken ct = default)
        => await _db.BusinessPartners.AddAsync(businessPartner, ct);

    /// <summary>
    /// Compromete todos los cambios tracked en el DbContext compartido.
    /// Los domain events del aggregate son publicados via Outbox por ErpDbContext.SaveChangesAsync.
    /// </summary>
    public Task SaveChangesAsync(CancellationToken ct = default)
        => _db.SaveChangesAsync(ct);

    // ── Query builder ─────────────────────────────────────────────────────────

    private IQueryable<BusinessPartner> BuildQuery(string? query, bool? isActive, RoleType[]? roles)
    {
        var q = _db.BusinessPartners.AsNoTracking();

        if (isActive.HasValue)
            q = q.Where(x => x.IsActive == isActive.Value);

        // Filtro por roles: EXISTS en master_bp_roles.
        // El global query filter de BusinessPartnerRoles agrega subscriber_id automáticamente.
        // Genera: WHERE EXISTS (SELECT 1 FROM master_bp_roles r WHERE r.bp_id = bp.id AND r.role_type IN (...) AND r.is_active)
        if (roles is { Length: > 0 })
            q = q.Where(x => _db.BusinessPartnerRoles
                                 .Any(r => r.BusinessPartnerId == x.Id
                                        && roles.Contains(r.RoleType)
                                        && r.IsActive));

        if (!string.IsNullOrWhiteSpace(query))
        {
            var lower = query.Trim().ToLowerInvariant();
            q = q.Where(x =>
                x.Name.LegalName.ToLower().Contains(lower) ||
                (x.Name.TradeName != null && x.Name.TradeName.ToLower().Contains(lower)) ||
                x.Identification.Number.Contains(lower));
        }

        return q;
    }
}

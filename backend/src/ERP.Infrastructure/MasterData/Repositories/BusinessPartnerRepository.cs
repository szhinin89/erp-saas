using ERP.Domain.MasterData.Entities;
using ERP.Domain.MasterData.Enums;
using ERP.Domain.MasterData.Interfaces;
using ERP.Domain.MasterData.Models;
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
///   El pre-check ExistsByIdentificationAsync en el handler es una validación por UX
///   (evita el roundtrip de excepción en el caso común), no la barrera de seguridad
///   (la BD lo es) — el catch de UniqueViolation permanece como protección ante
///   condiciones de carrera.
///
/// Unit of Work: ErpDbContext inyectado como Scoped. SaveChangesAsync() compromete
///   todas las entidades tracked en el contexto compartido de la request.
/// </summary>
public sealed class BusinessPartnerRepository : IBusinessPartnerRepository
{
    private readonly ErpDbContext _db;

    public BusinessPartnerRepository(ErpDbContext db) => _db = db;

    /// <summary>Devuelve entidad tracked — usar en command handlers (necesita tracking para writes).</summary>
    public Task<BusinessPartner?> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default
    ) => _db.BusinessPartners.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

    public Task<BusinessPartner?> GetByIdentificationAsync(
        string identificationType,
        string identificationNumber,
        CancellationToken cancellationToken = default
    ) =>
        _db
            .BusinessPartners.AsNoTracking()
            .FirstOrDefaultAsync(
                x =>
                    x.Identification.Type == identificationType
                    && x.Identification.Number == identificationNumber,
                cancellationToken
            );

    public Task<bool> ExistsByIdentificationAsync(
        string identificationType,
        string identificationNumber,
        Guid? excludeId = null,
        CancellationToken cancellationToken = default
    )
    {
        var query = _db
            .BusinessPartners.AsNoTracking()
            .Where(x =>
                x.Identification.Type == identificationType
                && x.Identification.Number == identificationNumber
            );

        if (excludeId.HasValue)
            query = query.Where(x => x.Id != excludeId.Value);

        return query.AnyAsync(cancellationToken);
    }

    /// <summary>
    /// Búsqueda paginada. Filtra por roles via correlated EXISTS (JOIN lógico a master_bp_roles).
    /// Reemplaza el patrón obsoleto isCustomer/isSupplier con RoleType[] extensible.
    /// </summary>
    public async Task<IReadOnlyList<BusinessPartner>> SearchAsync(
        string? query = null,
        bool? isActive = true,
        RoleType[]? roles = null,
        int skip = 0,
        int take = 50,
        CancellationToken cancellationToken = default
    )
    {
        return await BuildQuery(query, isActive, roles)
            .OrderBy(x => x.Name.LegalName)
            .Skip(skip)
            .Take(Math.Clamp(take, 1, 200))
            .ToListAsync(cancellationToken);
    }

    public Task<int> CountAsync(
        string? query = null,
        bool? isActive = true,
        RoleType[]? roles = null,
        CancellationToken cancellationToken = default
    ) => BuildQuery(query, isActive, roles).CountAsync(cancellationToken);

    public async Task<IReadOnlyDictionary<Guid, string>> GetNamesByIdsAsync(
        IEnumerable<Guid> ids,
        CancellationToken cancellationToken = default
    )
    {
        var idList = ids.Distinct().ToList();
        if (idList.Count == 0)
            return new Dictionary<Guid, string>();

        return await _db
            .BusinessPartners.AsNoTracking()
            .Where(x => idList.Contains(x.Id))
            .ToDictionaryAsync(x => x.Id, x => x.Name.LegalName, cancellationToken);
    }

    public async Task<IReadOnlyDictionary<Guid, BusinessPartnerDisplayInfo>> GetDisplayInfoByIdsAsync(
        IEnumerable<Guid> ids,
        CancellationToken cancellationToken = default
    )
    {
        var idList = ids.Distinct().ToList();
        if (idList.Count == 0)
            return new Dictionary<Guid, BusinessPartnerDisplayInfo>();

        var rows = await _db
            .BusinessPartners.AsNoTracking()
            .Where(x => idList.Contains(x.Id))
            .Select(x => new BusinessPartnerDisplayInfo(
                x.Id,
                x.Name.TradeName,
                x.Name.LegalName,
                x.Identification.Number
            ))
            .ToListAsync(cancellationToken);

        return rows.ToDictionary(x => x.Id);
    }

    public async Task AddAsync(
        BusinessPartner businessPartner,
        CancellationToken cancellationToken = default
    ) => await _db.BusinessPartners.AddAsync(businessPartner, cancellationToken);

    /// <summary>
    /// Compromete todos los cambios tracked en el DbContext compartido.
    /// Los domain events del aggregate son publicados via Outbox por ErpDbContext.SaveChangesAsync.
    /// </summary>
    public Task SaveChangesAsync(CancellationToken cancellationToken = default) =>
        _db.SaveChangesAsync(cancellationToken);

    // ── Query builder ─────────────────────────────────────────────────────────

    private IQueryable<BusinessPartner> BuildQuery(string? query, bool? isActive, RoleType[]? roles)
    {
        var q = _db.BusinessPartners.AsNoTracking();

        if (isActive.HasValue)
            q = q.Where(x => x.IsActive == isActive.Value);

        // Filtro por roles: EXISTS en master_bp_roles.
        // El global query filter de BusinessPartnerRoles agrega tenant_id automáticamente.
        // Genera: WHERE EXISTS (SELECT 1 FROM master_bp_roles r WHERE r.bp_id = bp.id AND r.role_type IN (...) AND r.is_active)
        if (roles is { Length: > 0 })
            q = q.Where(x =>
                _db.BusinessPartnerRoles.Any(r =>
                    r.BusinessPartnerId == x.Id && roles.Contains(r.RoleType) && r.IsActive
                )
            );

        if (!string.IsNullOrWhiteSpace(query))
        {
            var pattern = $"%{query.Trim()}%";
            q = q.Where(x =>
                EF.Functions.ILike(x.Name.LegalName, pattern)
                || (x.Name.TradeName != null && EF.Functions.ILike(x.Name.TradeName, pattern))
                || EF.Functions.ILike(x.Identification.Number, pattern)
            );
        }

        return q;
    }
}

using ERP.Domain.MasterData.Entities;
using ERP.Domain.MasterData.Enums;
using ERP.Domain.MasterData.Interfaces;
using ERP.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ERP.Infrastructure.MasterData.Repositories;

/// <summary>
/// Repositorio del Aggregate Root BusinessPartnerRole.
/// Accede a master_bp_roles (+ owned split tables master_bp_supplier_configs, master_bp_carrier_configs).
///
/// INVARIANTES DEL AR que el repositorio garantiza en colaboración con la BD:
///   1. Unicidad (tenant_id, business_partner_id, role_type) — índice UNIQUE en BD.
///      La violación se convierte en error de dominio por IDatabaseExceptionTranslator.
///   2. Coherencia tipo↔config — garantizada en BusinessPartnerRole.Create() (dominio).
///      El repositorio no re-valida esto.
///
/// UPSERT SEMÁNTICO (ADR-BP-12):
///   GetByTypeAsync devuelve el rol INDEPENDIENTEMENTE de is_active.
///   El handler de AssignRole usa esto para decidir: si existe revocado → Reactivate(),
///   si no existe → Create(). Nunca insertar duplicados.
///
/// OWNED TYPES (SupplierConfig, CarrierConfig):
///   EF Core incluye automáticamente los owned types en split tables al cargar el owner.
///   No se necesita Include() explícito.
///
/// Unit of Work: ErpDbContext Scoped compartido. SaveChangesAsync() despacha
///   domain events via Outbox y compromete todo el unit of work de la request.
/// </summary>
public sealed class BusinessPartnerRoleRepository : IBusinessPartnerRoleRepository
{
    private readonly ErpDbContext _db;

    public BusinessPartnerRoleRepository(ErpDbContext db) => _db = db;

    /// <summary>
    /// Devuelve entidad tracked con sus configs (OwnsOne).
    /// Usar en command handlers que modifican el rol.
    /// </summary>
    public Task<BusinessPartnerRole?> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default
    ) => _db.BusinessPartnerRoles.FirstOrDefaultAsync(r => r.Id == id, cancellationToken);

    /// <summary>
    /// Busca un rol por tipo para el BP dado, activo O revocado.
    /// Clave para el UPSERT semántico de AssignRole. Ver ADR-BP-12.
    /// Devuelve entidad tracked para permitir Reactivate() o actualización de config.
    /// </summary>
    public Task<BusinessPartnerRole?> GetByTypeAsync(
        Guid businessPartnerId,
        RoleType roleType,
        CancellationToken cancellationToken = default
    ) =>
        _db.BusinessPartnerRoles.FirstOrDefaultAsync(
            r => r.BusinessPartnerId == businessPartnerId && r.RoleType == roleType,
            cancellationToken
        );

    /// <summary>Todos los roles de un BP (activos + revocados según filtro).</summary>
    public async Task<IReadOnlyList<BusinessPartnerRole>> GetByBusinessPartnerAsync(
        Guid businessPartnerId,
        bool? onlyActive = true,
        CancellationToken cancellationToken = default
    )
    {
        var q = _db
            .BusinessPartnerRoles.AsNoTracking()
            .Where(r => r.BusinessPartnerId == businessPartnerId);

        if (onlyActive.HasValue)
            q = q.Where(r => r.IsActive == onlyActive.Value);

        return await q.OrderBy(r => r.RoleType).ToListAsync(cancellationToken);
    }

    /// <summary>
    /// Verificación rápida de si el BP tiene un rol activo de un tipo dado.
    /// Usado en handlers para validar prerrequisitos antes de crear documentos.
    /// </summary>
    public Task<bool> HasActiveRoleAsync(
        Guid businessPartnerId,
        RoleType roleType,
        CancellationToken cancellationToken = default
    ) =>
        _db.BusinessPartnerRoles.AnyAsync(
            r => r.BusinessPartnerId == businessPartnerId && r.RoleType == roleType && r.IsActive,
            cancellationToken
        );

    public async Task AddAsync(
        BusinessPartnerRole role,
        CancellationToken cancellationToken = default
    ) => await _db.BusinessPartnerRoles.AddAsync(role, cancellationToken);

    /// <summary>
    /// SaveChanges despacha todos los domain events del rol via Outbox.
    /// Los eventos BusinessPartnerRoleAssignedEvent, RevokedEvent, etc. se publican aquí.
    /// </summary>
    public Task SaveChangesAsync(CancellationToken cancellationToken = default) =>
        _db.SaveChangesAsync(cancellationToken);
}

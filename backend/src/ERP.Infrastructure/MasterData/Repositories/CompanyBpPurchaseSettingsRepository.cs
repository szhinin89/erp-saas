using ERP.Domain.MasterData.Entities;
using ERP.Domain.MasterData.Interfaces;
using ERP.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ERP.Infrastructure.MasterData.Repositories;

/// <summary>
/// Repositorio del Aggregate Root CompanyBpPurchaseSettings — ADR-033, Fase 3.
/// Scope: ICompanyScopedEntity + ITenantScopedEntity.
/// El global query filter aplica WHERE tenant_id = @tenant AND company_id = @company en cada query.
/// Filtro FAIL-CLOSED en ambas dimensiones — sin company context → 0 filas.
///
/// PATRÓN UPSERT (Fase 3d):
///   El handler de upsert llama GetByBusinessPartnerAsync.
///   Si existe → SetPaymentTerm(); si no existe → Create() + AddAsync().
///   La unicidad (tenant_id, company_id, business_partner_id) está garantizada
///   por el índice UNIQUE uq_cbps_company_bp en BD.
/// </summary>
public sealed class CompanyBpPurchaseSettingsRepository : ICompanyBpPurchaseSettingsRepository
{
    private readonly ErpDbContext _db;

    public CompanyBpPurchaseSettingsRepository(ErpDbContext db) => _db = db;

    /// <summary>
    /// Devuelve entidad tracked — necesaria para SetPaymentTerm() del AR.
    /// El global query filter restringe al (tenant, company) activo en el request.
    /// </summary>
    public Task<CompanyBpPurchaseSettings?> GetByBusinessPartnerAsync(
        Guid businessPartnerId,
        CancellationToken cancellationToken = default
    ) =>
        _db.CompanyBpPurchaseSettings.FirstOrDefaultAsync(
            s => s.BusinessPartnerId == businessPartnerId,
            cancellationToken
        );

    public async Task AddAsync(
        CompanyBpPurchaseSettings settings,
        CancellationToken cancellationToken = default
    ) => await _db.CompanyBpPurchaseSettings.AddAsync(settings, cancellationToken);

    public Task SaveChangesAsync(CancellationToken cancellationToken = default) =>
        _db.SaveChangesAsync(cancellationToken);
}

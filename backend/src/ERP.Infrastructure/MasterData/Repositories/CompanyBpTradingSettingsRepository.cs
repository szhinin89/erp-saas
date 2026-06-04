using ERP.Domain.MasterData.Entities;
using ERP.Domain.MasterData.Interfaces;
using ERP.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ERP.Infrastructure.MasterData.Repositories;

/// <summary>
/// Repositorio del Aggregate Root CompanyBpTradingSettings.
/// Scope: ICompanyScopedEntity + ISubscriberScopedEntity.
/// El global query filter aplica WHERE subscriber_id = @sub AND company_id = @company en cada query.
/// Filtro FAIL-CLOSED en ambas dimensiones — sin company context → 0 filas.
///
/// PATRÓN UPSERT:
///   El handler de UpsertCompanyBpTradingSettings llama GetByBusinessPartnerAsync.
///   Si existe → Update(); si no existe → Create() + AddAsync().
///   La unicidad (subscriber_id, company_id, business_partner_id) está garantizada
///   por el índice UNIQUE uq_cbts_company_bp en BD.
///
/// INVARIANTES DEL AR garantizadas por el dominio:
///   1. CreditLimit >= 0, PaymentDays >= 0 — CompanyBpTradingSettings.ApplyTradingTerms()
///   2. IsBlocked=true → BlockedReason/At/By obligatorios — CompanyBpTradingSettings.Block()
///      Respaldado por CHECK constraint chk_cbts_block_consistency en BD.
///   3. Unblock limpia los tres campos de audit — CompanyBpTradingSettings.Unblock()
/// </summary>
public sealed class CompanyBpTradingSettingsRepository : ICompanyBpTradingSettingsRepository
{
    private readonly ErpDbContext _db;

    public CompanyBpTradingSettingsRepository(ErpDbContext db) => _db = db;

    /// <summary>
    /// Devuelve entidad tracked — necesaria para Update(), Block(), Unblock() del AR.
    /// El global query filter restringe al (subscriber, company) activo en el request.
    /// </summary>
    public Task<CompanyBpTradingSettings?> GetByBusinessPartnerAsync(
        Guid businessPartnerId,
        CancellationToken ct = default)
        => _db.CompanyBpTradingSettings
              .FirstOrDefaultAsync(s => s.BusinessPartnerId == businessPartnerId, ct);

    /// <summary>
    /// Todos los BPs bloqueados en la empresa activa.
    /// Usa el índice parcial ix_cbts_blocked para eficiencia.
    /// El global query filter restringe al (subscriber, company) activo.
    /// </summary>
    public async Task<IReadOnlyList<CompanyBpTradingSettings>> GetBlockedAsync(
        CancellationToken ct = default)
    {
        return await _db.CompanyBpTradingSettings
            .AsNoTracking()
            .Where(s => s.IsBlocked)
            .OrderBy(s => s.BlockedAt)
            .ToListAsync(ct);
    }

    public async Task AddAsync(CompanyBpTradingSettings settings, CancellationToken ct = default)
        => await _db.CompanyBpTradingSettings.AddAsync(settings, ct);

    /// <summary>
    /// SaveChanges despacha domain events (BusinessPartnerBlockedEvent, etc.) via Outbox.
    /// </summary>
    public Task SaveChangesAsync(CancellationToken ct = default)
        => _db.SaveChangesAsync(ct);
}

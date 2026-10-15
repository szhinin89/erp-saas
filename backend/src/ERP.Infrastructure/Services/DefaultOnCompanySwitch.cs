using ERP.Domain.Configuration.Entities;
using ERP.Domain.Configuration.Enums;
using ERP.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ERP.Infrastructure.Services;

/// <summary>
/// Forma SQL de <see cref="OperationalPreferencesResolver"/> para interruptores booleanos de empresa
/// con default <c>true</c> (p. ej. <c>electronic_documents.auto_retry_enabled</c>,
/// <c>electronic_documents.email_on_authorization</c>): solo un valor persistido que
/// <c>bool.TryParse</c> lee como <c>false</c> lo apaga; ausente, vacío o corrupto conserva el
/// default. Única semántica para los filtros de background previos al lote (ADR-043).
/// </summary>
public static class DefaultOnCompanySwitch
{
    /// <summary>Valor normalizado (<c>lower(trim(value))</c>) que apaga el interruptor.</summary>
    public const string DisabledValue = "false";

    /// <summary>Settings de scope empresa que apagan <paramref name="key"/>; correlacionar por tenant y empresa.</summary>
    public static IQueryable<OrgSetting> Disabling(ErpDbContext db, string key) =>
        db.OrgSettings.AsPlatformQuery()
            .Where(s =>
                s.Scope == OrgScope.Company
                && s.ScopeId == s.CompanyId
                && s.Key == key
                && s.Value != null
                // ILIKE sin comodines = igualdad sin mayúsculas: lower(trim(value)) = 'false'.
                && EF.Functions.ILike(s.Value.Trim(), DisabledValue)
            );
}

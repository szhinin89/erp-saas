using ERP.Domain.Configuration.Constants;
using ERP.Domain.Configuration.Entities;
using ERP.Domain.Configuration.Enums;
using ERP.Infrastructure.Persistence;
using ERP.Infrastructure.Persistence.Repositories.Configuration;
using ERP.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;

namespace ERP.Infrastructure.Tests.TestData;

/// <summary>
/// SRI-TOGGLE-02 — siembra el interruptor <c>electronic_documents.emission_enabled</c> como lo deja el
/// guardado real (fila org_settings), con la fecha de habilitación controlada (sin historial auditado,
/// la fecha efectiva es la escritura de la fila).
/// </summary>
public static class ElectronicEmissionTestSupport
{
    public static async Task SetAsync(
        ErpDbContext db,
        Guid tenantId,
        Guid companyId,
        string value,
        DateTime writtenAtUtc,
        Guid userId
    )
    {
        db.OrgSettings.Add(
            OrgSetting.Create(
                tenantId,
                companyId,
                OrgScope.Company,
                companyId,
                OrgSettingKeys.ElectronicDocuments.EmissionEnabled,
                value,
                SettingDataType.Bool,
                userId
            )
        );
        await db.SaveChangesAsync();
        await db.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE org_settings SET created_at = {writtenAtUtc} WHERE tenant_id = {tenantId} AND company_id = {companyId} AND key = {OrgSettingKeys.ElectronicDocuments.EmissionEnabled}"
        );
    }

    /// <summary>
    /// SRI-TOGGLE-03 — escritura por la ruta de producción (OrgSettingsRepository + ConfigurationChangeLogger):
    /// el valor y su registro auditado viajan en el mismo SaveChanges.
    /// </summary>
    public static async Task SetAuditedAsync(
        ErpDbContext db,
        Guid tenantId,
        Guid companyId,
        bool enabled,
        Guid userId
    )
    {
        // Mismo orden que UpdateOperationalPreferencesCommandHandler (SRI-TOGGLE-04): transacción, lock
        // exclusivo de la empresa, upsert auditado, SaveChanges, commit.
        await using var transaction = await db.Database.BeginTransactionAsync();
        await new ElectronicEmissionSwitch(db).LockForChangeAsync(tenantId, companyId);
        await new OrgSettingsRepository(db, new ConfigurationChangeLogger(db)).UpsertAsync(
            OrgSetting.Create(
                tenantId,
                companyId,
                OrgScope.Company,
                companyId,
                OrgSettingKeys.ElectronicDocuments.EmissionEnabled,
                enabled ? "True" : "False",
                SettingDataType.Bool,
                userId
            )
        );
        await db.SaveChangesAsync();
        await transaction.CommitAsync();
    }

    /// <summary>Habilitada desde hace un día: los documentos sembrados en el test son posteriores.</summary>
    public static Task EnableSinceYesterdayAsync(
        ErpDbContext db,
        Guid tenantId,
        Guid companyId,
        Guid userId
    ) => SetAsync(db, tenantId, companyId, "True", DateTime.UtcNow.AddDays(-1), userId);
}

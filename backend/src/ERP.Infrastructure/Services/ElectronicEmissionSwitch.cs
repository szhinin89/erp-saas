using ERP.Application.Modules.ElectronicDocuments.Services;
using ERP.Domain.Configuration.Constants;
using ERP.Domain.Configuration.Enums;
using ERP.Domain.Modules.ElectronicDocuments.Entities;
using ERP.Domain.Modules.ElectronicDocuments.Enums;
using ERP.Domain.Modules.Retentions;
using ERP.Domain.Modules.Retentions.Entities;
using ERP.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ERP.Infrastructure.Services;

/// <summary>Empresa con la emisión electrónica habilitada y el instante de su última habilitación.</summary>
public sealed class ElectronicEmissionEnabledCompany
{
    public Guid TenantId { get; init; }
    public Guid CompanyId { get; init; }
    public DateTime EnabledSinceUtc { get; init; }
}

/// <summary>Instante persistido de negocio de un origen de comprobante electrónico.</summary>
public sealed class ElectronicDocumentSourceActivity
{
    public Guid TenantId { get; init; }
    public Guid CompanyId { get; init; }
    public string SourceModule { get; init; } = null!;
    public ElectronicDocumentType DocumentType { get; init; }
    public Guid SourceEntityId { get; init; }
    public DateTime? ActivityUtc { get; init; }
}

/// <summary>
/// SRI-TOGGLE-02/04 — forma SQL única del interruptor <c>electronic_documents.emission_enabled</c>
/// (default OFF, al revés que <see cref="DefaultOnCompanySwitch"/>): solo un valor persistido que se lee
/// como <c>true</c> la habilita; ausente, vacío o corrupto = deshabilitada. La usan el servicio del
/// issuer y el filtro del job de reintentos (ADR-043), para que ambos decidan igual.
/// </summary>
public static class ElectronicEmissionSwitchQuery
{
    private const string Key = OrgSettingKeys.ElectronicDocuments.EmissionEnabled;
    private const string EnabledValue = "true";
    private const string SalesSourceModule = "Sales";

    // Valores que escribe OrgSettingsRepository.UpsertAsync para un cambio de org_settings.
    private const string AuditedEntityType = "OrgSetting";
    private const string AuditedField = "Value";

    /// <summary>
    /// Una fila por empresa habilitada. <c>EnabledSinceUtc</c> (fecha de corte de la reanudación explícita):
    /// el ÚLTIMO registro auditado de esta key/empresa/scope (ConfigurationChangeLog, append-only, misma
    /// transacción que el valor), solo si es coherente con el valor vigente (dice <c>true</c>). Sin historial,
    /// o con un historial que no explica el valor vigente (fila escrita fuera de la ruta auditada), el corte
    /// es la última escritura de la fila: nunca anterior a la habilitación real (fail-closed).
    /// </summary>
    public static IQueryable<ElectronicEmissionEnabledCompany> Enabled(ErpDbContext db) =>
        db.OrgSettings.AsPlatformQuery()
            .Where(s =>
                s.Scope == OrgScope.Company
                && s.ScopeId == s.CompanyId
                && s.Key == Key
                && s.Value != null
                // ILIKE sin comodines = igualdad sin mayúsculas: lower(trim(value)) = 'true'.
                && EF.Functions.ILike(s.Value.Trim(), EnabledValue)
            )
            .Select(s => new
            {
                Setting = s,
                Latest = db.ConfigurationChangeLogs.AsPlatformQuery()
                    .Where(l =>
                        l.TenantId == s.TenantId
                        && l.CompanyId == s.CompanyId
                        && l.EntityType == AuditedEntityType
                        && l.FieldName == AuditedField
                        && l.Scope == OrgScope.Company
                        && l.ScopeId == s.CompanyId
                        && l.Key == Key
                    )
                    .OrderByDescending(l => l.ChangedAtUtc)
                    .ThenByDescending(l => l.Id)
                    .Select(l => new { l.NewValue, l.ChangedAtUtc })
                    .FirstOrDefault(),
            })
            .Select(x => new ElectronicEmissionEnabledCompany
            {
                TenantId = x.Setting.TenantId,
                CompanyId = x.Setting.CompanyId,
                EnabledSinceUtc =
                    x.Latest != null
                    && x.Latest.NewValue != null
                    && EF.Functions.ILike(x.Latest.NewValue.Trim(), EnabledValue)
                        ? x.Latest.ChangedAtUtc
                        : x.Setting.UpdatedAt ?? x.Setting.CreatedAt,
            });

    /// <summary>
    /// SRI-TOGGLE-04 — instante de negocio de cada origen conocido, desde datos ya persistidos: el
    /// <c>UpdatedAt</c> que fija el dominio en <c>SalesInvoice.Authorize</c>, <c>SalesReturn.Authorize</c> y
    /// <c>RetentionDocument.Issue</c>; después solo lo mueve <c>Cancel</c>, que deja el origen no procesable
    /// (lo rechaza el guard bajo su lock). Un origen sin ese instante (u otro módulo) no tiene fila: un
    /// proceso automático no lo transmite.
    /// </summary>
    public static IQueryable<ElectronicDocumentSourceActivity> SourceActivity(ErpDbContext db) =>
        db.SalesInvoices.AsPlatformQuery()
            .Select(i => new ElectronicDocumentSourceActivity
            {
                TenantId = i.TenantId,
                CompanyId = i.CompanyId,
                SourceModule = SalesSourceModule,
                DocumentType = ElectronicDocumentType.Invoice,
                SourceEntityId = i.Id,
                ActivityUtc = i.UpdatedAt,
            })
            .Concat(
                db.SalesReturns.AsPlatformQuery()
                    .Select(r => new ElectronicDocumentSourceActivity
                    {
                        TenantId = r.TenantId,
                        CompanyId = r.CompanyId,
                        SourceModule = SalesSourceModule,
                        DocumentType = ElectronicDocumentType.CreditNote,
                        SourceEntityId = r.Id,
                        ActivityUtc = r.UpdatedAt,
                    })
            )
            .Concat(
                db.Set<RetentionDocument>().AsPlatformQuery()
                    .Select(r => new ElectronicDocumentSourceActivity
                    {
                        TenantId = r.TenantId,
                        CompanyId = r.CompanyId,
                        SourceModule = RetentionElectronicDocumentSource.SourceModule,
                        DocumentType = ElectronicDocumentType.Retention,
                        SourceEntityId = r.Id,
                        ActivityUtc = r.UpdatedAt,
                    })
            );

    /// <summary>
    /// Candidatos de un job: los que INICIARÍAN una transmisión (Draft/Failed) solo con la empresa
    /// habilitada y actividad posterior a la habilitación (último intento o, sin intentos, el instante del
    /// origen; nunca la creación del comprobante). Los demás estados (consultas de autorización) no dependen
    /// del interruptor. Misma regla que <see cref="ElectronicEmissionState.AllowsTransmission"/> con automatic=true.
    /// </summary>
    public static IQueryable<ElectronicDocument> WhereAutomaticTransmissionAllowed(
        this IQueryable<ElectronicDocument> documents,
        ErpDbContext db
    )
    {
        var enabled = Enabled(db);
        var sources = SourceActivity(db);
        return documents.Where(x =>
            (
                x.CurrentState != ElectronicDocumentState.Draft
                && x.CurrentState != ElectronicDocumentState.Failed
            )
            || enabled.Any(e =>
                e.TenantId == x.TenantId
                && e.CompanyId == x.CompanyId
                && (
                    x.LastAttemptUtc
                    ?? sources
                        .Where(s =>
                            s.TenantId == x.TenantId
                            && s.CompanyId == x.CompanyId
                            && s.SourceModule == x.SourceModule
                            && s.DocumentType == x.DocumentType
                            && s.SourceEntityId == x.SourceEntityId
                        )
                        .Select(s => s.ActivityUtc)
                        .FirstOrDefault()
                ) >= e.EnabledSinceUtc
            )
        );
    }
}

/// <summary>SRI-TOGGLE-02/04 — implementación de <see cref="IElectronicEmissionSwitch"/> sobre org_settings.</summary>
public sealed class ElectronicEmissionSwitch : IElectronicEmissionSwitch
{
    private readonly ErpDbContext _db;

    public ElectronicEmissionSwitch(ErpDbContext db) => _db = db;

    public async Task<ElectronicEmissionState> GetAsync(
        Guid tenantId,
        Guid companyId,
        bool lockForShare,
        CancellationToken ct = default
    )
    {
        if (lockForShare && _db.Database.CurrentTransaction is null)
            throw new InvalidOperationException(
                "La lectura del interruptor de emisión dentro de un reclamo exige una transacción activa."
            );

        // El lock compartido espera a un cambio en curso; la consulta siguiente (otra sentencia, otro
        // snapshot read committed) ve el valor ya confirmado. Dentro de la transacción del lock: una
        // habilitación no puede fechar su registro entre el lock y la lectura.
        if (_db.Database.CurrentTransaction is not null)
            return await LockedReadAsync(tenantId, companyId, exclusive: false, ct);

        await using var transaction = await _db.Database.BeginTransactionAsync(ct);
        var state = await LockedReadAsync(tenantId, companyId, exclusive: false, ct);
        await transaction.CommitAsync(ct);
        return state;
    }

    public Task<DateTime?> GetSourceActivityUtcAsync(
        Guid tenantId,
        Guid companyId,
        string sourceModule,
        ElectronicDocumentType documentType,
        Guid sourceEntityId,
        CancellationToken ct = default
    ) =>
        ElectronicEmissionSwitchQuery
            .SourceActivity(_db)
            .Where(s =>
                s.TenantId == tenantId
                && s.CompanyId == companyId
                && s.SourceModule == sourceModule
                && s.DocumentType == documentType
                && s.SourceEntityId == sourceEntityId
            )
            .Select(s => s.ActivityUtc)
            .FirstOrDefaultAsync(ct);

    public async Task LockForChangeAsync(Guid tenantId, Guid companyId, CancellationToken ct = default)
    {
        if (_db.Database.CurrentTransaction is null)
            throw new InvalidOperationException(
                "Cambiar el interruptor de emisión exige una transacción activa (lock exclusivo hasta el commit)."
            );
        await AcquireAsync(tenantId, companyId, exclusive: true, ct);
    }

    private async Task<ElectronicEmissionState> LockedReadAsync(
        Guid tenantId,
        Guid companyId,
        bool exclusive,
        CancellationToken ct
    )
    {
        await AcquireAsync(tenantId, companyId, exclusive, ct);
        var enabled = await ElectronicEmissionSwitchQuery
            .Enabled(_db)
            .Where(e => e.TenantId == tenantId && e.CompanyId == companyId)
            .Select(e => (DateTime?)e.EnabledSinceUtc)
            .FirstOrDefaultAsync(ct);
        return enabled is null
            ? ElectronicEmissionState.Disabled
            : new ElectronicEmissionState(true, enabled);
    }

    // Lock consultivo de transacción por empresa (cubre también la primera habilitación, que es un INSERT
    // sin fila que bloquear). Una colisión de hash entre empresas solo serializa de más, nunca de menos.
    private Task AcquireAsync(Guid tenantId, Guid companyId, bool exclusive, CancellationToken ct)
    {
        var scope = $"{tenantId:N}:{companyId:N}";
        return exclusive
            ? _db.Database.ExecuteSqlInterpolatedAsync(
                $"SELECT pg_advisory_xact_lock(hashtext('erp.sri.emission_enabled'), hashtext({scope}))",
                ct
            )
            : _db.Database.ExecuteSqlInterpolatedAsync(
                $"SELECT pg_advisory_xact_lock_shared(hashtext('erp.sri.emission_enabled'), hashtext({scope}))",
                ct
            );
    }
}

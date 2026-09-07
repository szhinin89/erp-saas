using ERP.Application.Modules.Purchases.Services;
using ERP.Domain.MasterData.Enums;
using ERP.Domain.MasterData.Interfaces;
using ERP.Domain.Modules.Company.Interfaces;

namespace ERP.Application.Modules.Retentions.Services;

/// <summary>
/// RETENTIONS-ELIGIBILITY-01 — implementación de solo lectura. Vive en <c>Application</c> (no en
/// <c>Infrastructure</c>) porque no accede a <c>DbContext</c> directamente: solo orquesta
/// repositorios/servicios ya definidos en <c>Domain</c>/<c>Application</c>
/// (<see cref="ICompanyRepository"/>, <see cref="IBusinessPartnerRoleRepository"/>,
/// <see cref="ISupplierRetentionDefaultRepository"/>, <see cref="IRetentionCodeResolver"/>).
///
/// RETENTIONS-SUPPLIER-DEFAULTS-DYNAMIC-01: reemplaza la lectura de
/// SupplierRoleConfig.DefaultRetentionVatCode/DefaultRetentionIncomeCode (máx. 1 código por
/// impuesto, tenant-wide) por la lista de <c>SupplierRetentionDefault</c> activos del proveedor en
/// la empresa evaluada — 0..N candidatos por impuesto. El TaxType real de cada default se resuelve
/// siempre contra el catálogo SRI vigente (FK real, nunca duplicado en la fila del proveedor).
/// </summary>
public sealed class RetentionEligibilityService : IRetentionEligibilityService
{
    private readonly ICompanyRepository _companyRepo;
    private readonly IBusinessPartnerRoleRepository _roleRepo;
    private readonly ISupplierRetentionDefaultRepository _retentionDefaultRepo;
    private readonly IRetentionCodeResolver _retCodeResolver;

    public RetentionEligibilityService(
        ICompanyRepository companyRepo,
        IBusinessPartnerRoleRepository roleRepo,
        ISupplierRetentionDefaultRepository retentionDefaultRepo,
        IRetentionCodeResolver retCodeResolver
    )
    {
        _companyRepo = companyRepo;
        _roleRepo = roleRepo;
        _retentionDefaultRepo = retentionDefaultRepo;
        _retCodeResolver = retCodeResolver;
    }

    public async Task<RetentionEligibilityResult> EvaluateAsync(
        Guid tenantId,
        Guid companyId,
        Guid supplierId,
        decimal vatRetainableBase,
        decimal incomeRetainableBase,
        CancellationToken ct = default
    )
    {
        var reasons = new List<string>();

        // ── Empresa (fuente de verdad server-side, nunca body/usuario) ──────────
        // GetByIdAsync ya es fail-closed por tenant: Company implementa ITenantScopedEntity y el
        // query filter global de ErpDbContext filtra por ICurrentTenant — una empresa de otro
        // tenant nunca puede resolverse aquí (mismo mecanismo que el resto del ERP).
        var company = await _companyRepo.GetByIdAsync(companyId, ct);
        if (company is null || company.TenantId != tenantId)
        {
            const string reason = "No se pudo resolver la configuración tributaria de la empresa.";
            return new RetentionEligibilityResult(
                CanRetainVat: false,
                CanRetainIncome: false,
                IsSupplierExempt: false,
                HasRetainableBase: vatRetainableBase > 0 || incomeRetainableBase > 0,
                MissingRetentionCode: false,
                IsSupplierRequiredToKeepAccounting: false,
                Candidates: Array.Empty<RetentionEligibilityCandidate>(),
                Reasons: new[] { reason }
            );
        }

        // ── Proveedor / sujeto retenido ──────────────────────────────────────────
        var supplierRole = await _roleRepo.GetByTypeAsync(supplierId, RoleType.Supplier, ct);
        var config = supplierRole?.SupplierConfig;
        var isExempt = config?.IsRetentionExempt ?? false;
        var isRequiredToKeepAccounting = config?.IsRequiredToKeepAccounting ?? false;

        var hasVatBase = vatRetainableBase > 0;
        var hasIncomeBase = incomeRetainableBase > 0;
        var hasRetainableBase = hasVatBase || hasIncomeBase;

        // Resolver TODOS los defaults activos contra catálogo una sola vez — el TaxType real de
        // cada uno viene siempre del catálogo (SSOT), nunca se asume desde la fila del proveedor.
        var defaults = await _retentionDefaultRepo.GetActiveByBusinessPartnerAsync(supplierId, ct);
        var resolvedCandidates = new List<RetentionEligibilityCandidate>();
        var orphanCount = 0;
        foreach (var entry in defaults)
        {
            var info = await _retCodeResolver.GetRetentionCodeByIdAsync(entry.SriRetentionCodeId, ct);
            if (info is null)
            {
                orphanCount++;
                continue;
            }
            resolvedCandidates.Add(
                new RetentionEligibilityCandidate(info.TaxType, info.Code, info.Name, info.Percentage)
            );
        }

        var vatCandidates = resolvedCandidates
            .Where(c => string.Equals(c.TaxType, "IVA", StringComparison.OrdinalIgnoreCase))
            .ToList();
        var incomeCandidates = resolvedCandidates
            .Where(c => string.Equals(c.TaxType, "RENTA", StringComparison.OrdinalIgnoreCase))
            .ToList();

        var (canRetainVat, missingVatCode) = EvaluateTax(
            taxLabel: "IVA",
            taxTypeCode: "IVA",
            companyWithholds: company.WithholdsVat,
            isExempt,
            hasBase: hasVatBase,
            candidates: vatCandidates,
            hasAnyDefaultConfigured: defaults.Count > 0,
            reasons
        );

        var (canRetainIncome, missingIncomeCode) = EvaluateTax(
            taxLabel: "Renta",
            taxTypeCode: "RENTA",
            companyWithholds: company.WithholdsRenta,
            isExempt,
            hasBase: hasIncomeBase,
            candidates: incomeCandidates,
            hasAnyDefaultConfigured: defaults.Count > 0,
            reasons
        );

        if (isRequiredToKeepAccounting)
        {
            // Dato informativo únicamente (regla #8 de RETENTIONS-ELIGIBILITY-01) — no cambia
            // CanRetainVat/CanRetainIncome ni el porcentaje a resolver (responsabilidad exclusiva
            // de RetentionCalculator, no tocado en esta subfase).
            reasons.Add(
                "El proveedor está obligado a llevar contabilidad (dato informativo, no altera la elegibilidad ni el porcentaje calculado en esta subfase)."
            );
        }

        var candidates = (canRetainVat ? vatCandidates : Enumerable.Empty<RetentionEligibilityCandidate>())
            .Concat(canRetainIncome ? incomeCandidates : Enumerable.Empty<RetentionEligibilityCandidate>())
            .ToList();

        return new RetentionEligibilityResult(
            CanRetainVat: canRetainVat,
            CanRetainIncome: canRetainIncome,
            IsSupplierExempt: isExempt,
            HasRetainableBase: hasRetainableBase,
            MissingRetentionCode: missingVatCode || missingIncomeCode,
            IsSupplierRequiredToKeepAccounting: isRequiredToKeepAccounting,
            Candidates: candidates,
            Reasons: reasons
        );
    }

    /// <summary>
    /// Evalúa un único impuesto (IVA o Renta) siguiendo el orden fijado en
    /// RETENTIONS-MODULE-DESIGN-01.md § "Qué evalúa RETENTIONS-ELIGIBILITY-01": (1) la empresa
    /// puede/debe retener ese impuesto, (2) el proveedor no está exento, (3) el documento tiene
    /// base retenible para ese impuesto, (4) existe al menos un default activo con código de
    /// retención vigente en catálogo/SSOT. Nunca lanza excepción por regla de negocio no
    /// cumplida — siempre devuelve un resultado controlado.
    /// </summary>
    private static (bool CanRetain, bool MissingCode) EvaluateTax(
        string taxLabel,
        string taxTypeCode,
        bool companyWithholds,
        bool isExempt,
        bool hasBase,
        IReadOnlyList<RetentionEligibilityCandidate> candidates,
        bool hasAnyDefaultConfigured,
        List<string> reasons
    )
    {
        if (!companyWithholds)
        {
            reasons.Add(
                $"La empresa no está configurada como agente de retención de {taxLabel} (Company.Withholds{(taxTypeCode == "IVA" ? "Vat" : "Renta")}=false)."
            );
            return (false, false);
        }

        if (isExempt)
        {
            reasons.Add(
                $"El proveedor está exento de retención (SupplierRoleConfig.IsRetentionExempt=true) — no se genera retención de {taxLabel}."
            );
            return (false, false);
        }

        if (!hasBase)
        {
            reasons.Add($"El documento origen no tiene base retenible de {taxLabel}.");
            return (false, false);
        }

        if (candidates.Count > 0)
            return (true, false);

        if (hasAnyDefaultConfigured)
        {
            reasons.Add(
                $"Las retenciones de {taxLabel} configuradas para el proveedor ya no están activas en el catálogo SRI."
            );
            return (false, true);
        }

        reasons.Add(
            $"El proveedor no tiene retenciones de {taxLabel} configuradas para esta empresa — no hay ningún default activo."
        );
        return (false, false);
    }
}

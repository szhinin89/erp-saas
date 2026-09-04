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
/// <see cref="IRetentionCodeResolver"/>), el mismo criterio que ya usa
/// <c>CalculateRetentionHandler</c>/<c>IssueWithholdingHandler</c> de Purchases para resolver
/// config de proveedor + código de retención — no se inventa un patrón nuevo.
///
/// No modifica <see cref="IRetentionCodeResolver"/> (se inyecta y reutiliza tal cual, sin mover su
/// ubicación — la reubicación hacia <c>Retentions</c> es explícitamente de una fase posterior,
/// E1-A, según RETENTIONS-MODULE-DESIGN-01.md).
/// </summary>
public sealed class RetentionEligibilityService : IRetentionEligibilityService
{
    private readonly ICompanyRepository _companyRepo;
    private readonly IBusinessPartnerRoleRepository _roleRepo;
    private readonly IRetentionCodeResolver _retCodeResolver;

    public RetentionEligibilityService(
        ICompanyRepository companyRepo,
        IBusinessPartnerRoleRepository roleRepo,
        IRetentionCodeResolver retCodeResolver
    )
    {
        _companyRepo = companyRepo;
        _roleRepo = roleRepo;
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
                SuggestedVatRetentionCode: null,
                SuggestedIncomeRetentionCode: null,
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

        var (canRetainVat, missingVatCode, suggestedVatCode) = await EvaluateTaxAsync(
            taxLabel: "IVA",
            taxTypeCode: "IVA",
            companyWithholds: company.WithholdsVat,
            isExempt,
            hasBase: hasVatBase,
            configuredCode: config?.DefaultRetentionVatCode,
            reasons,
            ct
        );

        var (canRetainIncome, missingIncomeCode, suggestedIncomeCode) = await EvaluateTaxAsync(
            taxLabel: "Renta",
            taxTypeCode: "RENTA",
            companyWithholds: company.WithholdsRenta,
            isExempt,
            hasBase: hasIncomeBase,
            configuredCode: config?.DefaultRetentionIncomeCode,
            reasons,
            ct
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

        return new RetentionEligibilityResult(
            CanRetainVat: canRetainVat,
            CanRetainIncome: canRetainIncome,
            IsSupplierExempt: isExempt,
            HasRetainableBase: hasRetainableBase,
            MissingRetentionCode: missingVatCode || missingIncomeCode,
            IsSupplierRequiredToKeepAccounting: isRequiredToKeepAccounting,
            SuggestedVatRetentionCode: suggestedVatCode,
            SuggestedIncomeRetentionCode: suggestedIncomeCode,
            Reasons: reasons
        );
    }

    /// <summary>
    /// Evalúa un único impuesto (IVA o Renta) siguiendo el orden fijado en
    /// RETENTIONS-MODULE-DESIGN-01.md § "Qué evalúa RETENTIONS-ELIGIBILITY-01": (1) la empresa
    /// puede/debe retener ese impuesto, (2) el proveedor no está exento, (3) el documento tiene
    /// base retenible para ese impuesto, (4) existe código de retención activo en catálogo/SSOT.
    /// Nunca lanza excepción por regla de negocio no cumplida — siempre devuelve un resultado
    /// controlado, igual que <c>IssueWithholdingHandler</c>/<c>RetentionCalculator</c> ya hacen
    /// hoy (código ausente ⇒ línea omitida, no excepción).
    /// </summary>
    private async Task<(bool CanRetain, bool MissingCode, string? SuggestedCode)> EvaluateTaxAsync(
        string taxLabel,
        string taxTypeCode,
        bool companyWithholds,
        bool isExempt,
        bool hasBase,
        string? configuredCode,
        List<string> reasons,
        CancellationToken ct
    )
    {
        if (!companyWithholds)
        {
            reasons.Add(
                $"La empresa no está configurada como agente de retención de {taxLabel} (Company.Withholds{(taxTypeCode == "IVA" ? "Vat" : "Renta")}=false)."
            );
            return (false, false, null);
        }

        if (isExempt)
        {
            reasons.Add(
                $"El proveedor está exento de retención (SupplierRoleConfig.IsRetentionExempt=true) — no se genera retención de {taxLabel}."
            );
            return (false, false, null);
        }

        if (!hasBase)
        {
            reasons.Add($"El documento origen no tiene base retenible de {taxLabel}.");
            return (false, false, null);
        }

        if (string.IsNullOrWhiteSpace(configuredCode))
        {
            reasons.Add(
                $"El proveedor no tiene código de retención de {taxLabel} configurado — no hay código activo desde el catálogo SRI."
            );
            return (false, true, null);
        }

        var info = await _retCodeResolver.GetRetentionCodeAsync(configuredCode, taxTypeCode, ct);
        if (info is null)
        {
            reasons.Add(
                $"El código de retención de {taxLabel} configurado para el proveedor ('{configuredCode}') no está activo en el catálogo SRI."
            );
            return (false, true, null);
        }

        return (true, false, info.Code);
    }
}

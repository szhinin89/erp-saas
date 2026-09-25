using ERP.Application.Common;
using ERP.Application.Modules.Companies;
using ERP.Application.Modules.Companies.UseCases.PrecisionPolicy;
using ERP.Domain.Common;
using ERP.Domain.Configuration.Entities;
using ERP.Domain.Exceptions;

namespace ERP.Infrastructure.Persistence.Repositories.CompanyConfig;

/// <summary>
/// COMPANY-PRECISION-POLICY-SSOT-01. Ver contrato completo en
/// <see cref="ICompanyPrecisionPolicyProvider"/>.
/// </summary>
public sealed class CompanyPrecisionPolicyProvider : ICompanyPrecisionPolicyProvider
{
    private readonly ICompanyPrecisionPolicyRepository _repo;
    private readonly ICurrentTenant _tenant;
    private readonly ICurrentCompany _company;
    private readonly ICurrentUser _currentUser;

    public CompanyPrecisionPolicyProvider(
        ICompanyPrecisionPolicyRepository repo,
        ICurrentTenant tenant,
        ICurrentCompany company,
        ICurrentUser currentUser
    )
    {
        _repo = repo;
        _tenant = tenant;
        _company = company;
        _currentUser = currentUser;
    }

    public async Task<EffectivePrecisionPolicyDto> GetEffectiveAsync(CancellationToken ct = default)
    {
        if (!_company.HasCompanyContext || _company.CompanyId == Guid.Empty)
            throw CompanyScopeException.NoCompanyContext();

        var tenantId = _tenant.TenantId;
        var companyId = _company.CompanyId;

        var policy =
            await _repo.FindAsync(tenantId, companyId, ct)
            ?? throw new CompanyPrecisionPolicyMissingException(companyId);

        return ToDto(policy);
    }

    private static EffectivePrecisionPolicyDto ToDto(CompanyPrecisionPolicy p) =>
        new(
            ProfileType: p.ProfileType.ToString(),
            SalesUnitPriceDecimals: p.SalesUnitPriceDecimals,
            PurchaseUnitPriceDecimals: p.PurchaseUnitPriceDecimals,
            QuantityDecimals: p.QuantityDecimals,
            PercentageDecimals: p.PercentageDecimals,
            UnitCostDecimals: p.UnitCostDecimals,
            AverageCostDecimals: p.AverageCostDecimals,
            ConversionFactorDecimals: p.ConversionFactorDecimals,
            SettlementToleranceAmount: p.SettlementToleranceAmount,
            IsLocked: p.IsLocked,
            LockedAt: p.LockedAt,
            LockedReason: p.LockedReason,
            // FiscalPrecision no distingue constantes separadas para Money/Tax/Accounting: los
            // tres numeric(18,2) fiscales comparten la escala TaxAmount=2 (ver comentario de
            // FiscalPrecision.TaxAmount: "VatAmount, IceAmount, TaxableBase, TaxInclusiveTotal").
            // Se exponen como 3 campos separados solo para que el frontend tenga nombres
            // semánticos claros; el valor numérico de los 3 es intencionalmente el mismo hoy.
            MoneyDecimals: FiscalPrecision.TaxAmount,
            TaxDecimals: FiscalPrecision.TaxAmount,
            AccountingDecimals: FiscalPrecision.TaxAmount,
            FiscalPercentageDecimals: FiscalPrecision.Percentage
        );
}

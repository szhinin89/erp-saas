using System.Globalization;
using ERP.Application.Common;
using ERP.Domain.Configuration.Constants;
using ERP.Domain.Configuration.Enums;
using ERP.Domain.Configuration.Interfaces;
using ERP.Domain.Modules.Company.Interfaces;
using ERP.Domain.Modules.Sales.Interfaces;
using ERP.Domain.Modules.Sales.Policies;

namespace ERP.Infrastructure.Services;

/// <summary>
/// Implementación del resolver de política fiscal de Consumidor Final. Igual que
/// <see cref="OrgConfigResolver"/>, resuelve tenant/company del contexto de ejecución actual —
/// nunca los recibe como parámetro. "0" configurado manualmente se distingue de "sin configurar"
/// usando <c>decimal?</c> (ver <see cref="IOrgConfigResolver.GetValueAsync{T}"/>, que retorna
/// null cuando no hay fila, no 0).
/// </summary>
public sealed class SalesFiscalPolicyResolver : ISalesFiscalPolicyResolver
{
    private readonly IOrgConfigResolver _orgConfig;
    private readonly ICompanyRepository _companies;
    private readonly ICurrentTenant _currentTenant;
    private readonly ICurrentCompany _currentCompany;

    public SalesFiscalPolicyResolver(
        IOrgConfigResolver orgConfig,
        ICompanyRepository companies,
        ICurrentTenant currentTenant,
        ICurrentCompany currentCompany
    )
    {
        _orgConfig = orgConfig;
        _companies = companies;
        _currentTenant = currentTenant;
        _currentCompany = currentCompany;
    }

    public async Task<SalesFiscalPolicyResult> GetEffectivePolicyAsync(
        CancellationToken cancellationToken = default
    )
    {
        var tenantId = _currentTenant.TenantId;
        var companyId = _currentCompany.CompanyId;

        var company = await _companies.GetByIdForTenantAsync(companyId, tenantId, cancellationToken);
        var taxRegimeCode = company?.TaxRegimeCode;

        // Se usa el valor raw (string) — no el genérico decimal — porque "0" configurado
        // manualmente debe distinguirse de "sin configurar" (raw null), y default(decimal) es 0
        // en ambos casos con el genérico.
        var raw = await _orgConfig.GetValueAsync(
            OrgScope.Company,
            companyId,
            OrgSettingKeys.Sales.ConsumerFinalMaxAmount,
            cancellationToken
        );

        if (
            !string.IsNullOrWhiteSpace(raw)
            && decimal.TryParse(raw, NumberStyles.Number, CultureInfo.InvariantCulture, out var manual)
        )
        {
            return new SalesFiscalPolicyResult(
                BlockConsumerFinalCredit: true,
                ConsumerFinalMaxAmount: manual,
                ConsumerFinalMaxAmountSource.Manual,
                taxRegimeCode
            );
        }

        var (amount, isKnownRegime) = ConsumerFinalPolicyDefaults.ResolveDefault(taxRegimeCode);
        var source = isKnownRegime
            ? ConsumerFinalMaxAmountSource.TaxRegimeDefault
            : ConsumerFinalMaxAmountSource.Fallback;

        return new SalesFiscalPolicyResult(
            BlockConsumerFinalCredit: true,
            ConsumerFinalMaxAmount: amount,
            source,
            taxRegimeCode
        );
    }
}

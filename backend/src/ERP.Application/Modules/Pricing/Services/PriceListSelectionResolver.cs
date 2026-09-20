using ERP.Application.Common;
using ERP.Application.Common.Services;
using ERP.Domain.Modules.Pricing.Entities;
using ERP.Domain.Modules.Pricing.Interfaces;

namespace ERP.Application.Modules.Pricing.Services;

public sealed class PriceListSelectionResolver : IPriceListSelectionResolver
{
    private readonly IPriceListCustomerRepository _customerLists;
    private readonly IPriceListRepository _priceLists;
    private readonly ICurrentTenant _t;
    private readonly ICurrentCompany _c;
    private readonly ICompanyClock _companyClock;

    public PriceListSelectionResolver(
        IPriceListCustomerRepository customerLists,
        IPriceListRepository priceLists,
        ICurrentTenant t,
        ICurrentCompany c,
        ICompanyClock companyClock
    )
    {
        _customerLists = customerLists;
        _priceLists = priceLists;
        _t = t;
        _c = c;
        _companyClock = companyClock;
    }

    public async Task<IReadOnlyList<PriceListSelectionResult>> ResolveAsync(
        Guid? customerId,
        CancellationToken ct = default
    )
    {
        var tenantId = _t.TenantId;
        var today = await _companyClock.TodayAsync(_c.CompanyId, tenantId, ct);
        var candidates = new List<PriceListSelectionResult>(2);

        // 1. Lista del cliente — solo se consulta si customerId viene informado. NULL significa
        // explícitamente "sin cliente" y omite por completo PriceListCustomer (nunca se usa
        // Guid.Empty como sentinel mágico — PRICING-CONTEXT-NULL-CUSTOMER-05C1). Como máximo una
        // relación ACTIVA por (Tenant, Company, Customer) está garantizado en BD (índice único
        // parcial), así que FirstOrDefault sobre las activas nunca es ambiguo.
        PriceList? customerList = null;
        if (customerId.HasValue)
        {
            var customerAssignments = await _customerLists.GetByCustomerAsync(tenantId, customerId.Value, ct);
            var activeCustomerAssignment = customerAssignments.FirstOrDefault(a => a.IsActive);
            if (activeCustomerAssignment is not null)
            {
                customerList = await _priceLists.GetByIdAsync(
                    tenantId,
                    activeCustomerAssignment.PriceListId,
                    ct
                );
                if (Applies(customerList, today))
                    candidates.Add(
                        new PriceListSelectionResult(
                            customerList!.Id,
                            customerList.Name,
                            PriceListSelectionSource.Customer
                        )
                    );
            }
        }

        // 2. Lista default de la empresa — mismo criterio de vigencia que PricingResolver
        // (PRICE-LIST-EXPIRED-FALLBACK-PVP-01), replicado aquí porque la selección de lista es
        // una responsabilidad separada de la resolución de precio. Se omite si es la MISMA lista
        // ya agregada como candidato Customer — nunca se devuelve duplicada.
        var defaultList = await _priceLists.GetDefaultAsync(tenantId, ct);
        if (Applies(defaultList, today) && defaultList!.Id != customerList?.Id)
            candidates.Add(
                new PriceListSelectionResult(
                    defaultList.Id,
                    defaultList.Name,
                    PriceListSelectionSource.CompanyDefault
                )
            );

        // 3. Si ninguna aplicó, la colección queda vacía — el consumidor (PricingResolver, en su
        // propio pipeline ya existente) cae a BaseSalePrice (PVP) sin necesidad de un sentinel
        // "None" acá.
        return candidates;
    }

    private static bool Applies(PriceList? priceList, DateOnly today) =>
        priceList is not null && priceList.IsActive && priceList.IsValidOn(today);
}

using ERP.Application.Common;
using ERP.Application.Modules.Pricing.DTOs;

namespace ERP.Application.Modules.Pricing.Services;

/// <summary>
/// Única fuente oficial de resolución de precio de venta del ERP (SSOT). Ventas, Compras,
/// Inventario, POS y Facturación deben consumir esta interfaz — nunca reimplementar la
/// lógica de "precio vigente de un ítem" localmente.
///
/// Pipeline: Item.BaseSalePrice → Resolver PriceList (explícita o default, vigente) →
/// Resolver regla (PricingRule del ítem > regla general de la lista > sin ajuste) →
/// Aplicar estrategia de ajuste → PricingResult.
///
/// PRICE-LIST-EXPIRED-FALLBACK-PVP-01: una PriceList inexistente/deshabilitada/vencida/aún no
/// vigente NUNCA bloquea la venta — se ignora y el precio cae al PVP/BaseSalePrice del ítem tal
/// cual. El único bloqueo real es la ausencia de <c>Item.BaseSalePrice</c>.
///
/// NO calcula impuestos — frontera respetada con la infraestructura tributaria congelada
/// (ISriTaxResolver). El PricingResult es un precio neto.
/// </summary>
/// <summary>
/// Contexto comercial para <see cref="IPricingResolver.ResolveAsync(PricingContext, CancellationToken)"/>
/// — Tenant/Company/fecha operativa siguen resolviéndose de forma ambiental (ICurrentTenant/
/// ICurrentCompany/ICompanyClock), nunca se pasan explícitos aquí.
///
/// PRICING-CONTEXTUAL-RESOLUTION-05C: <see cref="CustomerId"/> es opcional — sin él, la
/// resolución se comporta exactamente igual que hoy (solo PriceList.IsDefault, sin candidato de
/// cliente). Todavía NO consumido desde Sales/frontend.
/// </summary>
public sealed record PricingContext(Guid ItemId, Guid? CustomerId = null);

public interface IPricingResolver
{
    Task<Result<PricingResult>> ResolveAsync(
        Guid itemId,
        Guid? priceListId = null,
        CancellationToken ct = default
    );

    /// <summary>
    /// PRICING-CONTEXTUAL-RESOLUTION-05C: resuelve el precio recorriendo los candidatos de
    /// <see cref="IPriceListSelectionResolver"/> en orden (Customer → CompanyDefault → ...),
    /// usando el primero cuyo ítem tenga un <c>PriceListItem</c> activo. No reimplementa ninguna
    /// fórmula — delega el cálculo por-lista a la misma ruta que <see cref="ResolveAsync(Guid, Guid?, CancellationToken)"/>.
    /// </summary>
    Task<Result<PricingResult>> ResolveAsync(PricingContext context, CancellationToken ct = default);
}

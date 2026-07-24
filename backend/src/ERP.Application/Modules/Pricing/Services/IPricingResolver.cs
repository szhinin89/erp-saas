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
/// NO calcula impuestos — frontera respetada con la infraestructura tributaria congelada
/// (ISriTaxResolver). El PricingResult es un precio neto.
/// </summary>
public interface IPricingResolver
{
    Task<Result<PricingResult>> ResolveAsync(
        Guid itemId,
        Guid? priceListId = null,
        CancellationToken ct = default);
}

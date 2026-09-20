namespace ERP.Application.Modules.Pricing.Services;

/// <summary>De dónde proviene un candidato de PriceList devuelto por <see cref="IPriceListSelectionResolver"/>.</summary>
public enum PriceListSelectionSource
{
    /// <summary>El cliente tiene una PriceListCustomer activa, y esa lista aplica (activa y vigente).</summary>
    Customer,

    /// <summary>PriceList.IsDefault de la empresa, activa y vigente.</summary>
    CompanyDefault,
}

/// <summary>
/// Un candidato de PriceList — QUÉ lista podría corresponder, no CUÁNTO cuesta nada ni si el
/// ítem que se está vendiendo pertenece a ella.
/// </summary>
public sealed record PriceListSelectionResult(
    Guid PriceListId,
    string PriceListName,
    PriceListSelectionSource Source
);

/// <summary>
/// Única fuente de verdad de QUÉ PriceLists son candidatas para un cliente en el contexto
/// comercial actual (Tenant + Company), en orden de precedencia. Responsabilidad exclusiva de
/// selección de lista — no calcula precios, no aplica reglas, no conoce PricingRule ni
/// PriceListItem (asignación de ítems).
///
/// PRICING-PRICE-LIST-SELECTION-CANDIDATES-05A1: devuelve una LISTA ORDENADA de candidatos en
/// vez de una única lista "ganadora", porque la lista del cliente puede no tener el ítem que se
/// está vendiendo asignado (PriceListItem) — decisión que NO le corresponde a este resolver
/// (sería acoplar selección de lista con la resolución de precio/asignación de PricingCalculation
/// / PricingResolver). El consumidor real (PricingResolver, en un ticket de integración futuro)
/// recorre los candidatos en orden y usa el primero cuyo PriceListItem esté activo para el ítem;
/// si ninguno lo tiene, cae a BaseSalePrice (PVP) — ese fallback por-ítem vive en el consumidor,
/// nunca aquí.
///
/// Jerarquía y orden de los candidatos:
///   1. Con <c>customerId</c> informado: cliente tiene PriceListCustomer activa cuya PriceList
///      es activa y vigente → candidato Customer. Sin <c>customerId</c> (null), este paso se
///      omite por completo — nunca se consulta PriceListCustomer.
///   2. PriceList.IsDefault de la empresa, activa y vigente → candidato CompanyDefault.
///   3. Si la lista del cliente y la default son LA MISMA PriceList, se devuelve un único
///      candidato (Customer) — nunca duplicado.
///   4. Si ninguna aplica → colección vacía (el consumidor cae directo a BaseSalePrice/PVP).
///
/// NO integrado todavía con Sales/PricingResolver — ver ticket de seguimiento para el
/// call-site real. Esta es solo la base extensible.
/// </summary>
public interface IPriceListSelectionResolver
{
    /// <summary>
    /// PRICING-CONTEXT-NULL-CUSTOMER-05C1: <paramref name="customerId"/> es opcional —
    /// <c>null</c> significa explícitamente "sin cliente" y omite la consulta de
    /// PriceListCustomer (nunca un <see cref="Guid.Empty"/> usado como sentinel mágico). Un
    /// <see cref="Guid.Empty"/> explícito se trata como cualquier otro id real: se consulta
    /// igual, y como ningún PriceListCustomer real tiene ese id, simplemente no encuentra
    /// asignación — mismo resultado que cualquier cliente sin lista propia, sin caso especial.
    /// </summary>
    Task<IReadOnlyList<PriceListSelectionResult>> ResolveAsync(
        Guid? customerId,
        CancellationToken ct = default
    );
}

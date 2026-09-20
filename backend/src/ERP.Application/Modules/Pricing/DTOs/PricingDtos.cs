using ERP.Application.Modules.Pricing.Services;

namespace ERP.Application.Modules.Pricing.DTOs;

public sealed record PriceListDto(
    Guid Id,
    string Code,
    string Name,
    string CurrencyCode,
    bool IsDefault,
    DateOnly? ValidFrom,
    DateOnly? ValidUntil,
    string? RuleType,
    decimal? RuleValue,
    bool IsActive,
    DateTime CreatedAt,
    DateTime? UpdatedAt
);

/// <summary>
/// Contrato de edición/lectura cruda de una excepción (PricingRule). RuleType/RuleValue son
/// correctos aquí porque este DTO representa la entidad que se está creando/editando — no es
/// un resumen de "por qué" se resolvió un precio (ver <see cref="PricingRuleSummaryDto"/> para eso).
/// LastModifiedAt/LastModifiedByName provienen de PricingRuleAudit (auditoría de dominio),
/// resuelto en batch por IAuditReader — nunca por fila.
/// </summary>
public sealed record PricingRuleDto(
    Guid Id,
    Guid PriceListId,
    Guid ItemId,
    string RuleType,
    decimal RuleValue,
    bool IsActive,
    DateTime CreatedAt,
    DateTime? UpdatedAt,
    DateTime? LastModifiedAt = null,
    string? LastModifiedByName = null
);

/// <summary>Producto perteneciente a una PriceList (PriceListItem activo) — identidad + precio base únicamente. No trae reglas ni asignaciones de otras listas: responsabilidad única de "qué ítems están en esta lista".</summary>
public sealed record PriceListAssignedItemDto(
    Guid ItemId,
    string Sku,
    string ItemName,
    decimal? BaseSalePrice
);

/// <summary>De dónde proviene el precio resuelto de un ítem en una lista concreta.</summary>
public enum PriceSource
{
    BasePrice,
    GeneralRule,
    Exception,
}

/// <summary>
/// Resumen encapsulado de la regla vigente para un ítem en una lista — reemplaza exponer
/// RuleType/RuleValue sueltos en los read-models de precio resuelto (simulación del Item).
/// Evoluciona sin romper contrato: agregar vigencia/prioridad/observaciones a futuro no
/// requiere cambiar los consumidores existentes de este DTO.
/// </summary>
public sealed record PricingRuleSummaryDto(
    PriceSource Source,
    string? Type,
    decimal? Value,
    string Description
);

/// <summary>Resultado de SetPricingRuleCommand — nunca crea una fila duplicada ni resucita una
/// deshabilitada en silencio; "ExistsInactive" obliga a una decisión explícita del usuario
/// (ver EnablePricingRuleCommand) en vez de un upsert automático.</summary>
public enum PricingRuleSetStatus
{
    Created,
    AlreadyActive,
    ExistsInactive,
}

/// <summary>
/// <c>ExistingRuleType</c>/<c>ExistingRuleValue</c> solo se completan cuando
/// <c>Status == ExistsInactive</c> — permiten a la UI mostrar el valor de la excepción
/// deshabilitada (comparado contra el precio base actual del ítem) antes de que el usuario
/// confirme la reactivación explícita, en vez de reactivar un precio potencialmente
/// obsoleto sin ninguna señal visible.
/// </summary>
public sealed record PricingRuleSetResultDto(
    PricingRuleSetStatus Status,
    Guid? PricingRuleId,
    string? ExistingRuleType = null,
    decimal? ExistingRuleValue = null
);

/// <summary>Resultado de PricingResolver — precio neto (sin impuestos, ver frontera con ISriTaxResolver).
/// <c>RuleDescription</c> es el mismo texto humano que <see cref="PricingRuleSummaryDto.Description"/>
/// (vía <see cref="Services.PricingCalculation.Summarize"/>) — null cuando <c>RuleApplied</c> también
/// lo es (precio base sin ajuste), para que un consumidor de UI (p. ej. Ventas) pueda explicar de
/// dónde salió el precio sin reimplementar el formato de <c>RuleApplied</c>.
/// PRICE-LIST-EXPIRED-FALLBACK-PVP-01: <c>PriceListId</c> es null cuando no hubo ninguna lista
/// aplicable (inexistente/deshabilitada/vencida/aún no vigente) — el precio siempre se resuelve
/// (PVP/base), nunca se bloquea por el estado de la lista; en ese caso <c>PriceListCode</c>/
/// <c>PriceListName</c> quedan en el sentinel "Precio base" y <c>RuleApplied</c>/<c>RuleDescription</c>
/// en null, exactamente igual que cuando SÍ hay lista pero no aplica ninguna regla.</summary>
// SelectionSource (PRICING-CONTEXTUAL-RESOLUTION-05C): de qué candidato vino la lista usada
// (Customer vs CompanyDefault) — solo se completa cuando la resolución pasó por
// IPricingResolver.ResolveAsync(PricingContext, ct). Null en el ResolveAsync explícito (lista
// forzada, sin selección de candidatos) y en el fallback a BaseSalePrice (ningún candidato aplicó).
public sealed record PricingResult(
    Guid ItemId,
    Guid? PriceListId,
    string PriceListCode,
    string PriceListName,
    string CurrencyCode,
    decimal BasePrice,
    string? RuleApplied,
    decimal UnitPrice,
    string? RuleDescription = null,
    PriceListSelectionSource? SelectionSource = null
);

# ADR-021: Motor de Pricing v2 — Item.BaseSalePrice como SSOT + reglas de ajuste

**Estado:** ✅ CLOSED — Dominio Items+Pricing (Item, PriceList, PricingRule, PricingResolver) cerrado definitivamente. Integración con Ventas/Compras/POS/Facturación (consumo de `IPricingResolver`) queda para fases posteriores, sin reabrir este dominio.
**Fecha aprobación:** 2026-07-04 (Fase 1) — **Fecha de cierre: 2026-07-05**
**Autor:** Sebastian Zhinin (decisión tomada en sesión de evolución arquitectónica guiada)
**Contexto:** ERP SaaS multiempresa — el módulo Pricing v1 (cerrado dentro de las fases 4/7 de Items, 2026-07-02) no tenía un servicio de resolución de precio centralizado; la lógica de "precio vigente de un ítem" estaba reimplementada de forma independiente en 4 lugares (Purchases ×2, Items-Profitability ×2, Sales Infra), con criterios inconsistentes entre sí.

> **Reabre parcialmente** el freeze "Items Module FROZEN v1.0" (2026-06-17) y el freeze
> implícito de Pricing (Fases 4/7 de Items, 2026-07-02). Ambos quedan reemplazados por
> este ADR en lo referente al modelo de precios — el resto de Items (identidad,
> variantes, stock config, tax config, etc.) permanece sin cambios.

---

## Decisión

1. **`Item.BaseSalePrice`** (decimal `numeric(18,6)`, nullable) es la única fuente de verdad del precio base de un ítem. Ninguna otra entidad almacena un precio absoluto salvo mediante una regla explícita `FixedPrice`.
2. **`PriceList`** deja de ser un simple catálogo y pasa a representar una **regla general** opcional (`RuleType` + `RuleValue`) aplicable a todo el catálogo.
3. **`ItemPrice` se elimina y se reemplaza por `PricingRule`**: override de la regla general para un ítem (+ variante opcional) específico dentro de una lista. **A lo sumo una `PricingRule` activa por `(PriceListId, ItemId, ItemVariantId)`** — garantizado por índice único en BD.
4. **`PricingRuleType`**: `FixedPrice`, `PercentDiscount`, `PercentMarkup`, `FixedAdjustment`. Aplicado vía Strategy pattern (`IPricingAdjustmentStrategy`, registrado en DI) — agregar un tipo nuevo no requiere modificar el resolver (OCP).
5. **`PricingResolver`** (`ERP.Application.Modules.Pricing.Services`) es la única fuente oficial de resolución: Item.BaseSalePrice → PriceList (explícita o default, vigente) → PricingRule del ítem > regla general de la lista > sin ajuste → estrategia → `PricingResult`. **No calcula impuestos** — frontera respetada con la infraestructura tributaria congelada (`ISriTaxResolver`, ADR de Configuración Tributaria 2026-07-01).
6. Los 4 consumidores duplicados detectados en la auditoría (`GetPurchaseItemContextQueryHandler`, `LoadPvpSnapshotsHandler`, `GetItemProfitabilityHandler`, `SimulateItemPricingHandler`) fueron migrados a `IPricingResolver` como parte de esta fase (forzado por la eliminación de `ItemPrice.UnitPrice`, no una migración funcional completa — ver Pendientes). `ConfirmPurchaseHandler` fue actualizado para escribir el PVP confirmado directamente en `Item.BaseSalePrice` en vez de crear un `ItemPrice` en la lista default. `InvoiceItemSearchRepository` (Sales, hot-path de búsqueda) usa `Item.BaseSalePrice` directo sin resolver reglas — simplificación documentada, no delega al resolver por costo de N+1 en un batch.
7. Dato existente en `price_lists`/`item_prices` **no se preserva** (sistema no productivo) — migración `EvolvePricingEngineToRuleBasedModel` recrea las tablas desde cero.

## Consecuencias

- `SetInitialItemPriceCommand` fue eliminado — el precio inicial de un ítem se define directamente en `CreateItemCommand.BaseSalePrice` / `UpdateItemCommand.BaseSalePrice`.
- El endpoint `POST /api/v1/pricing/item-prices/set-initial` fue eliminado; `item-prices/*` fue renombrado a `pricing-rules/*`.
- **Pendiente (fuera de este dominio):** los consumidores adaptados en el punto 6 fueron modificados solo para compilar contra el nuevo modelo — no se auditó si su semántica de negocio (ej. "primer precio activo" en `GetPurchaseItemContext`, que no filtraba por lista default) debe alinearse 1:1 con la resolución oficial del resolver. Requiere revisión funcional explícita al integrar cada módulo consumidor.
- **Pendiente (fuera de este dominio):** Ventas no reconcilia `UnitPrice` de línea contra `PricingResolver` al guardar — sigue confiando en el precio que trajo el frontend. No se corrige aquí porque pertenece al módulo Ventas, no a Items+Pricing.
- **Pendiente (fuera de este dominio):** Frontend `modules/pricing/` no fue tocado — seguirá llamando a endpoints eliminados hasta que se rediseñe.

## Addendum 2026-07-05 — Cierre definitivo (CLOSED)

Auditoría de cierre confirmó y corrigió lo siguiente antes de declarar el dominio CLOSED:

- **`PricingRule.MinQuantity` fue eliminado por completo** (propiedad, parámetro de `Create`/`UpdateRule`, columna `min_quantity`, validación, DTO, comando `SetPricingRuleCommand`). Quiebres de cantidad / descuentos por volumen son alcance explícito del futuro **Promotion Engine** (fuera de Pricing) — no debía quedar infraestructura parcial preparada para eso dentro de este dominio. Migración `ClosePricingEngineRemoveVolumeQuantityScope` (2026-07-05) dropea la columna.
- El parámetro `quantity` de `IPricingResolver.ResolveAsync` fue eliminado — quedó sin uso tras retirar `MinQuantity`.
- `IPricingRuleRepository.GetActiveForItemInListAsync` pasó de devolver `IReadOnlyList<PricingRule>` a `PricingRule?` (a lo sumo una fila posible, dado el índice único). Esto además elimina el bug de precedencia detectado en la auditoría técnica previa (`ORDER BY ... DESC` con `NULLS FIRST` de PostgreSQL sobre una columna que ya no existe).
- `IPricingRuleRepository.ExistsAsync` fue eliminado por no tener ningún invocador (código muerto).
- Duplicación de reglas FluentValidation entre `CreatePriceListCommandValidator`/`UpdatePriceListCommandValidator` fue refactorizada a un método compartido (`PriceListCommonRules.Apply<T>`), sin cambiar comportamiento.
- **Deliberadamente NO se agregó** `Priority` a `PriceList`/`PricingRule`: no hay hoy ningún escenario de conflicto entre múltiples `PriceList` que lo requiera (la resolución siempre usa una lista explícita o la única marcada `IsDefault`), y agregarlo sería diseñar para un escenario hipotético fuera del alcance de este cierre.
- **Deliberadamente NO se agregaron** adaptadores de Tax/Currency/Rounding: el redondeo vive inline en `PricingResolver` por ser la única transformación numérica final del pipeline actual; agregar abstracciones sin un segundo caso de uso real habría sido sobre-ingeniería.

Con esto, el dominio Items+Pricing (`Item.BaseSalePrice`, `PriceList`, `PricingRule`, `PricingResolver`) queda **CLOSED**. Cualquier módulo del ERP que necesite el precio vigente de un ítem debe consumir únicamente `IPricingResolver.ResolveAsync(itemId, itemVariantId?, priceListId?, ct)` — no debe volver a modificarse esta infraestructura para dar soporte a promociones, cupones, campañas, combos o programas de fidelización; esas capacidades pertenecen a un futuro módulo `Promotions` independiente.

## Addendum 2026-07-05b — Eliminación de `PriceListType`

Auditoría posterior confirmó que `PriceListType` (`Default/Wholesale/Promo/Custom`) no participaba en ninguna regla de negocio: `PricingResolver` nunca lo consultaba, ningún handler ni repositorio dependía de él — era una etiqueta visual hardcodeada y duplicada además en el frontend (`PRICE_LIST_TYPES` en `pricingService.ts`). El único campo que realmente influye en la resolución es `IsDefault`.

Se eliminó completamente: enum `PriceListType.cs`, propiedad `PriceList.Type`, parámetro `type` de `Create()`/`Update()`, columna `type` de `price_lists` (migración `RemovePriceListType`, 2026-07-05), campo `Type` en `CreatePriceListCommand`/`UpdatePriceListCommand`/`PriceListDto`/`IPriceListFields`, su regla `RuleFor(x => x.Type).IsInEnum()`, y en frontend el select "Tipo", `PRICE_LIST_TYPES`, `priceListTypeName()` y los campos `type` de los payloads/DTO TypeScript.

Una `PriceList` queda definida únicamente por: `Code, Name, CurrencyCode, ValidFrom, ValidUntil, IsDefault, RuleType, RuleValue, IsActive`. Si una lista necesita diferenciarse de otra, debe hacerlo mediante su nombre, código, regla comercial y vigencia — nunca mediante una clasificación hardcodeada sin impacto en el comportamiento del dominio. No se reemplazó por ningún otro enum o clasificación.

## Addendum 2026-07-05c — `PriceListItem` (asignación administrativa Item↔PriceList)

Auditoría confirmó que `PricingRule` no puede representar una simple pertenencia de un ítem a una lista: `RuleType`/`RuleValue` son `NOT NULL` tanto en el dominio como en la columna de BD, y `PricingResolver` asume que toda `PricingRule` trae una estrategia aplicable. Extender esos campos a nullable habría obligado a tocar `PricingResolver` — fuera de alcance de esta tarea.

Se agregó **`PriceListItem`** (`ERP.Domain/Modules/Pricing/Entities/PriceListItem.cs`): entidad de asociación pura (`PriceListId`, `ItemId`, `IsActive`), **sin `RuleType`/`RuleValue`**, **sin ninguna referencia en `PricingResolver`**. Tabla `price_list_items`, índice único `(price_list_id, item_id)`. CQRS: `SetItemPriceListsCommand` (reemplaza el conjunto completo, patrón "Replace" ya usado en Items para imágenes/conversiones/sustitutos) y `GetItemPriceListsQuery`. Endpoints `GET`/`PUT /api/v1/items/{id}/price-lists`. Frontend: checklist en la pestaña Precios del formulario de Item (`PricingTab.tsx`), reemplazando el aviso que remitía al módulo Precios.

Esta entidad es deliberadamente ciega a reglas de precio — un módulo de Ventas/Compras que necesite el precio de un ítem sigue consumiendo exclusivamente `IPricingResolver`, nunca `PriceListItem`. `PriceListItem` solo responde "¿a qué listas pertenece este ítem", nunca "cuánto cuesta".

## Referencias

- Auditoría previa a esta decisión: sesión de evolución arquitectónica 2026-07-04/05 (ver conversación).
- ERP_CORE_FREEZE.md no tiene entrada dedicada a Pricing — este ADR es la primera gobernanza formal e independiente del BC Pricing.

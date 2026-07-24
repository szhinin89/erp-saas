# Items — Fase 7: Pricing del Item

**Estado**: ✅ FASE 7 COMPLETADA
**Fecha de cierre**: 2026-07-02
**Nivel documental**: 3 (detalle técnico especializado, referenciado desde [`docs/STATUS.md`](../STATUS.md))

Este documento es la referencia oficial de las decisiones funcionales y técnicas de la Fase 7 del módulo Items. Se apoya en las Fases 1-6 ya cerradas y no las reabre.

---

## 1. Resumen de la fase

**Objetivo**: verificar la consistencia del ciclo de vida de `ItemPrice` (creación, actualización, eliminación) frente a la regla absoluta del proyecto de nunca eliminar registros físicamente, y definir el alcance del historial de cambios de precio.

**Alcance funcional**: `ItemPrice` (creación, actualización, deshabilitación), múltiples listas de precio por ítem, simulación/rentabilidad (solo lectura).

**Fuera de alcance de esta fase**: Compras.

---

## 2. Decisiones funcionales aprobadas

| # | Decisión | Estado |
|---|----------|--------|
| 1 | `RemoveItemPriceCommand` **deja de eliminar físicamente** la fila de `ItemPrice` — pasa a deshabilitarla (`ItemPrice.Disable()`), respetando la regla absoluta del proyecto de nunca hacer `DELETE` físico. | ✅ Aprobada e implementada |
| 2 | El **historial de cambios de precio se registra en la tabla de auditoría existente** (`UserActivity`, append-only) — no se crea una tabla de historial propia del dominio de Pricing. Cada actualización de precio (`SetItemPriceCommand` sobre un precio existente) y cada deshabilitación quedan registradas con el valor anterior y el nuevo. | ✅ Aprobada e implementada |
| 3 | El dominio de `ItemPrice` (`UpdatePrice()`) **sigue sobrescribiendo el valor vigente** — no mantiene múltiples filas históricas propias; la trazabilidad de "qué valor tenía antes" se consulta desde la auditoría, no desde `item_prices`. | ✅ Aprobada (alcance explícito de la decisión #2) |

---

## 3. Reglas de dominio (invariantes vigentes)

1. Un `ItemPrice` nunca se elimina físicamente — solo se deshabilita (`IsActive = false`) mediante `Disable()`, reversible con `Enable()`.
2. Las consultas de lectura (`GetByPriceListAsync`, `GetByItemAsync`) solo devuelven precios activos — ya filtraban por `IsActive` desde antes de esta fase, ahora con datos reales alimentándolo.
3. Cada cambio de precio de un `ItemPrice` existente genera un registro de auditoría (`UserActivity`, `module: "pricing"`, `action: "item_price.update"`) con el valor anterior y el nuevo.
4. Cada deshabilitación de un `ItemPrice` genera un registro de auditoría (`action: "item_price.disable"`) con el último valor vigente.
5. Un ítem puede tener múltiples precios activos simultáneamente, uno por cada combinación única de `(PriceListId, ItemId, ItemVariantId)` — sin cambios respecto a fases anteriores.

---

## 4. Impacto arquitectónico

**Módulos NO afectados por esta fase**: Ventas, Compras, Inventario.

**Por qué no hay impacto**: el contrato público (`RemoveItemPriceCommand`, ruta `DELETE /api/v1/pricing/item-prices/{id}`) no cambió de nombre ni de firma — solo su comportamiento interno; ningún consumidor externo del endpoint necesita cambios. La tabla de auditoría (`UserActivity`) ya es consumida transversalmente por el sistema (usada también por Items en fases previas) — no se introduce infraestructura nueva.

---

## 5. Cambios técnicos realizados

**Backend**: `ItemPrice.cs` — nuevos métodos `Disable(Guid updatedBy)`/`Enable(Guid updatedBy)`; `IItemPriceRepository`/`ItemPriceRepository` — eliminado `RemoveAsync` (hacía `DELETE` físico); `RemoveItemPriceHandler` — deshabilita en vez de eliminar, registra auditoría; `SetItemPriceHandler` — al actualizar un precio existente, registra auditoría con valor anterior y nuevo antes de sobrescribir.

**Base de datos**: sin migración — la columna `is_active` en `item_prices` ya existía (confirmado en BD real), solo estaba inerte por falta de métodos de dominio que la usaran.

**Tests**: 6 tests nuevos de dominio (`ItemPriceTests.cs`) cubriendo `Disable`/`Enable` (incluyendo rechazo de doble deshabilitación/habilitación) y documentando explícitamente que `UpdatePrice()` sobrescribe sin conservar historial en el dominio. Suite completa backend (69 dominio + 24 aplicación) en verde. Arranque de API verificado sin errores de resolución de dependencias.

---

## 6. Riesgos conocidos (a revisar en fases posteriores, no resueltos aquí)

- El historial de precios vive en `UserActivity.Description` como texto libre — es consultable pero no estructurado (no hay una consulta tipo "dame la serie de precios de este ítem en el tiempo" sin parsear texto). Si en el futuro se necesita un reporte estructurado de evolución de precios, esta decisión debe revisarse explícitamente.

---

## 7. Pendientes — pertenecen a otras fases (no tratados aquí)

- **Fase 8 — Compras**: relación con proveedor, integración de compras.
- **Fase 9 — Arquitectura**: revisión transversal backend/frontend/infraestructura.

---

## 8. Estado de la fase

**Estado: ✅ FASE 7 COMPLETADA**

**Resultado**: el ciclo de vida de `ItemPrice` quedó alineado con la regla absoluta del proyecto de nunca eliminar registros físicamente, con el historial de cambios de precio disponible a través de la auditoría existente. Las siguientes fases podrán construirse sobre esta base sin modificar las decisiones tomadas en esta fase.

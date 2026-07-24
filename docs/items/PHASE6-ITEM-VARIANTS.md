# Items — Fase 6: Variantes del Item

**Estado**: ✅ FASE 6 COMPLETADA
**Fecha de cierre**: 2026-07-02
**Nivel documental**: 3 (detalle técnico especializado, referenciado desde [`docs/STATUS.md`](../STATUS.md))

Este documento es la referencia oficial de las decisiones funcionales y técnicas de la Fase 6 del módulo Items. Se apoya en las Fases 1-5 ya cerradas y no las reabre.

---

## 1. Resumen de la fase

**Objetivo**: verificar la consistencia de las reglas de unicidad e integridad de variantes, atributos, barcode de variante e imágenes, aplicando el mismo criterio de unicidad global por tenant ya establecido en fases anteriores donde correspondiera.

**Alcance funcional**: `ItemVariant` (SKU, atributos de eje, variante por defecto), `ItemVariantAttribute`, barcode a nivel de variante (heredado de Fase 2), `ItemImage` (imagen principal).

**Fuera de alcance de esta fase**: Pricing, Compras.

---

## 2. Decisiones funcionales aprobadas

| # | Decisión | Estado |
|---|----------|--------|
| 1 | El **SKU de variante** (`ItemVariant.SKU`) pasa a ser **único globalmente por tenant** — `(tenant_id, sku)` — en vez de único solo dentro del ítem. Aplica el mismo principio ya usado para el SKU del ítem (Fase 1) y barcode/código de proveedor (Fase 2). | ✅ Aprobada e implementada |
| 2 | Las variantes son **opcionales** más allá de la variante "default" (autocreada, sin atributos de eje) — crear variantes adicionales con atributos reales es una operación posterior a la creación del ítem. | ✅ Aprobada (sin cambio, ya vigente) |
| 3 | El barcode de variante ya es único globalmente por tenant desde Fase 2 — aplica igual para cualquier variante, no solo la default. | ✅ Aprobada (sin cambio, ya vigente) |
| 4 | Como máximo una imagen por ítem puede marcarse como principal (`IsMain`). | ✅ Aprobada (sin cambio, ya vigente) |

---

## 3. Reglas de dominio (invariantes vigentes)

1. Todo ítem se crea con exactamente una variante "default" (sin atributos de eje) — su SKU es idéntico al SKU del ítem, por lo que su unicidad global queda garantizada automáticamente por la unicidad de SKU del ítem (Fase 1), sin verificación adicional.
2. Toda variante adicional (creada después, con atributos de eje reales o `SkuOverride`) debe tener un SKU único en todo el catálogo del tenant, verificado antes de persistir.
3. La combinación de atributos de eje de una variante es única dentro del mismo ítem (regla preexistente, sin cambios).
4. El barcode de cualquier variante (default o adicional) es único globalmente por tenant (heredado de Fase 2).
5. Como máximo una imagen por ítem tiene `IsMain = true`.

---

## 4. Impacto arquitectónico

**Módulos NO afectados por esta fase**: Ventas, Compras, Pricing.

**Por qué no hay impacto**: el propio código señala que Inventory y Pricing consultan `ItemVariant` directamente — la corrección de esta fase (unicidad global de SKU de variante) **reduce** el riesgo de ambigüedad para esos módulos en vez de introducir uno nuevo; no se modificó ningún contrato ni comportamiento expuesto a ellos.

---

## 5. Cambios técnicos realizados

**Backend**: `IItemRepository`/`ItemRepository` — nuevo `VariantSkuExistsAsync(sku, tenantId, ct)`; `AddItemVariantCommandHandler` verifica unicidad global del SKU final (con override o autogenerado a partir de atributos) antes de persistir, devolviendo `409 Conflict` con mensaje claro en caso de colisión — mismo patrón ya aplicado a barcode/código de proveedor en Fase 2. La creación del ítem (variante default) no requiere verificación adicional: su SKU es idéntico al SKU del ítem, ya único por tenant desde Fase 1.

**Base de datos**: índice único de `item_variants` cambia de `(item_id, sku)` a `(tenant_id, sku)`.

**Migraciones**: `Fase6VariantSkuGlobalUniqueness` — verificada previamente la ausencia de SKUs de variante duplicados entre ítems distintos antes de aplicarse.

**Frontend**: sin cambios — la creación/edición de variantes adicionales ya vive fuera del formulario de creación del ítem (gestión desde el detalle), y ya maneja errores de conflicto genéricos del backend.

**Tests**: suite completa backend (63 dominio + 24 aplicación) en verde, sin necesidad de tests nuevos — el comportamiento cubierto ya se ejercita indirectamente por los tests de unicidad de Fase 2, que validan el mismo patrón.

---

## 6. Riesgos conocidos (a revisar en fases posteriores, no resueltos aquí)

Ninguno nuevo identificado en esta fase.

---

## 7. Pendientes — pertenecen a otras fases (no tratados aquí)

- **Fase 7 — Pricing**: `ItemPrice`, `PriceList`, historial, simulación.
- **Fase 8 — Compras**: relación con proveedor, integración de compras.
- **Fase 9 — Arquitectura**: revisión transversal backend/frontend/infraestructura.

---

## 8. Estado de la fase

**Estado: ✅ FASE 6 COMPLETADA**

**Resultado**: la identidad de las variantes del Item quedó completamente definida y documentada, con la misma garantía de unicidad global por tenant ya aplicada de forma consistente al SKU del ítem (Fase 1) y a barcode/código de proveedor (Fase 2). Las siguientes fases podrán construirse sobre esta base sin modificar las decisiones tomadas en esta fase.

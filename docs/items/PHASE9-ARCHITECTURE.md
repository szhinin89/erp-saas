# Items — Fase 9: Arquitectura (revisión transversal)

**Estado**: ✅ FASE 9 COMPLETADA
**Fecha de cierre**: 2026-07-02
**Nivel documental**: 3 (detalle técnico especializado, referenciado desde [`STATUS.md`](../../STATUS.md))

Última fase de la auditoría del módulo Items. Revisa transversalmente backend, frontend e infraestructura de las Fases 1-8 ya cerradas, sin reabrir ninguna decisión funcional.

---

## 1. Resumen de la fase

**Objetivo**: verificar consistencia arquitectónica transversal (duplicación de lógica, acoplamientos, cumplimiento de infraestructuras FROZEN) en todo lo construido durante la auditoría del módulo Items (Fases 1-8).

**Alcance**: backend (entidades, commands, queries, validators, repositories, EF configurations), frontend (formularios, componentes, servicios), infraestructura (índices, migraciones, tests).

---

## 2. Decisiones aprobadas

| # | Decisión | Estado |
|---|----------|--------|
| 1 | Corregir la duplicación de lógica de resolución de código de proveedor introducida en Fase 8 (`PurchaseDraftUseCases.cs`), extrayendo un helper compartido (`SupplierCodeResolver`), sin refactorizar el resto del bloque de construcción de línea ya duplicado antes de esta auditoría. | ✅ Aprobada e implementada |

---

## 3. Hallazgos de la revisión transversal

**Duplicación de lógica**: un solo hallazgo real, de autoría propia de esta auditoría (Fase 8) — corregido. El resto de los métodos repetitivos de `IItemRepository` (`ExistsBySkuAsync`, `BarcodeExistsAsync`, `SupplierCodeExistsAsync`, `VariantSkuExistsAsync`, `GetSupplierCodeAsync`) siguen el mismo patrón de verificación de unicidad pero targeting entidades distintas — repetición esperada del patrón, no duplicación de lógica de negocio.

**Acoplamientos**: ninguno indebido detectado. La única dependencia cruzada de módulo introducida en las 8 fases (Purchases → Items, vía `IItemRepository.GetSupplierCodeAsync`, Fase 8) respeta la dirección de dependencia declarada en `ERP_CORE_FREEZE.md`.

**Cumplimiento de infraestructuras FROZEN**: verificado explícitamente — Configuración Tributaria (Fase 3), Entity Tracking/`NewChildEntityTrackingInterceptor` (patrón de carga-vía-query respetado en todas las mutaciones de agregado con hijos nuevos), regla "no eliminar registros" (corregida en Fase 7, sin nuevas violaciones en el resto de fases).

---

## 4. Cambios técnicos realizados

**Backend**: `PurchaseDraftUseCases.cs` — nuevo `file static class SupplierCodeResolver` con método `ResolveAsync`, reutilizado por `CreatePurchaseDraftHandler` (vía `BuildLines`) y `UpdatePurchaseDraftHandler`, reemplazando la lógica de resolución duplicada.

**Frontend / Infraestructura**: sin cambios — la revisión no encontró hallazgos que requirieran modificación fuera del backend.

**Tests**: suite completa backend (69 dominio + 24 aplicación) en verde tras el cambio.

---

## 5. Estado final del módulo Items — resumen de las 9 fases

| Fase | Alcance | Estado |
|------|---------|--------|
| 1 | Información Base (SKU, Descripción, Tipo, Marca, Categoría, árbol dinámico) | ✅ [`PHASE1-ITEM-IDENTITY.md`](PHASE1-ITEM-IDENTITY.md) |
| 2 | Identificación (barcodes, códigos de proveedor) | ✅ [`PHASE2-ITEM-IDENTIFICATION.md`](PHASE2-ITEM-IDENTIFICATION.md) |
| 3 | Tributación (códigos SRI, campos reservados) | ✅ [`PHASE3-ITEM-TAXATION.md`](PHASE3-ITEM-TAXATION.md) |
| 4 | Comercial (precio inicial, descuento máximo) | ✅ [`PHASE4-ITEM-COMMERCIAL.md`](PHASE4-ITEM-COMMERCIAL.md) |
| 5 | Inventario y Venta | ✅ [`PHASE5-ITEM-INVENTORY-SALE.md`](PHASE5-ITEM-INVENTORY-SALE.md) |
| 6 | Variantes | ✅ [`PHASE6-ITEM-VARIANTS.md`](PHASE6-ITEM-VARIANTS.md) |
| 7 | Pricing (ciclo de vida de `ItemPrice`) | ✅ [`PHASE7-ITEM-PRICING.md`](PHASE7-ITEM-PRICING.md) |
| 8 | Compras (integración con proveedor) | ✅ [`PHASE8-ITEM-PURCHASES.md`](PHASE8-ITEM-PURCHASES.md) |
| 9 | Arquitectura (revisión transversal) | ✅ Este documento |

---

## 6. Riesgos conocidos acumulados (registro consolidado, no resueltos en esta auditoría)

- `CategoryNodeLevel` sigue sin enforcement estructural (Fase 1).
- `ItemCategoryNode.Reparent()` es código muerto sin detección de ciclos (Fase 1).
- Combinaciones sin sentido de negocio (p. ej. servicio con stock configurado) son posibles por diseño — flexibilidad priorizada explícitamente (Fase 5).
- Historial de precios vive como texto libre en auditoría, no estructurado para reportes (Fase 7).
- `ProductPicker.tsx` no prioriza visualmente por proveedor de la factura (Fase 8).
- `Item.Code.PurchaseCode` (legacy) permanece indefinidamente como fallback junto a `ItemSupplierCode` (Fase 8).
- El resto de la duplicación de `PurchaseDraftUseCases.cs` (bloque completo de construcción de línea entre Create/Update) permanece sin refactorizar — excede el alcance de "Items".

---

## 7. Estado de la fase

**Estado: ✅ FASE 9 COMPLETADA**

**Resultado**: la revisión transversal del módulo Items no encontró inconsistencias arquitectónicas significativas más allá de una duplicación menor de autoría propia, ya corregida. Las infraestructuras FROZEN del proyecto fueron respetadas en las 9 fases. **La auditoría completa del módulo Items (Fases 1-9) queda cerrada.**

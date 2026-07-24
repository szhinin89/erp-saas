# Items — Fase 8: Compras (integración con Items)

**Estado**: ✅ FASE 8 COMPLETADA
**Fecha de cierre**: 2026-07-02
**Nivel documental**: 3 (detalle técnico especializado, referenciado desde [`docs/STATUS.md`](../STATUS.md))

Este documento es la referencia oficial de las decisiones funcionales y técnicas de la Fase 8 del módulo Items. A diferencia de las fases anteriores, esta fase tocó también el módulo de Compras (Purchases), por ser el único consumidor real de la relación Item↔Proveedor. Se apoya en las Fases 1-7 ya cerradas y no las reabre.

---

## 1. Resumen de la fase

**Objetivo**: verificar que el módulo de Compras resuelva correctamente el código de proveedor de un ítem, usando la entidad correcta (`ItemSupplierCode`, Fase 2) en vez de un campo legacy paralelo sin relación a proveedor.

**Alcance funcional**: relación Item↔Proveedor consumida desde Compras, resolución del código de proveedor en el contexto de compra y en las líneas de factura de compra.

**Fuera de alcance de esta fase**: el resto del módulo de Compras no relacionado con Items (flujo de aprobación, retenciones, pagos, etc.).

---

## 2. Decisiones funcionales aprobadas

| # | Decisión | Estado |
|---|----------|--------|
| 1 | Compras **migra a consumir `ItemSupplierCode`** — al conocerse el proveedor de la factura, se resuelve el código específico de ese proveedor para el ítem (preferencia por el marcado `IsPrimary`), en vez del campo legacy `Item.Code.PurchaseCode`. | ✅ Aprobada e implementada |
| 2 | Si el ítem **no tiene** ningún `ItemSupplierCode` registrado para ese proveedor específico, se usa `Item.Code.PurchaseCode` como **fallback** — no queda vacío, preservando compatibilidad con ítems creados antes de esta integración. | ✅ Aprobada e implementada (decisión técnica dentro del alcance de la Opción A) |

---

## 3. Reglas de dominio (invariantes vigentes)

1. El código de proveedor mostrado/snapshoteado en una línea de factura de compra corresponde al proveedor real de esa factura, cuando existe un `ItemSupplierCode` para esa combinación item+proveedor.
2. Si no existe ese registro específico, se usa el código legacy del ítem (`Item.Code.PurchaseCode`) como respaldo — nunca queda sin código si el ítem tiene alguno de los dos disponibles.
3. `Item.SupplierCodes` se carga correctamente en todas las lecturas completas del agregado `Item` (corrección de un defecto preexistente, ver Sección 5).

---

## 4. Impacto arquitectónico

**Módulos afectados por esta fase** (a diferencia de fases anteriores): **Compras**, además de Items — es la única fase de esta auditoría que requirió tocar un módulo consumidor externo, porque la relación Item↔Proveedor solo tiene sentido de negocio en el contexto de una compra real.

**Módulos NO afectados**: Ventas, Inventario, Pricing.

**Por qué el cambio en Compras es seguro**: el nuevo parámetro `SupplierId` en `GetPurchaseItemContextQuery` es **opcional** (`Guid?`, default `null`) — cualquier llamada existente que no lo envíe sigue funcionando exactamente igual que antes (fallback automático al campo legacy). No se rompió ningún contrato existente.

---

## 5. Cambios técnicos realizados

**Corrección de un defecto preexistente (hallazgo de esta fase, no una decisión de negocio)**: `ItemRepository.GetByIdAsync`/`GetBySkuAsync`/`ResolveByAnyCodeAsync` **nunca cargaban `Item.SupplierCodes`** (`.Include()` faltante) — la colección de códigos de proveedor de un ítem, aunque se guardaba correctamente desde Fase 2, jamás podía leerse de vuelta a través de estos métodos. Corregido agregando el `.Include(x => x.SupplierCodes)` faltante en los tres.

**Backend**: `IItemRepository`/`ItemRepository` — nuevo `GetSupplierCodeAsync(itemId, supplierId, tenantId, ct)` (resuelve el código, con preferencia por `IsPrimary`); `GetPurchaseItemContextQuery`/`Handler` — nuevo parámetro opcional `SupplierId`, resolución vía `ItemSupplierCode` con fallback al campo legacy; `PurchaseDraftUseCases` (`CreatePurchaseDraftHandler` vía `BuildLines`, `UpdatePurchaseDraftHandler`) — mismo patrón de resolución con fallback en las 2 líneas donde antes se leía directamente `item.Code.PurchaseCode`.

**API**: `GET /api/v1/purchases/items/context` gana el query param opcional `supplierId`.

**Frontend**: `purchaseService.getItemContext()` acepta `supplierId` opcional; `usePurchasesPage.ts` (`fetchItemContext`) lo obtiene automáticamente del proveedor ya seleccionado en el formulario de la factura de compra (`getValues('supplierId')`), sin requerir cambios en los puntos de llamada existentes.

**Base de datos**: sin migración — no se modificó ningún esquema, solo consultas.

**Tests**: suite completa backend (69 dominio + 24 aplicación) en verde. Build limpio en backend y frontend (typecheck sin errores nuevos). Arranque de API verificado sin errores de resolución de dependencias.

---

## 6. Riesgos conocidos (a revisar en fases posteriores, no resueltos aquí)

- El `ProductPicker.tsx` (selector genérico de ítem en Compras) sigue sin filtrar ni priorizar visualmente por el proveedor de la factura en curso — la resolución del código correcto ya ocurre en el backend al cargar el contexto de la línea, pero la búsqueda inicial del ítem no indica al usuario cuáles ítems ya tienen código registrado para ese proveedor específico. Es una mejora de UX, no una inconsistencia de datos.
- `Item.Code.PurchaseCode` (legacy) permanece activo como fallback indefinidamente — no se estableció ninguna fecha ni condición para deprecarlo formalmente en favor exclusivo de `ItemSupplierCode`.

---

## 7. Pendientes — pertenecen a otras fases (no tratados aquí)

- **Fase 9 — Arquitectura**: revisión transversal backend/frontend/infraestructura del módulo Items completo.

---

## 8. Estado de la fase

**Estado: ✅ FASE 8 COMPLETADA**

**Resultado**: la relación Item↔Proveedor quedó correctamente conectada a su único consumidor real (Compras), incluyendo la corrección de un defecto preexistente que impedía leer los códigos de proveedor guardados desde Fase 2. La siguiente fase (Arquitectura) podrá construirse sobre esta base sin modificar las decisiones tomadas en esta fase.

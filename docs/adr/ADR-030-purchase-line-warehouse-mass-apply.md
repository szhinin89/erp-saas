# ADR-030: Bodega por Línea en Compras — Selector General como Aplicación Masiva

## Status

**Accepted.** 2026-07-28. Formaliza como regla de arquitectura permanente un comportamiento ya implementado en el frontend del módulo de Compras (`frontend/src/modules/purchases`). No introduce backend, dominio, DTOs, endpoints, cálculos tributarios/comerciales ni cambios de Item Matching — es exclusivamente una regla de orquestación de UI. Cualquier cambio a lo aquí decidido requiere una nueva ADR.

## Contexto

`PurchaseInvoiceDetail.WarehouseId` (`ERP.Domain/Modules/Purchases/Entities/PurchaseInvoiceDetail.cs`) ya es un campo **por línea**, nullable, independiente entre líneas de la misma `PurchaseInvoice` — esto no es nuevo, ya existía antes de esta ADR y no se modifica. El problema que esta ADR cierra era exclusivamente de UI: el formulario de Compras (`PurchasesPage.tsx`) exponía dos selectores de bodega —uno general (`globalWarehouseId`, a nivel de documento) y uno por línea— pero solo el selector por línea ejecutaba el flujo completo (asignar `warehouseId`, refrescar contexto del Item, recalcular stock/costos/indicadores vía `fetchItemContext`). El selector general solo escribía `globalWarehouseId` en el formulario, sin propagar nada a las líneas ni disparar ningún recálculo — dos caminos con comportamiento distinto para lo que el usuario percibe como la misma acción.

## Regla de negocio (inmutable)

Una compra puede ingresar productos a **múltiples bodegas simultáneamente**. Esta es una capacidad oficial y estratégica del ERP — soporta empresas con sucursales, centros de distribución, talleres y consignación — y constituye una ventaja funcional frente a ERPs que limitan una compra a una única bodega. **No debe eliminarse, simplificarse ni restringirse** sin una ADR nueva que la reemplace explícitamente.

```
Purchase
   ↓
PurchaseLine
   WarehouseId   ← pertenece conceptualmente a la línea, nunca al documento completo
```

La compra **no posee** una bodega obligatoria única. La unidad de almacenamiento es siempre la línea.

## Decisión

### El selector general es una operación de aplicación masiva, no de sincronización

El selector general (`globalWarehouseId`) **no representa** "la bodega del documento". Representa exclusivamente el comando "aplicar esta bodega a todas las líneas actuales, ahora". No existe, y queda expresamente prohibido introducir, ningún mecanismo que mantenga las líneas permanentemente sincronizadas con `globalWarehouseId` después de ese momento (sin `useEffect` ni watcher reactivo sobre el campo).

### Single Source of Truth — un único flujo de actualización

Toda modificación de `warehouseId` de una línea pasa exclusivamente por `updateLineWarehouse(key, warehouseId)` (`frontend/src/modules/purchases/hooks/usePurchasesPage.ts`):

```
Selector individual                    Selector general
        │                                      │
        ▼                                      ▼
updateLineWarehouse(key, wh)      applyGlobalWarehouse(wh)
        │                                      │
        │                          setValue('globalWarehouseId', wh)  ← única responsabilidad propia
        │                          foreach línea:
        │                             updateLineWarehouse(línea, wh) ─┐
        │                                                              │
        └──────────────────────────────────────────────────────────────┘
                                     │
                                     ▼
                    ├── actualizar warehouseId
                    ├── refrescar contexto del Item (fetchItemContext)
                    ├── recalcular stock / costos / indicadores (ya dentro de fetchItemContext)
                    └── cualquier lógica futura sobre "cambiar bodega de una línea"
```

`applyGlobalWarehouse` **no reimplementa** ninguna parte de ese flujo — su única responsabilidad propia es escribir el valor de conveniencia `globalWarehouseId` (que se sigue enviando al backend sin cambios, es un campo ya existente de `CreatePurchaseDraftCommand`/`UpdatePurchaseDraftCommand`) y delegar línea por línea en `updateLineWarehouse`.

### Prioridad de la línea individual

Después de una aplicación masiva, el usuario puede modificar cualquier línea individualmente y ese cambio **permanece** — nada vuelve a sobrescribirlo automáticamente, porque `applyGlobalWarehouse` corre una única vez, en el momento del clic, y no existe ningún watcher que la reejecute. Ejemplo:

```
applyGlobalWarehouse('Matriz')  →  L1=Matriz, L2=Matriz, L3=Matriz, L4=Matriz
Usuario cambia L3 → 'Norte'      →  L1=Matriz, L2=Matriz, L3=Norte, L4=Matriz   (correcto, se conserva)
```

### Compatibilidad futura

Cualquier regla nueva relacionada con bodega — validaciones, reservas, disponibilidad, lotes, series, ubicaciones, reglas contables, costos por bodega, impuestos dependientes de bodega, políticas de inventario — se incorpora **exclusivamente dentro de `updateLineWarehouse`**. Ninguna debe copiarse ni reimplementarse en `applyGlobalWarehouse` ni en ningún otro punto de entrada: al vivir en el flujo único, cualquier extensión queda disponible automáticamente para ambos selectores sin trabajo adicional.

## Componentes involucrados (frontend únicamente)

| Componente | Ubicación | Responsabilidad |
|---|---|---|
| `updateLineWarehouse(key, warehouseId)` | `frontend/src/modules/purchases/hooks/usePurchasesPage.ts` | Único flujo de actualización de bodega por línea — asigna `warehouseId` y dispara `fetchItemContext` si la línea tiene Item. |
| `applyGlobalWarehouse(warehouseId)` | `frontend/src/modules/purchases/hooks/usePurchasesPage.ts` | Aplicación masiva — guarda `globalWarehouseId` y delega en `updateLineWarehouse` por cada línea. Sin lógica propia de recálculo. |
| Selector individual (`<select className="pdl-line__wh-select">`) | `frontend/src/modules/purchases/pages/PurchasesPage.tsx` | `onChange` llama únicamente `ctx.updateLineWarehouse(...)`. |
| Selector general (`<select>` "Bodega Destino") | `frontend/src/modules/purchases/pages/PurchasesPage.tsx` | `onChange` llama únicamente `ctx.applyGlobalWarehouse(...)`. |
| `PurchaseInvoiceDetail.WarehouseId` | `ERP.Domain/Modules/Purchases/Entities/PurchaseInvoiceDetail.cs` | Ya existente, sin cambios — campo por línea, base de datos ya soporta múltiples bodegas por compra. |

## Prohibido

- Forzar que todas las líneas tengan siempre la misma bodega.
- Sincronizar permanentemente el selector general con las líneas (watcher/effect reactivo).
- Sobrescribir automáticamente una línea que el usuario ya modificó manualmente.
- Eliminar, simplificar o restringir la capacidad de múltiples bodegas por compra.
- Duplicar la lógica de actualización de bodega entre el selector general y el individual.
- Crear un segundo flujo de actualización de `warehouseId` en cualquier punto del código (asignación directa fuera de `updateLineWarehouse`).

## Validación

| Caso | Resultado esperado |
|---|---|
| Cambiar bodega en una línea | Comportamiento sin cambios — pasa por `updateLineWarehouse`. |
| Cambiar bodega general | Cada línea ejecuta `updateLineWarehouse` — mismo resultado que si el usuario la hubiera tocado una por una. |
| Modificar una línea después de una aplicación masiva | Se conserva su nuevo valor; no es sobrescrita. |
| Compra con múltiples bodegas (L1=Matriz, L2=Norte, L3=Taller, L4=Consignación) | Sin restricciones — cada línea persiste su propio `warehouseId` al guardar. |
| Limpiar la bodega general | Cada línea recibe `warehouseId = null` vía `updateLineWarehouse`, mismo flujo único. |

## Compatibilidad

| Principio/ADR | Compatibilidad verificada |
|---|---|
| DRY / Single Source of Truth | Sí — una sola función (`updateLineWarehouse`) posee la lógica; `applyGlobalWarehouse` solo itera y delega. |
| ADR-028 (Recepción XML → Compra) | Sin conflicto — no toca Item Matching, XML ni el flujo de creación de Compra. |
| ADR-021 (Pricing SSOT) / Inventario / Costeo | Sin conflicto — `fetchItemContext` es la misma función de lectura ya existente, sin cambios en su contrato ni en el backend. |
| Backend / Dominio / DTOs / API | Sin cambios — `PurchaseInvoiceDetail.WarehouseId` y `PurchaseLineInput.WarehouseId` ya eran por línea antes de esta ADR. |

## Entrega

- No se modificó backend, dominio, DTOs, endpoints, contratos, Item Matching, cálculos tributarios/comerciales ni inventario/costeo.
- Se consolidó la lógica de cambio de bodega en un único flujo (`updateLineWarehouse`), consumido por ambos selectores.
- La capacidad de compras con múltiples bodegas queda formalizada como regla de arquitectura permanente — no debe modificarse sin una ADR nueva.

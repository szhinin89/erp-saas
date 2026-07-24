# Items — Fase 4: Comercial del Item

**Estado**: ✅ FASE 4 COMPLETADA
**Fecha de cierre**: 2026-07-02
**Nivel documental**: 3 (detalle técnico especializado, referenciado desde [`docs/STATUS.md`](../STATUS.md))

Este documento es la referencia oficial de las decisiones funcionales y técnicas de la Fase 4 del módulo Items. Se apoya en las Fases 1-3 ya cerradas y no las reabre.

---

## 1. Resumen de la fase

**Objetivo**: verificar y cerrar el comportamiento comercial mínimo del ítem — precio inicial y descuento máximo — confirmando que el modelo de asignación a lista de precios sea consistente y que la presentación no asuma datos incorrectos (moneda).

**Alcance funcional**: precio inicial (`pricing.initialPrice`), asignación a lista de precios, descuento máximo (`ItemSaleConfig.MaxDiscountPercent`), moneda mostrada en el formulario.

**Fuera de alcance de esta fase**: Inventario, Variantes, Pricing avanzado (gestión de múltiples listas), Compras.

---

## 2. Decisiones funcionales aprobadas

| # | Decisión | Estado |
|---|----------|--------|
| 1 | El precio inicial se asigna siempre y exclusivamente a la **lista de precios predeterminada** (`PriceList.IsDefault = true`) del tenant. **No existe selector de lista de precios** en el formulario de creación del ítem. | ✅ Aprobada (confirma comportamiento ya implementado) |
| 2 | Si no existe ninguna lista predeterminada, el ítem se crea igual, **sin precio**, sin error. | ✅ Aprobada (sin cambio, ya vigente) |
| 3 | El precio inicial puede ser **cero** (`InitialPrice >= 0`). | ✅ Aprobada (sin cambio, ya vigente) |
| 4 | El descuento máximo (`MaxDiscountPercent`) es opcional, `0-100`. | ✅ Aprobada (sin cambio, ya vigente) |
| 5 | El formulario debe mostrar la **moneda real** de la lista predeterminada (`PriceList.CurrencyCode`) en lugar de un símbolo fijo — corregido en esta fase. | ✅ Aprobada e implementada |

---

## 3. Reglas de dominio (invariantes vigentes)

1. Una empresa tiene, como máximo, una lista de precios marcada como predeterminada (`PriceListUseCases`, `DefaultExistsAsync` — ya garantizado, sin cambios).
2. El precio inicial de un ítem se persiste como el primer `ItemPrice` contra la lista predeterminada, creado atómicamente junto con el ítem.
3. `ItemPrice.UnitPrice` nunca es negativo; `MinQuantity`, si se especifica, es mayor a cero.
4. `ItemSaleConfig.MaxDiscountPercent` está siempre entre 0 y 100 cuando tiene valor.
5. La asignación del ítem a listas de precios adicionales (no predeterminadas) es una operación del módulo Pricing, no de la creación del ítem.

---

## 4. Impacto arquitectónico

**Módulos NO afectados por esta fase**: Ventas, Compras, Inventario, Variantes.

**Por qué no hay impacto**: esta fase no modificó ningún contrato de backend (el único cambio fue de presentación en frontend); `ItemPrice`/`PriceList` y su consumo desde Ventas/Compras permanecen exactamente igual.

---

## 5. Cambios técnicos realizados

**Backend**: ninguno — la fase confirmó el comportamiento ya implementado sin requerir cambios de dominio, comando ni API.

**Frontend**: `PricingTab.tsx` — se reemplazó el símbolo `$` hardcodeado (en el input de precio inicial y en las 4 tarjetas de la simulación de precio) por el `CurrencyCode` real de la lista de precios predeterminada, obtenido vía `priceListService.list()` (ya existente, reutilizado sin cambios en el servicio). Fallback visual a "USD" únicamente si no existe lista predeterminada — no bloquea la captura del precio.

**Base de datos / Migraciones**: sin cambios.

**Tests**: sin cambios — no se tocó ningún contrato de backend; se verificó build y typecheck limpios.

---

## 6. Riesgos conocidos (a revisar en fases posteriores, no resueltos aquí)

- Si en el futuro se requiere que el ítem se cree con precio en más de una lista simultáneamente, o que el usuario elija explícitamente la lista al crear el ítem, esta fase documenta que **se decidió explícitamente no soportarlo** — cualquier cambio futuro en ese sentido debe revisar esta decisión, no asumir que fue un olvido.

---

## 7. Pendientes — pertenecen a otras fases (no tratados aquí)

- **Fase 5 — Inventario y Venta**: TracksStock, lotes, series, decimales, stock mínimo/máximo, disponibilidad POS/Web/Mobile.
- **Fase 6 — Variantes**: atributos, SKU de variante, barcode de variante, imágenes.
- **Fase 7 — Pricing**: gestión de múltiples listas, historial, simulación avanzada.
- **Fase 8 — Compras**: relación con proveedor, integración de compras.

---

## 8. Estado de la fase

**Estado: ✅ FASE 4 COMPLETADA**

**Resultado**: el comportamiento comercial mínimo del Item quedó completamente definido y documentado, con la presentación de moneda corregida para reflejar el dato real del dominio. Las siguientes fases podrán construirse sobre esta base sin modificar las decisiones tomadas en esta fase.

# Items — Fase 5: Inventario y Venta del Item

**Estado**: ✅ FASE 5 COMPLETADA
**Fecha de cierre**: 2026-07-02
**Nivel documental**: 3 (detalle técnico especializado, referenciado desde [`docs/STATUS.md`](../STATUS.md))

Este documento es la referencia oficial de las decisiones funcionales de la Fase 5 del módulo Items. Se apoya en las Fases 1-4 ya cerradas y no las reabre.

---

## 1. Resumen de la fase

**Objetivo**: verificar si la configuración de Inventario (`TracksStock`, lotes, series, decimales, stock mín/máx) y Venta (disponibilidad POS/Web/Mobile) debe depender del `ItemType` del ítem, y confirmar la consistencia de sus validaciones internas.

**Alcance funcional**: `ItemStockConfig` (`TracksStock`, `TracksLot`, `TracksSeries`, `AllowDecimalQty`, `AllowDecimalSale`, `MinStockQty`, `MaxStockQty`), `ItemSaleConfig` (`IsForSale`, `IsAvailableOnWeb/POS/Mobile`, `IsEcommerceActive`, `MaxDiscountPercent` — este último ya cerrado en Fase 4).

**Fuera de alcance de esta fase**: Variantes, Pricing avanzado, Compras.

---

## 2. Decisiones funcionales aprobadas

| # | Decisión | Estado |
|---|----------|--------|
| 1 | La configuración de Inventario y Venta es **completamente independiente** del `ItemType` — no existen valores por defecto ni restricciones condicionadas por tipo. Un ítem `Service`/`Digital` puede configurarse con `TracksStock`, lotes, series, stock mín/máx exactamente igual que un ítem `Physical`, sin ninguna diferencia de comportamiento. | ✅ Aprobada (confirma comportamiento ya implementado) |

---

## 3. Reglas de dominio (invariantes vigentes)

1. `MinStockQty`/`MaxStockQty`, si se especifican, nunca son negativos, y el mínimo nunca es mayor al máximo.
2. `TracksStock`, `TracksLot`, `TracksSeries`, `AllowDecimalQty`, `AllowDecimalSale` son banderas de configuración libres — su combinación no está restringida ni condicionada por ningún otro atributo del ítem, incluido `ItemType`.
3. `TracksStock` es consumido por Ventas (búsqueda de ítems para facturación) para decidir si mostrar bodega y stock disponible en la línea — es la única integración real verificada de estos campos fuera del propio módulo Items.
4. La disponibilidad de venta (`IsForSale`, `IsAvailableOnWeb/POS/Mobile`, `IsEcommerceActive`) es igualmente libre, sin condicionamiento por tipo.

---

## 4. Impacto arquitectónico

**Módulos NO afectados por esta fase**: Ventas, Compras, Inventario, Pricing, Variantes.

**Por qué no hay impacto**: esta fase no modificó ningún comportamiento — fue una auditoría de confirmación. `TracksStock` sigue siendo consumido por Ventas exactamente igual que antes.

---

## 5. Cambios técnicos realizados

Ninguno. Esta fase concluyó sin cambios de backend ni frontend — la auditoría confirmó que el comportamiento actual (independencia total de `ItemType`) es la decisión de negocio correcta.

---

## 6. Riesgos conocidos (a revisar en fases posteriores, no resueltos aquí)

- Es posible crear hoy un ítem `Service`/`Digital` con `TracksStock = true` y stock mínimo/máximo configurado — una combinación sin sentido de negocio evidente, pero permitida deliberadamente por la decisión de esta fase (máxima flexibilidad). Si en el futuro se detecta que esto genera datos inconsistentes en la práctica, debe revisarse esta decisión explícitamente, no asumir que fue un descuido.

---

## 7. Pendientes — pertenecen a otras fases (no tratados aquí)

- **Fase 6 — Variantes**: atributos, SKU de variante, barcode de variante, imágenes.
- **Fase 7 — Pricing**: gestión de múltiples listas, historial, simulación avanzada.
- **Fase 8 — Compras**: relación con proveedor, integración de compras.
- **Fase 9 — Arquitectura**: revisión transversal backend/frontend/infraestructura.

---

## 8. Estado de la fase

**Estado: ✅ FASE 5 COMPLETADA**

**Resultado**: la configuración de Inventario y Venta del Item quedó formalmente confirmada como independiente del `ItemType`, sin cambios de código. Las siguientes fases podrán construirse sobre esta base sin modificar la decisión tomada en esta fase.

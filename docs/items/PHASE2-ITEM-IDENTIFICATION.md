# Items — Fase 2: Identificación del Item

**Estado**: ✅ FASE 2 COMPLETADA
**Fecha de cierre**: 2026-07-02
**Nivel documental**: 3 (detalle técnico especializado, referenciado desde [`STATUS.md`](../../STATUS.md))

Este documento es la referencia oficial de las decisiones funcionales y técnicas de la Fase 2 del módulo Items. Se apoya en la base establecida por [`docs/items/PHASE1-ITEM-IDENTITY.md`](PHASE1-ITEM-IDENTITY.md) (Fase 1) y no la reabre.

---

## 1. Resumen de la fase

**Objetivo**: definir cómo se identifica físicamente un ítem en el mundo real (código de barras) y cómo se referencia dentro del catálogo de sus proveedores (código de proveedor).

**Alcance funcional**: códigos de barra (obligatoriedad, tipos, código principal, unicidad), códigos de proveedor (obligatoriedad de la sección y del proveedor por fila, unicidad, código principal).

**Fuera de alcance de esta fase**: Tributación, Comercial, Inventario, Variantes, Pricing, Compras.

---

## 2. Decisiones funcionales aprobadas

| # | Decisión | Estado |
|---|----------|--------|
| 1 | Todo `Item` debe registrar **al menos un código de barras** al crearse. | ✅ Aprobada |
| 2 | Un ítem puede tener **múltiples** códigos de barras. | ✅ Aprobada |
| 3 | Exactamente **uno** de los códigos de barras debe marcarse como **principal** (`IsPrimary`). | ✅ Aprobada |
| 4 | El tipo de código de barras (`BarcodeType`) es un catálogo fijo: `EAN13, EAN8, QR, Code128, Internal, Other`. | ✅ Aprobada (sin cambio, ya vigente) |
| 5 | El código de barras es **único globalmente por tenant** — `(tenant_id, code)` — independientemente del ítem. Reemplaza la unicidad anterior "por ítem". | ✅ Aprobada |
| 6 | La sección de **códigos de proveedor es opcional** a nivel de ítem (0..N registros, ninguno requerido). | ✅ Aprobada (sin cambio, ya vigente) |
| 7 | Si se registra un código de proveedor, el campo `SupplierId` es **obligatorio** dentro de esa entrada — no existe "código de proveedor sin proveedor". | ✅ Aprobada |
| 8 | Un ítem puede tener **códigos de distintos proveedores** simultáneamente. | ✅ Aprobada (sin cambio, ya vigente) |
| 9 | El código de proveedor es único por **`(tenant_id, supplier_id, code)`** — el mismo proveedor no puede repetir el mismo código en dos ítems distintos. Reemplaza la unicidad anterior "por ítem". | ✅ Aprobada |
| 10 | A lo sumo **un** código de proveedor puede marcarse como principal por ítem — no es obligatorio (a diferencia del barcode). | ✅ Aprobada (sin cambio, ya vigente) |

---

## 3. Reglas de dominio (invariantes)

1. Todo `Item` debe tener al menos un `ItemVariantBarcode` activo.
2. Exactamente un `ItemVariantBarcode` por ítem debe tener `IsPrimary = true`.
3. El código de barras (`ItemVariantBarcode.Code`) es único dentro del tenant — ningún otro ítem del mismo tenant puede tener el mismo código.
4. Un código de proveedor (`ItemSupplierCode`) siempre pertenece a un proveedor (`SupplierId` no puede ser vacío).
5. El mismo proveedor no puede repetir el mismo código de proveedor para ítems distintos del mismo tenant.
6. A lo sumo un `ItemSupplierCode` por ítem puede tener `IsPrimary = true` (no es obligatorio que exista uno).
7. Los códigos de identificación (barcodes y códigos de proveedor) forman parte del agregado `Item` — se persisten/mutan siempre a través de él (`ItemVariant.AddBarcode`, `Item.AddSupplierCode`), nunca de forma independiente.
8. El código de proveedor no requiere que el ítem tenga configuración tributaria, comercial ni de inventario definida — es independiente de esas fases.

---

## 4. Impacto arquitectónico

**Módulos NO afectados por esta fase**: Ventas, Compras, Inventario, Pricing.

**Por qué no hay impacto**: ni el barcode ni el código de proveedor son consumidos hoy como clave relacional por ningún otro módulo — las líneas de venta/compra referencian el ítem por `ItemId` (Guid). El endurecimiento de unicidad (de "por ítem" a "por tenant") es puramente una corrección de integridad de datos dentro del propio módulo Items; no cambia ningún contrato ni comportamiento expuesto a Ventas, Compras, Inventario o Pricing. Fase 8 (Compras, fuera de este alcance) se beneficiará de esta base para matching automático por código de proveedor, pero no se implementó nada de esa fase aquí.

---

## 5. Cambios técnicos realizados

**Backend**: nuevas verificaciones de unicidad global (`IItemRepository.BarcodeExistsAsync`, `SupplierCodeExistsAsync`) invocadas antes de crear el ítem y al agregar un barcode a un ítem existente; `ItemSupplierCode.Create()` exige `SupplierId` no vacío; `CreateItemCommandValidator` exige `SupplierId` en cada entrada de la lista de códigos de proveedor.

**Frontend**: el selector de proveedor dentro de cada fila de "código de proveedor" pasa a ser obligatorio, con mensaje de validación visible.

**Base de datos**: índice único de `item_variant_barcodes` cambia de `(item_id, code)` a `(tenant_id, code)`; índice único de `item_supplier_codes` cambia de `(item_id, supplier_id, code)` a `(tenant_id, supplier_id, code)`; columna `supplier_id` pasa de nullable a `NOT NULL`.

**Migraciones**: `Fase2ItemIdentificationGlobalUniqueness` — verificada previamente la ausencia de barcodes duplicados entre ítems, códigos de proveedor duplicados entre ítems, y filas con `supplier_id` nulo, antes de aplicarse.

**Validaciones**: unicidad global de barcode (creación y edición); unicidad global de código de proveedor (creación); proveedor obligatorio por entrada de código de proveedor.

**Tests**: tests de dominio de `ItemSupplierCode` actualizados para la nueva firma (`SupplierId` obligatorio); tests de validador ampliados con el caso "código de proveedor sin proveedor es inválido". Suite completa (63 dominio + 24 aplicación) en verde. Arranque de API verificado sin errores de resolución de dependencias.

---

## 6. Riesgos conocidos (a revisar en fases posteriores, no resueltos aquí)

- **Política definitiva de barcode principal**: hoy "principal" no tiene ningún efecto funcional documentado más allá de existir como marca (p. ej. no se ha definido si el barcode principal es el que se imprime en etiquetas, el que se muestra por defecto en reportes, o el que se prioriza en escaneo ambiguo). Queda para una fase de consumo (POS/Inventario) definir su uso real.
- **Comportamiento ante desactivación**: si se desactiva (`DisableBarcode`) el barcode marcado como principal, no hay hoy una regla que reasigne automáticamente otro código como principal — el ítem podría quedar temporalmente sin barcode principal activo. No se resolvió en esta fase por no haber sido solicitado.
- **Selección automática del código por defecto**: no existe una regla que determine qué código de proveedor "principal" se usa por defecto en un flujo de compras (Fase 8) cuando hay más de un proveedor con código registrado para el mismo ítem.
- **Reutilización de códigos históricos**: si un ítem se deshabilita y su barcode/código de proveedor queda con `IsActive = false`, no está definido si ese código puede reasignarse a otro ítem nuevo, dado que los índices únicos actuales no filtran por `IsActive` (ver `BarcodeExistsAsync`/`SupplierCodeExistsAsync`, que sí filtran por activo en la verificación de aplicación, pero el índice de BD es incondicional). Esto es una asimetría entre la regla de aplicación y la restricción física que debe revisarse si llega a ser un caso real de negocio.

---

## 7. Pendientes — pertenecen a otras fases (no tratados aquí)

- **Fase 3 — Tributación**: IVA venta/compra, ICE, catálogos SRI.
- **Fase 4 — Comercial**: precio inicial, lista de precios, descuento máximo.
- **Fase 5 — Inventario y Venta**: TracksStock, lotes, series, decimales, stock mínimo/máximo, disponibilidad POS/Web/Mobile.
- **Fase 6 — Variantes**: atributos, SKU de variante, barcode de variante (más allá de la variante default), imágenes.
- **Fase 7 — Pricing**: `ItemPrice`, `PriceList`, historial, simulación.
- **Fase 8 — Compras**: relación con proveedor, integración de compras, matching automático por código de proveedor.

---

## 8. Estado de la fase

**Estado: ✅ FASE 2 COMPLETADA**

**Resultado**: la identificación del Item quedó completamente definida y documentada. Las siguientes fases podrán utilizar estas reglas sin modificar las decisiones tomadas en esta fase.

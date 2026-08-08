# Items — Fase 3: Tributación del Item

**Estado**: ✅ FASE 3 COMPLETADA
**Fecha de cierre**: 2026-07-02
**Nivel documental**: 3 (detalle técnico especializado, referenciado desde [`STATUS.md`](../../STATUS.md))

Este documento es la referencia oficial de las decisiones funcionales y técnicas de la Fase 3 del módulo Items. Se apoya en la base establecida por [`docs/items/PHASE1-ITEM-IDENTITY.md`](PHASE1-ITEM-IDENTITY.md) y [`docs/items/PHASE2-ITEM-IDENTIFICATION.md`](PHASE2-ITEM-IDENTIFICATION.md) y no las reabre.

---

## 1. Resumen de la fase

**Objetivo**: verificar que la configuración tributaria del ítem (`ItemTaxConfig`) esté completamente respaldada por comportamiento real del dominio, y retirar del flujo funcional cualquier campo sin soporte funcional actual.

**Alcance funcional**: `SaleVatCode`, `PurchaseVatCode`, `ExciseTaxCode`, campos de cuenta contable (`VatAccountId`, `PurchaseVatAccountId`, `ExciseAccountId`), `SriServiceCode`.

**Fuera de alcance de esta fase**: Comercial, Inventario, Variantes, Pricing, Compras.

---

## 2. Decisiones funcionales aprobadas

| # | Decisión | Estado |
|---|----------|--------|
| 1 | Los códigos tributarios (`SaleVatCode`, `PurchaseVatCode`, `ExciseTaxCode`) continúan siendo la **única fuente de verdad** — sin cambios respecto al rediseño previo. | ✅ Aprobada (sin cambio) |
| 2 | **No se reintroducen** flags booleanos de impuesto. | ✅ Aprobada (sin cambio) |
| 3 | **No se almacenan porcentajes** en el ítem — se resuelven en tiempo real vía `ISriCatalogResolver`/`ISriTaxResolver` contra los catálogos SRI. | ✅ Aprobada (sin cambio) |
| 4 | Los campos de cuenta contable (`VatAccountId`, `PurchaseVatAccountId`, `ExciseAccountId`) **se retiran del contrato público** del módulo Items — ya no forman parte de `CreateItemCommand`, `UpdateItemCommand` ni `ItemTaxConfigDto`. Permanecen únicamente en el dominio interno (`ItemTaxConfig`) por compatibilidad, sin ningún consumidor. | ✅ Aprobada |
| 5 | `SriServiceCode` se **retira del formulario** de creación/edición de Items (frontend) por no tener catálogo de respaldo ni caso de uso real hoy. Se conserva en el dominio, en el comando backend (como campo opcional ya existente) y en el DTO de lectura, documentado como reservado. | ✅ Aprobada |

---

## 3. Reglas de dominio (invariantes vigentes)

1. `SaleVatCode`, `PurchaseVatCode` y `ExciseTaxCode` son opcionales — `null` significa "no aplica"; ningún valor por defecto se asume.
2. El porcentaje de cada impuesto se resuelve siempre contra el catálogo SRI vigente (`sri_vat_rates`, `sri_ice_rates`), nunca se persiste en el ítem.
3. Ningún módulo de negocio implementa lógica tributaria propia — todo cálculo pasa por `ISriTaxResolver` (backend) / `sriLookupService` (frontend), sin excepción.
4. Los campos `VatAccountId`/`PurchaseVatAccountId`/`ExciseAccountId` no tienen ningún efecto funcional en el sistema actual — no se exponen a través de la API pública del módulo Items.
5. `SriServiceCode` no se captura desde el formulario de creación/edición de Items — su presencia en el dominio y en el DTO de lectura es exclusivamente para preservar compatibilidad con cualquier valor ya persistido.

---

## 4. Impacto arquitectónico

**Módulos NO afectados por esta fase**: Ventas, Compras, Inventario, Pricing.

**Por qué no hay impacto**: verificado explícitamente — ningún archivo de Ventas, Compras o Facturación Electrónica referencia `VatAccountId`, `PurchaseVatAccountId` ni `ExciseAccountId` (búsqueda exhaustiva en el backend: únicamente aparecen en los propios archivos del módulo Items y en migraciones/snapshots históricos). Los 4 puntos de consumo de impuestos en Ventas/Compras (`SalesDraftUseCases`, `PurchaseDraftUseCases`, `GetPurchaseItemContextQueryHandler`, `InvoiceItemSearchRepository`) ya usan exclusivamente `SaleVatCode`/`PurchaseVatCode`/`ExciseTaxCode` mediante `ISriTaxResolver`, sin cambios en esta fase. La Infraestructura Tributaria CLOSED (`ISriTaxResolver`/`ISriCatalogResolver`, catálogos `sri_vat_rates`/`sri_ice_rates`) permanece intacta.

---

## 5. Cambios técnicos realizados

**Backend**: `CreateItemCommand`/`UpdateItemCommand` pierden los parámetros `VatAccountId`/`PurchaseVatAccountId`/`ExciseAccountId`; sus handlers pasan `null` (creación) o preservan el valor interno existente del ítem (edición, para no perder compatibilidad con datos previos) al construir `ItemTaxConfig`; `ItemTaxConfigDto` pierde los 3 campos de cuenta; `ItemMappingService.MapTaxConfig` ajustado. El VO de dominio `ItemTaxConfig` **no se modificó** (se conserva su firma completa) por ser parte de la Infraestructura Tributaria CLOSED.

**Frontend**: `TaxConfigTab.tsx` pierde el campo de "Cuenta contable IVA venta" y la sección "SRI" (`sriServiceCode`) por completo — sin placeholders, sin inputs ocultos, sin controles deshabilitados. `createItemSchema.ts` pierde `vatAccountId`/`purchaseVatAccountId`/`exciseAccountId`/`sriServiceCode` de `taxConfigSchema`. `itemService.ts` y `types/items.ts` ajustados en consecuencia.

**Base de datos**: sin cambios — no se modificó ninguna columna ni se generó migración; los datos existentes en `vat_account_id`/`purchase_vat_account_id`/`excise_account_id`/`sri_service_code` permanecen intactos en la base de datos.

**API**: `POST/PUT /api/v1/items` ya no aceptan ni devuelven `vatAccountId`/`purchaseVatAccountId`/`exciseAccountId` en el cuerpo de tributación; `sriServiceCode` se mantiene como campo opcional de escritura (compatibilidad, sin uso desde el formulario) y de lectura (reservado).

**Tests**: suite completa backend (63 dominio + 24 aplicación) en verde, sin necesidad de cambios adicionales en tests — ningún test existente ejercitaba los campos retirados del contrato.

**Verificaciones realizadas**: build completo de `ERP.API` sin errores; búsqueda exhaustiva confirmando cero referencias rotas fuera del módulo Items; typecheck de frontend sin errores nuevos en los archivos modificados.

---

## 6. Infraestructura reservada

Existen elementos reservados para futuras integraciones (por ejemplo, Contabilidad), pero **no forman parte del comportamiento funcional actual del ERP**:

- **`VatAccountId`, `PurchaseVatAccountId`, `ExciseAccountId`** (`ItemTaxConfig`, dominio y columnas de base de datos): sin catálogo de cuentas, sin módulo de Contabilidad, sin validación, sin consumidor. Permanecen en el modelo interno únicamente para no perder compatibilidad con datos que pudieran existir, pero no son alcanzables desde ningún flujo de creación/edición de Items ni desde ningún otro módulo.
- **`SriServiceCode`** (`ItemTaxConfig`): sin catálogo oficial consumido por el ERP, sin validación de formato contra una fuente real. Se mantiene accesible vía API (compatibilidad) y visible en la vista de detalle del ítem si ya tiene un valor, pero no se captura desde el formulario.

Esto permitirá reutilizarlos en el futuro (cuando exista un módulo de Contabilidad real o un catálogo SRI de tipo de servicio) sin generar confusión en el presente — ningún usuario ve hoy un campo sin comportamiento real detrás.

---

## 7. Pendientes — pertenecen a otras fases (no tratados aquí)

- **Fase 4 — Comercial**: precio inicial, lista de precios, descuento máximo.
- **Fase 5 — Inventario y Venta**: TracksStock, lotes, series, decimales, stock mínimo/máximo, disponibilidad POS/Web/Mobile.
- **Fase 6 — Variantes**: atributos, SKU de variante, barcode de variante, imágenes.
- **Fase 7 — Pricing**: `ItemPrice`, `PriceList`, historial, simulación.
- **Fase 8 — Compras**: relación con proveedor, integración de compras.

---

## 8. Informe final

**Archivos modificados**:
- Backend: `CreateItemCommand.cs`, `CreateItemCommandHandler.cs`, `UpdateItemCommand.cs`, `UpdateItemCommandHandler.cs`, `ItemDtos.cs`, `ItemMappingService.cs`.
- Frontend: `createItemSchema.ts`, `TaxConfigTab.tsx`, `itemService.ts`, `types/items.ts`, `ItemFormTabs.tsx`.

**Reglas nuevas**: los campos de cuenta contable no son alcanzables desde la API pública del módulo Items; `SriServiceCode` no se captura desde el formulario.

**Reglas eliminadas**: ninguna regla tributaria de negocio — solo se retiró exposición de campos sin soporte funcional.

**Riesgos eliminados**: exposición de un campo de texto libre "UUID cuenta" sin validación ni contexto en el formulario; captura de un código SRI de servicio sin catálogo de respaldo.

**Riesgos pendientes**: ninguno nuevo — los campos reservados quedan documentados en la Sección 6, a resolver únicamente cuando exista el módulo de Contabilidad o el catálogo SRI correspondiente.

**Compatibilidad verificada**: cero referencias rotas en Ventas, Compras, Inventario, Pricing; los 4 consumidores de impuestos en Ventas/Compras siguen resolviendo tarifas exclusivamente vía `ISriTaxResolver` con código SRI.

**Tests ejecutados**: 63 tests de dominio + 24 de aplicación, todos en verde.

**Estado de migraciones**: sin migración nueva — no hubo cambio de esquema de base de datos en esta fase.

**Estado de la API**: `POST/PUT /api/v1/items` ya no exponen los 3 campos de cuenta contable; `GET /api/v1/items/{id}` tampoco los devuelve.

**Estado del frontend**: formulario de Items sin campos de cuenta contable ni código de servicio SRI; typecheck limpio en los archivos modificados.

---

## 9. Estado de la fase

**Estado: ✅ FASE 3 COMPLETADA**

**Resultado**: la infraestructura tributaria del módulo Items quedó completamente alineada con la arquitectura del ERP. Las inconsistencias detectadas durante la auditoría fueron eliminadas o documentadas. No existen funcionalidades tributarias expuestas al usuario que carezcan de soporte funcional dentro del sistema.

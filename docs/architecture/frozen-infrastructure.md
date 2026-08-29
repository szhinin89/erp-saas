# Infraestructuras CLOSED (INMUTABLE)

> Toda infraestructura clasificada como **CLOSED** forma parte de la **Baseline Arquitectónica del ERP**. Ningún cambio funcional podrá modificar su comportamiento sin un nuevo ADR, evidencia técnica, pruebas automatizadas y revisión de compatibilidad hacia atrás.

Esta regla rige a **todas** las infraestructuras transversales declaradas CLOSED en `STATUS.md` (tabla "Módulos FROZEN"), incluyendo —sin limitarse a— las listadas en este documento. Aplica también a toda infraestructura futura que se declare CLOSED bajo el mismo proceso.

Implicaciones:

- **Ningún módulo de negocio** puede alterar, sortear o reimplementar el comportamiento de una infraestructura CLOSED para resolver una necesidad puntual.
- Un cambio de comportamiento solo es válido si viene acompañado de: (1) una nueva ADR que documente contexto, alternativas y decisión; (2) evidencia técnica (tests, métricas, validación end-to-end); (3) pruebas automatizadas que cubran el nuevo comportamiento; (4) revisión explícita de compatibilidad hacia atrás con los consumidores existentes.
- Los gates CI-bloqueantes asociados a cada infraestructura CLOSED (p. ej. `SEQ-GATE-01..04`, `ATT-GATE-01`) son el mecanismo de cumplimiento automático de esta regla — no deben relajarse ni desactivarse para acomodar una excepción puntual.
- Un agente o desarrollador que detecte una necesidad de cambio sobre una infraestructura CLOSED debe tratarlo como una decisión arquitectónica formal, no como una corrección de código ordinaria.

Infraestructuras CLOSED con contrato/enforcement propio ya documentado en otros archivos canónicos de `docs/architecture/`: [audit-infrastructure.md](./audit-infrastructure.md) (Entity Audit), [visual-messages.md](./visual-messages.md) (Mensajes Visuales, ADR-018), [modal-standard.md](./modal-standard.md) (`ZHModal`). Las siguientes se documentan íntegramente aquí.

---

## Secuencias Documentales

Decisión arquitectónica congelada 2026-06-29. ADR: [`docs/decisions/ADR-019-document-sequence-infrastructure.md`](../decisions/ADR-019-document-sequence-infrastructure.md).

Infraestructura transversal del ERP. Asigna numeración SRI (`000000001`…`999999999`) por punto de emisión y tipo de comprobante bajo concurrencia garantizada.

### API pública congelada

La **única operación autorizada** para obtener un consecutivo documental es:

```csharp
IDocumentSequenceRepository.CaptureNextAsync(tenantId, companyId, emissionPointId, docTypeCode, ct)
```

Esta llamada es atómica (advisory lock + transacción propia), concurrentemente segura y crea la fila on-demand si no existe.

### Prohibido en todo el sistema

- Llamar directamente a `DocumentSequence.CaptureAndIncrement()` desde cualquier handler o servicio.
- Leer `CurrentSeq` y luego escribirlo fuera del repositorio oficial.
- Emitir SQL raw de escritura sobre `document_sequence` fuera de `DocumentSequenceRepository`.
- Llamar a `IDocumentSequenceRepository.GetForUpdateAsync()` desde capa Application (patrón obsoleto).
- Implementar lógica propia de numeración en cualquier módulo del ERP.
- Resetear o decrementar `current_seq` directamente en la BD.

### Reglas de evolución

- Todo nuevo tipo documental SRI se incorpora sin cambio de modelo: `CaptureNextAsync(…, "04", …)`.
- Cualquier cambio en estrategia de concurrencia requiere nueva ADR + repetir suite de pruebas con PostgreSQL real.
- Los 4 architecture gates CI-bloqueantes (`SEQ-GATE-01..04` en `ERP.Infrastructure.Tests`) garantizan que ningún módulo viole esta regla automáticamente.

### Componentes congelados

| Componente | Ubicación | Estado |
|---|---|---|
| `DocumentSequence` (entidad) | `ERP.Domain/Modules/Company/Entities/` | FROZEN |
| `IDocumentSequenceRepository` (interfaz) | `ERP.Domain/Modules/Company/Interfaces/` | FROZEN |
| `DocumentSequenceRepository` (impl.) | `ERP.Infrastructure/Persistence/Repositories/` | FROZEN |
| `DocumentSequenceConfiguration` (EF) | `ERP.Infrastructure/Persistence/Configurations/Company/` | FROZEN |
| Gates CI | `ERP.Infrastructure.Tests/Persistence/DocumentSequenceExclusivityTests.cs` | FROZEN |
| Suite concurrente | `ERP.API.Tests/Integration/DocumentSequenceConcurrencyTests.cs` | FROZEN |

---

## Entity Tracking / Change Tracking

Decisión arquitectónica congelada 2026-06-30. ADR: [`docs/decisions/ADR-020-entity-tracking-infrastructure.md`](../decisions/ADR-020-entity-tracking-infrastructure.md).

Infraestructura transversal del ERP. Corrige automáticamente una clasificación errónea de EF Core: una entidad hija **nueva**, con clave generada por dominio (`Guid.NewGuid()` en factory `Create()`), agregada a la colección de navegación de un agregado **ya trackeado** (p. ej. desde un domain event handler, entre dos `SaveChangesAsync`), es descubierta recién por `DetectChanges()` y queda mal clasificada como `Modified` con `OriginalValue == CurrentValue` en todas sus propiedades. El `UPDATE` no-op resultante afecta 0 filas → `DbUpdateConcurrencyException`.

### Propósito

Garantizar que cualquier módulo del ERP que agregue hijos nuevos a un agregado ya trackeado quede protegido automáticamente, sin requerir cambios en sus propios handlers ni reimplementar lógica de tracking.

### Componentes congelados

| Componente | Ubicación | Estado |
|---|---|---|
| `NewChildEntityTrackingInterceptor` (`ISaveChangesInterceptor`) | `ERP.Infrastructure/Persistence/Interceptors/` | FROZEN |
| `ErpDbContext.WasTrackedFromQuery` + suscripción `ChangeTracker.Tracked` | `ERP.Infrastructure/Persistence/ErpDbContext.cs` | FROZEN |
| Registro DI (`AddInterceptors`) | `ERP.Infrastructure/DependencyInjection.cs` | FROZEN |
| Gate CI | `ERP.Infrastructure.Tests/Persistence/NewChildEntityTrackingArchitectureTests.cs` (`ATT-GATE-01`) | FROZEN |
| Suite de integración (PostgreSQL real) | `ERP.Infrastructure.Tests/Persistence/NewChildEntityTrackingInterceptorTests.cs` | FROZEN |

### API pública / comportamiento congelado

La **única infraestructura autorizada** para corregir una clasificación ambigua del `ChangeTracker` es `NewChildEntityTrackingInterceptor`. Su regla de decisión:

1. Entidad `Modified` sin diferencia real de valores **y** nunca materializada por una query en este `DbContext` → se corrige a `Added` (firma inequívoca de entidad nueva).
2. Entidad `Modified` sin diferencia real de valores pero **sí** materializada por una query (combinación anómala) → lanza `InvalidOperationException` explícita. No se adivina ni se autocorrige.

### Regla arquitectónica permanente

> Ningún agregado existente podrá ser reatachado mediante `DbSet.Attach()`, `DbSet.Update()` o mecanismos equivalentes sin haber sido previamente cargado mediante una consulta del mismo `DbContext`. Toda modificación de un agregado existente deberá iniciarse desde una entidad obtenida mediante el repositorio correspondiente. La infraestructura de persistencia asume este invariante y lo protege mediante `ATT-GATE-01` y la validación interna del `ISaveChangesInterceptor`. Si este invariante se viola, la infraestructura deberá fallar explícitamente mediante una excepción en lugar de intentar corregir automáticamente el estado del `ChangeTracker`. Esta regla debe considerarse permanente.

### Prohibido en todo el sistema

- Mutar manualmente `EntityState` de una entrada del `ChangeTracker` como mecanismo de negocio en cualquier handler o servicio.
- Llamar a `DbSet<T>.Attach()`/`DbSet<T>.Update()` directo sobre una entidad detached con ID real fuera de la lista blanca cerrada de `ATT-GATE-01` (`PaymentTermRepository.cs`, `PaymentMethodRepository.cs`, `SriSettingsRepository.cs`, `ItemTypeRepository.cs` — catálogos sin colecciones de navegación hijas).
- Implementar lógica propia de corrección de tracking en cualquier módulo del ERP.
- Reatachar un agregado sin haberlo cargado antes con una query en el `DbContext` activo.

### Reglas de evolución

- Cualquier necesidad real de reatachar un agregado sin pasar por query previa requiere ampliar explícitamente la lista blanca de `ATT-GATE-01`, justificando que la entidad no tiene colecciones de navegación hijas expuestas al patrón de fixup.
- Cualquier cambio en la señal `WasTrackedFromQuery`, la condición de clasificación o la estrategia fail-fast requiere nueva ADR + repetir la suite de integración con PostgreSQL real.
- El gate CI-bloqueante (`ATT-GATE-01` en `ERP.Infrastructure.Tests`) garantiza que ningún módulo viole la regla de reatachamiento automáticamente.

---

## Configuración Tributaria

Decisión arquitectónica congelada 2026-07-01.

Infraestructura transversal del ERP. Define la fuente única de verdad para toda configuración tributaria y prohíbe que los documentos transaccionales generen, asuman o sustituyan impuestos.

### Reglas permanentes

**Regla 1 — Fuente de verdad tributaria**
Toda configuración tributaria pertenece exclusivamente a la entidad de negocio correspondiente (ítem, servicio o cualquier entidad master futura). El documento transaccional solo consume — nunca define — dicha configuración.

**Regla 2 — Los documentos no generan impuestos**
Los documentos transaccionales (Facturas, Notas de Crédito/Débito, Cotizaciones, Órdenes y cualquier documento futuro) únicamente consumen la configuración tributaria del ítem. Queda prohibido asumir IVA, asumir ICE, asumir códigos tributarios o generar impuestos por defecto.

**Regla 3 — Error de configuración**
Si una entidad obligatoria carece de configuración tributaria, el sistema la trata como un error de configuración del maestro. Nunca inventa valores, usa fallbacks, completa automáticamente ni sustituye información tributaria.

**Regla 4 — Motor único de cálculo**
Todo cálculo tributario usa exclusivamente: (1) configuración tributaria del ítem, (2) catálogos oficiales SRI (vía `ISriTaxResolver` en backend, `sriLookupService.*Rates()` en frontend), (3) reglas del dominio (`SalesInvoiceDetail.ApplyTaxes()`). Nunca reglas locales del módulo.

**Regla 5 — Catálogos**
Todos los códigos tributarios provienen exclusivamente de los catálogos oficiales (`sri_vat_rates`, `sri_ice_rates`). Nunca listas hardcodeadas ni catálogos reconstruidos manualmente en ninguna capa.

### Estado actual — componentes alineados

| Componente | Archivo | Estado |
|---|---|---|
| Fuente de verdad IVA | `item.TaxConfig.SaleVatCode` → `SalesLineInput.VatCode` | FROZEN |
| Fuente de verdad ICE | `item.TaxConfig.ExciseTaxCode` → `SalesLineInput.IceCode` | FROZEN |
| Cálculo IVA + ICE backend | `SalesTaxHelper.ResolveTaxesAsync()` vía `ISriTaxResolver` | FROZEN |
| Cálculo IVA + ICE frontend | `salesCalc.ts` vía `vatRatesMap` + `iceRatesMap` de catálogo | FROZEN |
| Validación obligatoriedad | `FluentValidation VatCode.NotEmpty()` + Zod `.min(1)` | FROZEN |
| Prohibición fallback tributario | `vatCode: saleVatCode ?? ''` — sin `'10'` ni `purchaseVatCode` | FROZEN |
| Catálogos SRI | `GET /api/v1/catalog/sri-vat-rates`, `/sri-ice-rates` | FROZEN |

### Prohibido en todo el sistema

- Usar cualquier código tributario literal (`'10'`, `'0'`, `'8'`, etc.) como valor por defecto en documentos transaccionales.
- Usar `purchaseVatCode` como fallback en documentos de venta.
- Asignar `vatCode` o `iceCode` desde el módulo de ventas sin que provengan del ítem.
- Crear `DefaultVatCode`, `DefaultIceCode` o cualquier configuración tributaria a nivel empresa.
- Resolver impuestos en módulos de negocio mediante reglas locales en lugar de `ISriTaxResolver` / `sriLookupService.*Rates()`.
- Crear listas de catálogos tributarios hardcodeadas en ninguna capa.

### Reglas de evolución

- Cualquier nuevo impuesto requiere actualizar **únicamente** `ISriTaxResolver` (backend) y los catálogos SRI (`sri_*_rates`), sin tocar los documentos transaccionales.
- Un módulo nuevo que calcule impuestos debe consumir `ISriTaxResolver` en backend y `sriLookupService.*Rates()` en frontend — nunca implementar lógica tributaria propia.
- Cualquier cambio en las reglas de obligatoriedad (`VatCode.NotEmpty()`) requiere análisis de compatibilidad hacia atrás.
- El mensaje de validación Zod `'El producto no tiene código IVA de venta configurado. Verifique el maestro de productos.'` es parte de esta infraestructura y no se modifica sin justificación formal.

### Excepción acotada — `CompanySpecialTaxResponsibility` (ADR-032)

[ADR-032](../decisions/ADR-032-tax-line-ssot-ice-irbpnr.md) (2026-08-29) abre una excepción **estricta y acotada** a la prohibición "cualquier configuración tributaria a nivel empresa" de esta infraestructura, exclusivamente para la responsabilidad de aplicar ICE/IRBPNR en ventas:

- La regla FROZEN de Configuración Tributaria **sigue vigente** en todo lo demás — sigue prohibido definir códigos, tarifas, porcentajes o catálogos tributarios a nivel empresa.
- `CompanySpecialTaxResponsibility` **no es** un catálogo tributario por empresa: no tiene `TaxCatalogCode`, no tiene tarifa, no reemplaza ni duplica `SriIceRate`/`SriIrbpnrRate`. Es exclusivamente un booleano (`IsResponsibleOnSales`) por `(CompanyId, SriTaxCategoryCode)` que responde "¿esta empresa es sujeto pasivo de este impuesto especial al vender?" — una realidad fiscal real del SRI (fabricante/importador vs. revendedor), no un dato tributario del documento.
- **No participa en Compras** — solo condiciona el cálculo en Ventas, siempre en conjunto (`AND`) con `ItemSpecialTaxConfiguration` del ítem, nunca como sustituto de la configuración del ítem ni como fallback de código/tarifa.
- Detalle completo del diseño y las reglas de coherencia compra-venta: ADR-032 §3.4/§5.1.

---

## Tipos de Ítem (Item Types)

Decisión arquitectónica congelada 2026-07-04. Reemplaza el enum C# fijo `ItemType { Physical, Service, Digital, Kit, Bundle }` (eliminado) por un catálogo tenant-editable.

### Reglas permanentes

**Regla 1 — Catálogo, no enum**
`ItemTypeDefinition` (`Id, TenantId, Code, Name, SortOrder, IsActive`) es la única fuente de verdad de los tipos de ítem. Cada tenant administra su propio catálogo (crear/editar/activar/desactivar/ordenar) vía `api/v1/item-types`, sin tocar código para agregar un tipo nuevo.

**Regla 2 — Relación por Id, nunca por texto**
`items.item_type_id (uuid)` es la única columna de relación, con FK física a `item_types.id`. Prohibido persistir o comparar por `Code`/`Name` como si fueran el identificador de la relación.

**Regla 3 — Clasificación pura, sin comportamiento**
`ItemTypeDefinition` no controla inventario, venta, compra ni ningún comportamiento funcional (decisión explícita 2026-07-04). El comportamiento por ítem vive exclusivamente en `ItemStockConfig`/`SaleConfig`, independientes del tipo. Evolucionar esto a flags de comportamiento (`EsServicio`, `PermiteVenta`, etc.) requiere una ADR nueva, no una extensión menor.

**Regla 4 — Fuente única de consumo en frontend**
`useItemTypeOptions()` (`modules/items/hooks/useItemTypeOptions.ts`) es el único punto de acceso al catálogo desde React, con caché de módulo para evitar peticiones duplicadas cuando varios componentes se montan en la misma vista. Prohibido hacer `apiGet('/api/v1/item-types')` directo fuera de este hook o de `itemTypeService.ts` (admin).

### Componentes congelados

| Componente | Ubicación | Estado |
|---|---|---|
| `ItemTypeDefinition` (entidad) | `ERP.Domain/Modules/Items/Entities/` | FROZEN |
| `IItemTypeRepository`/`ItemTypeRepository` | `ERP.Domain` / `ERP.Infrastructure/Persistence/Repositories/Items/` | FROZEN |
| `ItemTypeUseCases.cs` (CQRS completo) | `ERP.Application/Modules/Items/UseCases/` | FROZEN |
| `ItemTypesController.cs` (`api/v1/item-types`) | `ERP.API/Controllers/` | FROZEN |
| FK `items.item_type_id → item_types.id` | Migración `20260704193028_AddItemTypeIdForeignKey` | FROZEN |
| `itemTypeService.ts` / `useItemTypeOptions.ts` | `frontend/src/modules/items/api/` y `hooks/` | FROZEN |
| `ItemTypesPage.tsx` (`/inventory/item-types`) | `frontend/src/modules/items/pages/` | FROZEN |

### Prohibido en todo el sistema

- Reintroducir un enum fijo de tipos de ítem en backend o frontend.
- Guardar, filtrar o comparar por `Code`/`Name` del tipo de ítem como si fuera el identificador de relación (`item.itemType === 'Service'` y equivalentes).
- Implementar un segundo fetch independiente a `/api/v1/item-types` fuera de `useItemTypeOptions()`/`itemTypeService.ts`.
- Agregar flags de comportamiento a `ItemTypeDefinition` sin una ADR formal.
- Incluir `itemTypeId` en el payload de actualización de ítem (`UpdateItemCommand` no lo acepta — es inmutable post-creación).

### Reglas de evolución

- Un nuevo campo descriptivo en `ItemTypeDefinition` (ej. un ícono) se agrega como columna nueva sin cambiar el modelo de relación.
- Convertir la clasificación en comportamiento funcional (que el tipo controle inventario/venta/kardex) requiere ADR nueva, evidencia técnica y reconciliación explícita con `ItemStockConfig`/`SaleConfig`.
- Cualquier módulo nuevo que necesite el nombre del tipo de ítem debe resolverlo vía `ItemTypeName` ya expuesto en los DTOs (`ItemDto`, `ItemDetailDto`), nunca reimplementando la resolución.

---

## Valores por Defecto de Facturación

Decisión arquitectónica congelada 2026-07-01. **Migrado a org_settings 2026-07-01 (Phase 8).**

Infraestructura transversal del ERP. Gestiona los 5 parámetros por defecto que se precargan al crear una nueva factura de venta. **Fuente de verdad: tabla `org_settings` con `scope=Company`** — ya no `SriSettings`. Los 5 campos fueron eliminados de `SriSettings` y sus columnas dropeadas vía migración `RemoveSriSettingsInvoiceDefaults`.

### Parámetros congelados

| Clave `org_settings` | Tipo | Propósito |
|---|---|---|
| `invoice.default_doc_type_code` | `String` | Tipo de documento SRI por defecto |
| `invoice.default_payment_method_code` | `String` | Forma de pago SRI por defecto |
| `invoice.default_emission_point_id` | `Guid` | Punto de emisión por defecto |
| `invoice.default_warehouse_id` | `Guid` | Bodega por defecto |
| `invoice.default_payment_term_id` | `Guid` | Condición de pago por defecto |

Todos son opcionales: ausencia de fila significa "sin configurar" — el usuario lo seleccionará manualmente en cada factura.

### API pública congelada

```csharp
// Única operación autorizada para leer los defaults desde módulos de negocio:
GetSalesInvoiceDefaultsQuery  →  GET /api/v1/electronic-invoicing/invoice-defaults

// Única operación autorizada para mutar los defaults (Company Settings Hub):
UpdateSalesInvoiceDefaultsCommand  →  PUT /api/v1/electronic-invoicing/sales-defaults
```

El handler lee/escribe via `IOrgSettingsRepository` con `OrgScope.Company`. Los valores fallback SRI (`"01"`) vienen de las constantes `SriSettings.FallbackDocTypeCode` y `SriSettings.FallbackSriPaymentMethodCode`.

### Componentes congelados

| Componente | Ubicación | Estado |
|---|---|---|
| `UpdateSalesInvoiceDefaultsCommand` | `ERP.Application/Modules/ElectronicInvoicing/UseCases/UpdateSalesInvoiceDefaults/` | FROZEN |
| `UpdateSalesInvoiceDefaultsCommandHandler` | `ERP.Application/Modules/ElectronicInvoicing/UseCases/UpdateSalesInvoiceDefaults/` | FROZEN |
| `UpdateSalesInvoiceDefaultsCommandValidator` | `ERP.Application/Modules/ElectronicInvoicing/UseCases/UpdateSalesInvoiceDefaults/` | FROZEN |
| `GetSalesInvoiceDefaultsQueryHandler` | `ERP.Application/Modules/Sales/UseCases/GetSalesInvoiceDefaults/` | FROZEN |
| `GET /electronic-invoicing/sales-defaults` (endpoint) | `ERP.API/Controllers/ElectronicInvoicingController.cs` | FROZEN |
| `PUT /electronic-invoicing/sales-defaults` (endpoint) | `ERP.API/Controllers/ElectronicInvoicingController.cs` | FROZEN |
| `salesDefaultsSchema.ts` + `SalesDefaultsValues` | `frontend/src/modules/configuracion/empresa/schemas/` | FROZEN |
| `SalesDefaultsSettingsSection.tsx` | `frontend/src/modules/configuracion/empresa/sections/` | FROZEN |
| `salesDefaultsService.getSettings()` / `updateSettings()` | `frontend/src/modules/sales/api/salesDefaultsService.ts` | FROZEN |
| Tab `'sales-defaults'` en `companySettingsTabs.ts` | `frontend/src/modules/configuracion/empresa/` | FROZEN |

### Reglas permanentes

**Regla 1 — Fuente de verdad**
Los 5 defaults viven en `org_settings` con `scope=Company`. `SriSettings` no almacena defaults de factura.

**Regla 2 — Todos los campos son opcionales**
`null` / ausencia de fila es el estado válido para cualquiera de los 5 parámetros. El módulo de ventas debe manejar `null` sin inventar fallbacks de negocio.

**Regla 3 — Separación de concern con Facturación Electrónica**
Los defaults de factura son un concern de Company Settings, no de Electronic Invoicing. Los endpoints viven en `ElectronicInvoicingController` por afinidad histórica, pero la UI vive en Company Settings Hub (`/settings/company`).

**Regla 4 — Org Config Hierarchy**
Los defaults de nivel Empresa son el nivel base. Los niveles Sucursal, Establecimiento, PuntoEmisión y Bodega pueden sobrescribir campo a campo via `org-config/{scope}/{id}/invoice-defaults`. El módulo de ventas deberá aplicar la resolución jerárquica al momento de precargar una factura.

### Prohibido en todo el sistema

- Almacenar defaults de factura de venta en `SriSettings` ni en ninguna entidad distinta de `org_settings`.
- Reintroducir `DefaultDocTypeCode`, `DefaultSriPaymentMethodCode`, `DefaultEmissionPointId`, `DefaultWarehouseId` o `DefaultPaymentTermId` en `SriSettings`.
- Reintroducir `SriSettings.CreateForDefaults()` o `SriSettings.UpdateInvoiceDefaults()`.
- Usar valores hardcodeados como fallback cuando el campo es `null` en cualquier módulo de negocio.
- Crear selectores de defaults de factura fuera de la pestaña "Valores por Defecto" del Company Settings Hub.

### Reglas de evolución

- Un 6.° parámetro por defecto (p. ej. `DefaultCurrencyCode`) se agrega como nueva clave en `OrgSettingKeys.Invoice.*` — sin nueva entidad ni nueva tabla.
- Cualquier cambio en el contrato de `SalesInvoiceDefaultsDto` (campo nuevo o renombrado) requiere actualizar `GetSalesInvoiceDefaultsQuery`, `UpdateSalesInvoiceDefaultsCommand` y el servicio frontend `salesDefaultsService` de forma sincronizada.

---

## ElectronicDocuments v1.0 (Facturación Electrónica SRI)

Decisión arquitectónica congelada 2026-07-11. ADR: [`docs/decisions/ADR-023-electronic-documents-v1-closure.md`](../decisions/ADR-023-electronic-documents-v1-closure.md).

Núcleo funcional de facturación electrónica SRI Ecuador (esquema offline) — generación de XML, validación XSD, firma XAdES-BES, recepción, autorización, reintentos, auditoría. Cerrado tras tres rondas de verificación: auditoría de robustez (críticos/altos corregidos con evidencia y reproducción), validación de cumplimiento del Anexo Técnico SRI texto por texto contra el PDF oficial, y pruebas reales contra el ambiente de Pruebas del SRI (`celcer.sri.gob.ec`) con certificado real, incluyendo un rechazo real confirmado.

### Regla permanente

A partir de este cierre, cualquier cambio al núcleo de `ElectronicDocuments` debe estar justificado por una de estas cuatro causas — nunca por "mejora", "refactor" o "podría hacerse mejor":

1. **Cambio obligatorio del SRI** (nueva versión de XSD, nuevo código de error, cambio de URL de servicio, nuevo campo exigido por una actualización de la Ficha Técnica).
2. **Bug demostrado** (con reproducción, causa raíz y test de regresión).
3. **Vulnerabilidad de seguridad** (con evidencia de explotabilidad real).
4. **Rendimiento crítico** (con medición objetiva, no percepción).

### Componentes congelados

| Componente | Ubicación | Estado |
|---|---|---|
| `ElectronicDocument` (agregado, máquina de estados) | `ERP.Domain/Modules/ElectronicDocuments/Entities/` | FROZEN |
| `IElectronicDocumentIssuer` (`RegisterAsync`/`RetryAsync`) | `ERP.Application/Modules/ElectronicDocuments/Services/` | FROZEN |
| Pipeline (`ElectronicDocumentIssuer.RunPipelineAsync`) | `ERP.Application/Modules/ElectronicDocuments/Services/` | FROZEN |
| `XadesBesSigner` / `SriSoapClient` / `SriReceptionClient` / `SriAuthorizationClient` | `ERP.Infrastructure/Services/Sri/` | FROZEN |
| `ElectronicDocumentRetryPolicy` (5 intentos, backoff 1-16 min) + `ElectronicDocumentRetryJob` | `ERP.Application`/`ERP.API/Hangfire/` | FROZEN |
| `EmbeddedXmlSchemaProvider` + `manifest.json` + XSD oficiales | `ERP.Infrastructure/ElectronicDocuments/Resources/SRI/` | FROZEN |
| Controladores `ElectronicDocumentsController` / `ElectronicInvoicingController` | `ERP.API/Controllers/` | FROZEN |

### Prohibido en todo el sistema

- Modificar la máquina de estados, el pipeline o los contratos públicos listados arriba por "limpieza" o "consistencia" sin una de las 4 causas permitidas.
- Agregar builders/providers/validadores para nuevos tipos de comprobante (CreditNote, DebitNote, ShippingGuide, Retention, PurchaseSettlement — hoy solo XSD/catálogo, sin implementación activa) como si fuera mantenimiento — es funcionalidad nueva, requiere su propia fase con roadmap explícito.
- Reintroducir cálculo tributario, numeración documental o auditoría propia dentro de este módulo — son infraestructuras FROZEN de otros ADR (Configuración Tributaria, ADR-019, ADR-022), se consumen, nunca se reimplementan.
- Relajar el catálogo `sri_error_code` con datos no verificados textualmente contra la Ficha Técnica oficial (`docs/FICHA TECNICA COMPROBANTES ELECTRONICOS ESQUEMA OFFLINE Versio232.pdf`).

### Reglas de evolución

- Todo cambio, incluso bajo una de las 4 causas permitidas, sigue el protocolo de gate ya establecido: ¿es un bug real? ¿existe evidencia? ¿es reproducible? ¿cuál es el riesgo? ¿qué impacto tiene? ¿rompe compatibilidad? — antes de tocar código.
- Detalle completo de responsabilidades, límites, dependencias, interfaces públicas, estados, pipeline, eventos y deuda aceptada conscientemente: ver ADR-023.

---

## Auditoría por Dominio: Entity Audit (INMUTABLE) + Process Audit (diseño futuro)

Decisión arquitectónica congelada 2026-07-07. ADR: [`docs/decisions/ADR-022-audit-infrastructure-entity-vs-process.md`](../decisions/ADR-022-audit-infrastructure-entity-vs-process.md). Reglas ejecutables completas: [audit-infrastructure.md](./audit-infrastructure.md).

Infraestructura transversal del ERP. Reemplaza el patrón anterior de escribir auditoría de negocio a mano contra la tabla genérica `UserActivity` (que queda reservada exclusivamente para el feed liviano "mi actividad reciente", nunca para auditoría de negocio con valores tipados antes/después).

Componentes congelados, reglas 1-3, prohibiciones, `AuditActor` como snapshot histórico y deuda técnica conocida: ver [audit-infrastructure.md](./audit-infrastructure.md) (cuerpo normativo único — no duplicado aquí).

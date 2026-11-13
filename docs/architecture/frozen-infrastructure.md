# Infraestructuras CLOSED (INMUTABLE)

> Toda infraestructura clasificada como **CLOSED** forma parte de la **Baseline Arquitectónica del ERP**. Ningún cambio funcional podrá modificar su comportamiento sin un nuevo ADR, evidencia técnica, pruebas automatizadas y revisión de compatibilidad hacia atrás.

Esta regla rige a **todas** las infraestructuras transversales declaradas CLOSED en `STATUS.md` y en sus decisiones de cierre, incluyendo —sin limitarse a— las listadas en este documento. Aplica también a toda infraestructura futura que se declare CLOSED bajo el mismo proceso.

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

### Configuración de número inicial

La configuración reutiliza DocumentSequence por Company + EmissionPoint + DocTypeCode,
sin tabla ni contador paralelo por Branch/ambiente. HasBeenUsed representa captura real,
no se infiere de CurrentSeq > 1. El valor inicial válido está entre 1 y 999999999;
tras uso real ConfigureNextNumber rechaza cambios (Conflict); no existe ajuste restringido
operativo en ese comando ni permiso que lo habilite desde documentación. No se captura en Draft ni se libera un número al anular.
Rationale: [diseño de configuración](../decisions/DOCUMENT-SEQUENCES-DESIGN-02.md).

### Reglas de evolución

- **Extensión 2026-10-30 (PHYSICAL-FISCAL-F1, defecto demostrado: dos cajas concurrentes podían consumir un secuencial fuera del rango autorizado de un preimpreso):** sobrecarga `CaptureNextAsync(tenantId, companyId, emissionPointId, docTypeCode, authorizedRanges, ct)` → `string?`. Misma implementación única, advisory lock y transacción; el número se comprueba contra los rangos antes de cualquier escritura y, fuera de rango, devuelve `null` sin INSERT/UPDATE ni commit. La firma sin rangos conserva exactamente su comportamiento. Pruebas PostgreSQL reales en `DocumentSequenceAmbientTransactionTests` (RED sin la comprobación: se entregaban 501..511 con límite 500). Sin cambios de modelo ni migración. Detalle: [company-readiness.md § Captura acotada](./company-readiness.md#captura-acotada-al-rango-autorizado).
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
| FK `items.item_type_id → item_types.id` | `FK_items_item_types_item_type_id` en la baseline única `20261101012214_InitialProductionBaseline` | FROZEN |
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

Infraestructura transversal del ERP. Gestiona los valores que se precargan al crear una factura de venta. `SriSettings` no almacena defaults de factura. Mecánica común de `org_settings` (catálogo, guardrail de escritura, resolvers, historial): [Backend § Configuración organizacional](./backend.md#configuración-organizacional).

### Parámetros congelados

| Valor | Fuente única | Scope |
|---|---|---|
| Tipo de documento SRI | `invoice.default_doc_type_code` | Company |
| Forma de pago SRI | `invoice.default_payment_method_code` | Company |
| Condición de pago | `invoice.default_payment_term_id` | Company |
| Bodega | `invoice.default_warehouse_id` | Branch |
| Punto de emisión | `EmissionPoint.IsDefault` activo del establecimiento principal activo de la empresa (no es un `org_setting`) | — |

Todos son opcionales: ausencia de valor significa "sin configurar" y la selección es manual.
Vacío o solo espacios se guarda como `null`.

### Validez y guardado (Configuración → Ventas)

`IInvoiceDefaultsCatalogValidator` es la regla única de validez de los defaults de empresa:
tipo de documento solo `01` (Factura) activo en `sri_doc_types`; forma de pago SRI existente
y activa; condición de pago existente, activa y del tenant. Al escribir, el validador del
comando rechaza (422 por campo); la lectura administrativa publica `allowedDocTypeCodes` (tipos
aceptados hoy) y la UI filtra su selector con esa lista, sin replicar la política. Al leer, un valor guardado que ya no es válido se conserva
(nunca se borra ni corrige automáticamente): la lectura administrativa lo devuelve con
`ConfigurationWarnings` e `InvoiceDefaultsResolver` lo trata como `null` con advertencia.

El PUT de empresa guarda los tres settings y, si el payload incluye `defaultPriceList`, la
reasignación de la lista predeterminada en una única transacción
(`IUnitOfWork.ExecuteInTransactionAsync`): cualquier fallo revierte el conjunto. La lista sigue
viviendo solo en `PriceList.IsDefault` (proyectada como `defaultPriceListId`, nunca como
`org_setting`); `defaultPriceList` ausente no la toca y `priceListId: null` la quita (Ventas
aplica el PVP). Pedir ese cambio exige además `pricing.update`; sin él se rechaza la operación
completa (403) antes de escribir. Las reglas del cambio de default y su auditoría están solo en
`IPriceListDefaultAssigner`, compartido con `PATCH /api/v1/pricing/price-lists/{id}/set-default`
y con el alta/edición de listas cuando cambia `IsDefault` (que conservan su regla: 422 si ya hay
otra predeterminada activa; una lista deshabilitada no puede marcarse): como máximo una lista
predeterminada por empresa; desmarca toda lista que conserve la bandera, activa o no, porque
`uq_price_lists_tenant_company_default` no filtra por estado ni es diferible (se desmarca y se
envía a la BD antes de marcar, dentro de la misma transacción), y audita cada cambio.

### Resolución

`IInvoiceDefaultsResolver` (`InvoiceDefaultsResolver`) es el único punto de resolución. Bodega:
setting de la sucursal activa (debe existir, estar activa y pertenecer a esa sucursal) →
`Warehouse.IsMain` de la sucursal → `null` con `RequiresManualWarehouseSelection`. Un valor
inválido o cruzado no cae al siguiente nivel: devuelve `null`, selección manual y
`ConfigurationWarnings`. `DefaultWarehouseSource` informa `BranchSetting`, `BranchMainWarehouse`
o `None`. La bodega de la caja activa la aplica el frontend fuera de este resolver.
El PUT de sucursal aplica la misma validación al guardar: una bodega inexistente, de otra
empresa, inactiva o de otra sucursal se rechaza con 422 sin escribir; `null` («Sin configurar»)
se guarda y activa el fallback a `Warehouse.IsMain`. Los valores corruptos ya guardados siguen
el tratamiento fail-closed de lectura.
GET y PUT de sucursal autorizan la sucursal de la ruta con `IBranchAccessGuard.RequireBranchAsync`
(además de la sucursal activa): sin `CompanyUserBranch` o deshabilitada → 403; de otra empresa o
inexistente → 404; el administrador global en modo operador conserva el acceso.
La respuesta incluye las constantes `SriSettings.FallbackDocTypeCode` y
`SriSettings.FallbackSriPaymentMethodCode` como campos separados de los valores configurados.

### API pública congelada

```text
GetSalesInvoiceDefaultsQuery              →  GET /api/v1/sales/invoice-defaults ([Authorize])
GetCompanyInvoiceOrgSettingsQuery         →  GET /api/v1/org-config/company/invoice-defaults (settings.company.view)
UpsertCompanyInvoiceOrgSettingsCommand    →  PUT /api/v1/org-config/company/invoice-defaults (erp.companies.update; + pricing.update si incluye defaultPriceList)
GetBranchInvoiceOrgSettingsQuery          →  GET /api/v1/org-config/branch/{branchId}/invoice-defaults (settings.company.view)
UpsertBranchInvoiceOrgSettingsCommand     →  PUT /api/v1/org-config/branch/{branchId}/invoice-defaults (erp.companies.update)
```

### Componentes congelados

| Componente | Ubicación |
|---|---|
| Queries/commands Company y Branch | `ERP.Application/Modules/OrgConfig/UseCases/` |
| `GetSalesInvoiceDefaultsQueryHandler` | `ERP.Application/Modules/Sales/UseCases/GetSalesInvoiceDefaults/` |
| `IInvoiceDefaultsResolver` / `InvoiceDefaultsResolver` | `ERP.Domain/Modules/Sales/Interfaces/` / `ERP.Infrastructure/Services/` |
| `IInvoiceDefaultsCatalogValidator` / `InvoiceDefaultsCatalogValidator` | `ERP.Domain/Modules/Sales/Interfaces/` / `ERP.Infrastructure/Services/` |
| `IPriceListDefaultAssigner` / `PriceListDefaultAssigner` | `ERP.Domain/Modules/Pricing/Interfaces/` / `ERP.Infrastructure/Services/` |
| `OrgConfigController` | `ERP.API/Controllers/OrgConfigController.cs` |
| `orgConfigService` | `frontend/src/modules/configuracion/empresa/api/orgConfigService.ts` |
| `CompanySalesSettingsSection.tsx` (defaults Company) | `frontend/src/modules/configuracion/empresa/sections/` |
| `branchInvoiceDefaultsFacade` + `BranchConfigSection.tsx` (bodega por sucursal) | `frontend/src/modules/configuracion/empresa/facades/`, `frontend/src/modules/branches/components/` |
| `salesDefaultsService.get()` | `frontend/src/modules/sales/api/salesDefaultsService.ts` |

### Prohibido en todo el sistema

- Almacenar defaults de factura de venta en `SriSettings` o en otra entidad distinta de las fuentes de la tabla anterior.
- Reintroducir `DefaultDocTypeCode`, `DefaultSriPaymentMethodCode`, `DefaultEmissionPointId`, `DefaultWarehouseId` o `DefaultPaymentTermId` en `SriSettings`, o una key de punto de emisión por defecto.
- Reintroducir `SriSettings.CreateForDefaults()` o `SriSettings.UpdateInvoiceDefaults()`.
- Resolver la bodega por defecto en un handler distinto de `IInvoiceDefaultsResolver` o elegir el primer registro en el cliente.
- Inventar fallbacks de negocio cuando un valor es `null`.

### Reglas de evolución

- Un parámetro adicional se agrega como clave en `OrgSettingKeys.Invoice.*` con su `ConfigurationDefinition` — sin nueva entidad ni tabla.
- Cualquier cambio en `SalesInvoiceDefaultsDto` requiere actualizar de forma sincronizada `GetSalesInvoiceDefaultsQuery`, `InvoiceDefaultsResolver` y `salesDefaultsService`.

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
| `ElectronicDocumentRetryPolicy` (máximo por empresa, backoff 1-16 min; resultado SRI pendiente: ventana de 24 h, backoff ≤ 60 min) | `ERP.Application` | FROZEN; orchestration replaced by ADR-043 |
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
- **Extensión 2026-09-25 (ADR-034, causa n.º 2 — bug demostrado):** filtro por día del Monitor (`dateFrom`/`dateTo` → `DateOnly` + rango UTC del día de empresa), `IssueDate` de `ElectronicDocumentData` como `DateOnly`, y `fechaEmision` de la NC de venta desde el día de empresa (`ICompanyClock`). Máquina de estados, pipeline, firma y clientes SOAP sin cambios.
- **Extensión 2026-10-01 (ADR-036, ZH-RETENTION-ELECTRONIC-LIFECYCLE-01A — bug demostrado: XML de una retención anulada enviado al SRI):** estados `Dispatching` (reclamo de transmisión persistido antes de llamar al SRI) y `Discarded` (terminal, sin intento externo); `MarkSent` admite `Dispatching`; `MarkAuthorized`/`MarkRejected` admiten `Signed`/`Dispatching` (resolución por consulta, sin reenvío). Gate `IElectronicDocumentSourceLifecycleGuard` por `SourceModule` y `IElectronicDocumentSourceCancellation`. Solo los orígenes con guard registrado (hoy Retentions) usan el protocolo nuevo: Ventas/NC conservan exactamente el pipeline v1.0. Sin migración (enum persistido como `int`). Firma, XML builders, RIDE y clientes SOAP sin cambios.
- **Extensión 2026-10-01 (ADR-036 §24, ZH-RETENTION-SRI-ANNULMENT-01 — anulación oficial):** estado `AnnulmentPending` (13) y `AnnulmentRequestId`; `MarkAnnulmentPending` / `RevertAnnulment` / `ConfirmExternalAnnulment(requestId, ExternalAnnulmentEvidence)`. **Se eliminó `MarkCancelled`**: `Cancelled` solo se alcanza con una solicitud y la evidencia del ANULADO confirmado por el SRI. Servicio `IElectronicDocumentSriAnnulment` (mismo lock del origen). Migración aditiva `RetentionSriAnnulment`.
- **Extensión 2026-10-02 (ADR-036 §25, ZH-RETENTION-SRI-ANNULMENT-01B — alineación con la Ficha Técnica v2.34 §8):** el precheck de 01 que negaba un servicio de consulta era incorrecto. Se agrega la operación **ConsultaComprobante** (`consultarEstadoAutorizacionComprobante`) al cliente SOAP existente `SriSoapClient`: mismo HTTP, reintentos, SOAP Fault y parsing de mensajes; `PostSoapCoreAsync` distingue timeout de red caída, y Recepción/Autorización no cambian. Se expone tipada como `ISriDocumentStatusQuery`, con `SriStatusQueryOutcome` (técnico) separado de `SriFiscalStatus` (fiscal). `ExternalAnnulmentEvidence` pasa a ser la evidencia de esa consulta (usuario `Guid.Empty` = proceso automático). Sigue sin existir un WS para SOLICITAR la anulación. Máquina de estados del comprobante, pipeline de emisión, firma y XML sin cambios.
- **Extensión 2026-10-02 (ADR-038 D6/D7, ZH-SRI-ANEXO26-PROVIDER-RUC-01 — causa n.º 1, Ficha 2.34 Anexo 26):** `infoAdicional` se compone en un único `IElectronicDocumentAdditionalInfoComposer`, invocado por los dos orquestadores de ensamblado fiscal; el contributor del RUC Proveedor emite `campoAdicional nombre="RUC Proveedor"` en 01/04/07 desde `EffectiveDate`. Builders, providers, XSD, firma, SOAP, máquina de estados, pipeline del Issuer, jobs y RIDE sin cambios. Addendum en ADR-023.
- **Extensión 2026-10-29 (Caso #32, decisión del propietario — respuestas inciertas del SRI):** Ventas/NC dejan de reenviar un `Signed` en el reintento: solo consultan la autorización (como los orígenes con guard). Conciliación manual con permiso `electronic-documents.reconcile` y evidencia de texto: se audita y se reconsulta; nunca autoriza ni libera un reenvío. Sin estados, migraciones ni cambios en SOAP, firma o XML. Addendum en ADR-023; contrato en [backend.md](backend.md#límites-y-finalización).
- **Extensión 2026-10-31 (R12 Fase A — bug demostrado: anulación de factura sin consultar su comprobante y envío sin revalidar el origen):** Ventas (Factura 01 y NC 04, origen `Sales`) registra `SalesElectronicSourceLifecycleGuard` con `UsesDispatchProtocol = false`: el reclamo de envío revalida la factura/devolución bajo su lock y persiste `Signed`; el resto del pipeline v1.0 (job, consulta, R11, Caso #32) no cambia. `CancelSalesInvoice` usa `IElectronicDocumentSourceCancellation`. Retenciones (`true`) sin cambios. Sin estados ni migraciones. Addendum en ADR-023.
- **Extensión 2026-10-31 (R12 Fase B — causa n.º 1, Ficha 2.34 nota 1: NO AUTORIZADO corregible con la misma clave):** única salida de `Rejected` para 01/04/07: `ClaimCorrectedRetransmission` (`Rejected→Signed`/`Dispatching`) por el comando explícito del Monitor (permiso `electronic-documents.retransmit-corrected`), con elegibilidad por códigos 35/36/47/48/52/58/67 y procedencia, corrección semántica demostrada, evidencia versionada con SHA-256 (`ElectronicDocumentTransmissionAttempt`), reclamo bajo el lock del guard y una transmisión; NO AUTORIZADO históricos descartados por fecha del SRI; R11 por intento. Pipeline de emisión, firma, builders, XSD, job y Caso #32 sin cambios para comprobantes sin retransmisión. Migración aditiva `CorrectedRetransmission`. Addendum en ADR-023; contrato en [backend.md](backend.md#límites-y-finalización).
- **Extensión 2026-10-31 (cierre SRI 2.34 — causas n.º 1 y 2):** varias `<autorizacion>` → manda la AUTORIZADO (R14); DEVUELTA solo con código 50 no es rechazo y el documento conserva su estado (R13, Caso #32 sin reenvío); XML firmado > 320 KB → `Failed` antes de transmitir (R16); `contribuyenteEspecial` en 01/04 desde `ElectronicDocumentIssuerData.SpecialTaxpayerNumber`, fuente única también para 07 (R23); `Signed`/`Received` se siguen consultando sobre el máximo de la empresa; DeadLetter solo 24 h después del `Received` del timeline, y sin evidencia de recepción nunca por plazo (R11, mismo job y política). Estados, firma, SOAP de recepción/consulta, migraciones y XSD sin cambios. Contrato en [backend.md](backend.md#límites-y-finalización); addendum en ADR-023.
- **Extensión 2026-10-31 (R21/R22 — causa n.º 1):** `<agenteRetencion>` (Anexo 21) en el `infoTributaria` común de 01/04/07 (`InfoTributariaXmlBuilder`, salida idéntica sin designación) y `campoAdicional` "Gran Contribuyente" (Anexo 24) por un contributor del composer, ambos solo con resolución vigente a la fecha de emisión; proyección única del emisor `ElectronicDocumentIssuerProjection` (ADR-038 D5); RIDE con `contribuyenteEspecial` y agente de retención; gate de emisión con el formato de los datos fiscales del emisor. Migración aditiva de `Company` (`IssuerFiscalDesignations`). Estados, firma, SOAP, secuenciales y XSD sin cambios. Addendum en ADR-023.
- **Extensión 2026-11-01 (salud técnica SRI — observación pasiva, sin cambio de comportamiento):** `ISriReceptionClient`, `ISriAuthorizationClient` e `ISriConnectivityChecker` se registran envueltos por decoradores de DI `Observed*` (`ERP.Infrastructure/Services/Sri/Health/`) que devuelven el resultado intacto, propagan excepciones sin registrar y solo alimentan el monitor en memoria `SriServiceHealthMonitor` (sin clave de acceso, XML ni empresa). `SriSoapClient`, los adaptadores, el pipeline, la máquina de estados, R11/R12/R32 y `ElectronicInvoicingController` sin cambios; sin migraciones ni llamadas SOAP nuevas. Contrato y reglas: [`docs/frontend/ELECTRONIC_ENVIRONMENT_BANNER.md` § Salud técnica SRI](../frontend/ELECTRONIC_ENVIRONMENT_BANNER.md#salud-técnica-sri--monitor-central-zhsriservicehealth).
- **Extensión 2026-11-02 (SRI-TOGGLE-02 — decisión del propietario, intervención autorizada limitada al control):** interruptor de emisión por empresa `electronic_documents.emission_enabled` (org_settings, default OFF, auditado). El issuer consulta `IElectronicEmissionSwitch` antes de toda transmisión nueva (inicio del pipeline, reclamo de envío y de retransmisión con lock compartido del interruptor (consultivo por empresa desde 04), reactivación de DeadLetter hacia Draft/Failed) y responde `ELECTRONIC_DOCUMENT_EMISSION_DISABLED` sin escribir; los procesos automáticos no reanudan pendientes anteriores a la habilitación. Consultas, conciliación y operaciones comerciales sin cambios. XML, XAdES, SOAP, secuencias, máquina de estados, R11/R12/Caso #32, `ElectronicInvoicingController` y migraciones sin cambios. Addendum en ADR-023; contrato en [backend.md](backend.md#límites-y-finalización).
- **Extensión 2026-11-02 (SRI-TOGGLE-07 — mismo alcance):** la pausa total incluye descarga de XML y verificación de NC de proveedores y el ping de diagnóstico; todo consumidor llama al SRI dentro de un ámbito (lock consultivo compartido de sesión por empresa) que el cambio del interruptor espera: confirmado el apagado no inicia ninguna llamada nueva. Sin migraciones; XML/XAdES/SOAP/secuencias/estados sin cambios. Addendum en ADR-023.
- **Extensión 2026-11-02 (SRI-TOGGLE-06 — mismo alcance):** pausa total: con la emisión deshabilitada tampoco hay reintentos, consultas de autorización (manuales ni automáticas), historial R12 ni ConsultaComprobante de anulaciones de retención; el job de reintentos y el polling de anulaciones filtran por el interruptor en todo estado, sin cierre por exhaución mientras esté pausada; al rehabilitar, los procesos automáticos solo retoman actividad posterior a la habilitación. Sin migraciones; XML/XAdES/SOAP/secuencias/estados sin cambios. Addendum en ADR-023.
- **Extensión 2026-11-02 (SRI-TOGGLE-04 — mismo alcance):** la reanudación automática decide con el último intento o el instante persistido del origen (`UpdatedAt` de autorización/emisión), nunca con la creación del comprobante; lecturas y cambios del interruptor se serializan con un lock consultivo por empresa (exclusivo antes de fechar el registro auditado). Sin migraciones; XML/XAdES/SOAP/secuencias/estados sin cambios. Addendum en ADR-023.
- **Extensión 2026-11-12 (API-DOC-V1 — decisión del propietario, excepción documental limitada):** `ElectronicDocumentsController`, `ElectronicInvoicingController`, `SriServiceHealthController` y `RideController` reciben solo comentarios XML, `[ProducesResponseType]` y `using` para la documentación Swagger/OpenAPI (20 operaciones). No es una de las cuatro causas ni las modifica; no cambia comportamiento del SRI, rutas, DTO, permisos ni contratos. Detalle: [ADR-023 § Addendum 2026-11-12](../decisions/ADR-023-electronic-documents-v1-closure.md) y [STATUS](../../STATUS.md).

### Frontera: proveedor tecnológico global vs. empresa emisora (SRI-ELECTRONIC-DOCUMENTS-QA-FIX-01)

`SystemProviderSettings` (`ERP.Domain.Configuration.Entities`, singleton `Id=1`, sin `TenantId`/`CompanyId` — configurado desde AdminCore) es el registro del proveedor tecnológico del sistema ante el SRI (ERP-CORE-CLOSEOUT-09), **distinto y no intercambiable** con los datos del emisor de cada comprobante (`Company`/`SriSettings`/`Establishment`, siempre `TenantId`+`CompanyId` scoped).

Frontera vigente (ZH-SRI-ANEXO26-PROVIDER-RUC-01, ADR-038 D6/D7):

| Dato del XML | Fuente única | Quién lo coloca |
|---|---|---|
| Emisor: `ruc` / `razonSocial` de `infoTributaria` | `Company` (+ `SriSettings`/`Establishment`) de la empresa activa | El provider de cada tipo |
| `<campoAdicional nombre="RUC Proveedor">` (Ficha 2.34, Anexo 26; Res. NAC-DGERCGC26-00000027) | `SystemProviderSettings` (global de instancia) | `SystemProviderRucAdditionalInfoContributor` → `IElectronicDocumentAdditionalInfoComposer` |

- Ningún provider ni builder referencia `ISystemProviderSettingsRepository`; el composer se invoca solo en `CommercialElectronicDocumentXmlSupplier` (01/04) y `RetentionElectronicDocumentXmlService` (07). Lo protegen `ElectronicDocumentsAdditionalInfoBoundaryTests` (baseline 0) y `SalesInvoiceElectronicDocumentDataProviderTests.GetDataAsync_Issuer_proviene_de_Company_y_SriSettings_nunca_de_SystemProviderSettings`.
- `EffectiveDate` es la fecha de aplicabilidad fiscal y se compara con la fecha de emisión de negocio: antes, sin campo; desde ella, `Enabled` + RUC válido son obligatorios o la emisión falla cerrado (`SRI_SYSTEM_PROVIDER_RUC_NOT_CONFIGURED`). `Enabled = true` sin `EffectiveDate` no puede guardarse (dominio + validador, 422).
- Requisitos normativos nuevos de `infoAdicional` se agregan como contributor, nunca en providers ni builders.

---

## Auditoría por Dominio: Entity Audit (INMUTABLE) + Process Audit (diseño futuro)

Decisión arquitectónica congelada 2026-07-07. ADR: [`docs/decisions/ADR-022-audit-infrastructure-entity-vs-process.md`](../decisions/ADR-022-audit-infrastructure-entity-vs-process.md). Reglas ejecutables completas: [audit-infrastructure.md](./audit-infrastructure.md).

Infraestructura transversal del ERP. Reemplaza el patrón anterior de escribir auditoría de negocio a mano contra la tabla genérica `UserActivity` (que queda reservada exclusivamente para el feed liviano "mi actividad reciente", nunca para auditoría de negocio con valores tipados antes/después).

Componentes congelados, reglas 1-3, prohibiciones, `AuditActor` como snapshot histórico y deuda técnica conocida: ver [audit-infrastructure.md](./audit-infrastructure.md) (cuerpo normativo único — no duplicado aquí).

Background processing orchestration is governed by [ADR-043](../decisions/ADR-043-background-processing-ssot.md), replacing the former recurring jobs infrastructure. Fiscal pipeline and retry business policy remain FROZEN.

# Auditoría — Item Matching desde Compras (Recepción SRI)

**Tipo**: Auditoría (Nivel 3 — detalle técnico especializado, no normativo)
**Fecha**: 2026-07-27
**Alcance**: estado actual del sistema respecto a la vinculación entre líneas de compra (XML/TXT SRI) y el catálogo de `Item`. No implementa nada — solo documenta lo encontrado y lo faltante para una fase futura de matching.

---

## 1. Encontrado

### 1.1 Entidad `Item`

`backend/src/ERP.Domain/Modules/Items/Entities/Item.cs` — Aggregate Root, scope `TenantId`.

- `Code` (`ItemCode`): `SKU`, `ShortName`, `Description`, y campo legacy `PurchaseCode` (código de compra genérico, sin proveedor asociado).
- `ItemTypeId`, `CategoryNodeId`, `BrandId`, `DefaultUomCode`, `BaseSalePrice`.
- `TaxConfig` / `SaleConfig` / `StockConfig` — ninguno referencia Supplier.
- Colecciones: `Variants`, `Images`, `UnitConversions`, `Substitutes`, `PackagingLevels`, **`SupplierCodes`** (`List<ItemSupplierCode>`).
- `IsActive` (soft state, heredado de `MasterEntity`).

**Respuesta a las preguntas de validación:**

| Pregunta | Respuesta |
|---|---|
| ¿Puede existir Item sin proveedor? | Sí — `SupplierCodes` es una colección opcional, puede estar vacía. |
| ¿Puede un Item tener múltiples proveedores? | Sí — vía `ItemSupplierCode`, un registro por proveedor. |
| ¿Dónde se guarda el código del proveedor? | `ItemSupplierCode.Code`, más un fallback legacy en `Item.Code.PurchaseCode` (sin proveedor asociado, ver Fase 8 de Items). |
| ¿Dónde se guarda la descripción que usa cada proveedor? | En ningún lado — `ItemSupplierCode` no tiene campo de descripción. |

### 1.2 Relación Item↔Proveedor: `ItemSupplierCode`

`backend/src/ERP.Domain/Modules/Items/Entities/ItemSupplierCode.cs`

```
Id, TenantId, ItemId, SupplierId (FK a BusinessPartner)
Code (string, máx 100)
IsPrimary (bool)
IsActive (bool)
```

Configuración EF: `backend/src/ERP.Infrastructure/Persistence/Configurations/Items/ItemSupplierCodeConfiguration.cs`

- Índice único `(TenantId, SupplierId, Code)` — un código de proveedor identifica un único ítem en el catálogo del tenant.
- Índice único parcial: máximo un `IsPrimary=true` por `ItemId`.
- Índice `(ItemId, IsActive)`.
- FK a `BusinessPartner` con `DeleteBehavior.Restrict`.

**Ya consumido en producción** (Fase 8 de Items, `docs/items/PHASE8-ITEM-PURCHASES.md`, cerrada 2026-07-02): el flujo manual de factura de compra (`PurchaseDraftUseCases`, `GetPurchaseItemContextQuery`) resuelve el código específico del proveedor vía `ItemSupplierCode` (preferencia `IsPrimary`), con fallback a `Item.Code.PurchaseCode` si no existe registro específico.

**Faltante confirmado:**
- Sin campo de `SupplierDescription` (la descripción que usa cada proveedor para ese producto).
- Sin historial de precios de compra por proveedor (`LastPurchasePrice` o tabla equivalente) — búsqueda sin resultados en todo el backend.

### 1.3 Flujo XML/TXT SRI → `PurchaseReception`

Módulo: `backend/src/ERP.Domain/Modules/Purchases/PurchaseReception/`

- **`PurchaseReceptionDocument`** (cabecera): `SupplierRuc`, `SupplierName`, `SupplierId?`, `AccessKey`, `InvoiceNumber`, `IssueDate`, `AuthorizationDate/Number`, `Subtotal/VatAmount/TotalAmount`, `Status` (`Imported/Verified/Processed/Cancelled`), `PurchaseId?`, `XmlContent` (string crudo, reservado), colección `Lines`.
- **`PurchaseReceptionLine`** (detalle): `SupplierCode?`, `Description`, `Quantity`, `UnitPrice`, **`ItemId?`** (FK reservada, sin FK física en EF), **`MatchStatus?`** (string libre, sin vocabulario fijo). El propio código documenta que ambos campos están reservados para una fase futura de conciliación y **nunca se pueblan hoy**.
- **Parser real implementado**: `PurchaseInvoiceTxtParser.cs` — parsea el **TXT** de listado de comprobantes del SRI (solo totales de cabecera: RUC, razón social, tipo/serie de comprobante, clave de acceso, fechas, subtotal/IVA/total). **No trae** `codigoPrincipal`, `codigoAuxiliar`, `descripcion` ni `cantidad` de línea.
- **Descarga de XML autorizado**: `SriReceptionXmlProvider.cs` consulta el SOAP de autorización SRI y persiste el XML crudo en `PurchaseReceptionDocument.XmlContent` vía `AttachSriAuthorization(...)` — **no parsea `detalles`** del XML.
- `ImportPurchaseReceptionHandler` solo crea la cabecera (`PurchaseReceptionDocument.Create`) — nunca invoca `ReplaceLines` sobre líneas de recepción. No existe ningún punto del sistema que hoy genere `PurchaseReceptionLine` reales a partir de un documento SRI.

**Conclusión**: el modelo de datos anticipa la relación línea↔item (`ItemId`, `MatchStatus`), pero ninguna capa (parser, handler, repositorio, UI) la implementa. El XML de detalle del comprobante ni siquiera se parsea todavía — solo se descarga y guarda como texto.

### 1.4 Búsqueda de Items — endpoint existente

`ERP.API/Controllers/ItemsController.cs` → `GET /api/v1/items`

- Filtros: `search`, `sku`, `barcode`, `isActive`, `isForSale`, `isFavorite`, `isEcommerce`, `itemTypeId`, `categoryNodeId`, `brandId`, paginación (`pageNumber`/`pageSize`, máx. 200).
- `search` usa `EF.Functions.ILike` sobre `SKU`, `ShortName`, `Description` (substring, case-insensitive).
- `GET /api/v1/items/resolve/{code}` — resolución exacta por código.

**Ya reutilizado en frontend** para un caso análogo: `ProductPicker.tsx` (`frontend/src/modules/purchases/components/`) es un autocomplete que llama `itemService.getAll({ search, isActive: true, pageSize: 12 })` con debounce 300ms y navegación por teclado — pero pertenece al **formulario manual** de factura de compra (`PurchasesPage.tsx`), no a la pantalla de recepción SRI. Es reutilizable para el matching, sin necesidad de crear un segundo buscador.

### 1.5 Componentes frontend reutilizables

| Componente | Ubicación | Estado hoy |
|---|---|---|
| `ProductPicker.tsx` | `frontend/src/modules/purchases/components/` | Autocomplete de Item por search, funcional, pero desconectado de la pantalla de recepción SRI |
| `SupplierPicker.tsx` | `frontend/src/modules/purchases/components/` | Análogo para proveedores |
| `PurchaseReceptionPage.tsx` | `frontend/src/modules/purchases/pages/` | Solo trabaja a nivel de **documento** (cabecera): sube TXT, muestra tabla de documentos con badges `supplierExists`/`purchaseExists`/`status`, botón "Consultar XML", botón "Crear compra". El propio subtítulo de la página dice explícitamente: *"...todavía no crea compras ni concilia productos."* No existe ninguna UI a nivel de línea. |

---

## 2. Faltante

### 2.1 Matching automático
No existe ninguna lógica de comparación (exacta ni difusa) entre una línea de compra y el catálogo de Items.

### 2.2 Matching difuso / detección de duplicados
Búsqueda sin resultados de `pg_trgm`, `similarity()`, `levenshtein`, trigram o cualquier comparación fuzzy en backend (código y migraciones SQL). La única búsqueda textual disponible es `ILIKE` (substring), insuficiente para casos como `"Coca Cola 500 ML"` vs `"COCA COLA BOTELLA 500ML"` vs `"CC 500"` — ILIKE no encuentra estos tres como candidatos entre sí sin coincidencia de substring exacta.

No es necesario Elasticsearch para el volumen esperado (confirmado por el propio brief). La opción de menor costo arquitectónico es **PostgreSQL `pg_trgm`** (extensión nativa, ya disponible en Postgres, sin nueva infraestructura) con índice GIN sobre `Description`/`ShortName`, expuesto como un nuevo parámetro de similitud en el endpoint de búsqueda de Items existente — no un buscador nuevo.

### 2.3 Selección masiva
No existe ningún flujo para resolver múltiples líneas a la vez (caso de prueba 3 del brief: 100 líneas, 80 ya vinculadas automáticamente, 15 requieren revisión, 5 nuevas). Hoy no hay ni siquiera líneas persistidas para operar sobre ellas.

### 2.4 Vinculación proveedor-item desde este flujo
La infraestructura de datos existe (`ItemSupplierCode`, ya en producción desde Fase 8), pero no hay ningún punto de entrada en la UI de recepción SRI que, al vincular una línea a un Item, cree o actualice el `ItemSupplierCode` correspondiente (código del proveedor detectado en el XML → nuevo registro de `ItemSupplierCode`).

### 2.5 Estado "pendiente" persistente
`MatchStatus` existe como campo de texto libre sin vocabulario fijo, sin `enum`, sin `Configuration` EF que lo restrinja, y sin ningún handler que lo escriba o lea. No hay ninguna query que liste "líneas pendientes de vincular" (no existe endpoint ni frontend consumidor).

### 2.6 UX de vinculación por línea
No existe ningún componente para: mostrar las líneas de un documento de recepción, mostrar sugerencias, mostrar el badge de estado (🟢/🟡/🔴) por línea, ni acciones de "buscar / seleccionar / crear relación" a ese nivel. El único selector de Item existente (`ProductPicker`) no está integrado a esta pantalla.

### 2.7 Parseo del XML de detalle
Sin este paso previo, ningún matching es posible: hoy no se extraen `codigoPrincipal`, `codigoAuxiliar`, `descripcion`, `cantidad`, `precioUnitario` ni impuestos del XML de comprobante — el XML se descarga y persiste como blob de texto (`XmlContent`), pero no se deserializa su nodo `detalles`.

---

## 3. Riesgos

| Riesgo | Descripción | Mitigación sugerida (no implementada aquí) |
|---|---|---|
| **Duplicación de Items** | Sin matching (ni siquiera fuzzy), el flujo natural del usuario ante presión de tiempo es crear un Item nuevo por cada línea no reconocida textualmente, incluso si el producto ya existe con otro nombre/código de proveedor. | Matching difuso obligatorio antes de habilitar "crear item nuevo" como primera opción; mostrar candidatos antes de permitir alta. |
| **Creación sin validación** | `ItemId` en `PurchaseReceptionLine` no tiene FK física en EF (solo índice filtrado) — nada a nivel de base de datos impide que apunte a un Item inexistente o de otro tenant si se llenara manualmente por error. | Agregar FK real a `Items` al implementar la fase de escritura de este campo. |
| **Pérdida de relación proveedor-item** | Si la vinculación de línea no dispara la creación/actualización de `ItemSupplierCode`, cada nueva factura del mismo proveedor para el mismo producto repetirá el mismo esfuerzo de matching manual — el sistema no "aprende". | La acción de vincular una línea debe, como efecto de dominio, crear el `ItemSupplierCode` (código detectado en XML → Item elegido) si no existe ya uno para ese `(SupplierId, Code)`. |
| **Índice único de `ItemSupplierCode` como bloqueo silencioso** | El índice único `(TenantId, SupplierId, Code)` es correcto por diseño, pero un matching automático que intente crear un `ItemSupplierCode` ya existente (con distinto Item) fallará — hoy no hay un flujo de "conflicto de código ya vinculado a otro item" definido. | Definir explícitamente el comportamiento ante colisión de código antes de implementar escritura automática. |
| **`MatchStatus` como texto libre** | Sin `enum`/constraint, cualquier implementación futura puede introducir valores inconsistentes (`"pending"` vs `"Pending"` vs `"PENDIENTE"`) entre distintos desarrolladores/PRs. | Definir vocabulario fijo (enum de dominio) antes de la primera escritura real de este campo. |
| **Infraestructura CLOSED de Configuración Tributaria** | Cualquier matching automático que además intente inferir `VatCode`/`IceCode` de la línea del XML violaría la regla vigente de "los documentos no generan impuestos" (`CLAUDE.md`, sección Configuración Tributaria). | El matching debe limitarse a resolver `ItemId`; los impuestos siguen viniendo exclusivamente del `Item.TaxConfig` ya existente, nunca del XML de compra. |

---

## 4. Resumen — tabla de estado por pregunta del brief

| Pregunta del brief | Respuesta |
|---|---|
| ¿Item sin proveedor? | Sí, permitido |
| ¿Item con múltiples proveedores? | Sí, vía `ItemSupplierCode` |
| ¿Índice único en `ItemSupplierCode`? | Sí — `(TenantId, SupplierId, Code)` + único parcial por `IsPrimary` |
| ¿Permite varios proveedores por Item? | Sí |
| ¿Permite cambiar código proveedor? | Sí (`Code` es mutable vía recreación/gestión de la colección; no hay historial de cambios) |
| ¿Guarda historial de precios? | No |
| ¿`PurchaseReceptionLine` tiene `ItemId`? | Sí, pero reservado y nunca poblado (sin FK física) |
| ¿Busca por código/descripción ya existe? | Sí — `GET /api/v1/items?search=` (ILIKE), reutilizable |
| ¿Autocomplete ya existe? | Sí — `ProductPicker.tsx`, pero no integrado a recepción SRI |
| ¿Matching difuso (trigram/Elasticsearch)? | No existe ninguno; `pg_trgm` es la opción recomendada cuando se implemente |
| ¿UX de vinculación por línea (🟢🟡🔴)? | No existe |

**Conclusión general**: la base de datos (`ItemSupplierCode`) y el buscador de Items ya están listos y en producción para otro flujo (compra manual). Lo que falta es enteramente la cadena de Item Matching para Recepción SRI: parseo del XML de detalle, población de `PurchaseReceptionLine`, definición formal de `MatchStatus`, matching difuso, UI por línea, y el efecto de dominio que cree `ItemSupplierCode` al confirmar una vinculación. Nada de esto requiere modificar infraestructura CLOSED existente — es una fase nueva, aditiva, sobre un módulo (`PurchaseReception`) que el propio código ya declara como "Fase 2 completada, matching reservado para fase futura".

---

**Nota (2026-07-27)**: todos los hallazgos "Faltante" de este documento fueron implementados. Ver [`docs/items/ITEM_MATCHING.md`](ITEM_MATCHING.md) para la arquitectura, el flujo y las decisiones de la implementación resultante. Este documento se conserva sin modificar como snapshot de auditoría.

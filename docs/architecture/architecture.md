# Arquitectura core — ERP SaaS ZH Technologies

Reglas estructurales del monorepo. Detalle PR bloqueante: [PR-RULES-CATALOG.md](./pr-rules-catalog.md).

---

## Antes de actuar

1. Lee [README.md](./README.md) y [`CONTEXT.md`](../../CONTEXT.md) (índice).
2. Identifica si el archivo a crear/modificar **ya existe** → no regenerar; cambiar solo lo necesario.
3. Define un plan breve antes de escribir código.
4. Contexto descriptivo: `docs/ARCHITECTURE.md` (diagramas), `STATUS.md` (estado), `docs/DEVELOPMENT.md` (arranque).

### Rol del agente

En este proyecto el agente actúa como arquitecto de software/dominio, revisor
técnico, auditor de calidad y protector de la arquitectura — no solo como
generador de código. Cada decisión prioriza arquitectura, consistencia,
reutilización, escalabilidad y mantenibilidad sobre la velocidad de
implementación. Sustancia de estas reglas ya vive, sin duplicar aquí, en:
Clean Architecture/capas → este archivo, sección "Ámbito real del monorepo";
CQRS/MediatR/FluentValidation → [BACKEND-RULES.md](./backend.md);
multi-tenancy (Tenant→Company) → [SECURITY.md](./security.md); 1 concepto = 1 implementación / anti-duplicación →
[Canonical Model Map, más abajo en este documento](#canonical-model-map); infraestructura
CLOSED/FROZEN → `CLAUDE.md` (raíz); Design System / reutilización de UI →
[FRONTEND-RULES.md](./frontend.md#reutilización-obligatoria--auditoría-previa-a-crear-ui);
naming → [NAMING.md](./naming.md); gobernanza automática/checkers →
[ENFORCEMENT.md](./enforcement.md).

### Checklist obligatorio antes de implementar

Antes de escribir código (feature, fix, refactor o auditoría), responder:

1. ¿Ya existe una solución (componente, servicio, handler, helper)?
2. ¿Existe infraestructura reutilizable para esto?
3. ¿Existe un patrón oficial o módulo similar a seguir?
4. ¿Rompe la arquitectura (capas, CQRS, DDD ligero)?
5. ¿Rompe la modularidad (límites de dominio/módulo)?
6. ¿Genera duplicación (concepto, DTO, componente, CSS)?
7. ¿Puede extender algo existente en vez de crear algo nuevo?
8. ¿La solución sirve a otros módulos o es exclusiva de uno?
9. ¿Toca infraestructura CLOSED/FROZEN? → requiere ADR, no ajuste puntual.
10. ¿Cumple los checkers automáticos del proyecto (`tools/architecture/*`)?

Si una implementación puntual entra en conflicto con estas reglas, se asume
que la implementación requiere auditoría o corrección — no la regla.

---

## Ámbito real del monorepo

Estructura **real** (prevalece sobre diagramas desactualizados):

| Capa | Ubicación |
|------|-----------|
| Backend (.NET 10) | `backend/src/ERP.Domain`, `ERP.Application`, `ERP.Infrastructure`, `ERP.API` |
| Módulos backend | `ERP.*/Modules/<Nombre>/` (p. ej. `Accounting`, `Customers`, `Branches`) |
| Frontend | `frontend/` — Vite + React |
| i18n | `frontend/src/i18n/locales/` (`es`, `en`, `qu`) |
| Reglas de implementación | `docs/architecture/*` (canónico) |
| Docs humanas | `CONTEXT.md`, `docs/ARCHITECTURE.md`, `STATUS.md`, `docs/DEVELOPMENT.md`, `docs/DATABASE.md` |

---

## Reglas de arquitectura que no se rompen

- **Entidades:** jerarquía `ERP.Domain.Common` — `BaseEntity` (`Id`/`TenantId`); agregados `AggregateRoot` → `AuditableEntity` → `MasterEntity` o `DocumentEntity`.
- **No existe `ERP.Shared`** en este monorepo. Código compartido: dentro del módulo (`modules/{dominio}/`) o librería aprobada en [STACK.md](./stack.md).
- **Multi-tenant:** toda query de datos de tenant filtra por `TenantId` (+ filtros globales `DbContext`).
- **Sin lógica de negocio** en Controllers ni en Infrastructure (más allá de persistencia/servicios técnicos).
- **Sin entidades de dominio en la API** — solo DTOs/contratos.
- **Soft delete:** `IsActive = false`; nunca DELETE físico de negocio salvo excepciones en [BACKEND-RULES.md](./backend.md).
- **Sin dependencias directas** entre módulos Application; comunicación vía contratos, MediatR u orquestación explícita.
- **Sin AutoMapper** — mapeos manuales en handlers.
- **`pages/*.tsx`:** solo wrappers de enrutamiento (≤15 líneas, cero hooks, cero lógica). Implementación en `modules/{dominio}/pages/`.
- Evitar carpetas `shared/` genéricas sin ownership claro.

### Frontera ERP ↔ Platform (BLOQUEANTE — ver [ADR-ERP-002](../decisions/ADR-ERP-002-platform-separation.md))

- **ERP NEVER DEPENDS ON PLATFORM**: ningún proyecto `ERP.*` referencia, importa ni compila contra código `Platform.*` / `ZH.Platform.*`. Prohibido en `ProjectReference`, `using`, DbContext, repositorios o entidades.
- **PLATFORM MAY CONSUME ERP APIs ONLY**: una futura Platform solo puede integrarse contra `/api/integration/v1/*` (policy `IntegrationApi`). Prohibido el acceso directo a `ErpDbContext`, repositorios ERP, entidades de dominio ERP o query filters ERP. Cualquier necesidad nueva se resuelve extendiendo `/api/integration/v{n}/*`, no abriendo accesos internos.

---

## Branch Ownership Rule (OBLIGATORIA)

> Decisión arquitectónica permanente (2026-07-18). Estandariza el uso de `TenantId`, `CompanyId` y `BranchId` en todo Aggregate Root que represente un proceso operativo, para mantener consistencia de dominio, seguridad, auditoría y reportes. Precedente de implementación: `SalesInvoice`, `PurchaseInvoice`, `StockMovement`, `CashSession` (ver `docs/decisions/` — Branch Ownership en documentos operativos).

### Regla

Todo **Aggregate Root o documento que represente un proceso operativo** debe persistir obligatoriamente:

- `TenantId`
- `CompanyId`
- `BranchId`

Esta regla aplica a cualquier operación cuya ejecución ocurra dentro de una sucursal de una empresa.

### Regla de decisión (obligatoria antes de crear un nuevo Aggregate Root)

Responder explícitamente:

1. ¿Representa un proceso operativo?
2. ¿Pertenece a una empresa?
3. ¿La operación ocurre dentro de una sucursal?

Si las tres respuestas son **SÍ**, la entidad persiste `TenantId` + `CompanyId` + `BranchId` como propiedades obligatorias del dominio (no opcionales, no agregadas "después").

### Aplica (ejemplos, no exhaustivo)

Ventas, Compras, Inventario, Caja, POS, Producción, Servicios, Órdenes de trabajo, Recepciones, Ajustes, Conteos físicos, y cualquier módulo operacional futuro.

### No aplica automáticamente

Catálogos maestros, Configuración, Parámetros globales, Catálogos SRI, Datos SaaS, Geografía, Entidades transversales, y cualquier otro caso donde la operación no pertenezca a una sucursal.

### Reglas de `BranchId`

- **Nunca** se recibe desde el cliente (Command/DTO de entrada no lo expone como propiedad).
- **Nunca** se modifica después de creada la entidad — sin setter público, sin método `ChangeBranch`/`SetBranch`.
- Se asigna **exclusivamente** desde:
  - el contexto backend (`ICurrentBranch`) dentro del handler de creación, o
  - la entidad propietaria cuando corresponda (ejemplo: `StockMovement.BranchId` se resuelve desde `Warehouse.BranchId` en el repositorio, no desde la sesión del operador — necesario para que las transferencias inter-sucursal registren cada movimiento con la sucursal real del almacén afectado, no con la del usuario que inició la operación).
- La persistencia de `BranchId` es para **trazabilidad histórica, auditoría y reportes** — no reemplaza el control de acceso, que sigue siendo responsabilidad exclusiva de `BranchScopeBehavior` / `IBranchAccessGuard` / `IInterBranchAccessGuard`.

### Excepciones

Si un Aggregate Root que cumple las 3 preguntas de la regla de decisión **no** persiste `BranchId`, la excepción debe justificarse explícitamente mediante una ADR o una decisión de arquitectura documentada. **No se permiten excepciones implícitas.**

### Objetivo

Convertir Branch Ownership en una regla permanente del ERP para que todos los módulos futuros mantengan el mismo modelo de dominio y evitar rediseños posteriores.

---

## API versionada (`/api/v1`)

Toda ruta de `ERP.API.Controllers` vive bajo `api/v1/...` (`[Route("api/v1/...")]`).
Excepciones:

- `Integration/IntegrationController.cs` — boundary propio, ya versionado como
  `api/integration/v1` (no usar `api/v1/integration/...`).
- `DevCacheController.cs` — diagnóstico interno (`/api/dev/*`), no es contrato
  público y no se versiona.

Reglas:

- **Nuevo controller/endpoint**: `[Route("api/v1/<recurso>")]`, salvo que sea
  diagnóstico interno (`api/dev/*`) o parte del boundary de integración
  (`api/integration/v{n}/*`).
- El frontend consume siempre `/api/v1/...` — ver `apiContractValidator.ts`
  (`REGISTERED_CONTRACTS`) para el contrato vigente.
- `EnterpriseDiagnosticMiddleware._noCompanyPrefixes` debe mantenerse en sync
  con los prefijos `api/v1/...` que operan sin `company_id` (`me`, `access`,
  `auth`, `setup`, `public`).

---

## Platform Kernel — permisos/navegación/módulos (single source of truth)

`ERP.Domain/Kernel/` es la **única fuente de verdad** de permisos, navegación y
módulos. No existen seeders de "negocio", catálogos de permisos duplicados ni
configuración manual de menú fuera de esta capa.

| Pieza | Ubicación | Rol |
|-------|-----------|-----|
| Permisos | `Kernel/Permissions/*Permissions.cs` | `const string` por dominio (p. ej. `InventoryPermissions.ItemsView = "items.view"`) |
| Módulos + navegación | `Kernel/Modules/*Module.cs` | clases `[Module("code", Icon=..., SortOrder=...)]` con `const string` de rutas decoradas `[NavItem(...)]` |
| Atributos | `Kernel/Attributes/ModuleAttribute.cs`, `NavItemAttribute.cs` | metadatos leídos por reflexión |
| Registro | `Kernel/KernelRegistry.cs` | `Modules`/`Permissions`/`Navigation` — derivados por reflexión del assembly `ERP.Domain`, sin SQL ni JSON externo |
| Seed EF | `ERP.Infrastructure/Seeding/Extensions/KernelSeedExtensions.cs` | adaptador delgado: `KernelRegistry` → `HasData()` para `ui_nav_groups`/`ui_nav_items`. Sin lógica de negocio |

Reglas:

- **Nuevo permiso**: agregar `const string` en `Kernel/Permissions/<Modulo>Permissions.cs`. Usar siempre `$"perm:{XPermissions.Y}"` en `[Authorize(Policy=...)]` y `[AppFeature(...)]` — nunca literales `"perm:..."`.
- **Nuevo ítem de menú**: agregar `const string` con `[NavItem(...)]` en `Kernel/Modules/<Modulo>Module.cs`; se sincroniza solo (sin tocar `ui_nav_groups`/`ui_nav_items` a mano).
- **Nuevo módulo**: clase `[Module("code", ...)]` en `Kernel/Modules/`.
- Prohibido: seeders EF con lógica de menú/permisos fuera de `KernelSeedExtensions`, catálogos de permisos paralelos (`ERP.Application.Common.Permissions` y similares — eliminados), configuración de navegación manual fuera de `[NavItem]`.
- Tests obligatorios: `ERP.Domain.Tests/Kernel/KernelRegistryTests.cs` (sin duplicados, sin huérfanos, GUIDs heredados preservados) y `ERP.Architecture.Tests/KernelControllerPolicyTests.cs` (toda policy `perm:X` de un controller existe en `KernelRegistry.Permissions`).

---

## Política de compatibilidad legacy

> **Regla de fase de proyecto.** Se aplica mientras el sistema no esté oficialmente en producción con clientes reales.

### Estado actual del proyecto

**Pre-producción.** No existen:
- Clientes productivos ni datos reales que preservar.
- Integraciones externas ni contratos públicos vigentes.
- Consumidores de API que no se puedan migrar junto con el código.

### Dónde SÍ están permitidos los mappers

El mapping legítimo vive **exclusivamente en capas de borde** y debe tener un consumidor identificado:

| Capa | Mapping permitido | Ejemplo |
|---|---|---|
| `ERP.Infrastructure` — EF value converters | DB string ↔ enum de dominio | `FromCode(v)` en `SalesDocumentConfiguration` |
| `ERP.Infrastructure` — XML builders | Enum de dominio → código SRI para XML | `ToSriDocCode()` en `SriXmlFacturaBuilder` |
| `ERP.Infrastructure` — Importadores | Formato externo → modelo de dominio | Parse de XML SRI recibido |
| `ERP.API` — Contratos | Request DTO → Command / Query | Mapeo manual en controllers |

**El dominio no mapea.** Los Value Objects y entidades de dominio expresan el modelo canónico directo, sin métodos de conversión a formatos externos.

### Prohibiciones absolutas (pre-producción)

Mientras el sistema no esté confirmado en producción, está **prohibido** introducir:

| Patrón prohibido | Ejemplos concretos |
|---|---|
| Backward compatibility | Aceptar formato antiguo y nuevo simultáneamente |
| Aliases vacíos en dominio | `SriCode => Type` (alias trivial sin consumidor diferenciado) |
| Legacy adapters en dominio | `NormalizeType()`, `MapOldToNew()` en Value Objects o entidades |
| Aliases de constantes duplicadas | `TypeRuc = "RUC"` cuando el código real ya es `"04"` |
| Mappers de transición en Application | `"RUC"→"04"` automático en lugar de corregir el origen |
| Código temporal | Cualquier bloque marcado `// TODO: remove when migrated` sin issue link |
| Versiones duplicadas de modelos | `BusinessPartnerV1` + `BusinessPartner` coexistiendo |
| Wrappers polimórficos | Endpoints que aceptan tanto el formato viejo como el nuevo |

### Regla de decisión

Ante una disyuntiva entre **mantener compatibilidad** y **corregir el diseño**:

```
Pre-producción → siempre corregir el diseño
Producción confirmada → evaluar caso por caso con evidencia documentada
```

**Corregir en el origen** significa actualizar todos los call-sites, tests, seeders y contratos para usar el modelo correcto. No significa añadir una capa de traducción.

### Cuándo puede introducirse compatibilidad

Solo cuando se cumplan **todas** las condiciones siguientes:

1. El sistema está oficialmente en producción (deploy real, usuarios reales).
2. Existen consumidores externos documentados que no pueden migrarse.
3. Se documenta explícitamente:
   - qué consumidor la necesita y por qué no puede migrarse,
   - fecha límite de eliminación (máx. 2 sprints),
   - PR o issue de seguimiento.

Sin estas tres condiciones, cualquier capa de compatibilidad es rechazada en PR review.

### Conflicto con EVENT-VERSIONING.md

`EVENT-VERSIONING.md` define política de compatibilidad histórica para **Domain Events y Outbox** (log inmutable). Esa política es independiente y permanece vigente porque:
- Los mensajes en el Outbox no pueden retroactivamente modificarse.
- Aplica solo al schema de eventos, no a modelos de dominio, APIs ni Value Objects.

Esta política de compatibilidad legacy **no aplica** al Outbox ni al schema de eventos.

---

## SUBSCRIBER SCOPE — Modelo canónico sellado

> **Estado:** ARCHIVADO (2026-07-23). El Control Plane SaaS (billing, suscripción, plan comercial) fue eliminado del ERP Core en "FASE 1 — ERP Kernel Cleanup" (2026-06-05, ver [`STATUS.md`](../../STATUS.md)) y queda excluido **permanentemente** por [`ERP_CORE_FREEZE.md`](../../ERP_CORE_FREEZE.md). El modelo canónico completo de ese scope (entidades `SubscriberBillingProfile`/`SubscriberBillingAccount`/`SubscriberSubscription`, comandos de billing, prohibiciones de duplicación) se conserva íntegro como registro histórico en [`docs/archive/SUBSCRIBER-SCOPE-SEALED.md`](../archive/SUBSCRIBER-SCOPE-SEALED.md) — no vigente, no usar como referencia de implementación en este repo.

### Regla activa heredada: identidad global sin duplicar

La única responsabilidad de este bloque que sigue vigente en el ERP Core (no forma parte del Control Plane SaaS archivado):

| Responsabilidad | Entidad canónica | Tabla | Prohibición |
|---|---|---|---|
| Identidad global de usuario | `IdentityUser` | `identity_users` | No crear segunda tabla de usuarios |

Scope: `tenant_id` (capa IAM, multi-tenant — ver [`docs/ARCHITECTURE.md`](../ARCHITECTURE.md#capas-iam-vs-erp-runtime)). No confundir con el modelo SUBSCRIBER de Control Plane SaaS archivado arriba.

---

## Patrón de referencia: módulo Accounting

Para un **módulo nuevo**, copiar la vertical por capas de **Accounting**:

| Capa | Ruta |
|------|------|
| Domain | `ERP.Domain/Modules/Accounting/` — entidades, VOs, interfaces |
| Application | `ERP.Application/Modules/Accounting/` — commands/queries, handlers, validators, DTOs |
| Infrastructure | `IEntityTypeConfiguration`, repositorios, `ErpDbContext` |
| API | Controllers delgados, autorización, sin reglas de negocio |

Si la feature es solo frontend o solo API, aplicar las capas que correspondan (p. ej. Zod en UI mock, pero no “validar solo en front” para datos persistidos).

---

## Flujo jerárquico (implementar una feature)

| Paso | Qué revisar | Documento |
|------|-------------|-----------|
| 0 | Contexto y archivos existentes | Este doc + `CONTEXT.md` |
| 1 | Dónde vive el código | [Ámbito real](#ámbito-real-del-monorepo) |
| 2 | Capas, tenant, DTOs, soft delete | [BACKEND-RULES.md](./backend.md) |
| 3 | Vertical por módulo | [Patrón Accounting](#patrón-de-referencia-módulo-accounting) |
| 4 | Validación extremo a extremo | [ENFORCEMENT.md](./enforcement.md) |
| 5 | Tokens y ZH Form | [FRONTEND-RULES.md](./frontend.md) |
| 6 | Tabs Datos vs listado | [FRONTEND-RULES.md#formularios-de-entidad-zh-form-tabs](./frontend.md) |
| 7 | Copy UX, PageShell | [FRONTEND-RULES.md#copy-ux](./frontend.md) |
| 8 | Menú sin duplicar `to` | [FRONTEND-RULES.md#menú-estático](./frontend.md) |
| 9 | IDs sensibles fuera de la URL | [SAAS-RULES.md](./security.md) |
| 10 | Claves i18n nuevas | [FRONTEND-RULES.md#i18n-kichwa-de-cañar](./frontend.md) |

**Regla práctica:** en frontend, no bajar a Copy UX sin alinear ZH Form + orden de tabs. En backend, no exponer endpoints sin Validator + reglas dominio/EF.

---

## ICE (Impuesto a Consumos Especiales) — diferido

No implementar hasta requerimiento del cliente. Base en dominio:

- `Product.AppliesExciseTax` + `Product.ExciseTaxId`
- `TaxRateType.Excise`
- Cuando se implemente: `IceCode`, `IcePercentage`, `IceAmount` en `SalesBillLine`/`SalesNoteLine`; XML SRI `<impuesto><codigo>3</codigo>`.

---

## Event-Driven Foundation (preparación para IA)

El ERP utiliza Domain Events + Outbox como base para analytics, automatización e IA futura.

**Reglas irrenunciables:**

- Los eventos de dominio salen **solo** desde AggregateRoots (`RaiseDomainEvent`)
- La capa Application puede **reaccionar** a eventos (handlers MediatR), no emitirlos directamente
- Infrastructure procesa el Outbox (job Hangfire `process-outbox`)
- La IA futura consumirá eventos via Outbox — **no** accediendo al DbContext del ERP directamente
- **Nunca** llamar LLMs/IA desde `ERP.Domain` o `ERP.Application`

### `IIntegrationEvent` — eventos exportables (sin bus aún)

`ERP.Domain.Common.IIntegrationEvent : IDomainEvent` marca eventos de dominio
candidatos a exportación futura hacia Platform vía Outbox + bus de eventos
(no implementado todavía — solo el marcador). Ejemplos ya marcados:
`ItemCreatedEvent`, `InvoiceCreatedEvent`, `InvoiceAuthorizedEvent`.

`backend/src/Platform.Contracts/` es un proyecto **solo-contratos** (sin
implementación, sin `ProjectReference` hacia/desde `ERP.*`, cumple
ADR-ERP-002) que define el espejo externo: marcador `IIntegrationEvent`
propio, DTOs de eventos exportables (`ProductCreatedIntegrationEvent`,
`InvoiceIssuedIntegrationEvent`, `StockAdjustedIntegrationEvent`),
`WebhookEnvelope<T>` e `IErpPublicApiClient` (espejo de
`/api/integration/v1/*`). Es la base para una futura Platform externa — no
agregar lógica de negocio ni referencias cruzadas.

Reglas detalladas: [EVENT-DRIVEN-RULES.md](./events.md)
Arquitectura IA futura: [AI-FOUNDATION.md](./ai-foundation.md)

---

## CI y ramas

| Rama | Uso |
|------|-----|
| `main` | Integración estable |
| `development` | Features diarias |
| `release/*` | Estabilización |
| `hotfix/*` | Correcciones urgentes |

Tests antes de merge: ver [ENFORCEMENT.md](./enforcement.md#tests-pre-merge).

---

# Canonical Model Map

> Nota de auditoría (2026-06-08, resuelta 2026-07-23): la sección "SUBSCRIBER (Control Plane SaaS)" que existía aquí describía entidades (`SubscriberBillingProfile`, `SubscriberBillingAccount`, `SubscriberSubscription`, tablas `subscriber_billing_*`) que `STATUS.md` registra como **eliminadas** en "FASE 1 — ERP Kernel Cleanup" (2026-06-05) y excluidas permanentemente del ERP Core por [`ERP_CORE_FREEZE.md`](../../ERP_CORE_FREEZE.md). Esa sección fue removida; el modelo se conserva como registro histórico en [`docs/archive/SUBSCRIBER-SCOPE-SEALED.md`](../archive/SUBSCRIBER-SCOPE-SEALED.md).

El sistema tiene **3 scopes** con responsabilidades exclusivas. Ningún scope puede contener lógica o datos del otro.

## GLOBAL IMMUTABLE (schema `global`)

**Propósito:** Catálogos regulatorios y estándares externos. Sin tenant scope.

| Tabla | Concepto canónico |
|---|---|
| `global.sri_vat_rate` | Tarifas IVA Ecuador (SRI) |
| `global.sri_ice_rate` | Tarifas ICE Ecuador (SRI) |
| `global.sri_retention_code` | Códigos de retención SRI |
| `global.sri_uom` | Unidades de medida SRI |
| `global.sri_doc_type` | Tipos de comprobante SRI |
| `global.sri_id_type` | Tipos de identificación SRI |
| `global.sri_payment_method` | Formas de pago SRI |
| `global.sri_tax_support` | Sustentos tributarios SRI |
| `global.sri_tax_regime` | Regímenes tributarios SRI |
| `global.sri_error_code` | Códigos de error SRI |
| `global.sri_environment` | Ambientes SRI |
| `global.sri_emission_type` | Tipos de emisión SRI |
| `global.sri_country` | Países (ISO) |
| `global.geo_provinces` | Provincias Ecuador (INEC) |
| `global.geo_cantons` | Cantones Ecuador (INEC) |
| `global.geo_parishes` | Parroquias Ecuador (INEC) |

**Regla GLOBAL:** Sin `tenant_id`. Sin `company_id`. Sin lógica de negocio. Referencia histórica (Control Plane SaaS, no vigente): [docs/archive/SUBSCRIBER-SCOPE-SEALED.md](../archive/SUBSCRIBER-SCOPE-SEALED.md)

## COMPANY (ERP Operativo)

**Propósito:** Todos los datos operativos del ERP. Con `company_id` obligatorio (o en transición vía `ICompanyOperationalEntity`).

| Módulo | Entidades canónicas |
|---|---|
| **Ventas** | `SalesBill`, `SalesNote`, `Invoice`, `SalesWithholding` |
| **Compras** | `PurchBill`, `PurchNote`, `PurchaseDocument`, `IssuedRetention` |
| **Inventario** | `Warehouse`, `StockMovement`, `CurrentStock`, `StockAdjustment` |
| **Contabilidad** | `Account`, `JournalEntry`, `AccountingPeriod` |
| **Productos** | `Product` (con `UomCode`→`global.sri_uom`, `SaleVatCode`→`global.sri_vat_rate`) |
| **MasterData** | `BusinessPartner`, `CompanyBusinessPartnerSettings` |
| **Sucursales** | `Branch`, `Establishment`, `EmissionPoint` |
| **Gastos** | `ExpenseInvoice`, `ExpenseDocument` |
| **Comercial** | `Quote`, `SalesOrder` |
| **Caja** | `BankAccount`, `PettyCash`, `BankStatement` |
| **Fiscal** | `SriSettings`, `DocumentSequence`, `DigitalCertificate` |

## REGLA FUNDAMENTAL: 1 CONCEPTO = 1 IMPLEMENTACIÓN

```
1 Entidad canónica
1 DTO principal (+ DTO detallado si difiere en campos)
1 Command de escritura por operación
1 Query de lectura por caso de uso
1 Repository por agregado raíz
```

## Patrones permitidos

### DTO List vs Detail (PERMITIDO)

Es válido tener dos DTOs del mismo concepto cuando:
- **ListDto**: campos para tabla/listado (id, nombre, estado, fecha)
- **DetailDto**: campos para vista de detalle (+ navegaciones, líneas, historial)

```csharp
// ✅ CORRECTO — mismo concepto, propósito distinto
public record PurchBillDto(Guid Id, string InvoiceNumber, PurchaseStatus Status, ...);
public record PurchBillDetailDto(Guid Id, ..., IReadOnlyList<PurchBillLineDto> Lines);
```

**Límite:** Máximo 2 DTOs por entidad (List + Detail). Si se necesita un tercero, revisar si no hay duplicación semántica.

### Queries especializadas (PERMITIDO)

```csharp
// ✅ CORRECTO — mismo agregado, propósito distinto
GetProductsQuery       // listado con filtros
GetProductByIdQuery    // detalle completo
GetProductFullReport   // reporte con todos los children
```

## Patrones prohibidos

### ❌ Variantes de mismo concepto

```csharp
// ❌ PROHIBIDO — mismo concepto, distinto nombre
CreateProductCommand
AddProductCommand       // duplicado semántico de CreateProductCommand
RegisterProductCommand  // duplicado semántico
```

### ❌ DTOs con mismo propósito y distinto nombre

```csharp
// ❌ PROHIBIDO — mismo shape, distinto nombre
ProductDto
ProductSummaryDto       // si tiene los mismos campos que ProductDto
ProductResponseDto      // wrapping innecesario
```

### ❌ Naming patterns de degradación

```csharp
// ❌ PROHIBIDO — naming patterns que indican duplicación
BillingSettingsV2
SubscriberProfileLegacy
AlternativeTaxRate
ExtendedProductDto
ShadowInvoice
FallbackBillingConfig
```

### ❌ Cross-domain injection

```csharp
// ❌ PROHIBIDO — lógica SaaS en módulo ERP
namespace ERP.Application.Modules.Sales {
    // No puede referenciar SubscriberBillingAccount
    // No puede referenciar CommercialPlan
    // No puede tener lógica de suscripción
}
```

```csharp
// ❌ PROHIBIDO — lógica ERP en SUBSCRIBER
namespace ERP.Domain.Billing.Entities {
    // No puede tener Invoice (ERP)
    // No puede tener Product (ERP)
    // No puede tener TaxRate (GLOBAL)
}
```

### ❌ Tablas per-subscriber que duplican datos GLOBAL

```csharp
// ❌ PROHIBIDO — estas tablas fueron eliminadas y no pueden recrearse
tax_rates           // → global.sri_vat_rate + global.sri_ice_rate
units_of_measure    // → global.sri_uom
retention_settings  // → global.sri_retention_code
billing_settings    // → subscriber_billing_profile
```

## Enforcement ruleset (B-08 a B-11)

> Estas 4 reglas son la única fuente de verdad de B-08–B-11. [`pr-rules-catalog.md`](./pr-rules-catalog.md) las referencia por número — no las repite.

### B-08 — Single Command per Operation (BLOQUEANTE)

**Regla:** Para cada operación de escritura sobre un agregado, solo puede existir UN Command. Dos Commands con el mismo target y misma acción son duplicación.

```
✅ CreateProductCommand     — crear producto
✅ UpdateProductCommand     — actualizar producto
❌ AddProductCommand        — duplica CreateProductCommand
❌ ModifyProductCommand     — duplica UpdateProductCommand
```

### B-09 — No Semantic DTO Duplication (BLOQUEANTE)

**Regla:** Máximo 2 DTOs por entidad (List + Detail). Un tercer DTO requiere justificación documentada en el PR que demuestre propósito distinto.

```
✅ ProductDto              — campos para listado
✅ ProductDetailDto        — campos para vista completa + children
❌ ProductSummaryDto       — si tiene los mismos campos que ProductDto
❌ ProductResponseDto      — wrapping sin valor añadido
```

### B-10 — Scope Boundary Enforcement (BLOQUEANTE)

**Regla:** Las entidades de dominio no pueden cruzar boundaries de scope:

| Desde | Puede referenciar | NO puede referenciar |
|---|---|---|
| SUBSCRIBER *(histórico — Control Plane SaaS eliminado; boundary conservado como guardrail preventivo)* | Solo entidades SUBSCRIBER | Entidades COMPANY, lógica ERP |
| COMPANY | Entidades COMPANY + catálogos GLOBAL | Entidades SUBSCRIBER billing |
| GLOBAL | Solo datos estáticos propios | Nada de SUBSCRIBER ni COMPANY |

### B-11 — No Per-Subscriber Regulatory Data (BLOQUEANTE)

**Regla:** Ninguna tabla per-subscriber puede almacenar datos que ya existen en `global.*`. Los productos, facturas y retenciones deben referenciar directamente `global.sri_*` por código.

```
✅ Product.UomCode = "19"          → referencia global.sri_uom
✅ Product.SaleVatCode = "10"      → referencia global.sri_vat_rate
❌ TaxRate(subscriber_id, 15%)     → duplica global.sri_vat_rate
❌ UnitOfMeasure(subscriber_id)    → duplica global.sri_uom
```

## Governance checklist (PR review)

Antes de mergear cualquier PR que afecte Domain, Application o Infrastructure:

```
□ ¿La nueva entidad duplica semánticamente alguna existente?
□ ¿El nuevo DTO tiene propósito diferente a los DTOs existentes del mismo concepto?
□ ¿El nuevo Command hace algo diferente al Command de escritura existente?
□ ¿El nuevo servicio tiene responsabilidad distinta al servicio existente?
□ ¿Los datos regulatorios referencian global.sri_* en lugar de tablas per-subscriber?
□ ¿El código del módulo COMPANY NO importa entidades SUBSCRIBER billing?
□ ¿El código del módulo SUBSCRIBER NO importa lógica ERP operativa?
□ ¿El naming evita patrones prohibidos (V2, Legacy, Alternative, Extended, Shadow)?
```

Si cualquier checkbox es "SÍ" (hay violación) → **BLOQUEAR PR**.

## Referencia cruzada (Canonical Model Map)

| Área | Documento |
|---|---|
| Subscriber scope (histórico, archivado) | [docs/archive/SUBSCRIBER-SCOPE-SEALED.md](../archive/SUBSCRIBER-SCOPE-SEALED.md) |
| Política legacy pre-prod | [§ Política de compatibilidad legacy](#política-de-compatibilidad-legacy) más arriba en este documento |
| Reglas bloqueantes PR | [pr-rules-catalog.md](./pr-rules-catalog.md) (B-07 a B-11) |
| Catálogos globales | Schema `global` — 16 tablas SRI + INEC |

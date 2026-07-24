# Arquitectura core — ERP SaaS ZH Technologies

Reglas estructurales del monorepo. Detalle PR bloqueante: [PR-RULES-CATALOG.md](./PR-RULES-CATALOG.md).

---

## Antes de actuar

1. Lee [README.md](./README.md) y [`CONTEXT.md`](../CONTEXT.md) (índice).
2. Identifica si el archivo a crear/modificar **ya existe** → no regenerar; cambiar solo lo necesario.
3. Define un plan breve antes de escribir código.
4. Contexto descriptivo: `docs/ARCHITECTURE.md` (diagramas), `docs/STATUS.md` (estado), `docs/DEVELOPMENT.md` (arranque).

### Rol del agente

En este proyecto el agente actúa como arquitecto de software/dominio, revisor
técnico, auditor de calidad y protector de la arquitectura — no solo como
generador de código. Cada decisión prioriza arquitectura, consistencia,
reutilización, escalabilidad y mantenibilidad sobre la velocidad de
implementación. Sustancia de estas reglas ya vive, sin duplicar aquí, en:
Clean Architecture/capas → este archivo, sección "Ámbito real del monorepo";
CQRS/MediatR/FluentValidation → [BACKEND-RULES.md](./BACKEND-RULES.md);
multi-tenancy (Tenant→Company) → [SECURITY.md](./SECURITY.md); 1 concepto = 1 implementación / anti-duplicación →
[ARCHITECTURE-GOVERNANCE.md](./ARCHITECTURE-GOVERNANCE.md); infraestructura
CLOSED/FROZEN → `CLAUDE.md` (raíz); Design System / reutilización de UI →
[FRONTEND-RULES.md](./FRONTEND-RULES.md#reutilización-obligatoria--auditoría-previa-a-crear-ui);
naming → [NAMING.md](./NAMING.md); gobernanza automática/checkers →
[ENFORCEMENT.md](./ENFORCEMENT.md).

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
| Reglas IA | `AI-RULES/` (canónico) |
| Docs humanas | `CONTEXT.md`, `docs/ARCHITECTURE.md`, `docs/STATUS.md`, `docs/DEVELOPMENT.md`, `docs/DATABASE.md` |

---

## Reglas de arquitectura que no se rompen

- **Entidades:** jerarquía `ERP.Domain.Common` — `BaseEntity` (`Id`/`TenantId`); agregados `AggregateRoot` → `AuditableEntity` → `MasterEntity` o `DocumentEntity`.
- **No existe `ERP.Shared`** en este monorepo. Código compartido: dentro del módulo (`modules/{dominio}/`) o librería aprobada en [STACK.md](./STACK.md).
- **Multi-tenant:** toda query de datos de tenant filtra por `TenantId` (+ filtros globales `DbContext`).
- **Sin lógica de negocio** en Controllers ni en Infrastructure (más allá de persistencia/servicios técnicos).
- **Sin entidades de dominio en la API** — solo DTOs/contratos.
- **Soft delete:** `IsActive = false`; nunca DELETE físico de negocio salvo excepciones en [BACKEND-RULES.md](./BACKEND-RULES.md).
- **Sin dependencias directas** entre módulos Application; comunicación vía contratos, MediatR u orquestación explícita.
- **Sin AutoMapper** — mapeos manuales en handlers.
- **`pages/*.tsx`:** solo wrappers de enrutamiento (≤15 líneas, cero hooks, cero lógica). Implementación en `modules/{dominio}/pages/`.
- Evitar carpetas `shared/` genéricas sin ownership claro.

### Frontera ERP ↔ Platform (BLOQUEANTE — ver [ADR-ERP-002](../docs/architecture/decisions/ADR-ERP-002-platform-separation.md))

- **ERP NEVER DEPENDS ON PLATFORM**: ningún proyecto `ERP.*` referencia, importa ni compila contra código `Platform.*` / `ZH.Platform.*`. Prohibido en `ProjectReference`, `using`, DbContext, repositorios o entidades.
- **PLATFORM MAY CONSUME ERP APIs ONLY**: una futura Platform solo puede integrarse contra `/api/integration/v1/*` (policy `IntegrationApi`). Prohibido el acceso directo a `ErpDbContext`, repositorios ERP, entidades de dominio ERP o query filters ERP. Cualquier necesidad nueva se resuelve extendiendo `/api/integration/v{n}/*`, no abriendo accesos internos.

---

## Branch Ownership Rule (OBLIGATORIA)

> Decisión arquitectónica permanente (2026-07-18). Estandariza el uso de `TenantId`, `CompanyId` y `BranchId` en todo Aggregate Root que represente un proceso operativo, para mantener consistencia de dominio, seguridad, auditoría y reportes. Precedente de implementación: `SalesInvoice`, `PurchaseInvoice`, `StockMovement`, `CashSession` (ver `docs/adr/` — Branch Ownership en documentos operativos).

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

> **Estado:** ARCHIVADO (2026-07-23). El Control Plane SaaS (billing, suscripción, plan comercial) fue eliminado del ERP Core en "FASE 1 — ERP Kernel Cleanup" (2026-06-05, ver [`docs/STATUS.md`](../docs/STATUS.md)) y queda excluido **permanentemente** por [`ERP_CORE_FREEZE.md`](../ERP_CORE_FREEZE.md). El modelo canónico completo de ese scope (entidades `SubscriberBillingProfile`/`SubscriberBillingAccount`/`SubscriberSubscription`, comandos de billing, prohibiciones de duplicación) se conserva íntegro como registro histórico en [`docs/archive/SUBSCRIBER-SCOPE-SEALED.md`](../docs/archive/SUBSCRIBER-SCOPE-SEALED.md) — no vigente, no usar como referencia de implementación en este repo.

### Regla activa heredada: identidad global sin duplicar

La única responsabilidad de este bloque que sigue vigente en el ERP Core (no forma parte del Control Plane SaaS archivado):

| Responsabilidad | Entidad canónica | Tabla | Prohibición |
|---|---|---|---|
| Identidad global de usuario | `IdentityUser` | `identity_users` | No crear segunda tabla de usuarios |

Scope: `tenant_id` (capa IAM, multi-tenant — ver [`docs/ARCHITECTURE.md`](../docs/ARCHITECTURE.md#capas-iam-vs-erp-runtime)). No confundir con el modelo SUBSCRIBER de Control Plane SaaS archivado arriba.

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
| 2 | Capas, tenant, DTOs, soft delete | [BACKEND-RULES.md](./BACKEND-RULES.md) |
| 3 | Vertical por módulo | [Patrón Accounting](#patrón-de-referencia-módulo-accounting) |
| 4 | Validación extremo a extremo | [ENFORCEMENT.md](./ENFORCEMENT.md) |
| 5 | Tokens y ZH Form | [FRONTEND-RULES.md](./FRONTEND-RULES.md) |
| 6 | Tabs Datos vs listado | [FRONTEND-RULES.md#formularios-de-entidad-zh-form-tabs](./FRONTEND-RULES.md) |
| 7 | Copy UX, PageShell | [FRONTEND-RULES.md#copy-ux](./FRONTEND-RULES.md) |
| 8 | Menú sin duplicar `to` | [FRONTEND-RULES.md#menú-estático](./FRONTEND-RULES.md) |
| 9 | IDs sensibles fuera de la URL | [SAAS-RULES.md](./SAAS-RULES.md) |
| 10 | Claves i18n nuevas | [FRONTEND-RULES.md#i18n-kichwa-de-cañar](./FRONTEND-RULES.md) |

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

Reglas detalladas: [EVENT-DRIVEN-RULES.md](./EVENT-DRIVEN-RULES.md)
Arquitectura IA futura: [AI-FOUNDATION.md](./AI-FOUNDATION.md)

---

## CI y ramas

| Rama | Uso |
|------|-----|
| `main` | Integración estable |
| `development` | Features diarias |
| `release/*` | Estabilización |
| `hotfix/*` | Correcciones urgentes |

Tests antes de merge: ver [ENFORCEMENT.md](./ENFORCEMENT.md#tests-pre-merge).

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
| i18n | `frontend/src/i18n/locales/` (`es`, `en`) |
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
| 10 | Claves i18n nuevas | [FRONTEND-RULES.md#i18n](./frontend.md) |

**Regla práctica:** en frontend, no bajar a Copy UX sin alinear ZH Form + orden de tabs. En backend, no exponer endpoints sin Validator + reglas dominio/EF.

---

## ICE (Impuesto a Consumos Especiales)

Catálogo vigente, fuentes y disponibilidad: [§ ICE: definición oficial](#ice-definición-oficial-y-corrección-de-referencias).
Límite del cálculo mixto: [§ ICE mixto](#ice-mixto-representación-global-y-límite-operativo).

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

**Propósito:** catálogos regulatorios, estándares externos y catálogos internos de sistema, sin `tenant_id`
ni `company_id` y sin lógica de negocio. Esta sección es la **única definición vigente** de GLOBAL; los
documentos de `docs/sri/` son auditorías fechadas y las decisiones de origen están en ADR-037/ADR-042.

**Reglas (ADR-037 D1–D14, ADR-042):**

- Un catálogo = una tabla y una entidad tipadas. Canal único de datos: `HasData` materializado por la baseline EF
  única de ERP ZH V1 (`20261021162651_InitialProductionBaseline`, GREENFIELD: incluye todas las correcciones SRI),
  salvo la geografía INEC, que llega por el único script `Seeding/InstallData` (ver § Geografía INEC). Tras la
  primera instalación publicada, cada cambio es una migración incremental. Sin CRUD, UI de edición ni escrituras
  desde Application/API.
- `is_active` es **disponibilidad operativa** para operaciones nuevas, no validez normativa. Las resoluciones por
  Id o código de documentos existentes leen también filas no seleccionables. Ningún catálogo usa `HasQueryFilter`.
  Los flags `bool` con DEFAULT true se configuran con `ValueGeneratedNever` (ADR-042).
- No se borran filas: un código obsoleto queda no seleccionable y conserva sus referencias. Tras la primera
  instalación publicada, toda corrección de significado revisa referencias antes de escribir y aborta si encuentra
  alguna; el rollback automático se rechaza.
- Cada dato normativo cita su fuente: en `sri_normative_source` cuando el catálogo tiene vigencia; en esta sección
  para los catálogos estables. Ninguna tarifa, código o regla se infiere sin fuente oficial.

**Inventario (27 tablas).** OF = catálogo oficial SRI · INT = interna relacionada con SRI · ERP = propia del ERP.

| Tabla | Clase | Fuente oficial | Definición vigente |
|---|---|---|---|
| `sri_ice_rate` | OF | Ficha 2.34 Tabla 18; LRTI art. 82; NAC-DGERCGC25-00000040 | § ICE |
| `sri_irbpnr_rate` | OF | Ficha 2.34 Tabla 16 (`5001`, USD 0,02 por botella) | KEEP |
| `sri_vat_rate` | OF | Ficha 2.34 Tabla 17 | KEEP códigos; vigencia histórica del 12% diferida (§ Pendientes) |
| `sri_retention_code` | OF | Ficha 2.34 Tabla 20; Catálogo ATS 06/08/2026 Tabla 3.10 | § Retenciones e ISD |
| `sri_retention_code_version` | OF | idem | § Retenciones e ISD |
| `sri_payment_method` | OF | Ficha 2.34 Tabla 24 / ATS Tabla 13 | KEEP (01, 15–21) |
| `sri_tax_support` | OF | Catálogo ATS 06/08/2026 Tabla 5 | § Sustentos tributarios |
| `sri_supplier_type` | OF | Ficha 2.34 Tabla 26 / ATS Tabla 14 | KEEP (01, 02) |
| `sri_doc_type` | OF | Ficha 2.34 Tabla 3 (electrónicos) / ATS Tabla 4 | § Tipos documentales |
| `sri_id_type` | OF | Ficha 2.34 Tabla 6 | § Identificación |
| `sri_error_code` | OF | Ficha 2.34 §11 | § Errores |
| `sri_emission_type` | OF | Ficha 2.34 Tabla 2 | `1` emisión normal; `2` histórica (no existe en offline), sin consumidores |
| `sri_id_type_usage` | INT | Notas de la Tabla 6 y XSD | § Identificación |
| `sri_tax_regime` | INT | Ficha 2.34 Anexo 22 (leyendas RIMPE) | § Régimen tributario |
| `sri_country` | INT | ISO-3166 + Ficha 2.34 Tabla 25 / ATS Tabla 16 | § Países |
| `sri_uom` | INT | Sin catálogo SRI codificado (`unidadMedida` es texto libre) | Catálogo interno de unidades |
| `sri_normative_source` | INT | — | Registro de fuentes; sin `is_active` |
| `sri_annulment_policy` / `sri_annulment_policy_version` | INT | NAC-DGERCGC16-00000092; NAC-DGERCGC25-00000017 | Plazo de anulación por fecha de emisión |
| `doc_type_sri_map` | INT | — | Mapa tipo interno → `sri_doc_type` (sin consumidor en runtime) |
| `legal_entity_type` | INT | Tercer dígito del RUC | Natural / sociedad privada / institución pública |
| `doc_type` | ERP | — | Tipos de documento/proceso internos |
| `barcode_types` | ERP | — | Tipos de código de barras |
| `item_margin_statuses` | ERP | — | Estados de margen de ítems (presentación) |
| `geo_provinces` / `geo_cantons` / `geo_parishes` | ERP (estándar INEC) | INEC Clasificador Geográfico Estadístico 2026 | § Geografía INEC |

No existe tabla de ambientes: el ambiente (1 pruebas, 2 producción; Tabla 4) es la constante estructural
`SriEnvironmentCodes`, con espejo de validación en frontend.

### ICE: definición oficial y corrección de referencias

Definición vigente del catálogo ICE. `global.sri_ice_rate` mantiene una sola fila por código, cargada por
`HasData` (`SriIceRateConfiguration`) en la baseline; cada fila referencia su fuente en
`global.sri_normative_source` y registra `valid_from`/`valid_until` (inclusivos) solo cuando la vigencia fue comprobada.

**Fuentes (consultadas el 2026-10-21):**

- Identidad de códigos y conceptos: [Ficha técnica SRI offline 2.34](https://www.sri.gob.ec/o/sri-portlet-biblioteca-alfresco-internet/descargar/f8d9bb36-5632-4f96-b463-b9265b55338c/FICHA%20TE%CC%81CNICA%20COMPROBANTES%20ELECTRO%CC%81NICOS%20ESQUEMA%20OFFLINE%20Versio%CC%81n%202.34.pdf),
  julio de 2026, §9.13, Tabla 18, pp. 30–32 (71 códigos únicos). Sus columnas de tarifa son de enero y
  febrero-diciembre de 2023: no son vigencias ni tarifas de 2026.
- Tarifas específicas 2026: [NAC-DGERCGC25-00000040](https://www.sri.gob.ec/o/sri-portlet-biblioteca-alfresco-internet/descargar?id=c5a91b7c-c7ff-45c2-9b0d-ca0670fb8723&nombre=NAC-DGERCGC25-00000040.pdf),
  aplicable 2026-01-01 a 2026-12-31: cigarrillos USD 0,16 por unidad; alcohol y bebidas alcohólicas USD 10,41,
  cerveza artesanal USD 1,56 y cerveza industrial USD 13,62 por litro de alcohol puro; bebidas no alcohólicas
  y gaseosas con más de 25 g de azúcar por litro (excepto energizantes) USD 0,18 por 100 g de azúcar añadida;
  fundas plásticas USD 0,08 por funda.
- Tarifas ad valorem: [LRTI art. 82](https://www.sri.gob.ec/o/sri-portlet-biblioteca-alfresco-internet/descargar/7da8c609-4299-4597-8825-29446242c63b/LEY_DE_REGIMEN_TRIBUTARIO_INTERNO_-_LRTI.pdf)
  (codificación SRI, última reforma RO 525 del 2024-03-25), con ad valorem 75% para bebidas alcohólicas y cerveza
  (grupo IV). Prevalece sobre la columna de la Tabla 18: televisión pagada `3092` 15% (no 0%), clubes `3660` 30% (no 35%).
- Reformas del D.E. 645 sin efecto desde 2024-05-21 ([Oficio SRI-NAC-DNJ-2024-0042-OF, SENAE Boletín 88-2024](https://www.aduana.gob.ec/gaceta-boletin/tarifas-ad-valorem-del-impuesto-a-los-consumos-especiales-ice-que-rigen-por-la-derogacion-del-decreto-ejecutivo-no-645-de-10-de-enero-de-2023/)):
  tabaco calentado y líquidos con nicotina 150%; aviones, avionetas, helicópteros, motos acuáticas, tricares,
  cuadrones, yates y barcos de recreo 15%.
- Gravados, exenciones y no gravados: [matriz resumen ICE del SRI](https://www.sri.gob.ec/o/sri-portlet-biblioteca-alfresco-internet/descargar/3ba17b2c-6e81-4383-8d3e-ec6f7ee1160e/Tabla%20Resumen%20ICE%20.xlsx), hoja 2024.

**Catálogo (77 filas, 38 seleccionables):**

| Clase | Códigos | Representación | Seleccionable |
|---|---|---|---|
| Mixta, grupo IV | `3031`, `3033`, `3041`, `3043`, `3532`, `3533`, `3541`, `3545` | 75% + específica 2026 por litro de alcohol puro | No (ver ICE mixto) |
| Específica con referencias o importación XML existente | `3011`, `3021`, `3053`, `3111` | `Specific`, `unit_value` nulo, unidad y vigencia 2026 | Sí |
| Específica nueva | `3542`, `3543`, `3552`, `3602`, `3680` | `Specific` con tarifa 2026 completa | No |
| Ad valorem (art. 82) | tabaco `3023`/`3544`; gaseosas ≤25 g y energizantes `3054`/`3553`/`3101`/`3601`; perfumes `3610`/`3710`; focos `3640`/`3740`; vehículos `3073`, `3075`, `3077`–`3080`, `3684`–`3687`, `3871`, `3873`–`3877`; `3092`; `3660` | `Percentage`, `valid_from` nulo (inicio no comprobado) | Sí |
| Restablecida desde 2024-05-21 | `3682`/`3683` 150%; `3081`/`3878`/`3581`/`3582` 15% | `Percentage`, `valid_from` 2024-05-21 | Sí |
| Tarifa 2026 no verificada | `3630` (conserva 30%), `3730` (sin tarifa) | `Percentage` | No |
| Sin gravamen vigente | videojuegos `3620`/`3720`; calefones `3671`/`3771`; telefonía `3093`/`3681`; híbridos `3688`–`3699` (exentos) | sin tarifa | No |
| Clave histórica ausente de Tabla 18 | `3051`, `3071`, `3072`, `3082`, `3083`, `3091` | valores anteriores conservados | No |

**Disponibilidad (`is_active`):** una fila es seleccionable solo si todos los consumidores operativos la calculan
correctamente. Ventas y las líneas manuales de Compras multiplican `unit_value` por la cantidad en UoM base, lo que
no equivale a cigarrillos dentro de un paquete, gramos de azúcar ni litros de alcohol puro. Por eso las específicas
seleccionables mantienen `unit_value` nulo: Compras conserva el monto exacto del XML del proveedor y deshabilitarlas
rompería esa importación. Las tarifas siguen registradas en su fuente. `3680` no es seleccionable porque el
Anexo 18 usa el mismo código para fundas normales, con rebaja del 50% y exentas.

**Significados corregidos antes de la primera versión:** `3073` era armas y `3011`/`3021` ad valorem 150%;
`3041` era gaseosas, `3081` vehículos, `3101` televisión y `3111` bebidas alcohólicas. La baseline nace con los
significados de la Tabla 18; no hay referencias que remapear.

### Tipos documentales electrónicos

Electrónicos (ficha 2.34, §5.4, Tabla 3): exclusivamente `01`, `03`, `04`, `05`, `06` y `07`.
`02`, `08`, `09` y `18` son tipos del Catálogo ATS 06/08/2026, Tabla 4, no electrónicos y no seleccionables:
`02` Nota o boleta de venta, `08` Boletos o entradas a espectáculos públicos, `09` Tiquetes o vales emitidos por
máquinas registradoras, `18` Documentos autorizados utilizados en ventas excepto N/C N/D. La ATS Tabla 4 tiene más
códigos (11, 12, 15, 41…) que no se cargan mientras ningún flujo los use; no se infieren.
`02` nace con `is_electronic = false` y `02`/`08`/`18` con los nombres y `short_name` de la Tabla 4.

### ICE mixto: representación GLOBAL y límite operativo

EXTEND aprobado por el propietario el 2026-10-21: `SriTaxCalculationType` conserva `Percentage = 1`
y `Specific = 2`, y añade `Mixed = 3`. La misma fila de `global.sri_ice_rate` conserva simultáneamente
`Percentage` y `UnitValue`; no se suman ni se convierten a un porcentaje equivalente. `SpecificUnit`
declara la moneda/base física publicada y `NormativeSourceId` referencia el registro normativo existente.
`SpecificUnit` y `NormativeSourceId` son opcionales para las filas no mixtas.

Fuentes consultadas el 2026-10-21:

- Ficha técnica offline SRI 2.34, julio de 2026, Tabla 18, páginas 30–31: códigos `3031` (bebidas
  alcohólicas), `3033` (alcohol) y componente ad valorem 75%. Sus columnas de tarifa específica son de 2023;
  no se extrapolan a 2026.
- [NAC-DGERCGC25-00000040](https://www.sri.gob.ec/o/sri-portlet-biblioteca-alfresco-internet/descargar?id=c5a91b7c-c7ff-45c2-9b0d-ca0670fb8723&nombre=NAC-DGERCGC25-00000040.pdf),
  firmada el 2025-12-29, artículo único, período fiscal 2026, aplicable desde 2026-01-01: USD 10,41 por
  litro de alcohol puro para alcohol (uso distinto a bebidas alcohólicas y farmacéuticos) y bebidas alcohólicas.
  `PublishedOn` queda nulo: la fecha de firma no se presenta como fecha de publicación en Registro Oficial.

No se incorporan otros conceptos mixtos sin comprobar sus componentes y condiciones normativas.

Los conceptos mixtos quedan `IsActive = false` por disponibilidad **operativa**, no por invalidez normativa.
`ck_sri_ice_mixed_catalog_only` exige ambos componentes, unidad no vacía, fuente y deshabilitación;
impide activarlos mientras no exista soporte operativo aprobado. `ValueGeneratedNever` conserva el false por EF
con el DEFAULT true (ADR-042). La habilitación futura necesita una migración revisada que sustituya
ese bloqueo junto con la implementación y validación operativa. No existe CRUD de catálogo.

| Contrato / consumidor directo | Estado e impacto |
|---|---|
| `SriIceRate` / `SriTaxCalculationType` | EXTEND: componentes simultáneos y metadata, sin cambiar los valores persistidos 1/2. |
| `SriIceRateDto` / `GetSriIceRatesQueryHandler` | EXTEND aditivo: `unitValue`, `calculationType`, `specificUnit`; conserva `code`, `name`, `percentage`. El endpoint sigue ofreciendo solo activos. |
| `SriTaxCatalogEntry` / `SriTaxResolver` | KEEP: la proyección ya admite ambos importes y el enum; los métodos operativos no resuelven mixtos deshabilitados. No se habilita una fórmula nueva. |
| `SriCatalogLookupRepository`, `SriCatalogResolver`, `SriGlobalRateReader` | KEEP: filtran disponibilidad; los mixtos no alcanzan selects ni proyecciones escalares de porcentaje. |
| Compras: `PurchaseDraftUseCases`, `ConfirmPurchaseUseCases`, `PurchaseCostUseCases`, `PurchaseInvoiceDetail` | STRUCTURAL DEBT: discriminan `Specific` frente a porcentual; con Mixed activo omitirían el componente específico o recalcularían un importe XML. Requiere aprobar captura/snapshot de ambos componentes, bases y conservación del total XML; parte detenida sin modificar esos archivos. |
| Ventas: `SalesDraftUseCases`, `AuthorizeSalesUseCases`, `SalesReturnDraftUseCases`, `SalesInvoiceDetail` | STRUCTURAL DEBT: requiere resolver componentes sobre bases distintas y conservarlos en recálculo/devolución. `QuantityInBaseUom` no demuestra litros de alcohol puro. Condiciones de aplicación, cantidades físicas y snapshot requieren alcance propio; parte detenida. |
| Frontend `purchaseCalc.ts`, `salesCalc.ts` y mapas de tarifas de sus hooks | STRUCTURAL DEBT: los mapas `Record<string, number>` solo representan porcentaje. Requieren contratos completos y reglas concordantes con backend; sin modificar sus cálculos. |
| Contabilidad | DO NOT TOUCH: no se identificó consumo directo del modo Mixed; consume importes documentales. No se modifican traductores, cuentas, reglas ni eventos. |

La representación GLOBAL queda implementada; el **cálculo y la selección operativos** de ICE mixto permanecen
detenidos conforme al alcance autorizado. Los demás conceptos mixtos (`3041`, `3043`, `3532`, `3533`, `3541`, `3545`)
siguen el mismo límite; el catálogo completo está en § ICE: definición oficial. No se reinterpretan documentos anteriores.

### Identificación

`sri_id_type` mantiene `04`–`08` de la ficha 2.34, Tabla 6, y la clave histórica `09` (Placa), que no está en la
Tabla 6 y que los XSD de factura y guía de remisión rechazan (`[0][4-8]`). `sri_id_type_usage` gobierna la
disponibilidad por contexto: los usos `09/Customer` y `09/Carrier` quedan no seleccionables. Efecto:
`IdentificationUsageValidator` impide asignar esos roles, y la actualización de un socio existente con `09` y ese
rol devuelve un error explícito en vez de producir un XML que el SRI rechazaría. Columnas en snake_case
(`id`, `id_type_code`, `usage_type`, `is_active`).

### Régimen tributario

`01` Régimen General; `02` RIMPE – Emprendedor y `03` RIMPE – Negocio Popular, según la ficha 2.34
(historial 2.22 y Anexo 22, que fija las leyendas `CONTRIBUYENTE RÉGIMEN RIMPE` y
`CONTRIBUYENTE NEGOCIO POPULAR - RÉGIMEN RIMPE`). `Abbrev` (`RIMPE_ME`, `RIMPE_NP`) es clave interna y no cambia.
`04` Contribuyente Especial no es un régimen: la ficha lo emite como número de resolución
(`contribuyenteEspecial`, `Company.SpecialTaxpayerNo`), así que queda no seleccionable. Una empresa que lo tenga
guardado lo conserva; Configuración › Fiscal lo muestra como "no seleccionable" y no lo vacía al guardar.

### Errores

`sri_error_code` contiene los códigos de la ficha 2.34 §11, incluidos `2` (RUC del emisor no activo),
`10` (establecimiento clausurado) y `26` (tamaño máximo superado), con descripción y motivo literales. Es un
catálogo de referencia sin consumidores operativos.

### Países

`sri_country` es el catálogo ISO-3166 (`code` alpha-3, `iso2` alpha-2) con el código SRI numérico `sri_code`
(ficha 2.34 Tabla 25, igual a Catálogo ATS Tabla 16; único) para sus 29 países, p. ej. ECU 593, USA 110,
CHN 331 (China Popular). `geo_provinces.country_id` es `char(2)` como `iso2`. La fila ECU viene solo del
`HasData` (nombre `ECUADOR`); el script de geografía no la escribe.

### Sustentos tributarios

`sri_tax_support` sigue el Catálogo ATS 06/08/2026, Tabla 5 (SHA-256 `bd3f7834…6e3e`): `01`–`15` seleccionables
con su descripción literal; `00` y `16`–`20` no seleccionables.

### Retenciones e ISD

IVA: conceptos con versión y `xml_code` de la ficha 2.34 Tabla 20 (10%→9, 20%→10, 30%→1, 50%→11, 70%→2, 100%→3,
cero→7, no procede→8); `728` (15%) conservado sin representación oficial. Renta: Catálogo ATS 06/08/2026,
Tabla 3.10. ISD `4580`: 5% vigente desde 2024-04-01 (Tabla 20), deliberadamente sin versión para que cualquier
resolución falle cerrada, y no seleccionable porque el comprobante del ERP no emite el impuesto `6` (Tabla 19;
`RetentionTaxType` solo IVA/Renta). La historia de porcentajes ISD de la Tabla 20 omite enero de 2023 y no se carga.

### Geografía INEC

`geo_*` contiene la DPA de Ecuador según el Clasificador Geográfico Estadístico 2026 del INEC
(`CLASIFICADOR_GEOGRAFICO_2026.zip`, SHA-256 `9a2962bc…a2c`; hojas PROVINCIAS, CANTONES y CODIGOS):
25 provincias (incluida `90` Zona en estudio), 224 cantones y 1048 parroquias (cabecera cantonal `XX50` y rurales).
Carga única: `Seeding/InstallData/001_inec_cge_2026_geography.sql`, aplicado una vez por checksum por
`InstallDataBootstrapService`; todas las filas nacen activas. Los códigos 2012 que el CGE 2026 recodificó
(La Concordia `0808` → `2302`, El Carmen de Pijilí `011551` → `010852`, Sevilla Don Bosco `140157` → cantón `1413`,
zonas no delimitadas `9001`/`9003`/`9004` → `9006`/`9009`) o suprimió (Tiputini `220254`) no se cargan. Un código
retirado por una revisión INEC posterior a la primera instalación publicada queda `is_active = false`, sin borrarse.
La configuración oficial habilita la carga obligatoria (`InstallData:Enabled = true`, también por defecto en
`InstallDataOptions`). Si la carga INEC falla, el bootstrap propaga el error y la API no completa el arranque;
la instalación no se declara exitosa con geografía incompleta.

**Restricción vigente:** `geo_*` solo contiene Ecuador (`country_id = 'EC'`). `PhysicalAddress` no tiene país:
guarda códigos INEC con FK a `geo_*`, así que su país de catálogo es Ecuador por diseño. En frontend ese valor
vive una sola vez en `masterData/constants/physicalAddressGeography.ts` y se consume vía
`branches/facades/geographyLookupFacade` (único cliente HTTP: `branchService`; guard
`geographyClientSingleSource.test.ts`). `BusinessPartner.countryCode` es el país del socio, no el de la dirección.
Admitir direcciones fuera de Ecuador requiere agregar país a `PhysicalAddress`: decisión funcional pendiente.

### Pendientes GLOBAL (dependencias fuera de GLOBAL)

| Pendiente | Dependencia |
|---|---|
| ICE específico con `unit_value` nulo calcula 0 sin aviso en Ventas (`SalesDraftUseCases`) y en líneas manuales de Compras (`PurchaseDraftUseCases`): `UnitValue ?? 0 × QuantityInBaseUom` | Ventas y Compras deben rechazar o pedir el monto explícito; corregirlo en el resolvedor compartido rompería la importación XML de Compras |
| Cálculo ICE mixto, `3680` fundas y tarifa 2026 de armas (`3630`/`3730`) | § ICE |
| Historia del IVA 12% (2017-06-01 a 2024-03-31) | Modelo concepto + versión de ADR-037 en los resolvedores que usan Compras, Ventas y Gastos (ADR-042) |

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

## Catálogos y datos configurables (SSOT dinámico)

> Origen: CLEAN-01 (auditoría de enums/hardcodes/catálogos estáticos, 2026-08-08). Aplica a todo dato fiscal (SRI), operativo, configurable, o dependiente de tenant/empresa.

- Todo dato fiscal, operativo, configurable, dependiente de tenant/empresa, o que pueda cambiar por normativa/proceso, debe vivir en BD/configuración y exponerse mediante servicio/API — nunca como enum o array estático en código.
- El frontend debe consumir esos datos por hooks/facades/services (p. ej. `sriLookupFacade`, `useSriIdTypes`). No debe duplicar arrays estáticos de códigos si existe endpoint backend.
- Los enums (backend o frontend) solo se permiten para: estados internos de máquina; flags técnicos; resultados computados; conceptos invariantes no administrables; lógica interna que no cambia por tenant/empresa/normativa.
- Antes de crear un enum nuevo, justificar explícitamente por qué no es un catálogo persistido.
- Antes de crear un array estático en frontend, verificar si ya existe un endpoint/catálogo backend equivalente (auditoría de reutilización, igual que con componentes UI).
- No usar fallback hardcodeado para catálogos fiscales/SRI ante error de carga, salvo excepción documentada — mostrar estado de error/vacío en su lugar.
- Toda excepción debe llevar comentario explícito en el código: motivo, fuente normativa/técnica, y por qué no es configurable.
- **Patrón obligatorio:** BD/configuración → servicio backend → API → facade/hook frontend → UI.
- **Patrón prohibido:** enum backend + array frontend + literal repetido en múltiples archivos.

**Ejemplos — debe ir a BD/servicio:** métodos de pago SRI, tipos de documento SRI, tipos de identificación SRI, régimen tributario, IVA/ICE/retenciones, categorías configurables, motivos operativos, cualquier tipo administrable por el tenant.

**Ejemplos — puede ser enum:** `Draft`/`Authorized`/`Cancelled`, `Pending`/`Processing`/`Failed`, `AuditSource`, `MediaType`, resultados internos de cómputo, flags técnicos no configurables.

Catálogos SRI ya persistidos: ver el inventario de [GLOBAL IMMUTABLE](#global-immutable-schema-global). Constantes frontend que repliquen códigos de estos catálogos (p. ej. para validación Zod síncrona) son válidas solo como espejo de validación — nunca como fuente para poblar selects/listas (eso siempre es API).

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
| Catálogos globales | Schema `global` — 27 tablas (inventario en GLOBAL IMMUTABLE) |
| Ítems (alcance Company, naturaleza, catálogo) | [items.md](./items.md) |
| Inventario (Kardex, valoración, ajustes, transferencias) | [inventory.md](./inventory.md) |
| Compras (recepción, compra, devolución, NC, saldo a favor) | [purchases.md](./purchases.md) |

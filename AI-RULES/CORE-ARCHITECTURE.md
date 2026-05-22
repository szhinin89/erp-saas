# Arquitectura core — ERP SaaS ZH Technologies

Reglas estructurales del monorepo. Detalle PR bloqueante: [PR-RULES-CATALOG.md](./PR-RULES-CATALOG.md).

---

## Antes de actuar

1. Lee [README.md](./README.md) y [`CONTEXT.md`](../CONTEXT.md) (índice).
2. Identifica si el archivo a crear/modificar **ya existe** → no regenerar; cambiar solo lo necesario.
3. Define un plan breve antes de escribir código.
4. Contexto descriptivo: `docs/ARCHITECTURE.md` (diagramas), `docs/STATUS.md` (estado), `docs/DEVELOPMENT.md` (arranque).

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
| 9 | Contexto tenant entre rutas | [SAAS-RULES.md](./SAAS-RULES.md) |
| 10 | Claves i18n nuevas | [FRONTEND-RULES.md#i18n-kichwa-de-cañar](./FRONTEND-RULES.md) |
| 11 | Módulo/formulario comercializable | [SAAS-RULES.md#asignación-a-planes](./SAAS-RULES.md) |

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

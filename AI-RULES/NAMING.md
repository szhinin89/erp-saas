# Convenciones de nombres

---

## Base de datos

| Elemento | Convención |
|----------|------------|
| Tablas / columnas | `snake_case` |
| Índices | `ix_*`, `ux_*`, `uq_*` |
| Foreign keys | `fk_*` con `_tenant_` (no `_subscriber_`) |
| Retirado | `Subscriber`, `subscriber_id` → usar `Tenant`/`tenant_id` (consolidación FASE 4, ver [`docs/ARCHITECTURE.md`](../docs/ARCHITECTURE.md#jerarquía-multiempresa)) |

Tipos dominio: PascalCase; mapeo EF en `IEntityTypeConfiguration`.

Antes de crear tablas: declarar scope — `docs/ARCHITECTURE.md#scopes`.

---

## Backend (.NET)

| Elemento | Convención |
|----------|------------|
| Módulos | `ERP.*/Modules/{Nombre}/` PascalCase |
| Commands/Queries | `{Verbo}{Entidad}Command` / `{Verbo}{Entidad}Query` |
| Validators | `{Command/Query}Validator` |
| DTOs | Sufijo `Dto` |
| Permisos | `modulo.recurso.accion` (minúsculas, puntos) |

---

## Frontend

| Elemento | Convención |
|----------|------------|
| Módulos | `frontend/src/modules/{dominio}/` kebab o camel según carpeta existente |
| Schemas Zod | `schemas/{modulo}/{entidad}Schema.ts` |
| i18n claves | `{modulo}.{contexto}.{clave}`; menú `app.nav.*`; tabs listado `{modulo}.tabList` |
| sessionStorage SaaS | prefijo `erp.saas.` |
| Rutas páginas wrapper | `frontend/src/pages/` (≤15 líneas) |
| Implementación | `modules/{dominio}/pages/` |

---

## Prefijos CSS por página

| Página | Prefijo |
|--------|---------|
| Proveedores | `prv-*` |
| Clientes | `cls-*` |
| Productos | `prd-*` |
| Dashboard | `dsh-*` |
| Reportes | `rpt-*` |
| Platform Planes (shell) | `sap-*` |
| Platform overview/suscriptores | `sa-*` |
| SaaS overview/billing | `saas-*` |
| Inventario | `inv-*` |
| Compras | `pur-*` |
| Contabilidad | `acc-*` |

Jerarquía CSS: [FRONTEND-RULES.md](./FRONTEND-RULES.md#css--jerarquía-de-3-niveles).

---

## i18n locales

| Código | Archivo | Notas |
|--------|---------|-------|
| `es` | `es.json` | Español |
| `en` | `en.json` | Inglés |
| `qu` | `qu.json` | Kichwa de Cañar, Ecuador |

Toda clave nueva en **los tres** archivos.

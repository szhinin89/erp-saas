# Features — ERP SaaS

> Estado de delivery por módulo: [`docs/STATUS.md`](docs/STATUS.md) · Arquitectura y rutas API: [`docs/ARCHITECTURE.md`](docs/ARCHITECTURE.md)

## Módulos producto (scope: empresa — `company_id`)

| Dominio | Rutas / API | Notas |
|---------|-------------|-------|
| Auth & acceso | `/login`, `/api/auth/*`, `/api/me` | JWT + refresh rotation, RBAC |
| Master data — Business Partners (Customer/Supplier) | `/api/master/business-partners*` | BP V2, roles Customer/Supplier — FROZEN (ver `docs/STATUS.md`) |
| Catálogo / Ítems | `/api/v1/catalog/*`, `/api/v1/items/*` | 14 entidades, 56 endpoints, CRUD completo, variantes, atributos, SRI lookups. FROZEN v2.0 (rediseño flujo de creación, 2026-07-02) + auditoría por fases 1-9 (2026-07-02) |
| Motor de Pricing v2 | — (consumido internamente vía `IPricingResolver`) | `Item.BaseSalePrice` SSOT + `PricingRule` (reemplaza `ItemPrice`) + `PriceList` con regla general opcional. CLOSED (ADR-021, 2026-07-05) — ver `docs/STATUS.md` |
| Tipos de Ítem | `/api/v1/item-types`, `/inventory/item-types` | Catálogo tenant-editable (reemplaza enum fijo); `items.item_type_id` FK por Guid; sin flags de comportamiento. FROZEN |
| Inventario | `/api/inventory/*` (categorías, líneas, tipos, marcas, tarifas) | Company isolation FROZEN |
| Compras | `Modules/Purchases` | |
| Ventas | `/api/v1/sales/*`, `/sales` | SalesInvoice + Detail; Draft→Authorize→Cancel; snapshot fiscal IVA/ICE; DocumentSequence SRI; facturación electrónica. FROZEN |
| Configuración / SRI | `/api/settings/*`, catálogos SRI | Sucursales, geografía, parámetros |
| Sucursales | `/api/v1/settings/branches`, `/settings/branches`, `/settings/branches/:id` | CRUD + soft-disable; organizativas, no fiscales. FROZEN |
| Establecimientos SRI | `/api/v1/settings/establishments`, `/settings/establishments` | Código fiscal SRI (001-999) único por empresa; BranchId opcional; disable bloqueado si tiene PEs activos. FROZEN |
| Puntos de Emisión | `/api/v1/settings/emission-points`, `/settings/emission-points` | Código 001-999 único por Establecimiento; DocumentSequence automático. FROZEN |
| Acceso / Seguridad | `/api/admin/iam`, `/api/security`, `/api/admin/activity`, `/api/v1/admin/access/sessions*`, `/admin/access/sessions`, `/api/v1/admin/iam/company-users/{id}/preferences`, `/api/v1/admin/iam/memberships`, `/api/v1/admin/iam/memberships/revoke`, `/api/v1/admin/iam/memberships/{id}/branches`, `/admin/users` | Perfiles, permisos, auditoría; UserSession (contexto operativo empresa/sucursal/terminal) — ver `docs/IDENTITY.md#usersession-contexto-operativo-del-usuario`; CompanyUserPreferences (sucursal por defecto + modo de login por usuario, UI en `/admin/security`) — ver `docs/IDENTITY.md#companyuserpreferences-preferencias-operativas-de-login`; **`/admin/users` — administración completa de `CompanyUserMembership`** (alta/edición de rol y perfil, sucursales autorizadas, preferencias de login, revocación/reactivación; `UsersPage`, Fase I-C, ver `docs/STATUS.md`); **Security Hardening Fase S1** (ver `docs/STATUS.md`) — `POST /api/v1/auth/register` y `POST /api/v1/auth/password-reset` eliminados (registro anónimo cross-tenant y reset de password sin verificación); alta de usuarios exclusivamente vía `/api/v1/setup/admin` (first-run) + administración de memberships; reset de password exclusivamente vía `forgot-password`/`reset-password` (token por email); endpoints admin de memberships/branches/preferences ahora exigen `IRequiresCompanyContext` (mismo patrón ya usado por alta/revocación de membership) |

## Plataforma SaaS (scope: tenant — `tenant_id`)

| Feature | Ruta | Notas |
|---------|------|-------|
| Empresas | `/companies`, `/api/companies` | Sin UUID en URL (sessionStorage `erp.saas.*`) |
| Tenant / onboarding | `Modules/Tenants`, `/api/setup` | Contrato SaaS — ver [`docs/ARCHITECTURE.md`](docs/ARCHITECTURE.md#jerarquía-multiempresa) |

> La capa de planes/billing/entitlements descrita en versiones anteriores de este documento fue **eliminada en FASE 1 — ERP Kernel Cleanup** (2026-06-05). No existen rutas `/superadmin/*` ni `/api/saas/*` activas — ver [`docs/STATUS.md`](docs/STATUS.md) y [`docs/archive/SAAS-COMMERCIAL.md`](docs/archive/SAAS-COMMERCIAL.md) (histórico).

## i18n

Español, English, **Kichwa de Cañar (`qu`)**.

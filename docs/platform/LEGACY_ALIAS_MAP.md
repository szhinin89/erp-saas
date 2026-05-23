# Platform Control Plane — Legacy Alias Map

**Propósito:** mapa de aliases históricos SuperAdmin/SaaS → Platform canónico.

## API routes

| Legacy (eliminado) | Canónico | Status |
|--------------------|----------|--------|
| `/api/superadmin/*` | `/api/platform/*` | ✅ Eliminado Phase 4 |
| `POST /api/auth/superadmin-login` | `POST /api/platform/auth/login` | ✅ Eliminado |
| `/api/admin/iam/superadmin/subscribers*` | `/api/platform/subscribers` | ✅ Bloqueado |

## Backend types / files

| Legacy alias | Canónico | Status |
|--------------|----------|--------|
| `SuperAdminController` | `Platform*Controller` | ✅ Eliminado |
| `SuperAdminService` | Application + Platform controllers | ✅ Eliminado |
| `SaasPlan` (file) | `CommercialPlan` (class) | ⚠️ Archivo pendiente rename |
| `TenantSaasSubscription` (file) | `SubscriberSubscription` (class) | ⚠️ Archivo pendiente rename |
| `SaasFeatureDefinition` (file) | `PlatformFeature` (class) | ⚠️ Archivo pendiente rename |
| `TenantCustomMenu` (file) | `SubscriberCustomMenu` (class) | ⚠️ Archivo pendiente rename |

## Frontend

| Legacy | Canónico | Status |
|--------|----------|--------|
| `modules/superadmin/` | `modules/platform/` | ✅ Renombrado Phase 5b |
| `components/superadmin/` | `components/platform/` | ✅ Renombrado |
| `pages/SuperAdmin/` | `pages/Platform/` | ✅ Renombrado |
| `superAdminService.ts` | `platformService.ts` | ✅ Eliminado |
| `useSuperAdminGate` | `usePlatformGate` | ✅ Renombrado |
| `SuperAdminLayout` | `PlatformLayout` | ✅ Renombrado |
| `SUPERADMIN_UI` | `PLATFORM_UI` | ✅ Renombrado |
| `LEGACY_PLATFORM_API` | `PLATFORM_API` | ✅ Eliminado |

## UI / producto (alias activos intencionales)

| Alias activo | Canónico conceptual | Migración |
|--------------|---------------------|-----------|
| URL `/superadmin/*` | Platform Control Plane shell | Opcional → `/platform/*` |
| i18n `superadmin.*` | Platform labels | Pendiente |
| JWT role `SuperAdmin` | Platform operator | Backend identity (no rename) |
| Flag `superAdminPanelEnabled` | Platform panel feature | Deployment config |
| CSS prefix `sa-*` | Platform shell styles | Cosmético |
| `isSuperAdmin` (hook) | Platform operator check | Derivado del JWT |

## DB (sin rename destructivo)

| Concepto legacy | Tabla actual | Notas |
|-----------------|--------------|-------|
| Tenant | `subscribers` | Terminología unificada a Subscriber |
| SaaS plan | `commercial_plans` | No `saas_plans` |
| SuperAdmin audit | `platform_audit_logs` | Renombrado Phase 4 |

## CI enforcement

Patrones bloqueados en build: ver [`CI_GUARD_RULES.md`](./CI_GUARD_RULES.md) y `tools/ci/platform-guard-config.json`.

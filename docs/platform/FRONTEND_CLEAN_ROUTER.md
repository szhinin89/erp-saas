# Frontend — Router platform limpio

**Fuente única:** [`frontend/src/routes/platformRoutes.tsx`](../../frontend/src/routes/platformRoutes.tsx)

## Árbol de rutas

```
ProtectedRoute
├── platformShellRoutes()          → /superadmin/*
├── platformBookmarkRedirectRoutes() → /companies/*  (bookmark only)
└── AppLayout
    ├── mainRoutes                 → ERP runtime
    ├── catalogRoutes
    ├── companyManagementRoutes    → /saas/*
    └── accessRoutes
```

## Platform shell (`/superadmin/*`)

| Ruta | Componente | Notas |
|------|------------|-------|
| `/superadmin/overview` | `PlatformOverviewPage` | Dashboard |
| `/superadmin/subscribers` | `PlatformSubscribersPage` | Listado suscriptores |
| `/superadmin/subscribers/:id` | `PlatformSubscriberDetailPage` | Ficha 9 tabs |
| `/superadmin/plans` | `PlatformPlansPage` | Planes + `?tab=menu` |
| `/superadmin/users` | `PlatformUsersPage` | Operadores platform |
| `/superadmin/billing` | `PlatformBillingPage` | Billing agregado |
| `/superadmin/observability` | `PlatformObservabilityPage` | Métricas |
| `/superadmin/audit` | `PlatformAuditPage` | Audit log |

## Redirects internos (bookmarks viejos del shell)

| Legacy path | Target |
|-------------|--------|
| `/superadmin/companies` | `/superadmin/subscribers` |
| `/superadmin/navigation-menu` | `/superadmin/plans?tab=menu` |
| `/superadmin/menu-plans` | `/superadmin/plans?tab=menu` |
| `/superadmin/menu-builder` | `/superadmin/plans?tab=menu` |
| `/superadmin/features` | `/superadmin/plans?tab=plans` |
| `/superadmin/forms`, `/superadmin/growth` | `/superadmin/overview` |

## Bookmark externo

| Path | Handler | Comportamiento |
|------|---------|----------------|
| `/companies/*` | `PlatformBookmarkRedirect` | Lee `sessionStorage` legacy → ficha o listado platform |

**Eliminado:** `CompaniesLegacyRedirect.tsx`, `superAdminShellRoutes.tsx`, `adminRoutes.tsx`.

## Navegación programática

- Constantes: `PLATFORM_UI` en [`platformApiPaths.ts`](../../frontend/src/modules/platform/api/platformApiPaths.ts)
- Helper: `goToSubscriberDetail()` en [`platformSubscriberDetailNav.ts`](../../frontend/src/navigation/platformSubscriberDetailNav.ts)

## API client

- **Único client control plane:** [`platformService.ts`](../../frontend/src/modules/platform/api/platformService.ts)
- Prefijos: `PLATFORM_API.*` → `/api/platform/*`

## Estructura física (Phase 5b)

| Antes | Después |
|-------|---------|
| `modules/superadmin/` | `modules/platform/` |
| `components/superadmin/` | `components/platform/` |
| `pages/SuperAdmin/` | `pages/Platform/` |
| `SuperAdminLayout`, `SuperAdminCrudTemplate`, `useSuperAdminGate` | `PlatformLayout`, `PlatformCrudTemplate`, `usePlatformGate` |

## Guards

- `ProtectedRoute`: operador global solo en `/superadmin/*` y redirect `/companies/*`
- Impersonación: `platformService.switchSubscriber()` → `/saas/*` runtime

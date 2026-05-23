> **Documento histórico (Phase 2–5).** No usar como referencia de implementación. Rutas y naming actuales: [TEAM-NAMING-GUIDE.md](./TEAM-NAMING-GUIDE.md) · [CANONICAL-ROUTES.md](./CANONICAL-ROUTES.md).
# Frontend — Router platform limpio

**Fuente única:** [`frontend/src/routes/platformRoutes.tsx`](../../frontend/src/routes/platformRoutes.tsx)

## Árbol de rutas

```
ProtectedRoute
├── platformShellRoutes()          → /platform/*
├── platformBookmarkRedirectRoutes() → /companies/*  (bookmark only)
└── AppLayout
    ├── mainRoutes                 → ERP runtime
    ├── catalogRoutes
    ├── companyManagementRoutes    → /saas/*
    └── accessRoutes
```

## Platform shell (`/platform/*`)

| Ruta | Componente | Notas |
|------|------------|-------|
| `/platform/overview` | `PlatformOverviewPage` | Dashboard |
| `/platform/subscribers` | `PlatformSubscribersPage` | Listado suscriptores |
| `/platform/subscribers/:id` | `PlatformSubscriberDetailPage` | Ficha 9 tabs |
| `/platform/plans` | `PlatformPlansPage` | Planes + `?tab=menu` |
| `/platform/users` | `PlatformUsersPage` | Operadores platform |
| `/platform/billing` | `PlatformBillingPage` | Billing agregado |
| `/platform/observability` | `PlatformObservabilityPage` | Métricas |
| `/platform/audit` | `PlatformAuditPage` | Audit log |

## Redirects internos (bookmarks viejos del shell)

| Legacy path | Target |
|-------------|--------|
| `/superadmin/companies` | `/platform/subscribers` |
| `/superadmin/navigation-menu` | `/superadmin/plans?tab=menu` |
| `/superadmin/menu-plans` | `/superadmin/plans?tab=menu` |
| `/superadmin/menu-builder` | `/superadmin/plans?tab=menu` |
| `/superadmin/features` | `/superadmin/plans?tab=plans` |
| `/superadmin/forms`, `/superadmin/growth` | `/platform/overview` |

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
| `modules/platform/` | `modules/platform/` |
| `components/superadmin/` | `components/platform/` |
| `pages/Platform/` | `pages/Platform/` |
| `PlatformLayout`, `PlatformCrudTemplate`, `usePlatformGateGate` | `PlatformLayout`, `PlatformCrudTemplate`, `usePlatformGate` |

## Guards

- `ProtectedRoute`: operador global solo en `/platform/*` y redirect `/companies/*`
- Impersonación: `platformService.switchSubscriber()` → `/saas/*` runtime

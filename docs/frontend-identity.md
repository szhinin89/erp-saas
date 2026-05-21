# Frontend Identity

## `authService`

```typescript
authService.loginUser(credentials)     // POST /api/auth/login
authService.loginPlatform(credentials) // POST /api/platform/auth/login
authService.refresh(refreshToken?)     // POST /api/auth/refresh
```

## Login UX (`LoginPage`)

1. Intenta `loginUser`.
2. Si falla y `superAdminPanelEnabled`, intenta `loginPlatform`.
3. Si falla, flujo bootstrap IAM (`/api/admin/iam/bootstrap-login`).

## Guards

- **`useSuperAdminGate`**: `userType=Platform` + `platformRole=SuperAdmin`, o legacy `role=SuperAdmin` + subscriber global vacío.
- **ERP runtime**: requiere `companyId` en sesión (o selector `/select-company`).

## API pública (sin refresh loop)

- `/api/platform/auth/login` incluido en `PUBLIC_AUTH_PATHS` de `api.ts`.

## SuperAdmin panel

Consume rutas canónicas `/api/platform/subscribers/*` vía `superAdminService`.

Ver [legacy-tenant-cleanup.md](./legacy-tenant-cleanup.md) para wrappers `switchSubscriber`.

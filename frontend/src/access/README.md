# Frontend access (UI-only)

The frontend **never decides security**. It renders UI based on snapshots from the backend.

## Data flow

```
GET /api/admin/iam/me/permissions  ──┐
GET /api/subscribers/entitlements/me ┼── syncSessionEntitlements() ──► permissionsStore
GET /api/me/menu (nav metadata)     ──┘
                                              │
                                              ▼
                                    usePermissionsUi().canShow(key)
                                              │
                                              ▼
                                    Conditional rendering / NoAccessPage
```

## Rules

- Use **`usePermissionsUi().canShow(permissionKey)`** for page/button visibility.
- Do **not** duplicate `isAdmin || hasPerm(...)` in pages — backend sends `*` for Admin/SuperAdmin.
- **`PermissionGuard`** / nav filters use the same store snapshot.
- Module visibility uses **`enabledModules`** from entitlements API (display only).
- Real enforcement is always on the **.NET API** (`IRuntimePermissionAuthorizer`).

## Admin read vs runtime

Profile permission matrix (`ProfilesPage`) is admin CRUD — not used for runtime route guards.

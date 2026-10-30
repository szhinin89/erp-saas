# Frontend access (UI-only)

The frontend **never decides security**. It renders UI based on snapshots from the backend.

## Data flow

```
GET /api/v1/session/context ──► sessionStore.authorization.permissions
GET /api/v1/me/menu         ──► useAppLayoutNavigation (server-filtered menu)
                                              │
                                              ▼
                                    usePermissionsUi().canShow(key)
                                              │
                                              ▼
                                    Conditional rendering / NoAccessPage
```

## Rules

- Use **`usePermissionsUi().canShow(permissionKey)`** for page/button visibility.
- Do **not** duplicate `isAdmin || hasPerm(...)` in pages — backend sends `*` for Admin.
- Navigation menu is **server-driven** — `NavigationBuilder` pre-filters by permissions.
- To offer a link to another screen, use **`useNavigationAccess().findDestination(route)`** (`components/navigationAccess.ts`, provided by `RouteAccessGuard` from the same loaded menu — no extra request). A destination means the user can **view** that screen; edit permissions are not in the menu contract.
- Real enforcement is always on the **.NET API** (`[Authorize(Policy = "perm:xxx")]`).

## Admin read vs runtime

Profile permission matrix (`ProfilesPage`) is admin CRUD — not used for runtime route guards.

# Módulos frontend — mapa canónico

Patrón de referencia: `products/`, `customers/`.

```
modules/{domain}/
├── api/          # HTTP + DTOs del dominio
├── hooks/        # useAsync / estado de pantalla
├── schemas/      # Zod + tipos de formulario
├── components/   # UI específica del dominio
├── pages/        # pantallas (PageShell + tabs)
└── types/        # opcional
```

## Dominios consolidados (audit 2026-05-21)

| Dominio | API | Pages | Notas |
|---------|-----|-------|-------|
| `catalog` | `api/catalogService.ts` | `pages/*` (marcas, unidades, estructura) | |
| `inventario/warehouses` | `api/warehouseService.ts` | `pages/BodegasPage.tsx` | alias `bodegaService` |
| `auth` | `api/authService.ts`, `accessService.ts` | login, select-subscriber/company | |
| `branches` | `api/branchService.ts` | `pages/BranchesPage.tsx` | wrapper en `pages/BranchesPage.tsx` |
| `accounting` | `api/accountingService.ts`, `accountingConfigService.ts` | `pages/AccountingPage.tsx` | |
| `dashboard` | — | `pages/DashboardPage.tsx` | |
| `superadmin` | `api/superAdminService.ts` | parcial (`pages/SuperAdmin/*` wrappers) | |
| `products` | ✅ | ✅ | referencia |
| `customers` | ✅ | ✅ | referencia |

## Utilidades compartidas

- `modules/lib/api.ts` — cliente Axios + refresh
- `modules/lib/apiEnvelope.ts` — envelope `responseObject`
- `lib/session/` — logout y claves `sessionStorage`

## Adapters legacy

`schemas/*` raíz pueden re-exportar con `@deprecated` hacia módulos. **`frontend/src/services/` eliminado** — toda API vive en `modules/{domain}/api/`.

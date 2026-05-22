# Access — autorización runtime vs admin

## Runtime auth (única ruta de decisión)

```
[Authorize(Policy = "perm:x")]
  → PermissionHandler (API, thin adapter)
  → IRuntimePermissionAuthorizer
  → ICompanyContextProvider (contexto operativo)
  → IEffectivePermissionKeysProvider (cache-aside + BD)
```

- **PermissionHandler** no decide permisos ni accede a repositorios.
- **IEffectivePermissionKeysProvider** es la única fuente de claves efectivas (post-filtro plan).
- **IPermissionsCacheService** es optimización; la BD sigue siendo fuente de verdad.

## Admin read models

Handlers marcados con `[AdminReadModel]` pueden leer BD directamente para CRUD/UI admin.
**No** deben usarse en el flujo de autorización runtime.

| Handler | Propósito |
|---------|-----------|
| `GetProfilePermissionsHandler` | Matriz de permisos del perfil (sin filtro de plan) |

## Contexto multi-tenant

**ICompanyContextProvider** es la única fuente para:

- empresa por defecto del suscriptor (`ResolveDefaultCompanyIdAsync`)
- contexto operativo usuario+empresa+membresía (`ResolveOperationalFor*`)

## Invalidación de cache

Mutaciones que afectan permisos efectivos deben usar **IPermissionsCacheInvalidator** (write-side).
No inyectar `IPermissionsCacheService` en handlers de mutación.

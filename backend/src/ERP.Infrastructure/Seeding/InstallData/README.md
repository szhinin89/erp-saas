# InstallData

Datos de instalación cargados al iniciar la API. Política general: **[docs/DATABASE.md](../../../../docs/DATABASE.md)**.

## Archivos (solo estos dos)

| Archivo | Contenido |
|---------|-----------|
| `001_initdata_immutable_bootstrap.sql` | Geografía INEC + país EC (regenerar con `scripts/db/import_inec_ecuador_geography.ps1`) |
| `002_system_bootstrap.sql` | Función `erp_seed_subscriber_default_profiles`, menú global `ui_nav_*` |

No añadir parches incrementales (`003_`, `004_`, …). Los cambios de menú/planes van en:

- `002_system_bootstrap.sql` (fallback global), o
- `CommercialPlansBootstrap.cs` (menú por plan comercial), o
- un **nuevo** `00N_*.sql` numerado si el cambio es solo datos SQL y no código C#.

## Reglas

- Los scripts aplicados son **inmutables** (si cambian checksum, el arranque falla en BDs que ya los ejecutaron).
- Para BDs existentes con scripts viejos (`003`–`009` eliminados): hacer **reset greenfield** (`scripts/db/dev-greenfield-reset.ps1`).
- Scripts destructivos (`DELETE`, `TRUNCATE`, `DROP` o `-- @destructive`) bloqueados salvo confirmación explícita:
  - `InstallData:AllowDestructive = true`
  - `InstallData:ConfirmDestructive = CONFIRM_INSTALLDATA_DELETE`

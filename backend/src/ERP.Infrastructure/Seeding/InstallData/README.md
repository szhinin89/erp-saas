# InstallData

Datos de instalación cargados al iniciar la API. Política general: **[docs/DATABASE.md](../../../../docs/DATABASE.md)**.

## Archivos

| Archivo | Contenido |
|---------|-----------|
| `001_initdata_immutable_bootstrap.sql` | Geografía INEC + país EC (regenerar con `scripts/db/import_inec_ecuador_geography.ps1`) |

`001` es el **único** script procesado por `InstallDataBootstrapService` (checksum
+ `install_data_scripts`). Es exclusivamente datos de referencia inmutables
(geografía INEC).

La navegación global (`ui_nav_groups`/`ui_nav_items`) y el catálogo de permisos
**ya no se siembran por SQL ni por seeders separados**: la única fuente de
verdad es `ERP.Domain.Kernel.KernelRegistry` (módulos/permisos/navegación
derivados por reflexión de `[Module]`/`[NavItem]`), sincronizada automáticamente
por `NavigationSyncService` en cada startup (hoy ejecutado como
`NavigationBootstrapStep` dentro del `GlobalBootstrapOrchestrator` —
ver [`../README.md`](../README.md)). Cambios de menú/permisos del Platform
Kernel se hacen editando `ERP.Domain/Kernel/**`, no SQL.

Este `InstallDataBootstrapService` en sí mismo también corre como step
(`InstallDataBootstrapStep`, orden 20) dentro de ese mismo orquestador.

## Reglas

- El script aplicado (`001`) es **inmutable** (si cambia su checksum, el arranque
  falla en BDs que ya lo ejecutaron).
- Para BDs existentes con scripts viejos (`002`–`009` eliminados): hacer **reset
  greenfield** (`scripts/db/dev-greenfield-reset.ps1`).
- Scripts destructivos (`DELETE`, `TRUNCATE`, `DROP` o `-- @destructive`) bloqueados salvo confirmación explícita:
  - `InstallData:AllowDestructive = true`
  - `InstallData:ConfirmDestructive = CONFIRM_INSTALLDATA_DELETE`

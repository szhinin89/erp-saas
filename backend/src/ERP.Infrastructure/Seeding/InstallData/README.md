# InstallData

Datos de instalación cargados al iniciar la API. Política general: **[docs/DATABASE.md](../../../../docs/DATABASE.md)**.

## Archivos

| Archivo | Contenido |
|---------|-----------|
| `001_inec_cge_2026_geography.sql` | Geografía de Ecuador: Clasificador Geográfico Estadístico 2026 del INEC (25 provincias, 224 cantones, 1048 parroquias). Única carga inicial; el país EC viene del `HasData` de `sri_country` |

`InstallDataBootstrapService` procesa los scripts en orden, una vez por checksum
(`install_data_scripts`). Son exclusivamente datos de referencia de geografía INEC.
Definición vigente de la geografía: [architecture.md § Geografía INEC](../../../../docs/architecture/architecture.md#geografía-inec).

La navegación global (`ui_nav_groups`/`ui_nav_items`) y el catálogo de permisos
**ya no se siembran por SQL ni por seeders separados**: la única fuente de
verdad es `ERP.Domain.Kernel.KernelRegistry` (módulos/permisos/navegación
derivados por reflexión de `[Module]`/`[NavItem]`), sincronizada automáticamente
por `NavigationSyncService` en cada startup (hoy ejecutado como
`NavigationBootstrapStep` dentro del `GlobalBootstrapOrchestrator` —
ver [`../README.md`](../README.md)). Cambios de menú/permisos del Platform
Kernel se hacen editando `ERP.Domain/Kernel/**`, no SQL.

Este `InstallDataBootstrapService` en sí mismo también corre como step
(`InstallDataBootstrapStep`, orden 20) dentro de ese mismo orquestador. La configuración oficial
habilita `InstallData:Enabled = true`; un fallo de esta carga obligatoria bloquea el arranque
(ver SSOT de geografía).

## Reglas

- ERP ZH V1 es GREENFIELD: una sola carga inicial por dominio. Mientras no exista una instalación publicada, una
  corrección de geografía se hace en `001`, no en un script de alineación posterior.
- Un script aplicado en una base se identifica por nombre y checksum (`install_data_scripts`); si su checksum
  cambia, se vuelve a ejecutar. `001` usa `INSERT` simple: sobre una base que ya tiene `geo_*` falla en vez de
  mezclar versiones. Una base creada con instalaciones anteriores se recrea (con autorización del propietario).
- Scripts destructivos (`DELETE`, `TRUNCATE`, `DROP` o `-- @destructive`) bloqueados salvo confirmación explícita:
  - `InstallData:AllowDestructive = true`
  - `InstallData:ConfirmDestructive = CONFIRM_INSTALLDATA_DELETE`

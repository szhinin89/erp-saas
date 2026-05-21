# SQL auxiliar (`scripts/sql/`)

No es código de aplicación. Política de datos versionados: [`docs/DATABASE.md`](../../docs/DATABASE.md) e [`InstallData/`](../../backend/src/ERP.Infrastructure/Seeding/InstallData/).

## Archivos en repo

| Archivo | Uso |
|---------|-----|
| `002_unified_documents_schema_and_migration.sql` | Solo si `Documents:UseUnifiedSchema` está activo (ver `DocumentSchemaOptions.cs`) |
| `refactor_rename.sql`, `refactor_rename_v2.sql`, `refactor_rename_v3.sql` | Plantillas históricas de rename nav/permisos; **no** ejecutar en bases con baseline `InitialEnterpriseBaseline` |

## First-run / reset

Usar **`POST /api/dev/reset-first-run`** (Development) o [`Crear-SuperAdmin.ps1`](../../Crear-SuperAdmin.ps1). No mantener SQL manual contra tablas legacy `users`.

## Geografía INEC

Generar SQL con [`../import_inec_ecuador_geography.ps1`](../import_inec_ecuador_geography.ps1); datos inmutables en `InstallData/`.

No añadir `.sql` aquí sin documentar en este README y en `docs/DATABASE.md`.

# SQL auxiliar (`scripts/sql/`)

No es código de aplicación. Política de datos versionados: [`docs/DATABASE.md`](../../docs/DATABASE.md) e [`InstallData/`](../../backend/src/ERP.Infrastructure/Seeding/InstallData/).

## Mapa canónico (una sola versión por caso)

| Caso | Archivo único | Cuándo usar |
|------|---------------|-------------|
| Schema EF + RLS | `backend/.../Migrations/20260521034018_InitialEnterpriseBaseline.cs` | Siempre (arranque / `dotnet ef database update`) |
| Geografía INEC + país EC | `InstallData/001_initdata_immutable_bootstrap.sql` | Automático al arrancar API (InstallData) |
| Perfiles default + menú global EN | `InstallData/002_system_bootstrap.sql` | Automático al arrancar API (InstallData) |
| Esquema documentos unificados (opcional) | `002_unified_documents_schema_and_migration.sql` | Solo si `Documents:UseUnifiedSchema` (`DocumentSchemaOptions.cs`) |
| Rename nav/permisos legacy ES→EN | `legacy_pre_baseline_nav_permissions_rename.sql` | **Solo** BDs anteriores al baseline enterprise; no en instalaciones nuevas |

## First-run / reset

Usar **`POST /api/dev/reset-first-run`** (Development) o [`Crear-SuperAdmin.ps1`](../../setup/Crear-SuperAdmin.ps1). No mantener SQL manual contra tablas legacy `users`.

## Geografía INEC (regenerar 001)

1. `scripts/import_inec_ecuador_geography.ps1 -OutputFile .\geo.sql`
2. Reemplazar contenido de `001_initdata_immutable_bootstrap.sql` (o crear `003_...` si 001 ya está aplicado en prod — ver README InstallData).

No añadir `.sql` aquí sin documentar en este README y en `docs/DATABASE.md`.

# SQL auxiliar (`scripts/db/sql/`)

Política de datos versionados: [`docs/DATABASE.md`](../../docs/DATABASE.md) e [`InstallData/`](../../backend/src/ERP.Infrastructure/Seeding/InstallData/).

## Mapa canónico

| Caso | Ubicación | Cuándo |
|------|-----------|--------|
| Schema EF + RLS | `backend/.../Migrations/*.cs` | `dotnet ef database update` o `dev-greenfield-reset.ps1` |
| Geografía INEC + país EC | `InstallData/001_initdata_immutable_bootstrap.sql` | Automático al arrancar API |
| Perfiles default + menú global | `InstallData/002_system_bootstrap.sql` | Automático al arrancar API |
| Esquema documentos unificados (opcional) | `002_unified_documents_schema_and_migration.sql` | Solo si `Documents:UseUnifiedSchema` |

## Reset greenfield (desarrollo)

```powershell
.\scripts\db\dev-greenfield-reset.ps1
```

## Geografía INEC (regenerar 001)

1. `scripts/db/import_inec_ecuador_geography.ps1 -OutputFile .\geo.sql`
2. Reemplazar contenido de `001_initdata_immutable_bootstrap.sql` (solo si 001 aún no está aplicado en prod).

No añadir `.sql` aquí sin documentar en este README y en `docs/DATABASE.md`.

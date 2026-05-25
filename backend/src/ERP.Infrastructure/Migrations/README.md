# EF Core Migrations

Política oficial: **[docs/DATABASE.md](../../../../docs/DATABASE.md)**

## Desarrollo (greenfield)

Instalación nueva o reset local:

```powershell
.\scripts\db\dev-greenfield-reset.ps1
```

Equivale a `database drop` + `database update` + arranque API (InstallData 001–002).

## Baseline único

| Migración | Propósito |
|-----------|-----------|
| `20260525224928_InitialEnterpriseBaseline` | Schema enterprise completo + RLS |

Helper (no migración separada): `EnterpriseBaselineRowLevelSecurity.cs` (incluido en baseline `Up()`).

Schema futuro: solo migraciones **forward** nuevas con `dotnet ef migrations add`.

## Comandos

```bash
cd backend/src/ERP.Infrastructure
dotnet ef database update --startup-project ../ERP.API/ERP.API.csproj
dotnet ef migrations add <DescriptiveName> --startup-project ../ERP.API/ERP.API.csproj
dotnet ef migrations has-pending-model-changes --startup-project ../ERP.API/ERP.API.csproj
```

**Nunca** editar migraciones ya aplicadas en shared/staging/prod; solo añadir forward.

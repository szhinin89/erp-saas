# EF Core Migrations

Official policy: **[docs/DATABASE.md](../../../../docs/DATABASE.md)**

Single baseline (no prior history):

| Migration |
|-----------|
| `20260521034018_InitialEnterpriseBaseline` |

```bash
dotnet ef database update --startup-project ../ERP.API/ERP.API.csproj
```

RLS helper: `EnterpriseBaselineRowLevelSecurity.cs` (appended in baseline `Up()`).

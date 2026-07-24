# Backend tests — índice

El código de tests vive en **`backend/src/`**, no en esta carpeta. Esta ruta es el índice documental alineado a la estructura enterprise.

## Proyectos

| Proyecto | Ruta | Rol |
|----------|------|-----|
| API integration | `backend/src/ERP.API.Tests/` | HTTP, flujos E2E API |
| Application | `backend/src/ERP.Application.Tests/` | Handlers, validators |
| Domain | `backend/src/ERP.Domain.Tests/` | Entidades, value objects |
| Infrastructure | `backend/src/ERP.Infrastructure.Tests/` | Persistencia, servicios |
| **Architecture** | `backend/src/ERP.Architecture.Tests/` | NetArchTest + guardrails controllers |

## Architecture tests (enforcement)

```powershell
dotnet test backend/src/ERP.Architecture.Tests/ERP.Architecture.Tests.csproj -c Release
```

Validaciones:

- Domain sin EF Core / ASP.NET / Infrastructure
- Application sin Infrastructure / EF Core
- Infrastructure sin API
- Controllers sin referencia a `ErpDbContext`

Ver también: [`ARCHITECTURE_GATES.md`](../../ARCHITECTURE_GATES.md), [`tools/architecture/check-architecture-guardrails.ps1`](../../tools/architecture/check-architecture-guardrails.ps1).

## Suite completa

```powershell
dotnet test backend/src/ERP.slnx -c Release
```

Baseline v1.0: **299 tests** (2026-05-21).

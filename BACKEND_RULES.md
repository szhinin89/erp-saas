# Backend Rules

> Canónico: [`CLAUDE.md`](CLAUDE.md) · [`docs/ARCHITECTURE-RULES.md`](docs/ARCHITECTURE-RULES.md) (sección Backend)

## Obligatorio

1. **Capas:** API → Application → Domain ← Infrastructure (sin atajos).
2. **Entidades:** factory `Create(...)`, soft delete `Disable()`, nunca DELETE físico de negocio.
3. **CQRS:** MediatR + FluentValidation por command/query.
4. **Controllers:** `ApiResultExtensions` únicamente; declarar `ProducesResponseType`.
5. **Multi-tenant:** `SubscriberId` + filtros EF; índices únicos con tenant.
6. **Sin AutoMapper;** sin lógica de negocio en Infrastructure/API.
7. **Tests:** `dotnet test backend/src/ERP.slnx` antes de merge.

## Estructura

```
backend/
├── src/          # ERP.* projects + ERP.slnx
├── scripts/      # Ops SQL (legacy) → preferir infrastructure/postgres
├── tools/        # Vacío — tooling en repo root tools/
└── docs/         # Notas backend específicas
```

## Módulo nuevo

Copiar vertical **Accounting** → Domain / Application / Infrastructure / API.

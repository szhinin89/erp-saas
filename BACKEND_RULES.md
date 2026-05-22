# Backend Rules (adaptador)

> **Canónico:** [`AI-RULES/BACKEND-RULES.md`](AI-RULES/BACKEND-RULES.md) · PR B-xx: [`AI-RULES/PR-RULES-CATALOG.md`](AI-RULES/PR-RULES-CATALOG.md)

## Resumen

1. Capas: API → Application → Domain ← Infrastructure
2. Entidades: factory `Create(...)`, soft delete `Disable()`
3. CQRS: MediatR + FluentValidation
4. Controllers: `ApiResultExtensions`; `ProducesResponseType`
5. Multi-tenant: `TenantId` desde JWT; índices únicos con tenant
6. Sin AutoMapper; sin lógica de negocio en Infrastructure/API

## Estructura

```
backend/
├── src/          # ERP.* projects + ERP.slnx
├── scripts/      # Ops SQL (legacy) → preferir infrastructure/postgres
├── tools/        # Vacío — tooling en repo root tools/
└── docs/         # Notas backend específicas
```

Módulo nuevo: copiar vertical **Accounting**. Detalle: [`AI-RULES/CORE-ARCHITECTURE.md`](AI-RULES/CORE-ARCHITECTURE.md).

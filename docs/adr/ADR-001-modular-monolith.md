# ADR-001: Modular monolith

## Status

Accepted (2026-05)

## Context

ERP SaaS multi-tenant requiere coherencia transaccional, despliegue simple y equipo pequeño. Microservicios añadirían latencia operativa y complejidad de consistencia entre módulos (ventas, inventario, contabilidad).

## Decision

Monolito modular (.NET 10) con vertical slices por dominio:

`ERP.Domain` → `ERP.Application` → `ERP.Infrastructure` / `ERP.API`

Módulos bajo `Modules/{Nombre}/` (referencia: Accounting).

## Consequences

- ✅ Transacciones ACID entre módulos vía EF Core
- ✅ Un despliegue, un pipeline CI
- ⚠️ Disciplina de límites entre módulos (enforcement en CI)
- ⚠️ Escalar horizontalmente el mismo artefacto

## Alternatives Considered

- **Microservicios:** rechazado por costo operativo MVP
- **Shared kernel ERP.Shared:** rechazado (ver ADR-002)

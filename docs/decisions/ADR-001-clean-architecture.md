# ADR-001: Clean Architecture en backend .NET

## Estado
Aceptado

## Contexto
ERP multi-tenant con dominio complejo (SRI, inventario, contabilidad). Se requiere separación estricta de capas y testabilidad.

## Decisión
Monolito modular con capas **Domain → Application → Infrastructure → API**. Domain sin dependencias de framework. Controllers delgados; casos de uso en Application con MediatR.

## Consecuencias
- ✅ Reglas de dependencia verificables (NetArchTest + guardrails)
- ✅ Handlers aislados por módulo vertical
- ⚠️ Mapeo manual (sin AutoMapper) — más código explícito

## Referencias
- [`docs/ARCHITECTURE.md`](../ARCHITECTURE.md)
- [`ARCHITECTURE_RULES.md`](../../ARCHITECTURE_RULES.md)

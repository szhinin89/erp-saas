# ADR-002: No ERP.Shared

## Status

Accepted (2026-05)

## Context

Proyectos `Shared` tienden a acumular lógica sin ownership, rompiendo límites de capas y generando dependencias circulares.

## Decision

No existe `ERP.Shared`. Código compartido:

- Dentro del módulo de dominio correspondiente, o
- En `ERP.Domain/Common`, o
- Contratos explícitos en Application/Domain

Frontend: `modules/{dominio}/` o `modules/lib/` con ownership claro.

## Consequences

- ✅ Límites explícitos por módulo
- ⚠️ Duplicación menor evitable con contratos bien diseñados
- ✅ Checks de import boundaries detectan `shared/` genérico sin criterio

## Alternatives Considered

- **ERP.Shared central:** rechazado por historial de drift en otros repos
- **NuGet interno:** diferido hasta múltiples deployables

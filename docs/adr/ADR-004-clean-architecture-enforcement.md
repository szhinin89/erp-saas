# ADR-004: Clean Architecture enforcement

## Status

Accepted (2026-05)

## Context

Reglas documentales (AI-RULES, Claude, Cursor) no bastan: agentes IA y PRs pueden violar capas sin bloqueo automático.

## Decision

Enforcement ejecutable en CI:

**Frontend (Node ESM):** pages, imports, modules, CSS, cross-layer

**Backend (Node ESM heurístico):** layering csproj, usings prohibidos, controllers delgados, tenant/IgnoreQueryFilters

**Complemento:** PowerShell guardrails + NetArchTest existentes

Precedencia: **CI/scripts > AI-RULES > adaptadores IA**

## Consequences

- ✅ Violaciones reales bloquean merge
- ✅ Score de salud arquitectónica (`architecture-report.json`)
- ⚠️ Mantener configs y grandfather alineados

## Alternatives Considered

- **Solo Roslyn analyzers:** diferido (complejidad, tiempo)
- **Solo documentación:** insuficiente para multi-agente

## Contexto histórico

Decisión original (ex `docs/decisions/ADR-001`, 2026): ERP multi-tenant con dominio complejo (SRI, inventario, contabilidad) requiere separación estricta de capas y testabilidad. Se adoptó monolito modular con capas **Domain → Application → Infrastructure → API**, Domain sin dependencias de framework, controllers delgados y casos de uso en Application vía MediatR, sin AutoMapper (mapeo manual explícito). Esta ADR-004 formaliza el *enforcement* ejecutable de esa decisión original.

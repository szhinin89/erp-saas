# ADR-006: Multi-agent governance (Cursor + Claude)

## Status

Accepted (2026-05)

## Context

Cursor consume `.mdc`, Claude consume `CLAUDE.md`. Sin fuente única, las reglas derivan y los agentes inventan convenciones.

## Decision

1. **`AI-RULES/*`** — canónico (reglas + enforcement)
2. **Adaptadores** — `CLAUDE.md`, `.cursor/rules/*.mdc` (solo enlaces)
3. **`docs/decisions/`** — rationale (este directorio)
4. **CI authority** — scripts en `tools/architecture/` prevalecen sobre prompts

Ver [docs/architecture/enforcement.md § Compatibilidad multi-agente](../../docs/architecture/enforcement.md#compatibilidad-multi-agente).

## Consequences

- ✅ Onboarding consistente para futuros agentes
- ✅ Anti-drift documental explícito
- ⚠️ Cambios de regla requieren editar canónico + config CI

## Alternatives Considered

- **Duplicar reglas en cada agente:** rechazado
- **Solo AGENTS.md único:** insuficiente para Cursor globs y Claude entry

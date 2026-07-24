# Jerarquía de documentación y precedencia

Orden de autoridad cuando varias fuentes aplican al mismo cambio.

---

## Capas (de mayor a menor prioridad normativa)

| # | Capa | Ubicación | Rol |
|---|------|-----------|-----|
| 1 | **Scripts ejecutables + CI** | `tools/architecture/*.mjs`, guardrails PS | Bloquean merge si fallan |
| 2 | **Seguridad / multi-tenant** | [SECURITY.md](./SECURITY.md), [SAAS-RULES.md](./SAAS-RULES.md) | Innegociable |
| 3 | **Catálogo PR bloqueante** | [PR-RULES-CATALOG.md](./PR-RULES-CATALOG.md) | B-xx / F-xx |
| 4 | **Reglas canónicas por área** | [CORE-ARCHITECTURE.md](./CORE-ARCHITECTURE.md), [BACKEND-RULES.md](./BACKEND-RULES.md), [FRONTEND-RULES.md](./FRONTEND-RULES.md), [ENFORCEMENT.md](./ENFORCEMENT.md) | Implementación diaria |
| 5 | **Stack y herramientas** | [STACK.md](./STACK.md) → `docs/DEVELOPMENT.md#stack-oficial` | Solo herramientas aprobadas |
| 6 | **Adaptadores de agente** | `CLAUDE.md`, `.cursor/rules/*.mdc` | Índice + hints Cursor |
| 7 | **Contexto descriptivo** | [CONTEXT.md](../CONTEXT.md), `docs/ARCHITECTURE.md`, `docs/STATUS.md` | Estado, diagramas |
| 8 | **ADRs (rationale)** | [`docs/adr/`](../docs/adr/README.md) | **Por qué** se decidió (no enforcement) |
| 9 | **Docs feature-specific** | `docs/*` | Detalle de dominio |

### ADRs vs AI-RULES

| Fuente | Responde | Ejemplo |
|--------|----------|---------|
| **ADRs** (`docs/adr/ADR-*.md`) | *¿Por qué esta decisión?* | Modular monolith, no ERP.Shared |
| **AI-RULES** | *¿Qué hacer / qué prohíbe CI?* | PR-6 pages wrapper, B-layering |
| **tools/architecture/** | *¿Cumple el repo ahora?* | Score, violations, annotations |

---

## Resolución de conflictos

1. **Resultado de CI/scripts** prevalece sobre sugerencias de agentes IA.
2. **Seguridad / tenant / billing** prevalece sobre conveniencia o velocidad.
3. Entre reglas canónicas: la regla **más específica por área** gana.
4. Entre adaptador y canónico: **`AI-RULES/*` prevalece** si hay contradicción documental.
5. `.cursor/rules/*.mdc` con `globs` aplican sobre reglas generales **solo en su alcance**.

---

## Qué NO es fuente de verdad

- Comentarios sueltos en código
- Preferencias personales en PRs
- Diagramas desactualizados fuera de `AI-RULES/` o ADRs vigentes
- Copias duplicadas de reglas en `CLAUDE.md` o `.mdc`

---

## Flujo recomendado al implementar

Ver tabla en [CORE-ARCHITECTURE.md](./CORE-ARCHITECTURE.md#flujo-jerárquico-implementar-una-feature).

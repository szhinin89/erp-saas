# Jerarquía de documentación y precedencia

Orden de autoridad cuando varias fuentes aplican al mismo cambio.

---

## Capas (de mayor a menor prioridad normativa)

| # | Capa | Ubicación | Rol |
|---|------|-----------|-----|
| 1 | **Seguridad / multi-tenant / billing** | [SECURITY.md](./SECURITY.md), [SAAS-RULES.md](./SAAS-RULES.md), [PR-RULES-CATALOG.md](./PR-RULES-CATALOG.md) | Innegociable |
| 2 | **Catálogo PR bloqueante** | [PR-RULES-CATALOG.md](./PR-RULES-CATALOG.md) | B-xx / F-xx; impide merge |
| 3 | **Reglas canónicas por área** | [CORE-ARCHITECTURE.md](./CORE-ARCHITECTURE.md), [BACKEND-RULES.md](./BACKEND-RULES.md), [FRONTEND-RULES.md](./FRONTEND-RULES.md), [ENFORCEMENT.md](./ENFORCEMENT.md) | Implementación diaria |
| 4 | **Stack y herramientas** | [STACK.md](./STACK.md) → `docs/DEVELOPMENT.md#stack-oficial` | Solo herramientas aprobadas |
| 5 | **Adaptadores de agente** | `CLAUDE.md`, `.cursor/rules/*.mdc` | Índice + hints Cursor (globs, alwaysApply) |
| 6 | **Contexto descriptivo** | [CONTEXT.md](../CONTEXT.md), `docs/ARCHITECTURE.md`, `docs/STATUS.md` | Estado, diagramas, arranque |
| 7 | **Docs feature-specific** | `docs/*`, ADRs, módulos | Detalle de dominio; no contradice capas 1–4 |

---

## Resolución de conflictos

1. **Seguridad / tenant / billing** prevalece sobre conveniencia o velocidad.
2. Entre reglas canónicas: la regla **más específica por área** gana (p. ej. `BACKEND-RULES.md` sobre guía transversal).
3. Entre adaptador y canónico: **`AI-RULES/*` prevalece** si hay contradicción.
4. `.cursor/rules/*.mdc` con `globs` aplican sobre reglas generales **solo en su alcance de archivos**.

---

## Qué NO es fuente de verdad

- Comentarios sueltos en código
- Preferencias personales en PRs
- Diagramas desactualizados fuera de `AI-RULES/` o ADRs vigentes
- Copias duplicadas de reglas en `CLAUDE.md` o `.mdc`

---

## Flujo recomendado al implementar

Ver tabla en [CORE-ARCHITECTURE.md](./CORE-ARCHITECTURE.md#flujo-jerárquico-implementar-una-feature).

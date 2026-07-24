# Reglas de arquitectura enterprise — entrada PR

> **Adaptador.** Contenido normativo completo (B-xx / F-xx): **[`AI-RULES/PR-RULES-CATALOG.md`](../AI-RULES/PR-RULES-CATALOG.md)**

Este archivo existe para compatibilidad con enlaces históricos, CI y revisores PR. **No duplicar reglas aquí.**

---

## Jerarquía

| Prioridad | Fuente |
|-----------|--------|
| 1 | Seguridad / multi-tenant / billing |
| 2 | [`AI-RULES/PR-RULES-CATALOG.md`](../AI-RULES/PR-RULES-CATALOG.md) |
| 3 | Otros [`AI-RULES/`](../AI-RULES/README.md) por área |
| 4 | `.cursor/rules/*.mdc` (hints Cursor) |
| 5 | [`docs/ARCHITECTURE.md`](./ARCHITECTURE.md) (contexto) |

Ver [AI-RULES/HIERARCHY.md](../AI-RULES/HIERARCHY.md).

---

## Enforcement automatizado

| Herramienta | Ruta |
|-------------|------|
| Stack allowlist | `scripts/ci/verify-stack-allowlist.ps1` |
| Architecture guardrails | `tools/architecture/check-architecture-guardrails.ps1` |
| Handler size | `tools/quality/check-handler-size.ps1` |
| Identity guardrails | `tools/architecture/check-identity-guardrails.ps1` |
| NetArchTest | `backend/src/ERP.Architecture.Tests` |

Detalle: [AI-RULES/ENFORCEMENT.md](../AI-RULES/ENFORCEMENT.md).

---

## Relacionados

- Implementación diaria: [AI-RULES/README.md](../AI-RULES/README.md)
- Claude: [CLAUDE.md](../CLAUDE.md)
- Índice: [CONTEXT.md](../CONTEXT.md)
- Stack: [docs/DEVELOPMENT.md#stack-oficial](./DEVELOPMENT.md#stack-oficial)

**Al editar reglas B-xx/F-xx:** modificar solo `AI-RULES/PR-RULES-CATALOG.md`.

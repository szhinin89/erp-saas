# Reglas de arquitectura enterprise — entrada PR

> **Adaptador.** Contenido normativo completo (B-xx / F-xx): **[`docs/architecture/pr-rules-catalog.md`](architecture/pr-rules-catalog.md)**

Este archivo existe para compatibilidad con enlaces históricos, CI y revisores PR. **No duplicar reglas aquí.**

---

## Jerarquía

Jerarquía oficial: [CLAUDE.md](../CLAUDE.md#jerarquía-documental).
Gates técnicos: [enforcement](architecture/enforcement.md#jerarquía-de-documentación-y-precedencia).

---

## Enforcement automatizado

| Herramienta | Ruta |
|-------------|------|
| Stack allowlist | `scripts/ci/verify-stack-allowlist.ps1` |
| Architecture guardrails | `tools/architecture/check-architecture-guardrails.ps1` |
| Handler size | `tools/quality/check-handler-size.ps1` |
| Identity guardrails | `tools/architecture/check-identity-guardrails.ps1` |
| NetArchTest | `backend/src/ERP.Architecture.Tests` |

Detalle: [docs/architecture/enforcement.md](architecture/enforcement.md).

---

## Relacionados

- Implementación diaria: [docs/architecture/README.md](architecture/README.md)
- Claude: [CLAUDE.md](../CLAUDE.md)
- Índice: [CONTEXT.md](../CONTEXT.md)
- Stack: [docs/DEVELOPMENT.md#stack-oficial](./DEVELOPMENT.md#stack-oficial)

**Al editar reglas B-xx/F-xx:** modificar solo `docs/architecture/pr-rules-catalog.md`.

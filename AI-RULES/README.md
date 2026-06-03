# AI-RULES — Fuente canónica para agentes IA

**Única fuente de verdad** para reglas de implementación, arquitectura y enforcement del monorepo ERP SaaS ZH Technologies.

Cursor, Claude y futuros agentes deben leer **estos archivos** antes de inventar convenciones. Los demás documentos del repo son **adaptadores** o contexto descriptivo.

---

## Índice canónico

| Documento | Contenido |
|-----------|-----------|
| [HIERARCHY.md](./HIERARCHY.md) | Precedencia entre capas de documentación |
| [AGENT-COMPATIBILITY.md](./AGENT-COMPATIBILITY.md) | Cómo consume reglas cada agente |
| [CORE-ARCHITECTURE.md](./CORE-ARCHITECTURE.md) | Monorepo, capas, flujo de feature, patrón Accounting |
| [BACKEND-RULES.md](./BACKEND-RULES.md) | .NET, CQRS, API, multi-tenant backend |
| [FRONTEND-RULES.md](./FRONTEND-RULES.md) | React, ZH Form, tabs, i18n, CSS, navegación UI |
| [SAAS-RULES.md](./SAAS-RULES.md) | Planes comerciales, URLs sin IDs sensibles |
| [Platform naming (equipo)](../docs/platform/TEAM-NAMING-GUIDE.md) | Rutas `/platform/*`, operador platform, prohibiciones `SuperAdmin` |
| [SECURITY.md](./SECURITY.md) | Auth, JWT, tokens, aislamiento tenant |
| [STACK.md](./STACK.md) | Herramientas permitidas (detalle en `docs/DEVELOPMENT.md`) |
| [NAMING.md](./NAMING.md) | Convenciones de nombres (BD, código, i18n, CSS) |
| [ENFORCEMENT.md](./ENFORCEMENT.md) | Validación 4 capas, docs sync, CI, anti-drift |
| [PR-RULES-CATALOG.md](./PR-RULES-CATALOG.md) | Catálogo normativo B-xx / F-xx (bloqueante PR) |
| [EVENT-DRIVEN-RULES.md](./EVENT-DRIVEN-RULES.md) | Domain Events, Outbox, naming, handlers, idempotencia |
| [EVENT-VERSIONING.md](./EVENT-VERSIONING.md) | Additive-first, cuándo subir versión, compatibilidad histórica |
| [OUTBOX-RETENTION.md](./OUTBOX-RETENTION.md) | Política de retención, purge, archive, compliance |
| [ANALYTICS-FOUNDATION.md](./ANALYTICS-FOUNDATION.md) | Read models, proyecciones, estrategia BI/analytics |
| [AI-FOUNDATION.md](./AI-FOUNDATION.md) | Arquitectura IA futura — separación de capas, prohibiciones |
| [SUBSCRIBER-SCOPE-SEALED.md](./SUBSCRIBER-SCOPE-SEALED.md) | **SEALED** — Modelo canónico SUBSCRIBER, boundaries, prohibiciones de duplicación |
| [ARCHITECTURE-GOVERNANCE.md](./ARCHITECTURE-GOVERNANCE.md) | **GOVERNANCE** — Canonical Model Map, enforcement rules B-08–B-11, PR checklist |
| [docs/adr/](../docs/adr/README.md) | ADRs — rationale arquitectónico (no duplicar reglas de enforcement) |

---

## Adaptadores (no duplicar reglas aquí)

| Agente / entrada | Archivo |
|------------------|---------|
| Claude Code | [`CLAUDE.md`](../CLAUDE.md) |
| Cursor (transversal) | [`.cursor/rules/erp-unified-rules.mdc`](../.cursor/rules/erp-unified-rules.mdc) |
| Cursor (mapa) | [`.cursor/rules/rules-consolidated-map.mdc`](../.cursor/rules/rules-consolidated-map.mdc) |
| Índice humano | [`CONTEXT.md`](../CONTEXT.md) |
| PR / auditoría (entrada) | [`docs/ARCHITECTURE-RULES.md`](../docs/ARCHITECTURE-RULES.md) → [PR-RULES-CATALOG.md](./PR-RULES-CATALOG.md) |

---

## Política anti-drift

> **NO DUPLICAR REGLAS.** Cada regla existe **una vez** en `AI-RULES/*`. Otros archivos solo enlazan o resumen en ≤5 líneas operativas para el agente.

Al cambiar una regla: editar el archivo canónico correspondiente → actualizar adaptadores si cambia la ruta o el índice → no copiar el cuerpo completo a `CLAUDE.md` ni a `.mdc`.

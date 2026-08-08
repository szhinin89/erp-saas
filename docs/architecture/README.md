# docs/architecture — Fuente canónica de reglas de implementación

**Única fuente de verdad** para reglas de implementación, arquitectura y enforcement del monorepo ERP SaaS ZH Technologies. Sustituye a `AI-RULES/*` (archivado, ver [`docs/decisions/archive-ai-rules/README.md`](../decisions/archive-ai-rules/README.md)).

Cursor, Claude y futuros agentes deben leer **estos archivos** antes de inventar convenciones. `CLAUDE.md`, `backend/CLAUDE.md` y `frontend/CLAUDE.md` son **adaptadores** que enlazan aquí — no duplican el cuerpo de las reglas.

Diagramas/estado descriptivo (no normativo): [`../ARCHITECTURE.md`](../ARCHITECTURE.md). ADRs (por qué se decidió, no enforcement): [`../decisions/`](../decisions/README.md).

---

## Índice canónico

| Documento | Contenido |
|-----------|-----------|
| [enforcement.md](./enforcement.md) | Anti-drift, validación 4 capas, docs sync, CI, guardrails automatizados, **jerarquía y precedencia**, **compatibilidad multi-agente** |
| [architecture.md](./architecture.md) | Monorepo, capas, Branch Ownership Rule, frontera ERP↔Platform, **Canonical Model Map** (scopes GLOBAL/COMPANY, B-08–B-11), flujo de feature, patrón Accounting |
| [backend.md](./backend.md) | .NET, CQRS, API envelope, multi-tenant backend, AI/Analytics prohibiciones, tarifas SRI |
| [frontend.md](./frontend.md) | React, ZH Form, Design System, tabs, i18n, CSS, navegación UI |
| [security.md](./security.md) | Auth, JWT, tokens, aislamiento tenant, **SaaS — IDs sensibles fuera de la URL** |
| [stack.md](./stack.md) | Herramientas permitidas (detalle en `docs/DEVELOPMENT.md`) |
| [naming.md](./naming.md) | Convenciones de nombres (BD, código, i18n, CSS) |
| [pr-rules-catalog.md](./pr-rules-catalog.md) | Catálogo normativo B-xx / F-xx (bloqueante PR) |
| [events.md](./events.md) | Domain Events, Outbox, naming, handlers, idempotencia, **versionado**, **retención** |
| [ai-foundation.md](./ai-foundation.md) | Arquitectura IA futura (separación de capas) + **Analytics/read models** |
| [audit-infrastructure.md](./audit-infrastructure.md) | **FROZEN** — Entity Audit (contratos, dispatcher, extensión) + diseño de Process Audit futuro |
| [visual-messages.md](./visual-messages.md) | **FROZEN** — API `message.*`, store encapsulado (ADR-018) |
| [modal-standard.md](./modal-standard.md) | **FROZEN** — Componente oficial `ZHModal` |
| [error-handling.md](./error-handling.md) | Contrato único de errores Backend↔Frontend, categorías, mapeo HTTP, reglas E-B/E-F (ADR-027) |
| [ARCHITECTURE-BACKLOG.md](./ARCHITECTURE-BACKLOG.md) | Iniciativas de gobernanza/arquitectura (ADR aceptado, migración pendiente) — no es roadmap funcional |
| [ERROR-HANDLING-AUDIT.md](./ERROR-HANDLING-AUDIT.md) | Auditoría de adopción del contrato de errores por módulo |
| [saas-commercial-flow.md](./saas-commercial-flow.md) | Flujo comercial SaaS (histórico/Platform, fuera de alcance ERP Core — ver `ERP_CORE_FREEZE.md`) |
| [`docs/decisions/`](../decisions/README.md) | ADRs — rationale arquitectónico (no duplicar reglas de enforcement aquí) |

---

## Adaptadores (no duplicar reglas aquí)

| Agente / entrada | Archivo |
|------------------|---------|
| Claude Code (global) | [`/CLAUDE.md`](../../CLAUDE.md) |
| Claude Code (backend) | [`/backend/CLAUDE.md`](../../backend/CLAUDE.md) |
| Claude Code (frontend) | [`/frontend/CLAUDE.md`](../../frontend/CLAUDE.md) |
| Cursor (transversal) | `.cursor/rules/erp-unified-rules.mdc` |
| Cursor (mapa) | `.cursor/rules/rules-consolidated-map.mdc` |
| Índice humano | [`CONTEXT.md`](../../CONTEXT.md) |

---

## Política anti-drift

> **NO DUPLICAR REGLAS.** Cada regla existe **una vez** en `docs/architecture/*`. Otros archivos (`CLAUDE.md`, `backend/CLAUDE.md`, `frontend/CLAUDE.md`, `.mdc`) solo enlazan o resumen en ≤5 líneas operativas para el agente.

Al cambiar una regla: editar el archivo canónico correspondiente → actualizar adaptadores si cambia la ruta o el índice → no copiar el cuerpo completo a `CLAUDE.md`, `backend/CLAUDE.md`, `frontend/CLAUDE.md` ni a `.mdc`.

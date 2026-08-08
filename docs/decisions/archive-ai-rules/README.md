# AI-RULES — Archivado (histórico, no vigente)

**Este directorio no es fuente normativa activa.** Contiene el contenido original del antiguo directorio `AI-RULES/` (raíz del repo), archivado el 2026-08-07 como parte de la reorganización SSOT de documentación (Bloque 16B).

**No usar como fuente normativa.** Las reglas vigentes viven en:

- [`/CLAUDE.md`](../../../CLAUDE.md) — índice normativo principal
- [`/backend/CLAUDE.md`](../../../backend/CLAUDE.md) — reglas backend
- [`/frontend/CLAUDE.md`](../../../frontend/CLAUDE.md) — reglas frontend
- [`/docs/architecture/`](../../architecture/README.md) — cuerpo normativo completo (sustituye a `AI-RULES/*`)

## Qué pasó con cada archivo

La mayoría del contenido de `AI-RULES/*.md` fue migrado — no eliminado — a `docs/architecture/*.md` durante el Bloque 16B, con contradicciones y duplicaciones detectadas en la auditoría previa (Bloque 16A) corregidas en el destino. Los archivos que permanecen en este directorio son la versión **original, pre-migración**, conservada por trazabilidad:

| Archivo original (aquí) | Migrado a |
|---|---|
| `README-original-index.md` | [`docs/architecture/README.md`](../../architecture/README.md) |
| `HIERARCHY.md` | [`docs/architecture/enforcement.md`](../../architecture/enforcement.md) (sección "Jerarquía de documentación y precedencia") |
| `AGENT-COMPATIBILITY.md` | [`docs/architecture/enforcement.md`](../../architecture/enforcement.md) (sección "Compatibilidad multi-agente") |
| `ARCHITECTURE-GOVERNANCE.md` | [`docs/architecture/architecture.md`](../../architecture/architecture.md) (sección "Canonical Model Map") |
| `SAAS-RULES.md` | [`docs/architecture/security.md`](../../architecture/security.md) (sección "SaaS — IDs sensibles fuera de la URL") |
| `EVENT-VERSIONING.md` | [`docs/architecture/events.md`](../../architecture/events.md) (sección "Event Versioning") |
| `OUTBOX-RETENTION.md` | [`docs/architecture/events.md`](../../architecture/events.md) (sección "Outbox Retention Strategy") |
| `ANALYTICS-FOUNDATION.md` | [`docs/architecture/ai-foundation.md`](../../architecture/ai-foundation.md) (sección "Analytics Foundation") |

Los 14 archivos restantes de `AI-RULES/` (`BACKEND-RULES.md`, `CORE-ARCHITECTURE.md`, `FRONTEND-RULES.md`, `MODAL-STANDARD.md`, `VISUAL-MESSAGES.md`, `NAMING.md`, `AUDIT-INFRASTRUCTURE.md`, `ERROR-HANDLING.md`, `STACK.md`, `PR-RULES-CATALOG.md`, `EVENT-DRIVEN-RULES.md`, `SECURITY.md`, `ENFORCEMENT.md`, `AI-FOUNDATION.md`) fueron renombrados/movidos directamente (con `git mv`, historial de git preservado) a `docs/architecture/` con el mismo nombre en minúscula — no están duplicados aquí porque el `git mv` ya conserva su historial completo.

## Advertencia

Los enlaces internos de los archivos de este directorio (`./OTRO-ARCHIVO.md`, `../docs/...`) **no fueron corregidos** tras el archivado — apuntan a rutas que ya no existen en esa forma. Esto es intencional: son un snapshot histórico, no un documento navegable activo. Para las reglas vigentes, usar siempre `docs/architecture/`.

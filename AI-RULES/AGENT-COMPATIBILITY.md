# Compatibilidad multi-agente IA

Arquitectura para que **Cursor**, **Claude** y futuros agentes lean la misma verdad sin drift documental.

---

## Fuente canónica

**`AI-RULES/*`** es la única fuente donde viven las reglas completas.

Ningún agente debe inventar convenciones fuera de estos archivos sin confirmación explícita del usuario.

---

## Cómo consume cada agente

| Agente | Punto de entrada | Qué lee en la práctica |
|--------|------------------|------------------------|
| **Cursor** | `.cursor/rules/*.mdc` (`alwaysApply`, `globs`) | Adaptadores livianos → enlaces a `AI-RULES/*` |
| **Claude Code** | `CLAUDE.md` | Onboarding + índice → enlaces a `AI-RULES/*` |
| **Humanos / PR** | `CONTEXT.md`, `docs/ARCHITECTURE-RULES.md` | Índice → `AI-RULES/PR-RULES-CATALOG.md` |
| **Futuros agentes** | `AI-RULES/README.md` | Mismo índice canónico |

---

## Adaptadores (obligatorio mantener)

Los adaptadores **no duplican** reglas extensas. Solo:

- Enlaces al canónico
- Hints operativos mínimos (globs Cursor, checklist de 3–5 ítems)
- Referencias cruzadas entre áreas

| Adaptador | Propósito |
|-----------|-----------|
| `CLAUDE.md` | Entrada Claude; arranque y navegación |
| `erp-unified-rules.mdc` | Regla transversal Cursor (`alwaysApply: true`) |
| `rules-consolidated-map.mdc` | Mapa de precedencia Cursor |
| `backend-*.mdc`, `frontend-*.mdc` | Scope por glob en Cursor |
| `docs/ARCHITECTURE-RULES.md` | Entrada PR/auditoría → catálogo B-xx/F-xx |

---

## Executable Enforcement

Las reglas críticas de frontend **no dependen únicamente** de prompts IA.

**Capa oficial:** `tools/architecture/*.mjs` (Node.js ESM, sin deps pesadas).

| Comando | Uso |
|---------|-----|
| `npm run architecture:check` | CI + pre-merge (desde `frontend/`) |
| `node tools/architecture/run-all.mjs` | Mismo runner desde raíz |
| `npm run architecture:report` | Artefacto JSON para agentes/CI |

Si un agente (Cursor, Claude, futuro) sugiere código que viola un check, **el CI fallará** aunque el agente no haya leído el adaptador.

### Precedencia con conflictos

| Orden | Fuente |
|-------|--------|
| 1 | **Scripts ejecutables + CI** (resultado real) |
| 2 | `AI-RULES/*` (canónico documental) |
| 3 | Adaptadores IA (`.mdc`, `CLAUDE.md`) |

Detalle por check: [ENFORCEMENT.md](./ENFORCEMENT.md#architecture-enforcement-node--frontend--backend) · [`tools/architecture/README.md`](../tools/architecture/README.md)

---

## CI Authority

Si entran en conflicto **prompts de IA**, **documentación** o **código sugerido** con el resultado de CI:

1. **Scripts ejecutables** (`tools/architecture/*.mjs`, guardrails PowerShell, tests) tienen **prioridad absoluta**.
2. **`AI-RULES/*`** prevalece sobre adaptadores (`.mdc`, `CLAUDE.md`) en conflictos documentales.
3. **ADRs** (`docs/adr/`) explican el *por qué*; no anulan un check que falla en CI sin ADR + cambio de config.
4. Los agentes deben **corregir el código** o **proponer cambio en config/ADR**, no ignorar el fallo del pipeline.

Rationale histórico: [`docs/adr/ADR-006-multi-agent-governance.md`](../docs/adr/ADR-006-multi-agent-governance.md).

---

## Resolución de conflictos (documentación)

| Situación | Prevalece |
|-----------|-----------|
| Adaptador vs `AI-RULES/*` | **`AI-RULES/*`** |
| Regla general vs regla con `globs` específicos | Regla **más específica** en el alcance del archivo |
| Seguridad / tenant vs otra regla | **Seguridad / tenant** |
| `PR-RULES-CATALOG.md` vs convención informal | **Catálogo PR** |

---

## Integrar un agente nuevo

1. Leer [README.md](./README.md) y [HIERARCHY.md](./HIERARCHY.md).
2. Crear un adaptador mínimo (≤80 líneas) que enlace a `AI-RULES/*`.
3. **No** copiar cuerpos completos de reglas al adaptador.
4. Registrar el adaptador en [README.md](./README.md) y [CONTEXT.md](../CONTEXT.md).

---

## Anti-drift (obligatorio)

Al modificar reglas:

1. Editar **solo** el archivo canónico en `AI-RULES/`.
2. Si cambia el índice o la ruta, actualizar adaptadores (enlaces, no cuerpo).
3. No mantener dos versiones “casi iguales” en paralelo.

Ver [ENFORCEMENT.md](./ENFORCEMENT.md#no-duplicar-reglas).

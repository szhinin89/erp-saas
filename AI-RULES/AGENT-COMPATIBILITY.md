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

## Resolución de conflictos

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

# ADR-003: Pages wrapper only (frontend)

## Status

Accepted (2026-05)

## Context

Duplicar lógica en `frontend/src/pages/` y `modules/*/pages/` generaba wrappers gordos, hooks en rutas y acoplamiento a API en capa de enrutamiento.

## Decision

`frontend/src/pages/**/*.tsx` son **solo wrappers** (≤15 líneas):

- Re-export o `<ModulePage />` lazy
- Sin hooks React, sin API, sin stores

Implementación real en `modules/{dominio}/pages/`.

Enforcement: `check-pages-wrapper.mjs` (PR-6) + CI.

## Consequences

- ✅ Rutas estables; lógica testeable en módulos
- ✅ Lazy loading predecible
- ⚠️ Un archivo extra por pantalla (aceptable)

## Alternatives Considered

- **Páginas gordas en pages/:** rechazado (deuda PR-7/PR-6)
- **Eliminar pages/ y rutas directas a modules:** rechazado (rompe convención React Router actual)

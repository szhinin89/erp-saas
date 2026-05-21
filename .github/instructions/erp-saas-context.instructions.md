---
name: erp-saas-context
description: "ZH ERP SaaS — contexto mínimo para Copilot/IDE. El detalle vive en CONTEXT.md y docs/."
applyTo: ["backend/**", "frontend/**", "scripts/**"]
---

# ERP SaaS — contexto (resumen)

**No duplicar** guías largas aquí. Documentación canónica en el repo.

## Orden de lectura

1. **[`CONTEXT.md`](../../CONTEXT.md)** — mapa de documentos
2. **[`CLAUDE.md`](../../CLAUDE.md)** — reglas de implementación
3. **[`docs/DEVELOPMENT-RULES.md`](../../docs/DEVELOPMENT-RULES.md)** — arranque, Docker, EF, tests
4. **[`docs/HERRAMIENTAS-ERP-SAAS.md`](../../docs/HERRAMIENTAS-ERP-SAAS.md)** — stack permitido
5. **[`.cursor/rules/erp-unified-rules.mdc`](../../.cursor/rules/erp-unified-rules.mdc)** — validación 4 capas, ZH Form, i18n, navegación SaaS

## Reglas duras

- **Multi-tenant:** JWT + filtros EF; nunca filtrar datos sin scope coherente (`subscriber` / `company`).
- **Navegación:** sin UUID en query string; `sessionStorage` (`erp.saas.*`). Ver `saas-navigation-no-sensitive-url.mdc`.
- **Validación:** frontend Zod → FluentValidation → dominio → EF.
- **Estado del proyecto:** **`docs/STATUS.md`**; prioridades en **`docs/ROADMAP.md`**.

**Última revisión:** 2026-05-21.

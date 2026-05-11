---
name: erp-saas-context
description: "ZH ERP SaaS — contexto mínimo para Copilot/IDE. El detalle vive en CONTEXT.md y docs/."
applyTo: ["backend/**", "frontend/**", "scripts/**"]
---

# ERP SaaS — contexto (resumen)

**No duplicar** aquí guías largas: este archivo solo fija prioridades. La documentación canónica está en el repo.

## Orden de lectura

1. **[`CONTEXT.md`](../../CONTEXT.md)** (raíz del repo) — mapa de documentos y enlaces por tarea.
2. **[`docs/ARCHITECTURE.md`](../../docs/ARCHITECTURE.md)** — capas, multi-tenant, módulos.
3. **[`docs/DESARROLLO.md`](../../docs/DESARROLLO.md)** — arranque local, Docker, EF, tests.
4. **[`.cursor/rules/erp-unified-rules.mdc`](../../.cursor/rules/erp-unified-rules.mdc)** — reglas de implementación (validación 4 capas, ZH Form, i18n `qu`, navegación SaaS).
5. **[`.github/INSTRUCCIONES-COPILOT.md`](../INSTRUCCIONES-COPILOT.md)** — guía extendida en español (stack, flujos, ejemplos).

## Reglas duras

- **Multi-tenant:** JWT + filtros EF; nunca filtrar datos sin `TenantId` coherente.
- **Navegación:** sin UUID sensibles en query string; `sessionStorage` (`erp.saas.*`). Ver `saas-navigation-no-sensitive-url.mdc`.
- **Validación:** DTO → negocio → autorización → query filters.
- **Estado del proyecto:** `docs/ESTADO-PROYECTO.md`; diario: `docs/REGISTRO-PROYECTO.md`.
- **Histórico de riesgos resueltos:** `docs/HISTORIAL-ARQUITECTURA-RIESGOS.md` (solo referencia).

**Última revisión de este stub:** 2026-05-11.

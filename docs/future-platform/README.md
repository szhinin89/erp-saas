# Platform Control Plane — documentación

> ## 🚧 FUTURO / NO IMPLEMENTADO
>
> Este documento describe una posible plataforma externa futura.
>
> **NO forma parte del ERP actual.**
>
> No debe utilizarse como guía para desarrollar código dentro de ERP Core.

---

**Empezar aquí (equipo):** [TEAM-NAMING-GUIDE.md](./TEAM-NAMING-GUIDE.md)

| Documento | Uso |
|-----------|-----|
| [TEAM-NAMING-GUIDE.md](./TEAM-NAMING-GUIDE.md) | Naming, rutas, prohibiciones — **referencia diaria** |
| [CANONICAL-ROUTES.md](./CANONICAL-ROUTES.md) | API `/api/platform/*` y UI `/platform/*` |
| [CLEAN_TARGET_MODEL.md](./CLEAN_TARGET_MODEL.md) | Mapa entidad → tabla → API → frontend |
| [PRODUCTION-READINESS.md](./PRODUCTION-READINESS.md) | Checklist pre-producción |

Guards CI anti-regresión activos: [`docs/ci/CI_GUARD_RULES.md`](../ci/CI_GUARD_RULES.md) (protección del repo actual, no es contenido futuro).

Artefacto CI (generado): [API_USAGE_GRAPH.json](./API_USAGE_GRAPH.json) — no editar a mano; regenerar con `node tools/architecture/extract-api-usage-graph.mjs`.

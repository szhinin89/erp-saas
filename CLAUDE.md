# ERP SaaS ZH Technologies — Guía para Claude Code

Onboarding rápido. **Reglas completas:** [`AI-RULES/`](AI-RULES/README.md) (fuente canónica única).

---

## Source of Truth

| Tema | Canónico |
|------|----------|
| Índice y anti-drift | [AI-RULES/README.md](AI-RULES/README.md) |
| Precedencia | [AI-RULES/HIERARCHY.md](AI-RULES/HIERARCHY.md) |
| Multi-agente | [AI-RULES/AGENT-COMPATIBILITY.md](AI-RULES/AGENT-COMPATIBILITY.md) |
| Arquitectura core | [AI-RULES/CORE-ARCHITECTURE.md](AI-RULES/CORE-ARCHITECTURE.md) |
| Backend | [AI-RULES/BACKEND-RULES.md](AI-RULES/BACKEND-RULES.md) |
| Frontend | [AI-RULES/FRONTEND-RULES.md](AI-RULES/FRONTEND-RULES.md) |
| SaaS | [AI-RULES/SAAS-RULES.md](AI-RULES/SAAS-RULES.md) |
| Platform naming (equipo) | [docs/platform/TEAM-NAMING-GUIDE.md](docs/platform/TEAM-NAMING-GUIDE.md) |
| Seguridad / auth | [AI-RULES/SECURITY.md](AI-RULES/SECURITY.md) |
| Stack permitido | [AI-RULES/STACK.md](AI-RULES/STACK.md) → [docs/DEVELOPMENT.md#stack-oficial](docs/DEVELOPMENT.md#stack-oficial) |
| Naming | [AI-RULES/NAMING.md](AI-RULES/NAMING.md) |
| Enforcement / 4 capas | [AI-RULES/ENFORCEMENT.md](AI-RULES/ENFORCEMENT.md) |
| PR bloqueante (B-xx/F-xx) | [AI-RULES/PR-RULES-CATALOG.md](AI-RULES/PR-RULES-CATALOG.md) |

---

## Contexto del repo (no reglas)

| Necesidad | Documento |
|-----------|-----------|
| Índice maestro | [CONTEXT.md](CONTEXT.md) |
| Estado MVP | [docs/STATUS.md](docs/STATUS.md) |
| Arranque local, Docker, tests | [docs/DEVELOPMENT.md](docs/DEVELOPMENT.md) |
| Arquitectura descriptiva | [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md) |

---

## Antes de actuar

1. Verificar si el archivo **ya existe** → editar, no regenerar.
2. Seguir [flujo jerárquico](AI-RULES/CORE-ARCHITECTURE.md#flujo-jerárquico-implementar-una-feature).
3. **No inventar reglas** fuera de `AI-RULES/*` sin confirmación del usuario.

---

## Al terminar una tarea

Actualizar docs de avance → [AI-RULES/ENFORCEMENT.md#sincronización-docs-de-avance](AI-RULES/ENFORCEMENT.md#sincronización-docs-de-avance).

## Hardening multiempresa (resumen)

- Scope explícito en MediatR: `ICompanyScopedRequest` / `ISubscriberScopedRequest` / `IPlatformScopedRequest`
- Concurrencia PG: `IDatabaseExceptionTranslator` → nunca 500 por UNIQUE
- Métricas: `docs/observability/METRICS.md` · Seguridad: `docs/security/MULTI-TENANT-HARDENING.md`

---

## Convenciones esenciales (resumen — detalle en canónico)

- Capas: `ERP.API → Application → Domain ← Infrastructure`
- Validación 4 capas para datos persistidos
- Soft delete; factories `Create(...)`; sin AutoMapper
- Frontend: módulos `modules/{dominio}/`, ZH Form, i18n es/en/qu
- SaaS: IDs tenant en `sessionStorage` (`erp.saas.*`), no en URL
- Stack: solo herramientas en `docs/DEVELOPMENT.md#stack-oficial`

**NO duplicar reglas aquí.** Editar siempre el archivo canónico en `AI-RULES/`.

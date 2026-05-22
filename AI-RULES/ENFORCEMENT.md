# Enforcement — validación, docs sync, CI

---

## NO DUPLICAR REGLAS

> **Política anti-drift obligatoria.**

| ✅ Correcto | ❌ Incorrecto |
|------------|---------------|
| Regla completa **una vez** en `AI-RULES/*` | Misma regla en `CLAUDE.md` + `.mdc` + `AI-RULES/` |
| Adaptador con enlace + hint ≤5 líneas | Copiar 300 líneas a cada adaptador |
| Editar canónico → actualizar enlaces | Editar solo adaptador y olvidar canónico |

Al añadir regla nueva: elegir **un** archivo canónico → enlazar desde adaptadores.

Ver [AGENT-COMPATIBILITY.md](./AGENT-COMPATIBILITY.md).

---

## Validación en 4 capas (datos persistidos)

Toda validación que afecte datos guardados se refleja en **4 capas**. Prohibido validar solo en frontend.

| Capa | Herramienta | Ubicación |
|------|-------------|-----------|
| 1 Frontend | Zod + react-hook-form | `frontend/src/schemas/{modulo}/`; `zodResolver`; error por campo |
| 2 Application | FluentValidation + MediatR | `[Nombre]Validator`; `ValidationBehavior` |
| 3 Domain | Guard clauses + factories | `Entidad.Create(...)`; `DomainException` |
| 4 BD | EF Core configuration | `IsRequired`, `HasMaxLength`; índices únicos con `TenantId` |

### Convención de errores

| Capa | Formato |
|------|---------|
| Frontend | Mensajes en español junto al campo |
| Application | `ValidationException` → **422** |
| Domain | `DomainException` vía middleware |
| API | `ApiResponse<T>`; `Result<T>` fallido → **400** |

Detalle backend/frontend: [BACKEND-RULES.md](./BACKEND-RULES.md), [FRONTEND-RULES.md](./FRONTEND-RULES.md).

---

## Catálogo PR bloqueante

Reglas B-xx / F-xx con severidad **BLOQUEANTE**: [PR-RULES-CATALOG.md](./PR-RULES-CATALOG.md)

Entrada raíz PR: `docs/ARCHITECTURE-RULES.md` (adaptador).

---

## Sincronización docs de avance

**Al completar funcionalidad**, actualizar documentación de avance:

| Documento | Acción |
|-----------|--------|
| `PROGRESS.html` | Marcar ítem, `#last-updated`, badge |
| `docs/STATUS.md` | Resumen, tablas módulos, pendientes MVP, fecha |
| `docs/ROADMAP.md` | Si cambian prioridades/fases |
| `README.md` | Si cambia alcance, rutas, endpoints, permisos |

Estados módulo: `✅` completo, `🟡` parcial, `⏳` pendiente, `🚧` en progreso.

Fuente operativa consolidada: **`docs/STATUS.md`**.

Cursor hint: `.cursor/rules/docs-progress-status-sync.mdc` (glob `docs/STATUS.md`).

---

## Tests pre-merge

```powershell
cd backend
dotnet test src/ERP.API.Tests/ERP.API.Tests.csproj
dotnet test src/ERP.Application.Tests/ERP.Application.Tests.csproj
cd frontend && npx tsc --noEmit && npm run build
```

---

## Guardrails automatizados

| Herramienta | Ruta |
|-------------|------|
| Stack allowlist | `scripts/ci/verify-stack-allowlist.ps1` |
| Architecture guardrails | `tools/architecture/check-architecture-guardrails.ps1` |
| Identity guardrails | `tools/architecture/check-identity-guardrails.ps1` |
| Handler size | `tools/quality/check-handler-size.ps1` |
| NetArchTest | `backend/src/ERP.Architecture.Tests` |

Grandfather: `tools/architecture/architecture-grandfather.json`

---

## Ramas

| Rama | Uso |
|------|-----|
| `main` | Integración estable |
| `development` | Features diarias |
| `release/*` | Estabilización |
| `hotfix/*` | Correcciones urgentes |

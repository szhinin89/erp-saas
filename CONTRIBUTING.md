# Contributing — ERP SaaS

## Antes de abrir PR

1. Leer [`SYSTEM_TRUTH.md`](SYSTEM_TRUTH.md), [`ARCHITECTURE_GATES.md`](ARCHITECTURE_GATES.md), [`CONTEXT.md`](CONTEXT.md) y [`CLAUDE.md`](CLAUDE.md).
2. Rama desde `development` (features) o `hotfix/*` (urgente).
3. Ejecutar localmente:
   ```powershell
   ./scripts/ci/verify-stack-allowlist.ps1
   dotnet test backend/src/ERP.slnx -c Release
   cd frontend; npm run lint; npm run build
   ./tools/architecture/check-architecture-guardrails.ps1 -SkipFrontendChunk
   ```

## Naming

- Backend: PascalCase, módulos bajo `ERP.*/Modules/{Nombre}/`
- Frontend: módulos en kebab/camel según carpeta existente; prefijos CSS por página
- Commits: `feat(scope):`, `fix(scope):`, `docs:`, `refactor:` — imperativo, español o inglés consistente con repo

## Estructura — prohibido

- Nuevos `.ps1` fuera de `scriptsAllowed` (`scripts/stack-allowlist.json`)
- Herramientas fuera del stack oficial
- Lógica de negocio en Controllers o páginas React
- Validación solo en frontend para datos persistidos
- UUID de tenant en query URL compartible

## Testing

- Backend: xUnit + FluentAssertions; integración en `ERP.API.Tests`
- Frontend: Vitest (unit) + Playwright (e2e en CI)
- Architecture: `backend/src/ERP.Architecture.Tests/` (índice: `backend/tests/README.md`), guardrails PowerShell

## Documentación

Al cerrar feature: `docs/STATUS.md`, `PROGRESS.html`, y propagar si cambian rutas/endpoints.

## Review checklist

- [ ] Capas respetadas
- [ ] Validación 4 capas si hay persistencia
- [ ] i18n es/en/qu
- [ ] Sin regresión guardrails CI
- [ ] Plan SaaS asignado si es módulo/pantalla nueva

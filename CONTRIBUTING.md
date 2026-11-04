# Contributing — ERP SaaS

## Antes de abrir PR

1. Leer [`docs/ARCHITECTURE.md`](docs/ARCHITECTURE.md), [`ARCHITECTURE_GATES.md`](ARCHITECTURE_GATES.md), [`CONTEXT.md`](CONTEXT.md) y [`CLAUDE.md`](CLAUDE.md).
2. Consultar [CI y ramas](docs/architecture/architecture.md#ci-y-ramas): `main` para integración estable, `development` para features diarias, `release/*` para estabilización y `hotfix/*` para urgencias. Esta última es un tipo de rama, no una base de partida definida por esta guía.
3. Aplicar el [flujo obligatorio por fase](docs/architecture/enforcement.md#flujo-obligatorio-del-agente-por-fase), incluido el gate PASS/FAIL del propietario antes de la revisión final. Staging y commit corresponden al propietario; el agente no hace push sin autorización expresa. No declarar CLOSED antes de completar el flujo.
4. Ejecutar los checks aplicables al tipo de cambio según [enforcement](docs/architecture/enforcement.md). Para una tarea exclusivamente documental: verificar referencias y `git diff --check`, sin pruebas funcionales.

## Naming

- Backend: PascalCase, módulos bajo `ERP.*/Modules/{Nombre}/`
- Frontend: módulos en kebab/camel según carpeta existente; prefijos CSS por página
- Commits: `feat(scope):`, `fix(scope):`, `docs:`, `refactor:` — imperativo, español o inglés consistente con repo

## Reglas de implementación

Consultar el [catálogo PR bloqueante](docs/architecture/pr-rules-catalog.md), el
[stack oficial](docs/DEVELOPMENT.md#stack-oficial) y los adaptadores de
[backend](backend/CLAUDE.md) / [frontend](frontend/CLAUDE.md). Esta guía no redefine sus reglas.

## Testing

- Backend: xUnit + FluentAssertions; integración en `ERP.API.Tests`
- Frontend: Vitest (unit) + Playwright (e2e en CI)
- Architecture: `backend/src/ERP.Architecture.Tests/` (índice: `backend/tests/README.md`), guardrails PowerShell

## Documentación

Aplicar la [sincronización de docs de avance](docs/architecture/enforcement.md#sincronización-docs-de-avance): SSOT si cambia el comportamiento, `STATUS.md` si cambia el estado y entradas/runbooks cuando corresponda.

## Review checklist

- [ ] Capas respetadas
- [ ] Validación 4 capas si hay persistencia
- [ ] i18n es/en
- [ ] Sin regresión guardrails CI
- [ ] Frontera ERP ↔ Platform respetada ([reglas canónicas](docs/architecture/architecture.md#reglas-de-arquitectura-que-no-se-rompen))

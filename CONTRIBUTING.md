# Contributing — ERP SaaS

## Antes de abrir PR

1. Leer primero [CLAUDE.md](CLAUDE.md) y sus lecturas comunes; localizar la SSOT desde [CONTEXT](CONTEXT.md) y el [índice técnico](docs/architecture/README.md).
2. Elegir el tipo de rama según [CI y ramas](docs/architecture/architecture.md#ci-y-ramas); esa sección conserva los roles de cada rama.
3. Aplicar el [flujo obligatorio por fase](docs/architecture/enforcement.md#flujo-obligatorio-del-agente-por-fase), incluido el gate PASS/FAIL del propietario antes de la revisión final. Staging y commit corresponden al propietario; el agente no hace push sin autorización expresa. No declarar CLOSED antes de completar el flujo.
4. Ejecutar los checks aplicables al tipo de cambio según [enforcement](docs/architecture/enforcement.md). Para una tarea exclusivamente documental: verificar referencias y `git diff --check`, sin pruebas funcionales.

## Naming

- Nombres de código y CSS: [Naming](docs/architecture/naming.md).
- Commits: `feat(scope):`, `fix(scope):`, `docs:`, `refactor:` — imperativo, español o inglés consistente con repo

## Reglas de implementación

Consultar el [catálogo PR bloqueante](docs/architecture/pr-rules-catalog.md), el
[stack oficial](docs/DEVELOPMENT.md#stack-oficial) y los adaptadores de
[backend](backend/CLAUDE.md) / [frontend](frontend/CLAUDE.md). Esta guía no redefine sus reglas.

## Testing

Herramientas y comandos de pruebas: [DEVELOPMENT](docs/DEVELOPMENT.md#tests).
Checks obligatorios según el cambio: [enforcement](docs/architecture/enforcement.md#tests-pre-merge).
E2E completo: [procedimiento manual](docs/DEVELOPMENT.md#e2e-playwright).

## Documentación

Aplicar la [sincronización de docs de avance](docs/architecture/enforcement.md#sincronización-docs-de-avance): SSOT si cambia el comportamiento, `STATUS.md` si cambia el estado y entradas/runbooks cuando corresponda.

## Review checklist

- [ ] Capas respetadas
- [ ] Validación 4 capas si hay persistencia
- [ ] i18n es/en
- [ ] Sin regresión guardrails CI
- [ ] Frontera ERP ↔ Platform respetada ([reglas canónicas](docs/architecture/architecture.md#reglas-de-arquitectura-que-no-se-rompen))

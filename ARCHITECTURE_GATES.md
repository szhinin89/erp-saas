# Architecture Gates — ERP SaaS

Índice para PR, CI y agentes, bajo la [jerarquía de CLAUDE.md](CLAUDE.md#jerarquía-documental).
No define reglas ni excepciones independientes.

## Reglas prohibidas

| IDs de referencia | Tema | Fuente oficial |
|---|---|---|
| G-P01, G-P02, G-P06, G-P08, G-P12 | Capas, controllers, contratos y mapeos | [Catálogo PR](docs/architecture/pr-rules-catalog.md), [Backend](docs/architecture/backend.md) |
| G-P03 | Comunicación entre módulos | [Arquitectura](docs/architecture/architecture.md) |
| G-P04, G-P05 | Módulos frontend y schemas | [Frontend](docs/architecture/frontend.md), [validación](docs/architecture/form-validation.md) |
| G-P07, G-P11 | Rutas y herramientas admitidas | [Stack](docs/architecture/stack.md), [DEVELOPMENT](docs/DEVELOPMENT.md#stack-oficial) |
| G-P09 | Borrado y excepciones documentadas | [Backend](docs/architecture/backend.md#soft-delete) |
| G-P10 | IDs sensibles en URL | [Seguridad](docs/architecture/security.md#saas--ids-sensibles-fuera-de-la-url) |

## Reglas obligatorias

| IDs de referencia | Tema | Fuente oficial |
|---|---|---|
| G-O01, G-O02, G-O06, G-O09, G-O10 | Capas, CQRS, integración, API y entidades | [Backend](docs/architecture/backend.md), [Arquitectura](docs/architecture/architecture.md) |
| G-O03, G-O07 | Módulos frontend e i18n | [Frontend](docs/architecture/frontend.md) |
| G-O04 | Validación por capas | [Enforcement](docs/architecture/enforcement.md#validación-en-4-capas-datos-persistidos) |
| G-O05 | Aislamiento tenant/company | [Seguridad](docs/architecture/security.md) |
| G-O08 | Frontera ERP ↔ Platform | [Arquitectura](docs/architecture/architecture.md#reglas-de-arquitectura-que-no-se-rompen) |

## Enforcement (CI y local)

[Checks y comandos según el cambio](docs/architecture/enforcement.md#tests-pre-merge),
[guardrails automatizados](docs/architecture/enforcement.md#guardrails-automatizados)
y [distribución CI/E2E](docs/DEVELOPMENT.md#tests).

## Excepciones (legacy tolerado)

[Excepciones e integridad del grandfather](docs/architecture/enforcement.md#guardrails-automatizados).

## Uso en code review

[CONTRIBUTING](CONTRIBUTING.md) y [catálogo PR bloqueante](docs/architecture/pr-rules-catalog.md).

## Uso en agentes AI

[Reglas globales y lecturas comunes](CLAUDE.md#antes-de-actuar),
[flujo por fase](docs/architecture/enforcement.md#flujo-obligatorio-del-agente-por-fase)
y [sincronización documental](docs/architecture/enforcement.md#sincronización-docs-de-avance).

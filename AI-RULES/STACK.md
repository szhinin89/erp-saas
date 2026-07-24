# Stack oficial — herramientas permitidas

**Detalle operativo y versiones:** [`docs/DEVELOPMENT.md#stack-oficial`](../docs/DEVELOPMENT.md#stack-oficial)

**Verificación CI:** `scripts/ci/verify-stack-allowlist.ps1` + `scripts/stack-allowlist.json`

Cursor adapter: `.cursor/rules/stack-tools-source-of-truth.mdc`

---

## Reglas operativas

- **No** proponer, generar ni configurar herramientas fuera de la lista oficial sin autorización explícita del usuario.
- Si una necesidad no está cubierta, pedir confirmación y ofrecer alternativas **dentro** del stack listado.
- Antes de migraciones/scripts/config nueva: verificar herramienta en `docs/DEVELOPMENT.md`.
- Entorno oficial: Docker Compose (PostgreSQL + Redis), CI GitHub Actions, xUnit/Playwright.

---

## Resumen runtime

| Área | Herramienta |
|------|-------------|
| Backend | .NET 10 (`backend/src/global.json`) |
| ORM | EF Core + Npgsql |
| CQRS | MediatR |
| Validación | FluentValidation |
| Auth | JWT Bearer |
| Cache | Redis 7 |
| Jobs | Hangfire + PostgreSQL |
| Logs | Serilog |
| BD | PostgreSQL 16 (Docker, puerto 5435) |
| Contenedores | Docker Compose (raíz repo) |
| Frontend | React 19, TS, Vite, React Router v7, RHF + Zod, Axios, Zustand, i18next (`es`,`en`,`qu`) |
| Tests BE | xUnit, Moq, FluentAssertions, `WebApplicationFactory` |
| Tests FE | Vitest, Playwright, ESLint |

---

## NEVER (stack / schema)

Ver también `docs/DEVELOPMENT.md`:

- Migraciones `.cs` a mano sin `dotnet ef migrations add`
- `Subscriber` en código o schema nuevo (retirado, usar `Tenant`)
- Stripe SDK dentro de handlers MediatR
- Mezclar billing SaaS con facturas ERP

---

## Cambiar el stack

1. Actualizar `docs/DEVELOPMENT.md#stack-oficial`
2. Actualizar `scripts/stack-allowlist.json`
3. Documentar decisión (ADR si aplica)
4. **No** duplicar la lista completa fuera de `docs/DEVELOPMENT.md` y este resumen

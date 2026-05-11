# Herramientas en uso — ERP SaaS

Este documento enumera **solo** las herramientas y tecnologías que el proyecto está usando actualmente en código, CI y operación local.

Última revisión: 2026-05-11

---

## 1) Backend

| Herramienta | Uso en el proyecto |
|---|---|
| .NET SDK 10.0.201+ (`global.json`) | Build, run, test, EF migrations |
| ASP.NET Core Web API | Host HTTP, controllers, middleware |
| MediatR | CQRS (commands/queries/handlers) |
| FluentValidation | Validación de comandos/requests |
| Entity Framework Core 10 + Npgsql | Persistencia y migraciones sobre PostgreSQL |
| JWT Bearer | Autenticación de sesión |
| BCrypt (`BCrypt.Net-Next`) | Hash/verify de contraseñas |
| Serilog (Console/File) | Logging estructurado |
| Swashbuckle | Swagger/OpenAPI |
| Hangfire + Hangfire.PostgreSql | Jobs de background (cuando está habilitado) |
| QuestPDF + RazorLight + ClosedXML | Exportes/reportes (PDF/Excel) |

---

## 2) Frontend

| Herramienta | Uso en el proyecto |
|---|---|
| React 19 + React DOM | UI SPA |
| TypeScript | Tipado estático |
| Vite 8 | Dev server y build |
| React Router | Ruteo de pantallas |
| Zustand | Estado global (auth, permisos, etc.) |
| Axios | Cliente HTTP hacia API |
| React Hook Form + Zod | Formularios y validación |
| Recharts | Gráficos |

---

## 3) Base de datos y cache

| Herramienta | Uso en el proyecto |
|---|---|
| PostgreSQL 16 (Docker) | Base principal (`dberpsaas`) |
| Redis 7 (Docker) | Cache distribuida y soporte operativo |
| Docker Compose | Orquestación local de Postgres + Redis |

---

## 4) Calidad, testing y CI

| Herramienta | Uso en el proyecto |
|---|---|
| xUnit | Tests backend |
| FluentAssertions | Aserciones expresivas en tests |
| Moq | Mocks en pruebas unitarias |
| WebApplicationFactory | Integración HTTP backend |
| Playwright | E2E smoke frontend |
| ESLint + TypeScript compiler | Calidad y type-check frontend |
| GitHub Actions (`.github/workflows/ci.yml`) | CI backend + frontend |
| Dependabot | Actualización automática de dependencias/Actions |

---

## 5) Operación local (dev)

| Herramienta | Uso en el proyecto |
|---|---|
| `dotnet ef` | Crear/aplicar migraciones |
| `dotnet run` | Ejecutar API |
| `npm run dev/build/lint/test:e2e` | Ciclo de trabajo frontend |
| Swagger (`/swagger`) | Verificación y prueba manual de endpoints |

---

## 6) Alcance actual (qué sí / qué no)

- **Sí se usa actualmente:** todo lo listado arriba.
- **No está en uso activo como estándar del repo:** Kubernetes, Terraform, colas externas (Rabbit/Kafka), observabilidad SaaS externa (Datadog/Prometheus), mensajería cloud gestionada.
- **Integración SRI real:** existe estructura y servicios, pero en desarrollo se opera principalmente con flujo simulado para pruebas funcionales.

---

## Fuente de verdad

Para mantener este documento actualizado, validar contra:

- `backend/src/global.json`
- `backend/src/*.csproj`
- `frontend/package.json`
- `docker-compose.yml`
- `.github/workflows/ci.yml`

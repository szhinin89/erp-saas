# Production readiness — Platform SaaS ERP

**Phase 3** — guía operativa (sin rediseño de arquitectura).

---

## Infra recomendada

| Componente | Dev | Producción |
|------------|-----|------------|
| PostgreSQL | Docker Compose | Managed PG + backups diarios + PITR |
| Redis | Opcional (memory fallback) | Cluster AOF/RDB persistence |
| API | Kestrel `:5003` | Reverse proxy (TLS) → API |
| Frontend | Vite preview / CDN | Static assets + `VITE_API_URL` |
| Hangfire | Mismo proceso API | Worker dedicado + retry policy |

Plantilla: `docker-compose.prod.yml` + `infrastructure/docker/compose.base.yml`.

---

## Secrets & HTTPS

- JWT `SecretKey`, DB connection, Redis — **solo** env vars / vault (nunca repo).
- TLS terminado en reverse proxy (nginx/traefik/Azure App Gateway).
- Cookies refresh: `Secure`, `SameSite=Lax`, dominio API explícito.

---

## Observabilidad

- Prometheus scrape `/metrics` (flag `Observability:EnablePrometheus`).
- Health: `/health/live`, `/health/ready`, `/health/security-context`.
- Correlation: header `X-Correlation-Id` (middleware existente).
- Legacy strangler dashboard: `/platform/observability` → PostgreSQL persistente.

---

## Backups

- PostgreSQL: snapshot diario + WAL; probar restore trimestral.
- Redis: AOF si se usa para rate-limit / entitlements cache (recuperable desde DB).

---

## Hangfire

- Jobs: `CheckSubscriptionExpiryJob`, outbox, reconciliation.
- Configurar reintentos exponenciales; alertas en failed jobs (Phase 4).

---

## Seguridad roadmap

| Control | Estado |
|---------|--------|
| Session revoke platform | ✅ API + UI |
| Refresh token rotation | ✅ |
| Brute-force login | Rate limit auth (existente) — reforzar prod thresholds |
| MFA platform operators | Roadmap Phase 4 |
| Device/IP audit | Parcial (`platform_audit_logs`) |

---

## Multi-tenant invariants (NO romper)

1. Query filters `SubscriberId` en runtime ERP.
2. Platform JWT no accede datos tenant sin impersonación.
3. Impersonación → `switch-subscriber` → contexto company obligatorio para operaciones.

---

## CI / release gate

1. `dotnet build` + `dotnet test` (API + Architecture).
2. `npm run build`.
3. **E2E Manual** workflow (Postgres + API + Playwright) antes de promote prod.
4. Revisar `legacyMigrationPercent` en observability — objetivo subir antes de borrar legacy.

# Tests — ERP SaaS

| Suite | Ubicación | Comando |
|-------|-----------|---------|
| Backend unit/integration | `backend/src/ERP.*.Tests` | `dotnet test backend/src/ERP.slnx` |
| Architecture (NetArchTest) | `backend/src/ERP.Architecture.Tests` | (incluido en solución) |
| Frontend unit | `frontend/src/**/*.test.ts` | `cd frontend && npx vitest run` |
| E2E Playwright | `frontend/e2e/` | `npm run test:e2e` o `scripts/ci/run-e2e.ps1` |

Guardrails adicionales: `tools/architecture/`, `tools/quality/`.

# Architecture Rules (entrada raíz)

**Normativa bloqueante para PRs y agentes IA.**

Documento completo: **[`docs/ARCHITECTURE-RULES.md`](docs/ARCHITECTURE-RULES.md)**

Enforcement automatizado:

| Herramienta | Ruta |
|-------------|------|
| Stack allowlist | `scripts/ci/verify-stack-allowlist.ps1` |
| Architecture guardrails | `tools/architecture/check-architecture-guardrails.ps1` |
| Handler size | `tools/quality/check-handler-size.ps1` |
| NetArchTest | `backend/src/ERP.Architecture.Tests` |

No introducir herramientas fuera del stack oficial (`docs/DEVELOPMENT.md#stack-oficial`).

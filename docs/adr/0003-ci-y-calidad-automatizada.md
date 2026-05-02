# ADR 0003: CI en GitHub Actions y calidad automatizada

**Estado:** Aceptada  
**Fecha:** 2026-05-02  

## Contexto

El monorepo incluye backend (.NET) y frontend (Vite/React). Sin automatización, la regresión y el drift entre ramas cuestan caro; el equipo además definirá ramas `development`, `release/*` y `hotfix/*` tras estabilizar `main`.

## Decisión

- **GitHub Actions** como CI único del repositorio: workflow en `.github/workflows/ci.yml`.
- **Backend:** SDK fijado con `backend/src/global.json` (alineado a imágenes **ubuntu-latest** / Ubuntu 24.04), caché de NuGet, `dotnet test` sobre `backend/src/ERP.slnx`.
- **Frontend:** Node 22, `npm ci`, ESLint, build de producción, **Playwright** smoke mínimo (`npm run test:e2e`) sobre `vite preview` sin exigir API para el caso inicial.
- **Dependabot** semanal para actualizar Actions (`.github/dependabot.yml`).
- Disparos en `main`, `master`, `development`, `develop`, `release/**`, `hotfix/**`, más `workflow_dispatch`.

## Consecuencias

- **Positivas:** misma barra de calidad en PRs; documentación operativa (`docs/DESARROLLO.md`) y código alineados.
- **Negativas:** los PRs esperan a que pasen jobs (y descargas de browsers en CI); hay que vigilar límites de minutos del plan de GitHub.
- **Riesgos / siguientes pasos:** E2E con API + Postgres en CI (service container o Testcontainers) puede añadirse como ADR o ampliación del workflow cuando haya credenciales/seed estables para entorno de prueba.

## Qué cambia

<!-- Breve descripción para revisores. -->

### Decisiones de arquitectura (si aplica)

- [ ] Si esta PR introduce o cambia una decisión estable (stack, tenant, límites de módulo, CI, etc.), agregá o actualizá un **ADR** en [`docs/adr/`](docs/adr/) y el índice en [`docs/adr/README.md`](docs/adr/README.md).

## Checklist (definición de hecho sugerida)

Marcá lo que aplica a este PR.

### Backend

- [ ] Caso de uso con handler y validación acorde a [validación en 4 capas](.cursor/rules/erp-unified-rules.mdc#sec-validation) (donde corresponda).
- [ ] Tests nuevos o ajustados (`ERP.*.Tests`) y `dotnet test` local OK.

### Frontend (si aplica)

- [ ] Ruta en `frontend/src/App.tsx` y menú en `frontend/src/nav/navConfig.ts` si la pantalla es navegable.
- [ ] Inventario actualizado en [`docs/FRONTEND-PANTALLAS.md`](docs/FRONTEND-PANTALLAS.md).
- [ ] Texto nuevo con i18n **es**, **en** y **qu** (`frontend/src/i18n/locales/*.json`).
- [ ] Si cambió el login o el arranque de la SPA: `cd frontend && npm run test:e2e` (Playwright smoke).

### Datos / despliegue (si aplica)

- [ ] Migración EF probada (`dotnet ef database update` desde entorno local).
- [ ] Notas en [`docs/ESTADO-PROYECTO-2026-05.md`](docs/ESTADO-PROYECTO-2026-05.md) o [`docs/REGISTRO-PROYECTO.md`](docs/REGISTRO-PROYECTO.md) si cambia algo relevante para el equipo (migraciones, auth, puertos).

## Cómo lo probé

<!-- Comandos o pasos manuales. -->

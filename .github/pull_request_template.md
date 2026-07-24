## Qué cambia

<!-- Breve descripción para revisores. -->

### Decisiones de arquitectura (si aplica)

- [ ] Si esta PR cambia arquitectura, scopes o BD, actualizá el doc en [`docs/`](docs/) (`ARCHITECTURE.md`, `DATABASE.md`, `IDENTITY.md`, etc.) y [`docs/STATUS.md`](docs/STATUS.md) si afecta delivery.

## Checklist (definición de hecho sugerida)

Marcá lo que aplica a este PR.

### Backend

- [ ] Caso de uso con handler y validación acorde a [validación en 4 capas](.cursor/rules/erp-unified-rules.mdc#sec-validation) (donde corresponda).
- [ ] Tests nuevos o ajustados (`ERP.*.Tests`) y `dotnet test` local OK.

### Frontend (si aplica)

- [ ] Ruta en `frontend/src/App.tsx` y menú en `frontend/src/nav/navConfig.ts` si la pantalla es navegable.
- [ ] Inventario de pantallas/rutas reflejado en [`docs/STATUS.md`](docs/STATUS.md) si aplica.
- [ ] Texto nuevo con i18n **es**, **en** y **qu** (`frontend/src/i18n/locales/*.json`).
- [ ] Si cambió el login o el arranque de la SPA: `cd frontend && npm run test:e2e` (Playwright smoke).

### Datos / despliegue (si aplica)

- [ ] Migración EF probada (`dotnet ef database update` desde entorno local).
- [ ] [`docs/STATUS.md`](docs/STATUS.md) actualizado si cambia estado de entrega (migraciones, auth, puertos, módulos).

## Cómo lo probé

<!-- Comandos o pasos manuales. -->

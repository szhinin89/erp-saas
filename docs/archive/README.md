# docs/archive — Documentos históricos

Este directorio contiene documentos históricos. No son fuente normativa activa. Para reglas vigentes usar `/CLAUDE.md`, `/backend/CLAUDE.md`, `/frontend/CLAUDE.md`, `/STATUS.md` y `/FEATURES.md`.

Nivel 4 de la jerarquía documental (ver `/CLAUDE.md § Jerarquía documental`): snapshots congelados, releases pasadas, auditorías ya ejecutadas, planes de ejecución completados. **No debe usarse para** implementar funcionalidades, tomar decisiones arquitectónicas, definir comportamiento, reglas de negocio, contratos, seguridad ni el modelo multiempresa. Solo tiene valor de registro/bitácora.

## Estructura

| Carpeta | Contenido |
|---|---|
| [`audits/`](./audits/) | Auditorías puntuales de un módulo o del sistema, ya cerradas |
| [`plans/`](./plans/) | Planes de implementación por fases de una feature ya entregada |
| [`designs/`](./designs/) | Documentos de diseño de una feature específica, ya implementada |
| [`archive-ai-rules/`](../decisions/archive-ai-rules/README.md) | *(vive en `docs/decisions/`, no aquí)* — snapshot del antiguo `AI-RULES/` archivado en el Bloque 16B |
| Resto de archivos en `docs/archive/` (sin subcarpeta) | Releases selladas, modelos de scope históricos y otros snapshots previos a esta reorganización (Bloque 16C, 2026-08-07) |

## Contenido no reescrito

El contenido de estos documentos **no se actualiza retroactivamente** — son un registro de lo que era cierto en el momento en que se escribieron (auditoría, diseño o plan de una fase ya cerrada). Pueden contener referencias a rutas o nombres que cambiaron después (p. ej. `AI-RULES/`, `docs/adr/`, `docs/STATUS.md` previos a la reorganización SSOT de los bloques 16A-16C) — eso es esperado y no se considera un enlace roto a corregir, salvo que impida entender el documento.

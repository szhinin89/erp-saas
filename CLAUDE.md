# ERP SaaS ZH Technologies — Reglas globales del proyecto

Onboarding rápido y fuente principal de reglas globales del proyecto. Cuerpo normativo completo: [`docs/architecture/`](docs/architecture/README.md).

---

## Jerarquía documental

1. **`/CLAUDE.md`** (este archivo) — reglas globales, prevalece sobre todo lo demás.
2. **`/backend/CLAUDE.md`** — reglas específicas de backend. Complementa este archivo; no repite reglas globales; si hay conflicto, prevalece este archivo.
3. **`/frontend/CLAUDE.md`** — reglas específicas de frontend. Complementa este archivo; no repite reglas globales; si hay conflicto, prevalece este archivo.
4. **`/STATUS.md`** — solo estado actual del proyecto. No define reglas técnicas.
5. **`/FEATURES.md`** — solo funcionalidades y alcance funcional. No define reglas técnicas.
6. **`/docs/architecture/*`** — documentación técnica extendida; cuerpo normativo completo de cada regla. Puede explicar y detallar, nunca contradecir este archivo.
7. **`/docs/decisions/*`** — decisiones históricas (ADRs). Explican el *por qué*; no son fuente activa si contradicen una regla vigente aquí o en `docs/architecture/*`.
8. **`/docs/archive/**`** — históricos/snapshots congelados. No usar para implementar, decidir arquitectura, ni definir comportamiento, reglas de negocio, contratos, seguridad o el modelo multiempresa.

### Regla de precedencia (conflicto entre documentos)

- `/CLAUDE.md` prevalece siempre.
- `/backend/CLAUDE.md` prevalece solo dentro de `backend/` si no contradice este archivo.
- `/frontend/CLAUDE.md` prevalece solo dentro de `frontend/` si no contradice este archivo.
- `/STATUS.md` y `/FEATURES.md` no definen reglas técnicas — si un lector encuentra una regla ahí, es un defecto a corregir (moverla a `docs/architecture/*`), no una fuente válida.
- `docs/architecture/*` no puede contradecir este archivo — si lo hace, se considera desactualizado y debe corregirse para alinearse.
- `docs/decisions/*` es histórico y no reemplaza las reglas vigentes.
- `docs/archive/**` es Nivel 4 — solo registro/bitácora, nunca fuente de decisión.
- Detalle completo de precedencia (incluye CI/scripts ejecutables, seguridad/multi-tenant, catálogo PR bloqueante): [docs/architecture/enforcement.md § Jerarquía de documentación y precedencia](docs/architecture/enforcement.md#jerarquía-de-documentación-y-precedencia).

---

## Antes de actuar

1. Verificar si el archivo **ya existe** → editar, no regenerar.
2. Seguir el [flujo jerárquico de implementación](docs/architecture/architecture.md#flujo-jerárquico-implementar-una-feature).
3. **No inventar reglas** fuera de `docs/architecture/*` sin confirmación del usuario.

## Al terminar una tarea

Actualizar docs de avance → [docs/architecture/enforcement.md § sincronización docs de avance](docs/architecture/enforcement.md#sincronización-docs-de-avance).

---

## Reglas globales (resumen — cuerpo normativo en `docs/architecture/*`)

- **No romper funcionalidades existentes.** Verificar impacto antes de modificar código compartido o infraestructura CLOSED.
- **Monolito modular + Clean Architecture**: `ERP.API → ERP.Application → ERP.Domain ← ERP.Infrastructure`. No existe `ERP.Shared`. Detalle: [docs/architecture/architecture.md](docs/architecture/architecture.md), [docs/architecture/backend.md](docs/architecture/backend.md).
- **Multi-tenant fail-closed**: toda query de datos de tenant filtra por `TenantId`; sin filtro válido → 0 filas, nunca fuga cross-tenant. Detalle: [docs/architecture/security.md](docs/architecture/security.md).
- **No confiar en tenant/company/branch desde el body** cuando debe venir del contexto autenticado (JWT/`ICurrentTenant`/`ICurrentBranch`) — el body es un hint de UX, nunca autoridad. Ver [docs/architecture/security.md](docs/architecture/security.md), [Branch Ownership Rule](docs/architecture/architecture.md#branch-ownership-rule-obligatoria).
- **No borrar físico** salvo regla explícita — soft delete (`IsActive=false`) es el default; excepciones documentadas (`ExpenseCategory`, `SaasPlan`) en [docs/architecture/backend.md](docs/architecture/backend.md).
- **No duplicar fuentes de verdad**: 1 concepto = 1 implementación (1 entidad, 1 Command por operación, máx. 2 DTOs). Detalle: [docs/architecture/architecture.md § Canonical Model Map](docs/architecture/architecture.md#canonical-model-map).
- **SSOT dinámico para catálogos/datos configurables**: todo dato fiscal (SRI), operativo, configurable o dependiente de tenant/empresa vive en BD/config y se expone vía API — nunca como enum o array estático repetido en frontend/backend; enums solo para estados/flags internos no administrables. Detalle: [docs/architecture/architecture.md § Catálogos y datos configurables (SSOT dinámico)](docs/architecture/architecture.md#catálogos-y-datos-configurables-ssot-dinámico).
- **No crear componentes/CSS duplicados si existe equivalente** — Design System único (`ZH*`), auditoría de reutilización obligatoria antes de escribir UI nueva. Detalle: [docs/architecture/frontend.md](docs/architecture/frontend.md).
- **No introducir Platform/SaaS fuera del alcance ERP Core** — frontera *ERP never depends on Platform* / *Platform may consume ERP APIs only*. Ver [`ERP_CORE_FREEZE.md`](ERP_CORE_FREEZE.md), [docs/architecture/architecture.md § Frontera ERP ↔ Platform](docs/architecture/architecture.md#frontera-erp--platform-bloqueante--ver-adr-erp-002).
- **Reglas críticas de SRI, contabilidad, seguridad e IAM**: configuración tributaria fuente única en el ítem (nunca hardcodeada), secuencias documentales solo vía `CaptureNextAsync`, seguridad multi-tenant innegociable. Detalle: [docs/architecture/frozen-infrastructure.md](docs/architecture/frozen-infrastructure.md), [docs/architecture/security.md](docs/architecture/security.md).
- **Testing/build obligatorio según tipo de cambio**: backend `dotnet test`, frontend `npm run lint && npx tsc --noEmit && npm run build`, guardrails `npm run architecture:check`. Detalle: [docs/architecture/enforcement.md](docs/architecture/enforcement.md).
- **Validación de formularios** (dos niveles, Zod+RHF frontend / FluentValidation backend, `applyServerErrors<T>()` obligatorio): [docs/architecture/form-validation.md](docs/architecture/form-validation.md).
- **Mensajes visuales**: API pública `import { message, MSG } from 'lib/messages'` — nunca importar `_internal/`. Detalle: [docs/architecture/visual-messages.md](docs/architecture/visual-messages.md).
- **Precisión numérica y fechas** (INMUTABLE): `numeric(18,2/4/6)`/`numeric(5,2)` según tipo de dato, `ZhDecimalInput`/`ZhNumberInput` obligatorios, `DateTime.UtcNow` siempre en backend, `formatDate()`/`formatDateTime()` en frontend. Detalle: [docs/architecture/data-standards.md](docs/architecture/data-standards.md).
- **Infraestructuras CLOSED** (Secuencias Documentales, Entity Tracking, Configuración Tributaria, Tipos de Ítem, Defaults de Facturación, ElectronicDocuments v1.0, Entity Audit): inmutables salvo ADR + evidencia técnica + tests + revisión de compatibilidad. Detalle: [docs/architecture/frozen-infrastructure.md](docs/architecture/frozen-infrastructure.md).

**NO duplicar reglas aquí.** Editar siempre el archivo canónico en `docs/architecture/`.

---

## Links a documentos de detalle

| Necesidad | Documento |
|-----------|-----------|
| Reglas backend | [`/backend/CLAUDE.md`](backend/CLAUDE.md) |
| Reglas frontend | [`/frontend/CLAUDE.md`](frontend/CLAUDE.md) |
| Estado de delivery | [`STATUS.md`](STATUS.md) |
| Módulos del producto | [`FEATURES.md`](FEATURES.md) |
| Índice normativo completo | [`docs/architecture/README.md`](docs/architecture/README.md) |
| Precedencia y jerarquía de reglas | [`docs/architecture/enforcement.md`](docs/architecture/enforcement.md) |
| Arquitectura vigente (diagramas, estado) | [`docs/ARCHITECTURE.md`](docs/ARCHITECTURE.md) |
| Acta de congelamiento ERP↔Platform | [`ERP_CORE_FREEZE.md`](ERP_CORE_FREEZE.md) |
| Decisiones históricas (ADRs) | [`docs/decisions/`](docs/decisions/README.md) |
| Arranque local, Docker, tests | [`docs/DEVELOPMENT.md`](docs/DEVELOPMENT.md) |
| Índice maestro humano | [`CONTEXT.md`](CONTEXT.md) |

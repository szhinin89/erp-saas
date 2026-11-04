# ERP SaaS ZH Technologies — Reglas globales del proyecto

Onboarding rápido y fuente principal de reglas globales del proyecto. Cuerpo normativo completo: [`docs/architecture/`](docs/architecture/README.md).

---

## Jerarquía documental

`CLAUDE.md` es la fuente oficial de reglas globales para Codex, Claude, Cursor y
futuros agentes; prevalece sobre los demás documentos. Las instrucciones expresas
del propietario para la tarea tienen prioridad.

| Responsabilidad | Fuente oficial | Entradas y referencias |
|---|---|---|
| Reglas globales de agentes y esta jerarquía | `CLAUDE.md` | [AGENTS.md](AGENTS.md) (Codex), `backend/CLAUDE.md`, `frontend/CLAUDE.md`, `.cursor/rules/*.mdc`; no crean normativa paralela |
| Arquitectura, restricciones y contratos técnicos/funcionales por dominio | [docs/architecture/](docs/architecture/README.md) y las SSOT de dominio allí referenciadas | [enforcement](docs/architecture/enforcement.md#jerarquía-de-documentación-y-precedencia) detalla los gates técnicos bajo esta jerarquía; los adaptadores de capa no los sustituyen |
| Procedimiento de revisión y cierre de agentes | [Flujo por fase](docs/architecture/enforcement.md#flujo-obligatorio-del-agente-por-fase) | Común a todos los agentes |
| Desarrollo y Git | [CONTRIBUTING.md](CONTRIBUTING.md) (contribución/Git), [docs/DEVELOPMENT.md](docs/DEVELOPMENT.md) (operación y stack) | Validación técnica en [enforcement](docs/architecture/enforcement.md); roles de ramas en [Arquitectura](docs/architecture/architecture.md#ci-y-ramas) |
| Alcance funcional del producto | [FEATURES.md](FEATURES.md) | Catálogo de capacidades; enlaza las SSOT, no prescribe contratos técnicos |
| Estado de entregas, gates, cierres y riesgos | [STATUS.md](STATUS.md) | [WORK_QUEUE](docs/agent/WORK_QUEUE.md): solo trabajo abierto autorizado; [ROADMAP](docs/ROADMAP.md): planificación futura |
| Navegación y descripción | [CONTEXT.md](CONTEXT.md), [README.md](README.md), [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md), [ARCHITECTURE_GATES.md](ARCHITECTURE_GATES.md) | Índices/descripciones, sin normativa independiente |
| Justificación de decisiones | [ADRs](docs/decisions/README.md) | Explican el porqué; no reemplazan la SSOT ni los gates |
| Actas y evidencia fechada | [ERP_CORE_FREEZE.md](ERP_CORE_FREEZE.md), `RELEASES/`, `docs/archive/**` | Conservan su evidencia; no definen el inventario ni comportamiento actual |

Ante un conflicto, aplicar esta jerarquía y corregir la copia desactualizada.
Los checks ejecutables son gates obligatorios, no redefinen las reglas globales
ni autorizan omitirlas. Una contradicción de negocio se resuelve según el
[flujo por fase](docs/architecture/enforcement.md#flujo-obligatorio-del-agente-por-fase).

---

## Antes de actuar

- **First-run y credenciales:** ningún agente puede leer, extraer ni consumir tokens first-run, crear administradores o restablecer credenciales sin autorización expresa del propietario para esa operación. El launcher nunca crea administradores ni solicita sus credenciales; solo el propietario completa manualmente el flujo first-run existente ([SSOT First Run](docs/architecture/first-run.md)). No obtener tokens desde logs. Las operaciones destructivas requieren mostrar servidor, base y entorno efectivos, bloquear destinos no autorizados y exigir confirmación explícita del propietario.

1. Identificar la SSOT vigente del módulo ([regla global](docs/architecture/enforcement.md#ssot-por-módulo)) y verificar si el archivo **ya existe** → editar, no regenerar.
2. Seguir el [flujo jerárquico de implementación](docs/architecture/architecture.md#flujo-jerárquico-implementar-una-feature).
3. **No inventar reglas** fuera de `docs/architecture/*` sin confirmación del usuario.
4. Aplicar el [flujo obligatorio del agente por fase](docs/architecture/enforcement.md#flujo-obligatorio-del-agente-por-fase), incluidas sus restricciones CLOSED, clasificación, PostgreSQL real, decisiones del propietario y responsabilidades de staging/commit. No hacer push sin autorización expresa.
5. Leer `STATUS.md`, `FEATURES.md`, `docs/agent/WORK_QUEUE.md` y los ADRs vigentes relevantes (mínimos: [ADR-040](docs/decisions/ADR-040-item-company-scope-inventory-nature.md), [ADR-041](docs/decisions/ADR-041-negative-sale-inventory-cost-obligations.md)) antes de modificar código. Codex entra por `AGENTS.md`; Claude aplica estas mismas lecturas.
6. Mantener respuestas compactas.

## Al terminar una tarea

Actualizar docs de avance → [docs/architecture/enforcement.md § sincronización docs de avance](docs/architecture/enforcement.md#sincronización-docs-de-avance).

---

## Reglas globales y lecturas técnicas

- **ERP ZH V1 es una única primera versión oficial (GREENFIELD).** Una sola fuente de verdad vigente por dominio y una instalación inicial consolidada. No mantener código, rutas, funcionalidades, migraciones, seeds, scripts, ADR ni documentación paralela u obsoleta de versiones nunca publicadas; backend, frontend, base de datos, contratos, pruebas y SSOT deben reflejar exactamente el funcionamiento actual. La depuración de lo obsoleto nunca autoriza operaciones destructivas sobre datos existentes ni relaja las reglas de seguridad: se aplica sobre código/docs, preservando datos y con confirmación del propietario para cualquier acción irreversible.
- **No romper funcionalidades existentes.** Verificar impacto antes de modificar código compartido o infraestructura CLOSED.
Las reglas técnicas se mantienen únicamente en sus fuentes oficiales:

| Antes de trabajar en… | Leer y aplicar |
|---|---|
| Capas, scopes, unicidad, catálogos o frontera ERP ↔ Platform | [Arquitectura](docs/architecture/architecture.md), [backend](docs/architecture/backend.md) |
| Auth, permisos, flujo documental o aislamiento tenant/company/branch | [Seguridad](docs/architecture/security.md) |
| Infraestructura CLOSED/FROZEN, SRI, tracking, secuencias o auditoría | [Infraestructura congelada](docs/architecture/frozen-infrastructure.md) |
| UI, componentes, CSS o reutilización | [Frontend](docs/architecture/frontend.md) |
| Formularios o mensajes | [Validación](docs/architecture/form-validation.md), [mensajes](docs/architecture/visual-messages.md) |
| Valores numéricos de negocio o fechas/horas | [Precisión y contrato temporal](docs/architecture/data-standards.md); lectura previa obligatoria |
| Pruebas, build, CI o documentación | [Enforcement](docs/architecture/enforcement.md) |

Editar la fuente oficial y actualizar sus referencias; no copiar su normativa aquí.

---

## Links a documentos de detalle

| Necesidad | Documento |
|-----------|-----------|
| Reglas backend | [`/backend/CLAUDE.md`](backend/CLAUDE.md) |
| Reglas frontend | [`/frontend/CLAUDE.md`](frontend/CLAUDE.md) |
| Instalación inicial (First Run) | [`docs/architecture/first-run.md`](docs/architecture/first-run.md) |
| Estado de delivery | [`STATUS.md`](STATUS.md) |
| Módulos del producto | [`FEATURES.md`](FEATURES.md) |
| Índice normativo completo | [`docs/architecture/README.md`](docs/architecture/README.md) |
| Precedencia y jerarquía de reglas | [`docs/architecture/enforcement.md`](docs/architecture/enforcement.md) |
| Arquitectura vigente (diagramas, estado) | [`docs/ARCHITECTURE.md`](docs/ARCHITECTURE.md) |
| Acta de congelamiento ERP↔Platform | [`ERP_CORE_FREEZE.md`](ERP_CORE_FREEZE.md) |
| Decisiones arquitectónicas (ADRs) | [`docs/decisions/`](docs/decisions/README.md) |
| Arranque local, Docker, tests | [`docs/DEVELOPMENT.md`](docs/DEVELOPMENT.md) |
| Índice maestro humano | [`CONTEXT.md`](CONTEXT.md) |

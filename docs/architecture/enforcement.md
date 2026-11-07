# Enforcement — validación, docs sync, CI

---

## NO DUPLICAR REGLAS

> **Política anti-drift obligatoria.**

### SSOT por módulo

**Principio:** el código funcional vigente, respaldado por sus pruebas, determina el
comportamiento real del ERP. Cada módulo tiene un único documento SSOT
(`docs/architecture/<modulo>.md` o el archivo canónico definido para su dominio) que
describe exclusivamente ese comportamiento. Para saber cómo funciona hoy un módulo debe
bastar leer su SSOT.

| Documento | Responsabilidad |
|-----------|-----------------|
| SSOT del módulo | Única especificación del comportamiento implementado y comprobado |
| `CLAUDE.md`, `FEATURES.md`, `STATUS.md`, README y documentos generales | Enlazan a la SSOT; no copian sus reglas |
| ADR | Motivo de la decisión arquitectónica; no compite con la SSOT funcional |
| QA, roadmaps, entregas y notas de agente | Cobertura, planificación o evidencia temporal; nunca fuente funcional |
| Documentación de otros módulos | Enlace a la SSOT o explicación breve necesaria para la integración |

Reglas estrictas:

1. **SSOT único:** prohibida la documentación funcional paralela o duplicada de un módulo.
2. **Solo funcionalidad actual:** documentar lo implementado y comprobado. Diseños antiguos,
   propuestas o funcionalidades pendientes no se presentan como operativos.
3. **Sin documentación obsoleta:** eliminar documentos sustituidos, reglas antiguas y
   contradicciones. En preproducción no se conservan reglas por compatibilidad histórica.
4. **Referencias, no duplicaciones:** los documentos generales enlazan a la SSOT.
5. **Actualización obligatoria:** código, pruebas pertinentes y SSOT correspondiente
   se actualizan en el mismo ciclo de trabajo y fase; no posponer su sincronización.
6. **Sin enlaces rotos:** antes de cerrar una fase, verificar que los enlaces modificados,
   sus anclas y los documentos referenciados existen.
7. **Sin rutas paralelas:** no conservar implementaciones antiguas, fallbacks ni adaptadores
   de compatibilidad ya reemplazados y sin dependencia vigente.
8. **Sin documentos de entrega redundantes:** los informes temporales del agente no se
   convierten en otra fuente funcional permanente.
9. **Una única verdad:** ante una contradicción entre documentos, contrastar con código y
   pruebas vigentes, corregir la SSOT y eliminar la contradicción.
10. **Gate documental:** cada fase verifica unicidad de la SSOT, referencias vigentes,
    ausencia de contradicciones y `git diff --check`.

**Condición de cierre:** una fase no puede declararse CLOSED si deja documentación
funcional duplicada, referencias rotas o reglas vigentes contradictorias.

**Prohibido:** documentación por fase que replique el comportamiento del módulo,
historiales operativos innecesarios o especificaciones que ya no corresponden al sistema.

| ✅ Correcto | ❌ Incorrecto |
|------------|---------------|
| Regla completa **una vez** en `docs/architecture/*` | Misma regla repetida en `CLAUDE.md` + `backend/CLAUDE.md` + `frontend/CLAUDE.md` + `docs/architecture/*` |
| `backend/CLAUDE.md`/`frontend/CLAUDE.md` con enlace + hint ≤5 líneas | Copiar 300 líneas al `CLAUDE.md` de capa |
| Editar canónico → actualizar enlaces | Editar solo `CLAUDE.md` y olvidar canónico |

Al añadir una regla: ampliar la SSOT existente o, si el dominio aún no tiene una,
definir un único archivo canónico → actualizar sus referencias de entrada.

Ver [Compatibilidad multi-agente](#compatibilidad-multi-agente) más abajo en este documento.

---

## Flujo obligatorio del agente por fase

Regla de trabajo del agente acordada con el propietario (2026-10-25):

Protocolo obligatorio: **revisar lo implementado → validar funcionalmente → corregir únicamente bugs reales → documentar → cerrar → avanzar**, respetando los gates y responsables siguientes.

1. Leer `CLAUDE.md`, `STATUS.md`, `FEATURES.md` y la SSOT vigente del módulo, además de las lecturas comunes de [CLAUDE.md](../../CLAUDE.md#antes-de-actuar).
2. Trabajar en un chat nuevo por fase, con alcance acotado.
3. Revisar de forma focal lo implementado dentro del alcance autorizado, sin auditorías generales, y clasificar los hallazgos como `KEEP`, `BUG REAL`, `DEBT` o `FALTA`. Validar funcionalmente según el tipo de cambio y los checks aplicables; una tarea exclusivamente documental no requiere pruebas funcionales.
4. Resolver las decisiones funcionales con el propietario antes de implementar. Ante contradicción de negocio o dominio no definido, detenerse y reportar únicamente la decisión necesaria; no asumir reglas.
5. En la revisión de lo implementado, corregir únicamente BUG REAL: reproducir antes y aplicar el fix mínimo mediante una implementación corta y focalizada, sin refactors fuera de alcance.
6. Ejecutar pruebas focales pequeñas y entregar al propietario los comandos concretos para el gate intermedio en su PowerShell. Mantener los checks obligatorios aplicables; usar PostgreSQL real para concurrencia, transacciones, scope, locks e invariantes de persistencia; no sustituirlo por SQLite, in-memory ni mocks para esos casos.
7. Esperar el resultado del gate ejecutado por el propietario: `PASS` o `FAIL`. Las pruebas del agente no sustituyen este gate.
8. Si es `FAIL`, corregir de forma focalizada, ejecutar las pruebas pertinentes y volver al gate del propietario. Si es `PASS`, realizar la revisión final del diff y la documentación.
9. El propietario realiza staging y commit. El agente no ejecuta esas operaciones.
10. Solo después del gate PASS, la revisión final y el staging y commit del propietario, declarar la fase `CLOSED`. Abrir un chat nuevo para la siguiente fase.

**CLOSED = no reauditar ni rediseñar salvo BUG REAL directamente relacionado.**

ROADMAP, deudas históricas y mejoras futuras no se convierten en nuevas tareas ni bloqueantes V1 por esta revisión. La clasificación `DEBT` o `FALTA` no autoriza su implementación ni reabre módulos CLOSED.

Este flujo es la instrucción vigente para el agente. Las categorías históricas de entregas no se reclasifican retroactivamente.

---

## Validación en 4 capas (datos persistidos)

Toda validación que afecte datos guardados se refleja en **4 capas**. Prohibido validar solo en frontend.

| Capa | Herramienta | Ubicación |
|------|-------------|-----------|
| 1 Frontend | Zod + react-hook-form | `frontend/src/schemas/{modulo}/`; `zodResolver`; error por campo |
| 2 Application | FluentValidation + MediatR | `[Nombre]Validator`; `ValidationBehavior` |
| 3 Domain | Guard clauses + factories | `Entidad.Create(...)`; `DomainException` |
| 4 BD | EF Core configuration | `IsRequired`, `HasMaxLength`; índices únicos con `TenantId` |

### Convención de errores

| Capa | Formato |
|------|---------|
| Frontend | Mensajes en español junto al campo |
| Application | `ValidationException` → **422** |
| Domain | `DomainException` vía middleware |
| API | `ApiResponse<T>` (envelope `code/severity/message{user,dev}/data/meta`, `code` es la fuente única de verdad vía `MessageCatalog`; `meta.correlationId` viene siempre de `RequestCorrelationMiddleware`); `Result<T>` fallido → **400** — contrato **LOCKED**, ver BACKEND-RULES.md §"API Response Contract V1 — LOCKED" |

Detalle backend/frontend: [BACKEND-RULES.md](./backend.md), [FRONTEND-RULES.md](./frontend.md).

---

## Catálogo PR bloqueante

Reglas B-xx / F-xx con severidad **BLOQUEANTE**: [PR-RULES-CATALOG.md](./pr-rules-catalog.md)

Entrada raíz PR: `docs/ARCHITECTURE-RULES.md` (adaptador).

---

## Roles y acceso UI — contrato congelado

Fuente única de roles (`SecurityRoles` backend / `isAdminRole()` frontend) y modelo de
gating de páginas (server-driven menu + `isAdminRole`, deny-by-default): ver
[SECURITY.md — Security & Access Contract V1 — LOCKED](./security.md#security--access-contract-v1--locked)
y regla [SEC-01b / PR-18](./pr-rules-catalog.md#sec-01b--roles-fuente-única-securityroles--isadminrole).

---

## Sincronización docs de avance

`STATUS.md` es la única fuente de estado de entrega, gates y cierres. Actualizarla
cuando cambie ese estado, sin copiar reglas funcionales ni cronologías de implementación.
`FEATURES.md` describe capacidades actuales y enlaza la SSOT; `docs/agent/WORK_QUEUE.md`
contiene únicamente trabajo abierto autorizado; `docs/ROADMAP.md` conserva planificación futura.
Actualizar README o runbooks solo si cambian sus entradas o instrucciones de operación.
No se exige un mapa visual inexistente ni otro espejo manual de avance.
El dashboard existente tiene sus propias fuentes y scripts; no es autoridad de cierre.

Cursor hint: `.cursor/rules/docs-progress-status-sync.mdc`.

---

## Tests pre-merge

Aplicar únicamente los checks correspondientes al cambio:

| Cambio | Gate técnico |
|---|---|
| Backend | `dotnet test` de los proyectos/filtros afectados; PostgreSQL real para los casos del flujo por fase |
| Frontend | Desde `frontend/`: `npm run lint`, `npx tsc --noEmit`, `npm run build` |
| Arquitectura/código | Desde `frontend/`: `npm run architecture:check` (ratchet oficial) **y**, desde la raíz, `tools/architecture/check-architecture-guardrails.ps1 -SkipFrontendChunk` (límites PR-1–PR-10, handlers; mismo paso que CI). Antes de commitear: `tools/architecture/check-staged-guardrails.ps1` (Stack Audit + Architecture guardrails) sobre el contenido en staging |
| Solo documentación | `node tools/architecture/run-all.mjs --only documentation`, referencias modificadas y `git diff --check`; sin pruebas funcionales |

Pruebas focales y gate del propietario: [flujo por fase](#flujo-obligatorio-del-agente-por-fase).
Comandos de operación y distribución CI/E2E: [DEVELOPMENT](../DEVELOPMENT.md#tests).

---

## Guardrails automatizados

### Plan de CI y activación selectiva

El orquestador `.github/workflows/ci.yml` genera su plan mediante
[`ci_changes.mjs`](../../tools/ci/ci_changes.mjs). **Etapa 2: activación publicada por
autorización del propietario**, con `SELECTIVE_ENABLED = true` en ese archivo (no en el
YAML). Es una constante versionada sin override de entorno ni input manual. Evidencia
real en Actions, confirmada por el propietario: etapa 1 sobre `44f3f9709`, CI #1110
SUCCESS; etapa 2 sobre `01f1ff6f3` (perfil completo), CI #1111 SUCCESS; perfil frontend,
CI #1112 y #1115 SUCCESS; perfil documental, CI #1113 SUCCESS. Los perfiles backend y
mixto aún no tienen ejecución real independiente; no CLOSED (ver «Validación pendiente»).
El resumen distingue perfil propuesto y efectivo.

El detector compara `before` y SHA final en push, y merge-base de base/head fijos del
evento en PR; las pruebas se realizan sobre el SHA de integración de Actions. Usa
Git con historial completo y rutas NUL, sin detección de renombres para incluir
origen y destino. Historial ausente, rama nueva o evento no reconocido exige completo.
El job `changes` también verifica whitespace del rango cuando está disponible.

La selección admite únicamente Markdown en la lista positiva del selector
y entradas informativas explícitas; no admite `docs/**` indiscriminadamente. Código
frontend/backend ordinario selecciona su dominio; CI, guardrails, configuración,
seguridad sensible y rutas desconocidas exigen completo. La seguridad sensible se
detecta por términos en la ruta: un archivo sin efecto de seguridad cuyo nombre contiene
uno de ellos también exige completo (falso positivo conservador diagnosticado en CI #1114
con `reportScope.tsx`; no corregido). Arquitectura completa se
conserva para todo cambio no documental. El perfil documental conserva Stack Audit,
Identity, Platform y el check documental. La suite backend no se subdivide por módulo.

`ci-gate` (`CI gate` en GitHub) usa `always()` y dependencias explícitas. Exige plan
válido para el SHA del evento y `success` de cada job requerido; fallo, cancelación,
ausencia u omisión indebida fallan. Solo permite `skipped` para jobs excluidos por
el plan. Publicación exige este gate y perfil completo para el mismo SHA; nunca
se interpreta un CI documental como validación de release. Sus pruebas están en
[`ci_changes.test.mjs`](../../tools/ci/ci_changes.test.mjs).

| Cambio con clasificación válida | Perfil efectivo | Publicación automática API |
|---|---|---|
| Solo documentación admitida, incluido STATUS | Documentación, Stack Audit, Identity y Platform; sin backend/frontend | No |
| Frontend ordinario | Arquitectura completa, frontend y seguridad | No |
| Backend ordinario sin elegibilidad de publicación | Arquitectura completa, backend (13 grupos) y seguridad | No |
| Backend publicable o frontend/backend publicables | Completo: arquitectura, backend (13 grupos), frontend y seguridad | Tras CI gate exitoso |
| Seguridad sensible, CI, infraestructura o rutas desconocidas | Completo | Solo con elegibilidad e impacto API/empaquetado |
| Manual | Completo | No, salvo `publish_api` explícito en main/develop |

La publicación automática mantiene los eventos elegibles anteriores (push main/develop,
PR a main), pero requiere impacto API/empaquetado o incertidumbre del historial.
Documentación, frontend ordinario y cambios exclusivos de CI no publican API.
La ejecución manual siempre valida completo; solo publica al marcar `publish_api`
en main/develop y no despliega Azure. Azure conserva su condición de push a main
y variable existente; no se modifican secretos ni destinos. Ambos workflows evitan
cancelar ejecuciones activas de main mediante `cancel-in-progress: false` efectivo;
la concurrencia de GitHub conserva como máximo un pendiente por grupo, sin garantizar
FIFO. No se debe usar una ejecución pendiente como evidencia de release. Si el historial
no permite clasificar, se exige completo y puede publicarse en eventos elegibles; la
garantía documental requiere un rango válido y rutas admitidas. Documentos desconocidos
no se admiten automáticamente por su extensión.

**Protección de main:** el propietario confirma que no hay protección clásica ni
rulesets del repositorio. Esta activación no crea ni presupone protección: CI gate
condiciona la publicación dentro del workflow, pero no impide pushes o merges directos.
Solo con autorización separada, configurar una regla para `main` en Settings → Branches
o un ruleset de rama en Settings → Rules → Rulesets; exigir el check `CI gate` observado
en Actions, con GitHub Actions como origen esperado, y revisar actores con bypass.
No es necesario habilitar el requisito de pull request para exigir checks. Incluir
administradores en el cumplimiento si se pretende que tampoco puedan omitirlo.

Con el requisito efectivo y sin bypass, el push directo de un SHA nuevo sin CI previo
puede ser rechazado: el CI posterior al push en main no puede aprobarlo anticipadamente.
Para conservar un flujo sin PR, validar primero el mismo SHA en una rama con trigger
existente (p. ej. `release/ci-validation-*`, que exige completo), y después hacer push
fast-forward de ese SHA aprobado a main, sin crear otro commit y comprobando que main
no avanzó. Un bypass permitiría omitir el requisito y no equivale a protección efectiva.
No se modifica este procedimiento ni configuración en esta fase.
[Referencia de GitHub sobre checks requeridos y pushes](https://docs.github.com/en/repositories/configuring-branches-and-merges-in-your-repository/managing-protected-branches/about-protected-branches#require-status-checks-before-merging).

**Validación pendiente:** antes del cierre, verificar mediante Actions reales los perfiles
backend y mixto. Documentación, frontend, completo y omisiones/gate ya están verificados
(CI #1111, #1112, #1113 y #1115).
Si existe merge queue, añadir su trigger `merge_group` antes de activarla (el selector
ya exige completo para ese evento). La ejecución manual permite CI completo en
cualquier fase. Vitest, E2E y los requisitos independientes de producción se mantienen.

Limitación de verificación inicial (2026-11-06): GitHub CLI respondió que no hay sesión
autenticada al consultar protección de main, rulesets y reglas efectivas; el conector
disponible no expone esas consultas. No se verificaron checks obligatorios ni bypasses,
no se cambiaron protecciones. El contexto confirmado por el propietario para etapa 2
resuelve esa limitación: main no tiene protección configurada.

Validación local de etapa 1 (2026-11-06): selector/gate 34/34, arquitectura 147/147
y ratchet sin violaciones nuevas, guardrail PowerShell `-SkipFrontendChunk`, Stack
Audit, enlaces de los dos documentos modificados y `git diff --check` PASS. Los ocho
workflows pasan parseo YAML, dependencias locales y contratos de inputs reutilizables
con el parser ya disponible de Playwright; `actionlint` no está instalado. Esta
comprobación no sustituye el lint específico ni las ejecuciones reales de Actions.

Validación local de etapa 2 (2026-11-06): selector/gate 36/36, incluidos 20 escenarios
push/PR por CLI con Git real y 42 planes documentales sin publicación; arquitectura
147/147 y ratchet sin violaciones nuevas; guardrail PowerShell, Stack Audit, YAML y
contratos de los ocho workflows, referencias documentales y `git diff --check` PASS.
El propio diff de activación exige completo y no solicita publicación API. Sin cambios
en protecciones, staging, commit, push ni despliegues. La evidencia real de etapa 2 llegó
después con CI #1111 SUCCESS.

### Documentación (control focal sin red)

El check `documentation` de `run-all.mjs`, incorporado al runner y CI existentes,
protege los destinos y anclas Markdown de las entradas principales, fuentes técnicas
y adaptadores. Valida referencias de autoridad y rechaza listas de precedencia
paralelas en las entradas. [Alcance, límites y pruebas](../../tools/architecture/README.md#protección-documental).
No pretende inferir contradicciones de negocio ni validar todos los archivos históricos.

### Backend / monorepo (PowerShell + .NET)

| Herramienta | Ruta |
|-------------|------|
| Stack allowlist | `scripts/ci/verify-stack-allowlist.ps1` |
| Architecture guardrails | `tools/architecture/check-architecture-guardrails.ps1` |
| Guardrails sobre staging (precommit, manual) | `tools/architecture/check-staged-guardrails.ps1`: materializa el índice en un temporal y ejecuta allí Stack allowlist y Architecture guardrails (`-SkipFrontendChunk`), como CI en push; no define reglas, no modifica árbol ni índice, falla cerrado (exit ≠ 0). No cubre el chunk Vite ni otros jobs |
| Identity guardrails | `tools/architecture/check-identity-guardrails.ps1` |
| Handler size | `tools/quality/check-handler-size.ps1` |
| NetArchTest | `backend/src/ERP.Architecture.Tests` |

### Frontend + backend architecture (Node.js — enforcement ejecutable)

| Script | Comando | Qué valida |
|--------|---------|------------|
| Runner | `npm run architecture:check` (desde `frontend/`) | Pruebas de guardrails + checks registrados + ratchet + score + JSON report |
| Documentación | `node tools/architecture/run-all.mjs --only documentation` (raíz) | Enlaces locales, anclas y referencias de autoridad de las entradas de agentes; sin red |
| Pages wrapper | `npm run architecture:pages` | `pages/**/*.tsx` ≤15 líneas, sin hooks/api |
| Import boundaries | `npm run architecture:imports` | Imports prohibidos, profundidad relativa |
| Module boundaries | `npm run architecture:modules` | Pares de módulos que nunca pueden depender entre sí (`forbiddenCrossImports`) |
| Subscriber contracts | `npm run architecture:subscriber` | `frontend-subscriber-naming`: cross-módulo solo vía `modules/<owner>/facades/<x>Facade.ts`; naming de facades; sin CSS privado de otro módulo. Tests: `node --test tools/architecture/check-frontend-subscriber-naming.test.mjs` |
| CSS prefixes | `npm run architecture:css` | Prefijos por área, clases ambiguas |
| Cross-layer | `npm run architecture:cross-layer` | Pages/stores sin capas prohibidas |
| Backend (4 checks) | `npm run architecture:backend` | Layering csproj, usings, controllers, tenant |
| JSON + score | `npm run architecture:report` | `architecture-report.json` con `architectureScore` |
| GitHub annotations | `node tools/architecture/run-all.mjs --annotate` | `::error file=…::` / `::warning file=…::` |

Fuente: [`tools/architecture/README.md`](../../tools/architecture/README.md)

#### Backend checks (Node, heurístico — sin Roslyn)

| Check | Regla | Qué valida |
|-------|-------|------------|
| `backend-layering` | B-layering | Referencias de proyecto/paquetes por capa (.csproj) |
| `backend-clean-architecture` | B-domain / B-application | `using` prohibidos en Domain/Application |
| `backend-controller-thin` | B-controller | Líneas máx., patrones EF/SQL inline (error); líneas > umbral warning |
| `backend-subscriber-rules` | B-subscriber | `IgnoreQueryFilters()` en producción fuera de allowlist; entidades sin `TenantId`/marker. Excluye proyectos `*.Tests` solo en este check de aislamiento runtime. |

El scanner ignora comentarios C#/XML conservando números de línea y no cuenta la expresión canónica de `PlatformQueryAccessor.AsPlatformQuery`; sus callers productivos siguen auditándose individualmente y no se consideran automáticamente seguros.

Umbrales y allowlists: `tools/architecture/config/architecture-rules.json` → `backend`.

#### Architecture score

`calculate-score.mjs` + `config/scoring-rules.json` producen en `architecture-report.json`:

- `architectureScore` (0–100), `status` (`healthy` | `warning` | `critical`)
- `driftRisk` (`low` | `medium` | `high`)
- `modules` — desglose por módulo frontend/backend
- `adrs` — índice de ADRs vigentes

Penalizaciones: violations (−8), warnings (−2), entradas grandfather (−1). Solo afecta reporte/CI; **cero impacto runtime**.

#### Architecture ratchet

`npm run architecture:check` es el único gate oficial del ratchet de findings Node; no ejecuta el guardrail PowerShell sobre el repositorio (sus pruebas solo usan fixtures), por lo que PR-7 y los demás límites de `check-architecture-guardrails.ps1` requieren ese script, como hace CI. Compara findings con el snapshot versionado [`tools/architecture/architecture-baseline.json`](../../tools/architecture/architecture-baseline.json). La deuda histórica no puede crecer: cualquier finding nuevo falla, aunque desaparezca otro finding en el mismo cambio. Resolver deuda pasa y se informa; el baseline solo baja mediante una edición revisada del snapshot, nunca se regenera ni aumenta automáticamente.

La identidad estable de cada finding es `(check, rule, file, message)`; los números de línea se excluyen para tolerar movimientos de código y las ocurrencias idénticas conservan multiplicidad. El parser valida el schema y el desglose antes de comparar; baseline ausente o malformado falla cerrado.

Baseline versionado actual: **0** ocurrencias — deuda histórica saldada en todas las categorías (`violations: []`, `byCheck: {}`); desde aquí cualquier hallazgo del gate es nuevo y falla. (`duplicate-services`, `backend-subscriber-rules`, `design-system`, `module-boundaries`, `frontend-subscriber-naming` y `css-prefixes` en 0; los cruces entre módulos van solo por facades públicas en `modules/<owner>/facades/`, sin allowlist por pares). `F-04-token` acepta un `var(--x)` solo si `--x` existe en `design-tokens.css`, está declarado en el mismo archivo CSS (custom property local; comentarios no cuentan) o lo fija en runtime (`style={{ "--x": … }}` / `setProperty("--x")`) un componente que importa ese mismo CSS; una custom property declarada en otro CSS no habilita su uso. Los scans cross-tenant de backfills/bootstrap steps usan `AsPlatformQuery()` (`PlatformQueryAccessor`) solo para descubrimiento de solo lectura, con re-scope explícito antes de escribir (`JobExecutionContext.Begin` por tenant/company, `Where` por TenantId+CompanyId y un `SaveChanges` por scope). El checker `duplicate-services` identifica cada registro por namespace completo + nombre simple: registros simples y calificados de la misma interfaz se cuentan juntos; interfaces homónimas en namespaces distintos se cuentan por separado; un registro sin calificar junto a varias homónimas falla cerrado por ambigüedad. `backend.duplicateServices.allowedMultiRegistration` es una lista cerrada por nombre simple (sin wildcards) de interfaces strategy/plugin consumidas vía `IEnumerable<T>` por un resolver/orquestador. El snapshot inicial fue 244; se retiraron 25 hallazgos de proyectos `*.Tests` solo para la regla runtime, 2 falsos positivos de comentarios/wrapper y 4 llamadas productivas después de validación PostgreSQL real. El baseline es deuda visible, no aprobación arquitectónica: `architecture-report.json` conserva findings/checks crudos y score/status de salud; su bloque `ratchet` distingue baseline histórico, nuevas y resueltas. Las reglas de aislamiento multi-tenant (`IgnoreQueryFilters`) y límites de módulo siguen siendo violaciones visibles.

#### PR annotations (GitHub Actions)

`formatters/github-formatter.mjs` convierte violations/warnings en anotaciones:

```
::error file=frontend/src/pages/Foo.tsx,line=12::PR-6 violation: pages wrapper exceeds 15 lines
::warning file=backend/src/ERP.API/Controllers/BarController.cs::B-controller-warn: controller has 200 lines
```

Auto-emite en CI cuando `GITHUB_ACTIONS=true` (integrado en `run-all.mjs`).

#### Checks event-driven + IA (Node.js)

| Script | Qué valida |
|--------|------------|
| `check-domain-events-rules.mjs` | Naming eventos (past tense + `Event` suffix); solo en `ERP.Domain`; no en `ERP.API` |
| `check-ai-layer-boundaries.mjs` | `ERP.Domain`/`ERP.Application` no referencian paquetes IA; `ERP.AI.*` no acceden a DbContext ERP directamente |

---

#### ADRs (rationale, no enforcement)

| ADR | Tema |
|-----|------|
| [ADR-001](../decisions/ADR-001-modular-monolith.md) | Modular monolith |
| [ADR-002](../decisions/ADR-002-no-erp-shared.md) | Sin ERP.Shared |
| [ADR-003](../decisions/ADR-003-pages-wrapper-only.md) | Pages wrapper |
| [ADR-004](../decisions/ADR-004-clean-architecture-enforcement.md) | Clean Architecture + checks |
| [ADR-005](../decisions/ADR-005-multi-tenant-query-filters.md) | Multi-tenant filters |
| [ADR-006](../decisions/ADR-006-multi-agent-governance.md) | Governance multi-agente |
| [ADR-007](../decisions/ADR-007-domain-events-foundation.md) | Domain events foundation |
| [ADR-008](../decisions/ADR-008-outbox-pattern-foundation.md) | Outbox pattern foundation |
| [ADR-009](../decisions/ADR-009-ai-layer-separation.md) | Separación capa IA |

Índice: [`docs/decisions/README.md`](../decisions/README.md). **`docs/architecture/*`** = reglas; **ADRs** = por qué.

**Config:**

| Archivo | Uso |
|---------|-----|
| `tools/architecture/config/architecture-rules.json` | Reglas FE/BE import/module/cross-layer/backend + `exemptions` + `adr.index` |
| `tools/architecture/config/css-prefixes.json` | Mapa path → prefijo CSS |
| `tools/architecture/config/scoring-rules.json` | Pesos del architecture score |
| `tools/architecture/architecture-grandfather.json` | Legacy permitido (`tsxPageWrapperMaxLines15`, …) |

**Extender reglas:** editar JSON de config → `npm run architecture:check`. Documentar en este archivo (tabla) si la regla es normativa nueva.

**Excepciones:** añadir path a `architecture-grandfather.json` o `architecture-rules.json` → `exemptions`. Requiere ADR o nota en PR. No silenciar checks en código.

Grandfather legacy: `tools/architecture/architecture-grandfather.json`

C-03: `handlerHandleMaxLines150` usa objetos `{file, maxHandleLines, baselineCommit, adr}` según [ADR-046](../decisions/ADR-046-historical-handler-size-debt.md); no admite rutas sin techo. El guardrail PowerShell valida configuración, crecimiento y retirada de excepciones obsoletas. Sus fixtures reales se ejecutan dentro de `npm run architecture:check`.

PR-7: `tsxMaxLines500` usa objetos `{file, maxLines, baselineCommit, adr}` según [ADR-047](../decisions/ADR-047-historical-tsx-size-debt.md); no admite rutas sin techo. `check-architecture-guardrails.ps1` valida la configuración en cualquier modo, falla por crecimiento sobre el techo o por excepción obsoleta (≤ 500 líneas no vacías) y mantiene 500 para el resto. Fixtures en `check-architecture-guardrails.test.mjs`, dentro de `npm run architecture:check`.

**Integridad del grandfather (bloqueante):** `check-grandfather-integrity.mjs` hace fallar `architecture:check` si una entrada apunta a un archivo inexistente (mayúsculas exactas), queda fuera del alcance de la regla que la consume (p. ej. regla F-04 que no aplica al tipo de archivo), está malformada o pertenece a una lista sin checker consumidor registrado. El mensaje identifica lista + ruta. Listas vacías son válidas. Al corregir o mover un archivo grandfathered, eliminar su entrada en el mismo PR. Toda lista nueva se registra con su consumidor en `defaultListSpecs()`.

Ramas y CI: ver [architecture.md § CI y ramas](./architecture.md#ci-y-ramas) (cuerpo normativo único — no duplicado aquí).

---

# Compatibilidad multi-agente

Entradas para que **Codex**, **Claude**, **Cursor** y futuros agentes consuman la [misma jerarquía](../../CLAUDE.md#jerarquía-documental).

## Fuente canónica

Reglas globales: [CLAUDE.md](../../CLAUDE.md). Arquitectura y contratos por dominio: [índice canónico](README.md), bajo esa jerarquía.

Ningún agente debe inventar convenciones fuera de estos archivos sin confirmación explícita del usuario.

## Cómo consume cada agente

| Agente | Punto de entrada | Qué lee en la práctica |
|--------|------------------|------------------------|
| **Cursor** | `.cursor/rules/*.mdc` (`alwaysApply`, `globs`) | Adaptadores livianos → enlaces a `docs/architecture/*` |
| **Codex** | [`AGENTS.md`](../../AGENTS.md) → [`CLAUDE.md`](../../CLAUDE.md) | Mismas reglas globales, lecturas y fuentes técnicas |
| **Claude Code** | `CLAUDE.md`, `backend/CLAUDE.md`, `frontend/CLAUDE.md` | Onboarding + índice → enlaces a `docs/architecture/*` |
| **Humanos / PR** | `CONTEXT.md` | Índice → [pr-rules-catalog.md](./pr-rules-catalog.md) |
| **Futuros agentes** | [`docs/architecture/README.md`](./README.md) | Mismo índice canónico |

## Adaptadores (obligatorio mantener)

Los adaptadores **no duplican** reglas extensas. Solo:

- Enlaces al canónico
- Hints operativos mínimos (globs Cursor, checklist de 3–5 ítems)
- Referencias cruzadas entre áreas

| Adaptador | Propósito |
|-----------|-----------|
| `AGENTS.md` | Entrada Codex; enlace a las reglas y lecturas comunes |
| `CLAUDE.md` | Entrada Claude y autoridad global común para todos los agentes |
| `backend/CLAUDE.md` | Entrada backend; complementa `CLAUDE.md`, enlaza a `docs/architecture/*` |
| `frontend/CLAUDE.md` | Entrada frontend; complementa `CLAUDE.md`, enlaza a `docs/architecture/*` |
| `erp-unified-rules.mdc` | Regla transversal Cursor (`alwaysApply: true`) |
| `rules-consolidated-map.mdc` | Mapa de precedencia Cursor |
| `backend-*.mdc`, `frontend-*.mdc` | Scope por glob en Cursor |

## Executable Enforcement

Las reglas críticas de frontend **no dependen únicamente** de prompts IA.

**Capa oficial:** `tools/architecture/*.mjs` (Node.js ESM, sin deps pesadas).

| Comando | Uso |
|---------|-----|
| `npm run architecture:check` | CI + pre-merge (desde `frontend/`) |
| `node tools/architecture/run-all.mjs` | Mismo runner desde raíz |
| `npm run architecture:report` | Artefacto JSON para agentes/CI |

Si un agente (Cursor, Claude, futuro) sugiere código que viola un check, **el CI fallará** aunque el agente no haya leído el adaptador.

Detalle por check: [§ Guardrails automatizados](#guardrails-automatizados) más arriba · [`tools/architecture/README.md`](../../tools/architecture/README.md)

## CI Authority

Aplicar [CLAUDE.md](../../CLAUDE.md#jerarquía-documental) y los
[gates técnicos](#jerarquía-de-documentación-y-precedencia). Un agente corrige el
incumplimiento o propone un cambio explícito de configuración/ADR; no ignora CI.
Los ADRs no anulan un check sin el cambio de configuración correspondiente.

Rationale histórico: [`docs/decisions/ADR-006-multi-agent-governance.md`](../decisions/ADR-006-multi-agent-governance.md).

## Integrar un agente nuevo

1. Leer [CLAUDE.md](../../CLAUDE.md), este documento y el [índice canónico](./README.md).
2. Crear un adaptador mínimo (≤80 líneas) que enlace a `docs/architecture/*`.
3. **No** copiar cuerpos completos de reglas al adaptador.
4. Registrar el adaptador en [`docs/architecture/README.md`](./README.md) y `CONTEXT.md`.

---

# Jerarquía de documentación y precedencia

La [jerarquía documental única está en CLAUDE.md](../../CLAUDE.md#jerarquía-documental). Esta sección detalla la aplicación técnica bajo esas reglas globales.

## Capas de aplicación bajo las reglas globales

- CI y scripts ejecutables son gates obligatorios; su resultado no redefine las
  reglas globales ni permite omitirlas. Una discrepancia se resuelve explícitamente.
- Seguridad y aislamiento multi-tenant son innegociables; el catálogo PR define las
  reglas bloqueantes y cada SSOT conserva su detalle específico.
- Entre reglas técnicas compatibles se aplica la más específica del área.
- Los `globs` seleccionan qué referencia de agente se lee; no dan precedencia a
  una copia frente a su fuente oficial.

### ADRs vs docs/architecture/*

| Fuente | Responde | Ejemplo |
|--------|----------|---------|
| **ADRs** (`docs/decisions/ADR-*.md`) | *¿Por qué esta decisión?* | Modular monolith, no ERP.Shared |
| **`docs/architecture/*`** | *¿Qué hacer / qué prohíbe CI?* | PR-6 pages wrapper, B-layering |
| **`tools/architecture/`** | *¿Cumple el repo ahora?* | Score, violations, annotations |

## Resolución de conflictos

Aplicar la [jerarquía oficial](../../CLAUDE.md#jerarquía-documental) y el
[flujo de decisiones del propietario](#flujo-obligatorio-del-agente-por-fase).
No inventar soluciones a contradicciones de negocio ni omitir gates fallidos.

## Qué NO es fuente de verdad

- Comentarios sueltos en código
- Preferencias personales en PRs
- Diagramas desactualizados fuera de `docs/architecture/` o ADRs vigentes
- Copias duplicadas de reglas en `CLAUDE.md`, `backend/CLAUDE.md`, `frontend/CLAUDE.md` o `.mdc`

## Flujo recomendado al implementar

Ver tabla en [architecture.md § Flujo jerárquico](./architecture.md#flujo-jerárquico-implementar-una-feature).

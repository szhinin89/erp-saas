# Anticipos de Clientes V1 — Plan de implementación (CA-F1 a CA-F5 cerradas; CA-F6 aceptada, pendiente de CI)

Decisiones: [ADR-052](../decisions/ADR-052-customer-advances-v1-architecture.md) (**ACCEPTED**, 2026-11-26). Reglas y mecanismos:
[diseño](../decisions/CUSTOMER-ADVANCES-V1-DESIGN-01.md). Estado de entrega: [STATUS](../../STATUS.md); cola:
[WORK_QUEUE](WORK_QUEUE.md).

**Este plan no autoriza ninguna fase por sí mismo.** Estado (2026-11-27): CA-F0 aprobada; **CA-F1 CLOSED** (CI #1249
SUCCESS); **CA-F2 CLOSED** (CI #1250 SUCCESS); **CA-F3 CLOSED** (CI #1252 SUCCESS);
**CA-F4 CLOSED** (CI #1253 SUCCESS); **CA-F5 CLOSED** (CI #1254 SUCCESS); **CA-F6 aceptada por el propietario (2026-11-27), pendiente de push, CI y cierre formal**.
Cada fase requiere el ADR aprobado, autorización expresa del propietario con su lista de
rutas (incluidos los componentes protegidos), gate del propietario y staging y commit del propietario
([flujo por fase](../architecture/enforcement.md#flujo-obligatorio-del-agente-por-fase)). Sin suite completa; PostgreSQL real
para locks, transacciones, scope e invariantes de persistencia.

## Reglas transversales

- Prohibido en todas las fases: modificar Ventas o la emisión de facturas, `Payment`, `SalesReceivable`, la infraestructura
  FROZEN (auditoría, tracking, secuencias, mensajes), Spa, Órdenes de Pedido, integraciones tributarias, multimoneda,
  vencimientos, aprobaciones, adjuntos o backfills; regenerar el Dashboard sin autorización.
- Esquema: baseline inicial única, sin migraciones incrementales (TA-11, resuelta); ninguna eliminación ni cambio de bases de
  datos de trabajo sin autorización.
- Las rutas «nuevas» son propuestas; la lista definitiva se fija al autorizar cada fase. Una ruta no listada requiere
  autorización antes de tocarla.
- Clase nueva en `ERP.Infrastructure.Tests` → asignarla en `tools/ci/backend-test-groups.json` en la misma fase.
- SSOT, código y pruebas se actualizan en la misma fase. Antes de CA-F6 ningún perfil no administrador puede recibir los
  permisos nuevos y **no se habilita operación real antes del cierre de CA-F6**.
- Gates técnicos por fase: pruebas focales (`dotnet test --filter`), guardrails de [enforcement](../architecture/enforcement.md#tests-pre-merge)
  y `git diff --check`.

Abreviaturas de rutas: `D` = `backend/src/ERP.Domain`, `A` = `backend/src/ERP.Application`, `I` = `backend/src/ERP.Infrastructure`,
`API` = `backend/src/ERP.API`, `FE` = `frontend/src`.

## Secuencia

| Fase | Entrega | Depende de |
|---|---|---|
| CA-F0 | ADR y plan (documental) | — |
| CA-F1 | Dominio, persistencia y auditoría | ADR aprobado |
| CA-F2 | Hechos contables y completitud | F1 |
| CA-F3 | Registro, consultas, Caja y permisos | F2 |
| CA-F4 | Aplicación a CxC y su reversa | F3 |
| CA-F5 | Devolución, reversas y anulación | F4 |
| CA-F6 | Frontend, menú y cierre | F5 |

La contabilidad (F2) precede a cualquier operación expuesta para que ningún movimiento nazca sin obligación.

## Componentes protegidos (extensiones estrictamente necesarias; ninguna autorizada)

| Área | Ruta | Fase | Justificación |
|---|---|---|---|
| Persistencia | `I/Migrations/20261101012214_InitialProductionBaseline.cs`, `…Baseline.Designer.cs`, `I/Migrations/ErpDbContextModelSnapshot.cs` | F1 | Cuatro tablas, índices y `CHECK` |
| Persistencia | `I/Persistence/ErpDbContext.cs`, `I/DependencyInjection.cs` | F1 | `DbSet` y repositorios |
| Contabilidad | `A/Modules/Accounting/Posting/PostingFactCatalog.cs` | F2 | Tres hechos |
| Contabilidad | `I/Seeding/Steps/AccountingBootstrapStep.cs` | F2 | Tres reglas para empresas nuevas |
| Contabilidad | `A/Modules/Accounting/Posting/Translators/CustomerAdvance*PostingTranslator.cs` (nuevos) | F2 | Obligaciones y reversos |
| Contabilidad | `I/Accounting/Repositories/AccountingRelevantFactsReader.cs`, `A/Modules/Accounting/Continuity/IAccountingRelevantFactsReader.cs` (documentación del contrato) | F2 | Completitud y vigencia |
| Contabilidad (UI) | `FE/modules/accounting/labels/accountingLabels.ts`, `FE/modules/accounting/pages/postingRules/postingRuleView.ts`, `…/postingRulesI18n.test.ts`, `FE/i18n/locales/es.json`, `en.json` | F2 | Nombres de los hechos y de la cuenta que aporta el origen |
| Contabilidad (SSOT) | `docs/architecture/accounting.md` | F2 | Hechos, identidades y completitud |
| Kernel | `D/Kernel/Permissions/FinancePermissions.cs` | F3 | Cuatro permisos |
| Caja | `D/Modules/Caja/Enums/CashReferenceType.cs` | F3 | `CustomerAdvance = 6`, sin renumerar ni cambiar esquema |
| Finance compartido | `A/Modules/Finance/PaymentMethodDestinationPolicy.cs` | F3 | Su mensaje de crédito dice «pagar a un proveedor»; generalizar solo el texto |
| Orden de locks | `docs/architecture/backend.md` | F3, F4, F5 | Anticipo y turno (F3), anticipo antes de CxC (F4), salidas sin sobregiro (F5); idempotencia |
| Cobros | `A/Modules/Finance/UseCases/Payments/PaymentUseCases.cs` | F4 | Usar la regla compartida de aplicación (TA-12), sin cambio de comportamiento |
| CI | `tools/ci/backend-test-groups.json` | F1–F5 | Manifiesto de pruebas |
| Kernel | `D/Kernel/Modules/CustomersModule.cs` | F6 | `[NavItem]` y permisos relacionados; visibilidad del grupo CxC con `finance.view` |

Sin cambios: `Payment`, `SalesReceivable` y sus cuotas, `CashSession`, `CashMovement`, Posting Engine, reverso común,
infraestructura de auditoría, Identity y Access.

## CA-F0 — Documentación

**Alcance:** ADR-052, este plan e índices. **Aceptación:** guardrail documental, enlaces y `git diff --check`; cero código.
**Gate (cumplido el 2026-11-26):** aprobación conjunta de TA-01 a TA-13 ([ADR §14](../decisions/ADR-052-customer-advances-v1-architecture.md#14-decisiones-técnicas)) y de CA-F1.

## CA-F1 — Dominio, persistencia y auditoría

**Estado (2026-11-26): CLOSED** (commit `1b37c5d72`, CI #1249 SUCCESS). Resultado, evidencia y límites en [STATUS](../../STATUS.md). El gate
agregó en `ErpDbContext` (archivo 18) el rechazo de bajas y reescrituras del historial de anticipos.

- **Objetivo:** modelo de ADR-052 §4 con sus invariantes de dominio y de base de datos.
- **Alcance:** agregado, movimientos, transacción de devolución, eventos, repositorios y Entity Audit. Sin casos de uso,
  endpoints, permisos, Caja, Contabilidad, `backend.md` ni frontend.
- **Esquema:** `customer_advances`, `customer_advance_movements`, `customer_advance_refund_transactions` y
  `customer_advance_audit` con las restricciones de ADR-052 §4, dentro de la baseline única (TA-11).
- **Invariantes:** CA-INV-01, 02, 03 y 04.

**Inventario exacto** (rutas relativas a `backend/src/`, salvo indicación):

| # | Archivo | Tipo |
|---|---|---|
| 1 | `ERP.Domain/Modules/Finance/Entities/CustomerAdvance.cs` | Nuevo |
| 2 | `ERP.Domain/Modules/Finance/Entities/CustomerAdvanceMovement.cs` | Nuevo |
| 3 | `ERP.Domain/Modules/Finance/Entities/CustomerAdvanceRefundTransaction.cs` | Nuevo |
| 4 | `ERP.Domain/Modules/Finance/Entities/CustomerAdvanceAudit.cs` | Nuevo |
| 5 | `ERP.Domain/Modules/Finance/Enums/CustomerAdvanceStatus.cs` | Nuevo |
| 6 | `ERP.Domain/Modules/Finance/Enums/CustomerAdvanceMovementType.cs` | Nuevo |
| 7 | `ERP.Domain/Modules/Finance/Enums/CustomerAdvanceRefundTransactionType.cs` | Nuevo |
| 8 | `ERP.Domain/Modules/Finance/Events/CustomerAdvanceEvents.cs` | Nuevo |
| 9 | `ERP.Domain/Modules/Finance/Interfaces/ICustomerAdvanceRepository.cs` | Nuevo |
| 10 | `ERP.Domain/Modules/Finance/Interfaces/ICustomerAdvanceRefundTransactionRepository.cs` | Nuevo |
| 11 | `ERP.Application/Modules/Finance/EventHandlers/CustomerAdvanceAuditHandler.cs` | Nuevo |
| 12 | `ERP.Infrastructure/Persistence/Configurations/Finance/CustomerAdvanceConfiguration.cs` | Nuevo |
| 13 | `ERP.Infrastructure/Persistence/Configurations/Finance/CustomerAdvanceMovementConfiguration.cs` | Nuevo |
| 14 | `ERP.Infrastructure/Persistence/Configurations/Finance/CustomerAdvanceRefundTransactionConfiguration.cs` | Nuevo |
| 15 | `ERP.Infrastructure/Persistence/Configurations/Finance/CustomerAdvanceAuditConfiguration.cs` | Nuevo |
| 16 | `ERP.Infrastructure/Persistence/Repositories/Finance/CustomerAdvanceRepository.cs` | Nuevo |
| 17 | `ERP.Infrastructure/Persistence/Repositories/Finance/CustomerAdvanceRefundTransactionRepository.cs` | Nuevo |
| 18 | `ERP.Infrastructure/Persistence/ErpDbContext.cs` | **Protegido** (`DbSet`) |
| 19 | `ERP.Infrastructure/DependencyInjection.cs` | **Protegido** (dos repositorios) |
| 20 | `ERP.Infrastructure/Migrations/20261101012214_InitialProductionBaseline.cs` | **Protegido** |
| 21 | `ERP.Infrastructure/Migrations/20261101012214_InitialProductionBaseline.Designer.cs` | **Protegido** |
| 22 | `ERP.Infrastructure/Migrations/ErpDbContextModelSnapshot.cs` | **Protegido** |
| 23 | `ERP.Domain.Tests/Finance/CustomerAdvanceTests.cs` | Prueba nueva |
| 24 | `ERP.Application.Tests/Finance/CustomerAdvanceAuditHandlerTests.cs` | Prueba nueva |
| 25 | `ERP.Infrastructure.Tests/Finance/CustomerAdvancePersistencePostgreSqlTests.cs` | Prueba nueva |
| 26 | `tools/ci/backend-test-groups.json` (raíz) | **Protegido** (CI: registrar la clase 25) |
| 27 | `STATUS.md` (raíz) | Estado de la fase |

Los archivos 20 a 22 se editan sobre la baseline vigente, sin `migrations add`. La SSOT del módulo nace en CA-F3, con el
primer comportamiento expuesto. Un archivo fuera de esta lista requiere autorización antes de tocarlo.

**Pruebas:**

| Suite | Casos |
|---|---|
| Dominio (23) | Monto > 0 y exactamente un destino; nace `Confirmed`, disponible = original y un `Received`; aplicación multilínea numerada `1..N` y ≤ disponible; reversa de la operación completa una sola vez; devolución ≤ disponible y su reversa única por igual monto; anulación y reversa del ingreso solo con el anticipo íntegro; `Cancelled` terminal; sucursal inmutable; evento por operación; motivo y referencia obligatorios en compensaciones |
| Auditoría (24) | Cada evento genera su registro con actor, instante, acción y datos del movimiento |
| PostgreSQL real (25) | BD vacía migra desde la baseline; `CHECK` (disponible, montos, un destino, CxC y referencia de reversa según tipo); únicos (clave + línea, reversa por movimiento, un `Received`, una terminación); FK `Restrict` e `installment_id` sin FK; `GetByIdForUpdateAsync` serializa y no devuelve filas de otra empresa o tenant; `xmin`; filtros de tenant y empresa; auditoría persistida |
| Regresión | `ERP.Architecture.Tests` completo (aislamiento, capas, SQL crudo) y compilación de la solución |

**Gate:** `dotnet test` de Domain, Application e Infrastructure con filtro `CustomerAdvance`, Architecture sin filtro,
`check-architecture-guardrails.ps1 -SkipFrontendChunk`, `npm run architecture:check` y `git diff --check`.

**Aceptación:** pruebas verdes con PostgreSQL desechable; sin cambios de comportamiento existente; ninguna BD de trabajo
modificada.

**Autorización necesaria:** CA-F1 con sus 27 archivos. Recrear una BD de desarrollo es una operación aparte y expresa.

## CA-F2 — Hechos contables y completitud

**Estado: CLOSED** (commit `97b3eccdb`, CI #1250 SUCCESS). Resultado y evidencia en
[STATUS](../../STATUS.md); contrato en [Accounting §28](../architecture/accounting.md#28-anticipos-de-clientes-customer-advances-v1-ca-f2).
Traductores en un solo archivo (`CustomerAdvancePostingTranslators.cs`). `postingRuleView.ts` no cambió: sus textos solo
aplican a lados dinámicos y estos hechos sustituyen una línea fija. La sustitución en el Haber quedó verificada. Pruebas
ajustadas fuera de la lista de rutas: `CustomerAdvancePersistencePostgreSqlTests.cs` y `OpeningBalancePostingSetupTests.cs`.

- **Objetivo:** ADR-052 §10.
- **Alcance:** catálogo, reglas, traductores (tres hechos y reversas por espejo), completitud, etiquetas de la pantalla de reglas.
- **Rutas:** las de Contabilidad de la tabla; pruebas `ERP.Infrastructure.Tests/Seeding/AccountingBootstrapStepTests.cs`
  (el conteo de reglas sube en 3), `ERP.Application.Tests/Accounting/` y nuevas en `ERP.Infrastructure.Tests/Accounting/`.
- **Esquema:** ninguno. **Invariantes:** CA-INV-08, 10.
- **Pruebas:** reglas sembradas y su forma; sustitución del destino en Debe y en Haber (NO VERIFICADO en Haber); obligación única
  por movimiento; N líneas → N obligaciones; regla o cuenta ausente → `Failed` reprocesable; reverso espejo y obligación
  pendiente cancelada; completitud y vigencia detectan orígenes de anticipo.
- **Aceptación:** sin asiento duplicado; sin cambios en hechos existentes.
- **Autorización necesaria:** fase y extensión de Contabilidad.

## CA-F3 — Registro, consultas, Caja y permisos

**Estado (2026-11-27): IMPLEMENTADO Y ACEPTADO LOCALMENTE.** Resultado en [STATUS](../../STATUS.md); contrato en
la [SSOT](../architecture/customer-advances.md). Resuelto en la fase: cliente activo = socio del tenant activo con rol Cliente
activo; paginación `page`/`pageSize` (1–100). `GET /by-receivable/{id}` pasa a CA-F4. Rutas ajustadas fuera de la lista:
repositorio del anticipo y su interfaz (consultas), inventario de `OpenApiContractTests.cs` y estado en `accounting.md`.

- **Objetivo:** registrar un anticipo y consultar saldo e historial.
- **Medios:** solo efectivo y transferencia bancaria (TA-02).
- **Referencias que la base no garantiza (validar aquí con las reglas vigentes, sin duplicarlas):** cliente activo de la
  empresa; medio activo, TA-02 y `RequiresReference`; cuenta bancaria o caja activa y de la empresa (cuenta contable
  `FOR SHARE`, como el reembolso de proveedor); moneda de la empresa; sucursal activa validada por el guard; turno abierto de
  esa caja, bloqueado y del usuario.
- **Reintento (sobre la garantía física de CA-F1):** replay por tenant + empresa + clave y huella (original o 409); traducir
  la violación de los índices de clave a conflicto, también cuando la clave pertenece a otra empresa del tenant.
- **Verificaciones previas (en la fase):** validación oficial de cliente activo de la empresa; convención de paginación.
- **Rutas nuevas:** `A/Modules/Finance/UseCases/CustomerAdvances/**`, DTOs, `API/Controllers/CustomerAdvanceController.cs`,
  `docs/architecture/customer-advances.md` (SSOT inicial) y su entrada en `docs/architecture/README.md`; pruebas en
  `ERP.API.Tests/Integration/` y `ERP.Application.Tests/Finance/`. Si el controller declara `AppFeature`:
  `backend/src/ERP.API.Tests/Snapshots/app-features.txt`.
- **Protegidos:** permisos de Kernel, `CashReferenceType`, política medio↔destino, `backend.md`, CI.
- **Esquema:** ninguno. **Invariantes:** CA-INV-03, 04, 05, 08, 10.
- **Pruebas:** banco y caja; destino ausente, doble o incoherente; tarjeta, cheque y crédito rechazados; sin turno; turno de otro usuario; caja de otra sucursal;
  cliente y destino de otra empresa o tenant (404); replay y huella distinta (409), secuencial y concurrente; saldo por empresa
  igual desde cualquier sucursal; 403 por permiso; sin `CollectionApplied` y un solo hecho contable.
- **Aceptación:** operación atómica y auditada; `KernelControllerPolicyTests` y `KernelRegistryTests` verdes.
- **Autorización necesaria:** fase, Kernel (permisos), Caja, `backend.md` y política compartida.

## CA-F4 — Aplicación a CxC y reversa

**Estado (2026-11-27): CLOSED** (`23c899297`, CI #1253 SUCCESS). Resultado en [STATUS](../../STATUS.md); contrato en
la [SSOT](../architecture/customer-advances.md). Rutas ajustadas fuera de la lista: repositorio del anticipo y su interfaz
(aplicaciones por CxC), inventario de OpenAPI y snapshot OpenAPI de Companies (tag global del controller).

- **Objetivo:** aplicar parcial o totalmente a una o varias CxC y revertir la operación completa.
- **Cronología ([ADR-052 §17](../decisions/ADR-052-customer-advances-v1-architecture.md), 2026-11-27):** fecha efectiva
  entre las fechas de origen y el día de negocio. Pruebas HTTP + PostgreSQL: límites aceptados; anterior al anticipo,
  factura o apertura y futura rechazadas sin efectos; origen indeterminable; varias CxC con una incompatible; clave
  reutilizable tras rechazo; replay sin reevaluar; regresión de cobros. El registro rechaza además una fecha de anticipo
  futura (efectivo y transferencia, sin efectos y con la clave reutilizable).
- **Rutas:** casos de uso y controller de F3 (incluye `GET /by-receivable/{id}`); regla compartida nueva `A/Modules/Finance/ReceivableApplicationPolicy.cs`;
  `PaymentUseCases.cs`; `backend.md`, `docs/architecture/receivables.md`, SSOT de anticipos.
- **Protegidos:** cobros, `backend.md`, CI. **Esquema:** ninguno. **Invariantes:** CA-INV-01, 03, 05, 06, 07, 10.
- **Referencias que la base no garantiza:** CxC bloqueada, del mismo tenant, empresa (el agregado también lo exige) y
  cliente, no cancelada y con saldo; cuota perteneciente a esa CxC y con saldo suficiente (regla compartida TA-12 y
  `SalesReceivable`); sucursal activa; en la reversa, CxC y cuota tomadas del movimiento original. `installment_id` no tiene
  FK porque las cuotas se reconstruyen o eliminan (`RebuildInstallments`, `Cancel`).
- **Integridad (TA-06):** además de bloquear la reversa incompatible, verificar que no se creen aplicaciones sobre CxC con
  inconsistencias conocidas que impidan su reversa posterior; sin correcciones artificiales de saldo.
- **Pruebas:** casos «Aplicación», «Concurrencia» y «Reversa de aplicación» del
  [diseño §10](../decisions/CUSTOMER-ADVANCES-V1-DESIGN-01.md#10-casos-de-prueba-concurrencia-y-errores); TA-06 con devolución
  de venta anterior y posterior a la aplicación (rechazo sin efectos); regresión de `RegisterCollectionCommandHandlerTests`, `ReverseCollectionCommandHandlerTests`,
  `ReceivableCollectionConcurrencyTests`, `ReceivableCollectionCustomerScopeTests` y `SalesCancelCollectionConcurrencyTests`
  (esta última tiene una intermitencia registrada, CI-1201, en la [cola](WORK_QUEUE.md#b-incidencias-técnicas-y-deuda)).
- **Prueba mixta obligatoria (PostgreSQL):** sobre una CxC de dos cuotas, cobro + anticipo en ambos órdenes, con y sin cuota
  indicada; revertir cada uno por separado. En cada paso `PaidAmount` = cobros vigentes + aplicaciones vigentes (CA-INV-07),
  Σ cobrado de cuotas = `PaidAmount`, nada negativo y la otra operación sigue reversible. La distribución por cuota puede
  cambiar al revertir (comportamiento existente): se comprueban sumas, no qué cuota. La anulación de la factura se rechaza
  mientras haya aplicación vigente.
- **Aceptación:** sin deadlock ni doble consumo; cobros sin cambios de comportamiento.
- **Autorización necesaria:** fase, cambio acotado en cobros y `backend.md`.

## CA-F5 — Devolución, reversa de devolución, reversa del ingreso y anulación

**Estado (2026-11-27): CLOSED, commit `21ad867f6`, CI #1254 SUCCESS.** Resultado en
[STATUS](../../STATUS.md); contrato en la [SSOT](../architecture/customer-advances.md). Rutas ajustadas fuera de la lista: casos de uso existentes (reglas
comunes de dinero y detalle con transacciones), repositorio de transacciones de devolución y su interfaz, inventario y
snapshot de OpenAPI. Sin clases de prueba nuevas (la de API de CA-F3 pasa a parcial).

- **Objetivo:** ADR-052 §6 y diseño §6.2–6.5.
- **Rutas:** casos de uso y controller de anticipos; `backend.md`, `docs/architecture/receivables.md` («Límites vigentes»: la
  devolución de cobros ordinarios sigue fuera) y SSOT.
- **Referencias que la base no garantiza:** medio y destino (TA-02) activos y de la empresa en la devolución; turno abierto
  de esa caja, titular y sin sobregiro en cada salida o compensación; sucursal activa; mismo destino en la reversa de una
  devolución (lo hereda el dominio); ausencia de asiento publicado antes de una anulación administrativa.
- **Protegidos:** `backend.md`, CI. **Esquema:** ninguno. **Invariantes:** CA-INV-01, 02, 03, 08, 09, 10.
- **Pruebas:** casos «6.2», «6.3», «Devolución y su reversa», «Contabilidad» y «Seguridad» del diseño §10; sin sobregiro y
  titularidad en cada salida; devolución concurrente contra aplicación; período cerrado bloquea la reversa; anulación con
  asiento publicado rechazada sin efectos.
- **Aceptación:** ningún saldo ficticio; caja, saldo y contabilidad cuadran tras cada operación.
- **Autorización necesaria:** fase y `backend.md`.

## CA-F6 — Frontend, menú y cierre

**Estado (2026-11-27): ACEPTADO por el propietario en el gate definitivo, pendiente de push, CI SUCCESS y cierre
formal.** Resultado y prueba operativa en [STATUS](../../STATUS.md); contrato en la
[SSOT](../architecture/customer-advances.md#11-pantallas). Rutas ajustadas fuera de la lista: rutas en
`customerAdvanceRoutes.tsx`, facade de sucursales de sesión, etiquetas de dos permisos en el catálogo de asignación y la
expectativa de navegación del Kernel. Ajustes autorizados: filtro opcional `customerId` en la consulta de CxC (query,
handler, repositorio, controller y SSOT), consolidación visual de CxC, terminología «Monto» en los textos visibles de
todo el ERP y traslado a `FE/hooks/` del hook de retorno de foco. Registro funcional y Dashboard autorizados en el gate.

- **Objetivo:** ADR-052 §13. **Auditoría de reutilización de UI** declarada antes de escribir.
- **Rutas nuevas:** `FE/modules/finance/pages/CustomerAdvance*Page.tsx`, `components/*CustomerAdvance*Modal.tsx`,
  `api/customerAdvanceService.ts`, `schemas/customerAdvanceSchema.ts` y sus pruebas.
- **Rutas existentes:** `FE/modules/finance/pages/AccountsReceivablePage.tsx`, `FE/routes/catalogRoutes.tsx`,
  `FE/i18n/locales/es.json`, `en.json`, `CustomersModule.cs`, `docs/navigation/MENU_MAP.md`, `FEATURES.md`, SSOT.
- **Protegidos:** Kernel (`CustomersModule.cs`). **Esquema:** ninguno. **Invariantes:** CA-INV-05.
- **Pruebas:** asignación de los cuatro permisos desde la administración de perfiles; modales, confirmación, errores de servidor en campo, permisos, doble clic con un solo envío; `npm run lint`,
  `npx tsc -b`, `npm run build`, `npm run architecture:check`.
- **Aceptación:** verificación y aceptación visual y funcional del propietario; reglas contables configuradas en la empresa
  donde se pruebe.
- **Autorización necesaria:** fase y Kernel (menú); autorización aparte para el registro funcional y el Dashboard.

## Condiciones de cierre

Todas las fases cerradas; casos del diseño §10 y prueba mixta verdes; SSOT sincronizada; ningún componente protegido modificado
fuera de lo autorizado. Anticipos **no se contabiliza como funcionalidad implementada** hasta el cierre con gates; hasta
entonces el control funcional V1 no cambia.

# PEDIDOS-VENTA-V1 — Plan de implementación por fases

**Estado: ADR-049 ACCEPTED (aprobación formal del propietario, 17/11/2026) — diseño aprobado, NO implementado. F0 PENDING / NO AUTORIZADA; ninguna fase está autorizada para programación.**
Documento de planificación, no normativa ni segunda SSOT. Arquitectura y justificación:
[ADR-049](../decisions/ADR-049-sales-order-v1-architecture.md). Comportamiento propuesto:
[especificación funcional](../decisions/PEDIDOS-VENTA-V1-FUNCTIONAL-SPEC-01.md) (incluye la matriz de trazabilidad,
Anexo A, y las pruebas, §19). Estado de entrega: [STATUS](../../STATUS.md). Cola:
[WORK_QUEUE](WORK_QUEUE.md) — registra `PEDIDOS-VENTA-V1-F0` como PENDING / NO AUTORIZADA; este plan **no** autoriza trabajo por existir.

**Toda implementación posterior requiere autorización expresa del propietario, fase por fase.** La aprobación de las
decisiones DEC-01 a DEC-06 **no** es autorización para programar. Una incompatibilidad no autoriza al agente a inventar
una solución ni a ampliar la fase.

## 0. Estado de las decisiones y verificaciones

Se distinguen tres cosas: la **decisión aprobada** (comportamiento ya definido por el propietario), el **contrato técnico
pendiente** (algo que se comprueba en código o infraestructura; **no** es una nueva decisión comercial) y el **bloqueo de
implementación** (una fase que todavía no puede ejecutarse con seguridad).

| ID | Estado | Resumen |
|---|---|---|
| DEC-01 | **APROBADA — opción A** | Extensiones mínimas controladas de Ventas e Inventario; sin refactor general; autorización y lista exacta de archivos por fase; sin regresiones |
| DEC-02 | **APROBADA — opción A** | Reservas = existencias físicas reales; protegidas frente a operaciones ajenas; **existencia física comprometida** protegida (`Quantity_after ≥ ReservedQuantity` en el punto central, bajo lock); ADR-041 intacto; consumo propio como excepción; cuatro casos (contrato técnico: VER-13) |
| DEC-03 | **APROBADA — opción B** | Vigencia por empresa en días calendario, calculada al confirmar (contrato técnico: VER-11; sin configurar: Confirmar se rechaza, **OPS-01 resuelta**) |
| DEC-04 | **APROBADA — opción A** | Liberación manual de la selección; vínculo `Released` irreversible; borrador no autorizable |
| DEC-05 | **APROBADA — A1 + B1 + C1** | Anulación devuelve saldo en Confirmado; Cerrado se conserva; incidencia auditada y regularización con evidencia real |
| DEC-06 | **APROBADA — opción B (expresa)** | **Una sola migración inicial consolidada** (§3.1) |
| OPS-01 | **APROBADA / RESUELTA** | Sin vigencia configurada: se permite crear y editar el Borrador; **Confirmar se rechaza** sin efectos y con mensaje a Preferencias operativas; sin duración predeterminada ni fecha manual; VER-11 sigue pendiente |
| VER-01..VER-14 | Pendientes (evidencia) | Definidas en la especificación, Anexo B.5 |
| TP-01..TP-20 | Propuestas | Sin aprobación; TP-05 y TP-16 además exigen evidencia |
| AMB-01..AMB-11 | Interpretaciones provisionales | No son decisiones definitivas |

## 1. Reglas del agente en la futura implementación (`PEDIDOS-AGENT-GUARD-01`)

1. Ejecutar únicamente la fase expresamente autorizada.
2. Verificar Git (rama, árbol limpio) y los contratos citados antes de modificar.
3. Respetar la **lista exacta de rutas aprobada** de la fase (§3); todo lo demás es protegido.
4. Prohibidas las funcionalidades adicionales.
5. Prohibidos los refactors oportunistas.
6. Respetar el ownership por dominio.
7. Proteger las reglas oficiales de Inventario y Facturación.
8. No modificar infraestructura congelada sin autorización específica (DEC-01: extensiones mínimas por fase; DEC-06).
9. Ejecutar las pruebas y guardrails de la fase (focalizadas; nunca la suite completa).
10. No hacer commit ni push sin autorización; el propietario realiza staging y commit.
11. Informar cualquier desviación **antes** de modificar.
12. No declarar COMPLETADO/CLOSED sin la evidencia exigida y el gate del propietario.

## 2. Mapa de fases

Ninguna fase está autorizada. «Bloqueos» son las verificaciones, definiciones operativas y listas de rutas que deben
resolverse **antes** de pedir la autorización de esa fase.

| Fase | Objetivo | Requisitos que cubre | Depende de | Bloqueos | Autorización |
|---|---|---|---|---|---|
| **F0** | Verificaciones y autorizaciones (solo lectura/documental; no es programación) | VER-01..VER-14; demostración de TP-16 | ADR-049 Accepted | — | **PENDING / NO AUTORIZADA** (requiere autorización expresa) |
| **F1** | Pedido comercial: borrador, edición, cancelación de borrador, listado, detalle, historial, permisos, menú. **Sin «Confirmar»** | PV-COM-01, PV-NAV-01, PV-FUT-01; parte de PV-SEC-01, PV-EDI-01, PV-CAN-01, PV-EST-01 | F0, ADR aprobado | VER-01, VER-07, VER-14; lista exacta | No autorizada |
| **F2** | Inventario: reservas y protección de egresos (cuatro casos) | PV-RES-05; base de PV-RES-01..04 | F0 | VER-10, VER-13, VER-14; lista exacta | No autorizada |
| **F3** | Reservas del pedido: confirmar, completar, liberar, extender, vencimiento por empresa; edición y cancelación sin facturación | PV-RES-01..04, PV-EDI-01, PV-CAN-01, PV-EST-01 (Confirmado) | F1, F2 | VER-11; lista exacta | No autorizada |
| **F4** | Preparación y reconciliación previa | PV-PREP-01 | F3 | Lista exacta | No autorizada |
| **F5** | Selección de facturación, liberación de selección, borrador desde pedido y bloqueo comercial | PV-FAC-01, PV-FAC-02, PV-FAC-03, PV-FAC-05; PV-EDI-01 (selección activa) | F3 | VER-03, VER-04, VER-13 | **NO AUTORIZABLE** (lista de rutas no cerrada) |
| **F6** | Consumo atómico en la autorización, guard de vínculo y pruebas de concurrencia | PV-FAC-04, PV-FAC-05 (guard) | F2, F5 | VER-05, VER-06, VER-08, VER-13; elección T1/T2 con evidencia | **NO AUTORIZABLE** (lista de rutas no cerrada) |
| **F7** | Entregas y habilitación fiscal | PV-ENT-01 | F6 | VER-02 | No autorizada |
| **F8** | Reconciliación, indicadores, incidencias, regularización M1 y cierre | PV-REC-01, PV-EST-01 (Cerrado) | F7 | VER-12 (las modalidades M2–M4 permanecen bloqueadas) | No autorizada |
| **F9** | Cierre documental (propuesta separada) | Todas | F1..F8 | — | No autorizada |

## 3. Regla de archivos: lista exacta aprobada

Cada fase distingue **cuatro** categorías de archivos. Los patrones amplios (`Sales/**`, `SalesOrder*.cs`, `frontend/…/orders/**`)
**no constituyen autorización** para modificar ningún archivo.

| Categoría | Significado |
|---|---|
| **Existentes autorizables** | Archivos que hoy existen y que la fase podría modificar, solo tras aprobación expresa de la ruta |
| **Nuevos previstos** | Archivos que no existen y se crearían; se citan por directorio y propósito; los nombres exactos se fijan en la lista de la fase |
| **Protegidos** | No se modifican; un cambio requiere autorización específica y suspende la fase |
| **Candidatos pendientes de verificación** | Posibles archivos cuyo uso depende de una verificación (VER) o definición (OPS); **no** están autorizados |

**Antes de implementar una fase se exige una lista exacta de rutas** (existentes, nuevas, protegidas, candidatas),
**revisada y aprobada por el propietario**. **DEC-01 no es una reapertura ilimitada:** autoriza solo extensiones mínimas,
fase por fase; cualquier archivo adicional exige aprobación independiente. F5 y F6 permanecen NO AUTORIZABLES mientras sus
candidatos no se conviertan en una lista cerrada.

### 3.1 Estrategia de esquema: baseline única (DEC-06)

El propietario exige **una sola migración inicial consolidada**; el ERP aún no está en producción. **Ninguna fase deja una
migración incremental permanente**; donde antes se decía «una migración» por fase, aplica esta estrategia
([DATABASE.md § Migraciones](../DATABASE.md)). Cada fase con cambios de esquema distingue:

| Paso | Contenido |
|---|---|
| **1. Cambios de modelo y configuraciones EF** | Entidades y `IEntityTypeConfiguration` previstos en la fase (tablas, relaciones, índices, restricciones). No se crea una migración nueva |
| **2. Verificación del esquema** | Se materializa en una base **PostgreSQL desechable** y se comprueba (restricciones, unicidad, `ClientRequestId`, escalas decimales, alcance tenant/empresa/sucursal). Nunca en bases del propietario |
| **3. Consolidación en la baseline única** | El esquema del pedido se integra en `20261101012214_InitialProductionBaseline` (`.cs`, `.Designer.cs`) y `ErpDbContextModelSnapshot.cs`; las migraciones parciales provisionales no sobreviven |
| **4. Condiciones para autorizar la regeneración** | Lista exacta que incluye esos tres archivos; **autorización específica del propietario para esa fase**; VER-14 resuelto (procedimiento que conserva nombre y objetos); fase probada; sin cambios funcionales ajenos |
| **5. Evidencias obligatorias de integridad** | Comparación del catálogo PostgreSQL antes/después (tablas, columnas, restricciones, índices, triggers, funciones y extensiones) con **solo** las diferencias previstas; `RawSqlDatabaseObjectsSurviveMigrationSquashTests` y `RawSqlDatabaseObjectsBaselineIntegrationTests` en PASS; todos los objetos raw SQL oficiales preservados; una sola migración en la cadena |

**Consecuencias y límites:** no existe upgrade-path; una base creada con la cadena anterior falla en `MigrateAsync` y se
recrea **solo con autorización del propietario**. **No se inventa una actualización automática de bases existentes**, no
se aplican cambios a una base real sin aprobación específica y no se ejecutan operaciones destructivas sobre las bases del
propietario. Mostrar servidor, base y entorno efectivos antes de cualquier operación sobre una base.

## 4. Distribución de pruebas por fase

Cada prueba se ejecuta en la **primera fase que dispone de todos sus componentes**; ninguna fase exige una prueba que
dependa de un componente de una fase posterior. Los escenarios `ESC` son aceptación integrada (especificación §18);
las pruebas `AT` están definidas en el §19 de la especificación.

| Fase | Pruebas `AT` que se ejecutan | Pasos de escenario |
|---|---|---|
| F1 | AT-COM-01..03, AT-SEC-01a..c, AT-NAV-01, AT-FUT-01, AT-EST-01a | — |
| F2 | AT-RES-05a..i (05f y 05i según VER-13), AT-CON-06 | ESC-14.1–14.6, 14.8–14.12, 14.14–14.15 (reserva de origen sintético; el pedido aún no existe) |
| F3 | AT-RES-01a..g (01g: rechazo de Confirmar sin vigencia, OPS-01), 02a..c, 03a..d, 04a..d, AT-EDI-01a,c,d,e, AT-CAN-01a,d, AT-EST-01d, AT-CON-01, AT-SEC-01a (operaciones de F3) | ESC-01, ESC-02, ESC-13 (incl. 13.0), ESC-15.0 |
| F4 | AT-PREP-01a..d, AT-RES-04e, AT-EDI-01f, AT-CAN-01b | ESC-03.1 |
| F5 | AT-FAC-01a..g, 02a..d, 03a, 03d, 05a, 05c, 05d, 05e, AT-RES-04f, AT-EDI-01b, AT-CAN-01e | ESC-03.2, ESC-08.1–8.3, ESC-09.1–9.4, ESC-15.1 |
| F6 | AT-FAC-03b, 03c, 04a..i, 05b, AT-RES-04g, AT-EDI-01g, AT-CAN-01c, AT-CON-02, 03, 04, 05, 07 | ESC-03 (integral), ESC-07, ESC-08, ESC-09 (integral), ESC-12.1, ESC-14.7, ESC-14.13, ESC-15 (integral) |
| F7 | AT-ENT-01a..f, AT-EST-01c | ESC-04, ESC-05 (incl. 5b), ESC-06.1–6.2, ESC-12.2–12.3a |
| F8 | AT-REC-01a..i, AT-EST-01b | ESC-06 (integral), ESC-10, ESC-11, ESC-12 (integral), ESC-16, ESC-17 |

**Pruebas diferidas** (se definen desde una fase pero requieren un componente posterior): AT-RES-04e → F4, 04f → F5, 04g →
F6; AT-EDI-01f → F4, 01b → F5, 01g → F6; AT-CAN-01b → F4, 01e → F5, 01c → F6; AT-FAC-03b/03c y AT-FAC-05b → F6;
AT-CON-02..05 y 07 → F6; ESC-05 y ESC-06 integrales → F7/F8. Ninguna es condición de cierre de una fase anterior.

**Concurrencia (DEC-02, TP-05, TP-16):** la ausencia de deadlocks, de doble autorización, de doble consumo, el rollback
correcto, la protección de ventas directas y la compatibilidad con la política de stock negativo se demuestran con
**AT-CON-01 a AT-CON-07** en PostgreSQL real. **No se elige T1 ni T2** sin esa evidencia.

## 5. Guardrails, evidencia y rollback comunes

**Guardrails de toda fase con código** (los aplicables al tipo de cambio; resultados completos en el informe):
`cd frontend && npx tsc -b` (nunca `tsc --noEmit` en la raíz), lint y `npm run build`; `npm run architecture:check`
(incluye i18n); `tools/architecture/check-architecture-guardrails.ps1 -SkipFrontendChunk` (el PR-7 no corre dentro de
`architecture:check`); `dotnet build` (configuración Release si hay una API en ejecución) y pruebas **focalizadas** con
filtro; PostgreSQL real para concurrencia, transacciones, scope, locks e invariantes de persistencia (nunca SQLite,
in-memory ni mocks; base desechable, nunca la del propietario); regla C-03 (handler `Handle` ≤ 150 líneas, ADR-046); toda
clase nueva en `ERP.Infrastructure.Tests` debe asignarse en `tools/ci/backend-test-groups.json` o el CI falla;
`git diff --check`, **más** una comprobación de espacios y fin de línea sobre los archivos nuevos no rastreados (el
`git diff --check` no los inspecciona). **Prohibido** ejecutar la suite completa; la corre el propietario en su gate. No
dejar `ERP.API.exe` en ejecución al terminar la fase.

**Nota sobre el guardrail documental:** `node tools/architecture/run-all.mjs --only documentation` **escribe**
`tools/architecture/architecture-report.json` (reporte JSON) y su alcance son las entradas de agentes y
`docs/architecture/*`. Ejecutarlo modifica un archivo; solo se corre con autorización y revisando el diff del reporte.

**Evidencia común exigida:** (1) `git diff --name-only` igual a la lista exacta aprobada de la fase (cero archivos
protegidos); (2) comandos y resultados focalizados (nombre, conteo, PASS/FAIL); (3) guardrails PASS; (4) auditoría de
reutilización UI previa si hay frontend (plantillas revisadas, componentes reutilizados, justificación de lo nuevo);
(5) `git diff --check` y `git status --short`; (6) lista de desviaciones y riesgos residuales; (7) confirmación de que no
hubo commit ni push; (8) matriz de trazabilidad actualizada de la fase; (9) si hay esquema, las evidencias del §3.1.

**Rollback común:** revertir el conjunto exacto de archivos de la fase (incluida la baseline regenerada, desde Git);
**ninguna operación destructiva sobre datos del propietario** y ningún cambio de esquema se aplica a una base existente
sin su confirmación expresa. Facturación directa queda intacta en cualquier punto de reversión.

**Condición común para autorizar la fase siguiente:** gate del propietario `PASS`, revisión final del diff, staging y
commit del propietario, evidencia completa, verificaciones/definiciones de la fase siguiente resueltas y lista exacta de
rutas aprobada.

---

## 6. Fase 0 — Verificaciones y autorizaciones

| Campo | Contenido |
|---|---|
| Objetivo | Cerrar con evidencia VER-01..VER-14; **demostrar** la compatibilidad de TP-16/TP-05 (T1/T2) con autorización, secuenciales, stock y reintentos; fijar listas exactas de rutas (en especial F5 y F6) |
| Dependencias | ADR-049 y documentos hermanos revisados por el propietario |
| Requisitos | Todos (habilitación) |
| Archivos existentes autorizables | Ninguno |
| Archivos nuevos previstos | Ninguno (informe de hallazgos en el chat) |
| Archivos protegidos | Todo el repositorio |
| Archivos candidatos pendientes | Cualquier actualización de `STATUS.md`, `WORK_QUEUE.md`, `FEATURES.md`, `ROADMAP.md`, `docs/decisions/README.md` o `docs/navigation/MENU_MAP.md` es una propuesta aparte |
| Operaciones/contratos que cambian | Ninguno |
| Esquema y baseline | Sin cambios; VER-14 define el procedimiento de regeneración |
| Pruebas | Ninguna (lectura). Diseña, sin ejecutar, AT-CON-01..07; si VER-06/VER-08 exigen reproducir, se pide autorización específica |
| Guardrails | `git diff --check` sobre cualquier documento autorizado |
| Riesgos | Que una verificación desmienta una propuesta ⇒ el ADR se corrige antes de F1 |
| Rollback | No aplica |
| Criterios de aceptación | Cada VER resuelta o marcada bloqueo; compatibilidad de locks demostrada o fase marcada no autorizable; F5/F6 con lista exacta o declaradas no autorizables |
| Evidencia | Informe con archivo:línea por VER; listas de rutas por fase |
| Siguiente fase | El propietario aprueba formalmente el ADR y autoriza F1 con su lista exacta |

Qué comprueba cada VER: **VER-01** numeración interna y API pública de secuencias FROZEN · **VER-02** ausencia de flujo
de anulación SRI para Ventas y suficiencia de `ElectronicDocument.CurrentState` · **VER-03** validación de `UnitPrice`
en `CreateSalesDraft` · **VER-04** `loadForEdit`/hidratación del frontend · **VER-05** participación de
`CaptureNextAsync` en transacción ambiente · **VER-06** restauración de vínculos y reservas en
`RecoverFromConflictAndRetrackAsync` · **VER-07** `KernelRegistry`/`ui_nav_items` · **VER-08** causa raíz de CI-1201 ·
**VER-09** campos de `CompanyPrecisionPolicy` · **VER-10** alcance del congelamiento de Inventario · **VER-11** mecanismo
de la vigencia por empresa y techo ADR-046 · **VER-12** contratos de las modalidades de regularización M2–M4 · **VER-13**
implementabilidad de los cuatro casos de egreso sin alterar ADR-041 · **VER-14** procedimiento de regeneración de la
baseline única.

## 7. Fase 1 — Pedido comercial (sin confirmar)

| Campo | Contenido |
|---|---|
| Objetivo | Crear, editar, listar, ver y cancelar pedidos en Borrador, con historial, permisos y menú; **sin reservar y sin acción «Confirmar»** (aparece en F3 con su reserva) |
| Dependencias | F0; ADR aprobado |
| Bloqueos | VER-01 (numeración), VER-07 (permisos y menú), VER-14 |
| Requisitos | PV-COM-01, PV-NAV-01, PV-FUT-01; PV-SEC-01 (`view/create/update`); PV-EDI-01 (borrador); PV-CAN-01 (cancelar borrador); PV-EST-01 (Borrador, Cancelado) |
| Archivos existentes autorizables (solo aditivo, ruta por aprobar) | `backend/src/ERP.Domain/Kernel/Modules/SalesModule.cs` (nuevo `[NavItem]`); `backend/src/ERP.Infrastructure/Persistence/ErpDbContext.cs`; `backend/src/ERP.Infrastructure/DependencyInjection.cs`; `frontend/src/routes/catalogRoutes.tsx`; `frontend/src/i18n/locales/es.json` y `en.json`; `tools/ci/backend-test-groups.json`; **baseline (solo con la autorización específica del §3.1):** `backend/src/ERP.Infrastructure/Migrations/20261101012214_InitialProductionBaseline.cs`, `…Designer.cs` y `ErpDbContextModelSnapshot.cs` |
| Archivos nuevos previstos (nombres exactos por fijar) | Domain: entidades `SalesOrder`, `SalesOrderLine`, enum de estado, `ISalesOrderRepository` y eventos de auditoría bajo `ERP.Domain/Modules/Sales/`; `ERP.Domain/Kernel/Permissions/SalesOrderPermissions.cs`; Application: casos de uso y DTOs de pedidos bajo `ERP.Application/Modules/Sales/`; Infrastructure: configuraciones EF y repositorio bajo `…/Persistence/Configurations/Sales/` y `…/Repositories/Sales/`; `ERP.API/Controllers/SalesOrdersController.cs`; frontend bajo `frontend/src/modules/sales/orders/`; pruebas nuevas en `ERP.Domain.Tests`, `ERP.Application.Tests` y `ERP.Infrastructure.Tests` y `*.test.tsx` colocados |
| Archivos protegidos | `SalesInvoice*`, `SalesDraftUseCases.cs`, `AuthorizeSalesUseCases.cs`, `CancelSalesUseCases.cs`, `SalesReturn*`, `SalesPermissions.cs`, `SalesController.cs`, `SalesPage*`/`useSalesPage.ts`; todo `Inventory/`; `ElectronicDocuments`; secuencias, auditoría y configuración tributaria FROZEN; `architecture-grandfather.json` |
| Archivos candidatos pendientes | `ERP.Domain/Kernel/KernelRegistry.cs` (solo si VER-07 lo exige); la pieza de numeración que determine VER-01 |
| Operaciones/contratos | Endpoints de lectura, alta, edición y cancelación de borrador de pedidos; claves propuestas `sales.orders.view/create/update`; ítem de menú |
| Esquema y baseline (§3.1) | Modelo EF: `sales_orders`, `sales_order_lines`, `sales_order_audits`. Verificación en BD desechable y consolidación en la baseline única; **sin migración incremental** |
| Pruebas focalizadas | Las de F1 en §4; aislamiento tenant/empresa/sucursal en PostgreSQL real; inventario de rutas alcanzables; regresión: el menú de Ventas no cambia para usuarios sin el permiso |
| Guardrails | Comunes + `check-i18n-keys` (es/en), `frontend-http-access`, `module-boundaries`, `frontend-subscriber-naming`, gate de columnas decimales, `check-design-system` |
| Riesgos | Duplicar componentes de factura; numeración (VER-01); exponer una operación sin permiso; regeneración de la baseline (R-16) |
| Rollback | Común |
| Criterios de aceptación | Un pedido Borrador se crea, edita y cancela sin Kardex ni reserva; el menú muestra «Gestión de Ventas» solo con permiso; `/sales` y `/sales/returns` idénticos; una sola migración en la cadena; ningún archivo protegido modificado |
| Evidencia | Común + salida de las pruebas de F1, del inventario de rutas y las evidencias del §3.1 |
| Siguiente fase | F2 requiere VER-10, VER-13 y VER-14 resueltos y su lista exacta aprobada |

## 8. Fase 2 — Inventario: reservas y protección de egresos

| Campo | Contenido |
|---|---|
| Objetivo | Sustituir la entidad huérfana `StockReservation` por el libro de reservas, mantener `ReservedQuantity` como proyección verificable y proteger **la existencia física comprometida** en el punto único, bajo lock, con la regla `Quantity_after ≥ ReservedQuantity` y los **cuatro casos** de DEC-02 (consumo propio, egreso libre, venta negativa legítima ADR-041 solo sin reservas comprometidas, intento sobre reserva ajena) |
| Dependencias | F0 |
| Bloqueos | VER-10, VER-13, VER-14 |
| Requisitos | PV-RES-05; base de PV-RES-01..04 |
| Archivos existentes autorizables (extensión mínima bajo DEC-01, ruta por aprobar) | `ERP.Domain/Modules/Inventory/Entities/CurrentStock.cs`; `…/Entities/StockReservation.cs` (reemplazo); `…/Interfaces/IStockRepository.cs`; `ERP.Infrastructure/Persistence/Repositories/Inventory/StockRepository.cs`; `…/Configurations/Inventory/CurrentStockConfiguration.cs`; `ERP.Infrastructure/Persistence/Repositories/WarehouseRepository.cs` (bloqueo de cambio de sucursal con reservas); `ErpDbContext.cs`; `tools/ci/backend-test-groups.json`; baseline (§3.1) |
| Archivos nuevos previstos | `IStockReservationRepository.cs`; `StockReservationRepository.cs`; configuraciones de `stock_reservations` y `stock_reservation_operations`; pruebas nuevas (Domain/Infrastructure) |
| Archivos protegidos | `ExecuteStockAdjustmentCommandHandler.cs`, `AuthorizePurchaseReturnUseCases.cs`, `ConfirmStockTransferCommandHandler.cs` (la regla central rechaza; no se tocan), `SaleStockPolicy.cs`, todo `Sales/`, `ElectronicDocuments`, Compras |
| Archivos candidatos pendientes | Registro de objetos raw SQL y sus pruebas de vigilancia, solo si se decide agregar un trigger de alcance; mensajes de los tres handlers protegidos, solo con autorización específica; puntos de integración de DEC-02 (VER-13): `CurrentStock.ApplyKardexMovement` (línea 75, resultado negativo de la venta autorizada) y el parámetro opcional de consumo propio en `AppendMovementAsync`/`PendingMovement` |
| Operaciones/contratos | Regla de egreso en `ApplyKardexMovement`/`CreateAndTrackMovementAsync` (TP-04, spec §9.5); operaciones reservar/completar/liberar/extender/consumir con `ClientRequestId`; sin cambiar firmas existentes salvo parámetros opcionales con valor por defecto; la comprobación física se hace **dentro** del punto central bajo el lock (no como precheck); **sin cambiar la valoración de ADR-041** |
| Esquema y baseline (§3.1) | Modelo EF: `stock_reservations`, `stock_reservation_operations`; tratamiento de la entidad huérfana; consolidación en la baseline única con los objetos raw SQL preservados |
| Pruebas focalizadas | Las de F2 en §4 (reservas de **origen sintético**); INV-07 y AT-CON-06 en **PostgreSQL real**; regresión obligatoria: `StockRepositoryCompanyScopeIntegrationTests`, `InventoryDocumentConcurrencyPostgreSqlTests`, `NegativeSaleCostA2Tests`, `SaleStockPolicyTests`, `StockAdjustmentExecuteCancelTests`, `InventoryAdjustmentsEndToEndTests`, `PurchaseReturnEndToEndTests`, `WarehouseBranchReassignmentHttpTests`, `ConfirmInitialStockAtomicPostgreSqlTests` |
| Guardrails | Comunes + vigilancia de objetos raw SQL (`RawSqlDatabaseObjectsSurviveMigrationSquashTests`, `RawSqlDatabaseObjectsBaselineIntegrationTests`) |
| Riesgos | R-01, R-02, R-14, R-16; romper ventas sin stock autorizadas cuando no hay reservas (ADR-041); comprobación previa que falle bajo concurrencia; latencia por el lock |
| Rollback | Común; `ReservedQuantity` queda en 0 |
| Criterios de aceptación | Ningún egreso ajeno consume ni libera reservas de otro pedido; sin reservas comprometidas la venta sin stock conserva su comportamiento y su costo (caso C), y con reservas no reduce la existencia por debajo de ellas (casos B y E); los casos A, B, C, E y F pasan; todos los flujos existentes pasan su regresión; sin Kardex nuevo por reservar |
| Evidencia | Común + matriz de egresos (venta, ajuste, transferencia, devolución de compra, anulación de compra, venta sin stock) con resultado antes/después y evidencias del §3.1 |
| Siguiente fase | F3 requiere F1 y VER-11 resuelta |

## 9. Fase 3 — Reservas del pedido (confirmar, completar, liberar, extender)

| Campo | Contenido |
|---|---|
| Objetivo | Confirmar con reserva automática y vencimiento calculado por la configuración de la empresa; completar, liberar y extender manualmente; vencimiento como advertencia; edición y cancelación del pedido confirmado **sin facturación** |
| Dependencias | F1, F2 |
| Bloqueos | VER-11 (OPS-01 está resuelta) |
| Requisitos | PV-RES-01..04; PV-EDI-01; PV-CAN-01; PV-EST-01 (Confirmado); claves propuestas `confirm`, `amend`, `reserve.*`, `cancel` |
| Archivos existentes autorizables (ruta por aprobar) | `frontend/src/i18n/locales/es.json` y `en.json`; `frontend/src/routes/catalogRoutes.tsx` (si hay rutas nuevas); `SalesModule.cs` (acciones relacionadas del `[NavItem]`); `tools/ci/backend-test-groups.json`; baseline (§3.1) |
| Archivos nuevos previstos | Casos de uso, DTOs, repositorio, configuración y UI de confirmación, reservas, edición y cancelación bajo las carpetas de pedidos de F1; `SalesOrderPermissions.cs` (ampliación del archivo de F1, que pasa a existente); libro de cancelaciones |
| Archivos protegidos | Los de F1 más todo `Inventory/` (se consume el contrato creado en F2) y `Sales*` de facturación |
| Archivos candidatos pendientes de verificación | La key de vigencia en días y su lectura (**VER-11**): `OrgSettingKeys.cs`, `InventoryConfigurationDefinitions.cs`, `IOperationalPreferencesResolver.cs`, `OperationalPreferencesDto.cs`, `UpdateOperationalPreferencesCommandHandler.cs` (techo ADR-046) |
| Operaciones/contratos | Confirmar (idempotente), completar, liberar, extender, editar confirmado, cancelar pendientes; códigos §17 de la especificación; **sin vigencia configurada, Confirmar se rechaza (OPS-01 resuelta): sin efectos, sin valor predeterminado ni fecha manual, con el mensaje de la especificación §10.3; validación en backend y UI** |
| Esquema y baseline (§3.1) | Modelo EF: `sales_order_cancellations` y columnas de vigencia/días aplicados en `sales_orders`; consolidación en la baseline única |
| Pruebas focalizadas | Las de F3 en §4. Concurrencia (AT-CON-01) en PostgreSQL real. **No exige** pruebas que dependan de preparación, selección, consumo o entregas (diferidas a F4–F8) |
| Guardrails | Comunes |
| Riesgos | Sobre-reserva; liberar lo comprometido; confirmar sin vigencia configurada (rechazo definido en OPS-01); techo ADR-046 (R-15) |
| Rollback | Común; retirar la acción «Confirmar» |
| Criterios de aceptación | ESC-01, ESC-02 y ESC-13 reproducibles con las cifras de la especificación §18; Confirmar sin vigencia configurada se rechaza sin efectos y con el mensaje definido (AT-RES-01g, ESC-13.0), y tras configurarla se puede confirmar de nuevo; cero Kardex/factura/CxC/contabilidad |
| Evidencia | Común + tablas ESC con el resultado real por paso |
| Siguiente fase | F4 |

## 10. Fase 4 — Preparación

| Campo | Contenido |
|---|---|
| Objetivo | Preparar y revertir preparación sobre lo comprometido; aplicar la secuencia de reconciliación previa a liberar, cancelar o editar |
| Dependencias | F3 |
| Bloqueos | Lista exacta |
| Requisitos | PV-PREP-01; completa AT-RES-04e, AT-EDI-01f, AT-CAN-01b |
| Archivos existentes autorizables (ruta por aprobar) | Los casos de uso de liberar, cancelar y editar creados en F3, solo para invocar la reconciliación; `es.json`/`en.json`; `tools/ci/backend-test-groups.json`; baseline (§3.1) |
| Archivos nuevos previstos | Libro `sales_order_preparations`, casos de uso, repositorio y UI de preparación |
| Archivos protegidos | Inventario y facturación |
| Archivos candidatos pendientes | — |
| Operaciones/contratos | Preparar, revertir (motivo), consulta de exceso |
| Esquema y baseline (§3.1) | Modelo EF: `sales_order_preparations`; consolidación en la baseline única |
| Pruebas focalizadas | Las de F4 en §4; INV-05 en PostgreSQL real con `I = 0` (la desviación por anulación se prueba en F8) |
| Guardrails | Comunes |
| Riesgos | Liberar con mercancía preparada sin respaldo |
| Rollback | Común |
| Criterios de aceptación | `P + q ≤ I + R` en cada preparación; rechazo con exceso y aceptación tras revertir |
| Evidencia | Común + ESC-03.1 y AT-RES-04e con sus cifras |
| Siguiente fase | F5, solo con VER-03, VER-04 y VER-13 resueltos y lista exacta aprobada |

## 11. Fase 5 — Selección de facturación, liberación de selección, borrador desde pedido y bloqueo comercial — **NO AUTORIZABLE**

| Campo | Contenido |
|---|---|
| Objetivo | Seleccionar cantidades (productos reservables, no reservables y servicios), **liberar manualmente una selección abandonada (DEC-04)**, crear el borrador de factura desde el pedido en el servidor, mostrar la pantalla oficial con datos comerciales de solo lectura y rechazar en backend cualquier edición comercial de un borrador con origen |
| Dependencias | F3 |
| Bloqueos | VER-03, VER-04, VER-13 |
| Requisitos | PV-FAC-01, PV-FAC-02, PV-FAC-03 (no regresión), PV-FAC-05 (liberación); PV-EDI-01 (líneas con selección activa) |
| **Qué falta para cerrar la lista de rutas** | VER-03 (¿`UnitPrice` validado?), VER-04 (contrato real de hidratación/bloqueo en el frontend) y VER-13 (selección de productos no reservables). DEC-01 y DEC-04 ya están aprobadas; la lista exacta debe aprobarla el propietario |
| Archivos existentes autorizables | **Ninguno hasta cerrar los bloqueos** |
| Archivos nuevos previstos | Tabla `sales_order_invoice_links`; casos de uso de selección y de **liberación de selección** (API prevista en la especificación §12.5, clave `sales.orders.selection.release`); UI de facturación del pedido |
| Archivos protegidos | `AuthorizeSalesUseCases.cs`, `CancelSalesUseCases.cs`, `SalesReturn*`, ElectronicDocuments, Inventario; el ciclo de vida de `SalesInvoice` **no se altera** |
| Archivos candidatos pendientes de verificación | `ERP.Application/Modules/Sales/UseCases/SalesDraftUseCases.cs` (guard de `UpdateSalesDraft`, comando nuevo); `ERP.Application/Modules/Sales/DTOs/SalesDtos.cs` (campo de origen); `ERP.API/Controllers/SalesController.cs` (o un controlador nuevo); `ERP.Infrastructure/Persistence/Repositories/Sales/SalesInvoiceRepository.cs`; `frontend/src/modules/sales/pages/SalesPage.tsx`, `hooks/useSalesPage.ts`, `utils/salesInvoiceHydration.ts` y componentes de líneas; baseline (§3.1) |
| Operaciones/contratos | Comando `CreateSalesDraftFromOrder` (TP-06); guard en `UpdateSalesDraft`; `SalesInvoiceDto` con origen; liberación de selección; claves `invoice` + `sales.create` y `selection.release` |
| Esquema y baseline (§3.1) | Modelo EF: `sales_order_invoice_links` (estados `Selected`/`Consumed`/`Released`, `Released` irreversible); consolidación en la baseline única |
| Pruebas focalizadas | Las de F5 en §4; regresión de borrador y edición de ventas directas; concurrencia PostgreSQL real |
| Guardrails | Comunes + F-http y DS |
| Riesgos | R-03, R-06, R-12; que el cliente edite datos comerciales por API |
| Rollback | Común; las ventas directas no dependen de la fase |
| Criterios de aceptación | `PUT` directo con otro precio/cantidad/cliente ⇒ 422; ventas directas sin cambios; selección ≤ `Sel`; liberación solo con vínculo `Selected` y factura Borrador |
| Evidencia | Común + lista exacta de rutas aprobada |
| Siguiente fase | F6 |

## 12. Fase 6 — Consumo atómico en la autorización — **NO AUTORIZABLE**

| Campo | Contenido |
|---|---|
| Objetivo | Consumir el vínculo y la reserva **solo de los productos reservables** y registrar las `SaleExit` en la misma unidad de trabajo atómica e idempotente de `AuthorizeSalesInvoiceHandler`; facturar servicios sin reserva, bodega ni `SaleExit`; impedir la autorización de un borrador desvinculado; **demostrar** la concurrencia |
| Dependencias | F2, F5 |
| Bloqueos | VER-05, VER-06, VER-08, VER-13; **elección T1/T2 con evidencia (AT-CON-02..05, 07)** |
| Requisitos | PV-FAC-04, PV-FAC-05 (guard) |
| **Qué falta para cerrar la lista de rutas** | VER-05 (transacción ambiente y secuencial), VER-06 (reintento restaura el rastreo), VER-08 (causa raíz de CI-1201), VER-13 y la elección T1/T2 (ADR-049 §6) |
| Archivos existentes autorizables | **Ninguno hasta cerrar los bloqueos** |
| Archivos nuevos previstos | Servicio de consumo y su contrato; pruebas |
| Archivos protegidos | `CancelSalesUseCases.cs`, `SalesReturn*`, ElectronicDocuments, secuencias FROZEN |
| Archivos candidatos pendientes de verificación | `ERP.Application/Modules/Sales/UseCases/AuthorizeSalesUseCases.cs` (invocar el servicio; el `Handle` no puede crecer); `StockRepository.cs`/`IStockRepository.cs` si se opta por C2; `ERP.Infrastructure/Persistence/Repositories/Sales/SalesAuthorizationRetryReader.cs` |
| Operaciones/contratos | Consumo + guard `…LINK_NOT_SELECTED`; precheck `libre + reserva propia`; tratamiento por naturaleza de línea (spec §12.4) |
| Esquema y baseline (§3.1) | Sin cambios de esquema previstos |
| Pruebas focalizadas | Las de F6 en §4; regresión: `AuthorizeSalesInvoiceHandlerTests`, `AuthorizeSalesInvoiceCancelledMessageTests`, `CancelSalesInvoiceHandlerTests`, `SalesCancelCollectionConcurrencyTests`, `SalesWarehouseScopeA6Tests`; pruebas de factura mixta Producto + Servicio, de producto bajo venta sin stock, del caso D de DEC-02 (AT-FAC-04i) y de concurrencia en PostgreSQL real |
| Guardrails | Comunes + C-03 (el `Handle` no puede crecer) |
| Riesgos | R-04, R-05, R-09, R-10, R-14 |
| Rollback | Común; el servicio se desregistra y la autorización vuelve a su comportamiento actual |
| Criterios de aceptación | Un solo consumo por vínculo; rollback sin efectos parciales; sin deadlocks ni doble autorización; ventas directas sin cambios |
| Evidencia | Común + comparación antes/después de `Handle` (líneas) y de las pruebas de regresión; resultado de AT-CON-02..05 y 07 |
| Siguiente fase | F7 |

## 13. Fase 7 — Entregas y habilitación fiscal

| Campo | Contenido |
|---|---|
| Objetivo | Registrar entregas parciales de cantidades facturadas y legalmente habilitadas, sin segunda salida de Kardex |
| Dependencias | F6 |
| Bloqueos | VER-02 |
| Requisitos | PV-ENT-01 |
| Archivos existentes autorizables (ruta por aprobar) | `es.json`/`en.json`; `tools/ci/backend-test-groups.json`; baseline (§3.1) |
| Archivos nuevos previstos | Libro `sales_order_deliveries`, regla de habilitación (lectura de `ElectronicDocument`/`SalesInvoiceDto.ElectronicStatus` por contratos existentes), casos de uso y UI |
| Archivos protegidos | ElectronicDocuments, estrategias de emisión, Ventas, Inventario |
| Archivos candidatos pendientes | Fuente fiscal adicional, solo si VER-02 lo exige |
| Operaciones/contratos | Registrar entrega; consulta de habilitación por factura |
| Esquema y baseline (§3.1) | Modelo EF: `sales_order_deliveries`; consolidación en la baseline única |
| Pruebas focalizadas | Las de F7 en §4; estados electrónicos Authorized/Sent/Failed/Rejected/AnnulmentPending |
| Guardrails | Comunes |
| Riesgos | R-08; fuente fiscal insuficiente (VER-02 ⇒ entrega bloqueada) |
| Rollback | Común |
| Criterios de aceptación | Entrega rechazada sin habilitación; cero Kardex; cada entrega cumple `0 < q ≤ E_v(antes)` por línea de factura |
| Evidencia | Común + tabla de estados fiscales con resultado |
| Siguiente fase | F8 |

## 14. Fase 8 — Reconciliación, indicadores, incidencias, regularización y cierre

| Campo | Contenido |
|---|---|
| Objetivo | Derivar `I`/`Ih`/`Dv`/`Esr` de anulaciones y devoluciones; mostrar ambos hechos (histórico y vigente); gestionar la **incidencia auditada** (DEC-05 C1); registrar la regularización **M1** (refacturación oficial); conservar los pedidos Cerrados (B1); aplicar la condición de cierre |
| Dependencias | F7 |
| Bloqueos | VER-12 (**M2, M3 y M4 permanecen bloqueadas**; M1 está definida) |
| Requisitos | PV-REC-01, PV-EST-01 (Cerrado) |
| Archivos existentes autorizables (ruta por aprobar) | Los de pedidos creados en F1–F7 que calculen estado e indicadores; `es.json`/`en.json`; baseline (§3.1) |
| Archivos nuevos previstos | Consultas y UI de trazabilidad e incidencias; registro `sales_order_delivery_regularizations` y su caso de uso |
| Archivos protegidos | `CancelSalesUseCases.cs`, `SalesReturn*` (solo lectura de sus resultados), ElectronicDocuments, Inventario |
| Archivos candidatos pendientes | Lo que exijan las modalidades M2–M4 una vez resuelta VER-12; **no se asume ninguno** |
| Operaciones/contratos | Lectura de facturas y devoluciones vinculadas; incidencia derivada (`Esr`); regularización M1 con verificación de evidencia (spec §15.3); **ninguna transición desde Cerrado**; sin cierre manual de incidencias |
| Esquema y baseline (§3.1) | Modelo EF: `sales_order_delivery_regularizations`; consolidación en la baseline única |
| Pruebas focalizadas | Las de F8 en §4 (incluidas ESC-16 y ESC-17); las modalidades M2–M4 verifican el rechazo `…MODALITY_BLOCKED` |
| Guardrails | Comunes |
| Riesgos | R-07; doble contabilización si se emite Kardex desde el pedido (prohibido) |
| Rollback | Común |
| Criterios de aceptación | Reservas no se reabren; Cerrado se conserva; indicadores = fórmulas del §9.1; incidencia bloquea el cierre de un Confirmado; la regularización no genera Kardex, CxC ni contabilidad |
| Evidencia | Común + ESC-10/11/16/17 con cifras reales |
| Siguiente fase | F9 |

## 15. Fase 9 — Cierre documental (propuesta separada)

Objetivo: crear la SSOT funcional del módulo (`docs/architecture/` — archivo único, solo con lo implementado y
probado) y actualizar los documentos de avance (`STATUS.md`, `FEATURES.md`, `WORK_QUEUE.md`, `ROADMAP.md`, el índice de
[docs/decisions/README.md](../decisions/README.md), [docs/architecture/README.md](../architecture/README.md) y
[MENU_MAP](../navigation/MENU_MAP.md)), según el [flujo por fase](../architecture/enforcement.md#flujo-obligatorio-del-agente-por-fase).

**Esta fase no autoriza eliminar, sustituir ni retirar ADR, especificaciones, planes ni documentos históricos**
(incluidos ADR-049, la especificación funcional y este plan). **Cualquier limpieza documental posterior requiere
alcance y autorización propios.** Verificación documental: enlaces y anclas de los archivos modificados y
`git diff --check`; el guardrail `run-all.mjs --only documentation` escribe un reporte (ver §5) y se ejecuta solo con
autorización. **No autorizada.**

## 16. Registro documental y propuesta posterior

**Registrado el 17/11/2026 con autorización del propietario:** estado `Accepted` del ADR-049; entrada en el índice
`docs/decisions/README.md`; registro en `STATUS.md` (diseño aprobado, no implementado) y en `WORK_QUEUE.md`
(`PEDIDOS-VENTA-V1-F0`, PENDING / NO AUTORIZADA).

**Pendiente, requiere autorización separada:** (1) nota en `ROADMAP.md` sobre el límite de reservas (hoy «sin
obligación V1 implícita»); (2) línea en `FEATURES.md` solo cuando exista capacidad implementada. Nada de esto se modificó.

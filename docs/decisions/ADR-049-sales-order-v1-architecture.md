# ADR-049 — Orden de Pedido de Venta V1 (Gestión de Ventas)

Estado: **Accepted** — aprobación formal del propietario registrada el **17/11/2026**.
Las decisiones DEC-01 a DEC-06 (y OPS-01) fueron aprobadas por el propietario y están consolidadas aquí. **La aceptación
del ADR aprueba el diseño; no autoriza código, endpoints, migraciones, permisos, cambios en módulos FROZEN/CLOSED ni
ejecución alguna.** El diseño **no está implementado**. **Ninguna fase de implementación está autorizada** (F0 figura en
la cola como PENDING / NO AUTORIZADA); cada fase requiere autorización expresa del propietario, con su lista exacta de rutas.

Este ADR conserva el porqué de la arquitectura; el comportamiento funcional está en la
[especificación](PEDIDOS-VENTA-V1-FUNCTIONAL-SPEC-01.md) (diseño propuesto, no SSOT) y el orden de trabajo en el
[plan de implementación](../agent/PEDIDOS-VENTA-V1-IMPLEMENTATION-PLAN-01.md). Cuando exista código probado, su SSOT
nacerá en `docs/architecture/` ([enforcement](../architecture/enforcement.md)). Estado de entrega:
[STATUS](../../STATUS.md); cola: [WORK_QUEUE](../agent/WORK_QUEUE.md) (registra F0 como PENDING / NO AUTORIZADA); planificación: [ROADMAP](../ROADMAP.md).

**Convención de etiquetas** (idéntica a la especificación): **[APROBADO]** regla `PV-*` del encargo ·
**[DECIDIDO DEC-xx]** decisión del propietario ya aprobada · **[VERIFICADO]** hecho comprobado en el repositorio ·
**[PROPUESTA]** decisión técnica `TP-xx` pendiente de aprobación · **[VERIFICAR]** contrato técnico pendiente `VER-xx` ·
**[OPS-xx]** definición operativa del propietario (OPS-01: **resuelta**). Se distinguen tres cosas: la **decisión aprobada** (el
comportamiento ya definido), el **contrato técnico pendiente** (algo que debe comprobarse en código o infraestructura) y
el **bloqueo de implementación** (una fase que todavía no puede ejecutarse con seguridad). Una verificación pendiente
**no** se convierte en una nueva decisión comercial cuando ya existe una regla aprobada.

## 1. Contexto y problema

ERP ZH V1 factura, controla inventario y contabiliza, pero **no tiene Orden de Pedido**: solo existe la palabra
`SalesOrder` en una taxonomía ([architecture.md](../architecture/architecture.md), tabla «Comercial») y nada en
código, FEATURES, STATUS ni WORK_QUEUE. El propietario aprobó una Orden de Pedido que concentre la edición comercial,
**proteja el inventario comprometido**, registre preparación y entrega, y alimente a la Factura de Venta oficial sin
duplicar facturación, SRI, Kardex, CxC ni contabilidad. La revisión técnica encontró cuatro obstáculos reales:

1. Las reservas **no existen operativamente** (§4, H-02/H-03).
2. Solo la `SaleExit` respeta lo reservado; ajustes, transferencias y devoluciones de compra no (H-04/H-05).
3. Un borrador de factura **no puede descartarse** (H-10): resuelto por **DEC-04** con la liberación manual del vínculo.
4. Ventas es FROZEN e Inventario se trata como FROZEN/CLOSED (§11): **DEC-01** autoriza solo extensiones mínimas
   controladas, fase por fase.

## 2. Alcance y exclusiones

**Alcance:** Gestión de Ventas → Orden de Pedido V1 (único documento disponible), menú permanente **Gestión de
Ventas**, reservas, preparación, entregas, selección de facturación, vínculo con la Factura de Venta existente.
**Exclusiones [APROBADO]:** Cotizaciones, CRM, campañas, oportunidades, otros tipos de documento, pantallas/menús
futuros vacíos o deshabilitados; no se renombra Facturación; **no hay motores paralelos** de facturación, impuestos,
inventario, Kardex, contabilidad ni CxC; no se cambian las reglas de facturación física o electrónica.

## 3. Reglas aprobadas y decisiones del propietario

### 3.1 Reglas funcionales

Detalle completo (precondiciones, entradas, validaciones, efectos, errores, concurrencia, pruebas) en la
[especificación](PEDIDOS-VENTA-V1-FUNCTIONAL-SPEC-01.md).

| ID | Resumen aprobado | Especificación |
|---|---|---|
| PV-COM-01 | Toda edición comercial en el pedido; borrador sin reserva; confirmar = compromiso | §7.1 |
| PV-RES-01 | Reserva automática al confirmar, parcial permitida, faltante registrado, sin Kardex | §10.1 |
| PV-RES-02 | Completar reservas por operación manual autorizada, revalidando | §10.2 |
| PV-RES-03 | Reservas con vencimiento; vencida sigue comprometida; sin liberación automática; extensión con historial | §10.3 |
| PV-RES-04 | Liberación manual parcial/total, permiso y motivo, historial, idempotente | §10.4 |
| PV-RES-05 | Ninguna operación ajena consume ni libera stock reservado de otro pedido | §10.5 |
| PV-PREP-01 | Preparación opcional/parcial sobre lo comprometido, revertible y trazable | §11 |
| PV-FAC-01 | Selección parcial/total ≤ pendiente y reservado; sin duplicados | §12.1 |
| PV-FAC-02 | Pedido edita; factura solo emite; datos comerciales de solo lectura también en backend | §12.2 |
| PV-FAC-03 | Solo pantalla y procesos oficiales de facturación | §12.3 |
| PV-FAC-04 | Consumo de reserva atómico e idempotente en la autorización interna | §12.4 |
| PV-FAC-05 | Selecciones no bloquean indefinidamente: liberación manual del vínculo (DEC-04) | §12.5 |
| PV-ENT-01 | Entrega solo de lo facturado y legalmente habilitado; sin segunda salida de Kardex | §13 |
| PV-EDI-01 | Modificación del confirmado con permiso, motivo, historial; sin alterar lo facturado | §16 |
| PV-CAN-01 | Cancelación parcial/total de pendientes con liberación de reservas | §14 |
| PV-REC-01 | Anulaciones y devoluciones por flujos oficiales; sin reabrir reservas (DEC-05) | §15 |
| PV-EST-01 | Estado principal Borrador/Confirmado/Cerrado/Cancelado + indicadores calculados | §6, §9 |
| PV-SEC-01 | `perm:*` con capacidades específicas | §2 |
| PV-FUT-01 | Límites reutilizables sin implementar nada futuro | §7.2 |
| PV-NAV-01 | Opción permanente «Gestión de Ventas» sin alterar rutas actuales (encargo §1) | §3.1 |

### 3.2 Decisiones del propietario consolidadas (DEC-01 a DEC-06)

Todas **APROBADAS por el propietario**. Sustituyen las alternativas y recomendaciones que este ADR contenía. Su
aprobación **no** equivale a autorización para programar.

| ID | Aprobado | Contenido consolidado | Dónde se desarrolla |
|---|---|---|---|
| **DEC-01** | Opción A | Se permiten **extensiones mínimas** de Ventas e Inventario indispensables para Pedidos V1. Se conserva el estado FROZEN/CLOSED; sin refactorización general; **cada fase requiere autorización individual y lista exacta de archivos**; se conservan los contratos oficiales; sin regresiones en ventas directas, Kardex, Contabilidad, CxC, SRI, Compras y Devoluciones; cualquier modificación adicional exige aprobación independiente. **No es una reapertura ilimitada.** | §11, §15 |
| **DEC-02** | Opción A | Las reservas representan **solo existencias físicas reales**: nunca negativas, nunca más que lo físicamente disponible y no comprometido, pertenecen al pedido que las originó, **ninguna venta directa u operación de inventario ajena puede consumirlas o liberarlas**. **Se protege la existencia física comprometida, no solo el contador `ReservedQuantity`:** toda operación de egreso ajena al pedido que retire `q` unidades debe cumplir `Quantity_after = Quantity_before − q ≥ ReservedQuantity` y se **rechaza** si deja la existencia física por debajo de la reserva vigente de otros pedidos. Excepción controlada: el consumo de la reserva propia en la autorización oficial de la factura del pedido (reserva y existencia bajan en una misma unidad atómica). Compatibilidad con ADR-041 (intacto, sin cambios de valoración): **sin reservas comprometidas** la venta sin stock puede dejar existencia negativa; **con reservas comprometidas** no puede reducir la existencia por debajo de ellas. Los servicios no reservan ni generan `SaleExit`. Se valida con pruebas de concurrencia en PostgreSQL real | §5.1, §9, spec §9.5 y §10.5 |
| **DEC-03** | Opción B | Vigencia inicial **configurable por empresa en días calendario**; al confirmar se calcula con el calendario oficial de negocio (ADR-034, alcance tenant/empresa) y queda registrada y auditable; la reserva vencida sigue comprometida y muestra advertencia; sin liberación automática; extensión con permiso e historial; liberación manual con motivo; **sin duración universal** y sin valores predeterminados silenciosos. **OPS-01 (aprobada/resuelta):** si la empresa no tiene configurada la vigencia, el Borrador se crea y edita, pero **Confirmar se rechaza** sin efectos, con un mensaje que indica configurar la vigencia en Preferencias operativas; sin duración predeterminada ni fecha manual. El mecanismo exacto de configuración es **contrato técnico pendiente (VER-11)** | §5 TP-20, §8, spec §10.3 |
| **DEC-04** | Opción A | Liberación manual de la selección desde el pedido: permiso específico, motivo obligatorio, registro de usuario/fecha/cantidad/factura; solo con vínculo `Selected` y factura en Borrador; el vínculo pasa a `Released` (irreversible); la cantidad vuelve a estar seleccionable; la reserva, la existencia, el Kardex, la CxC y la contabilidad no cambian; el borrador queda registrado y **permanentemente no autorizable**; la autorización verifica en backend la vigencia del vínculo; liberación y autorización concurrentes son mutuamente excluyentes; sin liberación automática por tiempo; ventas directas intactas; **no se amplía el ciclo de vida de `SalesInvoice`** | §9, spec §12.5 |
| **DEC-05** | A1 + B1 + C1 | **A1:** si la factura se anula y el pedido sigue Confirmado, sus cantidades vuelven al saldo pendiente de facturación; las reservas consumidas no se restablecen; el vínculo `Consumed` se conserva como histórico. **B1:** un pedido Cerrado conserva su estado, no se reabre, no crea cantidades facturables ni reservas; las anulaciones y devoluciones posteriores quedan visibles en el historial con incidencias cuando corresponda. **C1:** una entrega que pierde respaldo fiscal se gestiona con **incidencia auditada**; se regulariza solo con evidencia real y flujos oficiales | §9, spec §6, §9, §15 |
| **DEC-06** | Opción B (expresa) | **Una sola migración inicial consolidada** (baseline única). Las tablas y restricciones del pedido forman parte de esa baseline; **sin migraciones incrementales permanentes por fase**; se preservan los objetos raw SQL y su vigilancia; la regeneración se prueba en una base PostgreSQL desechable; sin operaciones destructivas sobre bases del propietario ni aplicación a una base real sin aprobación específica; no se regenera la baseline en este encargo | §8, §11, plan §3 |

## 4. Arquitectura actual comprobada

Rutas relativas al repositorio. Todo lo de esta sección es **[VERIFICADO]** salvo donde se indique.

| # | Hecho | Evidencia |
|---|---|---|
| H-01 | No hay pedido en código; los documentos de entrega solo registran el diseño aprobado (ADR-049) y F0 pendiente | `docs/architecture/architecture.md` (taxonomía); [WORK_QUEUE](../agent/WORK_QUEUE.md) |
| H-02 | `StockReservation` es una entidad sin `DbSet`, sin configuración EF, sin migración ni flujo; sin `CompanyId`/`BranchId`/`RowVersion`; `ExpiresAt` es instante UTC | `backend/src/ERP.Domain/Modules/Inventory/Entities/StockReservation.cs`; [inventory.md §8](../architecture/inventory.md#8-pendiente-no-operativo) |
| H-03 | `CurrentStock.ReservedQuantity`, `AvailableQuantity = Quantity − ReservedQuantity`, `Reserve()` y `ReleaseReserve()` existen, pero **nadie llama** a `Reserve`/`ReleaseReserve` en código de producción; `ReleaseReserve` no es idempotente (`Math.Max(0, …)`) | `ERP.Domain/Modules/Inventory/Entities/CurrentStock.cs` líneas 12-13, 85-101 |
| H-04 | `ApplyKardexMovement` exige `AvailableQuantity` **solo** para `SaleExit` con `enforceAvailability`; para cualquier otro egreso solo impide el negativo; una `SaleExit` autorizada por política puede dejar saldo negativo | `CurrentStock.cs` líneas 63-83 |
| H-05 | Ajuste de egreso y devolución de compra comparan contra `stock.Quantity`; la transferencia no hace precheck; ninguno considera lo reservado | `ExecuteStockAdjustmentCommandHandler.cs` 159-165; `AuthorizePurchaseReturnUseCases.cs` 305-318; `ConfirmStockTransferCommandHandler.cs` 103-130 |
| H-06 | Todo movimiento pasa por `AppendMovementAsync` → `CreateAndTrackMovementAsync` → `ApplyKardexMovement` bajo `pg_advisory_xact_lock` por `inventory:{tenant}:{empresa}:{producto}:{bodega}`; `AcquireResourceLockAsync` es público en `IStockRepository` y exige transacción activa | `ERP.Infrastructure/Persistence/Repositories/Inventory/StockRepository.cs` 101-158, 204-229, 490, 514-539; `ERP.Domain/Modules/Inventory/Interfaces/IStockRepository.cs` |
| H-07 | La disponibilidad se exige solo si Producto + control de empresa ON + control del ítem ON + `AllowSellWithoutStock` OFF; solo una `SaleExit` así autorizada puede dejar saldo negativo ([ADR-041](ADR-041-negative-sale-inventory-cost-obligations.md)) | `SaleStockPolicy.cs`; [inventory.md §5](../architecture/inventory.md) |
| H-08 | `AuthorizeSalesInvoiceHandler` no abre transacción explícita: precheck de stock (471-491) → captura de secuencial en transacción propia (541-553) → `inv.Authorize` (616) → `SaleExit` por línea ordenado por `ItemId`,`WarehouseId` (627-650) → CxC → `SaveChangesWithSequenceRetryAsync` (698); la transacción de stock se abre en el primer `AppendMovementAsync`; la emisión electrónica es posterior al commit | `ERP.Application/Modules/Sales/UseCases/AuthorizeSalesUseCases.cs` |
| H-09 | Autorizar una factura ya `Authorized` devuelve el resultado sin repetir efectos; ante conflicto, `ReadCommittedAuthorizationAsync` confirma lo comprometido | `AuthorizeSalesUseCases.cs` 126-181 |
| H-10 | `SalesInvoice.Cancel` solo acepta `Authorized`; el controlador no expone DELETE: un Borrador **no se descarta** | `ERP.Domain/Modules/Sales/Entities/SalesInvoice.cs` 611-614; `ERP.API/Controllers/SalesController.cs`; [backend.md § Soft delete](../architecture/backend.md) |
| H-11 | `CreateSalesDraft` exige caja abierta: `EmissionPointId`, `EmissionType` (snapshot inmutable) y `CashSessionId` salen de `ICurrentCashSession`, nunca del cliente; fail-closed | `SalesDraftUseCases.cs` 89-92, 501-536; [backend.md § Tipo de emisión](../architecture/backend.md) |
| H-12 | `UpdateSalesDraft` acepta cliente, líneas, pagos y cronograma sin distinguir origen; opera dentro de `ExecuteInTransactionAsync` | `SalesDraftUseCases.cs` 114-124, 690-756 |
| H-13 | La anulación bloquea la factura (`GetByIdForUpdateAsync`), rechaza con devolución autorizada o CxC con cobros, y con comprobante electrónico `Authorized` (exige NC) o en proceso; revierte Kardex con `SaleReturn`; la factura registra `CancelledAt`/`CancelledBy` | `CancelSalesUseCases.cs` 65-185; `SalesInvoiceCancellationPolicy.cs`; `SalesInvoice.cs` 611-639; [backend.md tabla de caminos](../architecture/backend.md#revertir-una-venta-un-solo-camino-por-factura-zh-sales-cancel-authorized-return-rule-01) |
| H-14 | La devolución abre transacción, toma advisory + factura `FOR UPDATE`, revalida remanente por `OriginalInvoiceDetailId`, reingresa `SaleReturn`, reembolsa y emite NC tras el commit; no conoce pedidos; solo opera sobre facturas `Authorized` | `AuthorizeSalesReturnUseCases.cs` 130-384 |
| H-15 | `ElectronicDocumentState`: Draft, XmlGenerated, Signed, Sent, Received, Authorized, Rejected, DeadLetter, Cancelled, Failed, Dispatching, Discarded, AnnulmentPending; el guard de Ventas permite procesar solo facturas/devoluciones `Authorized` y no usa el protocolo `Dispatching`; `Status` comercial y estado electrónico son independientes | `ERP.Domain/Modules/ElectronicDocuments/Enums/ElectronicDocumentState.cs`; `SalesElectronicSourceLifecycleGuard.cs`; `ElectronicDocumentSourceCancellation.cs` 118-142; [backend.md § Estado comercial vs. estado SRI](../architecture/backend.md#estado-comercial-vs-estado-sri-de-una-venta-sales-electronic-status-clarity-01) |
| H-16 | La estrategia física no ejecuta nada tras autorizar; sus requisitos y rango de secuenciales se validan antes del secuencial | `PhysicalSalesInvoiceEmissionStrategy.cs` |
| H-17 | `SalesPermissions` tiene `sales.view/create/update`; `sales.update` cubre autorizar, anular y descuento global; la navegación es server-driven por `[NavItem]` con `Id` explícito | `SalesPermissions.cs`; `SalesController.cs`; `ERP.Domain/Kernel/Modules/SalesModule.cs` |
| H-18 | `SalesPage` abre un borrador por `?invoiceId=`; un módulo solo importa facades de otro (`modules/<owner>/[<área>/]facades/`); `RouteAccessGuard` permite detalle/alta/edición de una pantalla del menú | `frontend/src/modules/sales/pages/SalesPage.tsx` 67-75; [frontend.md](../architecture/frontend.md); [security.md](../architecture/security.md#security--access-contract-v1--locked) |
| H-19 | Ventas figura FROZEN y Devolución de Ventas CLOSED; Inventario se menciona como FROZEN/CLOSED en STATUS (no en FEATURES); CI-1201 (concurrencia cancelación/devolución de Ventas) tiene causa raíz pendiente | `FEATURES.md` 46 y 48; `STATUS.md` 713 y 308-311 |
| H-20 | Instalación greenfield con **una** baseline `20261101012214_InitialProductionBaseline` (archivos `.cs`, `.Designer.cs` y `ErpDbContextModelSnapshot.cs`); sin upgrade-path (una BD creada con otra cadena se recrea con autorización del propietario); objetos raw SQL registrados y vigilados por pruebas | [DATABASE.md § Migraciones](../DATABASE.md); `ERP.Infrastructure/Migrations/` |
| H-21 | Todo Aggregate Root operativo persiste `TenantId`+`CompanyId`+`BranchId` (`BranchId` desde el contexto o la bodega, nunca del cliente); cantidades `numeric(20,6)`, precios `numeric(22,10)`, % operativos `numeric(9,6)`, dinero `numeric(18,2)`; toda columna decimal nueva exige justificación | [Branch Ownership Rule](../architecture/architecture.md#branch-ownership-rule-obligatoria); [data-standards.md](../architecture/data-standards.md) |
| H-22 | No existe un archivo SSOT de Ventas/Facturación en `docs/architecture/`; los contratos están en `backend.md`, `frozen-infrastructure.md`, `receivables.md`, `accounting.md`, ADR-023/031/033/036/038 y `FEATURES.md` | Listado de `docs/architecture/` |
| H-23 | Convenciones vigentes: lock pesimista antes de decidir (el perdedor recibe 422 sin efectos), orden canónico de locks, y `ClientRequestId` + índice único para comandos creadores de efectos | [backend.md](../architecture/backend.md#saldos-compartidos-lock-pesimista-antes-de-decidir-zh-collections-receivable-concurrency-01) |
| H-24 | `AppendMovementAsync` ya admite `sourceDocId`/`sourceDocType`/`sourceDocLineId`; es el patrón de referencia entre documento y movimiento | `StockRepository.cs` 101-158 |
| H-25 | La configuración por empresa se guarda como `OrgSetting` (`org_settings`, único `(tenant, empresa, scope, key)`) declarada en `ConfigurationDefinitionCatalog`/`OrgSettingKeys` y leída por resolvers tipados (`IOperationalPreferencesResolver`); «null o vacío = sin configurar»; el hub administrativo es `/settings/operations` con `settings.operations.view/configure`; `inventory.stock_control_enabled` es un ejemplo | [backend.md § Configuración organizacional](../architecture/backend.md#configuración-organizacional); `OrgSettingKeys.cs`; `IOperationalPreferencesResolver.cs`; `OperationalPreferencesPermissions.cs` |
| H-26 | `UpdateOperationalPreferencesCommandHandler.Handle` es una de las excepciones de tamaño con techo individual (451 líneas, [ADR-046](ADR-046-historical-handler-size-debt.md)): agregarle lógica no puede superar ese techo | `ERP.Application/Modules/Settings/Operations/UseCases/UpdateOperationalPreferences/UpdateOperationalPreferencesCommandHandler.cs`; ADR-046 |

## 5. Decisiones técnicas propuestas

Todas **[PROPUESTA]**: no son decisiones aprobadas hasta que el propietario las apruebe. Las DEC aprobadas (§3.2) las
condicionan, pero no las convierten en aprobadas.

| ID | Propuesta | Justificación breve |
|---|---|---|
| TP-01 | Pedido como sub-dominio `SalesOrders` **dentro del módulo Sales** (Domain/Application/Infrastructure y `frontend/src/modules/sales/orders/`) | Reutiliza guards, facades y contexto de Ventas; evita facades entre módulos |
| TP-02 | Reservas como **libro de operaciones append-only en Inventario** (`StockReservation` + `StockReservationOperation`); la entidad huérfana se **sustituye**; `CurrentStock.ReservedQuantity` es proyección verificable (INV-07) | Mismo patrón que Kardex→`CurrentStock`; historial, idempotencia y vencimiento por operación; sin dos reservas paralelas |
| TP-03 | Vínculo pedido↔factura mediante **tabla de vínculos** por línea (`sales_order_invoice_links`), sin columna nueva en `SalesInvoice` | Mapeo por línea; menor intervención sobre Ventas FROZEN (DEC-01) |
| TP-04 | Protección de la **existencia física comprometida** en el **punto único** `ApplyKardexMovement`/`CreateAndTrackMovementAsync` (`Quantity_after ≥ ReservedQuantity` para todo egreso ajeno), con la **discriminación de cuatro casos** de DEC-02 (§5.1) | Ya se ejecuta bajo el lock del recurso, con `CurrentStock` recargada tras el lock; alcanza a todos los flujos presentes y futuros; un precheck previo no basta bajo concurrencia |
| TP-05 | Consumo con un servicio inyectado en la autorización, con **control optimista** sobre vínculo y reserva; **T1/T2 sin elegir** | Menos invasivo; **la elección exige evidencia (VER-05, VER-06, AT-CON-*)** |
| TP-06 | Precarga por **comando de servidor** que construye el borrador desde el pedido; `UpdateSalesDraft` rechaza cambios comerciales con vínculo; permisos `sales.orders.invoice` + `sales.create` | El cliente nunca envía datos comerciales; el backend distingue origen sin tocar ventas directas |
| TP-07 | Fuente fiscal de entrega: física = `Authorized` interna; electrónica = `ElectronicDocument.CurrentState = Authorized` | Estados independientes (H-15); el encargo exige autorización efectiva del SRI |
| TP-08 | Indicadores **derivados** por consulta de los libros y documentos; sin contadores como fuente de verdad | PV-EST-01 |
| TP-09 | Historial comercial con **Entity Audit** ([ADR-022](ADR-022-audit-infrastructure-entity-vs-process.md)) | Mecanismo oficial; solo agrega consumidores |
| TP-10 | `ClientRequestId` + índice único `(tenant_id, client_request_id)` en cada libro de operaciones | Convención de comandos creadores de efectos (H-23) |
| TP-11 | Cierre **evaluado por el sistema** tras cada operación; Cerrado y Cancelado terminales (DEC-05 B1: un pedido Cerrado se conserva) | Calculado desde operaciones reales |
| TP-12 | **Catorce** claves `sales.orders.*` propuestas en un `SalesOrderPermissions` nuevo (las doce del encargo más liberar selección y regularizar incidencias) | Convención `modulo.recurso.accion`; no toca `SalesPermissions` ni `sales.update`; **no se modifica el catálogo real** |
| TP-13 | `[NavItem]` hijo del grupo Ventas, `/sales/orders`, `SortOrder` 5, `LabelKey app.nav.item.sales.orders` | Sin renombrar ni mover Facturas ni Devoluciones |
| TP-14 | Totales del pedido **referenciales**; los impuestos legales los fija la factura al autorizar | Evita un segundo motor de impuestos |
| TP-15 | Campos editables de una factura con origen: pagos, cronograma de cobro y fecha de emisión | Datos legales/fiscales/de cobro; requiere revisión (AMB-08) |
| TP-16 | Orden de locks: **pedido primero**, luego el canónico existente. **No está validado**: la Fase 0 debe demostrar la compatibilidad con autorización, secuenciales, stock y reintentos | Evita deadlocks si se confirma; sin evidencia no se fija |
| TP-17 | Preparación, entrega, cancelación y vínculos como **libros append-only** por línea | Historial completo, sin DELETE |
| TP-18 | Reconciliación de anulaciones/devoluciones **por consulta**; sin eventos nuevos ni cambios en los flujos CLOSED de anulación y devolución | Esos flujos no conocen pedidos |
| TP-19 | **Incidencia de entrega sin respaldo fiscal** derivada de los hechos oficiales (`Esr`), con **registro auditado de regularización** (`sales_order_delivery_regularizations`); única modalidad definida: refacturación oficial verificable; las demás, bloqueadas (spec §15) | DEC-05 C1: evidencia real, sin documentos ni estados inexistentes |
| TP-20 | La vigencia de reserva se guarda como **`OrgSetting` por empresa en días calendario**, leída por el resolver tipado de preferencias operativas; sin sistema paralelo de parámetros | DEC-03: mecanismo oficial H-25; sección, key y validadores quedan en VER-11; el comportamiento sin configurar está resuelto (OPS-01) |

### 5.1 Protección de la existencia física comprometida (DEC-02, TP-04)

**Regla aprobada.** Para una operación de egreso **ajena al pedido** que retira `q` unidades:
`Quantity_after = Quantity_before − q` y, mientras `ReservedQuantity > 0`, debe cumplirse
**`Quantity_after ≥ ReservedQuantity`**; en caso contrario se **rechaza** sin efectos. No basta con dejar intacto el
contador `ReservedQuantity` si la existencia física queda por debajo de la reserva. Se evalúa dentro de
`ApplyKardexMovement`/`CreateAndTrackMovementAsync` **bajo el lock del recurso**, no como un precheck previo.
**Propuesta; no implementada.** Cada fila es una *extensión mínima autorizable por fase* (DEC-01).

| Caso | Cómo se reconoce | Regla | `ReservedQuantity` y libro | Costo |
|---|---|---|---|---|
| **1. Consumo autorizado de reserva propia** | `SaleExit` de la autorización oficial de una factura con origen pedido, que identifica su línea de pedido (`sourceDocLineId`) y su vínculo `Selected` vigente | Reserva y existencia bajan **en la misma unidad de trabajo atómica**; tras el consumo debe seguir cumpliéndose `Quantity_after ≥ ReservedQuantity_after` (la reserva restante, propia o de otros) | Disminuye exactamente lo consumido; registra `Consumir` | Regla oficial de la `SaleExit` |
| **2. Egreso normal de existencia libre** | Cualquier egreso sin consumo propio y sin autorización de venta negativa (ajuste, transferencia, devolución de compra, anulación de compra, `SaleExit` con control activo) | Permitido si `Quantity_after ≥ ReservedQuantity`; con `SaleExit` enforce se mantiene además `AvailableQuantity ≥ c` (`c` = cantidad de la salida) | Sin cambio | Regla oficial |
| **3. Venta negativa legítima (ADR-041)** | `SaleExit` autorizada por la política `AllowSellWithoutStock`/controles apagados (`authorizedNegativeSale`) | **Sin reservas comprometidas (`ReservedQuantity = 0`):** se permite y la existencia puede quedar negativa conforme a ADR-041. **Con reservas comprometidas:** solo si `Quantity_after ≥ ReservedQuantity`; nunca reduce la existencia por debajo de ellas, ni consume ni libera reservas ajenas | Sin cambio | **Sin cambio** (la valoración de ADR-041 no se toca) |
| **4. Intento de consumir reserva ajena** | Cualquier egreso sin la autorización del caso 1 que deje `Quantity_after < ReservedQuantity` | **Rechazado** `SALES_ORDER_RESERVED_STOCK_PROTECTED`; sin efectos | Sin cambio | — |

**Resultado de los seis casos de aceptación** (spec ESC-14.10 a 14.15): A) existencia 20, reservado 15, venta externa 3 ⇒
permitida (17/15) · B) existencia 20, reservado 15, venta externa 10 ⇒ rechazada (20/15) · C) existencia 0, reservado 0,
venta sin stock habilitada de 5 ⇒ permitida por ADR-041 (−5/0) · D) existencia 20, reservado 15, autorización de la factura
del propio pedido por 8 ⇒ existencia 12, reserva 7, una sola `SaleExit` · E) existencia 10, reservado 10, venta sin stock de
1 ⇒ rechazada (10/10) · F) existencia 0, reservado 0, venta ordinaria con control activo de 1 ⇒ rechazada por el contrato
vigente.

**Puntos de integración que requieren extensión mínima (DEC-01; no se implementa ahora):** el comportamiento vigente es
incompatible con la regla en dos lugares **[VERIFICADO]**: (1) `CurrentStock.ApplyKardexMovement` (`CurrentStock.cs`
63-83) solo exige disponibilidad a la `SaleExit` con `enforceAvailability` y **permite resultado negativo** a la venta
autorizada (línea 75), sin considerar `ReservedQuantity`; (2) `StockRepository.AppendMovementAsync`/`PendingMovement`
(101-153) no puede informar al punto central que una salida es **consumo propio** de una línea de pedido. Los prechecks
actuales (`AuthorizeSalesUseCases.cs` 471-491, `ExecuteStockAdjustmentCommandHandler.cs` 159-165,
`AuthorizePurchaseReturnUseCases.cs` 305-318) son preventivos y **no** son la defensa autoritativa. La implementabilidad sin
alterar ADR-041 es **VER-13**.

## 6. Alternativas evaluadas

| Tema | Alternativas | Evaluación | Resultado |
|---|---|---|---|
| **Estructura de reservas** | **R1** contador + columna · **R2** libro de reservas en Inventario con proyección · **R3** reservas de Pedido con solo el contador en Inventario | R1 sin historial ni vencimiento por operación; R3 dos fuentes de verdad; R2 patrón probado | TP-02 (R2), confirmada con pruebas en PostgreSQL real |
| **Protección de egresos** | **E1** regla en el punto único · **E2** precheck por flujo · **E3** CHECK/trigger | E2 duplica y olvida flujos; E3 no puede forzar `Quantity ≥ Reservado` con venta negativa legítima (DEC-02); E1 cubre todo bajo lock | TP-04 (E1) con los cuatro casos de §5.1 |
| **Consumo de reserva** | **C1** servicio en el handler · **C2** parámetro en `AppendMovementAsync` | C2 cambia la interfaz de `IStockRepository` | TP-05 (C1), pendiente de evidencia |
| **Control de concurrencia en la autorización** | **T1** transacción explícita al inicio + pedido `FOR UPDATE` · **T2** control optimista sobre vínculo y reserva | T1 cambia la estructura del handler y depende de VER-05; T2 la conserva | **Sin elegir**: la elección exige evidencia (VER-05, VER-06, AT-CON-*) |
| **Precarga de la factura** | **P1** comando de servidor · **P2** precarga por router · **P3** flag en frontend | P2/P3 permiten alterar datos | TP-06 (P1) |
| **Vínculo con factura** | **L1** columna `SourceOrderId` · **L2** tabla de vínculos | L1 no mapea líneas y modifica un agregado FROZEN | TP-03 (L2) |
| **Liberar selección abandonada** | **A** liberar el vínculo + guard · **B** descartar el borrador · **C** TTL automático | B altera el ciclo de vida de `SalesInvoice`; C libera automáticamente (no aprobado) | **DEC-04: opción A (aprobada)** |
| **Ownership** | Sub-dominio de Sales vs. módulo nuevo | Un módulo nuevo exigiría facades en ambos sentidos | TP-01 |
| **Indicadores** | Contadores persistidos vs. derivados | Los contadores divergen de su fuente | TP-08 |
| **Historial** | Entity Audit vs. tabla propia | Entity Audit es el mecanismo oficial | TP-09 |
| **Esquema de BD** | Migración incremental vs. regenerar la baseline única | El propietario exige una sola migración inicial consolidada | **DEC-06: opción B (aprobada)** |
| **Vigencia de reserva** | Fecha por usuario · parámetro por empresa · política fija | Una política fija impone una duración universal | **DEC-03: parámetro por empresa (aprobado)** |

## 7. Contratos de integración

| # | Servicio oficial reutilizado | Extensión mínima (autorizable por fase, DEC-01) | Invariantes que protege | Archivos que podrían verse afectados | Regresión obligatoria |
|---|---|---|---|---|---|
| 1 | `IStockRepository` (`AcquireResourceLockAsync`, `GetStockAsync`, `AppendMovementAsync`, `SaveChangesWithSequenceRetryAsync`) | Contrato de reservas (`IStockReservationRepository`, nuevo), regla de egreso físico `Quantity_after ≥ ReservedQuantity` con los cuatro casos de §5.1 y parámetro opcional de consumo propio | INV-07, INV-08, INV-09 | `CurrentStock.cs`, `StockRepository.cs`, `IStockRepository.cs` (FROZEN) + nuevos | `StockRepositoryCompanyScopeIntegrationTests`, `InventoryDocumentConcurrencyPostgreSqlTests`, `NegativeSaleCostA2Tests`, ajustes, transferencias, devolución de compra |
| 2 | `CreateSalesDraftHandler` (`ICurrentCashSession`, `IPricingResolver`, `SalesLinePackagingResolver`, resolvers de pago/cliente) | Comando `CreateSalesDraftFromOrder` que reutiliza esos servicios | Snapshot de emisión; caja abierta | `SalesDraftUseCases.cs` o archivo nuevo; `SalesDtos.cs`; `SalesController.cs` | `CreateSalesDraftHandlerTests` y las de borrador de venta directa |
| 3 | `UpdateSalesDraftHandler` | Guard «borrador con vínculo ⇒ sin cambios comerciales» | PV-FAC-02 en backend | `SalesDraftUseCases.cs` | Edición normal de ventas directas |
| 4 | `AuthorizeSalesInvoiceHandler` | Servicio de consumo y guard de vínculo `Selected` | PV-FAC-04/05, INV-10, INV-12 | `AuthorizeSalesUseCases.cs` (FROZEN) | `AuthorizeSalesInvoiceHandlerTests`, `AuthorizeSalesInvoiceCancelledMessageTests`, pruebas de reintento/concurrencia |
| 5 | `CancelSalesInvoiceHandler`, `AuthorizeSalesReturnHandler` | **Ninguna** (solo lectura de sus resultados, TP-18) | INV-11 | — | `SalesCancelCollectionConcurrencyTests` (CI-1201: VER-08) |
| 6 | `ElectronicDocument`/`SalesInvoiceDto.ElectronicStatus` | **Ninguna** (solo lectura) | PV-ENT-01, INV-14 | — | Pruebas de estado electrónico de Ventas |
| 7 | `IPricingResolver`, `ISriTaxResolver`, `salesCalc.ts` | **Ninguna** | TP-14 | — | — |
| 8 | `BusinessPartner` y resolvers de condición de pago (`ResolveForSaleAsync`) | **Ninguna** | PV-COM-01 | — | — |
| 9 | `IWarehouseRepository` (`GetBranchChangeBlockerAsync`) | Considerar reservas/pedidos abiertos al cambiar la sucursal de una bodega | Integridad de bodega | `WarehouseRepository.cs` (Inventario FROZEN) | `WarehouseBranchReassignmentHttpTests` |
| 10 | Secuencias documentales ([ADR-019](ADR-019-document-sequence-infrastructure.md), FROZEN) | **Ninguna** si VER-01 confirma que un `DocType` interno es posible con la API pública | Número del pedido | — | `DocumentSequenceExclusivityTests` |
| 11 | `KernelRegistry`, `[NavItem]`, `perm:*` | Claves y entrada de menú nuevas (aditivo) | PV-SEC-01, PV-NAV-01 | `SalesOrderPermissions.cs` (nuevo), `SalesModule.cs`, `KernelRegistry.cs` si aplica | Pruebas de kernel y de rutas alcanzables |
| 12 | Entity Audit (ADR-022) | Entidad `SalesOrderAudit` y su handler (solo agrega) | PV-EDI-01 | Nuevos | Pruebas de auditoría de dominio |
| 13 | Frontend: `CustomerPicker`/`customerPickerFacade`, `invoiceItemSearchService`, `ZHDataTable`, `ZHMoneyValue`, `ZHBtn`, `ZHModal`, `useClientRequestId` | Páginas y componentes del área `orders` | Reutilización obligatoria | `frontend/src/modules/sales/**`, `routes/catalogRoutes.tsx`, `i18n/locales/es.json` y `en.json` | `tsc -b`, lint, guardrails de arquitectura y DS |
| 14 | Configuración por empresa: `OrgSettingKeys`/`ConfigurationDefinitionCatalog`, `IOperationalPreferencesResolver`, hub `/settings/operations` | Una key de vigencia en días (TP-20) y su lectura al confirmar; **sección y validador: VER-11**; sin vigencia configurada, Confirmar se rechaza (OPS-01 resuelta) | DEC-03; sin valores predeterminados silenciosos | Candidatos: `OrgSettingKeys.cs`, `InventoryConfigurationDefinitions.cs`, `IOperationalPreferencesResolver.cs`, `OperationalPreferencesDto.cs`, `UpdateOperationalPreferencesCommandHandler.cs` (techo ADR-046, H-26) | Pruebas de preferencias operativas y de `OrgSettingsAccessGuardrailTests` |
| 15 | Baseline de migraciones ([DATABASE.md](../DATABASE.md)) | Regeneración de la **baseline única** con el esquema del pedido (DEC-06) | Una sola cadena; objetos raw SQL | `ERP.Infrastructure/Migrations/20261101012214_InitialProductionBaseline.cs`, `.Designer.cs`, `ErpDbContextModelSnapshot.cs` | `RawSqlDatabaseObjectsSurviveMigrationSquashTests`, `RawSqlDatabaseObjectsBaselineIntegrationTests`, comparación de catálogo en BD desechable |

## 8. Modelo de datos

**Todo [PROPUESTA]** (TP-01/02/17/19/20); estructura definitiva sujeta a las verificaciones y a la evaluación con
PostgreSQL real. Alcance: toda tabla operativa lleva `tenant_id`, `company_id` y `branch_id` (obligatorios, de contexto
o de la bodega); filtros globales por tenant/empresa; unicidad siempre con `tenant_id`; sin DELETE físico;
`RowVersion` en los agregados mutables; auditoría vía Entity Audit.

**Estrategia de esquema (DEC-06, aprobada):** las tablas, relaciones, índices y restricciones de este capítulo forman
parte de la **baseline única consolidada**. Se distingue, por fase: (1) **cambios de modelo y configuraciones EF**
previstos; (2) **verificación del esquema** en una base PostgreSQL desechable; (3) **consolidación en la baseline
única**; (4) **condiciones para autorizar su regeneración**; (5) **evidencias de integridad**. No quedan migraciones
incrementales permanentes y no se define ninguna actualización automática de bases existentes (§11, plan §3).

| Tabla | Entidad | Contenido y restricciones |
|---|---|---|
| `sales_orders` | `SalesOrder` | Número (VER-01), fecha de negocio `date`, cliente, snapshot de condición de pago, observaciones, estado, confirmado por/el, **vencimiento de reservas calculado y días aplicados (DEC-03)**, `row_version`. Único `(tenant_id, company_id, order_number)`. Índices por `(tenant_id, company_id, status, order_date)` y `(tenant_id, customer_id)` |
| `sales_order_lines` | `SalesOrderLine` | FK `order_id`; ítem, bodega (nula si Servicio), presentación, cantidad y cantidad base, precio, descuento %, códigos IVA/ICE, trazabilidad de precio, observaciones. CHECK cantidad > 0, precio ≥ 0, 0 ≤ desc ≤ 100 |
| `sales_order_cancellations` | libro `X` | `order_line_id`, cantidad base > 0, motivo obligatorio, usuario, instante, `client_request_id` |
| `sales_order_preparations` | libro `P` | `order_line_id`, tipo (Preparar/Revertir), cantidad base > 0, motivo (si revierte), usuario, instante, `operation_id`, `client_request_id` |
| `sales_order_deliveries` | libro `D` | `order_line_id`, `link_id` (línea de factura vinculada), cantidad base > 0, usuario, instante, `client_request_id`. **La entrega original nunca se modifica** (DEC-05 C1) |
| `sales_order_invoice_links` | vínculo | `order_line_id`, `sales_invoice_id`, `sales_invoice_detail_id` (**único**), cantidad base > 0, estado (`Selected`/`Consumed`/`Released`), seleccionado por/el, consumido el, **liberado por/el/cantidad/motivo (DEC-04)**, `row_version`. `Released` es **irreversible** (no se reactiva ni se reutiliza el borrador). Un vínculo `Consumed` **no se modifica** cuando la factura se anula: se conserva como histórico (DEC-05 A1) |
| `sales_order_delivery_regularizations` | registro auditado (TP-19) | `from_link_id` (entrega sin respaldo), `to_link_id` (factura oficial vigente que la cubre), cantidad base > 0, modalidad (solo `OfficialReinvoice` está definida), motivo, referencia a la factura de evidencia, usuario, instante, `client_request_id`. Sin DELETE ni edición |
| `sales_order_audits` | `SalesOrderAudit` | Entity Audit (solo campos del dominio: cambios comerciales, transiciones, extensiones de vigencia, con motivo y valores anteriores/nuevos) |
| `stock_reservations` | `StockReservation` (**reemplaza** la huérfana) | `company_id`, `branch_id` (de la bodega), producto, bodega, `source_doc_type`/`source_doc_id`/`source_doc_line_id` (único activo por línea origen; **sin FK al pedido**: es el patrón de `StockMovement`), vigencia `expires_on` (`date`, DEC-03), estado |
| `stock_reservation_operations` | libro `R` | `reservation_id`, tipo (Reservar/Completar/Liberar/Extender/Consumir), cantidad base > 0 (nula en Extender), fecha nueva (Extender), motivo, usuario, instante, `source_movement_id` (en Consumir), `client_request_id`. `Σ(Reservar+Completar) − Σ(Liberar+Consumir)` ≥ 0 |

- **Incidencia de entrega sin respaldo fiscal:** es **derivada** (`Esr = max(0, D_v − H_v − regularizado)`); la fecha de
  origen es `SalesInvoice.CancelledAt` (H-13) o el cambio de estado fiscal; no existe una tabla de incidencias que deba
  actualizarse con eventos de los flujos CLOSED (TP-18). La **resolución** exige un registro de regularización.
- **Derivación:** `R`, `P`, `D`, `X`, `S`, `I` y las demás cantidades se calculan de los libros y vínculos;
  `CurrentStock.ReservedQuantity` se actualiza en la misma transacción bajo el lock y se verifica con INV-07
  (proyección, no fuente).
- **Idempotencia:** índice único `(tenant_id, client_request_id)` por libro (parcial si procede, convención H-23).
- **Raw SQL:** un trigger tipo `a1_stock_product_scope` para que solo se reserve un Producto de la misma empresa se
  registra entre los objetos raw vigilados ([DATABASE.md](../DATABASE.md)); su necesidad se decide en la fase de
  inventario. Se **preservan** todos los objetos raw vigentes.
- **Fechas:** vigencia y fecha del pedido `date` (ADR-034); instantes `timestamptz` UTC. La `ExpiresAt` UTC de la
  entidad huérfana es otra razón para sustituirla.
- **Configuración de vigencia (DEC-03):** no es una tabla nueva: es un `OrgSetting` por empresa (TP-20, H-25).
- **Gate de columnas decimales nuevas** ([data-standards](../architecture/data-standards.md)): cantidades `numeric(20,6)`
  (política: cantidad de la empresa; VER-09), precio `numeric(22,10)`, descuento `numeric(9,6)`, factor
  `numeric(18,10)`. Los campos exactos de `CompanyPrecisionPolicy` se confirman en la Fase 0.
- **No se inventan entidades:** cliente, ítem, bodega, factura, devolución, impuestos, pagos, CxC y contabilidad **se
  consumen**, no se duplican.

## 9. Transacciones y concurrencia

**Orden de locks propuesto (TP-16) — NO VALIDADO.** Extensión del [orden canónico](../architecture/backend.md#saldos-compartidos-lock-pesimista-antes-de-decidir-zh-collections-receivable-concurrency-01):
0. `SalesOrder` (nuevo, primero) → 1. advisory de documento → 2. `SalesInvoice` → 3. `Payment` → 4. `SalesReceivable`
(ids ascendentes) → 5. secuencia documental → 6. recursos de stock `(ítem, bodega)` en orden ascendente (el orden con que
la autorización ya emite las `SaleExit`). La **Fase 0 debe demostrar** su compatibilidad con la autorización, los
secuenciales, el stock y los reintentos; **mientras tanto no se elige T1 ni T2** y el orden es una hipótesis de trabajo.

| Operación | Transacción | Locks en orden | Comprobaciones bajo lock | Persistencia | Rollback | Reintento / idempotencia |
|---|---|---|---|---|---|---|
| Crear/editar borrador | Un `SaveChanges` | — (`RowVersion`) | Validaciones §8 de la especificación | Pedido y líneas | Nada persistido | Conflicto de versión ⇒ 409 |
| Confirmar | `BeginTransactionAsync` → commit tras `SaveChanges` | 0 pedido → 6 recursos de stock por línea | Estado Borrador; **vigencia configurada (OPS-01: sin ella se rechaza antes de cualquier efecto)**; disponibilidad real; líneas elegibles | Estado, vencimiento calculado, reservas, `ReservedQuantity` | Sin reserva ni cambio de estado | Ya Confirmado ⇒ resultado original; `client_request_id` |
| Completar reserva | Ídem | 0 → 6 | Estado, `F > 0`, disponible real | Operación + proyección | Sin efectos | `client_request_id`; el perdedor 422 |
| Liberar reserva | Ídem | 0 → 6 | `cantidad ≤ R − S`; reconciliación de preparación | Operación + proyección | Sin efectos | `client_request_id` |
| Extender | Un `SaveChanges` | 0 | Fecha válida | Operación + auditoría | Sin efectos | `client_request_id` |
| Preparar / revertir | Ídem | 0 | INV-05; motivo al revertir | Registro del libro | Sin efectos | `client_request_id` |
| Cancelar pendientes | `BeginTransactionAsync` | 0 → 6 | INV-15; preparación reconciliada | `X` + liberación asociada | Sin efectos | `client_request_id` |
| Editar confirmado | `BeginTransactionAsync` | 0 → 6 (si cambia `R`) | Reglas §16 de la especificación | Línea, auditoría, liberación asociada | Sin efectos | `RowVersion`; `client_request_id` |
| Seleccionar y crear borrador | Una unidad de trabajo (`IUnitOfWork`) | 0 | `cantidad ≤ Sel`; caja abierta | Vínculos `Selected` + borrador de factura | Ni vínculos ni borrador | `client_request_id`; el perdedor 422 |
| **Liberar selección (DEC-04)** | `BeginTransactionAsync` | 0 pedido → 2 factura (`GetByIdForUpdateAsync`) | Vínculo `Selected`; factura **Borrador**; motivo | Vínculo `Released` (usuario, fecha, cantidad, motivo, referencia de factura), auditoría | Sin efectos | `client_request_id`; segundo intento ⇒ 422 `…LINK_NOT_SELECTED`. Frente a la autorización, **exactamente una gana** por `RowVersion` del vínculo y la relectura del vínculo en la unidad de trabajo de la autorización (AT-CON-07) |
| **Autorizar factura con origen** | Hoy: sin transacción explícita (H-08) | Existentes de la autorización; vínculo y reserva con `RowVersion` (T2 no elegida) | Vínculo `Selected`; reserva propia suficiente (producto reservable); stock | Vínculo `Consumed`, baja de `R`, `ReservedQuantity`, `SaleExit` (solo productos), CxC y eventos en el mismo `SaveChangesWithSequenceRetryAsync` | Vuelve al estado Borrador/`Selected` sin consumo | Factura `Authorized` ⇒ resultado; reintento por conflicto restaura la unidad de trabajo (VER-06) |
| Registrar entrega | Un `SaveChanges` | 0 | `0 < q ≤ E_v(antes)` por línea de factura vinculada (INV-06); estado fiscal | Registro del libro | Sin efectos | `client_request_id` |
| Regularizar incidencia (TP-19) | `BeginTransactionAsync` | 0 pedido → 2 facturas de evidencia (lectura) | Evidencia verificable (spec §15); cantidad ≤ lo cubierto | Registro de regularización | Sin efectos | `client_request_id` |

**Puntos críticos (sin asumir atomicidad que el código no garantiza):**
- La captura del secuencial usa su propia transacción y ocurre **antes** del commit de la venta (H-08). Es comportamiento
  existente y **no se modifica**: un fallo posterior puede dejar un secuencial consumido. El consumo de la reserva debe
  quedar **dentro del mismo `SaveChanges`** que las `SaleExit`.
- Si se eligiera T1 habría que comprobar antes (VER-05) que `CaptureNextAsync` y `SaveChangesWithSequenceRetryAsync` se
  comporten con transacción ambiente.
- Toda operación debe sobrevivir a `RecoverFromConflictAndRetrackAsync` (VER-06).
- La emisión electrónica ocurre **después** del commit; su fallo no revierte `I` ni el consumo (INV-14).
- Una factura con origen pedido puede mezclar Productos y Servicios: solo los **productos con bodega** generan
  `SaleExit` y consumen reserva (spec §12.4).

**Pruebas de concurrencia obligatorias** (PostgreSQL real; definidas en la especificación §19): AT-CON-01 ausencia de
deadlocks entre operaciones de reserva con líneas en orden inverso (F3) · AT-CON-02 ausencia de doble autorización (F6)
· AT-CON-03 ausencia de doble consumo de reservas (F6) · AT-CON-04 rollback correcto de las operaciones del pedido (F6) ·
AT-CON-05 protección de ventas directas concurrentes (F6) · AT-CON-06 ventas sin existencias concurrentes con reservas (existencia física comprometida y ADR-041)
(F2) · AT-CON-07 liberación de selección frente a autorización (F6).

## 10. Seguridad

- Autorización con `[Authorize(Policy = "perm:...")]` y las **catorce** claves propuestas (TP-12; **no se modifica el
  catálogo real**); la UI solo oculta por conveniencia (`usePermissionsUi`, fail-closed). `sales.update` no concede
  operaciones del pedido. La configuración de la vigencia por empresa reutiliza los permisos existentes
  `settings.operations.view/configure`.
- La liberación de la selección y la regularización de incidencias exigen sus claves propias; ambas registran usuario,
  instante, motivo y referencias.
- Aislamiento: tenant desde el contexto, empresa y sucursal desde `ICurrentCompany`/`ICurrentBranch`; comandos con los
  marcadores de scope existentes (`IBranchScopedRequest`); `BranchId` nunca del cliente; ningún dato cruza tenant,
  empresa o sucursal; fila fuera de alcance ⇒ 404.
- Rutas: detalle/alta/edición se autorizan por la pantalla de menú del padre; una prueba de inventario exige que toda
  ruta registrada sea alcanzable con el menú completo.
- La factura con origen sigue exigiendo los permisos oficiales de Ventas. Ningún agente puede leer tokens first-run ni
  tocar credenciales; operaciones destructivas fuera de alcance ([CLAUDE.md](../../CLAUDE.md)).

## 11. Restricciones FROZEN/CLOSED

**DEC-01 (aprobada, opción A) no es una reapertura ilimitada:** autoriza **extensiones mínimas** indispensables, con
estas condiciones: se conserva el estado FROZEN/CLOSED; sin refactorización general; **autorización individual por fase
y lista exacta de archivos aprobada por el propietario**; contratos oficiales intactos; sin regresiones en ventas
directas, Kardex, Contabilidad, CxC, SRI, Compras y Devoluciones; toda modificación adicional exige aprobación
independiente.

| Elemento | Estado y evidencia | Extensión mínima prevista (sujeta a lista exacta) | Fase |
|---|---|---|---|
| Ventas (`SalesInvoice`, handlers de borrador/autorización, `SalesController`, `SalesPage`) | FROZEN (`FEATURES.md` 46) | Comando desde pedido, guard de edición, consumo y guard de vínculo | F5, F6 |
| Devolución de Ventas | CLOSED (P0-01) | **Nada** (solo lectura) | F8 |
| Inventario (`CurrentStock`, `StockRepository`, `IStockRepository`, `WarehouseRepository`) | Tratado como FROZEN/CLOSED (`STATUS.md` 713; alcance exacto **VER-10**) | Reservas, regla de egreso de §5.1, bloqueo de bodega | F2 |
| Compras (devolución y anulación de compra) | CLOSED (`FEATURES.md`) | Solo regresión; mensajes opcionales con autorización específica | F2 |
| `ElectronicDocuments`, estrategias de emisión | FROZEN ([frozen-infrastructure.md](../architecture/frozen-infrastructure.md)) | **Nada** (solo lectura) | F7 |
| Secuencias documentales (ADR-019) | FROZEN | Nada si VER-01 lo confirma | F1 |
| Entity Audit (ADR-022) | FROZEN | Solo **agregar** consumidores | F1 |
| Configuración tributaria, valores por defecto de facturación | FROZEN | Consumir; nada | — |
| Ítems (ADR-040), Pricing (ADR-021), Mensajes (ADR-018) | FROZEN/CLOSED | Consumir; nada | — |
| Configuración por empresa / preferencias operativas | Mecanismo oficial (H-25); handler con techo ADR-046 (H-26) | Una key de vigencia (VER-11) sin superar el techo | F3 |
| **Baseline de migraciones** | Única cadena ([DATABASE.md](../DATABASE.md)) | **DEC-06 aprobada:** esquema del pedido dentro de la baseline única; regeneración probada en BD desechable y autorizada específicamente por fase | F1–F8 |

Ninguna excepción adicional está aprobada. Una incompatibilidad no autoriza a inventar una solución ni a ampliar una fase.

## 12. Riesgos y medidas de control

| ID | Riesgo | Control |
|---|---|---|
| R-01 | Carrera entre reserva y venta/egreso ajeno | Un único lock por `(tenant, empresa, producto, bodega)`; TP-04 en el punto único; AT-CON-* en PostgreSQL |
| R-02 | Cambiar `ApplyKardexMovement` afecta Compras, Transferencias, Ajustes y Ventas | Fase propia (F2), lista exacta de archivos, regresión focalizada de todos los flujos, DEC-01 con condiciones |
| R-03 | Doble facturación o consumo | INV-02/04/12; unicidad por línea de factura; control optimista; AT-CON-02/03 |
| R-04 | Consumo parcial si falla el guardado | Todo en el mismo `SaveChanges` que las `SaleExit`; AT-CON-04 |
| R-05 | Secuencial consumido tras fallo (comportamiento existente) | No se modifica; se documenta |
| R-06 | Borrador desvinculado que pueda autorizarse | DEC-04: guard de vínculo en la autorización + liberación irreversible; AT-FAC-05b/05c y AT-CON-07 |
| R-07 | Entrega sin respaldo fiscal tras una anulación | Incidencia visible, cierre bloqueado (DEC-05 C1); regularización solo por modalidad verificable |
| R-08 | Confusión autorización interna vs. SRI | INV-14; `I` e `I_hab` separados en indicadores y pruebas |
| R-09 | CI-1201 sin causa raíz | VER-08 antes de tocar Ventas |
| R-10 | Handlers largos (límite 150 líneas, ADR-046) | Casos de uso pequeños y servicios; `AuthorizeSalesInvoiceHandler.Handle` no puede crecer |
| R-11 | Duplicar cálculo de impuestos o precios | TP-14; reutilizar resolvers |
| R-12 | Ruptura de ventas directas | Guard solo con vínculo; regresión de borrador/autorización directos |
| R-13 | Interpretación del agente | `PEDIDOS-AGENT-GUARD-01`; listas exactas de rutas; informar desviaciones antes de modificar |
| R-14 | Una venta sin existencias podría dejar la existencia física por debajo de las reservas de otros pedidos, o una comprobación previa podría fallar bajo concurrencia | La regla `Quantity_after ≥ ReservedQuantity` se evalúa **dentro** del punto central bajo el lock (§5.1), no solo en prechecks; AT-RES-05i, AT-CON-05 y AT-CON-06 en PostgreSQL real; VER-13 |
| R-15 | La key de vigencia obliga a tocar un handler con techo ADR-046 | VER-11; no superar el techo individual; extraer la lógica |
| R-16 | Regenerar la baseline invalida bases ya creadas (no hay upgrade-path) | DEC-06: solo con aprobación específica; recreación de BD con autorización del propietario; sin operaciones destructivas automáticas |
| R-17 | Confirmar sin vigencia configurada | **OPS-01 resuelta:** Confirmar se rechaza sin efectos (sin reserva, estado, Kardex, factura, CxC ni contabilidad), sin duración predeterminada ni fecha manual, con mensaje que remite a Preferencias operativas; validación obligatoria en backend y reflejada en la UI. Pruebas AT-RES-01g y ESC-13.0 |

## 13. Decisiones, contratos técnicos pendientes y bloqueos

### 13.1 Decisiones del propietario

**DEC-01 a DEC-06: APROBADAS** (§3.2). **Ya no son decisiones comerciales pendientes.**

### 13.2 Definición operativa del propietario

| ID | Estado | Resumen |
|---|---|---|
| **OPS-01** | **APROBADA / RESUELTA** | Con la vigencia de reservas sin configurar: se permite crear y editar el Borrador; se **rechaza Confirmar** (sin reserva, sin cambio de estado, sin Kardex, factura, CxC ni contabilidad); el ERP explica que debe configurarse la vigencia en Preferencias operativas; no se asigna duración predeterminada ni se pide fecha manual; validación obligatoria en backend y reflejada en la UI; al configurarla se puede volver a confirmar y se **revalidan** la configuración y la disponibilidad. Mensaje previsto: «No se puede confirmar el pedido porque esta empresa no tiene configurada la vigencia de las reservas. Configure los días de vigencia en Preferencias operativas e inténtelo nuevamente». El código del error seguirá la convención oficial del repositorio. **VER-11** (ubicación exacta de la configuración, validadores y contratos) sigue pendiente |

### 13.3 Contratos técnicos pendientes (sin nueva decisión comercial)

| ID | Qué debe comprobarse |
|---|---|
| VER-01 a VER-10 | Ver especificación, Anexo B.4 |
| **VER-11** | Sección, key, validador y resolver donde vive la vigencia en días (H-25), y cómo evitar superar el techo de ADR-046 (H-26) |
| **VER-12** | Contratos oficiales de cada modalidad de regularización distinta de la refacturación (devolución física contra factura anulada, ajuste de existencias, recuperación de habilitación) |
| **VER-13** | Que la regla `Quantity_after ≥ ReservedQuantity`, la excepción del consumo propio y los cuatro casos de §5.1 son implementables en el punto central **bajo el lock** sin alterar ADR-041 ni su valoración; confirmar los puntos de integración de §5.1 (`ApplyKardexMovement` línea 75 y `PendingMovement`/`AppendMovementAsync`) y la elegibilidad de productos no reservables |
| **VER-14** | Procedimiento exacto de regeneración de la baseline única conservando nombre, objetos raw SQL y pruebas de vigilancia |

### 13.4 Bloqueos de implementación

| Fase | Bloqueos restantes |
|---|---|
| F1 | VER-01, VER-07, VER-14; lista exacta de rutas |
| F2 | VER-10, VER-13, VER-14; lista exacta |
| F3 | VER-11; F1 y F2 |
| F4 | F3 |
| F5 | VER-03, VER-04; lista exacta (**NO AUTORIZABLE**) |
| F6 | VER-05, VER-06, VER-08; demostración de TP-16; lista exacta (**NO AUTORIZABLE**) |
| F7 | VER-02 |
| F8 | VER-12 (modalidades distintas de la refacturación); lista exacta |

## 14. Consecuencias

- **Positivas:** reservas auditables y protegidas en un único punto; facturación sin motores paralelos; trazabilidad
  completa pedido→factura→entrega; indicadores consistentes por derivación; ventas directas intactas; una sola baseline.
- **Costos:** extensiones mínimas de módulos FROZEN (DEC-01), tablas nuevas en Inventario, un guard en la autorización,
  una configuración por empresa nueva y un estado fiscal que depende del SRI para habilitar entregas.
- **Límites aceptados:** sin liberación automática de reservas ni de selecciones; sin cotizaciones; un pedido Cerrado no
  se reabre; la regularización se limita a las modalidades con contrato verificable.
- **Reversibilidad:** cada fase declara sus condiciones de rollback; Facturación conserva su flujo directo independiente.
- **No cambia:** facturación física/electrónica, SRI, Kardex, CxC, contabilidad, anulación y devolución.

## 15. Condiciones para permitir implementación

1. **Cumplida:** el propietario aprobó formalmente este ADR el 17/11/2026 (estado Accepted). Esto no autoriza ninguna fase.
2. Se completa la **Fase 0** (VER-01..VER-14) con evidencia, incluida la demostración de TP-16.
3. Se resuelve **VER-11** antes de F3 (OPS-01 ya está resuelta).
4. **Registro cumplido** (17/11/2026) en `STATUS.md` y `WORK_QUEUE.md` (F0 PENDING / NO AUTORIZADA). El propietario autoriza **cada fase por
   separado**. Antes de implementar se exige una **lista exacta de rutas** (existentes autorizables, nuevas previstas,
   protegidas y candidatas), **revisada y aprobada por el propietario**; ningún patrón amplio equivale a esa autorización
   y DEC-01 no es una reapertura ilimitada.
5. Toda fase con cambios de esquema pide además la **autorización específica de regeneración de la baseline única**
   (DEC-06), probada en base PostgreSQL desechable.
6. Antes de crear UI: auditoría de reutilización (plantillas revisadas, componentes a reutilizar y justificación).
7. Cada fase cierra con pruebas focalizadas (PostgreSQL real para concurrencia/scope/locks), guardrails de la fase,
   gate del propietario, staging y commit del propietario ([flujo por fase](../architecture/enforcement.md#flujo-obligatorio-del-agente-por-fase)).

## Referencias

[Especificación](PEDIDOS-VENTA-V1-FUNCTIONAL-SPEC-01.md) · [Plan](../agent/PEDIDOS-VENTA-V1-IMPLEMENTATION-PLAN-01.md) ·
[Inventario](../architecture/inventory.md) · [Backend](../architecture/backend.md) · [Seguridad](../architecture/security.md) ·
[Frontend](../architecture/frontend.md) · [Infraestructura congelada](../architecture/frozen-infrastructure.md) ·
[Datos](../architecture/data-standards.md) · [DATABASE](../DATABASE.md) · [ADR-019](ADR-019-document-sequence-infrastructure.md) ·
[ADR-022](ADR-022-audit-infrastructure-entity-vs-process.md) · [ADR-033](ADR-033-payment-term-ssot-and-document-schedules.md) ·
[ADR-034](ADR-034-temporal-contract-single-source.md) · [ADR-040](ADR-040-item-company-scope-inventory-nature.md) ·
[ADR-041](ADR-041-negative-sale-inventory-cost-obligations.md) · [ADR-043](ADR-043-background-processing-ssot.md) ·
[ADR-046](ADR-046-historical-handler-size-debt.md).

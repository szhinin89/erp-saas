# PEDIDOS-VENTA-V1 — Especificación funcional: Gestión de Ventas → Orden de Pedido V1

**Estado: ADR-049 ACCEPTED (aprobación formal del propietario, 17/11/2026) — diseño aprobado, NO implementado; F0 PENDING / NO AUTORIZADA.**
Diseño propuesto. **Nada de lo descrito está implementado ni autorizado para implementar.** Las decisiones DEC-01 a
DEC-06 ya están **aprobadas por el propietario** y consolidadas aquí, y el ADR-049 está **Accepted**; la aceptación no autoriza programar. No es una SSOT: la
SSOT funcional del módulo nace cuando exista código y pruebas
([SSOT por módulo](../architecture/enforcement.md)). Documentos hermanos:
[ADR-049](ADR-049-sales-order-v1-architecture.md) (arquitectura) y
[plan de implementación](../agent/PEDIDOS-VENTA-V1-IMPLEMENTATION-PLAN-01.md) (fases).
Estado de entrega: [STATUS](../../STATUS.md). Cola de trabajo: [WORK_QUEUE](../agent/WORK_QUEUE.md) (registra F0 como PENDING / NO AUTORIZADA).

## 0. Convenciones de este documento

**Jerarquía de autoridad** (encargo del propietario, 2026-11-17): (1) reglas funcionales aprobadas, (2) contratos
oficiales y restricciones del repositorio, (3) evidencia comprobada en código, (4) propuestas técnicas pendientes.
Los dos informes técnicos previos fueron material de investigación, no decisiones.

| Etiqueta | Significado |
|---|---|
| **[APROBADO]** | Regla `PV-*` enumerada en el encargo del propietario. |
| **[VERIFICADO]** | Comportamiento existente comprobado en el repositorio (se cita archivo y líneas). |
| **[PROPUESTA TP-xx]** | Decisión técnica del [ADR-049](ADR-049-sales-order-v1-architecture.md) pendiente de aprobación. |
| **[DECIDIDO DEC-xx]** | Decisión del propietario ya **aprobada** (DEC-01 a DEC-06). No es una decisión pendiente. |
| **[RESUELTO OPS-xx]** | Definición operativa del propietario ya aprobada (OPS-01). Si surgiera otra aún sin definir se marcaría **[PENDIENTE OPS-xx]**. |
| **[BLOQUEADO]** | No se puede implementar todavía por una verificación, una definición operativa o una lista de rutas pendiente. |
| **[VERIFICAR VER-xx]** | Contrato técnico pendiente: se comprueba con evidencia en la Fase 0 del plan; **no es una nueva decisión comercial**. |

Se distinguen: la **decisión aprobada** (comportamiento ya definido), el **contrato técnico pendiente** (algo que hay que
comprobar en código o infraestructura) y el **bloqueo de implementación** (una fase que aún no puede ejecutarse).

Los identificadores `DEC-*` (decisiones aprobadas), `OPS-*` (definiciones operativas; OPS-01 resuelta), `TP-*` (propuestas),
`AMB-*` (interpretaciones conservadoras), `VER-*` (verificaciones) y `ESC-*` (escenarios) están definidos en el
[Anexo B](#anexo-b--decisiones-definiciones-operativas-ambigüedades-y-verificaciones).

**Glosario de cantidades.** Todas las cantidades se expresan **en unidad base** (`QuantityInBaseUom`); la
presentación solo se muestra. **Existencia física** = `CurrentStock.Quantity` (proyección del Kardex).
**Disponible general** = existencia física − reservado total de todos los pedidos.

| Símbolo | Nombre | Fuente de verdad |
|---|---|---|
| **Q** | Solicitada | Línea del pedido |
| **R** | Reserva vigente | Libro de operaciones de reserva (Σ reservado + completado − liberado − consumido) |
| **P** | Preparada (neta) | Libro de preparación (Σ preparado − revertido) |
| **S** | Seleccionada para facturar | Vínculos pedido↔factura en estado `Selected` |
| **I** | Facturada **vigente** | Vínculos `Consumed` cuya factura está `Authorized` internamente y **no anulada** |
| **Ih** | Facturada histórica | Todos los vínculos `Consumed`, incluidos los de facturas anuladas (nunca se borran; DEC-05) |
| **D** | Entregada | Libro de entregas |
| **X** | Cancelada | Libro de cancelaciones |
| **Dv** | Devuelta (derivada) | Devoluciones `Authorized` sobre líneas de factura vinculadas (`OriginalInvoiceDetailId`) |

## 1. Objetivo y alcance

Permitir que Ventas capture el compromiso comercial con un cliente (**Orden de Pedido**), proteja el inventario
comprometido, registre preparación y entrega, y genere borradores de **Factura de Venta** por selección parcial o
total, sin alterar los procesos oficiales de facturación, SRI, Kardex, Cuentas por Cobrar ni Contabilidad.

**Entra en V1:** Orden de Pedido y el menú/pantalla **Gestión de Ventas** (título permanente; único documento
disponible en V1). **Quedan fuera [APROBADO]:** Cotizaciones, CRM, campañas, oportunidades, otros tipos de
documento, opciones futuras deshabilitadas, y cualquier cambio de comportamiento de las opciones actuales de
Facturación. La pantalla de Factura de Venta (`/sales`) sigue siendo la existente y **no se renombra**.

## 2. Usuarios y permisos (PV-SEC-01)

Autorización real en backend con `[Authorize(Policy = "perm:...")]` y los controles tenant/empresa/sucursal vigentes
([Seguridad](../architecture/security.md#security--access-contract-v1--locked)); la UI solo oculta por conveniencia.

| Capacidad [APROBADO] | Clave técnica **[PROPUESTA TP-12]** | Operaciones que habilita |
|---|---|---|
| Consultar | `sales.orders.view` | Lista, detalle, historial, indicadores |
| Crear | `sales.orders.create` | Crear pedido en borrador |
| Modificar borradores | `sales.orders.update` | Editar/cancelar un pedido en Borrador |
| Confirmar | `sales.orders.confirm` | Confirmar (dispara la reserva automática) |
| Modificar confirmados | `sales.orders.amend` | Editar pedido Confirmado con motivo |
| Completar reservas | `sales.orders.reserve.complete` | Reservar faltantes manualmente |
| Liberar reservas | `sales.orders.reserve.release` | Liberación manual parcial/total |
| Extender vencimientos | `sales.orders.reserve.extend` | Extender la fecha de vencimiento |
| Preparar | `sales.orders.prepare` | Preparar y revertir preparación |
| Registrar entrega | `sales.orders.deliver` | Registrar entrega física |
| Cancelar | `sales.orders.cancel` | Cancelar cantidades pendientes / pedido |
| Preparar facturación | `sales.orders.invoice` | Seleccionar cantidades y crear el borrador de factura |
| Liberar una selección (**DEC-04**) | `sales.orders.selection.release` | Liberar manualmente la selección de un borrador abandonado |
| Regularizar incidencias (**DEC-05 C1**) | `sales.orders.incidents.resolve` | Registrar la regularización de una entrega sin respaldo fiscal |

- Las dos últimas filas son **claves propuestas adicionales** a las doce del encargo (catorce en total); **no se modifica
  el catálogo real de permisos** hasta que el propietario autorice la fase correspondiente.
- La configuración de la vigencia de reservas por empresa (DEC-03) reutiliza los permisos existentes
  `settings.operations.view/configure` [VERIFICADO: `OperationalPreferencesPermissions.cs`]; no se crea una clave nueva.
- La convención del repositorio es `modulo.recurso.accion` (p. ej. `inventory.adjustments.confirm`
  [VERIFICADO: `InventoryPermissions.cs`]). Los nombres definitivos los fija el propietario al aprobar el ADR.
- **Emitir la factura sigue exigiendo los permisos oficiales de Ventas** (`sales.create`, `sales.update`,
  [VERIFICADO: `SalesPermissions.cs`, `SalesController.cs`]) más caja abierta. El endpoint de creación del borrador
  desde pedido exige `sales.orders.invoice` **y** `sales.create` **[PROPUESTA TP-06]**.
- `sales.update` **no** concede ninguna operación de pedidos.
- Ninguna clave nueva reutiliza una existente; se registran en el catálogo del kernel y en el menú server-driven
  (`KernelRegistry`, `[NavItem]`), y se asignan a perfiles por empresa.

## 3. Pantallas

**Alcance de aprobación de este capítulo:** solo son **[APROBADO]** el nombre y título permanente **Gestión de
Ventas**, que Orden de Pedido sea el único documento de V1 y que la pantalla de Factura de Venta siga siendo la
existente (encargo §1). La disposición de pantallas, rutas, pestañas, columnas y modales de §3–§5 es **[PROPUESTA
TP-13]** y no está aprobada.

### 3.1 Navegación (PV-NAV-01) [APROBADO] / rutas **[PROPUESTA TP-13]**

Módulo **Ventas** → grupo **Ventas** → opción permanente **Gestión de Ventas** (`/sales/orders`). No se renombran ni
se mueven `Facturas y punto de venta` (`/sales`) ni `Devoluciones de venta` (`/sales/returns`)
[VERIFICADO: `SalesModule.cs`]. Se agrega un `[NavItem]` con `Id` explícito nuevo, `Permission =
sales.orders.view`, `LabelKey = app.nav.item.sales.orders` y las demás claves como acciones relacionadas.
Detalle/alta/edición (`/sales/orders/new`, `/sales/orders/:id`) heredan el permiso del padre
(`RouteAccessGuard`, [Seguridad](../architecture/security.md#security--access-contract-v1--locked)).
No se crean pantallas ni ítems de menú vacíos o deshabilitados para documentos futuros (PV-FUT-01).

### 3.2 Listado — `/sales/orders`

Título permanente **Gestión de Ventas**; la tabla lista **Órdenes de Pedido**. Reutiliza `PageShell`/`TableCard`/
`ZHDataTable`/`ZHMoneyValue`/`ZHBtn` del Design System. Columnas: N.º de pedido, Fecha, Cliente, Estado
(Borrador/Confirmado/Cerrado/Cancelado), indicadores (Reserva, Preparación, Facturación, Entrega), Total referencial,
Alertas (reserva vencida, entrega sin respaldo fiscal). Filtros: estado, cliente, rango de fechas (`DateOnly`),
indicador. Acción principal: **Nueva orden de pedido** (`sales.orders.create`).

### 3.3 Formulario — alta y edición de borrador

Cabecera + líneas. Reutiliza `CustomerPicker`/`customerPickerFacade` y la búsqueda de ítems de Ventas
(`GET /api/v1/sales/item-search`: barcode/SKU, stock por bodega, precio resuelto)
[VERIFICADO: `SalesController.cs` `SearchItems`; `frontend.md` § Selección manual de Item]. Patrón canónico de formularios:
`useForm` + `zodResolver` + `FormProvider` + `applyServerErrors`
([Validación de formularios](../architecture/form-validation.md)). Decimales con `ZhDecimalInput` y punto decimal.

### 3.4 Detalle — pedido Confirmado / Cerrado / Cancelado

Pestañas (patrón de más de dos pestañas, `frontend.md` § Formularios de entidad): **Resumen**, **Líneas y reservas**,
**Preparación y entrega**, **Facturación**, **Historial**. Barra de acciones según estado y permiso (§5). Solo
lectura en Cerrado y Cancelado.

### 3.5 Modales (modal estándar `ZHModal`, [modal-standard](../architecture/modal-standard.md))

Confirmar pedido · Motivo (modificar, liberar, cancelar, revertir preparación) · Completar reserva · Extender
vencimiento · Preparar · Registrar entrega · Seleccionar cantidades a facturar. Reutilización de un componente de
motivo existente: se audita en la Fase 1 (regla de reutilización previa, `frontend.md`).

### 3.6 Factura de Venta con origen en pedido (PV-FAC-02) [APROBADO]

La pantalla oficial `/sales` recibe el borrador por `?invoiceId=` (mecanismo existente,
[VERIFICADO: `SalesPage.tsx` líneas 67-75]). Si la factura tiene origen pedido: banner «Origen: Pedido N.º …» con
enlace **Volver al pedido**; cliente, líneas, cantidades, presentaciones, precios, descuentos, bodegas y condiciones
comerciales en **solo lectura**; solo se completan los datos legales, fiscales y de cobro permitidos (§12.2). La
edición habitual de facturas de venta directa **no cambia**.

## 4. Campos

Los campos comerciales enumerados por PV-COM-01 son **[APROBADO]** (cliente, productos y servicios, cantidades y
presentaciones, precios, descuentos, bodega, condiciones comerciales y de pago, fechas y observaciones). Los nombres,
tipos, restricciones y tablas de este capítulo son **[PROPUESTA TP-01/TP-02/TP-17]**; las interpretaciones están en
AMB-01 a AMB-03.

### 4.1 Cabecera `SalesOrder`

| Campo | Tipo / regla |
|---|---|
| `TenantId`, `CompanyId`, `BranchId` | Obligatorios, desde el contexto; `BranchId` nunca viene del cliente ni cambia ([Branch Ownership Rule](../architecture/architecture.md#branch-ownership-rule-obligatoria)) |
| Número de pedido | Correlativo interno **[VERIFICAR VER-01]** (mecanismo de numeración no comprobado) |
| Fecha del pedido | `DateOnly` (día de la empresa, `ICompanyClock`); [ADR-034](ADR-034-temporal-contract-single-source.md) |
| Cliente | `BusinessPartner` activo con rol cliente; se guarda `CustomerId` + nombre de presentación |
| Condición de pago | Snapshot `PaymentTermSnapshot` resuelto con el mismo servicio de borradores de factura ([ADR-033](ADR-033-payment-term-ssot-and-document-schedules.md)); AMB-02 |
| Observaciones | Texto libre acotado |
| Estado | `Borrador`/`Confirmado`/`Cerrado`/`Cancelado` (§6) |
| Confirmado por/el | Auditoría de confirmación |
| `RowVersion` | Concurrencia optimista |

Vigencia de reservas (**DEC-03 aprobada**): `expires_on` y los días aplicados se calculan al confirmar y quedan registrados (§10.3). La key de configuración por empresa es **[VERIFICAR VER-11]** ; sin vigencia configurada, Confirmar se rechaza (**OPS-01 resuelta**, §10.3); no se define aquí ningún valor.

### 4.2 Línea `SalesOrderLine`

| Campo | Regla |
|---|---|
| Ítem | Activo y de la empresa (ADR-040); naturaleza Producto/Servicio |
| Descripción | Snapshot de presentación |
| Bodega | Obligatoria para Producto con inventario; **prohibida** para Servicio; debe ser válida para empresa+sucursal (`SalesWarehouseGuard`) |
| Presentación, unidad, factor | Resueltas con `SalesLinePackagingResolver`; base = cantidad × factor |
| Cantidad y cantidad base | `numeric(20,6)`; > 0 |
| Precio unitario | `numeric(22,10)`; ≥ 0; resuelto con `IPricingResolver` (ADR-021); VER-03 |
| Descuento % | `numeric(9,6)`; 0–100 |
| Códigos IVA/ICE | Referencias del catálogo oficial; **no** se copian tarifas ni se calculan impuestos legales (TP-14) |
| Observaciones | Texto libre acotado |

### 4.3 Registros de operación (libros) [PROPUESTA TP-02, TP-08, TP-17]

Reserva, preparación, entrega, cancelación y vínculos con factura son **registros append-only** con: línea de
pedido, cantidad base (> 0), usuario, instante UTC, motivo (cuando aplica), `ClientRequestId` (TP-10) y, en
vínculos y entregas, la factura y la línea de factura relacionadas. Los indicadores se **calculan** a partir de
estos registros y de los documentos reales; no existen contadores independientes (TP-08).

## 5. Acciones por estado y permiso

| Acción | Estado requerido | Permiso | Precondición adicional |
|---|---|---|---|
| Guardar/editar borrador | Borrador | `create`/`update` | Validaciones de §8 |
| Confirmar | Borrador | `confirm` | ≥ 1 línea; validaciones de §8; vigencia configurada (OPS-01: sin ella se rechaza con mensaje, §10.3); reserva automática (PV-RES-01). Guardar y editar el Borrador **no** la exige |
| Cancelar borrador | Borrador | `update` | Motivo; sin DELETE físico |
| Modificar pedido confirmado | Confirmado | `amend` | Motivo obligatorio; reglas §16 |
| Completar reserva | Confirmado | `reserve.complete` | Faltante > 0 y disponible > 0 |
| Liberar reserva | Confirmado | `reserve.release` | Motivo; sin consumir lo ya facturado; reconciliar preparación |
| Extender vencimiento | Confirmado | `reserve.extend` | Fecha futura |
| Preparar / revertir preparación | Confirmado | `prepare` | Solo cantidades comprometidas |
| Registrar entrega | Confirmado | `deliver` | Solo cantidades facturadas y legalmente habilitadas |
| Cancelar cantidades pendientes | Confirmado | `cancel` | Motivo; sin cantidades facturadas ni seleccionadas |
| Preparar facturación | Confirmado | `invoice` + `sales.create` | Seleccionable > 0; caja abierta |
| Abrir factura del pedido | Confirmado/Cerrado | `view` (+ permisos de Ventas) | Navega a `/sales?invoiceId=` |
| Liberar selección (**DEC-04**) | Confirmado | `selection.release` | Vínculo `Selected`, factura en Borrador, motivo obligatorio (§12.5) |
| Regularizar incidencia (**DEC-05 C1**) | Confirmado | `incidents.resolve` | Evidencia verificable (§15.3); no disponible en pedidos Cerrados |

## 6. Estados y transiciones (PV-EST-01)

Estado comercial principal **[APROBADO]**: Borrador, Confirmado, Cerrado, Cancelado. **No se introducen estados nuevos.**
Los indicadores de Reservas, Preparación, Facturación y Entrega son independientes y calculados (§9). Las transiciones
de la tabla son **[PROPUESTA TP-11]**, condicionadas por **DEC-05 (A1 + B1 + C1)**.

| Desde | Hacia | Condición exacta | Efecto |
|---|---|---|---|
| (nuevo) | Borrador | Alta válida | Sin reserva, sin efecto en inventario |
| Borrador | Confirmado | Confirmación válida **y vigencia de reservas configurada** (DEC-03) | Compromiso comercial + reserva automática con su vencimiento calculado; idempotente |
| Borrador | Borrador (rechazo) | Confirmar con la empresa **sin vigencia configurada** (**OPS-01 resuelta**) | Se rechaza **sin efectos**: no cambia el estado, no registra reservas, no genera Kardex, factura, CxC ni contabilidad; el mensaje remite a Preferencias operativas. Al configurarla se puede volver a confirmar y se revalidan configuración e inventario |
| Borrador | Cancelado | Motivo | Sin efectos; el registro se conserva |
| Confirmado | Cancelado | `X = Q` en todas las líneas, `S = 0`, `D = 0`, preparación neta 0 **y `Ih = 0`** (ningún vínculo `Consumed`, vigente o de factura anulada) | Libera reservas asociadas; nada facturado nunca. Un pedido con facturación histórica **nunca** pasa a Cancelado: si cumple la condición de cierre pasa a Cerrado |
| Confirmado | Cerrado | Condición de cierre (abajo) | Pedido terminado; solo lectura |
| Cerrado, Cancelado | — | Estados terminales en V1 | **Un pedido Cerrado conserva su estado (DEC-05 B1):** no se reabre automáticamente, no se crean cantidades facturables ni reservas; las anulaciones y devoluciones posteriores quedan visibles en el historial, con incidencia cuando corresponda (§15). El cierre histórico no se borra ni se reinterpreta |

**Condición exacta de cierre (Confirmado → Cerrado)** **[PROPUESTA TP-11; AMB-04]** — se evalúa tras cada operación bajo
el lock del pedido, con las derivadas del §9.1:
1. Para toda línea: `Q = I + X` (con `I` vigente; saldo facturable 0; implica `S = 0` y `R = 0`). Si una factura se anula
   mientras el pedido sigue Confirmado, sus cantidades vuelven al saldo (A1) y el pedido no cierra hasta facturarlas o
   cancelarlas.
2. Para toda línea de Producto: `E = 0` (suma por línea de factura vinculada; todo lo facturado y habilitado, neto de
   devoluciones, ya se entregó), `Pp = max(0, P − D) = 0` (no queda preparación sin entregar ni revertir) y `Esr = 0`.
3. Sin vínculos en estado `Selected`.
4. Sin incidencia de entrega sin respaldo fiscal **sin resolver** (`Esr > 0`; **DEC-05 C1, regla 5**, §15).

Caso **facturación parcial + cancelación del saldo** [APROBADO]: `Q = 20`, `I = 8`, `X = 12`, entregas completas ⇒
**Cerrado**, y el pedido conserva ambos resultados (nunca pasa a Cancelado porque existe un vínculo `Consumed`).
Las líneas de **Servicio** no tienen reserva, preparación ni entrega; cuentan solo para la condición 1.

**Coherencia entre estados (tabla de verificación):**

| Situación | Estado resultante |
|---|---|
| Confirmado, `Pf > 0` o entregas pendientes o preparación sin entregar | Confirmado |
| Confirmado, todo facturado, cancelado el resto, entregas completas | Cerrado (ESC-06) |
| Confirmado, factura vinculada anulada (A1) | Confirmado: `Pf` aumenta por lo anulado; las reservas consumidas **no** se restablecen; no se crea factura alguna (ESC-10) |
| Confirmado, devolución posterior sobre lo entregado (`Dv > 0`, `E = 0`) | Confirmado hasta cumplir la condición; la devolución no abre saldo (ESC-11) |
| Confirmado, anulación que deja `Esr > 0` | Confirmado, con incidencia visible; no cierra |
| Cerrado, anulación o devolución posterior | **Cerrado conservado** (B1): `Ih` e `I` se muestran por separado, sin saldo ni reservas nuevos; incidencia si `Esr > 0` (ESC-17) |
| Cancelado | No tiene facturas vinculadas; no admite anulación ni devolución |

## 7. Reglas de negocio — índice

| Regla | Sección | Regla | Sección |
|---|---|---|---|
| PV-COM-01 | §7.1 | PV-ENT-01 | §13 |
| PV-RES-01 a PV-RES-05 | §10 | PV-EDI-01 | §16 |
| PV-PREP-01 | §11 | PV-CAN-01 | §14 |
| PV-FAC-01 a PV-FAC-05 | §12 | PV-REC-01 | §15 |
| PV-EST-01 | §6 y §9 | PV-SEC-01 | §2 |
| PV-NAV-01 | §3.1 | PV-FUT-01 | §7.2 |

### 7.1 PV-COM-01 — Gestión comercial [APROBADO]

- **Precondiciones:** sesión con empresa y sucursal activas; permiso `create`/`update`.
- **Entradas:** cliente, líneas (ítem, presentación, cantidad, bodega, precio, descuento), condición de pago, fecha,
  observaciones. **Nunca** `TenantId`, `CompanyId` ni `BranchId` (contexto del servidor).
- **Validaciones:** §8.
- **Efectos permitidos:** persistir el pedido en Borrador **sin reservar existencias**; editar libremente mientras
  sea Borrador. La **confirmación** establece el compromiso comercial.
- **Efectos prohibidos:** reservar, generar Kardex, factura, CxC o contabilidad al guardar un borrador; calcular
  impuestos legales (los fija la factura al autorizar; TP-14).
- **Errores:** §17. **Concurrencia:** `RowVersion` en la edición; conflicto ⇒ 409 con recarga.
- **Pruebas:** AT-COM-01 a AT-COM-03 (F1).

### 7.2 PV-FUT-01 — Evolución futura [APROBADO]

Los límites de dominio se diseñan para reutilizar cabecera, líneas y componentes de captura con otros documentos
comerciales **sin** implementarlos: el agregado se llama `SalesOrder`, no existe enum, tabla, columna, ruta ni menú
de tipos de documento futuros, y la pantalla no contiene selectores ni pestañas vacías. Verificación: AT-FUT-01.

## 8. Validaciones transversales

| # | Validación | Capa |
|---|---|---|
| V-01 | Cliente activo y del tenant (`BusinessPartner`) | Application + Domain |
| V-02 | Ítem activo, `item.CompanyId` = empresa activa (ADR-040) | Application |
| V-03 | Producto con inventario ⇒ bodega obligatoria; Servicio ⇒ sin bodega | Application + Domain |
| V-04 | Bodega de la empresa y de la sucursal activa (`SalesWarehouseGuard`) | Application |
| V-05 | Cantidad > 0 con precisión de política; precio ≥ 0; descuento 0–100 | FluentValidation + Domain + CHECK |
| V-06 | Fecha de negocio `DateOnly` ("YYYY-MM-DD"); instantes UTC | ADR-034 |
| V-07 | Aislamiento tenant/empresa/sucursal en toda lectura y escritura; fila fuera de alcance ⇒ 404 | Backend + filtros globales |
| V-08 | Operaciones de cantidad con `ClientRequestId` (`Guid` obligatorio) | Application + índice único |
| V-09 | Sin DELETE físico: se cancela, se desactiva o se registra reversa | Domain |
| V-10 | Validación en cuatro capas (frontend Zod, FluentValidation, Domain, BD) | [Enforcement](../architecture/enforcement.md) |

## 9. Cálculos, fórmulas e invariantes

### 9.1 Cantidades derivadas

Siempre calculadas, nunca almacenadas como verdad. Se definen **por vínculo** `v` (línea de pedido ↔ línea de factura,
cantidad base `q_v`, estado `Consumed`) y se suman por línea de pedido `L`:

| Concepto | Fórmula |
|---|---|
| Facturada **vigente** | `I_v = q_v` si la factura está `Authorized` y no anulada; `I = Σ I_v` |
| Facturada **histórica** | `Ih = Σ q_v` de todos los vínculos `Consumed`; **anulada** `Ia = Ih − I`. Los vínculos nunca se borran (DEC-05 A1) |
| Habilitada para entrega | `H_v = q_v` si la factura está habilitada legalmente (§13); `I_hab = Σ H_v` |
| Devuelta | `Dv_v` = devoluciones `Authorized` sobre la línea de factura (`OriginalInvoiceDetailId`); `Dv = Σ Dv_v` |
| Entregada (histórica) | `D_v` = entregas registradas contra `v`; `D = Σ D_v`. **Hecho histórico: nunca se recalcula ni se revalida** |
| Regularizada | `Rg_out_v` = parte de `D_v` regularizada contra otra factura vigente; `Rg_in_v` = entregas históricas de otros vínculos regularizadas contra `v` (registros de §15.3) |
| Pendiente de facturar | Pedido **Confirmado**: `Pf = Q − X − I` (la cantidad de una factura anulada vuelve al saldo, DEC-05 A1). Pedido **Cerrado** o Cancelado: `Pf = 0` (DEC-05 B1: no se crean cantidades facturables) |
| Seleccionable — Producto reservable | `Sel = R − S` |
| Seleccionable — Producto no reservable y Servicio | `Sel = Pf − S` (no hay reserva que exigir; **AMB-09**). Producto no reservable: regla sujeta a **VER-13** y a DEC-02 |
| Faltante de reserva (pendiente de abastecimiento) | Confirmado: `F = Q − X − I − R`; Cerrado: `F = 0` |
| Preparado pendiente de entrega | `Pp = max(0, P − D)` |
| Entregable restante por vínculo | `E_v = max(0, H_v − Dv_v − D_v − Rg_in_v)`; `E = Σ E_v` |
| Entrega sin respaldo fiscal | `Esr_v = max(0, D_v − H_v − Rg_out_v)`; `Esr = Σ Esr_v` (la entrega histórica supera lo hoy habilitado y no está regularizada) |
| Total referencial del pedido | `Σ importes de líneas de factura vinculadas vigentes + Σ (saldo × precio vigente × (1 − desc%))`, sin impuestos; el saldo editable cumple `S = 0` |

**Reporte del pedido:** se muestran **ambos hechos sin contradicción**: lo históricamente facturado (`Ih`) y el estado
vigente de sus comprobantes (`I`, `Ia`, `H`).

### 9.2 Invariantes de estado

Los mantienen las **operaciones del pedido**; no obligan a modificar hechos que ocurren en otros módulos. Se verifican
bajo lock y con PostgreSQL real.

| ID | Invariante |
|---|---|
| INV-01 | `Q, R, P, S, I, Ih, D, X ≥ 0` y `Ih ≥ I` |
| INV-02 | `I + S + X ≤ Q` |
| INV-03 | `R ≤ Q − X − I` (una anulación solo amplía el lado derecho; nunca fuerza cambios en `R`) |
| INV-04 | `S ≤ R` en líneas reservables (el consumo reduce `R` y `S` por igual) |
| INV-07 | `CurrentStock.ReservedQuantity = Σ R vigente` de la clave (empresa, producto, bodega) |
| INV-09 | Preparar, entregar, cancelar, reservar, liberar y regularizar **nunca** crean movimientos de Kardex, CxC ni contabilidad desde el pedido |
| INV-10 | Cada línea de factura autorizada **con producto y bodega** genera exactamente un `SaleExit` (proceso oficial); los servicios no generan ninguno |
| INV-11 | **Definición de `I`:** solo facturas `Authorized` no anuladas. `Ih` conserva todo lo históricamente facturado. En un pedido Confirmado lo anulado vuelve al saldo (A1) sin restablecer reservas |
| INV-12 | Un vínculo `Selected` pasa **una sola vez** a `Consumed` o `Released`; ambos estados son finales |
| INV-13 | Branch del pedido = branch de las bodegas de sus líneas = branch de la factura |
| INV-14 | Autorización interna de la factura ≠ autorización electrónica del SRI: `I` cuenta lo primero; `H` exige lo segundo cuando la emisión es electrónica |
| INV-16 | Un vínculo `Released` **no se reactiva** y su borrador **no puede autorizarse jamás** (DEC-04); una factura con origen solo se autoriza si su vínculo está `Selected` en la misma unidad de trabajo |

### 9.3 Reglas de admisión de operaciones nuevas

Se evalúan **antes** de la operación, con los valores vigentes («antes»); **no** reevalúan hechos históricos.

| ID | Regla de admisión |
|---|---|
| INV-05 | **Preparar `q`:** `0 < q` y `P + q ≤ I + R`. **Toda operación que reduzca `R` o la base comprometida** (liberar, cancelar, bajar `Q`) exige además `P ≤ I + R_después` (§11.2). Una desviación causada por un hecho externo (p. ej. una anulación) no se corrige sola: bloquea nuevas preparaciones y esas reducciones hasta revertir preparación |
| INV-06 | **Entregar `q` contra el vínculo `v`:** `0 < q ≤ E_v(antes)`. No se compara con totales acumulados ni se revalidan entregas pasadas |
| INV-08 | **Egreso ajeno al pedido que retira `q`:** `Quantity_after = Quantity_before − q` debe cumplir `Quantity_after ≥ ReservedQuantity` cuando hay reservas comprometidas; si no las hay, rigen las reglas oficiales (incluida la venta sin stock de ADR-041). Excepción: consumo propio (§9.5, DEC-02) |
| INV-15 | **Seleccionar `q`:** `0 < q ≤ Sel`. **Liberar reserva `q`:** `0 < q ≤ R − S`. **Cancelar `q`:** `0 < q ≤ Pf − S`. **Editar `Q`:** `Q_nuevo ≥ I + S + X`. **Liberar selección:** vínculo `Selected` y factura en Borrador (DEC-04) |
| INV-17 | **Reservar o completar `q`:** `0 < q ≤ F` y `q ≤ Free = Quantity − ReservedQuantity` (nunca reservas negativas ni sobre lo comprometido; DEC-02 reglas 1-2) |

### 9.4 Efecto de anulaciones y devoluciones sobre las derivadas

| Derivada | Anulación legal de una factura | Devolución autorizada |
|---|---|---|
| `Q`, `X`, `R`, `S` | Sin cambio (las reservas **no** se reabren ni se restablecen) | Sin cambio |
| `Ih` | Sin cambio (histórico conservado; el vínculo `Consumed` permanece) | Sin cambio |
| `I` | Baja: la factura anulada deja de contar; `Ia` sube | Sin cambio |
| `Dv` | — | Sube |
| `D` | Sin cambio (hecho histórico) | Sin cambio |
| `H` / `I_hab` | Baja a 0 para esa factura | Sin cambio |
| `E` | Baja a 0 para sus vínculos | Baja (`− Dv`); no habilita entregas adicionales (AMB-05) |
| `Esr` | Sube si `D_v > 0` ⇒ **incidencia visible**; impide el cierre | Sin cambio |
| `P` | Sin cambio; puede quedar `P > I + R` ⇒ INV-05 bloquea nuevas preparaciones y reducciones de reserva hasta revertir | Sin cambio; la preparación no entregada tras una devolución mantiene `Pp > 0` hasta revertirla |
| `Pf` y `F` | **Confirmado:** `Pf` sube por lo anulado (A1) y `F` con él; la facturación posterior exige cantidades elegibles y, cuando corresponde, reservas suficientes. **Cerrado:** sin cambio (B1) | Sin cambio (la devolución no crea saldo ni obligación) |
| Estado del pedido | Confirmado se conserva; **Cerrado se conserva** (B1) | Ídem |
| Kardex, CxC y contabilidad desde el pedido | Ninguno (los emite el flujo oficial de anulación) | Ninguno (los emite el flujo oficial de devolución) |

### 9.5 Protección de la existencia física comprometida (DEC-02)

**Se protege la existencia física comprometida, no solo el contador de reservas.** Regla aprobada: toda operación de
egreso ajena al pedido **rechaza** su ejecución cuando deja la existencia física por debajo de la reserva vigente de otros
pedidos. Para una operación ajena que retira `q`: `Quantity_after = Quantity_before − q` y debe cumplirse
`Quantity_after ≥ ReservedQuantity`. Se evalúa en `StockRepository.AppendMovementAsync` → `ApplyKardexMovement` **bajo el
lock del recurso** ([ADR-049 §5.1](ADR-049-sales-order-v1-architecture.md)), no como un precheck previo que pueda fallar
bajo concurrencia. **Propuesta TP-04; extensión mínima autorizable por fase, no implementada; contrato técnico pendiente
VER-13.**

| Caso | Cómo se reconoce | Regla |
|---|---|---|
| 1. Consumo autorizado de reserva propia | `SaleExit` de la autorización oficial de una factura con origen pedido, con su línea de pedido y su vínculo `Selected` vigente | Reserva y existencia bajan **en la misma unidad de trabajo atómica**; registra `Consumir`; debe seguir cumpliéndose `Quantity_after ≥ ReservedQuantity_after` |
| 2. Egreso normal de existencia libre | Cualquier egreso sin consumo propio ni autorización de venta negativa | Permitido si `Quantity_after ≥ ReservedQuantity`; `SaleExit` con control activo conserva `AvailableQuantity ≥ c` (`c` = cantidad de la salida) |
| 3. Venta negativa legítima (ADR-041) | `SaleExit` con `authorizedNegativeSale` (control apagado o `AllowSellWithoutStock`) | **Sin reservas comprometidas:** permitida, la existencia puede quedar negativa (ADR-041). **Con reservas comprometidas:** solo si `Quantity_after ≥ ReservedQuantity`; nunca consume ni libera reservas ajenas. La valoración de ADR-041 no cambia |
| 4. Intento de consumir una reserva ajena | Egreso sin la autorización del caso 1 que deje `Quantity_after < ReservedQuantity` | **Rechazado** `SALES_ORDER_RESERVED_STOCK_PROTECTED` sin efectos |

**Compatibilidad con ADR-041 (sin modificarlo):** no es una regla global que prohíba los saldos negativos: mientras no
existan reservas comprometidas la venta sin stock oficialmente habilitada puede dejar existencia negativa. Con reservas
comprometidas, la venta externa no puede reducir la existencia física por debajo de ellas. Los servicios no reservan ni
generan `SaleExit`. Nunca se permite que una operación ajena **libere** una reserva: solo el pedido que la originó la
libera (PV-RES-04).

**Puntos de integración que necesitarán extensión mínima bajo DEC-01 (no implementados):**
1. `CurrentStock.ApplyKardexMovement` (`CurrentStock.cs` 63-83): hoy solo exige disponibilidad a la `SaleExit` con
   `enforceAvailability` y permite resultado negativo a la venta autorizada (línea 75), sin considerar `ReservedQuantity`.
2. `StockRepository.AppendMovementAsync`/`PendingMovement` (101-153): hoy no puede informar al punto central que una
   salida es **consumo propio** de una línea de pedido (parámetro opcional con valor por defecto).
3. `CurrentStock` recargada tras el lock (`LockStockResourceAsync`, 514-526): la comprobación debe usar esa lectura y no
   una previa.
Los prechecks existentes son preventivos; la defensa autoritativa es el punto central.

## 10. Reservas (PV-RES-01 a PV-RES-05)

### 10.1 PV-RES-01 — Reserva automática [APROBADO]

- **Precondiciones:** pedido en Borrador; permiso `confirm`; cada línea Producto con bodega válida; **vigencia de
  reservas configurada para la empresa** (DEC-03, §10.3). **OPS-01 resuelta:** si no está configurada, Confirmar se
  **rechaza antes de cualquier efecto** (validación obligatoria en backend, reflejada en la UI): no se registra ninguna
  reserva, no cambia el estado, no se genera Kardex, factura, CxC ni contabilidad, y no se asigna duración
  predeterminada ni se pide fecha manual. Crear, editar y guardar el Borrador siguen permitidos.
- **Entradas:** `ClientRequestId`. Nada más (las cantidades salen del pedido).
- **Elegibilidad (DEC-02 opción A; contrato técnico VER-13):**
  - **Producto reservable:** Producto con inventario, `inventory.stock_control_enabled` de la empresa **y**
    `StockControlEnabled` del ítem activos. `AllowSellWithoutStock` **no** decide la elegibilidad (rige las ventas, no
    las reservas) y la reserva queda protegida aunque una operación ajena pueda vender sin existencias.
  - **Producto no reservable** (control de disponibilidad apagado): `R = 0`; la confirmación no falla; se factura con
    las reglas de §12.1/§12.4.
  - **Servicio:** no reserva.
- **Validaciones (INV-17):** por línea, `R_línea = min(Q_línea, Free)`; nunca más que lo físicamente disponible y no
  comprometido; nunca reservas negativas.
- **Efectos permitidos:** bajo el lock del pedido y de cada recurso de stock (`(ítem, bodega)` en orden), registra la
  operación `Reservar` con la **fecha de vencimiento calculada**, actualiza `ReservedQuantity` y pasa el pedido a
  Confirmado; **la confirmación no se impide por stock insuficiente**; el faltante queda en `F`.
- **Efectos prohibidos:** reservar más que la disponibilidad real; reservar servicios; Kardex, factura, CxC,
  contabilidad; duplicar reservas al reconfirmar.
- **Errores:** `SALES_ORDER_INVALID_STATE`, `SALES_ORDER_LINE_NOT_RESERVABLE` (§17).
- **Concurrencia:** confirmación repetida o concurrente ⇒ el estado se relee bajo lock; si ya está Confirmado se
  responde el resultado original sin nuevas reservas. Dos pedidos que compiten por el mismo stock se serializan por
  el lock del recurso: `Σ R ≤ existencia`.
- **Pruebas:** AT-RES-01a a AT-RES-01g (F3), AT-CON-01.
- **Ejemplo obligatorio:** existencia 12, pedido 20 ⇒ existencia 12, reservado 12, pendiente de abastecimiento 8,
  disponible general 0 (ESC-01).

### 10.2 PV-RES-02 — Completar reservas [APROBADO]

- **Precondiciones:** pedido Confirmado; `F > 0`; permiso `reserve.complete`.
- **Entradas:** línea, cantidad a reservar (0 < cantidad ≤ `F`), `ClientRequestId`.
- **Validaciones:** se **revalida** la disponibilidad bajo lock (INV-17): `cantidad ≤ min(F, Free)`. El ingreso
  pertenece al proceso oficial de Inventario/Compras y **no reserva automáticamente**.
- **Efectos permitidos:** reserva parcial o total del faltante; registra usuario, instante y cantidad; **no modifica el
  vencimiento vigente** (AMB-11).
- **Efectos prohibidos:** reservar automáticamente lo recibido; Kardex; reservar más que `F` o que lo libre.
- **Errores:** `SALES_ORDER_RESERVATION_EXCEEDS_AVAILABLE`, `SALES_ORDER_RESERVATION_EXCEEDS_SHORTFALL`.
- **Concurrencia:** lock del pedido → lock del recurso; el perdedor reevalúa y recibe 422 sin efectos
  ([patrón oficial](../architecture/backend.md#saldos-compartidos-lock-pesimista-antes-de-decidir-zh-collections-receivable-concurrency-01)).
  Reintento con el mismo `ClientRequestId` devuelve el resultado original.
- **Pruebas:** AT-RES-02a a AT-RES-02c (F3; ESC-02).

### 10.3 PV-RES-03 — Vencimiento [APROBADO; DEC-03 opción B]

- **Configuración (DEC-03):** la vigencia inicial es **configurable por empresa, en días calendario**. Mecanismo
  propuesto **[TP-20]**: un `OrgSetting` por empresa declarado en el catálogo de configuración y leído por el resolver
  tipado de preferencias operativas ([backend.md § Configuración organizacional](../architecture/backend.md#configuración-organizacional));
  se administra desde `/settings/operations` con los permisos existentes `settings.operations.view/configure`. **No se
  crea un sistema paralelo de parámetros.** Sección, key, validador y rango de valores: **[VERIFICAR VER-11]**; no se
  define aquí ningún valor.
- **Cálculo al confirmar:** `expires_on = hoy_empresa + N días calendario`, con `hoy_empresa` de `ICompanyClock`
  (calendario oficial de negocio, ADR-034, alcance tenant/empresa) y `DateOnly`. La fecha calculada y los días aplicados
  quedan **registrados y auditables** en el pedido y en la operación de reserva.
- **Sin configuración (OPS-01, aprobada/resuelta):** «null o vacío = sin configurar» (contrato vigente). **No existe
  valor silencioso.** Confirmar se **rechaza** con el mensaje «No se puede confirmar el pedido porque esta empresa no
  tiene configurada la vigencia de las reservas. Configure los días de vigencia en Preferencias operativas e inténtelo
  nuevamente» (código de error según la convención oficial; propuesto `SALES_ORDER_RESERVATION_DAYS_NOT_CONFIGURED`).
  El pedido permanece en Borrador, sin reservas ni otros efectos; la validación es obligatoria en backend y se refleja en
  la interfaz (aviso en el modal de confirmación). Al configurarla, el usuario puede volver a confirmar y se
  **revalidan** la configuración y la disponibilidad de inventario en esa operación.
- **Estado de la reserva vencida:** continúa comprometida; la UI muestra advertencia (indicador calculado `Vencida` =
  `expires_on` < hoy de la empresa). **Prohibida la liberación automática por vencimiento** (no existe proceso
  recurrente).
- **Extender:** permiso `reserve.extend`; nueva fecha ≥ hoy y posterior a la vigente; aplica a las reservas vigentes del
  pedido (AMB-11); deja historial (usuario, fecha anterior, fecha nueva, instante). Motivo opcional (AMB-06).
- **Liberación:** sigue requiriendo autorización manual y motivo obligatorio (§10.4).
- **Errores:** `SALES_ORDER_RESERVATION_DATE_INVALID`. **Pruebas:** AT-RES-03a a AT-RES-03d (F3; ESC-13).

### 10.4 PV-RES-04 — Liberación [APROBADO]

- **Precondiciones:** pedido Confirmado; `R > 0`; permiso `reserve.release`.
- **Entradas:** línea, cantidad (0 < cantidad ≤ `R − S`), **motivo obligatorio**, `ClientRequestId`.
- **Validaciones:** nunca se libera lo ya consumido por facturación (`R` ya excluye lo consumido) ni lo que cubre una
  selección activa (`S`); **reconciliación de preparación** (§11.2).
- **Efectos permitidos:** baja `R`; deja historial; **puede dejar faltantes** (la cantidad solicitada no se cancela).
- **Efectos prohibidos:** Kardex y contabilidad; cancelar automáticamente `Q`; que una operación ajena libere la reserva.
- **Errores:** `SALES_ORDER_REASON_REQUIRED`, `SALES_ORDER_RELEASE_EXCEEDS_RESERVED`, `SALES_ORDER_PREPARATION_MUST_BE_REVERTED`.
- **Concurrencia/idempotencia:** seguro ante reintentos (`ClientRequestId` + índice único) y ante concurrencia
  (lock del pedido y del recurso). Hoy `CurrentStock.ReleaseReserve` no es idempotente (`Math.Max(0, …)`)
  [VERIFICADO: `CurrentStock.cs` 96-101]; la idempotencia se garantiza en el libro de operaciones.
- **Pruebas:** AT-RES-04a a 04d (F3), 04e (F4: requiere preparación), 04f (F5: requiere selección), 04g (F6:
  requiere consumo); referencia integrada: ESC-05/ESC-05b (F7).

### 10.5 PV-RES-05 — Protección de stock [APROBADO; DEC-02 opción A]

Ninguna venta directa ni operación de inventario ajena puede **consumir ni liberar** reservas de otro pedido, y ninguna
puede dejar la **existencia física** por debajo de la reserva vigente (`Quantity_after ≥ ReservedQuantity`, §9.5). Las
reservas pertenecen al pedido que las originó. Estado actual **[VERIFICADO]**:

| Operación | Comprobación actual de existencias | ¿Protege la existencia física reservada hoy? |
|---|---|---|
| Venta directa (`SaleExit`, control ON) | `AvailableQuantity` en `ApplyKardexMovement` y en el precheck de autorización (`AuthorizeSalesUseCases.cs` 471-491) | Sí, en cuanto exista `ReservedQuantity` |
| Ajuste de egreso | `stock.Quantity` (`ExecuteStockAdjustmentCommandHandler.cs` 159-165) | **No** |
| Devolución de compra | `stock.Quantity` (`AuthorizePurchaseReturnUseCases.cs` 305-318) | **No** |
| Transferencia (`TransferExit`) | Ninguna previa; solo el piso ≥ 0 de `ApplyKardexMovement` | **No** |
| Anulación de compra / reversas de entrada | Solo el piso ≥ 0 | **No** |
| Venta con control apagado o `AllowSellWithoutStock` | Sin exigencia de disponibilidad; `ApplyKardexMovement` permite resultado negativo (línea 75) | **No:** hoy podría dejar la existencia por debajo de lo reservado; con DEC-02 **se rechaza** si hay reservas comprometidas |

Todos los egresos pasan por `StockRepository.AppendMovementAsync` → `ApplyKardexMovement` bajo el lock por
`(tenant, empresa, producto, bodega)` [VERIFICADO: `StockRepository.cs` 101-158, 204-229, 514-539]; ese es el punto
único propuesto **[TP-04]** y la discriminación de los **cuatro casos** (consumo propio, egreso libre, venta negativa
legítima, intento sobre reserva ajena) está en §9.5, junto con los puntos de integración que necesitarán extensión
mínima. **No se modifica ahora.**

- **Compatibilidad con ADR-041:** se conservan las ventas sin stock oficialmente habilitadas y su valoración de costo;
  **sin reservas comprometidas** pueden dejar existencia negativa (caso C); **con reservas comprometidas** no pueden
  reducir la existencia por debajo de ellas (casos B y E).
- **La restricción física se aplica en el punto central**, bajo lock y con `CurrentStock` recargada, no solo en un
  precheck; las pruebas AT-CON-05 y AT-CON-06 la validan bajo concurrencia.
- **Si la separación requiere comprobaciones adicionales en el flujo existente** (informar la línea de pedido del consumo
  propio, `ApplyKardexMovement`), son **extensiones mínimas autorizables por fase** bajo DEC-01, no una implementación
  aprobada.
- **Pruebas:** AT-RES-05a a AT-RES-05i (F2, con una reserva de origen sintético porque el pedido aún no existe;
  ESC-14.1–14.12, 14.14–14.15), AT-CON-06 (F2); el caso D (AT-FAC-04i, ESC-14.13) se prueba en F6.

## 11. Preparación (PV-PREP-01) [APROBADO]

### 11.1 Operación

- **Precondiciones:** pedido Confirmado; permiso `prepare`.
- **Entradas:** línea, cantidad, `ClientRequestId`; para revertir, **motivo obligatorio** (AMB-07).
- **Validaciones:** preparación opcional, parcial o completa; **solo sobre cantidades comprometidas**:
  `P + q ≤ I + R` evaluado con los valores vigentes antes de la operación (INV-05); un hecho externo posterior (p. ej.
  una anulación) que deje `P > I + R` no se corrige solo: bloquea nuevas preparaciones hasta revertir. Servicio no se
  prepara.
- **Efectos permitidos:** registra responsable, instante y cantidades; permite **corregir o revertir** con operación
  autorizada y trazable. **Efectos prohibidos:** Kardex, contabilidad, cambio de reservas.
- **Errores:** `SALES_ORDER_PREPARATION_EXCEEDS_COMMITTED`. **Pruebas:** AT-PREP-01a a AT-PREP-01d (F4).

### 11.2 Secuencia de reconciliación antes de liberar o cancelar stock [APROBADO]

Si una liberación (PV-RES-04), cancelación (PV-CAN-01) o modificación (PV-EDI-01) deja `P > I + R_después`:
1. Bajo lock del pedido se calcula `exceso = P − (I + R_después)`.
2. Si `exceso > 0` la operación **se rechaza completa** con `SALES_ORDER_PREPARATION_MUST_BE_REVERTED` indicando
   `exceso`; no se libera nada.
3. El usuario con permiso `prepare` revierte al menos `exceso` unidades (con motivo).
4. Se repite la operación original, que ahora se acepta.
Nunca se libera stock mientras haya mercancía preparada sin respaldo (ESC-05b).

## 12. Facturación desde el pedido (PV-FAC-01 a PV-FAC-05)

### 12.1 PV-FAC-01 — Selección de facturación [APROBADO]

- **Precondiciones:** pedido Confirmado; permiso `invoice` + `sales.create`; caja abierta (el borrador de factura
  la exige) [VERIFICADO: `SalesDraftUseCases.cs` 501-536].
- **Entradas:** líneas y cantidades base a facturar; `ClientRequestId`.
- **Validaciones:** `0 < cantidad ≤ Sel` por línea (INV-15), según la naturaleza de la línea:

| Naturaleza de la línea | `Sel` | Notas |
|---|---|---|
| **Producto reservable** | `R − S` | **No se selecciona más de lo pendiente y reservado** (INV-04). Si existen unidades disponibles pero **no reservadas**, primero deben reservarse con PV-RES-02 (`SALES_ORDER_SELECTION_EXCEEDS_RESERVED`) |
| **Producto no reservable** (control apagado; DEC-02) | `Pf − S` | **No se asume igual a un Servicio:** exige bodega, genera `SaleExit` por el proceso oficial bajo ADR-041 y no puede consumir reservas ajenas (§9.5). Regla sujeta a **[VERIFICAR VER-13]** |
| **Servicio** | `Pf − S` | No reserva ni requiere bodega (AMB-09) |

- **Efectos permitidos:** crea vínculos `Selected` y el **borrador** de factura (PV-FAC-02) en una sola unidad de
  trabajo; conserva la relación pedido ↔ líneas seleccionadas ↔ factura. Una selección puede mezclar Productos y
  Servicios (ESC-15).
- **Efectos prohibidos:** **consumir reservas al seleccionar**; autorizar la factura; emitir comprobante.
- **Concurrencia:** bloqueo del pedido (`FOR UPDATE`) antes de decidir; el perdedor recibe 422 canónico sin efectos.
  Selecciones duplicadas/concurrentes imposibles por INV-02/INV-04 y por índice único sobre la línea de factura.
- **Pruebas:** AT-FAC-01a a AT-FAC-01g, todas en F5 (referencias: ESC-03.2, ESC-08.1–8.3, ESC-15.1; el consumo en la
  autorización se prueba en F6).

### 12.2 PV-FAC-02 — Separación de pantallas [APROBADO]

**Orden de Pedido es propietaria de la edición comercial; Factura de Venta es propietaria de la emisión legal.**
La factura recibe cliente, líneas, cantidades, presentaciones, precios, descuentos, bodegas, condiciones comerciales
y referencia al pedido; todo ello de **solo lectura**. Para corregir, el usuario vuelve al pedido. Consecuencia
directa de AMB-10: mientras la línea tenga una selección activa, el pedido **no** admite edición comercial de esa
línea; para corregir se libera primero la selección con la operación de **§12.5** (DEC-04).

- **Backend [APROBADO que exista]:** el borrador con origen pedido se construye **en el servidor** a partir del
  pedido (comando propio, **[PROPUESTA TP-06]**); el cliente nunca envía precios, cantidades ni cliente.
  `UpdateSalesDraft` **rechaza** cualquier cambio comercial cuando el borrador tiene vínculo con pedido, aunque llegue
  directo por API (`SALES_INVOICE_COMMERCIAL_DATA_LOCKED`). Las facturas de venta directa no se ven afectadas.
- **Campos que siguen editables en una factura con origen** **[PROPUESTA TP-15 — requiere revisión del propietario]**:
  pagos (`Payments`), cronograma de cobro (`Schedule`) y fecha de emisión (`IssueDate`, sujeta a la tolerancia SRI
  vigente). Quedan bloqueados: cliente, líneas (ítem, cantidad, presentación, precio, descuento, bodega) y condición
  de pago. Las observaciones del documento y el vencimiento se tratan como AMB-08.
- **Impuestos:** los fija el proceso oficial al autorizar (`AuthorizeSalesInvoiceHandler` recalcula con el catálogo
  vigente); el pedido no los congela.
- **Pruebas:** AT-FAC-02a a AT-FAC-02d (F5).

### 12.3 PV-FAC-03 — Facturación oficial [APROBADO]

La factura se emite **exclusivamente** desde `/sales` y por los procesos oficiales: estrategias física/electrónica
(`ISalesInvoiceEmissionStrategy`), secuenciales (`IDocumentSequenceRepository`), caja y punto de emisión
(`ICurrentCashSession`; snapshot `EmissionType` inmutable), SRI/XML/firma (`ElectronicDocuments`), impuestos
(`ISriTaxResolver`), cobros, CxC, contabilidad (eventos durables), Kardex y permisos
[VERIFICADO: `AuthorizeSalesUseCases.cs`, `backend.md`]. El **tipo y punto de emisión se resuelven por el flujo de
caja**, no por parámetros del pedido. **El pedido no genera comprobantes legales por sí mismo.** Pruebas: AT-FAC-03a y
03d (F5); AT-FAC-03b y 03c (F6, requieren autorizar una factura con origen).

### 12.4 PV-FAC-04 — Consumo de reserva [APROBADO]

La reserva se consume **solo** cuando la factura se **autoriza internamente** por el proceso oficial de Ventas. El
vínculo (`Selected → Consumed`), la baja de `R` y el `SaleExit` se persisten en la **misma unidad de trabajo atómica** e
**idempotente**; si no se confirma, no quedan cantidades facturadas ni reservas consumidas a medias.

**Precondiciones y efectos por naturaleza de línea** (una factura con origen puede contener las tres):

| Naturaleza | Precondiciones bajo lock | En la autorización |
|---|---|---|
| **Producto reservable** | Vínculo `Selected` vigente; reserva propia suficiente (`R ≥ cantidad`); bodega válida | Consume **únicamente su reserva** (caso 1 de §9.5) y genera la salida oficial `SaleExit` conforme al contrato de inventario |
| **Producto no reservable / bajo venta sin stock** | Vínculo `Selected` vigente; bodega válida; cumple DEC-02 (no consume reservas ajenas) | Genera `SaleExit` por el proceso oficial con las reglas de ADR-041 (caso 2 o 3 de §9.5); **no** se trata como un Servicio. Sujeto a **[VERIFICAR VER-13]** |
| **Servicio** | Vínculo `Selected` vigente. **No** requiere reserva **ni** bodega | **No** genera `SaleExit`; se factura por el proceso oficial de Ventas dentro del saldo permitido |

- **Idempotencia:** una factura ya `Authorized` devuelve su resultado sin repetir efectos (ya existe)
  [VERIFICADO: `AuthorizeSalesUseCases.cs` 177-181]; el vínculo `Consumed` no puede consumirse de nuevo (INV-12).
- **Guard de vínculo (DEC-04):** la autorización verifica en backend que el vínculo siga `Selected`; si está `Released`
  se rechaza `SALES_ORDER_LINK_NOT_SELECTED` sin efectos (INV-16).
- **Distinción obligatoria:** autorización interna ≠ autorización SRI (INV-14). Un error de emisión electrónica no
  revierte la venta: `I` y el consumo permanecen (ESC-12).
- **Riesgos de implementación:** el handler vigente no abre una transacción explícita (el secuencial se captura con su
  propia transacción antes y la de stock se abre en el primer `AppendMovementAsync`); el reintento por conflicto de
  secuencia restaura la unidad de trabajo. El control de concurrencia (T1/T2) **no está elegido**: exige **VER-05,
  VER-06 y las pruebas AT-CON-02 a AT-CON-05**. Ver [ADR-049 §9](ADR-049-sales-order-v1-architecture.md).
  Requiere extensiones mínimas de Ventas e Inventario bajo **DEC-01** (aprobada, fase por fase).
- **Pruebas:** AT-FAC-04a a AT-FAC-04h (F6), AT-CON-02 a AT-CON-05 (F6).

### 12.5 PV-FAC-05 — Borradores abandonados: liberación manual de la selección [APROBADO; DEC-04 opción A]

La selección **no bloquea indefinidamente** el pedido: puede liberarse manualmente desde Orden de Pedido. **Operación
documentada, no implementada.**

- **Operación y API prevista [PROPUESTA]:** `POST /api/v1/sales/orders/{orderId}/invoice-selections/{invoiceId}/release`
  con `{ reason, clientRequestId }`. Libera **todos** los vínculos `Selected` de ese borrador (la selección de un
  borrador es una unidad). Sin endpoint de reactivación.
- **Permiso:** clave propuesta `sales.orders.selection.release` (específica; **no** `sales.update`).
- **Precondiciones (todas bajo lock):** (1) cada vínculo continúa en `Selected`; (2) la factura vinculada continúa en
  **Borrador**; (3) pedido Confirmado; (4) motivo no vacío.
- **Efectos:** cada vínculo pasa a `Released` (usuario, fecha, **cantidad**, motivo y **referencia de factura**
  registrados); `S` baja y **la cantidad vuelve a estar disponible para una nueva selección desde el pedido**; el
  borrador **permanece registrado** para trazabilidad y queda **permanentemente no autorizable**.
- **Efectos prohibidos:** liberar o consumir la reserva; modificar la existencia física; generar Kardex, CxC o
  contabilidad; reactivar un vínculo `Released`; reutilizar el borrador desvinculado; liberar por tiempo; alterar el ciclo
  de vida de `SalesInvoice`; afectar facturas directas.
- **Transacción y concurrencia (DEC-04 regla 16):** `BeginTransactionAsync` → bloquea el pedido (`FOR UPDATE`) → bloquea
  la factura (`GetByIdForUpdateAsync`) → verifica vínculos y estado → marca `Released` → auditoría → commit. La
  **autorización oficial** relee el vínculo en su unidad de trabajo y lo consume con control de versión (`RowVersion`):
  si la liberación confirma primero, la autorización se rechaza (`…LINK_NOT_SELECTED`); si la autorización confirma
  primero, el vínculo ya es `Consumed` y la liberación se rechaza. **Exactamente una gana.** La exclusión mutua se
  **demuestra con AT-CON-07** y depende de VER-05/VER-06.
- **Errores:** `SALES_ORDER_LINK_NOT_SELECTED`, `SALES_ORDER_INVOICE_NOT_DRAFT`, `SALES_ORDER_REASON_REQUIRED`, 403 sin
  permiso, 404 fuera de alcance.
- **Idempotencia:** `ClientRequestId`; un segundo intento sobre vínculos ya `Released` responde el resultado original con
  el mismo `ClientRequestId`, o `…LINK_NOT_SELECTED` si es una intención distinta.
- **Pruebas:** AT-FAC-05a, 05c, 05d, 05e (F5), AT-FAC-05b (F6), AT-CON-07 (F6); ESC-09.

## 13. Entregas (PV-ENT-01) [APROBADO]

- **Precondiciones:** pedido Confirmado; permiso `deliver`; línea de Producto.
- **Entradas:** línea, factura relacionada (línea vinculada), cantidad, `ClientRequestId`.
- **Validaciones:** por **línea de factura vinculada** `v`, `0 < q ≤ E_v(antes)` con `E_v = max(0, H_v − Dv_v − D_v −
  Rg_in_v)` (INV-06, §9.1): solo cantidades **facturadas y legalmente habilitadas**, descontando lo ya devuelto, lo ya
  entregado contra esa misma línea y lo regularizado contra ella. No se compara contra totales acumulados ni se
  revalidan entregas históricas. Preparar antes de facturar está permitido; **entregar, no**. Entregas parciales
  permitidas.
- **Habilitación legal por tipo de comprobante** (snapshot `SalesInvoice.EmissionType`):

| Emisión | Condición para habilitar la entrega | Fuente técnica autoritativa |
|---|---|---|
| Física | Factura `Authorized` internamente: el gate fiscal físico y el rango de secuenciales ya se validaron antes de capturar el secuencial | `SalesInvoice.Status` + `PhysicalSalesInvoiceEmissionStrategy` (`ExecuteAsync` no agrega pasos) |
| Electrónica | `ElectronicDocument.CurrentState = Authorized` (**autorización efectiva del SRI**) **[APROBADO como diseño V1]** | `ElectronicDocument` es la única fuente del estado electrónico; se lee vía `SalesInvoiceDto.ElectronicStatus` / `IElectronicDocumentRepository.GetCurrentStatesBySourcesAsync` |

- **Bloquean la entrega** (electrónica): `Draft`, `XmlGenerated`, `Signed`, `Sent`, `Received` (**factura pendiente de
  respuesta del SRI**), `Rejected`, `DeadLetter`, `Failed`, `Discarded`, `AnnulmentPending`, `Cancelled`. Una factura
  `Cancelled` deja `H = 0` y una devolución reduce `E` (§9.1, §9.4; AMB-05). **No se registran entregas nuevas contra un
  comprobante que perdió su habilitación legal** (`E_v = 0`). Una entrega histórica que quede por encima de lo hoy
  habilitado **no se revierte**: se señala como `Esr > 0` (§15).
- **Efectos permitidos:** registra cantidades, usuario, fecha y factura relacionada. **Efectos prohibidos:** una
  segunda salida de Kardex (la existencia ya salió en la autorización de la factura; INV-09/INV-10).
- **Errores:** `SALES_ORDER_DELIVERY_NOT_ENABLED`, `SALES_ORDER_DELIVERY_EXCEEDS_INVOICED`.
- **Verificación pendiente [VER-02]:** no se encontró un flujo de anulación SRI para facturas de Ventas (solo
  Retenciones, [backend.md § Verificación fiscal de anulación de retenciones](../architecture/backend.md)) ni uso de
  `SriFiscalStatus` en Ventas. Si el contrato actual no permite verificar un estado fiscal, la entrega queda
  **bloqueada** y se registra el bloqueo; no se infiere.
- **Pruebas:** AT-ENT-01a a AT-ENT-01f, todas en F7 (referencias: ESC-04, ESC-12.2–12.3a, ESC-16).

## 14. Cancelaciones (PV-CAN-01) [APROBADO]

- **Precondiciones:** pedido Confirmado; permiso `cancel`; **motivo obligatorio**.
- **Entradas:** línea y cantidad (0 < cantidad ≤ `Pf − S`), `ClientRequestId`; o «cancelar saldo de todo el pedido».
- **Validaciones:** no se cancelan cantidades **facturadas** (`I`) ni **seleccionadas** (`S`); reconciliación de
  preparación previa (§11.2).
- **Efectos permitidos:** sube `X`; **libera las reservas correspondientes** (`R` baja hasta `Q − X − I`) como parte
  de la misma operación; deja historial. Un pedido parcialmente facturado y parcialmente cancelado **conserva ambos
  resultados**.
- **Efectos prohibidos:** borrar o alterar facturas, entregas o movimientos; Kardex; contabilidad.
- **Errores:** `SALES_ORDER_CANCEL_EXCEEDS_PENDING`, `SALES_ORDER_REASON_REQUIRED`.
- **Pruebas:** AT-CAN-01a y 01d (F3), 01b (F4), 01e (F5), 01c (F6); referencia integrada: ESC-06 (F8).

## 15. Anulaciones, devoluciones e incidencias (PV-REC-01) [APROBADO; DEC-05: A1 + B1 + C1]

Se usan **exclusivamente** los flujos oficiales ([backend.md](../architecture/backend.md#revertir-una-venta-un-solo-camino-por-factura-zh-sales-cancel-authorized-return-rule-01)).
Distinguir **anulación legal** de factura y **devolución** de mercancía: una devolución no equivale a una anulación ni
genera nuevo saldo comercial.

### 15.1 Efecto en el pedido

| Evento oficial | Qué ocurre hoy [VERIFICADO] | Pedido **Confirmado** | Pedido **Cerrado** |
|---|---|---|---|
| **Anulación legal** de factura (`CancelSalesInvoice`) | Revierte CxC y Kardex (`SaleReturn`); rechazada con comprobante electrónico `Authorized` (exige NC) o en proceso; permitida con `Rejected`/`Draft`/`Failed` y con CxC sin cobros; registra `CancelledAt` | **A1:** sus cantidades vuelven al saldo pendiente (`I` baja, `Ia` sube, `Pf` sube); las reservas consumidas **no** se restablecen; no se crea factura automáticamente; la facturación posterior exige cantidades elegibles y, si corresponde, reservas suficientes (`Sel = R − S`); el vínculo `Consumed` se conserva; sin duplicar Kardex, CxC ni contabilidad | **B1:** conserva el estado Cerrado; no se reabre; no se crean cantidades facturables ni reservas; la anulación queda visible en el historial; incidencia si `Esr > 0` |
| **Devolución** de mercancía (`AuthorizeSalesReturn`) | Reingresa Kardex (`SaleReturn`), reembolsa, emite NC; solo sobre facturas `Authorized`; impide anular esa factura | **No** reabre reservas, **no** crea saldo ni obligación comercial; solo muestra `Dv` derivada | Conserva el estado Cerrado; visible en el historial |

**Prohibido desde el pedido:** emitir movimientos de Kardex, CxC o contabilidad; modificar facturas históricas; reabrir
reservas; crear facturas.

### 15.2 Incidencia de entrega sin respaldo fiscal (DEC-05 C1) [APROBADO]

Una entrega que pierde su respaldo fiscal (`Esr > 0`) se gestiona con una **incidencia auditada**:
1. Incidencia **visible en la lista y en el detalle** del pedido.
2. La **entrega original se conserva** como hecho histórico (nunca se modifica ni se borra).
3. Se mantiene el vínculo con la factura y su estado fiscal vigente.
4. **No se registran nuevas entregas** contra el comprobante que perdió habilitación (`E_v = 0`).
5. Un pedido **Confirmado** con incidencia sin resolver **no puede Cerrarse**.
6. **No se registran devoluciones físicas ficticias.**
7. La incidencia **no genera Kardex, CxC ni contabilidad** directamente.
8. Se regulariza **únicamente** mediante evidencia real y flujos oficiales aplicables (§15.3).
9. Se registran usuario, fecha, motivo, evidencia y referencias documentales.
10. **Un cierre manual de la incidencia no sustituye la regularización**: no existe la acción «cerrar incidencia».
11. Si la incidencia aparece sobre un pedido **Cerrado**, el pedido continúa Cerrado y muestra la incidencia posterior.

**Origen auditable:** la incidencia se **deriva** de los hechos oficiales (`Esr`); su fecha de origen es la de la
anulación de la factura (`SalesInvoice.CancelledAt`, H-13) o la del cambio fiscal. No requiere eventos nuevos en los
flujos CLOSED (TP-18).

### 15.3 Verificación de la evidencia y criterios de resolución (TP-19)

La resolución exige un **registro de regularización** (usuario, fecha, motivo, referencias) y una **modalidad con
evidencia verificable por el sistema**. El sistema nunca acepta texto libre como evidencia ni inventa documentos
fiscales, estados del SRI o procesos oficiales inexistentes.

| Modalidad | Evidencia que se verifica | Estado |
|---|---|---|
| **M1 — Refacturación oficial** (`OfficialReinvoice`) | Una factura de Venta oficial **vinculada al mismo pedido y línea**, en estado `Authorized` y **habilitada** según §13 (fuente: `SalesInvoice.Status` + `ElectronicDocument.CurrentState`). El sistema comprueba: (a) misma línea de pedido, producto y bodega; (b) vínculo destino `Consumed`, vigente (no anulado) y no `Released`; (c) `q ≤ Esr_v` pendiente y `q ≤ H_v' − Dv_v' − D_v' − Rg_in_v'`; (d) permiso `incidents.resolve`; (e) motivo obligatorio. Al registrarla, `Rg_out_v` y `Rg_in_v'` suben; **la incidencia queda resuelta solo cuando `Esr = 0` por cobertura verificada**, y el destino no puede entregarse dos veces lo ya cubierto | **Definida** (contratos existentes: factura oficial y habilitación). Solo en pedidos **Confirmados**: en un pedido Cerrado no se crean cantidades facturables (B1), así que M1 no está disponible allí |
| **M2 — Reingreso físico real contra la factura anulada** | Requeriría un flujo oficial de devolución sobre una factura **anulada**. Hoy la devolución solo admite facturas `Authorized` (`SalesReturnInvoiceEligibility`) | **BLOQUEADA.** Verificación necesaria (**VER-12a**): confirmar si existe o debe definirse un flujo oficial de reingreso para mercancía entregada de una factura anulada y qué documento lo evidencia |
| **M3 — Corrección de existencias por el flujo oficial de Ajustes** | Un ajuste de inventario ejecutado que referencie producto, bodega, cantidad y fecha coherentes | **BLOQUEADA.** Verificación necesaria (**VER-12b**): determinar si el ajuste puede ser evidencia verificable de una incidencia sin crear movimientos desde el pedido |
| **M4 — Recuperación de la habilitación del mismo comprobante** | Estado fiscal vigente que vuelva a habilitar la entrega | **BLOQUEADA.** No existe flujo de anulación/reactivación SRI para Ventas (VER-02); **VER-12c**: confirmar el contrato antes de definir cualquier criterio |

Las modalidades bloqueadas rechazan la operación con `SALES_ORDER_REGULARIZATION_MODALITY_BLOCKED` y no registran nada.
Mientras no exista una modalidad verificable aplicable, la incidencia **permanece visible y bloquea el cierre**.
**Pruebas:** AT-REC-01a a AT-REC-01i (F8; ESC-10, ESC-11, ESC-16, ESC-17).

## 16. Edición, historial y trazabilidad (PV-EDI-01) [APROBADO]

- **Precondiciones:** pedido Confirmado; permiso `amend`; **motivo obligatorio**.
- **Reglas:** (1) no se modifican cantidades, precios ni descuentos **ya facturados**; (2) cambios de precio y
  descuento se aplican **solo al saldo comercial permitido** `Q − X − I − S`; (3) una línea con selección activa
  (`S > 0`) no admite edición comercial hasta que se autorice o libere la selección (**AMB-10**, interpretación
  conservadora de «protección de cantidades seleccionadas»); (4) `Q_nuevo ≥ I + S + X` (INV-15); (5)
  si baja `Q` y `R > Q − X − I`, la reserva excedente se libera como parte de la operación y se reconcilia la
  preparación primero (§11.2); (6) no se alteran facturas existentes ni mercancía entregada.
- **Historial:** cada edición registra usuario, instante, motivo y valores anteriores/nuevos; se lee en la pestaña
  Historial. Mecanismo: Entity Audit ([ADR-022](ADR-022-audit-infrastructure-entity-vs-process.md)) **[TP-09]**.
- **Errores:** `SALES_ORDER_REASON_REQUIRED`, `SALES_ORDER_LINE_HAS_ACTIVE_SELECTION`,
  `SALES_ORDER_QUANTITY_BELOW_COMMITTED`.
- **Pruebas:** AT-EDI-01a, 01c, 01d y 01e (F3), 01f (F4), 01b (F5), 01g (F6); referencia integrada: ESC-07 (F6).

## 17. Mensajes y errores

Códigos **[PROPUESTOS]** (se registran en el catálogo de mensajes al implementar; contrato de errores:
[error-handling](../architecture/error-handling.md)). Formato `ApiResponse` con `code`; **422** para reglas de dominio y
validación, **404** fuera de alcance, **409** conflicto de `RowVersion`, **403** sin permiso.

| Código | Cuándo | HTTP |
|---|---|---|
| `SALES_ORDER_INVALID_STATE` | Operación no válida en el estado actual | 422 |
| `SALES_ORDER_RESERVATION_DAYS_NOT_CONFIGURED` *(código definitivo según la convención oficial)* | Confirmar con la empresa sin vigencia de reservas configurada (OPS-01) | 422 |
| `SALES_ORDER_REASON_REQUIRED` | Falta motivo obligatorio | 422 |
| `SALES_ORDER_LINE_NOT_RESERVABLE` | Línea de Servicio o no elegible | 422 |
| `SALES_ORDER_RESERVATION_EXCEEDS_AVAILABLE` | Reserva mayor que el disponible | 422 |
| `SALES_ORDER_RESERVATION_EXCEEDS_SHORTFALL` | Reserva mayor que el faltante | 422 |
| `SALES_ORDER_RESERVATION_DATE_INVALID` | Fecha de vigencia inválida | 422 |
| `SALES_ORDER_RELEASE_EXCEEDS_RESERVED` | Liberación mayor que lo liberable | 422 |
| `SALES_ORDER_PREPARATION_EXCEEDS_COMMITTED` | Preparación mayor que lo comprometido | 422 |
| `SALES_ORDER_PREPARATION_MUST_BE_REVERTED` | Debe revertirse preparación (incluye `exceso`) | 422 |
| `SALES_ORDER_SELECTION_EXCEEDS_RESERVED` | Selección mayor que lo reservado y pendiente | 422 |
| `SALES_ORDER_LINK_NOT_SELECTED` | La factura no tiene selección vigente (autorizar) | 422 |
| `SALES_ORDER_DELIVERY_NOT_ENABLED` | Comprobante sin habilitación legal | 422 |
| `SALES_ORDER_DELIVERY_EXCEEDS_INVOICED` | Entrega mayor que lo habilitado | 422 |
| `SALES_ORDER_CANCEL_EXCEEDS_PENDING` | Cancelación mayor que lo pendiente | 422 |
| `SALES_ORDER_LINE_HAS_ACTIVE_SELECTION` | Línea con selección activa | 422 |
| `SALES_ORDER_QUANTITY_BELOW_COMMITTED` | `Q < I + S + X` | 422 |
| `SALES_ORDER_RESERVED_STOCK_PROTECTED` | Egreso ajeno afectaría stock reservado | 422 |
| `SALES_INVOICE_COMMERCIAL_DATA_LOCKED` | Edición comercial de factura con origen pedido | 422 |
| `SALES_ORDER_INVOICE_NOT_DRAFT` | Liberar una selección cuya factura ya no está en Borrador | 422 |
| `SALES_ORDER_REGULARIZATION_EVIDENCE_INVALID` | La evidencia de regularización no cumple los criterios verificables (§15.3) | 422 |
| `SALES_ORDER_REGULARIZATION_MODALITY_BLOCKED` | Modalidad de regularización sin contrato verificable (M2–M4) | 422 |

## 18. Escenarios numéricos

**Convención de tablas.** Producto de unidad base, una bodega, control de disponibilidad **activo** y venta sin
existencias **desactivada**. Sin otras reservas. «Sol.» = Q, «Res.» = R, «Prep.» = P, «Sel.» = S, «Fact.» = I,
«Entr.» = D, «Canc.» = X, «Falt.» = F. «Fís.» = existencia física; «Disp.» = disponible general. «—» = no aplica.
Las fechas son **ilustrativas**. Las cifras cumplen los invariantes de estado (§9.2) y las reglas de admisión (§9.3)
con las fórmulas de §9.1.

**Los escenarios son aceptación integrada, no pruebas por fase.** Se ejecutan completos en la primera fase que dispone
de todos sus componentes; cada fase ejecuta antes las pruebas `AT-*` del §19, cuyos resultados no dependen de
componentes posteriores. Un paso que requiere componentes aún inexistentes es una **prueba diferida**.

| Escenario | Pasos y fase en que se ejecutan | Ejecución integral |
|---|---|---|
| ESC-01 | 1.0–1.2 en F3 | F3 |
| ESC-02 | 2.1–2.3 en F3 (el ingreso usa el flujo oficial de Compras ya existente) | F3 |
| ESC-03 | 3.1 en F4 · 3.2 en F5 · 3.3 en F6 | F6 |
| ESC-04 | 4.1–4.4 en F7 | F7 |
| ESC-05 | 5.1–5.2 y 5b en F7 (la regla se prueba antes: AT-RES-04a..d en F3, AT-RES-04e en F4) | F7 |
| ESC-06 | 6.1–6.2 en F7 · 6.3 en F8 | F8 |
| ESC-07 | 7.1–7.2 en F6 (la edición sobre el saldo con `I = 0` se prueba antes: AT-EDI-01a y 01e en F3) | F6 |
| ESC-08 | 8.1–8.3 en F5 · 8.4–8.5 en F6 | F6 |
| ESC-09 | 9.1–9.4 en F5 · 9.5 en F6 | F6 |
| ESC-10 | 10a–10d en F8 | F8 |
| ESC-11 | 11.1 en F8 | F8 |
| ESC-12 | 12.1 en F6 · 12.2–12.3a en F7 · 12.3b en F8 | F8 |
| ESC-13 | 13.0–13.3 en F3 (la vigencia en días depende de VER-11; OPS-01 está resuelta) | F3 |
| ESC-14 | 14.1–14.6, 14.8–14.12 y 14.14–14.15 en F2 (reserva de origen sintético) · 14.7 y 14.13 en F6 | F6 |
| ESC-15 | 15.0 en F3 · 15.1 en F5 · 15.2–15.3 en F6 | F6 |
| ESC-16 | 16.1–16.7 en F8 (la entrega 16.6–16.7 usa F7) | F8 |
| ESC-17 | 17.1–17.3 en F8 | F8 |

### ESC-01 — Pedido de 20 con stock 12: reserva parcial

| Paso | Sol. | Res. | Prep. | Sel. | Fact. | Entr. | Canc. | Falt. | Fís. | Disp. | Kardex | Documento legal |
|---|--:|--:|--:|--:|--:|--:|--:|--:|--:|--:|---|---|
| 1.0 Borrador guardado | 20 | 0 | 0 | 0 | 0 | 0 | 0 | — | 12 | 12 | ninguno | ninguno |
| 1.1 Confirmar | 20 | 12 | 0 | 0 | 0 | 0 | 0 | 8 | 12 | 0 | ninguno | ninguno |
| 1.2 Reconfirmar (reintento) | 20 | 12 | 0 | 0 | 0 | 0 | 0 | 8 | 12 | 0 | ninguno | ninguno |

Lateral 1.3: otro pedido de 5 uds se confirma con disponible 0 ⇒ reserva 0, faltante 5; el pedido A no cambia.

### ESC-02 — Ingreso posterior de 10: completar manualmente 8 (continúa de 1.1)

| Paso | Sol. | Res. | Prep. | Sel. | Fact. | Entr. | Canc. | Falt. | Fís. | Disp. | Kardex | Documento legal |
|---|--:|--:|--:|--:|--:|--:|--:|--:|--:|--:|---|---|
| 2.1 Compra recibida +10 (flujo oficial de Compras) | 20 | 12 | 0 | 0 | 0 | 0 | 0 | 8 | 22 | 10 | `PurchaseEntry +10` (Compras) | Factura de proveedor |
| 2.2 Completar 8 (manual, usuario autorizado) | 20 | 20 | 0 | 0 | 0 | 0 | 0 | 0 | 22 | 2 | ninguno | ninguno |
| 2.3 Intentar completar 1 más | 20 | 20 | 0 | 0 | 0 | 0 | 0 | 0 | 22 | 2 | ninguno | rechazado `…EXCEEDS_SHORTFALL` |

El ingreso **no** reserva automáticamente: entre 2.1 y 2.2 el disponible general (10) puede ser tomado por otra venta.

### ESC-03 — Facturación parcial de 8 (continúa de 2.2)

| Paso | Sol. | Res. | Prep. | Sel. | Fact. | Entr. | Canc. | Falt. | Fís. | Disp. | Kardex | Documento legal |
|---|--:|--:|--:|--:|--:|--:|--:|--:|--:|--:|---|---|
| 3.1 Preparar 8 | 20 | 20 | 8 | 0 | 0 | 0 | 0 | 0 | 22 | 2 | ninguno | ninguno |
| 3.2 Seleccionar 8 → borrador F-0001 | 20 | 20 | 8 | 8 | 0 | 0 | 0 | 0 | 22 | 2 | ninguno | F-0001 Borrador, sin secuencial |
| 3.3 Autorizar F-0001 (interna) | 20 | 12 | 8 | 0 | 8 | 0 | 0 | 0 | 14 | 2 | `SaleExit −8` (único) | F-0001 `Authorized` interna; comprobante electrónico en trámite |

Comprobaciones: `P = 8 ≤ I + R = 8 + 12`; `Pf = 20 − 0 − 8 = 12`; `F = 20 − 0 − 8 − 12 = 0`; disponible `14 − 12 = 2`. Si la
autorización falla, el estado vuelve a 3.2 (ninguna reserva consumida, ningún `SaleExit`).

### ESC-04 — Entrega parcial legalmente habilitada (continúa de 3.3; factura electrónica)

| Paso | Sol. | Res. | Prep. | Sel. | Fact. | Entr. | Canc. | Falt. | Fís. | Disp. | Kardex | Documento legal |
|---|--:|--:|--:|--:|--:|--:|--:|--:|--:|--:|---|---|
| 4.1 Entregar 5 con comprobante `Sent` | 20 | 12 | 8 | 0 | 8 | 0 | 0 | 0 | 14 | 2 | ninguno | rechazado `…DELIVERY_NOT_ENABLED` |
| 4.2 El SRI autoriza (`Authorized`) | 20 | 12 | 8 | 0 | 8 | 0 | 0 | 0 | 14 | 2 | ninguno | Comprobante `Authorized`; habilitadas 8 |
| 4.3 Entregar 5 | 20 | 12 | 8 | 0 | 8 | 5 | 0 | 0 | 14 | 2 | ninguno | ninguno nuevo |
| 4.4 Intentar entregar 4 más | 20 | 12 | 8 | 0 | 8 | 5 | 0 | 0 | 14 | 2 | ninguno | rechazado `…EXCEEDS_INVOICED` (solo quedan 3) |

Con emisión **física** el paso 4.2 no existe: `Authorized` interna ya habilita las 8. La existencia (14) no cambia al
entregar: ya salió en 3.3. Estado tras 4.3: `Pp = max(0, 8 − 5) = 3`; `E = 8 − 0 − 5 = 3`.

### ESC-05 — Liberación manual de 4 unidades pendientes (continúa de 4.3)

| Paso | Sol. | Res. | Prep. | Sel. | Fact. | Entr. | Canc. | Falt. | Fís. | Disp. | Kardex | Documento legal |
|---|--:|--:|--:|--:|--:|--:|--:|--:|--:|--:|---|---|
| 5.1 Liberar 4 (motivo «cliente pospuso») | 20 | 8 | 8 | 0 | 8 | 5 | 0 | 4 | 14 | 6 | ninguno | ninguno |
| 5.2 Reintento con el mismo `ClientRequestId` | 20 | 8 | 8 | 0 | 8 | 5 | 0 | 4 | 14 | 6 | ninguno | sin cambio |

`F = 20 − 0 − 8 − 8 = 4`: liberar **no cancela** la cantidad solicitada. Reconciliación: `P = 8 ≤ I + R_después = 16`.

**ESC-05b (variante, desde 5.1):** se preparan 6 más (`P = 14 ≤ 8 + 8 = 16`). Liberar los 8 restantes dejaría
`I + R_después = 8 < 14` ⇒ exceso 6 ⇒ **rechazo** `…PREPARATION_MUST_BE_REVERTED (6)`, sin cambios (`R = 8`).
El usuario revierte 6 (`P = 8`) y la liberación de 8 se acepta: `R = 0`, `F = 12`, `Fís. = 14`, `Disp. = 14`.

### ESC-06 — Cancelación de 12 unidades pendientes (continúa de 5.1)

| Paso | Sol. | Res. | Prep. | Sel. | Fact. | Entr. | Canc. | Falt. | Fís. | Disp. | Kardex | Documento legal |
|---|--:|--:|--:|--:|--:|--:|--:|--:|--:|--:|---|---|
| 6.1 Cancelar 12 (motivo) | 20 | 0 | 8 | 0 | 8 | 5 | 12 | 0 | 14 | 14 | ninguno | F-0001 intacta |
| 6.2 Entregar las 3 restantes | 20 | 0 | 8 | 0 | 8 | 8 | 12 | 0 | 14 | 14 | ninguno | ninguno |
| 6.3 Evaluación de cierre | — | — | — | — | — | — | — | — | — | — | ninguno | Pedido **Cerrado** |

6.1: `Pf = 20 − 0 − 8 = 12`; al subir `X` a 12, `R ≤ Q − X − I = 0` ⇒ se liberan 8 reservas asociadas (nada se consume).
Cierre en 6.3: `Q = I + X` (20 = 8 + 12), `E = 0`, `Pp = 0`, sin selecciones ni incidencias. El pedido conserva
facturación parcial **y** cancelación; no es Cancelado.

### ESC-07 — Precio y descuento de cantidades no facturadas (desde 3.3: `Q=20, I=8, R=12`; precio 10.00, desc. 0 %)

| Paso | Sol. | Res. | Prep. | Sel. | Fact. | Entr. | Canc. | Falt. | Fís. | Disp. | Kardex | Documento legal |
|---|--:|--:|--:|--:|--:|--:|--:|--:|--:|--:|---|---|
| 7.1 Editar precio 10.00→11.00 y desc. 0→5 % sobre el saldo (motivo) | 20 | 12 | 8 | 0 | 8 | 0 | 0 | 0 | 14 | 2 | ninguno | F-0001 intacta (8 × 10.00 = 80.00) |
| 7.2 Reducir cantidad 20→15 (motivo) | 15 | 7 | 8 | 0 | 8 | 0 | 0 | 0 | 14 | 7 | ninguno | ninguno |

7.1: el saldo comercial es 12 uds; valor de referencia `12 × 11.00 × (1 − 0.05) = 125.40` (`132.00 − 6.60`); total
referencial `80.00 + 125.40 = 205.40` sin impuestos; el historial guarda valores anteriores y nuevos. 7.2: `Q ≥ I + S + X
= 8`; como `R = 12 > Q − X − I = 7`, se libera 5 como parte de la edición (`F = 15 − 0 − 8 − 7 = 0`); `P = 8 ≤ 8 + 7`.
Lateral: con `S = 4` en la línea (borrador sin autorizar) la edición se rechaza `…LINE_HAS_ACTIVE_SELECTION`.

### ESC-08 — Dos usuarios facturando el mismo saldo (desde 3.3)

| Paso | Sol. | Res. | Prep. | Sel. | Fact. | Entr. | Canc. | Falt. | Fís. | Disp. | Kardex | Documento legal |
|---|--:|--:|--:|--:|--:|--:|--:|--:|--:|--:|---|---|
| 8.1 A selecciona 10 → F-0002 | 20 | 12 | 8 | 10 | 8 | 0 | 0 | 0 | 14 | 2 | ninguno | F-0002 Borrador |
| 8.2 B selecciona 5 (seleccionable 12 − 10 = 2) | 20 | 12 | 8 | 10 | 8 | 0 | 0 | 0 | 14 | 2 | ninguno | rechazado 422 `…SELECTION_EXCEEDS_RESERVED` |
| 8.3 B selecciona 2 → F-0003 | 20 | 12 | 8 | 12 | 8 | 0 | 0 | 0 | 14 | 2 | ninguno | F-0003 Borrador |
| 8.4 A autoriza F-0002 | 20 | 2 | 8 | 2 | 18 | 0 | 0 | 0 | 4 | 2 | `SaleExit −10` | F-0002 `Authorized` interna |
| 8.5 B autoriza F-0003 | 20 | 0 | 8 | 0 | 20 | 0 | 0 | 0 | 2 | 2 | `SaleExit −2` | F-0003 `Authorized` interna |

Si A y B envían 8.1/8.2 **simultáneamente**, el lock del pedido serializa: exactamente una selección de 10 y una
rechazada. Invariantes en 8.4: `I + S + X = 18 + 2 + 0 = 20 ≤ Q`; `S = 2 ≤ R = 2`. Disponibles: 8.4 `4 − 2 = 2`; 8.5 `2 − 0 = 2`.

### ESC-09 — Selección de factura abandonada y liberación manual (desde 3.3) — **DEC-04**

| Paso | Sol. | Res. | Prep. | Sel. | Fact. | Entr. | Canc. | Falt. | Fís. | Disp. | Kardex | Documento legal |
|---|--:|--:|--:|--:|--:|--:|--:|--:|--:|--:|---|---|
| 9.1 A selecciona 10 y abandona F-0002 | 20 | 12 | 8 | 10 | 8 | 0 | 0 | 0 | 14 | 2 | ninguno | F-0002 Borrador (no descartable) |
| 9.2 B intenta seleccionar 5 (seleccionable `12 − 10 = 2`) | 20 | 12 | 8 | 10 | 8 | 0 | 0 | 0 | 14 | 2 | ninguno | rechazado 422 `…SELECTION_EXCEEDS_RESERVED` |
| 9.3 Usuario con permiso libera la selección (motivo «borrador abandonado») | 20 | 12 | 8 | 0 | 8 | 0 | 0 | 0 | 14 | 2 | ninguno | Vínculo `Released`; F-0002 sigue Borrador, **no autorizable**, sin secuencial consumido |
| 9.4 B selecciona 5 → F-0003 | 20 | 12 | 8 | 5 | 8 | 0 | 0 | 0 | 14 | 2 | ninguno | F-0003 Borrador |
| 9.5 Se intenta autorizar F-0002 | 20 | 12 | 8 | 5 | 8 | 0 | 0 | 0 | 14 | 2 | ninguno | rechazado 422 `…LINK_NOT_SELECTED` |

La liberación no toca la reserva (`R = 12`), la existencia ni el Kardex. El vínculo `Released` no puede reactivarse ni
reutilizarse. Si liberación y autorización de F-0002 ocurren **a la vez**, exactamente una tiene efecto (AT-CON-07).

### ESC-10 — Anulación de factura con el pedido Confirmado (desde 3.3, F-0001 de 8 uds) — **DEC-05 A1**

| Paso | Sol. | Res. | Prep. | Sel. | Fact. | Entr. | Canc. | Falt. | Fís. | Disp. | Kardex | Documento legal |
|---|--:|--:|--:|--:|--:|--:|--:|--:|--:|--:|---|---|
| 10a Anular F-0001 **física**, sin cobros ni devoluciones | 20 | 12 | 8 | 0 | 0 | 0 | 0 | 8 | 22 | 10 | `SaleReturn +8` (ref. «ANULACIÓN») | F-0001 `Cancelled` |
| 10a.2 Completar manualmente la reserva de 8 (`Free = 10`) | 20 | 20 | 8 | 0 | 0 | 0 | 0 | 0 | 22 | 2 | ninguno | ninguno |
| 10b Anular con comprobante electrónico `Authorized` | 20 | 12 | 8 | 0 | 8 | 0 | 0 | 0 | 14 | 2 | ninguno | rechazado `ELECTRONIC_DOCUMENT_REQUIRES_SRI_ANNULMENT` (usar ESC-11) |
| 10c Anular con comprobante en trámite | 20 | 12 | 8 | 0 | 8 | 0 | 0 | 0 | 14 | 2 | ninguno | rechazado `ELECTRONIC_DOCUMENT_IN_PROCESS` |
| 10d Como 10a pero con 5 uds ya entregadas | 20 | 12 | 8 | 0 | 0 | 5 | 0 | 8 | 22 | 10 | `SaleReturn +8` | **Incidencia**: `Esr = 5` |

10a (A1): `I = 0` (vigente), `Ih = 8`, `Ia = 8`; el saldo vuelve: `Pf = 20 − 0 − 0 = 20` y `F = 20 − 0 − 0 − 12 = 8`. **Las
reservas consumidas no se restablecen** (`R = 12`); no se crea ninguna factura; el vínculo `Consumed` se conserva. Para
facturar de nuevo, `Sel = R − S = 12`: hace falta completar la reserva (10a.2, `R = 20`, `F = 0`, `Disp. = 2`). Kardex y
estado legal son los del flujo oficial. 10d: la anulación no conoce las entregas; la existencia reingresa pese a
mercancía entregada ⇒ `Esr = max(0, 5 − 0 − 0) = 5`: incidencia visible, pedido no cerrable (ESC-16 muestra su
regularización). Preparación: `P = 8 ≤ I + R = 0 + 12`, sin desviación.

### ESC-11 — Devolución de mercancía facturada y entregada (desde 4.3: `I=8, D=5`, comprobante `Authorized`)

| Paso | Sol. | Res. | Prep. | Sel. | Fact. | Entr. | Canc. | Falt. | Fís. | Disp. | Kardex | Documento legal |
|---|--:|--:|--:|--:|--:|--:|--:|--:|--:|--:|---|---|
| 11.1 Devolución de 3 (efectivo o CxC) | 20 | 12 | 8 | 0 | 8 | 5 | 0 | 0 | 17 | 5 | `SaleReturn +3` (documento: la devolución) | Nota de Crédito 04 por el flujo oficial; F-0001 sigue `Authorized` y ya no es anulable |

`Dv = 3` (derivada por `OriginalInvoiceDetailId`), `I = 8` (histórico intacto), `Pf = 12` sin cambio, `R = 12` sin
reabrir, sin nueva obligación comercial (una devolución no equivale a anulación). Por la línea de factura vinculada:
`E_v = max(0, 8 − 3 − 5 − 0) = 0`, así que no se habilitan entregas adicionales por una devolución (interpretación
conservadora, AMB-05); `D = 5` es histórico y no se corrige. Queda `Pp = max(0, 8 − 5) = 3` (preparado no entregado):
para cumplir la condición de cierre debe revertirse esa preparación.

### ESC-12 — Error de emisión electrónica tras la autorización interna (desde 3.2)

| Paso | Sol. | Res. | Prep. | Sel. | Fact. | Entr. | Canc. | Falt. | Fís. | Disp. | Kardex | Documento legal |
|---|--:|--:|--:|--:|--:|--:|--:|--:|--:|--:|---|---|
| 12.1 Autorizar F-0001; el comprobante queda `Failed` | 20 | 12 | 8 | 0 | 8 | 0 | 0 | 0 | 14 | 2 | `SaleExit −8` (ya posteado) | Interna `Authorized`; electrónico `Failed`; error informativo en la respuesta |
| 12.2 Entregar 5 | 20 | 12 | 8 | 0 | 8 | 0 | 0 | 0 | 14 | 2 | ninguno | rechazado `…DELIVERY_NOT_ENABLED` (`E_v = 0`) |
| 12.3a Reintento oficial ⇒ `Authorized` | 20 | 12 | 8 | 0 | 8 | 0 | 0 | 0 | 14 | 2 | ninguno | Comprobante `Authorized`: habilitadas 8 |
| 12.3b Comprobante `Rejected` ⇒ anulación local permitida (A1) | 20 | 12 | 8 | 0 | 0 | 0 | 0 | 8 | 22 | 10 | `SaleReturn +8` | Como ESC-10a: `Pf = 20`, `F = 8` |

La autorización interna **no se revierte** por el error electrónico (la venta ya está comprometida y `I` cuenta).

### ESC-13 — Vigencia por empresa, vencimiento y extensión (desde 1.1) — **DEC-03**

La empresa tiene configurada una vigencia de **14 días calendario** (*valor ilustrativo*: no es un predeterminado). El
pedido se confirma el 2026-11-17 ⇒ `expires_on = 2026-11-17 + 14 = 2026-12-01`, registrado y auditable.

| Paso | Sol. | Res. | Prep. | Sel. | Fact. | Entr. | Canc. | Falt. | Fís. | Disp. | Kardex | Documento legal |
|---|--:|--:|--:|--:|--:|--:|--:|--:|--:|--:|---|---|
| 13.0 Confirmar **sin** vigencia configurada (existencia 12) | 20 | 0 | 0 | 0 | 0 | 0 | 0 | — | 12 | 12 | ninguno | rechazado; pedido sigue Borrador |
| 13.1 Confirmar el 2026-11-17 (con 14 días configurados): vence el 2026-12-01 | 20 | 12 | 0 | 0 | 0 | 0 | 0 | 8 | 12 | 0 | ninguno | ninguno |
| 13.2 Hoy 2026-12-02: indicador «Vencida» | 20 | 12 | 0 | 0 | 0 | 0 | 0 | 8 | 12 | 0 | ninguno | advertencia visible; **sin liberación** |
| 13.3 Extender a 2026-12-15 (historial) | 20 | 12 | 0 | 0 | 0 | 0 | 0 | 8 | 12 | 0 | ninguno | indicador «Vigente» |

Paso 13.0 (OPS-01 resuelta): si la empresa **no** tiene la vigencia configurada, Confirmar se rechaza con el mensaje de §10.3; el pedido sigue en Borrador, sin reservas ni otros efectos; al configurarla (13.1) se confirma y se revalidan configuración e inventario. Ningún valor se asume.

### ESC-14 — Egreso ajeno y existencia física comprometida (desde 1.1: `Fís. 12`, reservado 12, `Disp. 0`) — **DEC-02**

Regla: un egreso ajeno que retira `q` exige `Quantity_after ≥ ReservedQuantity` (§9.5). Cada fila parte del estado inicial 1.1.

| Operación ajena | Comprobación actual [VERIFICADO] | Resultado esperado tras la protección (TP-04, §9.5) | Fís. | Reservado |
|---|---|---|--:|--:|
| 14.1 Venta directa de 1 (control ON) | `AvailableQuantity` ⇒ rechaza | rechazada (caso 4) | 12 | 12 |
| 14.2 Ajuste de egreso de 1 | Compara `Quantity` (12 ≥ 1) y el piso ≥ 0 ⇒ **lo permitiría** (dejaría 11 < 12) | rechazado `…RESERVED_STOCK_PROTECTED` (caso 4) | 12 | 12 |
| 14.3 Transferencia de 1 | Sin precheck; piso ≥ 0 ⇒ **lo permitiría** | rechazada (caso 4) | 12 | 12 |
| 14.4 Devolución de compra de 1 | Compara `Quantity` ⇒ **lo permitiría** | rechazada (caso 4) | 12 | 12 |
| 14.5 Anulación de compra con salida de 1 | Piso ≥ 0 ⇒ **lo permitiría** | rechazada (caso 4) | 12 | 12 |
| 14.6 Venta directa de 1 con `AllowSellWithoutStock` ON | Sin exigencia de disponibilidad; permitiría 11 < 12 (línea 75) | **rechazada**: `Quantity_after = 11 < ReservedQuantity = 12`; la reserva y la existencia no cambian | 12 | 12 |
| 14.7 Facturación del propio pedido (consume su reserva) | n/a | permitida (caso 1; ESC-03) | 14 | 12 |
| 14.8 Otro pedido intenta reservar 1 con `Free ≤ 0` | n/a | rechazado (INV-17): nunca reservas negativas ni sobre lo comprometido | 12 | 12 |
| 14.9 Operación ajena intenta liberar la reserva de este pedido | n/a | no existe vía: solo este pedido libera su reserva (PV-RES-04) | 12 | 12 |

**Casos de aceptación obligatorios de DEC-02** (cada uno con su propio estado inicial):

| Caso | Operación | Antes (Fís. / Reservado) | Resultado | Después (Fís. / Reservado) |
|---|---|---|---|---|
| 14.10 **A** | Venta externa de 3 | 20 / 15 | **permitida** (`17 ≥ 15`) | 17 / 15 |
| 14.11 **B** | Venta externa de 10 (con control activo o con venta sin stock) | 20 / 15 | **rechazada** (`10 < 15`) | 20 / 15 |
| 14.12 **C** | Venta sin stock oficialmente habilitada de 5, **sin reservas** | 0 / 0 | **permitida** (ADR-041) | −5 / 0 |
| 14.13 **D** | Autorización oficial de la factura del propio pedido por 8 | 20 / 15 | **permitida**: una sola `SaleExit` de 8; consume 8 de la reserva propia | 12 / 7 |
| 14.14 **E** | Venta externa sin stock habilitada de 1, con reservas | 10 / 10 | **rechazada** (`9 < 10`) | 10 / 10 |
| 14.15 **F** | Venta ordinaria con control de stock activo de 1 | 0 / 0 | **rechazada** por el contrato vigente (`AvailableQuantity 0 < 1`) | 0 / 0 |

Comprobación del caso D: `Quantity_after = 12 ≥ ReservedQuantity_after = 7`; `20 − 8 = 12` y `15 − 8 = 7`. El caso C **no**
viola la regla porque no hay reservas comprometidas. Los casos A, B, C, E y F se prueban en F2 con una reserva de origen
sintético; el D, en F6 (AT-FAC-04i).

### ESC-15 — Factura mixta Producto + Servicio (pedido con `P`: Producto reservable; `S`: Servicio) — **DEC-02, §2.1**

Producto `P` con existencia 10 y control activo; servicio `S`. «Línea» identifica la naturaleza.

| Paso | Línea | Sol. | Res. | Prep. | Sel. | Fact. | Entr. | Canc. | Falt. | Fís. | Disp. | Kardex | Documento legal |
|---|---|--:|--:|--:|--:|--:|--:|--:|--:|--:|--:|---|---|
| 15.0 Confirmar | P | 10 | 10 | 0 | 0 | 0 | 0 | 0 | 0 | 10 | 0 | ninguno | ninguno |
| 15.0 Confirmar | S | 2 | 0 | — | 0 | 0 | — | 0 | — | — | — | ninguno | ninguno (el servicio no reserva) |
| 15.1 Seleccionar P 4 y S 2 → F-0010 | P | 10 | 10 | 0 | 4 | 0 | 0 | 0 | 0 | 10 | 0 | ninguno | F-0010 Borrador |
| 15.1 Seleccionar P 4 y S 2 → F-0010 | S | 2 | 0 | — | 2 | 0 | — | 0 | — | — | — | ninguno | F-0010 Borrador |
| 15.2 Autorizar F-0010 | P | 10 | 6 | 0 | 0 | 4 | 0 | 0 | 0 | 6 | 0 | `SaleExit −4` (solo `P`) | F-0010 `Authorized` interna |
| 15.2 Autorizar F-0010 | S | 2 | 0 | — | 0 | 2 | — | 0 | — | — | — | **ninguno** (sin bodega ni `SaleExit`) | F-0010 `Authorized` interna |
| 15.3 Intentar seleccionar 1 más de `S` (`Pf = 2 − 0 − 2 = 0`) | S | 2 | 0 | — | 0 | 2 | — | 0 | — | — | — | ninguno | rechazado 422 (`Sel = 0`) |

15.2: la autorización consume **solo** la reserva de `P` (`R`: 10 → 6; `Fís.`: 10 → 6; `Disp. = 6 − 6 = 0`); un único
movimiento de Kardex; el servicio se factura por el proceso oficial sin reserva, bodega ni `SaleExit`.

### ESC-16 — Incidencia de entrega sin respaldo y regularización por refacturación oficial (continúa de 10d) — **DEC-05 C1**

Estado inicial: el de 10d (pedido Confirmado; `Q=20`, `R=12`, `P=8`, `I=0`, `Ih=8`, `D=5`, `Fís.=22`, `Disp.=10`).

| Paso | Sol. | Res. | Prep. | Sel. | Fact. | Entr. | Canc. | Falt. | Fís. | Disp. | Kardex | Documento legal |
|---|--:|--:|--:|--:|--:|--:|--:|--:|--:|--:|---|---|
| 16.1 Incidencia visible (`Esr = 5`); entregas nuevas contra F-0001 rechazadas (`E_v = 0`) | 20 | 12 | 8 | 0 | 0 | 5 | 0 | 8 | 22 | 10 | ninguno | F-0001 `Cancelled` |
| 16.2 Completar reserva de 8 (`Free = 10`) | 20 | 20 | 8 | 0 | 0 | 5 | 0 | 0 | 22 | 2 | ninguno | ninguno |
| 16.3 Seleccionar 8 → F-0004 | 20 | 20 | 8 | 8 | 0 | 5 | 0 | 0 | 22 | 2 | ninguno | F-0004 Borrador |
| 16.4 Autorizar F-0004; el SRI la autoriza (`H = 8`) | 20 | 12 | 8 | 0 | 8 | 5 | 0 | 0 | 14 | 2 | `SaleExit −8` | F-0004 `Authorized`; comprobante `Authorized`; `Esr` sigue 5 |
| 16.5 Regularizar 5 por **M1** (motivo; evidencia F-0004) | 20 | 12 | 8 | 0 | 8 | 5 | 0 | 0 | 14 | 2 | ninguno | Registro de regularización; `Esr = 0`; `E_v' = max(0, 8 − 0 − 0 − 5) = 3` |
| 16.6 Entregar 3 contra F-0004 | 20 | 12 | 8 | 0 | 8 | 8 | 0 | 0 | 14 | 2 | ninguno | ninguno |
| 16.7 Intentar entregar 1 más (`E_v' = 0`) | 20 | 12 | 8 | 0 | 8 | 8 | 0 | 0 | 14 | 2 | ninguno | rechazado `…DELIVERY_NOT_ENABLED` |

La regularización **no** genera Kardex, CxC ni contabilidad (los generó la refacturación oficial 16.4) y **evita**
entregar dos veces lo cubierto. Las modalidades M2–M4 (bloqueadas) rechazan con `…MODALITY_BLOCKED` y no cambian nada.
El pedido no cierra: `Pf = 20 − 0 − 8 = 12`.

### ESC-17 — Anulación posterior al cierre (pedido Cerrado) — **DEC-05 B1**

Pedido de 8 uds de un producto con existencia 8, facturado (F-0001 física), entregado y **Cerrado**.

| Paso | Sol. | Res. | Prep. | Sel. | Fact. | Entr. | Canc. | Falt. | Fís. | Disp. | Kardex | Documento legal |
|---|--:|--:|--:|--:|--:|--:|--:|--:|--:|--:|---|---|
| 17.1 Pedido Cerrado | 8 | 0 | 8 | 0 | 8 | 8 | 0 | 0 | 0 | 0 | `SaleExit −8` (histórico) | F-0001 `Authorized` |
| 17.2 Anulación oficial de F-0001 (física, sin cobros) | 8 | 0 | 8 | 0 | 0 | 8 | 0 | 0 | 8 | 8 | `SaleReturn +8` (flujo oficial) | F-0001 `Cancelled` |
| 17.3 Intentar una nueva selección | 8 | 0 | 8 | 0 | 0 | 8 | 0 | 0 | 8 | 8 | ninguno | rechazado: pedido Cerrado |

17.2: el pedido **conserva el estado Cerrado**; `I = 0`, `Ih = 8`, `Ia = 8` se muestran juntos; `Pf = 0` y `F = 0` (B1:
sin cantidades facturables ni reservas nuevas); `Esr = 8` ⇒ **incidencia visible** sin reabrir el pedido; la regularización
M1 no está disponible en un pedido Cerrado (VER-12). Las existencias ahora disponibles (`Disp. = 8`) pertenecen al
inventario general.

## 19. Pruebas de aceptación

Resultado esperado verificable para cada caso. Pruebas de concurrencia, transacciones, scope, locks e invariantes de
persistencia: **PostgreSQL real**, no SQLite/in-memory/mocks ([Enforcement](../architecture/enforcement.md#flujo-obligatorio-del-agente-por-fase)).

**Regla de fase.** Cada prueba se ejecuta en la **primera fase que dispone de todos sus componentes** (columna *Fase*).
Su resultado esperado **no depende de componentes de fases posteriores**; el escenario `ESC` citado es solo la
referencia numérica y se ejecuta de forma integrada según §18. Las pruebas que necesitan facturación consumida
(`I > 0`), selección (`S > 0`), preparación o entregas se ubican en F5–F8 y **no** se exigen antes (prueba diferida).
`AT-SEC-01a` se ejecuta por operación, en la fase que la introduce.

| ID | Regla | Fase | Referencia | Resultado esperado |
|---|---|---|---|---|
| AT-COM-01 | PV-COM-01 | F1 | — | Guardar borrador: estado Borrador, `R = 0`, sin Kardex ni reserva |
| AT-COM-02 | PV-COM-01 | F1 | — | Servicio con bodega, producto sin bodega, cliente inactivo, bodega de otra sucursal: 422/404 canónico, nada persistido |
| AT-COM-03 | PV-COM-01 | F1 | — | Otro tenant/empresa/sucursal: 404, sin fuga |
| AT-RES-01a | PV-RES-01 | F3 | ESC-01.1 | Existencia 12, pedido 20 ⇒ `R = 12`, `F = 8`, `Disp. = 0`, sin Kardex |
| AT-RES-01b | PV-RES-01 | F3 | ESC-01.2 | Reconfirmar no duplica reservas; respuesta original |
| AT-RES-01c | PV-RES-01 | F3 | ESC-15.0 | Línea de Servicio: sin reserva y la confirmación no falla |
| AT-RES-01d | PV-RES-01 | F3 | — | Dos pedidos concurrentes sobre 12 uds: `Σ R ≤ 12` |
| AT-RES-01e | PV-RES-01 | F3 | — | Confirmar: cero Kardex, facturas, CxC y asientos |
| AT-RES-01f | PV-RES-01 | F3 | — | Producto con control de disponibilidad apagado: no se reserva (`R = 0`) y la confirmación no falla (DEC-02) |
| AT-RES-01g | PV-RES-01 | F3 | ESC-13.0 | **OPS-01 resuelta:** Confirmar con la empresa sin vigencia configurada se rechaza en backend con el mensaje definido; el pedido sigue Borrador; sin reservas, Kardex, factura, CxC ni contabilidad; al configurarla y reintentar se confirma y se revalidan configuración e inventario; crear y editar el Borrador siguen permitidos |
| AT-RES-02a | PV-RES-02 | F3 | ESC-02.2 | Faltante 8 y disponible 10: completar 8 ⇒ `R = 20`, `F = 0`, `Disp. = 2` |
| AT-RES-02b | PV-RES-02 | F3 | ESC-02.3 | Completar más que el faltante o que el disponible: 422 sin cambios |
| AT-RES-02c | PV-RES-02 | F3 | — | Reintento con el mismo `ClientRequestId`: un solo efecto; no modifica el vencimiento |
| AT-RES-03a | PV-RES-03 | F3 | ESC-13.1 | Con vigencia de `N` días configurada, `expires_on = hoy_empresa + N`; queda registrada y auditable (**VER-11**) |
| AT-RES-03b | PV-RES-03 | F3 | ESC-13.2 | Fecha vencida: la reserva sigue comprometida, indicador «Vencida»; ningún proceso libera; sin Kardex |
| AT-RES-03c | PV-RES-03 | F3 | ESC-13.3 | Extender con fecha válida: historial; fecha inválida: 422 |
| AT-RES-03d | PV-RES-03 | F3 | — | El cálculo usa el calendario de la empresa (ADR-034), no el huso del servidor |
| AT-RES-04a | PV-RES-04 | F3 | ESC-05.1 | Liberar `q` con motivo: `R` baja `q`, `F` sube `q`; sin Kardex |
| AT-RES-04b | PV-RES-04 | F3 | — | Sin motivo: 422 |
| AT-RES-04c | PV-RES-04 | F3 | ESC-05.2 | Reintento y concurrencia: un solo efecto |
| AT-RES-04d | PV-RES-04 | F3 | — | Liberar más que `R`: 422 sin cambios |
| AT-RES-04e | PV-RES-04 | F4 | ESC-05b | Con `P > I + R_después` la liberación se rechaza con `exceso = P − (I + R_después)`; tras revertir al menos ese exceso se acepta |
| AT-RES-04f | PV-RES-04 | F5 | — | Liberar lo que cubre una selección activa (`S`): 422 |
| AT-RES-04g | PV-RES-04 | F6 | ESC-03.3 | Lo ya consumido por facturación no es liberable (`R` excluye lo consumido) |
| AT-RES-05a | PV-RES-05 | F2 | ESC-14.1 | Venta directa que consumiría stock reservado: rechazada |
| AT-RES-05b | PV-RES-05 | F2 | ESC-14.2 | Ajuste de egreso: rechazado; `Fís.` intacta |
| AT-RES-05c | PV-RES-05 | F2 | ESC-14.3 | Transferencia: rechazada |
| AT-RES-05d | PV-RES-05 | F2 | ESC-14.4 | Devolución de compra: rechazada |
| AT-RES-05e | PV-RES-05 | F2 | ESC-14.5 | Anulación de compra con salida: rechazada |
| AT-RES-05f | PV-RES-05 | F2 | ESC-14.6 | Venta con `AllowSellWithoutStock` ON que dejaría la existencia por debajo de la reserva vigente: **rechazada**; la existencia y la reserva no cambian |
| AT-RES-05i | PV-RES-05 | F2 | ESC-14.10–14.12, 14.14–14.15 | Casos A, B, C, E y F de DEC-02: A permitida (17/15); B rechazada (20/15); C permitida (−5/0, ADR-041); E rechazada (10/10); F rechazada por el contrato vigente (0/0) (**VER-13**) |
| AT-RES-05g | PV-RES-05 | F2 | ESC-14.9 | Ninguna operación ajena puede liberar ni consumir la reserva de otro pedido |
| AT-RES-05h | PV-RES-05 | F2 | ESC-14.8 | Reservar con `Free ≤ 0`: rechazado; nunca reservas negativas ni sobre lo comprometido |
| AT-PREP-01a | PV-PREP-01 | F4 | ESC-03.1 | Preparar 8 con `R = 20`: `P = 8`, sin Kardex |
| AT-PREP-01b | PV-PREP-01 | F4 | — | Revertir con motivo: `P` baja; historial |
| AT-PREP-01c | PV-PREP-01 | F4 | — | Preparar `q` con `P + q > I + R`: 422 |
| AT-PREP-01d | PV-PREP-01 | F4 | — | Preparar: cero Kardex y contabilidad |
| AT-FAC-01a | PV-FAC-01 | F5 | ESC-03.2 | Seleccionar `q ≤ Sel`: `S = q`, reserva intacta, factura Borrador |
| AT-FAC-01b | PV-FAC-01 | F5 | — | Selección de un Producto reservable mayor que `Sel`: 422 con indicación de reservar primero |
| AT-FAC-01c | PV-FAC-01 | F5 | ESC-08.1–8.3 | Dos selecciones concurrentes: una gana, la otra 422; `S ≤ R` |
| AT-FAC-01d | PV-FAC-01 | F5 | — | Unidades libres no reservadas de un Producto reservable: rechazo hasta reservar por PV-RES-02 |
| AT-FAC-01e | PV-FAC-01 | F5 | — | Seleccionar: cero `SaleExit`; `R` sin cambio |
| AT-FAC-01f | PV-FAC-01 | F5 | ESC-15.1 | Selección mixta Producto + Servicio: `Sel = R − S` para el producto y `Sel = Pf − S` para el servicio; un solo borrador |
| AT-FAC-01g | PV-FAC-01 | F5 | — | Producto no reservable: `Sel = Pf − S`, exige bodega, no se trata como Servicio (**VER-13**) |
| AT-FAC-02a | PV-FAC-02 | F5 | — | Abrir borrador con origen: campos comerciales de solo lectura; enlace al pedido |
| AT-FAC-02b | PV-FAC-02 | F5 | — | `PUT` directo con otro precio/cantidad/cliente: 422 `…COMMERCIAL_DATA_LOCKED`; borrador intacto |
| AT-FAC-02c | PV-FAC-02 | F5 | — | Venta directa: comportamiento actual sin cambio (regresión) |
| AT-FAC-02d | PV-FAC-02 | F5 | — | Editar pagos, cronograma y fecha de emisión: permitido (**TP-15**) |
| AT-FAC-03a | PV-FAC-03 | F5 | — | Sin caja abierta: no se crea el borrador |
| AT-FAC-03b | PV-FAC-03 | F6 | — | Autorizar: estrategia según el snapshot `EmissionType` de la factura |
| AT-FAC-03c | PV-FAC-03 | F6 | ESC-03.3 | Exactamente un `SaleExit` por línea de producto con bodega |
| AT-FAC-03d | PV-FAC-03 | F5 | — | El pedido no genera comprobante legal |
| AT-FAC-04a | PV-FAC-04 | F6 | ESC-03.3 | `I = 8`, `R` baja 8, `SaleExit −8`, atómico |
| AT-FAC-04b | PV-FAC-04 | F6 | — | Falla el guardado: `I`, `R` y Kardex iguales al estado previo |
| AT-FAC-04c | PV-FAC-04 | F6 | — | Reintento o doble autorización: un solo consumo |
| AT-FAC-04d | PV-FAC-04 | F6 | — | Autorizaciones concurrentes de la misma factura: un ganador; sin duplicados |
| AT-FAC-04e | PV-FAC-04 | F6 | ESC-12.1 | Error de emisión electrónica: `I` y consumo permanecen |
| AT-FAC-04f | PV-FAC-04 | F6 | ESC-15.2 | Factura mixta: consume solo la reserva del producto; un único movimiento de Kardex; el servicio queda facturado |
| AT-FAC-04g | PV-FAC-04 | F6 | ESC-15.2 | Servicio: sin reserva, sin bodega y sin `SaleExit` |
| AT-FAC-04h | PV-FAC-04 | F6 | — | Producto bajo venta sin stock: no consume reservas ajenas, respeta `Quantity_after ≥ ReservedQuantity` y su costo sigue ADR-041 (**VER-13**) |
| AT-FAC-04i | PV-FAC-04 | F6 | ESC-14.13 | Caso D: existencia 20, reservado 15, autorización de 8 del propio pedido ⇒ existencia 12, reserva 7; una sola `SaleExit` |
| AT-FAC-05a | PV-FAC-05 | F5 | ESC-09.3 | Liberar la selección con permiso y motivo: vínculo `Released` con usuario, fecha, cantidad y factura; `S` baja; `R`, existencia, Kardex, CxC y contabilidad sin cambio; el borrador se conserva |
| AT-FAC-05b | PV-FAC-05 | F6 | ESC-09.5 | Autorizar un borrador desvinculado: 422 `…LINK_NOT_SELECTED`; permanentemente no autorizable |
| AT-FAC-05c | PV-FAC-05 | F5 | — | Liberar con vínculo no `Selected`, factura no Borrador, sin motivo o sin permiso: rechazo sin efectos |
| AT-FAC-05d | PV-FAC-05 | F5 | ESC-09.4 | `Released` es irreversible: no se reactiva ni se reutiliza el borrador; la cantidad se vuelve a seleccionar con un borrador nuevo |
| AT-FAC-05e | PV-FAC-05 | F5 | — | Las facturas directas conservan su comportamiento actual |
| AT-ENT-01a | PV-ENT-01 | F7 | ESC-04 | Física: entrega permitida con `Authorized` interna y `0 < q ≤ E_v` |
| AT-ENT-01b | PV-ENT-01 | F7 | ESC-04.1, ESC-12.2 | Comprobante no habilitado: rechazada |
| AT-ENT-01c | PV-ENT-01 | F7 | ESC-04.4 | `q > E_v(antes)`: rechazada por exceder |
| AT-ENT-01d | PV-ENT-01 | F7 | — | Entregar: cero Kardex |
| AT-ENT-01e | PV-ENT-01 | F7 | ESC-12.3a | Habilitada tras `Authorized` |
| AT-ENT-01f | PV-ENT-01 | F7 | ESC-16.1 | Contra un comprobante que perdió habilitación (`E_v = 0`): rechazada |
| AT-EDI-01a | PV-EDI-01 | F3 | ESC-07.1 | Con `I = 0`: precio/descuento nuevos aplican al saldo; historial con antes/después |
| AT-EDI-01b | PV-EDI-01 | F5 | — | Línea con `S > 0`: edición comercial rechazada (**AMB-10**) |
| AT-EDI-01c | PV-EDI-01 | F3 | — | `Q_nuevo < X` rechazado (se amplía a `I`, `S` en AT-EDI-01b y 01g) |
| AT-EDI-01d | PV-EDI-01 | F3 | — | Sin motivo: rechazo |
| AT-EDI-01e | PV-EDI-01 | F3 | ESC-07.2 | Con `I = 0`: bajar `Q` de modo que `R > Q − X − I` libera la reserva excedente asociada y se cumple INV-03 |
| AT-EDI-01f | PV-EDI-01 | F4 | ESC-05b | Con preparación en exceso: exige reconciliar primero |
| AT-EDI-01g | PV-EDI-01 | F6 | ESC-07 | Con `I > 0`: la factura queda intacta y `Q_nuevo ≥ I + S + X` |
| AT-CAN-01a | PV-CAN-01 | F3 | ESC-06.1 | Cancelar `q ≤ Pf − S` (con `I = 0`): `X` sube; `R ≤ Q − X − I`; sin Kardex |
| AT-CAN-01b | PV-CAN-01 | F4 | — | Preparación en exceso: exige revertir primero |
| AT-CAN-01c | PV-CAN-01 | F6 | — | Cancelar cantidad facturada: 422 |
| AT-CAN-01d | PV-CAN-01 | F3 | — | Cancelar todo sin facturar: estado Cancelado |
| AT-CAN-01e | PV-CAN-01 | F5 | — | Cancelar cantidad seleccionada: 422 |
| AT-REC-01a | PV-REC-01 | F8 | ESC-10a | Pedido Confirmado (A1): `I` baja, `Ia` sube, `Pf` y `F` suben por lo anulado; `R` no se reabre; el vínculo `Consumed` se conserva; no se crea factura; sin Kardex del pedido |
| AT-REC-01b | PV-REC-01 | F8 | ESC-11.1 | Devolución: `Dv = 3`; sin nuevo saldo ni reserva; `E_v = 0` |
| AT-REC-01c | PV-REC-01 | F8 | ESC-10, ESC-11 | Las reservas no se reabren ni se restablecen automáticamente |
| AT-REC-01d | PV-REC-01 | F8 | ESC-10d | `Esr > 0`: incidencia visible en lista y detalle; entrega original conservada; el Confirmado no cierra |
| AT-REC-01e | PV-REC-01 | F8 | ESC-10, ESC-11 | Reporte con ambos hechos sin contradicción: `Ih` e `I` |
| AT-REC-01f | PV-REC-01 | F8 | ESC-17 | Pedido Cerrado + anulación posterior: continúa Cerrado, `Pf = 0`, sin reservas ni saldo, incidencia visible |
| AT-REC-01g | PV-REC-01 | F8 | ESC-16.5 | Regularización M1: registro con usuario, fecha, motivo y referencias; `Esr = 0` solo por cobertura verificada; sin Kardex/CxC/contabilidad |
| AT-REC-01h | PV-REC-01 | F8 | — | Modalidades M2–M4 rechazan con `…MODALITY_BLOCKED` sin registrar nada |
| AT-REC-01i | PV-REC-01 | F8 | — | No existe cierre manual de la incidencia; M1 no está disponible en un pedido Cerrado |
| AT-EST-01a | PV-EST-01 | F1 | — | Transiciones Borrador↔Cancelado; las no listadas se rechazan |
| AT-EST-01d | PV-EST-01 | F3 | ESC-01 | Borrador → Confirmado idempotente; Confirmado → Cancelado solo con `Ih = 0` |
| AT-EST-01c | PV-EST-01 | F7 | — | Cada indicador (reserva, preparación, facturación, entrega) = su fórmula de §9.1 |
| AT-EST-01b | PV-EST-01 | F8 | ESC-06.3 | Cerrado con facturación parcial + cancelación; un pedido con `Ih > 0` nunca queda Cancelado |
| AT-CON-01 | PV-RES-01 | F3 | — | Confirmar/completar/liberar concurrentes de pedidos con los mismos ítems y bodegas en orden opuesto: **sin deadlock** y sin sobre-reserva |
| AT-CON-02 | PV-FAC-04 | F6 | — | Dos autorizaciones simultáneas de la misma factura con origen: **sin doble autorización**; un solo conjunto de `SaleExit` y un consumo |
| AT-CON-03 | PV-FAC-04 | F6 | — | Autorizaciones concurrentes de dos facturas del mismo pedido sobre la misma reserva: **sin doble consumo**; `Σ consumido ≤ R` |
| AT-CON-04 | PV-FAC-04 | F6 | — | Fallo forzado antes del commit: **rollback correcto**; `I`, `R`, `S`, Kardex y `ReservedQuantity` iguales al estado previo |
| AT-CON-05 | PV-RES-05 | F6 | — | Ventas directas concurrentes con la autorización de un pedido sobre el mismo producto y bodega: sin deadlock, serialización correcta, ventas directas con su comportamiento actual y reservas protegidas |
| AT-CON-06 | PV-RES-05 | F2 | ESC-14.6, 14.12 | Ventas sin existencias concurrentes con reservas: nunca dejan `Quantity < ReservedQuantity`; sin reservas permiten saldo negativo con la valoración de ADR-041 idéntica a la actual; la comprobación ocurre bajo el lock (**VER-13**) |
| AT-CON-07 | PV-FAC-05 | F6 | ESC-09 | Liberación de la selección frente a la autorización concurrente: **exactamente una** tiene efecto |
| AT-SEC-01a | PV-SEC-01 | F1 | — | Cada operación sin su permiso: 403 (por operación, en la fase que la introduce) |
| AT-SEC-01b | PV-SEC-01 | F1 | — | Solo `sales.update`: 403 en pedidos |
| AT-SEC-01c | PV-SEC-01 | F1 | — | Otra sucursal/empresa: 404 |
| AT-NAV-01 | PV-NAV-01 | F1 | — | «Gestión de Ventas» solo con permiso; `/sales` y `/sales/returns` sin cambios |
| AT-FUT-01 | PV-FUT-01 | F1 | — | Revisión del diff (en cada fase): sin enums, tablas, rutas ni menús de documentos futuros |

## Anexo A — Matriz de trazabilidad

Columnas según el encargo: (1) ID · (2) decisión aprobada · (3) comportamiento exacto · (4) propietario · (5) contrato
existente · (6) desarrollo necesario · (7) archivos previstos · (8) efectos prohibidos · (9) escenario/prueba y fase ·
(10) estado documental · (11) fase del [plan](../agent/PEDIDOS-VENTA-V1-IMPLEMENTATION-PLAN-01.md).

**La columna (7) lista rutas *previstas* y nunca autoriza modificar un archivo.** Los patrones y los nombres no
confirmados son orientativos («(nuevo)» = no existe hoy); antes de implementar cada fase se exige una **lista exacta de
rutas, revisada y aprobada por el propietario** (plan §3). Las rutas existentes citadas se contrastaron con el repositorio.

| (1) ID | (2) Aprobado | (3) Comportamiento | (4) Propietario | (5) Contrato existente | (6) Desarrollo | (7) Archivos previstos | (8) Prohibido | (9) Prueba (fase) | (10) Estado | (11) Fase |
|---|---|---|---|---|---|---|---|---|---|---|
| PV-COM-01 | Encargo §3 | §7.1 | Ventas/Pedido (nuevo) | `IPricingResolver`, `SalesLinePackagingResolver`, `SalesWarehouseGuard`, `PaymentTermSnapshot` | Agregado, casos de uso, UI | `Domain/Modules/Sales/Entities/SalesOrder*.cs` (nuevo), configuración EF (nuevo), controlador (nuevo), `frontend/src/modules/sales/orders/` (nuevo) | Reservar/Kardex/impuestos legales al guardar | AT-COM-01..03 (F1) | Documentado | F1 |
| PV-RES-01 | Encargo §3; DEC-02, DEC-03 | §10.1 | Inventario + Pedido | `IStockRepository.AcquireResourceLockAsync`, `CurrentStock.ReservedQuantity`, `SaleStockPolicy` | Libro de reservas, confirmación con vencimiento calculado | `CurrentStock.cs`, `IStockRepository.cs`, `StockRepository.cs` (FROZEN, extensión mínima) + nuevos | Sobre-reservar, reservas negativas, servicios, Kardex | AT-RES-01a..g (F3); AT-CON-01 (F3); ESC-01 | Documentado — VER-11, VER-13 pendientes (OPS-01 resuelta) | F2, F3 |
| PV-RES-02 | Encargo §3 | §10.2 | Inventario + Pedido | Ídem | Operación manual | Nuevos en Sales/Inventory | Reserva automática del ingreso | AT-RES-02a..c (F3); ESC-02 | Documentado | F3 |
| PV-RES-03 | Encargo §3; DEC-03 | §10.3 | Pedido + Configuración | `ICompanyClock`, ADR-034, `OrgSetting`/`IOperationalPreferencesResolver` | Vigencia por empresa, indicador, extensión | Candidatos: `OrgSettingKeys.cs`, `InventoryConfigurationDefinitions.cs`, `IOperationalPreferencesResolver.cs`, `UpdateOperationalPreferencesCommandHandler.cs` (techo ADR-046) | Liberación automática; valor predeterminado silencioso; sistema paralelo de parámetros | AT-RES-03a..d (F3); ESC-13 | Documentado — VER-11 pendiente (OPS-01 resuelta) | F3 |
| PV-RES-04 | Encargo §3 | §10.4 | Pedido + Inventario | `CurrentStock.ReleaseReserve` (no idempotente) | Operación idempotente | Nuevos + `CurrentStock.cs` | Kardex/contabilidad; cancelar `Q` | AT-RES-04a..d (F3), 04e (F4), 04f (F5), 04g (F6); ESC-05 | Documentado | F3 (+ pruebas F4–F6) |
| PV-RES-05 | Encargo §3; DEC-01, DEC-02 | §10.5, §9.5 | Inventario | `ApplyKardexMovement`, `AppendMovementAsync`, ADR-041 | Regla de egreso físico `Quantity_after ≥ ReservedQuantity` con cuatro casos | `CurrentStock.cs`, `StockRepository.cs`; mensajes en ajustes/transferencia/devolución de compra (candidatos) | Regla global que prohíba saldos negativos; cambiar costos ADR-041 | AT-RES-05a..i (F2), AT-CON-06 (F2); ESC-14 | Documentado — **VER-13** pendiente | F2 |
| PV-PREP-01 | Encargo §3 | §11 | Pedido | — | Libro de preparación | Nuevos | Kardex, contabilidad | AT-PREP-01a..d (F4); ESC-03.1, ESC-05b | Documentado | F4 |
| PV-FAC-01 | Encargo §3 | §12.1 | Pedido | `ICurrentCashSession`, `CreateSalesDraftCommand` | Selección + vínculos | Nuevos | Consumir al seleccionar | AT-FAC-01a..g (F5); ESC-03.2, ESC-08.1–8.3, ESC-15.1 | Documentado — VER-03, VER-04, VER-13 | F5 |
| PV-FAC-02 | Encargo §3; DEC-01 | §12.2 | Ventas (FROZEN) | `UpdateSalesDraftCommand`, `SalesPage` `?invoiceId=` | Comando desde pedido, guard, UI | Candidatos: `SalesDraftUseCases.cs`, `SalesDtos.cs`, `SalesController.cs`, `useSalesPage.ts`, `SalesPage.tsx` | Bloquear venta directa | AT-FAC-02a..d (F5) | Documentado — VER-03, VER-04 (+TP-15) | F5 |
| PV-FAC-03 | Encargo §3 | §12.3 | Ventas | Estrategias, secuenciales, `ElectronicDocuments`, `ISriTaxResolver` | Ninguno propio | Ninguno | Segundo motor, emisión desde el pedido | AT-FAC-03a,d (F5); 03b,c (F6) | Documentado | F5, F6 |
| PV-FAC-04 | Encargo §3; DEC-01, DEC-02 | §12.4 | Ventas + Inventario | `AuthorizeSalesInvoiceHandler`, `SaleExit` | Servicio de consumo; productos y servicios | Candidatos: `AuthorizeSalesUseCases.cs`, `StockRepository.cs` | Consumo parcial; Kardex duplicado; `SaleExit` de servicios | AT-FAC-04a..i (F6), AT-CON-02..05 (F6); ESC-03.3, ESC-12.1, ESC-15 | Documentado — VER-05, VER-06, VER-08, VER-13; T1/T2 sin elegir | F6 |
| PV-FAC-05 | Encargo §3; DEC-04 | §12.5 | Pedido + Ventas | `SalesInvoice.Cancel` solo `Authorized` | Liberación manual del vínculo + guard | Nuevos; guard en `AuthorizeSalesUseCases.cs` (candidato, FROZEN) | Alterar ciclo de `SalesInvoice`; liberación automática | AT-FAC-05a,c,d,e (F5), 05b (F6), AT-CON-07 (F6); ESC-09 | Documentado — VER-05, VER-06 | F5 (no autorizable), F6 (guard) |
| PV-ENT-01 | Encargo §3 | §13 | Pedido | `ElectronicDocument.CurrentState`, `SalesInvoiceDto.ElectronicStatus` | Libro de entregas, regla de habilitación | Nuevos | Segunda salida de Kardex | AT-ENT-01a..f (F7); ESC-04, ESC-12.2–12.3a | Documentado — VER-02 | F7 |
| PV-EDI-01 | Encargo §3 | §16 | Pedido | ADR-022 (Entity Audit) | Edición e historial | Nuevos | Alterar facturado/facturas | AT-EDI-01a,c,d,e (F3), 01f (F4), 01b (F5), 01g (F6); ESC-07 | Documentado (protección de selección: AMB-10) | F1, F3, F4, F5, F6 |
| PV-CAN-01 | Encargo §3 | §14 | Pedido | — | Cancelación | Nuevos | Alterar facturas/entregas | AT-CAN-01a,d (F3), 01b (F4), 01e (F5), 01c (F6); ESC-06 | Documentado | F1, F3, F4, F5, F6 |
| PV-REC-01 | Encargo §3; DEC-05 | §15, §9.4 | Pedido | `CancelSalesInvoice`, `AuthorizeSalesReturn` | Reconciliación derivada, incidencia y regularización M1 | Nuevos (solo lectura sobre Ventas) | Kardex/reservas desde el pedido; reaperturas; cierre manual de incidencias | AT-REC-01a..i (F8); ESC-10, ESC-11, ESC-16, ESC-17 | Documentado — M2–M4 **bloqueadas** (VER-12) | F8 |
| PV-EST-01 | Encargo §3; DEC-05 B1 | §6, §9 | Pedido | — | Estados e indicadores | Nuevos | Contadores desconectados; estados nuevos | AT-EST-01a (F1), 01d (F3), 01c (F7), 01b (F8) | Documentado | F1, F3, F7, F8 |
| PV-SEC-01 | Encargo §3 | §2 | Seguridad | `perm:*`, `KernelRegistry` | Catorce claves propuestas | `SalesOrderPermissions.cs` (nuevo); `KernelRegistry.cs` (candidato, VER-07) | Reutilizar `sales.update`; modificar el catálogo real | AT-SEC-01a..c (F1; 01a por operación en su fase) | Documentado (claves = propuesta TP-12) | F1+ |
| PV-FUT-01 | Encargo §3 | §7.2 | Arquitectura | — | Límites de dominio | — | Cotizaciones, enums, menús | AT-FUT-01 (F1, revisión en cada fase) | Documentado | F1 |
| PV-NAV-01 | Encargo §1 | §3.1 | Navegación | `[NavItem]`, `RouteAccessGuard` | Ítem de menú y rutas | `SalesModule.cs`, `catalogRoutes.tsx`, `es.json`, `en.json` | Renombrar Facturación | AT-NAV-01 (F1) | Documentado | F1 |

## Anexo B — Decisiones, definiciones operativas, ambigüedades y verificaciones

### B.1 Decisiones del propietario ya aprobadas (`DEC`)

**DEC-01 a DEC-06 están APROBADAS.** Ya no son decisiones comerciales pendientes. Su aprobación no autoriza programar.
Contenido consolidado en el [ADR-049 §3.2](ADR-049-sales-order-v1-architecture.md).

| ID | Aprobado | Resumen | Dónde se aplica |
|---|---|---|---|
| DEC-01 | Opción A | Extensiones mínimas controladas de Ventas e Inventario; sin refactor general; autorización y lista exacta de archivos por fase; sin regresiones | §10.5, §12; ADR §11 |
| DEC-02 | Opción A | Reservas = existencias físicas reales; protegidas frente a ventas directas y operaciones ajenas; `AllowSellWithoutStock` y ADR-041 intactos; discriminación de cuatro casos | §9.5, §10.1, §10.5, §12.4 |
| DEC-03 | Opción B | Vigencia por empresa en días calendario; calculada al confirmar con el calendario de negocio; sin valores silenciosos | §10.3; ESC-13 |
| DEC-04 | Opción A | Liberación manual de la selección; vínculo `Released` irreversible; borrador no autorizable; sin ampliar el ciclo de `SalesInvoice` | §12.5; ESC-09 |
| DEC-05 | A1 + B1 + C1 | Anulación devuelve saldo en Confirmado; Cerrado se conserva; incidencia auditada y regularización con evidencia real | §6, §9, §15; ESC-10, ESC-16, ESC-17 |
| DEC-06 | Opción B (expresa) | Una sola migración inicial consolidada; sin migraciones incrementales por fase | ADR §8, §11; plan §3 |

### B.2 Definición operativa (`OPS`)

| ID | Estado | Resumen |
|---|---|---|
| **OPS-01** | **APROBADA / RESUELTA** | Sin vigencia de reservas configurada: el Borrador se crea y edita; **Confirmar se rechaza** sin efectos, con mensaje que remite a Preferencias operativas; sin duración predeterminada ni fecha manual; validación en backend y UI; al configurarla se puede reconfirmar con revalidación (§10.3). **VER-11** sigue pendiente |

### B.3 Propuestas técnicas (`TP`) — pendientes de aprobación

Listadas y justificadas en el [ADR-049 §5](ADR-049-sales-order-v1-architecture.md): TP-01 a TP-20. Mientras el
propietario no las apruebe permanecen como propuestas (incluidas TP-05 y TP-16, que además exigen evidencia).

### B.4 Interpretaciones conservadoras (`AMB`)

Son interpretaciones de trabajo, **no decisiones definitivas**; el propietario puede reemplazarlas.

| ID | Texto del encargo | Interpretación adoptada hasta que el propietario indique otra |
|---|---|---|
| AMB-01 | «Fechas» (PV-COM-01) | Solo la fecha del pedido; no se agrega «entrega prometida» u otras |
| AMB-02 | «Condiciones comerciales y de pago» | `PaymentTerm` como snapshot (ADR-033); medios de cobro reales quedan en la factura |
| AMB-03 | «Bodega correspondiente» | Bodega por línea; sin bodega predeterminada del pedido |
| AMB-04 | Cierre del pedido | Lo evalúa el sistema por la condición de §6; sin botón manual |
| AMB-05 | Entregas tras una devolución | La devolución reduce lo entregable por línea de factura (`E_v`); no habilita entregas adicionales |
| AMB-06 | Extensión de vencimiento | Motivo opcional; historial obligatorio |
| AMB-07 | «Corregir o revertir preparación» | Revertir exige motivo; la corrección es revertir y volver a preparar |
| AMB-08 | Observaciones y vencimiento de la factura con origen | Bloqueados salvo que el propietario los incluya en TP-15 |
| AMB-09 | «No seleccionar más de lo pendiente y reservado» aplicado a Servicios | Los servicios no reservan; se seleccionan hasta `Pf − S` |
| AMB-10 | «Protección de cantidades seleccionadas para facturar» | Una línea con selección activa no admite edición comercial hasta autorizar o liberar la selección |
| AMB-11 | Vigencia al completar o extender (DEC-03) | Completar no modifica el vencimiento vigente; extender aplica a las reservas vigentes del pedido |

### B.5 Contratos técnicos pendientes (`VER`) — se comprueban en la Fase 0 del plan

No son nuevas decisiones comerciales: cada una verifica cómo implementar una regla ya aprobada.

| ID | Qué comprobar |
|---|---|
| VER-01 | Cómo `StockAdjustment`/`StockTransfer` obtienen su correlativo (`ADJ-NNNN`) y si un `DocType` nuevo es posible con la API pública de secuencias FROZEN ([ADR-019](ADR-019-document-sequence-infrastructure.md)) |
| VER-02 | Que no exista flujo de anulación SRI para facturas de Ventas y que `ElectronicDocument.CurrentState` sea suficiente como fuente fiscal |
| VER-03 | Si `CreateSalesDraft` valida `UnitPrice` contra `IPricingResolver` o acepta el del cliente |
| VER-04 | Contrato real de `loadForEdit`/`salesInvoiceHydration` en el frontend para bloquear campos |
| VER-05 | Si `IDocumentSequenceRepository.CaptureNextAsync` participa de la transacción ambiente |
| VER-06 | Que el reintento `SaveChangesWithSequenceRetryAsync`/`RecoverFromConflictAndRetrackAsync` restaure vínculos y reservas |
| VER-07 | Registro de permisos/menú en `KernelRegistry` y sincronización de `ui_nav_items` |
| VER-08 | Causa raíz de CI-1201 antes de tocar concurrencia de Ventas |
| VER-09 | Campos de `CompanyPrecisionPolicy` que gobiernan cantidad, precio y porcentaje operativo |
| VER-10 | Alcance exacto del congelamiento de Inventario (STATUS lo menciona; FEATURES no lo marca) |
| VER-11 | Mecanismo oficial de la vigencia por empresa (DEC-03): sección, key, validador, rango y resolver (`OrgSetting`/`IOperationalPreferencesResolver`), y cómo agregarla sin superar el techo de ADR-046 del handler de preferencias |
| VER-12 | Contratos oficiales de las modalidades de regularización distintas de M1: (a) reingreso físico contra una factura anulada, (b) ajuste de inventario como evidencia, (c) recuperación de la habilitación del comprobante |
| VER-13 | Que la regla `Quantity_after ≥ ReservedQuantity`, la excepción del consumo propio y los cuatro casos de §9.5 son implementables en el punto central **bajo el lock** sin alterar ADR-041 ni su valoración; confirmar los puntos de integración de §9.5 y la elegibilidad de productos no reservables |
| VER-14 | Procedimiento de regeneración de la baseline única (DEC-06): conserva nombre, objetos raw SQL y pruebas de vigilancia; comparación de catálogo en base desechable |

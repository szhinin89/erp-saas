# Anticipos de Clientes V1 — diseño funcional y técnico mínimo (CUSTOMER-ADVANCES-V1-DESIGN-01)

**DISEÑO FUNCIONAL APROBADO / PROPUESTA TÉCNICA DEFINIDA / IMPLEMENTACIÓN NO AUTORIZADA.** El alcance de ERP V1.1 está aprobado (D-12). El mecanismo técnico quedó definido como propuesta en §14, sobre la evidencia de código de §13; permanecen las autorizaciones y los riesgos no comprobados de §14.5.

Solo documentación. El diseño técnico de este documento es **PROPUESTA** salvo donde se marca **APROBADO**. Ninguna fase, migración, endpoint, pantalla ni permiso está autorizado.

Fecha: 2026-11-24 (revisión 4: aprobación funcional del propietario, §0.1; cierre técnico, §13–§14). Responde al vacío registrado como `O-14` / `C-08` en SPA ([contrato §20.4](SPA-V1.1-CONTRACT-SPEC-01.md#204-anticipos-de-clientes-proceso-independiente-y-transversal-dir-ssot-01), [REG §7](SPA-V1.1-APPROVED-DECISIONS-01.md#7-dir-ssot-01--anticipos-de-clientes-como-proceso-transversal)) con el alcance aprobado en §0.1. Este documento no modifica SPA ni actualiza `O-14` allí, y no es una SSOT: una SSOT de módulo nace cuando existen código y pruebas ([enforcement](../architecture/enforcement.md#ssot-por-módulo)). Un ADR, un plan por fases y la SSOT requerirán instrucción posterior del propietario.

**Actualización 2026-11-26 (prevalece sobre los estados de autorización anteriores de este documento):** [ADR-052](ADR-052-customer-advances-v1-architecture.md) fue **aprobado**; donde precisa o sustituye una propuesta técnica de aquí (por ejemplo, el destino opcional, TA-02), rige el ADR. Solo la fase CA-F1 del [plan](../agent/CUSTOMER-ADVANCES-V1-IMPLEMENTATION-PLAN-01.md) está autorizada; estado en [STATUS](../../STATUS.md).

Estado de entrega: [STATUS](../../STATUS.md) · Cola: [WORK_QUEUE](../agent/WORK_QUEUE.md) · Índice: [decisiones](README.md).

## 0. Cómo leer este documento

| Etiqueta | Significado |
|---|---|
| **EXISTENTE** | Verificado en código, pruebas o SSOT vigentes del repositorio. |
| **APROBADO** | Decisión funcional expresa del propietario (2026-11-24, §0.1), solo dentro de su alcance literal. |
| **PROPUESTO** | Diseño o mecanismo técnico recomendado aquí; no existe y no está aprobado ni autorizado; verificado contra el código en §13–§14, salvo lo marcado como no comprobado. |
| **PENDIENTE** | Validación o autorización específica aún no otorgada (§11, `D-xx`). Sin ella no se puede implementar. |

Fuentes revisadas: [ADR-035](ADR-035-supplier-payment-unapplied-advance.md) (solo precedente), [receivables.md](../architecture/receivables.md), [accounting.md](../architecture/accounting.md), [backend.md](../architecture/backend.md) (idempotencia, locks, orden canónico), `DIR-SSOT-01`, `O-14`, y el código de `Payment`, `SalesReceivable`, `SupplierCredit`, `SupplierCreditRefundTransaction`, controladores `FinancePayments`/`SupplierCredit` y bootstrap contable.

---

## 0.1 Aprobación del propietario y estado documental (2026-11-24)

**Estado: DISEÑO FUNCIONAL APROBADO / PROPUESTA TÉCNICA DEFINIDA / IMPLEMENTACIÓN NO AUTORIZADA.** Alcance V1.1 aprobado (D-12). Pendientes: las autorizaciones y los riesgos no comprobados de §14.5.

El propietario aprobó expresamente el **2026-11-24** los siguientes siete criterios funcionales. Quedan registrados aquí con ese alcance literal; los mecanismos técnicos que los desarrollan en este documento siguen siendo **PROPUESTOS** (verificados contra el código en §13–§14) y no autorizados.

| # | Criterio funcional **APROBADO** (2026-11-24) | Decisión relacionada |
|---|---|---|
| 1 | **Finance** es el módulo propietario del proceso de Anticipos de Clientes. | D-01 |
| 2 | Se utiliza **`CustomerAdvance`** con **un único libro de movimientos**. | D-02 |
| 3 | Los anticipos pueden aplicarse **parcial o totalmente** a CxC existentes. | §5 |
| 4 | Las anulaciones, reversiones y devoluciones requieren **controles, autorización y evidencia**. | D-11 |
| 5 | El saldo pertenece **al cliente y a la empresa**. | §4 |
| 6 | La interfaz se ubica en **Finance, junto a Cuentas por Cobrar**. | D-03 |
| 7 | En V1 **no se modifica el flujo de emisión de facturas** para aplicar anticipos. | D-06 |

**Regla transversal aprobada: Tenant, empresa y sucursal.**

- **Tenant:** aislamiento obligatorio.
- **Empresa:** propietaria de la operación, del saldo y de los efectos contables.
- **Sucursal:** identifica el **origen** de la operación y permite reportes, **sin restringir automáticamente** el uso del anticipo entre sucursales de una misma empresa.
- Las entidades conservan las referencias correspondientes según su responsabilidad, reutilizando los identificadores oficiales del ERP (`TenantId`, `CompanyId`, `BranchId`) y evitando duplicaciones innecesarias.
- Una **aplicación entre sucursales de la misma empresa es válida** cuando cumple las reglas de cliente, saldo, permisos y documentos.
- **No se permiten aplicaciones entre empresas.**

Esto resuelve funcionalmente D-07. El mecanismo (de dónde sale el `BranchId` y cómo se evita que el filtro operativo por sucursal oculte el anticipo a otra sucursal) es **PROPUESTO** en §3 y §5 y fue verificado contra el código (§13.4): no hay bloqueo técnico.

**D-05 — Caja/Bancos: APROBADA (funcional, 2026-11-24).**

1. **Efectivo:** el ingreso o devolución de un anticipo genera el movimiento correspondiente en el turno de caja.
2. **Transferencia bancaria:** el ingreso o devolución se registra contra la cuenta bancaria correspondiente, sin crear movimientos de caja.
3. **Sucursal:** identifica el origen de la operación para trazabilidad y reportes; no limita el saldo del anticipo entre sucursales de una misma empresa.
4. **Empresa:** mantiene la propiedad del anticipo y de sus efectos financieros.
5. **Contabilidad:** una operación financiera no puede producir contabilización duplicada.

Verificación técnica **realizada** (§13.3, §14.4): el patrón del reembolso de proveedor cubre turno abierto, efectivo y reversas. Permanecen la **autorización de Caja** para `CashReferenceType.CustomerAdvance` y la confirmación operativa del titular del turno (§14.5). La aprobación funcional **no autoriza cambios en Caja, Contabilidad ni otros componentes protegidos**.

**D-04 — Seguridad: APROBADA funcionalmente (2026-11-24).**

- Permisos independientes para registrar, aplicar, revertir y devolver anticipos.
- La devolución requiere permiso específico.
- Se mantiene el aislamiento por tenant y empresa.
- Los cambios de Kernel requieren **autorización técnica separada**; esta aprobación no la otorga ni autoriza implementación. Nombres y catálogo concretos (§7) siguen PROPUESTOS.

**D-08 — Concurrencia: APROBADA como principio (2026-11-24).**

- Orden único y consistente de bloqueos para anticipos y CxC.
- Evitar doble aplicación, saldos negativos y deadlocks.
- El orden técnico quedó definido como propuesta en §14.2 (fila `FOR UPDATE` del anticipo antes de las CxC, sin ciclos). No se modifica `backend.md` ni infraestructura; su edición requiere autorización.

**D-11 — Devoluciones: APROBADA funcionalmente (2026-11-24).**

- Devolver únicamente hasta el saldo disponible, con permiso específico.
- Efectivo: turno de caja correspondiente y fondos suficientes. Transferencia: cuenta bancaria autorizada.
- Motivo, usuario, fecha y referencia obligatorios.
- Reversiones únicamente con evidencia de que el movimiento financiero realmente se deshizo.
- Historial inmutable y auditoría completa.
- Se conservan las distinciones entre anulación administrativa (§6.2), reversa del ingreso (§6.3) y devolución efectiva (§6.4). Las condiciones detalladas de §6 siguen PROPUESTAS; sus condiciones exactas se fijan en el ADR.

**D-09 — RETIRADA (2026-11-24).** No forma parte del desarrollo de Anticipos V1; no genera tareas ni dependencias de integración con SRI. La numeración se conserva solo por trazabilidad.

**D-10 — Contabilidad: APROBADA funcionalmente (2026-11-24).**

- Recepción: Debe Caja/Bancos; Haber Anticipos de clientes.
- Aplicación: Debe Anticipos de clientes; Haber Cuentas por cobrar.
- Devolución: Debe Anticipos de clientes; Haber Caja/Bancos.
- Se utiliza la cuenta existente `2.1.01.002 Anticipos de clientes`.
- Las reversiones usan los mecanismos contables oficiales (§8).
- La verificación técnica contable se realizó (§13.6, §14.3): es expresable con el pipeline oficial. Permanecen la autorización para extender Contabilidad y los datos no inspeccionados (cuentas y reglas por empresa, §14.5); esta aprobación no autoriza cambios en Contabilidad.

**D-12 — Alcance: APROBADA (2026-11-24).** Anticipos de Clientes forma parte del alcance de ERP V1.1. La implementación requerirá **autorización expresa posterior**; no se establecen fechas de desarrollo.

**Pendientes reales.** (1) Autorizaciones para implementar (§14.5): migración de BD, Caja, Contabilidad, Kernel (`D-04`) y `backend.md` (`D-08`), más la configuración de reglas en empresas existentes. (2) Riesgos no comprobados que permanecen abiertos (§14.5): titular del turno, cuentas y reglas por empresa real, exportaciones del frontend, prueba mixta de reversa de cobro y completitud contable. La investigación está en §13 y sus hallazgos se trataron en §14. D-05 y D-10 están aprobadas solo en lo funcional y **no autorizan cambios en Caja, Contabilidad ni otros componentes protegidos**. **Ninguna fase, migración, endpoint, pantalla ni permiso está autorizado.** Un ADR, un plan por fases y la SSOT del módulo requerirán instrucción posterior del propietario. Este registro no modifica SPA: `O-14` no se actualiza aquí.

---

## 1. Alcance y exclusiones

**Dentro (alcance funcional **APROBADO**, §0.1; detalle técnico PROPUESTO):** registrar un anticipo de un cliente sin factura; consultar el saldo disponible por cliente y empresa; aplicarlo total o parcialmente a una o varias CxC existentes; revertir una aplicación; devolver dinero autorizado al cliente; historial y trazabilidad.

**Fuera (PROPUESTO, salvo el flujo de emisión de facturas, cuya exclusión está **APROBADA**):**

- Registrar el anticipo **automáticamente** como venta, factura o ingreso.
- Reservas con depósito, prepago por paquete o sesión, crédito para compras futuras, saldo a favor por devolución de venta, puntos o promociones.
- Aplicarlo al **cobro en el momento de emitir una factura** (medio de pago de `SalesInvoicePayment`): **APROBADO** (`D-06`): en V1 no se modifica el flujo de emisión de facturas; solo se aplica a CxC ya existentes.
- Multimoneda, conversión, intereses, vencimiento o caducidad del anticipo.
- **Anticipos V1 no desarrollará ninguna integración tributaria propia.** Las obligaciones tributarias aplicables a una operación comercial, si las hubiera, corresponden a sus procesos oficiales, no a este módulo.
- Cualquier lógica específica de Spa: SPA solo consumiría el contrato de este proceso ([DIR-SSOT-01](SPA-V1.1-APPROVED-DECISIONS-01.md#7-dir-ssot-01--anticipos-de-clientes-como-proceso-transversal)); no hay saldo monetario en Spa.
- Anticipos de proveedor: ya existen ([ADR-035](ADR-035-supplier-payment-unapplied-advance.md)); no se tocan.

**Propiedad del proceso (**APROBADA**, `D-01`, 2026-11-24).** Módulo propietario: **Finance**. Justificación: Finance ya posee `Payment` (dirección Collection), destinos `CompanyBankAccount`/`CashRegister`, `SupplierCreditRefundTransaction`, `PaymentMethodDestinationPolicy` y los hechos contables `Finance/*`. **No** Ventas: el anticipo nace sin factura y no es un documento de venta. **No** CxC (`SalesReceivable`): es el *destino* de una aplicación, no el dueño de dinero sin documento. Consistente con `DIR-SSOT-01` (no se asigna a Ventas ni a CxC). Ventas/CxC/SPA lo consumen por contrato.

---

## 2. Inventario: qué existe y qué falta

### 2.1 EXISTENTE y reutilizable

| Capacidad | Dónde | Uso en esta propuesta |
|---|---|---|
| Cobro idempotente `ClientRequestId` + huella SHA-256 + índice único `(tenant_id, client_request_id)` | [backend.md § Idempotencia](../architecture/backend.md#idempotencia-de-comandos-financieros-creadores-de-dinero-zh-financial-command-idempotency-01); `RegisterCollectionCommandHandler` | Mismo patrón y mismo `ClientRequestKey` para registrar, aplicar, devolver y revertir. Un comando creador de dinero sin él es defecto bloqueante de PR. |
| Lock pesimista `FOR UPDATE` y orden canónico de locks | [backend.md § Saldos compartidos](../architecture/backend.md#saldos-compartidos-lock-pesimista-antes-de-decidir-zh-collections-receivable-concurrency-01) | Bloqueo del anticipo y de las CxC antes de decidir. |
| Libro de saldo a favor con movimientos append-only, saldo derivado, reversas y devolución | `SupplierCredit` + `SupplierCreditMovement` + `SupplierCreditRefundTransaction` (ADR-035, [Accounting §10](../architecture/accounting.md#10-integración-y-matriz-de-atomicidad)) | **Patrón** a replicar para clientes. No es reutilizable como tabla: está atado a `SupplierId` y a CxP. |
| Destinos de dinero (cuenta bancaria XOR caja), validación de activa/de la empresa | `CompanyBankAccount`, `CashRegister`, `RegisterCollectionCommandHandler` | Mismos destinos y mismas validaciones. |
| Reducción y restauración de saldo de CxC y cuotas | `SalesReceivable.RegisterCollection(amount, userId, installmentId)` / `ReverseCollection(...)` | La aplicación del anticipo llama estos mismos métodos; **no** se agrega otra forma de reducir la CxC. |
| Validación cliente↔CxC, cuota↔CxC, monto ≤ saldo de cuota, 404 fuera de empresa | `RegisterCollectionCommandHandler` | Mismas reglas, extraídas por reutilización (no copiadas) en la implementación. |
| Reparto en cuotas FIFO o cuota indicada | [receivables.md § Registrar un cobro](../architecture/receivables.md#registrar-un-cobro) | Idéntico. |
| Obligación contable durable en la misma transacción, espejo exacto en reverso, gate de período | [Accounting §7, §19](../architecture/accounting.md#7-reversos-fecha-efectiva-e-inmutabilidad) (`AccountingPostingRecorder`, `PostingFactCatalog`) | Nuevos hechos `Finance/*` con el mismo pipeline; sin pipeline paralelo (§15 de Accounting). |
| Cuenta contable **`2.1.01.002 Anticipos de clientes`** (pasivo, acreedora, imputable) | `AccountingBootstrapStep` | Ya sembrada. **Ninguna regla ni traductor la usa hoy** (verificado: única aparición en el bootstrap). |
| Permisos `finance.view`, `finance.create`, `finance.update`, `finance.collections.reverse` | `FinancePermissions` | Base de permisos; ver §7. |
| Entity Audit append-only por agregado | [ADR-022](ADR-022-audit-infrastructure-entity-vs-process.md), `SupplierCreditAuditHandler` | Mismo patrón de auditoría. |
| Aislamiento `TenantId` + `CompanyId` (`ITenantScopedEntity`, `ICompanyOperationalEntity`) y fail-closed 404 | [Seguridad](../architecture/security.md) | Obligatorio en las entidades nuevas. |
| Precisión `numeric(18,2)`, `DateOnly` de negocio, UTC para instantes | [data-standards](../architecture/data-standards.md) | Sin reglas nuevas. |
| Pantalla `/finance/receivables` con `RegisterCollectionModal` y «Consultar / anular cobro»; patrón `ApplySupplierCreditModal`, `RegisterSupplierCreditRefundModal` | `frontend/src/modules/finance` | Plantillas de UI (§9). |

### 2.2 Brechas técnicas reales (no existe)

| # | Brecha | Evidencia |
|---|---|---|
| G-1 | **No hay cobro sin CxC.** `Payment.Apply` exige ≥ 1 línea y `EnsureBalanced` (Σ líneas = monto); el cobro de CxC valida cada línea contra una CxC. Un cobro sin factura es imposible hoy. | `Payment.cs` `Apply`; [receivables.md](../architecture/receivables.md) |
| G-2 | **No hay libro de saldo a favor de cliente.** `SupplierCredit` es solo de proveedor; la devolución de venta reembolsa únicamente `Cash` o `ReceivableCredit`. | `SalesReturnRefundMethod`; [contrato SPA N-03](SPA-V1.1-CONTRACT-SPEC-01.md) |
| G-3 | **No hay hechos contables de anticipo de cliente.** La cuenta `2.1.01.002` existe pero sin `PostingRule`, traductor ni hecho. | Bootstrap; [Accounting §19](../architecture/accounting.md#19-integraciones-durables-accounting-continuity-04) |
| G-4 | **No hay devolución de dinero de cobros.** Está declarada «pendiente de decisión funcional separada». | [receivables.md § Límites vigentes](../architecture/receivables.md#límites-vigentes) |
| G-5 | **El cobro de CxC no toca `CashSession` ni `CashMovement`;** la devolución de crédito de proveedor sí. Un anticipo en efectivo recibido y luego devuelto quedaría asimétrico respecto al cajón (`D-05`). | [receivables.md § Reversa](../architecture/receivables.md#reversa-de-un-cobro); `RegisterSupplierCreditRefundUseCases` |
| G-6 | `Payment` (dirección Collection) no tiene `BranchId`; `SupplierCredit` sí lo hereda del pago. La regla funcional de sucursal ya está aprobada (§0.1, `D-07`); mecanismo verificado (§13.4): el filtro EF es solo tenant + empresa y no impide usar el anticipo entre sucursales de la misma empresa. | `Payment.cs`, `SupplierCredit.cs`, `ICurrentBranch`, `CashMovement.cs` |
| G-7 | El orden canónico de locks de [backend.md](../architecture/backend.md) no incluye el anticipo. | backend.md |

---

## 3. Registro y confirmación de anticipos sin factura

**Modelo (funcional **APROBADO**: `CustomerAdvance` con un único libro de movimientos, `D-02`; campos y mecanismos PROPUESTOS).** Agregado nuevo `CustomerAdvance` en Finance (`TenantId`, `CompanyId`, `BranchId` de origen, `CustomerId` = `BusinessPartner` con rol Customer, `CurrencyCode`, `OriginalAmount`, `AvailableAmount`, fecha de negocio `DateOnly`, destino banco XOR caja, `PaymentMethodId`, referencia externa, motivo/nota, estado) con una colección append-only `CustomerAdvanceMovement`, que conserva también el `BranchId` de cada operación (origen y reportes). **Un solo libro de saldo**, igual que ADR-035 descartó un segundo libro: aquí no existe ninguno de cliente, así que se crea uno y solo uno.

Alternativa descartada: extender `Payment` para permitir remanente sin CxC (como ADR-035 hizo con `SupplierPayment`). Cambiaría el invariante `EnsureBalanced` de un agregado CLOSED usado por todos los cobros, con riesgo de romper cobros existentes. Se recomienda no tocarlo; verificado en §13.1.

**Ciclo de vida (PROPUESTO).** Sin Draft: es una transacción simple de dinero que entra, y la regla vigente es confirmación directa con modal de confirmación y transacción atómica. Estados: `Confirmed` y `Cancelled` (este último solo por anulación administrativa o reversa del ingreso, §6.2–6.3; son operaciones distintas con condiciones distintas). Confirmado es solo lectura; la corrección es un flujo separado y nunca deja dinero recibido sin tratamiento (§6).

**Sucursal (regla aprobada §0.1; mecanismo PROPUESTO).** `BranchId` = sucursal activa del contexto autenticado (`ICurrentBranch`) al registrar, inmutable y solo para origen y reportes; nunca lo elige el cliente ni restringe el uso del anticipo. Tenant y empresa salen del contexto. Las entidades reutilizan `TenantId`/`CompanyId`/`BranchId` oficiales y no duplican otros identificadores de alcance.

**Comando `RegisterCustomerAdvanceCommand`:** `ClientRequestId` (obligatorio), `CustomerId`, `Amount > 0`, `PaymentDate`, `PaymentMethodId`, destino (banco XOR caja, opcional como en cobros), referencia, motivo. Reglas:

1. Idempotencia: misma clave + misma huella → el anticipo original (201); distinta huella → `409 CONFLICT`.
2. Cliente activo de la empresa; destino activo y de la empresa (mismas validaciones de `RegisterCollection`).
3. Monto > 0 con la precisión de `data-standards`.
4. El anticipo nace con `AvailableAmount = OriginalAmount`, un movimiento inicial `Received`, y levanta un único hecho contable (§8).
5. Una sola transacción: anticipo, movimiento, obligación contable, auditoría y outbox.

---

## 4. Saldo disponible por cliente y empresa

- `AvailableAmount` se **deriva** de los movimientos (`Original + Σ reversas de aplicación + Σ reversas de devolución recuperadas − Σ aplicaciones − Σ devoluciones − anulación/reversa del ingreso`); no se escribe suelto. Se persiste como columna de lectura bajo el mismo `SaveChanges`, con `CHECK (available_amount >= 0 AND available_amount <= original_amount)` como última barrera (patrón `SupplierCredit`).
- **Saldo del cliente** = Σ `AvailableAmount` de sus anticipos `Confirmed` en la empresa activa. Consulta de solo lectura `GET …/customer-advances/balance?customerId=` con `TenantId`+`CompanyId`; nunca suma entre empresas ni entre tenants.
- **Aprobado:** el saldo pertenece al cliente y a la empresa. La sucursal **no** lo particiona ni lo restringe: el saldo del cliente es el mismo desde cualquier sucursal de la empresa. La sucursal solo sirve de filtro opcional en reportes.
- Un anticipo agotado (`AvailableAmount = 0`) permanece consultable; no se borra.
- La consulta del saldo es la **única** vía que SPA u otros módulos tendrían ([contrato §20.4](SPA-V1.1-CONTRACT-SPEC-01.md)): sin copiar el estado.

---

## 5. Aplicación a CxC, aplicaciones múltiples y doble consumo

**Comando `ApplyCustomerAdvanceCommand` (la aplicación parcial o total a CxC existentes está **APROBADA**; el comando es PROPUESTO):** `ClientRequestId`, `CustomerAdvanceId`, líneas `{ receivableId, installmentId?, amount }`, fecha efectiva. Una aplicación puede cubrir total o parcialmente una CxC, y varias CxC del **mismo cliente** en un solo comando.

Reglas (todas bajo locks, antes de mutar):

1. Σ líneas ≤ `AvailableAmount` del anticipo; cada línea ≤ saldo de su CxC y de su cuota; Σ > 0. Cualquier línea inválida rechaza el comando completo (`422 DOMAIN_RULE_VIOLATION`).
2. CxC del mismo `CustomerId` del anticipo, **misma empresa**, no cancelada. La CxC puede pertenecer a **otra sucursal** de la misma empresa (válido si cumple cliente, saldo, permisos y documentos); la sucursal de la operación queda registrada. Una CxC de **otra empresa** no se encuentra (404): no se permiten aplicaciones entre empresas.
3. Reparto en cuotas igual que un cobro (FIFO o cuota indicada). Se reutiliza `SalesReceivable.RegisterCollection`; no hay otro camino para reducir una CxC.
4. Se registra **un movimiento `Application` por línea** (append-only), con `ReceivableId`, `InstallmentId` y `LineNumber`; se reduce `AvailableAmount` por la misma cantidad. La idempotencia es por operación: una `ClientRequestId` y unicidad `(tenant, client_request_id, line_number)` (§14.1).
5. **No se crea un `Payment`** ni se dispara `CollectionApplied` (eso debitaría caja de nuevo). La aplicación es un hecho propio (§8).

**Prevención de doble consumo** (tres capas, patrón oficial, sin mecanismo nuevo):

| Capa | Mecanismo |
|---|---|
| Misma intención repetida (doble clic, reintento, timeout) | `ClientRequestId` + huella + índice único; replay devuelve el resultado original sin volver a aplicar. |
| Intenciones distintas que compiten por el mismo anticipo o CxC | Transacción explícita; `FOR UPDATE` de la fila del anticipo (mecanismo único, §14.2) y luego de las CxC en orden ascendente de Id; recargar bajo lock y **recién entonces** decidir. El perdedor reevalúa y recibe `422`. |
| Última barrera | `CHECK (available_amount >= 0)`, token `xmin` y unicidad de `(tenant_id, client_request_id, line_number)` en los movimientos (§14.1). |

**Orden de locks (principio **APROBADO**, `D-08`: orden único y consistente, sin doble aplicación, saldos negativos ni deadlocks; mecanismo PROPUESTO y definido en §14.2):** `FOR UPDATE` de la fila del anticipo (acotado a tenant y empresa), luego `SalesReceivable` en Id ascendente; para efectivo, anticipo → destinos `FOR SHARE` → cuenta → `CashSession FOR UPDATE`. Ningún flujo toma `SalesInvoice` ni `Payment` junto con el anticipo ni CxC junto con `CashSession` (mismo criterio que el cobro). Registrar el anticipo en el orden canónico requiere una edición posterior de [backend.md](../architecture/backend.md) que **este documento no hace** y que **requiere autorización**.

---

## 6. Anulación, reversas y devoluciones

Los anticipos son contabilidad y dinero real: **nada se borra ni se edita**; todo es un movimiento compensatorio inmutable ([Accounting §7](../architecture/accounting.md#7-reversos-fecha-efectiva-e-inmutabilidad)).

**Aprobado (criterio 4 y `D-11`, 2026-11-24):** anulaciones, reversiones y devoluciones requieren controles, autorización y evidencia. Devolución solo hasta el saldo disponible, con permiso específico; efectivo en el turno de caja correspondiente con fondos suficientes; transferencia contra cuenta bancaria autorizada; motivo, usuario, fecha y referencia obligatorios; reversiones solo con evidencia de que el movimiento financiero realmente se deshizo; historial inmutable y auditoría completa. Las condiciones concretas de cada operación, abajo, siguen **PROPUESTAS**; sus condiciones exactas se fijan en el ADR.

**Regla rectora.** Una anulación, reversa o asiento espejo es un hecho **documental/contable**; **no sustituye** el movimiento real de dinero. Ninguna operación de este apartado puede dejar el saldo en cero mientras el dinero recibido sigue en poder de la empresa sin tratamiento: en ese caso la única salida es la **devolución efectiva** (§6.4). Cuando el dinero sí entró o salió de verdad, la operación exige antes la **evidencia** de ese movimiento real (referencia bancaria, movimiento de caja en turno abierto) y se rechaza sin ella.

Son cuatro operaciones distintas que no se sustituyen entre sí:

| Operación | Qué corrige | Dinero real | Saldo | Contabilidad | Permiso (§7) |
|---|---|---|---|---|---|
| **6.1 Reversa de una aplicación** | Aplicación a una CxC | No se mueve | Devuelve lo aplicado | Espejo de `CustomerAdvanceApplied` | `.reverse` |
| **6.2 Anulación administrativa** | Registro sin efecto financiero | El dinero **nunca** se recibió | → 0 | Sin asiento publicado; se cancela la obligación pendiente | `.reverse` |
| **6.3 Reversa del ingreso** | Ingreso ya registrado/contabilizado que se deshace realmente | Se anula de verdad, con evidencia | → 0 | Espejo de `CustomerAdvanceReceived` | `.refund` |
| **6.4 Devolución efectiva** | Dinero que se entrega al cliente | **Sale** de Caja/Bancos | Baja el monto | `CustomerAdvanceRefunded` | `.refund` |

La reversa de una devolución (§6.5) no es otra vía de corrección del ingreso: solo deshace una salida que no llegó a ocurrir.

### 6.1 Reversa de una aplicación (PROPUESTO)

Movimiento `ReversalOfApplication` por la aplicación completa; restaura la CxC con `SalesReceivable.ReverseCollection` (cuota de la aplicación; sin cobrado negativo) y devuelve el saldo al anticipo. Exige CxC sin cancelar y con cobrado suficiente; una operación posterior incompatible bloquea la reversa. Motivo obligatorio (≤ 500). Doble reversa rechazada sin efecto. No toca Caja/Bancos. Precedente: `ReverseCollection`; `ReverseSupplierCreditApplication`.

### 6.2 Anulación administrativa del registro (PROPUESTO, `D-11`)

Solo para un anticipo cuyo **dinero nunca fue recibido** (p. ej. transferencia no acreditada, registro duplicado). Condiciones, todas obligatorias:

1. Íntegro: `AvailableAmount == OriginalAmount`, sin aplicaciones ni devoluciones vigentes.
2. Sin efecto financiero publicado: la obligación contable no tiene asiento publicado (está `Pending` o `Failed`; [Accounting §7](../architecture/accounting.md#7-reversos-fecha-efectiva-e-inmutabilidad) permite cancelar una pendiente sin asiento) y no existe `CashMovement` vinculado.
3. Declaración expresa «el dinero no fue recibido» más referencia de evidencia, ambas auditadas con actor y fecha UTC. Si el dinero sí fue recibido, la operación **no procede**: se usa §6.3 o §6.4.

Efecto: movimiento de sistema `SourceCancelled`, saldo 0, estado `Cancelled`, obligación pendiente cancelada, **sin asiento y sin movimiento de Caja/Bancos**. Un anticipo con asiento publicado nunca se anula administrativamente.

### 6.3 Reversa del ingreso (PROPUESTO, `D-11`)

Para un ingreso ya contabilizado que realmente se deshace (error de captura de cliente, monto o destino, o un ingreso que el banco revierte). Condiciones, todas obligatorias:

1. Íntegro (igual que 6.2, condición 1).
2. **Evidencia del deshacer real**, no solo la intención: banco → referencia del reverso o rechazo bancario de ese ingreso; caja → el efectivo efectivamente retirado del turno, registrado como movimiento compensatorio (ver Efecto). Sin evidencia, se rechaza.
3. Motivo obligatorio y referencia de evidencia, auditados.

Si el dinero recibido **permanece** en la empresa, no procede la reversa: corresponde devolverlo (§6.4). Un error de captura se corrige con reversa más un nuevo registro con nueva `ClientRequestId`.

Efecto: movimiento `IncomeReversed`, saldo 0, estado `Cancelled`; asiento espejo exacto del publicado (período Open de la fecha efectiva, [Accounting §7](../architecture/accounting.md#7-reversos-fecha-efectiva-e-inmutabilidad)). Con destino caja (`D-05` aprobada), movimiento compensatorio en el turno abierto, sujeto a «sin sobregiro» (mecanismo PROPUESTO, verificado en §14.4). Con destino banco no hay movimiento operativo; la evidencia es la referencia bancaria.

### 6.4 Devolución efectiva de dinero (PROPUESTO, `D-11`)

Comando propio con permiso específico; monto ≤ `AvailableAmount`; destino banco/caja activos de la empresa; fecha efectiva, referencia de ejecución (transferencia, cheque o comprobante interno) y motivo obligatorios. Movimiento `Refund` más transacción financiera append-only (destino usado, método, referencia, fecha) bajo lock del anticipo. Con caja (`D-05` aprobada), movimiento de egreso en el turno, con turno abierto y efectivo suficiente (mecanismo PROPUESTO, verificado en §14.4). Cierra G-4 solo para anticipos; la devolución de **cobros ordinarios** sigue fuera ([receivables.md § Límites vigentes](../architecture/receivables.md#límites-vigentes)). Lo ya aplicado a una CxC no se devuelve en dinero: primero se revierte la aplicación (§6.1).

### 6.5 Reversa de una devolución (PROPUESTO, `D-11`)

Distingue una devolución que **no llegó a ejecutarse** de una **efectivamente ejecutada**:

| Caso | Tratamiento |
|---|---|
| **No ejecutada** (transferencia rechazada o devuelta por el banco; efectivo no entregado y reintegrado al cajón) | Reversa administrativa permitida **solo con evidencia de recuperación**: referencia del reverso/rechazo bancario o `CashMovement` compensatorio de ingreso en turno abierto, fecha y **monto igual** al de la devolución. Movimiento `ReversalOfRefund`, asiento espejo del publicado y saldo restituido **exactamente** por el monto de esa devolución. |
| **Ejecutada** (dinero entregado al cliente y no recuperado) | **No se revierte ni se restaura saldo.** Si el cliente reintegra el dinero, es un **nuevo ingreso**: nuevo anticipo con nueva `ClientRequestId`, con su propia evidencia. |

Reglas: sin evidencia de recuperación la reversa se rechaza y el saldo no cambia; **una reversa por devolución** (única por transacción original); se rechaza si la devolución ya fue revertida, si el anticipo fue anulado o si restituir superaría `OriginalAmount`. Nunca un saldo ficticio: lo disponible siempre está respaldado por dinero realmente en poder de la empresa.

### 6.6 Doble devolución, doble reversa y saldos ficticios

| Riesgo | Barrera |
|---|---|
| Doble devolución (doble clic, reintento) | `ClientRequestId` + huella; monto ≤ disponible bajo lock; el perdedor concurrente reevalúa y recibe `422`. |
| Doble reversa (de ingreso, de aplicación, de devolución) | Índice único por movimiento original reversado; estado y saldo revalidados bajo lock. |
| Saldo ficticio | `CHECK (available_amount >= 0 AND available_amount <= original_amount)`; ninguna reversa restituye más que su operación original ni sin evidencia. |
| Anulación que oculta dinero recibido | 6.2 exige no haber asiento ni caja; 6.3 exige evidencia del deshacer real; si no, solo devolución efectiva. |

---

## 7. Historial, trazabilidad y permisos

**Historial (PROPUESTO).** `CustomerAdvanceMovement` es la fuente de verdad: tipo (`Received`, `Application`, `ReversalOfApplication`, `Refund`, `ReversalOfRefund`, `SourceCancelled`, `IncomeReversed`), referencia de evidencia de cada operación financiera (ejecución de la devolución, recuperación, deshacer del ingreso), monto, referencias (`ReceivableId`/`InstallmentId`, transacción de reembolso), `ClientRequestId`, actor (`CreatedBy`), instante UTC, motivo. Los movimientos son append-only. Consulta por anticipo y por cliente; desde la CxC, las aplicaciones recibidas por ese documento. **Entity Audit** por agregado (ADR-022): `CustomerAdvanceReceived/Applied/ApplicationReversed/Refunded/RefundReversed/Cancelled/IncomeReversed`, con actor y fecha UTC, reutilizando la infraestructura existente sin modificarla (FROZEN).

**Permisos (funcional **APROBADO**, `D-04`: permisos independientes para registrar, aplicar, revertir y devolver; la devolución con permiso específico; aislamiento por tenant y empresa; nombres y catálogo PROPUESTOS).** Auth real = `perm:`. Se reutiliza el prefijo `finance.` del Kernel; los permisos nuevos se agregan al catálogo del Kernel con su propio nombre, no se reutiliza un permiso ajeno por conveniencia:

| Operación | Permiso propuesto | Nota |
|---|---|---|
| Consultar saldo e historial | `finance.view` | Existente. |
| Registrar anticipo | `finance.customer-advances.create` | Nuevo; **no** `finance.create` (que registra cobros), porque recibir dinero sin factura es una capacidad distinta. |
| Aplicar a CxC | `finance.customer-advances.apply` | Nuevo. |
| Revertir aplicación (§6.1) y anulación administrativa (§6.2) | `finance.customer-advances.reverse` | Nuevo, del mismo tipo que `finance.collections.reverse`. Sin movimiento real de dinero. |
| Devolución efectiva (§6.4), reversa del ingreso (§6.3) y reversa de devolución (§6.5) | `finance.customer-advances.refund` | Nuevo; todas exigen evidencia de un movimiento real de dinero, por eso se separan de las demás. |

Los permisos nuevos se asignan desde el catálogo del Kernel en la pantalla correspondiente. Cualquier cambio del Kernel LOCKED requiere autorización técnica separada del propietario (`D-04`), no otorgada. El tenant, la empresa y el actor salen siempre del contexto autenticado, nunca del cliente.

---

## 8. Integración contable y Caja/Bancos

**Principio:** un hecho de negocio genera **un** hecho contable; el anticipo no debita dos veces ni se registra automáticamente como ingreso. Se usan las obligaciones durables existentes (`AccountingPostingRecorder`, [Accounting §4, §19](../architecture/accounting.md)): una falla contable posterior no revierte el núcleo operativo; el reverso espeja el asiento publicado y respeta el gate de período (§7). Un asiento espejo **no sustituye** el movimiento real de dinero (§6).

| Hecho (`D-10` aprobada funcionalmente; reglas técnicas PROPUESTAS) | Debe | Haber | Fecha |
|---|---|---|---|
| `Finance/CustomerAdvanceReceived` | Caja o Banco elegido (cuenta vigente del destino; sin destino, cuenta Debe de la regla) | **`2.1.01.002 Anticipos de clientes`** | `PaymentDate` |
| `Finance/CustomerAdvanceApplied` (por aplicación) | `2.1.01.002 Anticipos de clientes` | `1.1.xx` CxC clientes (la misma cuenta que usa `CollectionApplied`) | Fecha efectiva de la aplicación |
| `Finance/CustomerAdvanceRefunded` | `2.1.01.002 Anticipos de clientes` | Caja o Banco real de la devolución | Fecha efectiva |
| Reversas de los tres (6.1, 6.3, 6.5) | Espejo exacto del asiento publicado (§7); sin reglas vigentes | | Fecha efectiva de la reversa |
| Anulación administrativa (6.2) | **Sin asiento**: cancela la obligación `Pending`/`Failed` sin asiento publicado | | — |

Efecto de cada operación (§6) sobre Caja/Bancos, saldo y contabilidad:

| Operación | Caja/Bancos | Saldo | Contabilidad |
|---|---|---|---|
| Registro | Entra dinero (destino) | + Original | `CustomerAdvanceReceived` |
| Aplicación | Ninguno | − aplicado | `CustomerAdvanceApplied` |
| Reversa de aplicación | Ninguno | + aplicado | Espejo de la aplicación |
| Anulación administrativa | Ninguno (el dinero nunca entró) | → 0 | Sin asiento |
| Reversa del ingreso | Caja: compensatorio (`D-05`); Banco: solo evidencia | → 0 | Espejo del ingreso |
| Devolución efectiva | **Sale** dinero | − devuelto | `CustomerAdvanceRefunded` |
| Reversa de devolución (no ejecutada, con evidencia) | Caja: compensatorio de ingreso; Banco: evidencia de la recuperación | + devuelto | Espejo de la devolución |

Notas:

- **EXISTENTE:** la cuenta `2.1.01.002` y el pipeline. **PROPUESTO:** las reglas (`PostingFactCatalog` + bootstrap + traductores; dos líneas fijas con sustitución por el destino, §14.3). Ningún asiento se escribe a mano ni por atajo; las cuentas se resuelven en cada intento, sin fallback; una cuenta/regla faltante deja la obligación `Failed` y reprocesable ([Accounting §6, §17](../architecture/accounting.md)).
- **Sin doble contabilización:** (a) recibir el anticipo no genera `CollectionApplied` ni un asiento de venta, porque no es un `Payment` de CxC; (b) aplicarlo a CxC genera solo `CustomerAdvanceApplied`: la CxC baja sin dinero nuevo y sin segundo Debe a Caja; (c) los movimientos operativos de caja no son fuente contable: «Posting nunca consume `CashMovement`/`CashSession`» ([Accounting §25](../architecture/accounting.md#25-caja-contable-accounting-12c)); (d) este diseño no registra el anticipo como ingreso de venta sino como obligación con el cliente; su tratamiento contable es el aprobado en `D-10` (funcional), con verificación técnica realizada (§14.3) y autorización de Contabilidad pendiente.
- **Caja (`D-05`, APROBADA funcional 2026-11-24):** con efectivo, el ingreso o devolución genera el movimiento correspondiente en el turno de caja; con transferencia bancaria se registra contra la cuenta bancaria, sin movimientos de caja. **Mecanismo PROPUESTO** (verificado en §14.4): `CashMovement` `ManualIncome`/`ManualExpense` internos en el turno abierto, como el reembolso de crédito de proveedor, contabilizados por el documento de origen y no como fuente contable propia; se rechaza sin turno abierto o sin efectivo suficiente ([sin sobregiro](../architecture/backend.md#turnos-de-caja-uno-por-empresa-y-sin-sobregiro)). Banco: sin `CashMovement`; la evidencia es la referencia bancaria. Una operación financiera no puede producir contabilización duplicada. La sucursal identifica el origen y no limita el saldo entre sucursales de la empresa. Permanecen la autorización de Caja y de Contabilidad (§14.5). No autoriza cambios en Caja, Contabilidad ni componentes protegidos.
- **Alcance tributario.** Anticipos V1 no desarrolla ninguna integración tributaria propia; las obligaciones tributarias que pudieran aplicar a una operación comercial corresponden a sus procesos oficiales, no a este módulo.

---

## 9. Interfaz mínima reutilizando patrones

Sin pantallas ni componentes nuevos «de catálogo». Reutilizar y extender, tras la auditoría de reutilización obligatoria antes de código de UI:

| Necesidad | Reutilizar | Cambio |
|---|---|---|
| Registrar anticipo | `RegisterCollectionModal` (cliente, monto, fecha, método, destino, referencia, `useClientRequestId`, `ZhDecimalInput`) | Variante sin selector de CxC. Si el parecido no alcanza, extraer el bloque común, no copiarlo. |
| Aplicar a CxC | `ApplySupplierCreditModal` y la lista de CxC de `/finance/receivables` | Desde la CxC: «Aplicar anticipo» con saldo del cliente visible. |
| Devolver | `RegisterSupplierCreditRefundModal` | Mismos campos (monto, destino, fecha, referencia, motivo). |
| Revertir / anular | `ReverseCollectionModal` | Motivo obligatorio y confirmación del importe; las operaciones 6.2, 6.3 y 6.5 piden además la evidencia (declaración y referencia). |
| Consulta de saldo e historial | Listado y filtros de crédito de proveedor (`SupplierCreditFilters`) | Por cliente; movimientos del anticipo. |
| Ubicación | Finance, junto a CxC (`D-03`, **APROBADO**) | Rutas concretas y entrada de navegación PROPUESTAS; sin módulo nuevo. |

Reglas de UI aplicables: formularios con `useForm` + `zodResolver` + `FormProvider` + `applyServerErrors`; mensajes por `message.*`; `ZhDecimalInput` y punto decimal; fechas por `dateFormatters`; sin estilos inline; permisos reflejados pero el servidor es la autoridad. Confirmación obligatoria antes de operaciones de dinero (modal).

---

## 10. Casos de prueba, concurrencia y errores

**Pruebas focalizadas (PROPUESTO; sin suite completa).** Unitarias de dominio + PostgreSQL real para locks e idempotencia, + HTTP para permisos y aislamiento.

| Área | Casos mínimos |
|---|---|
| Registro | feliz con banco / caja / sin destino; monto ≤ 0; cliente de otra empresa/tenant (404); destino inactivo/ajeno; replay misma clave → 201 original sin efectos duplicados; misma clave huella distinta → 409; sin `ClientRequestId` → `VALIDATION_ERROR`; cuenta/regla contable faltante → obligación `Failed`, anticipo vigente. |
| Saldo | consulta por cliente = Σ disponibles; nunca incluye otra empresa; cancelado y agotado no suman; el saldo es el mismo desde cualquier sucursal de la empresa. |
| Sucursal | `BranchId` de origen registrado e inmutable en anticipo y movimientos; aplicación de un anticipo de la sucursal A a una CxC de la sucursal B de la misma empresa → válida; el filtro por sucursal no oculta el anticipo; reporte filtrable por sucursal; aplicación a CxC de otra empresa → 404; tenant ajeno → 404. |
| Aplicación | total y parcial; múltiples CxC; cuota indicada y FIFO; monto > disponible; monto > saldo de CxC/cuota; CxC de otro cliente; CxC cancelada; CxC de otra empresa (404); línea inválida rechaza todo sin efecto; Σ aplicaciones vigentes nunca supera el original. |
| Concurrencia (PostgreSQL real) | dos aplicaciones distintas que agotan el mismo anticipo (una gana, la otra `422`); misma intención concurrente (un solo efecto); aplicación vs devolución simultáneas; aplicación vs reversa; dos aplicaciones a la misma CxC con anticipos distintos; sin deadlock con el orden de locks. |
| Reversa de aplicación | restaura CxC/cuota/saldo; doble reversa rechazada; CxC con operación posterior incompatible bloquea; período Closed/Locked bloquea. |
| Anulación administrativa (6.2) | solo íntegro, sin asiento publicado ni `CashMovement`, con declaración y evidencia; con asiento publicado → rechazada; con aplicación o devolución vigente → rechazada; sin evidencia → rechazada; no genera asiento ni movimiento de caja. |
| Reversa del ingreso (6.3) | sin evidencia → rechazada; dinero retenido sin devolución → rechazada; asiento espejo exacto; caja (`D-05`) con compensatorio y sin sobregiro; banco solo con referencia; una sola reversa; anticipo con aplicación o devolución vigente → rechazada; queda `Cancelled` sin borrar nada. |
| Devolución y su reversa (6.4/6.5) | monto ≤ disponible; banco y caja; efectivo sin turno abierto o sin sobregiro rechazado; doble devolución concurrente (una gana); reversa **con** evidencia restituye exactamente el monto; reversa **sin** evidencia o de devolución ejecutada → rechazada y saldo intacto; segunda reversa rechazada; reintegro posterior = nuevo anticipo, no restaura el original; restitución nunca supera `OriginalAmount`; permiso propio. |
| Contabilidad | un solo hecho por operación; ningún `CollectionApplied` en recibir/aplicar; no se registra como ingreso de venta; reproceso de `Failed` idempotente; sin doble asiento; anulación administrativa sin asiento; un asiento espejo nunca existe sin su evidencia operativa. |
| Seguridad | cada operación sin su permiso → 403; aislamiento tenant/empresa; auditoría con actor y fecha UTC; nada se borra. |
| UI | modal, confirmación, errores del servidor en campo, permisos, un solo envío con doble clic (`saving` no cuenta como garantía). |

**Catálogo de errores (PROPUESTO)** con el envelope `ApiResponse<T>` y `code` vigentes ([backend.md](../architecture/backend.md#envelope-de-respuesta-apiresponset--code-es-la-fuente-única-de-verdad), [errores](../architecture/error-handling.md)):

| Situación | HTTP | Nota |
|---|---|---|
| Validación de campos | 422 `VALIDATION_ERROR` | |
| Clave repetida con payload distinto | 409 `CONFLICT` | |
| Saldo/monto insuficiente, CxC cancelada, anticipo no íntegro, doble reversa, falta de evidencia, dinero no recuperado, devolución ya ejecutada | 422 `DOMAIN_RULE_VIOLATION` | Mensaje canónico, sin efectos. |
| Cliente, anticipo o CxC fuera del alcance | 404 | Fail-closed. |
| Sin permiso | 403 | |
| Período contable cerrado en reversa | Bloqueo con código de período existente | Accounting §7. |

---

## 11. Matriz de decisiones D-01 a D-12

La aprobación del 2026-11-24 (§0.1) cubre **decisiones funcionales concretas**; no aprueba automáticamente todas las decisiones de la matriz. Columna «Estado»: **APROBADA** (decisión funcional del propietario), **APROBADA (principio) / DETALLE PENDIENTE**, **MECANISMO PROPUESTO** (verificado contra el código en §13–§14, sin autorización para implementar) o **PENDIENTE** (autorización específica).

| ID | Decisión | Estado | Mecanismo propuesto / recomendación |
|---|---|---|---|
| **D-01** | Anticipos de Clientes como proceso, con **Finance** como módulo propietario. | **APROBADA** (criterio 1) | Justificación en §1. |
| **D-02** | Modelo: `CustomerAdvance` con un único libro de movimientos. | **APROBADA** (criterio 2) en lo funcional. **MECANISMO PROPUESTO**: agregado propio sin modificar `Payment`; alternativa descartada (extender `Payment`). | Compatibilidad con los cobros CLOSED verificada (§13.1, §14.3): `Payment` no se toca. La migración requiere autorización; los lectores de CxC están comprobados salvo las exportaciones del frontend. |
| **D-03** | Ubicación de la UI: Finance, junto a CxC. | **APROBADA** (criterio 6) | Rutas y componentes concretos: §9, propuestos. |
| **D-04** | Nombres y granularidad de permisos (§7), autorización para ampliar el catálogo del Kernel y qué rol ejecuta las operaciones que mueven dinero real. | **APROBADA** (funcional, 2026-11-24): permisos independientes para registrar, aplicar, revertir y devolver; devolución con permiso específico; aislamiento por tenant y empresa. **Cambios de Kernel: autorización técnica separada PENDIENTE.** | Mecanismo PROPUESTO: 4 permisos nuevos separados; `.refund` para toda operación con movimiento real. Kernel LOCKED: autorización técnica separada. No autoriza implementación. **Verificación técnica (2026-11-24): §13.2.** |
| **D-05** | Efectivo en caja: (A) solo dato, como cobros; (B) `CashMovement` interno en el turno, como reembolso de proveedor. | **APROBADA** (funcional, 2026-11-24): efectivo → movimiento en el turno de caja; transferencia → cuenta bancaria sin movimientos de caja; sucursal solo origen/reportes; empresa propietaria; sin contabilización duplicada. **Verificación técnica realizada** (§13.3, §14.4); **autorización de Caja PENDIENTE** (no autoriza cambios en Caja, Contabilidad ni componentes protegidos). | Mecanismo PROPUESTO: `CashMovement` interno en el turno para efectivo; banco sin movimiento, con evidencia. Verificado (§13.3, §14.4): compatible con `CashSession`/`CashMovement`; `OverdraftMessage` en salidas; titular del turno vigente; `CashReferenceType.CustomerAdvance` requiere autorización de Caja. |
| **D-06** | No modificar el flujo de emisión de facturas para aplicar anticipos. | **APROBADA** (criterio 7) | V1 solo aplica a CxC ya existentes. |
| **D-07** | Alcance de sucursal. | **APROBADA** (regla transversal de §0.1) | Mecanismo propuesto en §3 y §5, verificado (§13.4): sin bloqueo técnico. |
| **D-08** | Edición posterior del orden canónico de locks en backend.md (incluir `CustomerAdvance` antes de la CxC, §14.2). | **APROBADA como principio** (2026-11-24): orden único y consistente de bloqueos; sin doble aplicación, saldos negativos ni deadlocks. **Orden técnico definido como propuesta (§14.2); edición de `backend.md` PENDIENTE de autorización.** | Mecanismo recomendado (§14.1–14.2): `FOR UPDATE` de la fila, anticipo → CxC, idempotencia `(tenant, client_request_id, line_number)`. `backend.md` e infraestructura no se modifican aquí; su edición requiere autorización. |
| **D-09** | *(Retirada.)* Tratamiento tributario y fiscal. | **RETIRADA — fuera del alcance de Anticipos V1.** No es pendiente de implementación; la numeración se conserva solo por trazabilidad. | Anticipos V1 no desarrolla integración tributaria propia (SRI, comprobantes, facturación automática, IVA, retenciones). |
| **D-10** | Reglas contables: uso de `2.1.01.002`, reglas del §8, reversiones por el mecanismo oficial. | **APROBADA** (funcional, 2026-11-24): Recepción Debe Caja/Bancos, Haber Anticipos de clientes; Aplicación Debe Anticipos de clientes, Haber Cuentas por cobrar; Devolución Debe Anticipos de clientes, Haber Caja/Bancos; cuenta existente `2.1.01.002`. **Verificación técnica realizada** (§13.6, §14.3); **autorización de Contabilidad PENDIENTE** (no autoriza cambios en Contabilidad). | Mecanismo PROPUESTO: hechos `Finance/*` con el pipeline oficial, sin motor contable adicional ni asientos duplicados (§8). La «sin asiento» de 6.2 y los espejos de reversa están verificados (§13.6, §14.3); en empresas existentes las reglas se configuran con «Configurar». |
| **D-11** | Política de anulación, reversa y devolución (§6). | **APROBADA** (funcional, 2026-11-24; criterio 4): devolución solo hasta el saldo disponible y con permiso específico; efectivo en el turno de caja con fondos suficientes; transferencia a cuenta bancaria autorizada; motivo, usuario, fecha y referencia obligatorios; reversiones solo con evidencia de que el movimiento realmente se deshizo; historial inmutable y auditoría completa. Se conservan las distinciones 6.2 / 6.3 / 6.4. | Las condiciones detalladas de 6.2, 6.3 y 6.5 (evidencia exacta, quién declara «dinero no recibido», aprobación adicional) siguen **PROPUESTAS**; se fijan en el ADR. |
| **D-12** | Alcance de Anticipos de Clientes en ERP V1.1. | **APROBADA** (2026-11-24): forma parte del alcance de ERP V1.1. | La implementación requiere autorización expresa posterior; sin fechas de desarrollo aprobadas. |

Criterios aprobados que no son una fila de la matriz: **aplicación parcial o total a CxC existentes** (criterio 3, §5) y **saldo del cliente y la empresa** (criterio 5, §4).

**Siguiente paso, sin autorizarlo:** si el propietario lo instruye, un ADR con las decisiones tomadas y un plan por fases cortas (modelo/migración, registro, aplicación, reversa/devolución, UI), cada una con su revisión. La investigación técnica (§13) y su resolución (§14) ya se realizaron; antes de implementar solo restan las autorizaciones de §14.5 (migración de BD, Caja, Contabilidad, Kernel, `backend.md` y reglas de empresas existentes), y los riesgos no comprobados de §14.5 permanecen abiertos hasta su verificación. Al implementarse, nacería la SSOT del módulo y se sincronizarían [STATUS](../../STATUS.md), [FEATURES](../../FEATURES.md) y [receivables.md](../architecture/receivables.md) (donde hoy dice que las devoluciones de dinero están pendientes).

---

## 12. Relación con otros documentos (sin modificarlos)

| Documento | Relación |
|---|---|
| [ADR-035](ADR-035-supplier-payment-unapplied-advance.md) | Precedente de patrón (libro único, movimientos, reversa solo íntegra). No se reutiliza su tabla. |
| [SPA-V1.1-CONTRACT-SPEC-01 §20.4](SPA-V1.1-CONTRACT-SPEC-01.md), [REG §7](SPA-V1.1-APPROVED-DECISIONS-01.md#7-dir-ssot-01--anticipos-de-clientes-como-proceso-transversal) | `O-14` permanece abierta hasta decisión expresa; este documento no la cierra. SPA solo consumiría saldo, aplicación y devolución mediante el contrato de este proceso. |
| [receivables.md](../architecture/receivables.md), [accounting.md](../architecture/accounting.md), [backend.md](../architecture/backend.md) | SSOT vigentes citadas; se editarán solo si el diseño se aprueba y se implementa. |

---

## 13. Verificación técnica en modo lectura (2026-11-24)

Alcance: solo lectura de código, pruebas y SSOT vigentes. No se programó, no se tocó ningún componente CLOSED/FROZEN y **no se modifica ninguna decisión funcional aprobada**. «**Verificado**» = comprobado en código o SSOT citados. «**No verificado**» = no comprobado aquí. «**Requiere autorización**» = cambio sobre un componente protegido o decisión del propietario aún no otorgada. Las rutas de código se citan sin enlace (están fuera de `docs/`).

**Evidencia histórica.** Esta sección conserva la investigación tal como se hizo el 2026-11-24. Sus hallazgos y bloqueos fueron tratados en §14; donde §14 refina un mecanismo (idempotencia, lock del anticipo, forma de las reglas contables), **prevalece §14**. Los textos «No verificado» y «requiere verificación» de esta sección se leen en ese contexto.

### 13.1 D-02 — `CustomerAdvance` frente a los cobros CLOSED

**Evidencia (verificado).**
- `Payment` (`ERP.Domain/Modules/Finance/Entities/Payment.cs`): `Apply` exige ≥ 1 línea y `EnsureBalanced` (Σ líneas = monto); `Create` no recibe `BranchId`; publica `CollectionAppliedEvent`. Un cobro sin CxC es imposible por diseño (G-1 confirmada).
- El cobro (`RegisterCollectionCommandHandler`, `ERP.Application/Modules/Finance/UseCases/Payments/PaymentUseCases.cs`) crea el `Payment`, bloquea las CxC con `GetByIdsForUpdateAsync` y llama a `SalesReceivable.RegisterCollection`; no toca `CashSession`.
- `SalesReceivable.RegisterCollection(amount, updatedBy, installmentId?)` y `ReverseCollection(...)` son métodos públicos de dominio que no reciben ni referencian un `Payment`: validan estado no cancelado, monto ≤ `BalanceDue` y la cuota.
- Idempotencia reutilizable: `ClientRequestKey` y `BindClientRequest` (patrón de `Payment`).

**Mecanismos reutilizables.** `SalesReceivable.RegisterCollection/ReverseCollection`, `GetByIdsForUpdateAsync`, `ClientRequestKey`, `PaymentMethodDestinationPolicy`, validaciones de `CompanyBankAccount`/`CashRegister`.

**Brechas reales.**
- Hay que crear agregado, movimientos, transacción de devolución y eventos nuevos, y por tanto tablas y migración (cambio de esquema: **requiere autorización** en la fase de implementación).
- Una CxC cobrada con anticipo tendría `PaidAmount` incrementado **sin** `Payment`. **No verificado:** qué lectores (reportes, estados de cuenta, conciliaciones, `receivables.md`) derivan «cobros» de `Payment` y cuáles de `PaidAmount`; debe auditarse antes de implementar para que ambos coincidan.

**Conclusión.** Compatible: la alternativa de agregado propio **no modifica** `Payment`, `EnsureBalanced` ni `RegisterCollectionCommandHandler`, y no dispara `CollectionApplied`. Sin bloqueo técnico para D-02; quedan la migración (autorización) y la auditoría de lectores de `PaidAmount` (no verificada).

### 13.2 D-04 — Permisos existentes y límites de Kernel

**Evidencia (verificado).**
- `FinancePermissions` (`ERP.Domain/Kernel/Permissions/FinancePermissions.cs`): `finance.view`, `.create`, `.update`, `.delete`, `finance.collections.reverse`. `FinancePaymentsController` usa `perm:{FinancePermissions.Create}` y `perm:{FinancePermissions.ReverseCollection}`.
- [architecture.md](../architecture/architecture.md) («Platform Kernel»): un permiso nuevo es un `const string` en `Kernel/Permissions/<Modulo>Permissions.cs`; se usa siempre como `perm:{...}`; los tests `KernelRegistryTests` y `KernelControllerPolicyTests` exigen que toda policy exista en el registro.
- [security.md](../architecture/security.md) («Security & Access Contract V1 — LOCKED»): «Agregar nuevos permission keys al catálogo backend (`ERP.Domain/Kernel/Permissions/*.cs`)» figura como **Permitido** (no requiere revisión arquitectónica). Los permisos nuevos se conceden por perfil/rol; `InitialProfileBundles` otorga solo `finance.view` por defecto y `finance.create` se asigna aparte.
- Menú/rutas: ítems con `[NavItem]` en `Kernel/Modules/*Module.cs`; el menú es server-driven.

**Conclusión técnica.** Sin barrera técnica ni documental para agregar `finance.customer-advances.{create,apply,reverse,refund}` al catálogo existente; es el mecanismo previsto. La exigencia de **autorización técnica separada para tocar Kernel** es una condición del propietario (D-04, §0.1) y se mantiene: **requiere autorización**, tanto para los cuatro permisos como para cualquier `[NavItem]` o ruta nueva de menú. Los nombres siguen PROPUESTOS. No verificado: la ubicación exacta del ítem de menú (módulo Kernel de Finance).

### 13.3 D-05 — `CashSession`, `CashMovement` y reversas

**Evidencia (verificado).**
- Patrón análogo completo en `RegisterSupplierCreditRefundUseCases.cs` y `ReverseSupplierCreditRefundUseCases.cs`: transacción, lock del saldo, destino `FOR SHARE`, `GetOpenByCashRegisterForUpdateAsync` (lock exclusivo hasta el commit, recarga tras el lock), `CashSession.IsControlledBy(userId)` (solo quien opera la caja la usa), `RecordMovement(ManualIncome|ManualExpense, …, ReferenceType, ReferenceId = id de la transacción)` y `LinkCashMovement` a la transacción financiera. La reversa usa el movimiento contrario.
- `CashSessionAvailability.OverdraftMessage(session, amount)` implementa «sin sobregiro» (`CurrentBalance`); [backend.md](../architecture/backend.md#turnos-de-caja-uno-por-empresa-y-sin-sobregiro) lo aplica a salidas elegidas por el cajero y excluye los reembolsos del sistema.
- Los movimientos internos sin motivo los contabiliza su documento de origen; Posting nunca consume `CashMovement`/`CashSession` ([accounting.md §25](../architecture/accounting.md#25-caja-contable-accounting-12c)). Esto cumple la regla D-05.5 (sin contabilización duplicada).
- El cobro de CxC no crea `CashMovement`; el anticipo en efectivo es por tanto un flujo distinto, como aprobó D-05.
- Banco: sin `CashMovement`; el destino se valida con `CompanyBankAccount` activa de la empresa (mismo patrón).

**Brechas reales.**
1. `CashReferenceType` (`ERP.Domain/Modules/Caja/Enums/CashReferenceType.cs`) solo tiene `None, SalesInvoice, SalesReturn, SupplierPayment, SupplierCreditRefund, CompanyBankAccount`. El movimiento de un anticipo necesita su propia referencia (la regla 02A-CLOSE exige que un movimiento de reembolso nunca quede en `None`). Agregar el valor es un cambio en el módulo Caja: **requiere autorización**. No verificado: restricciones de BD adicionales sobre esa columna (la configuración solo declara valor por defecto).
2. Salidas de efectivo (devolución efectiva §6.4, reversa del ingreso §6.3): D-11 exige «fondos suficientes». `CashSessionAvailability` existe y es reutilizable, pero el reembolso de proveedor es una **entrada** y su ruta no la aplica; para estas salidas debe invocarse expresamente.
3. `IsControlledBy` implica que solo el usuario titular de la sesión puede registrar o devolver efectivo en esa caja. Es una regla vigente (02B), no un cambio; **confirmar operativamente** que encaja con el flujo de ventanilla.
4. Alcance: `GetOpenByCashRegisterForUpdateAsync` filtra por tenant + empresa (no por sucursal); la sesión pertenece a una sucursal (`CashSession.BranchId`). El reembolso de proveedor no exige coincidencia de sucursal; para anticipos no hay impedimento técnico, pero la regla aprobada (sucursal solo informativa) debe reflejarse en un test explícito.

**Conclusión.** El mecanismo aprobado es técnicamente viable reutilizando el patrón verificado. Bloqueos: nuevo valor de `CashReferenceType` (autorización de Caja) y definición de la validación de fondos en salidas. La aprobación D-05 no autoriza tocar Caja; no se tocó.

### 13.4 D-07 — Origen y alcance real de `BranchId`

**Evidencia (verificado).**
- `ICurrentBranch` (`ERP.Application/Modules/Common/ICurrentBranch.cs`): sucursal activa desde el header `X-Branch-Id`. `IBranchAccessGuard.RequireCurrentBranchAsync` valida que exista, esté activa, pertenezca a la empresa y que el usuario tenga `CompanyUserBranch` activa; `IBranchScopedRequest` + `BranchScopeBehavior` lo aplican por request (orden Tenant → Company → Branch).
- Filtro global EF para `ICompanyOperationalEntity` = **tenant + empresa** (`EnterpriseQueryFilterConfigurator`, `CompanyOperationalQueryExtensions`): no filtra por sucursal. `SalesReceivableRepository` y el cobro (`RegisterCollectionCommandHandler`) tampoco filtran por sucursal. Un `CustomerAdvance` con `ITenantScopedEntity`/`ICompanyOperationalEntity` **no queda oculto** a otra sucursal de la empresa.
- Precedentes de columna: `SalesReceivable.BranchId` es `Guid?`; `SupplierCredit.BranchId` es obligatorio e inmutable (heredado); `CashSession.BranchId` obligatorio; `Payment` y `CashMovement` no tienen `BranchId`.

**Conclusión.** Mecanismo de §3 y §5 verificado y sin bloqueo: `BranchId` se toma de la sucursal activa validada por el guard (nunca del cliente), se guarda inmutable en anticipo y movimientos, y no participa en ningún filtro de saldo ni de aplicación. Condición de diseño: el comando de **aplicación** no debe filtrar la CxC por sucursal. Efecto a documentar: registrar, aplicar o devolver exige que el usuario tenga una sucursal activa válida (`CompanyUserBranch`), aunque la sucursal no limite el saldo. No verificado: el comportamiento de los reportes por sucursal (no existen todavía).

### 13.5 D-08 — Orden canónico de bloqueos y concurrencia

**Evidencia (verificado).**
- [backend.md](../architecture/backend.md#saldos-compartidos-lock-pesimista-antes-de-decidir-zh-collections-receivable-concurrency-01): orden canónico 1 advisory de documento → 2 `SalesInvoice` → 3 `Payment` → 4 `SalesReceivable` (Id ascendente) → 5 secuencia/Kardex. Allí no figuran `CashSession` ni `SupplierCredit`; «el cobro no toma `CashSession`».
- `SupplierCredit`: lock **advisory** transaccional por `(tenant, id)` (`AcquireLockAsync`, «Lock B»), token `xmin`, `CHECK` sobre `available_amount`, e índice único `(tenant_id, client_request_id)` por movimiento; flujo de reembolso: Lock B → destino `FOR SHARE` → cuenta → `CashSession FOR UPDATE`; probado en `SupplierCreditRefundConcurrencyTests` (PostgreSQL real).
- CxC: `GetByIdsForUpdateAsync` (fila `FOR UPDATE`, Id ascendente). Caja: `GetOpenByCashRegisterForUpdateAsync`.

**Análisis del orden propuesto.** Aplicar o revertir una aplicación: anticipo → CxC (ascendente). Registrar, devolver o revertir ingreso o devolución: anticipo → destinos → `CashSession`. Ningún flujo toma a la vez CxC y `CashSession`, ni `SalesInvoice`/`Payment` con el anticipo; no se detecta ciclo.

**Hallazgos.**
1. **Idempotencia:** el patrón verificado impone unicidad `(tenant_id, client_request_id)` **por movimiento**. Una aplicación con N líneas (§5, un movimiento `Application` por línea) compartiría un único `ClientRequestId` y violaría ese índice. Debe elegirse otro ancla de idempotencia (por ejemplo, una entidad de aplicación por comando con sus líneas, o una clave que incluya la línea). Es un ajuste de diseño técnico, no funcional.
2. **Mecanismo de lock del anticipo:** existen dos patrones válidos (advisory como `SupplierCredit`; fila `FOR UPDATE` como CxC). Debe fijarse uno en el ADR.
3. El orden canónico vigente no incluye `SupplierCredit` ni `CashSession`; la posición «3b» y la de Caja deben quedar documentadas, lo que implica editar `backend.md`: **requiere autorización** (D-08 aprobada solo como principio).

**Conclusión.** Principio D-08 técnicamente viable y sin ciclo; el orden definitivo depende de resolver 1–3. Sin ellos no debe implementarse la aplicación multi-línea. **Resueltos técnicamente en §14.1 (idempotencia) y §14.2 (mecanismo de lock y orden completo); queda la autorización de `backend.md`.**

### 13.6 D-10 — Integración con Contabilidad

**Evidencia (verificado).**
- Cuenta `2.1.01.002 Anticipos de clientes` (Pasivo, naturaleza Acreedora, imputable) sembrada en `AccountingBootstrapStep`; es su única aparición en código de producción (G-3 confirmada).
- Pipeline: `PostingFactCatalog` define los hechos y los tipos de monto (`CollectionApplied` = `Finance`/`GrandTotal`); el bootstrap siembra las reglas (`CollectionApplied`: Debe `1.1.01.001`, Haber `1.1.03.001`); el traductor (`CollectionAppliedPostingTranslator`) registra la obligación con `AccountingPostingRecorder` y sustituye el Debe por la cuenta vigente de la caja/banco destino (`AccountingPostingDestination.FromCashOrBank`).
- Patrones equivalentes ya existentes: `SupplierCreditRefunded` (una línea fija + lado dinámico Caja/Banco), `SupplierCreditApplied` (dos líneas fijas), `SupplierPaymentConfirmed` (lado Haber dinámico).
- Reversas: `IAccountingReversalRecorder.RecordSourceAsync(tenant, company, módulo, hecho, fuenteId, motivo, fecha)` espeja el asiento histórico sin reglas vigentes (`CollectionReversedPostingTranslator`); [accounting.md §7 y §18](../architecture/accounting.md#7-reversos-fecha-efectiva-e-inmutabilidad): una obligación Pending/Failed sin asiento se cancela sin espejo; el reverso exige período Open en la fecha efectiva.

**Correspondencia con D-10 aprobada (propuesta técnica).**

| Hecho | Patrón existente | Reglas y dinámica |
|---|---|---|
| `Finance/CustomerAdvanceReceived` | `CollectionApplied` + `SupplierCreditRefunded` | Línea fija Haber `2.1.01.002`; Debe Caja/Banco dinámico por destino |
| `Finance/CustomerAdvanceApplied` | `SupplierCreditApplied` | Debe `2.1.01.002`, Haber la cuenta de CxC de `CollectionApplied` |
| `Finance/CustomerAdvanceRefunded` | `SupplierPaymentConfirmed` | Línea fija Debe `2.1.01.002`; Haber Caja/Banco dinámico |
| Reversas (6.1, 6.3, 6.5) | `CollectionReversedPostingTranslator` | Espejo exacto vía `IAccountingReversalRecorder` |
| Anulación administrativa (6.2) | §7 y §18 de Accounting | Cancela obligación Pending/Failed, sin asiento |

El modelo evita doble contabilización: los `CashMovement` internos no son fuente contable (13.3) y la aplicación no dispara `CollectionApplied`.

**Brechas reales.**
- Hay que agregar a Contabilidad: tres definiciones en `PostingFactCatalog`, sus reglas en el bootstrap, traductores y eventos de dominio del nuevo agregado, y las pruebas (por ejemplo `AccountingBootstrapStepTests`). **Requiere autorización**: D-10 está aprobada solo en lo funcional y no autoriza cambios en Contabilidad.
- Una aplicación con N líneas necesita una identidad contable por línea (`SourceDocumentId` distinto para poder espejar cada una); depende del ancla de 13.5-1.
- **No verificado:** cómo se propagan reglas nuevas a empresas ya existentes (el bootstrap siembra reglas por empresa) y la vigencia de la cuenta `1.1.03.001` por empresa.

**Conclusión.** La contabilización aprobada es expresable íntegramente con el mecanismo oficial, sin motor adicional ni asientos manuales. Bloqueo: autorización para extender Contabilidad y resolver la propagación a empresas existentes.

### 13.7 Resumen: verificado, bloqueos y autorizaciones

| Punto | Resultado técnico | Bloqueo real / requiere autorización |
|---|---|---|
| D-02 | **Compatible**; `Payment` y el cobro no se tocan | Migración de esquema (autorización en implementación); auditar lectores de `PaidAmount` (no verificado) |
| D-04 | **Viable**; agregar claves al catálogo es el mecanismo documentado | Autorización técnica separada de Kernel (condición del propietario); `[NavItem]` y rutas |
| D-05 | **Viable**; patrón verificado en reembolso de proveedor | Nuevo valor de `CashReferenceType` (Caja); validación de fondos en salidas; confirmar regla del titular de sesión |
| D-07 | **Verificado sin bloqueo**; sin filtro de sucursal en EF ni en CxC | Condición: el comando de aplicación no filtra por sucursal; requiere sucursal activa válida |
| D-08 | **Principio viable**; sin ciclo detectado | Idempotencia por línea (el índice único por movimiento no sirve); elegir mecanismo de lock; editar `backend.md` (autorización) |
| D-10 | **Expresable con el pipeline oficial** | Autorización para extender Contabilidad; propagación de reglas a empresas existentes (no verificado) |

Ninguna de estas verificaciones autoriza implementación ni modifica las decisiones aprobadas de §0.1.

Estado tras §14: idempotencia y lock del anticipo, lectores de CxC y Contabilidad, y Caja quedaron definidos o comprobados (§14.1–§14.4); permanecen las autorizaciones y los riesgos no comprobados de §14.5.

---

## 14. Resolución técnica de los hallazgos de §13 (2026-11-24)

Alcance: solo lectura del código existente. No se programó ni se modificó código, migraciones, API, frontend, Kernel, Caja, Contabilidad ni `backend.md`; **no se reabre ninguna decisión funcional aprobada**. Todo mecanismo de esta sección es una **propuesta técnica sustentada en patrones oficiales ya existentes**; ninguno está autorizado para implementarse (§14.5). Las rutas de código se citan sin enlace.

### 14.1 Idempotencia de una aplicación a varias CxC

**Problema (§13.5-1).** El patrón de `SupplierCreditMovement` tiene el índice único `uq_supplier_credit_movements_tenant_client_request_id` sobre `(tenant_id, client_request_id)`: un comando, un movimiento. Aplicar a N CxC en un comando produce N movimientos (uno por línea, §5) y la reversa de una aplicación completa produce otros N.

**Opciones evaluadas.**

| Opción | Patrón existente | Valoración |
|---|---|---|
| A. Cabecera de comando con líneas (`CustomerAdvanceApplication` + líneas), clave única en la cabecera | `Payment` + `PaymentApplicationLine`: índice único `(TenantId, ClientRequestId)` en el cobro, líneas con identidad propia | Válida, pero añade un agregado y tablas solo para repartir la clave |
| **B (recomendada).** Una sola tabla de movimientos con identidad compuesta `(tenant_id, client_request_id, line_number)` | Es el índice de `SupplierCreditMovement` ampliado con un número de línea | Cambio mínimo: sin entidades nuevas; cubre registro, aplicación, devolución y todas las reversas |

**Mecanismo recomendado (B).**
1. Una `ClientRequestId` por operación (comando), obligatoria, con huella `RequestPayloadHash` calculada con `CanonicalRequestFingerprint` (record versionado con cliente, anticipo, fecha efectiva y líneas `{ReceivableId, InstallmentId, Amount}`), igual que `CollectionIntentV1` en `PaymentUseCases.cs`.
2. Cada movimiento guarda `ClientRequestId`, `LineNumber` (smallint) y la huella. Índice único `(tenant_id, client_request_id, line_number)`. Operaciones de un solo efecto (registro, devolución, reversa de ingreso, reversa de devolución, anulación) usan `line_number = 0`; la aplicación y la reversa de aplicación usan `1..N` en el orden de las líneas.
3. Identidad individual de cada aplicación: el `Id` del movimiento `Application` de cada línea. Esa identidad es la `SourceEventId` del hecho contable de esa línea (patrón de `SupplierCreditApplied`, identidad = movimiento) y el objeto de la reversa: índice único filtrado por movimiento original reversado (`ReversalOfMovementId`, patrón `uq_supplier_credit_movements_reversal_of_movement`).
4. **Reintento:** con el anticipo bloqueado (§14.2), si existen movimientos con esa `ClientRequestId`, se compara la huella: igual → se devuelve el resultado original sin efectos; distinta → `409 CONFLICT`. Mismo flujo que `RegisterSupplierCreditRefundHandler` (busca el movimiento por `ClientRequestId`, compara huella) y `RegisterCollectionCommandHandler` (consulta previa, transacción, perdedor de carrera relee al ganador).
5. **Asientos sin duplicar:** `AccountingPostingRecorder.RecordAsync` toma el advisory de idempotencia por `(empresa, módulo, SourceEventId, FactType)` y, si ya existe la obligación, retorna sin crear otra. Con `SourceEventId` = id del movimiento, un reintento o un replay nunca crea una segunda obligación; una aplicación con N líneas crea N obligaciones distintas, cada una espejable por separado.
6. Última barrera ante una carrera que eluda el lock: la violación del índice único se traduce a conflicto sin efectos (`IDatabaseExceptionTranslator.TryGetUniqueViolation`, como en el reembolso de proveedor).

**Efecto en el diseño.** §5 (regla 4 y tabla de doble consumo) se ajusta a este mecanismo; no cambia ninguna regla funcional aprobada. Requiere migración (autorización, §14.5).

### 14.2 Bloqueos y concurrencia de `CustomerAdvance`

**Opciones.**

| Mecanismo | Evidencia | Valoración |
|---|---|---|
| Advisory lock transaccional por `(tenant, id)` | `SupplierCreditRepository.AcquireLockAsync` («Lock B»), hash estable de namespace + tenant + id | Funciona, pero no está acotado a la empresa: el 404 fail-closed depende de una lectura posterior. Los advisory del ERP se usan para idempotencia/documento (Accounting, jobs) |
| **`FOR UPDATE` de la fila (recomendado)** | [backend.md § Saldos compartidos](../architecture/backend.md#saldos-compartidos-lock-pesimista-antes-de-decidir-zh-collections-receivable-concurrency-01): patrón oficial para saldos compartidos; `SalesReceivableRepository.GetByIdsForUpdateAsync`, `CashSessionRepository.GetByIdForUpdateAsync` | Es «el patrón oficial, nunca un segundo mecanismo»: `SELECT 1 … WHERE tenant_id AND company_id AND id … FOR UPDATE` → lectura acotada → `ReloadAsync`; una fila fuera de la empresa no se bloquea (404) |

**Mecanismo único recomendado.** `ICustomerAdvanceRepository.GetByIdForUpdateAsync(tenantId, id)` con `FOR UPDATE` de la fila acotado a tenant + empresa, dentro de `IUnitOfWork.BeginTransactionAsync`, recarga tras el lock y decisión de dominio recién entonces. Además, el token de concurrencia `xmin` y el `CHECK (available_amount >= 0 AND available_amount <= original_amount)` como última barrera (ambos ya usados por `SupplierCredit`). No se usa advisory para el saldo.

**Orden completo (verificado contra el código existente).**

| Flujo | Orden de locks |
|---|---|
| Registrar anticipo | (fila nueva; idempotencia por índice único) → destino `FOR SHARE` (cuenta bancaria / caja) → cuenta `FOR SHARE` → `CashSession FOR UPDATE` (solo efectivo) → advisory de idempotencia del recorder contable al publicar |
| Aplicar / revertir aplicación | `CustomerAdvance FOR UPDATE` → `SalesReceivable FOR UPDATE` en Id ascendente → recorder contable |
| Devolver / revertir ingreso / revertir devolución | `CustomerAdvance FOR UPDATE` → destino `FOR SHARE` → cuenta `FOR SHARE` → `CashSession FOR UPDATE` (solo efectivo) → recorder contable |
| Anulación administrativa | `CustomerAdvance FOR UPDATE` → obligación contable |

Verificación de ciclos: (a) el orden «saldo → destino `FOR SHARE` → cuenta → `CashSession FOR UPDATE`» replica exactamente `RegisterSupplierCreditRefundHandler` (probado en `SupplierCreditRefundConcurrencyTests`); (b) «anticipo → CxC» sigue el orden canónico de `backend.md` (la CxC es la posición 4) y ningún flujo de Ventas toma `CustomerAdvance`; (c) ningún flujo del anticipo toma a la vez CxC y `CashSession` (el cobro tampoco toma `CashSession`), ni `SalesInvoice` ni `Payment`; (d) los flujos de Caja que toman `CashSession` (cierre, depósito, apertura, movimientos manuales) no toman `CustomerAdvance` ni CxC; (e) la anulación de factura exige `SalesReceivable.Cancel`, que rechaza una CxC con `PaidAmount > 0`, así que una CxC con anticipo aplicado no se cancela sin revertir antes, sin necesidad de tomar el anticipo. No se detecta ciclo posible.

**Qué no se hace aquí.** No se edita `backend.md`. Cuando se autorice, la edición debe incluir `CustomerAdvance` (antes de la posición 4) y una nota de que `CashSession` solo se toma después de los destinos y nunca junto con una CxC.

### 14.3 Lectores de CxC y Contabilidad

**Lectores que dependen de `SalesReceivable.PaidAmount` (verificado).** `SalesReceivableUseCases.cs` (DTO de CxC, cuotas pendientes `PaidAmount < Amount`, etiquetas «Pagada/Parcial/Vencida»), `SalesReceivableRepository` (filtro pendiente/pagada por `OriginalAmount − PaidAmount`), `DashboardKpiReader` (pendiente y vencido) y `SalesReceivable.Cancel`. Al aplicar el anticipo con `RegisterCollection` todos reflejan el saldo correcto sin cambios.

**Lectores que dependen de `Payment` (verificado).**
- La consulta de cobros por CxC (`PaymentRepository`) y la reversa de cobro (`PaymentUseCases.cs`): una aplicación de anticipo **no aparece** como cobro; se consulta por su propio historial (§7). La guarda de reversa compara Σ líneas con `PaidAmount` sin distinguir origen: es segura porque cada reversa se limita a su propio importe; exige una prueba mixta (cobro + anticipo sobre la misma CxC y cuota).
- Contabilidad: `JournalEntrySourceResolution` solo resuelve `Finance/CollectionApplied` hacia `Payment`; los hechos de saldo a favor de proveedor «quedan sin resolver deliberadamente» y los de anticipo seguirían el mismo criterio. `AccountingRelevantFactsReader` (completitud `ORIGINS_WITHOUT_ACCOUNTING` y vigencia) cuenta cobros y movimientos de `SupplierCredit`; los orígenes de anticipo **deben agregarse** o la completitud no detectaría anticipos sin asiento.

**Estados de cuenta y conciliaciones.** En el backend no existe un estado de cuenta de cliente ni una conciliación de CxC que lea `Payment` o `PaidAmount`; las coincidencias de «conciliación» son de comprobantes electrónicos, comunicaciones y apertura contable. Los reportes contables leen asientos, no `Payment`. Por tanto no hay lectores afectados más allá de los listados. No verificado: exportaciones o reportes del frontend (no se inspeccionó).

**Propagación de reglas a empresas existentes (verificado).** `AccountingBootstrapStep.SeedPostingRulesAsync` crea solo las reglas que faltan, y el paso corre al aprovisionar una empresa (`CompanyProvisioningService` → `ICompanyBootstrapService`); no hay corrida para empresas ya aprovisionadas, y [accounting.md §27](../architecture/accounting.md#27-limpieza-de-esquema-heredado-accounting-schema-cleanup) retiró los comandos de backfill de reglas. Para una empresa existente, un hecho sin regla deja la obligación `Failed` con `RULE_NOT_FOUND` (reprocesable, [accounting.md §17](../architecture/accounting.md#17-reproceso-manual-accounting-continuity-02)) y la pantalla de reglas contables lo muestra como «Sin regla» con la acción «Configurar» ([accounting.md §23](../architecture/accounting.md#23-reglas-contables-y-balance-general-accounting-11)). **Recomendación:** sin comando de backfill; las tres reglas se siembran para empresas nuevas y, en empresas existentes, se configuran desde esa pantalla antes de habilitar la operación (o con una autorización puntual del propietario).

**Resolución de cuentas por empresa (verificado).** Las líneas de regla guardan la cuenta de la propia empresa (validada: misma empresa, activa, imputable); el bootstrap resuelve por código `2.1.01.002 Anticipos de clientes` y `1.1.03.001 Cuentas por cobrar clientes` dentro de cada empresa y omite la regla si falta un código; Caja/Banco se resuelven en cada intento desde `CashRegister`/`CompanyBankAccount.AccountingAccountId` por `AccountingPostingDestination.FromCashOrBank(..., AccountNature, PostingAmountKind)`. No verificado: la existencia de esas cuentas en cada empresa real (datos).

**Forma recomendada de las reglas (la más simple).** `Finance/CustomerAdvanceReceived` y `Finance/CustomerAdvanceRefunded`: patrón de `CollectionApplied` (dos líneas fijas por defecto, con la línea de Caja/Banco sustituida por el destino elegido; la naturaleza del destino es un parámetro). `Finance/CustomerAdvanceApplied`: patrón de `SupplierCreditApplied` (dos líneas fijas). Reversas: espejo vía `IAccountingReversalRecorder`, sin reglas nuevas. Sin lados dinámicos nuevos y sin motor adicional.

### 14.4 Caja

**Integración con `CashSession` y `CashMovement` (verificado).** El anticipo en efectivo sigue el patrón de `RegisterSupplierCreditRefundHandler`/`ReverseSupplierCreditRefundHandler`: transacción, destino `FOR SHARE`, `GetOpenByCashRegisterForUpdateAsync`, `CashSession.IsControlledBy`, `RecordMovement(ManualIncome|ManualExpense, …, ReferenceType, ReferenceId = id de la transacción/movimiento)` y vínculo del movimiento. El movimiento interno sin motivo no genera hecho propio de Caja (lo contabiliza el documento de origen), de modo que no hay doble contabilización.

| Operación en efectivo | Movimiento de caja | Validación de fondos |
|---|---|---|
| Registrar anticipo | `ManualIncome` | No aplica (entrada) |
| Devolución efectiva | `ManualExpense` | **Sí**: `CashSessionAvailability.OverdraftMessage` |
| Reversa del ingreso | `ManualExpense` compensatorio | **Sí** |
| Reversa de devolución no ejecutada | `ManualIncome` compensatorio | No aplica (entrada) |

**Fondos suficientes.** `CashSessionAvailability.OverdraftMessage(session, amount)` ya la usan `RecordCashBankDepositUseCases` y `RecordCashMovementUseCases` con la sesión bloqueada y sin override. Las salidas del anticipo la invocan igual, después del `FOR UPDATE` de la sesión. Es la regla de D-11 («fondos suficientes») sin crear otra.

**Titular del turno.** `CashSession.IsControlledBy(userId)` y `CashSessionOwnership.RejectionMessage` se aplican en reembolso de proveedor, cierre, depósito y solicitudes de fondeo. Se aplica igual al anticipo en efectivo: solo el titular de la sesión de esa caja registra o devuelve efectivo. Es una regla vigente, no un cambio; consecuencia operativa a confirmar (§14.5).

**Referencia oficial.** `CashReferenceType` termina en `CompanyBankAccount = 5`; se necesita un valor propio (`CustomerAdvance = 6`, al final, sin renumerar) para que el movimiento nunca quede en `None` (regla 02A-CLOSE). La columna `reference_type` es un entero con valor por defecto y sin `CHECK` (configuración EF y migración base), así que el cambio es solo del enum, sin cambio de esquema. Observaciones: el frontend de Caja trata `referenceType` como texto sin mapeo, y la API de movimiento manual acepta cualquier nombre del enum (`Enum.TryParse`), como ya ocurre con `SupplierCreditRefund`.

**Sucursales de la misma empresa.** `GetOpenByCashRegisterForUpdateAsync` filtra por tenant + empresa (no por sucursal) y el reembolso de proveedor no exige coincidencia de sucursal; el movimiento manual de Caja sí exige `session.BranchId == sucursal activa` (`RecordCashMovementUseCases`), pero es un flujo del cajero, no del sistema. El anticipo sigue el patrón del sistema: la sesión es la de la caja elegida (puede ser de otra sucursal de la misma empresa) y la sucursal del anticipo es la activa del actor; ambas se guardan como dato informativo y ninguna limita el saldo (D-05, D-07). Requiere prueba explícita; sin bloqueo técnico.

### 14.5 Resultado: definido, riesgos que permanecen y autorizaciones

**Técnicamente definido (propuesta lista para el ADR).**

| Tema | Definición |
|---|---|
| Idempotencia | Una `ClientRequestId` por operación; movimientos con índice único `(tenant, client_request_id, line_number)`; identidad de cada aplicación = id del movimiento = `SourceEventId` contable; replay con huella canónica |
| Bloqueos | `FOR UPDATE` de la fila de `CustomerAdvance` (patrón oficial), `xmin` y `CHECK` como barrera; orden anticipo → CxC (asc.) y anticipo → destino → cuenta → `CashSession`; sin ciclos |
| Lectores | Reportes de CxC leen `PaidAmount` y no cambian; la lista de cobros no incluye anticipos (historial propio); sin estados de cuenta ni conciliaciones afectados |
| Contabilidad | Tres hechos con patrones existentes; reversas por espejo; reglas para empresas nuevas por bootstrap y para existentes por «Configurar»; sin backfill |
| Caja | Patrón del reembolso de proveedor; `OverdraftMessage` en salidas; titular de turno vigente; `CashReferenceType.CustomerAdvance` |

**Riesgos y puntos no verificados que permanecen abiertos (no se declaran resueltos).**
1. Titular del turno: confirmar con operación que el flujo de ventanilla lo admite.
2. Cuentas `2.1.01.002` y `1.1.03.001` en cada empresa real y reglas en empresas ya aprovisionadas: datos no inspeccionados.
3. Exportaciones y reportes del frontend que consuman `PaidAmount` o cobros: no inspeccionados.
4. La guarda de reversa de cobro no distingue el origen del cobrado (segura por importe, requiere prueba mixta).
5. Los orígenes de anticipo deben incorporarse a la completitud contable; de lo contrario no se detectarían anticipos sin asiento.

**Autorizaciones necesarias al implementar (ninguna otorgada).**
1. **Migración** de esquema: tablas de anticipo, movimientos, transacción de devolución y auditoría (propietario).
2. **Caja:** nuevo valor `CashReferenceType.CustomerAdvance`.
3. **Contabilidad:** tres definiciones en `PostingFactCatalog`, tres reglas del bootstrap, traductores y eventos, ampliación de `AccountingRelevantFactsReader` y pruebas del bootstrap.
4. **Kernel:** cuatro permisos `finance.customer-advances.*` y la ruta/`[NavItem]` de la pantalla (autorización técnica separada, D-04).
5. **`backend.md`:** incluir `CustomerAdvance` en el orden canónico de locks (D-08).
6. **Empresas existentes:** configurar las reglas desde la pantalla de reglas o autorización puntual del propietario.

Estas definiciones no autorizan implementación ni modifican las decisiones de §0.1.

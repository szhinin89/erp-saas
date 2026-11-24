# Anticipos de Clientes V1 — diseño funcional y técnico mínimo (CUSTOMER-ADVANCES-V1-DESIGN-01)

**DISEÑO FUNCIONAL APROBADO / ALCANCE V1.1 APROBADO / IMPLEMENTACIÓN NO AUTORIZADA.** Solo quedan pendientes las verificaciones técnicas necesarias para implementar lo aprobado (§0.1).

Solo documentación. El diseño técnico de este documento es **PROPUESTA** salvo donde se marca **APROBADO**. Ninguna fase, migración, endpoint, pantalla ni permiso está autorizado.

Fecha: 2026-11-24 (revisión 3: aprobación funcional del propietario registrada, §0.1). Responde al vacío registrado como `O-14` / `C-08` en SPA ([contrato §20.4](SPA-V1.1-CONTRACT-SPEC-01.md#204-anticipos-de-clientes-proceso-independiente-y-transversal-dir-ssot-01), [REG §7](SPA-V1.1-APPROVED-DECISIONS-01.md#7-dir-ssot-01--anticipos-de-clientes-como-proceso-transversal)) con el alcance aprobado en §0.1. Este documento no modifica SPA ni actualiza `O-14` allí, y no es una SSOT: una SSOT de módulo nace cuando existen código y pruebas ([enforcement](../architecture/enforcement.md#ssot-por-módulo)). Un ADR, un plan por fases y la SSOT requerirán instrucción posterior del propietario.

Estado de entrega: [STATUS](../../STATUS.md) · Cola: [WORK_QUEUE](../agent/WORK_QUEUE.md) · Índice: [decisiones](README.md).

## 0. Cómo leer este documento

| Etiqueta | Significado |
|---|---|
| **EXISTENTE** | Verificado en código, pruebas o SSOT vigentes del repositorio. |
| **APROBADO** | Decisión funcional expresa del propietario (2026-11-24, §0.1), solo dentro de su alcance literal. |
| **PROPUESTO** | Diseño o mecanismo técnico recomendado aquí; no existe y no está aprobado; sujeto a verificación. |
| **PENDIENTE** | Validación o autorización específica aún no otorgada (§11, `D-xx`). Sin ella no se puede implementar. |

Fuentes revisadas: [ADR-035](ADR-035-supplier-payment-unapplied-advance.md) (solo precedente), [receivables.md](../architecture/receivables.md), [accounting.md](../architecture/accounting.md), [backend.md](../architecture/backend.md) (idempotencia, locks, orden canónico), `DIR-SSOT-01`, `O-14`, y el código de `Payment`, `SalesReceivable`, `SupplierCredit`, `SupplierCreditRefundTransaction`, controladores `FinancePayments`/`SupplierCredit` y bootstrap contable.

---

## 0.1 Aprobación del propietario y estado documental (2026-11-24)

**Estado: DISEÑO FUNCIONAL APROBADO / ALCANCE V1.1 APROBADO / IMPLEMENTACIÓN NO AUTORIZADA.** Pendientes: solo las verificaciones técnicas necesarias para implementar lo aprobado.

El propietario aprobó expresamente el **2026-11-24** los siguientes siete criterios funcionales. Quedan registrados aquí con ese alcance literal; los mecanismos técnicos que los desarrollan en este documento siguen siendo **PROPUESTOS** y sujetos a verificación.

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

Esto resuelve funcionalmente D-07. El mecanismo (de dónde sale el `BranchId` y cómo se evita que el filtro operativo por sucursal oculte el anticipo a otra sucursal) es **PROPUESTO** en §3 y §5 y requiere verificación.

**D-05 — Caja/Bancos: APROBADA (funcional, 2026-11-24).**

1. **Efectivo:** el ingreso o devolución de un anticipo genera el movimiento correspondiente en el turno de caja.
2. **Transferencia bancaria:** el ingreso o devolución se registra contra la cuenta bancaria correspondiente, sin crear movimientos de caja.
3. **Sucursal:** identifica el origen de la operación para trazabilidad y reportes; no limita el saldo del anticipo entre sucursales de una misma empresa.
4. **Empresa:** mantiene la propiedad del anticipo y de sus efectos financieros.
5. **Contabilidad:** una operación financiera no puede producir contabilización duplicada.

Verificaciones **pendientes**: compatibilidad con `CashSession` y `CashMovement` existentes; reglas del turno abierto, disponibilidad de efectivo y reversas; mecanismo técnico de integración y orden de bloqueos; verificación técnica contable (`D-10`). La aprobación funcional **no autoriza cambios en Caja, Contabilidad ni otros componentes protegidos**.

**D-04 — Seguridad: APROBADA funcionalmente (2026-11-24).**

- Permisos independientes para registrar, aplicar, revertir y devolver anticipos.
- La devolución requiere permiso específico.
- Se mantiene el aislamiento por tenant y empresa.
- Los cambios de Kernel requieren **autorización técnica separada**; esta aprobación no la otorga ni autoriza implementación. Nombres y catálogo concretos (§7) siguen PROPUESTOS.

**D-08 — Concurrencia: APROBADA como principio (2026-11-24).**

- Orden único y consistente de bloqueos para anticipos y CxC.
- Evitar doble aplicación, saldos negativos y deadlocks.
- El orden técnico definitivo (incluida la posición «3b» de §5) se verificará antes de implementar. No se modifica `backend.md` ni infraestructura.

**D-11 — Devoluciones: APROBADA funcionalmente (2026-11-24).**

- Devolver únicamente hasta el saldo disponible, con permiso específico.
- Efectivo: turno de caja correspondiente y fondos suficientes. Transferencia: cuenta bancaria autorizada.
- Motivo, usuario, fecha y referencia obligatorios.
- Reversiones únicamente con evidencia de que el movimiento financiero realmente se deshizo.
- Historial inmutable y auditoría completa.
- Se conservan las distinciones entre anulación administrativa (§6.2), reversa del ingreso (§6.3) y devolución efectiva (§6.4). Las condiciones detalladas de §6 siguen PROPUESTAS y sujetas a verificación.

**D-09 — RETIRADA (2026-11-24).** No forma parte del desarrollo de Anticipos V1; no genera tareas ni dependencias de integración con SRI. La numeración se conserva solo por trazabilidad.

**D-10 — Contabilidad: APROBADA funcionalmente (2026-11-24).**

- Recepción: Debe Caja/Bancos; Haber Anticipos de clientes.
- Aplicación: Debe Anticipos de clientes; Haber Cuentas por cobrar.
- Devolución: Debe Anticipos de clientes; Haber Caja/Bancos.
- Se utiliza la cuenta existente `2.1.01.002 Anticipos de clientes`.
- Las reversiones usan los mecanismos contables oficiales (§8).
- Sigue **pendiente la verificación técnica contable** (reglas, traductores, cuentas efectivas y período); esta aprobación no autoriza cambios en Contabilidad.

**D-12 — Alcance: APROBADA (2026-11-24).** Anticipos de Clientes forma parte del alcance de ERP V1.1. La implementación requerirá **autorización expresa posterior**; no se establecen fechas de desarrollo.

**Pendientes reales** (solo verificaciones técnicas para implementar lo aprobado, §11): `D-02` compatibilidad del agregado con los cobros CLOSED; `D-04` autorización técnica separada de Kernel; `D-05` compatibilidad con `CashSession`/`CashMovement`, turno abierto, efectivo y reversas; `D-07` mecanismo del `BranchId`; `D-08` orden definitivo de bloqueos; `D-10` verificación técnica contable. D-05 y D-10 están aprobadas solo en lo funcional y **no autorizan cambios en Caja, Contabilidad ni otros componentes protegidos**. **Ninguna fase, migración, endpoint, pantalla ni permiso está autorizado.** Un ADR, un plan por fases y la SSOT del módulo requerirán instrucción posterior del propietario. Este registro no modifica SPA: `O-14` no se actualiza aquí.

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
| G-6 | `Payment` (dirección Collection) no tiene `BranchId`; `SupplierCredit` sí lo hereda del pago. La regla funcional de sucursal ya está aprobada (§0.1, `D-07`); falta verificar el mecanismo: el filtro operativo por sucursal de entidades y de `CashSession`/`CashMovement` no debe impedir usar el anticipo entre sucursales de la misma empresa. | `Payment.cs`, `SupplierCredit.cs`, `ICurrentBranch`, `CashMovement.cs` |
| G-7 | El orden canónico de locks de [backend.md](../architecture/backend.md) no incluye el anticipo. | backend.md |

---

## 3. Registro y confirmación de anticipos sin factura

**Modelo (funcional **APROBADO**: `CustomerAdvance` con un único libro de movimientos, `D-02`; campos y mecanismos PROPUESTOS).** Agregado nuevo `CustomerAdvance` en Finance (`TenantId`, `CompanyId`, `BranchId` de origen, `CustomerId` = `BusinessPartner` con rol Customer, `CurrencyCode`, `OriginalAmount`, `AvailableAmount`, fecha de negocio `DateOnly`, destino banco XOR caja, `PaymentMethodId`, referencia externa, motivo/nota, estado) con una colección append-only `CustomerAdvanceMovement`, que conserva también el `BranchId` de cada operación (origen y reportes). **Un solo libro de saldo**, igual que ADR-035 descartó un segundo libro: aquí no existe ninguno de cliente, así que se crea uno y solo uno.

Alternativa descartada: extender `Payment` para permitir remanente sin CxC (como ADR-035 hizo con `SupplierPayment`). Cambiaría el invariante `EnsureBalanced` de un agregado CLOSED usado por todos los cobros, con riesgo de romper cobros existentes. Se recomienda no tocarlo; la verificación técnica de esta recomendación queda pendiente (`D-02`).

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
4. Se registra **un movimiento `Application` por línea** (append-only), con `ReceivableId` e `InstallmentId`; se reduce `AvailableAmount` por la misma cantidad.
5. **No se crea un `Payment`** ni se dispara `CollectionApplied` (eso debitaría caja de nuevo). La aplicación es un hecho propio (§8).

**Prevención de doble consumo** (tres capas, patrón oficial, sin mecanismo nuevo):

| Capa | Mecanismo |
|---|---|
| Misma intención repetida (doble clic, reintento, timeout) | `ClientRequestId` + huella + índice único; replay devuelve el resultado original sin volver a aplicar. |
| Intenciones distintas que compiten por el mismo anticipo o CxC | Transacción explícita; `FOR UPDATE` del anticipo y luego de las CxC en orden ascendente de Id; recargar bajo lock y **recién entonces** decidir. El perdedor reevalúa y recibe `422`. |
| Última barrera | `CHECK (available_amount >= 0)`, y unicidad de `(tenant_id, client_request_id)` por tabla de movimiento/aplicación. |

**Orden de locks (principio **APROBADO**, `D-08`: orden único y consistente, sin doble aplicación, saldos negativos ni deadlocks; el orden definitivo se verifica antes de implementar. Mecanismo PROPUESTO):** el anticipo se toma después del advisory de documento y **antes** de las `SalesReceivable` (posición nueva «3b» en el orden canónico). Ningún flujo toma `SalesInvoice` ni `Payment` junto con el anticipo (mismo criterio que el cobro). Requiere una edición posterior de [backend.md](../architecture/backend.md) que **este documento no hace**.

---

## 6. Anulación, reversas y devoluciones

Los anticipos son contabilidad y dinero real: **nada se borra ni se edita**; todo es un movimiento compensatorio inmutable ([Accounting §7](../architecture/accounting.md#7-reversos-fecha-efectiva-e-inmutabilidad)).

**Aprobado (criterio 4 y `D-11`, 2026-11-24):** anulaciones, reversiones y devoluciones requieren controles, autorización y evidencia. Devolución solo hasta el saldo disponible, con permiso específico; efectivo en el turno de caja correspondiente con fondos suficientes; transferencia contra cuenta bancaria autorizada; motivo, usuario, fecha y referencia obligatorios; reversiones solo con evidencia de que el movimiento financiero realmente se deshizo; historial inmutable y auditoría completa. Las condiciones concretas de cada operación, abajo, siguen **PROPUESTAS** y sujetas a verificación.

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

Efecto: movimiento `IncomeReversed`, saldo 0, estado `Cancelled`; asiento espejo exacto del publicado (período Open de la fecha efectiva, [Accounting §7](../architecture/accounting.md#7-reversos-fecha-efectiva-e-inmutabilidad)). Con destino caja (`D-05` aprobada), movimiento compensatorio en el turno abierto, sujeto a «sin sobregiro» (mecanismo PROPUESTO, verificación pendiente). Con destino banco no hay movimiento operativo; la evidencia es la referencia bancaria.

### 6.4 Devolución efectiva de dinero (PROPUESTO, `D-11`)

Comando propio con permiso específico; monto ≤ `AvailableAmount`; destino banco/caja activos de la empresa; fecha efectiva, referencia de ejecución (transferencia, cheque o comprobante interno) y motivo obligatorios. Movimiento `Refund` más transacción financiera append-only (destino usado, método, referencia, fecha) bajo lock del anticipo. Con caja (`D-05` aprobada), movimiento de egreso en el turno, con turno abierto y efectivo suficiente (mecanismo PROPUESTO, verificación pendiente). Cierra G-4 solo para anticipos; la devolución de **cobros ordinarios** sigue fuera ([receivables.md § Límites vigentes](../architecture/receivables.md#límites-vigentes)). Lo ya aplicado a una CxC no se devuelve en dinero: primero se revierte la aplicación (§6.1).

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

- **EXISTENTE:** la cuenta `2.1.01.002` y el pipeline. **PROPUESTO:** las reglas habilitadoras (`PostingFactCatalog` + bootstrap + traductores). Ningún asiento se escribe a mano ni por atajo; las cuentas se resuelven en cada intento, sin fallback; una cuenta/regla faltante deja la obligación `Failed` y reprocesable ([Accounting §6, §17](../architecture/accounting.md)).
- **Sin doble contabilización:** (a) recibir el anticipo no genera `CollectionApplied` ni un asiento de venta, porque no es un `Payment` de CxC; (b) aplicarlo a CxC genera solo `CustomerAdvanceApplied`: la CxC baja sin dinero nuevo y sin segundo Debe a Caja; (c) los movimientos operativos de caja no son fuente contable: «Posting nunca consume `CashMovement`/`CashSession`» ([Accounting §25](../architecture/accounting.md#25-caja-contable-accounting-12c)); (d) este diseño no registra el anticipo como ingreso de venta sino como obligación con el cliente; su tratamiento contable es el aprobado en `D-10` (funcional), con verificación técnica pendiente.
- **Caja (`D-05`, APROBADA funcional 2026-11-24):** con efectivo, el ingreso o devolución genera el movimiento correspondiente en el turno de caja; con transferencia bancaria se registra contra la cuenta bancaria, sin movimientos de caja. **Mecanismo PROPUESTO** (verificación pendiente): `CashMovement` `ManualIncome`/`ManualExpense` internos en el turno abierto, como el reembolso de crédito de proveedor, contabilizados por el documento de origen y no como fuente contable propia; se rechaza sin turno abierto o sin efectivo suficiente ([sin sobregiro](../architecture/backend.md#turnos-de-caja-uno-por-empresa-y-sin-sobregiro)). Banco: sin `CashMovement`; la evidencia es la referencia bancaria. Una operación financiera no puede producir contabilización duplicada. La sucursal identifica el origen y no limita el saldo entre sucursales de la empresa. Pendiente: compatibilidad con `CashSession`/`CashMovement`, reglas de turno, efectivo y reversas, orden de bloqueos y verificación técnica contable (`D-10`). No autoriza cambios en Caja, Contabilidad ni componentes protegidos.
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

La aprobación del 2026-11-24 (§0.1) cubre **decisiones funcionales concretas**; no aprueba automáticamente todas las decisiones de la matriz. Columna «Estado»: **APROBADA** (decisión funcional del propietario), **APROBADA (principio) / DETALLE PENDIENTE**, **MECANISMO PROPUESTO** (sujeto a verificación técnica) o **PENDIENTE** (validación o autorización específica).

| ID | Decisión | Estado | Mecanismo propuesto / recomendación |
|---|---|---|---|
| **D-01** | Anticipos de Clientes como proceso, con **Finance** como módulo propietario. | **APROBADA** (criterio 1) | Justificación en §1. |
| **D-02** | Modelo: `CustomerAdvance` con un único libro de movimientos. | **APROBADA** (criterio 2) en lo funcional. **MECANISMO PROPUESTO**: agregado propio sin modificar `Payment`; alternativa descartada (extender `Payment`). | La implementación exacta del agregado y su compatibilidad con los cobros CLOSED requieren verificación técnica. |
| **D-03** | Ubicación de la UI: Finance, junto a CxC. | **APROBADA** (criterio 6) | Rutas y componentes concretos: §9, propuestos. |
| **D-04** | Nombres y granularidad de permisos (§7), autorización para ampliar el catálogo del Kernel y qué rol ejecuta las operaciones que mueven dinero real. | **APROBADA** (funcional, 2026-11-24): permisos independientes para registrar, aplicar, revertir y devolver; devolución con permiso específico; aislamiento por tenant y empresa. **Cambios de Kernel: autorización técnica separada PENDIENTE.** | Mecanismo PROPUESTO: 4 permisos nuevos separados; `.refund` para toda operación con movimiento real. Kernel LOCKED: autorización técnica separada. No autoriza implementación. |
| **D-05** | Efectivo en caja: (A) solo dato, como cobros; (B) `CashMovement` interno en el turno, como reembolso de proveedor. | **APROBADA** (funcional, 2026-11-24): efectivo → movimiento en el turno de caja; transferencia → cuenta bancaria sin movimientos de caja; sucursal solo origen/reportes; empresa propietaria; sin contabilización duplicada. **Verificaciones PENDIENTES** (no autoriza cambios en Caja, Contabilidad ni componentes protegidos). | Mecanismo PROPUESTO: `CashMovement` interno en el turno para efectivo; banco sin movimiento, con evidencia. Verificar compatibilidad con `CashSession`/`CashMovement`, turno abierto, efectivo disponible, reversas, orden de bloqueos y alcance por sucursal. |
| **D-06** | No modificar el flujo de emisión de facturas para aplicar anticipos. | **APROBADA** (criterio 7) | V1 solo aplica a CxC ya existentes. |
| **D-07** | Alcance de sucursal. | **APROBADA** (regla transversal de §0.1) | Mecanismo propuesto en §3 y §5; sujeto a verificación. |
| **D-08** | Edición posterior del orden canónico de locks en backend.md (posición «3b»). | **APROBADA como principio** (2026-11-24): orden único y consistente de bloqueos; sin doble aplicación, saldos negativos ni deadlocks. **Orden técnico definitivo PENDIENTE de verificación.** | Mecanismo PROPUESTO (posición «3b»). `backend.md` e infraestructura no se modifican aquí; su edición requiere autorización en la fase de implementación. |
| **D-09** | *(Retirada.)* Tratamiento tributario y fiscal. | **RETIRADA — fuera del alcance de Anticipos V1.** No es pendiente de implementación; la numeración se conserva solo por trazabilidad. | Anticipos V1 no desarrolla integración tributaria propia (SRI, comprobantes, facturación automática, IVA, retenciones). |
| **D-10** | Reglas contables: uso de `2.1.01.002`, reglas del §8, reversiones por el mecanismo oficial. | **APROBADA** (funcional, 2026-11-24): Recepción Debe Caja/Bancos, Haber Anticipos de clientes; Aplicación Debe Anticipos de clientes, Haber Cuentas por cobrar; Devolución Debe Anticipos de clientes, Haber Caja/Bancos; cuenta existente `2.1.01.002`. **Verificación técnica contable PENDIENTE** (no autoriza cambios en Contabilidad). | Mecanismo PROPUESTO: hechos `Finance/*` con el pipeline oficial, sin motor contable adicional ni asientos duplicados (§8). La «sin asiento» de 6.2 y los espejos de reversa se verifican técnicamente. |
| **D-11** | Política de anulación, reversa y devolución (§6). | **APROBADA** (funcional, 2026-11-24; criterio 4): devolución solo hasta el saldo disponible y con permiso específico; efectivo en el turno de caja con fondos suficientes; transferencia a cuenta bancaria autorizada; motivo, usuario, fecha y referencia obligatorios; reversiones solo con evidencia de que el movimiento realmente se deshizo; historial inmutable y auditoría completa. Se conservan las distinciones 6.2 / 6.3 / 6.4. | Las condiciones detalladas de 6.2, 6.3 y 6.5 (evidencia exacta, quién declara «dinero no recibido», aprobación adicional) siguen **PROPUESTAS** y sujetas a verificación técnica. |
| **D-12** | Alcance de Anticipos de Clientes en ERP V1.1. | **APROBADA** (2026-11-24): forma parte del alcance de ERP V1.1. | La implementación requiere autorización expresa posterior; sin fechas de desarrollo aprobadas. |

Criterios aprobados que no son una fila de la matriz: **aplicación parcial o total a CxC existentes** (criterio 3, §5) y **saldo del cliente y la empresa** (criterio 5, §4).

**Siguiente paso, sin autorizarlo:** si el propietario lo instruye, un ADR con las decisiones tomadas y un plan por fases cortas (modelo/migración, registro, aplicación, reversa/devolución, UI), cada una con su revisión. Antes de implementar deben cerrarse las verificaciones técnicas pendientes: compatibilidad del agregado con los cobros CLOSED (`D-02`), autorización técnica separada de Kernel (`D-04`), compatibilidad con `CashSession`/`CashMovement` (`D-05`), mecanismo del `BranchId` (`D-07`), orden definitivo de bloqueos (`D-08`) y verificación técnica contable (`D-10`). Al implementarse, nacería la SSOT del módulo y se sincronizarían [STATUS](../../STATUS.md), [FEATURES](../../FEATURES.md) y [receivables.md](../architecture/receivables.md) (donde hoy dice que las devoluciones de dinero están pendientes).

---

## 12. Relación con otros documentos (sin modificarlos)

| Documento | Relación |
|---|---|
| [ADR-035](ADR-035-supplier-payment-unapplied-advance.md) | Precedente de patrón (libro único, movimientos, reversa solo íntegra). No se reutiliza su tabla. |
| [SPA-V1.1-CONTRACT-SPEC-01 §20.4](SPA-V1.1-CONTRACT-SPEC-01.md), [REG §7](SPA-V1.1-APPROVED-DECISIONS-01.md#7-dir-ssot-01--anticipos-de-clientes-como-proceso-transversal) | `O-14` permanece abierta hasta decisión expresa; este documento no la cierra. SPA solo consumiría saldo, aplicación y devolución mediante el contrato de este proceso. |
| [receivables.md](../architecture/receivables.md), [accounting.md](../architecture/accounting.md), [backend.md](../architecture/backend.md) | SSOT vigentes citadas; se editarán solo si el diseño se aprueba y se implementa. |

# Gali Spa V1.1 — Especificación técnica de componentes operativos (borrador)

> **REFERENCIA HISTÓRICA NO NORMATIVA — actualizada el 2026-11-18.** SPA-DEC-115/116 se recuperaron aprobadas por evidencia aportada por el propietario: [registro §9/10](GALI-SPA-V1.1-APPROVED-DECISIONS-01.md). El [contrato central](GALI-SPA-V1.1-CONTRACT-SPEC-01.md) contiene el alcance funcional aprobado y O-23 cerrada en inclusión general; los detalles técnicos, legales y las fichas originales siguen pendientes según su registro. [ADR-051](ADR-051-gali-spa-v1.1-architecture.md) PROPOSED / NO APROBADO y [plan](GALI-SPA-V1.1-IMPLEMENTATION-PLAN-01.md) sin fases autorizadas. Se preserva el cuerpo previo como evidencia de su fecha: sus afirmaciones «solo título», alcance sin confirmar, alternativas y cierres pendientes describen la recuperación anterior y no el estado actual. No sustituye contrato/registro ni autoriza implementar.

**BORRADOR TÉCNICO DOCUMENTAL — NO ADR — NO CONTRATO APROBADO — NO IMPLEMENTACIÓN AUTORIZADA.**

Documento de desarrollo técnico (SPA-V1.1-DOC-02). Complementa, sin copiarlo, el registro de recuperación
[GALI-SPA-V1.1-RECOVERY-01](GALI-SPA-V1.1-RECOVERY-01.md), que permanece como registro histórico y conserva todos sus identificadores
(`SPA-AR-*`, `SPA-FC-*`, `SPA-AT-*`, `SPA-CN-*`, `SPA-SG-*`, `SPA-AG-*`, `SPA-SV-*`, `SPA-PQ-01`, `SPA-VT-01`, `SPA-IN-*`, `SPA-INV-*`,
`SPA-T-*`, `SPA-DEC-116`). Este borrador desarrolla cuatro componentes: agenda y disponibilidad (§2), servicios y tratamientos (§3),
paquetes y sesiones (§4) y la integración con ventas, cobros y anticipos (§5). **Ninguna regla de este documento está aprobada por el
propietario**; todas las reglas de Spa son propuestas hasta que se apruebe expresamente un ADR y una especificación contractual. Estado
de entrega: [STATUS](../../STATUS.md); cola: [WORK_QUEUE](../agent/WORK_QUEUE.md).

## 0. Cómo leer este documento

### 0.1 Etiquetas de evidencia

Se conservan las de RECOVERY-01 y se añade una para el ERP existente.

| Etiqueta | Significado |
|---|---|
| **[A]** / **[A, derivado]** | Aprobado literal o derivado, citando la sección de [ADR-050](ADR-050-employees-v1-architecture.md) o de la [especificación de Empleados](EMPLOYEES-V1-CONTRACT-SPEC-01.md) |
| **[E]** | **EXISTENTE en el ERP**: comprobado en documentación oficial o código del repositorio. Describe una capacidad del ERP; **no es una decisión de Spa** ni convierte esa capacidad en aprobada para Spa |
| **[F]** | Fuente externa de Gali Spa (formularios), pendiente de contraste directo; Claude no la ha inspeccionado |
| **[R]** | Reconstruido de la planificación, sin acta íntegra |
| **[T]** | Propuesta técnica de este borrador; no aprobada |
| **[P]** | Pendiente: decisión o contrato no recuperado. Se acompaña de alternativas explícitas, sin elegir una |
| **[NV]** | No verificado en el repositorio |

### 0.2 Reglas de lectura

1. Las reglas nuevas llevan el prefijo `SPA-OPS-` y no sustituyen ningún identificador de RECOVERY-01; cada una cita su origen en RECOVERY-01.
2. Una alternativa no es una decisión. Donde hay una laguna histórica se enumeran alternativas con sus consecuencias y dependencias; **no se
   elige ninguna**.
3. No se atribuye al propietario ninguna regla que no conste en ADR-050 o en su confirmación expresa de SPA-DEC-116 (título solamente).
4. Spa no duplica entidades oficiales: BusinessPartner/Customer, Employee, Branch, Item, facturación, cuentas por cobrar y contabilidad.
5. Las pruebas `SPA-T-01` a `SPA-T-18` son las de RECOVERY-01; las de este documento usan el prefijo `SPA-OPS-T-`.

## 1. Contratos existentes del ERP verificados para la integración

Capacidades reales que condicionan el diseño. Cada fila indica cómo se verificó. Que existan no las hace decisiones de Spa.

| Área | Capacidad verificada [E] | Evidencia | Implicación para Spa |
|---|---|---|---|
| Empleados | **No implementado.** ADR-050 Proposed; EMP-E1 PENDING / NO AUTORIZADA. `IEmployeeDirectory` es propuesta (spec §19) | `STATUS.md`, `WORK_QUEUE.md`, ADR-050 §13 | Spa no puede consumir Empleados todavía; toda dependencia es contractual |
| Items | Naturaleza `Product` o `Service`, fijada al crear; un Service no usa bodega ni genera stock, Kardex o costo; admite como máximo una presentación base | [items.md](../architecture/items.md) §3 y §5 | Un servicio comercial ya existe como Item `Service` |
| Items | `Item` es de alcance Tenant + Company y **no tiene `BranchId`**; no tiene campo de duración | [security.md](../architecture/security.md) «Items: alcance Tenant + Company (A1)»; `Item.cs` (propiedades) | Duración, sucursales y recursos de un servicio no existen en Items: serían datos propios de Spa |
| Items | Precio base `BaseSalePrice` (SSOT de Pricing); IVA de venta e IVA de compra obligatorios; categoría hoja y marca obligatorias | items.md §4; [ADR-021](ADR-021-pricing-engine-ssot.md) | El precio e impuestos de un servicio los resuelve el ERP, no Spa |
| Ventas | `SalesInvoice`: Draft → Authorize → Cancel; Ventas está **FROZEN**; una línea de ítem `Service` no admite bodega y una de `Product` la exige | `FEATURES.md`; `AuthorizeSalesUseCases.cs` («El servicio '…' no admite bodega») | Un servicio puede facturarse hoy sin inventario |
| Ventas | Devolución parcial o total sobre factura autorizada con Nota de Crédito, y reembolso explícito (efectivo, crédito a CxC o mixto); una factura se revierte por **un solo camino** | `FEATURES.md`; [backend.md](../architecture/backend.md#revertir-una-venta-un-solo-camino-por-factura-zh-sales-cancel-authorized-return-rule-01) | Devoluciones y anulaciones comerciales ya tienen reglas propias; el trato de líneas `Service` en devoluciones es [NV] |
| Ventas | No se encontró un vendedor en el dominio de ventas (búsqueda de `seller`/`vendedor`/`SalesPerson` sin resultados) | `ERP.Domain/Modules/Sales` | La separación profesional / vendedor / cobrador no tiene soporte [NV]; es contrato adicional |
| Cuentas por cobrar | Una CxC nace **solo** de una factura a crédito autorizada con saldo pendiente o de un saldo inicial (IL-5) | [receivables.md](../architecture/receivables.md) «Origen» | No existe CxC sin documento comercial |
| Cuentas por cobrar | Un cobro se aplica **a una CxC existente** del mismo cliente, con idempotencia (`ClientRequestId`) y bloqueo del saldo; el importe aplicado no puede exceder el saldo | receivables.md «Registrar un cobro» | No hay cobro «a cuenta» sin CxC |
| Cuentas por cobrar | La factura admite pago parcial con medios no-Crédito y el saldo restante nace como CxC | receivables.md «Origen» | El pago parcial al facturar ya existe |
| Anticipos | **No existe un modelo de anticipos de clientes** en `docs/architecture` ni `docs/decisions`. Existe solo el anticipo de **proveedor** ([ADR-035](ADR-035-supplier-payment-unapplied-advance.md)). Las devoluciones de dinero por cobros están pendientes de decisión funcional separada | receivables.md «Límites vigentes»; [accounting.md](../architecture/accounting.md) (anticipo `SupplierCredit`) | Los prepagos de paquetes **no tienen contrato** en el ERP |
| Contabilidad | No se encontró en `accounting.md` ingreso diferido ni devengo por servicio | búsqueda de `devengo`/`diferid` | Devengo por sesión no existe [NV] |
| Inventario | Sin tipo de movimiento de «consumo de insumos»; existen ajustes (`PositiveAdjust`, `NegativeAdjust`) con motivo y ejecución/anulación; una salida que deje saldo negativo solo la admite `SaleExit` ([ADR-041](ADR-041-negative-sale-inventory-cost-obligations.md)) | [inventory.md](../architecture/inventory.md) §5 y §6 | Un consumo automático de insumos exigiría un contrato nuevo |
| Sucursales | `Branch` es `MasterEntity` de empresa con `CompanyId`; **no tiene zona horaria** (la zona es `Company.Timezone`) | `Branch.cs`; `Company.cs` | Los horarios se interpretan en la zona de la empresa |
| Acceso a sucursal | `CompanyUserBranch` autoriza a un usuario; `IBranchAccessGuard` valida existencia, actividad, pertenencia a la empresa y esa autorización | `IBranchAccessGuard.cs`; [frozen-infrastructure.md](../architecture/frozen-infrastructure.md) | Es acceso de usuario, **no** disponibilidad laboral |
| Tiempo | Fecha de negocio `DateOnly`; instante `DateTime` UTC mostrado en `Company.Timezone`; la hora ingresada se convierte a UTC una sola vez; una hora inexistente por cambio de horario se rechaza | [data-standards.md](../architecture/data-standards.md) «Contrato temporal»; [ADR-034](ADR-034-temporal-contract-single-source.md) | Una cita es un instante UTC; los horarios recurrentes no tienen precedente |
| Integridad | `EXCLUDE USING gist` con `btree_gist` para intervalos sin solape (`accounting_periods`); advisory locks transaccionales; `xmin` como token de concurrencia | baseline `InitialProductionBaseline`; `AccountingOperationalPolicyRepository` | Hay precedentes técnicos para evitar citas solapadas |
| Auditoría | Entity Audit oficial, con eventos de dominio, `*AuditHandler` y `AuditActor`; prohibido modificar su infraestructura | [audit-infrastructure.md](../architecture/audit-infrastructure.md) | Spa añade sus propias entidades de auditoría |
| Errores | Códigos en `ApiResponseCodes` y `MessageCatalog`; el HTTP lo deriva la categoría (`Validation` 422, `BusinessRule` 400, `Duplicate` 409, `NotFound` 404, `Authorization` 403) | `ApiErrorStatus.cs` | Los errores de Spa se declaran con esas categorías |
| Permisos | Convención `modulo.recurso.accion`; el Kernel es la única fuente de permisos; los perfiles existentes no reciben permisos nuevos | [naming.md](../architecture/naming.md); ADR-050 §6 | Spa define un catálogo propio, aún inexistente |

## 2. Agenda y disponibilidad (SPA-OPS-AGE-*)

**Origen:** RECOVERY-01 §6 y SPA-AG-01 a AG-03 ([R]/[T]); ADR-050 §13 [A] (agenda, horarios, disponibilidad y sesiones pertenecen a Spa).

### 2.1 Objetivo, alcance y exclusiones

- **Objetivo [R]:** programar atenciones de clientes con profesionales en sucursales y evitar programaciones incompatibles.
- **Alcance [T]:** citas, horarios de establecimiento, disponibilidad de profesionales, duración, conflictos, reprogramación, cancelación,
  ausencia, vínculo con la atención realizada e historial.
- **Exclusiones identificadas:** no gestiona contratos laborales ni asignaciones de sucursal (Empleados, ADR-050 §7 y §8); no define permisos
  de acceso ERP (Access); no factura ni cobra (§5); no incluye recordatorios ni confirmaciones automáticas porque no están recuperados [P].

### 2.2 Separación entre disponibilidad operativa y permisos de acceso [A, derivado]

La disponibilidad operativa (si un profesional puede atender en una sucursal y a una hora) y los permisos de acceso ERP (qué puede hacer un
usuario en el sistema) son conceptos distintos (ADR-050 §6 y §8: las asignaciones laborales nunca conceden permisos). Una cita no concede ni
requiere el acceso de un usuario a una sucursal, y el acceso de un usuario a una sucursal no hace disponible a un profesional.

### 2.3 Modelo conceptual [T]

| Entidad candidata | Contenido | Relación con módulos oficiales |
|---|---|---|
| Cita (`Appointment`) | Empresa, sucursal, cliente (referencia a BusinessPartner), profesional (`EmployeeId`), servicio y duración (instantánea), inicio y fin UTC, estado, motivo de cancelación o reprogramación | `BusinessPartner`, `Employee`, `Branch`, `Item` (por referencia; sin copiar sus datos maestros) |
| Cambio de cita (`AppointmentChange`) | Registro append-only: tipo de cambio, valores anteriores y nuevos, autor, instante, motivo | `AuditActor` para el actor |
| Horario de establecimiento | Por sucursal: patrón semanal de franjas y excepciones por fecha (cierres) | `Branch`; zona de `Company.Timezone` |
| Horario de profesional | Franjas de trabajo del profesional por sucursal | `Employee` por referencia; no modifica Empleados |
| Recurso (`Resource`) | Cabina, equipo u otro elemento bloqueante, **solo si se decide** (SPA-OPS-AGE-05) | — |

### 2.4 Reglas (todas [T] salvo indicación)

| ID | Regla propuesta | Origen en RECOVERY-01 | Estado |
|---|---|---|---|
| SPA-OPS-AGE-01 | Una cita pertenece a una sola empresa y una sola sucursal, referencia a un cliente BusinessPartner activo y a un profesional (`EmployeeId`); su inicio es un instante UTC y su fin se deriva del inicio más la duración instantánea del servicio | SPA-AG-01 | [T] |
| SPA-OPS-AGE-02 | Estados candidatos: `Programada`, `Cancelada`, `Ausente`, `Atendida`. «Confirmada» y los recordatorios no se incluyen porque no están recuperados | SPA-AG-01 | [T]; confirmaciones [P] |
| SPA-OPS-AGE-03 | Horarios de establecimiento por sucursal como patrón de horas locales de la empresa; el instante UTC se obtiene con `ICompanyClock.CompanyLocalToUtcAsync`; una hora inexistente por cambio de horario se rechaza | §6 de RECOVERY-01 | [T]; no hay precedente de horarios recurrentes [NV] |
| SPA-OPS-AGE-04 | Un profesional está **disponible** en un instante si: (a) está dentro del horario del establecimiento de la sucursal; (b) está dentro de su horario de trabajo; (c) `IEmployeeDirectory` lo informa activo en esa fecha y elegible para esa sucursal; (d) no tiene otra cita activa que se solape | SPA-AG-03, SPA-AR-06 | [T]; depende de que exista `IEmployeeDirectory` [P] |
| SPA-OPS-AGE-05 | Recursos bloqueantes: alternativa A, sin recursos en V1.1 (solo profesional y sucursal); alternativa B, catálogo de recursos de Spa con exclusión de solape por recurso | SPA-AG-02 | [P]; ver 2.5 |
| SPA-OPS-AGE-06 | Dos citas activas del mismo profesional no pueden solaparse; el solape se rechaza con una condición de duplicidad. Si se permite que una cita comience en el instante en que termina otra (intervalo semiabierto) es [P] | SPA-AG-02 | [T]; tolerancias [P] |
| SPA-OPS-AGE-07 | Reprogramar mantiene la identidad de la cita y registra un cambio (instante anterior y nuevo, autor, motivo). La alternativa (cancelar y crear otra) rompe la trazabilidad y no se propone | SPA-AG-01 | [T]; efecto sobre paquetes [P] |
| SPA-OPS-AGE-08 | Cancelar fija el estado `Cancelada`, el motivo, el autor y el instante; nunca elimina. Quién puede cancelar y con qué anticipación es [P] | §6 | [T]; política [P] |
| SPA-OPS-AGE-09 | Ausencia: tras el inicio de la cita, un usuario autorizado la marca `Ausente`; su efecto sobre un paquete es [P] | §6 | [T]; efecto [P] |
| SPA-OPS-AGE-10 | Atención efectivamente realizada es un registro distinto de la cita (sesión, SPA-AT-06/AT-07). La cita pasa a `Atendida` solo cuando existe una sesión realizada vinculada; no se marca por haber llegado la fecha | SPA-AT-06 | [T] |
| SPA-OPS-AGE-11 | Una atención sin cita previa (cliente sin reserva) puede o no admitirse | §6 | [P]; alternativas: admitirla creando cita y sesión juntas, o exigir cita siempre |
| SPA-OPS-AGE-12 | Historial: todo cambio de estado, hora, profesional o sucursal queda en `AppointmentChange` y en Entity Audit | SPA-AU-01, AU-02 | [T] |
| SPA-OPS-AGE-13 | Cambiar el profesional de una cita debe revalidar la disponibilidad (AGE-04) y registrar el cambio | SPA-AR-06 | [T] |
| SPA-OPS-AGE-14 | Si un profesional deja de estar disponible (baja o cambio de asignación en Empleados), las citas futuras **no se modifican automáticamente**; se listan para su reasignación manual. La política de reasignación es [P] | SPA-AR-06 | [T]; política [P] |

### 2.5 Alternativas para las lagunas

| Tema | Alternativa A | Alternativa B | Consecuencia |
|---|---|---|---|
| Recursos (AGE-05) | Sin recursos: la agenda solo bloquea por profesional | Catálogo de recursos con exclusión de solape por recurso | A es más simple pero no evita dos citas en la misma cabina; B añade una entidad y su integridad |
| Citas contiguas (AGE-06) | Se permite que una comience cuando otra termina | Se exige un margen entre citas | B introduce un parámetro de negocio no recuperado |
| Cita sin reserva (AGE-11) | Se admite y se crea cita y sesión juntas | Se exige cita siempre | A modifica el flujo de recepción; B puede frenar la atención |

### 2.6 Flujos [T]

- **Reservar:** seleccionar cliente, servicio, sucursal, profesional y hora → comprobar AGE-04 → crear cita `Programada` y su primer registro de cambio, en una transacción.
- **Reprogramar:** nueva hora (y opcionalmente profesional) → revalidar → actualizar y registrar el cambio.
- **Cancelar:** motivo → estado `Cancelada` y cambio registrado.
- **Marcar ausencia:** solo sobre una cita pasada `Programada`.
- **Atender:** crear la sesión realizada (SPA-AT-06/AT-07) y marcar la cita `Atendida`.
- **Excepcionales:** profesional no disponible; cliente inactivo; sucursal inactiva; solape detectado por una reserva simultánea; hora inexistente por cambio de horario.

### 2.7 Transiciones candidatas [T]

`Programada → Cancelada`, `Programada → Ausente`, `Programada → Atendida`. `Cancelada`, `Ausente` y `Atendida` son finales en este borrador;
si una ausencia puede revertirse o una cita cancelada reactivarse es [P].

### 2.8 Permisos y aislamiento [T]

- Catálogo propuesto, **no existente**: `spa.agenda.view`, `spa.agenda.create`, `spa.agenda.update`, `spa.agenda.cancel`, `spa.agenda.mark-absent`,
  `spa.availability.manage`. Siguen `modulo.recurso.accion` y se declararían solo en el Kernel; ningún perfil existente los recibe
  (SPA-SG-01, SPA-SG-04).
- Aislamiento por tenant y empresa (`ICompanyOperationalEntity`); la sucursal es un filtro del recurso. Reutilizar `IBranchAccessGuard` para limitar al
  usuario a sus sucursales es una alternativa [T]; no hacerlo permitiría ver la agenda de todas las sucursales de la empresa [P].
- `employees.view` no autoriza consultar la agenda de Spa (SPA-SG-06).

### 2.9 Validaciones y errores funcionales [T]

Validaciones: cliente y profesional existentes y activos; sucursal activa de la empresa; servicio activo; inicio dentro del horario; duración
positiva; sin solape; motivo de cancelación acotado. Errores candidatos (categoría): `SPA_APPOINTMENT_NOT_FOUND` (NotFound),
`SPA_APPOINTMENT_OVERLAP` (Duplicate), `SPA_PROFESSIONAL_UNAVAILABLE` (BusinessRule), `SPA_OUTSIDE_BUSINESS_HOURS` (BusinessRule),
`SPA_APPOINTMENT_INVALID_STATE` (BusinessRule), `SPA_INVALID_DATETIME` (Validation), `SPA_CONCURRENT_MODIFICATION` (Duplicate).

### 2.10 Concurrencia e integridad [T]

- Restricción de exclusión `EXCLUDE USING gist (professional_id WITH =, tstzrange(start_utc, end_utc) WITH &&) WHERE (estado activo)`, que reutiliza el
  patrón existente de `accounting_periods`; exige `btree_gist` (ya presente) y declararse como SQL crudo en la baseline (precedente documentado en
  `docs/DATABASE.md`).
- Reserva y reprogramación toman un advisory lock transaccional por profesional (patrón de `AccountingOperationalPolicyRepository`) y verifican un token
  de versión (`xmin`).
- El mecanismo de claves de lock y la creación de la restricción deben verificarse antes de programar (como se hizo en EMP-E1) [NV].

### 2.11 API conceptual [T]

Recursos: citas (listar, consultar, crear, reprogramar, cancelar, marcar ausencia, atender), disponibilidad (consulta por profesional, sucursal y
fecha), horarios de establecimiento y de profesionales. Sin rutas aprobadas. Cada operación declara permiso, scope, validación, errores,
transacción y concurrencia (SPA-API-01).

### 2.12 Experiencia de usuario [T]

Vista de agenda por día o semana y por sucursal y profesional; creación rápida de cita; indicación de profesional no disponible; adaptación móvil.
Debe seguir los componentes y patrones oficiales ([frontend.md](../architecture/frontend.md)); sin menús definitivos (SPA-UX-01).

### 2.13 Pruebas y criterios de aceptación [T]

| ID | Escenario | Relación con RECOVERY-01 |
|---|---|---|
| SPA-OPS-T-AGE-01 | Dos reservas simultáneas del mismo profesional en el mismo intervalo: una gana y la otra recibe `SPA_APPOINTMENT_OVERLAP` (PostgreSQL real) | SPA-T-13 |
| SPA-OPS-T-AGE-02 | Una cita fuera del horario de establecimiento se rechaza | SPA-T-12 |
| SPA-OPS-T-AGE-03 | Un profesional dado de baja en Empleados no figura como disponible y sus citas futuras no cambian solas | SPA-T-18 |
| SPA-OPS-T-AGE-04 | La cita en una hora inexistente por cambio de horario se rechaza | SPA-INV-10 |
| SPA-OPS-T-AGE-05 | Reprogramar conserva la identidad y deja un cambio con valores anterior y nuevo | SPA-T-17 |
| SPA-OPS-T-AGE-06 | Cancelar y marcar ausencia no eliminan la cita y registran autor y motivo | SPA-T-17 |
| SPA-OPS-T-AGE-07 | Un usuario sin el permiso de agenda no accede; `employees.view` no alcanza | SPA-T-12 |
| SPA-OPS-T-AGE-08 | La asignación de sucursal de Empleados no concede acceso ni permisos de agenda | SPA-T-04 |
| SPA-OPS-T-AGE-09 | La cita de otra empresa o tenant no es visible | SPA-T-16 |

### 2.14 Dependencias y evidencia

Dependencias: Empleados (`IEmployeeDirectory`, no implementado), BusinessPartner/Customer, `Branch`, Items (servicio), `ICompanyClock`, Kernel de
permisos y Entity Audit. Evidencia: ADR-050 §13 [A]; RECOVERY-01 §6 y SPA-AG-01 a AG-03 [R]/[T]; §1 de este documento [E].

## 3. Servicios y tratamientos (SPA-OPS-SRV-*)

**Origen:** RECOVERY-01 §7, SPA-SV-01, SPA-SV-02, SPA-IN-01 a IN-03 ([R]/[T]/[F]).

### 3.1 Objetivo, alcance y exclusiones

- **Objetivo [R]:** definir qué servicios comercializa Spa y qué procedimientos se realizan, reutilizando el catálogo de Items.
- **Alcance [T]:** relación servicio comercial ↔ procedimiento, duración, sucursales, profesionales y especialidades elegibles, insumos documentales,
  aparatología, estado e historial, integración comercial.
- **Exclusiones:** Spa no crea otro catálogo de productos ni precios; no crea un catálogo de especialidades (es de Empleados, ADR-050 §3 DEC-E5); no
  introduce movimientos de inventario automáticos; no incorpora un módulo de activos o mantenimiento de aparatología (SPA-IN-03).

### 3.2 Servicio comercial frente a procedimiento estético [T]

| Concepto | Definición propuesta | Dueño |
|---|---|---|
| Servicio comercial | Lo que se vende y factura: un Item de naturaleza `Service` [E] con precio, impuestos y categoría | Items |
| Procedimiento estético | Lo que se realiza sobre la persona (p. ej., una técnica facial o corporal), con su protocolo, precauciones y equipos [F] | Spa |
| Tratamiento | Término de uso común; se mantiene sin definición operativa hasta decidir la relación (SPA-OPS-SRV-02) | [P] |

La planificación identificó «tratamientos individuales» y «compuestos» ([R], RECOVERY-01 §7). No está recuperada la cardinalidad entre servicio y procedimiento.

### 3.3 Reglas

| ID | Regla propuesta | Origen | Estado |
|---|---|---|---|
| SPA-OPS-SRV-01 | Todo servicio comercializable de Spa es un Item `Service` de la empresa; Spa no crea un catálogo comercial paralelo | SPA-SV-01 | [T]; Items [E] |
| SPA-OPS-SRV-02 | Relación servicio ↔ procedimiento: alternativa A, perfil de Spa 1:1 por Item (un servicio equivale a un procedimiento); alternativa B, catálogo de procedimientos de Spa con relación N:M a Items (permite servicios compuestos) | SPA-SV-01, §7 | [P]; ver 3.4 |
| SPA-OPS-SRV-03 | Duración, sucursales, recursos y condiciones de atención son **datos propios de Spa**, porque Items no tiene duración ni `BranchId` [E] | SPA-SV-02 | [T] |
| SPA-OPS-SRV-04 | Duración en minutos enteros positivos; la cita guarda una instantánea de la duración y del nombre al reservar | SPA-AG-01 | [T] |
| SPA-OPS-SRV-05 | Sucursales: por defecto el servicio es atendible en todas las sucursales activas; restringirlo a algunas es una relación opcional de Spa. Qué se espera por defecto es [P] | SPA-SV-02 | [T]; [P] |
| SPA-OPS-SRV-06 | Profesionales elegibles: se determinan por especialidad con el catálogo de Empleados (`specialtyIds` de `IEmployeeDirectory`); Spa no crea especialidades. Un empleado sin especialidades o con cargo/profesión nulos no debe suponerse elegible ni inelegible sin regla | SPA-AR-02 (P-01 de Empleados) | [T]; regla [P] |
| SPA-OPS-SRV-07 | Insumos: alternativa A, solo registro documental por sesión (productos usados como texto o referencia de Item, sin movimiento); alternativa B, lista sugerida de insumos por procedimiento, sin movimiento; alternativa C, salida manual mediante los ajustes de inventario existentes con motivo; alternativa D, consumo automático (**no propuesta**: requiere un contrato de inventario no aprobado y trata ADR-041) | SPA-IN-01, IN-02 | [P]; A y B [T] |
| SPA-OPS-SRV-08 | Aparatología: texto o referencia de identificación por procedimiento y por sesión; sin módulo de activos | SPA-IN-03 | [T] |
| SPA-OPS-SRV-09 | Estado activo/inactivo: un servicio o procedimiento inactivo no admite citas nuevas y se conserva en el historial; nunca se elimina | regla permanente del proyecto | [T] |
| SPA-OPS-SRV-10 | Condiciones de atención (precauciones, contraindicaciones, preparación): texto asociado al procedimiento; ningún campo es obligatorio por el solo hecho de figurar en una ficha física | RECOVERY-01 §0 regla 2 | [T] |
| SPA-OPS-SRV-11 | Historial de cambios: Entity Audit de las entidades de servicio de Spa; Items conserva el suyo (`ItemAudit`) [E] | SPA-AU-01 | [T] |
| SPA-OPS-SRV-12 | La venta del servicio usa el Item `Service` y el flujo de facturación existente; el precio y los impuestos los resuelve el ERP (`BaseSalePrice`, IVA) | SPA-SV-01, SPA-AR-05 | [T]; Ventas [E] |

### 3.4 Alternativas para SRV-02

| Alternativa | Descripción | Consecuencias | Dependencias |
|---|---|---|---|
| A. Perfil 1:1 | `SpaServiceProfile` por Item `Service` con duración, sucursales, especialidades requeridas, precauciones | Simple; no modela tratamientos compuestos ni un procedimiento usado en varios servicios | Items; `IEmployeeDirectory` |
| B. Procedimientos N:M | Catálogo `Procedure` de Spa; un servicio comercial referencia uno o varios procedimientos con orden y duración | Modela compuestos; añade una entidad y su catálogo | Items; reglas de duración total |

### 3.4b Flujos y estados [T]

- **Configurar un servicio:** elegir un Item `Service` activo de la empresa → completar duración, sucursales, especialidades y condiciones → activar.
- **Desactivar:** el servicio o procedimiento deja de admitir citas nuevas; el historial no cambia.
- **Excepcionales:** Item que no es `Service`; Item inactivo; especialidad inexistente en Empleados; servicio ya configurado.
- **Estados candidatos:** `Activo` e `Inactivo` (habilitar y deshabilitar; nunca eliminar). No hay otros estados.

### 3.5 Modelo conceptual [T]

`SpaServiceProfile` (o `Procedure`), referencia a `Item`; relaciones opcionales servicio-sucursal y servicio-especialidad; historial de auditoría. No se
duplican datos de Items (nombre, precio, impuestos), que se leen del Item.

### 3.6 Permisos, validaciones y errores [T]

Permisos candidatos: `spa.services.view`, `spa.services.manage`. Validaciones: el Item existe, es de la empresa, tiene naturaleza `Service` y está
activo; duración positiva; especialidades existentes en Empleados. Errores: `SPA_SERVICE_NOT_FOUND` (NotFound), `SPA_ITEM_NOT_SERVICE` (BusinessRule),
`SPA_SERVICE_INACTIVE` (BusinessRule).

### 3.7 Concurrencia, API y UX [T]

Un solo perfil por Item (`UNIQUE (company_id, item_id)`); versión optimista. API: consultar y administrar la configuración de servicio. UX: gestión
dentro de la navegación de Spa, reutilizando los componentes de configuración (patrón Lista→Editor); sin diseño aprobado.

### 3.8 Pruebas y criterios de aceptación [T]

| ID | Escenario |
|---|---|
| SPA-OPS-T-SRV-01 | Solo un Item `Service` activo de la empresa puede configurarse como servicio de Spa |
| SPA-OPS-T-SRV-02 | Un servicio inactivo no admite citas nuevas y su historial se conserva |
| SPA-OPS-T-SRV-03 | La cita conserva el nombre y la duración del momento de la reserva aunque el servicio cambie |
| SPA-OPS-T-SRV-04 | Registrar insumos documentales no crea ningún movimiento de inventario |
| SPA-OPS-T-SRV-05 | Un profesional se propone solo si su especialidad cumple la regla vigente |
| SPA-OPS-T-SRV-06 | Facturar el servicio reutiliza el flujo de ventas y no crea otro documento comercial (SPA-T-15) |

### 3.9 Dependencias y evidencia

Items [E] (items.md §3 y §5; security.md A1), Empleados [A]/[T], Ventas [E]. RECOVERY-01 §7 y SPA-SV-01/SV-02 [R]/[T]; SPA-IN-01 [F].

## 4. Paquetes y sesiones (SPA-OPS-PKG-*)

**Origen:** RECOVERY-01 §7, SPA-AT-01, SPA-AT-05, SPA-AT-06, SPA-AT-07 y SPA-PQ-01. **Contrato histórico no recuperado:** ninguna política comercial se
impone. Este apartado ofrece una estructura que admite las distintas respuestas posibles.

### 4.1 Objetivo, alcance y exclusiones

- **Objetivo [R]:** controlar los derechos de consumo de sesiones derivados de una compra, sin confundirlos con el plan de atención clínico.
- **Alcance [T]:** composición, contratación, sesiones programadas, realizadas, consumidas y pendientes, vigencia, cancelación, reprogramación,
  ausencias, cambio de profesional y sucursal, devoluciones, trazabilidad e integridad.
- **Exclusión:** no define precios, descuentos ni contabilidad (Items, Ventas y Contabilidad); no se impone ninguna política comercial.

### 4.2 Distinción conceptual [T] (SPA-AT-05)

| Concepto | Qué es | Quién lo posee |
|---|---|---|
| Plan de atención | Lo previsto para la persona: objetivo, procedimiento, sesiones sugeridas [F] | Spa (expediente) |
| Definición de paquete | Plantilla comercial: servicios incluidos y cantidades | Spa, con Item para la venta |
| Paquete contratado | Instancia de una compra del cliente: derechos de consumo | Spa |
| Libro de sesiones | Movimientos de derecho: otorgadas, consumidas, restituidas, vencidas | Spa |
| Documento comercial | La factura | Ventas [E] |

La relación entre plan de atención y paquete (automática, manual o por regla) es [P]. No se vinculan automáticamente.

### 4.3 Modelo conceptual [T]

| Entidad candidata | Contenido |
|---|---|
| `PackageDefinition` | Nombre, Item `Service` que se vende, servicios incluidos con cantidad de sesiones, vigencia opcional, estado |
| `PackageContract` | Cliente (BusinessPartner), definición, fecha de contratación, referencia a la factura (id y línea), vigencia final opcional, estado |
| `PackageSessionLedger` | Movimientos append-only: tipo (`Granted`, `Consumed`, `Restored`, `Expired`, `Adjusted`), cantidad, referencia a cita o sesión, autor, instante, motivo |
| `Session` | Atención realizada o programada (SPA-AT-06/AT-07): cita, profesional, sucursal, servicio, estado y evidencia |

### 4.4 Contadores y definiciones propuestas [T]

**La distinción entre sesión realizada y sesión consumida es una propuesta de este borrador, no una decisión histórica ni aprobada**; si una
ausencia consume o no una sesión es una pregunta contractual pendiente (SPA-PQ-01).

| Contador | Definición propuesta | Estado |
|---|---|---|
| Contratadas | Suma de movimientos `Granted` del contrato | [T] |
| Programadas | Citas activas vinculadas al contrato que aún no ocurrieron | [T] |
| Realizadas | Sesiones con estado realizado | [T] |
| Consumidas | Suma de movimientos `Consumed` menos `Restored`. «Consumida» no es sinónimo de «realizada» porque una ausencia podría consumir una sesión | [P] |
| Pendientes | Contratadas − consumidas (programadas aparte); si las programadas reservan saldo es [P] | [P] |

### 4.5 Las 12 preguntas contractuales y alternativas (SPA-PQ-01)

Ninguna respuesta está aprobada; las alternativas no son decisiones.

| Tema | Pregunta | Alternativa 1 | Alternativa 2 | Consecuencia / dependencia |
|---|---|---|---|---|
| Venta | Qué origina un paquete contratado | La factura autorizada del Item del paquete origina el contrato de forma explícita (un usuario lo crea referenciando la factura) | Un evento de la autorización de la factura crea el contrato automáticamente | La alternativa 2 exige que Ventas, que está FROZEN, publique o consuma un evento: requiere autorización y verificación [NV] |
| Composición | Qué servicios y cantidades comprende | Definición cerrada de servicios y cantidades fijas | Definición con sesiones intercambiables entre servicios | La alternativa 2 complica el saldo |
| Uso | Cuándo una sesión se considera consumida | Al realizarse la sesión | Al realizarse o al registrarse ausencia | Define el efecto de SPA-OPS-AGE-09 |
| Saldo | Cómo calcular sesiones pendientes | Saldo derivado del libro de movimientos | Saldo almacenado y actualizado bajo lock | Derivado evita desincronización; almacenado mejora la lectura |
| Vigencia | Si existen fechas límite | Sin vigencia | Con vigencia y movimiento `Expired` | Introduce un job o cálculo de vencimiento; un parámetro de negocio no recuperado |
| Reprogramación | Si afecta al saldo | No afecta | Afecta según anticipación | Ver AGE-07/AGE-08 |
| Ausencia | Si descuenta una sesión | No descuenta | Descuenta | Relacionada con «Uso» |
| Cancelación | Qué ocurre con el saldo | Se restituye la sesión | Se restituye según anticipación | Ver AGE-08 |
| Devolución | Relación con documentos comerciales | La devolución comercial (Nota de Crédito) cancela el contrato y sus sesiones no consumidas | La devolución comercial no afecta el contrato; se ajusta manualmente | Dependencia con Devolución de Ventas [E]; trato de líneas `Service` [NV] |
| Transferencia | Si puede cambiarse el beneficiario | No transferible | Transferible con trazabilidad | La alternativa 2 exige registrar al nuevo cliente y su autorización |
| Profesional | Si puede variar entre sesiones | Puede variar | Fijo salvo autorización | Afecta a AGE-13 |
| Sucursal | Si puede utilizarse en varias sucursales | Cualquier sucursal de la empresa | Solo la sucursal de contratación | Afecta a la consulta de saldo y a AGE-04 |

### 4.6 Reglas

| ID | Regla propuesta | Estado |
|---|---|---|
| SPA-OPS-PKG-01 | El saldo de sesiones se modela como libro append-only; los movimientos nunca se editan ni eliminan, se compensan con otro movimiento | [T] |
| SPA-OPS-PKG-02 | Una sesión consume como máximo una vez: restricción única `(contract_id, session_id)` en movimientos de consumo | [T] |
| SPA-OPS-PKG-03 | Todo movimiento referencia el contrato, y según el caso la cita o la sesión, el profesional (`EmployeeId`), la sucursal, el autor y el instante | [T] |
| SPA-OPS-PKG-04 | Un contrato referencia la factura (id y línea) pero no copia importes ni impuestos; el documento comercial lo posee Ventas | [T] |
| SPA-OPS-PKG-05 | El cambio de profesional o de sucursal en una sesión de un contrato se rige por las respuestas de «Profesional» y «Sucursal» | [P] |
| SPA-OPS-PKG-06 | Un contrato inactivo, vencido o devuelto no admite sesiones nuevas; sus movimientos se conservan | [T]; condiciones [P] |
| SPA-OPS-PKG-07 | No se crea ningún anticipo ni prepago de cliente dentro de Spa (ver §5) | [T] |

### 4.7 Estados candidatos [T]

Contrato: `Activo`, `Agotado`, `Vencido`, `Cancelado`. Transiciones: `Activo → Agotado` (sin saldo), `Activo → Vencido` (si existe vigencia),
`Activo → Cancelado` (devolución o anulación). Qué estados existen finalmente es [P].

### 4.8 Permisos, validaciones y errores [T]

Permisos candidatos: `spa.packages.view`, `spa.packages.manage`, `spa.packages.consume`, `spa.packages.adjust`. Validaciones: cliente igual al de la
factura; saldo suficiente para consumir; sesión del mismo contrato; sin consumo repetido. Errores: `SPA_PACKAGE_NOT_FOUND` (NotFound),
`SPA_PACKAGE_NO_BALANCE` (BusinessRule), `SPA_PACKAGE_ALREADY_CONSUMED` (Duplicate), `SPA_PACKAGE_INACTIVE` (BusinessRule).

### 4.9 Concurrencia e integridad [T]

- Consumo, restitución y ajuste toman un advisory lock por contrato y se ejecutan en una transacción, con idempotencia por `(contract_id, session_id)`.
- El saldo derivado del libro no admite saldo negativo salvo política expresa [P].
- Un contrato se crea una sola vez por línea de factura: restricción única sobre la referencia a la factura y línea.

### 4.10 API conceptual y UX [T]

Operaciones: definir paquetes; crear contrato desde una factura autorizada; consultar saldo; consumir; restituir; ajustar con motivo. UX: saldo visible
al agendar y al atender; sin diseño aprobado.

### 4.11 Pruebas y criterios de aceptación [T]

| ID | Escenario | Relación con RECOVERY-01 |
|---|---|---|
| SPA-OPS-T-PKG-01 | Dos consumos simultáneos de la misma sesión: solo uno se registra | SPA-T-14 |
| SPA-OPS-T-PKG-02 | El saldo coincide con la suma de movimientos tras una secuencia mixta | SPA-T-14 |
| SPA-OPS-T-PKG-03 | Un contrato no puede crearse dos veces para la misma línea de factura | SPA-T-15 |
| SPA-OPS-T-PKG-04 | Un movimiento no se edita ni elimina; la corrección es otro movimiento | SPA-INV-06 |
| SPA-OPS-T-PKG-05 | Cancelar, reprogramar o ausencia producen el efecto sobre el saldo que defina la política aprobada (pendiente) | SPA-PQ-01 |
| SPA-OPS-T-PKG-06 | Un contrato guarda la referencia a la factura sin copiar importes | SPA-INV-08 |

### 4.12 Dependencias y evidencia

Ventas [E] (factura, devolución), Items [E], Empleados [A]/[T], agenda (§2). Origen: RECOVERY-01 §7, SPA-AT-05, SPA-PQ-01 [R]/[T]/[P].

## 5. Integración con ventas, cobros y anticipos (SPA-OPS-VTA-*)

**Origen:** RECOVERY-01 §8, SPA-AR-05, SPA-VT-01 ([R]/[A, derivado]/[P]).

### 5.1 Distinción de conceptos

| Concepto | Descripción | Módulo propietario | Estado |
|---|---|---|---|
| Atención estética | Lo realizado a la persona | Spa | [R] |
| Servicio vendido | Línea de una factura con un Item `Service` | Ventas | [E] |
| Paquete contratado | Derechos de sesiones derivados de una compra | Spa (referenciando la factura) | [T] |
| Sesión realizada | Atención documentada | Spa | [T] |
| Documento comercial | Factura, nota de crédito | Ventas | [E] |
| Pago recibido | Cobro aplicado a una CxC, o medio de pago al facturar | Ventas / Cuentas por cobrar | [E] |
| Saldo pendiente | `BalanceDue` de la CxC | Cuentas por cobrar | [E] |

### 5.2 Capacidades existentes y capacidades que requieren contrato adicional

| Capacidad | Existe en el ERP | Evidencia | Qué requiere Spa |
|---|---|---|---|
| Vender un servicio de Spa | **Sí**: factura con un Item `Service` sin bodega | `AuthorizeSalesUseCases.cs`; items.md §3 | Nada nuevo; Spa referencia la factura |
| Pago parcial al facturar | **Sí**: medios no-Crédito y el saldo como CxC | receivables.md «Origen» | Nada nuevo |
| Cobrar un saldo posterior | **Sí**: cobro aplicado a una CxC existente | receivables.md «Registrar un cobro» | Nada nuevo |
| Devolver o anular una venta | **Sí**, con las reglas de Ventas (un solo camino) | backend.md; FEATURES.md | Definir el efecto sobre el contrato (SPA-PQ-01) |
| Facturar por cada sesión | No existe un concepto de factura por sesión; una factura es una venta | [NV] | Contrato nuevo [P] |
| Anticipo o prepago de cliente | **No existe** modelo de anticipos de clientes | §1 de este documento | Contrato nuevo **fuera de Spa**; decisión del propietario |
| Devengo de ingresos por sesión | No se encontró ingreso diferido ni devengo | accounting.md | Contrato contable nuevo [NV] |
| Devolución de dinero por cobros | Pendiente de decisión funcional separada | receivables.md «Límites vigentes» | Decisión del propietario |
| Vendedor distinto de profesional y cobrador | No se encontró vendedor en el dominio de ventas [NV] | `ERP.Domain/Modules/Sales` | Contrato nuevo [P] |

### 5.3 Reglas

| ID | Regla propuesta | Estado |
|---|---|---|
| SPA-OPS-VTA-01 | Spa **no** emite facturas, cobros ni asientos; solo referencia documentos de Ventas y de Cuentas por cobrar por identificador | [T]; SPA-AR-05, SPA-INV-08 |
| SPA-OPS-VTA-02 | El paquete se vende como un Item `Service` por la facturación existente; Spa conserva el contrato y sus sesiones | [T] |
| SPA-OPS-VTA-03 | Spa **no** crea un mecanismo de anticipos de clientes. Si el negocio requiere cobrar por adelantado un paquete, existen dos caminos: (A) vender el paquete completo con la factura, pagado al contado o parcial con saldo a crédito (capacidades existentes); (B) definir un contrato nuevo de anticipos de clientes en el módulo de Cobros, **fuera de Spa**, con decisión del propietario | [P]; no se elige |
| SPA-OPS-VTA-04 | El registro de una sesión no genera factura, cobro ni movimiento contable | [T] |
| SPA-OPS-VTA-05 | El profesional que presta el servicio y el usuario que registra u opera la transacción se identifican por separado | [A, derivado] (ADR-050 §3 DEC-E4, §13); vendedor y cobrador [P] |
| SPA-OPS-VTA-06 | La devolución o anulación comercial no modifica el libro de sesiones por sí sola; la acción sobre el contrato es explícita y sigue la política de SPA-PQ-01 | [T]; política [P] |

### 5.3b Estados y modelo conceptual [T]

Spa **no define estados comerciales**: el estado de la factura, de la CxC y de los cobros pertenece a Ventas y a Cuentas por cobrar [E]. Spa solo
guarda referencias (identificador de factura y de línea, identificador de CxC si se consulta) y el estado de sus propias entidades (contrato y sesión,
§4). Entidades comerciales nuevas: ninguna.

### 5.4 Flujos [T]

- **Venta de servicio individual:** facturar el Item `Service` por el flujo existente → la cita o sesión referencia la factura si el negocio lo requiere.
- **Venta de paquete:** facturar el Item del paquete → un usuario autorizado crea el contrato referenciando la factura y línea (alternativa «explícita» de §4.5).
- **Consumo:** el consumo de sesiones no toca Ventas.
- **Devolución:** se ejecuta en Ventas; Spa recibe la decisión sobre el contrato según la política pendiente.

### 5.5 Permisos, aislamiento y errores [T]

La consulta de datos comerciales desde Spa requiere un permiso de Spa y el de Ventas correspondiente (`SPA-SG-04`: «consultar información comercial»); el
catálogo no existe. Errores candidatos: `SPA_INVOICE_NOT_AUTHORIZED` (BusinessRule), `SPA_INVOICE_CUSTOMER_MISMATCH` (BusinessRule),
`SPA_INVOICE_LINE_ALREADY_LINKED` (Duplicate).

### 5.6 Concurrencia, API y UX [T]

Referencia única por línea de factura (§4.9). API: enlazar contrato con factura; consultar estado comercial. UX: mostrar factura y saldo pendiente como lectura.

### 5.7 Pruebas y criterios de aceptación [T]

| ID | Escenario | Relación con RECOVERY-01 |
|---|---|---|
| SPA-OPS-T-VTA-01 | Facturar un servicio de Spa reutiliza el flujo de ventas sin bodega | SPA-T-15 |
| SPA-OPS-T-VTA-02 | Registrar una sesión no crea factura, cobro ni asiento | SPA-T-15 |
| SPA-OPS-T-VTA-03 | El contrato referencia la factura sin copiar importes | SPA-INV-08 |
| SPA-OPS-T-VTA-04 | Una factura de otro cliente no puede enlazarse al contrato | SPA-T-01 |
| SPA-OPS-T-VTA-05 | La anulación o devolución comercial sigue las reglas de Ventas y no altera el libro de sesiones de forma implícita | SPA-T-14 |
| SPA-OPS-T-VTA-06 | Profesional y usuario registrador se guardan por separado | SPA-T-03 |

### 5.8 Dependencias y evidencia

Ventas (FROZEN) [E], Cuentas por cobrar [E], Items [E], Contabilidad [E] (sin devengo), ADR-050 §13 y DEC-E4 [A]. Origen: RECOVERY-01 §8, SPA-AR-05, SPA-VT-01.

## 6. Decisiones históricas conservadas

- **SPA-DEC-116 — Revisión y consolidación del alcance de Gali Spa V1.1.** Estado: aprobada y confirmada por el propietario; solo el título está
  confirmado y su texto íntegro y alcance literal **no están recuperados**. Este borrador **no deduce su alcance** del título ni de este documento.
- No se declaran existentes ni aprobadas las decisiones `SPA-DEC-001` a `SPA-DEC-115`.
- Ningún identificador de RECOVERY-01 se ha reemplazado.

## 7. Trazabilidad hacia RECOVERY-01

| Regla de este borrador | RECOVERY-01 |
|---|---|
| SPA-OPS-AGE-01 a AGE-14 | §6, SPA-AG-01 a AG-03, SPA-AT-06, SPA-AT-07, SPA-AR-06, SPA-AU-01, AU-02, SPA-SG-04 a SG-06 |
| SPA-OPS-SRV-01 a SRV-12 | §7, SPA-SV-01, SV-02, SPA-IN-01 a IN-03, SPA-AR-02, SPA-AU-01 |
| SPA-OPS-PKG-01 a PKG-07 | §7, SPA-AT-05, SPA-PQ-01, SPA-INV-06 |
| SPA-OPS-VTA-01 a VTA-06 | §8, SPA-AR-05, SPA-VT-01, SPA-INV-08 |

## 8. Vacíos de evidencia y riesgos

| # | Vacío o riesgo | Efecto |
|---|---|---|
| 1 | Empleados no está implementado y `IEmployeeDirectory` es propuesta | La disponibilidad (AGE-04) no puede verificarse hasta que exista el contrato |
| 2 | Las fichas de Gali Spa no han sido inspeccionadas por Claude | Todo el contenido [F] sigue sin contraste directo |
| 3 | Decisiones históricas `SPA-DEC-*` no recuperadas | No se puede afirmar qué se aprobó sobre agenda, paquetes y cobros |
| 4 | No existe un modelo de anticipos de clientes ni devengo por sesión | Los prepagos de paquetes no tienen soporte; requiere una decisión fuera de Spa |
| 5 | Ventas está FROZEN y no se verificó un evento de autorización consumible | La creación automática de contratos desde la factura requeriría autorización |
| 6 | No se verificó el trato de líneas `Service` en devoluciones de venta | El efecto de una devolución sobre un paquete no puede afirmarse |
| 7 | No hay precedente de horarios recurrentes con zona horaria | El diseño de horarios (AGE-03) necesita verificación técnica |
| 8 | No hay catálogo de permisos de Spa | Todos los permisos de este borrador son nombres candidatos |
| 9 | El vendedor no existe en Ventas | La separación vendedor / cobrador / profesional queda pendiente |

## 9. Verificación

Coherencia con ADR-050 y con la especificación de Empleados; ninguna regla se atribuye al propietario salvo el título de SPA-DEC-116; no se duplican
entidades oficiales; trazabilidad hacia RECOVERY-01 (§7). No autoriza ninguna implementación de Spa ni de Empleados, ni migraciones, ni cambios en módulos
FROZEN.

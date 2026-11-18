# Gali Spa V1.1 — Normalización de contratos y preparación del cierre documental

> **REFERENCIA HISTÓRICA NO NORMATIVA — actualizada el 2026-11-18.** SPA-DEC-115/116 se recuperaron aprobadas por evidencia aportada por el propietario: [registro §9/10](GALI-SPA-V1.1-APPROVED-DECISIONS-01.md). El [contrato central](GALI-SPA-V1.1-CONTRACT-SPEC-01.md) contiene el alcance funcional aprobado y O-23 cerrada en inclusión general; los detalles técnicos, legales y las fichas originales siguen pendientes según su registro. [ADR-051](ADR-051-gali-spa-v1.1-architecture.md) PROPOSED / NO APROBADO y [plan](GALI-SPA-V1.1-IMPLEMENTATION-PLAN-01.md) sin fases autorizadas. Se preserva el cuerpo previo como evidencia de su fecha: sus afirmaciones «solo título», alcance sin confirmar, alternativas y cierres pendientes describen la recuperación anterior y no el estado actual. No sustituye contrato/registro ni autoriza implementar.

**NORMALIZACIÓN DOCUMENTAL — PROPUESTAS NO APROBADAS — NO CONTRATO IMPLEMENTABLE.**

Documento de normalización (SPA-V1.1-DOC-06). Ejecuta las tareas `K-01` a `K-08` de la [matriz maestra](GALI-SPA-V1.1-CONTRACT-CONSOLIDATION-MATRIX-01.md) (§10.4) sobre los cuatro documentos de Spa
([RECOVERY-01](GALI-SPA-V1.1-RECOVERY-01.md), [OPERATIONS-SPEC](GALI-SPA-V1.1-OPERATIONS-SPEC-DRAFT-01.md), [CARE-PRIVACY](GALI-SPA-V1.1-CARE-PRIVACY-SPEC-DRAFT-01.md) y
[SECURITY-COMPLIANCE](GALI-SPA-V1.1-SECURITY-COMPLIANCE-DRAFT-01.md)), **sin modificarlos, sin renumerar sus identificadores y sin reconstruir la matriz**. Estado de entrega: [STATUS](../../STATUS.md); cola:
[WORK_QUEUE](../agent/WORK_QUEUE.md).

**Ningún contenido de este documento está aprobado por el propietario.** Las redacciones, el glosario, el catálogo de permisos y la relación de pruebas son **propuestas de normalización**. No se aprueban políticas comerciales, jurídicas ni de seguridad.

## 0. Cómo leer este documento

**Actualización del 2026-11-18:** el propietario aprobó `O-25` (alternativa A: cita separada de la sesión realizada) y `O-15` (alternativa A: contrato de paquete creado explícitamente tras validar una factura autorizada); el texto exacto y sus límites están en el [registro de decisiones aprobadas](GALI-SPA-V1.1-APPROVED-DECISIONS-01.md).
Este documento **conserva su redacción original** y solo se actualizan las referencias afectadas, marcadas como «aprobada por `O-25`» o «aprobada por `O-15`». **Ninguna otra cuestión `O-xx` se cierra por inferencia**, y las definiciones y propuestas no mencionadas siguen sin aprobarse.

Se conservan las etiquetas de la matriz: **A** (aprobada literal), **A-der** (derivada), **E** (existente en el ERP), **F** (formularios, sin contraste directo), **R** (reconstruida), **T** (propuesta técnica), **P** (pendiente) y **NV** (no verificado).
Los identificadores `SPA-*` son referencias a los definidos en R1 a R4; los códigos `K-xx`, `C-xx`, `D-xx`, `V-xx`, `O-xx`, `DUP-xx` y `CE-xx` son los de la matriz; los `PT-xx` (grupos de pruebas) y `G-xx` (glosario) son **propios de este documento**.

## 1. Resultado de K-01 a K-08

Redacción original de cada tarea (matriz §10.4) y su clasificación.

| K | Tarea (redacción original) | Resultado | Clasificación |
|---|---|---|---|
| K-01 | Unificar las duplicaciones `DUP-01` a `DUP-16` en una redacción única por regla | **Parcialmente ejecutada:** 14 de 16 grupos tienen redacción canónica propuesta (§1.1; `DUP-08` se incorporó tras aprobarse `O-25`); 2 quedan bloqueados | Ejecutable sin decisiones nuevas (14); dependiente del propietario (2: `DUP-07` por O-05 y `DUP-10` por O-14) |
| K-02 | Unificar el catálogo candidato de permisos y resolver el solapamiento de nombres | **Ejecutada** en cuanto a nombres (§3); las políticas de acceso siguen abiertas | Ejecutable sin decisiones nuevas (nombres); dependiente del propietario (políticas) |
| K-03 | Alinear la terminología de sesión, cita y consumo dejando la decisión como alternativa explícita | **Ejecutada como glosario propuesto** (§2); no resuelve C-06 | Ejecutable sin decisiones nuevas; la decisión es de O-25 |
| K-04 | Unificar las familias de pruebas y enlazarlas a los invariantes | **Ejecutada** (§4): 102 pruebas agrupadas en 17 grupos `PT-xx`, sin renumerar | Ejecutable sin decisiones nuevas |
| K-05 | Incorporar el texto de las verificaciones V-03 a V-05 y V-08 por lectura del repositorio | **Ejecutada** (§5): V-03, V-04, V-05 y V-08 cerradas en lo verificable; V-07 cerrada en parte | Ejecutable sin decisiones nuevas; restos dependientes de evidencia externa o de implementación |
| K-06 | Añadir los documentos de Spa al índice de `docs/decisions/README.md` | **Preparada** (§8); **no aplicada** (el README no se modifica en esta fase) | Dependiente de autorización del propietario |
| K-07 | Contrastar el contenido [F] con las fichas cuando estén disponibles (V-01) | **No ejecutable ahora** | Dependiente de evidencia externa |
| K-08 | Consolidar los pendientes en el registro O-xx | **Ya hecha en la matriz (§11); aquí se clasifica cada `O-xx` por tipo de dependencia** (§6.2) | Ejecutable sin decisiones nuevas |

Resumen: **ejecutadas 5** (K-02, K-03, K-04, K-05, K-08), **parcialmente 1** (K-01), **preparada sin aplicar 1** (K-06) y **no ejecutable 1** (K-07).

### 1.1 Redacciones canónicas propuestas (K-01)

Cada redacción **reproduce el contenido del ancla de la matriz** y no añade decisiones; la etiqueta de evidencia es la de la fuente. Una redacción canónica es una propuesta de texto, no una aprobación.

| DUP | Ancla | Redacción canónica propuesta | Evidencia | Estado |
|---|---|---|---|---|
| DUP-01 | `SPA-AR-03` | Cada atención registrada identifica al **profesional responsable** (Employee, `EmployeeId`) y, por separado, al **usuario ERP que la registró**; son conceptos distintos. Los profesionales que documentan atenciones utilizan usuarios ERP autorizados. | A (ADR-050 §3 DEC-E4, §13); «se auditan por separado» es A-der | Ejecutada |
| DUP-02 | `SPA-AR-01` y `SPA-AR-02` | Spa no crea identidades paralelas: el cliente es un BusinessPartner con rol Customer y el profesional un Employee; Spa referencia sus identificadores y no copia nombre, identificación ni contacto. | A para Employee (ADR-050 §3 DEC-E1, §13); R para el cliente | Ejecutada |
| DUP-03 | `SPA-SG-01` | Ningún permiso de Empleados (`employees.*`) ni de Access (`access.*`), ni una asignación laboral a una sucursal, concede acceso a datos clínicos de Spa; Spa declara permisos propios en el Kernel y ningún perfil existente los recibe automáticamente. | A-der (ADR-050 §6, §13) | Ejecutada |
| DUP-04 | `SPA-CN-06` | Las autorizaciones de procedimiento, de fotografías de seguimiento, de publicación promocional y de uso académico son independientes entre sí: aceptar una no implica aceptar otra, y la finalidad se verifica contra la autorización vigente en el momento del uso. | F + T; el uso académico está pendiente de confirmar (O-17) | Ejecutada |
| DUP-05 | `SPA-SEC-AUD-01` | Toda lectura exitosa de un recurso sensible deja un registro append-only con usuario, empresa, recurso, operación, instante y resultado, sin contenido clínico. | T | Ejecutada; modo de escritura y retención pendientes (O-03) |
| DUP-06 | `SPA-FC-08` | Una corrección crea una versión nueva con autor, fecha y motivo; las versiones anteriores se conservan con acceso restringido y no se muestran como vigentes. | T; si la rectificación obliga a ocultar la versión inexacta es L-NV | Ejecutada |
| DUP-07 | `SPA-SEC-RET-01` | **Bloqueada:** el texto de R3 («nunca se elimina») y el de R4 (procedimiento de eliminación legal controlado) son incompatibles; no se redacta una versión única. | — | **Bloqueada por O-05 (C-02)** |
| DUP-08 | `SPA-AT-06` | La **cita** representa la programación en la agenda y la **sesión realizada** es un registro independiente creado al documentar una atención; una **sesión programada** de un paquete es una cita vinculada al contrato; **reservar y reprogramar no consume saldo**; cancelar o registrar una ausencia **no crea una sesión realizada**. | A (aprobada por `O-25`, [registro de decisiones aprobadas](GALI-SPA-V1.1-APPROVED-DECISIONS-01.md)) | Ejecutada; las políticas de saldo (ausencias que consumen, efecto de cancelar) siguen abiertas (O-13) |
| DUP-09 | `SPA-AT-05` | El plan de tratamiento describe lo previsto para la persona; el paquete comercial representa derechos de consumo derivados de una compra; no se vinculan automáticamente y su relación es una decisión pendiente. | T + P | Ejecutada; relación pendiente (O-13) |
| DUP-10 | `SPA-OPS-VTA-03` | **Bloqueada:** depende de si el ERP debe tener anticipos de clientes, capacidad inexistente hoy. | — | **Bloqueada por O-14 (C-08)** |
| DUP-11 | `SPA-INV-10` | Las fechas de negocio son `DateOnly`; los instantes, UTC mostrados en `Company.Timezone`; los valores numéricos siguen la política de precisión oficial. Las medidas corporales **no coinciden con ninguna clase de la tabla de escalas** y requieren revisión arquitectónica (V-04). | A-der (normas oficiales) + hallazgo V-04 | Ejecutada; medidas pendientes (O-22) |
| DUP-12 | `SPA-IN-02` | El registro de insumos y equipos es documental y no genera movimientos de inventario; cualquier movimiento exige un contrato aprobado. | T | Ejecutada; contrato pendiente (O-12) |
| DUP-13 | `SPA-AG-01` a `AG-03` | La agenda, los horarios y la disponibilidad son de Spa; la disponibilidad combina horario del establecimiento, horario del profesional, elegibilidad informada por Empleados y ausencia de solape; no se confunde con el acceso de usuario. | A (ADR-050 §13 para la pertenencia) + T | Ejecutada; depende de Empleados |
| DUP-14 | `SPA-SV-01` | Todo servicio comercializable es un Item `Service` de la empresa; Spa guarda como datos propios lo que Items no tiene (duración, sucursales, condiciones) sin duplicar nombre, precio ni impuestos. | E + T | Ejecutada |
| DUP-15 | `SPA-SG-06` | Las proyecciones de agenda y los listados no exponen datos clínicos ni fotografías; estos solo se ven dentro del expediente y con permiso. | T | Ejecutada |
| DUP-16 | `SPA-FC-07` | La fecha de nacimiento y el contacto de emergencia no existen en BusinessPartner; su almacenamiento se decide sin copiar la identidad de BP. | P | Ejecutada como enunciado; decisión pendiente (O-06) |

## 2. Glosario contractual (propuesta)

Distingue los conceptos que los documentos usan de forma entrelazada. **Todas las definiciones son propuestas [T]**, salvo donde se indica una etiqueta A o E y salvo las **alternativas A de G-09 y G-11, aprobadas por `O-25` el 2026-11-18**. Resuelven el conflicto terminológico C-06; **no resuelven sus políticas comerciales**, que siguen abiertas.

| G | Término | Definición propuesta | No es | Entidad u origen oficial | Fuentes | Estado |
|---|---|---|---|---|---|---|
| G-01 | **Cliente** | Persona atendida. Su identidad es un BusinessPartner con rol Customer; Spa no crea otra identidad. | El expediente | MasterData (BusinessPartner) | `SPA-AR-01`, `SPA-INV-01` | R |
| G-02 | **Expediente** | Registro longitudinal, propio de Spa, de la atención de un cliente (antecedentes versionados, valoraciones, sesiones, consentimientos y fotografías). Referencia al cliente; no copia su identidad. | El cliente; la ficha física | Spa | `SPA-FC-01`, `SPA-CARE-EXP-01` | T; cardinalidad pendiente (O-20) |
| G-03 | **Profesional** | Empleado que presta o documenta la atención, identificado por `EmployeeId`. «Profesional responsable» es el que realiza la atención. | El usuario registrador | Empleados (Employee) | `SPA-AR-03`, `SPA-INV-02` | **A** (ADR-050 §3 DEC-E4, §13) |
| G-04 | **Usuario registrador** | Usuario ERP que registra la información en el sistema. Distinto del profesional aunque puedan coincidir en una misma persona (observación T). | El profesional | Identity / Access (IdentityUser) | `SPA-SG-02`, `SPA-INV-03` | **A** para la distinción; la coincidencia posible es T |
| G-05 | **Servicio** | Lo que se comercializa y factura: un Item de naturaleza `Service`. | El procedimiento | Items (`items.md` §3) | `SPA-SV-01`, `SPA-OPS-SRV-01` | **E** + T |
| G-06 | **Procedimiento** | Técnica estética realizada sobre la persona, con su protocolo, precauciones y equipos. Concepto propio de Spa. Su relación con el servicio (1:1 o N:M) es pendiente. | El servicio | Spa | `SPA-OPS-SRV-02`, `SPA-FC-04` | F + R; relación pendiente (O-11) |
| G-07 | **Plan de tratamiento** | Lo previsto para la persona: objetivo, procedimiento, sesiones sugeridas, cuidados y decisión de proceder, adaptar o posponer. Sinónimo propuesto de «plan de atención» (`SPA-AT-01`). | El paquete comercial | Spa (expediente) | `SPA-AT-01`, `SPA-AT-05` | F; el sinónimo es T |
| G-08 | **Paquete comercial** | Derecho de consumo de sesiones derivado de una compra (definición, contrato y libro de movimientos). | El plan de tratamiento | Spa, con la factura de Ventas | `SPA-PQ-01`, `SPA-OPS-PKG-01` a `PKG-07` | T + P (O-13) |
| G-09 | **Reserva** | Acto de apartar un intervalo para un profesional (y, si se decide, un recurso). **Alternativa A (APROBADA por `O-25`, 2026-11-18):** la reserva es la operación que crea una cita, no una entidad aparte. ~~Alternativa B: reserva provisional como entidad previa a la confirmación~~ (no aprobada). | La atención | Spa (agenda) | `SPA-AG-01`, `SPA-OPS-AGE-01` | **A (aprobada por `O-25`)** |
| G-10 | **Cita** | Registro programado en la agenda: cliente, profesional, sucursal, servicio, inicio y fin (UTC) y estado. | La atención ni la sesión | Spa (agenda) | `SPA-OPS-AGE-01`, `SPA-OPS-AGE-02` | T |
| G-11 | **Sesión programada** | Sesión prevista dentro de un plan o paquete que aún no se realiza. **Alternativa A (APROBADA por `O-25`, 2026-11-18):** no es entidad propia, es una cita vinculada al plan o contrato. ~~Alternativa B: entidad propia con estado~~ (no aprobada). | La sesión realizada | Spa | `SPA-AT-06`, `SPA-OPS-AGE-10` | **A (aprobada por `O-25`)**; políticas de saldo abiertas (O-13) |
| G-12 | **Atención** | El hecho de prestar un procedimiento a un cliente en un momento determinado. Se documenta mediante una sesión realizada. | La cita (programación) ni la sesión (registro) | Spa | `SPA-OPS-AGE-10`, `SPA-AT-06` | T |
| G-13 | **Sesión realizada** | Registro documentado de una atención efectivamente prestada (fecha, procedimiento, profesional, observaciones, reacciones, evolución). No se registra como realizada por haber llegado la fecha. | La sesión programada ni la consumida | Spa | `SPA-AT-02`, `SPA-AT-06`, `SPA-AT-07` | F + T |
| G-14 | **Sesión consumida** | Sesión que descuenta saldo de un paquete (movimiento de consumo del libro). **No equivale a «realizada»:** si una ausencia consume o no es una pregunta pendiente. | La sesión realizada | Spa (libro del contrato) | `SPA-OPS-PKG-01`, §4.4 de R2 | T + P; propuesta de R2 (O-13) |
| G-15 | **Consentimiento** | Autorización registrada, con tipo (procedimiento, fotografías de seguimiento, publicación promocional, uso académico pendiente), versión del texto, decisión, fecha y evidencia. Cada tipo es independiente. | Una firma sola ni una fotografía | Spa | `SPA-CN-01` a `CN-06`, `SPA-CARE-CNS-01` | F + T |

### 2.1 Relaciones entre conceptos (propuesta)

- Un **cliente** tiene uno o varios **expedientes** (O-20); el expediente contiene **planes de tratamiento**, **sesiones realizadas**, **consentimientos** y **fotografías**.
- Un **servicio** se vende por Ventas; un **paquete comercial** referencia la factura y el servicio y genera **sesiones consumidas** en su libro.
- Una **cita** (resultado de una **reserva**) puede dar lugar a una **atención**, que se documenta en una **sesión realizada**; esa sesión puede ser, además, **consumida** de un paquete.
- La **cita** y la **sesión programada** pueden ser el mismo registro (alternativa A de G-11) o dos (alternativa B): decisión pendiente.
- El **profesional** (Employee) y el **usuario registrador** (IdentityUser) se guardan siempre por separado.

## 3. Catálogo unificado de permisos candidatos

**Ninguno existe en el Kernel ni está aprobado.** Siguen `modulo.recurso.accion` ([naming](../architecture/naming.md)); solo se declararían en el Kernel y ningún perfil existente los recibiría automáticamente.
Respeta los contratos de Identity y Access: **no se modifica el bypass del administrador ni se define la política de acceso clínico** (conflicto C-01, abierto; O-01).

### 3.1 Resultado de la unificación

R2, R3 y R4 mencionan **30 nombres distintos**. **Ninguna operación recibe dos nombres diferentes**; el solapamiento detectado en la matriz (C-09) correspondía a seis nombres repetidos con el mismo significado
(`spa.consents.manage`, `spa.history.update`, `spa.photos.create`, `spa.photos.view`, `spa.records.export`, `spa.records.view`). Se conservan los 30 nombres y se fija un único significado para cada uno.
**C-09 queda cerrado en cuanto a nombres (propuesta no aprobada)**; las políticas siguen abiertas.

### 3.2 Catálogo

| Familia | Permiso candidato | Operación (significado único propuesto) | Aparece en |
|---|---|---|---|
| Expediente | `spa.records.view` | Consultar el expediente y sus listados (incluye valoraciones, sesiones y consentimientos salvo que se decida separar su consulta; ver §3.3) | R3, R4 |
| Expediente | `spa.records.manage` | Abrir o inactivar un expediente | R3 |
| Expediente | `spa.records.export` | Exportar expediente o fotografías | R3, R4 |
| Expediente | `spa.records.emergency-access` | Acceso excepcional con motivo obligatorio (alternativa D de R4 §3.3; no aprobada) | R4 |
| Antecedentes | `spa.history.view` | Consultar los antecedentes de salud | R4 |
| Antecedentes | `spa.history.update` | Modificar antecedentes (versión nueva con motivo) | R3, R4 |
| Valoraciones | `spa.assessments.create` | Registrar una valoración facial o corporal | R3 |
| Valoraciones | `spa.assessments.update` | Modificar una valoración (versión nueva) | R3 |
| Sesiones | `spa.sessions.create` | Registrar una sesión realizada | R3 |
| Consentimientos | `spa.consents.manage` | Registrar y retirar consentimientos | R3, R4 |
| Consentimientos | `spa.consents.templates` | Administrar los textos versionados de consentimiento | R3 |
| Fotografías | `spa.photos.view` | Consultar fotografías | R3, R4 |
| Fotografías | `spa.photos.create` | Subir fotografías | R3, R4 |
| Agenda | `spa.agenda.view` | Consultar la agenda | R2 |
| Agenda | `spa.agenda.create` | Crear una cita (reservar) | R2 |
| Agenda | `spa.agenda.update` | Reprogramar o modificar una cita | R2 |
| Agenda | `spa.agenda.cancel` | Cancelar una cita | R2 |
| Agenda | `spa.agenda.mark-absent` | Marcar ausencia | R2 |
| Agenda | `spa.availability.manage` | Administrar horarios y disponibilidad | R2 |
| Servicios | `spa.services.view` | Consultar la configuración de servicios de Spa | R2 |
| Servicios | `spa.services.manage` | Administrar la configuración de servicios de Spa | R2 |
| Paquetes | `spa.packages.view` | Consultar paquetes, contratos y saldo | R2 |
| Paquetes | `spa.packages.manage` | Definir paquetes y crear contratos | R2 |
| Paquetes | `spa.packages.consume` | Consumir sesiones de un contrato | R2 |
| Paquetes | `spa.packages.adjust` | Ajustar el saldo con motivo | R2 |
| Auditoría | `spa.audit.view` | Consultar el **historial de cambios** de un expediente (Entity Audit) | R3 |
| Auditoría | `spa.access-log.view` | Consultar el **registro de accesos** (lecturas) | R4 |
| Acceso clínico | `spa.access-grants.manage` | Gestionar concesiones de acceso clínico (alternativa C de R4 §3.3; no aprobada) | R4 |
| Retención | `spa.erasure.request` | Solicitar una eliminación legal (procedimiento no aprobado) | R4 |
| Retención | `spa.erasure.execute` | Ejecutar una eliminación legal (procedimiento no aprobado) | R4 |

### 3.3 Operaciones sin permiso candidato o con alcance ambiguo

No se inventan nombres: se dejan como **pendientes de definición**.

| Operación | Observación |
|---|---|
| Consultar sesiones realizadas | R3 lista «registrar sesiones» pero no su consulta; no se sabe si cubre `spa.records.view` |
| Consultar valoraciones | R3 lista crear y modificar valoraciones; su consulta se supone dentro de `spa.records.view` sin decirlo |
| Consultar consentimientos | R3 lista «gestionar consentimientos»; la consulta no tiene permiso propio |
| Desactivar fotografías | No hay permiso; podría entrar en `spa.photos.create` o requerir otro |
| Horarios de establecimiento frente a horarios de profesionales | `spa.availability.manage` los cubriría a ambos; no se separa |
| Mostrar información comercial desde Spa | R1 `SPA-SG-04` menciona «consultar información comercial» sin nombre |

### 3.4 Distinciones que evitan confundir nombres parecidos

- `spa.audit.view` (cambios de un expediente) **no** es `spa.access-log.view` (quién leyó).
- `spa.history.*` (antecedentes de salud) **no** es `spa.assessments.*` (valoraciones) ni `spa.records.*` (expediente).
- `spa.sessions.create` (registrar la atención) **no** es `spa.packages.consume` (descontar saldo del paquete); una operación puede requerir ambos.
- `spa.records.emergency-access`, `spa.access-grants.manage` y `spa.erasure.*` existen **solo** si se aprueban las alternativas de R4; sin esa decisión son nombres reservados.

## 4. Trazabilidad de pruebas (102 candidatas)

Relación única de las pruebas de R1 (`SPA-T-*`), R2 (`SPA-OPS-T-*`), R3 (`SPA-CARE-T-*`) y R4 (`SPA-SEC-T-*`), **sin renumerar ni crear identificadores**. Cada una figura en **un solo grupo**.
Del total, **80 identificadores son literales** y **22 son inferidos** (pruebas de R3 escritas con sufijo abreviado); en el conjunto de la matriz hay **273 identificadores literales y 22 inferidos**, y los 22 inferidos son todos pruebas.
Los inferidos se listan en la columna correspondiente; su referencia literal abreviada está en el §4.2 de la matriz. **Ninguna prueba está aprobada.**

| PT | Tema | Verifica (reglas e invariantes) | Pruebas literales | Pruebas inferidas | Estado |
|---|---|---|---|---|---|
| PT-01 | Cliente e identidad | SPA-AR-01, SPA-INV-01, SPA-FC-07, SPA-CARE-EXP-01, SPA-SEC-INV-11 | `SPA-T-01`, `SPA-CARE-T-EXP-01`, `SPA-OPS-T-VTA-04`, `SPA-SEC-T-13` | — | Abierto en lo no recuperado (flujo de cliente inexistente) |
| PT-02 | Profesional, empleado y usuario registrador | SPA-AR-03, SPA-SG-02, SPA-INV-02, SPA-INV-03, SPA-CARE-EXP-05, SPA-SEC-INV-08 | `SPA-T-02`, `SPA-T-03`, `SPA-CARE-T-EXP-05`, `SPA-CARE-T-SEG-06`, `SPA-OPS-T-VTA-06`, `SPA-OPS-T-SRV-05` | — | Depende de `IEmployeeDirectory` (no implementado) |
| PT-03 | Permisos y no derivación desde Empleados o asignaciones | SPA-SG-01, SPA-INV-04, SPA-CARE-SEG-01, SPA-CARE-SEG-02, SPA-SEC-INV-07 | `SPA-T-04`, `SPA-T-05`, `SPA-T-12`, `SPA-CARE-T-EXP-06`, `SPA-CARE-T-SEG-01`, `SPA-CARE-T-SEG-02`, `SPA-CARE-T-SEG-03`, `SPA-CARE-T-SEG-05`, `SPA-CARE-T-SEG-07`, `SPA-SEC-T-02`, `SPA-OPS-T-AGE-07`, `SPA-OPS-T-AGE-08` | — | Catálogo de permisos no aprobado (§3) |
| PT-04 | Acceso del administrador y alcance por sucursal | SPA-SEC-R-01, SPA-SEC-R-08, SPA-CARE-SEG-04 | `SPA-CARE-T-SEG-09`, `SPA-SEC-T-01`, `SPA-SEC-T-15` | — | **Abierto** (C-01; O-01, O-02) |
| PT-05 | Auditoría y registro de accesos | SPA-AU-01, SPA-AU-02, SPA-SEC-AUD-01, SPA-SEC-INV-01 | `SPA-T-17`, `SPA-CARE-T-SEG-04`, `SPA-SEC-T-03`, `SPA-SEC-T-04`, `SPA-SEC-T-05` | `SPA-CARE-T-FOT-04` | Modo de escritura y retención pendientes (O-03) |
| PT-06 | Aislamiento por tenant y empresa | SPA-SEC-INV-12, SPA-INV-10 | `SPA-T-16`, `SPA-CARE-T-EXP-07`, `SPA-CARE-T-SEG-08`, `SPA-OPS-T-AGE-09`, `SPA-SEC-T-14` | `SPA-CARE-T-FOT-07` | Sin decisión pendiente específica |
| PT-07 | Valoraciones facial y corporal | SPA-FC-04, SPA-FC-05, SPA-CARE-FAC-01 a FAC-07, SPA-CARE-COR-01, COR-03, COR-04 | `SPA-T-06`, `SPA-T-07`, `SPA-CARE-T-FAC-01`, `SPA-CARE-T-COR-01` | `SPA-CARE-T-FAC-03`, `SPA-CARE-T-FAC-04`, `SPA-CARE-T-FAC-05`, `SPA-CARE-T-COR-03` | Contenido [F] sin contraste (V-01); obligatoriedad pendiente (O-21) |
| PT-08 | Mediciones, unidades, precisión y fechas | SPA-FC-06, SPA-CARE-COR-05, SPA-INV-10 | `SPA-T-08` | `SPA-CARE-T-COR-04`, `SPA-CARE-T-COR-05`, `SPA-CARE-T-COR-06` | Requiere revisión arquitectónica del gate decimal (V-04; O-22) |
| PT-09 | Sesiones y atención | SPA-AT-02, SPA-AT-04, SPA-AT-06, SPA-AT-07, SPA-CARE-CNS-05, SPA-CARE-CNS-06 | `SPA-T-09`, `SPA-OPS-T-VTA-02` | `SPA-CARE-T-CNS-05`, `SPA-CARE-T-CNS-06` | Terminología **resuelta por `O-25`** (2026-11-18); políticas de saldo y ausencia abiertas (O-13) |
| PT-10 | Consentimientos y autorizaciones independientes | SPA-CN-01 a CN-06, SPA-INV-05, SPA-CARE-CNS-01 a CNS-08 | `SPA-T-10`, `SPA-T-11`, `SPA-CARE-T-CNS-01`, `SPA-CARE-T-FOT-01`, `SPA-SEC-T-12` | `SPA-CARE-T-CNS-02`, `SPA-CARE-T-CNS-03`, `SPA-CARE-T-CNS-04`, `SPA-CARE-T-CNS-07`, `SPA-CARE-T-FOT-02` | Firma, versionado, menores y uso académico pendientes (O-16 a O-18; V-06) |
| PT-11 | Fotografías y almacenamiento | SPA-FO-01, SPA-CARE-FOT-01 a FOT-11, SPA-SEC-INV-02, SPA-SEC-INV-03, SPA-SEC-INV-05 | `SPA-SEC-T-06`, `SPA-SEC-T-07`, `SPA-SEC-T-08`, `SPA-SEC-T-09` | `SPA-CARE-T-FOT-03`, `SPA-CARE-T-FOT-06` | Cifrado y claves pendientes (O-04) |
| PT-12 | Corrección, retención y eliminación | SPA-FC-08, SPA-INV-06, SPA-SEC-RET-01 a RET-06, SPA-SEC-INV-04 | `SPA-CARE-T-EXP-03`, `SPA-SEC-T-10`, `SPA-SEC-T-11` | `SPA-CARE-T-FAC-02`, `SPA-CARE-T-COR-02`, `SPA-CARE-T-FOT-05`, `SPA-CARE-T-FOT-08` | **Abierto** (C-02; O-05, O-07) |
| PT-13 | Expediente | SPA-CARE-EXP-02, SPA-CARE-EXP-07 | `SPA-CARE-T-EXP-02`, `SPA-CARE-T-EXP-04` | — | Número de ficha y cardinalidad pendientes (O-19, O-20) |
| PT-14 | Agenda, disponibilidad y concurrencia | SPA-AG-01 a AG-03, SPA-OPS-AGE-01 a AGE-14, SPA-AR-06 | `SPA-T-13`, `SPA-T-18`, `SPA-OPS-T-AGE-01`, `SPA-OPS-T-AGE-02`, `SPA-OPS-T-AGE-03`, `SPA-OPS-T-AGE-04`, `SPA-OPS-T-AGE-05`, `SPA-OPS-T-AGE-06` | — | Recursos, tolerancias y política de ausencia pendientes (O-08 a O-10); depende de Empleados |
| PT-15 | Servicios y tratamientos | SPA-SV-01, SPA-SV-02, SPA-OPS-SRV-01 a SRV-12, SPA-IN-01 a IN-03 | `SPA-OPS-T-SRV-01`, `SPA-OPS-T-SRV-02`, `SPA-OPS-T-SRV-03`, `SPA-OPS-T-SRV-04` | — | Relación servicio-procedimiento e insumos pendientes (O-11, O-12) |
| PT-16 | Paquetes y saldos de sesiones | SPA-PQ-01, SPA-OPS-PKG-01 a PKG-07, SPA-AT-05 | `SPA-T-14`, `SPA-OPS-T-PKG-01`, `SPA-OPS-T-PKG-02`, `SPA-OPS-T-PKG-03`, `SPA-OPS-T-PKG-04`, `SPA-OPS-T-PKG-05`, `SPA-OPS-T-PKG-06` | — | Las 12 preguntas contractuales pendientes (O-13) |
| PT-17 | Ventas, cobros y reutilización de módulos oficiales | SPA-AR-05, SPA-VT-01, SPA-INV-08, SPA-OPS-VTA-01 a VTA-06 | `SPA-T-15`, `SPA-OPS-T-SRV-06`, `SPA-OPS-T-VTA-01`, `SPA-OPS-T-VTA-03`, `SPA-OPS-T-VTA-05` | — | Anticipos pendientes (O-14); el origen del contrato fue **aprobado por `O-15`** (2026-11-18); estado SRI requerido, anulaciones y crédito pendientes (O-13, O-14) |

Cada grupo `PT-xx` agrupa pruebas que verifican la misma capacidad desde distintos documentos; en el contrato final deben unificarse en una sola prueba por escenario, y las pruebas de `PT-04`, `PT-09`, `PT-12` y `PT-16` quedan condicionadas a decisiones abiertas.

## 5. Verificaciones técnicas cerradas (por lectura del repositorio)

Solo lectura, sin cambios de código. «Cerrada» significa que el hallazgo es verificable en el repositorio; **no significa que la decisión de negocio esté tomada**.

| V | Verificación | Resultado | Evidencia | Pendiente que se conserva |
|---|---|---|---|---|
| **V-03a** | Trato de líneas `Service` en devoluciones de venta | La reversión de inventario de una devolución **omite** las líneas sin `ItemId` o sin `WarehouseId` (`continue`); las líneas de servicio no llevan bodega, de modo que no generan movimiento de Kardex al devolverse | `AuthorizeSalesReturnUseCases.cs` (paso «Reversión de inventario»); `AuthorizeSalesUseCases.cs` (un servicio no admite bodega) | No se examinó toda la validación del borrador de devolución ni `GetReturnableLinesByInvoice` para confirmar que una línea de servicio sea devolvible [NV parcial] |
| **V-03b** | Evento de autorización de factura consumible | **Existe** `SalesInvoiceAuthorizedEvent` (notificación MediatR). Se publica durante `SaveChanges` (el contexto inyecta `IPublisher`, como en el piloto de auditoría, dentro de la misma transacción) y lo atienden `SalesInvoiceAuthorizedPostingTranslator`, `SalesInvoiceCogsPostingTranslator` y `SalesInvoiceAuthorizedHandler` (Caja). **Su contenido no incluye cliente ni líneas** (`InvoiceId`, número, totales, usuario, caja, empresa, fecha, importes cobrados) | `SalesInvoiceAuthorizedEvent.cs`; `SalesInvoiceAuthorizedHandlerOrderTests.cs` (exige que el handler de Caja sea el primero) | Que Spa consuma el evento acoplaría su resultado a la autorización de la factura y obligaría a releer la factura; el origen del contrato sigue siendo la decisión **O-15** |
| **V-03c** | Vendedor en el dominio de ventas | **No existe** un campo de vendedor (búsqueda de `SellerId`, `SalesPersonId`, `VendedorId` y `SoldBy` sin resultados en `ERP.Domain/Modules/Sales`) | Búsqueda negativa | La separación vendedor / cobrador / profesional sigue siendo un contrato nuevo (O-14, `SPA-VT-01`) |
| **V-04** | Escala de las medidas corporales | El estándar de precisión **no tiene una clase** para medidas de peso o de contorno: la tabla de escalas técnicas cubre montos, precios, cantidades operativas, porcentajes y escalas fijas de contrato; una nueva columna decimal exige justificar tipo, campo de política, escala y motivo, y **si no coincide con una fila, «requiere revisión arquitectónica formal»** | `data-standards.md`, «PostgreSQL — escalas técnicas» y «Gate para nuevas columnas decimales» | La clasificación de las medidas necesita revisión arquitectónica y decisión (O-22); no se propone una escala |
| **V-05a** | Horarios recurrentes del establecimiento | **No hay precedente** de horarios recurrentes (búsqueda de `OpeningHours`, `BusinessHours`, `WorkingHours`, `HorarioAtencion` y `WeeklySchedule` sin resultados en dominio y aplicación) | Búsqueda negativa | El diseño de horarios sería nuevo y debe verificarse al implementarse |
| **V-05b** | Restricción de exclusión para citas | El patrón `EXCLUDE USING gist` existe para **fechas** (`daterange` en `accounting_periods`, con `btree_gist`); **no hay uso de `tstzrange`** en código ni migraciones | Baseline `InitialProductionBaseline`; búsqueda negativa de `tstzrange` | Una exclusión sobre instantes sería una variante nueva de una técnica existente |
| **V-07a** | Publicación estática del directorio de archivos | **No se encontró** `UseStaticFiles`, `StaticFileOptions` ni `MapStaticAssets` en el código del backend, y `frontend/nginx.conf` no contiene referencias a rutas de archivos (sin coincidencias de `/files` ni `data/files`) | Búsqueda negativa; `frontend/nginx.conf` | La configuración del proxy de producción está fuera del repositorio [NV]; el cifrado de disco y la custodia de claves son externos |
| **V-08** | Resolutor de `StorageObjectId` | `StorageObjectId` existe en `ItemImage` y sus DTO y como columna `storage_object_id`, pero **no hay entidad, tabla ni resolutor de «objeto de almacenamiento»** en el repositorio. Los usos reales de `IFileStorage` guardan rutas opacas (XML de documentos electrónicos y adjuntos de comunicaciones) | `ItemImage.cs`; baseline (solo la columna); usos de `IFileStorage` en ElectronicDocuments y Communications | Un registro de archivos privados para Spa sería nuevo; no existe precedente de referencia por identificador |

### 5.1 Verificaciones que no se cierran

| V | Motivo |
|---|---|
| V-01 | Depende de las fichas originales, que Claude no ha inspeccionado |
| V-02 | Depende de la implementación de Empleados (`IEmployeeDirectory` es propuesta) |
| V-06 | Depende de asesoría jurídica |
| V-09 | Depende de la consulta del Registro Oficial |
| V-03a (parte), V-07a (parte), V-04 (decisión) | Ver la columna «Pendiente» de §5 |

## 6. Pendientes externos

### 6.1 Por tipo de dependencia

| Tipo | Elementos |
|---|---|
| Formularios originales | V-01; `O-21`, `O-24`; contraste de todo contenido [F] |
| Legislación y asesoría legal | V-06, V-09; `O-07`, `O-16`, `O-18`; L-INT-01 a L-INT-07 de R4 |
| Decisiones del propietario | `O-01` a `O-25` (§6.2) |
| Implementación futura | V-02 (Empleados), cifrado y claves, registro de accesos, eliminación legal, contrato de agenda y paquetes |
| Revisión arquitectónica | Permisos sin bypass del Admin (si se elige, C-01); clase de precisión de las medidas (V-04) |

### 6.2 Clasificación de las 25 cuestiones `O-xx`

Las `O-xx` son **cuestiones pendientes**, no decisiones históricas `SPA-DEC`.

| O | Cuestión | Propietario | Evidencia externa | Legal | Implementación futura | Revisión arquitectónica |
|---|---|---|---|---|---|---|
| O-01 | Acceso del Admin a datos clínicos | Sí | — | — | — | Si se excluye al Admin |
| O-02 | Acceso entre sucursales | Sí | — | — | — | — |
| O-03 | Registro de lecturas y retención | Sí | — | Sí (plazos) | Sí | — |
| O-04 | Cifrado, claves y respaldos | Sí | — | — | Sí | Sí (despliegue) |
| O-05 | Excepción a la regla de no borrado y eliminación legal | Sí | — | Sí | Sí | — |
| O-06 | Fecha de nacimiento y contacto de emergencia | Sí | — | Sí (datos de terceros) | — | Si se tocara MasterData |
| O-07 | Asesoría legal L-INT-01 a L-INT-07 | — | — | Sí | — | — |
| O-08 | Recursos bloqueantes de la agenda | Sí | — | — | — | — |
| O-09 | Contigüidad, tolerancias, anticipación y confirmaciones | Sí | — | — | — | — |
| O-10 | Atención sin cita previa | Sí | — | — | — | — |
| O-11 | Servicio y procedimiento (1:1 o N:M) | Sí | — | — | — | — |
| O-12 | Tratamiento de insumos | Sí | — | — | Sí (inventario) | — |
| O-13 | Las 12 preguntas de paquetes | Sí | — | Posible (consumidores) | — | — |
| O-14 | Anticipos o prepagos de clientes | Sí | — | — | Sí (otro módulo) | Sí (módulo de cobros) |
| O-15 | **APROBADA el 2026-11-18 (alternativa A).** Origen del contrato de paquete | Aprobada | — | — | Sí | No (la creación por evento no se aprobó) |
| O-16 | Firma, versionado y conservación de consentimientos | Sí | — | Sí | — | — |
| O-17 | Uso académico | Sí | Sí (ficha) | — | — | — |
| O-18 | Consentimiento de menores y representantes | Sí | — | Sí | — | — |
| O-19 | Número de ficha y código del caso | Sí | Sí (fichas) | — | — | — |
| O-20 | Cardinalidad del expediente | Sí | — | — | — | — |
| O-21 | Obligatoriedad de los campos de valoración | Sí | Sí (fichas) | — | — | — |
| O-22 | Escalas de mediciones, frecuencia y controles | Sí | Sí (fichas) | — | — | Sí (V-04) |
| O-23 | Texto íntegro de SPA-DEC-116 y registro histórico | Sí | Sí (historial) | — | — | — |
| O-24 | Versión vigente de cada formulario | Sí | Sí | — | — | — |
| O-25 | **APROBADA el 2026-11-18 (alternativa A).** Terminología de sesión, cita y reserva | Aprobada | — | — | — | — |

## 7. Conflictos que siguen abiertos

| C | Estado tras esta normalización |
|---|---|
| **C-01** Administrador y permisos clínicos | **ABIERTO.** Este documento no resuelve el bypass ni la política de acceso clínico; el catálogo de §3 no excluye al Admin. Las alternativas de R4 §3.3 siguen sin aprobarse |
| **C-02** Conservación y eliminación | **ABIERTO.** `DUP-07` queda sin redacción canónica; ninguna alternativa de eliminación está aprobada |
| **C-06** Terminología de citas y sesiones | **TERMINOLOGÍA RESUELTA** por `O-25` (alternativa A, 2026-11-18; [registro de decisiones aprobadas](GALI-SPA-V1.1-APPROVED-DECISIONS-01.md)). **Las políticas comerciales siguen abiertas** (ausencias que consumen saldo, efecto de cancelar, atención sin cita, varios servicios por cita: O-10, O-11, O-13) |
| C-03, C-04, C-05, C-07, C-10 | Sin cambio (decisiones pendientes o compatibles) |
| C-08 | Sin cambio (brecha de capacidad; `DUP-10` bloqueada) |
| C-09 | **Cerrado en cuanto a nombres** (§3.1), como propuesta no aprobada; las políticas dependen de C-01 |

## 8. Propuesta de entrada para el índice documental

`docs/decisions/README.md` **no se modifica en esta fase**. Se propone añadir, **en la sección «Diseños con consumidores»** (la misma que ya lista los diseños sin ADR, como `PEDIDOS-VENTA-V1-FUNCTIONAL-SPEC-01.md`), un bloque que identifique con claridad
qué es cada documento. **Ninguno es un ADR ni está aprobado.**

```markdown
- [GALI-SPA-V1.1-RECOVERY-01.md](GALI-SPA-V1.1-RECOVERY-01.md) (Gali Spa V1.1: registro de recuperación provisional; no es ADR ni contrato implementable).
- [GALI-SPA-V1.1-OPERATIONS-SPEC-DRAFT-01.md](GALI-SPA-V1.1-OPERATIONS-SPEC-DRAFT-01.md) (borrador técnico: agenda, servicios, paquetes y ventas; propuestas no aprobadas).
- [GALI-SPA-V1.1-CARE-PRIVACY-SPEC-DRAFT-01.md](GALI-SPA-V1.1-CARE-PRIVACY-SPEC-DRAFT-01.md) (borrador técnico: expedientes, consentimientos, fotografías y privacidad; propuestas no aprobadas).
- [GALI-SPA-V1.1-SECURITY-COMPLIANCE-DRAFT-01.md](GALI-SPA-V1.1-SECURITY-COMPLIANCE-DRAFT-01.md) (borrador técnico: seguridad y cumplimiento; no es asesoría jurídica).
- [GALI-SPA-V1.1-CONTRACT-CONSOLIDATION-MATRIX-01.md](GALI-SPA-V1.1-CONTRACT-CONSOLIDATION-MATRIX-01.md) (matriz de recuperación y trazabilidad de los cuatro documentos anteriores; no es ADR).
- [GALI-SPA-V1.1-CONTRACT-NORMALIZATION-01.md](GALI-SPA-V1.1-CONTRACT-NORMALIZATION-01.md) (normalización de vocabulario, permisos y pruebas; propuestas no aprobadas).
```

Observaciones: los enlaces son relativos al directorio `docs/decisions/`; el índice no debe presentarlos en la tabla de ADR; la actualización requiere autorización documental expresa.

## 9. Condiciones necesarias para redactar la especificación final

La especificación contractual final (capítulos `CE-01` a `CE-15` de la matriz) **no puede redactarse todavía** como contrato aprobado. Condiciones:

### 9.1 Bloqueantes

1. **Decisiones del propietario** que condicionan la estructura: `O-01` (administrador), `O-05` (eliminación frente a la regla de no borrado), y `O-14` (anticipos). **`O-25` y `O-15` fueron aprobadas el 2026-11-18 y dejan de ser bloqueantes.**
2. **Recuperación o confirmación del historial:** texto de `SPA-DEC-116` (`O-23`) y contraste de las fichas originales (V-01; `O-24`).
3. **Empleados:** el contrato `IEmployeeDirectory` debe existir y verificarse (V-02; EMP-E6b).
4. **Validación jurídica** de las interpretaciones de R4 (V-06; `O-07`).
5. **Revisión arquitectónica** de la clase de precisión de las medidas (V-04) y, solo si se elige, del bypass del Admin (C-01).

### 9.2 Necesarias pero no bloqueantes

- Aprobar el glosario (§2) y el catálogo de permisos (§3), o sustituirlos.
- Resolver las políticas de agenda y paquetes (`O-08` a `O-13`).
- Definir firma, versionado, menores y uso académico (`O-16` a `O-18`).
- Aplicar el índice del §8.

### 9.3 Ya disponibles para la redacción

Las redacciones canónicas de 14 grupos (§1.1), el glosario (§2), el catálogo de nombres (§3), la relación única de pruebas (§4) y las verificaciones cerradas (§5).

## 10. Límites

Este documento no modifica los cinco documentos de Spa, ADR-050, la especificación de Empleados, `STATUS.md`, `WORK_QUEUE.md` ni el índice. No autoriza implementación de Spa ni de Empleados, migraciones, cambios en módulos FROZEN ni cambios en Identity, Access, Kernel o almacenamiento. De `SPA-DEC-116` solo está confirmado el título.

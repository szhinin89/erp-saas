# Gali Spa V1.1 — Matriz maestra de consolidación de contratos

**MATRIZ DOCUMENTAL — NO ADR — NO CONTRATO IMPLEMENTABLE — NO IMPLEMENTACIÓN AUTORIZADA.**

Documento de consolidación (SPA-V1.1-DOC-05). Reúne en una matriz única los requisitos de los cuatro documentos de Spa existentes, **sin duplicar ni modificar su
contenido** y **sin renumerar ningún identificador**. Estado de entrega: [STATUS](../../STATUS.md); cola: [WORK_QUEUE](../agent/WORK_QUEUE.md).

| Código | Documento fuente |
|---|---|
| **R1** | [GALI-SPA-V1.1-RECOVERY-01](GALI-SPA-V1.1-RECOVERY-01.md) |
| **R2** | [GALI-SPA-V1.1-OPERATIONS-SPEC-DRAFT-01](GALI-SPA-V1.1-OPERATIONS-SPEC-DRAFT-01.md) |
| **R3** | [GALI-SPA-V1.1-CARE-PRIVACY-SPEC-DRAFT-01](GALI-SPA-V1.1-CARE-PRIVACY-SPEC-DRAFT-01.md) |
| **R4** | [GALI-SPA-V1.1-SECURITY-COMPLIANCE-DRAFT-01](GALI-SPA-V1.1-SECURITY-COMPLIANCE-DRAFT-01.md) |

También se contrastó con [ADR-050](ADR-050-employees-v1-architecture.md), la [especificación de Empleados](EMPLOYEES-V1-CONTRACT-SPEC-01.md) y las reglas oficiales
del ERP citadas por las fuentes. **Esta matriz no aprueba ninguna decisión, no crea reglas nuevas y no es una especificación contractual.**

## 0. Cómo leer este documento

### 0.1 Naturaleza de los identificadores

- Los identificadores `SPA-*` de las filas **son referencias** a los definidos en R1 a R4; este documento no los redefine ni los renumera. Si una descripción difiere de la fuente, **prevalece la fuente**.
- Los códigos `C-xx` (conflictos), `D-xx` (dependencias), `V-xx` (verificaciones), `O-xx` (decisiones del propietario), `DUP-xx` (duplicaciones) y `CE-xx` (capítulos
  de la futura especificación contractual) se definen **solo aquí**, son de este documento y **no colisionan** con ningún identificador existente.
- `R-xx` en la columna «Riesgo» abrevia `SPA-SEC-R-xx` (R4 §2).

### 0.2 Cómo se construyó la matriz

Los identificadores, su descripción, su sección y sus **etiquetas de evidencia** se extrajeron mecánicamente de los cuatro documentos (cada fila conserva la etiqueta que le dio la fuente; en los encabezados de R1 se unen las
etiquetas de la sección). La **aprobación**, el área, las **dependencias**, las **verificaciones pendientes** y la referencia a riesgos se asignaron por reglas explícitas
(según las etiquetas, el área y palabras clave) y son **orientativas**: la fuente es la que manda. Los **conflictos** y las **duplicaciones** se identificaron por lectura de los documentos.

### 0.3 Leyenda de evidencia

| Etiqueta | Significado |
|---|---|
| A | Aprobada literal (cita ADR-050 o la especificación de Empleados) |
| A-der | Aprobada derivada (consecuencia de una decisión aprobada que su texto no formula tal cual) |
| E | Existente en el ERP; no es una decisión de Spa |
| F | Respaldada por formularios originales de Gali Spa, **no inspeccionados por Claude** |
| R | Reconstruida de la planificación, sin acta íntegra |
| T | Propuesta técnica no aprobada |
| P | Pendiente de decisión o contrato no recuperado |
| NV | No verificado |
| L-rec, L-NV | Recomendación legal-técnica; punto legal no verificado |

### 0.4 Leyenda de aprobación

«Aprobada literal», «Aprobada derivada» y «Parcialmente aprobada» son las **únicas** categorías con respaldo en una decisión aprobada verificable (ADR-050 o la especificación de Empleados); «Parcialmente
aprobada» significa que solo un núcleo lo está y el resto de la fila no. Cualquier otra categoría **no es una aprobación**. La única decisión histórica de Spa confirmada es el **título** de SPA-DEC-116.

**Alcance de la clasificación:** las **18 filas** con respaldo aprobado (marcadas «(rev.)» en la columna «Aprobación» de §3) fueron **revisadas manualmente** una por una contra el texto de ADR-050 o de la especificación de Empleados (§3.1).
**Todas las demás clasificaciones, así como las columnas de dependencias, verificaciones y riesgos, se generaron con reglas automáticas y son orientativas**; la fuente prevalece.

## 1. Resumen

| Concepto | Cantidad |
|---|---|
| Registros consolidados (identificadores definidos en R1 a R4) | **295** |
| — Reglas | 160 (R1 51, R2 39, R3 50, R4 20) |
| — Invariantes candidatos | 22 (R1 10, R4 12) |
| — Pruebas candidatas | 102 (R1 18, R2 27, R3 42, R4 15) |
| — Riesgos registrados | 10 (R4) |
| — Decisiones históricas | 1 (`SPA-DEC-116`, en R1) |
| — de ellos con identificador **literal** en su fuente | 273 |
| — de ellos con identificador **inferido** (no literal) | 22 (pruebas de R3; ver §4.2) |
| **Referencias externas (no son registros)** | 55 códigos `GS-*` del documento maestro y 21 códigos de la V01, literales en la tabla de equivalencias de R1 §17 |
| **Cuestiones pendientes** | 25 decisiones del propietario `O-xx` (§11), 9 verificaciones `V-xx` (§8) y 67 reglas o invariantes con puntos pendientes (§10.2) |
| **Decisiones aprobadas verificables** | **18** filas con respaldo en ADR-050 o Empleados (4 literales, 4 derivadas, 10 parciales) más el **título** de `SPA-DEC-116`. **El total de registros (295) no es un total de decisiones aprobadas.** |
| Colisiones de identificadores entre los cuatro documentos | **0** |
| Conflictos identificados (§6) | 10: contradicciones reales o parciales 2 (C-01, C-02); inconsistencia terminológica 1 (C-06); sin contradicción (compatibles, decisión pendiente, brecha o solapamiento) 7 |

Las reglas están en §3, las pruebas en §4, los riesgos en §4.1 y los conflictos en §6.

### 1.1 Distribución por estado de aprobación (reglas e invariantes: 182)

| Estado de aprobación | Registros |
|---|---|
| Propuesta técnica (no aprobada) | 82 |
| Propuesta con puntos pendientes (no aprobada) | 28 |
| Respaldada por formulario (no aprobada como regla digital) | 25 |
| Pendiente de decisión | 22 |
| Parcialmente aprobada (núcleo aprobado; resto no) | 10 |
| Reconstruida (no aprobada) | 6 |
| Aprobada literal (ADR-050) | 4 |
| Aprobada derivada | 4 |
| Existente en el ERP (no es decisión de Spa) | 1 |

Pruebas candidatas: 102, todas **no aprobadas**. Riesgos: 10, registrados, no son decisiones. Decisión histórica: 1 (solo el título de SPA-DEC-116).

### 1.2 Registros por área (reglas, invariantes y decisión histórica)

| Área | Registros |
|---|---|
| General y alcance | 4 |
| Clientes y BusinessPartner | 6 |
| Profesionales y Empleados | 15 |
| Expedientes | 11 |
| Valoraciones | 18 |
| Consentimientos | 16 |
| Fotografías | 16 |
| Agenda | 12 |
| Servicios | 8 |
| Tratamientos | 4 |
| Planes y paquetes | 6 |
| Sesiones | 9 |
| Ventas | 8 |
| Cobros | 2 |
| Inventario | 3 |
| Seguridad | 30 |
| Auditoría | 13 |
| Frontend | 1 |
| API | 1 |

## 2. Capítulos de la futura especificación contractual (`CE-xx`)

Ubicación futura propuesta (no aprobada) para cada área. Es solo un índice de destino de esta matriz.

| CE | Capítulo | Áreas incluidas |
|---|---|---|
| CE-01 | Alcance y arquitectura | General y alcance |
| CE-02 | Clientes y BusinessPartner | Clientes y BusinessPartner |
| CE-03 | Profesionales y Empleados | Profesionales y Empleados |
| CE-04 | Expedientes | Expedientes |
| CE-05 | Valoraciones | Valoraciones |
| CE-06 | Consentimientos | Consentimientos |
| CE-07 | Fotografías | Fotografías |
| CE-08 | Agenda | Agenda |
| CE-09 | Servicios y tratamientos | Servicios, Tratamientos |
| CE-10 | Planes, paquetes y sesiones | Planes y paquetes, Sesiones |
| CE-11 | Ventas, cobros e inventario | Ventas, Cobros, Inventario |
| CE-12 | Seguridad, privacidad y auditoría | Seguridad, Auditoría |
| CE-13 | API | API |
| CE-14 | Frontend | Frontend |
| CE-15 | Pruebas y criterios de aceptación | Pruebas |

## 3. Matriz de reglas, invariantes y decisión histórica

Columnas: **Fuente** (documento y sección), **Evid.** (etiquetas de la fuente), **Aprobación** (§0.4), **Dep.** (§7), **Conf.** (§6), **Riesgo** (R4 §2), **Verif.** (§8), **Futuro** (§2).
La descripción está abreviada; el texto completo está en la fuente. «(rev.)» marca las 18 filas revisadas manualmente (§3.1); el resto de la columna «Aprobación» es **orientativa**.

### General y alcance (4)

| ID | Fuente | Descripción | Evid. | Aprobación | Dep. | Conf. | Riesgo | Verif. | Futuro |
|---|---|---|---|---|---|---|---|---|---|
| SPA-AL-01 | R1 §2 | Alcance funcional general | R+P | Reconstruida (no aprobada) | D-12 | — | — | — | CE-01 |
| SPA-AL-02 | R1 §2 | Reutilización del ERP | A-der+R+T | Parcialmente aprobada (núcleo aprobado; resto no) (rev.) | — | — | — | — | CE-01 |
| SPA-DEC-116 | R1 §21 | Revisión y consolidación del alcance de Gali Spa V1.1 | — | Aprobada por el propietario (solo título); texto no recuperado | — | — | — | — | CE-01 |
| SPA-INV-10 | R1 §10 | Aplicar estándares oficiales de fechas, decimales, aislamiento y auditoría | A-der | Aprobada derivada (rev.) | D-08, D-10 | — | — | V-04 | CE-01 |

### Clientes y BusinessPartner (6)

| ID | Fuente | Descripción | Evid. | Aprobación | Dep. | Conf. | Riesgo | Verif. | Futuro |
|---|---|---|---|---|---|---|---|---|---|
| SPA-AR-01 | R1 §2 | Identidad del cliente | R+P | Reconstruida (no aprobada) | D-02, D-12 | — | R-07 | — | CE-02 |
| SPA-CARE-EXP-01 | R3 §2.3 | Un expediente referencia a un BusinessPartner activo con rol Customer de la empresa; Spa lee su nombre,… | T | Propuesta técnica (no aprobada) | D-02 | C-10 | R-07 | — | CE-02 |
| SPA-CL-01 | R1 §3 | Historial del cliente | F+T+P | Respaldada por formulario (no aprobada como regla digital) | D-02, D-12 | — | R-07 | V-01 | CE-02 |
| SPA-FC-07 | R1 §3 | Origen de los datos generales de la persona | T+P | Propuesta con puntos pendientes (no aprobada) | D-12 | C-10 | R-07 | — | CE-02 |
| SPA-INV-01 | R1 §10 | No duplicar identidades de clientes | R | Reconstruida (no aprobada) | D-02 | — | R-07 | — | CE-02 |
| SPA-SEC-INV-11 | R4 §11 | Los datos personales que no están en BusinessPartner viven en Spa y nunca duplican la identidad de BP | T | Propuesta técnica (no aprobada) | D-02, D-11 | — | R-07 | V-06 | CE-02 |

### Profesionales y Empleados (15)

| ID | Fuente | Descripción | Evid. | Aprobación | Dep. | Conf. | Riesgo | Verif. | Futuro |
|---|---|---|---|---|---|---|---|---|---|
| SPA-AR-02 | R1 §2 | Profesionales y Empleados | A+A-der+R+T | Parcialmente aprobada (núcleo aprobado; resto no) (rev.) | D-01 | — | R-09 | V-02 | CE-03 |
| SPA-AR-03 | R1 §2 | Profesional responsable y usuario registrador | A+T+P | Parcialmente aprobada (núcleo aprobado; resto no) (rev.) | D-01, D-12 | — | R-09 | V-02 | CE-03 |
| SPA-AR-04 | R1 §2 | Responsabilidades separadas | A+A-der+R | Parcialmente aprobada (núcleo aprobado; resto no) (rev.) | — | — | R-09 | — | CE-03 |
| SPA-AR-06 | R1 §2 | Elegibilidad histórica del profesional | T+P | Propuesta con puntos pendientes (no aprobada) | D-01, D-12 | C-07 | R-09 | V-02 | CE-03 |
| SPA-CARE-EXP-05 | R3 §2.3 | Cada valoración y cada sesión referencian al profesional responsable (`EmployeeId`), al usuario que… | A+T | Parcialmente aprobada (núcleo aprobado; resto no) (rev.) | D-01, D-13 | — | R-09 | V-02 | CE-03 |
| SPA-CARE-SEG-06 | R3 §7.1 | Profesional responsable y usuario registrador se guardan por separado y se auditan por separado | A | Parcialmente aprobada (núcleo aprobado; resto no) (rev.) | D-01 | — | R-09 | V-02 | CE-03 |
| SPA-INV-02 | R1 §10 | No duplicar Employee | A | Aprobada literal (ADR-050) (rev.) | D-01 | — | R-09 | — | CE-03 |
| SPA-INV-03 | R1 §10 | No confundir usuario registrador con profesional responsable | A | Aprobada literal (ADR-050) (rev.) | D-01 | — | R-09 | V-02 | CE-03 |
| SPA-OPS-AGE-04 | R2 §2.4 | Un profesional está disponible en un instante si: (a) está dentro del horario del establecimiento de la… | T+P | Propuesta con puntos pendientes (no aprobada) | D-01, D-10, D-13, D-12 | C-07 | R-09 | V-02, V-05 | CE-03 |
| SPA-OPS-AGE-13 | R2 §2.4 | Cambiar el profesional de una cita debe revalidar la disponibilidad (AGE-04) y registrar el cambio | T | Propuesta técnica (no aprobada) | D-01 | — | R-09 | V-02, V-05 | CE-03 |
| SPA-OPS-AGE-14 | R2 §2.4 | Si un profesional deja de estar disponible (baja o cambio de asignación en Empleados), las citas futuras… | T+P | Propuesta con puntos pendientes (no aprobada) | D-01, D-12 | C-07 | R-09 | V-02, V-05 | CE-03 |
| SPA-OPS-SRV-06 | R2 §3.3 | Profesionales elegibles: se determinan por especialidad con el catálogo de Empleados (`specialtyIds` de… | T+P | Propuesta con puntos pendientes (no aprobada) | D-01, D-12 | — | R-09 | V-02 | CE-03 |
| SPA-OPS-VTA-05 | R2 §5.3 | El profesional que presta el servicio y el usuario que registra u opera la transacción se identifican… | A-der+P | Parcialmente aprobada (núcleo aprobado; resto no) (rev.) | D-01, D-12 | — | R-09 | V-02 | CE-03 |
| SPA-SEC-INV-08 | R4 §11 | El profesional y el usuario registrador se guardan y se auditan por separado | A | Parcialmente aprobada (núcleo aprobado; resto no) (rev.) | D-01 | — | R-09 | V-02 | CE-03 |
| SPA-SG-02 | R1 §9 | Identidad del responsable | A | Aprobada literal (ADR-050) (rev.) | D-02 | — | R-09 | — | CE-03 |

### Expedientes (11)

| ID | Fuente | Descripción | Evid. | Aprobación | Dep. | Conf. | Riesgo | Verif. | Futuro |
|---|---|---|---|---|---|---|---|---|---|
| SPA-CARE-EXP-02 | R3 §2.3 | Número de ficha: único por empresa. Alternativa A: secuencia propia mediante la infraestructura de… | P | Pendiente de decisión | D-12 | — | R-01, R-08 | — | CE-04 |
| SPA-CARE-EXP-03 | R3 §2.3 | Código del caso: aparece en la ficha compacta; su significado y si identifica un caso por tratamiento,… | P | Pendiente de decisión | D-12 | — | R-01, R-08 | — | CE-04 |
| SPA-CARE-EXP-04 | R3 §2.3 | Un cliente puede tener uno o varios expedientes: alternativa A, un expediente por cliente y empresa;… | P | Pendiente de decisión | D-02, D-13, D-12 | — | R-01, R-08 | — | CE-04 |
| SPA-CARE-EXP-06 | R3 §2.3 | Los antecedentes se guardan como versiones append-only: corregir crea una versión nueva con autor, fecha… | T | Propuesta técnica (no aprobada) | D-10, D-11 | — | R-01, R-08 | V-06 | CE-04 |
| SPA-CARE-EXP-07 | R3 §2.3 | En cada categoría de antecedentes se distingue «sin responder», «no», «sí» y, cuando corresponda, texto… | T | Propuesta técnica (no aprobada) | — | — | R-01, R-08 | — | CE-04 |
| SPA-CARE-EXP-08 | R3 §2.3 | Un expediente nunca se elimina; puede inactivarse. Quién lo cierra y qué impide un cierre son pendientes | T+P | Propuesta con puntos pendientes (no aprobada) | D-12 | C-02 | R-04, R-05 | V-06 | CE-04 |
| SPA-CARE-EXP-09 | R3 §2.3 | Una atención sobre un cliente cuyo BusinessPartner se desactiva conserva el expediente; no se crean… | T | Propuesta técnica (no aprobada) | D-02, D-11 | — | R-01, R-08 | V-06 | CE-04 |
| SPA-FC-01 | R1 §3 | Fichas facial y corporal | F | Respaldada por formulario (no aprobada como regla digital) | — | — | R-01, R-08 | V-01 | CE-04 |
| SPA-FC-02 | R1 §3 | Identificación del expediente | F+P | Respaldada por formulario (no aprobada como regla digital) | D-12 | C-10 | R-01, R-08 | V-01 | CE-04 |
| SPA-FC-03 | R1 §3 | Antecedentes relevantes | F+P | Respaldada por formulario (no aprobada como regla digital) | D-12 | — | R-01, R-08 | V-01 | CE-04 |
| SPA-FC-08 | R1 §3 | Correcciones e historial del expediente | T+P | Propuesta con puntos pendientes (no aprobada) | D-12 | — | R-01, R-08 | — | CE-04 |

### Valoraciones (18)

| ID | Fuente | Descripción | Evid. | Aprobación | Dep. | Conf. | Riesgo | Verif. | Futuro |
|---|---|---|---|---|---|---|---|---|---|
| SPA-CARE-COR-01 | R3 §4.4 | La valoración corporal pertenece a un expediente y fija fecha de negocio, profesional responsable,… | T | Propuesta técnica (no aprobada) | D-01, D-10, D-13 | — | R-06 | V-02 | CE-05 |
| SPA-CARE-COR-02 | R3 §4.4 | Una medición es un conjunto (`BodyMeasurementSet`) con fecha de negocio (`DateOnly`) y tipo `Inicial`,… | T | Propuesta técnica (no aprobada) | D-10 | — | R-06 | V-04 | CE-05 |
| SPA-CARE-COR-03 | R3 §4.4 | Un conjunto de mediciones no se sobrescribe: corregirlo crea un conjunto o una versión nueva con motivo,… | T | Propuesta técnica (no aprobada) | D-10 | — | R-06 | V-04 | CE-05 |
| SPA-CARE-COR-04 | R3 §4.4 | Las diferencias entre conjuntos (inicial frente a control o final) son derivadas en lectura y no se… | T | Propuesta técnica (no aprobada) | D-09 | — | R-06 | V-07 | CE-05 |
| SPA-CARE-COR-05 | R3 §4.4 | Los valores numéricos y las fechas respetan los estándares oficiales de decimales y fechas; no se fijan… | T+NV | Propuesta técnica (no aprobada) | D-10, D-12 | — | R-06 | V-04 | CE-05 |
| SPA-CARE-COR-06 | R3 §4.4 | Frecuencia de medición y asociación obligatoria de un conjunto con una sesión: no recuperadas | P | Pendiente de decisión | D-10, D-12 | — | R-06 | V-04 | CE-05 |
| SPA-CARE-COR-07 | R3 §4.4 | Cuántos controles son posibles y si el «final» es único: no recuperado | P | Pendiente de decisión | D-12 | — | R-06 | — | CE-05 |
| SPA-CARE-COR-08 | R3 §4.4 | La decisión (proceder, adaptar o posponer) es de un solo valor y no ejecuta acciones automáticas | T | Propuesta técnica (no aprobada) | — | — | R-06 | — | CE-05 |
| SPA-CARE-FAC-01 | R3 §3.3 | La valoración facial pertenece a un expediente, fija fecha de negocio (`DateOnly`), profesional… | T | Propuesta técnica (no aprobada) | D-01, D-10, D-13 | — | R-06 | V-02 | CE-05 |
| SPA-CARE-FAC-02 | R3 §3.3 | Una valoración es un registro con versiones append-only: una corrección genera una versión nueva con… | T | Propuesta técnica (no aprobada) | D-10 | — | R-06 | — | CE-05 |
| SPA-CARE-FAC-03 | R3 §3.3 | Una valoración estética no se rotula ni exporta como diagnóstico médico; el campo «diagnóstico médico… | F | Respaldada por formulario (no aprobada como regla digital) | — | — | R-06 | V-01 | CE-05 |
| SPA-CARE-FAC-04 | R3 §3.3 | Fototipo: un único valor entre I y VI o «sin evaluar». La clasificación es la recuperada de la ficha | F+T | Respaldada por formulario (no aprobada como regla digital) | — | — | R-06 | V-01 | CE-05 |
| SPA-CARE-FAC-05 | R3 §3.3 | La decisión (proceder, adaptar o posponer) es de un solo valor y no ejecuta ninguna acción automática… | T | Propuesta técnica (no aprobada) | — | — | R-06 | V-05 | CE-05 |
| SPA-CARE-FAC-06 | R3 §3.3 | Los mínimos de integridad de una valoración son solo los de la regla de lectura 2; los campos clínicos… | T+P | Propuesta con puntos pendientes (no aprobada) | D-12 | — | R-06 | — | CE-05 |
| SPA-CARE-FAC-07 | R3 §3.3 | Si el formulario antiguo y el actualizado difieren, la versión digital no asume equivalencia; el… | P | Pendiente de decisión | D-12 | — | R-06 | V-03 | CE-05 |
| SPA-FC-04 | R1 §3 | Valoración facial | F | Respaldada por formulario (no aprobada como regla digital) | — | — | R-06 | V-01 | CE-05 |
| SPA-FC-05 | R1 §3 | Valoración corporal | F | Respaldada por formulario (no aprobada como regla digital) | — | — | R-06 | V-01 | CE-05 |
| SPA-FC-06 | R1 §3 | Mediciones corporales | F+P | Respaldada por formulario (no aprobada como regla digital) | D-10, D-12 | — | R-06 | V-01, V-04 | CE-05 |

### Consentimientos (16)

| ID | Fuente | Descripción | Evid. | Aprobación | Dep. | Conf. | Riesgo | Verif. | Futuro |
|---|---|---|---|---|---|---|---|---|---|
| SPA-CARE-CNS-01 | R3 §5.4 | Cada tipo de autorización es un registro separado; aceptar uno no implica aceptar otro | F+T | Respaldada por formulario (no aprobada como regla digital) | — | C-04 | R-06 | V-01 | CE-06 |
| SPA-CARE-CNS-02 | R3 §5.4 | El texto de un consentimiento está versionado; un registro referencia la versión presentada y esa… | T | Propuesta técnica (no aprobada) | D-11 | — | R-06 | V-06 | CE-06 |
| SPA-CARE-CNS-03 | R3 §5.4 | Un registro de consentimiento no se edita ni se elimina; un cambio de decisión es un registro nuevo o un… | T | Propuesta técnica (no aprobada) | D-10, D-11 | C-02 | R-06 | V-06 | CE-06 |
| SPA-CARE-CNS-04 | R3 §5.4 | Retiro: una autorización retirada deja de habilitar usos futuros; no revierte las atenciones ya… | T+P | Propuesta con puntos pendientes (no aprobada) | D-11, D-12 | — | R-06 | V-06 | CE-06 |
| SPA-CARE-CNS-05 | R3 §5.4 | Interrupción: una sesión interrumpida se registra con instante y motivo y no se marca como realizada por… | T+P | Propuesta con puntos pendientes (no aprobada) | D-05, D-10, D-12 | C-06 | R-06 | V-03 | CE-06 |
| SPA-CARE-CNS-06 | R3 §5.4 | Una sesión que requiere un consentimiento de procedimiento vigente no puede registrarse como realizada… | T+P | Propuesta con puntos pendientes (no aprobada) | D-11, D-12 | — | R-06 | V-06 | CE-06 |
| SPA-CARE-CNS-07 | R3 §5.4 | Uso académico: se documenta como autorización separada solo si el propietario confirma que pertenece al… | P | Pendiente de decisión | D-11, D-12 | — | R-06 | V-06 | CE-06 |
| SPA-CARE-CNS-08 | R3 §5.4 | Quién consiente: la persona atendida; un representante (menores u otros casos) no está recuperado | P | Pendiente de decisión | D-11, D-12 | — | R-06 | V-06 | CE-06 |
| SPA-CN-01 | R1 §5 | Consentimiento para el procedimiento | F | Respaldada por formulario (no aprobada como regla digital) | D-11 | — | R-06 | V-01, V-06 | CE-06 |
| SPA-CN-02 | R1 §5 | Fotografías para seguimiento | F | Respaldada por formulario (no aprobada como regla digital) | D-09 | C-04 | R-06 | V-01, V-07 | CE-06 |
| SPA-CN-03 | R1 §5 | Fotografías para publicidad | F | Respaldada por formulario (no aprobada como regla digital) | D-09 | C-04 | R-06 | V-01, V-07 | CE-06 |
| SPA-CN-04 | R1 §5 | Uso académico | P | Pendiente de decisión | D-12 | — | R-06 | — | CE-06 |
| SPA-CN-05 | R1 §5 | Evidencia y retiro de autorización | F+T+P | Respaldada por formulario (no aprobada como regla digital) | D-11, D-12 | — | R-06 | V-01, V-06 | CE-06 |
| SPA-CN-06 | R1 §5 | Independencia de las autorizaciones | F+P | Respaldada por formulario (no aprobada como regla digital) | D-12 | C-04 | R-06 | V-01 | CE-06 |
| SPA-INV-05 | R1 §10 | No publicar imágenes sobre la base exclusiva del consentimiento de seguimiento | F | Respaldada por formulario (no aprobada como regla digital) | D-11 | C-04 | R-06 | V-01, V-06 | CE-06 |
| SPA-SEC-INV-10 | R4 §11 | Una autorización retirada impide usos futuros sin alterar los hechos ya registrados | T | Propuesta técnica (no aprobada) | — | C-04 | R-06 | — | CE-06 |

### Fotografías (16)

| ID | Fuente | Descripción | Evid. | Aprobación | Dep. | Conf. | Riesgo | Verif. | Futuro |
|---|---|---|---|---|---|---|---|---|---|
| SPA-AT-03 | R1 §4 | Comparación fotográfica | F+P | Respaldada por formulario (no aprobada como regla digital) | D-12 | — | R-03, R-04 | V-01 | CE-07 |
| SPA-CARE-FOT-01 | R3 §6.3 | Una fotografía pertenece a un expediente y a una atención (sesión o valoración) y guarda quién la tomó y… | T | Propuesta técnica (no aprobada) | D-09 | — | R-03, R-04 | V-07 | CE-07 |
| SPA-CARE-FOT-02 | R3 §6.3 | El archivo se almacena mediante `IFileStorage` y se referencia por identificador; la ruta física no se… | T | Propuesta técnica (no aprobada) | D-09 | — | R-03, R-04 | V-07 | CE-07 |
| SPA-CARE-FOT-03 | R3 §6.3 | La finalidad de uso no es un atributo del archivo sino una verificación de autorización en el momento… | T | Propuesta técnica (no aprobada) | D-09, D-11 | C-04 | R-03, R-04 | V-06, V-07 | CE-07 |
| SPA-CARE-FOT-04 | R3 §6.3 | Ninguna fotografía se publica, exporta ni comparte automáticamente; toda exportación es una acción… | T | Propuesta técnica (no aprobada) | D-07, D-09 | — | R-03, R-04 | V-05, V-07 | CE-07 |
| SPA-CARE-FOT-05 | R3 §6.3 | Las fotografías no aparecen en listados generales ni en vistas de agenda; solo dentro del expediente y… | T | Propuesta técnica (no aprobada) | D-07, D-09 | — | R-03, R-04 | V-05, V-07 | CE-07 |
| SPA-CARE-FOT-06 | R3 §6.3 | Cada consulta o exportación de una fotografía se registra en un registro de accesos (usuario, instante,… | T+NV | Propuesta técnica (no aprobada) | D-07, D-08, D-09, D-10 | C-03 | R-03, R-04 | V-07 | CE-07 |
| SPA-CARE-FOT-07 | R3 §6.3 | Las fotografías se desactivan lógicamente; no se invoca `DeleteAsync` sobre ellas salvo una decisión… | T+P | Propuesta con puntos pendientes (no aprobada) | D-09, D-12 | C-02 | R-04, R-05 | V-07 | CE-07 |
| SPA-CARE-FOT-08 | R3 §6.3 | Retiro de la autorización de seguimiento: la fotografía deja de poder consultarse o usarse según el… | P | Pendiente de decisión | D-09, D-11, D-12 | C-02, C-04 | R-03, R-04 | V-06, V-07 | CE-07 |
| SPA-CARE-FOT-09 | R3 §6.3 | Metadatos: el archivo puede contener metadatos del dispositivo (ubicación, hora); si se eliminan al… | P | Pendiente de decisión | D-09, D-12 | — | R-03, R-04 | V-06, V-07 | CE-07 |
| SPA-CARE-FOT-10 | R3 §6.3 | Formatos admitidos, tamaño máximo, compresión y número de fotografías por atención: no recuperados | P | Pendiente de decisión | D-09, D-12 | — | R-03, R-04 | V-07 | CE-07 |
| SPA-CARE-FOT-11 | R3 §6.3 | Condiciones comparables (iluminación, distancia, ángulo y ausencia de filtros) se registran como nota de… | F+T | Respaldada por formulario (no aprobada como regla digital) | — | — | R-03, R-04 | V-01 | CE-07 |
| SPA-FO-01 | R1 §5 | Seguridad de imágenes | F+T | Respaldada por formulario (no aprobada como regla digital) | — | — | R-03, R-04 | V-01 | CE-07 |
| SPA-SEC-INV-02 | R4 §11 | Ninguna fotografía se sirve sin autorización, sin consentimiento vigente para la finalidad y sin… | T | Propuesta técnica (no aprobada) | D-07, D-08, D-09, D-11 | C-04 | R-03, R-04 | V-06, V-07 | CE-07 |
| SPA-SEC-INV-03 | R4 §11 | Las rutas de archivos las genera el servidor; ninguna ruta proviene del usuario | T | Propuesta técnica (no aprobada) | D-09 | — | R-03, R-04 | V-07 | CE-07 |
| SPA-SEC-INV-04 | R4 §11 | Los archivos clínicos no se borran físicamente salvo por el procedimiento de eliminación legal aprobado | T | Propuesta técnica (no aprobada) | D-09, D-11 | C-02 | R-04, R-05 | V-06, V-07 | CE-07 |

### Agenda (12)

| ID | Fuente | Descripción | Evid. | Aprobación | Dep. | Conf. | Riesgo | Verif. | Futuro |
|---|---|---|---|---|---|---|---|---|---|
| SPA-AG-01 | R1 §6 | Elementos del contrato de agenda | T | Propuesta técnica (no aprobada) | — | — | R-09 | V-03, V-05 | CE-08 |
| SPA-AG-02 | R1 §6 | Integridad de las reservas | T+P | Propuesta con puntos pendientes (no aprobada) | D-12 | — | R-09 | V-05 | CE-08 |
| SPA-AG-03 | R1 §6 | Relación de la agenda con Empleados | T+P | Propuesta con puntos pendientes (no aprobada) | D-01, D-12 | C-07 | R-09 | V-02, V-05 | CE-08 |
| SPA-OPS-AGE-01 | R2 §2.4 | Una cita pertenece a una sola empresa y una sola sucursal, referencia a un cliente BusinessPartner… | T | Propuesta técnica (no aprobada) | D-01, D-02, D-10, D-13 | — | R-09 | V-02, V-05 | CE-08 |
| SPA-OPS-AGE-02 | R2 §2.4 | Estados candidatos: `Programada`, `Cancelada`, `Ausente`, `Atendida`. «Confirmada» y los recordatorios… | T+P | Propuesta con puntos pendientes (no aprobada) | D-11, D-12 | — | R-09 | V-06 | CE-08 |
| SPA-OPS-AGE-03 | R2 §2.4 | Horarios de establecimiento por sucursal como patrón de horas locales de la empresa; el instante UTC se… | T+NV | Propuesta técnica (no aprobada) | D-10, D-13, D-12 | — | R-09 | V-05 | CE-08 |
| SPA-OPS-AGE-05 | R2 §2.4 | Recursos bloqueantes: alternativa A, sin recursos en V1.1 (solo profesional y sucursal); alternativa B,… | P | Pendiente de decisión | D-01, D-13, D-12 | — | R-09 | V-02, V-05 | CE-08 |
| SPA-OPS-AGE-06 | R2 §2.4 | Dos citas activas del mismo profesional no pueden solaparse; el solape se rechaza con una condición de… | T+P | Propuesta con puntos pendientes (no aprobada) | D-01, D-10, D-12 | — | R-09 | V-02, V-05 | CE-08 |
| SPA-OPS-AGE-07 | R2 §2.4 | Reprogramar mantiene la identidad de la cita y registra un cambio (instante anterior y nuevo, autor,… | T+P | Propuesta con puntos pendientes (no aprobada) | D-02, D-10, D-12 | — | R-09 | V-05 | CE-08 |
| SPA-OPS-AGE-08 | R2 §2.4 | Cancelar fija el estado `Cancelada`, el motivo, el autor y el instante; nunca elimina. Quién puede… | T+P | Propuesta con puntos pendientes (no aprobada) | D-10, D-12 | — | R-09 | V-06 | CE-08 |
| SPA-OPS-AGE-09 | R2 §2.4 | Ausencia: tras el inicio de la cita, un usuario autorizado la marca `Ausente`; su efecto sobre un… | T+P | Propuesta con puntos pendientes (no aprobada) | D-12 | — | R-09 | V-03, V-05 | CE-08 |
| SPA-OPS-AGE-11 | R2 §2.4 | Una atención sin cita previa (cliente sin reserva) puede o no admitirse | P | Pendiente de decisión | D-02, D-12 | — | R-09 | V-05 | CE-08 |

### Servicios (8)

| ID | Fuente | Descripción | Evid. | Aprobación | Dep. | Conf. | Riesgo | Verif. | Futuro |
|---|---|---|---|---|---|---|---|---|---|
| SPA-OPS-SRV-01 | R2 §3.3 | Todo servicio comercializable de Spa es un Item `Service` de la empresa; Spa no crea un catálogo… | E+T | Propuesta técnica (no aprobada) | D-03 | — | — | — | CE-09 |
| SPA-OPS-SRV-03 | R2 §3.3 | Duración, sucursales, recursos y condiciones de atención son datos propios de Spa, porque Items no tiene… | T | Propuesta técnica (no aprobada) | D-03, D-13 | — | — | — | CE-09 |
| SPA-OPS-SRV-04 | R2 §3.3 | Duración en minutos enteros positivos; la cita guarda una instantánea de la duración y del nombre al… | T | Propuesta técnica (no aprobada) | — | — | — | V-05 | CE-09 |
| SPA-OPS-SRV-05 | R2 §3.3 | Sucursales: por defecto el servicio es atendible en todas las sucursales activas; restringirlo a algunas… | T+P | Propuesta con puntos pendientes (no aprobada) | D-13, D-12 | — | — | — | CE-09 |
| SPA-OPS-SRV-09 | R2 §3.3 | Estado activo/inactivo: un servicio o procedimiento inactivo no admite citas nuevas y se conserva en el… | T | Propuesta técnica (no aprobada) | D-11 | — | — | V-05, V-06 | CE-09 |
| SPA-OPS-SRV-11 | R2 §3.3 | Historial de cambios: Entity Audit de las entidades de servicio de Spa; Items conserva el suyo… | T | Propuesta técnica (no aprobada) | D-03, D-08, D-11 | — | — | V-06 | CE-09 |
| SPA-SV-01 | R1 §7 | Integración con el catálogo de Items | R+T | Reconstruida (no aprobada) | D-03 | — | — | — | CE-09 |
| SPA-SV-02 | R1 §7 | Datos de servicio por documentar | T | Propuesta técnica (no aprobada) | — | — | — | — | CE-09 |

### Tratamientos (4)

| ID | Fuente | Descripción | Evid. | Aprobación | Dep. | Conf. | Riesgo | Verif. | Futuro |
|---|---|---|---|---|---|---|---|---|---|
| SPA-IN-03 | R1 §8 | Aparatología | T | Propuesta técnica (no aprobada) | — | — | — | — | CE-09 |
| SPA-OPS-SRV-02 | R2 §3.3 | Relación servicio ↔ procedimiento: alternativa A, perfil de Spa 1:1 por Item (un servicio equivale a un… | P | Pendiente de decisión | D-03, D-07, D-12 | — | — | — | CE-09 |
| SPA-OPS-SRV-08 | R2 §3.3 | Aparatología: texto o referencia de identificación por procedimiento y por sesión; sin módulo de activos | T | Propuesta técnica (no aprobada) | — | — | — | — | CE-09 |
| SPA-OPS-SRV-10 | R2 §3.3 | Condiciones de atención (precauciones, contraindicaciones, preparación): texto asociado al… | T | Propuesta técnica (no aprobada) | — | — | — | — | CE-09 |

### Planes y paquetes (6)

| ID | Fuente | Descripción | Evid. | Aprobación | Dep. | Conf. | Riesgo | Verif. | Futuro |
|---|---|---|---|---|---|---|---|---|---|
| SPA-AT-01 | R1 §4 | Plan de atención estética | F+P | Respaldada por formulario (no aprobada como regla digital) | D-12 | C-05 | — | V-01 | CE-10 |
| SPA-AT-05 | R1 §4 | Distinción conceptual entre plan de atención y paquete comercial | T+P | Propuesta con puntos pendientes (no aprobada) | D-12 | C-05 | — | V-03 | CE-10 |
| SPA-OPS-PKG-01 | R2 §4.6 | El saldo de sesiones se modela como libro append-only; los movimientos nunca se editan ni eliminan, se… | T | Propuesta técnica (no aprobada) | D-05 | — | — | V-06 | CE-10 |
| SPA-OPS-PKG-05 | R2 §4.6 | El cambio de profesional o de sucursal en una sesión de un contrato se rige por las respuestas de… | P | Pendiente de decisión | D-01, D-13, D-12 | — | — | V-02, V-03 | CE-10 |
| SPA-OPS-PKG-06 | R2 §4.6 | Un contrato inactivo, vencido o devuelto no admite sesiones nuevas; sus movimientos se conservan | T+P | Propuesta con puntos pendientes (no aprobada) | D-11, D-12 | — | — | V-03, V-06 | CE-10 |
| SPA-PQ-01 | R1 §7 | Preguntas contractuales sobre paquetes y sesiones comerciales | P | Pendiente de decisión | D-12 | C-05, C-08 | — | V-03 | CE-10 |

### Sesiones (9)

| ID | Fuente | Descripción | Evid. | Aprobación | Dep. | Conf. | Riesgo | Verif. | Futuro |
|---|---|---|---|---|---|---|---|---|---|
| SPA-AT-02 | R1 §4 | Seguimiento de tratamientos | F | Respaldada por formulario (no aprobada como regla digital) | — | — | R-06 | V-01 | CE-10 |
| SPA-AT-04 | R1 §4 | Resultado final del tratamiento | F+P | Respaldada por formulario (no aprobada como regla digital) | D-12 | — | R-06 | V-01 | CE-10 |
| SPA-AT-06 | R1 §4 | Sesión prevista frente a sesión realizada | T | Propuesta técnica (no aprobada) | — | C-06 | R-06 | — | CE-10 |
| SPA-AT-07 | R1 §4 | Registro histórico de la atención | T+P | Propuesta con puntos pendientes (no aprobada) | D-12 | C-06 | R-06 | — | CE-10 |
| SPA-INV-09 | R1 §10 | Verificar coherencia entre citas, sesiones y documentos comerciales | P | Pendiente de decisión | D-12 | C-06 | R-06 | V-05 | CE-10 |
| SPA-OPS-AGE-10 | R2 §2.4 | Atención efectivamente realizada es un registro distinto de la cita (sesión, SPA-AT-06/AT-07). La cita… | T | Propuesta técnica (no aprobada) | D-10 | C-06 | R-06 | V-05 | CE-10 |
| SPA-OPS-PKG-02 | R2 §4.6 | Una sesión consume como máximo una vez: restricción única `(contract_id, session_id)` en movimientos de… | T | Propuesta técnica (no aprobada) | — | — | R-06 | — | CE-10 |
| SPA-OPS-PKG-03 | R2 §4.6 | Todo movimiento referencia el contrato, y según el caso la cita o la sesión, el profesional… | T | Propuesta técnica (no aprobada) | D-01, D-10, D-13 | — | R-06 | V-02, V-03, V-05 | CE-10 |
| SPA-OPS-VTA-04 | R2 §5.3 | El registro de una sesión no genera factura, cobro ni movimiento contable | T | Propuesta técnica (no aprobada) | D-04, D-05 | — | R-06 | V-03 | CE-10 |

### Ventas (8)

| ID | Fuente | Descripción | Evid. | Aprobación | Dep. | Conf. | Riesgo | Verif. | Futuro |
|---|---|---|---|---|---|---|---|---|---|
| SPA-AR-05 | R1 §2 | Integración con el ERP | R+P | Reconstruida (no aprobada) | D-12 | C-08 | — | — | CE-11 |
| SPA-INV-08 | R1 §10 | No crear operaciones comerciales o contables paralelas | R | Reconstruida (no aprobada) | — | — | — | — | CE-11 |
| SPA-OPS-PKG-04 | R2 §4.6 | Un contrato referencia la factura (id y línea) pero no copia importes ni impuestos; el documento… | T | Propuesta técnica (no aprobada) | D-04 | C-08 | — | V-03 | CE-11 |
| SPA-OPS-SRV-12 | R2 §3.3 | La venta del servicio usa el Item `Service` y el flujo de facturación existente; el precio y los… | E+T | Propuesta técnica (no aprobada) | D-03, D-04 | — | — | V-03 | CE-11 |
| SPA-OPS-VTA-01 | R2 §5.3 | Spa no emite facturas, cobros ni asientos; solo referencia documentos de Ventas y de Cuentas por cobrar… | T | Propuesta técnica (no aprobada) | D-04, D-05 | — | — | V-03 | CE-11 |
| SPA-OPS-VTA-02 | R2 §5.3 | El paquete se vende como un Item `Service` por la facturación existente; Spa conserva el contrato y sus… | T | Propuesta técnica (no aprobada) | D-03, D-04, D-11 | — | — | V-03, V-06 | CE-11 |
| SPA-OPS-VTA-06 | R2 §5.3 | La devolución o anulación comercial no modifica el libro de sesiones por sí sola; la acción sobre el… | T+P | Propuesta con puntos pendientes (no aprobada) | D-12 | — | — | V-03, V-05 | CE-11 |
| SPA-VT-01 | R1 §8 | Separación entre profesional y usuario que opera | A-der+P | Parcialmente aprobada (núcleo aprobado; resto no) (rev.) | D-01, D-12 | — | — | V-02 | CE-11 |

### Cobros (2)

| ID | Fuente | Descripción | Evid. | Aprobación | Dep. | Conf. | Riesgo | Verif. | Futuro |
|---|---|---|---|---|---|---|---|---|---|
| SPA-OPS-PKG-07 | R2 §4.6 | No se crea ningún anticipo ni prepago de cliente dentro de Spa (ver §5) | T | Propuesta técnica (no aprobada) | D-02, D-05 | C-08 | — | — | CE-11 |
| SPA-OPS-VTA-03 | R2 §5.3 | Spa no crea un mecanismo de anticipos de clientes. Si el negocio requiere cobrar por adelantado un… | P | Pendiente de decisión | D-02, D-04, D-05, D-12 | C-08 | — | V-03 | CE-11 |

### Inventario (3)

| ID | Fuente | Descripción | Evid. | Aprobación | Dep. | Conf. | Riesgo | Verif. | Futuro |
|---|---|---|---|---|---|---|---|---|---|
| SPA-IN-01 | R1 §8 | Registro documental de insumos y equipos | F | Respaldada por formulario (no aprobada como regla digital) | D-06 | — | — | V-01 | CE-11 |
| SPA-IN-02 | R1 §8 | Registro documental frente a movimiento real de inventario | T+P | Propuesta con puntos pendientes (no aprobada) | D-04, D-06, D-12 | — | — | — | CE-11 |
| SPA-OPS-SRV-07 | R2 §3.3 | Insumos: alternativa A, solo registro documental por sesión (productos usados como texto o referencia de… | T+P | Propuesta con puntos pendientes (no aprobada) | D-03, D-04, D-06, D-12 | — | — | V-03 | CE-11 |

### Seguridad (30)

| ID | Fuente | Descripción | Evid. | Aprobación | Dep. | Conf. | Riesgo | Verif. | Futuro |
|---|---|---|---|---|---|---|---|---|---|
| SPA-CARE-EXP-10 | R3 §2.3 | El expediente es de empresa; la sucursal es un atributo de cada atención y no del expediente | T+P | Propuesta con puntos pendientes (no aprobada) | D-13, D-12 | — | R-01 | — | CE-12 |
| SPA-CARE-SEG-01 | R3 §7.1 | Ni `employees.view` ni ningún permiso de `employees.*` o `access.*` habilita el acceso a expedientes,… | A-der | Aprobada derivada (rev.) | D-01, D-07, D-09, D-11 | C-01 | R-01 | V-06, V-07 | CE-12 |
| SPA-CARE-SEG-02 | R3 §7.1 | Una asignación laboral a una sucursal no concede acceso a datos clínicos de esa sucursal | A | Aprobada derivada (rev.) | D-07, D-13 | — | R-01 | — | CE-12 |
| SPA-CARE-SEG-03 | R3 §7.1 | Los permisos de Spa se declaran solo en el Kernel y ningún perfil existente los recibe automáticamente | T | Propuesta técnica (no aprobada) | D-07 | C-09 | R-01 | — | CE-12 |
| SPA-CARE-SEG-04 | R3 §7.1 | El acceso total del rol Admin a datos clínicos es una decisión pendiente: alternativa A, el rol Admin… | P | Pendiente de decisión | D-07, D-11, D-12 | C-01 | R-01 | V-05, V-06 | CE-12 |
| SPA-CARE-SEG-05 | R3 §7.1 | Las proyecciones exponen solo lo necesario para cada operación; la agenda no muestra datos clínicos | T | Propuesta técnica (no aprobada) | — | — | R-01 | V-05 | CE-12 |
| SPA-INV-04 | R1 §10 | No derivar permisos de una asignación laboral | A | Aprobada literal (ADR-050) (rev.) | D-07 | — | R-01 | — | CE-12 |
| SPA-INV-07 | R1 §10 | Proteger datos sensibles mediante autorizaciones específicas | F+T | Respaldada por formulario (no aprobada como regla digital) | — | — | R-01 | V-01 | CE-12 |
| SPA-SEC-INV-05 | R4 §11 | La clave maestra de cifrado no reside en el volumen de datos ni en sus respaldos | T | Propuesta técnica (no aprobada) | D-09 | — | R-03 | V-07 | CE-12 |
| SPA-SEC-INV-07 | R4 §11 | Ni `employees.*` ni `access.*` conceden acceso a datos clínicos | A-der | Aprobada derivada (rev.) | D-01, D-07 | C-01 | R-01 | — | CE-12 |
| SPA-SEC-INV-09 | R4 §11 | El acceso excepcional exige motivo y queda marcado para revisión | T | Propuesta técnica (no aprobada) | D-07 | — | R-01 | — | CE-12 |
| SPA-SEC-INV-12 | R4 §11 | Los datos de aislamiento por tenant y empresa no se omiten ni en lecturas, exportaciones ni registros de… | E | Existente en el ERP (no es decisión de Spa) | D-07 | — | R-01 | — | CE-12 |
| SPA-SEC-LEG-01 | R4 §7.4 | Mostrar al titular antes de recabar el consentimiento los puntos del Art. 12 aplicables y registrar la… | T+L-rec | Propuesta técnica (no aprobada) | D-11 | — | R-06 | V-06 | CE-12 |
| SPA-SEC-LEG-02 | R4 §7.4 | Conservar la evidencia del consentimiento de forma que pueda demostrarse (versión del texto, instante,… | T+L-rec | Propuesta técnica (no aprobada) | D-10, D-11 | — | R-06 | V-06 | CE-12 |
| SPA-SEC-LEG-03 | R4 §7.4 | Registrar las solicitudes de ejercicio de derechos con fecha de recepción y plazo de 15 días | T+L-rec | Propuesta técnica (no aprobada) | D-10 | — | R-06 | V-06 | CE-12 |
| SPA-SEC-LEG-04 | R4 §7.4 | Mantener un plan de respuesta a incidentes con los términos de 5 y 2 días | T+L-rec | Propuesta técnica (no aprobada) | — | — | R-06 | V-06 | CE-12 |
| SPA-SEC-LEG-05 | R4 §7.4 | Minimizar los datos: no almacenar más de lo necesario para cada finalidad | T+L-rec | Propuesta técnica (no aprobada) | D-09 | — | R-06 | V-06, V-07 | CE-12 |
| SPA-SEC-LEG-06 | R4 §7.4 | Anonimizar o seudonimizar cuando se usen datos de seguimiento para fines distintos de la atención | T+L-rec | Propuesta técnica (no aprobada) | — | — | R-06 | V-06 | CE-12 |
| SPA-SEC-LEG-07 | R4 §7.4 | Obtener asesoría legal para L-INT-01 a L-INT-07 antes de aprobar retención, exportación, publicación,… | T+L-rec | Propuesta técnica (no aprobada) | D-11 | — | R-06 | V-06 | CE-12 |
| SPA-SEC-RET-01 | R4 §6.4 | Ningún borrado físico fuera de un procedimiento de eliminación legal aprobado | T | Propuesta técnica (no aprobada) | D-11 | C-02 | R-04, R-05 | V-06 | CE-12 |
| SPA-SEC-RET-02 | R4 §6.4 | El procedimiento de eliminación exige un permiso propio, una solicitud registrada con fecha y titular,… | T | Propuesta técnica (no aprobada) | D-07, D-10, D-11 | C-02 | R-04, R-05 | V-06 | CE-12 |
| SPA-SEC-RET-03 | R4 §6.4 | Los antecedentes corregidos conservan sus versiones anteriores con acceso restringido, sin mostrarse… | T+L-NV | Propuesta técnica (no aprobada) | D-07, D-11 | — | R-01 | V-06 | CE-12 |
| SPA-SEC-RET-04 | R4 §6.4 | Un retiro de consentimiento suspende el uso futuro; las versiones y los registros de atención ya… | T | Propuesta técnica (no aprobada) | D-11 | — | R-01 | V-06 | CE-12 |
| SPA-SEC-RET-05 | R4 §6.4 | Los respaldos con datos eliminados se tratan con un ciclo de rotación documentado; con borrado… | T | Propuesta técnica (no aprobada) | — | — | R-01 | V-06 | CE-12 |
| SPA-SEC-RET-06 | R4 §6.4 | Plazos de conservación por tipo de dato: no recuperados | P+L-NV | Pendiente de decisión | D-11, D-12 | — | R-01 | V-06 | CE-12 |
| SPA-SG-01 | R1 §9 | Separación de permisos | A+A-der+P | Parcialmente aprobada (núcleo aprobado; resto no) (rev.) | D-07, D-12 | C-01 | R-01 | — | CE-12 |
| SPA-SG-03 | R1 §9 | Información sensible | F+P | Respaldada por formulario (no aprobada como regla digital) | D-12 | — | R-01 | V-01 | CE-12 |
| SPA-SG-04 | R1 §9 | Permisos diferenciados de Spa | T | Propuesta técnica (no aprobada) | D-07 | C-01, C-09 | R-01 | — | CE-12 |
| SPA-SG-05 | R1 §9 | Aislamiento de datos | T | Propuesta técnica (no aprobada) | — | — | R-01 | — | CE-12 |
| SPA-SG-06 | R1 §9 | Acceso mínimo en proyecciones | T | Propuesta técnica (no aprobada) | D-07 | — | R-01 | — | CE-12 |

### Auditoría (13)

| ID | Fuente | Descripción | Evid. | Aprobación | Dep. | Conf. | Riesgo | Verif. | Futuro |
|---|---|---|---|---|---|---|---|---|---|
| SPA-AU-01 | R1 §9 | Auditoría con los mecanismos oficiales | T | Propuesta técnica (no aprobada) | D-08 | C-03 | R-02 | — | CE-12 |
| SPA-AU-02 | R1 §9 | Operaciones a auditar | T+P | Propuesta con puntos pendientes (no aprobada) | D-12 | C-03 | R-02 | — | CE-12 |
| SPA-INV-06 | R1 §10 | Preservar trazabilidad histórica | T | Propuesta técnica (no aprobada) | — | — | R-02 | V-05 | CE-12 |
| SPA-OPS-AGE-12 | R2 §2.4 | Historial: todo cambio de estado, hora, profesional o sucursal queda en `AppointmentChange` y en Entity… | T | Propuesta técnica (no aprobada) | D-01, D-08, D-13 | — | R-02 | V-02 | CE-12 |
| SPA-SEC-AUD-01 | R4 §4.5 | Toda consulta del detalle de un expediente, valoración, antecedentes, consentimiento o fotografía, toda… | T | Propuesta técnica (no aprobada) | D-09, D-11 | C-03 | R-02 | V-06, V-07 | CE-12 |
| SPA-SEC-AUD-02 | R4 §4.5 | Un listado registra una entrada por solicitud con el filtro resumido, no una por fila | T | Propuesta técnica (no aprobada) | — | — | R-02 | — | CE-12 |
| SPA-SEC-AUD-03 | R4 §4.5 | Los accesos denegados a recursos de Spa se registran con el resultado `Denied` | T | Propuesta técnica (no aprobada) | D-07 | — | R-02 | — | CE-12 |
| SPA-SEC-AUD-04 | R4 §4.5 | El registro de accesos es append-only; nadie lo edita ni lo elimina; su consulta exige un permiso propio… | T | Propuesta técnica (no aprobada) | D-07, D-08 | — | R-02 | V-06 | CE-12 |
| SPA-SEC-AUD-05 | R4 §4.5 | El acceso excepcional (alternativa D de §3.3) exige un motivo y se marca para revisión | T | Propuesta técnica (no aprobada) | D-07 | — | R-02 | — | CE-12 |
| SPA-SEC-AUD-06 | R4 §4.5 | El registro no contiene contenido clínico ni nombres de archivo | T | Propuesta técnica (no aprobada) | D-09 | — | R-02 | V-07 | CE-12 |
| SPA-SEC-AUD-07 | R4 §4.5 | Plazo de conservación del registro de accesos: no recuperado | P | Pendiente de decisión | D-07, D-08, D-11, D-12 | C-03 | R-02 | V-06 | CE-12 |
| SPA-SEC-INV-01 | R4 §11 | Toda lectura exitosa de un recurso sensible deja un registro sin contenido clínico | T | Propuesta técnica (no aprobada) | — | C-03 | R-02 | — | CE-12 |
| SPA-SEC-INV-06 | R4 §11 | El registro de accesos es append-only y no contiene contenido | T | Propuesta técnica (no aprobada) | D-07, D-08 | — | R-02 | — | CE-12 |

### Frontend (1)

| ID | Fuente | Descripción | Evid. | Aprobación | Dep. | Conf. | Riesgo | Verif. | Futuro |
|---|---|---|---|---|---|---|---|---|---|
| SPA-UX-01 | R1 §19 | Frontend y experiencia de usuario | T | Propuesta técnica (no aprobada) | — | — | — | — | CE-14 |

### API (1)

| ID | Fuente | Descripción | Evid. | Aprobación | Dep. | Conf. | Riesgo | Verif. | Futuro |
|---|---|---|---|---|---|---|---|---|---|
| SPA-API-01 | R1 §18 | API conceptual | T | Propuesta técnica (no aprobada) | — | — | — | — | CE-13 |

### 3.1 Revisión manual de las filas con respaldo aprobado (18)

Cada clasificación se contrastó con el texto de ADR-050 o de la especificación de Empleados. Cuando la evidencia no bastaba, **se corrigió el estado en la matriz sin modificar la regla original de la fuente**.

| ID | Clasificación en la matriz | Etiqueta en la fuente | Evidencia exacta | Resultado |
|---|---|---|---|---|
| `SPA-SG-02` | Aprobada literal | [A] (R1 §9) | ADR-050 §3, fila DEC-E4: «la atención identifica al profesional responsable y al usuario que ejecutó el registro» | Confirmada |
| `SPA-INV-02` | Aprobada literal | [A] (R1 §10) | ADR-050 §3 DEC-E1 y §13: «Spa reutiliza Employee y no crea otro registro laboral» | Confirmada |
| `SPA-INV-03` | Aprobada literal | [A] (R1 §10) | ADR-050 §3 DEC-E4 y §13: distingue al profesional responsable del usuario que registra | Confirmada |
| `SPA-INV-04` | Aprobada literal | [A] (R1 §10) | ADR-050 §6: «las asignaciones laborales nunca conceden permisos ni modifican `CompanyUserBranch`»; §8 | Confirmada |
| `SPA-CARE-SEG-02` | Aprobada derivada | [A] (R3 §7.1) | ADR-050 §6 dice que las asignaciones nunca conceden permisos; extender eso a «datos clínicos de esa sucursal» es una aplicación, no un texto literal | **Corregida:** de literal a derivada (la regla de la fuente no se modifica) |
| `SPA-CARE-SEG-01` | Aprobada derivada | [A, derivado] (R3 §7.1) | ADR-050 §6 (los perfiles existentes no reciben permisos de Empleados; las asignaciones no conceden permisos) y §13 (Spa conserva sus permisos clínicos); el texto no menciona `employees.view` respecto de expedientes | Confirmada |
| `SPA-SEC-INV-07` | Aprobada derivada | [A, derivado] (R4 §11) | Igual que SPA-CARE-SEG-01 (ADR-050 §6 y §13) | Confirmada |
| `SPA-INV-10` | Aprobada derivada | [A, derivado] (R1 §10) | No procede de ADR-050 sino de normas oficiales del repositorio: `data-standards.md`, ADR-034, `security.md` y `audit-infrastructure.md`; no es una decisión de Spa | Confirmada como derivada de normas generales |
| `SPA-CARE-SEG-06` | Parcialmente aprobada | [A] (R3 §7.1) | DEC-E4 aprueba identificar ambos; «se auditan por separado» no figura en ADR-050 (su §10 trata la auditoría en general) | **Corregida:** de literal a parcial |
| `SPA-SEC-INV-08` | Parcialmente aprobada | [A] (R4 §11) | Igual que SPA-CARE-SEG-06 (DEC-E4 y §13 para identificar ambos; la auditoría separada es derivada) | **Corregida:** de literal a parcial |
| `SPA-CARE-EXP-05` | Parcialmente aprobada | [A] + [T] (R3 §2.3) | ADR-050 §3 DEC-E4 y §13 aprueban la distinción; los campos (`EmployeeId`, usuario, sucursal) son propuesta | Confirmada |
| `SPA-AL-02` | Parcialmente aprobada | [A, derivado] + [R] + [T] (R1 §2) | ADR-050 §3 DEC-E1 y §13 respaldan solo que Spa no crea identidades laborales; el resto (clientes, otros módulos) es [R]/[T] | Confirmada |
| `SPA-AR-02` | Parcialmente aprobada | [A] + [A, derivado] + [R] + [T] (R1 §2) | ADR-050 §3 DEC-E1 y §13; especificación de Empleados EMP-BR-21 y §19 aprueban la necesidad de `IEmployeeDirectory`; firmas y contenido de la consulta son [T]/[R] | Confirmada |
| `SPA-AR-03` | Parcialmente aprobada | [A] + [T] + [P] (R1 §2) | ADR-050 §3 DEC-E4 y §13; cómo se representan ambos en Spa es [P] | Confirmada |
| `SPA-AR-04` | Parcialmente aprobada | [A] + [A, derivado] + [R] (R1 §2) | ADR-050 §13 (permisos clínicos, agenda, horarios, sesiones y disponibilidad de Spa); §7 y §8 para lo derivado; servicios y tratamientos son [R] | Confirmada |
| `SPA-SG-01` | Parcialmente aprobada | [A] + [A, derivado] + [P] (R1 §9) | ADR-050 §6 y §13; el catálogo de permisos de Spa es [P] | Confirmada |
| `SPA-OPS-VTA-05` | Parcialmente aprobada | [A, derivado] + [P] (R2 §5.3) | ADR-050 §3 DEC-E4 y §13 («el cobro no se confunde con quien presta el servicio»); la extensión a ventas es derivada y el vendedor y cobrador son [P] | Confirmada (núcleo derivado) |
| `SPA-VT-01` | Parcialmente aprobada | [A, derivado] + [P] (R1 §8) | Igual que SPA-OPS-VTA-05 | Confirmada (núcleo derivado) |

Resumen: 4 literales, 4 derivadas y 10 parciales; 3 reclasificadas a la baja (`SPA-CARE-SEG-02`, `SPA-CARE-SEG-06`, `SPA-SEC-INV-08`).

## 4. Pruebas candidatas (102)

Ninguna está aprobada. Los 22 identificadores marcados «(inferido)» no figuran literalmente en R3 (§4.2). Se agrupan por origen; la cobertura se solapa entre documentos (§5, `DUP-xx`) y **debe unificarse** en el contrato.

| ID | Fuente | Escenario | Futuro |
|---|---|---|---|
| SPA-T-01 | R1 §20 | La atención referencia al cliente correcto | CE-15 |
| SPA-T-02 | R1 §20 | No se crea un segundo empleado desde Spa | CE-15 |
| SPA-T-03 | R1 §20 | Se diferencia profesional de usuario registrador | CE-15 |
| SPA-T-04 | R1 §20 | Una asignación laboral no concede permisos | CE-12 |
| SPA-T-05 | R1 §20 | Se aplican las restricciones autorizadas de acceso a expedientes | CE-06 |
| SPA-T-06 | R1 §20 | Se conserva la valoración facial documentada | CE-05 |
| SPA-T-07 | R1 §20 | Se conserva la valoración corporal documentada | CE-05 |
| SPA-T-08 | R1 §20 | Las medidas respetan unidades y precisión oficial | CE-05 |
| SPA-T-09 | R1 §20 | Una sesión conserva sus observaciones y evolución | CE-10 |
| SPA-T-10 | R1 §20 | Las autorizaciones fotográficas son independientes | CE-06 |
| SPA-T-11 | R1 §20 | El permiso de seguimiento no habilita publicación promocional | CE-12 |
| SPA-T-12 | R1 §20 | Las consultas de agenda respetan los permisos correspondientes | CE-08 |
| SPA-T-13 | R1 §20 | Las reservas concurrentes cumplen el contrato de disponibilidad | CE-08 |
| SPA-T-14 | R1 §20 | Los saldos de sesiones cumplen las reglas comerciales aprobadas | CE-10 |
| SPA-T-15 | R1 §20 | Las operaciones comerciales reutilizan los módulos oficiales | CE-15 |
| SPA-T-16 | R1 §20 | Se mantiene aislamiento de datos por tenant y empresa | CE-15 |
| SPA-T-17 | R1 §20 | Los eventos relevantes quedan auditados | CE-15 |
| SPA-T-18 | R1 §20 | No se altera el historial por cambios posteriores en Empleados | CE-15 |
| SPA-CARE-T-CNS-01 | R3 (en línea) | cada tipo es independiente, SPA-T-10 y SPA-T-11 | CE-15 |
| SPA-CARE-T-CNS-02 (inferido) | R3 (en línea) | el texto de una versión no cambia | CE-15 |
| SPA-CARE-T-CNS-03 (inferido) | R3 (en línea) | un registro no se edita ni elimina | CE-15 |
| SPA-CARE-T-CNS-04 (inferido) | R3 (en línea) | un retiro bloquea usos futuros sin alterar atenciones pasadas | CE-15 |
| SPA-CARE-T-CNS-05 (inferido) | R3 (en línea) | la interrupción no marca la sesión como realizada completa | CE-10 |
| SPA-CARE-T-CNS-06 (inferido) | R3 (en línea) | una sesión sin consentimiento de procedimiento vigente no puede registrarse como realizada | CE-10 |
| SPA-CARE-T-CNS-07 (inferido) | R3 (en línea) | el uso académico no existe como funcionalidad hasta confirmarse | CE-15 |
| SPA-CARE-T-COR-01 | R3 (en línea) | medidas opcionales dentro de un conjunto | CE-05 |
| SPA-CARE-T-COR-02 (inferido) | R3 (en línea) | una corrección conserva el conjunto anterior | CE-15 |
| SPA-CARE-T-COR-03 (inferido) | R3 (en línea) | las diferencias no se almacenan y coinciden con los conjuntos | CE-15 |
| SPA-CARE-T-COR-04 (inferido) | R3 (en línea) | las medidas respetan unidades y precisión oficial, SPA-T-08 | CE-05 |
| SPA-CARE-T-COR-05 (inferido) | R3 (en línea) | «no medida» es distinto de cero | CE-05 |
| SPA-CARE-T-COR-06 (inferido) | R3 (en línea) | fechas como `DateOnly` | CE-15 |
| SPA-CARE-T-EXP-01 | R3 §2.8 | El expediente referencia un BP activo con rol Customer y no copia su identidad | CE-04 |
| SPA-CARE-T-EXP-02 | R3 §2.8 | El número de ficha es único por empresa bajo concurrencia | CE-15 |
| SPA-CARE-T-EXP-03 | R3 §2.8 | Corregir antecedentes crea una versión nueva y conserva la anterior | CE-04 |
| SPA-CARE-T-EXP-04 | R3 §2.8 | «Sin responder» no se interpreta como «no» | CE-15 |
| SPA-CARE-T-EXP-05 | R3 §2.8 | Profesional y usuario registrador se guardan por separado | CE-15 |
| SPA-CARE-T-EXP-06 | R3 §2.8 | `employees.view` no permite consultar un expediente | CE-04 |
| SPA-CARE-T-EXP-07 | R3 §2.8 | El expediente de otra empresa o tenant no es visible | CE-04 |
| SPA-CARE-T-FAC-01 | R3 (en línea) | una valoración puede guardarse sin campos clínicos opcionales y con los mínimos de integridad | CE-05 |
| SPA-CARE-T-FAC-02 (inferido) | R3 (en línea) | corregir conserva la versión anterior | CE-15 |
| SPA-CARE-T-FAC-03 (inferido) | R3 (en línea) | el rótulo no indica diagnóstico médico | CE-15 |
| SPA-CARE-T-FAC-04 (inferido) | R3 (en línea) | fototipo admite solo I a VI o sin evaluar | CE-05 |
| SPA-CARE-T-FAC-05 (inferido) | R3 (en línea) | la decisión no crea citas ni consume sesiones | CE-08 |
| SPA-CARE-T-FOT-01 | R3 (en línea) | subir sin consentimiento de seguimiento vigente se rechaza | CE-06 |
| SPA-CARE-T-FOT-02 (inferido) | R3 (en línea) | la finalidad promocional exige su autorización, SPA-T-11 | CE-06 |
| SPA-CARE-T-FOT-03 (inferido) | R3 (en línea) | no hay acceso sin permiso | CE-12 |
| SPA-CARE-T-FOT-04 (inferido) | R3 (en línea) | cada consulta queda registrada | CE-15 |
| SPA-CARE-T-FOT-05 (inferido) | R3 (en línea) | una fotografía no se elimina físicamente | CE-07 |
| SPA-CARE-T-FOT-06 (inferido) | R3 (en línea) | las fotografías no aparecen en listados ni en la agenda | CE-08 |
| SPA-CARE-T-FOT-07 (inferido) | R3 (en línea) | aislamiento por empresa | CE-15 |
| SPA-CARE-T-FOT-08 (inferido) | R3 (en línea) | un retiro bloquea el uso futuro según la política aprobada, pendiente | CE-15 |
| SPA-CARE-T-SEG-01 | R3 §7.6 | Un usuario con `employees.view` y sin permiso de Spa no accede a un expediente | CE-04 |
| SPA-CARE-T-SEG-02 | R3 §7.6 | Una asignación laboral a una sucursal no concede acceso a sus expedientes | CE-04 |
| SPA-CARE-T-SEG-03 | R3 §7.6 | Modificar antecedentes exige un permiso distinto del de consulta y del de valoraciones | CE-04 |
| SPA-CARE-T-SEG-04 | R3 §7.6 | Exportar exige un permiso propio y deja registro | CE-12 |
| SPA-CARE-T-SEG-05 | R3 §7.6 | Un perfil existente no recibe los permisos nuevos de Spa | CE-12 |
| SPA-CARE-T-SEG-06 | R3 §7.6 | Profesional y usuario registrador son distintos y ambos auditados | CE-15 |
| SPA-CARE-T-SEG-07 | R3 §7.6 | La agenda y los listados no exponen datos clínicos | CE-08 |
| SPA-CARE-T-SEG-08 | R3 §7.6 | Un usuario de otra empresa o tenant no accede | CE-15 |
| SPA-CARE-T-SEG-09 | R3 §7.6 | El comportamiento del rol Admin sigue la decisión aprobada (pendiente) | CE-15 |
| SPA-OPS-T-AGE-01 | R2 §2.13 | Dos reservas simultáneas del mismo profesional en el mismo intervalo: una gana y la otra recibe… | CE-08 |
| SPA-OPS-T-AGE-02 | R2 §2.13 | Una cita fuera del horario de establecimiento se rechaza | CE-08 |
| SPA-OPS-T-AGE-03 | R2 §2.13 | Un profesional dado de baja en Empleados no figura como disponible y sus citas futuras no cambian… | CE-08 |
| SPA-OPS-T-AGE-04 | R2 §2.13 | La cita en una hora inexistente por cambio de horario se rechaza | CE-08 |
| SPA-OPS-T-AGE-05 | R2 §2.13 | Reprogramar conserva la identidad y deja un cambio con valores anterior y nuevo | CE-15 |
| SPA-OPS-T-AGE-06 | R2 §2.13 | Cancelar y marcar ausencia no eliminan la cita y registran autor y motivo | CE-08 |
| SPA-OPS-T-AGE-07 | R2 §2.13 | Un usuario sin el permiso de agenda no accede; `employees.view` no alcanza | CE-08 |
| SPA-OPS-T-AGE-08 | R2 §2.13 | La asignación de sucursal de Empleados no concede acceso ni permisos de agenda | CE-08 |
| SPA-OPS-T-AGE-09 | R2 §2.13 | La cita de otra empresa o tenant no es visible | CE-08 |
| SPA-OPS-T-PKG-01 | R2 §4.11 | Dos consumos simultáneos de la misma sesión: solo uno se registra | CE-10 |
| SPA-OPS-T-PKG-02 | R2 §4.11 | El saldo coincide con la suma de movimientos tras una secuencia mixta | CE-10 |
| SPA-OPS-T-PKG-03 | R2 §4.11 | Un contrato no puede crearse dos veces para la misma línea de factura | CE-11 |
| SPA-OPS-T-PKG-04 | R2 §4.11 | Un movimiento no se edita ni elimina; la corrección es otro movimiento | CE-15 |
| SPA-OPS-T-PKG-05 | R2 §4.11 | Cancelar, reprogramar o ausencia producen el efecto sobre el saldo que defina la política aprobada… | CE-10 |
| SPA-OPS-T-PKG-06 | R2 §4.11 | Un contrato guarda la referencia a la factura sin copiar importes | CE-11 |
| SPA-OPS-T-SRV-01 | R2 §3.8 | Solo un Item `Service` activo de la empresa puede configurarse como servicio de Spa | CE-09 |
| SPA-OPS-T-SRV-02 | R2 §3.8 | Un servicio inactivo no admite citas nuevas y su historial se conserva | CE-08 |
| SPA-OPS-T-SRV-03 | R2 §3.8 | La cita conserva el nombre y la duración del momento de la reserva aunque el servicio cambie | CE-08 |
| SPA-OPS-T-SRV-04 | R2 §3.8 | Registrar insumos documentales no crea ningún movimiento de inventario | CE-11 |
| SPA-OPS-T-SRV-05 | R2 §3.8 | Un profesional se propone solo si su especialidad cumple la regla vigente | CE-15 |
| SPA-OPS-T-SRV-06 | R2 §3.8 | Facturar el servicio reutiliza el flujo de ventas y no crea otro documento comercial (SPA-T-15) | CE-11 |
| SPA-OPS-T-VTA-01 | R2 §5.7 | Facturar un servicio de Spa reutiliza el flujo de ventas sin bodega | CE-11 |
| SPA-OPS-T-VTA-02 | R2 §5.7 | Registrar una sesión no crea factura, cobro ni asiento | CE-10 |
| SPA-OPS-T-VTA-03 | R2 §5.7 | El contrato referencia la factura sin copiar importes | CE-11 |
| SPA-OPS-T-VTA-04 | R2 §5.7 | Una factura de otro cliente no puede enlazarse al contrato | CE-11 |
| SPA-OPS-T-VTA-05 | R2 §5.7 | La anulación o devolución comercial sigue las reglas de Ventas y no altera el libro de sesiones de… | CE-08 |
| SPA-OPS-T-VTA-06 | R2 §5.7 | Profesional y usuario registrador se guardan por separado | CE-15 |
| SPA-SEC-T-01 | R4 §12 | Un Admin sin concesión no obtiene el detalle de un expediente (si se adopta C) y un acceso… | CE-04 |
| SPA-SEC-T-02 | R4 §12 | Un usuario con perfil sin el permiso de Spa recibe 403 aunque tenga `employees.view` | CE-12 |
| SPA-SEC-T-03 | R4 §12 | Cada consulta de detalle, descarga y exportación crea un registro de acceso con usuario, empresa,… | CE-12 |
| SPA-SEC-T-04 | R4 §12 | Un acceso denegado a un recurso de Spa queda registrado con el resultado `Denied` | CE-15 |
| SPA-SEC-T-05 | R4 §12 | Si el registro síncrono falla, la lectura de una fotografía se deniega | CE-07 |
| SPA-SEC-T-06 | R4 §12 | Una ruta con `..` o absoluta no puede resolverse fuera del directorio base | CE-15 |
| SPA-SEC-T-07 | R4 §12 | Dos subidas simultáneas no sobrescriben un archivo existente | CE-15 |
| SPA-SEC-T-08 | R4 §12 | Un archivo cifrado no se lee sin su clave; el contenido en disco no es legible | CE-15 |
| SPA-SEC-T-09 | R4 §12 | La restauración de un respaldo cifrado recupera archivos y base y verifica sus hashes | CE-15 |
| SPA-SEC-T-10 | R4 §12 | La eliminación legal deja el registro del hecho sin datos personales y los archivos ilegibles | CE-15 |
| SPA-SEC-T-11 | R4 §12 | Una corrección crea una versión nueva y la anterior queda con acceso restringido | CE-15 |
| SPA-SEC-T-12 | R4 §12 | Un retiro de consentimiento bloquea el uso futuro y no altera atenciones previas | CE-06 |
| SPA-SEC-T-13 | R4 §12 | La fecha de nacimiento y el contacto de emergencia no se copian a BusinessPartner | CE-15 |
| SPA-SEC-T-14 | R4 §12 | El aislamiento por tenant y empresa se mantiene en lecturas, exportaciones y registros de acceso | CE-15 |
| SPA-SEC-T-15 | R4 §12 | Un usuario limitado a una sucursal no accede a expedientes de otra (si se adopta la alternativa 2) | CE-04 |

## 4.1 Riesgos registrados (10)

| ID | Riesgo | Fuente |
|---|---|---|
| SPA-SEC-R-01 | El administrador de empresa accede a todos los datos clínicos por defecto | R4 §2 |
| SPA-SEC-R-02 | No existe registro de lecturas exitosas de datos sensibles | R4 §2 |
| SPA-SEC-R-03 | No hay cifrado de archivos ni de campos clínicos; el respaldo no está cifrado y contiene el keyring | R4 §2 |
| SPA-SEC-R-04 | `LocalFileStorage` sobrescribe, borra físicamente y no valida la contención de la ruta | R4 §2 |
| SPA-SEC-R-05 | Conflicto entre la regla de no borrado físico y el derecho de eliminación | R4 §2 |
| SPA-SEC-R-06 | Requisitos legales de consentimiento, firma, datos de salud y menores sin validar por un profesional | R4 §2 |
| SPA-SEC-R-07 | Fecha de nacimiento y contacto de emergencia sin campo en BusinessPartner | R4 §2 |
| SPA-SEC-R-08 | Acceso entre sucursales sin política | R4 §2 |
| SPA-SEC-R-09 | Empleados no implementado: la identidad del profesional depende de un contrato propuesto | R4 §2 |
| SPA-SEC-R-10 | Respaldos y réplicas conservan datos que un titular pidió eliminar | R4 §2 |

## 4.2 Identificadores de prueba inferidos de R3 (22)

R3 escribe en cuatro párrafos «**Pruebas:** SPA-CARE-T-FAC-01 (…), -02 (…), …». **Solo el primer identificador de cada párrafo figura completo y literal** (`SPA-CARE-T-FAC-01`, `-COR-01`, `-CNS-01`, `-FOT-01`). Los 22 siguientes se **infirieron** como prefijo más sufijo y **no figuran literalmente** en R3; su descripción sí es la de R3. No se crearon identificadores nuevos: son los que la notación abreviada denota, y aquí se marcan como **inferidos**. Las correspondencias con otras reglas que R3 no cita literalmente se marcan **inferidas**.

| ID inferido | Referencia literal en R3 | Sección y línea de R3 | Prueba o regla relacionada |
|---|---|---|---|
| `SPA-CARE-T-FAC-02` (inferido) | `SPA-CARE-T-FAC-01 (…), … -02 (corregir conserva la versión anterior)` | §3.4 (línea 185) | **Inferida:** `SPA-INV-06` |
| `SPA-CARE-T-FAC-03` (inferido) | `SPA-CARE-T-FAC-01 (…), … -03 (el rótulo no indica diagnóstico médico)` | §3.4 (línea 185) | **Inferida:** `SPA-CARE-FAC-03` |
| `SPA-CARE-T-FAC-04` (inferido) | `SPA-CARE-T-FAC-01 (…), … -04 (fototipo admite solo I a VI o sin evaluar)` | §3.4 (línea 185) | **Inferida:** `SPA-CARE-FAC-04` |
| `SPA-CARE-T-FAC-05` (inferido) | `SPA-CARE-T-FAC-01 (…), … -05 (la decisión no crea citas ni consume sesiones)` | §3.4 (línea 185) | **Inferida:** `SPA-CARE-FAC-05` |
| `SPA-CARE-T-COR-02` (inferido) | `SPA-CARE-T-COR-01 (…), … -02 (una corrección conserva el conjunto anterior)` | §4.5 (línea 239) | **Inferida:** `SPA-INV-06` |
| `SPA-CARE-T-COR-03` (inferido) | `SPA-CARE-T-COR-01 (…), … -03 (las diferencias no se almacenan y coinciden con los conjuntos)` | §4.5 (línea 239) | **Inferida:** `SPA-CARE-COR-04` |
| `SPA-CARE-T-COR-04` (inferido) | `SPA-CARE-T-COR-01 (…), … -04 (las medidas respetan unidades y precisión oficial, SPA-T-08)` | §4.5 (línea 239) | Literal en R3: `SPA-T-08` (dentro del propio fragmento) |
| `SPA-CARE-T-COR-05` (inferido) | `SPA-CARE-T-COR-01 (…), … -05 («no medida» es distinto de cero)` | §4.5 (línea 239) | **Inferida:** `SPA-CARE-COR-05` |
| `SPA-CARE-T-COR-06` (inferido) | `SPA-CARE-T-COR-01 (…), … -06 (fechas como DateOnly)` | §4.5 (línea 239) | **Inferida:** `SPA-INV-10` |
| `SPA-CARE-T-CNS-02` (inferido) | `SPA-CARE-T-CNS-01 (…), … -02 (el texto de una versión no cambia)` | §5.6 (línea 303) | **Inferida:** `SPA-CARE-CNS-02` |
| `SPA-CARE-T-CNS-03` (inferido) | `SPA-CARE-T-CNS-01 (…), … -03 (un registro no se edita ni elimina)` | §5.6 (línea 303) | **Inferida:** `SPA-CARE-CNS-03` |
| `SPA-CARE-T-CNS-04` (inferido) | `SPA-CARE-T-CNS-01 (…), … -04 (un retiro bloquea usos futuros sin alterar atenciones pasadas)` | §5.6 (línea 303) | **Inferida:** `SPA-CARE-CNS-04` |
| `SPA-CARE-T-CNS-05` (inferido) | `SPA-CARE-T-CNS-01 (…), … -05 (la interrupción no marca la sesión como realizada completa)` | §5.6 (línea 303) | **Inferida:** `SPA-CARE-CNS-05` |
| `SPA-CARE-T-CNS-06` (inferido) | `SPA-CARE-T-CNS-01 (…), … -06 (una sesión sin consentimiento de procedimiento vigente no puede registrarse como realizada)` | §5.6 (línea 303) | **Inferida:** `SPA-CARE-CNS-06` |
| `SPA-CARE-T-CNS-07` (inferido) | `SPA-CARE-T-CNS-01 (…), … -07 (el uso académico no existe como funcionalidad hasta confirmarse)` | §5.6 (línea 303) | **Inferida:** `SPA-CARE-CNS-07` |
| `SPA-CARE-T-FOT-02` (inferido) | `SPA-CARE-T-FOT-01 (…), … -02 (la finalidad promocional exige su autorización, SPA-T-11)` | §6.5 (línea 358) | Literal en R3: `SPA-T-11` (dentro del propio fragmento) |
| `SPA-CARE-T-FOT-03` (inferido) | `SPA-CARE-T-FOT-01 (…), … -03 (no hay acceso sin permiso)` | §6.5 (línea 358) | **Inferida:** `SPA-CARE-FOT-02` |
| `SPA-CARE-T-FOT-04` (inferido) | `SPA-CARE-T-FOT-01 (…), … -04 (cada consulta queda registrada)` | §6.5 (línea 358) | **Inferida:** `SPA-CARE-FOT-06` |
| `SPA-CARE-T-FOT-05` (inferido) | `SPA-CARE-T-FOT-01 (…), … -05 (una fotografía no se elimina físicamente)` | §6.5 (línea 358) | **Inferida:** `SPA-CARE-FOT-07` |
| `SPA-CARE-T-FOT-06` (inferido) | `SPA-CARE-T-FOT-01 (…), … -06 (las fotografías no aparecen en listados ni en la agenda)` | §6.5 (línea 358) | **Inferida:** `SPA-CARE-FOT-05` |
| `SPA-CARE-T-FOT-07` (inferido) | `SPA-CARE-T-FOT-01 (…), … -07 (aislamiento por empresa)` | §6.5 (línea 358) | **Inferida:** `SPA-INV-10` |
| `SPA-CARE-T-FOT-08` (inferido) | `SPA-CARE-T-FOT-01 (…), … -08 (un retiro bloquea el uso futuro según la política aprobada, pendiente)` | §6.5 (línea 358) | **Inferida:** `SPA-CARE-FOT-08` |

## 5. Duplicaciones semánticas y reglas compatibles

Reglas de distintos documentos que expresan lo mismo. **No se fusionan ni se renumeran aquí**; la columna «Ancla» propone cuál usar como referencia al redactar el contrato.

| ID | Tema | Registros | Ancla propuesta |
|---|---|---|---|
| DUP-01 | Profesional responsable distinto del usuario registrador | `SPA-AR-03`, `SPA-SG-02`, `SPA-INV-03`, `SPA-VT-01`, `SPA-CARE-EXP-05`, `SPA-CARE-SEG-06`, `SPA-OPS-VTA-05`, `SPA-SEC-INV-08` | SPA-AR-03 |
| DUP-02 | No duplicar identidades (cliente y empleado) | `SPA-AR-01`, `SPA-AR-02`, `SPA-AL-02`, `SPA-INV-01`, `SPA-INV-02`, `SPA-FC-07`, `SPA-CARE-EXP-01`, `SPA-SEC-INV-11` | SPA-AR-01 y SPA-AR-02 |
| DUP-03 | Los permisos no derivan de Empleados ni de asignaciones | `SPA-SG-01`, `SPA-INV-04`, `SPA-CARE-SEG-01`, `SPA-CARE-SEG-02`, `SPA-SEC-INV-07` | SPA-SG-01 |
| DUP-04 | Autorizaciones fotográficas independientes | `SPA-CN-02`, `SPA-CN-03`, `SPA-CN-06`, `SPA-INV-05`, `SPA-CARE-CNS-01`, `SPA-CARE-FOT-03`, `SPA-SEC-INV-02`, `SPA-SEC-INV-10` | SPA-CN-06 |
| DUP-05 | Auditoría de accesos y lecturas | `SPA-AU-02`, `SPA-CARE-FOT-06`, `SPA-SEC-AUD-01`, `SPA-SEC-INV-01`, `SPA-SEC-INV-06` | SPA-SEC-AUD-01 |
| DUP-06 | Correcciones append-only con historial | `SPA-FC-08`, `SPA-INV-06`, `SPA-CARE-EXP-06`, `SPA-CARE-FAC-02`, `SPA-CARE-COR-03`, `SPA-SEC-RET-03`, `SPA-OPS-AGE-12` | SPA-FC-08 |
| DUP-07 | Sin borrado físico arbitrario | `SPA-CARE-EXP-08`, `SPA-CARE-CNS-03`, `SPA-CARE-FOT-07`, `SPA-SEC-RET-01`, `SPA-SEC-INV-04` | SPA-SEC-RET-01 |
| DUP-08 | Sesión prevista y sesión realizada | `SPA-AT-06`, `SPA-AT-07`, `SPA-INV-09`, `SPA-OPS-AGE-10` | SPA-AT-06 |
| DUP-09 | Plan de atención y paquete comercial | `SPA-AT-01`, `SPA-AT-05`, `SPA-PQ-01` | SPA-AT-05 |
| DUP-10 | Sin anticipos ni contabilidad paralela | `SPA-AR-05`, `SPA-INV-08`, `SPA-OPS-VTA-01`, `SPA-OPS-VTA-03`, `SPA-OPS-PKG-07` | SPA-OPS-VTA-03 |
| DUP-11 | Estándares de fechas y decimales | `SPA-FC-06`, `SPA-INV-10`, `SPA-CARE-COR-05` | SPA-INV-10 |
| DUP-12 | Insumos documentales frente a movimiento de inventario | `SPA-IN-02`, `SPA-OPS-SRV-07` | SPA-IN-02 |
| DUP-13 | Reglas de agenda: elementos, integridad y relación con Empleados | `SPA-AG-01`, `SPA-AG-02`, `SPA-AG-03`, `SPA-OPS-AGE-01`, `SPA-OPS-AGE-04`, `SPA-OPS-AGE-05`, `SPA-OPS-AGE-06` | SPA-AG-01 a AG-03 |
| DUP-14 | Servicios y catálogo de Items | `SPA-SV-01`, `SPA-SV-02`, `SPA-OPS-SRV-01`, `SPA-OPS-SRV-03`, `SPA-OPS-SRV-04` | SPA-SV-01 |
| DUP-15 | Acceso mínimo y fotografías fuera de listados | `SPA-SG-06`, `SPA-CARE-SEG-05`, `SPA-CARE-FOT-05` | SPA-SG-06 |
| DUP-16 | Fecha de nacimiento y contacto de emergencia | `SPA-FC-02`, `SPA-FC-07`, `SPA-CARE-EXP-01` | SPA-FC-07 |

Además, la cobertura de pruebas está duplicada: las `SPA-T-01` a `SPA-T-18` de R1 y las familias `SPA-OPS-T-*`, `SPA-CARE-T-*` y `SPA-SEC-T-*` verifican los mismos invariantes
(p. ej. `SPA-T-05`, `SPA-CARE-T-EXP-06` y `SPA-SEC-T-02` sobre `employees.view`).

### 5.1 Clasificación por tipo de decisión

Listas de identificadores (reglas e invariantes) según su origen; un registro puede figurar en más de un grupo.

| Grupo | Descripción | Registros |
|---|---|---|
| A | Decisiones aprobadas verificables (con ADR-050 o Empleados; incluye las parcialmente aprobadas) | 14: `SPA-AL-02`, `SPA-AR-02`, `SPA-AR-03`, `SPA-AR-04`, `SPA-CARE-EXP-05`, `SPA-CARE-SEG-06`, `SPA-INV-02`, `SPA-INV-03`, `SPA-INV-04`, `SPA-OPS-VTA-05`, `SPA-SEC-INV-08`, `SPA-SG-01`, `SPA-SG-02`, `SPA-VT-01` |
| B | Consecuencias derivadas de decisiones aprobadas | 10: `SPA-AL-02`, `SPA-AR-02`, `SPA-AR-04`, `SPA-CARE-SEG-01`, `SPA-CARE-SEG-02`, `SPA-INV-10`, `SPA-OPS-VTA-05`, `SPA-SEC-INV-07`, `SPA-SG-01`, `SPA-VT-01` |
| C | Requisitos respaldados por formularios originales (fuente externa, sin contraste directo) | 25: `SPA-AT-01`, `SPA-AT-02`, `SPA-AT-03`, `SPA-AT-04`, `SPA-CARE-CNS-01`, `SPA-CARE-FAC-03`, `SPA-CARE-FAC-04`, `SPA-CARE-FOT-11`, `SPA-CL-01`, `SPA-CN-01`, `SPA-CN-02`, `SPA-CN-03`, `SPA-CN-05`, `SPA-CN-06`, `SPA-FC-01`, `SPA-FC-02`, `SPA-FC-03`, `SPA-FC-04`, `SPA-FC-05`, `SPA-FC-06`, `SPA-FO-01`, `SPA-IN-01`, `SPA-INV-05`, `SPA-INV-07`, `SPA-SG-03` |
| D | Propuestas técnicas no aprobadas (compatibles con el ERP según las fuentes) | 118 registros (ver columna «Aprobación» de §3) |
| E | Alternativas de negocio pendientes (con etiqueta P) | 67 registros (ver columna «Evid.» de §3 y §10) |
| F | Riesgos legales y de privacidad | 89 registros más los de §4.1 |
| G | Contratos externos necesarios (Empleados, Ventas, CxC, Inventario, almacenamiento) | 57 registros (columna «Dep.» de §3) |

## 6. Matriz de conflictos

**Ningún conflicto se declara resuelto.** C-01 y C-02 son conflictos arquitectónicos **abiertos**; C-06 es una inconsistencia terminológica **abierta**. Las alternativas técnicas de R3 y R4 se conservan **sin aprobarse**.

**Contradicción real:** dos documentos afirman cosas incompatibles. **Inconsistencia terminológica:** el mismo concepto con modelos distintos. **Decisión pendiente / brecha:** no hay
contradicción, falta una decisión o una capacidad del ERP.

| C | Tema | Naturaleza | Registros implicados | Detalle y precedencia |
|---|---|---|---|---|
| C-01 | Administrador y permisos clínicos | Contradicción real parcial | `SPA-SG-01`, `SPA-SG-04`, `SPA-CARE-SEG-01`, `SPA-CARE-SEG-04`, `SPA-SEC-R-01`, `SPA-SEC-INV-07` | R3 `SPA-CARE-SEG-04` presenta la exclusión del Admin como alternativa B sin advertir que exige modificar el contrato LOCKED; R4 §3.1 y §3.3 lo demuestran con el código (el autorizador resuelve el bypass antes de la clave). **Precedencia: R4** (evidencia de código posterior). **Estado: ABIERTO.** Observación: ADR-050 §6 aprueba que Admin conserva su comportamiento oficial respecto de los permisos de Empleados, pero no decide nada sobre permisos clínicos de Spa. Sigue pendiente la política (O-01) y la alternativa de permisos «sellados» sigue sin aprobarse. |
| C-02 | Conservación y eliminación | Contradicción real entre propuestas y tensión normativa | `SPA-CARE-EXP-08`, `SPA-CARE-CNS-03`, `SPA-CARE-FOT-07`, `SPA-CARE-FOT-08`, `SPA-SEC-RET-01`, `SPA-SEC-RET-02`, `SPA-SEC-INV-04`, `SPA-SEC-R-05` | R3 `SPA-CARE-EXP-08` y `SPA-CARE-CNS-03` dicen que un expediente o consentimiento «nunca se elimina»; R4 `SPA-SEC-RET-01` y `RET-02` proponen un procedimiento de eliminación legal controlado. Además, la regla permanente de no borrado físico del propietario choca con el derecho de eliminación (R4 L-07). Ambas son propuestas: se resuelven con O-05 y asesoría legal (O-07). **Precedencia: R4 refina R3.** **Estado: ABIERTO**; ninguna alternativa de eliminación está aprobada. |
| C-03 | Auditoría y privacidad | Decisión pendiente (sin contradicción) | `SPA-AU-01`, `SPA-AU-02`, `SPA-CARE-FOT-06`, `SPA-SEC-AUD-01`, `SPA-SEC-AUD-07`, `SPA-SEC-INV-01`, `SPA-SEC-R-02` | No hay contradicción: R3 deja el registro de lectura como mecanismo inexistente; R4 §4 lo propone. Pendiente: el propio registro de accesos es dato personal y su retención (`SPA-SEC-AUD-07`) no está definida (O-03). |
| C-04 | Fotografías y consentimiento | Compatibles; decisión pendiente | `SPA-CN-02`, `SPA-CN-03`, `SPA-CN-06`, `SPA-INV-05`, `SPA-CARE-FOT-03`, `SPA-CARE-FOT-08`, `SPA-CARE-CNS-01`, `SPA-SEC-INV-02`, `SPA-SEC-INV-10` | Compatibles: R1 `SPA-CN-06`, R3 `SPA-CARE-FOT-03` y R4 `SPA-SEC-INV-02` coinciden en que la finalidad se verifica contra autorizaciones independientes. Pendiente: retiro y conservación (`SPA-CARE-FOT-08`). |
| C-05 | Plan de atención y paquete comercial | Decisión pendiente (sin contradicción) | `SPA-AT-01`, `SPA-AT-05`, `SPA-PQ-01` | No hay contradicción: R1 `SPA-AT-05` y R2 §4.2 coinciden en separar plan y paquete; la relación sigue pendiente (`SPA-PQ-01`). |
| C-06 | Sesión programada, realizada y consumida | Inconsistencia terminológica real | `SPA-AT-06`, `SPA-AT-07`, `SPA-OPS-AGE-10`, `SPA-CARE-CNS-05`, `SPA-INV-09` | R1 `SPA-AT-06` distingue sesión prevista de realizada, pero R2 §4.3 define la entidad `Session` como «realizada o programada» y además existe `Appointment` (cita); R3 `SPA-CARE-CNS-05` añade la sesión interrumpida. Hay que decidir si una sesión programada es una cita o una sesión (O-25). La distinción realizada/consumida (R2 §4.4) es propuesta de R2. **Estado: ABIERTO.** |
| C-07 | Agenda y asignaciones de Empleados | Compatibles; dependencia no implementada | `SPA-OPS-AGE-04`, `SPA-OPS-AGE-14`, `SPA-AR-06`, `SPA-AG-03` | Compatibles con ADR-050 §8 y EMP-BR-11 y EMP-BR-12: la disponibilidad usa la elegibilidad de Empleados y no los permisos de acceso. Dependencia en un contrato **no implementado** (D-01). |
| C-08 | Anticipos y capacidades de Ventas y CxC | Brecha de capacidad (sin contradicción entre documentos) | `SPA-AR-05`, `SPA-PQ-01`, `SPA-OPS-VTA-03`, `SPA-OPS-PKG-07`, `SPA-OPS-PKG-04` | R1 §8, R2 `SPA-OPS-VTA-03` y `SPA-OPS-PKG-07` coinciden en no crear anticipos dentro de Spa. La capacidad no existe en el ERP: Ventas y CxC solo cobran saldos de facturas a crédito (R2 §1). Se resuelve fuera de Spa (O-14). |
| C-09 | Catálogo de permisos candidatos solapados | Solapamiento de nombres (no contradicción) | `SPA-SG-04`, `SPA-CARE-SEG-03`, `SPA-SEC-R-01` | Los permisos candidatos de R2, R3 y R4 se solapan o usan nombres distintos para lo mismo (ver §6.1). No es una contradicción; hay que unificar el catálogo candidato. |
| C-10 | Fecha de nacimiento y contacto de emergencia | Compatibles (SECURITY amplía las alternativas de CARE-PRIVACY) | `SPA-FC-02`, `SPA-FC-07`, `SPA-CARE-EXP-01`, `SPA-SEC-R-07` | R3 §2.4 ofrece las alternativas A a C y R4 §8, A a E; son compatibles y R4 amplía a R3. Pendiente O-06. |

### 6.1 Permisos candidatos mencionados en R2, R3 y R4

Solo nombres candidatos; ninguno existe en el Kernel. Los que aparecen en más de un documento se solapan: `spa.consents.manage`, `spa.history.update`, `spa.photos.create`, `spa.photos.view`, `spa.records.export`, `spa.records.view`.
Unificar el catálogo es una tarea documental (no requiere decisión salvo en los nombres que expresan políticas aún pendientes, como el acceso del Admin).

| Permiso candidato | Aparece en |
|---|---|
| `spa.access-grants.manage` | R4 |
| `spa.access-log.view` | R4 |
| `spa.agenda.cancel` | R2 |
| `spa.agenda.create` | R2 |
| `spa.agenda.mark-absent` | R2 |
| `spa.agenda.update` | R2 |
| `spa.agenda.view` | R2 |
| `spa.assessments.create` | R3 |
| `spa.assessments.update` | R3 |
| `spa.audit.view` | R3 |
| `spa.availability.manage` | R2 |
| `spa.consents.manage` | R3, R4 |
| `spa.consents.templates` | R3 |
| `spa.erasure.execute` | R4 |
| `spa.erasure.request` | R4 |
| `spa.history.update` | R3, R4 |
| `spa.history.view` | R4 |
| `spa.packages.adjust` | R2 |
| `spa.packages.consume` | R2 |
| `spa.packages.manage` | R2 |
| `spa.packages.view` | R2 |
| `spa.photos.create` | R3, R4 |
| `spa.photos.view` | R3, R4 |
| `spa.records.emergency-access` | R4 |
| `spa.records.export` | R3, R4 |
| `spa.records.manage` | R3 |
| `spa.records.view` | R3, R4 |
| `spa.services.manage` | R2 |
| `spa.services.view` | R2 |
| `spa.sessions.create` | R3 |

## 7. Dependencias (`D-xx`)

| D | Dependencia |
|---|---|
| D-01 | Empleados no implementado; `IEmployeeDirectory` es propuesta (ADR-050, EMP-E1 a E6b) |
| D-02 | BusinessPartner / MasterData (FROZEN, ADR-017) |
| D-03 | Items naturaleza `Service` (items.md) |
| D-04 | Ventas (FROZEN) y facturación |
| D-05 | Cuentas por cobrar y cobros |
| D-06 | Inventario (ajustes; ADR-041) |
| D-07 | Identity / Access / Kernel (permisos; contrato LOCKED) |
| D-08 | Entity Audit (FROZEN) |
| D-09 | `IFileStorage` y almacenamiento |
| D-10 | `ICompanyClock`, ADR-034 y estándar numérico |
| D-11 | Asesoría legal (LOPDP, firma, menores) |
| D-12 | Decisión del propietario |
| D-13 | Sucursales (`Branch`, `CompanyUserBranch`) |

La asignación de dependencias por registro (columna «Dep.» de §3) es orientativa. **D-12** indica que el registro tiene puntos pendientes que decide el propietario.

## 8. Verificaciones pendientes (`V-xx`)

| V | Verificación |
|---|---|
| V-01 | Contraste directo del contenido [F] con las fichas originales de Gali Spa |
| V-02 | Verificar `IEmployeeDirectory` cuando exista (EMP-E1 a E6b) |
| V-03 | Verificar el trato de líneas `Service` en devoluciones de Ventas y la existencia de un evento de autorización de factura consumible |
| V-04 | Verificar escalas decimales y estándar numérico de mediciones (`data-standards.md`) |
| V-05 | Verificar horarios recurrentes con zona horaria y la restricción de exclusión para citas (patrón `accounting_periods`) |
| V-06 | Validación legal de LOPDP, firma, menores, imágenes y plazos (asesoría) |
| V-07 | Verificar con infraestructura cifrado de disco, publicación del directorio de archivos y claves |
| V-08 | Verificar el resolutor de `StorageObjectId` y el uso futuro de `IFileStorage` |
| V-09 | Confirmar vigencia de LOPDP y Reglamento en el Registro Oficial (reformas posteriores a enero de 2025) |

## 9. Decisiones históricas y su correspondencia

### 9.1 SPA-DEC-116

- **SPA-DEC-116 — Revisión y consolidación del alcance de Gali Spa V1.1.** Estado: **aprobada, según confirmación expresa del propietario.** Solo el título está confirmado; **el texto íntegro y el alcance literal no están recuperados** y la relación con decisiones anteriores está pendiente.
- Evidencia: declaración del propietario en la conversación del proyecto (R1 §21); no existe un documento del repositorio que la registre.

### 9.1.1 Las cuestiones `O-xx` no son decisiones históricas

Las **25 cuestiones `O-xx`** de §11 son preguntas **pendientes** formuladas por esta consolidación a partir de los pendientes de R1 a R4; **no son decisiones `SPA-DEC`**, no están aprobadas ni recuperadas del historial, y su numeración no guarda relación con la de `SPA-DEC-*`.

### 9.2 Matriz de correspondencia con el registro recuperado

| Registro histórico | Requisitos relacionados | Correspondencia verificable |
|---|---|---|
| `SPA-DEC-116` | El título sugiere relación con el alcance (`SPA-AL-01`, `SPA-AL-02`) y con la lista de pendientes (R1 §13) | **Ninguna verificable**; ningún requisito de esta matriz se deriva de ella |
| `SPA-DEC-001` a `SPA-DEC-115` | Ninguno | **No se declaran existentes, aprobadas ni recuperadas**; la numeración no se infiere |
| Decisiones aprobadas de ADR-050 (`DEC-E1`, `DEC-E4`, §6, §8, §13) | `SPA-AR-02`, `SPA-AR-03`, `SPA-AR-04`, `SPA-SG-01`, `SPA-SG-02`, `SPA-INV-02` a `INV-04`, `SPA-CARE-SEG-02`, `SPA-CARE-SEG-06`, `SPA-SEC-INV-08` | Verificable en el repositorio (único respaldo aprobado de Spa) |
| Recuerdo aproximado de unas 116 decisiones | Ninguno | Referencia de búsqueda y control, **no** prueba de 116 textos |

## 10. Evaluación de cierre documental

### 10.1 Reglas suficientemente definidas (9)

Aprobadas (literal o derivada) o existentes en el ERP, sin puntos pendientes: `SPA-CARE-SEG-01`, `SPA-CARE-SEG-02`, `SPA-INV-02`, `SPA-INV-03`, `SPA-INV-04`, `SPA-INV-10`, `SPA-SEC-INV-07`, `SPA-SEC-INV-12`, `SPA-SG-02`.

### 10.2 Reglas incompletas (67)

Con puntos pendientes de decisión o contrato (etiqueta P en la fuente): `SPA-AG-02`, `SPA-AG-03`, `SPA-AL-01`, `SPA-AR-01`, `SPA-AR-03`, `SPA-AR-05`, `SPA-AR-06`, `SPA-AT-01`, `SPA-AT-03`, `SPA-AT-04`, `SPA-AT-05`, `SPA-AT-07`, `SPA-AU-02`, `SPA-CARE-CNS-04`, `SPA-CARE-CNS-05`, `SPA-CARE-CNS-06`, `SPA-CARE-CNS-07`, `SPA-CARE-CNS-08`, `SPA-CARE-COR-06`, `SPA-CARE-COR-07`, `SPA-CARE-EXP-02`, `SPA-CARE-EXP-03`, `SPA-CARE-EXP-04`, `SPA-CARE-EXP-08`, `SPA-CARE-EXP-10`, `SPA-CARE-FAC-06`, `SPA-CARE-FAC-07`, `SPA-CARE-FOT-07`, `SPA-CARE-FOT-08`, `SPA-CARE-FOT-09`, `SPA-CARE-FOT-10`, `SPA-CARE-SEG-04`, `SPA-CL-01`, `SPA-CN-04`, `SPA-CN-05`, `SPA-CN-06`, `SPA-FC-02`, `SPA-FC-03`, `SPA-FC-06`, `SPA-FC-07`, `SPA-FC-08`, `SPA-IN-02`, `SPA-INV-09`, `SPA-OPS-AGE-02`, `SPA-OPS-AGE-04`, `SPA-OPS-AGE-05`, `SPA-OPS-AGE-06`, `SPA-OPS-AGE-07`, `SPA-OPS-AGE-08`, `SPA-OPS-AGE-09`, `SPA-OPS-AGE-11`, `SPA-OPS-AGE-14`, `SPA-OPS-PKG-05`, `SPA-OPS-PKG-06`, `SPA-OPS-SRV-02`, `SPA-OPS-SRV-05`, `SPA-OPS-SRV-06`, `SPA-OPS-SRV-07`, `SPA-OPS-VTA-03`, `SPA-OPS-VTA-05`, `SPA-OPS-VTA-06`, `SPA-PQ-01`, `SPA-SEC-AUD-07`, `SPA-SEC-RET-06`, `SPA-SG-01`, `SPA-SG-03`, `SPA-VT-01`.

### 10.3 Propuestas o requisitos de formulario sin puntos pendientes explícitos (106)

Quedan definidos como propuesta (T), fuente de formulario (F) o reconstrucción (R), **sin aprobación**: ver la columna «Aprobación» de §3.

### 10.4 Elementos que pueden cerrarse documentalmente sin nuevas decisiones del propietario

| K | Elemento | Observación |
|---|---|---|
| K-01 | Unificar las duplicaciones `DUP-01` a `DUP-16` en una redacción única por regla | No cambia el contenido; elige un ancla |
| K-02 | Unificar el catálogo candidato de permisos (§6.1) y resolver el solapamiento de nombres | Solo nombres; las políticas siguen pendientes |
| K-03 | Alinear la terminología de sesión, cita y consumo (C-06) dejando la decisión como alternativa explícita | La elección es de O-25 |
| K-04 | Unificar las familias de pruebas y enlazarlas a los invariantes | Sin aprobar pruebas |
| K-05 | Incorporar el texto de las verificaciones V-03 a V-05 y V-08 por lectura del repositorio | Son verificaciones de solo lectura, no decisiones |
| K-06 | Añadir los documentos de Spa al índice de `docs/decisions/README.md` | Requiere autorización documental |
| K-07 | Contrastar el contenido [F] con las fichas cuando estén disponibles (V-01) | Depende de acceso a las fuentes |
| K-08 | Consolidar los pendientes en el registro O-xx (§11) | Ya preparado aquí |

### 10.5 Elementos que no pueden considerarse aprobados

Todo registro cuya columna «Aprobación» no sea «Aprobada literal», «Aprobada derivada» o el núcleo aprobado de «Parcialmente aprobada»; todas las pruebas; todos los riesgos; todas las alternativas de
R2, R3 y R4; y el contenido de las fichas (F) como regla digital. De Spa solo está confirmado el **título** de `SPA-DEC-116`.

### 10.6 Riesgos de privacidad y seguridad pendientes (de R4)

El acceso del administrador, la ausencia de registro de lecturas, la falta de cifrado de archivos y campos, los riesgos de `LocalFileStorage`, la custodia de claves y respaldos, la
eliminación frente a la regla de no borrado, la validación legal y la fecha de nacimiento y el contacto de emergencia: ver `SPA-SEC-R-01` a `R-10` en §4.1.

## 11. Decisiones del propietario consolidadas (`O-xx`)

Son cuestiones **pendientes**, no decisiones tomadas ni decisiones históricas `SPA-DEC`. La lista unifica las de R2, R3 y R4.

| O | Decisión pendiente | Registros relacionados |
|---|---|---|
| O-01 | Política de acceso del administrador a datos clínicos y mecanismo (SECURITY §3) | SPA-SEC-R-01, SPA-CARE-SEG-04 |
| O-02 | Alcance de acceso entre sucursales (SECURITY §3.4; CARE-PRIVACY §7.3) | SPA-SEC-R-08 |
| O-03 | Modo de registro de lecturas y plazo de retención del registro (SECURITY §4) | SPA-SEC-AUD-07 |
| O-04 | Nivel de cifrado antes de datos reales, custodia de claves y respaldos cifrados (SECURITY §5) | SPA-SEC-R-03 |
| O-05 | Excepción a la regla de no borrado físico y procedimiento de eliminación legal (SECURITY §6) | SPA-SEC-R-05 |
| O-06 | Tratamiento de fecha de nacimiento, edad y contacto de emergencia (SECURITY §8) | SPA-SEC-R-07, SPA-FC-07 |
| O-07 | Asesoría legal sobre las interpretaciones L-INT-01 a L-INT-07 | SPA-SEC-R-06 |
| O-08 | Recursos bloqueantes de la agenda (OPERATIONS §2.5) | SPA-OPS-AGE-05 |
| O-09 | Citas contiguas, tolerancias, anticipación y confirmaciones | SPA-OPS-AGE-06, SPA-OPS-AGE-08 |
| O-10 | Atención sin cita previa | SPA-OPS-AGE-11 |
| O-11 | Cardinalidad entre servicio comercial y procedimiento (perfil 1:1 o catálogo N:M) | SPA-OPS-SRV-02 |
| O-12 | Tratamiento de insumos: documental, sugerido, salida manual o consumo automático | SPA-OPS-SRV-07, SPA-IN-02 |
| O-13 | Las 12 preguntas contractuales de paquetes | SPA-PQ-01 |
| O-14 | Capacidad de anticipos o prepagos de clientes fuera de Spa | SPA-OPS-VTA-03 |
| O-15 | Origen del contrato de paquete: explícito desde la factura o por evento de Ventas | SPA-OPS-PKG-04 |
| O-16 | Mecanismo de firma, versionado y conservación del consentimiento | SPA-CARE-CNS-02, SPA-CN-05 |
| O-17 | Uso académico: pertenece o no al producto V1.1 | SPA-CN-04, SPA-CARE-CNS-07 |
| O-18 | Consentimiento de menores y representantes | SPA-CARE-CNS-08 |
| O-19 | Número de ficha y significado del código del caso | SPA-CARE-EXP-02, SPA-CARE-EXP-03 |
| O-20 | Cardinalidad del expediente por cliente | SPA-CARE-EXP-04 |
| O-21 | Obligatoriedad real de los campos de las valoraciones | SPA-CARE-FAC-06, SPA-FC-03 |
| O-22 | Escalas numéricas de las mediciones, frecuencia y número de controles | SPA-CARE-COR-05 a COR-07 |
| O-23 | Texto íntegro de SPA-DEC-116 y registro histórico SPA-DEC-* | SPA-DEC-116 |
| O-24 | Versión vigente de cada formulario original | SPA-FC-01, SPA-CARE-FAC-07 |
| O-25 | Modelo de sesión programada, cita y sesión realizada (terminología) | SPA-AT-06, SPA-OPS-AGE-10 |

## 12. Trazabilidad y límites

- Las filas de §3 a §4.1 apuntan a las fuentes R1 a R4; la descripción completa está en cada fuente.
- Esta matriz no modifica R1 a R4, ADR-050, la especificación de Empleados, `STATUS.md`, `WORK_QUEUE.md` ni los índices.
- No autoriza implementación de Spa ni de Empleados, migraciones, cambios en módulos FROZEN ni cambios en Identity, Access, Kernel o almacenamiento.
- Las fichas originales de Gali Spa siguen sin inspección directa por Claude; el contenido [F] no tiene contraste.
- No hay asesoría jurídica ni certificación de cumplimiento.

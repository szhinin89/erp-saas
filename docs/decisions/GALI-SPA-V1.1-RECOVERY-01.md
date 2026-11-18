# Gali Spa V1.1 — Recuperación de planificación y decisiones (SPA-V1.1-RECOVERY-01)

**RECUPERACIÓN PROVISIONAL — NO ADR — NO CONTRATO IMPLEMENTABLE.**

Documento de preservación. No es un ADR, no es una especificación contractual y no es una SSOT. **Ninguna implementación de Spa está
autorizada**, ni se autoriza programación de Spa o de Empleados por este documento. Preserva la reconstrucción de la planificación de
Gali Spa V1.1 hecha el 18 de noviembre de 2026 para que no dependa de conversaciones anteriores, y servirá de base para un ADR, una
especificación contractual y un plan de fases posteriores (§13). Estado de entrega: [STATUS](../../STATUS.md); cola:
[WORK_QUEUE](../agent/WORK_QUEUE.md).

La auditoría documental previa (SPA-V1.1-DOC-AUDIT) comprobó que el repositorio no contenía documentación de Spa fuera de las
referencias de Empleados; este documento no sustituye decisiones de conversaciones anteriores que todavía no se han recuperado
literalmente.

**Versión:** RECOVERY-01 fusionado con el contrato de recuperación V01 (SPA-V1.1-RECOVERY-MERGE-01) y con el documento maestro
`GALI-SPA-V1.1-MASTER-RECOVERY-01` (SPA-V1.1-RECOVERY-MERGE-02, 2026-11-18). Este es el **único** documento de recuperación de Spa. La
fusión conserva los identificadores del provisional (`SPA-AR-*`, `SPA-FC-*`, `SPA-AT-*`, `SPA-CN-*`, `SPA-SG-*`), añade identificadores
nuevos solo para conceptos nuevos y registra en el §17 la equivalencia entre los códigos del provisional, los de V01 y los `GS-*` del
maestro. Los identificadores históricos `SPA-DEC-*` (§21) y los de pruebas `SPA-T-*` (§20) se mantienen aparte. No introduce decisiones
de negocio nuevas.

**Estado: RECUPERACIÓN PROVISIONAL — NO ADR — NO CONTRATO IMPLEMENTABLE. No constituye autorización de implementación y no declara
completas las decisiones históricas de Spa.**

## 0. Clasificación de evidencia

| Etiqueta | Significado |
|---|---|
| **[A] APROBADO LITERAL (ADR-050)** | Decisión aprobada y verificada en la documentación de Empleados del repositorio ([ADR-050](ADR-050-employees-v1-architecture.md), [especificación](EMPLOYEES-V1-CONTRACT-SPEC-01.md), [plan](../agent/EMPLOYEES-V1-IMPLEMENTATION-PLAN-01.md)) |
| **[A, derivado] APROBADO DERIVADO** | Consecuencia razonable de una decisión aprobada que el texto literal de ADR-050 no formula para Spa; cita la sección de la que se deriva |
| **[F] FUENTE EXTERNA PENDIENTE DE CONTRASTE: formularios de Gali Spa (no verificada por Claude)** | Requisito extraído de los documentos originales de Gali Spa, tal como lo transmite el propietario. Claude **no ha leído esos archivos**; el contraste directo queda pendiente (§12) |
| **[R] RECONSTRUIDO** | Tema tratado en la planificación previa, recuperado de la continuidad disponible, sin contrato original completo |
| **[P] PENDIENTE** | Detalle que no puede considerarse aprobado sin recuperar o tomar su decisión |
| **[T] PROPUESTA TÉCNICA** | Posible traducción a software, no aprobada |

Reglas de lectura:

1. Una propuesta técnica o un formulario físico **no se convierte automáticamente en decisión de producto**.
2. Los campos y preguntas de los formularios físicos **no son por sí mismos campos obligatorios del software**: qué es obligatorio,
   opcional o condicional en la versión digital es una decisión pendiente (§3, SPA-FC-03).
3. Ninguna etiqueta [F], [R] o [T] autoriza funcionalidad ni implementación.
4. **[A, derivado]** indica una consecuencia razonable de una decisión aprobada que el texto literal de ADR-050 no formula para Spa. Cada
   regla [A] cita la sección exacta de ADR-050 o de la especificación de Empleados; sin esa cita no es [A].
5. La referencia a «unas 116 decisiones originales» de la planificación de Spa es **un recuerdo aproximado del propietario, una
   referencia de búsqueda y control, no una cifra documental verificada ni una prueba de 116 textos recuperados**; no se declara
   recuperada ninguna parte proporcional de ellas (§16). Lo único confirmado es el título de SPA-DEC-116 (§21).
6. Correspondencia con el sistema de etiquetas del documento maestro: APR = [A] o [A, derivado] (según el caso; el maestro no
   distingue el derivado y esas atribuciones se corrigieron aquí); SRC = [F]; REC = [R]; TEC = [T]; PEN = [P]. Donde el maestro
   escribe «APR / TEC», este documento separa la parte aprobada de la propuesta.

## 1. Propósito

Recuperar y consolidar los requisitos, decisiones y documentos originales de Gali Spa V1.1 para incorporarlos posteriormente al
repositorio ERP ZH V1, sin eliminar información recuperada y sin inventar decisiones.

## 2. Arquitectura general recuperada

### SPA-AL-01 — Alcance funcional general [R] (nuevo; GS-ALC-01)

Gali Spa V1.1 es el ámbito funcional del ERP destinado a administrar la operación de un establecimiento de servicios estéticos. Se
identificaron como áreas de planificación: clientes y expedientes de atención; profesionales responsables; valoraciones faciales y
corporales; consentimientos informados; fotografías y seguimiento; servicios y tratamientos; planes de atención; agenda y reservas;
sesiones realizadas y pendientes; paquetes comerciales; integración con ventas y cobros; control de acceso y trazabilidad.

**Pendiente [P]:** la inclusión definitiva de cada área, sus límites y sus reglas comerciales requieren contrastar las decisiones
históricas (SPA-DEC, §21); esta lista identifica áreas y no las declara incluidas en V1.1.

### SPA-AL-02 — Reutilización del ERP [A, derivado / R / T] (nuevo; GS-ALC-02)

- **[A, derivado]** Spa no crea identidades laborales paralelas ni gestiona directamente la ficha, los períodos o las asignaciones de
  Empleados (ADR-050 §3 DEC-E1, §13; ver SPA-AR-02 y SPA-AR-04).
- **[R]** Spa no crea identidades de cliente paralelas ni gestiona directamente ámbitos de otros módulos oficiales (BusinessPartner,
  sucursales, usuarios, operaciones comerciales); es la orientación de la planificación y de la arquitectura modular del ERP, no una
  decisión de Spa con acta recuperada.
- **[T]** Los contratos concretos de integración que no consten aprobados siguen siendo propuestas técnicas.

### SPA-AR-01 — Identidad del cliente [R]

Gali Spa debe integrarse con la identidad oficial de clientes del ERP. El diseño deberá reutilizar BusinessPartner y su rol Customer,
evitando un segundo registro de identidad personal dentro de Spa. La identidad comercial del cliente permanece en el módulo oficial de
datos maestros; Spa administra únicamente su información específica [R].

**Pendiente [P]:** recuperar el flujo exacto previamente acordado para registrar una atención cuando el cliente todavía no existe en
el ERP.

### SPA-AR-02 — Profesionales y Empleados

Spa utiliza el módulo general de Empleados V1; no crea una entidad Employee paralela ni mantiene otro catálogo de empleados.

**Necesidad aprobada [A]** (ADR-050 §3 DEC-E1 y §13; especificación de Empleados EMP-BR-21 y §19; plan EMP-E6b): Empleados entrega el contrato mínimo de lectura `IEmployeeDirectory`
dentro de V1, y Spa lo reutiliza en lugar de crear otro registro laboral. Garantías aprobadas del contrato: solo lectura, ids estables y
nunca reutilizados, e inclusión de empleados inactivos para atenciones históricas.

**Contenido de la consulta [R]:** la planificación previa identificó que Spa necesita consultar identidad estable del empleado, empresa,
estado laboral por fecha, asignaciones laborales por sucursal, cargo, profesión y especialidades, usuario ERP vinculado e historial para
consultas retrospectivas.

**Firmas técnicas [T] — PROPUESTAS, no aprobadas:** `GetByIdAsync`, `ListAsync`, `IsActiveOnAsync`, `GetBranchAssignmentsAsync`,
`IsEligibleForBranchAsync`, `GetLinkedUserAsync` y el tipo `EmployeeSummary` figuran en la especificación de Empleados §19 como diseño
propuesto y pendiente de validación en EMP-E1 y EMP-E6b. Cargo y profesión pueden ser nulos (P-01 de Empleados); Spa no debe asumir
que existen.

**No duplicación [A, derivado]:** Spa no crea empleados duplicados ni administra altas, bajas, reincorporaciones ni asignaciones
laborales; esa administración pertenece a Empleados (ADR-050 §3 DEC-E1, §7 y §8; especificación EMP-BR-07 a EMP-BR-12). El contrato de
Empleados debe existir antes de integrar Spa (ADR-050 §13; plan de Empleados, reglas transversales y EMP-E6b).

### SPA-AR-03 — Profesional responsable y usuario registrador [A]

Respaldado por ADR-050 §3 DEC-E4 y §13. El profesional que realiza una atención y el usuario que la registra en el ERP son conceptos
diferentes; la atención debe permitir identificar ambos. Los profesionales que documenten atenciones deben utilizar usuarios ERP
autorizados. No debe confundirse quién realizó el tratamiento con quién registró, facturó o cobró.

**Pendiente [P]:** cómo se representan ambos en los registros de Spa (campos, obligatoriedad y validaciones); no existe especificación.

**Observación [T] (GS-EMP-03), no aprobada:** ambos identificadores pueden corresponder a la misma persona sin dejar de ser conceptos
distintos. ADR-050 no formula esta frase.

**Acceso ERP [A]** (ADR-050 §3 DEC-E4; GS-EMP-04): el profesional que documenta atenciones necesita un usuario ERP autorizado; la asignación
laboral a una sucursal no concede automáticamente permisos (ADR-050 §6 y §8); Spa conserva sus permisos funcionales (§13).

### SPA-AR-04 — Responsabilidades separadas

**Aprobado [A]** (ADR-050 §13): Spa reutiliza Employee y no crea otro registro laboral; Spa conserva sus **permisos clínicos, agenda,
horarios, sesiones y disponibilidad**; Empleados no implementa funcionalidad clínica ni lógica de Spa.

**Consecuencia derivada de la arquitectura de Empleados [A, derivado]:** Empleados administra la ficha laboral, los períodos y las
asignaciones de sucursales (ADR-050 §7 y §8); por tanto Spa no los modifica. El texto literal de ADR-050 no formula esta frase para Spa (referencias de Empleados: especificación EMP-BR-07 a EMP-BR-12).

**Recuperado de la planificación, sin aprobación formal [R]:** que Spa conserve además las responsabilidades de servicios y
tratamientos, seguimiento y la información de atención estética. Estos puntos **no están en ADR-050** y conservan estado de
recuperación hasta su aprobación.

### SPA-AR-05 — Integración con el ERP [R]

Durante la planificación se trataron las relaciones con Items, servicios, Ventas, facturación, cobros e inventario. Estas
integraciones deben aprovechar los módulos oficiales ya existentes.

**Pendiente [P]:** recuperar las reglas exactas sobre venta de paquetes, generación de cuentas por cobrar, anticipos, facturación por
sesión y consumo de productos. No se consideran cerradas en este documento.

### SPA-AR-06 — Elegibilidad histórica del profesional [T] (nuevo; GS-EMP-05)

La elegibilidad del profesional debe evaluarse con el contrato oficial de Empleados (`IEmployeeDirectory`, propuesta de especificación §19)
y las fechas relevantes de la operación. No debe alterarse el pasado porque un profesional haya terminado su relación laboral;
`IEmployeeDirectory` incluye inactivos para atenciones históricas [A, ADR-050 §13].

**Pendiente [P]:** política específica de Spa para reasignar citas cuando un empleado deja de estar disponible.

## 3. Expediente del cliente y valoración estética

### SPA-FC-01 — Fichas facial y corporal [F]

Los documentos originales contienen dos tipos diferenciados de valoración: facial y corporal. Ambos registran información común de
identificación, consulta, antecedentes relevantes, valoración inicial, plan de atención, seguimiento y consentimiento.

La existencia de formularios separados no exige automáticamente crear dos entidades independientes en la base de datos.

### SPA-FC-02 — Identificación del expediente [F]

Los formularios incluyen número de ficha, fecha de atención o ingreso, nombre de la persona atendida, edad y datos de contacto,
profesional responsable, motivo de consulta y, en algunas versiones, código del caso y contacto de emergencia. Los formularios incluyen
también teléfono y ciudad; los formularios extensos añaden identificación, fecha de nacimiento, dirección y correo.

**Traducción técnica pendiente [P]:** establecer qué campos proceden de BusinessPartner, cuáles se conservan como referencia histórica
y cómo se numera el expediente.

### SPA-FC-03 — Antecedentes relevantes [F]

La valoración inicial contempla preguntas sobre enfermedades o condiciones relevantes, medicamentos, alergias, embarazo o lactancia
cuando corresponda, cirugías y tratamientos anteriores, implantes y procedimientos en la zona, irritación, infección, heridas o
reacciones recientes, y antecedentes y hábitos relevantes según el procedimiento. Los formularios más extensos contienen información
adicional sobre salud y estilo de vida; los formularios antiguos incluyen además preguntas sobre tabaquismo, alcohol, actividad física,
depilación y ciclo menstrual.

**Pendiente [P]:** definir qué preguntas serán obligatorias, opcionales o condicionales en la versión digital y qué información
histórica se preservará. Que aparezcan en un formulario físico no las hace obligatorias en el software.

### SPA-FC-04 — Valoración facial [F]

Componentes recuperados: motivo de consulta; tipo o condición de piel (normal, seca, grasa o mixta); sensibilidad; fototipo cutáneo
(clasificación de Fitzpatrick I a VI); alteraciones observables
(manchas, acné, deshidratación); zona y tiempo de evolución; exposición solar y protección; observaciones del profesional; diagnóstico
médico previo, si existe; objetivo del tratamiento; procedimiento, productos y equipos previstos; precauciones; decisión de proceder,
adaptar o posponer.

La valoración estética no debe presentarse automáticamente como un diagnóstico médico. Las fichas actualizadas advierten esta
diferenciación; la documentación digital deberá respetarla.

### SPA-FC-05 — Valoración corporal [F]

Zona corporal a tratar; motivo de consulta; objetivos (relajación, reducción, tonificación u otros); hallazgos iniciales; procedimiento
previsto; equipo o aparatología; precauciones; decisión de proceder, adaptar o posponer.

### SPA-FC-06 — Mediciones corporales [F]

Los formularios contienen una tabla comparativa para: peso en kilogramos; brazo derecho e izquierdo; abdomen alto; abdomen medio (a la
altura del ombligo); abdomen bajo; cadera; muslo derecho e izquierdo; fecha de medición; observaciones o evolución. Las medidas
corporales se expresan en centímetros cuando corresponda. Se contempla comparar medidas iniciales, de control y finales.

**Pendiente [P]:** frecuencia de registro, correcciones y asociación exacta con una sesión o valoración. Cuando se digitalicen, los
valores numéricos y las fechas quedan sujetos a los estándares oficiales ([datos](../architecture/data-standards.md),
[ADR-034](ADR-034-temporal-contract-single-source.md)); esta nota no define el contrato. No se fijan aquí tipos ni escalas decimales
nuevos.

### SPA-FC-07 — Origen de los datos generales de la persona [T] (nuevo; GS-CLI-02)

**PROPUESTA, no aprobada:** obtener los datos generales (nombre, identificación, contacto) desde la proyección autorizada de
BusinessPartner y guardar en Spa únicamente la información específica de atención (SPA-AR-01, SPA-FC-02). La división exacta, y qué datos
se conservan como referencia histórica, están pendientes [P].

### SPA-FC-08 — Correcciones e historial del expediente [T] (nuevo; GS-EXP-05)

**PROPUESTA, no aprobada:** preservar la trazabilidad de correcciones (autor, fecha y motivo), de modo que las modificaciones posteriores
no oculten contenido histórico relevante. La política exacta de edición y cierre de expedientes sigue pendiente [P].

### SPA-CL-01 — Historial del cliente [F / T] (nuevo; GS-CLI-03)

Los documentos contemplan múltiples sesiones y seguimientos de una misma persona [F]. **Propuesta [T]:** definir un mecanismo para
consultar la evolución de sus atenciones sin mezclar registros de clientes diferentes.

**Pendiente [P]:** creación inicial de la ficha, múltiples expedientes por cliente, traslado entre sucursales y correcciones de
identificación.

## 4. Plan de atención y seguimiento

### SPA-AT-01 — Plan de atención estética [F]

Los documentos recuperados contemplan objetivo del tratamiento, procedimiento, productos y equipos, número previsto de sesiones,
intervalos sugeridos, cuidados, precauciones, posible derivación y decisión de proceder, adaptar o posponer. Esto demuestra que el
negocio utiliza planes de atención; no demuestra por sí solo que un plan estético deba ser igual a un paquete comercial comprado.

**Diferencia que debe conservarse en el diseño:** el plan de atención describe lo previsto para la persona; el paquete comercial, si
existe, representa una operación de venta y sus derechos de consumo. La relación exacta entre ambos **permanece pendiente [P]**.

### SPA-AT-02 — Seguimiento de tratamientos [F]

Los formularios registran por sesión: fecha; tratamiento o procedimiento realizado; productos utilizados; aparatología empleada; zona
tratada, cuando corresponda; evolución; observaciones; reacciones posteriores; próxima sesión sugerida; firma o identificación del
cliente y profesional, según el formulario. Se conserva una descripción de los resultados o de la evolución final.

### SPA-AT-03 — Comparación fotográfica [F]

Los documentos indican mantener condiciones comparables para fotografías de seguimiento: iluminación, distancia, ángulo y ausencia de
filtros que alteren la comparación. Los formularios utilizan referencias o códigos de fotografías.

**Pendiente [P]:** almacenamiento digital, formatos admitidos, vinculación a sesión, conservación, visibilidad, eliminación y
tratamiento de metadatos.

### SPA-AT-04 — Resultado final del tratamiento [F] (nuevo; procede de V01 AT-03)

Los formularios registran el resultado o la evolución del tratamiento y las observaciones finales. Formaliza como requisito propio el
último párrafo de SPA-AT-02 (descripción de resultados o evolución final), sin sustituirlo.

**Pendiente [P]:** cuándo se considera finalizado un tratamiento, quién lo registra y si admite correcciones; no está definido.

### SPA-AT-05 — Distinción conceptual entre plan de atención y paquete comercial [T] (nuevo; procede de V01 AT-04)

**PROPUESTA, no aprobada.** El plan estético (SPA-AT-01) y el paquete comercial (§7) deben estudiarse por separado: el primero describe la
atención prevista para la persona y el segundo podría representar derechos de consumo derivados de una compra. La relación exacta entre
ambos **no está recuperada como decisión aprobada [P]**.

### SPA-AT-06 — Sesión prevista frente a sesión realizada [T] (nuevo; GS-SES-02)

**PROPUESTA, no aprobada:** diferenciar una sesión programada de una atención efectivamente realizada. No debe registrarse una sesión
como realizada únicamente por haber alcanzado la fecha programada.

### SPA-AT-07 — Registro histórico de la atención [T] (nuevo; GS-SES-03)

**PROPUESTA, no aprobada:** una atención realizada conserva referencias estables a la persona, al profesional (`EmployeeId`), a la
sucursal y al tratamiento.

**Pendientes [P]:** estados oficiales de sesión, correcciones, anulaciones, ausencias, sesiones no consumidas y restricciones
comerciales.

## 5. Consentimiento informado y fotografías

### SPA-CN-01 — Consentimiento para el procedimiento [F]

Los documentos originales recogen que la persona recibe información sobre el procedimiento, riesgos, limitaciones y cuidados, y puede
hacer preguntas antes de decidir. Se registra aceptación o rechazo del procedimiento y firma.

### SPA-CN-02 — Fotografías para seguimiento [F]

Existe una autorización diferenciada para realizar fotografías destinadas al registro y seguimiento del tratamiento. No debe
confundirse con la autorización publicitaria.

### SPA-CN-03 — Fotografías para publicidad [F]

Los formularios actualizados incluyen autorización independiente para publicar fotografías en redes sociales o materiales
promocionales. Aceptar fotografías para seguimiento no equivale a aceptar su publicación.

### SPA-CN-04 — Uso académico [F; documento específico]

El expediente compacto de dos caras incluye una autorización adicional para utilizar datos codificados y fotografías en una tesis o
sustentación académica. Ese uso se distingue expresamente de la publicidad.

**Pendiente [P]:** confirmar si el uso académico pertenece al producto comercial Gali Spa V1.1 o únicamente al documento elaborado para
un caso académico. **No se incorpora automáticamente como funcionalidad comercial.**

### SPA-CN-05 — Evidencia y retiro de autorización [F / P]

Los formularios contienen fecha, lugar y firmas, y algunos contemplan que la persona pueda detener una sesión o retirar su autorización
para futuras sesiones.

**Pendiente [P]:** definir el contrato digital de firma, versionado de consentimientos, revocación, efectos sobre sesiones y
conservación histórica. Equivale a la evidencia digital «SPA-CN-06» de V01, que V01 marca como pendiente y que aquí se mantiene en este
mismo SPA-CN-05 para evitar duplicados.

**Contrato digital propuesto [T]** (GS-CNS-07; propuesta, no aprobada): la especificación futura deberá cubrir versión del consentimiento
presentado, procedimiento relacionado, identificación de quien consiente, fecha y contexto de aceptación, evidencia de firma,
autorizaciones independientes, cambios posteriores, retiro del consentimiento, conservación histórica y permisos de consulta y
exportación. No se fija el proveedor ni el mecanismo técnico de firma.

### SPA-CN-06 — Independencia de las autorizaciones [F] (nuevo; procede de V01 CN-04)

Las autorizaciones son independientes entre sí: la autorización de fotografías de seguimiento (SPA-CN-02), la de publicación promocional
(SPA-CN-03) y la de uso académico (SPA-CN-04) son autorizaciones diferenciadas, y el uso académico se distingue expresamente de la
publicidad. Autorizar fotografías de seguimiento no equivale a autorizar su publicación. Formaliza lo ya descrito en SPA-CN-02, SPA-CN-03 y SPA-CN-04 sin cambiar su contenido.

**Pendiente [P]:** cómo se representa cada autorización de forma independiente en la versión digital y cómo se revoca (ver SPA-CN-05).

Finalidades documentales diferenciadas de las fotografías [F] (GS-FOT-02): seguimiento confidencial y publicación promocional; además,
una finalidad académica en un documento particular, pendiente de confirmar para el producto (SPA-CN-04).

### SPA-FO-01 — Seguridad de imágenes [T] (nuevo; GS-FOT-03)

**PROPUESTA, no aprobada.** La especificación técnica deberá definir: almacenamiento; referencias al expediente y a la sesión; permisos
de lectura; acceso a originales; exportaciones; registro de accesos; retención; revocaciones; gestión de errores; y la prohibición de
interpretar la autorización de seguimiento como autorización promocional (SPA-CN-06). Hoy los formularios solo usan referencias o
códigos de fotografías (SPA-AT-03) [F].

## 6. Agenda, citas y disponibilidad

**Estado: [R] recuperado como área planificada; contrato [P] no recuperado.**

La continuidad disponible y ADR-050 §13 confirman que agenda, horarios, sesiones y disponibilidad son responsabilidades de Spa. No se
ha recuperado una decisión original suficientemente detallada para afirmar:

- Duración definitiva de las citas.
- Horarios de establecimiento y profesionales.
- Bloqueos de agenda.
- Tratamiento de reservas simultáneas.
- Reprogramaciones y cancelaciones.
- Ausencias.
- Confirmaciones o recordatorios.
- Reglas para seleccionar profesional y sucursal.
- Conflictos de reserva (profesional, sucursal o recurso ocupado).
- Distinción entre cita reservada y atención efectivamente realizada.
- Anticipación mínima o máxima, tolerancias, bloqueos y confirmaciones.

Estos puntos no deben transformarse silenciosamente en reglas nuevas. Los conflictos de reserva, las ausencias y la atención efectiva
son **temas pendientes [P], no decisiones aprobadas**; no existe aquí ninguna regla concreta sobre anticipación, tolerancias, bloqueos o
confirmaciones.

Origen aprobado [A, ADR-050 §13]: agenda, horarios y disponibilidad pertenecen a Spa. Las reglas operativas específicas no tienen acta
histórica recuperada.

### SPA-AG-01 — Elementos del contrato de agenda [T] (nuevo; GS-AGE-01)

**PROPUESTA, no aprobada:** la futura especificación deberá cubrir sucursal, fecha, hora, duración, servicio o tratamiento, cliente,
profesional, disponibilidad, conflictos de programación, estado de reserva, cancelación, reprogramación, ausencia y atención efectiva.

### SPA-AG-02 — Integridad de las reservas [T] (nuevo; GS-AGE-02)

**PROPUESTA, no aprobada:** especificar un criterio de concurrencia que impida resultados incompatibles al reservar simultáneamente
recursos que no pueden atender dos citas. **Pendiente [P]:** antes de aprobarlo hay que conocer los recursos bloqueantes efectivos, las
excepciones y los horarios de trabajo.

### SPA-AG-03 — Relación de la agenda con Empleados [T] (nuevo; GS-AGE-03)

**PROPUESTA, no aprobada:** la disponibilidad laboral no se confunde con los permisos de acceso ERP, y una asignación de sucursal
consultada desde Empleados no sustituye por sí sola las reglas de agenda (ADR-050 §6 y §8 sobre asignaciones y permisos; ver SPA-AR-06).

**Contratos todavía no recuperados [P]:** horarios, tolerancias, cancelaciones, reasignaciones y reglas para múltiples recursos por cita.

## 7. Servicios, tratamientos y paquetes

**Estado: [R] / [P].** La planificación anterior trató servicios, tratamientos, planes y paquetes. El ERP ya dispone de la naturaleza
`Service` en Items ([items.md](../architecture/items.md)), pero falta recuperar el contrato concreto de Spa respecto de:

- Servicio comercial frente a procedimiento realizado.
- Tratamientos individuales.
- Tratamientos compuestos.
- Paquetes de varias sesiones.
- Control de sesiones utilizadas y pendientes.
- Vencimientos o restricciones.
- Cambios y devoluciones.
- Relación con la venta y su comprobante.
- Consumos de productos e insumos.

La existencia de un plan de sesiones en las fichas físicas no acredita por sí sola reglas de venta de paquetes.

### SPA-SV-01 — Integración con el catálogo de Items [R / T] (nuevo; GS-SRV-01)

El ERP dispone de un catálogo de Items con naturaleza `Service` ([items.md](../architecture/items.md)) [R]. **Propuesta [T]:**
consolidar cómo Spa reutiliza ese catálogo para la comercialización de servicios y cómo identifica el procedimiento efectivamente
aplicado.

### SPA-SV-02 — Datos de servicio por documentar [T] (nuevo; GS-SRV-02)

**PROPUESTA, no aprobada:** nombre y clasificación; duración; sucursal; recursos necesarios; profesionales elegibles; insumos y equipos;
precio comercial; condiciones de venta; restricciones operativas; estado activo o inactivo; historial de cambios. **Ningún campo de esta
lista queda declarado obligatorio.**

### SPA-PQ-01 — Preguntas contractuales sobre paquetes y sesiones comerciales [P] (nuevo; §14 del maestro)

Contrato histórico no recuperado. Son cuestiones de especificación, **no respuestas aprobadas**; no se impone ninguna política comercial
por defecto.

| Tema | Pregunta de contrato |
|---|---|
| Venta | Qué origina un paquete contratado |
| Composición | Qué servicios y cantidades comprende |
| Uso | Cuándo una sesión se considera consumida |
| Saldo | Cómo calcular sesiones pendientes |
| Vigencia | Si existen fechas límite |
| Reprogramación | Si afecta al saldo |
| Ausencia | Si descuenta una sesión |
| Cancelación | Qué ocurre con el saldo |
| Devolución | Relación con documentos comerciales |
| Transferencia | Si puede cambiarse el beneficiario |
| Profesional | Si puede variar entre sesiones |
| Sucursal | Si puede utilizarse en varias sucursales |

## 8. Integración con Ventas, cobros e inventario

**Estado: [R] / [P].** Módulos existentes que deberán analizarse: Items y servicios, Ventas y comprobantes, Cuentas por cobrar, Cobros
y medios de pago, Inventario y movimientos. El objetivo de integración es evitar crear registros comerciales o contables paralelos
dentro de Spa.

**No se recuperaron todavía decisiones verificables** sobre anticipos de clientes, facturación anticipada de paquetes, devengo por
sesión, reembolsos o imputación de insumos. Ninguna de estas funcionalidades queda autorizada por este documento. (La auditoría previa
no encontró en `docs/architecture` ni en `docs/decisions` un modelo documentado de anticipos de clientes.)

**Aspectos de integración comercial pendientes [P]** (GS-VTA-02; ninguno está aprobado): venta de servicios individuales; venta de
paquetes; facturación y comprobantes; pagos parciales; anticipos o prepagos; saldos pendientes; devoluciones; cancelación de
operaciones; relación entre sesión atendida y venta; separación entre profesional, vendedor y cobrador. No se considera aprobada una
regla de anticipo de clientes por el solo hecho de que existan paquetes. Las operaciones económicas de Spa deberán respetar las
responsabilidades de Ventas y Cuentas por Cobrar y no se diseña una contabilidad paralela [R] (GS-VTA-01; ver SPA-AR-05).

### SPA-VT-01 — Separación entre profesional y usuario que opera [A, derivado] (nuevo; GS-VTA-03)

ADR-050 §13 establece que el cobro no se confunde con quien presta el servicio, y §3 DEC-E4 distingue al profesional responsable del
usuario que registró. Se deriva que no se confunde al profesional que presta el servicio con el usuario que registra u opera una
transacción comercial. El texto literal de ADR-050 no formula esta extensión a ventas y facturación; el detalle comercial
(vendedor, cobrador) sigue pendiente [P].

### SPA-IN-01 — Registro documental de insumos y equipos [F] (nuevo; GS-INS-01)

Los formularios registran los productos y los equipos utilizados durante los tratamientos (ver SPA-AT-02).

### SPA-IN-02 — Registro documental frente a movimiento real de inventario [T] (nuevo; GS-INS-02)

**PROPUESTA, no aprobada:** debe definirse si el registro de productos utilizados produce movimientos de inventario o solo es evidencia
de atención. **No se asumen** consumos automáticos, cantidades, costos ni movimientos de stock sin reglas aprobadas. Contrato [P].

### SPA-IN-03 — Aparatología [T] (nuevo; GS-INS-03)

**PROPUESTA, no aprobada:** la aparatología utilizada puede requerir identificación operacional, pero no queda aprobado un módulo
independiente de mantenimiento, activos o reservas de equipos.

## 9. Seguridad y confidencialidad

### SPA-SG-01 — Separación de permisos [A, con alcance acotado]

**Aprobado [A]** (ADR-050 §6 y §13): las asignaciones laborales nunca conceden permisos; los perfiles existentes no reciben automáticamente
los permisos de Empleados; Spa conserva sus permisos clínicos.

**Consecuencia derivada [A, derivado]:** la condición de Employee no concede automáticamente acceso a información clínica o estética de
Spa. El texto literal de ADR-050 habla de asignaciones laborales y permisos clínicos de Spa; no formula esta frase tal cual.

**Los permisos de Empleados (`employees.*`) y de Access (`access.*`) pertenecen a esos módulos y no se atribuyen a Spa.** Spa define
sus propios permisos funcionales, todavía **inexistentes y pendientes [P]**, mediante la infraestructura de seguridad oficial del ERP.
No existe catálogo de permisos de Spa.

### SPA-SG-02 — Identidad del responsable [A]

Respaldado por ADR-050 DEC-E4: debe distinguirse el empleado responsable de una atención del usuario ERP que efectuó el registro.

### SPA-SG-03 — Información sensible [F / P]

Las fichas contienen antecedentes de salud, información corporal, fotografías y consentimientos; la necesidad de confidencialidad está
explícita en los documentos originales.

**Pendiente [P]:** formalizar los permisos de lectura, edición, acceso a fotografías, exportación, auditoría, retención y corrección
del expediente, conforme a los mecanismos de seguridad oficiales. **No debe asumirse que `employees.view` autoriza consultar
expedientes de atención.**

### SPA-SG-04 — Permisos diferenciados de Spa [T] (nuevo; GS-SEG-03)

**PROPUESTA, no aprobada:** la especificación definirá autorizaciones diferenciadas para consultar expedientes, crear valoraciones,
editar información, registrar sesiones, acceder a fotografías, exportar información, gestionar consentimientos, administrar agenda,
gestionar servicios y consultar información comercial. **El catálogo definitivo de permisos no está aprobado** y no existe (SPA-SG-01).

### SPA-SG-05 — Aislamiento de datos [T] (nuevo; GS-SEG-04)

**PROPUESTA, no aprobada:** verificar aislamiento de tenant, empresa y sucursal donde corresponda, sin confundir visibilidad operativa
con autorizaciones de usuarios (ver las reglas oficiales de [seguridad](../architecture/security.md)).

### SPA-SG-06 — Acceso mínimo en proyecciones [T] (nuevo; GS-SEG-05)

**PROPUESTA, no aprobada:** las proyecciones de agenda y de atención exponen únicamente la información necesaria para cada operación
autorizada. `employees.view` no es permiso suficiente para consultar un expediente de Spa (SPA-SG-03).

### SPA-AU-01 — Auditoría con los mecanismos oficiales [T] (nuevo; GS-AUD-02)

**PROPUESTA, no aprobada:** el contrato técnico deberá usar los mecanismos oficiales del ERP ([auditoría](../architecture/audit-infrastructure.md))
para registrar operaciones relevantes, sin construir un sistema de auditoría paralelo. La distinción entre profesional responsable y
usuario registrador ya es aprobada (SPA-SG-02).

### SPA-AU-02 — Operaciones a auditar [T] (nuevo; GS-AUD-03)

**PROPUESTA, no aprobada:** creación de expediente; modificación de valoración; registro o corrección de sesión; aceptación o retiro
de consentimiento; cambios de agenda; consulta o exportación de imágenes, cuando corresponda; cambios en asignaciones operativas de
atención. **Pendiente [P]:** política de auditoría, retención y acceso conforme a las reglas oficiales del ERP y la normativa aplicable.

## 10. Modelo conceptual por consolidar [T]

Las siguientes agrupaciones son **candidatas de diseño [T]**, no entidades aprobadas:

| Grupo conceptual | Información procedente de fuentes o planificación |
|---|---|
| Cliente Spa | Referencia a BusinessPartner/Customer |
| Expediente estético | Ficha, cliente, antecedentes y evolución |
| Valoración facial | Condición de piel, hallazgos, objetivos y precauciones |
| Valoración corporal | Zonas, objetivos, hallazgos y mediciones |
| Plan de atención | Procedimientos y sesiones previstas |
| Sesión realizada | Fecha, procedimiento, profesional, observaciones |
| Consentimiento | Procedimiento autorizado, fecha, firma y alcance |
| Fotografía | Referencia, finalidad, autorización y seguimiento |
| Agenda | Cita, profesional, sucursal y disponibilidad |
| Paquete comercial | Venta y eventual saldo de sesiones |
| Profesional | Employee existente (Empleados V1) |
| Sucursal | Branch existente |
| Medición | Observaciones corporales comparables |
| Servicio Spa | Relación con los servicios comerciales de Items |
| Consumo de paquete | Utilización de sesiones, pendiente |
| Venta y cobro | Módulos comerciales oficiales (Ventas, Cuentas por cobrar) |

La separación definitiva en tablas, agregados y servicios requiere recuperar o aprobar su contrato específico. No se crearán
duplicados de Employee, BusinessPartner, permisos, facturas o cuentas por cobrar.

### Invariantes candidatos (SPA-INV-01 a SPA-INV-10) [T]

Son una matriz de consolidación: cada uno debe trazarse a una decisión oficial o aprobarse expresamente antes de elevarse a contrato
implementable. La clasificación de evidencia de cada uno es la siguiente.

| ID | Invariante candidato | Clasificación de evidencia y fuente |
|---|---|---|
| SPA-INV-01 | No duplicar identidades de clientes | [R] (SPA-AR-01); no hay acta de Spa |
| SPA-INV-02 | No duplicar Employee | [A] (ADR-050 §3 DEC-E1; §13) |
| SPA-INV-03 | No confundir usuario registrador con profesional responsable | [A] (ADR-050 §3 DEC-E4; §13) |
| SPA-INV-04 | No derivar permisos de una asignación laboral | [A] (ADR-050 §6: las asignaciones laborales nunca conceden permisos; §8) |
| SPA-INV-05 | No publicar imágenes sobre la base exclusiva del consentimiento de seguimiento | [F] (SPA-CN-03, SPA-CN-06); fuente externa pendiente de contraste |
| SPA-INV-06 | Preservar trazabilidad histórica | [T] (SPA-FC-08, SPA-AU-01) |
| SPA-INV-07 | Proteger datos sensibles mediante autorizaciones específicas | Naturaleza de los datos [F] (SPA-SG-03); control [T] (SPA-SG-04) |
| SPA-INV-08 | No crear operaciones comerciales o contables paralelas | [R] (SPA-AR-05, SPA-AL-02) |
| SPA-INV-09 | Verificar coherencia entre citas, sesiones y documentos comerciales | [P]; contrato no recuperado |
| SPA-INV-10 | Aplicar estándares oficiales de fechas, decimales, aislamiento y auditoría | [A, derivado] de las normas generales del repositorio ([datos](../architecture/data-standards.md), [ADR-034](ADR-034-temporal-contract-single-source.md), [seguridad](../architecture/security.md), [auditoría](../architecture/audit-infrastructure.md)); no es una decisión de Spa |

## 11. Matriz de conservación de decisiones

| Código | Evidencia | Situación |
|---|---|---|
| SPA-AR-01 | Planificación anterior | [R] recuperado parcialmente |
| SPA-AR-02 | ADR-050 / especificación de Empleados | Necesidad [A]; firmas [T] propuestas; contenido [R] |
| SPA-AR-03 | ADR-050 DEC-E4 | [A] |
| SPA-AR-04 | ADR-050 §13 | Núcleo [A]; servicios, tratamientos e información estética [R] |
| SPA-AR-05 | Planificación anterior | [R] recuperado parcialmente |
| SPA-FC-01 a FC-06 | Fichas originales de Gali Spa | [F], sin verificación directa por Claude |
| SPA-AT-01 a AT-03 | Fichas y expediente compacto | [F], sin verificación directa por Claude |
| SPA-CN-01 a CN-04 | Formularios de consentimiento | [F], sin verificación directa por Claude |
| SPA-CN-05 | Formularios / contrato digital pendiente | [F] / [P] |
| SPA-AT-04 (nuevo) | V01 AT-03; último párrafo de SPA-AT-02 | [F], sin verificación directa por Claude; criterios [P] |
| SPA-AT-05 (nuevo) | V01 AT-04; SPA-AT-01 | [T] propuesta no aprobada; relación [P] |
| SPA-CN-06 (nuevo) | V01 CN-04; SPA-CN-02 a CN-04 | [F], sin verificación directa por Claude; representación digital [P] |
| SPA-AL-01 (nuevo) | Maestro GS-ALC-01 | [R]; límites [P] |
| SPA-AL-02 (nuevo) | Maestro GS-ALC-02; ADR-050 §3 DEC-E1, §13 | Parte Empleados [A, derivado]; resto [R]; contratos [T] |
| SPA-AR-06 (nuevo) | Maestro GS-EMP-05 | [T]; política de reasignación [P] |
| SPA-FC-07, FC-08 (nuevos) | Maestro GS-CLI-02, GS-EXP-05 | [T] propuestas; detalle [P] |
| SPA-CL-01 (nuevo) | Maestro GS-CLI-03; fichas | [F] / [T]; mecanismo [P] |
| SPA-AT-06, AT-07 (nuevos) | Maestro GS-SES-02, GS-SES-03 | [T] propuestas; estados [P] |
| SPA-FO-01 (nuevo) | Maestro GS-FOT-03 | [T] propuesta; contrato [P] |
| SPA-AG-01 a AG-03 (nuevos) | Maestro GS-AGE-01 a 03; ADR-050 §13 | Área [A] (ADR-050 §13); contrato [T] / [P] |
| SPA-SV-01, SV-02 (nuevos) | Maestro GS-SRV-01, 02 | [R] / [T]; sin campos obligatorios |
| SPA-PQ-01 (nuevo) | Maestro §14 | [P]; preguntas, sin respuestas |
| SPA-VT-01 (nuevo) | ADR-050 §3 DEC-E4, §13 | [A, derivado]; detalle comercial [P] |
| SPA-IN-01 a IN-03 (nuevos) | Maestro GS-INS-01 a 03 | [F] / [T]; contrato [P] |
| SPA-SG-01 y SG-02 | ADR-050 | [A] (SG-01 con parte [A, derivado]) |
| SPA-SG-03 | Naturaleza de los datos y fichas | [F] / [P]; política pendiente |
| SPA-SG-04 a SG-06 (nuevos) | Maestro GS-SEG-03 a 05 | [T]; catálogo de permisos inexistente |
| SPA-AU-01, AU-02 (nuevos) | Maestro GS-AUD-02, 03 | [T]; política [P] |
| SPA-INV-01 a INV-10 (nuevos) | Maestro §19 | Clasificación individual en el §10 |
| SPA-API-01, SPA-UX-01, SPA-T-01 a T-18 | Maestro §20, §21, §22 | [T] candidatos, no aprobados (§18 a §20) |
| SPA-DEC-116 | Confirmación expresa del propietario | Título [aprobado por el propietario]; texto íntegro [P] (§21) |
| Agenda y disponibilidad | Planificación anterior / ADR-050 | [R]; contrato [P] |
| Paquetes, sesiones comerciales y cobros | Planificación anterior | [R] / [P]; contrato pendiente |
| API, permisos específicos y pruebas | Sin documento recuperado | [P] |

## 12. Fuentes utilizadas

**A. Documentos oficiales existentes en el repositorio (verificados):**

1. [ADR-050](ADR-050-employees-v1-architecture.md).
2. [Especificación de Empleados](EMPLOYEES-V1-CONTRACT-SPEC-01.md).
3. [Plan de Empleados](../agent/EMPLOYEES-V1-IMPLEMENTATION-PLAN-01.md).

Para las normas generales citadas: [datos](../architecture/data-standards.md), [ADR-034](ADR-034-temporal-contract-single-source.md),
[seguridad](../architecture/security.md), [auditoría](../architecture/audit-infrastructure.md) e [items.md](../architecture/items.md).

**B. Formularios originales de Gali Spa disponibles en el proyecto ChatGPT del propietario, NO verificados por Claude** (no están en el repositorio ni en los
directorios de trabajo; su contenido [F] fue comunicado y recuperado en el proyecto ChatGPT del propietario, se transmite tal como lo describió y debe
contrastarse directamente después; Claude no las ha inspeccionado):

5. `Gali_Spa_Fichas_Actualizadas.docx`.
6. `Gali_Spa_Fichas_Clinicas_y_Consentimiento.docx`.
7. `Gali_Spa_Ficha_Compacta_Dos_Caras.pdf`.
8. `Gali_Spa_Facial_Actualizada.pdf`.
9. `Gali_Spa_Corporal_Actualizada.pdf`.
10. Otras variantes de las fichas y consentimientos.

Las versiones de los formularios no se consideran automáticamente equivalentes y se conservarán con su procedencia. Antes de fijar el
contrato digital debe identificarse cuál versión sustituye a cuál.

**C. Informes y reconstrucciones de sesiones de trabajo (no son documentos versionados del repositorio):**

11. Informe SPA-V1.1-DOC-AUDIT (auditoría documental de Spa).
12. Informes EMP-E1 y EMP-E2-PREP (verificaciones técnicas de Empleados).
13. Contrato de recuperación V01 y documento maestro `GALI-SPA-V1.1-MASTER-RECOVERY-01` (reconstrucciones aportadas por el propietario;
    fusionados en este documento, que es el único documento de recuperación).
14. Confirmación expresa del propietario sobre SPA-DEC-116 (§21).

## 13. Trabajo documental pendiente

Se necesita recuperar especialmente:

1. Decisiones exactas de agenda, horarios, disponibilidad y reservas.
2. Servicios y tratamientos; relación con Items.
3. Paquetes y control comercial de sesiones.
4. Integración con facturación, cobros y anticipos.
5. Modelo de expediente y sus reglas de modificación.
6. Privacidad y permisos por información sensible.
7. Contratos definitivos de datos, API y frontend.
8. Pruebas, invariantes y criterios de aceptación.
9. Exclusiones aprobadas de Gali Spa V1.1.
10. Contraste directo de las fuentes originales (§12) y flujo de cliente inexistente (SPA-AR-01).
11. Recuperación de las decisiones históricas `SPA-DEC-*` de la planificación de Spa. Solo el título de SPA-DEC-116 está confirmado
    (§21); el propietario recuerda, de forma aproximada, unas 116 decisiones, **un recuerdo aproximado y no una cifra documental
    verificada** (§16).

**Áreas con evidencia suficiente para preservar requisitos documentales:** integración fundamental con Empleados; separación entre
profesional y usuario registrador; identificación de formularios; antecedentes; valoración facial y corporal; mediciones; plan de
atención físico; seguimiento de procedimientos; consentimientos; finalidades diferenciadas de las fotografías.

**Áreas sin contrato histórico completo recuperado:** agenda y sus reglas detalladas; servicios y tratamientos comercializables;
paquetes y consumo de sesiones; anticipos, facturación y cobros; políticas de cancelación y reprogramación; modelo definitivo de
persistencia; permisos específicos de expedientes; protección y conservación de información sensible; API y estados operativos;
frontend definitivo; exclusiones originales de V1.1. No se declara que estas áreas fueron aprobadas con una regla concreta sin
evidencia.

Este documento debe evolucionar hacia un ADR, una especificación contractual y un plan de fases, con trazabilidad hacia las fuentes.
**No se declara completa la planificación de Spa mientras existan decisiones históricas sin recuperar.** No se autoriza programación de
Spa ni de Empleados.

## 14. Condición de cierre de la consolidación

Gali Spa V1.1 estará documentalmente consolidado cuando cada decisión recuperada tenga: identificador estable, evidencia de origen,
estado de aprobación, comportamiento funcional preciso, integraciones, validaciones y restricciones, seguridad, criterios de
aceptación, y exclusiones y límites. La documentación debe ser suficiente para continuar el proyecto sin depender de conversaciones
anteriores.

## 15. Trazabilidad y control de cambios

Cada regla que se consolide a partir de este documento debe conservar los siguientes campos (aún no completos para ninguna regla de
Spa):

| Campo | Contenido |
|---|---|
| Código estable | `SPA-xx-nn`; no se reutiliza ni se renumera |
| Estado de evidencia | [A], [A, derivado], [F], [R], [P] o [T] |
| Fuente concreta | Sección de ADR-050 o de la especificación de Empleados; ficha o formulario original; o planificación recuperada |
| Regla funcional | Comportamiento exacto |
| Dependencias de otros módulos | BusinessPartner, Empleados, Items, Ventas, Cobros y CxC, Inventario, Access |
| Validaciones | Obligatoriedad, formatos y límites |
| Permisos | Los propios de Spa, aún por definir |
| Pruebas requeridas | Casos de aceptación |
| Estado de aprobación | Aprobada por el propietario o no |

Ninguna regla reconstruida [R], propuesta [T] o pendiente [P] se promueve automáticamente a aprobada. Un cambio de estado de evidencia o
la renumeración de un código se registra en este documento.

## 16. Exclusiones y límites documentales

Este documento **no autoriza**:

- La implementación de Gali Spa.
- La implementación de Empleados.
- Migraciones o modificaciones de bases de datos.
- Nuevas funcionalidades comerciales.
- Cambios en módulos FROZEN.
- Crear rutas de API ni pantallas.
- Elevar este documento a ADR definitivo mientras existan contratos esenciales no recuperados o sin aprobación verificable.
- Reemplazar las decisiones históricas `SPA-DEC-*` ni el texto íntegro de SPA-DEC-116.

Tampoco declara recuperadas las decisiones originales de la planificación de Spa. El propietario recuerda, de forma aproximada, unas 116
decisiones originales: es **un recuerdo aproximado, no una cifra documental verificada** y una referencia de búsqueda y control, y de él
no se infiere qué parte está recuperada. Las exclusiones propias de Gali Spa V1.1 (qué queda fuera de la versión) **no se han recuperado** y siguen pendientes [P].

## 17. Equivalencias de identificadores (RECOVERY-01 ↔ V01 ↔ maestro `GS-*`)

Los identificadores de la primera columna son los **vigentes**; V01 y `GS-*` son referencias de procedencia y no sustituyen ninguno. Los
identificadores históricos `SPA-DEC-*` (§21) y los de pruebas `SPA-T-*` (§20) se mantienen aparte. «—» indica que esa fuente no tiene un
código equivalente.

| RECOVERY-01 (vigente) | V01 | Maestro `GS-*` | Observación |
|---|---|---|---|
| SPA-AL-01 (nuevo) | — | GS-ALC-01 | Alcance funcional general |
| SPA-AL-02 (nuevo) | — | GS-ALC-02 | Reutilización del ERP; etiquetas corregidas |
| SPA-AR-01 | SPA-AR-01 | GS-CLI-01 | Identidad del cliente |
| SPA-AR-02 | SPA-AR-02, SPA-AR-03 | GS-EMP-01, GS-EMP-02 | Reutilización de Employee; necesidad de `IEmployeeDirectory` y firma [T] |
| SPA-AR-03 | SPA-AR-04 | GS-EMP-03, GS-EMP-04 | Profesional responsable, usuario registrador y acceso ERP |
| SPA-AR-04 | SPA-AR-02 (parte), SPA-AR-06 | GS-ALC-02 (parte), §12 maestro (origen ADR-050 §13) | Responsabilidades separadas |
| SPA-AR-05 | — | GS-VTA-01 | Integración con el ERP; reglas pendientes |
| SPA-AR-06 (nuevo) | — | GS-EMP-05 | Elegibilidad histórica del profesional |
| SPA-FC-01 | SPA-FC-01 (parte) | GS-EXP-01 | Fichas facial y corporal |
| SPA-FC-02 | SPA-FC-01 | GS-CLI-02 | Identificación del expediente |
| SPA-FC-03 | SPA-FC-02 | GS-EXP-02 | Antecedentes, hábitos y estilo de vida |
| SPA-FC-04 | SPA-FC-03 | GS-FAC-01, GS-EXP-04 | Valoración facial; carácter no médico |
| SPA-FC-05 | SPA-FC-04 | GS-COR-01 | Valoración corporal |
| SPA-FC-06 | SPA-FC-05 | GS-COR-02, GS-COR-03 | Mediciones; estándares oficiales |
| SPA-FC-07 (nuevo) | — | GS-CLI-02 (regla propuesta) | Origen de los datos generales |
| SPA-FC-08 (nuevo) | — | GS-EXP-05 | Correcciones e historial del expediente |
| SPA-CL-01 (nuevo) | — | GS-CLI-03 | Historial del cliente |
| SPA-AT-01 | SPA-AT-01 | GS-PLA-01 | Plan de atención |
| SPA-AT-02 | SPA-AT-02 | GS-FAC-02, GS-SES-01, GS-EXP-03 | Seguimiento por sesión |
| SPA-AT-03 | — | GS-FAC-03, GS-FOT-01 | Comparación fotográfica |
| SPA-AT-04 (nuevo) | SPA-AT-03 | GS-EXP-03 (resultado final) | Resultado final del tratamiento |
| SPA-AT-05 (nuevo) | SPA-AT-04 | GS-PLA-02 | Plan frente a paquete; [T] |
| SPA-AT-06 (nuevo) | — | GS-SES-02 | Sesión prevista frente a realizada |
| SPA-AT-07 (nuevo) | — | GS-SES-03 | Registro histórico de la atención |
| SPA-CN-01 | SPA-CN-01 | GS-CNS-01 | Consentimiento para el procedimiento |
| SPA-CN-02 | SPA-CN-02 | GS-CNS-02, GS-FOT-02 | Fotografías de seguimiento |
| SPA-CN-03 | SPA-CN-03 | GS-CNS-03 | Uso promocional |
| SPA-CN-04 | SPA-CN-05 | GS-CNS-05 | Uso académico |
| SPA-CN-05 | SPA-CN-06 | GS-CNS-06, GS-CNS-07 | Evidencia digital; detención y retiro |
| SPA-CN-06 (nuevo) | SPA-CN-04 | GS-CNS-04, GS-FOT-02 | Independencia de autorizaciones |
| SPA-FO-01 (nuevo) | — | GS-FOT-03 | Seguridad de imágenes |
| SPA-AG-01 (nuevo) | — | GS-AGE-01 | Elementos de agenda |
| SPA-AG-02 (nuevo) | — | GS-AGE-02 | Integridad de reservas |
| SPA-AG-03 (nuevo) | — | GS-AGE-03 | Relación con Empleados |
| SPA-SV-01 (nuevo) | — | GS-SRV-01 | Integración con Items |
| SPA-SV-02 (nuevo) | — | GS-SRV-02 | Datos de servicio por documentar |
| SPA-PQ-01 (nuevo) | — | §14 del maestro | Preguntas sobre paquetes |
| §8 (aspectos pendientes) | — | GS-VTA-02 | Lista de pendientes comerciales |
| SPA-VT-01 (nuevo) | — | GS-VTA-03 | Profesional frente a usuario que opera; [A, derivado] |
| SPA-IN-01 (nuevo) | — | GS-INS-01 | Insumos y equipos documentados |
| SPA-IN-02 (nuevo) | — | GS-INS-02 | Documental frente a movimiento de inventario |
| SPA-IN-03 (nuevo) | — | GS-INS-03 | Aparatología |
| SPA-SG-01 | SPA-AR-05 | GS-SEG-01 | Separación de permisos |
| SPA-SG-02 | SPA-AR-04 | GS-AUD-01 | Identidad del responsable |
| SPA-SG-03 | §9 de V01 | GS-SEG-02 | Información sensible |
| SPA-SG-04 (nuevo) | — | GS-SEG-03 | Permisos diferenciados de Spa |
| SPA-SG-05 (nuevo) | — | GS-SEG-04 | Aislamiento |
| SPA-SG-06 (nuevo) | — | GS-SEG-05 | Acceso mínimo |
| SPA-AU-01 (nuevo) | — | GS-AUD-02 | Auditoría oficial |
| SPA-AU-02 (nuevo) | — | GS-AUD-03 | Operaciones a auditar |
| SPA-INV-01 a SPA-INV-10 (nuevos) | — | Invariantes del maestro §19 | Ver §10 |
| SPA-API-01 (nuevo) | — | §20 del maestro | Ver §18 |
| SPA-UX-01 (nuevo) | — | §21 del maestro | Ver §19 |
| §6 a §8 | V01 §6 a §8 | §12 a §15 del maestro | Agenda, servicios y paquetes, integración comercial |
| §9 | V01 §9 | §17 y §18 del maestro | Seguridad, privacidad y auditoría |
| §15 | V01 §11 | — | Trazabilidad y control de cambios |
| §16 | V01 §12 | §26 del maestro | Exclusiones y límites |
| §21 (SPA-DEC-116) | — | §23 del maestro | Decisión histórica |

**Información de RECOVERY-01 que V01 no contiene y se conserva:** contacto de emergencia y código del caso (SPA-FC-02); antecedentes,
hábitos y estilo de vida (SPA-FC-03); condiciones comparables de las fotografías (SPA-AT-03); retiro de autorización y derecho a detener
la sesión (SPA-CN-05); la distinción entre campos de formularios físicos y obligaciones del software (§0, regla 2; SPA-FC-03); la
separación de permisos de Spa, Empleados y Access (SPA-SG-01); la matriz de conservación (§11) y la lista de fuentes (§12).

## 18. API conceptual (SPA-API-01) [T]

**PROPUESTA de cobertura, no aprobada.** No se aprueban rutas HTTP ni modificaciones de API. La futura especificación distinguirá
operaciones por dominio:

| Dominio | Capacidades a especificar |
|---|---|
| Expedientes | Crear, consultar y administrar información específica |
| Valoraciones | Registrar evaluaciones y seguimiento |
| Consentimientos | Registrar, consultar y gestionar autorizaciones |
| Fotografías | Registrar referencias y controlar acceso |
| Agenda | Consultar disponibilidad, reservar, cancelar y reprogramar |
| Sesiones | Documentar atención y evolución |
| Servicios | Consultar configuración aplicable |
| Paquetes | Consultar contratación y saldo, si procede |
| Integraciones | Consultar información oficial de clientes, empleados y ventas |

Cada contrato deberá especificar autorización, scope, validación, errores, transacciones, concurrencia, respuestas y pruebas. No quedan
aprobados nuevos endpoints.

## 19. Frontend y experiencia de usuario (SPA-UX-01) [T]

**PROPUESTA de cobertura, no diseño aprobado.** La futura documentación debe contemplar: acceso a Spa desde la navegación oficial;
agenda; búsqueda de cliente; expediente; valoración facial; valoración corporal; plan de atención; registro de sesión; fotografías;
consentimientos; seguimiento; consulta de servicios y paquetes, según el alcance final. Los formularios deberán seguir los componentes y
patrones oficiales del ERP ([frontend](../architecture/frontend.md)); la experiencia móvil respetará las reglas de diseño existentes, sin
sistemas paralelos de navegación, avisos o validaciones. No se define una estructura de menús definitiva ni se modifica `MENU_MAP.md`.

## 20. Pruebas candidatas (SPA-T-01 a SPA-T-18) [T]

**MATRIZ TÉCNICA PROPUESTA.** Son candidatas para un futuro contrato de aceptación; no se afirma que se aprobaron originalmente. No
confundir con las pruebas T-01 a T-15 de Empleados.

| ID | Escenario a verificar | Invariante relacionado |
|---|---|---|
| SPA-T-01 | La atención referencia al cliente correcto | SPA-INV-01 |
| SPA-T-02 | No se crea un segundo empleado desde Spa | SPA-INV-02 |
| SPA-T-03 | Se diferencia profesional de usuario registrador | SPA-INV-03 |
| SPA-T-04 | Una asignación laboral no concede permisos | SPA-INV-04 |
| SPA-T-05 | Se aplican las restricciones autorizadas de acceso a expedientes | SPA-INV-07 |
| SPA-T-06 | Se conserva la valoración facial documentada | SPA-FC-04 |
| SPA-T-07 | Se conserva la valoración corporal documentada | SPA-FC-05 |
| SPA-T-08 | Las medidas respetan unidades y precisión oficial | SPA-INV-10 |
| SPA-T-09 | Una sesión conserva sus observaciones y evolución | SPA-AT-02 |
| SPA-T-10 | Las autorizaciones fotográficas son independientes | SPA-CN-06 |
| SPA-T-11 | El permiso de seguimiento no habilita publicación promocional | SPA-INV-05 |
| SPA-T-12 | Las consultas de agenda respetan los permisos correspondientes | SPA-SG-04 |
| SPA-T-13 | Las reservas concurrentes cumplen el contrato de disponibilidad | SPA-AG-02 |
| SPA-T-14 | Los saldos de sesiones cumplen las reglas comerciales aprobadas | SPA-PQ-01 |
| SPA-T-15 | Las operaciones comerciales reutilizan los módulos oficiales | SPA-INV-08 |
| SPA-T-16 | Se mantiene aislamiento de datos por tenant y empresa | SPA-INV-10, SPA-SG-05 |
| SPA-T-17 | Los eventos relevantes quedan auditados | SPA-INV-06, SPA-AU-02 |
| SPA-T-18 | No se altera el historial por cambios posteriores en Empleados | SPA-AR-06 |

Las pruebas SPA-T-13, SPA-T-14 y SPA-T-15 dependen de contratos que **no están recuperados** (agenda, paquetes y cobros).

## 21. Decisiones históricas `SPA-DEC-*`

### SPA-DEC-116 — Revisión y consolidación del alcance de Gali Spa V1.1

**Estado: APROBADA — CONFIRMADA POR EL PROPIETARIO.**

- **Título:** confirmado expresamente por el propietario.
- **Evidencia disponible:** declaración del propietario dentro de la conversación del proyecto. No existe un documento del repositorio
  que la registre.
- **Texto íntegro de la decisión y alcance literal: NO RECUPERADOS.** No se infiere su alcance a partir del título ni de los documentos
  de este archivo.
- **Relación con decisiones anteriores:** pendiente de reconstrucción histórica.
- **Efecto sobre este documento:** ninguno de los requisitos recuperados aquí queda aprobado por esta decisión.

### Resto del registro `SPA-DEC-*`

El intervalo `SPA-DEC-001` a `SPA-DEC-115` **no se declara existente, aprobado ni recuperado** por inferencia de la numeración. No se
crean artificialmente esos identificadores. La reconstrucción deberá preservar los identificadores originales cuando aparezcan evidencias
concretas. El recuerdo aproximado del propietario de unas 116 decisiones es una referencia de búsqueda y control, no una prueba de 116
textos recuperados.

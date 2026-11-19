# SPA V1.1 — Registro de decisiones funcionales aprobadas (SPA-DEC-115, SPA-DEC-116, SPA-ARQ-01, SPA-ARQ-02, O-25, O-15 y directriz transversal)

**REGISTRO DE APROBACIONES LIMITADAS E INSTRUCCIONES DEL PROPIETARIO — NO ADR — NO CONTRATO IMPLEMENTABLE — NO IMPLEMENTACIÓN AUTORIZADA.**

Registro canónico de las aprobaciones de Spa. Conserva O-25/O-15, aprobadas el 2026-11-18, y añade la evidencia histórica SPA-DEC-115/116 recuperada por el propietario en el encargo de integración del 2026-11-18. Las decisiones históricas fueron aprobadas antes de planificar Empleados; su fecha original exacta no fue aportada.
Estado de entrega: [STATUS](../../STATUS.md); cola: [WORK_QUEUE](../agent/WORK_QUEUE.md).

Comportamiento consolidado: [contrato central](SPA-V1.1-CONTRACT-SPEC-01.md). Arquitectura candidata: [ADR-051](ADR-051-spa-v1.1-architecture.md), PROPOSED / NO APROBADO. Fases: [plan](SPA-V1.1-IMPLEMENTATION-PLAN-01.md), ninguna autorizada. DIR-SSOT-01 (§7) es una instrucción de propiedad, no una aprobación de diseño financiero.

## 0. Naturaleza y límites

1. **La aprobación corresponde únicamente a las alternativas indicadas y a sus límites**, no a todas las propuestas técnicas incluidas en los borradores de Spa.
2. O-25/O-15 no reciben identificadores SPA-DEC nuevos. SPA-DEC-115/116 se registran con sus identificadores históricos y la evidencia aportada por el propietario (§9/10). **No se fabrican SPA-DEC-001 a SPA-DEC-114 ni se dan por recuperados sus textos.**
3. **Evidencia de la aprobación:** confirmación expresa del propietario en la conversación del proyecto (SPA-V1.1-DECISION-CLOSURE-01), sobre la base del análisis SPA-V1.1-DECISION-REVIEW-01 (informe de sesión, no versionado en el repositorio).
4. Los cierres parciales de §5 se apoyan en SPA-DEC-115/116 y el encargo expreso, no en inferencias. No se crea un ADR implementable y no se autoriza implementación de Spa ni de Empleados.
5. No se modifica Ventas ni ningún módulo FROZEN.

Antecedentes retirados (historial Git; su contenido está consolidado en el [contrato](SPA-V1.1-CONTRACT-SPEC-01.md) y [ADR-051](ADR-051-spa-v1.1-architecture.md)): RECOVERY-01, OPERATIONS-SPEC, CARE-PRIVACY, SECURITY-COMPLIANCE, matriz de consolidación y normalización; no se les atribuyen aprobaciones que no tenían cuando se redactaron. «Gali Spa» aparece en este registro únicamente como atribución histórica en citas literales de la evidencia del propietario; el producto es el módulo genérico SPA V1.1.

## 1. O-25 — Cita separada de la sesión realizada

| Campo | Contenido |
|---|---|
| Identificador | `O-25` |
| Estado | **APROBADA POR EL PROPIETARIO** |
| Fecha | 2026-11-18 |
| Alternativa seleccionada | **Alternativa A — Cita separada de la sesión realizada** |
| Cuestión original | Modelo de reserva, cita, atención y sesión (terminología de sesión, cita y reserva) |

### 1.1 Descripción funcional

La agenda y el registro clínico son dos registros distintos. La **cita** es el espacio reservado en la agenda. La **sesión realizada** es el registro de una atención efectivamente prestada y solo existe cuando esa atención se documenta.
«Reservar» es la operación que crea la cita. Una sesión prevista de un paquete no es un registro aparte: es una cita vinculada al contrato.

### 1.2 Contrato aprobado

1. **Reserva** es la operación que crea una cita, no una entidad independiente.
2. La **cita** representa la programación en la agenda.
3. La **sesión realizada** es un registro independiente creado cuando se documenta una atención efectivamente realizada.
4. Una **sesión programada** de un paquete se representa mediante una cita vinculada al contrato.
5. **Reservar y reprogramar no consume saldo** de sesiones.
6. **Cancelar o registrar una ausencia no crea una sesión clínica realizada.**
7. El **consumo comercial** se representa por un movimiento independiente del contrato, cuando corresponda.
8. Se conserva la **trazabilidad** de reprogramaciones, cancelaciones y atenciones.

### 1.3 Justificación

Presentada en el análisis previo a la aprobación: separa la agenda de los datos clínicos (coherente con las reglas de acceso mínimo de Spa); se ajusta a la regla de que la atención realizada es un registro distinto de la cita; no obliga a tratar toda atención como sesión de paquete;
y deja las ausencias y cancelaciones como hechos de agenda. Su costo es mantener dos registros en lugar de uno. La alternativa no seleccionada (sesión única desde la reserva) **no se aprueba**.

### 1.4 Alcance exacto

Lo aprobado es **qué registros existen y cómo se relacionan** (los ocho enunciados de §1.2). Nada más.

### 1.5 Exclusiones (no aprobado)

- Las reglas sobre **ausencias que consumen saldo**.
- La **atención sin cita** previa.
- **Varios servicios por cita**.
- Cualquier otro **efecto comercial** sobre el saldo de un paquete (por ejemplo, restituir o no una sesión al cancelar).
- Horarios, recursos, tolerancias, anticipación y confirmaciones de la agenda.

### 1.6 Dependencias pendientes

`O-10` (atención sin cita), `O-11` (cardinalidad entre servicio y procedimiento y servicios por cita), `O-13` (efectos de ausencia, cancelación y reprogramación sobre el saldo), `O-08` y `O-09` (reglas de agenda) y el contrato `IEmployeeDirectory` de Empleados (V-02), que sigue siendo propuesta.

### 1.7 Implicaciones técnicas que todavía requieren diseño (no aprobadas)

Entidades y estados de la cita y de la sesión realizada; registro de cambios de la cita; vínculo opcional cita–contrato; creación de la sesión a partir de la cita; restricción de intervalos solapados; permisos; errores; API y pantallas. Estas son propuestas de los borradores y siguen sin aprobarse.

### 1.8 Reglas existentes que se ven afectadas

| Enunciado aprobado | Reglas relacionadas (en las fuentes) |
|---|---|
| 1, 2 | `SPA-AG-01`, `SPA-OPS-AGE-01`, `SPA-OPS-AGE-02` |
| 3, 6 | `SPA-OPS-AGE-08`, `SPA-OPS-AGE-09`, `SPA-OPS-AGE-10`, `SPA-AT-06`, `SPA-AT-07` |
| 4 | `SPA-AT-06`, `SPA-INV-09` |
| 5 | `SPA-OPS-AGE-07` (queda cerrado el efecto de reprogramar sobre el saldo; el de cancelar y ausentarse no) |
| 7 | `SPA-OPS-PKG-01`, `SPA-OPS-PKG-02` |
| 8 | `SPA-OPS-AGE-12`, `SPA-AT-07`, `SPA-AU-02` |

## 2. O-15 — Contrato de paquete creado explícitamente tras validar una factura autorizada

| Campo | Contenido |
|---|---|
| Identificador | `O-15` |
| Estado | **APROBADA POR EL PROPIETARIO** |
| Fecha | 2026-11-18 |
| Alternativa seleccionada | **Alternativa A — Creación explícita del contrato después de validar una factura autorizada** |
| Cuestión original | Origen del contrato de paquete: explícito desde la factura o por evento de Ventas |

### 2.1 Descripción funcional

Después de facturar, Spa crea el contrato de paquete de forma explícita, validando una factura autorizada y referenciando la factura y su línea. La creación ocurre fuera de la autorización de Ventas, y un error de Spa no afecta a la factura.

### 2.2 Contrato aprobado

1. **Spa crea explícitamente el contrato de paquete después de validar una factura autorizada.**
2. La creación **referencia la factura y su línea correspondiente**.
3. La creación se realiza **fuera de la transacción de autorización de Ventas**.
4. Deben **evitarse contratos duplicados** para una misma línea facturada.
5. **Un error de Spa no debe revertir la autorización de la factura.**
6. **Spa no crea facturas, CxC, cobros ni asientos contables.**
7. **No se aprueba la creación automática** mediante `SalesInvoiceAuthorizedEvent`.

### 2.3 Justificación

Presentada en el análisis previo a la aprobación: no acopla la facturación a Spa (los manejadores del evento de autorización corren en la misma transacción, de modo que un fallo de Spa podría revertir una autorización fiscal); aprovecha una consulta de factura que ya existe; no requiere extender un módulo FROZEN
(el evento de autorización, además, no trae cliente ni líneas); y permite reintentar la creación sin tocar la venta. Su costo es un paso adicional. La alternativa no seleccionada (creación automática por eventos de Ventas) **no se aprueba**.

### 2.4 Alcance exacto

Lo aprobado son los siete enunciados de §2.2. Nada más.

### 2.5 Exclusiones y pendientes (no aprobado)

- **Estado electrónico SRI requerido** para permitir la creación del contrato.
- **Comportamiento de los contratos ante anulaciones o notas de crédito.**
- **Política frente a facturas a crédito pendientes de cobro.**
- **Anticipos y prepagos de clientes** (`O-14`).
- **Necesidad y diseño de una lista de facturas pendientes de generar contrato.**

### 2.6 Dependencias pendientes

`O-14` (anticipos y prepagos), `O-13` (las 12 preguntas contractuales de paquetes, en particular devolución, cancelación y saldo), y la definición de qué significa «factura autorizada» para este fin.

### 2.7 Implicaciones técnicas que todavía requieren diseño (no aprobadas)

Entidades del contrato y de su libro de movimientos; restricción de unicidad por línea facturada; frontera transaccional propia; lectura de la factura mediante una consulta existente de Ventas; validaciones de cliente y estado; permisos; errores; API y pantalla de creación. Siguen siendo propuestas y sin aprobar.

### 2.8 Reglas existentes que se ven afectadas

| Enunciado aprobado | Reglas relacionadas (en las fuentes) |
|---|---|
| 1, 2 | `SPA-OPS-PKG-04`, `SPA-OPS-PKG-06`, `SPA-OPS-VTA-02`, `SPA-PQ-01` (pregunta «Venta») |
| 3, 5 | Sin regla previa equivalente; se relaciona con la verificación V-03b de la normalización (los manejadores de `SalesInvoiceAuthorizedEvent` corren en la misma transacción que la autorización) |
| 4 | `SPA-OPS-PKG-03` (prueba `SPA-OPS-T-PKG-03`) |
| 6 | `SPA-OPS-VTA-01`, `SPA-INV-08`, `SPA-AR-05` |
| 7 | La alternativa «evento de autorización» de OPERATIONS-SPEC §4.5 queda **no aprobada** |

## 3. Conflictos

- **C-06 (terminología de citas y sesiones):** el **conflicto terminológico queda resuelto** por `O-25` (alternativa A). Las **políticas comerciales** relacionadas (ausencias que consumen saldo, efecto de cancelar, atención sin cita) **siguen abiertas**. Varios servicios/profesionales por visita sí están aprobados por SPA-DEC-116; su representación técnica se delimita en §11.
- **C-01 y C-02** no se modifican y siguen abiertos.
- **C-08** (anticipos frente a la capacidad real de Ventas y CxC) no se modifica: depende de `O-14`.

## 4. Lo que estas decisiones no cambian

- No aprueban políticas de saldo, de ausencias que consumen, de cancelación ni de reprogramación con efecto comercial.
- No aprueban anticipos, prepagos, facturación por sesión ni devengo.
- No aprueban el catálogo de permisos, los estados ni las entidades propuestos en los borradores.
- No aprueban la creación automática de contratos.
- No modifican ADR-050 ni la especificación de Empleados.

## 5. Estado de las cuestiones tras la evidencia histórica

O-15/O-25 permanecen íntegros. **O-23 queda CERRADA en su componente de inclusión funcional general**, por SPA-DEC-116 y la instrucción expresa del propietario. No se requiere volver a aprobar capacidad por capacidad el alcance ya declarado.

O-08 queda resuelta en cuanto a cabinas/equipos: su control de disponibilidad está expresamente excluido. O-20 queda resuelta: expediente único por cliente y empresa. O-09/O-11/O-12/O-13/O-16 tienen componentes funcionales aprobados y detalles abiertos, delimitados en el contrato §30. No se presenta como pendiente la existencia de firma en pantalla, versiones, tratamientos multisesión, paquetes combinados, vigencia, cupos o varios servicios/profesionales.

**O-14 y O-24 siguen abiertas.** También siguen pendientes política Admin, acceso entre sucursales, registro de lecturas, cifrado, conservación/eliminación, menores, alcance académico, datos/obligatoriedad de formularios, precisión de medidas y reglas comerciales concretas. El contraste de fichas está en O-24/V-01; los detalles de decisiones SPA-DEC-001..114 no recuperados son evidencia histórica pendiente, no ausencia de aprobación del alcance. Estado detallado único: contrato §30.

## 6. Límites

Este documento registra dos decisiones históricas aprobadas, O-15/O-25 y una instrucción transversal de propiedad, sin aprobar mecanismos técnicos pendientes. No es un ADR, no es una especificación contractual, no autoriza implementación de Spa ni de Empleados, migraciones ni cambios en módulos FROZEN, y no aprueba ninguna otra decisión.

## 7. DIR-SSOT-01 — Anticipos de Clientes como proceso transversal

**Estado: INSTRUCCIÓN DEL PROPIETARIO; no aprobación de diseño.** Fecha: 2026-11-18. Procedencia: encargo SPA-V1.1-SSOT-CONSOLIDATION, ya recogido en el contrato §20.4, y reiteración expresa en el encargo «Consolidación documental definitiva», §4.

Anticipos de Clientes es un proceso independiente y transversal del ERP, no una entidad financiera propia de Gali Spa. Esta frontera no asigna su implementación a Ventas o CxC, no diseña cobros sin factura, aplicaciones, devoluciones, contabilización ni tratamiento fiscal, y no autoriza desarrollo. **O-14 permanece abierta; C-08 conserva su brecha funcional.** Spa solo podría consumir el contrato del proceso si se aprueba separadamente. DIR-SSOT-01 no cierra O-23; su componente funcional se cierra por SPA-DEC-116 y el encargo expreso, no por esta instrucción.

## 8. Decisiones de Empleados aplicables a Spa

Referencia de autoridad: [ADR-050](ADR-050-employees-v1-architecture.md), arquitectura funcional aprobada; ADR en estado Proposed y módulo sin implementar. Este resumen no modifica sus decisiones.

| Fuente exacta | Alcance aplicable | Límite |
|---|---|---|
| §3 DEC-E1; §13 | Reutilizar Employee; contrato mínimo IEmployeeDirectory de solo lectura, ids estables no reutilizados, inactivos consultables para historia | No aprueba firmas técnicas ni habilita consumo antes de EMP-E6b |
| §3 DEC-E4; §13 | Profesional responsable separado del usuario registrador; quien documenta requiere usuario autorizado | No identifica automáticamente profesional, vendedor y cobrador |
| §3 DEC-E5 y P-01 | Catálogos distintos de cargo, profesión y especialidad; cargo/profesión pueden faltar | No aprueba una regla de elegibilidad de Spa por especialidad |
| §3 DEC-E6, E-D4 y P-04; §8 | Varias sucursales laborales; sin principal obligatoria; sin asignación válida no hay elegibilidad para operaciones que la exijan | Asignación laboral no es autorización ERP ni disponibilidad horaria |
| §6, §8, §13 | Permisos y acceso ERP oficiales; Spa posee permisos clínicos, agenda, horarios, sesiones y disponibilidad | employees.* y access.* no son permisos clínicos; política Admin de Spa pendiente O-01 |

**Alcance del registro:** O-15/O-25 conservan su aprobación y texto; SPA-DEC-115/116 aportan las aprobaciones históricas de §9/10. Los componentes O-xx resueltos o todavía pendientes se delimitan en §5 y contrato §30, sin aprobación técnica integral ni autorización de desarrollo.

## 9. SPA-DEC-115 — Búsqueda de disponibilidad por servicio

**APROBADA HISTÓRICA — evidencia recuperada el 2026-11-18.** Fuente: contenido de conversaciones originales aportado expresamente por el propietario en el encargo «Integración de las decisiones históricas aprobadas SPA-DEC-115 y SPA-DEC-116», §1. No se declara inspección independiente de la conversación original ni una fecha histórica no proporcionada. Transcripción aportada:

**Título:** Búsqueda de disponibilidad por servicio para Gali Spa V1.1.

Funcionamiento aprobado:

1. Recepción podrá buscar horarios disponibles sin seleccionar previamente un profesional.
2. La búsqueda considerará servicio, fecha y sucursal.
3. El ERP mostrará los profesionales habilitados y sus horarios disponibles.
4. Se respetarán descansos, bloqueos, tiempos de preparación, traslados y demás restricciones aprobadas.
5. Recepción seleccionará el profesional y horario que mejor convengan al cliente.
6. La cita no se confirmará automáticamente.
7. Se reutilizará la agenda y su mecanismo centralizado de disponibilidad.

**No convertir esta decisión en una segunda agenda ni en otro motor independiente.**

Las reglas exactas de cálculo y representación de restricciones deberán verificarse contra las decisiones anteriores que permanezcan disponibles. No inventar valores de duración, tolerancias ni márgenes.

## 10. SPA-DEC-116 — Revisión y consolidación del alcance

**APROBADA HISTÓRICA — evidencia recuperada el 2026-11-18.** Misma fuente aportada por el propietario, §1/2. Ya no se conoce solo el título: se recuperaron las áreas, agrupaciones funcionales y exclusiones siguientes. Esto no equivale a recuperar todos los detalles de decisiones anteriores ni aprobar estructuras de datos. Transcripción aportada:

**Título:** Revisión y consolidación del alcance de Gali Spa V1.1.

La decisión fue aprobada expresamente por el propietario.

La revisión posterior documentó que las siguientes áreas **forman parte del alcance funcional aprobado de V1.1**:

| Área | Alcance histórico aprobado |
|---|---|
| Clientes y expedientes | Expediente único por empresa, antecedentes, valoraciones, alertas e historial |
| Empleados | Modelo general reutilizable del ERP, vinculado con usuarios y sucursales |
| Tratamientos | Planes individuales, multisesión, ampliaciones, suspensión, cancelación y cierre |
| Sesiones | Registro técnico individual, resultados, productos, incidencias, fotografías y evolución |
| Agenda | Citas diarias y semanales, profesionales, horarios, disponibilidad y reprogramaciones |
| Paquetes | Individuales y combinados, vigencia, cupos, ampliaciones y sustituciones |
| Consentimientos | Por procedimiento, firma en pantalla, versiones y autorizaciones específicas |
| Ventas y cobros | Integración con servicios, facturas, CxC, cobros y ventas de productos oficiales |
| Seguridad | Permisos existentes, aislamiento por empresa, auditoría y archivos privados |
| Interfaz | Diseño responsivo para computadora, tablet y celular |
| Gestión operativa | Panel inicial, seguimiento de tratamientos, lista de espera y reportes básicos |

**También se aprobaron estas agrupaciones funcionales:**

- Motor de agenda: disponibilidad, horarios, bloqueos, traslados, preparación y conflictos.
- Gestión de visitas: citas, varios servicios, varios profesionales, llegada y cambios.
- Gestión de sesiones: formularios, borradores, finalización, interrupciones y correcciones.
- Gestión de tratamientos: sesiones contratadas, cupos, ampliaciones, suspensiones y cierres.
- Expediente del cliente: valoraciones, alertas, consentimientos, fotografías e historial.
- Gestión comercial: paquetes, facturas, cobros, saldos, cancelaciones y ajustes.
- Seguridad: permisos, auditoría, acceso a archivos y protección de datos.

Estas agrupaciones forman parte de procesos integrados. No requieren crear un módulo independiente por cada característica.

### 10.1 Exclusiones originales expresas de V1.1

SPA-DEC-116 documenta que quedaron fuera:

1. Control de disponibilidad de cabinas y equipos.
2. Recordatorios automáticos por WhatsApp, correo o SMS.
3. Desarrollo de un nuevo sistema de respaldos o recuperación.
4. Reservas públicas de clientes.
5. Descuento automático mediante Kardex de productos aplicados en sesiones.
6. Nómina y administración completa de recursos humanos.
7. Aplicación móvil independiente.
8. Funcionamiento sin conexión a Internet.
9. Diagnósticos automáticos e inteligencia artificial para evaluar resultados.
10. Dashboard analítico adicional y contabilidad paralela.

**Distinciones obligatorias:**

- Los descansos, bloqueos, preparación y traslados de SPA-DEC-115 no autorizan un módulo de disponibilidad de cabinas o equipos.
- El panel operativo y los reportes básicos están incluidos. El dashboard analítico adicional está excluido.
- Registrar los productos utilizados en una sesión no implica descontarlos automáticamente de Kardex.
- El diseño responsive está incluido. Una aplicación móvil independiente está excluida.
- El uso de funcionalidades existentes de respaldo no equivale a desarrollar un sistema nuevo de respaldos.
- La integración con Ventas y cobros no autoriza crear contabilidad, Caja ni CxC dentro de Spa.

### 10.2 Orden preliminar histórico de fases

El propietario aporta en §7 del encargo la secuencia histórica de SPA-DEC-116: F0 arquitectura y contratos; F1 Empleados y base segura; F2 expediente y valoraciones; F3 tratamientos y sesiones; F4 consentimientos y fotografías; F5 agenda y lista de espera; F6 integración comercial; F7 operación y reportes; F8 pruebas integrales. Es orden preliminar, adaptable a dependencias técnicas, no secuencia inmutable ni autorización de fases. Desarrollo del orden y gates: plan oficial.

## 11. Compatibilidad con decisiones posteriores y pendientes concretos

| Evidencias conservadas | Delimitación, sin revocación inferida |
|---|---|
| SPA-DEC-115 y O-25 | Buscar devuelve profesionales/horarios y requiere selección explícita; no confirma automáticamente. Reservar crea cita y nunca consume por reservar/reprogramar. Buscar no crea sesión clínica ni una segunda agenda |
| SPA-DEC-116 (visitas) y O-25 | Varios servicios/profesionales por visita es capacidad aprobada. O-25 no aprobó esa cardinalidad en su propio alcance, pero tampoco la excluyó. SPA-ARQ-01 (§12) resuelve visita que agrupa una o varias citas; cada cita es bloque de profesional/intervalo. Varios bloques pueden servir a una prestación comercial sin duplicarla; esquema y vínculos técnicos siguen pendientes |
| SPA-DEC-116 (sesiones) y O-25 | Borradores, finalización, interrupciones y correcciones aprobados como capacidades. Un borrador técnico no certifica una atención realizada. Efectos de sesión parcial/interrumpida sobre cita, cupos y consumo siguen sin definir; ausencia/cancelación no crea sesión clínica |
| SPA-DEC-116 (paquetes) y O-15/O-25 | Individual/combinado, vigencia, cupos, ampliación/sustitución aprobados. Contrato explícito desde factura/línea, transacción independiente, sin duplicados; movimiento comercial separado de sesión. No se aprueba creación automática ni se fija política de consumo/NC/crédito por inclusión general |
| SPA-DEC-116 (comercial) y DIR-SSOT-01 | Integrar servicios, productos, facturas, CxC/cobros oficiales no crea motores financieros Spa; anticipos siguen transversales, O-14 abierta |
| SPA-DEC-116 y ADR-050 | Employee general reutilizado con usuario/sucursales; sin segundo registro. Responsable y registrador separados. Directorio real pendiente |
| Firma/privacidad histórica y seguridad actual | Firma en pantalla, versiones, permisos oficiales, auditoría y archivos privados aprobados como capacidad; autenticidad, conservación legal, Admin, cifrado, concurrencia y claves concretas pendientes |
| Restricciones y exclusiones históricas | Descansos/bloqueos/preparación/traslados aprobados, sin valores inventados. No control de disponibilidad de cabinas/equipos. Panel/reportes operativos incluidos; dashboard analítico adicional excluido. Usar respaldos existentes no permite desarrollar un sistema nuevo |

**No se detecta revocación expresa entre las fuentes aportadas.** SPA-ARQ-01 (§12) resuelve el modelo conceptual visita–citas. SPA-ARQ-02 (§13) aprueba los estados de cita, llegada y situación derivada de visita, y reprogramación como evento/indicador. Quedan por definir esquema/vínculos técnicos, tratamiento clínico de interrupciones/sesiones parciales/correcciones, políticas de ausencias, cálculo de restricciones, concurrencia, consumo/ajustes comerciales y protección legal/técnica. Se conservan ambas evidencias y no se resuelven esas cuestiones en silencio. O-23 está cerrada solo para inclusión funcional; O-14/O-24 abiertas.

## 12. SPA-ARQ-01 — Modelo conceptual de visitas, citas y sesiones

**APROBADA por el propietario — 2026-11-19.** Evidencia: instrucción expresa del propietario en este encargo «Registrar decisión SPA-ARQ-01». Aprobación conceptual de alcance limitado; no es aceptación integral del ADR ni autorización de implementación.

### 12.1 Enunciados aprobados (texto del propietario)

1. Una visita agrupa una o varias citas del mismo cliente para una ocasión de atención en una sucursal.
2. Cada cita representa un bloque de agenda con profesional e intervalo de tiempo.
3. Una visita admite varios servicios y profesionales.
4. Un servicio que requiere varios profesionales puede utilizar varios bloques, sin duplicar la prestación comercial.
5. Las sesiones realizadas se registran independientemente cuando se documenta la atención efectiva.
6. La cancelación de una cita no cancela automáticamente las demás citas de la visita.
7. La reserva y reprogramación no consumen saldo; el consumo comercial se registra independientemente conforme a O-25.

### 12.2 Alcance exacto y pendientes

Se resuelve la agrupación conceptual visita → una o varias citas y cita → bloque de profesional/intervalo. Los bloques de agenda no son prestaciones comerciales adicionales por sí mismos ni sesiones clínicas inferidas. O-25 conserva la separación entre programación, atención efectiva y consumo. O-15 y su creación explícita desde factura/línea no cambian.

No se aprueban por inferencia estados exactos de visita, número de sesiones clínicas por prestación, sesiones parciales, tiempos de preparación, concurrencia ni política de ausencias. También siguen pendientes entidades/agregados físicos, claves, relaciones persistidas, API/DTO y controles concretos. La cancelación local no fija el estado colectivo de la visita ni efectos comerciales no aprobados.

Integración funcional: [contrato §8.5](SPA-V1.1-CONTRACT-SPEC-01.md#85-modelo-conceptual-aprobado-spa-arq-01). ADR-051 permanece **PROPOSED / NO APROBADO**; ninguna fase F0–F8 autorizada. Solo documentación: sin código, staging, commit ni push en este encargo.

### 12.3 Actualización de alcance por SPA-ARQ-02

Los siete enunciados de SPA-ARQ-01 permanecen íntegros. Su exclusión de decisiones por inferencia en §12.2 describe el alcance de esa aprobación; SPA-ARQ-02 (§13) aporta después la aprobación expresa de estados/eventos de agenda y situación derivada de visita. No aprueba por ello cardinalidad clínica por prestación, sesiones parciales, preparación, concurrencia o políticas de ausencias/comerciales.

## 13. SPA-ARQ-02 — Estados de citas, situación de visitas y reprogramación

**APROBADA por el propietario — 2026-11-19.** Fuente: instrucciones expresas «Registro de SPA-ARQ-02» y «Completar SPA-ARQ-02» de este encargo. Ambas partes se integran sin sustituir SPA-ARQ-01 ni O-25. Aprobación funcional limitada; ADR-051 permanece PROPOSED / NO APROBADO y F0–F8 sin autorización.

### 13.1 Reglas aprobadas de estados y visitas (texto del propietario)

1. La cita tiene los estados Programada, En atención, Atendida, Cancelada y Ausente.
2. La llegada se registra como evento de visita, identificando fecha, hora y usuario.
3. Cada cita inicia y termina su atención independientemente de las demás.
4. La cancelación de una cita no cancela automáticamente toda la visita.
5. La situación general de la visita se deriva de sus citas, sin una máquina de estados independiente.
6. La visita finaliza cuando todas sus citas tienen estado final.
7. Una cita Atendida requiere atención efectivamente realizada y documentada.
8. Una atención interrumpida no se considera automáticamente completada.
9. Las transiciones de agenda no consumen automáticamente el saldo de paquetes.

### 13.2 Complemento aprobado de reprogramación (texto del propietario)

1. Estados operativos de cita: `Programada`, `En atención`, `Atendida`, `Cancelada` y `Ausente`.
2. `Reprogramada` es un evento de cambio, no un estado.
3. La cita conserva su identidad y estado `Programada` después de una reprogramación válida.
4. Registrar fecha/hora anterior y nueva, profesional anterior y nuevo cuando corresponda, autor y momento del cambio.
5. Mostrar en agenda que la cita fue reprogramada y permitir consultar su historial.
6. La visita conserva su agrupación y no se cancela automáticamente por modificar una de sus citas.
7. Reprogramar no consume saldo de paquetes, conforme a O-25.
8. Preservar las demás reglas aprobadas de SPA-ARQ-01 y SPA-ARQ-02.

### 13.3 Alcance exacto y pendientes expresos

Son cinco estados de cita: ni llegada ni `Reprogramada` añaden un sexto estado. Llegada es evento de visita; reprogramación es cambio trazable e indicador visible de agenda. La situación de visita se deriva de sus citas y su finalización requiere que todas estén en estado final; no se crea una máquina de estados independiente ni se propaga cancelación por cambiar una cita.

`Atendida` exige atención efectiva documentada. Interrupción no equivale automáticamente a atención completada. El inicio/fin de cada cita es independiente y las transiciones de agenda no generan consumo automático. Reprogramación válida conserva identidad/Programada y registra los valores anteriores/nuevos, profesional cuando corresponda, autor y momento; no decide nuevas condiciones de validez o reprogramación desde otros estados.

**Pendientes expresos:** tratamiento clínico de interrupciones, sesiones parciales, correcciones, políticas de ausencias —incluidas penalizaciones—, cancelaciones/efectos comerciales y concurrencia. Se mantienen cardinalidad clínica por prestación, tiempos de preparación, esquema/API/DTO y mecanismos técnicos no aprobados. No asignar automáticamente un estado nuevo a la interrupción ni deducir un consumo o ajuste de paquete.

Comportamiento central: [contrato §8.6](SPA-V1.1-CONTRACT-SPEC-01.md#86-estados-eventos-y-situación-de-visita-aprobados-spa-arq-02). No hay cambio del orden o dependencias F0–F8, ni autorización de código, staging, commit o push.

## 14. SPA-ARQ-03 — Ciclo de atención clínica

**APROBADA por el propietario — 2026-11-19.** Fuente: encargo expreso «Registrar SPA-ARQ-03 — Ciclo de atención clínica». Complementa SPA-DEC-116, O-25, SPA-ARQ-01/02 y la distinción profesional responsable/usuario registrador de ADR-050. No acepta integralmente ADR-051 ni autoriza implementación. Se preservan íntegros los registros anteriores: los detalles ahora resueltos proceden de esta aprobación expresa, no de inferencia.

### 14.1 Ocho reglas aprobadas (texto del propietario)

**Regla 1 — Borradores**

Permitir preparar, guardar y editar un formulario clínico incompleto sin acreditar todavía una sesión realizada.

Guardar un borrador no constituye atención completada ni genera consumo comercial.

**Regla 2 — Inicio de atención**

Cuando comience la atención efectiva, registrar fecha y hora de inicio, profesional responsable y usuario ERP registrador, manteniendo ambos identificados separadamente.

El registro debe corresponder al cliente, empresa, sucursal y contexto de atención correctos.

**Regla 3 — Finalización**

La atención se finaliza mediante una acción explícita del profesional autorizado.

El ERP conserva la información efectivamente documentada: procedimientos, observaciones, productos utilizados, incidencias, resultados y evolución, según corresponda al servicio.

La finalización clínica no genera por sí misma facturas, cobros, asientos ni movimientos de inventario.

**Regla 4 — Interrupción**

Si un procedimiento iniciado debe detenerse, registrar el motivo, momento y trabajo efectivamente realizado.

Una atención interrumpida no se marca como completada.

**Regla 5 — Sesiones parciales**

La atención efectivamente prestada, aunque sea parcial, debe conservarse como registro clínico del expediente, diferenciada de una sesión completada.

No confundir una atención parcial documentada con una cita programada, cancelada o ausente.

**Regla 6 — Correcciones**

Permitir corregir registros clínicos finalizados exclusivamente mediante una operación autorizada que conserve motivo, autor, fecha y trazabilidad de versiones.

La conservación de versiones queda sujeta a la política legal aplicable, todavía pendiente de definición. Esta aprobación no resuelve por inferencia el conflicto C-02 sobre conservación y eliminación legal.

**Regla 7 — Evolución**

Toda atención efectivamente documentada se incorpora al seguimiento del tratamiento, incluidas las interrupciones que hayan producido una atención parcial.

Debe poder distinguirse la evolución registrada en sesiones completadas de la correspondiente a atenciones parciales.

**Regla 8 — Consumo comercial independiente**

El consumo comercial de paquetes se representa y decide por separado del registro clínico, de acuerdo con O-25.

- Un borrador no consume sesiones.
- Una atención finalizada no determina por sí sola la cantidad comercial a descontar.
- Una atención parcial tampoco determina automáticamente un descuento.
- Las políticas exactas de consumo, compensación, restauración o penalización siguen pendientes de O-13.

Esta regla no autoriza descontar automáticamente una sesión completa ni conceder sesiones gratuitas por inferencia.

### 14.2 Modelo conceptual aprobado del registro clínico (texto del propietario)

Registrar el siguiente modelo conceptual aprobado:

- **Borrador:** formulario editable, sin atención acreditada.
- **En atención:** atención efectiva iniciada y en documentación.
- **Finalizada:** atención completada y documentada.
- **Interrumpida:** atención iniciada y detenida, con motivo y alcance real documentados.

Las correcciones posteriores conservan trazabilidad; no deben interpretarse como una nueva atención efectiva ni como consumo comercial adicional.

Estos conceptos describen el **ciclo del registro clínico**, separado de los cinco estados aprobados de la cita en SPA-ARQ-02.

No inventar estados adicionales, mecanismos de persistencia, transiciones de reapertura ni anulaciones clínicas que el propietario no haya aprobado.

### 14.3 Alcance exacto, compatibilidad y límites pendientes

La atención parcial efectivamente documentada se conserva en el expediente y seguimiento, diferenciada de una completada. La corrección autorizada conserva motivo, autor, fecha y versiones, sin nueva atención ni consumo adicional. Persistencia de borradores, cierre/concurrencia y mecanismos de versionado siguen técnicos pendientes; no se aprueban entidades, tablas, interfaces, reaperturas o anulaciones clínicas adicionales.

SPA-ARQ-02 mantiene sus cinco estados de cita (`Programada`, `En atención`, `Atendida`, `Cancelada`, `Ausente`), reprogramación como evento/indicador trazable y visita derivada de citas. **Pendiente:** correspondencia exacta entre atención clínica interrumpida y estado final de su cita. No asignar Atendida automáticamente por una parcial interrumpida ni crear sexto estado de cita. La coincidencia del nombre En atención en ambos ciclos no aprueba sincronización automática.

**O-13 sigue pendiente:** cantidades, consumo, compensación, restauración, penalización y efectos comerciales de finalización/interrupción. No inferir descuento completo ni gratuidad. **C-02 permanece abierto:** conservación de versiones sujeta a política legal aplicable. Sin excepciones nuevas de consentimiento ni cierre de validez legal de firma, menores, Admin excepcional, cifrado/claves o auditoría técnica definitiva.

Referencia funcional principal: [contrato §12](SPA-V1.1-CONTRACT-SPEC-01.md#12-sesiones-realizadas-y-consumo); aceptación futura en §28.5. ADR-051 permanece **PROPOSED / NO APROBADO**; F0–F8 **PENDING / NO AUTORIZADA**. Solo documentación, sin staging, commit, push o implementación.

## 15. SPA-ARQ-04 — Agenda y disponibilidad

**APROBADA POR EL PROPIETARIO — 2026-11-19.** Decisión posterior a SPA-DEC-115/116 y complementaria de SPA-ARQ-01/02/03. Conserva O-15/O-25 y las aprobaciones anteriores sin ampliarlas.

### 15.1 Doce reglas aprobadas (texto del propietario)

**Regla 1 — Horarios**

Cada sucursal tiene sus horarios de atención y cada profesional tiene horarios laborales y asignaciones vigentes.

La disponibilidad debe respetar ambos horarios y la elegibilidad del profesional, obtenida del módulo oficial de Empleados.

El acceso ERP del usuario a una sucursal no equivale a disponibilidad laboral.

**Regla 2 — Duración de servicios**

Cada servicio o procedimiento atendible en Spa tiene una duración configurable para agenda.

La búsqueda debe considerar la duración efectiva del servicio solicitado.

El producto comercial continúa siendo Item `Service`; Spa no crea un segundo catálogo comercial.

La configuración concreta de perfiles y procedimientos sigue pendiente de O-11.

**Regla 3 — Descansos, ausencias y bloqueos**

Los descansos, ausencias laborales y bloqueos autorizados impiden ofrecer horarios incompatibles.

Se deben contemplar intervalos no disponibles de profesionales y las restricciones pertinentes de la sucursal.

No crear disponibilidad de cabinas ni equipos, expresamente excluida de V1.1.

**Regla 4 — Preparación**

La duración de preparación es configurable según servicio o procedimiento y debe reservar el intervalo necesario en el cálculo de disponibilidad.

No se aprueba un margen universal fijo para todos los servicios.

Los valores concretos y el modo de almacenamiento siguen pendientes de detalle técnico.

**Regla 5 — Traslados**

Cuando un profesional atiende en distintas sucursales, la agenda debe considerar un tiempo de traslado configurado para evitar compromisos incompatibles.

No inventar minutos predeterminados ni un servicio externo de rutas.

La política exacta de configuración entre sucursales y sus excepciones queda pendiente de detalle.

**Regla 6 — Citas consecutivas**

Se permiten citas consecutivas cuando sus intervalos no se solapan ni incumplen preparación, descanso, traslado u otras restricciones aplicables.

No imponer márgenes adicionales no aprobados.

**Regla 7 — Varios servicios y profesionales**

Una visita puede agrupar varias citas, servicios y profesionales conforme a SPA-ARQ-01.

Cada bloque de agenda valida la disponibilidad de su profesional correspondiente.

Los servicios que requieren más de un profesional pueden necesitar varios bloques sin duplicar la prestación comercial.

**Regla 8 — Búsqueda por servicio**

Recepción selecciona servicio, fecha y sucursal, sin tener que elegir previamente un profesional.

El ERP devuelve profesionales habilitados y sus horarios disponibles utilizando el mecanismo centralizado de la agenda.

Recepción selecciona explícitamente profesional y horario.

La búsqueda no crea ni confirma automáticamente una cita.

Respetar los siete enunciados de SPA-DEC-115.

**Regla 9 — Reservas simultáneas**

Al confirmar una reserva, el ERP debe revalidar la disponibilidad.

Dos operaciones concurrentes no pueden reservar compromisos incompatibles para el mismo profesional.

El mecanismo exacto de transacción, bloqueo, exclusión de intervalos e índices PostgreSQL sigue siendo una alternativa técnica pendiente de ADR-051.

**Regla 10 — Reprogramación**

Revalidar la disponibilidad del nuevo horario antes de confirmar el cambio.

Si la reprogramación falla, conservar la cita original y comunicar el conflicto.

La cita conserva su identidad y estado `Programada` cuando se reprograma válidamente.

Registrar historial, usuario y cambios, mostrar el indicador de reprogramación y no consumir saldo comercial, conforme a SPA-ARQ-02 y O-25.

**Regla 11 — Zona horaria**

Presentar horarios según la configuración horaria oficial del ERP para la empresa o sucursal, utilizando el estándar de tiempo vigente.

Gestionar los instantes de forma consistente en UTC.

No introducir una configuración de zona horaria por sucursal si el ERP todavía no dispone de ella y no se ha aprobado su ampliación. En ese caso, utilizar la zona horaria oficial de la empresa.

Los detalles de horarios inexistentes o ambiguos por cambios de zona horaria deben resolverse técnicamente sin permitir reservas inconsistentes.

**Regla 12 — Cambios e interrupciones**

Conservar la trazabilidad de los cambios de agenda.

Una interrupción de atención clínica se registra conforme a SPA-ARQ-03 y no libera automáticamente el tiempo restante de agenda ni genera consumo comercial.

La correspondencia exacta entre una atención clínica interrumpida y el estado final de su cita permanece pendiente.

No modificar ni ampliar unilateralmente los cinco estados aprobados por SPA-ARQ-02.

### 15.2 Límites de la aprobación (texto del propietario)

**Aprobado funcionalmente:**

- Horarios y elegibilidad.
- Duración configurable.
- Descansos y bloqueos.
- Preparación.
- Traslados.
- Citas consecutivas compatibles.
- Visitas con varios servicios y profesionales.
- Búsqueda centralizada por servicio.
- Prevención de reservas simultáneas incompatibles.
- Reprogramación con revalidación y conservación.
- Consistencia horaria.
- Trazabilidad y tratamiento de tiempo ante interrupciones.

**No aprobado técnicamente por esta decisión:**

- Entidades y tablas definitivas.
- Estructuras de intervalos recurrentes.
- Algoritmos de cálculo.
- Claves y estrategia de bloqueo.
- Restricciones SQL concretas.
- Contratos API.
- Política de reasignación automática.
- Duraciones predeterminadas.
- Estados adicionales.
- Liberación manual de tiempo después de interrupciones.
- Nuevas capacidades de Empleados.

Mantener esos mecanismos como pendientes de ADR-051.

### 15.3 Integración y autorización

Referencia funcional única: [contrato §7](SPA-V1.1-CONTRACT-SPEC-01.md#7-sucursales-y-disponibilidad); §8/9/27/28/30 remiten a ella. La aprobación funcional no acepta ADR-051, que permanece PROPOSED / NO APROBADO, ni autoriza F0–F8, Empleados o implementación. No agrega estados de cita, liberación manual de tiempo, capacidades de Empleados ni excepciones comerciales o legales. Estado final de cita interrumpida pendiente; consumo independiente conforme O-25/SPA-ARQ-03.

## 16. SPA-ARQ-05 — Tratamientos y paquetes comerciales

**APROBADA POR EL PROPIETARIO — 2026-11-19.** Complementa SPA-DEC-116, O-15/O-25, SPA-ARQ-01/02/03/04 y DIR-SSOT-01, sin alterar su literalidad ni ampliar Anticipos.

### 16.1 Diez reglas aprobadas (texto del propietario)

### Regla 1 — Composición de paquetes

Cada paquete define los servicios incluidos y sus cantidades específicas.

Se admiten:

- Paquetes individuales.
- Paquetes combinados con diferentes servicios.
- Cantidades independientes por servicio.

No existe intercambio libre de sesiones entre servicios.

La sustitución autorizada es una operación distinta y trazable.

El paquete comercial utiliza los Items `Service` oficiales del ERP y la configuración funcional de Spa, sin duplicar el catálogo comercial de Items.

### Regla 2 — Vigencia

La vigencia es configurable por paquete y comienza al crear su contrato.

Al vencer:

- No admite nuevas reservas vinculadas al contrato.
- No admite nuevos consumos de sesiones.
- Las citas futuras que ya existan se identifican para revisión.
- No se cancelan automáticamente esas citas.
- Se conserva el historial de derechos y movimientos.

No inventar un plazo predeterminado.

El mecanismo técnico de vencimiento, cálculo temporal, actualización de estado y ejecución programada queda pendiente de ADR-051.

No asumir que la expiración comercial elimina el expediente, tratamiento o registro clínico.

### Regla 3 — Cupos y saldo

El saldo disponible se calcula a partir de movimientos comerciales trazables.

Distinguir explícitamente:

- Sesiones contratadas por servicio.
- Sesiones comercialmente consumidas.
- Sesiones disponibles.
- Sesiones programadas.
- Sesiones realizadas clínicamente.

Las citas programadas se presentan por separado y no descuentan saldo.

No obstante, el sistema debe impedir comprometer más sesiones futuras de las disponibles para cada servicio del paquete.

Esto requiere validar cupos al programar y reprogramar citas vinculadas al contrato, incluyendo operaciones concurrentes.

**Distinción obligatoria:** una cita programada ocupa temporalmente capacidad de programación del paquete, pero no genera un movimiento comercial `Consumed`.

Los mecanismos exactos de reserva lógica de cupos, saldos derivados, locks y concurrencia quedan pendientes de diseño técnico.

### Regla 4 — Consumo comercial

Una sesión clínica finalizada podrá originar el consumo de su cupo mediante un movimiento comercial independiente, sujeto a las reglas autorizadas.

- Un borrador nunca consume.
- Una cita programada nunca consume.
- Reprogramar no consume.
- Una sesión finalizada no genera por sí sola un descuento automático.
- Una atención parcial o interrumpida requiere una resolución comercial autorizada antes de cualquier consumo.

Separar el registro clínico del movimiento comercial.

No duplicar movimientos de consumo ante reintentos.

El catálogo definitivo de acciones, permisos y mecanismos de idempotencia continúa como diseño técnico pendiente.

### Regla 5 — Ausencias

La ausencia del cliente se registra y conserva su trazabilidad.

No se descuenta automáticamente una sesión por ausencia.

**SPA V1.1 no incorpora penalización comercial automática por inasistencia.**

La ausencia no genera una sesión clínica realizada.

Esta regla complementa O-25 y resuelve, para V1.1, la alternativa de penalización automática por ausencia.

No crear cargos monetarios, facturas o cobros a partir de una ausencia.

### Regla 6 — Cancelaciones

Cancelar una cita antes de consumir una sesión no descuenta saldo comercial.

Reprogramar tampoco lo descuenta.

Cuando exista un movimiento de consumo ya confirmado, cancelar posteriormente una cita o modificar el registro de atención no lo revierte automáticamente.

Cualquier corrección del consumo deberá realizarse mediante una operación autorizada de compensación, con motivo, autor y trazabilidad.

Mantener la cancelación de cita separada de la cancelación de tratamiento, contrato de paquete o documento comercial.

No inventar cancelación automática del contrato ni devolución monetaria.

### Regla 7 — Ampliaciones

Se permite agregar sesiones a un paquete mediante una operación explícita autorizada.

Toda ampliación de derechos comerciales debe estar respaldada por el documento comercial oficial correspondiente.

La ampliación:

- Conserva el contrato y su historial.
- Identifica el servicio y la cantidad agregada.
- Registra el documento comercial que respalda los nuevos derechos.
- Genera movimientos trazables sin modificar movimientos históricos.
- No crea facturas, CxC, cobros ni asientos desde Spa.

La relación técnica entre el contrato original y las nuevas líneas comerciales queda pendiente de definición en ADR-051.

No asumir ampliaciones gratuitas ni aumentos de saldo sin respaldo comercial.

### Regla 8 — Sustituciones

La sustitución de un servicio por otro requiere:

- Operación explícita.
- Usuario autorizado.
- Equivalencia definida.
- Identificación de los servicios y cantidades afectados.
- Motivo y trazabilidad.

No se permite convertir arbitrariamente sesiones entre servicios.

No alterar registros clínicos históricos ni movimientos comerciales ya confirmados.

La política exacta para determinar equivalencias y cuándo se requiere un documento comercial adicional permanece pendiente de definición técnica/comercial.

### Regla 9 — Devoluciones y documentos oficiales

Toda nota de crédito o anulación comercial se gestiona exclusivamente mediante Ventas y los procesos oficiales del ERP.

Spa no emite ni modifica documentos comerciales.

Cuando una nota de crédito o anulación afecte derechos de un paquete:

- Identificar el contrato y las sesiones afectadas.
- Revisar los derechos consumidos y pendientes.
- Determinar el ajuste que corresponda mediante una operación autorizada.
- Conservar la trazabilidad y la referencia al documento comercial.
- No revertir automáticamente registros clínicos ni atenciones realmente realizadas.

El tratamiento fiscal, las restricciones frente a facturas a crédito, la autorización SRI y los ajustes económicos específicos requieren verificación y decisiones adicionales.

No crear procedimientos paralelos de devolución monetaria dentro de Spa.

### Regla 10 — Profesionales y sucursales

Un paquete puede utilizarse con diferentes profesionales habilitados y en diferentes sucursales de la misma empresa, respetando:

- La vigencia del contrato.
- Los servicios contratados.
- La habilitación y especialidad del profesional.
- La disponibilidad real.
- Las condiciones aplicables de atención.
- Los permisos y restricciones oficiales.

No se permiten consumos de paquetes entre empresas distintas.

La movilidad entre sucursales no elimina la obligación de verificar disponibilidad laboral y restricciones temporales de SPA-ARQ-04.

### 16.2 Estado de O-13 y alcance pendiente

**O-13 PARCIALMENTE RESUELTA, no cerrada.** Resueltos: composición/cantidades por servicio sin intercambio libre; inicio/efectos de vigencia; distinción de saldo, programación y clínica; prevención de sobreprogramación concurrente sin consumo; consumo independiente/no duplicado; ausencia sin penalización automática V1.1 ni documentos financieros; cancelación sin descuento previo ni reversión automática posterior, con compensación autorizada; ampliaciones respaldadas; requisitos de sustitución; revisión autorizada por NC/anulación y movilidad dentro de la misma empresa. Pendientes: mecanismos técnicos, equivalencias exactas/documento adicional de sustitución, transferencia de beneficiario, vigencia resultante de ampliaciones, resoluciones comerciales de parciales, compensaciones económicas y ajustes fiscales/crediticios específicos. No se eligen valores ni condiciones adicionales.

### 16.3 Límites expresos (texto del propietario)

Esta decisión no aprueba automáticamente:

- Algoritmo definitivo de saldo.
- Modelo físico del libro comercial.
- Bloqueos e índices PostgreSQL.
- Política de expiración mediante jobs.
- Equivalencias exactas de sustitución.
- Definición fiscal/SRI de factura autorizada.
- Tratamiento contable de devoluciones.
- Política de compensaciones económicas.
- Acceso Admin a datos sensibles.
- Conservación legal y firma.
- Nuevas capacidades del módulo Empleados.
- Diseño de Anticipos de Clientes.

Registrar únicamente los pendientes reales que aún condicionan el diseño.

Referencia funcional principal: [contrato §11](SPA-V1.1-CONTRACT-SPEC-01.md#11-paquetes-comerciales). ADR-051 continúa PROPOSED / NO APROBADO; F0–F8 PENDING / NO AUTORIZADA. Ninguna implementación o nueva capacidad de Empleados autorizada.

## 17. SPA-ARQ-06 — Expedientes y seguridad

**APROBADA POR EL PROPIETARIO — 2026-11-19.** Complementa SPA-DEC-116, SPA-ARQ-01..05, ADR-050 y O-15/O-25 sin alterar sus enunciados ni autorizar implementación.

### 17.1 Ocho reglas aprobadas (texto del propietario)

### Regla 1 — Expediente único

Existirá un expediente de SPA por cliente y empresa.

Las atenciones realizadas en distintas sucursales de una misma empresa formarán parte del mismo expediente.

- No crear expedientes separados por sucursal.
- No duplicar la identidad del cliente.
- Reutilizar BusinessPartner como identidad oficial.
- Distinguir el expediente de cada empresa aun cuando el BusinessPartner sea compartido dentro del tenant.
- Conservar la sucursal de cada valoración, atención, sesión y registro correspondiente.

Esta regla confirma el alcance previamente aprobado por SPA-DEC-116.

### Regla 2 — Acceso profesional

Solamente usuarios expresamente autorizados para consultar o modificar información clínica podrán acceder a expedientes.

- Los permisos de Ventas no conceden acceso clínico.
- Los permisos de Empleados no conceden acceso clínico.
- Las asignaciones laborales no equivalen automáticamente a permisos clínicos.
- La consulta, modificación y corrección deben corresponder a las facultades del usuario.
- El vínculo entre profesional y usuario registrador debe permanecer diferenciado.

La política técnica debe integrarse con la arquitectura oficial de Identity, Access y Kernel.

No crear un sistema paralelo de autenticación o permisos.

### Regla 3 — Acceso del administrador

El administrador podrá gestionar usuarios, roles y permisos conforme a sus responsabilidades, pero **su condición de administrador no debe otorgarle acceso ordinario al contenido clínico**.

El acceso excepcional a información clínica requerirá:

- Justificación explícita.
- Autorización conforme a una política controlada.
- Identificación del usuario.
- Registro auditable del acceso.
- Delimitación de su alcance.

**Condición arquitectónica obligatoria:**

El ERP actual utiliza un mecanismo de bypass para Admin en determinadas políticas de permisos.

Por tanto, esta regla es un **objetivo de seguridad aprobado**, pero su aplicación requiere una revisión arquitectónica especial del contrato LOCKED de Identity/Access/Kernel.

No asumir que agregar permisos `spa.*` resuelve el bypass.

No seleccionar unilateralmente una implementación de permisos sellados, concesiones clínicas o acceso excepcional.

Registrar C-01/O-01 como:

**POLÍTICA FUNCIONAL APROBADA — MECANISMO TÉCNICO BLOQUEADO HASTA AUTORIZACIÓN ESPECIAL.**

No declarar cerrado el conflicto técnico.

### Regla 4 — Acceso entre sucursales

Los profesionales podrán acceder a expedientes de otras sucursales de su misma empresa únicamente cuando su función y sus permisos lo justifiquen.

- El expediente sigue siendo único por empresa.
- La sucursal de atención permanece registrada.
- La asignación laboral a una sucursal no concede por sí sola acceso clínico.
- Un profesional no obtiene acceso general a todos los expedientes únicamente por trabajar en varias sucursales.
- El acceso debe evaluarse según contexto empresarial, permisos clínicos y política autorizada.

El mecanismo detallado de autorización entre sucursales permanece pendiente de ADR-051.

### Regla 5 — Auditoría de accesos y cambios

El sistema deberá registrar las operaciones sensibles sobre expedientes, incluyendo:

- Consultas.
- Creación.
- Modificación.
- Corrección.
- Acceso a fotografías.
- Acceso a consentimientos.

La auditoría deberá identificar:

- Usuario responsable.
- Fecha y hora.
- Expediente o recurso afectado.
- Tipo de operación.
- Contexto de empresa y sucursal cuando corresponda.
- Resultado y motivo cuando aplique.

**No almacenar contenido clínico sensible en los registros de auditoría.**

La infraestructura oficial de auditoría del ERP se mantiene FROZEN.

Se podrá estudiar una extensión específica de SPA para registrar consultas exitosas, sin modificar esa infraestructura por inferencia.

La elección entre auditoría síncrona, asíncrona o mixta, así como el comportamiento ante errores, sigue pendiente de aprobación técnica.

### Regla 6 — Fotografías y archivos privados

Los documentos, fotografías y archivos clínicos deberán conservarse mediante almacenamiento privado y controles de acceso apropiados.

Requisitos funcionales:

- Acceso únicamente autorizado.
- Ausencia de enlaces públicos permanentes.
- Protección de los archivos durante su transmisión y almacenamiento.
- Cifrado adecuado conforme al diseño de seguridad aprobado.
- Custodia y protección de claves.
- Trazabilidad de accesos.
- Aislamiento entre empresas.
- Asociación con expediente, atención, finalidad y autorización cuando corresponda.

No modificar automáticamente `IFileStorage`, `LocalFileStorage` ni los scripts oficiales de respaldo.

El mecanismo exacto de cifrado, administración de claves, almacenamiento, descifrado, restauración y protección de respaldos debe quedar pendiente de revisión técnica y operativa.

Esta aprobación **no crea un sistema nuevo de respaldos**, funcionalidad expresamente excluida de SPA V1.1.

### Regla 7 — Correcciones, conservación y eliminación legal

Las correcciones de expedientes y registros clínicos deberán conservar trazabilidad.

Una versión anterior no deberá presentarse como información vigente después de una rectificación.

La política de conservación deberá contemplar:

- Correcciones autorizadas.
- Versiones y trazabilidad.
- Restricción de acceso a información sustituida.
- Conservación durante los plazos legalmente aplicables.
- Rectificación, supresión o anonimización cuando corresponda jurídicamente.
- Ejecución controlada y auditable de medidas legalmente exigibles.

**Condición obligatoria:**

La regla general del proyecto de no realizar borrados físicos no resuelve por sí sola los derechos de protección de datos.

Registrar C-02/O-05 como:

**OBJETIVO FUNCIONAL APROBADO — PROCEDIMIENTO Y VALIDACIÓN LEGAL PENDIENTES.**

No aprobar borrado físico general ni eliminar versiones indiscriminadamente.

No afirmar cumplimiento de la legislación ecuatoriana sin evidencia jurídica suficiente.

### Regla 8 — Protección por empresa

Toda operación sobre expedientes y sus datos relacionados deberá validar el contexto empresarial correspondiente.

- No permitir consultar expedientes de otra empresa por error.
- No permitir editar, corregir o descargar información de otra empresa sin autorización y alcance válidos.
- Un usuario con acceso a varias empresas debe operar dentro del contexto autorizado de cada una.
- Las referencias a BusinessPartner, Employee, sucursal, consentimiento, sesión y archivo deben respetar sus propietarios y ámbitos oficiales.
- La agenda no debe exponer información clínica mediante listados o consultas operativas.

Esta regla debe respetar la arquitectura de aislamiento de tenants y empresas del ERP.

**No cambiar el ámbito oficial de BusinessPartner**, que conserva su contrato propio.

### 17.2 Decisiones abiertas y límites (texto del propietario)

SPA-ARQ-06 aprueba las ocho reglas funcionales, pero no resuelve automáticamente:

- Mecanismo para impedir acceso clínico ordinario mediante bypass Admin.
- Contrato especial de autorizaciones excepcionales.
- Política técnica de acceso clínico entre sucursales.
- Persistencia y confiabilidad del registro de lecturas.
- Comportamiento ante fallos de auditoría.
- Diseño definitivo del almacenamiento privado.
- Cifrado de archivos y campos sensibles.
- Custodia, rotación, recuperación y protección de claves.
- Política concreta de conservación y eliminación legal.
- Validación jurídica aplicable en Ecuador.
- Condiciones técnicas de los registros de fotografías y consentimientos.

Mantener estas cuestiones como pendientes de arquitectura, seguridad o validación legal.

No transformar una recomendación técnica anterior en decisión aprobada.

### 17.3 Estados parciales y referencia funcional

- **C-01/O-01: POLÍTICA FUNCIONAL APROBADA — MECANISMO TÉCNICO BLOQUEADO HASTA AUTORIZACIÓN ESPECIAL.** No se cierra el conflicto de bypass ni se modifica Identity/Access/Kernel LOCKED.
- **O-02: POLÍTICA FUNCIONAL APROBADA — MECANISMO TÉCNICO PENDIENTE.** Acceso intersucursal solo justificado por función/permisos/contexto; no acceso general por asignación laboral.
- **O-03: OBLIGACIÓN FUNCIONAL APROBADA — PERSISTENCIA, CONFIABILIDAD, MODO Y ERRORES PENDIENTES.** Auditoría sensible sin contenido clínico; infraestructura FROZEN intacta.
- **O-04: PROTECCIÓN FUNCIONAL APROBADA — DISEÑO TÉCNICO Y OPERATIVO PENDIENTE.** Almacenamiento privado, cifrado adecuado, claves y aislamiento; sin cambios implícitos a almacenamiento ni respaldos, sin sistema nuevo de backups.
- **C-02/O-05: OBJETIVO FUNCIONAL APROBADO — PROCEDIMIENTO Y VALIDACIÓN LEGAL PENDIENTES.** No borrado general, eliminación indiscriminada ni certificación de cumplimiento.

Referencia normativa única: [contrato §21](SPA-V1.1-CONTRACT-SPEC-01.md#21-seguridad-y-privacidad); registro conserva evidencia, ADR alternativas y plan fases. ADR-051 PROPOSED / NO APROBADO; F0–F8 PENDING / NO AUTORIZADA.

## 18. SPA-ARQ-07 — Valoraciones, consentimientos y fotografías

**APROBADA POR EL PROPIETARIO — 2026-11-19.** Complementa SPA-DEC-116 y SPA-ARQ-03/06, preservando SPA-ARQ-01..06, O-15/O-25, ADR-050 y Empleados oficial; no autoriza implementación.

### 18.1 Diez reglas aprobadas (texto del propietario)

### Regla 1 — Valoración facial

La valoración facial deberá permitir documentar:

- Antecedentes relevantes.
- Tipo y condición de piel.
- Sensibilidad.
- Fototipo Fitzpatrick I–VI, cuando se evalúe.
- Alteraciones observadas.
- Precauciones o contraindicaciones relevantes.
- Objetivos del tratamiento.

Conservar los campos adicionales ya documentados en el contrato funcional.

No convertir la valoración estética en un diagnóstico médico automático.

La obligatoriedad específica de cada dato dependerá del procedimiento y de las reglas que se aprueben.

### Regla 2 — Valoración corporal

La valoración corporal deberá permitir registrar:

- Peso en kilogramos.
- Brazo derecho e izquierdo, en centímetros.
- Abdomen superior, medio e inferior, en centímetros.
- Cadera, en centímetros.
- Muslo derecho e izquierdo, en centímetros.

Conservar las denominaciones oficiales del contrato, sin sustituir las tres mediciones abdominales por una única «cintura».

No imponer valores, rangos o decimales sin validación del estándar oficial de precisión del ERP.

### Regla 3 — Mediciones iniciales, de control y finales

Las mediciones deberán identificarse según su finalidad:

- Inicial.
- Control.
- Final.

Los campos no medidos podrán permanecer vacíos.

**No medida no equivale a cero.**

Conservar fecha, contexto del expediente y profesional responsable.

No imponer que todas las medidas sean obligatorias ni una cantidad fija de controles.

### Regla 4 — Seguimiento y comparación

El sistema permitirá consultar cronológicamente valoraciones y mediciones, diferenciando los registros iniciales, de control y finales.

La comparación deberá mostrar las variaciones documentadas cuando existan valores comparables.

No atribuir automáticamente cambios corporales o faciales a un procedimiento ni emitir diagnósticos médicos.

No generar resultados inventados cuando falten mediciones.

### Regla 5 — Formularios y borradores

Se permitirá guardar valoraciones incompletas como borradores para continuarlas posteriormente.

La obligatoriedad de los campos dependerá del procedimiento y de las reglas funcionales correspondientes.

No imponer una obligatoriedad universal para todos los procedimientos.

El guardado como borrador no acredita por sí solo una atención realizada ni genera consumo comercial.

Mantener coherencia con SPA-ARQ-03 sin confundir el borrador de una valoración con una sesión clínica finalizada.

### Regla 6 — Consentimiento por procedimiento

Cuando un procedimiento requiera consentimiento, el sistema deberá presentar el documento correspondiente antes de realizar la atención.

Registrar:

- Procedimiento al que corresponde.
- Versión del consentimiento presentada.
- Decisión de la persona autorizada.
- Firma en pantalla.
- Información de identificación y trazabilidad que corresponda.

La captura de firma en pantalla es una funcionalidad aprobada por SPA-DEC-116.

La autenticidad técnica, la validez jurídica, las reglas de vigencia y los requisitos específicos de identificación siguen sujetos a revisión.

No equiparar automáticamente la firma dibujada en pantalla con una firma electrónica certificada.

### Regla 7 — Autorizaciones independientes

Mantener separadas las autorizaciones para:

1. Realizar el procedimiento correspondiente.
2. Capturar y utilizar fotografías de seguimiento.
3. Utilizar fotografías o información con fines promocionales.

Aceptar una autorización no implica aceptar las demás.

El uso académico continúa sujeto a O-17 y no se incorpora como capacidad aprobada por esta decisión.

La finalidad autorizada deberá respetarse al consultar o utilizar el material correspondiente.

### Regla 8 — Fotografías y archivos

Las fotografías deberán vincularse al expediente y, cuando corresponda, a la valoración o sesión.

Registrar la información necesaria para conocer:

- Fecha de captura.
- Contexto de atención.
- Finalidad de utilización.
- Autorización aplicable.
- Profesional o usuario responsable según corresponda.

Los archivos serán privados y su acceso estará restringido conforme a SPA-ARQ-06.

No utilizar enlaces públicos permanentes.

No autorizar publicación automática de fotografías.

El almacenamiento físico, el cifrado, las claves y los mecanismos técnicos definitivos permanecen pendientes del ADR-051.

### Regla 9 — Correcciones trazables

Las valoraciones, mediciones y registros de consentimiento deberán conservar trazabilidad de sus correcciones o cambios.

Registrar quién efectuó el cambio, cuándo y su motivo.

No sobrescribir silenciosamente la evidencia anterior ni presentar información sustituida como vigente.

Los retiros de consentimiento y las rectificaciones de datos deberán respetar la finalidad y la normativa aplicable.

La política definitiva de conservación, rectificación, supresión o anonimización permanece pendiente de validación jurídica.

Esta regla no autoriza conservar indefinidamente datos que legalmente deban suprimirse.

### Regla 10 — Protección y validación

Todo el bloque deberá respetar:

- Permisos clínicos específicos.
- Aislamiento por tenant y empresa.
- Acceso intersucursal autorizado.
- Protección del contenido sensible.
- Auditoría de accesos y modificaciones.
- Requisitos de privacidad y consentimiento.
- Separación entre profesional y usuario registrador.

**SPA-ARQ-07 no resuelve por inferencia:**

- Precisión y escala numérica definitiva de las mediciones.
- Requisitos jurídicos de la firma.
- Consentimiento para menores y representantes.
- Plazos y condiciones legales de conservación.
- Mecanismos definitivos de cifrado y administración de claves.
- Procedimientos de eliminación legal.
- Contenido y versiones exactas de las fichas originales todavía no contrastadas.

### 18.2 Estados parciales y límites

- O-16 PARCIALMENTE RESUELTA: documento previo cuando el procedimiento lo requiera, procedimiento/versión/decisión/firma e identificación/trazabilidad, autorizaciones independientes y correcciones aprobadas; captura/autenticidad, vigencia, identificación específica y validez jurídica pendientes, sin equivalencia con firma electrónica certificada.
- O-17 ABIERTA: uso académico no se incorpora por esta decisión.
- O-18 ABIERTA: menores/representantes y evidencia jurídica no resueltos.
- O-21 PARCIALMENTE RESUELTA: contenido funcional y borradores aprobados, sin obligatoriedad universal; mínimos por procedimiento/validaciones concretas pendientes.
- O-22 PARCIALMENTE RESUELTA: medidas/unidades, Inicial/Control/Final, vacíos distintos de cero y comparación aprobados; precisión/escala V-04, rangos y frecuencia concreta/final único pendientes, sin cantidad fija de controles.
- O-24 ABIERTA: contenido y versiones originales V-01 aún no contrastados; esta aprobación no certifica equivalencia ni inspección.

Referencia normativa única: contrato [§14](SPA-V1.1-CONTRACT-SPEC-01.md#14-valoración-facial), [§15](SPA-V1.1-CONTRACT-SPEC-01.md#15-valoración-corporal), §16/17/18/21 según regla. Registro es evidencia, ADR diseño candidato y plan fases. V-01/04/06/07/08/09 siguen abiertos en sus aspectos pendientes; C-01/C-02 y seguridad/ley SPA-ARQ-06 intactos. ADR-051 PROPOSED / NO APROBADO; F0–F8 PENDING / NO AUTORIZADA.

## 19. SPA-ARQ-08 — Integraciones con ERP ZH

**APROBADA POR EL PROPIETARIO — 2026-11-19.** Complementa SPA-DEC-115/116, O-15/O-25, SPA-ARQ-01..07, ADR-050 y DIR-SSOT-01, sin reescribir ni ampliar decisiones anteriores.

### 19.1 Ocho reglas aprobadas (texto del propietario)

### Regla 1 — Clientes y BusinessPartner

SPA utilizará el BusinessPartner oficial con rol Customer.

- No creará un catálogo paralelo de clientes.
- No duplicará nombres, identificaciones ni información comercial cuya fuente oficial sea BusinessPartner.
- Cada expediente clínico pertenecerá a la empresa correspondiente.
- La identidad de BusinessPartner conservará su ámbito oficial dentro del tenant.
- Las operaciones deberán respetar el aislamiento empresarial aprobado en SPA-ARQ-06.

La consulta de datos se realizará mediante contratos autorizados del módulo propietario.

### Regla 2 — Profesionales y Empleados

Los profesionales provendrán del módulo general de Empleados.

SPA consumirá un contrato de lectura oficial para consultar identidad laboral, asignaciones y elegibilidad.

- Empleados administra fichas y asignaciones laborales.
- SPA administra horarios de atención, agenda, procedimientos y sesiones.
- Profesional responsable y usuario ERP registrador son conceptos diferentes.
- No se crea otra entidad laboral Employee dentro de SPA.
- Los permisos de Empleados no conceden acceso clínico.

El directorio `IEmployeeDirectory` es una dependencia contractual pendiente de implementación bajo ADR-050 y su plan correspondiente.

Esta decisión no autoriza implementar Empleados.

### Regla 3 — Servicios e Items

SPA utilizará los Items oficiales de naturaleza `Service`.

- El precio y los impuestos pertenecen a los mecanismos comerciales del ERP.
- SPA administra duración, preparación, condiciones de atención y datos operativos del servicio.
- No se crea otro catálogo comercial de servicios.
- La configuración de procedimientos deberá respetar la relación servicio–procedimiento que se defina técnicamente.
- La integración debe permitir servicios individuales y paquetes combinados.

No modificar Items ni Pricing por inferencia.

### Regla 4 — Ventas y contratos de paquetes

SPA creará explícitamente el contrato de paquete después de validar una factura autorizada, conforme a O-15.

Se conservarán sus siete reglas:

1. Creación explícita tras validar factura autorizada.
2. Referencia a factura y línea.
3. Operación fuera de la transacción de autorización de Ventas.
4. Prevención de contratos duplicados.
5. Un error de SPA no revierte la factura.
6. SPA no crea facturas, CxC, cobros ni asientos.
7. No se utiliza `SalesInvoiceAuthorizedEvent` para crear contratos automáticamente.

Las ampliaciones, sustituciones y ajustes de paquetes respetarán SPA-ARQ-05 y los documentos comerciales oficiales que correspondan.

Esta aprobación no determina por sí misma el estado fiscal/SRI requerido ni la política frente a facturas a crédito y notas de crédito.

### Regla 5 — Caja y Cuentas por Cobrar

SPA utilizará únicamente la información comercial autorizada que necesite de los módulos oficiales.

- Los cobros corresponden a Caja y los procesos oficiales.
- Las cuentas pendientes y saldos monetarios corresponden a CxC.
- SPA administra derechos comerciales expresados en sesiones, no saldos monetarios.
- Una atención clínica no crea automáticamente un cobro.
- SPA no crea otra Caja, otra CxC ni una contabilidad paralela.

**Anticipos de Clientes** permanece como un proceso transversal independiente del ERP, conforme a DIR-SSOT-01.

No cerrar O-14 ni introducir anticipos propios de SPA.

### Regla 6 — Inventario y productos

SPA podrá registrar los productos utilizados durante una atención con fines técnicos y de seguimiento.

Ese registro no generará automáticamente movimientos de Kardex.

Las ventas de productos físicos seguirán el proceso comercial y de inventario oficial.

No crear un módulo de inventario paralelo ni inventar movimientos automáticos de consumo de insumos.

Conservar la exclusión HX-05.

### Regla 7 — Seguridad y auditoría

SPA reutilizará la arquitectura oficial de Identity, Access, Kernel y auditoría.

- No creará otro sistema de usuarios.
- No creará autenticación paralela.
- No duplicará el sistema general de permisos.
- Los accesos clínicos estarán sujetos a SPA-ARQ-06.
- La auditoría de consultas y modificaciones deberá cumplir las reglas funcionales aprobadas.
- Las extensiones técnicas necesarias deberán integrarse sin modificar módulos LOCKED/FROZEN por inferencia.

La política de acceso ordinario del Admin a datos clínicos está aprobada en SPA-ARQ-06, pero su mecanismo sigue bloqueado por C-01/O-01.

Conservar también C-02/O-05 y los demás pendientes de privacidad y conservación.

### Regla 8 — Módulo genérico y multiempresa

SPA V1.1 será un módulo genérico de ERP ZH, utilizable por diferentes empresas del sector.

- No dependerá de nombres comerciales específicos.
- Respetará tenant, empresa y sucursal.
- Las consultas y operaciones validarán el contexto empresarial.
- Los expedientes serán únicos por cliente y empresa.
- Los paquetes y derechos de consumo no se trasladarán entre empresas.
- Los profesionales deberán cumplir las reglas de elegibilidad y disponibilidad correspondientes.
- La interfaz utilizará la denominación SPA.
- SPA conservará sus responsabilidades sin duplicar las de otros módulos.

Las menciones a «Gali Spa» podrán permanecer únicamente como atribución de evidencia histórica, no como dependencia funcional.

### 19.2 Límites de la aprobación (texto del propietario)

SPA-ARQ-08 aprueba las fronteras funcionales entre módulos, pero no autoriza automáticamente:

- Implementar Empleados o `IEmployeeDirectory`.
- Extender BusinessPartner, Items, Ventas o CxC.
- Modificar el Kernel LOCKED.
- Modificar la auditoría FROZEN.
- Cambiar `IFileStorage`.
- Crear contratos API definitivos.
- Crear migraciones.
- Seleccionar índices, locks o algoritmos de concurrencia.
- Resolver O-14 sobre Anticipos.
- Aprobar políticas fiscales o legales todavía pendientes.

Las dependencias técnicas deberán resolverse dentro del proceso de revisión arquitectónica del ADR-051, con las autorizaciones especiales que correspondan.

### 19.3 Fuente única y autorización

Referencia normativa principal: [contrato §4](SPA-V1.1-CONTRACT-SPEC-01.md#4-dependencias-y-límites-con-otros-módulos). Registro conserva aprobaciones, ADR arquitectura y plan fases. Las fronteras aprobadas no implementan ni aprueban firmas de contratos de lectura, nuevas entidades/APIs, índices/locks o extensiones. IEmployeeDirectory sigue dependencia pendiente bajo ADR-050/plan propio; O-14 independiente y abierto; C-01/O-01 y C-02/O-05 visibles, fiscal/ley pendientes. ADR-051 PROPOSED / NO APROBADO; F0–F8 PENDING / NO AUTORIZADA. No preparación de código ni implementación de SPA o Empleados.

## 20. SPA-TEC-01 — Nueve decisiones técnicas

**APROBADA POR EL PROPIETARIO — 2026-11-19.** Fuente: autorización expresa de consolidación documental única. Texto aprobado y límites completos:

## Nueve decisiones técnicas aprobadas

### TEC-01.1 — Modelo de visitas, citas y prestaciones

Aprobar la dirección arquitectónica:

- Una visita puede agrupar varias citas.
- Cada cita representa un bloque temporal de atención de un profesional.
- Una prestación identificable se vincula a uno o varios bloques profesionales.
- No duplicar una prestación comercial cuando intervienen varios profesionales.
- Mantener la situación de visita derivada de sus citas.
- Conservar llegada y reprogramación como eventos, no como estados adicionales.

La selección definitiva de agregados, identificadores, claves y persistencia se realizará dentro del diseño técnico de ADR-051.

No modificar los cinco estados de cita ya aprobados.

### TEC-01.2 — Persistencia clínica

Aprobar:

- Borradores editables.
- Inicio y cierre explícito de atención.
- Interrupciones documentadas.
- Diferenciación entre atención completa, parcial e interrumpida.
- Correcciones versionadas y trazables.
- Separación del registro clínico respecto al movimiento comercial de consumo.

Conservar SPA-ARQ-03 y SPA-ARQ-07.

El diseño físico y la retención/versionado sujetos a obligaciones legales permanecen pendientes de especificación técnica y validación aplicable.

### TEC-01.3 — Modelo temporal de agenda

Aprobar:

- Horarios recurrentes.
- Excepciones por fecha.
- Intervalos UTC generados utilizando la zona horaria oficial de la empresa.
- Reutilización de `CompanyTimeZone` e `ICompanyClock`, conforme a sus contratos reales.
- Intervalos semiabiertos `[inicio, fin)` para permitir citas contiguas sin solape.
- Preparación y traslados como restricciones adicionales de disponibilidad.

No crear un sistema temporal paralelo.

Respetar las reglas oficiales para horas ambiguas o inexistentes.

### TEC-01.4 — Concurrencia de reservas

Aprobar como dirección técnica:

- Revalidación de disponibilidad en la transacción de reserva.
- Bloqueos coordinados por profesional, sin incluir la sucursal en la identidad del recurso bloqueado.
- Orden consistente de adquisición de bloqueos cuando participan varios profesionales.
- Comprobación de preparación, traslados y ausencias.
- Restricciones adicionales de PostgreSQL cuando sean compatibles con el esquema aprobado.
- Reprogramación atómica: ante conflicto, conservar la cita original.

No declarar `tstzrange`, índices, locks SQL o migraciones concretas como ya implementados.

El mecanismo físico final requiere verificación y aprobación técnica específica.

### TEC-01.5 — Libro de movimientos comerciales

Aprobar:

- Derechos separados por servicio.
- Libro de movimientos trazables.
- Operaciones idempotentes.
- Consumos independientes del cierre clínico.
- Compensaciones mediante nuevos movimientos, sin alterar históricos.
- Reserva lógica de cupos programados sin marcarlos como consumidos.
- Control de capacidad por prestación comercial, no por cantidad de profesionales.

Mantener O-15/O-25 y SPA-ARQ-05.

### TEC-01.6 — Auditoría clínica durable

Aprobar:

- Registrar accesos a información clínica sensible.
- Persistencia durable y comprobada antes de entregar contenido protegido.
- Si el registro obligatorio falla, no entregar dicho contenido.
- Si se deniega un acceso, un fallo de auditoría nunca lo transforma en autorización.
- Registrar metadatos necesarios sin copiar contenido clínico sensible.
- Mantener los cambios clínicos dentro de una trazabilidad transaccional adecuada.

**Condición técnica importante:** llamar a `EfAuditWriter.RecordAsync` no equivale por sí solo a haber confirmado una transacción.

Seleccionar un mecanismo compatible con el sistema oficial de auditoría, sin modificar su contrato FROZEN por inferencia.

### TEC-01.7 — Almacenamiento privado

Aprobar como dirección técnica:

- Adaptador específico de SPA sobre mecanismos oficiales autorizados.
- Identificadores opacos.
- Rutas controladas y validadas.
- Acceso por recurso y autorización en cada descarga.
- Prohibición de enlaces públicos permanentes.
- Cifrado adecuado de archivos sensibles.
- Gestión segura de claves y restauración.

**No aprobar por inferencia un proveedor externo, un algoritmo concreto, un esquema definitivo de claves ni cambios de infraestructura compartida.**

La custodia de claves, cifrado efectivo, recuperación, restauración y protección de respaldos siguen requiriendo diseño operativo y validación.

No crear un sistema nuevo de respaldos.

### TEC-01.8 — Integraciones oficiales

Aprobar contratos mínimos de lectura con los módulos propietarios.

- BusinessPartner conserva su ámbito real dentro del tenant.
- Empleados conserva los datos laborales.
- Items y Pricing conservan servicios, precios e impuestos.
- Ventas conserva facturas y documentos comerciales.
- Caja y CxC conservan pagos y saldos.
- SPA conserva datos clínicos, agenda y derechos de sesiones.

No acceder directamente a repositorios internos de otros módulos ni duplicar sus datos maestros.

`IEmployeeDirectory` continúa como dependencia no implementada, sujeta a ADR-050.

**Corrección del diagnóstico:** no afirmar que existe una bandera de habilitación Customer por empresa en BusinessPartner. El rol Customer es tenant-scoped; el expediente SPA sí conserva ámbito empresarial.

### TEC-01.9 — Precisión de mediciones

Aprobar como propuesta arquitectónica preferente:

- Uso de tipos decimales anulables para mediciones opcionales.
- Candidato `decimal?` en aplicación y `numeric(10,2)` en persistencia para kg y cm.
- No equivalencia entre valor ausente y cero.
- Comparaciones sin errores de representación binaria.

**No declarar `numeric(10,2)` como estándar definitivo aprobado.**

Debe completarse la revisión formal de precisión del dominio, resolución necesaria, almacenamiento y límites operativos antes de implementar.

**Delimitación:** decisión funcional/dirección técnica aprobadas solo en su alcance; mecanismo detallado pendiente, autorización especial pendiente, validación jurídica/fiscal pendiente y dependencia no implementada se distinguen según cada regla. No cierre integral de C-01/C-02/O-13, no cierre O-14, no modificación LOCKED/FROZEN/CLOSED. ADR-051 PROPOSED / NO APROBADO; F0–F8 PENDING / NO AUTORIZADA. Ninguna implementación ni operación Git de escritura autorizada por esta entrada.

## 21. SPA-TEC-02 — Seguridad Admin, cierre de citas interrumpidas y cupos

**APROBADA POR EL PROPIETARIO — 2026-11-19.** Fuente: autorización expresa de consolidación documental única. Texto aprobado y límites completos:

## Seguridad Admin, citas interrumpidas y cupos comerciales

### TEC-02.A — Seguridad clínica y C-01

Registrar seis reglas aprobadas:

1. Admin no obtiene acceso clínico ordinario por su condición administrativa.
2. El backend exige autorización clínica específica y evaluación del recurso protegido, incluso frente al bypass general.
3. Los accesos excepcionales requieren motivo explícito y autorización de otra persona facultada. No se permite autoautorización.
4. Las concesiones excepcionales son temporales, delimitadas por recurso y acción, y auditadas.
5. Si no puede comprobarse la autorización o persistirse la auditoría obligatoria, se deniega la entrega del contenido sensible.
6. La implementación requiere autorización especial para revisar el contrato LOCKED de Identity/Access/Kernel.

**Diferenciar rigurosamente:**

- Política funcional y dirección de seguridad: APROBADAS.
- Revisión arquitectónica especial del contrato LOCKED: PENDIENTE.
- Modificación del Kernel: NO AUTORIZADA.
- Mecanismo específico de permisos sellados, concesiones y ejecución de excepciones: pendiente de diseño detallado.

No declarar C-01 completamente CLOSED.

Actualizar AT-11 y los apartados pertinentes de ADR-051.

### TEC-02.B — Cierre de citas con atención interrumpida

Registrar las reglas aprobadas:

1. Mantener exclusivamente los cinco estados de cita existentes.
2. La interrupción clínica no cierra automáticamente la cita.
3. Un usuario autorizado podrá ejecutar un cierre administrativo explícito hacia `Atendida` cuando realmente haya existido atención, aunque el procedimiento no haya concluido.
4. `Atendida` indica atención efectiva ocurrida y cierre administrativo, no necesariamente procedimiento clínico completado.
5. El registro clínico conserva `Interrumpida` o su condición parcial. No cambiarlo artificialmente a `Finalizada`.
6. Registrar responsable, fecha, motivo y referencia a la atención.
7. Liberar la ocupación futura al cerrar administrativamente, sin alterar retroactivamente el intervalo original reservado ni borrar eventos de agenda.
8. No generar consumo comercial automático.

Actualizar la correspondencia previamente pendiente entre SPA-ARQ-02 y SPA-ARQ-03.

No crear un sexto estado de cita.

La implementación concreta de transiciones, permisos y efectos temporales sigue sujeta al diseño técnico y a las pruebas futuras.

### TEC-02.C — Cupos de paquetes y atenciones parciales

Registrar diez reglas:

1. Los derechos se administran por servicio dentro de cada contrato.
2. Cada prestación comercial programada reserva un cupo lógico; varios profesionales no multiplican la reserva.
3. Cancelar antes de la atención libera el cupo cuando no existe consumo confirmado.
4. Reprogramar conserva el compromiso comercial, evitando doble reserva.
5. La atención completada permite una resolución de consumo autorizada y separada de su cierre clínico.
6. La atención parcial o interrumpida no consume automáticamente.
7. Mientras esté pendiente la resolución comercial, el cupo permanece retenido.
8. Una resolución comercial autorizada puede consumir el derecho correspondiente o liberar el cupo, registrando responsable, motivo y evidencia.
9. Los ajustes monetarios, notas de crédito y compensaciones económicas corresponden a los módulos comerciales oficiales.
10. Los movimientos son trazables e idempotentes y no reescriben consumos anteriores.

La decisión de consumo o liberación debe respetar las condiciones comerciales aplicables; no se permite convertir arbitrariamente una prestación parcial en una prestación completa.

No interpretar estas reglas como permiso para facturar, cobrar o modificar CxC desde SPA.

**Delimitación:** decisión funcional/dirección técnica aprobadas solo en su alcance; mecanismo detallado pendiente, autorización especial pendiente, validación jurídica/fiscal pendiente y dependencia no implementada se distinguen según cada regla. No cierre integral de C-01/C-02/O-13, no cierre O-14, no modificación LOCKED/FROZEN/CLOSED. ADR-051 PROPOSED / NO APROBADO; F0–F8 PENDING / NO AUTORIZADA. Ninguna implementación ni operación Git de escritura autorizada por esta entrada.

## 22. SPA-TEC-03 — Tratamientos, paquetes y relación con Ventas

**APROBADA POR EL PROPIETARIO — 2026-11-19.** Fuente: autorización expresa de consolidación documental única. Texto aprobado y límites completos:

## Tratamientos, paquetes y relación con Ventas

### 1. Principio arquitectónico aprobado

SPA no tendrá facturación ni cobranza propia.

La relación conceptual aprobada es:

**Plan de tratamiento SPA → Prestaciones del plan → Contratos comerciales SPA → Facturas y líneas oficiales de Ventas → Información oficial de Caja/CxC.**

La relación será explícita mediante identificadores y referencias válidas.

No vincular automáticamente una factura a un plan únicamente por pertenecer al mismo BusinessPartner.

Un plan puede existir antes de su contratación completa.

Un plan puede relacionarse con varias facturas y varios contratos comerciales.

Conservar separadas las situaciones:

- Propuesto.
- Contratado.
- Facturado.
- Pagado.
- Realizado.

No presentar pagos como sesiones consumidas ni reservas como sesiones realizadas.

El contrato del paquete continúa sujeto a O-15: creación explícita después de validar una factura autorizada, con referencia a factura y línea y fuera de la transacción de autorización de Ventas.

### 2. Diez reglas comerciales aprobadas

#### Regla 1 — Sustitución de servicios

Permitir sustituciones únicamente con autorización y equivalencia documentada.

No convertir libremente sesiones de un servicio en otro.

#### Regla 2 — Equivalencias

Las equivalencias deberán constar en las condiciones comerciales aplicables o mediante autorización específica documentada.

Sin equivalencia válida, no realizar la sustitución.

#### Regla 3 — Historial de sustituciones

Conservar:

- Servicio original.
- Servicio sustituto.
- Cantidades.
- Motivo.
- Responsable y autorización.
- Fecha.
- Documento de respaldo.

No sobrescribir ni eliminar derechos o movimientos anteriores para ocultar la sustitución.

#### Regla 4 — Transferencia de beneficiario

No permitir transferencias automáticas.

Una transferencia excepcional requerirá:

- Autorización.
- Respaldo comercial.
- Verificación del alcance de los derechos transferibles.
- Revisión de identidad y consentimientos de las personas involucradas.

Nunca trasladar el expediente clínico de un cliente a otro.

La documentación jurídica específica permanece sujeta a validación.

#### Regla 5 — Ampliaciones

Permitir agregar servicios o sesiones mediante documentos comerciales oficiales válidos.

Cada ampliación deberá conservar:

- Referencia al contrato correspondiente.
- Documento y línea de respaldo.
- Derechos nuevos incorporados.
- Historial de movimientos.

No modificar retrospectivamente el contrato original para ocultar la ampliación.

#### Regla 6 — Vigencia de ampliaciones

Una ampliación no prolonga automáticamente la vigencia de los derechos originales.

Toda extensión requiere aprobación expresa, registro y respaldo aplicable.

Permitir controlar la vigencia correspondiente a los derechos incorporados, sin perder su procedencia.

La estrategia exacta de persistencia y cálculo de vigencias continúa sujeta a diseño técnico.

#### Regla 7 — Vencimientos

Una vez vencidos los derechos:

- No permitir nuevas reservas sobre ellos.
- No permitir nuevos consumos ordinarios sobre ellos.
- Identificar citas previamente programadas para revisión manual.
- No cancelar automáticamente dichas citas.
- Mantener el historial de compromisos y movimientos.

Conservar SPA-ARQ-05.

#### Regla 8 — Cambios comerciales

Las devoluciones, notas de crédito y anulaciones se gestionan en Ventas.

SPA podrá consultar los documentos oficiales vinculados y realizar, mediante autorización, los ajustes correspondientes a los derechos de atención.

No modificar retrospectivamente el historial clínico.

No generar documentos fiscales desde SPA.

La política tributaria exacta y sus consecuencias monetarias requieren validación específica.

#### Regla 9 — Facturación, pagos y cuotas

SPA consultará por medio de contratos oficiales:

- Facturas.
- Líneas facturadas e Items asociados.
- Estado comercial y fiscal disponible.
- Pagos y abonos.
- Cuotas y vencimientos cuando existan.
- Saldos monetarios informados por CxC.

No duplicar tablas financieras, calendarios de cuotas ni lógica de cobros.

No reconstruir saldos monetarios mediante lógica paralela en SPA.

La existencia de cuotas pendientes no suspende automáticamente derechos de sesiones. Cualquier restricción deberá estar fundada en una política comercial expresamente aprobada.

Los estados y condiciones exactos de elegibilidad fiscal/SRI permanecen pendientes de validación.

#### Regla 10 — Trazabilidad del plan

Cada prestación del plan podrá vincularse con los contratos y documentos comerciales que realmente le correspondan.

La interfaz operativa de SPA deberá permitir consultar:

- Servicios propuestos.
- Servicios contratados.
- Documentos y líneas facturadas.
- Estado de pagos disponible mediante el ERP.
- Sesiones reservadas.
- Sesiones realizadas.
- Derechos restantes.

Las consultas deben reutilizar contratos, permisos y navegación oficiales.

No duplicar pantallas completas de Ventas o CxC.

No inferir un porcentaje de facturación del plan cuando falten importes comparables o asignaciones verificables.

La interfaz detallada será diseñada posteriormente con los componentes oficiales de ERP ZH.

### 3. Preservar las fronteras de responsabilidad

**SPA:** planificación clínica, agenda, atención, contratos de paquetes y derechos de sesiones.

**Items:** productos y servicios comerciales oficiales.

**Ventas:** facturas, líneas, impuestos, autorizaciones y notas de crédito.

**Caja/CxC:** cobros, cuotas, pagos y saldos.

**Contabilidad:** asientos y registros contables.

**Inventario:** movimientos oficiales de existencias.

**Anticipos de Clientes:** proceso transversal independiente conforme a DIR-SSOT-01.

SPA no crea otro sistema financiero.

**Delimitación:** decisión funcional/dirección técnica aprobadas solo en su alcance; mecanismo detallado pendiente, autorización especial pendiente, validación jurídica/fiscal pendiente y dependencia no implementada se distinguen según cada regla. No cierre integral de C-01/C-02/O-13, no cierre O-14, no modificación LOCKED/FROZEN/CLOSED. ADR-051 PROPOSED / NO APROBADO; F0–F8 PENDING / NO AUTORIZADA. Ninguna implementación ni operación Git de escritura autorizada por esta entrada.

Referencia normativa: contrato §4.3/7.3/8.7/10.2/11.5/11.6/15.2/18.1/21.5/22.4/27; ADR explica direcciones y mecanismos pendientes; plan fases/dependencias. Las menciones previas de cuestiones pendientes conservan el alcance de la decisión original; SPA-TEC-01/02/03 aportan ahora resolución expresa parcial, sin alterar los enunciados SPA-ARQ ni O-15/O-25.

## 23. Revisión técnica concentrada F0 — registro de recomendación, sin nueva aprobación

**2026-11-19 — AUTORIZACIÓN DE REVISIÓN Y DOCUMENTACIÓN EXCLUSIVAMENTE.** Encargo «Cierre técnico concentrado de ADR-051 — F0», base publicada eaba7dfd79a3912b56abd69f07878010ff18578a. El propietario permite seleccionar soluciones preferentes compatibles con patrones reales; exige: «No registrar como aprobada por el propietario una elección nueva que solo haya sido recomendada por el agente».

**Dirección aprobada:** SPA-ARQ-01..08 y SPA-TEC-01..03 permanecen íntegras en §§12–22, junto a SPA-DEC-115/116, O-15/O-25 y DIR-SSOT-01. No se crea SPA-TEC-04 ni se atribuye aprobación de nuevos mecanismos al propietario.

**Selección técnica recomendada y contrastada estáticamente:** [ADR-051 §5.10](ADR-051-spa-v1.1-architecture.md#510-selección-técnica-concentrada-f0--recomendación-verificada-no-aprobación-del-propietario) contiene matriz AT-01..12; §5.11 modelo/integridad/agenda/libro, §5.12 contratos mínimos, §5.13 propuesta protegida C-01, §5.14 auditoría/archivos y §5.15 precisión/esquema/gates. No es evidencia de implementación ni pruebas PostgreSQL/operativas. Contrato conserva comportamiento normativo; ADR arquitectura elegida como recomendación; plan dependencias y gates.

**Autorización especial pendiente:** clasificación clínica sin bypass/wildcard, concesiones y protección frente a autoescalamiento afectan Kernel/Access/sesión/menú/UI compartida. Impacto concreto y archivos en ADR §5.13; ni revisión especial del contrato LOCKED ni modificación protegida autorizadas por este encargo. C-01 no CLOSED. Lectores propietarios nuevos de Ventas y Finance solo firmas candidatas; implementar/extender módulos FROZEN exige su autorización propia.

**Validación externa pendiente:** C-02/conservación/supresión, firma y menores, fotografías/finalidades, fiscal/SRI/crédito/NC, operación de claves/TLS/cifrado/recuperación/backups. numeric(10,2) permanece propuesta preferente y exige gate formal del estándar: no se selecciona escala definitiva por inferencia.

**Dependencia no implementada:** Empleados/IEmployeeDirectory EMP-E6b bajo ADR-050/plan propios y dos lectores comerciales candidatos estrictamente necesarios donde las lecturas existentes no cubren el contexto autorizado. No Employee ni identidad/finanzas paralelas.

**Estado:** ADR-051 PROPOSED / NO APROBADO. F0–F8 PENDING / NO AUTORIZADA. Selección concentrada preparada para revisión conjunta con reservas explícitas, sin aceptación integral, código, migraciones, staging, commit ni push. STATUS/WORK_QUEUE no se modifican en este encargo.

## 24. Planificación completa de SPA V1.1 — selecciones SPA-SEL (recomendación, sin aprobación del propietario)

**2026-11-19 — ENCARGO DE CIERRE DOCUMENTAL COMPLETO, SOLO DOCUMENTACIÓN.** El propietario ordenó completar la planificación de SPA V1.1 en los cuatro documentos oficiales, seleccionar soluciones concretas cuando los estándares y decisiones existentes lo permitan y registrarlas como selección técnica recomendada, **sin atribuirles aprobación inexistente**. Este apartado no registra ninguna decisión nueva del propietario: SPA-DEC-115/116, SPA-ARQ-01..08, SPA-TEC-01..03, O-15, O-25, DIR-SSOT-01, las 19 fichas y las diez exclusiones permanecen íntegras.

| ID | Selección recomendada | Dónde está | Estado |
|---|---|---|---|
| SPA-SEL-01 | Modelo de datos físico candidato (tablas `spa_*`, claves, unicidades, FK compuestas, versionado) | ADR §5.16 | RECOMENDADA — pendiente de decisión del propietario |
| SPA-SEL-02 | Casos de uso backend, resultados y errores; sucursales vacías = sin restricción; preparación NULL = sin preparación | ADR §5.17; contrato §31.4 | RECOMENDADA |
| SPA-SEL-03 | API candidata `api/v1/spa/...` con las convenciones reales (envelope, permisos, idempotencia) | ADR §5.18 | RECOMENDADA |
| SPA-SEL-04 | Frontend: módulo `modules/spa`, rutas, componentes reutilizados y nuevos justificados; especificación operativa de las 19 pantallas | ADR §5.19; contrato §23.4 | RECOMENDADA |
| SPA-SEL-05 | Algoritmo de disponibilidad, traslados (par no configurado ⇒ rechazo), reserva atómica, locks ordenados, exclusión GiST, reprogramación y cierre administrativo | ADR §5.20 | RECOMENDADA |
| SPA-SEL-06 | Catálogo de permisos `spa.*` sin duplicados (navegación / acciones / recursos sensibles), clasificación clínica y guard de recurso (la ejecución depende de COND-01) | ADR §5.21; contrato §31.1 | RECOMENDADA; ejecución BLOQUEADA hasta autorización especial |
| SPA-SEL-07 | Verificación de contratos de integración reales y brechas (`ISalesEntitlementReader`, `IReceivableCollectionReader`, `IEmployeeDirectory`) | ADR §5.22 | RECOMENDADA; dependencias no implementadas |
| SPA-SEL-08 | Matrices de transición de cita, sesión, plan (4 estados con historial de eventos) y contrato; pendientes con recomendación: DP-01 reanudar plan, DP-02 no reabrir planes finales, DP-03 citas y cupos asociados sin cambio automático | ADR §5.23; contrato §31 | RECOMENDADA; SPA-ARQ-02 no aprobó transiciones adicionales |
| SPA-SEL-09 | Registro de errores `SPA_*` y traducción de concurrencia | ADR §5.24; contrato §31.16 | RECOMENDADA |
| SPA-SEL-10 | Estrategia de pruebas técnicas por capa | ADR §5.25 | RECOMENDADA |
| SPA-SEL-11 | Condiciones externas y protegidas COND-01..13 con verificación, responsable, fase y prohibiciones | ADR §5.26; contrato §31.15 | VIGENTES — ninguna cumplida |
| SPA-SEL-12 | Entregas internas y orden de construcción que resuelven los ciclos F3↔F4 y F5↔F6 sin simular capacidades: la reserva vinculada a paquete responde `SPA_PACKAGE_CAPABILITY_UNAVAILABLE` hasta F6.c y la atención no inicia sin cita hasta F5.c | Plan §7.1 | RECOMENDADA; fases siguen PENDING / NO AUTORIZADA |

**Límites.** No se aprueba la modificación de Identity/Access/Kernel (C-01), no se implementa Empleados, no se declaran válidas la firma ni la atención a menores, no se fija política de conservación (C-02), no se infiere regla fiscal/SRI, no se afirma custodia operativa de claves, no se cambian módulos FROZEN/CLOSED y no se adopta `numeric(10,2)` como precisión definitiva. **ADR-051 permanece PROPOSED / NO APROBADO y SPA-F0..F8 PENDING / NO AUTORIZADA.** Ninguna operación Git de escritura queda autorizada por esta entrada.

# SPA V1.1 — Registro de decisiones funcionales aprobadas (SPA-DEC-115, SPA-DEC-116, O-25, O-15 y directriz transversal)

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
| SPA-DEC-116 (visitas) y O-25 | Varios servicios/profesionales por visita es capacidad aprobada. O-25 no aprobó esa cardinalidad en su propio alcance, pero tampoco la excluyó. Representación visita–cita–servicios–profesionales queda pendiente; no imponer una cita de un único servicio como modelo definitivo |
| SPA-DEC-116 (sesiones) y O-25 | Borradores, finalización, interrupciones y correcciones aprobados como capacidades. Un borrador técnico no certifica una atención realizada. Efectos de sesión parcial/interrumpida sobre cita, cupos y consumo siguen sin definir; ausencia/cancelación no crea sesión clínica |
| SPA-DEC-116 (paquetes) y O-15/O-25 | Individual/combinado, vigencia, cupos, ampliación/sustitución aprobados. Contrato explícito desde factura/línea, transacción independiente, sin duplicados; movimiento comercial separado de sesión. No se aprueba creación automática ni se fija política de consumo/NC/crédito por inclusión general |
| SPA-DEC-116 (comercial) y DIR-SSOT-01 | Integrar servicios, productos, facturas, CxC/cobros oficiales no crea motores financieros Spa; anticipos siguen transversales, O-14 abierta |
| SPA-DEC-116 y ADR-050 | Employee general reutilizado con usuario/sucursales; sin segundo registro. Responsable y registrador separados. Directorio real pendiente |
| Firma/privacidad histórica y seguridad actual | Firma en pantalla, versiones, permisos oficiales, auditoría y archivos privados aprobados como capacidad; autenticidad, conservación legal, Admin, cifrado, concurrencia y claves concretas pendientes |
| Restricciones y exclusiones históricas | Descansos/bloqueos/preparación/traslados aprobados, sin valores inventados. No control de disponibilidad de cabinas/equipos. Panel/reportes operativos incluidos; dashboard analítico adicional excluido. Usar respaldos existentes no permite desarrollar un sistema nuevo |

**No se detecta revocación expresa entre las fuentes aportadas.** Las cuestiones concretas que necesitan definición son representación de visita/cita múltiple y borrador/sesión parcial, cálculo de restricciones, consumo/ajustes comerciales y protección legal/técnica. Se conservan ambas evidencias y no se resuelven esas cuestiones en silencio. O-23 está cerrada solo para inclusión funcional; O-14/O-24 abiertas.

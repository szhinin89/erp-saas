# Gali Spa V1.1 — Registro de decisiones funcionales aprobadas (O-25 y O-15)

**REGISTRO DE DECISIONES APROBADAS POR EL PROPIETARIO — ALCANCE LIMITADO — NO ADR — NO CONTRATO IMPLEMENTABLE — NO IMPLEMENTACIÓN AUTORIZADA.**

Registro canónico de SPA-V1.1-DECISION-CLOSURE-01. Documenta las **dos primeras decisiones funcionales de Gali Spa V1.1 aprobadas expresamente por el propietario**, ambas el **2026-11-18**: `O-25` y `O-15`.
Estado de entrega: [STATUS](../../STATUS.md); cola: [WORK_QUEUE](../agent/WORK_QUEUE.md).

## 0. Naturaleza y límites

1. **La aprobación corresponde únicamente a las alternativas indicadas y a sus límites**, no a todas las propuestas técnicas incluidas en los borradores de Spa.
2. **Estas aprobaciones no son decisiones históricas.** No se les asigna ningún identificador `SPA-DEC` nuevo, y no deben confundirse con el historial perdido `SPA-DEC-001` a `SPA-DEC-115` ni con
   `SPA-DEC-116` (de la cual solo está confirmado el título).
3. **Evidencia de la aprobación:** confirmación expresa del propietario en la conversación del proyecto (SPA-V1.1-DECISION-CLOSURE-01), sobre la base del análisis SPA-V1.1-DECISION-REVIEW-01 (informe de sesión, no versionado en el repositorio).
4. No se cierra ninguna otra cuestión `O-xx` por inferencia (§5). No se crea un ADR implementable y no se autoriza implementación de Spa ni de Empleados.
5. No se modifica Ventas ni ningún módulo FROZEN.

Documentos relacionados: [RECOVERY-01](GALI-SPA-V1.1-RECOVERY-01.md), [OPERATIONS-SPEC](GALI-SPA-V1.1-OPERATIONS-SPEC-DRAFT-01.md), [CARE-PRIVACY](GALI-SPA-V1.1-CARE-PRIVACY-SPEC-DRAFT-01.md),
[SECURITY-COMPLIANCE](GALI-SPA-V1.1-SECURITY-COMPLIANCE-DRAFT-01.md), [matriz de consolidación](GALI-SPA-V1.1-CONTRACT-CONSOLIDATION-MATRIX-01.md) y
[normalización](GALI-SPA-V1.1-CONTRACT-NORMALIZATION-01.md). Los cuatro primeros **no se modifican retrospectivamente** ni se les atribuyen aprobaciones que no tenían cuando se redactaron.

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

- **C-06 (terminología de citas y sesiones):** el **conflicto terminológico queda resuelto** por `O-25` (alternativa A). Las **políticas comerciales** relacionadas (ausencias que consumen saldo, efecto de cancelar, atención sin cita, varios servicios por cita) **siguen abiertas**.
- **C-01 y C-02** no se modifican y siguen abiertos.
- **C-08** (anticipos frente a la capacidad real de Ventas y CxC) no se modifica: depende de `O-14`.

## 4. Lo que estas decisiones no cambian

- No aprueban políticas de saldo, de ausencias que consumen, de cancelación ni de reprogramación con efecto comercial.
- No aprueban anticipos, prepagos, facturación por sesión ni devengo.
- No aprueban el catálogo de permisos, los estados ni las entidades propuestos en los borradores.
- No aprueban la creación automática de contratos.
- No modifican ADR-050 ni la especificación de Empleados.

## 5. Decisiones que permanecen abiertas

Las **23 cuestiones restantes** siguen pendientes: `O-01` a `O-14` y `O-16` a `O-24`. En particular siguen abiertas `O-01` (administrador y datos clínicos), `O-05` (eliminación frente a la regla de no borrado), `O-14` (anticipos),
`O-13` (paquetes), `O-10` (atención sin cita) y `O-11` (servicio y procedimiento). Tampoco se resuelven por inferencia los pendientes de evidencia externa (fichas originales, texto de `SPA-DEC-116`, validación jurídica, Empleados implementado).

## 6. Límites

Este documento solo registra dos aprobaciones funcionales. No es un ADR, no es una especificación contractual, no autoriza implementación de Spa ni de Empleados, migraciones ni cambios en módulos FROZEN, y no aprueba ninguna otra decisión.

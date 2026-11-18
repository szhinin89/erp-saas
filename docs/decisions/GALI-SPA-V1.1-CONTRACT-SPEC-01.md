# Gali Spa V1.1 — Contrato funcional consolidado (SPA-V1.1-CONTRACT-SPEC-01)

**CONTRATO CONSOLIDADO EN REVISIÓN — NO APROBADO PARA IMPLEMENTACIÓN.**

Candidato a fuente única de verdad **funcional** de Gali Spa V1.1 (2026-11-18, revisión 1). Responde a cuatro preguntas por funcionalidad: **qué se desarrollará, cómo debe funcionar, qué está
aprobado y qué no lo está, y qué queda excluido.** Consolida, sin añadir decisiones de negocio, los siete documentos previos de Spa (§0.7). **No es un ADR, no es una SSOT vigente** (la SSOT de un
módulo nace cuando existen código y pruebas: [enforcement](../architecture/enforcement.md#ssot-por-módulo)) **y no autoriza implementar Gali Spa ni Empleados**, migraciones, endpoints, pantallas,
permisos ni cambios en módulos FROZEN o CLOSED. Estado de entrega: [STATUS](../../STATUS.md); cola: [WORK_QUEUE](../agent/WORK_QUEUE.md) (a la fecha, ninguno de los dos registra este contrato; el índice
[docs/decisions/README.md](README.md) tampoco lista los documentos de Spa).

Patrón documental: el mismo de Orden de Pedido de Venta V1 ([ADR-049](ADR-049-sales-order-v1-architecture.md), [especificación funcional](PEDIDOS-VENTA-V1-FUNCTIONAL-SPEC-01.md)) y de Empleados V1
([ADR-050](ADR-050-employees-v1-architecture.md), [especificación contractual](EMPLOYEES-V1-CONTRACT-SPEC-01.md)): una especificación central con etiquetas de estado, jerarquía de autoridad
explícita, decisiones del propietario separadas de las propuestas técnicas y un ADR y un plan que se redactan **después** de resolver las decisiones bloqueantes.

## 0. Cómo leer este contrato

### 0.1 Precedencia normativa

Ante una diferencia entre documentos se aplica este orden. Una diferencia puramente documental se corrige aquí; **dos decisiones aprobadas que realmente se contradigan no se resuelven en silencio:
se registran como bloqueo (§29).**

| Nivel | Fuente | Alcance |
|---|---|---|
| 1 | Decisiones expresamente aprobadas por el propietario | Solo dentro de su alcance literal: `O-25`, `O-15` ([registro](GALI-SPA-V1.1-APPROVED-DECISIONS-01.md)), las decisiones de Empleados de [ADR-050](ADR-050-employees-v1-architecture.md) que atañen a Spa y la **instrucción** del propietario `DIR-SSOT-01` (§20.4), que fija una frontera y no aprueba un diseño |
| 2 | ADR y contratos vigentes del ERP | Respetando sus límites: [ADR-017](ADR-017-business-partner-scope.md) (BusinessPartner, FROZEN), [ADR-034](ADR-034-temporal-contract-single-source.md), [ADR-035](ADR-035-supplier-payment-unapplied-advance.md), [ADR-041](ADR-041-negative-sale-inventory-cost-obligations.md), «Security & Access Contract V1 — LOCKED» ([security](../architecture/security.md)), [auditoría](../architecture/audit-infrastructure.md), [datos](../architecture/data-standards.md), Ventas FROZEN |
| 3 | Evidencia técnica verificada del repositorio | Las verificaciones `V-xx` cerradas ([normalización §5](GALI-SPA-V1.1-CONTRACT-NORMALIZATION-01.md)) y las capacidades [E] citadas en cada sección |
| 4 | Propuestas pendientes | **Sin carácter normativo**: formularios sin contraste, reconstrucciones y propuestas técnicas |

### 0.2 Estados de aprobación (columna «Estado»)

| Estado | Significado |
|---|---|
| **APROBADO** | Decisión expresa del propietario, dentro del alcance indicado en su registro. Incluye lo aprobado literalmente en ADR-050 para Spa |
| **APROBADO-DERIVADO** | Consecuencia razonable de una decisión aprobada que el texto literal no formula para Spa; cita la sección de origen. No es una aprobación nueva |
| **INSTRUCCIÓN DEL PROPIETARIO** | Directriz expresa del propietario con alcance limitado (`DIR-SSOT-01`). Fija una frontera; **no es una aprobación de diseño** ni cierra ninguna cuestión `O-xx` |
| **VIGENTE-ERP** | Capacidad o restricción ya vigente y verificada del ERP que Spa debe respetar. No es decisión de Spa |
| **PROPUESTO** | Propuesta técnica o requisito de formulario [F] sin contraste. **No normativo** |
| **PENDIENTE** | Requiere una decisión del propietario (`O-xx`), una verificación (`V-xx`) o recuperar evidencia. No se elige ninguna alternativa |
| **BLOQUEADO** | Conflicto real entre decisiones o dependencia externa que impide cerrar la regla (§29) |
| **EXCLUIDO** / **NO AUTORIZADO** | §3 |

Etiquetas de evidencia de las fuentes (columna «Evid.»): **A** aprobada literal, **A-der** derivada, **E** existente en el ERP, **F** formularios originales (Claude no los ha inspeccionado), **R**
reconstruida, **T** propuesta técnica, **P** pendiente, **NV** no verificado; **L-EXPRESO**, **L-INTERPRETACIÓN**, **L-RECOMENDACIÓN**, **L-NV** para el contenido legal.

### 0.3 Reglas de lectura

1. **Las reglas conservan su identificador histórico** (`SPA-AR-*`, `SPA-OPS-*`, `SPA-CARE-*`, `SPA-SEC-*`, etc.); no se renumera ni se reutiliza ninguno. Este contrato los reagrupa; el texto aquí prevalece sobre la fuente (§0.1).
2. Una alternativa no es una decisión. Donde hay una laguna se listan las alternativas y **no se elige ninguna**.
3. Un campo de un formulario físico **no es obligatorio en el software por sí mismo**; la obligatoriedad real es una decisión pendiente (`O-21`). Los únicos mínimos de integridad proponibles son cliente, fecha,
   profesional responsable, usuario registrador y sucursal [T].
4. Una ausencia de respuesta no equivale a una respuesta negativa en ningún antecedente [T].
5. Un nombre de entidad, permiso, error o endpoint de este contrato es **candidato** salvo indicación expresa; ninguno existe en el código.
6. De `SPA-DEC-116` solo está confirmado el título; `SPA-DEC-001` a `SPA-DEC-115` no se declaran existentes ni recuperadas. Las cuestiones `O-xx` **no son** decisiones históricas `SPA-DEC`.
7. «Sesión» significa en todo el contrato **sesión realizada** (§12). La programación es una **cita** (§8).
8. Términos que no se mezclan: «**contrato de paquete**» (`PackageContract`, registro de Spa) frente a «contrato» como especificación o interfaz (este documento, `IEmployeeDirectory`, el contrato LOCKED);
   «**sesión de caja**» (`CashSession`, concepto de Caja) frente a la sesión realizada de Spa; «**reserva**» es la operación que crea una cita y no una reserva de stock de Inventario.
9. Los párrafos descriptivos bajo las tablas (modelos, flujos, estados, alternativas, recomendaciones) son PROPUESTOS o PENDIENTES salvo que una frase diga expresamente otra cosa. **Lo único aprobado con
   redacción exacta son los enunciados literales de §8.1 (`O-25`) y §11.1 (`O-15`)** y lo marcado APROBADO en la columna «Estado».
10. Una propuesta nunca es una obligación implementable: el verbo «debe» describe el comportamiento que tendría la regla si se aprueba, salvo en los enunciados APROBADOS.

### 0.4 Estado global de implementabilidad

**Ninguna funcionalidad es implementable hoy.** Causas comunes: (1) este contrato no está aprobado y no existe ADR ni plan de fases de Spa; (2) Empleados no está implementado (`IEmployeeDirectory` es propuesta;
EMP-E1 PENDING / NO AUTORIZADA); (3) decisiones bloqueantes abiertas (§29); (4) fichas originales sin contraste (`V-01`); (5) validación jurídica pendiente (`V-06`).

### 0.5 Mapa de estado por área

| Área | Qué está aprobado | Qué sigue abierto | Implementable hoy |
|---|---|---|---|
| Clientes (§5) | Nada específico de Spa; identidad de cliente = BusinessPartner (VIGENTE-ERP) | Flujo de cliente inexistente; fecha de nacimiento y contacto de emergencia (`O-06`) | No |
| Profesionales (§6) | `Employee` reutilizado; necesidad de `IEmployeeDirectory`; profesional ≠ usuario registrador (ADR-050) | Firmas del contrato; política de reasignación | No (Empleados no existe) |
| Disponibilidad y agenda (§7, §8) | Cita separada de la sesión realizada y sus 8 enunciados (`O-25`) | Recursos, tolerancias, anticipación, ausencias, atención sin cita (`O-08` a `O-10`) | No |
| Servicios y planes (§9, §10) | Servicio = Item `Service` (VIGENTE-ERP) | Servicio ↔ procedimiento (`O-11`); relación plan–paquete | No |
| Paquetes y sesiones (§11, §12) | Creación explícita del contrato tras validar una factura autorizada (`O-15`); reservar y reprogramar no consumen saldo (`O-25`) | Las demás preguntas de paquetes (`O-13`) | No |
| Expediente y valoraciones (§13 a §16) | Nada | Todo el contenido [F] sin contraste; obligatoriedad, cardinalidad, escalas (`O-19` a `O-22`) | No |
| Consentimientos y fotografías (§17, §18) | Nada | Firma, versionado, menores, uso académico, retiro (`O-16` a `O-18`) | No |
| Ventas, Caja y CxC (§20) | Spa no crea facturas, CxC, cobros ni asientos (`O-15`); anticipos no son de Spa (`DIR-SSOT-01`) | Anticipos y prepagos (`O-14`) | No |
| Seguridad, auditoría, conservación (§21, §22) | Los permisos de Empleados y las asignaciones no dan acceso clínico (ADR-050 §6, §13) | Acceso del administrador (`O-01`), eliminación (`O-05`), cifrado (`O-04`), lecturas (`O-03`) | No |

### 0.6 Qué no hace este documento

No crea una matriz ni un documento de recuperación nuevos, no modifica los documentos fuente, índices, `STATUS.md` ni `WORK_QUEUE.md`, no crea ADR ni plan, y no toca código, bases de datos ni migraciones.

### 0.7 Fuentes consolidadas

| Código | Documento | Contenido aportado |
|---|---|---|
| R1 | [GALI-SPA-V1.1-RECOVERY-01](GALI-SPA-V1.1-RECOVERY-01.md) | Reconstrucción, requisitos [F], invariantes `SPA-INV-*`, pruebas `SPA-T-*` |
| R2 | [GALI-SPA-V1.1-OPERATIONS-SPEC-DRAFT-01](GALI-SPA-V1.1-OPERATIONS-SPEC-DRAFT-01.md) | Agenda, servicios, paquetes, ventas (`SPA-OPS-*`) |
| R3 | [GALI-SPA-V1.1-CARE-PRIVACY-SPEC-DRAFT-01](GALI-SPA-V1.1-CARE-PRIVACY-SPEC-DRAFT-01.md) | Expediente, valoraciones, consentimientos, fotografías (`SPA-CARE-*`) |
| R4 | [GALI-SPA-V1.1-SECURITY-COMPLIANCE-DRAFT-01](GALI-SPA-V1.1-SECURITY-COMPLIANCE-DRAFT-01.md) | Seguridad, auditoría, retención, legal (`SPA-SEC-*`) |
| MATRIZ | [GALI-SPA-V1.1-CONTRACT-CONSOLIDATION-MATRIX-01](GALI-SPA-V1.1-CONTRACT-CONSOLIDATION-MATRIX-01.md) | 295 registros, conflictos `C-xx`, cuestiones `O-xx`, verificaciones `V-xx` |
| NORM | [GALI-SPA-V1.1-CONTRACT-NORMALIZATION-01](GALI-SPA-V1.1-CONTRACT-NORMALIZATION-01.md) | Redacciones canónicas, glosario, catálogo de permisos, grupos de pruebas `PT-xx` |
| REG | [GALI-SPA-V1.1-APPROVED-DECISIONS-01](GALI-SPA-V1.1-APPROVED-DECISIONS-01.md) | Evidencia de `O-25` y `O-15` |

No existen otros documentos de Spa en el repositorio (comprobado con `git ls-files`; solo hay una mención incidental en `docs/archive/BUSINESSPARTNER-READINESS.md`).

### 0.8 Cobertura por sección

Auditoría de las secciones funcionales (§5 a §22) contra los diez elementos exigidos. **D** = definido en el contrato (como propuesta o aprobado, según su estado); **P** = parcial o incompleto;
**—** = no recuperado o no definido; **n/a** = no aplica. «Definido» **no** significa aprobado: la última columna lo indica. Las secciones §1 a §4 y §23 a §30 son transversales.

| § | Flujos (usuario y sistema) | Reglas y validaciones | Entidades e integraciones | Pantallas y API | Permisos | Estados | Errores | Pruebas | Aprobación |
|---|---|---|---|---|---|---|---|---|---|
| 5 | P | D | D | P | P | n/a | — | D | Sin regla específica aprobada |
| 6 | n/a | D | P | — | P | n/a | — | D | Núcleo APROBADO (ADR-050) |
| 7 | P | D | P | P | D | n/a | P | D | Pertenencia APROBADA; reglas propuestas |
| 8 | D | D | D | D | D | D | D | D | Modelo cita/sesión APROBADO (`O-25`) |
| 9 | D | D | D | D | D | D | D | D | Servicio = Item (VIGENTE-ERP); resto propuesto |
| 10 | — | P | P | — | — | — | — | — | Propuesto; **incompleta** |
| 11 | P | D | D | D | D | D | D | D | Origen del contrato APROBADO (`O-15`); resto pendiente |
| 12 | P | D | D | D | D | P | P | D | Modelo APROBADO (`O-25`); consumo pendiente |
| 13 | D | D | D | D | D | D | P | D | Propuesto [F] |
| 14 | D | D | P | D | D | D | — | D | Propuesto [F] |
| 15 | D | D | P | D | D | D | — | D | Propuesto [F]; escalas pendientes |
| 16 | P | P | — | P | P | — | — | P | Propuesto [F]; **incompleta** |
| 17 | D | D | D | D | D | D | — | D | Propuesto [F]; firma pendiente |
| 18 | D | D | D | D | D | D | — | D | Propuesto |
| 19 | — | P | — | — | — | — | — | P | Pendiente (`O-12`); **incompleta** |
| 20 | P | D | D | P | P | n/a | D | D | Frontera APROBADA (`O-15`); anticipos pendientes |
| 21 | n/a | D | P | — | D | n/a | — | D | Núcleo APROBADO; acceso del Admin bloqueado |
| 22 | P | D | D | P | D | n/a | — | D | Propuesto; eliminación bloqueada |

**Especificaciones incompletas** (qué falta y por qué; no se completa inventando):

| § | Falta | Motivo |
|---|---|---|
| 5 | Flujo del cliente inexistente; permiso de lectura de BusinessPartner aplicable; errores | No recuperado (`SPA-AR-01`); `O-06` |
| 6 | Firmas definitivas de `IEmployeeDirectory`; errores | Empleados no implementado (BL-01) |
| 7 | Entidades de horarios, errores y pruebas específicas de disponibilidad por horario | `V-05a`: sin precedente; `O-08`, `O-09` |
| 10 | Flujos, estados, permisos, errores y pruebas del plan de tratamiento; nombre de entidad | Las fuentes solo lo describen como registro [F] |
| 11, 12 | Flujos de consumo y restitución, estados definitivos del contrato y de la sesión, errores del consumo | `O-13` |
| 13 a 18 | Catálogo de errores clínicos; obligatoriedad de campos; mínimos de borrador | No recuperado; `O-21`, `V-01` |
| 16 | Entidad, permisos, estados y errores propios del seguimiento | Se apoya en sesión, valoración y fotografía |
| 19 | Entidad, permisos, estados, errores y flujo de insumos | `O-12` |
| 20 | Pantalla de creación del contrato, permiso de lectura de Ventas, definición de «factura autorizada», estado SRI | `O-15` §2.5 del registro |
| 21, 22 | Catálogo de errores de seguridad y auditoría; permisos para consultar sesiones, valoraciones y consentimientos | `O-01`, `O-03`, `O-05` |

## 1. Objetivo del módulo

**Qué se desarrollará (candidato, `SPA-AL-01` [R]):** el ámbito funcional del ERP destinado a administrar un establecimiento de servicios estéticos: clientes y expedientes de atención, profesionales
responsables, valoraciones faciales y corporales, consentimientos informados, fotografías y seguimiento, servicios y tratamientos, planes de tratamiento, agenda y reservas, sesiones realizadas, paquetes
comerciales, integración con ventas y cobros, y control de acceso y trazabilidad.

**Principio de reutilización (`SPA-AL-02`):** Spa no crea identidades laborales ni de cliente paralelas, ni registros comerciales o contables paralelos; reutiliza Empleados, BusinessPartner, Items, Ventas,
Cuentas por Cobrar y los mecanismos oficiales de seguridad y auditoría. Parte Empleados: APROBADO-DERIVADO (ADR-050 §3 DEC-E1, §13); parte resto: [R].

**Estado:** objetivo [R]; la lista de áreas **identifica** capacidades y no declara su inclusión definitiva (§2).

## 2. Alcance exacto de V1.1

**El alcance de V1.1 no está aprobado como lista cerrada.** El texto íntegro de `SPA-DEC-116 — Revisión y consolidación del alcance de Gali Spa V1.1` (aprobada; solo el título confirmado) no está recuperado, y
las exclusiones originales de la versión tampoco (`O-23`). Por ello este contrato distingue:

| Capacidad candidata | Sección | Respaldo | Inclusión en V1.1 |
|---|---|---|---|
| Expediente del cliente | §13 | F, R | **PENDIENTE** (`O-23`) |
| Valoración facial y corporal | §14, §15 | F | **PENDIENTE** |
| Seguimiento, evolución y comparación fotográfica | §16, §18 | F | **PENDIENTE** |
| Consentimientos y firmas | §17 | F | **PENDIENTE** |
| Plan de tratamiento | §10 | F | **PENDIENTE** |
| Agenda, citas y reservas | §7, §8 | ADR-050 §13 (pertenece a Spa), R; modelo cita/sesión APROBADO (`O-25`) | **PENDIENTE** en su inclusión; su **diseño** está condicionado por `O-25` |
| Servicios y tratamientos | §9 | E, R | **PENDIENTE** |
| Paquetes comerciales y sesiones | §11, §12 | R; creación del contrato APROBADA (`O-15`) | **PENDIENTE** en su inclusión; su **diseño** está condicionado por `O-15` y `O-25` |
| Integración con Ventas, Caja y CxC | §20 | E, R | Solo como **referencia** a documentos de Ventas y CxC |
| Equipos e insumos (registro documental) | §19 | F, T | **PENDIENTE** |

Las decisiones `O-25` y `O-15` establecen **cómo** funcionarían agenda y paquetes; no declaran por sí solas su inclusión en V1.1. **Condición para declarar el alcance definitivo:** que el propietario confirme
la columna «Inclusión» (y, si lo recupera, el texto de `SPA-DEC-116`); hasta entonces ninguna capacidad es «parte aprobada de V1.1».

### 2.1 Clasificación del alcance (A a E)

| Clase | Contenido | Dónde |
|---|---|---|
| **A. Aprobadas para V1.1** | **Ninguna capacidad está aprobada como parte de V1.1**: el propietario no ha aprobado una lista de alcance. Lo aprobado no declara inclusión: `O-25` y `O-15` (cómo funcionan agenda y paquetes), las decisiones de ADR-050 que atañen a Spa y la instrucción `DIR-SSOT-01` | §8.1, §11.1, §6, §20.4 |
| **B. Propuestas para V1.1, pendientes de aprobación** | Las diez capacidades candidatas de la tabla anterior | §2 |
| **C. Dependencias externas necesarias** | Empleados (`IEmployeeDirectory`), BusinessPartner, Items, Ventas y CxC (por referencia), Kernel/Access, Entity Audit, almacenamiento, asesoría legal, fichas originales | §4, §29 |
| **D. Expresamente excluidas** | Las de la clase «Excluido» | §3.1 |
| **E. No autorizadas** (sin afirmar que estén definitivamente excluidas) | Las de la clase «No autorizado» | §3.2 |

### 2.2 Qué se necesita para cerrar `O-23`

`O-23` **sigue abierta** y este contrato no la cierra. Para cerrarla, el propietario debería, de forma expresa: (1) aportar el texto íntegro de `SPA-DEC-116` o declarar que no se recuperará; (2) confirmar o corregir la
columna «Inclusión» de §2 capacidad por capacidad; (3) confirmar si las exclusiones de §3.1 son las únicas o aportar las originales; (4) indicar si existe un historial `SPA-DEC-*` que deba registrarse (sin inferirlo de la
numeración); (5) fijar la versión vigente de las fichas originales (`O-24`), de la que depende el contenido [F]. Hasta entonces la clase A permanece vacía.

## 3. Funcionalidades excluidas

Se separan tres conceptos que no deben confundirse: **excluido** (tiene respaldo), **no autorizado** (sin decisión ni capacidad; fuera hasta que se decida) y **pendiente** (está dentro del contrato y espera
una decisión, §30). **Pendiente no es excluido.** Las exclusiones originales de V1.1 no se recuperaron: lo que no figura en estas listas ni en §2 no está ni excluido ni incluido.

### 3.1 Excluido (con respaldo)

| ID | Exclusión | Estado | Respaldo |
|---|---|---|---|
| X-01 | Spa **no** crea facturas, CxC, cobros ni asientos contables | APROBADO | `O-15` #6; `SPA-OPS-VTA-01`, `SPA-INV-08`, `SPA-AR-05` |
| X-02, X-03 | Sin uso: reclasificadas como N-10 y N-11 (alternativas no aprobadas, no exclusiones expresas) | — | §3.2 |
| X-04 | Entidad `Employee`, catálogos laborales, altas, bajas, reincorporaciones y asignaciones de sucursal dentro de Spa | APROBADO-DERIVADO | ADR-050 §3 DEC-E1, §7, §8, §13 |
| X-05 | Derivar permisos de Spa de `employees.*`, `access.*` o de una asignación laboral | APROBADO | ADR-050 §6, §13 (`SPA-SG-01`, `SPA-INV-04`) |
| X-06 | Identidad de cliente paralela a BusinessPartner | VIGENTE-ERP | ADR-017; `SPA-INV-01` |
| X-07 | Anticipos y prepagos de clientes **como entidad financiera propia de Spa** | INSTRUCCIÓN DEL PROPIETARIO | `DIR-SSOT-01` (§20.4); `SPA-OPS-VTA-03`, `SPA-OPS-PKG-07` |
| X-08 | Contabilidad, Caja, cobros, sistema de auditoría o cola de eventos paralelos | VIGENTE-ERP | `SPA-INV-08`, `SPA-AU-01`; auditoría FROZEN |
| X-09 | Modificar módulos FROZEN o CLOSED, el contrato LOCKED de Identity/Access/Kernel, `IFileStorage` o scripts de operación sin autorización expresa y separada | VIGENTE-ERP | CLAUDE.md; [frozen-infrastructure](../architecture/frozen-infrastructure.md) |
| X-10 | Borrado físico arbitrario de registros de Spa; solo un procedimiento de eliminación legal aprobado podría hacerlo | BLOQUEADO en su excepción (`O-05`, `C-02`) | Regla permanente del proyecto de no borrado físico; `SPA-SEC-RET-01` |

### 3.2 No autorizado (sin decisión ni capacidad del ERP)

| ID | Funcionalidad | Motivo |
|---|---|---|
| N-01 | Facturación por sesión y devengo de ingresos por sesión | El ERP no tiene factura por sesión ni ingreso diferido [NV]; ninguna decisión lo autoriza |
| N-02 | Reserva con depósito, prepago sin factura, saldo a favor del cliente | Dependen de Anticipos de Clientes (`O-14`; §20.4); el ERP no tiene ese proceso |
| N-03 | Devolución de dinero a tarjeta o transferencia y crédito para compras futuras | Las devoluciones de Ventas reembolsan solo en efectivo o contra CxC; sin proceso transversal no hay otra vía (`O-14`) |
| N-04 | Consumo automático de insumos con movimiento de inventario | Exige un contrato de inventario no aprobado (`SPA-OPS-SRV-07` D no se propone; ADR-041) |
| N-05 | Módulo de activos, mantenimiento o reserva de equipos | `SPA-IN-03`: no aprobado |
| N-06 | Uso académico de datos y fotografías | `SPA-CARE-CNS-07`: no se implementa ni se asume hasta que el propietario confirme que pertenece al producto (`O-17`) |
| N-07 | Recordatorios y confirmaciones automáticas de citas | No recuperados (`SPA-OPS-AGE-02`) |
| N-08 | Vendedor y cobrador distintos del profesional en Ventas | El dominio de Ventas no tiene vendedor (`V-03c`); sería un contrato nuevo y Ventas es FROZEN |
| N-09 | Publicación o exportación automática de fotografías | `SPA-CARE-FOT-04`; solo acción explícita con permiso propio |
| N-10 | Creación automática del contrato de paquete mediante `SalesInvoiceAuthorizedEvent` | `O-15` #7: «no se aprueba»; alternativa no seleccionada |
| N-11 | Sesión única creada desde la reserva y reserva como entidad independiente | `O-25`: la alternativa no seleccionada «no se aprueba» |

Una funcionalidad no autorizada **no está definitivamente excluida**: puede incorporarse mediante una decisión posterior del propietario.

## 4. Dependencias y límites con otros módulos

Capacidades verificadas [E] que condicionan el contrato. Que existan no las convierte en decisiones de Spa.

| Módulo | Capacidad vigente | Límite o consecuencia para Spa | Estado |
|---|---|---|---|
| Empleados | **No implementado.** ADR-050 Proposed; EMP-E1 PENDING / NO AUTORIZADA; `IEmployeeDirectory` necesario en V1 (aprobado) con firmas propuestas (especificación §19) | Spa no puede consumirlo todavía; toda dependencia es contractual (`D-01`, `V-02`) | BLOQUEADO (§29) |
| BusinessPartner | Identidad con rol Customer; FROZEN (ADR-017); sin fecha de nacimiento ni contacto de emergencia | Spa lee por proyección autorizada y no copia; ampliar BP exige autorización aparte | VIGENTE-ERP |
| Items | Naturaleza `Service` sin bodega ni stock; alcance Tenant + Company; **sin duración ni `BranchId`**; precio `BaseSalePrice` ([ADR-021](ADR-021-pricing-engine-ssot.md)) | Duración, sucursales y condiciones del servicio son datos de Spa; precio e impuestos los resuelve el ERP | VIGENTE-ERP |
| Ventas | `SalesInvoice` Draft → Authorize → Cancel; FROZEN; línea `Service` sin bodega; `SalesInvoiceAuthorizedEvent` sin cliente ni líneas, atendido en la misma transacción; sin vendedor; devolución con Nota de Crédito; reversa por un solo camino | Spa referencia la factura y su línea; no consume el evento (`O-15` #7) | VIGENTE-ERP |
| Cuentas por cobrar | CxC solo desde factura a crédito (o saldo inicial); un cobro se aplica a una CxC existente | Sin cobro «a cuenta»; Spa no cobra | VIGENTE-ERP |
| Caja | Movimientos de apertura, ingreso por venta, ingreso o egreso manual, retiro, reembolso de devolución, pagos a proveedor y depósito; la factura de venta nace ligada a una sesión de caja; un ingreso manual no referencia a un cliente | Spa no registra movimientos de Caja ni cobra | VIGENTE-ERP |
| Inventario | Sin tipo «consumo de insumos»; ajustes con motivo; saldo negativo solo `SaleExit` (ADR-041) | Cualquier movimiento por insumos exige contrato nuevo | VIGENTE-ERP |
| Contabilidad | Sin devengo ni ingreso diferido [NV]; anticipo contabilizado solo de proveedor (ADR-035) | Spa no contabiliza | VIGENTE-ERP |
| Identity, Access, Kernel | Permisos `modulo.recurso.accion` solo en el Kernel; **el rol Admin pasa toda política `perm:*`** (`HasAdminBypass`); contrato LOCKED | Declarar claves nuevas está permitido; excepcionar al Admin exige revisión arquitectónica (`C-01`) | VIGENTE-ERP |
| Entity Audit | Append-only; FROZEN; sin registro de **lecturas** exitosas | Spa añade sus entidades de auditoría; las lecturas son una necesidad sin mecanismo | VIGENTE-ERP |
| Almacenamiento | `IFileStorage`/`LocalFileStorage`: sobrescribe, borra físicamente, no valida contención de ruta, no cifra; volumen único con keyring | Spa no lo modifica; usaría un adaptador propio (§18) | VIGENTE-ERP |
| Sucursales | `Branch` por empresa, sin zona horaria; acceso de usuario por `CompanyUserBranch`/`IBranchAccessGuard` | Acceso de usuario ≠ disponibilidad laboral | VIGENTE-ERP |
| Tiempo y números | Fecha de negocio `DateOnly`; instante UTC mostrado en `Company.Timezone`; precisión numérica con una sola SSOT | Spa no define escalas ni helpers propios | VIGENTE-ERP |
| Anticipos de Clientes | **No existe** | Proceso independiente fuera de Spa (§20.4) | PENDIENTE (`O-14`) |

## 5. BusinessPartner y clientes

**Qué hace:** identifica a la persona atendida sin duplicar su identidad. **Estado de la sección:** el principio es VIGENTE-ERP/[R]; el flujo de cliente inexistente y los datos sin campo en BusinessPartner
están PENDIENTES. **Implementable hoy:** no. **Bloqueos:** BL-09 (§29.1).

| ID | Comportamiento contractual | Estado | Evid. |
|---|---|---|---|
| SPA-AR-01 | El cliente es un BusinessPartner con rol Customer; Spa referencia su identificador y no crea otro registro de identidad personal | VIGENTE-ERP | R; ADR-017 |
| SPA-CARE-EXP-01 | Un expediente referencia un BP **activo** con rol Customer de la empresa; Spa lee nombre, identificación y contacto por proyección autorizada y no los copia | PROPUESTO | T |
| SPA-FC-07 | Spa guarda solo la información específica de atención; qué datos se conservan como referencia histórica es pendiente | PROPUESTO / PENDIENTE | T, P |
| SPA-CARE-EXP-09 | Si el BP se desactiva, el expediente se conserva y no se crean atenciones nuevas hasta reactivarlo | PROPUESTO | T |
| SPA-CL-01 | El historial del cliente permite consultar su evolución sin mezclar registros de clientes distintos | PROPUESTO | F, T |
| SPA-INV-01 | No se duplican identidades de clientes | VIGENTE-ERP | R |
| SPA-SEC-INV-11 | Los datos personales que BP no tiene viven en Spa y nunca duplican la identidad de BP | PROPUESTO | T |

**Pendientes:** flujo cuando el cliente aún no existe (alternativa A: exigir que el BP exista, creado por MasterData con sus permisos; B: flujo asistido que remita a MasterData; en ambas Spa no crea BP con
permisos propios; el flujo exacto no está recuperado); **fecha de nacimiento, edad y contacto de emergencia** (`O-06`): BP no los tiene. Alternativas sin elegir: A campos propios de Spa opcionales y de acceso
restringido (`CareRecordPersonalData`); B ampliar BP (módulo FROZEN, requiere ADR nuevo); C contacto de emergencia como contacto de BP con rol `Other`; D guardar solo la **categoría de edad** (menor de 15,
15 a 17, adulto) en la fecha del consentimiento; E edad declarada como dato histórico. El contacto de emergencia es dato de un tercero (tema legal pendiente, `L-NV`). `DUP-16`: ambos datos no existen en BP; su
almacenamiento se decide sin copiar la identidad de BP.

## 6. Profesionales y Empleados

**Qué hace:** identifica quién presta la atención y quién la registra, reutilizando Empleados. **Estado de la sección:** el núcleo está APROBADO (ADR-050); el contrato `IEmployeeDirectory` no existe.
**Implementable hoy:** no (Empleados no está implementado). **Bloqueos:** BL-01, BL-09 (§29.1).

| ID | Comportamiento contractual | Estado | Evid. |
|---|---|---|---|
| SPA-AR-02 / SPA-INV-02 | Spa usa el módulo general de Empleados; no crea una entidad `Employee` paralela ni otro catálogo de empleados | APROBADO | A (ADR-050 §3 DEC-E1, §13; especificación EMP-BR-21, §19) |
| SPA-AR-02 | Empleados entrega el contrato mínimo de **lectura** `IEmployeeDirectory` dentro de V1; garantías aprobadas: solo lectura, ids estables nunca reutilizados, inactivos incluidos para atenciones históricas | APROBADO (necesidad y garantías) | A |
| SPA-AR-02 | Firmas `GetByIdAsync`, `ListAsync`, `IsActiveOnAsync`, `GetBranchAssignmentsAsync`, `IsEligibleForBranchAsync`, `GetLinkedUserAsync` y `EmployeeSummary` | PROPUESTO (a validar en EMP-E1 y EMP-E6b) | T |
| SPA-AR-03 / SPA-SG-02 / SPA-INV-03 / SPA-CARE-EXP-05 / SPA-CARE-SEG-06 / SPA-SEC-INV-08 / SPA-OPS-VTA-05 | Toda atención identifica por separado al **profesional responsable** (`EmployeeId`) y al **usuario ERP que la registra**; son conceptos distintos (pueden coincidir en una persona: observación [T]); se guardan y auditan por separado. Quien documenta atenciones usa un usuario ERP autorizado | APROBADO (la distinción); campos PROPUESTOS | A (DEC-E4, §13) |
| SPA-AR-04 | Spa conserva permisos clínicos, agenda, horarios, sesiones y disponibilidad; Empleados no implementa funcionalidad clínica ni lógica de Spa | APROBADO | A (§13) |
| SPA-AR-04 | Empleados administra la ficha laboral, los períodos y las asignaciones; Spa no los modifica | APROBADO-DERIVADO | A-der (§7, §8; EMP-BR-07 a EMP-BR-12) |
| SPA-AR-04 | Spa conserva además servicios y tratamientos, seguimiento e información de atención estética | PENDIENTE (recuperado [R], sin aprobación) | R |
| SPA-AR-06 | La elegibilidad del profesional se evalúa con `IEmployeeDirectory` y las fechas de la operación; el pasado no se altera porque el profesional deje la empresa | PROPUESTO | T |
| SPA-OPS-AGE-14 | Si un profesional deja de estar disponible, las citas futuras **no se modifican automáticamente**; se listan para reasignación manual. La política de reasignación es pendiente | PROPUESTO / PENDIENTE | T, P |
| SPA-OPS-SRV-06 | Profesionales elegibles por **especialidad** del catálogo de Empleados (DEC-E5); Spa no crea especialidades. Cargo y profesión pueden ser nulos: no suponer elegibilidad sin regla | PROPUESTO / PENDIENTE | T |

**Verificación pendiente:** `V-02` (contrato `IEmployeeDirectory` real, EMP-E1 a E6b). **Bloqueo:** `D-01` (§29).

## 7. Sucursales y disponibilidad

**Qué hace:** determina cuándo un profesional puede atender en una sucursal. **Estado de la sección:** la pertenencia de agenda, horarios y disponibilidad a Spa está APROBADA (ADR-050 §13); las reglas
operativas son PROPUESTAS o PENDIENTES. **Implementable hoy:** no. **Bloqueos:** BL-01, BL-09 (§29.1).

| ID | Comportamiento contractual | Estado | Evid. |
|---|---|---|---|
| SPA-AG-01 a AG-03 / DUP-13 | Agenda, horarios y disponibilidad son de Spa. La disponibilidad combina horario del establecimiento, horario del profesional, elegibilidad informada por Empleados y ausencia de solape | pertenencia APROBADA; reglas PROPUESTAS | A (§13) + T |
| SPA-OPS-AGE-04 | Un profesional está **disponible** en un instante si: (a) está dentro del horario de la sucursal; (b) dentro de su horario de trabajo; (c) `IEmployeeDirectory` lo informa activo y elegible para la sucursal; (d) no tiene otra cita activa que se solape | PROPUESTO | T |
| SPA-OPS-AGE-03 | Horarios de establecimiento por sucursal como patrón de horas locales de la empresa; el instante UTC lo obtiene `ICompanyClock`; una hora inexistente por cambio de horario se rechaza. No hay precedente de horarios recurrentes (`V-05a`) | PROPUESTO | T, NV |
| SPA-OPS-AGE-05 | Recursos bloqueantes: A sin recursos (solo profesional y sucursal) o B catálogo `Resource` con exclusión de solape por recurso | PENDIENTE (`O-08`) | P |
| SPA-OPS-AGE-06 | Dos citas activas del mismo profesional no se solapan (condición de duplicidad). Si una cita puede comenzar cuando otra termina, o si se exige margen, es pendiente | PROPUESTO / PENDIENTE (`O-09`) | T, P |
| SPA-AG-03 / SPA-OPS-AGE-02 (nota) | La disponibilidad laboral no se confunde con el acceso ERP: una cita no concede ni requiere acceso a una sucursal y el acceso a una sucursal no hace disponible a un profesional | APROBADO-DERIVADO | A-der (ADR-050 §6, §8) |
| Anticipación, tolerancias, bloqueos, confirmaciones | No existe ninguna regla recuperada | PENDIENTE (`O-09`) | P |

## 8. Agenda, citas y reservas

**Qué hace:** programa atenciones y conserva su historial. **Estado de la sección:** el modelo cita/sesión realizada está **APROBADO** (`O-25`, 2026-11-18); lo demás es PROPUESTO o PENDIENTE.
**Implementable hoy:** no. **Bloqueos:** BL-01, BL-09 (§29.1).

### 8.1 Decisión aprobada O-25 (alternativa A — cita separada de la sesión realizada)

Texto literal de los ocho enunciados ([registro](GALI-SPA-V1.1-APPROVED-DECISIONS-01.md) §1.2):

1. **Reserva** es la operación que crea una cita, no una entidad independiente.
2. La **cita** representa la programación en la agenda.
3. La **sesión realizada** es un registro independiente creado cuando se documenta una atención efectivamente realizada.
4. Una **sesión programada** de un paquete se representa mediante una cita vinculada al contrato.
5. **Reservar y reprogramar no consume saldo** de sesiones.
6. **Cancelar o registrar una ausencia no crea una sesión clínica realizada.**
7. El **consumo comercial** se representa por un movimiento independiente del contrato, cuando corresponda.
8. Se conserva la **trazabilidad** de reprogramaciones, cancelaciones y atenciones.

**No aprobado por O-25 (exclusiones):** ausencias que consumen saldo; atención sin cita; varios servicios por cita; cualquier efecto comercial de cancelar sobre el saldo; horarios, recursos, tolerancias,
anticipación y confirmaciones.

### 8.2 Reglas de la cita (PROPUESTAS, compatibles con O-25)

| ID | Comportamiento contractual | Estado | Evid. |
|---|---|---|---|
| SPA-OPS-AGE-01 | Una cita pertenece a una empresa y una sucursal, referencia a un cliente BP activo y a un profesional (`EmployeeId`); su inicio es un instante UTC y su fin se deriva del inicio más la duración instantánea del servicio | PROPUESTO | T |
| SPA-OPS-AGE-02 | Estados candidatos: `Programada`, `Cancelada`, `Ausente`, `Atendida`. «Confirmada» y los recordatorios no se incluyen | PROPUESTO | T |
| SPA-OPS-AGE-07 | Reprogramar **mantiene la identidad de la cita** y registra un cambio (instante anterior y nuevo, autor, motivo); no consume saldo (`O-25` #5). Cancelar y crear otra rompe la trazabilidad y no se propone | PROPUESTO; efecto sobre saldo APROBADO | T + `O-25` |
| SPA-OPS-AGE-08 | Cancelar fija estado `Cancelada`, motivo, autor e instante; nunca elimina. Quién cancela y con qué anticipación es pendiente; no crea sesión realizada (`O-25` #6) | PROPUESTO / PENDIENTE | T, P |
| SPA-OPS-AGE-09 | Ausencia: tras el inicio de la cita, un usuario autorizado la marca `Ausente`; no crea sesión realizada (`O-25` #6). **Su efecto sobre el saldo del paquete es pendiente** (`O-13`) | PROPUESTO / PENDIENTE | T, P |
| SPA-OPS-AGE-10 | La atención efectivamente realizada es un registro distinto de la cita (sesión, §12); la cita pasa a `Atendida` solo cuando existe una sesión realizada vinculada, no por llegar la fecha | APROBADO en el modelo (`O-25` #3); estado `Atendida` PROPUESTO | T + `O-25` |
| SPA-OPS-AGE-11 | Atención sin cita previa: alternativas A admitirla creando cita y sesión juntas, B exigir cita siempre | PENDIENTE (`O-10`) | P |
| SPA-OPS-AGE-12 | Todo cambio de estado, hora, profesional o sucursal queda en `AppointmentChange` y en Entity Audit | APROBADO en el principio de trazabilidad (`O-25` #8); mecanismo PROPUESTO | T |
| SPA-OPS-AGE-13 | Cambiar el profesional de una cita revalida la disponibilidad (§7) y registra el cambio | PROPUESTO | T |
| Transiciones | `Programada → Cancelada`, `→ Ausente`, `→ Atendida`; los tres destinos son finales en el borrador. Si una ausencia se revierte o una cancelada se reactiva es pendiente | PROPUESTO / PENDIENTE | T, P |
| Servicios por cita | Una cita con varios servicios | PENDIENTE (`O-11`) | P |

### 8.3 Flujos (PROPUESTOS)

Reservar (cliente, servicio, sucursal, profesional, hora → comprobar disponibilidad → crear cita `Programada` y su primer registro de cambio, en una transacción); reprogramar; cancelar con motivo; marcar
ausencia solo sobre una cita pasada `Programada`; atender (crear la sesión realizada y marcar la cita `Atendida`). Casos excepcionales: profesional no disponible, cliente o sucursal inactivos, solape por
reserva simultánea, hora inexistente por cambio de horario.

## 9. Servicios y tratamientos

**Qué hace:** define qué se vende y qué procedimiento se realiza. **Estado de la sección:** «servicio = Item `Service`» es VIGENTE-ERP; el resto PROPUESTO o PENDIENTE. **Implementable hoy:** no. **Bloqueos:** BL-01, BL-09 (§29.1).

| ID | Comportamiento contractual | Estado | Evid. |
|---|---|---|---|
| SPA-SV-01 / SPA-OPS-SRV-01 / DUP-14 | Todo servicio comercializable es un Item `Service` de la empresa; Spa no crea otro catálogo comercial. Precio e impuestos los resuelve el ERP | VIGENTE-ERP (Items) + PROPUESTO | E, T |
| SPA-OPS-SRV-03 / SRV-04 | Duración (minutos enteros positivos), sucursales y condiciones de atención son **datos propios de Spa** (Items no los tiene); la cita guarda una instantánea de duración y nombre | PROPUESTO | T |
| SPA-OPS-SRV-02 | **Relación servicio ↔ procedimiento:** A perfil 1:1 por Item (`SpaServiceProfile`); B catálogo `Procedure` con relación N:M a Items (permite servicios compuestos) | PENDIENTE (`O-11`) | P |
| Servicio comercial / procedimiento / tratamiento | El servicio es lo que se vende (Items); el procedimiento es lo que se realiza sobre la persona (Spa); «tratamiento» no tiene definición operativa hasta decidir `O-11` | PROPUESTO | T |
| SPA-OPS-SRV-05 | Sucursales: por defecto atendible en todas las sucursales activas; restringir es una relación opcional. Qué se espera por defecto es pendiente | PROPUESTO / PENDIENTE | T, P |
| SPA-OPS-SRV-09 | Un servicio o procedimiento inactivo no admite citas nuevas y se conserva en el historial; nunca se elimina | PROPUESTO | T |
| SPA-OPS-SRV-10 | Condiciones de atención (precauciones, contraindicaciones, preparación): texto asociado al procedimiento; ningún campo es obligatorio por figurar en una ficha física | PROPUESTO | T |
| SPA-SV-02 | Datos por documentar: nombre y clasificación, duración, sucursal, recursos, profesionales elegibles, insumos y equipos, precio comercial, condiciones de venta, restricciones, estado e historial. **Ninguno declarado obligatorio** | PROPUESTO | T |
| SPA-OPS-SRV-11 / SRV-12 | Historial por Entity Audit de las entidades de servicio de Spa (Items conserva `ItemAudit`); la venta usa el Item `Service` y la facturación existente | PROPUESTO / VIGENTE-ERP | T, E |

**Flujos (PROPUESTOS):** configurar (elegir un Item `Service` activo de la empresa → completar duración, sucursales, especialidades y condiciones → activar); desactivar (deja de admitir citas nuevas; el historial no cambia). Casos excepcionales: Item que no es `Service`, Item inactivo, especialidad inexistente en Empleados, servicio ya configurado. Estados `Activo` e `Inactivo` (habilitar y deshabilitar; nunca eliminar). Un perfil por Item (`UNIQUE (company_id, item_id)`, propuesto). Permisos `spa.services.view` y `spa.services.manage` (§21.2); errores en §26.

## 10. Planes de tratamiento

**Qué hace:** describe lo previsto para la persona. «Plan de tratamiento» es el término del contrato; «plan de atención» (R1) es su sinónimo histórico (`G-07`, propuesta). **Estado de la sección:** contenido [F]
sin contraste; relación con el paquete PENDIENTE. **Implementable hoy:** no. **Bloqueos:** BL-05, BL-09 (§29.1).

| ID | Comportamiento contractual | Estado | Evid. |
|---|---|---|---|
| SPA-AT-01 | El plan registra objetivo, procedimiento, productos y equipos, número previsto de sesiones, intervalos sugeridos, cuidados, precauciones, posible derivación y decisión (proceder, adaptar o posponer) | PROPUESTO | F |
| SPA-AT-05 / DUP-09 | El plan de tratamiento describe lo previsto para la persona; el paquete comercial representa derechos de consumo derivados de una compra. **No se vinculan automáticamente**; su relación (automática, manual o por regla) es pendiente | PROPUESTO / PENDIENTE (`O-13`) | T, P |
| SPA-AT-04 | El resultado final del tratamiento se registra al finalizar; cuándo se considera finalizado, quién lo registra y si admite corrección es pendiente | PROPUESTO / PENDIENTE | F, P |
| SPA-CARE-FAC-05 / COR-08 | La decisión (proceder, adaptar, posponer) es de un solo valor y **no ejecuta acciones automáticas** (no crea citas ni consume sesiones) | PROPUESTO | T |

**Especificación incompleta:** las fuentes no definen entidad, permisos, estados, errores ni pruebas propios del plan de tratamiento. Entidad candidata sin nombre de clase en §24; los permisos del plan no tienen nombre candidato (se supone `spa.records.view` y `spa.assessments.*`, sin que las fuentes lo digan); las pruebas `PT-07` y `PT-16` solo lo rozan.

## 11. Paquetes comerciales

**Qué hace:** controla los derechos de consumo de sesiones derivados de una compra, sin confundirlos con el plan de tratamiento ni crear documentos comerciales. **Estado de la sección:** el origen del
contrato está **APROBADO** (`O-15`, 2026-11-18); composición, saldo, vigencia, devolución y demás políticas siguen PENDIENTES (`O-13`). **Implementable hoy:** no. **Bloqueos:** BL-04, BL-08, BL-09 (§29.1).

### 11.1 Decisión aprobada O-15 (alternativa A — creación explícita tras validar una factura autorizada)

Texto literal de los siete enunciados ([registro](GALI-SPA-V1.1-APPROVED-DECISIONS-01.md) §2.2):

1. **Spa crea explícitamente el contrato de paquete después de validar una factura autorizada.**
2. La creación **referencia la factura y su línea correspondiente**.
3. La creación se realiza **fuera de la transacción de autorización de Ventas**.
4. Deben **evitarse contratos duplicados** para una misma línea facturada.
5. **Un error de Spa no debe revertir la autorización de la factura.**
6. **Spa no crea facturas, CxC, cobros ni asientos contables.**
7. **No se aprueba la creación automática** mediante `SalesInvoiceAuthorizedEvent`.

**No aprobado por O-15:** el estado electrónico SRI requerido para crear el contrato; el comportamiento de los contratos ante anulaciones o notas de crédito; la política frente a facturas a crédito
pendientes de cobro; anticipos y prepagos (`O-14`); la necesidad y el diseño de una lista de facturas pendientes de generar contrato; qué significa «factura autorizada» para este fin.

### 11.2 Reglas (PROPUESTAS, compatibles con O-15 y O-25)

| ID | Comportamiento contractual | Estado | Evid. |
|---|---|---|---|
| SPA-OPS-PKG-04 | El contrato referencia la factura (id y línea) y **no copia importes ni impuestos**; el documento comercial lo posee Ventas | APROBADO en la referencia (`O-15` #2); resto PROPUESTO | T |
| SPA-OPS-PKG-03 / §4.9 | Un contrato se crea una sola vez por línea facturada: restricción única sobre factura y línea | APROBADO en el principio (`O-15` #4); mecanismo PROPUESTO | T |
| SPA-OPS-PKG-01 | El saldo se modela como **libro append-only** (`PackageSessionLedger`: `Granted`, `Consumed`, `Restored`, `Expired`, `Adjusted`); los movimientos no se editan ni eliminan, se compensan | PROPUESTO (el consumo como movimiento independiente: APROBADO, `O-25` #7) | T |
| SPA-OPS-PKG-02 | Una sesión consume como máximo una vez: único `(contract_id, session_id)` en movimientos de consumo | PROPUESTO | T |
| SPA-OPS-PKG-05 | Cambio de profesional o sucursal en una sesión del contrato se rige por «Profesional» y «Sucursal» de §11.3 | PENDIENTE | P |
| SPA-OPS-PKG-06 | Un contrato inactivo, vencido o devuelto no admite sesiones nuevas; sus movimientos se conservan. Las condiciones son pendientes | PROPUESTO / PENDIENTE | T, P |
| SPA-OPS-PKG-07 | No se crea ningún anticipo ni prepago de cliente dentro de Spa | INSTRUCCIÓN DEL PROPIETARIO (`DIR-SSOT-01`) | T + DIR |
| SPA-OPS-VTA-02 | El paquete se vende como Item `Service` por la facturación existente; Spa conserva el contrato y sus sesiones | PROPUESTO (compatible con `O-15`) | T |

Modelo candidato: `PackageDefinition` (Item `Service` que se vende, servicios y cantidades, vigencia opcional, estado), `PackageContract` (cliente, definición, fecha, factura id y línea, vigencia final
opcional, estado), `PackageSessionLedger`. Estados del contrato candidatos: `Activo`, `Agotado`, `Vencido`, `Cancelado` (qué estados existen es pendiente). Permisos y errores en §21 y §26. El libro controla **cantidades de sesiones**, no importes: Spa no mantiene saldos monetarios (§20.4).

### 11.3 Las doce preguntas contractuales (`SPA-PQ-01`)

Ninguna respuesta está aprobada salvo las indicadas. Las alternativas son de R2 §4.5 y **no se eligen**.

| Tema | Pregunta | Estado | Alternativas (sin elegir) |
|---|---|---|---|
| Venta | Qué origina un paquete | **RESUELTA por `O-15`** (creación explícita tras validar una factura autorizada) | La alternativa «evento de autorización» queda no aprobada |
| Composición | Qué servicios y cantidades | PENDIENTE (`O-13`) | Definición cerrada y cantidades fijas / sesiones intercambiables entre servicios |
| Uso | Cuándo una sesión se considera consumida | PENDIENTE (`O-13`); el consumo comercial es un movimiento independiente «cuando corresponda» (`O-25` #7) | Al realizarse / al realizarse o registrarse ausencia |
| Saldo | Cómo calcular pendientes | PENDIENTE | Derivado del libro / almacenado y actualizado bajo lock. Reservar **no consume** (`O-25` #5); si las programadas apartan saldo sigue pendiente |
| Vigencia | Si hay fechas límite | PENDIENTE | Sin vigencia / con vigencia y movimiento `Expired` |
| Reprogramación | Si afecta al saldo | **RESUELTA por `O-25` #5:** reprogramar no consume saldo | La alternativa «afecta según anticipación» queda no aprobada |
| Ausencia | Si descuenta una sesión | PENDIENTE (`O-13`) | No descuenta / descuenta |
| Cancelación | Qué ocurre con el saldo | PENDIENTE (`O-13`) | Se restituye / se restituye según anticipación |
| Devolución | Relación con documentos comerciales | PENDIENTE (`O-13`; trato de líneas `Service` en devoluciones `V-03a` parcial) | La devolución cancela el contrato y sus sesiones no consumidas / no lo afecta y se ajusta manualmente |
| Transferencia | Si cambia el beneficiario | PENDIENTE | No transferible / transferible con trazabilidad |
| Profesional | Si puede variar entre sesiones | PENDIENTE | Puede variar / fijo salvo autorización |
| Sucursal | Si se usa en varias sucursales | PENDIENTE | Cualquiera de la empresa / solo la de contratación |

## 12. Sesiones realizadas y consumo

**Qué hace:** documenta la atención efectivamente prestada y, cuando corresponda, su consumo comercial. **Estado de la sección:** el modelo está **APROBADO** (`O-25` #3, #6, #7); contenido de la sesión [F]
sin contraste; políticas de consumo PENDIENTES. **Implementable hoy:** no. **Bloqueos:** BL-01, BL-05, BL-08, BL-09 (§29.1).

| ID | Comportamiento contractual | Estado | Evid. |
|---|---|---|---|
| SPA-AT-06 | La sesión realizada es un registro independiente de la cita, creado al documentar una atención efectivamente realizada; cancelar o ausentarse no la crea | APROBADO (`O-25` #3, #6) | `O-25` |
| SPA-AT-06 (complemento) | No se registra como realizada por haber llegado la fecha programada | PROPUESTO | T |
| SPA-AT-02 | La sesión registra fecha, procedimiento, productos, aparatología, zona, evolución, observaciones, reacciones posteriores, próxima sesión sugerida y firma o identificación | PROPUESTO | F |
| SPA-AT-07 | Una atención realizada conserva referencias estables a la persona, al profesional (`EmployeeId`), a la sucursal y al tratamiento; estados oficiales, correcciones, anulaciones y restricciones comerciales son pendientes | PROPUESTO / PENDIENTE | T, P |
| SPA-CARE-CNS-06 | Una sesión que requiere consentimiento de procedimiento vigente no se registra como realizada sin él; si se admite excepción o consentimiento verbal es pendiente | PROPUESTO / PENDIENTE | T, P |
| SPA-CARE-CNS-05 | Una sesión interrumpida se registra con instante y motivo y no se marca como realizada por completo; su efecto sobre paquete o cobro, y si genera sesión realizada parcial, son pendientes | PROPUESTO / PENDIENTE | T, P |
| SPA-OPS-PKG-01 / G-14 | **Consumo comercial** = movimiento `Consumed` del libro del contrato (independiente de la sesión realizada). «Consumida» no es sinónimo de «realizada» | APROBADO en la independencia (`O-25` #7); definición de «consumida» PENDIENTE (`O-13`) | T, P |
| Contadores | Contratadas = Σ `Granted`; Programadas = citas activas vinculadas al contrato; Realizadas = sesiones realizadas registradas; Consumidas = Σ `Consumed` − Σ `Restored`; Pendientes = Contratadas − Consumidas | PROPUESTO (Consumidas y Pendientes PENDIENTES) | T, P |
| SPA-OPS-VTA-04 | Registrar una sesión no genera factura, cobro ni movimiento contable | PROPUESTO (compatible con `O-15` #6) | T |
| SPA-AT-03 | Las fotografías de seguimiento se vinculan a la sesión o valoración (§18) | PROPUESTO | F |

## 13. Expedientes estéticos

**Qué hace:** conserva de forma longitudinal la información específica de atención de una persona. **Estado de la sección:** ninguna regla de expediente está aprobada salvo la distinción profesional /
usuario registrador (§6); el contenido [F] no tiene contraste (`V-01`). **Implementable hoy:** no. **Bloqueos:** BL-03, BL-05, BL-09 (§29.1).

| ID | Comportamiento contractual | Estado | Evid. |
|---|---|---|---|
| SPA-FC-01 / G-02 | El expediente es el registro longitudinal, propio de Spa, de la atención de un cliente (antecedentes versionados, valoraciones, sesiones, consentimientos y fotografías); referencia al cliente, no copia su identidad. Las fichas facial y corporal no exigen dos entidades separadas | PROPUESTO | F, T |
| SPA-FC-02 | La ficha contiene número de ficha, fecha, persona, edad y contacto, profesional, motivo de consulta y, en algunas versiones, código del caso y contacto de emergencia | PROPUESTO (qué campos proceden de BP y cuáles se conservan: PENDIENTE) | F, P |
| SPA-CARE-EXP-02 | Número de ficha único por empresa. A: secuencia propia con la infraestructura de secuencias existente; B: número libre digitado. Formato y reinicio pendientes | PENDIENTE (`O-19`) | P |
| SPA-CARE-EXP-03 | Código del caso: significado no recuperado (por tratamiento, por persona o por trabajo académico) | PENDIENTE (`O-19`) | P |
| SPA-CARE-EXP-04 | Cardinalidad: A un expediente por cliente y empresa; B varios (por caso o sucursal) | PENDIENTE (`O-20`) | P |
| SPA-FC-03 / SPA-CARE-EXP-07 | Antecedentes: en cada categoría se distingue «sin responder», «no», «sí» y texto libre; «sin responder» no equivale a «no». Qué preguntas son obligatorias, opcionales o condicionales es pendiente | PROPUESTO / PENDIENTE (`O-21`) | F, T |
| SPA-CARE-EXP-06 / SPA-FC-08 / DUP-06 | Los antecedentes se guardan como **versiones append-only**: corregir crea una versión nueva con autor, fecha y motivo; la anterior se conserva con acceso restringido y no se muestra como vigente. Si una rectificación obliga a ocultar la versión inexacta es `L-NV` | PROPUESTO | T |
| SPA-CARE-EXP-08 | Un expediente nunca se elimina; puede inactivarse. Quién lo cierra y qué impide cerrarlo es pendiente. **Conflicto con la eliminación legal:** `C-02` (§29) | PROPUESTO / BLOQUEADO | T |
| SPA-CARE-EXP-10 | El expediente es de empresa; la sucursal es atributo de cada atención. El acceso entre sucursales es pendiente | PROPUESTO / PENDIENTE (`O-02`) | T, P |
| SPA-CARE-EXP-05 | Cada valoración y sesión referencia al profesional responsable, al usuario registrador y a la sucursal | APROBADO (la distinción); campos PROPUESTOS | A |

Modelo candidato: `CareRecord` (empresa, cliente, número de ficha, código del caso, estado, fecha de apertura), `MedicalHistoryVersion` (append-only), `CareRecordPersonalData` (solo si se decide, `O-06`).
Estados: `Activo` e `Inactivo`. Flujos: abrir (cliente activo con rol Customer + número de ficha + versión inicial opcional), consultar (exige permiso de Spa; `employees.view` no basta), corregir con motivo,
inactivar. La lista de clientes no muestra información clínica (§21).

## 14. Valoración facial

**Qué hace:** registra la valoración estética inicial del rostro. **Estado de la sección:** campos y valores recuperados de la ficha [F] sin contraste; obligatoriedad PENDIENTE (`O-21`). **Implementable
hoy:** no. **Bloqueos:** BL-01, BL-05, BL-09 (§29.1).

| ID | Comportamiento contractual | Estado | Evid. |
|---|---|---|---|
| SPA-FC-04 / SPA-CARE-FAC-01 | La valoración pertenece a un expediente y fija fecha de negocio (`DateOnly`), profesional responsable, usuario registrador y sucursal | PROPUESTO | F, T |
| SPA-CARE-FAC-03 | Una valoración estética **no** se rotula ni exporta como diagnóstico médico; «diagnóstico médico previo» es información declarada, no un diagnóstico de Spa | PROPUESTO | F |
| SPA-CARE-FAC-04 | Fototipo cutáneo: un valor de Fitzpatrick I a VI o «sin evaluar» | PROPUESTO | F, T |
| SPA-CARE-FAC-02 | Una valoración es un registro con versiones append-only; corregir crea una versión nueva con autor, fecha y motivo | PROPUESTO | T |
| SPA-CARE-FAC-06 | Mínimos de integridad: solo los de la regla de lectura 3 (§0.3); los campos clínicos son opcionales hasta decidir lo contrario | PROPUESTO / PENDIENTE (`O-21`) | T, P |
| SPA-CARE-FAC-07 | Si la ficha antigua y la actualizada difieren, la versión digital no asume equivalencia; el contraste de versiones es previo al contrato | PENDIENTE (`O-24`) | P |

Campos recuperados [F] (ninguno declarado obligatorio): motivo de consulta; condición de piel (normal, seca, grasa, mixta); sensibilidad; fototipo; alteraciones observadas (manchas, acné, deshidratación y
otras); zona y tiempo de evolución; exposición solar y protección; observaciones del profesional; diagnóstico médico previo; objetivo del tratamiento; procedimiento, productos y equipos previstos;
precauciones; decisión (proceder, adaptar, posponer). La evolución se registra por sesión (§12), no en la valoración. Estados: `Registrada` y versiones (`Corregida`). Cierre y borrador son pendientes. **Flujos (PROPUESTOS):** registrar la valoración inicial (guardarla incompleta como borrador es una alternativa pendiente), registrar la decisión y corregir con motivo. Permisos `spa.assessments.create`, `spa.assessments.update` y de consulta `spa.records.view`; errores sin catálogo recuperado (§26).

## 15. Valoración corporal

**Qué hace:** registra la valoración corporal, sus mediciones y su comparación. **Estado de la sección:** campos [F] sin contraste; escala numérica de las mediciones PENDIENTE de revisión arquitectónica
(`V-04`, `O-22`). **Implementable hoy:** no. **Bloqueos:** BL-01, BL-05, BL-07, BL-09 (§29.1).

| ID | Comportamiento contractual | Estado | Evid. |
|---|---|---|---|
| SPA-FC-05 / SPA-CARE-COR-01 | La valoración corporal pertenece a un expediente y fija fecha de negocio, profesional responsable, usuario registrador y sucursal | PROPUESTO | F, T |
| SPA-FC-06 / SPA-CARE-COR-02 | Una medición es un conjunto (`BodyMeasurementSet`) con fecha de negocio (`DateOnly`) y tipo `Inicial`, `Control` o `Final`; cada medida es opcional dentro del conjunto | PROPUESTO | F, T |
| SPA-CARE-COR-03 | Un conjunto no se sobrescribe: corregirlo crea otro conjunto o una versión nueva con motivo, autor y fecha | PROPUESTO | T |
| SPA-CARE-COR-04 | Las diferencias entre conjuntos son **derivadas en lectura** y no se almacenan | PROPUESTO | T |
| SPA-CARE-COR-05 / SPA-INV-10 | Los valores numéricos y fechas respetan los estándares oficiales; «no medida» se distingue de cero. **El estándar de precisión no tiene una clase para medidas de peso o contorno**: una columna decimal nueva exige coincidir con una fila de la tabla de escalas o pasar revisión arquitectónica formal. No se propone escala | PENDIENTE (`O-22`, `V-04`) | T, NV |
| SPA-CARE-COR-06 / COR-07 | Frecuencia de medición, vínculo obligatorio u opcional con una sesión, número de controles y unicidad del «final»: no recuperados | PENDIENTE (`O-22`) | P |

Mediciones [F]: peso (kg); brazo derecho e izquierdo, abdomen alto, medio (a la altura del ombligo) y bajo, cadera, muslo derecho e izquierdo (cm). Otros campos [F]: zona corporal, motivo, objetivos
(relajación, reducción, tonificación u otros), hallazgos iniciales, procedimiento, aparatología, precauciones, decisión. La tabla comparativa es de solo lectura; una gráfica es pendiente. **Flujos (PROPUESTOS):** registrar la valoración inicial y la medición `Inicial` → registrar controles → registrar la medición `Final` → consultar la comparación. Estados: `Registrada` y versiones; la comparación no es un estado. Permisos en §21.2; errores sin catálogo recuperado (§26).

## 16. Seguimiento y evolución

**Qué hace:** permite comparar la evolución de una persona a lo largo de sus atenciones. **Estado de la sección:** contenido [F]; sin aprobación. **Implementable hoy:** no. **Bloqueos:** BL-05, BL-09 (§29.1).

| ID | Comportamiento contractual | Estado | Evid. |
|---|---|---|---|
| SPA-AT-02 | La evolución se registra por sesión (fecha, procedimiento, productos, aparatología, reacciones, próxima sesión sugerida) | PROPUESTO | F |
| SPA-AT-03 | La comparación fotográfica exige condiciones comparables (iluminación, distancia, ángulo, sin filtros), registradas como nota de captura; no se verifican automáticamente | PROPUESTO | F, T |
| SPA-CARE-COR-04 | La comparación de mediciones es derivada en lectura | PROPUESTO | T |
| SPA-AT-04 | Resultado final o evolución final del tratamiento (§10) | PROPUESTO / PENDIENTE | F, P |
| SPA-CL-01 | Consulta de la evolución del cliente sin mezclar registros de clientes diferentes | PROPUESTO | F, T |

## 17. Consentimientos y firmas

**Qué hace:** conserva la evidencia de que la persona fue informada y decidió, y distingue cada autorización. **Estado de la sección:** la independencia de las autorizaciones es un requisito [F]
PROPUESTO; firma, versionado, menores, uso académico y conservación PENDIENTES; validez legal NO VERIFICADA. **Implementable hoy:** no. **Bloqueos:** BL-03, BL-05, BL-06, BL-09 (§29.1).

| ID | Comportamiento contractual | Estado | Evid. |
|---|---|---|---|
| SPA-CN-06 / SPA-CARE-CNS-01 / SPA-INV-05 / DUP-04 | Las autorizaciones de procedimiento, de fotografías de seguimiento, de publicación promocional y de uso académico son **independientes**: aceptar una no implica aceptar otra, y la finalidad se verifica contra la autorización vigente en el momento del uso | PROPUESTO | F, T |
| SPA-CN-01 | Consentimiento del procedimiento: información sobre riesgos, limitaciones y cuidados; aceptación o rechazo y firma | PROPUESTO | F |
| SPA-CN-02 / SPA-CN-03 | Fotografías de seguimiento y publicación promocional son autorizaciones diferenciadas; la primera no autoriza publicación | PROPUESTO | F |
| SPA-CARE-CNS-02 | El texto del consentimiento está versionado (`ConsentDefinition`); un registro (`ConsentRecord`) referencia la versión presentada y esa versión no se modifica | PROPUESTO | T |
| SPA-CARE-CNS-03 | Un registro no se edita ni elimina; un cambio de decisión es un registro nuevo o un retiro con fecha, motivo y autor | PROPUESTO | T |
| SPA-CN-05 / SPA-CARE-CNS-04 | **Retiro:** una autorización retirada deja de habilitar usos futuros; no revierte atenciones ni documentos ya emitidos; el material ya publicado fuera del ERP no es controlable por el ERP. Efectos sobre sesiones y material publicado: pendientes. La persona puede detener una sesión | PROPUESTO / PENDIENTE | F, T, P |
| SPA-CN-04 / SPA-CARE-CNS-07 | **Uso académico:** autorización separada solo si el propietario confirma que pertenece al producto; hasta entonces no se implementa ni se asume | PENDIENTE (`O-17`) | F, P |
| SPA-CARE-CNS-08 | Quién consiente: la persona atendida; representante o menores no recuperado | PENDIENTE (`O-18`) | P |

**Mecanismo de firma, versionado y conservación** (`O-16`, sin elegir): firma A documento físico escaneado, B firma manuscrita capturada en pantalla, C aceptación electrónica sin imagen de firma; versionado A
una versión vigente por tipo, B varias vigentes (por procedimiento); conservación A sin plazo, B con plazo (ninguna alternativa elimina físicamente); evidencia A hash del texto presentado y metadatos, B solo
referencia a la versión. La validez legal de cada firma **no está verificada** (`L-INT-05`). Texto de consentimiento y entidades `ConsentWithdrawal` y `SessionInterruption` (§24). Estados: registro `Vigente` o
`Retirado`; definición `Vigente` o `Reemplazada`. **Flujos (PROPUESTOS):** presentar la versión vigente → registrar la decisión y la evidencia → habilitar la atención; retirar; registrar la interrupción de una sesión. Permisos `spa.consents.manage` y `spa.consents.templates`; errores sin catálogo recuperado (§26).

## 18. Fotografías y archivos

**Qué hace:** conserva fotografías de seguimiento con su finalidad y autorización y controla quién las ve. **Estado de la sección:** todo PROPUESTO; el almacenamiento seguro depende de decisiones abiertas
(`O-03`, `O-04`, `O-05`). **Implementable hoy:** no. **Bloqueos:** BL-02, BL-03, BL-06, BL-09 (§29.1).

| ID | Comportamiento contractual | Estado | Evid. |
|---|---|---|---|
| SPA-CARE-FOT-01 | Una fotografía pertenece a un expediente y a una atención (sesión o valoración) y guarda quién la tomó y quién la registró | PROPUESTO | T |
| SPA-CARE-FOT-02 / SPA-SEC-INV-03 | El archivo se almacena mediante un adaptador propio de Spa sobre `IFileStorage` (`ICareFileStore`) y se referencia por identificador; la ruta la **genera el servidor**; nunca hay URL pública: se sirve por un endpoint autorizado | PROPUESTO | T |
| SPA-CARE-FOT-03 / SPA-SEC-INV-02 | La finalidad es una **verificación de autorización en el momento del uso**: ver en seguimiento exige el consentimiento de seguimiento vigente; publicidad, el promocional; académico, el suyo. Ninguna fotografía se sirve sin autorización, consentimiento vigente y registro de acceso | PROPUESTO | T |
| SPA-CARE-FOT-04 | Ninguna fotografía se publica, exporta ni comparte automáticamente; toda exportación es una acción explícita con permiso propio | PROPUESTO | T |
| SPA-CARE-FOT-05 | Las fotografías no aparecen en listados ni en la agenda; solo dentro del expediente y con permiso | PROPUESTO | T |
| SPA-CARE-FOT-06 / SPA-SEC-AUD-01 | Cada consulta o exportación queda en el registro de accesos (§22) | PROPUESTO | T, NV |
| SPA-CARE-FOT-07 / SPA-SEC-INV-04 | Las fotografías se desactivan lógicamente; no se invoca `DeleteAsync` sobre ellas salvo el procedimiento de eliminación legal aprobado | PROPUESTO / BLOQUEADO (`C-02`) | T |
| SPA-CARE-FOT-08 | Retiro de la autorización de seguimiento: conservar y bloquear, anonimizar o eliminar | PENDIENTE (`O-05`) | P |
| SPA-CARE-FOT-09 a FOT-10 | Metadatos del dispositivo, formatos, tamaño máximo, compresión y número de fotografías por atención: no recuperados | PENDIENTE | P |
| SPA-CARE-FOT-11 | Las condiciones comparables se registran como nota de captura | PROPUESTO | F, T |
| SPA-FO-01 | La especificación de seguridad de imágenes cubre almacenamiento, referencias, permisos de lectura, acceso a originales, exportaciones, registro de accesos, retención, revocaciones y errores | PROPUESTO | T |

**Protección técnica (propuestas de R4 §5, ninguna aprobada):** `LocalFileStorage` sobrescribe, borra físicamente, no valida contención de ruta ni cifra, y comparte volumen con certificados y keyring; Spa no
lo modifica. Recomendación técnica no aprobada: referencias privadas, lectura autorizada y auditada, rutas del servidor, hash SHA-256 del contenido, ausencia de borrado físico y respaldos cifrados con
clave separada (`SPA-SEC-INV-05`); muy recomendable antes de datos reales de salud: cifrado de aplicación por archivo con cifrado de sobre y clave maestra externa. Cifrado de campos clínicos: segunda etapa.
Cifrado de disco y publicación del directorio de archivos: `V-07` (verificación con infraestructura). Decisión `O-04`.

**Flujos (PROPUESTOS):** subir una fotografía a una atención (comprobando el consentimiento de seguimiento vigente) → consultar dentro del expediente → comparar un grupo → exportar con permiso → desactivar. Estados: `Activa` e `Inactiva`; el bloqueo por retiro de autorización es pendiente (`SPA-CARE-FOT-08`). Permisos `spa.photos.view`, `spa.photos.create` y `spa.records.export`; errores sin catálogo recuperado (§26).

## 19. Equipos e insumos

**Qué hace:** documenta los productos y equipos usados en cada atención. **Estado de la sección:** registro documental [F]; **sin movimientos de inventario**. **Implementable hoy:** no. **Bloqueos:** BL-09 (§29.1).

| ID | Comportamiento contractual | Estado | Evid. |
|---|---|---|---|
| SPA-IN-01 | Los formularios registran los productos y equipos usados durante los tratamientos | PROPUESTO | F |
| SPA-IN-02 / SPA-OPS-SRV-07 / DUP-12 | El registro de insumos es documental y no genera movimientos de inventario; cualquier movimiento exige un contrato aprobado. Alternativas sin elegir: A registro documental por sesión; B lista sugerida por procedimiento; C salida manual con los ajustes de inventario existentes; D consumo automático (no se propone; N-04) | PENDIENTE (`O-12`) | T, P |
| SPA-OPS-SRV-08 / SPA-IN-03 | Aparatología: texto o referencia de identificación por procedimiento y por sesión; sin módulo de activos (N-05) | PROPUESTO | T |

## 20. Integración con Ventas, Caja y CxC

**Qué hace:** relaciona la atención y el paquete con los documentos comerciales sin duplicarlos. **Estado de la sección:** la frontera (Spa no crea documentos comerciales) y el origen del contrato están
**APROBADOS** (`O-15`); anticipos y prepagos PENDIENTES (`O-14`). **Implementable hoy:** no. **Bloqueos:** BL-04, BL-09 (§29.1).

### 20.1 Capacidades existentes y no existentes del ERP

| Capacidad | Existe | Consecuencia para Spa |
|---|---|---|
| Vender un servicio | **Sí:** factura con Item `Service` sin bodega; la factura nace ligada a una sesión de caja (`CashSession`, de Caja; distinta de la sesión realizada de Spa) | Spa referencia la factura; no toca Caja |
| Pago parcial al facturar | **Sí:** medios no-Crédito y el saldo como CxC | Nada nuevo |
| Cobrar un saldo posterior | **Sí:** cobro aplicado a una CxC existente | Nada nuevo |
| Devolver o anular una venta | **Sí**, con las reglas de Ventas (un solo camino: anular o devolver; un comprobante autorizado por el SRI no se anula, se corrige con Nota de Crédito) | El efecto sobre el contrato es pendiente (`O-13`) |
| Facturar por sesión | No | N-01 |
| Anticipo o prepago de cliente; cobro sin CxC; saldo a favor | **No** (solo existe anticipo de **proveedor**, ADR-035) | N-02 |
| Reembolso de devolución | Solo en efectivo o contra CxC | N-03 |
| Vendedor y cobrador distintos del profesional | No (Ventas FROZEN) | N-08 |

### 20.2 Reglas

| ID | Comportamiento contractual | Estado | Evid. |
|---|---|---|---|
| SPA-OPS-VTA-01 / SPA-AR-05 / SPA-INV-08 | Spa **no** crea facturas, CxC, cobros ni asientos contables | APROBADO (`O-15` #6) | `O-15` |
| SPA-OPS-VTA-01 (complemento) | Spa referencia documentos de Ventas y CxC por identificador y no registra movimientos de Caja (X-08) | PROPUESTO | T |
| SPA-OPS-VTA-02 / SPA-OPS-PKG-04 | El paquete se vende como Item `Service`; el contrato se crea explícitamente tras validar una factura autorizada, referenciando factura y línea (§11) | APROBADO (`O-15` #1, #2) | `O-15` |
| §11.1 #3, #5 | La creación es una operación propia de Spa, fuera de la transacción de autorización de Ventas; un fallo de Spa no revierte la factura | APROBADO (`O-15` #3, #5) | `O-15` |
| SPA-OPS-VTA-05 / SPA-VT-01 | El profesional que presta el servicio y el usuario que registra u opera la transacción se identifican por separado; vendedor y cobrador son pendientes | APROBADO-DERIVADO + PENDIENTE | A-der |
| SPA-OPS-VTA-06 | La devolución o anulación comercial no modifica el libro de sesiones por sí sola; la acción sobre el contrato es explícita y sigue la política de `SPA-PQ-01` | PROPUESTO / PENDIENTE (`O-13`) | T, P |
| Validación de la creación | Cliente del contrato igual al de la factura; factura autorizada; línea no vinculada antes. Errores `SPA_INVOICE_NOT_AUTHORIZED`, `SPA_INVOICE_CUSTOMER_MISMATCH`, `SPA_INVOICE_LINE_ALREADY_LINKED` | PROPUESTO | T |
| Estado SRI, anulaciones/NC, facturas a crédito, lista de pendientes | Condiciones de creación y efectos posteriores | PENDIENTE (`O-15` §2.5; `O-13`, `O-14`) | P |

### 20.3 Lectura de la factura

El estado de la factura, de la CxC y de los cobros pertenece a Ventas y CxC [E]. Spa guarda solo identificador de factura y de línea (y de CxC si la consulta) más el estado de sus propias entidades. El
contrato se crea leyendo la factura mediante una consulta existente de Ventas (V-03b: el evento no trae cliente ni líneas); no se extiende Ventas.

### 20.4 Anticipos de Clientes: proceso independiente y transversal (`DIR-SSOT-01`)

**Directriz del propietario (encargo SPA-V1.1-SSOT-CONSOLIDATION, 2026-11-18):** *Anticipos de Clientes es un proceso independiente y transversal del ERP, no una entidad financiera propia de Gali Spa.*
Alcance: esta directriz fija la **propiedad** del proceso; **no** decide si el proceso existirá, ni su diseño, ni cierra `O-14`. A la fecha no está registrada en el
[registro de decisiones aprobadas](GALI-SPA-V1.1-APPROVED-DECISIONS-01.md).

- `O-14` **permanece abierta**. Su posible desarrollo es un **proyecto separado** (futuro, sin documento, sin ADR y sin autorización): no se diseña aquí ni dentro de Ventas ni de CxC.
- **Único punto de integración que Spa podría necesitar** (no es contrato): disponer, si ese proceso llegara a existir, de un modo de (a) consultar el saldo de anticipo de un cliente, (b) aplicarlo como
  pago de la factura que origina un paquete y (c) tramitar su devolución, **siempre mediante el contrato del propio proceso transversal**, sin copiar su estado ni duplicar sus reglas.
- Efectos contables y fiscales de los anticipos de clientes (cuenta, traductor, tratamiento SRI): **no verificados** [NV]; el repositorio no los documenta.
- **Mientras `O-14` siga abierta, ninguna regla de este contrato depende de anticipos**; las funciones que los requieren quedan **NO AUTORIZADAS** (N-02, N-03). Qué se hace en V1.1 cuando el negocio necesite
  cobrar antes de facturar es una **decisión comercial pendiente** (§30), no una conclusión de este documento.

## 21. Seguridad y privacidad

**Qué hace:** limita quién puede ver y hacer qué con datos de salud, fotografías y consentimientos. **Estado de la sección:** lo aprobado se limita a que Empleados, Access y las asignaciones **no** dan acceso
clínico (ADR-050); la política de acceso (Admin, sucursales), el cifrado y el catálogo de permisos son PROPUESTOS o PENDIENTES. **Implementable hoy:** no. **Bloqueos:** BL-02, BL-09 (§29.1).

### 21.1 Reglas

| ID | Comportamiento contractual | Estado | Evid. |
|---|---|---|---|
| SPA-SG-01 / SPA-INV-04 / SPA-CARE-SEG-01 / SPA-CARE-SEG-02 / SPA-SEC-INV-07 / DUP-03 | Ningún permiso `employees.*` o `access.*` ni una asignación laboral a una sucursal concede acceso a expedientes, valoraciones, consentimientos o fotografías; Spa declara permisos propios en el Kernel y ningún perfil existente los recibe automáticamente | APROBADO / APROBADO-DERIVADO | A (ADR-050 §6, §8, §13) |
| SPA-CARE-SEG-03 / SPA-SG-04 | Los permisos de Spa se declaran solo en el Kernel con la convención `modulo.recurso.accion` | PROPUESTO | T |
| SPA-SG-03 | Las fichas contienen antecedentes de salud, información corporal, fotografías y consentimientos: confidencialidad explícita; la política de lectura, edición, fotografías, exportación, auditoría, retención y corrección está pendiente | PROPUESTO / PENDIENTE | F, P |
| SPA-SG-05 / SPA-SEC-INV-12 | Aislamiento por tenant y empresa (entidades `ICompanyOperationalEntity`, filtro fail-closed) en lecturas, exportaciones y registros de acceso; la sucursal es un filtro del recurso | VIGENTE-ERP + PROPUESTO | E, T |
| SPA-SG-06 / SPA-CARE-SEG-05 / SPA-CARE-FOT-05 / DUP-15 | Las proyecciones, listados y la agenda no exponen datos clínicos ni fotografías; estos solo se ven dentro del expediente y con permiso | PROPUESTO | T |
| SPA-SEC-INV-09 | El acceso excepcional exige motivo y queda marcado para revisión | PROPUESTO (solo si se aprueba la alternativa D) | T |
| SPA-SEC-R-01 / SPA-CARE-SEG-04 | **Acceso del rol Admin a datos clínicos.** El autorizador resuelve el bypass del Admin antes de la clave de permiso; excepcionarlo modifica el contrato LOCKED. Alternativas: A aceptar el bypass (control solo por auditoría de lecturas); B permisos «sellados» sin bypass (cambia Kernel, Access, sesión, navegación y frontend; revisión arquitectónica expresa); C concesión de acceso clínico propia de Spa (`CareAccessGrant`) exigida además del permiso; D acceso excepcional con motivo (variante de C). Recomendación técnica no aprobada: C + D, con B como cambio futuro | **BLOQUEADO** (`C-01`, `O-01`) | T, P |
| SPA-SEC-R-08 | **Acceso entre sucursales:** A expediente de empresa visible a todo usuario con el permiso; B limitado a sucursales con `CompanyUserBranch` (patrón `IBranchAccessGuard`); C la sucursal de cada atención limita solo esa atención | PENDIENTE (`O-02`) | P |

### 21.2 Catálogo unificado de permisos candidatos (30)

Ninguno existe en el Kernel ni está aprobado. Una operación tiene un solo nombre; los seis nombres repetidos entre borradores tienen un significado único (`C-09` cerrado en nombres).

| Familia | Permisos candidatos | Sección |
|---|---|---|
| Expediente | `spa.records.view`, `spa.records.manage`, `spa.records.export` | §13, §18 |
| Antecedentes | `spa.history.view`, `spa.history.update` (separado del de valoraciones por su sensibilidad) | §13 |
| Valoraciones | `spa.assessments.create`, `spa.assessments.update` | §14, §15 |
| Sesiones | `spa.sessions.create` (registrar la atención; no equivale a `spa.packages.consume`) | §12 |
| Consentimientos | `spa.consents.manage`, `spa.consents.templates` | §17 |
| Fotografías | `spa.photos.view`, `spa.photos.create` | §18 |
| Agenda | `spa.agenda.view`, `spa.agenda.create`, `spa.agenda.update`, `spa.agenda.cancel`, `spa.agenda.mark-absent`, `spa.availability.manage` | §7, §8 |
| Servicios | `spa.services.view`, `spa.services.manage` | §9 |
| Paquetes | `spa.packages.view`, `spa.packages.manage` (definir paquetes y crear contratos), `spa.packages.consume`, `spa.packages.adjust` | §11, §12 |
| Auditoría | `spa.audit.view` (cambios de un expediente), `spa.access-log.view` (quién leyó) | §22 |
| **Reservados** (existen solo si se aprueba la alternativa correspondiente) | `spa.records.emergency-access`, `spa.access-grants.manage` (`O-01`, alternativas C y D); `spa.erasure.request`, `spa.erasure.execute` (`O-05`) | §21, §22 |

**Sin permiso candidato o con alcance ambiguo (PENDIENTE; no se inventan nombres):** consultar sesiones realizadas, valoraciones y consentimientos (se supone dentro de `spa.records.view` sin decirlo);
desactivar fotografías; separar horarios del establecimiento de los del profesional; consultar información comercial desde Spa (requeriría además el permiso de lectura de Ventas aplicable, cuyo nombre no se
verificó); administrar claves de cifrado (operación de infraestructura, no permiso de usuario).

### 21.3 Dependencias

El Kernel solo admite claves nuevas; ningún perfil existente las recibe; la regla anti-escalamiento de perfiles y membresías sigue vigente. Cambiar el bypass del Admin, el autorizador o la navegación exige
autorización arquitectónica explícita (X-09).

## 22. Auditoría y conservación

**Qué hace:** deja rastro de quién hizo o consultó qué, y define cuánto se conserva. **Estado de la sección:** todo PROPUESTO o PENDIENTE; la tensión entre el no borrado y la eliminación legal está
**BLOQUEADA**. **Implementable hoy:** no. **Bloqueos:** BL-02, BL-03, BL-06, BL-09 (§29.1).

### 22.1 Auditoría

| ID | Comportamiento contractual | Estado | Evid. |
|---|---|---|---|
| SPA-AU-01 | La auditoría de escrituras usa Entity Audit oficial (`AuditRecordBase`, `AuditActor`, `IAuditService`); sin sistema paralelo; cada entidad de Spa añade la suya | PROPUESTO | T, E |
| SPA-AU-02 | Operaciones a auditar: creación de expediente; modificación de valoración; registro o corrección de sesión; aceptación o retiro de consentimiento; cambios de agenda; consulta o exportación de imágenes; cambios en asignaciones operativas de atención | PROPUESTO (el principio de trazabilidad de reprogramaciones, cancelaciones y atenciones: APROBADO, `O-25` #8) | T |
| SPA-SEC-AUD-01 / SPA-SEC-INV-01 / DUP-05 | Toda lectura exitosa de detalle de un expediente, valoración, antecedentes, consentimiento o fotografía, toda descarga y exportación deja un registro append-only con usuario, empresa, recurso, operación, instante y resultado, **sin contenido clínico** | PROPUESTO | T |
| SPA-SEC-AUD-02 a AUD-06 / SPA-SEC-INV-06 | Un listado registra una entrada por solicitud; los accesos denegados se registran (`Denied`); el registro es append-only, su consulta exige permiso propio y se registra a su vez; el acceso excepcional exige motivo y revisión; no hay contenido clínico ni nombres de archivo | PROPUESTO | T |
| SPA-SEC-AUD-07 | Plazo de conservación del registro de accesos | PENDIENTE (`O-03`) | P |
| Entidad | `CareAccessLog` (hereda de `AuditRecordBase`; propia de Spa; sin modificar la infraestructura FROZEN). **Unifica** el `PhotoAccessLog` de R3 (§B-05) | PROPUESTO | T |
| Modo de escritura | A síncrona fail-closed; B asíncrona con outbox ([ADR-008](ADR-008-outbox-pattern-foundation.md)); C mixta (A para fotografías, exportaciones y apertura del expediente completo; B para listados). Recomendación técnica no aprobada: C | PENDIENTE (`O-03`) | T, P |

### 22.2 Conservación, corrección y eliminación

| ID | Comportamiento contractual | Estado | Evid. |
|---|---|---|---|
| SPA-SEC-RET-01 / DUP-07 | Ningún borrado físico fuera de un procedimiento de eliminación legal aprobado. **No existe redacción canónica única**: R3 dice «nunca se elimina»; R4 propone un procedimiento controlado | **BLOQUEADO** (`C-02`, `O-05`) | T |
| SPA-SEC-RET-02 | El procedimiento exige permiso propio, solicitud registrada con fecha y titular, aprobación, y deja un registro del hecho sin datos personales | PROPUESTO (condicionado a `O-05`) | T |
| SPA-SEC-RET-03 | Los antecedentes corregidos conservan sus versiones anteriores con acceso restringido, sin mostrarse como vigentes | PROPUESTO | T |
| SPA-SEC-RET-04 | Un retiro de consentimiento suspende el uso futuro; las atenciones ya realizadas siguen su plazo de conservación | PROPUESTO | T |
| SPA-SEC-RET-05 | Los respaldos con datos eliminados siguen un ciclo de rotación documentado; con borrado criptográfico la clave eliminada cubre los respaldos | PROPUESTO | T |
| SPA-SEC-RET-06 | Plazos de conservación por tipo de dato | PENDIENTE (`L-NV`) | P |

Alternativas de eliminación o anonimización (sin elegir): A borrado criptográfico (cifrado por expediente o archivo); B anonimización en el lugar; C borrado físico con lápida (contradice la regla permanente); D
no eliminar nunca (incompatible con una obligación legal de eliminación, si aplica). Recomendación técnica no aprobada: A + B bajo un procedimiento aprobado. Distinciones: corrección, retiro de consentimiento,
restricción de acceso, conservación histórica y eliminación son conceptos distintos; el borrado arbitrario está prohibido.

### 22.3 Obligaciones legales y su estado de verificación

Fuente: [R4 §7](GALI-SPA-V1.1-SECURITY-COMPLIANCE-DRAFT-01.md) (consulta del 2026-11-18 a textos de gob.ec extraídos de PDF con errores de reconocimiento de caracteres). **No es asesoría jurídica ni certificación de
cumplimiento**; cada cita debe confirmarse contra el Registro Oficial (`V-09`); no se verificó ninguna reforma posterior a enero de 2025.

| Estado | Contenido |
|---|---|
| **L-EXPRESO** (texto leído; no certificado) | Consentimiento libre, específico, informado, inequívoco y revocable (LOPDP Art. 8; Reglamento Arts. 5 y 6); demostrabilidad del consentimiento (Reglamento Art. 5); rectificación y eliminación atendidas en 15 días (LOPDP Arts. 14, 15); datos sensibles y de salud como categorías especiales (Arts. 25, 26, 30, 31); menores de 15 años: consentimiento del representante legal (Art. 21; Reglamento Arts. 19, 20); medidas de seguridad incluido el cifrado (Art. 37); notificación de vulneraciones en 5 días y del encargado al responsable en 2 días (Art. 43; Reglamento Arts. 24 a 28); delegado y evaluación de impacto en el tratamiento a gran escala de categorías especiales (Art. 48; Reglamento Arts. 31, 53, 54); firma electrónica y mensajes de datos (Ley 67, Arts. 2, 13 a 15, 52 a 55) |
| **L-INTERPRETACIÓN** (lectura del autor; requiere profesional legal) | Los antecedentes declarados son probablemente datos de salud (`L-INT-01`); el régimen sancionatorio estaría vigente desde el 26 de mayo de 2023 (`L-INT-04`); una firma capturada en pantalla o una aceptación electrónica pueden o no cumplir el Art. 15 de la Ley 67 (`L-INT-05`) |
| **L-NV** (no verificado) | Si Spa es «profesional de la salud» del Sistema Nacional de Salud (`L-INT-01`); si las fotografías son datos biométricos (`L-INT-02`); si el volumen es «gran escala» (`L-INT-03`); cómo se acredita la representación de un menor (`L-INT-06`); quién es «responsable» y quién «encargado» (`L-INT-07`); normativa no consultada: Ley Orgánica de Salud, normativa sanitaria de establecimientos de estética, Código de la Niñez y Adolescencia, protección al consumidor, derecho a la propia imagen, resoluciones de la Superintendencia de Protección de Datos Personales y plazos de conservación de historias clínicas |

Recomendaciones técnicas derivadas (no requisitos legales): `SPA-SEC-LEG-01` a `SPA-SEC-LEG-07` (mostrar la información del Art. 12 antes del consentimiento y registrar la versión mostrada; conservar la
evidencia demostrable; registrar solicitudes de derechos con plazo de 15 días; plan de incidentes de 5 y 2 días; minimización; anonimización para fines distintos de la atención; asesoría legal previa).
**Decisión `O-07` y verificación `V-06`: asesoría legal antes de aprobar retención, exportación, publicación, firma y atención a menores.**

## 23. Pantallas y experiencia de usuario

**Estado de la sección:** cobertura PROPUESTA (`SPA-UX-01`); no hay diseño aprobado, no se define estructura de menús ni se modifica `MENU_MAP.md`. **Implementable hoy:** no.

| Pantalla candidata | Función | Sección |
|---|---|---|
| Agenda (día o semana, por sucursal y profesional) | Crear cita rápido, reprogramar, cancelar, marcar ausencia, indicar profesional no disponible | §8 |
| Configuración de servicios de Spa | Lista → Editor (patrón oficial de configuración) | §9 |
| Paquetes y contratos | Definir paquetes, crear contrato desde una factura autorizada, ver saldo y movimientos | §11, §12 |
| Ficha del cliente / expediente | Secciones: general, antecedentes, valoraciones, sesiones, fotografías, consentimientos | §13 |
| Valoración facial y corporal; tabla comparativa de mediciones | Registro y comparación de solo lectura | §14, §15 |
| Registro de sesión | Documentar la atención; vincular fotografías y consentimiento | §12, §16 |
| Consentimientos | Registrar, consultar, retirar; versión y estado | §17 |
| Visor de fotografías (dentro del expediente) | Miniaturas solo con permiso; comparación; exportar | §18 |
| Registro de accesos y de cambios | Consulta restringida | §22 |

Reglas PROPUESTAS: usar los componentes y patrones oficiales ([frontend](../architecture/frontend.md)) y el componente decimal oficial; ocultar acciones sin permiso (la autoridad es el backend); indicar cuándo un dato
es sensible; no mostrar datos clínicos en listados, notificaciones ni agenda; adaptación móvil según las reglas de diseño existentes; sin sistemas paralelos de navegación, avisos ni validaciones.

## 24. Entidades y relaciones

**Estado de la sección:** todas las entidades son **candidatas**, ninguna existe y su separación definitiva en tablas y agregados requiere aprobación. Los nombres siguen los borradores; `Session` significa
exclusivamente sesión realizada (§0.3).

| Grupo | Entidades candidatas | Referencias a módulos oficiales (sin copiar sus datos) |
|---|---|---|
| Agenda | `Appointment`, `AppointmentChange` (append-only), horario de establecimiento, horario de profesional, `Resource` (solo si `O-08` lo decide) | `BusinessPartner`, `Employee`, `Branch`, `Item` |
| Servicios | `SpaServiceProfile` o `Procedure` (`O-11`); relaciones opcionales servicio–sucursal y servicio–especialidad | `Item`; especialidades de Empleados |
| Plan de tratamiento | Plan de tratamiento del expediente (las fuentes no proponen nombre de clase; R1 §10 lo llama «Plan de atención») | Expediente, procedimiento |
| Paquetes | `PackageDefinition`, `PackageContract`, `PackageSessionLedger` | Factura (id y línea) de Ventas |
| Sesiones | `Session` (sesión realizada), `SessionInterruption` | `Appointment`, `Employee`, `Branch` |
| Expediente | `CareRecord`, `MedicalHistoryVersion`, `CareRecordPersonalData` (solo si `O-06`), valoración facial y corporal, `BodyMeasurementSet` | `BusinessPartner` |
| Consentimientos | `ConsentDefinition`, `ConsentRecord`, `ConsentWithdrawal` | — |
| Fotografías | `CarePhoto`, grupo comparativo | `IFileStorage` mediante `ICareFileStore` |
| Seguridad y auditoría | `CareAccessLog` (unifica `PhotoAccessLog`), `CareAccessGrant` (solo si `O-01` C o D), solicitud de eliminación (solo si `O-05`) | `AuditRecordBase`, `AuditActor` |

Relaciones: un cliente (BP) tiene uno o varios expedientes (`O-20`); el expediente contiene valoraciones, sesiones realizadas, consentimientos y fotografías; una cita (resultado de una reserva) puede dar lugar
a una sesión realizada; una sesión sugerida de un paquete es una cita vinculada al contrato (`O-25` #4); el contrato referencia una línea de factura (`O-15` #2); el consumo es un movimiento del libro del contrato
que referencia contrato y, según el caso, cita o sesión. Profesional (`EmployeeId`) y usuario registrador (usuario ERP) se guardan siempre por separado.

## 25. API y contratos de aplicación

**Estado de la sección:** `SPA-API-01` [T]; **ninguna ruta, endpoint ni DTO está aprobado.** Cada operación deberá declarar permiso, scope, validación, errores, transacción, concurrencia, respuesta y pruebas, y
usar el envelope oficial de la API.

| Dominio | Capacidades conceptuales | Permiso candidato (§21.2) |
|---|---|---|
| Expedientes | Abrir, consultar, listar por cliente, registrar y corregir antecedentes, inactivar | `spa.records.*`, `spa.history.*` |
| Valoraciones | Registrar, consultar y corregir valoraciones y mediciones | `spa.assessments.*` |
| Consentimientos | Registrar, consultar, retirar, administrar textos | `spa.consents.*` |
| Fotografías | Subir, consultar metadatos, ver (flujo autorizado), comparar, exportar, desactivar | `spa.photos.*`, `spa.records.export` |
| Agenda | Consultar disponibilidad, reservar, reprogramar, cancelar, marcar ausencia, atender; horarios | `spa.agenda.*`, `spa.availability.manage` |
| Sesiones | Documentar la atención y la evolución | `spa.sessions.create` |
| Servicios | Consultar y administrar la configuración de servicios | `spa.services.*` |
| Paquetes | Definir paquetes; **crear contrato desde una factura autorizada** (`O-15`); consultar saldo; consumir; restituir; ajustar con motivo | `spa.packages.*` |
| Integraciones | Consultar clientes, empleados y facturas por sus contratos oficiales (solo lectura) | Permisos de los módulos origen |
| Auditoría | Consultar historial de cambios y registro de accesos | `spa.audit.view`, `spa.access-log.view` |

## 26. Validaciones y errores

**Estado:** todo PROPUESTO. Los códigos viven en `ApiResponseCodes` y `MessageCatalog`; el HTTP lo deriva la categoría (`Validation` 422, `BusinessRule` 400, `Duplicate` 409, `NotFound` 404,
`Authorization` 403) [E].

| Área | Validaciones candidatas | Errores candidatos (categoría) |
|---|---|---|
| Agenda | Cliente y profesional existentes y activos; sucursal activa de la empresa; servicio activo; inicio dentro del horario; duración positiva; sin solape; motivo de cancelación acotado | `SPA_APPOINTMENT_NOT_FOUND` (NotFound), `SPA_APPOINTMENT_OVERLAP` (Duplicate), `SPA_PROFESSIONAL_UNAVAILABLE` (BusinessRule), `SPA_OUTSIDE_BUSINESS_HOURS` (BusinessRule), `SPA_APPOINTMENT_INVALID_STATE` (BusinessRule), `SPA_INVALID_DATETIME` (Validation), `SPA_CONCURRENT_MODIFICATION` (Duplicate) |
| Servicios | El Item existe, es de la empresa, tiene naturaleza `Service` y está activo; duración positiva; especialidades existentes | `SPA_SERVICE_NOT_FOUND` (NotFound), `SPA_ITEM_NOT_SERVICE` (BusinessRule), `SPA_SERVICE_INACTIVE` (BusinessRule) |
| Paquetes | Cliente igual al de la factura; saldo suficiente para consumir; sesión del mismo contrato; sin consumo repetido | `SPA_PACKAGE_NOT_FOUND` (NotFound), `SPA_PACKAGE_NO_BALANCE` (BusinessRule), `SPA_PACKAGE_ALREADY_CONSUMED` (Duplicate), `SPA_PACKAGE_INACTIVE` (BusinessRule) |
| Facturación | Factura autorizada; mismo cliente; línea no vinculada | `SPA_INVOICE_NOT_AUTHORIZED` (BusinessRule), `SPA_INVOICE_CUSTOMER_MISMATCH` (BusinessRule), `SPA_INVOICE_LINE_ALREADY_LINKED` (Duplicate) |
| Expediente y clínica | Cliente activo y de la empresa; profesional existente y activo en la fecha (`IEmployeeDirectory`); motivo de corrección y textos acotados; «sin responder» ≠ «no»; fechas `DateOnly`; medidas con la precisión oficial | Sin catálogo recuperado (PENDIENTE) |
| Consentimientos y fotografías | Consentimiento vigente para la finalidad; versión presentada existente; ruta generada por el servidor | Sin catálogo recuperado (PENDIENTE) |

## 27. Reglas de concurrencia

**Estado:** PROPUESTO; verificaciones técnicas pendientes antes de programar (como se hizo en EMP-E1).

| Tema | Regla propuesta | Evidencia |
|---|---|---|
| Citas del mismo profesional | Restricción `EXCLUDE USING gist (professional_id WITH =, tstzrange(start_utc, end_utc) WITH &&) WHERE (activa)`; reutiliza el patrón de `accounting_periods` (que usa `daterange`; **no hay precedente de `tstzrange`**, `V-05b`) | [NV] |
| Reserva y reprogramación | Advisory lock transaccional por profesional (patrón `AccountingOperationalPolicyRepository`) y token de versión (`xmin`) | [E] el patrón; el diseño [NV] |
| Consumo de sesiones | Advisory lock por contrato en una transacción; idempotencia por `(contract_id, session_id)` (`SPA-OPS-PKG-02`) | T |
| Creación del contrato | Único por factura y línea; el fallo no revierte la autorización (`O-15` #4, #5) | APROBADO el principio; mecanismo T |
| Saldo | Sin saldo negativo salvo política expresa | PENDIENTE |
| Perfil de servicio | `UNIQUE (company_id, item_id)`; versión optimista | T |
| Número de ficha | Único por empresa bajo concurrencia (`SPA-CARE-T-EXP-02`) | PENDIENTE (`O-19`) |
| Archivos | Rutas generadas por el servidor para que dos subidas simultáneas no se sobrescriban (`SPA-SEC-T-07`) | T |

## 28. Pruebas y criterios de aceptación

**Estado:** las 102 pruebas candidatas son **PROPUESTAS**; ninguna está aprobada, y los criterios de las reglas PENDIENTES o BLOQUEADAS no pueden fijarse. Las pruebas de concurrencia, aislamiento y cifrado
exigen PostgreSQL real y almacenamiento aislado. De los 102 identificadores, 80 son literales de sus fuentes y 22 son inferidos de la notación abreviada de R3 (columna «inferidas»).

### 28.1 Pruebas por requisito

| PT | Tema | Secciones del contrato | Requisitos verificados | Pruebas literales | Pruebas inferidas | Condición |
|---|---|---|---|---|---|---|
| PT-01 | Cliente e identidad | §5 | SPA-AR-01, SPA-INV-01, SPA-FC-07, SPA-CARE-EXP-01, SPA-SEC-INV-11 | `SPA-T-01`, `SPA-CARE-T-EXP-01`, `SPA-OPS-T-VTA-04`, `SPA-SEC-T-13` | — | Abierto en lo no recuperado (flujo de cliente inexistente) |
| PT-02 | Profesional, empleado y usuario registrador | §6 | SPA-AR-03, SPA-SG-02, SPA-INV-02, SPA-INV-03, SPA-CARE-EXP-05, SPA-SEC-INV-08 | `SPA-T-02`, `SPA-T-03`, `SPA-CARE-T-EXP-05`, `SPA-CARE-T-SEG-06`, `SPA-OPS-T-VTA-06`, `SPA-OPS-T-SRV-05` | — | Depende de `IEmployeeDirectory` (no implementado) |
| PT-03 | Permisos y no derivación desde Empleados o asignaciones | §21 | SPA-SG-01, SPA-INV-04, SPA-CARE-SEG-01, SPA-CARE-SEG-02, SPA-SEC-INV-07 | `SPA-T-04`, `SPA-T-05`, `SPA-T-12`, `SPA-CARE-T-EXP-06`, `SPA-CARE-T-SEG-01`, `SPA-CARE-T-SEG-02`, `SPA-CARE-T-SEG-03`, `SPA-CARE-T-SEG-05`, `SPA-CARE-T-SEG-07`, `SPA-SEC-T-02`, `SPA-OPS-T-AGE-07`, `SPA-OPS-T-AGE-08` | — | Catálogo de permisos no aprobado (§3) |
| PT-04 | Acceso del administrador y alcance por sucursal | §21 | SPA-SEC-R-01, SPA-SEC-R-08, SPA-CARE-SEG-04 | `SPA-CARE-T-SEG-09`, `SPA-SEC-T-01`, `SPA-SEC-T-15` | — | **Abierto** (C-01; O-01, O-02) |
| PT-05 | Auditoría y registro de accesos | §22 | SPA-AU-01, SPA-AU-02, SPA-SEC-AUD-01, SPA-SEC-INV-01 | `SPA-T-17`, `SPA-CARE-T-SEG-04`, `SPA-SEC-T-03`, `SPA-SEC-T-04`, `SPA-SEC-T-05` | `SPA-CARE-T-FOT-04` | Modo de escritura y retención pendientes (O-03) |
| PT-06 | Aislamiento por tenant y empresa | §21, §27 | SPA-SEC-INV-12, SPA-INV-10 | `SPA-T-16`, `SPA-CARE-T-EXP-07`, `SPA-CARE-T-SEG-08`, `SPA-OPS-T-AGE-09`, `SPA-SEC-T-14` | `SPA-CARE-T-FOT-07` | Sin decisión pendiente específica |
| PT-07 | Valoraciones facial y corporal | §14, §15 | SPA-FC-04, SPA-FC-05, SPA-CARE-FAC-01 a FAC-07, SPA-CARE-COR-01, COR-03, COR-04 | `SPA-T-06`, `SPA-T-07`, `SPA-CARE-T-FAC-01`, `SPA-CARE-T-COR-01` | `SPA-CARE-T-FAC-03`, `SPA-CARE-T-FAC-04`, `SPA-CARE-T-FAC-05`, `SPA-CARE-T-COR-03` | Contenido [F] sin contraste (V-01); obligatoriedad pendiente (O-21) |
| PT-08 | Mediciones, unidades, precisión y fechas | §15 | SPA-FC-06, SPA-CARE-COR-05, SPA-INV-10 | `SPA-T-08` | `SPA-CARE-T-COR-04`, `SPA-CARE-T-COR-05`, `SPA-CARE-T-COR-06` | Requiere revisión arquitectónica del gate decimal (V-04; O-22) |
| PT-09 | Sesiones y atención | §8, §12 | SPA-AT-02, SPA-AT-04, SPA-AT-06, SPA-AT-07, SPA-CARE-CNS-05, SPA-CARE-CNS-06 | `SPA-T-09`, `SPA-OPS-T-VTA-02` | `SPA-CARE-T-CNS-05`, `SPA-CARE-T-CNS-06` | Terminología **resuelta por `O-25`** (2026-11-18); políticas de saldo y ausencia abiertas (O-13) |
| PT-10 | Consentimientos y autorizaciones independientes | §17 | SPA-CN-01 a CN-06, SPA-INV-05, SPA-CARE-CNS-01 a CNS-08 | `SPA-T-10`, `SPA-T-11`, `SPA-CARE-T-CNS-01`, `SPA-CARE-T-FOT-01`, `SPA-SEC-T-12` | `SPA-CARE-T-CNS-02`, `SPA-CARE-T-CNS-03`, `SPA-CARE-T-CNS-04`, `SPA-CARE-T-CNS-07`, `SPA-CARE-T-FOT-02` | Firma, versionado, menores y uso académico pendientes (O-16 a O-18; V-06) |
| PT-11 | Fotografías y almacenamiento | §18 | SPA-FO-01, SPA-CARE-FOT-01 a FOT-11, SPA-SEC-INV-02, SPA-SEC-INV-03, SPA-SEC-INV-05 | `SPA-SEC-T-06`, `SPA-SEC-T-07`, `SPA-SEC-T-08`, `SPA-SEC-T-09` | `SPA-CARE-T-FOT-03`, `SPA-CARE-T-FOT-06` | Cifrado y claves pendientes (O-04) |
| PT-12 | Corrección, retención y eliminación | §22 | SPA-FC-08, SPA-INV-06, SPA-SEC-RET-01 a RET-06, SPA-SEC-INV-04 | `SPA-CARE-T-EXP-03`, `SPA-SEC-T-10`, `SPA-SEC-T-11` | `SPA-CARE-T-FAC-02`, `SPA-CARE-T-COR-02`, `SPA-CARE-T-FOT-05`, `SPA-CARE-T-FOT-08` | **Abierto** (C-02; O-05, O-07) |
| PT-13 | Expediente | §13 | SPA-CARE-EXP-02, SPA-CARE-EXP-07 | `SPA-CARE-T-EXP-02`, `SPA-CARE-T-EXP-04` | — | Número de ficha y cardinalidad pendientes (O-19, O-20) |
| PT-14 | Agenda, disponibilidad y concurrencia | §7, §8 | SPA-AG-01 a AG-03, SPA-OPS-AGE-01 a AGE-14, SPA-AR-06 | `SPA-T-13`, `SPA-T-18`, `SPA-OPS-T-AGE-01`, `SPA-OPS-T-AGE-02`, `SPA-OPS-T-AGE-03`, `SPA-OPS-T-AGE-04`, `SPA-OPS-T-AGE-05`, `SPA-OPS-T-AGE-06` | — | Recursos, tolerancias y política de ausencia pendientes (O-08 a O-10); depende de Empleados |
| PT-15 | Servicios y tratamientos | §9, §19 | SPA-SV-01, SPA-SV-02, SPA-OPS-SRV-01 a SRV-12, SPA-IN-01 a IN-03 | `SPA-OPS-T-SRV-01`, `SPA-OPS-T-SRV-02`, `SPA-OPS-T-SRV-03`, `SPA-OPS-T-SRV-04` | — | Relación servicio-procedimiento e insumos pendientes (O-11, O-12) |
| PT-16 | Paquetes y saldos de sesiones | §11 | SPA-PQ-01, SPA-OPS-PKG-01 a PKG-07, SPA-AT-05 | `SPA-T-14`, `SPA-OPS-T-PKG-01`, `SPA-OPS-T-PKG-02`, `SPA-OPS-T-PKG-03`, `SPA-OPS-T-PKG-04`, `SPA-OPS-T-PKG-05`, `SPA-OPS-T-PKG-06` | — | Las 12 preguntas contractuales pendientes (O-13) |
| PT-17 | Ventas, cobros y reutilización de módulos oficiales | §20 | SPA-AR-05, SPA-VT-01, SPA-INV-08, SPA-OPS-VTA-01 a VTA-06 | `SPA-T-15`, `SPA-OPS-T-SRV-06`, `SPA-OPS-T-VTA-01`, `SPA-OPS-T-VTA-03`, `SPA-OPS-T-VTA-05` | — | Anticipos pendientes (O-14); el origen del contrato fue **aprobado por `O-15`** (2026-11-18); estado SRI requerido, anulaciones y crédito pendientes (O-13, O-14) |

Cada grupo agrupa pruebas que verifican la misma capacidad desde distintos documentos; al aprobarse el contrato deben unificarse en una prueba por escenario. Los grupos `PT-04`, `PT-09`, `PT-12` y `PT-16`
quedan condicionados a decisiones abiertas. Los casos `SPA-AC-*` de §28.3 son adicionales a estas 102.

### 28.2 Cobertura de las decisiones aprobadas

Enunciados de `O-25` y `O-15` frente a las 102 pruebas anteriores y a los casos de aceptación nuevos (§28.3).

| Decisión | Enunciado | Pruebas anteriores (de las 102) | Caso nuevo | Cobertura anterior | Cobertura con el caso |
|---|---|---|---|---|---|
| `O-25` | #1, #2 reserva y cita | `SPA-OPS-T-AGE-01` (concurrencia) | `SPA-AC-25-01` | Parcial | Cubierto |
| `O-25` | #3 sesión realizada independiente | `SPA-T-09`, `SPA-CARE-T-CNS-06` | `SPA-AC-25-02` | Parcial | Cubierto |
| `O-25` | #4 sesión programada = cita vinculada | Ninguna | `SPA-AC-25-03` | **Sin prueba** | Cubierto |
| `O-25` | #5 reservar y reprogramar no consumen saldo | `SPA-OPS-T-AGE-05` (identidad y cambio, no el saldo) | `SPA-AC-25-04` | **Sin prueba del saldo** | Cubierto |
| `O-25` | #6 cancelar o ausentarse no crea sesión realizada | `SPA-OPS-T-AGE-06` (no elimina; autor y motivo) | `SPA-AC-25-05` | **Sin prueba de «no crea sesión»** | Cubierto |
| `O-25` | #7 consumo como movimiento independiente | `SPA-OPS-T-PKG-02`, `SPA-OPS-T-PKG-04` | `SPA-AC-25-06` | Parcial | Cubierto |
| `O-25` | #8 trazabilidad | `SPA-OPS-T-AGE-05`, `SPA-OPS-T-AGE-06`, `SPA-T-17` | `SPA-AC-25-07` | Parcial | Cubierto |
| `O-15` | #1 creación explícita tras validar factura autorizada | `SPA-OPS-T-VTA-04` | `SPA-AC-15-01` | Parcial | Cubierto (depende de la definición pendiente de «autorizada») |
| `O-15` | #2 referencia a factura y línea | `SPA-OPS-T-PKG-06`, `SPA-OPS-T-VTA-03` | `SPA-AC-15-02` | Parcial | Cubierto |
| `O-15` | #3 fuera de la transacción de Ventas y #5 el error de Spa no revierte la factura | Ninguna | `SPA-AC-15-03` | **Sin prueba** | Cubierto |
| `O-15` | #4 sin duplicados | `SPA-OPS-T-PKG-03` | `SPA-AC-15-04` (variante concurrente) | Cubierto en secuencia | Cubierto |
| `O-15` | #6 Spa no crea documentos comerciales | `SPA-OPS-T-VTA-01`, `SPA-OPS-T-VTA-02`, `SPA-T-15` | `SPA-AC-15-05` | Parcial | Cubierto |
| `O-15` | #7 sin creación automática por evento | Ninguna | `SPA-AC-15-06` | **Sin prueba** | Cubierto |

### 28.3 Casos de aceptación nuevos (`SPA-AC-*`)

**No forman parte de las 102 pruebas anteriores.** Son 13 casos con identificadores nuevos (prefijo `SPA-AC-`, sin relación con `SPA-T-*`, `SPA-OPS-T-*`, `SPA-CARE-T-*` ni `SPA-SEC-T-*`); no reutilizan ni renumeran
ninguno histórico. El **enunciado** que verifican está aprobado; el **caso** es una propuesta de verificación, no una aprobación. Los que dependen de la concurrencia o de una frontera transaccional exigen PostgreSQL real.

| ID | Enunciado | Escenario | Criterio de aceptación |
|---|---|---|---|
| `SPA-AC-25-01` | `O-25` #1, #2 | Reservar un intervalo para un cliente y un profesional | Se crea una cita `Programada`; no existe ni se crea ninguna entidad de reserva distinta de la cita |
| `SPA-AC-25-02` | `O-25` #3 | Una cita `Programada` llega a su fecha sin que se documente la atención | No existe sesión realizada; esta solo aparece cuando se documenta la atención, y la cita pasa a `Atendida` solo entonces |
| `SPA-AC-25-03` | `O-25` #4 | Programar una atención de un paquete | Es una cita con vínculo al contrato; no existe un registro de «sesión programada» distinto de la cita |
| `SPA-AC-25-04` | `O-25` #5 | Reservar y reprogramar una cita vinculada a un contrato | No se crea ningún movimiento de consumo y los contadores de consumidas y pendientes no cambian |
| `SPA-AC-25-05` | `O-25` #6 | Cancelar una cita y marcar otra como ausente | Ninguna de las dos genera una sesión realizada |
| `SPA-AC-25-06` | `O-25` #7 | Consumir saldo de un contrato | El consumo es un movimiento propio del libro del contrato, enlazado a la sesión o cita; no es un atributo de la sesión realizada |
| `SPA-AC-25-07` | `O-25` #8 | Reprogramar, cancelar, marcar ausencia y atender una cita | Cada hecho deja un registro con autor, instante y motivo (y valores anterior y nuevo en la reprogramación) |
| `SPA-AC-15-01` | `O-15` #1 | Crear un contrato con una factura autorizada y con una no autorizada | La primera se acepta mediante la operación explícita de Spa; la segunda se rechaza con `SPA_INVOICE_NOT_AUTHORIZED` |
| `SPA-AC-15-02` | `O-15` #2 | Crear un contrato desde una línea de factura | El contrato guarda el identificador de la factura y de la línea y no copia importes ni impuestos |
| `SPA-AC-15-03` | `O-15` #3, #5 | Forzar un fallo de Spa al crear el contrato (PostgreSQL real) | La factura sigue autorizada; la creación se ejecutó en una transacción distinta de la autorización de Ventas |
| `SPA-AC-15-04` | `O-15` #4 | Dos creaciones simultáneas para la misma línea facturada (PostgreSQL real) | Existe un solo contrato; la otra recibe `SPA_INVOICE_LINE_ALREADY_LINKED` |
| `SPA-AC-15-05` | `O-15` #6 | Crear un contrato | No se crean facturas, CxC, cobros, movimientos de Caja ni asientos contables |
| `SPA-AC-15-06` | `O-15` #7 | Autorizar una factura con una línea de paquete | No se crea ningún contrato ni se ejecuta código de Spa en la autorización |

## 29. Dependencias bloqueantes

### 29.1 Bloqueos de la especificación definitiva

| ID | Bloqueo | Naturaleza | Afecta | Resolución |
|---|---|---|---|---|
| BL-01 | **Empleados no implementado** y `IEmployeeDirectory` propuesta (`D-01`, `V-02`) | Dependencia externa | §6 a §9, §12, §14, §15 | EMP-E1 a E6b, cada una con autorización expresa |
| BL-02 | **`C-01` / `O-01`: administrador y datos clínicos.** El bypass del Admin pertenece a un contrato LOCKED vigente (nivel 2); la restricción de acceso clínico es un requisito propuesto (nivel 4) y su mecanismo B exige revisión arquitectónica. No hay dos decisiones aprobadas en contradicción, pero **no se puede cerrar la política sin decisión del propietario** | Decisión + posible revisión arquitectónica | §18, §21, §22 | `O-01`; revisión solo si se elige B |
| BL-03 | **`C-02` / `O-05`: no borrado frente a eliminación legal.** La regla permanente del propietario (no borrado físico) y el derecho de eliminación (LOPDP Arts. 14, 15, 18; `L-NV` en su aplicación a Spa) **pueden contradecirse realmente**; no se elige una en silencio | Conflicto real | §13, §17, §18, §22 | `O-05` + asesoría legal (`O-07`) |
| BL-04 | **`O-14`: anticipos y prepagos.** Define la estructura comercial y el origen del contrato; el proceso es independiente (`DIR-SSOT-01`) | Decisión + proyecto separado | §11, §20 | `O-14`; sin desarrollo en Spa |
| BL-05 | **`V-01` / `O-23` / `O-24`:** fichas originales sin contraste; texto de `SPA-DEC-116` sin recuperar | Evidencia externa | §2, §10, §12 a §17 | Acceso del propietario a las fichas y al historial |
| BL-06 | **`V-06` / `O-07`:** validación jurídica de LOPDP, firma, menores, imágenes y plazos; `V-09` vigencia | Asesoría legal | §17, §18, §22 | Profesional legal |
| BL-07 | **`V-04` / `O-22`:** clase de precisión de las medidas corporales | Revisión arquitectónica | §15 | Decisión arquitectónica formal |
| BL-08 | **`O-13`:** diez preguntas de paquetes sin responder (la de «Venta» y la de «Reprogramación» están resueltas) | Decisión | §11, §12 | `O-13` |
| BL-09 | Falta de **ADR** y **plan de fases** de Spa, y de aprobación de este contrato | Proceso | Todo | Después de resolver los bloqueos |

### 29.2 Necesarias pero no bloqueantes de la redacción

Aprobar glosario y catálogo de permisos o sustituirlos; políticas de agenda (`O-08` a `O-12`); firma, versionado, menores y uso académico (`O-16` a `O-18`); cifrado y claves (`O-04`); registro de lecturas
(`O-03`); acceso entre sucursales (`O-02`); fecha de nacimiento y contacto de emergencia (`O-06`); cifrado de disco y publicación del directorio de archivos (`V-07`); resolutor de `StorageObjectId` (`V-08`);
indexar los documentos de Spa en `docs/decisions/README.md` (requiere autorización documental).

### 29.3 Matriz de implementabilidad

| Funcionalidad | Estado | ¿Implementable? | Bloqueos |
|---|---|---|---|
| Agenda, citas y reservas (§7, §8) | Modelo cita/sesión APROBADO; reglas PROPUESTAS | **No** | BL-01, BL-09; `O-08` a `O-10` |
| Paquetes: creación del contrato (§11) | APROBADA en su origen | **No** | BL-08 (políticas), BL-09; `O-15` pendientes de §11.1 |
| Paquetes: saldo y consumo (§11, §12) | PENDIENTE | **No** | BL-08 |
| Sesiones realizadas (§12) | Modelo APROBADO; contenido [F] | **No** | BL-01, BL-05, BL-09 |
| Servicios (§9) | PROPUESTO | **No** | `O-11`; BL-01 (especialidades) |
| Expediente y valoraciones (§13 a §16) | PROPUESTO [F] | **No** | BL-05, BL-07, BL-03 |
| Consentimientos y firmas (§17) | PROPUESTO [F] | **No** | BL-05, BL-06 |
| Fotografías y archivos (§18) | PROPUESTO | **No** | BL-02, BL-03, BL-06; `O-03`, `O-04` |
| Integración con Ventas, Caja y CxC (§20) | Frontera APROBADA | **No** (solo referencia) | BL-04, BL-09 |
| Seguridad, auditoría y retención (§21, §22) | PROPUESTO / BLOQUEADO | **No** | BL-02, BL-03, BL-06 |

## 30. Decisiones todavía pendientes

### 30.1 Aprobadas e instrucciones del propietario (no pendientes)

| ID | Decisión | Fecha | Evidencia |
|---|---|---|---|
| `O-25` | Alternativa A: cita separada de la sesión realizada | 2026-11-18 | [REG §1](GALI-SPA-V1.1-APPROVED-DECISIONS-01.md) |
| `O-15` | Alternativa A: contrato de paquete creado explícitamente tras validar una factura autorizada | 2026-11-18 | [REG §2](GALI-SPA-V1.1-APPROVED-DECISIONS-01.md) |
| `DIR-SSOT-01` | **Instrucción del propietario (no aprueba el diseño de `O-14`):** Anticipos de Clientes es un proceso independiente y transversal, no una entidad financiera de Spa (propiedad; no cierra `O-14`) | 2026-11-18 | Encargo SPA-V1.1-SSOT-CONSOLIDATION; **pendiente de registrarse** en el registro de decisiones |
| ADR-050 | `DEC-E1`, `DEC-E4`, §6, §8, §13 (aplicables a Spa) | Proposed (la arquitectura funcional fue aprobada por el propietario) | [ADR-050](ADR-050-employees-v1-architecture.md) |

### 30.2 Pendientes (23 `O-xx`)

| O | Cuestión | Secciones | Tipo de dependencia | Bloqueo (§29) |
|---|---|---|---|---|
| O-01 | Acceso del Admin a datos clínicos y mecanismo | §21 | Propietario; revisión arquitectónica si B | BL-02 |
| O-02 | Acceso entre sucursales | §13, §21 | Propietario | — |
| O-03 | Modo de registro de lecturas y retención del registro | §22 | Propietario; legal; implementación | — |
| O-04 | Cifrado, claves y respaldos cifrados | §18 | Propietario; implementación y despliegue | — |
| O-05 | Excepción a la regla de no borrado y eliminación legal | §22 | Propietario; legal | BL-03 |
| O-06 | Fecha de nacimiento, edad y contacto de emergencia | §5, §13 | Propietario; legal (terceros) | — |
| O-07 | Asesoría legal L-INT-01 a L-INT-07 | §22 | Legal | BL-06 |
| O-08 | Recursos bloqueantes de la agenda | §7 | Propietario | — |
| O-09 | Contigüidad, tolerancias, anticipación y confirmaciones | §7, §8 | Propietario | — |
| O-10 | Atención sin cita previa | §8 | Propietario | — |
| O-11 | Servicio y procedimiento (1:1 o N:M); varios servicios por cita | §8, §9 | Propietario | — |
| O-12 | Tratamiento de insumos | §19 | Propietario; inventario | — |
| O-13 | Las diez preguntas restantes de paquetes (la de «Venta» y la de «Reprogramación» están resueltas) | §11, §12 | Propietario; posible legal (consumidores) | BL-08 |
| O-14 | Anticipos y prepagos de clientes — **ABIERTA; proyecto separado** | §20 | Propietario; otro módulo; revisión arquitectónica; criterio contable y fiscal | BL-04 |
| O-16 | Firma, versionado y conservación de consentimientos | §17 | Propietario; legal | BL-06 |
| O-17 | Uso académico | §17 | Propietario; ficha | BL-05 |
| O-18 | Consentimiento de menores y representantes | §17 | Propietario; legal | BL-06 |
| O-19 | Número de ficha y código del caso | §13 | Propietario; fichas | BL-05 |
| O-20 | Cardinalidad del expediente por cliente | §13 | Propietario | — |
| O-21 | Obligatoriedad de los campos de valoración | §13, §14 | Propietario; fichas | BL-05 |
| O-22 | Escalas, frecuencia y controles de las mediciones | §15 | Propietario; revisión arquitectónica (`V-04`) | BL-07 |
| O-23 | Texto íntegro de `SPA-DEC-116` y confirmación del alcance de V1.1 (**abierta; este contrato no la cierra**, §2.2) | §2 | Propietario; historial | BL-05 |
| O-24 | Versión vigente de cada formulario original | §14 | Propietario; fichas | BL-05 |

Son 23 cuestiones: `O-01` a `O-14` y `O-16` a `O-24`; `O-15` y `O-25` están aprobadas (§30.1).

### 30.3 Otras decisiones o evidencias

- **Decisión comercial derivada de `O-14`:** si Gali Spa necesita recibir dinero antes de emitir la factura o puede facturar siempre al cobrar; si vende paquetes a crédito o con pagos parciales y si se usan
  sesiones con saldo pendiente; qué ocurre con las sesiones no usadas (devolver dinero, dejar crédito o nada); si cobra por cancelar o no asistir (ligado a `O-13`). Son insumos del informe de sesión
  SPA-V1.1-DECISION-REVIEW-02 (no versionado). Hasta decidirlo, ver §20.4.
- **Pendientes de `O-15` (§2.5 del registro):** estado SRI requerido, anulaciones y notas de crédito, facturas a crédito, lista de facturas pendientes de generar contrato, definición de «factura autorizada».
- **Evidencia externa:** fichas originales (`V-01`), texto de `SPA-DEC-116`, validación jurídica (`V-06`), vigencia de la normativa (`V-09`), Empleados implementado (`V-02`).
- **Aprobaciones del propio contrato:** aprobación de este documento, del glosario y catálogo de permisos, redacción del ADR y del plan de fases de Spa.

## Anexo A. Responsabilidad de cada documento

| Documento | Responsabilidad | Estado |
|---|---|---|
| **Contrato central** (este documento) | Comportamiento funcional y técnico de Spa V1.1: qué se desarrollará, cómo funciona, qué está aprobado, qué no y qué queda excluido | Candidato a SSOT funcional; EN REVISIÓN |
| **ADR de Spa V1.1** | Decisiones arquitectónicas aprobadas y su justificación | **No existe**; se redactará (sin número asignado) después de resolver los bloqueos de §29 |
| **Plan de implementación de Spa** | Fases, dependencias y autorizaciones | **No existe**; se redactará con el ADR |
| **Registro de decisiones** ([REG](GALI-SPA-V1.1-APPROVED-DECISIONS-01.md)) | Evidencia de las aprobaciones del propietario | Vigente; las aprobaciones nuevas (incluida `DIR-SSOT-01`) se registran allí o en su sucesor |
| R1 [RECOVERY-01](GALI-SPA-V1.1-RECOVERY-01.md) | Respaldo histórico de la reconstrucción y de los códigos `GS-*`/V01; registro de `SPA-DEC-116` | Referencia **no normativa** |
| R2 [OPERATIONS-SPEC](GALI-SPA-V1.1-OPERATIONS-SPEC-DRAFT-01.md), R3 [CARE-PRIVACY](GALI-SPA-V1.1-CARE-PRIVACY-SPEC-DRAFT-01.md), R4 [SECURITY-COMPLIANCE](GALI-SPA-V1.1-SECURITY-COMPLIANCE-DRAFT-01.md) | Análisis, alternativas y evidencia técnica y legal que respaldan este contrato | Referencia **no normativa** (el texto funcional vive aquí; R4 §7 conserva el detalle legal) |
| [MATRIZ](GALI-SPA-V1.1-CONTRACT-CONSOLIDATION-MATRIX-01.md) | Trazabilidad de los 295 registros y conflictos `C-xx` | Referencia **no normativa** |
| [NORM](GALI-SPA-V1.1-CONTRACT-NORMALIZATION-01.md) | Redacciones canónicas, glosario, catálogo de permisos, verificaciones `V-xx` cerradas | Referencia **no normativa**; sus contenidos están integrados aquí |

**Efecto práctico:** desde su creación este contrato es el único texto con candidatura a normativo; los seis documentos de referencia no se eliminan, renombran ni archivan, y en una operación documental
posterior autorizada deberá marcarse su carácter de referencia y añadirse el contrato (y, solo entonces, los demás) al índice `docs/decisions/README.md` (propuesta de entrada en NORM §8). Hasta entonces,
sus cabeceras no mencionan este contrato.

## Anexo B. Diferencias documentales resueltas por la precedencia

| ID | Diferencia | Resolución en este contrato | Nivel |
|---|---|---|---|
| B-01 | R2 §4.3 define `Session` como «realizada o programada» y `Appointment` aparte; R1 `SPA-AT-06` distingue prevista y realizada (`C-06`) | `Session` es solo sesión realizada; la sesión programada es una cita vinculada al contrato | 1 (`O-25`) |
| B-02 | R2 §4.5 ofrece como alternativa que un evento de la factura cree el contrato | Descartada; creación explícita tras validar una factura autorizada | 1 (`O-15` #7) |
| B-03 | R2 §4.5 «Reprogramación» ofrecía «afecta según anticipación» | Reprogramar no consume saldo | 1 (`O-25` #5) |
| B-04 | R2 `SPA-OPS-VTA-03` planteaba dos caminos para cobrar por adelantado, uno dentro del módulo de Cobros | Anticipos de Clientes es un proceso transversal independiente; ni Spa ni este contrato lo diseñan; `O-14` abierta | 1 (`DIR-SSOT-01`) |
| B-05 | R3 `PhotoAccessLog` y R4 `CareAccessLog` nombran la misma necesidad | Una sola entidad candidata, `CareAccessLog` (R4 la generaliza: recurso, operación, resultado) | 4 (unificación de propuestas) |
| B-06 | R2, R3 y R4 usaban 30 nombres de permiso con seis repetidos | Catálogo único de 30 nombres (§21.2); sin renombrar | 4 (NORM §3) |
| B-07 | R1 «plan de atención» y la estructura del encargo «plan de tratamiento» | «Plan de tratamiento»; «plan de atención» es sinónimo histórico | 4 (`G-07`) |
| B-08 | R2 §4.4 define «Realizadas» como «sesiones con estado realizado» | Realizadas = sesiones realizadas registradas; no hay estado «programada» en la sesión | 1 (`O-25` #3, #4) |
| B-09 | R3 `SPA-CARE-CNS-05` (sesión interrumpida «no realizada por completo») frente a `O-25` #3 | No se resuelve: si una interrupción genera sesión parcial queda PENDIENTE | — |
| B-10 | R3 `SPA-CARE-EXP-08` («nunca se elimina») frente a R4 `SPA-SEC-RET-01` (procedimiento de eliminación) | No se resuelve en silencio: BL-03 | 1 vs legal |
| B-11 | Los borradores dicen «ninguna regla está aprobada» aunque `O-25` y `O-15` lo estén después | Las reglas afectadas se marcan con su aprobación (§8, §11, §12, §20); los borradores no se modifican | 1 |
| B-12 | R1 §16 afirma que las exclusiones de V1.1 no están recuperadas | Se distingue excluido / no autorizado / pendiente (§3); no se declaran exclusiones sin respaldo | — |
| B-13 | La primera versión clasificaba como «excluidas» la creación automática del contrato y la sesión única desde la reserva | Reclasificadas como no autorizadas (N-10, N-11): `O-15` y `O-25` dicen «no se aprueba», no «excluido» | 1 |
| B-14 | La primera versión hablaba de «once» preguntas de paquetes pendientes | Son diez: dos de las doce están resueltas por `O-15` y `O-25` | 1 |

## Anexo C. Mapa de origen por identificador

Sección principal del contrato donde se consolida cada regla, invariante, riesgo y decisión de los documentos previos. Las pruebas figuran en §28.1 y los casos de aceptación nuevos en §28.3.

| Sección | Identificadores consolidados |
|---|---|
| §1, §2 | `SPA-AL-01` |
| §1, §6 | `SPA-AL-02` |
| §2, §30 | `SPA-DEC-116` |
| §4, §15 | `SPA-INV-10` |
| §5 | `SPA-AR-01`, `SPA-CARE-EXP-01`, `SPA-INV-01`, `SPA-SEC-INV-11`, `SPA-CARE-EXP-09`, `SPA-SEC-R-07` |
| §5, §16 | `SPA-CL-01` |
| §6 | `SPA-AR-02`, `SPA-AR-03`, `SPA-AR-04`, `SPA-AR-06`, `SPA-CARE-EXP-05`, `SPA-CARE-SEG-06`, `SPA-INV-02`, `SPA-INV-03`, `SPA-SEC-INV-08`, `SPA-SEC-R-09` |
| §6, §9 | `SPA-OPS-SRV-06` |
| §6, §20 | `SPA-OPS-VTA-05` |
| §7 | `SPA-OPS-AGE-04`, `SPA-OPS-AGE-14`, `SPA-AG-01`, `SPA-AG-02`, `SPA-AG-03`, `SPA-OPS-AGE-03`, `SPA-OPS-AGE-05`, `SPA-OPS-AGE-06` |
| §8 | `SPA-OPS-AGE-13`, `SPA-OPS-AGE-01`, `SPA-OPS-AGE-02`, `SPA-OPS-AGE-07`, `SPA-OPS-AGE-08`, `SPA-OPS-AGE-09`, `SPA-OPS-AGE-11`, `SPA-OPS-AGE-10`, `SPA-OPS-AGE-12` |
| §8, §12 | `SPA-INV-09` |
| §9 | `SPA-OPS-SRV-01`, `SPA-OPS-SRV-03`, `SPA-OPS-SRV-04`, `SPA-OPS-SRV-05`, `SPA-OPS-SRV-09`, `SPA-OPS-SRV-11`, `SPA-SV-01`, `SPA-SV-02`, `SPA-OPS-SRV-02`, `SPA-OPS-SRV-10`, `SPA-OPS-SRV-12` |
| §10 | `SPA-CARE-COR-08`, `SPA-CARE-FAC-05`, `SPA-AT-01`, `SPA-AT-05` |
| §10, §16 | `SPA-AT-04` |
| §11 | `SPA-OPS-PKG-05`, `SPA-OPS-PKG-06`, `SPA-PQ-01`, `SPA-OPS-PKG-02`, `SPA-OPS-PKG-03`, `SPA-OPS-PKG-04`, `SPA-OPS-PKG-07` |
| §11, §12 | `SPA-OPS-PKG-01` |
| §11, §20 | `SPA-OPS-VTA-02` |
| §12 | `SPA-CARE-CNS-05`, `SPA-CARE-CNS-06`, `SPA-AT-06`, `SPA-AT-07` |
| §12, §16 | `SPA-AT-02` |
| §12, §20 | `SPA-OPS-VTA-04` |
| §13 | `SPA-FC-07`, `SPA-CARE-EXP-02`, `SPA-CARE-EXP-03`, `SPA-CARE-EXP-04`, `SPA-CARE-EXP-06`, `SPA-CARE-EXP-07`, `SPA-CARE-EXP-08`, `SPA-FC-01`, `SPA-FC-02`, `SPA-FC-03`, `SPA-FC-08`, `SPA-CARE-EXP-10` |
| §14 | `SPA-CARE-FAC-01`, `SPA-CARE-FAC-02`, `SPA-CARE-FAC-03`, `SPA-CARE-FAC-04`, `SPA-CARE-FAC-06`, `SPA-CARE-FAC-07`, `SPA-FC-04` |
| §15 | `SPA-CARE-COR-01`, `SPA-CARE-COR-02`, `SPA-CARE-COR-03`, `SPA-CARE-COR-04`, `SPA-CARE-COR-05`, `SPA-CARE-COR-06`, `SPA-CARE-COR-07`, `SPA-FC-05`, `SPA-FC-06` |
| §16, §18 | `SPA-AT-03` |
| §17 | `SPA-CARE-CNS-01`, `SPA-CARE-CNS-02`, `SPA-CARE-CNS-03`, `SPA-CARE-CNS-04`, `SPA-CARE-CNS-07`, `SPA-CARE-CNS-08`, `SPA-CN-01`, `SPA-CN-02`, `SPA-CN-03`, `SPA-CN-04`, `SPA-CN-05`, `SPA-CN-06`, `SPA-INV-05`, `SPA-SEC-INV-10` |
| §18 | `SPA-CARE-FOT-01`, `SPA-CARE-FOT-02`, `SPA-CARE-FOT-03`, `SPA-CARE-FOT-04`, `SPA-CARE-FOT-05`, `SPA-CARE-FOT-07`, `SPA-CARE-FOT-08`, `SPA-CARE-FOT-09`, `SPA-CARE-FOT-10`, `SPA-CARE-FOT-11`, `SPA-FO-01`, `SPA-SEC-INV-02`, `SPA-SEC-INV-03`, `SPA-SEC-INV-05`, `SPA-SEC-R-03`, `SPA-SEC-R-04` |
| §18, §22 | `SPA-CARE-FOT-06`, `SPA-SEC-INV-04` |
| §19 | `SPA-IN-03`, `SPA-OPS-SRV-08`, `SPA-IN-01`, `SPA-IN-02`, `SPA-OPS-SRV-07` |
| §20 | `SPA-AR-05`, `SPA-INV-08`, `SPA-OPS-VTA-01`, `SPA-OPS-VTA-06`, `SPA-VT-01`, `SPA-OPS-VTA-03` |
| §21 | `SPA-SG-02`, `SPA-CARE-SEG-01`, `SPA-CARE-SEG-02`, `SPA-CARE-SEG-03`, `SPA-CARE-SEG-04`, `SPA-CARE-SEG-05`, `SPA-INV-04`, `SPA-INV-07`, `SPA-SEC-INV-07`, `SPA-SEC-INV-09`, `SPA-SEC-INV-12`, `SPA-SG-01`, `SPA-SG-03`, `SPA-SG-04`, `SPA-SG-05`, `SPA-SG-06`, `SPA-SEC-R-08` |
| §21, §29 | `SPA-SEC-R-01` |
| §22 | `SPA-SEC-LEG-01`, `SPA-SEC-LEG-02`, `SPA-SEC-LEG-03`, `SPA-SEC-LEG-04`, `SPA-SEC-LEG-05`, `SPA-SEC-LEG-06`, `SPA-SEC-LEG-07`, `SPA-SEC-RET-01`, `SPA-SEC-RET-02`, `SPA-SEC-RET-03`, `SPA-SEC-RET-04`, `SPA-SEC-RET-05`, `SPA-SEC-RET-06`, `SPA-AU-01`, `SPA-AU-02`, `SPA-INV-06`, `SPA-SEC-AUD-01`, `SPA-SEC-AUD-02`, `SPA-SEC-AUD-03`, `SPA-SEC-AUD-04`, `SPA-SEC-AUD-05`, `SPA-SEC-AUD-06`, `SPA-SEC-AUD-07`, `SPA-SEC-INV-01`, `SPA-SEC-INV-06`, `SPA-SEC-R-02`, `SPA-SEC-R-06`, `SPA-SEC-R-10` |
| §22, §29 | `SPA-SEC-R-05` |
| §23 | `SPA-UX-01` |
| §25 | `SPA-API-01` |

## Anexo D. Límites

Este contrato no modifica los documentos fuente, ADR-050, la especificación de Empleados, `STATUS.md`, `WORK_QUEUE.md` ni los índices, y no autoriza implementación de Spa ni de Empleados, migraciones, cambios en
módulos FROZEN o CLOSED, ni cambios en Identity, Access, Kernel, Items, Ventas, Cuentas por Cobrar, Contabilidad, almacenamiento o scripts de operación. De `SPA-DEC-116` solo está confirmado el título. Las
fichas originales de Gali Spa siguen sin inspección directa por Claude. No hay asesoría jurídica ni certificación de cumplimiento.

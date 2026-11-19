# SPA V1.1 — Módulo de Gestión de Spa y Tratamientos Estéticos de ERP ZH: contrato funcional (SPA-V1.1-CONTRACT-SPEC-01)

**ALCANCE FUNCIONAL APROBADO (SPA-DEC-115/116) — CONSOLIDACIÓN TÉCNICA EN REVISIÓN — SIN IMPLEMENTACIÓN AUTORIZADA.**

Candidato a fuente única de verdad **funcional** de SPA V1.1 (2026-11-18, revisión 3 — integración de SPA-DEC-115/116 recuperadas). Responde a cuatro preguntas por funcionalidad: **qué se desarrollará, cómo debe funcionar, qué está
aprobado y qué no lo está, y qué queda excluido.** Consolida, sin añadir decisiones de negocio, los siete documentos previos de Spa (§0.7). **No es un ADR, no es una SSOT vigente** (la SSOT de un
módulo nace cuando existen código y pruebas: [enforcement](../architecture/enforcement.md#ssot-por-módulo)) **y no autoriza implementar SPA ni Empleados**, migraciones, endpoints, pantallas,
permisos ni cambios en módulos FROZEN o CLOSED. Estado de entrega: [STATUS](../../STATUS.md); cola: [WORK_QUEUE](../agent/WORK_QUEUE.md); índice oficial: [decisiones](README.md).

Patrón documental: el mismo de Orden de Pedido de Venta V1 ([ADR-049](ADR-049-sales-order-v1-architecture.md), [especificación funcional](PEDIDOS-VENTA-V1-FUNCTIONAL-SPEC-01.md)) y de Empleados V1
([ADR-050](ADR-050-employees-v1-architecture.md), [especificación contractual](EMPLOYEES-V1-CONTRACT-SPEC-01.md)): una especificación central con etiquetas de estado, jerarquía de autoridad
explícita, decisiones del propietario separadas de las propuestas técnicas y [ADR-051](ADR-051-spa-v1.1-architecture.md) ACCEPTED / APROBADO y [plan](SPA-V1.1-IMPLEMENTATION-PLAN-01.md) preparado sin autorización de ejecución; su aprobación arquitectónica (2026-11-19, REG §25) no autoriza fases y sigue condicionada a §29.

## 0. Cómo leer este contrato

### 0.1 Precedencia normativa

Ante una diferencia entre documentos se aplica este orden. Una diferencia puramente documental se corrige aquí; **dos decisiones aprobadas que realmente se contradigan no se resuelven en silencio:
se registran como bloqueo (§29).**

| Nivel | Fuente | Alcance |
|---|---|---|
| 1 | Decisiones expresamente aprobadas por el propietario | Solo dentro de su alcance literal: `SPA-DEC-115`, `SPA-DEC-116` (evidencia histórica aportada por el propietario, REG §9/10), `SPA-ARQ-01` (modelo conceptual aprobado, REG §12), `SPA-ARQ-02` (estados/eventos y situación derivada aprobados, REG §13), `SPA-ARQ-03` (ciclo clínico aprobado, REG §14 y §12 de este contrato), `SPA-ARQ-04` (agenda/disponibilidad funcional, REG §15 y §7), `SPA-ARQ-05` (tratamientos/paquetes, REG §16 y §11), `SPA-ARQ-06` (expedientes/seguridad, REG §17 y §21), `SPA-ARQ-07` (valoraciones/consentimientos/fotografías, REG §18 y §14–18/21), `SPA-ARQ-08` (fronteras ERP, REG §19 y §4), `O-25`, `O-15` ([registro](SPA-V1.1-APPROVED-DECISIONS-01.md)), las decisiones de Empleados de [ADR-050](ADR-050-employees-v1-architecture.md) que atañen a Spa y la **instrucción** del propietario `DIR-SSOT-01` (§20.4), que fija una frontera y no aprueba un diseño |
| 2 | ADR y contratos vigentes del ERP | Respetando sus límites: [ADR-017](ADR-017-business-partner-scope.md) (BusinessPartner, FROZEN), [ADR-034](ADR-034-temporal-contract-single-source.md), [ADR-035](ADR-035-supplier-payment-unapplied-advance.md), [ADR-041](ADR-041-negative-sale-inventory-cost-obligations.md), «Security & Access Contract V1 — LOCKED» ([security](../architecture/security.md)), [auditoría](../architecture/audit-infrastructure.md), [datos](../architecture/data-standards.md), Ventas FROZEN |
| 3 | Evidencia técnica verificada del repositorio | Las verificaciones `V-xx` cerradas (normalización §5) y las capacidades [E] citadas en cada sección |
| 4 | Planificación histórica recuperada y propuestas pendientes, distinguidas por evidencia | [F]/[R] conserva lo planificado sin acta íntegra o contraste directo; [T] identifica traducciones técnicas nuevas. No se atribuye aprobación a ninguna de estas categorías ni se descarta la planificación por falta de acta |

### 0.2 Estados de aprobación (columna «Estado»)

| Estado | Significado |
|---|---|
| **APROBADO** | Decisión expresa del propietario, dentro del alcance indicado en su registro. Incluye lo aprobado literalmente en ADR-050 para Spa |
| **APROBADO-DERIVADO** | Consecuencia razonable de una decisión aprobada que el texto literal no formula para Spa; cita la sección de origen. No es una aprobación nueva |
| **INSTRUCCIÓN DEL PROPIETARIO** | Directriz expresa del propietario con alcance limitado (`DIR-SSOT-01`). Fija una frontera; **no es una aprobación de diseño** ni cierra ninguna cuestión `O-xx` |
| **VIGENTE-ERP** | Capacidad o restricción ya vigente y verificada del ERP que Spa debe respetar. No es decisión de Spa |
| **PLANIFICACIÓN HISTÓRICA RECUPERADA** | Función o dato ya recogido en la planificación [R] o los formularios transmitidos [F]. Se conserva; falta de acta literal no es rechazo ni exclusión. Contraste y aprobación específica se indican aparte |
| **PROPUESTO** | Traducción a software o solución técnica [T], no aprobada. **No normativo** |
| **PENDIENTE** | Requiere una decisión del propietario (`O-xx`), una verificación (`V-xx`) o recuperar evidencia. No se elige ninguna alternativa |
| **BLOQUEADO** | Conflicto real entre decisiones o dependencia externa que impide cerrar la regla (§29) |
| **EXCLUIDO** / **NO AUTORIZADO** | §3 |

Etiquetas de evidencia de las fuentes (columna «Evid.»): **A** aprobada literal, **A-der** derivada, **E** existente en el ERP, **F** formularios originales (Claude no los ha inspeccionado), **R**
reconstruida, **T** propuesta técnica, **P** pendiente, **NV** no verificado; **L-EXPRESO**, **L-INTERPRETACIÓN**, **L-RECOMENDACIÓN**, **L-NV** para el contenido legal.

### 0.3 Reglas de lectura

1. **La jerarquía global es la de [CLAUDE.md](../../CLAUDE.md); §0.1 ordena las evidencias de esta planificación bajo ella. Las reglas conservan su identificador histórico** (`SPA-AR-*`, `SPA-OPS-*`, `SPA-CARE-*`, `SPA-SEC-*`, etc.); no se renumera ni se reutiliza ninguno. Este contrato los reagrupa; el texto aquí prevalece sobre la fuente (§0.1).
2. Una alternativa no es una decisión. Donde hay una laguna se listan las alternativas y **no se elige ninguna**.
3. Un campo de un formulario físico **no es obligatorio en el software por sí mismo**; la obligatoriedad real es una decisión pendiente (`O-21`). Los únicos mínimos de integridad proponibles son cliente, fecha,
   profesional responsable, usuario registrador y sucursal [T].
4. Una ausencia de respuesta no equivale a una respuesta negativa en ningún antecedente [T].
5. Un nombre de entidad, permiso, error o endpoint de este contrato es **candidato** salvo indicación expresa; ninguno existe en el código.
6. SPA-DEC-115/116 están recuperadas y APROBADAS según el contenido aportado por el propietario en este encargo. No se fabrican SPA-DEC-001..114 ni se declaran recuperados sus textos. O-xx identifica cuestiones de consolidación, no decisiones históricas SPA-DEC.
7. «Sesión realizada» documenta atención efectiva (O-25); programación es cita (§8). SPA-DEC-116 también aprueba borradores de sesión técnica (§12): un borrador no certifica atención realizada. Su representación y las interrupciones se delimitan allí sin confundirlas con citas o consumo.
8. Términos que no se mezclan: «**contrato de paquete**» (`PackageContract`, registro de Spa) frente a «contrato» como especificación o interfaz (este documento, `IEmployeeDirectory`, el contrato LOCKED);
   «**sesión de caja**» (`CashSession`, concepto de Caja) frente a la sesión realizada de Spa; «**reserva**» es la operación que crea una cita y no una reserva de stock de Inventario.
9. Los contenidos [F]/[R] son PLANIFICACIÓN HISTÓRICA RECUPERADA, incluso donde la revisión anterior los rotulaba PROPUESTOS; conservan procedencia. Las traducciones [T] (modelos, flujos técnicos, estados y alternativas) son PROPUESTAS o PENDIENTES salvo indicación expresa. Las aprobaciones históricas SPA-DEC-115/116 constan en REG §9/10 y se aplican por su alcance; O-25 (§8.1) y O-15 (§11.1) se conservan literales. Se distingue aprobación funcional de mecanismo técnico propuesto.
10. Una propuesta nunca es una obligación implementable: el verbo «debe» describe el comportamiento que tendría la regla si se aprueba, salvo en los enunciados APROBADOS.

### 0.4 Estado global de implementabilidad

**Ninguna funcionalidad es implementable hoy.** Causas comunes: (1) el alcance funcional está aprobado, ADR-051 está ACCEPTED / APROBADO (2026-11-19) y SPA-SEL-01..12 aprobadas, pero el plan no autoriza fases ni se han cumplido COND-01..13; (2) Empleados no está implementado (`IEmployeeDirectory` es propuesta;
EMP-E1 PENDING / NO AUTORIZADA); (3) decisiones bloqueantes abiertas (§29); (4) fichas originales sin contraste (`V-01`); (5) validación jurídica pendiente (`V-06`).

### 0.5 Mapa de estado por área

| Área | Qué está aprobado | Detalle que sigue abierto | Implementable hoy |
|---|---|---|---|
| Disponibilidad, agenda y visitas (§7/8) | Búsqueda servicio/fecha/sucursal sin profesional previo; agenda única día/semana; restricciones; varios servicios/profesionales, llegada/cambios (SPA-DEC-115/116); O-25 íntegra | Cálculo de restricciones, esquema/vínculos técnicos del modelo SPA-ARQ-01, presentación de situación derivada SPA-ARQ-02 y locks, atención sin cita; no disponibilidad de cabinas/equipos | No |
| Expediente y valoración (§13/15) | Único por cliente/empresa; antecedentes, alertas, valoraciones, medidas/historial (SPA-DEC-116) | Fichas O-24/V-01, mínimos/errores, numeración, datos personales y precisión | No |
| Tratamientos y sesiones (§10/12/16) | Individual/multisesión, ampliación/suspensión/cancelación/cierre; borrador/finalización/interrupción/corrección, resultados/incidencias/evolución | Ciclo clínico SPA-ARQ-03 aprobado (§12); mecanismos técnicos, permisos concretos, cardinalidad clínica y efectos comerciales O-13 pendientes | No |
| Paquetes (§11) | Individuales/combinados, vigencia/cupos/ampliaciones/sustituciones; origen O-15 y movimiento separado O-25 | Reglas concretas de consumo, equivalencias, vencimiento, ajustes, NC/crédito y concurrencia | No |
| Consentimientos y fotos (§17/18) | Por procedimiento, firma en pantalla, versiones/finalidades específicas, fotos privadas y trazabilidad | Mecanismo/evidencia de firma, menores/académico, retiro y conservación legal; cifrado/clave/lecturas | No |
| Integración ERP (§5/6/9/19/20) | Reutilización BP/Employee y módulos oficiales de servicios/productos, facturas, Caja/CxC/cobros; sin motores paralelos | Directorio real, contratos públicos y política financiera; O-14 abierta | No |
| Seguridad (§21/22) | Permisos oficiales, aislamiento empresa, auditoría y archivos privados | Admin, sucursales, catálogo/controles concretos, conservación y cifrado | No |
| Interfaz y operación (§23) | Responsive computadora/tablet/celular; panel inicial, seguimiento, lista de espera y reportes básicos | Composición visual, campos, métricas y políticas específicas no aportadas | No |

### 0.6 Qué no hace este documento

No crea matriz ni recuperación nuevas. Esta revisión actualiza las entradas históricas, registro, índice y estados y prepara ADR/plan, sin tocar código, bases de datos ni migraciones ni aprobar decisiones pendientes.

### 0.7 Fuentes consolidadas

| Código | Documento | Contenido aportado |
|---|---|---|
| R1 | SPA-V1.1-RECOVERY-01 | Reconstrucción, requisitos [F], invariantes `SPA-INV-*`, pruebas `SPA-T-*` |
| R2 | SPA-V1.1-OPERATIONS-SPEC-DRAFT-01 | Agenda, servicios, paquetes, ventas (`SPA-OPS-*`) |
| R3 | SPA-V1.1-CARE-PRIVACY-SPEC-DRAFT-01 | Expediente, valoraciones, consentimientos, fotografías (`SPA-CARE-*`) |
| R4 | SPA-V1.1-SECURITY-COMPLIANCE-DRAFT-01 | Seguridad, auditoría, retención, legal (`SPA-SEC-*`) |
| MATRIZ | SPA-V1.1-CONTRACT-CONSOLIDATION-MATRIX-01 | 295 registros, conflictos `C-xx`, cuestiones `O-xx`, verificaciones `V-xx` |
| NORM | SPA-V1.1-CONTRACT-NORMALIZATION-01 | Redacciones canónicas, glosario, catálogo de permisos, grupos de pruebas `PT-xx` |
| REG | [SPA-V1.1-APPROVED-DECISIONS-01](SPA-V1.1-APPROVED-DECISIONS-01.md) | Fuente histórica aportada SPA-DEC-115/116, O-25/O-15, DIR-SSOT-01 y Empleados aplicable |

Inventario: los seis antecedentes R1–R4, MATRIZ y NORM se retiraron del árbol tras comprobar que su contenido está representado en estos cuatro documentos oficiales (identificadores en Anexo C; evidencia legal, glosario, conflictos y dependencias en Anexos G–I; cambios de arquitectura en ADR-051 §9.1); sus versiones previas se conservan en el historial Git y los códigos R1–R4/MATRIZ/NORM se mantienen solo como procedencia. No se localizaron originales V01 ni GS-* independientes en el inventario de rutas; sus equivalencias transmitidas se conservan en Anexo E. Las fichas originales siguen sin contraste.

Fuentes externas sin inspección directa en esta consolidación: `Gali_Spa_Fichas_Actualizadas.docx`, `Gali_Spa_Fichas_Clinicas_y_Consentimiento.docx`, `Gali_Spa_Ficha_Compacta_Dos_Caras.pdf`, `Gali_Spa_Facial_Actualizada.pdf`, `Gali_Spa_Corporal_Actualizada.pdf` y otras variantes transmitidas. No se presume equivalencia entre versiones (O-24). SPA-DEC-115/116 se recuperaron con alcance aprobado mediante el encargo del propietario (§9/10 de REG), antes conocido solo parcialmente. No se recuperaron los textos SPA-DEC-001..114; la numeración no demuestra inventario completo ni fechas originales.

### 0.8 Cobertura y límites de la consolidación

§2 conserva las once áreas y siete agrupaciones de SPA-DEC-116; §3 las diez exclusiones; §7.1 los siete enunciados operativos de SPA-DEC-115. §23 reúne las fichas funcionales y §28 las verificaciones candidatas. Cada regla anterior mantiene identificador/procedencia; una aprobación general no aprueba automáticamente su traducción [T].

Detalles aún no recuperados: campos y obligatoriedad de fichas (O-21/O-24/V-01), flujo oficial de cliente inexistente, numeración/código de expediente (O-19), permisos/errores precisos por operación, persistencia/vínculos técnicos de visita–citas según SPA-ARQ-01 y mecanismos técnicos del ciclo clínico SPA-ARQ-03 (§12), transiciones/efectos de tratamiento, restricciones temporales y ajustes/consumos de paquetes. Firma en pantalla, versiones, expediente único y las capacidades de §2 ya no se califican como pendientes de inclusión. Las fichas indican lo conocido y el detalle pendiente sin inventar diseño visual.

## 1. Objetivo del módulo

**Producto genérico y multiempresa.** SPA V1.1 es el módulo genérico de ERP ZH para cualquier empresa que gestione un spa o tratamientos estéticos. No codifica empresas, profesionales, sucursales ni servicios concretos: toda configuración respeta tenant, empresa y sucursal; los clientes reutilizan BusinessPartner, los profesionales reutilizan Empleados y los servicios comerciales reutilizan Items; Ventas, Caja, CxC, Inventario y Contabilidad conservan sus responsabilidades. El módulo administra atención, agenda, tratamientos, expedientes y paquetes, y ningún expediente es accesible desde otra empresa. La interfaz utiliza el nombre SPA. «Gali Spa» solo aparece como atribución histórica de la procedencia de decisiones y formularios originales (p. ej. archivos `Gali_Spa_*` y evidencia SPA-DEC); sus particularidades no son valores fijos del módulo y este documento no crea un motor genérico nuevo de formularios ni aprueba configuraciones no documentadas.

**Jerarquía documental de SPA V1.1 (fuente única por tema):** este contrato define el comportamiento funcional; [ADR-051](ADR-051-spa-v1.1-architecture.md) define arquitectura, decisiones técnicas y dependencias; el [plan](SPA-V1.1-IMPLEMENTATION-PLAN-01.md) define fases F0–F8, validaciones y cierre; el [registro](SPA-V1.1-APPROVED-DECISIONS-01.md) conserva aprobaciones y su evidencia. Cada uno remite a los demás sin repetir sus reglas.

**Qué se desarrollará (alcance APROBADO por SPA-DEC-116; antecedente `SPA-AL-01` [R]):** el ámbito funcional del ERP destinado a administrar un establecimiento de servicios estéticos: clientes y expedientes de atención, profesionales
responsables, valoraciones faciales y corporales, consentimientos informados, fotografías y seguimiento, servicios y tratamientos, planes de tratamiento, agenda y reservas, sesiones realizadas, paquetes
comerciales, integración con ventas y cobros, y control de acceso y trazabilidad.

**Principio de reutilización (`SPA-AL-02`):** Spa no crea identidades laborales ni de cliente paralelas, ni registros comerciales o contables paralelos; reutiliza Empleados, BusinessPartner, Items, Ventas,
Cuentas por Cobrar y los mecanismos oficiales de seguridad y auditoría. Parte Empleados: APROBADO-DERIVADO (ADR-050 §3 DEC-E1, §13); parte resto: [R].

**Estado:** alcance funcional APROBADO por SPA-DEC-116 y búsqueda APROBADA por SPA-DEC-115; se conservan antecedentes [F]/[R] y mecanismos [T] separados. No se reinicia la planificación ni se autoriza desarrollo.

## 2. Alcance funcional aprobado de V1.1

**SPA-DEC-116 — APROBADA HISTÓRICA, recuperada en este encargo.** El propietario aporta expresamente su revisión de alcance y agrupaciones funcionales. Fuente canónica de evidencia: [REG §10](SPA-V1.1-APPROVED-DECISIONS-01.md). SPA-DEC-115 añade el comportamiento aprobado de búsqueda por servicio (§7.1). No se exige una nueva aprobación general ni se confunde inclusión con aprobación técnica integral.

| Área aprobada | Comportamiento incluido | Sección | Detalle pendiente, sin cuestionar inclusión |
|---|---|---|---|
| Clientes y expedientes | Expediente único por cliente y empresa; antecedentes, valoraciones, alertas e historial; sin identidad BP paralela | §5/13/15 | Versiones de fichas, datos/obligatoriedad, numeración y políticas de alerta |
| Empleados | Modelo ERP general reutilizable, vinculado con usuarios/sucursales; profesional separado del registrador | §6 | Directorio implementado/probado y contratos; no desarrollar Employee en Spa |
| Tratamientos | Plan individual, multisesión, ampliaciones, suspensión, cancelación, cierre y seguimiento de resultados | §10/16 | Transiciones, roles, validaciones y efectos sobre cupos/comercial |
| Sesiones | Registro técnico individual, formulario/borrador/finalización/interrupción/corrección; productos, incidencias, fotos, resultados y evolución | §12 | Ciclo clínico SPA-ARQ-03 aprobado (§12); persistencia/concurrencia, permisos concretos y consumo O-13 pendientes |
| Agenda y visitas | Día/semana, sucursal/profesional; disponibilidad por servicio; horarios, bloqueos, descansos, preparación, traslados/conflictos, reprogramaciones; varios servicios/profesionales por visita, llegada y cambios | §7/8 | Modelo conceptual resuelto SPA-ARQ-01; cálculo de restricciones, esquema y vínculos técnicos pendientes, sin valores inventados |
| Paquetes | Individuales/combinados, vigencia, cupos/cantidades, ampliaciones/sustituciones, saldo en sesiones; contrato desde factura autorizada | §11 | Reglas de ampliación/sustitución/consumo, vigencia concreta, NC/crédito |
| Consentimientos | Por procedimiento, firma en pantalla, versiones y autorizaciones específicas | §17 | Evidencia técnica/validez legal, menores, retiro y conservación |
| Ventas y cobros | Servicios, facturas, CxC/cobros y ventas de productos mediante procesos oficiales; movimientos comerciales independientes de clínica | §19/20 | Contratos públicos, tratamiento NC/crédito/saldos; O-14 sigue abierta |
| Seguridad | Permisos oficiales existentes, aislamiento por empresa, auditoría, archivos privados y protección de datos | §21/22 | Catálogo/controles de Spa, Admin, sucursales, cifrado, conservación legal |
| Interfaz | Responsive computadora, tablet y celular | §23 | Diseño exacto sin evidencia; sin app móvil independiente ni offline |
| Gestión operativa | Panel inicial, seguimiento de tratamientos, lista de espera, consultas y reportes básicos | §23.3 | Campos/métricas, orden de lista y acciones/reportes específicos no aportados |

### 2.1 Agrupaciones funcionales aprobadas

Procesos integrados, no obligación de crear un módulo independiente por característica: motor de agenda (disponibilidad/horarios/bloqueos/traslados/preparación/conflictos); visitas (citas, varios servicios/profesionales, llegada/cambios); sesiones (formularios/borradores/finalización/interrupciones/correcciones); tratamientos (sesiones contratadas/cupos/ampliaciones/suspensiones/cierres); expediente (valoraciones/alertas/consentimientos/fotos/historial); comercial (paquetes/facturas/cobros/saldos/cancelaciones/ajustes); seguridad (permisos/auditoría/archivos/protección).

Plan individual describe lo previsto; tratamiento operativo gestiona su ejecución multisesión y ciclo; paquete vendido concede derechos comerciales; sesión realizada documenta atención efectiva. No se fusionan ni se vinculan automáticamente sin regla respaldada.

### 2.2 Estado de O-23 y evidencia restante

**O-23: CERRADA en inclusión funcional general de SPA-DEC-116**, por recuperación histórica e instrucción expresa de este encargo. También se recuperaron las exclusiones originales (§3.1). No es cierre de una fase de implementación ni aceptación de ADR-051.

Se separan las cuestiones antes mezcladas: (a) alcance y agrupaciones de SPA-DEC-116, aprobados; (b) contraste/versiones de formularios originales, **O-24/V-01 siguen pendientes**; (c) detalles de SPA-DEC-001..114 no recuperados, evidencia histórica pendiente para reglas concretas, sin inventar textos o reabrir el alcance general. O-14, decisiones de detalle, revisión técnica, cumplimiento jurídico y autorización de fases no se cierran por O-23.

## 3. Funcionalidades excluidas

Se separan tres conceptos que no deben confundirse: **excluido** (tiene respaldo), **no autorizado** (sin decisión ni capacidad; fuera hasta que se decida) y **pendiente** (está dentro del contrato y espera
una decisión, §30). **Pendiente no es excluido.** SPA-DEC-116 recupera las diez exclusiones originales de §3.1; los pendientes de detalle no invalidan el alcance aprobado de §2.

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

### 3.1.1 Diez exclusiones originales expresas de SPA-DEC-116

Los códigos HX son etiquetas locales de trazabilidad documental, no decisiones históricas nuevas. Fuente: REG §10.1, transcripción del propietario.

| Código | Exclusión histórica aprobada | Distinción que se conserva |
|---|---|---|
| HX-01 | Control de disponibilidad de cabinas y equipos | Descansos, bloqueos, preparación y traslados sí incluidos; sin calendario de recursos físicos |
| HX-02 | Recordatorios automáticos por WhatsApp, correo o SMS | Búsqueda/selección explícita y agenda interna sí incluidas; no confirmación automática por búsqueda |
| HX-03 | Desarrollo de un nuevo sistema de respaldos o recuperación | Usar/verificar los mecanismos existentes no autoriza desarrollar otro sistema |
| HX-04 | Reservas públicas de clientes | Recepción opera agenda y disponibilidad interna |
| HX-05 | Descuento automático mediante Kardex de productos aplicados en sesiones | Registro técnico de productos y venta oficial de productos incluidos, sin consumo automático de stock |
| HX-06 | Nómina y administración completa de recursos humanos | Employee reutilizable según ADR-050 incluido como dependencia |
| HX-07 | Aplicación móvil independiente | Responsive computadora/tablet/celular incluido |
| HX-08 | Funcionamiento sin conexión a Internet | No se añade offline por diseño móvil |
| HX-09 | Diagnósticos automáticos e inteligencia artificial para evaluar resultados | Valoraciones y seguimiento técnico humano incluidos |
| HX-10 | Dashboard analítico adicional y contabilidad paralela | Panel operativo/reportes básicos incluidos; contabilidad, Caja y CxC oficiales |

### 3.2 No autorizado (sin decisión ni capacidad del ERP)

| ID | Funcionalidad | Motivo |
|---|---|---|
| N-01 | Facturación por sesión y devengo de ingresos por sesión | El ERP no tiene factura por sesión ni ingreso diferido [NV]; ninguna decisión lo autoriza |
| N-02 | Reserva con depósito, prepago sin factura, saldo a favor del cliente | Dependen de Anticipos de Clientes (`O-14`; §20.4); el ERP no tiene ese proceso |
| N-03 | Devolución de dinero a tarjeta o transferencia y crédito para compras futuras | Las devoluciones de Ventas reembolsan solo en efectivo o contra CxC; sin proceso transversal no hay otra vía (`O-14`) |
| N-04 | Consumo automático de insumos con movimiento de inventario | Reclasificado como exclusión expresa HX-05 por SPA-DEC-116; no alternativa pendiente de inclusión |
| N-05 | Módulo de activos, mantenimiento o reserva de equipos | `SPA-IN-03`: no aprobado |
| N-06 | Uso académico de datos y fotografías | `SPA-CARE-CNS-07`: no se implementa ni se asume hasta que el propietario confirme que pertenece al producto (`O-17`) |
| N-07 | Recordatorios y confirmaciones automáticas de citas | Recordatorios excluidos HX-02; buscar disponibilidad no confirma automáticamente (SPA-DEC-115). Estado y operación de confirmación manual no definidos |
| N-08 | Vendedor y cobrador distintos del profesional en Ventas | El dominio de Ventas no tiene vendedor (`V-03c`); sería un contrato nuevo y Ventas es FROZEN |
| N-09 | Publicación o exportación automática de fotografías | `SPA-CARE-FOT-04`; solo acción explícita con permiso propio |
| N-10 | Creación automática del contrato de paquete mediante `SalesInvoiceAuthorizedEvent` | `O-15` #7: «no se aprueba»; alternativa no seleccionada |
| N-11 | Sesión única creada desde la reserva y reserva como entidad independiente | `O-25`: la alternativa no seleccionada «no se aprueba» |

Una funcionalidad no autorizada **no está definitivamente excluida**: puede incorporarse mediante una decisión posterior del propietario.

## 4. Dependencias y límites con otros módulos

**Revisión concentrada F0 (recomendación técnica, no nueva aprobación):** [ADR-051 §5.10–5.15](ADR-051-spa-v1.1-architecture.md#510-selección-técnica-concentrada-f0--contrastada-con-precedentes-aprobada-reg-25) selecciona integraciones/modelo/seguridad. §5.12 del ADR inventaría proyecciones mínimas, consumidores y permisos; solo lectores nuevos candidatos Ventas/Finance donde falte superficie pública suficiente. No implementados ni acceso a repositorios internos ajenos. BP/Customer tenant sin bandera empresarial; IEmployeeDirectory bajo ADR-050 pendiente.

**Referencia normativa principal de SPA-ARQ-08 — APROBADA POR EL PROPIETARIO el 2026-11-19.** Texto literal y límites: [REG §19](SPA-V1.1-APPROVED-DECISIONS-01.md#19-spa-arq-08--integraciones-con-erp-zh). §4.2 define fronteras funcionales; la tabla y §4.1 conservan evidencia técnica previa, sin nueva inspección de código ni contratos implementados.

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

### 4.1 Verificaciones técnicas heredadas y límites de su evidencia

La revisión documental previa registró estos resultados; se conservan como evidencia fechada, sin afirmar una nueva auditoría de código en este encargo:

| V | Resultado disponible | Pendiente que no se cierra |
|---|---|---|
| V-03a | Devolución omite Kardex para líneas sin bodega; Service no lleva bodega | Flujo completo de validación de devolución Service y líneas devolvibles sin comprobar íntegramente |
| V-03b | SalesInvoiceAuthorizedEvent existe, no trae cliente/líneas y sus handlers participan en la transacción de autorización | Lectura pública de factura para creación explícita: verificar contrato consumible sin extender Ventas; no crear mediante evento (O-15) |
| V-03c | No se encontró vendedor en dominio Ventas | Separación vendedor/cobrador respecto del profesional sigue pendiente, no se agrega campo |
| V-04 | No hay clase de precisión corporal en el estándar | Revisión formal antes de elegir política/escala; no proponer numeric por analogía |
| V-05a/b | Sin precedente de horarios recurrentes ni tstzrange; sí existe exclusión daterange/btree_gist | Horas ambiguas, claves de lock, scope e integridad de intervalos requieren comprobación futura |
| V-07 | Sin publicación estática de archivos encontrada en backend/nginx del repositorio | Proxy productivo, cifrado de disco y custodia de claves externos sin verificar |
| V-08 | ItemImage/DTO tienen StorageObjectId, sin entidad/tabla/resolutor de objetos reutilizable | Registro privado de archivos Spa sería nuevo; referencia no equivale a infraestructura resuelta |
| V-01/02/06/09 | Fichas originales, directorio implementado, asesoría jurídica y vigencia oficial no disponibles como evidencia cerrada | Permanecen pendientes, respectivamente |


### 4.2 Ocho fronteras funcionales aprobadas SPA-ARQ-08

| Regla | Integración | Obligación aprobada y límite |
|---|---|---|
| 1 | Clientes/BP | BusinessPartner oficial con rol Customer, sin catálogo paralelo ni duplicar nombre/identificación/información comercial cuya fuente sea BP. BP conserva ámbito oficial tenant; expediente por empresa y aislamiento SPA-ARQ-06. Consultar por contratos autorizados del propietario. |
| 2 | Profesionales/Empleados | Profesionales del módulo general de Empleados, mediante lectura oficial de identidad laboral/asignaciones/elegibilidad. Empleados posee fichas/asignaciones; SPA horarios/agenda/procedimientos/sesiones. Profesional y registrador distintos; sin Employee paralelo ni acceso clínico por permisos Empleados. IEmployeeDirectory pendiente de implementación bajo ADR-050/plan propio; no implementarlo por esta decisión. |
| 3 | Servicios/Items | Items oficiales Service, precios/impuestos del ERP; SPA duración/preparación/condiciones y datos operativos. Sin catálogo comercial paralelo ni modificación inferida Items/Pricing. Relación servicio–procedimiento técnica pendiente; permitir individuales y paquetes combinados. |
| 4 | Ventas/paquetes | Aplicar siete reglas intactas O-15 (§11.1): creación explícita tras validar factura autorizada, factura/línea, fuera de autorización Ventas, sin duplicados ni reversión de factura por fallo SPA, sin documentos financieros ni creación automática por SalesInvoiceAuthorizedEvent. Ampliación/sustitución/ajustes según SPA-ARQ-05 y documentos oficiales; fiscal/SRI, crédito y NC específicos pendientes. |
| 5 | Caja/CxC | Consultar solo información comercial autorizada necesaria; cobros en Caja/procesos oficiales, cuentas/saldos monetarios en CxC. SPA derechos en sesiones, sin saldos monetarios, otra Caja/CxC/contabilidad ni cobro automático por atención. Anticipos transversal independiente DIR-SSOT-01; O-14 abierto, sin anticipos SPA. |
| 6 | Inventario/productos | Productos utilizados documentados con fines técnicos/seguimiento sin Kardex automático. Venta física por procesos comerciales/inventario oficiales; sin inventario paralelo ni consumo automático de insumos. HX-05 preservada. |
| 7 | Seguridad/auditoría | Reutilizar Identity/Access/Kernel/auditoría oficiales, sin usuarios/autenticación/permisos generales paralelos. Clínica y auditoría sensible conforme SPA-ARQ-06; extensiones deben integrarse sin cambios LOCKED/FROZEN inferidos. Admin sin acceso ordinario por su condición aprobado, mecanismo C-01/O-01 bloqueado; C-02/O-05 y demás pendientes intactos. |
| 8 | Genérico/multiempresa | Módulo genérico ERP ZH para diferentes empresas del sector, sin dependencia de nombres comerciales. Respetar tenant/empresa/sucursal, contexto autorizado, expediente único cliente/empresa, derechos de paquete sin traslado entre empresas y elegibilidad/disponibilidad profesional. Interfaz denominada SPA. Gali Spa solo atribución de evidencia histórica; sin duplicar responsabilidades de otros módulos. |

**Pendientes técnicos y autorización:** contratos autorizados de lectura/firmas, APIs, entidades/relaciones, índices/locks/algoritmos, revisión arquitectónica ADR-051 y evidencia de integración. No extender BP/Items/Pricing/Ventas/CxC ni cambiar Kernel LOCKED, auditoría FROZEN o IFileStorage por esta aprobación. No migraciones ni implementación de dependencias; fiscal/ley/O-14 no resueltos. ADR-051 ACCEPTED / APROBADO, F0–F8 PENDING / NO AUTORIZADA.

### 4.3 SPA-TEC-01.8 — Dirección de integración aprobada

Contratos mínimos oficiales de lectura, sin repositorios internos ajenos ni copia de maestros; aplicar §4.2. BP y Customer conservan scope tenant: no afirmar bandera de habilitación Customer por empresa. Expediente empresarial distinto. IEmployeeDirectory no implementado bajo ADR-050; firmas/consultas específicas por verificar, sin ampliar módulos protegidos. Precios/impuestos, documentos y pagos en sus propietarios; SPA clínica, agenda y derechos. SPA-TEC-03 concreta relación comercial §10.2/11.6/20.5.

REG §20/21/22 conserva decisiones completas. Distinguir función aprobada, dirección técnica aprobada, detalle pendiente, autorización especial pendiente, validación jurídica/fiscal pendiente y dependencia no implementada. No aceptación integral de ADR-051 ni implementación.

## 5. BusinessPartner y clientes

Aplicar §4.2 regla 1: identidad oficial BP Customer y lectura autorizada; no copia ni catálogo paralelo. Contexto de expediente por empresa sin cambiar scope BP tenant. Flujo de cliente inexistente/O-06 sigue pendiente.

**Qué hace:** identifica a la persona atendida sin duplicar su identidad. **Estado de la sección:** el principio es VIGENTE-ERP/[R]; el flujo de cliente inexistente y los datos sin campo en BusinessPartner
están PENDIENTES. **Implementable hoy:** no. **Bloqueos:** BL-09 (§29.1).

| ID | Comportamiento contractual | Estado | Evid. |
|---|---|---|---|
| SPA-AR-01 | El cliente es un BusinessPartner con rol Customer; Spa referencia su identificador y no crea otro registro de identidad personal | VIGENTE-ERP | R; ADR-017 |
| SPA-CARE-EXP-01 | Identidad BP Customer y datos por contrato autorizado conforme §4.2 regla 1; pertenencia empresarial del expediente distinta de ámbito tenant BP. Condición activo y detalles de proyección aún candidatos | APROBADO frontera SPA-ARQ-08; mecanismo/condiciones adicionales PROPUESTOS | A + T |
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

SPA-ARQ-08 §4.2 regla 2 confirma propiedad laboral de Empleados y responsabilidades operativas SPA; IEmployeeDirectory sigue dependencia contractual pendiente bajo ADR-050, no implementación autorizada.

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
| SPA-AR-04 | Spa conserva servicios, tratamientos, seguimiento e información de atención estética | APROBADO como alcance por SPA-DEC-116; mecanismos pendientes | A + R |
| SPA-AR-06 | La elegibilidad del profesional se evalúa con `IEmployeeDirectory` y las fechas de la operación; el pasado no se altera porque el profesional deje la empresa | PROPUESTO | T |
| SPA-OPS-AGE-14 | Si un profesional deja de estar disponible, las citas futuras **no se modifican automáticamente**; se listan para reasignación manual. La política de reasignación es pendiente | PROPUESTO / PENDIENTE | T, P |
| SPA-OPS-SRV-06 | Profesionales elegibles por **especialidad** del catálogo de Empleados (DEC-E5); Spa no crea especialidades. Cargo y profesión pueden ser nulos: no suponer elegibilidad sin regla | PROPUESTO / PENDIENTE | T |

**Verificación pendiente:** `V-02` (contrato `IEmployeeDirectory` real, EMP-E1 a E6b). **Bloqueo:** `D-01` (§29).

## 7. Sucursales y disponibilidad

**Selección técnica F0 recomendada:** ADR §5.11 concreta horario/excepciones, ocupación y transacción/locks/exclusión GiST; la obligación aprobada sigue §7.3/8.7. No son capacidades ya implementadas ni SQL aprobado por el propietario.

**Referencia funcional principal de SPA-ARQ-04 — APROBADA POR EL PROPIETARIO el 2026-11-19.** Texto literal y límites: [REG §15](SPA-V1.1-APPROVED-DECISIONS-01.md#15-spa-arq-04--agenda-y-disponibilidad). Complementa SPA-DEC-115/116 y SPA-ARQ-01/02/03 sin ampliar O-15/O-25. **Implementable hoy: no.** BL-01/09 y detalle técnico aplicable siguen pendientes.

### 7.1 SPA-DEC-115 — Búsqueda de disponibilidad por servicio

**APROBADA; siete enunciados literales conservados en REG §9.** Aplicar §7.2 regla 8 y sus restricciones mediante la misma agenda, sin segundo motor. Flujo: servicio/fecha/sucursal → alternativas → selección explícita de profesional y horario → operación de reserva según O-25. Sin resultados, informar ausencia de alternativas; confirmar exige revalidación conforme regla 9. Mensajes/códigos y política técnica de reconsulta siguen pendientes.

### 7.2 Reglas aprobadas SPA-ARQ-04

| Regla | Materia | Obligación funcional aprobada y límite |
|---|---|---|
| 1 | Horarios y elegibilidad | Respetar horarios de sucursal y profesionales, asignaciones vigentes y elegibilidad del módulo oficial de Empleados; acceso ERP a sucursal no equivale a disponibilidad laboral. |
| 2 | Duración | Duración configurable por servicio/procedimiento; búsqueda utiliza duración efectiva. Producto comercial Item `Service`, sin otro catálogo; perfiles/procedimientos O-11 pendientes. |
| 3 | Descansos, ausencias y bloqueos | Intervalos no disponibles y bloqueos autorizados impiden ofrecer horarios incompatibles; contemplar restricciones pertinentes de sucursal. Sin disponibilidad de cabinas/equipos (HX-01). |
| 4 | Preparación | Preparación configurable por servicio/procedimiento reserva el intervalo necesario; sin margen universal. Valores y almacenamiento pendientes. |
| 5 | Traslados | Considerar tiempo configurado entre sucursales para evitar compromisos incompatibles del profesional. Sin minutos predeterminados ni servicio externo de rutas; política de configuración y excepciones pendientes. |
| 6 | Citas consecutivas | Permitidas sin solape ni incumplimiento de preparación, descanso, traslado u otras restricciones aplicables; sin márgenes adicionales. |
| 7 | Varios servicios/profesionales | Aplicar SPA-ARQ-01: cada bloque valida al profesional correspondiente; varios bloques de un servicio no duplican prestación comercial. |
| 8 | Búsqueda por servicio | Servicio, fecha y sucursal sin profesional inicial; mecanismo centralizado devuelve profesionales habilitados y horarios; selección explícita, sin crear ni confirmar cita. Conservar siete enunciados SPA-DEC-115 (§7.1). |
| 9 | Reservas simultáneas | Revalidar al confirmar; dos operaciones concurrentes no pueden reservar compromisos incompatibles para el mismo profesional. Transacciones, locks, exclusión e índices PostgreSQL pendientes de ADR-051. |
| 10 | Reprogramación | Revalidar nuevo horario; ante fallo conservar cita original y comunicar conflicto. Cambio válido conserva identidad/Programada, historial, usuario, cambios e indicador; sin consumo, conforme SPA-ARQ-02/O-25. |
| 11 | Zona horaria | Presentar según configuración horaria oficial del ERP de empresa/sucursal y gestionar instantes consistentemente en UTC. Si no existe zona por sucursal ni ampliación aprobada, usar zona oficial de empresa; no crear configuración. Resolver técnicamente horas inexistentes/ambiguas sin reservas inconsistentes. |
| 12 | Cambios e interrupciones | Conservar trazabilidad; interrupción clínica según SPA-ARQ-03 no libera automáticamente tiempo restante ni genera consumo. Estado final de cita interrumpida pendiente; sin ampliar cinco estados SPA-ARQ-02 con cierre administrativo explícito según §8.7. |

Trazabilidad de identificadores anteriores: SPA-AG-01..03/DUP-13 y SPA-OPS-AGE-04 corresponden a regla 1; SPA-OPS-AGE-03 a 11; SPA-OPS-AGE-05 a 3/HX-01; SPA-OPS-AGE-06 a 6/9; restricciones de descanso/preparación/traslado a 3–5. La pertenencia de agenda a Spa y la separación de acceso ERP/disponibilidad laboral se conservan. Las interfaces `IEmployeeDirectory`/`ICompanyClock`, recurrencias, algoritmos y estructuras de intervalos no se aprueban por esta decisión.

**Pendientes técnicos y límites:** entidades/tablas y representación física de intervalos recurrentes, algoritmos detallados, claves/locks, restricciones SQL y API; dirección temporal/transaccional aprobada §7.3; contratos con Empleados sin capacidades nuevas, política de configuración de traslados/excepciones, valores sin predeterminados, tratamiento técnico de horas inexistentes/ambiguas. Reasignación automática y estados adicionales no aprobados; liberación de tiempo solo bajo cierre administrativo explícito de §8.7, sin reinterpretar otras operaciones. O-09 queda parcialmente resuelta funcionalmente; O-11, aplicación técnica del cierre administrativo aprobado (§8.7) y políticas comerciales conservan su detalle abierto. No se autoriza implementación, recursos físicos, recordatorios ni reservas públicas.

### 7.3 SPA-TEC-01.3/01.4 — Dirección temporal y de reservas aprobada

Horarios recurrentes, excepciones por fecha e intervalos UTC con zona oficial de empresa mediante CompanyTimeZone/ICompanyClock conforme contratos y reglas de horas ambiguas/inexistentes. Intervalos semiabiertos [inicio, fin) permiten contiguas sin solape; preparación/traslado adicionales, sin sistema temporal paralelo.

Revalidar disponibilidad en transacción; bloqueos coordinados por profesional sin sucursal en identidad del recurso y orden consistente multiprofesional; comprobar preparación/traslados/ausencias. Restricciones PostgreSQL compatibles con esquema aprobado; mecanismo físico SQL/índices/locks requiere verificación/aprobación específica, no implementado. Reprogramación atómica ante conflicto conserva cita original (§27). Interrupción sola no libera tiempo; cierre explícito §8.7 sí libera ocupación futura en su alcance.

## 8. Agenda, citas y reservas

**Qué hace:** programa atenciones y conserva su historial. **Estado de la sección:** agenda, visitas y búsqueda incluidas por SPA-DEC-115/116; modelo cita/sesión APROBADO por O-25. Mecanismos y políticas no aportadas siguen pendientes.
**Implementable hoy:** no. **Bloqueos:** BL-01, BL-09 (§29.1).

### 8.1 Decisión aprobada O-25 (alternativa A — cita separada de la sesión realizada)

Texto literal de los ocho enunciados ([registro](SPA-V1.1-APPROVED-DECISIONS-01.md) §1.2):

1. **Reserva** es la operación que crea una cita, no una entidad independiente.
2. La **cita** representa la programación en la agenda.
3. La **sesión realizada** es un registro independiente creado cuando se documenta una atención efectivamente realizada.
4. Una **sesión programada** de un paquete se representa mediante una cita vinculada al contrato.
5. **Reservar y reprogramar no consume saldo** de sesiones.
6. **Cancelar o registrar una ausencia no crea una sesión clínica realizada.**
7. El **consumo comercial** se representa por un movimiento independiente del contrato, cuando corresponda.
8. Se conserva la **trazabilidad** de reprogramaciones, cancelaciones y atenciones.

**Fuera del alcance de aprobación de O-25, sin ser exclusiones globales:** ausencias, atención sin cita y efectos comerciales de cancelación no fueron resueltos por O-25. Posteriormente SPA-ARQ-05 (§11.2 reglas 5/6) resuelve ausencia sin penalización/descuento automático y cancelaciones sin reversión automática; atención sin cita O-10 sigue pendiente. Varios servicios/profesionales por visita y restricciones de agenda sí están aprobados por SPA-DEC-115/116; O-25 no los revoca. SPA-ARQ-01 resuelve visita que agrupa citas/bloques; cálculo exacto y mecanismos técnicos pendientes; cabinas/equipos excluidos HX-01.

### 8.2 Reglas de la cita (aprobaciones delimitadas y mecanismos propuestos)

| ID | Comportamiento contractual | Estado | Evid. |
|---|---|---|---|
| SPA-OPS-AGE-01 | Visita: una o varias citas del mismo cliente para una ocasión en una sucursal. Cada cita es bloque de agenda con profesional e intervalo; visita admite varios servicios/profesionales y varios bloques de un servicio no duplican prestación comercial | APROBADO conceptual SPA-ARQ-01; esquema técnico pendiente | A + T |
| SPA-OPS-AGE-02 | Estados operativos de cita: `Programada`, `En atención`, `Atendida`, `Cancelada`, `Ausente`. Llegada es evento de visita (fecha, hora, usuario); `Reprogramada` es evento/indicador visible, no sexto estado | APROBADO SPA-ARQ-02; mecanismos técnicos pendientes | A |
| SPA-OPS-AGE-07 | Reprogramación válida conserva identidad y estado `Programada`; registrar fecha/hora anterior/nueva, profesional anterior/nuevo cuando corresponda, autor/momento. Mostrar indicador de reprogramación e historial; no consumo de saldo ni cancelación de la visita | APROBADO SPA-ARQ-02/O-25 y SPA-ARQ-04 §7.2 regla 10; mecanismos técnicos pendientes | A + P |
| SPA-OPS-AGE-08 | Cancelar fija estado `Cancelada`, motivo, autor e instante; nunca elimina. Quién cancela y con qué anticipación es pendiente; no crea sesión realizada (`O-25` #6) | PROPUESTO / PENDIENTE | T, P |
| SPA-OPS-AGE-09 | Ausencia: tras el inicio de la cita, un usuario autorizado la marca `Ausente`; no crea sesión realizada (`O-25` #6). Aplicar §11.2 regla 5: sin descuento ni penalización comercial automática, cargo/factura/cobro | APROBADO efecto SPA-ARQ-05; momento/autorización técnica PROPUESTOS | A + T |
| SPA-OPS-AGE-10 | Cada cita inicia y termina atención independientemente. `Atendida` exige atención efectiva documentada; una atención interrumpida no se considera automáticamente completada, ni se alcanza Atendida por llegar la fecha | APROBADO SPA-ARQ-02/O-25; ciclo clínico SPA-ARQ-03 §12, aplicación técnica del cierre administrativo aprobado (§8.7) pendiente | A + P |
| SPA-OPS-AGE-11 | Atención sin cita previa: alternativas A admitirla creando cita y sesión juntas, B exigir cita siempre | PENDIENTE (`O-10`) | P |
| SPA-OPS-AGE-12 | Todo cambio de estado, hora, profesional o sucursal queda en `AppointmentChange` y en Entity Audit | APROBADO en el principio de trazabilidad (`O-25` #8); mecanismo PROPUESTO | T |
| SPA-OPS-AGE-13 | Aplicar revalidación y conservación ante fallo de §7.2 regla 10; trazabilidad según SPA-ARQ-02 | APROBADO funcional SPA-ARQ-04; mecanismos pendientes | A + P |
| Transiciones | Inicio y fin de atención independientes por cita; visita deriva su situación de las citas y finaliza cuando todas tienen estado final. Reprogramar válidamente conserva Programada; ni llegada ni reprogramación son estados nuevos. No se fija matriz adicional de excepciones/reversas o interrupciones | APROBADO SPA-ARQ-02 en su alcance; detalle no aportado pendiente | A + P |
| Servicios y profesionales por visita | Visita agrupa una o varias citas; un servicio con varios profesionales puede usar varios bloques sin duplicar prestación comercial. Número de sesiones clínicas por prestación no decidido | APROBADO conceptual SPA-ARQ-01; O-11 solo detalle técnico | A + P |

### 8.3 Flujos (reglas aprobadas y mecanismos propuestos)

Aplicar §7.2 reglas 9/10/12: confirmación y reprogramación revalidan; fallo conserva original; interrupción no libera automáticamente el tiempo. Transacción y registro físico del flujo siguiente son candidatos técnicos.

Reservar (cliente, servicio, sucursal, profesional, hora → comprobar disponibilidad → crear cita `Programada` y su primer registro de cambio, en una transacción); reprogramar; cancelar con motivo; marcar
ausencia con mecanismo aún pendiente y efecto comercial aprobado §11.2 regla 5; cada cita inicia/finaliza atención independientemente (`En atención` / `Atendida`), con atención efectiva documentada para Atendida (SPA-ARQ-02). Interrumpir no completa automáticamente. Casos excepcionales: profesional no disponible, cliente o sucursal inactivos, solape por
reserva simultánea, hora inexistente por cambio de horario.

### 8.4 Gestión de visitas y llegada

SPA-DEC-116 aprueba visita con citas, varios servicios, varios profesionales, llegada, reprogramación/cancelación y cambios. Se presenta el contexto de cliente, sucursal, servicios/profesionales y horarios documentados, sin inventar un formulario original. Recepción selecciona y registra esos hechos; el motor único controla conflictos y conserva historial. La búsqueda no confirma la cita automáticamente.

**Modelo conceptual resuelto por SPA-ARQ-01 (§8.5):** visita agrupa una o varias citas/bloques de profesional e intervalo del mismo cliente y ocasión en una sucursal. **Detalle pendiente:** secuencia o solape de intervalos, esquema/vínculos de prestaciones, persistencia del evento de llegada, cambios parciales y presentación de la situación derivada. SPA-ARQ-02 aprueba los cinco estados de cita, inicio/fin independientes y visita derivada sin máquina de estados propia: finaliza al tener todas sus citas en estado final. Llegada lleva fecha/hora/usuario; no finaliza la visita por completar solo una cita. Cancelar o ausentarse no genera sesión clínica; la atención efectiva sí se documenta separadamente (O-25). Flujos excepcionales: conflicto al modificar, profesional no habilitado o prestación interrumpida; efectos exactos pendientes, sin automatismos comerciales.

### 8.5 Modelo conceptual aprobado SPA-ARQ-01

**APROBADO por el propietario el 2026-11-19.** Texto exacto y alcance: [REG §12](SPA-V1.1-APPROVED-DECISIONS-01.md#12-spa-arq-01--modelo-conceptual-de-visitas-citas-y-sesiones).

La visita agrupa una o varias citas del mismo cliente para una ocasión de atención en una sucursal. Cada cita es un bloque de agenda con profesional e intervalo. La visita admite varios servicios y profesionales; un servicio que requiera varios profesionales puede utilizar varios bloques **sin duplicar la prestación comercial**. Cancelar una cita **no cancela automáticamente las demás** de la visita.

Las sesiones realizadas se registran independientemente al documentar atención efectiva (§12). No se deriva una sesión por bloque ni se decide su número por prestación. Reservar y reprogramar no consumen saldo: consumo comercial independiente conforme a O-25. La aprobación no altera O-15 ni sus referencias factura/línea.

Flujo documentado: agrupar citas/bloques bajo la visita del cliente/ocasión/sucursal; consultar o modificar la programación mediante la agenda; una cancelación afecta a la cita seleccionada sin propagar cancelación a las demás. Documentar atención efectiva crea el registro independiente correspondiente; no se fija cardinalidad clínica por esa secuencia.

**Actualización SPA-ARQ-02 (§8.6):** estados de cita, llegada/reprogramación y situación derivada de visita aprobados expresamente, sin máquina de estados propia. **Pendientes:** sesiones clínicas por prestación, mecanismos técnicos de interrupciones/parciales/correcciones aprobadas en SPA-ARQ-03 (§12), correspondencia con agenda, tiempos de preparación, concurrencia, ausencias y efectos comerciales. Entidades/agregados físicos, relaciones persistidas, claves/API/DTO y algoritmos también requieren definición técnica; no se inventan aquí.

Criterios candidatos de verificación documental/funcional futura: una visita conserva sus bloques por cliente/ocasión/sucursal; un servicio multiprofesional conserva una prestación comercial aunque use varios bloques; cancelar un bloque no cancela los restantes; programación y atención/consumo permanecen separados. Son criterios de comprobación del modelo aprobado, no pruebas ejecutadas ni aprobación de mecanismos técnicos.

### 8.6 Estados, eventos y situación de visita aprobados SPA-ARQ-02

**APROBADA 2026-11-19.** Enunciados completos y complemento del propietario: [REG §13](SPA-V1.1-APPROVED-DECISIONS-01.md#13-spa-arq-02--estados-de-citas-situación-de-visitas-y-reprogramación). Se conservan SPA-ARQ-01 y O-25.

La cita tiene exactamente cinco estados operativos: `Programada`, `En atención`, `Atendida`, `Cancelada`, `Ausente`. Inicia y termina su atención independientemente de las demás citas. `Atendida` exige atención efectivamente realizada y documentada; una interrupción no se considera automáticamente completada. SPA-ARQ-02 no aprobó transiciones clínicas ni estado de interrupción; TEC-02.B aporta posteriormente cierre administrativo explícito (§8.7), sin sexto estado.

La llegada es **evento de visita** con fecha, hora y usuario. La situación general de la visita **se deriva de sus citas, sin máquina de estados independiente**; la visita finaliza cuando todas sus citas tienen estado final. Cancelar o modificar una cita no cancela automáticamente la visita ni sus otras citas y conserva la agrupación aprobada por SPA-ARQ-01. No se inventan etiquetas de situación ni reglas adicionales de reversa.

**Reprogramación:** `Reprogramada` es evento de cambio e indicador visible, no sexto estado. Después de una reprogramación válida, la cita conserva identidad y `Programada`. Registrar fecha/hora anterior y nueva, profesional anterior/nuevo cuando corresponda, autor y momento del cambio. Mostrar en agenda que fue reprogramada y permitir consultar historial. Se mantiene la visita; no se deduce permiso para reprogramar desde cualquier estado ni nuevas condiciones de validez.

Las transiciones de agenda no consumen saldo automáticamente; reprogramar no consume, conforme a O-25. Consumo y efectos comerciales permanecen independientes. **Pendientes expresos:** persistencia/concurrencia del ciclo clínico aprobado SPA-ARQ-03 (§12), aplicación técnica del cierre administrativo aprobado (§8.7), mecanismos de ausencia/cancelación bajo SPA-ARQ-05 (§11; sin penalización automática V1.1) y concurrencia; tampoco se eligen tiempos de preparación o cardinalidad clínica por prestación.

Los escenarios previos de aceptación (§28) se leen bajo esta aprobación: documentar una interrupción no habilita por sí solo Atendida ni finaliza automáticamente la visita; ciclo clínico aprobado SPA-ARQ-03 (§12), aplicación técnica del cierre administrativo (§8.7) y efectos comerciales pendientes.

Criterios candidatos de comprobación futura: cinco estados, sin `Reprogramada` como estado; evento de llegada con fecha/hora/usuario; inicio/fin independiente y Atendida respaldada por atención documentada; interrupción sin completado automático; situación derivada y fin de visita solo al terminar todas las citas; cancelación no propagada; reprogramación conserva identidad/Programada, metadatos completos, indicador e historial sin consumo. No son pruebas ejecutadas ni aprobación de mecanismos de persistencia/concurrencia.

### 8.7 SPA-TEC-01.1 y SPA-TEC-02.B — Prestación y cierre administrativo

Dirección aprobada: visita agrupa citas/bloques temporales profesionales y una prestación identificable se vincula a uno o varios bloques sin duplicación comercial. Situación de visita derivada; llegada/reprogramación eventos. Agregados/ids/claves/persistencia pendientes.

**Ocho reglas SPA-TEC-02.B aprobadas:**

1. Solo los cinco estados existentes: Programada, En atención, Atendida, Cancelada y Ausente.
2. Interrupción clínica no cierra cita automáticamente.
3. Usuario autorizado puede cerrar administrativamente hacia Atendida si hubo atención real aunque el procedimiento no concluyera.
4. Atendida significa atención efectiva y cierre administrativo, no necesariamente procedimiento clínico completado.
5. Clínica conserva Interrumpida/parcial; no convertir artificialmente a Finalizada.
6. Registrar responsable, fecha, motivo y referencia a atención.
7. Al cerrar, liberar ocupación futura sin modificar retroactivamente intervalo original ni borrar eventos.
8. Sin consumo automático; cupo comercial se rige separadamente por §11.5.

La correspondencia funcional antes pendiente queda resuelta expresamente; no sexto estado. Transiciones/permisos/efectos temporales detallados y pruebas futuras pendientes. La visita deriva su cierre de citas, no de supuesta finalización clínica.

## 9. Servicios y tratamientos

**Selección técnica F0 recomendada:** perfil por Item Service y catálogo Procedure con vínculos N:M operativos ordenados según ADR §5.11; duración efectiva explícita, sin composición comercial/cupo inferidos. O-11 pasa a representación recomendada para revisión, sin alterar Items.

SPA-ARQ-08 §4.2 regla 3 aprueba integración con Items Service y propiedad ERP de precio/impuestos; configuración operativa SPA, relación servicio–procedimiento y contratos técnicos pendientes.

**Qué hace:** define qué se vende y qué procedimiento se realiza. **Estado de la sección:** «servicio = Item `Service`» es VIGENTE-ERP; duración configurable aprobada SPA-ARQ-04 (§7.2 regla 2); resto PROPUESTO o PENDIENTE. **Implementable hoy:** no. **Bloqueos:** BL-01, BL-09 (§29.1).

| ID | Comportamiento contractual | Estado | Evid. |
|---|---|---|---|
| SPA-SV-01 / SPA-OPS-SRV-01 / DUP-14 | Todo servicio comercializable es un Item `Service` de la empresa; Spa no crea otro catálogo comercial. Precio e impuestos los resuelve el ERP | VIGENTE-ERP y APROBADO frontera SPA-ARQ-08 §4.2 regla 3; mecanismo pendiente | A + E/T |
| SPA-OPS-SRV-03 / SRV-04 | Duración configurable conforme §7.2 regla 2; representación numérica, sucursales/condiciones propias e instantánea de duración/nombre siguen candidatos técnicos | APROBADO funcional SPA-ARQ-04 solo duración; mecanismo PROPUESTO | A + T |
| SPA-OPS-SRV-02 | Perfil por Item Service y procedimientos operativos reutilizables mediante vínculo N:M ordenado, selección preferente ADR §5.11 | RECOMENDACIÓN TÉCNICA F0, no aprobación del propietario; implementación pendiente O-11 | T |
| Servicio comercial / procedimiento / tratamiento | El servicio es lo que se vende (Items); el procedimiento es lo que se realiza sobre la persona (Spa); tratamiento operativo ejecuta el plan individual/multisesión y su ciclo aprobado (§10); O-11 tiene selección preferente recomendada de perfil/procedimiento en ADR §5.11; permanece pendiente aprobación/implementación del esquema y los vínculos técnicos entre prestación comercial y bloques de cita según SPA-ARQ-01 | PROPUESTO | T |
| SPA-OPS-SRV-05 | Sucursales: por defecto atendible en todas las sucursales activas; restringir es una relación opcional. Qué se espera por defecto es pendiente | PROPUESTO / PENDIENTE | T, P |
| SPA-OPS-SRV-09 | Un servicio o procedimiento inactivo no admite citas nuevas y se conserva en el historial; nunca se elimina | PROPUESTO | T |
| SPA-OPS-SRV-10 | Condiciones de atención (precauciones, contraindicaciones, preparación): texto asociado al procedimiento; ningún campo es obligatorio por figurar en una ficha física | PROPUESTO | T |
| SPA-SV-02 | Datos por documentar: nombre y clasificación, duración, sucursal, recursos, profesionales elegibles, insumos y equipos, precio comercial, condiciones de venta, restricciones, estado e historial. **Ninguno declarado obligatorio** | PROPUESTO | T |
| SPA-OPS-SRV-11 / SRV-12 | Historial por Entity Audit de las entidades de servicio de Spa (Items conserva `ItemAudit`); la venta usa el Item `Service` y la facturación existente | PROPUESTO / VIGENTE-ERP | T, E |

**Flujos (PROPUESTOS):** configurar (elegir un Item `Service` activo de la empresa → completar duración, sucursales, especialidades y condiciones → activar); desactivar (deja de admitir citas nuevas; el historial no cambia). Casos excepcionales: Item que no es `Service`, Item inactivo, especialidad inexistente en Empleados, servicio ya configurado. Estados `Activo` e `Inactivo` (habilitar y deshabilitar; nunca eliminar). Un perfil por Item (`UNIQUE (company_id, item_id)`, propuesto). Permisos `spa.services.view` y `spa.services.manage` (§21.2); errores en §26.

## 10. Planes de tratamiento

**Qué hace:** describe lo previsto para la persona. «Plan de tratamiento» es el término del contrato; «plan de atención» (R1) es su sinónimo histórico (`G-07`, propuesta). **Estado de la sección:** plan individual y tratamiento multisesión con ampliación, suspensión, cancelación y cierre APROBADOS por SPA-DEC-116; campos [F] sin contraste y relación exacta con paquete pendientes. **Implementable hoy:** no. **Bloqueos:** BL-05, BL-09 (§29.1).

| ID | Comportamiento contractual | Estado | Evid. |
|---|---|---|---|
| SPA-AT-01 | El plan registra objetivo, procedimiento, productos y equipos, número previsto de sesiones, intervalos sugeridos, cuidados, precauciones, posible derivación y decisión (proceder, adaptar o posponer) | PLANIFICACIÓN HISTÓRICA RECUPERADA | F |
| SPA-AT-05 / DUP-09 | El plan de tratamiento describe lo previsto para la persona; el paquete comercial representa derechos de consumo derivados de una compra. **No se vinculan automáticamente**; relación explícita mediante prestaciones/contratos/documentos aprobada SPA-TEC-03 (§10.2) | APROBADO vínculo explícito SPA-TEC-03; representación técnica pendiente | T, P |
| SPA-AT-04 | Resultado final y cierre del tratamiento incluidos por SPA-DEC-116; cuándo se cierra, quién registra y condiciones/correcciones exactas pendientes | APROBADO resultado/cierre; detalle PENDIENTE | A + F, P |
| SPA-CARE-FAC-05 / COR-08 | La decisión (proceder, adaptar, posponer) es de un solo valor y **no ejecuta acciones automáticas** (no crea citas ni consume sesiones) | PROPUESTO | T |

### 10.1 Tratamiento individual y multisesión — APROBADO

SPA-DEC-116 aprueba plan individual, tratamiento multisesión, sesiones previstas/contratadas y cupos, ampliación, suspensión, cancelación, cierre y seguimiento de resultados. El plan recoge lo previsto para la persona; el tratamiento operativo lleva su ejecución y ciclo. El paquete vendido representa derechos comerciales; no es el plan ni la sesión realizada.

Flujo funcional: definir plan individual → organizar el tratamiento y sus sesiones previstas → documentar atenciones/evolución → ampliar, suspender, cancelar o cerrar según la situación real y las reglas de detalle por definir. La ampliación comercial se rige por §11.2 regla 7; modificar sesiones clínicas previstas no concede derechos comerciales por sí mismo; no se deducen cantidades, equivalencias o movimientos por el nombre de la operación. Los efectos comerciales requieren contrato oficial y política aprobada; Spa no emite facturas propias. Excepciones: falta de cupo/respaldo, interrupción o cambio de profesional; no se elige política sin evidencia.

**Pendiente exclusivamente de detalle:** condiciones/permisos de cada operación; origen/destino de estados; reanudación después de suspensión no aportada; cierre con sesiones pendientes, corrección de resultados, representación técnica del vínculo explícito aprobado §10.2, relación clínica/cupos según §11.5 y detalles restantes de §11.3/11.4. Ampliación/cancelación comercial sigue §11, sin reinterpretar como pendiente lo aprobado. Se reconocen operaciones aprobadas sin inventar un autómata ni nombres de clases/permisos. Las pruebas de aceptación de §28.4 cubren esas capacidades; transiciones y errores específicos se definirán antes de implementar.

### 10.2 SPA-TEC-03 — Plan, prestaciones y documentos oficiales

**APROBADO:** Plan SPA → Prestaciones del plan → Contratos comerciales SPA → Facturas/líneas oficiales Ventas → Información oficial Caja/CxC, por identificadores/referencias válidas. Un plan puede existir antes de contratación completa y relacionar varias facturas/contratos. Nunca vincular por coincidencia de BusinessPartner; O-15 mantiene creación explícita factura/línea fuera de autorización de Ventas.

Separar situaciones Propuesto, Contratado, Facturado, Pagado y Realizado: no son por inferencia una nueva máquina de estados. Pagos no son consumo y reservas no son sesiones realizadas.

SPA-TEC-03 regla 10: consultar por prestación servicios propuestos/contratados, documentos/líneas, estado de pagos disponible en ERP, sesiones reservadas/realizadas y derechos restantes. Contratos/permisos/navegación oficiales, sin duplicar pantallas Ventas/CxC. No porcentaje facturado sin importes comparables/asignaciones verificables; UI detallada posterior con componentes oficiales. Reglas comerciales §11.6.

## 11. Paquetes comerciales

**Selección técnica F0 recomendada:** ADR §5.11 concreta contrato/derechos por lote/vigencia, libro, compromiso por prestación y transferencia documentada sin mover expediente. §11.5/11.6 conserva reglas normativas, sin nueva regla fiscal ni documento financiero SPA.

Integración ERP según SPA-ARQ-08 §4.2 reglas 3–5; O-15/O-25 y reglas de SPA-ARQ-05 intactas, sin ampliar significado fiscal/SRI/crédito ni diseñar Anticipos.

**Referencia funcional principal de SPA-ARQ-05 — APROBADA POR EL PROPIETARIO el 2026-11-19.** Diez reglas literales y límites: [REG §16](SPA-V1.1-APPROVED-DECISIONS-01.md#16-spa-arq-05--tratamientos-y-paquetes-comerciales). Conserva O-15/O-25, SPA-DEC-116 y SPA-ARQ-01/02/03/04; no confunde derechos comerciales con plan, clínica ni disponibilidad profesional. **Implementable hoy: no.** O-13 parcialmente resuelta; BL-08/09 y pendientes concretos vigentes. Anticipos independiente O-14 (§20.4).

### 11.1 Decisión aprobada O-15 (alternativa A — creación explícita tras validar una factura autorizada)

Texto literal de los siete enunciados ([registro](SPA-V1.1-APPROVED-DECISIONS-01.md) §2.2):

1. **Spa crea explícitamente el contrato de paquete después de validar una factura autorizada.**
2. La creación **referencia la factura y su línea correspondiente**.
3. La creación se realiza **fuera de la transacción de autorización de Ventas**.
4. Deben **evitarse contratos duplicados** para una misma línea facturada.
5. **Un error de Spa no debe revertir la autorización de la factura.**
6. **Spa no crea facturas, CxC, cobros ni asientos contables.**
7. **No se aprueba la creación automática** mediante `SalesInvoiceAuthorizedEvent`.

**No aprobado por O-15:** el estado electrónico SRI requerido para crear el contrato; el comportamiento de los contratos ante anulaciones o notas de crédito; la política frente a facturas a crédito
pendientes de cobro; anticipos y prepagos (`O-14`); la necesidad y el diseño de una lista de facturas pendientes de generar contrato; qué significa «factura autorizada» para este fin.

### 11.2 Reglas funcionales aprobadas SPA-ARQ-05

| Regla | Materia | Alcance aprobado y límite |
|---|---|---|
| 1 | Composición | Paquetes individuales/combinados con servicios Items `Service` oficiales y cantidades independientes por servicio; sin intercambio libre. Sustitución autorizada distinta y trazable; sin duplicar catálogo. |
| 2 | Vigencia | Configurable por paquete, comienza al crear contrato, sin plazo predeterminado. Vencido impide nuevas reservas vinculadas y consumos; identificar citas futuras existentes para revisión, sin cancelarlas automáticamente. Conservar derechos/movimientos e historia clínica; cálculo, estado y ejecución programada pendientes. |
| 3 | Cupos y saldo | Saldo desde movimientos trazables. Distinguir contratadas por servicio, consumidas, disponibles, programadas y realizadas clínicamente. Programar/reprogramar valida cupo por servicio, también concurrentemente: no comprometer más sesiones futuras que disponibles. La cita ocupa temporalmente capacidad de programación, sin descontar saldo ni generar `Consumed`; reserva lógica, saldo derivado y locks pendientes. |
| 4 | Consumo | Finalizada puede originar consumo mediante movimiento comercial independiente sujeto a reglas autorizadas; nunca por sí sola automáticamente. Borrador, programación y reprogramación no consumen. Parcial/interrumpida exige resolución comercial autorizada previa; no duplicar ante reintentos. Acciones, permisos e idempotencia técnica pendientes. |
| 5 | Ausencias | Ausencia trazable no descuenta automáticamente ni genera sesión clínica, cargo, factura o cobro. V1.1 no incorpora penalización comercial automática por inasistencia; alternativa resuelta sin inferir otras penalizaciones. |
| 6 | Cancelaciones | Cancelar antes de consumir no descuenta; reprogramar tampoco. Cancelación posterior o modificación clínica no revierte consumo confirmado: corrección mediante compensación autorizada con motivo, autor y trazabilidad. Separar cita, tratamiento, contrato y documento comercial; sin cancelación automática de contrato ni devolución monetaria inferida. |
| 7 | Ampliaciones | Operación explícita autorizada con respaldo de documento comercial oficial; conserva contrato/historial, identifica servicio/cantidad y referencia documento. Movimientos trazables sin modificar históricos; Spa no crea factura/CxC/cobro/asiento. Relación con nuevas líneas pendiente; sin ampliaciones gratuitas ni saldo sin respaldo. |
| 8 | Sustituciones | Operación explícita por usuario autorizado, equivalencia definida, servicios/cantidades, motivo y trazabilidad. Sin conversión arbitraria ni alterar clínica histórica/movimientos confirmados. Equivalencias válidas documentadas y respaldo conforme §11.6 reglas 1–3; valores/evidencia particular y documento comercial adicional aplicable pendientes. |
| 9 | Devoluciones | NC/anulaciones solo en Ventas/procesos oficiales; Spa no emite/modifica documentos. Identificar contrato/sesiones, revisar derechos consumidos/pendientes y ajustar mediante operación autorizada con referencia y trazabilidad. No revertir clínica real. Fiscal/SRI, crédito, economía y decisiones específicas pendientes; sin devolución monetaria paralela. |
| 10 | Profesionales y sucursales | Uso con distintos profesionales habilitados y sucursales de la misma empresa, respetando vigencia, servicios, especialidad, disponibilidad, condiciones, permisos/restricciones y SPA-ARQ-04. Prohibido consumo entre empresas; movilidad no concede acceso clínico irrestricto ni nuevas capacidades Empleados. |

### 11.3 Las doce preguntas contractuales (`SPA-PQ-01`)

Las alternativas históricas se actualizan conforme a SPA-ARQ-05 y SPA-TEC-02/03; direcciones técnicas aprobadas §11.5, mecanismos físicos candidatos. O-13 no se cierra integralmente.

| Tema | Estado funcional vigente | Pendiente real o alternativa descartada |
|---|---|---|
| Venta | RESUELTA O-15; siete enunciados intactos §11.1 | SRI/crédito y alcance de factura autorizada pendientes; evento automático no aprobado |
| Composición | RESUELTA por servicio, sin intercambio libre | Sustitución solo con equivalencia y respaldo documentados §11.6 reglas 1–3; valores particulares por especificar |
| Uso | PARCIAL: resolución autorizada separada, parcial sin consumo automático | Cupo retenido hasta consumir/liberar con evidencia §11.5; mecanismos/condiciones económicas restantes pendientes |
| Saldo | PARCIAL: libro/capacidad por prestación, no por profesionales | Reserva lógica, cancelación previa/reprogramación y retención/resolución aprobadas §11.5; algoritmo/locks/esquema pendientes |
| Vigencia | PARCIAL: configurable desde creación y por derechos incorporados | §11.6 reglas 6/7: ampliación no extiende originales; extensión expresa respaldada, vencimiento/revisión manual aprobados; cálculo/persistencia pendientes |
| Reprogramación | RESUELTA O-25 y reglas 2/3: no consume; validar vigencia/cupo y disponibilidad §7 | Implementación concurrente y conservación ante fallo; sin separar motor de paquetes |
| Ausencia | RESUELTA para V1.1 regla 5: sin penalización comercial automática ni descuento automático | No crear cargos/facturas/cobros ni sesión clínica; no reabrir alternativa automática |
| Cancelación | PARCIAL regla 6: sin descuento previo ni reversión automática de consumo confirmado | Compensación autorizada trazable; detalles económicos/técnicos pendientes, sin devolución inferida |
| Devolución | PARCIAL regla 9: NC/anulación oficial y revisión/ajuste autorizado, clínica preservada | Tratamiento fiscal/SRI/crédito y ajustes económicos específicos; no cancelación automática del contrato |
| Transferencia | PARCIALMENTE RESUELTA SPA-TEC-03 regla 4 | No automática; excepcional autorizada con respaldo/derechos/identidad/consentimientos verificados, sin trasladar expediente; documentación jurídica específica pendiente |
| Profesional | RESUELTA regla 10: puede variar dentro de condiciones oficiales | Contratos técnicos con Empleados sin ampliación inferida |
| Sucursal | RESUELTA regla 10: diferentes sucursales de la misma empresa bajo restricciones | Consumo entre empresas prohibido; mecanismos de aislamiento y concurrencia pendientes |

### 11.4 Cupos, ampliaciones y sustituciones — alcance y mecanismos

Aplicar reglas 3/7/8 de §11.2 como definición normativa. Agenda conserva su motor central §7: disponibilidad profesional y saldo comercial son independientes, pero la cita vinculada exige validar cupo y vigencia sin movimiento de consumo. Varios bloques de un servicio (SPA-ARQ-01) no duplican prestación ni derechos; su representación de cupos es diseño pendiente, sin inferir una sesión comercial por bloque.

Trazabilidad anterior preservada: SPA-OPS-PKG-04 referencia factura/línea (O-15); no copiar importes/impuestos sigue propuesta. SPA-OPS-PKG-03/§4.9 exige evitar duplicados, índice candidato. SPA-OPS-PKG-01 propone `PackageSessionLedger` append-only y tipos `Granted`, `Consumed`, `Restored`, `Expired`, `Adjusted`: nombres/tipos/modelo físico no aprobados por SPA-ARQ-05. SPA-OPS-PKG-02: no duplicar consumo aprobado; llave `(contract_id, session_id)` candidata. SPA-OPS-PKG-05 aplica regla 10; SPA-OPS-PKG-06 aplica vencimiento regla 2, otras condiciones de contrato inactivo/devuelto no inferidas. SPA-OPS-PKG-07 conserva DIR-SSOT-01; SPA-OPS-VTA-02 usa Items `Service` oficiales sin nuevo catálogo, contratos comerciales técnicos pendientes.

`PackageDefinition`, `PackageContract`, `PackageSessionLedger`, estados `Activo`/`Agotado`/`Vencido`/`Cancelado`, permisos/errores §21/26 y mecanismos de compensación son candidatos técnicos. El libro representa cantidades, no saldos monetarios. Pendientes: algoritmo/reserva lógica/concurrencia/idempotencia, vencimiento/jobs, detalles específicos de equivalencias/documentos y referencias de nuevas líneas, requisitos jurídicos de transferencia, cálculo/persistencia de vigencias por derechos y resoluciones comerciales, compensaciones económicas y fiscal/SRI/crédito; dirección/reglas aprobadas §10.2/11.5/11.6; no autoriza diseño de Anticipos, seguridad, firma o conservación legal.

### 11.5 SPA-TEC-01.5 / SPA-TEC-02.C — Libro y cupos

Dirección aprobada: derechos por servicio, libro trazable, operaciones idempotentes, consumo independiente del cierre clínico y compensación mediante nuevos movimientos sin alterar históricos. Reserva lógica/capacidad por prestación comercial, no por cantidad de profesionales. Modelo físico/tipos/ids/locks pendientes.

| Regla TEC-02.C | Comportamiento aprobado |
|---|---|
| 1 | Derechos por servicio dentro de contrato. |
| 2 | Prestación programada reserva un cupo lógico, varios profesionales no multiplican reserva. |
| 3 | Cancelación antes de atención libera cupo sin consumo confirmado. |
| 4 | Reprogramación conserva compromiso, sin doble reserva. |
| 5 | Atención completada permite resolución autorizada de consumo separada del cierre clínico. |
| 6 | Parcial/interrumpida sin consumo automático. |
| 7 | Resolución comercial pendiente mantiene cupo retenido. |
| 8 | Resolución autorizada consume derecho correspondiente o libera cupo, con responsable/motivo/evidencia. |
| 9 | Ajustes monetarios/NC/compensaciones económicas solo en módulos comerciales oficiales. |
| 10 | Trazabilidad/idempotencia sin reescribir consumos anteriores. |

Aplicar condiciones comerciales; no convertir arbitrariamente parcial en prestación completa. Liberar tiempo de agenda (§8.7) no libera por sí solo cupo. Sin facturar/cobrar/modificar CxC desde SPA.

### 11.6 SPA-TEC-03 — Diez reglas comerciales

| Regla | Alcance aprobado y límite |
|---|---|
| 1 — Sustitución | Solo autorización/equivalencia documentada; sin conversión libre. |
| 2 — Equivalencia | Condiciones comerciales aplicables o autorización específica documentada; sin equivalencia válida rechazar. Valores concretos y documento adicional aplicable pendientes de especificación/verificación. |
| 3 — Historial | Original/sustituto, cantidades, motivo, responsable/autorización, fecha y documento respaldo; no ocultar sustitución alterando derechos/movimientos previos. |
| 4 — Beneficiario | Sin transferencia automática; excepción autorizada con respaldo comercial, alcance transferible e identidad/consentimientos verificados; nunca trasladar expediente. Documentación jurídica específica pendiente. |
| 5 — Ampliación | Servicios/sesiones con documento oficial válido; contrato, documento/línea, nuevos derechos e historial; no modificación retrospectiva que oculte ampliación. |
| 6 — Vigencia | No prolongar automáticamente derechos originales; extensión expresa aprobada/registrada/con respaldo; controlar vigencia de derechos incorporados por procedencia. Persistencia/cálculo pendientes. |
| 7 — Vencimiento | Sin nuevas reservas ni consumos ordinarios de derechos vencidos; citas previas a revisión manual sin cancelación automática; conservar compromisos/movimientos. |
| 8 — Cambios comerciales | Ventas posee devoluciones/NC/anulaciones; SPA consulta vinculados y ajusta derechos autorizado, sin alterar clínica ni documentos fiscales propios; tributación/efectos monetarios por validar. |
| 9 — Facturas/pagos/cuotas | Consulta oficial §20.5; no tablas financieras/calendarios/cobro/saldo paralelo. Cuotas pendientes no suspenden automáticamente derechos: restricción requiere política comercial expresamente aprobada. Elegibilidad fiscal/SRI pendiente. |
| 10 — Plan | Referencias y consulta §10.2; sin pantallas financieras duplicadas ni porcentaje inventado. |

**O-13 parcialmente resuelta:** relación plan/documentos, cupos retenidos/resolución, requisitos de sustitución/equivalencia y transferencia excepcional, ampliación sin extensión automática, vigencias por procedencia, vencimiento y cuotas sin suspensión automática están contestados. Solo detalles específicos de equivalencias/respaldo/transferibilidad, efectos económicos/fiscal/ley y diseño físico/cálculo/concurrencia siguen abiertos.

## 12. Sesiones realizadas y consumo

SPA-TEC-01.2 aprueba dirección de borradores editables, inicio/cierre explícitos, interrupciones/diferenciación completa/parcial, correcciones versionadas y consumo separado; diseño físico/retención legal pendientes. Cierre administrativo §8.7 no falsea clínica; cupos §11.5.

**Referencia funcional principal de SPA-ARQ-03 — APROBADA por el propietario el 2026-11-19.** Ocho reglas y modelo conceptual literales: [REG §14](SPA-V1.1-APPROVED-DECISIONS-01.md#14-spa-arq-03--ciclo-de-atención-clínica). Este ciclo documenta atención clínica; no sustituye visita/citas SPA-ARQ-01, los cinco estados de cita SPA-ARQ-02 (§8.6), ni el consumo comercial independiente O-25. No es implementación autorizada; siguen BL-01/02/03/05/06/08/09 y los detalles técnicos pertinentes de BL-10.

### 12.1 Formulario, borrador, finalización, interrupción y corrección

| Regla SPA-ARQ-03 | Comportamiento funcional aprobado | Límite exacto |
|---|---|---|
| 1 — Borradores | Preparar, guardar y editar formulario clínico incompleto; continuar su preparación sin acreditar sesión realizada | Guardar borrador no es atención completada ni consumo comercial; mínimos de datos y persistencia técnicos pendientes |
| 2 — Inicio | Al comenzar atención efectiva, registrar fecha/hora de inicio, profesional responsable y usuario ERP registrador separados, en el cliente/empresa/sucursal/contexto correctos | No confundir disponibilidad, llegada o mera programación con inicio clínico; directorio Empleados aún dependencia |
| 3 — Finalización | Acción explícita del profesional autorizado; conservar procedimientos, observaciones, productos usados, incidencias, resultados y evolución efectivamente documentados según servicio | No genera por sí misma factura, cobro, asiento ni movimiento de inventario; validaciones técnicas de finalización pendientes |
| 4 — Interrupción | Si un procedimiento iniciado se detiene, registrar motivo, momento y trabajo efectivamente realizado | Atención interrumpida no se marca completada; no inventar reapertura ni estado adicional |
| 5 — Atención parcial | Conservar atención efectiva parcial como registro clínico del expediente, diferenciada de sesión completada | No confundirla con cita programada/cancelada/ausente; número de sesiones clínicas por prestación y implementación del cierre administrativo (§8.7) pendiente |
| 6 — Correcciones | Corregir registros clínicos finalizados solo mediante operación autorizada con motivo, autor, fecha y trazabilidad de versiones | Versiones sujetas a política legal pendiente: C-02 no resuelto. Corrección no es nueva atención efectiva ni consumo adicional; mecanismo técnico no aprobado |
| 7 — Evolución | Toda atención documentada integra seguimiento del tratamiento, también interrupciones con atención parcial | Distinguir evolución de sesiones completadas frente a parciales; no generar diagnóstico automático ni resultado inexistente |
| 8 — Consumo independiente | Consumo comercial separado del registro clínico según O-25. Borrador no consume; Finalizada o parcial no determinan automáticamente cantidad a descontar | SPA-ARQ-05 §11.2 reglas 4–6 precisa consumo/ausencia/cancelación; O-13 conserva detalles comerciales/técnicos. No inferir descuento de sesión completa ni sesiones gratuitas |

### 12.2 Ciclo conceptual del registro clínico

| Concepto aprobado | Significado |
|---|---|
| Borrador | Formulario editable sin atención acreditada |
| En atención | Atención efectiva iniciada y en documentación |
| Finalizada | Atención completada y documentada mediante acción explícita del profesional autorizado |
| Interrumpida | Atención iniciada y detenida, con motivo, momento y alcance real documentados; no completada |

Se puede preparar/guardar/editar el borrador; cuando comienza atención efectiva se registra su inicio; una atención iniciada se finaliza explícitamente o se documenta su interrupción. Estas operaciones describen el ciclo aprobado, no una matriz adicional de reaperturas/reversas. Correcciones posteriores mantienen trazabilidad, sin nueva atención o consumo. No crear estados adicionales, anulaciones clínicas o transiciones de reapertura no aprobadas. No confundir Finalizada clínica con Atendida de agenda por mera equivalencia de nombres.

### 12.3 Consistencia con agenda, consentimiento y seguridad

SPA-ARQ-01/02 y O-25 permanecen intactas. La programación no acredita atención y cancelar/ausentarse no crea sesión clínica. **Actualización SPA-TEC-02.B:** cierre administrativo funcional resuelto en §8.7, mecanismo detallado pendiente. Una parcial interrumpida no asigna automáticamente Atendida ni un sexto estado de cita; la finalización de visita sigue derivándose de sus citas bajo SPA-ARQ-02. El número de bloques/profesionales tampoco decide número de registros clínicos por prestación.

Atender exige respetar autorizaciones, permisos y consentimientos aplicables; SPA-ARQ-03 no aprueba excepciones. Siguen abiertos validez jurídica de firma en pantalla, menores, conservación/eliminación y C-02, acceso excepcional Admin, cifrado/claves y auditoría técnica definitiva. La operación de corrección autorizada no convierte la conservación de versiones en prohibición absoluta de eliminación legal.

**Pendientes técnicos:** persistir borradores y registros, mínimos/validaciones por operación, mecanismos de autorización/versión, concurrencia/finalización y consistencia transaccional con agenda. **Alcance comercial vigente:** §11.2 reglas 4–6 resuelve no consumo automático, ausencia sin penalización automática y cancelación sin reversión automática; O-13 conserva detalles de §11.3/11.4. O-14 no se modifica. El ciclo funcional y conservación de parcial ya están aprobados y no vuelven a presentarse como alternativas por resolver.

### 12.4 Trazabilidad y datos anteriores conservados

SPA-AT-06/07 y O-25: atención documentada independiente de cita, no por llegar la fecha; ahora se distinguen completada/parcial conforme a SPA-ARQ-03. SPA-AR-03, SPA-SG-02 y SPA-CARE-EXP-05: profesional responsable/registrador separados, según ADR-050. SPA-CARE-CNS-05: interrupción/registro parcial resueltos funcionalmente en §12.1; efecto de paquete según §11.2 regla 4 (resolución autorizada de parcial) y estado final de cita aún pendiente. SPA-CARE-CNS-06 conserva dependencia de consentimiento aplicable, sin aprobar excepción. SPA-OPS-VTA-04: sin efectos comerciales/contables por registrar clínica. SPA-AT-03: fotografías de seguimiento vinculadas a valoración/atención privada (§18).

SPA-AT-02 conserva datos recuperados [F], sin declararlos obligatorios por aparecer en fichas: fecha, procedimiento, productos/aparatología, zona, evolución/observaciones, reacciones posteriores, próxima sesión sugerida y firma/identificación. Obligatoriedad/versión de fichas siguen O-21/O-24/V-01. SPA-OPS-PKG-01/G-14 conserva consumo como movimiento independiente; consumo sujeto a §11.2 regla 4 y detalles restantes O-13. Contadores candidatos anteriores: contratadas = Σ Granted; programadas = citas activas del contrato; consumidas = Σ Consumed − Σ Restored; pendientes = contratadas − consumidas. El contador de realizadas debe distinguir las completadas de las parciales según el criterio aún por concretar, sin equipararlo al consumo ni fijar saldo por clínica.

## 13. Expedientes estéticos

Versionado clínico §12, autorización reforzada §21.5 y auditoría durable §22.4; sin cambio de unicidad/scopes.

SPA-ARQ-07 integra valoraciones/mediciones, consentimientos y fotos en el expediente conforme §14–18; no altera unicidad/scopes ni permisos de SPA-ARQ-06. Correcciones según §17.1, sin clínica por mero borrador ni consumo comercial.

Aplicar §21.1 reglas 1/2/4/7/8 como referencia SPA-ARQ-06: expediente por cliente/empresa con sucursal por registro, autorización clínica contextual y corrección/conservación controladas; mecanismos y ley pendientes.

**Qué hace:** conserva de forma longitudinal la información específica de atención de una persona. **Estado de la sección:** expediente único por cliente y empresa, antecedentes/alertas/valoraciones/historial APROBADOS por SPA-DEC-116; profesional/registrador por ADR-050. Campos [F] siguen sin contraste (`V-01`). **Implementable hoy:** no. **Bloqueos:** BL-03, BL-05, BL-09 (§29.1).

| ID | Comportamiento contractual | Estado | Evid. |
|---|---|---|---|
| SPA-FC-01 / G-02 | El expediente es el registro longitudinal, propio de Spa, de la atención de un cliente (antecedentes versionados, valoraciones, sesiones, consentimientos y fotografías); referencia al cliente, no copia su identidad. Las fichas facial y corporal no exigen dos entidades separadas | PROPUESTO | F, T |
| SPA-FC-02 | La ficha contiene número de ficha, fecha, persona, edad y contacto, profesional, motivo de consulta y, en algunas versiones, código del caso y contacto de emergencia | PROPUESTO (qué campos proceden de BP y cuáles se conservan: PENDIENTE) | F, P |
| SPA-CARE-EXP-02 | Número de ficha único por empresa. A: secuencia propia con la infraestructura de secuencias existente; B: número libre digitado. Formato y reinicio pendientes | PENDIENTE (`O-19`) | P |
| SPA-CARE-EXP-03 | Código del caso: significado no recuperado (por tratamiento, por persona o por trabajo académico) | PENDIENTE (`O-19`) | P |
| SPA-CARE-EXP-04 | Expediente único por cliente y empresa; no varios por caso o sucursal | APROBADO; O-20 resuelta | A (SPA-DEC-116) |
| SPA-FC-03 / SPA-CARE-EXP-07 | Antecedentes: en cada categoría se distingue «sin responder», «no», «sí» y texto libre; «sin responder» no equivale a «no». Qué preguntas son obligatorias, opcionales o condicionales es pendiente | PROPUESTO / PENDIENTE (`O-21`) | F, T |
| SPA-CARE-EXP-06 / SPA-FC-08 / DUP-06 | Aplicar §21.1 regla 7: corrección trazable y versión sustituida restringida/no vigente. Append-only y almacenamiento de versiones siguen candidatos sujetos a C-02/O-05 | APROBADO objetivo SPA-ARQ-06; mecanismo/ley pendientes | A + T |
| SPA-CARE-EXP-08 | No eliminación arbitraria; aplicar §21.1 regla 7 y C-02: objetivo aprobado, procedimiento/validación legal pendientes, sin prohibición absoluta que resuelva derechos por sí sola. Inactivación/cierre y sus condiciones siguen candidatos | PROPUESTO / BLOQUEADO | T |
| SPA-CARE-EXP-10 | Expediente de empresa, sucursal por atención; acceso intersucursal según §21.1 regla 4 | APROBADO funcional SPA-ARQ-06; autorización técnica O-02 pendiente | A + P |
| SPA-CARE-EXP-05 | Cada valoración y sesión referencia al profesional responsable, al usuario registrador y a la sucursal | APROBADO (la distinción); campos PROPUESTOS | A |

**Alertas incluidas (SPA-DEC-116):** el expediente conserva alertas junto con antecedentes y valoración, planes/tratamientos, sesiones/evolución, medidas, fotos privadas, consentimientos e historial. No se recuperaron categorías, niveles, origen manual/automático, obligatoriedad ni bloqueos derivados: no inventarlos ni declarar pendiente la capacidad. Consulta sin duplicar identidad de BusinessPartner; privacidad y controles oficiales.

### 13.1 Datos y antecedentes recuperados que se preservan

**PLANIFICACIÓN HISTÓRICA RECUPERADA [F], sin contraste directo (R1 SPA-FC-02/03).** Datos generales: nombre, identificación, teléfono, ciudad; las fichas extensas añaden nacimiento, dirección y correo; algunas versiones, edad, código del caso y contacto de emergencia. Se leerán del propietario oficial cuando existan allí; el almacenamiento de los restantes y las instantáneas históricas sigue pendiente (O-06), sin extender BusinessPartner por este documento.

Categorías conservadas: enfermedades o condiciones relevantes; medicamentos; alergias; embarazo o lactancia cuando corresponda; cirugías y tratamientos anteriores; implantes y procedimientos en la zona; irritación, infección, heridas o reacciones recientes; salud y estilo de vida. Las variantes antiguas incluyen tabaquismo, alcohol, actividad física, depilación y ciclo menstrual. No se descartan esas preguntas por no tener confirmación literal recuperada; su versión vigente, obligatoriedad y pertinencia por procedimiento quedan en O-21/O-24. Las respuestas sin contestar se distinguen de las negativas; el control de respuesta y las versiones son propuestas técnicas (§13).

Modelo candidato: `CareRecord` (empresa, cliente, número de ficha, código del caso, estado, fecha de apertura), `MedicalHistoryVersion` (append-only), `CareRecordPersonalData` (solo si se decide, `O-06`).
Estados: `Activo` e `Inactivo`. Flujos: abrir (cliente activo con rol Customer + número de ficha + versión inicial opcional), consultar (exige permiso de Spa; `employees.view` no basta), corregir con motivo,
inactivar. La lista de clientes no muestra información clínica (§21).

## 14. Valoración facial

**Qué hace:** registra la valoración estética inicial del rostro. **Estado de la sección:** contenido funcional SPA-ARQ-07 aprobado (§14.1), campos adicionales [F] preservados sin contraste V-01; obligatoriedad específica pendiente O-21. **Implementable
hoy:** no. **Bloqueos:** BL-01, BL-05, BL-09 (§29.1).

| ID | Comportamiento contractual | Estado | Evid. |
|---|---|---|---|
| SPA-FC-04 / SPA-CARE-FAC-01 | La valoración pertenece a un expediente y fija fecha de negocio (`DateOnly`), profesional responsable, usuario registrador y sucursal | PROPUESTO | F, T |
| SPA-CARE-FAC-03 | Una valoración estética **no** se rotula ni exporta como diagnóstico médico; «diagnóstico médico previo» es información declarada, no un diagnóstico de Spa | APROBADO no diagnóstico automático §14.1; alcance de rótulo/exportación histórico | A + F |
| SPA-CARE-FAC-04 | Fototipo cutáneo: un valor de Fitzpatrick I a VI o «sin evaluar» | APROBADO Fitzpatrick I–VI cuando evaluado (§14.1); representación sin evaluar candidata | A + F/T |
| SPA-CARE-FAC-02 | Una valoración es un registro con versiones append-only; corregir crea una versión nueva con autor, fecha y motivo | APROBADO corrección trazable §17.1; append-only candidato sujeto C-02 | A + T |
| SPA-CARE-FAC-06 | Mínimos de integridad: solo los de la regla de lectura 3 (§0.3); los campos clínicos son opcionales hasta decidir lo contrario | PROPUESTO / PENDIENTE (`O-21`) | T, P |
| SPA-CARE-FAC-07 | Si la ficha antigua y la actualizada difieren, la versión digital no asume equivalencia; el contraste de versiones es previo al contrato | PENDIENTE (`O-24`) | P |

Campos recuperados [F] (ninguno declarado obligatorio): motivo de consulta; condición de piel (normal, seca, grasa, mixta); sensibilidad; fototipo; alteraciones observadas (manchas, acné, deshidratación y
otras); zona y tiempo de evolución; exposición solar y protección; observaciones del profesional; diagnóstico médico previo; objetivo del tratamiento; procedimiento, productos y equipos previstos;
precauciones; decisión (proceder, adaptar, posponer). La evolución se registra por sesión (§12), no en la valoración. Estados técnicos candidatos: `Registrada` y versiones (`Corregida`); cierre pendiente. Borrador aprobado en §14.1, sin aprobar estados físicos. **Flujo funcional y mecanismos candidatos:** preparar/guardar/continuar valoración incompleta según §14.1, registrar decisión y corregir según §17.1. Permisos `spa.assessments.create`, `spa.assessments.update` y de consulta `spa.records.view`; errores sin catálogo recuperado (§26).

### 14.1 SPA-ARQ-07 — Valoración facial y borradores (reglas 1/5)

**APROBADA**, texto literal en [REG §18](SPA-V1.1-APPROVED-DECISIONS-01.md#18-spa-arq-07--valoraciones-consentimientos-y-fotografías). La valoración permite antecedentes relevantes, tipo/condición de piel, sensibilidad, Fitzpatrick I–VI cuando se evalúe, alteraciones observadas, precauciones/contraindicaciones y objetivos; conserva los campos adicionales anteriores. Sin diagnóstico médico automático ni obligatoriedad universal: requisitos específicos dependen de procedimiento/reglas aprobadas.

Permitir guardar valoraciones incompletas como borradores y continuarlas. Guardar no acredita atención realizada ni genera consumo; coherencia con SPA-ARQ-03 sin equiparar borrador de valoración y sesión clínica finalizada. Persistencia, mínimos/validaciones por procedimiento y estados técnicos siguen pendientes. Correcciones §17.1; seguridad §21.4.

## 15. Valoración corporal

**Gate F0:** numeric(10,2)/decimal? sigue recomendación preferente, no escala definitiva. ADR §5.15 contrasta estándar oficial y explica revisión formal pendiente; resolución de instrumento/rangos no probados.

**Qué hace:** registra la valoración corporal, sus mediciones y su comparación. **Estado de la sección:** medidas/unidades/finalidad/comparación SPA-ARQ-07 aprobadas (§15.1), campos adicionales [F] sin contraste; escala numérica PENDIENTE de revisión arquitectónica
(`V-04`, `O-22`). **Implementable hoy:** no. **Bloqueos:** BL-01, BL-05, BL-07, BL-09 (§29.1).

| ID | Comportamiento contractual | Estado | Evid. |
|---|---|---|---|
| SPA-FC-05 / SPA-CARE-COR-01 | La valoración corporal pertenece a un expediente y fija fecha de negocio, profesional responsable, usuario registrador y sucursal | PROPUESTO | F, T |
| SPA-FC-06 / SPA-CARE-COR-02 | Una medición es un conjunto (`BodyMeasurementSet`) con fecha de negocio (`DateOnly`) y tipo `Inicial`, `Control` o `Final`; cada medida es opcional dentro del conjunto | APROBADO finalidad/opcionalidad §15.1; BodyMeasurementSet/DateOnly candidatos | A + F/T |
| SPA-CARE-COR-03 | Un conjunto no se sobrescribe: corregirlo crea otro conjunto o una versión nueva con motivo, autor y fecha | APROBADO trazabilidad §17.1; conjunto/versionado físico candidato | A + T |
| SPA-CARE-COR-04 | Las diferencias entre conjuntos son **derivadas en lectura** y no se almacenan | PROPUESTO | T |
| SPA-CARE-COR-05 / SPA-INV-10 | Los valores numéricos y fechas respetan los estándares oficiales; «no medida» se distingue de cero. **El estándar de precisión no tiene una clase para medidas de peso o contorno**: una columna decimal nueva exige coincidir con una fila de la tabla de escalas o pasar revisión arquitectónica formal. No se propone escala | APROBADO no medida ≠ cero §15.1; precisión/escala PENDIENTE O-22/V-04 | A + T/NV |
| SPA-CARE-COR-06 / COR-07 | Frecuencia de medición, vínculo obligatorio u opcional con una sesión, número de controles y unicidad del «final»: no recuperados | PENDIENTE (`O-22`) | P |

Mediciones [F]: peso (kg); brazo derecho e izquierdo, abdomen alto, medio (a la altura del ombligo) y bajo, cadera, muslo derecho e izquierdo (cm). Otros campos [F]: zona corporal, motivo, objetivos
(relajación, reducción, tonificación u otros), hallazgos iniciales, procedimiento, aparatología, precauciones, decisión. La tabla comparativa es de solo lectura; una gráfica es pendiente. **Flujos (PROPUESTOS):** registrar la valoración inicial y la medición `Inicial` → registrar controles → registrar la medición `Final` → consultar la comparación. Estados: `Registrada` y versiones; la comparación no es un estado. Permisos en §21.2; errores sin catálogo recuperado (§26).

### 15.1 SPA-ARQ-07 — Medidas y finalidad (reglas 2/3)

**APROBADO:** peso en kilogramos; brazo derecho/izquierdo, abdomen superior/medio/inferior, cadera y muslo derecho/izquierdo en centímetros. Denominaciones ya documentadas: abdomen alto = superior, medio a altura del ombligo e inferior = bajo; conservar las tres, nunca sustituirlas por una única «cintura». Campos adicionales anteriores preservados.

Identificar finalidad Inicial, Control o Final; conservar fecha, contexto de expediente y profesional responsable. Campos no medidos pueden quedar vacíos: **no medida no equivale a cero**. Sin todas las medidas obligatorias, cantidad fija de controles ni valores/rangos/decimales inferidos. Precisión/escala requieren estándar oficial/revisión V-04 antes de persistir; frecuencia concreta y final único siguen sin decidir. Comparación §16; borradores §14.1; correcciones §17.1.

### 15.2 SPA-TEC-01.9 — Precisión preferente, no definitiva

Propuesta arquitectónica preferente aprobada: decimal? y candidato numeric(10,2) para kg/cm opcionales, ausencia distinta de cero y comparación sin error binario. **numeric(10,2) no es estándar definitivo aprobado**. Completar revisión formal V-04 de dominio, resolución necesaria, almacenamiento y límites antes de implementar; no escala por analogía fiscal.

## 16. Seguimiento y evolución

Cierre administrativo §8.7 conserva evolución parcial/interrumpida sin falsa Finalizada ni consumo. Propuesta §15.2 no cierra V-04.

**SPA-ARQ-07 regla 4 — APROBADA:** consultar cronológicamente valoraciones y mediciones, distinguiendo Inicial/Control/Final; mostrar variaciones documentadas solo cuando haya valores comparables. No inventar valores/resultados ante mediciones faltantes, atribuir automáticamente cambios a un procedimiento ni emitir diagnóstico médico. Cálculo en lectura o almacenamiento son candidatos técnicos.

**SPA-ARQ-03, regla 7 — APROBADA:** aplicar §12.1: toda atención efectivamente documentada integra el seguimiento, incluidas interrupciones con atención parcial; distinguir evolución completa y parcial. Correcciones autorizadas siguen §12 sin nueva atención ni consumo. Presentación y almacenamiento son detalles técnicos, no aprobación de estados nuevos.

**Qué hace:** permite comparar la evolución de una persona a lo largo de sus atenciones. **Estado de la sección:** seguimiento/evolución APROBADOS como alcance por SPA-DEC-116; datos [F] y mecanismos de comparación conservan su evidencia y detalle pendiente. **Implementable hoy:** no. **Bloqueos:** BL-05, BL-09 (§29.1).

| ID | Comportamiento contractual | Estado | Evid. |
|---|---|---|---|
| SPA-AT-02 | La evolución se registra por sesión (fecha, procedimiento, productos, aparatología, reacciones, próxima sesión sugerida) | PLANIFICACIÓN HISTÓRICA RECUPERADA | F |
| SPA-AT-03 | La comparación fotográfica exige condiciones comparables (iluminación, distancia, ángulo, sin filtros), registradas como nota de captura; no se verifican automáticamente | PROPUESTO | F, T |
| SPA-CARE-COR-04 | La comparación de mediciones es derivada en lectura | PROPUESTO | T |
| SPA-AT-04 | Resultado/evolución final incluidos por SPA-DEC-116; reglas de cierre/corrección §10 | APROBADO capacidad; detalle PENDIENTE | A + F, P |
| SPA-CL-01 | Consulta de la evolución del cliente sin mezclar registros de clientes diferentes | PROPUESTO | F, T |

## 17. Consentimientos y firmas

**Qué hace:** conserva la evidencia de que la persona fue informada y decidió, y distingue cada autorización. **Estado de la sección:** consentimiento por procedimiento, firma en pantalla, versiones y autorizaciones específicas APROBADOS por SPA-DEC-116. Menores, académico, conservación y validez legal siguen pendientes; no se confunde firma en pantalla con firma electrónica certificada. **Implementable hoy:** no. **Bloqueos:** BL-03, BL-05, BL-06, BL-09 (§29.1).

| ID | Comportamiento contractual | Estado | Evid. |
|---|---|---|---|
| SPA-CN-06 / SPA-CARE-CNS-01 / SPA-INV-05 / DUP-04 | Aplicar §17.1: procedimiento, seguimiento y promoción independientes; académico no aprobado, O-17 abierto | APROBADO tres finalidades SPA-ARQ-07; mecanismo/vigencia pendientes | A + P |
| SPA-CN-01 | Documento previo y registro de procedimiento/versión/decisión/firma/identificación-trazabilidad según §17.1 | APROBADO funcional SPA-ARQ-07; captura/autenticidad/ley pendientes | A + P |
| SPA-CN-02 / SPA-CN-03 | Fotografías de seguimiento y publicación promocional son autorizaciones diferenciadas; la primera no autoriza publicación | APROBADO SPA-ARQ-07 §17.1 | A + F |
| SPA-CARE-CNS-02 | Versiones de consentimiento incluidas; entidades `ConsentDefinition`/`ConsentRecord`, vínculo al texto presentado e inmutabilidad como diseño técnico | APROBADO versiones; modelo PROPUESTO | A (SPA-DEC-116) + T |
| SPA-CARE-CNS-03 | Corrección/decisión/retiro según §17.1; inmutabilidad absoluta no resuelve eliminación legal C-02 | APROBADO trazabilidad; registro nuevo/almacenamiento candidatos | A + T |
| SPA-CN-05 / SPA-CARE-CNS-04 | **Retiro:** una autorización retirada deja de habilitar usos futuros; no revierte atenciones ni documentos ya emitidos; el material ya publicado fuera del ERP no es controlable por el ERP. Efectos sobre sesiones y material publicado: pendientes. La persona puede detener una sesión | PROPUESTO / PENDIENTE | F, T, P |
| SPA-CN-04 / SPA-CARE-CNS-07 | **Uso académico:** autorización separada solo si el propietario confirma que pertenece al producto; hasta entonces no se implementa ni se asume | PENDIENTE (`O-17`) | F, P |
| SPA-CARE-CNS-08 | Quién consiente: la persona atendida; representante o menores no recuperado | PENDIENTE (`O-18`) | P |

**O-16 parcialmente resuelta:** firma en pantalla y versiones APROBADAS; no se ofrecen documento escaneado o aceptación sin firma como sustitutos de ese requisito. Falta mecanismo de captura/identidad/autenticidad y evidencia, reglas de vigencia por procedimiento y conservación legal. Las alternativas previas de hash/metadatos o referencia de versión siguen propuestas de evidencia, no aprobaciones jurídicas. La validez legal de cada firma **no está verificada** (`L-INT-05`). Texto de consentimiento y entidades `ConsentWithdrawal` y `SessionInterruption` (§24). Estados: registro `Vigente` o
`Retirado`; definición `Vigente` o `Reemplazada`. **Flujos (PROPUESTOS):** presentar la versión vigente → registrar la decisión y la evidencia → habilitar la atención; retirar; registrar la interrupción de una sesión. Permisos `spa.consents.manage` y `spa.consents.templates`; errores sin catálogo recuperado (§26).

### 17.1 SPA-ARQ-07 — Consentimiento, finalidades y correcciones (reglas 6/7/9)

Cuando el procedimiento requiera consentimiento, presentar su documento antes de realizar la atención. Registrar procedimiento, versión presentada, decisión de persona autorizada, firma en pantalla e identificación/trazabilidad aplicables. Firma en pantalla ya aprobada SPA-DEC-116; autenticidad, validez jurídica, vigencia e identificación específica pendientes; no equivale automáticamente a firma electrónica certificada. No habilitar atención que requiere consentimiento sin uno válido según reglas aplicables; no inventar vigencias ni excepciones.

Autorizaciones independientes: realizar procedimiento, capturar/utilizar fotos de seguimiento y utilizar fotos/información promocional. Aceptar una no implica las demás; respetar finalidad al consultar/usar material. **Académico O-17 sigue abierto y no se incorpora como capacidad aprobada**; menores/representantes O-18 pendientes.

Valoraciones, mediciones y consentimientos conservan correcciones/cambios con autor, momento y motivo; no sobrescribir silenciosamente evidencia anterior ni presentar sustituido como vigente. Retiro/rectificación respeta finalidad y ley; conservación/rectificación/supresión/anonimización definitiva requiere validación jurídica, sin conservación indefinida de datos legalmente suprimibles (C-02/O-05, §21.1/22). Versionado físico, evidencia de firma y procedimientos definitivos no aprobados.

## 18. Fotografías y archivos

**Selección técnica F0 recomendada:** ADR §5.14 concreta adaptador privado, autorización por objeto, rutas controladas y cifrado autenticado por archivo; algoritmo/clave/proveedor son propuesta nueva sin aprobación del propietario, operación/restauración pendientes, infraestructura intacta.

**SPA-ARQ-07 regla 8 — APROBADA:** vincular fotografías a expediente y, cuando corresponda, valoración/sesión. Registrar fecha de captura, contexto de atención, finalidad, autorización aplicable y profesional/usuario responsable según corresponda. Privadas bajo SPA-ARQ-06 (§21), sin enlaces públicos permanentes ni publicación automática; finalidad según §17.1. Almacenamiento físico, cifrado/claves y mecanismos definitivos pendientes ADR-051.

Aplicar §21.1 reglas 5/6/7/8 para auditoría, privacidad, cifrado adecuado y claves, aislamiento y gestión legal. El diseño técnico/operativo de protección permanece pendiente; IFileStorage/LocalFileStorage/scripts intactos, sin sistema nuevo de backups.

**Qué hace:** conserva fotografías de seguimiento con su finalidad y autorización y controla quién las ve. **Estado de la sección:** fotografías privadas, acceso y trazabilidad APROBADOS por SPA-DEC-116; mecanismos de almacenamiento dependen de decisiones abiertas
(`O-03`, `O-04`, `O-05`). **Implementable hoy:** no. **Bloqueos:** BL-02, BL-03, BL-06, BL-09 (§29.1).

| ID | Comportamiento contractual | Estado | Evid. |
|---|---|---|---|
| SPA-CARE-FOT-01 | Metadatos/asociaciones según §18 regla 8, vínculo a valoración/sesión cuando corresponda | APROBADO SPA-ARQ-07; modelo físico pendiente | A + P |
| SPA-CARE-FOT-02 / SPA-SEC-INV-03 | Archivo privado APROBADO; adaptador `ICareFileStore` sobre `IFileStorage`, identificador, ruta del servidor y endpoint autorizado son mecanismos candidatos; sin URL pública | APROBADO privacidad; mecanismo PROPUESTO | A (SPA-DEC-116) + T |
| SPA-CARE-FOT-03 / SPA-SEC-INV-02 | Respetar finalidad/autorización §17.1 y acceso privado §18/21; académico sigue O-17, no capacidad aprobada | APROBADO funcional SPA-ARQ-07; mecanismos/ley pendientes | A + P |
| SPA-CARE-FOT-04 | Ninguna fotografía se publica, exporta ni comparte automáticamente; toda exportación es una acción explícita con permiso propio | APROBADO no publicación automática SPA-ARQ-07 §18; exportación/compartir y permiso específicos candidatos | A + T |
| SPA-CARE-FOT-05 | Las fotografías no aparecen en listados ni en la agenda; solo dentro del expediente y con permiso | PROPUESTO | T |
| SPA-CARE-FOT-06 / SPA-SEC-AUD-01 | Cada consulta o exportación queda en el registro de accesos (§22) | APROBADO acceso sensible auditable §21.1 regla 5; detalle exportación/mecanismo candidato | A + T |
| SPA-CARE-FOT-07 / SPA-SEC-INV-04 | Las fotografías se desactivan lógicamente; no se invoca `DeleteAsync` sobre ellas salvo el procedimiento de eliminación legal aprobado | PROPUESTO / BLOQUEADO (`C-02`) | T |
| SPA-CARE-FOT-08 | Retiro de la autorización de seguimiento: conservar y bloquear, anonimizar o eliminar | PENDIENTE (`O-05`) | P |
| SPA-CARE-FOT-09 a FOT-10 | Metadatos del dispositivo, formatos, tamaño máximo, compresión y número de fotografías por atención: no recuperados | PENDIENTE | P |
| SPA-CARE-FOT-11 | Las condiciones comparables se registran como nota de captura | PROPUESTO | F, T |
| SPA-FO-01 | La especificación de seguridad de imágenes cubre almacenamiento, referencias, permisos de lectura, acceso a originales, exportaciones, registro de accesos, retención, revocaciones y errores | PROPUESTO | T |

**Protección técnica (mecanismos de R4 §5 no aprobados; objetivo de archivos privados sí aprobado por SPA-DEC-116):** `LocalFileStorage` sobrescribe, borra físicamente, no valida contención de ruta ni cifra, y comparte volumen con certificados y keyring; Spa no
lo modifica. Recomendación técnica no aprobada: referencias privadas, lectura autorizada y auditada, rutas del servidor, hash SHA-256 del contenido, ausencia de borrado físico, y protección/cifrado de los respaldos existentes con
clave separada solo si se aprueba ese detalle, sin desarrollar sistema nuevo (HX-03) (`SPA-SEC-INV-05`); objetivo de cifrado adecuado aprobado en §21.1 regla 6; cifrado de aplicación por archivo, cifrado de sobre y clave maestra externa siguen candidatos. Cifrado de campos clínicos y su secuencia de adopción pendientes de revisión, sin diferir protección necesaria para operar datos reales.
Cifrado de disco y publicación del directorio de archivos: `V-07` (verificación con infraestructura). Decisión `O-04`.

Alternativas de protección por capa (sin elegir; R4 §5.3–5.4): transporte, TLS en la terminación del despliegue (pendiente en el runbook); archivos en reposo, A cifrado de disco/volumen de infraestructura (no verificado), B cifrado de aplicación por archivo con clave de datos por archivo y clave maestra, C almacén de objetos con cifrado del servidor (futuro); campos clínicos, A sin cifrado de campo (permisos y auditoría), B cifrado de columnas con conversor de EF (se pierde búsqueda y orden), C cifrado de disco del servidor de base de datos; claves, A Data Protection con keyring en el mismo volumen, B clave maestra en gestor de secretos o KMS externo, C clave por expediente (permite borrado criptográfico); respaldos, cifrado con clave guardada aparte o sin cifrar (hoy). El keyring comparte volumen y respaldo con los archivos, por lo que cifrar con él no protegería el respaldo: la clave maestra debe vivir separada de datos y respaldos. Perder las claves implica perder los datos (custodia, rotación e identificador de versión por objeto). No modificar `IFileStorage`/`LocalFileStorage`: adaptador propio `ICareFileStore`.

**Flujos (PROPUESTOS):** subir una fotografía a una atención (comprobando el consentimiento de seguimiento vigente) → consultar dentro del expediente → comparar un grupo → exportar con permiso → desactivar. Estados: `Activa` e `Inactiva`; el bloqueo por retiro de autorización es pendiente (`SPA-CARE-FOT-08`). Permisos `spa.photos.view`, `spa.photos.create` y `spa.records.export`; errores sin catálogo recuperado (§26).

### 18.1 SPA-TEC-01.7 — Dirección de almacenamiento aprobada

Adaptador específico SPA sobre mecanismos oficiales autorizados, identificadores opacos, rutas controladas/validadas y autorización por recurso en cada descarga; sin enlaces públicos permanentes, cifrado adecuado, claves/restauración seguras. No elegir proveedor, algoritmo, esquema final de claves ni cambios compartidos. Custodia/cifrado efectivo/recuperación/restauración/respaldos requieren diseño operativo/validación. Sin sistema nuevo de respaldos.

## 19. Equipos e insumos

Aplicar SPA-ARQ-08 §4.2 regla 6: registro técnico sin Kardex automático y venta física oficial. Campos/detalles y eventuales operaciones adicionales O-12 no resueltos por esta aprobación.

**Qué hace:** documenta los productos y equipos usados en cada atención. **Estado de la sección:** productos usados y venta de productos oficiales incluidos por SPA-DEC-116; registro documental [F], **sin descuento automático de Kardex** (HX-05). **Implementable hoy:** no. **Bloqueos:** BL-09 (§29.1).

| ID | Comportamiento contractual | Estado | Evid. |
|---|---|---|---|
| SPA-IN-01 | Productos utilizados incluidos por SPA-DEC-116; formularios registran productos/equipos usados, sin calendario de disponibilidad ni Kardex automático | APROBADO productos; campos [F] sin contraste | A + F |
| SPA-IN-02 / SPA-OPS-SRV-07 / DUP-12 | El registro de insumos es documental y no genera movimientos de inventario; cualquier movimiento exige un contrato aprobado. Alternativas sin elegir: A registro documental por sesión; B lista sugerida por procedimiento; C salida manual con los ajustes de inventario existentes; D consumo automático expresamente excluido (HX-05, N-04) | APROBADO documental/sin Kardex §4.2 regla 6; listas sugeridas/salida manual adicional O-12 pendientes | A + T/P |
| SPA-OPS-SRV-08 / SPA-IN-03 | Aparatología: texto o referencia de identificación por procedimiento y por sesión; sin módulo de activos (N-05) | PROPUESTO | T |

## 20. Integración con Ventas, Caja y CxC

SPA-ARQ-08 §4.2 reglas 4/5 fija propietarios y consultas autorizadas sin documentos financieros ni saldo monetario SPA. Anticipos §20.4 permanece proceso transversal independiente, O-14 abierto.

**Qué hace:** relaciona la atención y el paquete con los documentos comerciales sin duplicarlos. **Estado de la sección:** integración de servicios, facturas, CxC/cobros y venta oficial de productos APROBADA por SPA-DEC-116; frontera y origen del contrato por O-15; anticipos y prepagos PENDIENTES (`O-14`). **Implementable hoy:** no. **Bloqueos:** BL-09 y pendientes específicos de O-15; BL-04 solo para funciones dependientes de anticipos (§29.1).

### 20.1 Capacidades existentes y no existentes del ERP

| Capacidad | Existe | Consecuencia para Spa |
|---|---|---|
| Vender un servicio | **Sí:** factura con Item `Service` sin bodega; la factura nace ligada a una sesión de caja (`CashSession`, de Caja; distinta de la sesión realizada de Spa) | Spa referencia la factura; no toca Caja |
| Pago parcial al facturar | **Sí:** medios no-Crédito y el saldo como CxC | Nada nuevo |
| Cobrar un saldo posterior | **Sí:** cobro aplicado a una CxC existente | Nada nuevo |
| Devolver o anular una venta | **Sí**, con las reglas de Ventas (un solo camino: anular o devolver; un comprobante autorizado por el SRI no se anula, se corrige con Nota de Crédito) | Aplicar §11.2 regla 9: revisión/ajuste autorizado; efectos específicos fiscales/crediticios y económicos pendientes |
| Facturar por sesión | No | N-01 |
| Anticipo o prepago de cliente; cobro sin CxC; saldo a favor | **No** (solo existe anticipo de **proveedor**, ADR-035) | N-02 |
| Reembolso de devolución | Solo en efectivo o contra CxC | N-03 |
| Vendedor y cobrador distintos del profesional | No (Ventas FROZEN) | N-08 |

### 20.2 Reglas

| ID | Comportamiento contractual | Estado | Evid. |
|---|---|---|---|
| SPA-OPS-VTA-01 / SPA-AR-05 / SPA-INV-08 | Spa **no** crea facturas, CxC, cobros ni asientos contables | APROBADO (`O-15` #6) | `O-15` |
| SPA-OPS-VTA-01 (complemento) | Spa referencia documentos de Ventas y CxC por identificador y no registra movimientos de Caja (X-08) | APROBADO frontera SPA-ARQ-08 §4.2 regla 5; contratos técnicos pendientes | A + T |
| SPA-OPS-VTA-02 / SPA-OPS-PKG-04 | El paquete utiliza Items `Service` oficiales conforme §11.2 regla 1; el contrato se crea explícitamente tras validar una factura autorizada, referenciando factura y línea (§11) | APROBADO origen/referencia O-15 y uso Items `Service` SPA-ARQ-05; mecanismo pendiente | T + `O-15` |
| §11.1 #3, #5 | La creación es una operación propia de Spa, fuera de la transacción de autorización de Ventas; un fallo de Spa no revierte la factura | APROBADO (`O-15` #3, #5) | `O-15` |
| SPA-OPS-VTA-05 / SPA-VT-01 | El profesional que presta el servicio y el usuario que registra u opera la transacción se identifican por separado; vendedor y cobrador son pendientes | APROBADO-DERIVADO + PENDIENTE | A-der |
| SPA-OPS-VTA-06 | Aplicar §11.2 regla 9: documento oficial, revisión de derechos y ajuste autorizado trazable sin revertir clínica real | APROBADO funcional SPA-ARQ-05; detalle fiscal/económico/técnico pendiente | A + P |
| Validación de la creación | Cliente del contrato igual al de la factura; factura autorizada; línea no vinculada antes. Errores `SPA_INVOICE_NOT_AUTHORIZED`, `SPA_INVOICE_CUSTOMER_MISMATCH`, `SPA_INVOICE_LINE_ALREADY_LINKED` | PROPUESTO | T |
| Estado SRI, anulaciones/NC, facturas a crédito, lista de pendientes | Condiciones de creación y efectos posteriores | PENDIENTE (`O-15` §2.5; `O-13`, `O-14`) | P |

### 20.3 Lectura de la factura

El estado de la factura, de la CxC y de los cobros pertenece a Ventas y CxC [E]. Spa guarda solo identificador de factura y de línea (y de CxC si la consulta) más el estado de sus propias entidades. El
contrato se crea leyendo la factura mediante una consulta existente de Ventas (V-03b: el evento no trae cliente ni líneas); no se extiende Ventas.

### 20.4 Anticipos de Clientes: proceso independiente y transversal (`DIR-SSOT-01`)

**Directriz del propietario (encargo SPA-V1.1-SSOT-CONSOLIDATION, 2026-11-18):** *Anticipos de Clientes es un proceso independiente y transversal del ERP, no una entidad financiera propia de Gali Spa.*
Alcance: esta directriz fija la **propiedad** del proceso; **no** decide si el proceso existirá, ni su diseño, ni cierra `O-14`. Se registra con este alcance en el
[registro de decisiones](SPA-V1.1-APPROVED-DECISIONS-01.md), §7.

- `O-14` **permanece abierta**. Su posible desarrollo es un **proyecto separado** (futuro, sin documento, sin ADR y sin autorización): no se diseña aquí ni dentro de Ventas ni de CxC.
- **Único punto de integración que Spa podría necesitar** (no es contrato): disponer, si ese proceso llegara a existir, de un modo de (a) consultar el saldo de anticipo de un cliente, (b) aplicarlo como
  pago de la factura que origina un paquete y (c) tramitar su devolución, **siempre mediante el contrato del propio proceso transversal**, sin copiar su estado ni duplicar sus reglas.
- Efectos contables y fiscales de los anticipos de clientes (cuenta, traductor, tratamiento SRI): **no verificados** [NV]; el repositorio no los documenta.
- **Mientras `O-14` siga abierta, ninguna regla de este contrato depende de anticipos**; las funciones que los requieren quedan **NO AUTORIZADAS** (N-02, N-03). Qué se hace en V1.1 cuando el negocio necesite
  cobrar antes de facturar es una **decisión comercial pendiente** (§30), no una conclusión de este documento.

### 20.5 SPA-TEC-03 regla 9 — Lecturas financieras oficiales

Consultar facturas/líneas/Items, estado comercial/fiscal disponible, pagos/abonos, cuotas/vencimientos existentes y saldos informados por CxC mediante contratos oficiales. Relaciones verificables §10.2; no duplicar tablas financieras/calendarios/lógica de cobro ni reconstruir saldo monetario. Cuotas pendientes no suspenden sesiones automáticamente (§11.6); elegibilidad fiscal/SRI y efectos económicos por validar. O-14 transversal independiente, sin diseño de Anticipos.

## 21. Seguridad y privacidad

**C-01:** propuesta exacta de archivos/contratos protegidos en ADR §5.13; clasificación sin bypass/wildcard más guard de recurso y concesiones. Es selección técnica recomendada, revisión especial LOCKED PENDIENTE y modificación Kernel NO AUTORIZADA. No cambia §21.5 ni atribuye una autorización nueva.

Integración oficial SPA-ARQ-08 §4.2 reglas 7/8, sujeta a SPA-ARQ-06: sin usuarios/autenticador/permisos generales paralelos; C-01/C-02 y LOCKED/FROZEN conservan sus gates.

**Referencia normativa principal de SPA-ARQ-06 — APROBADA POR EL PROPIETARIO el 2026-11-19.** Ocho reglas literales y límites: [REG §17](SPA-V1.1-APPROVED-DECISIONS-01.md#17-spa-arq-06--expedientes-y-seguridad). Complementa SPA-DEC-116 y SPA-ARQ-01..05, ADR-050 y O-15/O-25 sin alterar scopes oficiales. **Implementable hoy: no.** C-01 y C-02 permanecen visibles; BL-02/03/06/09. Protección y auditoría disponibles y validadas antes de operar datos clínicos reales; ninguna fase autorizada.

### 21.1 Ocho reglas funcionales aprobadas

| Regla | Materia | Alcance aprobado y límites |
|---|---|---|
| 1 | Expediente único | Un expediente Spa por cliente/empresa; atenciones de sucursales de esa empresa integran el mismo expediente, conservando sucursal de cada valoración/atención/sesión/registro. Reutilizar BusinessPartner, sin duplicar identidad ni cambiar su ámbito tenant; expediente diferenciado por empresa aunque BP compartido. |
| 2 | Acceso profesional | Solo usuarios expresamente autorizados para consultar/modificar clínica según sus facultades, incluida corrección. Ventas, Empleados y asignaciones laborales no conceden acceso clínico. Profesional responsable y usuario registrador separados. Integración con Identity/Access/Kernel oficiales, sin autenticador/autorizador paralelo. |
| 3 | Administrador | Gestionar usuarios/roles/permisos no concede acceso clínico ordinario por condición Admin. Acceso excepcional exige justificación, autorización por política controlada, identificación, auditoría y alcance delimitado. **C-01/O-01: POLÍTICA FUNCIONAL APROBADA — MECANISMO TÉCNICO BLOQUEADO HASTA AUTORIZACIÓN ESPECIAL.** Bypass actual requiere revisión especial LOCKED; permisos spa.* no lo resuelven; sin seleccionar permisos sellados/concesiones/acceso excepcional. |
| 4 | Entre sucursales | Acceso a expediente de otras sucursales de la misma empresa solo cuando función/permisos lo justifiquen y bajo contexto/política autorizados. Ni asignación laboral ni trabajar en varias sucursales concede acceso general. Sucursal de atención conservada; autorización técnica intersucursal pendiente. |
| 5 | Auditoría | Registrar consultas, creación, modificación, corrección, acceso a fotos y consentimientos; usuario, fecha/hora, expediente/recurso, operación, empresa/sucursal cuando corresponda, resultado y motivo cuando aplique. **Sin contenido clínico sensible en logs.** Infraestructura oficial FROZEN; extensión Spa de consultas exitosas solo alternativa de estudio; modo síncrono/asíncrono/mixto y errores pendientes. |
| 6 | Archivos privados | Documentos/fotos/archivos clínicos privados, acceso autorizado sin enlaces públicos permanentes; protección en transmisión/almacenamiento, cifrado adecuado bajo diseño aprobado y claves protegidas, acceso trazable/aislado por empresa, asociación expediente/atención/finalidad/autorización cuando corresponda. Cifrado, claves, almacenamiento/descifrado/restauración/backups pendientes de revisión técnica/operativa; sin modificar IFileStorage/LocalFileStorage/scripts ni crear sistema de respaldo. |
| 7 | Corrección y conservación | Corrección autorizada trazable, versiones sustituidas restringidas y nunca vigentes tras rectificación. Contemplar plazos legalmente aplicables y rectificación/supresión/anonimización jurídicamente procedentes mediante ejecución controlada/auditable. **C-02/O-05: OBJETIVO FUNCIONAL APROBADO — PROCEDIMIENTO Y VALIDACIÓN LEGAL PENDIENTES.** No borrado físico general ni versiones eliminadas indiscriminadamente; regla de no borrado no resuelve derechos por sí sola; sin afirmar cumplimiento ecuatoriano. |
| 8 | Protección empresarial | Validar contexto empresarial en toda operación y relaciones BP/Employee/sucursal/consentimiento/sesión/archivo conforme propietarios/scopes oficiales. Impedir consulta cruzada por error y edición/corrección/descarga sin autorización/alcance válidos; usuario multiempresa opera en contexto autorizado. Agenda/listados operativos no exponen clínica; respetar aislamiento tenant/empresa sin cambiar ámbito BP. |

**Trazabilidad anterior:** SPA-SG-01/SPA-INV-04/SPA-CARE-SEG-01/02/SPA-SEC-INV-07/DUP-03 aplican regla 2; SPA-SG-03 reglas 2/5/7; SPA-SG-05/SPA-SEC-INV-12 regla 8; SPA-SG-06/SPA-CARE-SEG-05/SPA-CARE-FOT-05/DUP-15 regla 8; SPA-SEC-INV-09 regla 3; SPA-SEC-R-01/SPA-CARE-SEG-04 C-01/regla 3; SPA-SEC-R-08 regla 4. SPA-CARE-SEG-03/SPA-SG-04, convención de permisos y catálogo siguiente siguen mecanismos candidatos, no nueva aprobación de claves.

**Alternativas técnicas pendientes:** permisos sellados (B), concesiones clínicas (C) y acceso excepcional (D) requieren revisión/autorización sin seleccionar C+D por recomendación heredada; aceptar acceso ordinario por bypass (A) no satisface el objetivo aprobado. `CareAccessGrant`, doble control, notificaciones y revisión posterior no quedan aprobados por mención. Entre sucursales, ni acceso general por permiso aislado ni `CompanyUserBranch`/`IBranchAccessGuard` como criterio suficiente se adoptan por inferencia; diseñar según regla 4. Seguridad, confiabilidad de lecturas, cifrado/claves, almacenamiento, conservación y validación legal siguen §22/29/30 y ADR-051. No autoriza modificar LOCKED/FROZEN ni nuevos permisos efectivos.

### 21.2 Catálogo unificado de permisos candidatos (30)

Ninguno existe en el Kernel ni está aprobado. **Catálogo operativo ampliado recomendado** (cubre los nombres que esta tabla deja pendientes —consultar sesiones/valoraciones/consentimientos, planes, visitas, lista de espera, panel, reportes, desactivar fotografías—): ADR §5.21 (SPA-SEL-06), también candidato y sin aprobación. Una operación tiene un solo nombre; los seis nombres repetidos entre borradores tienen un significado único (`C-09` cerrado en nombres).

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

### 21.4 SPA-ARQ-07 — Protección y validación (regla 10)

Aplicar permisos clínicos, aislamiento tenant/empresa, autorización intersucursal, protección sensible, auditoría de acceso/cambios, privacidad/consentimiento y profesional separado de usuario registrador conforme §21.1. Sin datos clínicos reales hasta condiciones necesarias de seguridad/cumplimiento resueltas y verificadas. No ampliar módulos FROZEN/CLOSED ni LOCKED.

SPA-ARQ-07 no resuelve precisión/escala, requisitos jurídicos de firma, menores/representantes, plazos de conservación, cifrado/claves definitivos, eliminación legal ni originales no contrastados. V-01/04/06/07/08/09, C-01/C-02 conservan los aspectos pendientes; reglas aprobadas no vuelven a propuesta por faltar mecanismo.

### 21.5 SPA-TEC-02.A — Autorización clínica y Admin

**Seis reglas aprobadas:** (1) Admin sin acceso ordinario por condición administrativa; (2) backend exige autorización clínica específica y evaluación del recurso incluso frente al bypass; (3) excepción con motivo y autorización de **otra persona facultada**, sin autoautorización; (4) concesión temporal delimitada por recurso/acción y auditada; (5) sin comprobar autorización o persistir auditoría obligatoria, denegar contenido sensible; (6) implementación requiere autorización especial de revisión Identity/Access/Kernel LOCKED.

Política/dirección aprobadas; revisión especial pendiente, **modificación Kernel NO AUTORIZADA**. Permisos sellados/concesiones/ejecución excepcional aún diseño detallado, C-01 no CLOSED; sin sistema general paralelo. Auditoría durable §22.4.

## 22. Auditoría y conservación

**Selección técnica F0 recomendada:** ADR §5.14 especifica intento/evento/handler/writer y commit síncrono antes de entrega; no basta RecordAsync ni un mensaje Outbox. Sin cambiar contratos FROZEN; ley/retención C-02 pendiente.

**Qué hace:** deja rastro de quién hizo o consultó qué, y define cuánto se conserva. **Estado de la sección:** auditoría/protección incluidas por SPA-DEC-116, mecanismos y conservación pendientes; la tensión entre el no borrado y la eliminación legal está
**BLOQUEADA**. **Implementable hoy:** no. **Bloqueos:** BL-02, BL-03, BL-06, BL-09 (§29.1).

### 22.1 Auditoría

Correcciones de valoraciones/mediciones/consentimientos SPA-ARQ-07 según §17.1; protección §21.4. Auditoría no sustituye validación jurídica ni autoriza conservación indefinida.

Obligación funcional aprobada: §21.1 regla 5; las filas siguientes distinguen trazabilidad exigida de mecanismos candidatos (append-only, granularidad, entidad, modo y comportamiento ante errores). Infraestructura oficial FROZEN intacta.

| ID | Comportamiento contractual | Estado | Evid. |
|---|---|---|---|
| SPA-AU-01 | La auditoría de escrituras usa Entity Audit oficial (`AuditRecordBase`, `AuditActor`, `IAuditService`); sin sistema paralelo; cada entidad de Spa añade la suya | PROPUESTO | T, E |
| SPA-AU-02 | Operaciones a auditar: creación de expediente; modificación de valoración; registro o corrección de sesión; aceptación o retiro de consentimiento; cambios de agenda; consulta o exportación de imágenes; cambios en asignaciones operativas de atención | APROBADO alcance sensible §21.1 regla 5 y trazabilidad O-25; detalle técnico/granularidad restante PROPUESTO | T |
| SPA-SEC-AUD-01 / SPA-SEC-INV-01 / DUP-05 | Toda lectura exitosa de detalle de un expediente, valoración, antecedentes, consentimiento o fotografía, toda descarga y exportación deja un registro append-only con usuario, empresa, recurso, operación, instante y resultado, **sin contenido clínico** | APROBADO obligación/metadatos sin clínica §21.1 regla 5; append-only y granularidad técnica candidatos | A + T |
| SPA-SEC-AUD-02 a AUD-06 / SPA-SEC-INV-06 | Un listado registra una entrada por solicitud; los accesos denegados se registran (`Denied`); el registro es append-only, su consulta exige permiso propio y se registra a su vez; el acceso excepcional exige motivo y revisión; no hay contenido clínico ni nombres de archivo | PROPUESTO | T |
| SPA-SEC-AUD-07 | Plazo de conservación del registro de accesos | PENDIENTE (`O-03`) | P |
| Entidad | `CareAccessLog` (hereda de `AuditRecordBase`; propia de Spa; sin modificar la infraestructura FROZEN). **Unifica** el `PhotoAccessLog` de R3 (§B-05) | PROPUESTO | T |
| Modo de escritura | A síncrona fail-closed; B asíncrona con outbox ([ADR-008](ADR-008-outbox-pattern-foundation.md)); C mixta (A para fotografías, exportaciones y apertura del expediente completo; B para listados). Recomendación técnica no aprobada: C | PENDIENTE (`O-03`) | T, P |

### 22.2 Conservación, corrección y eliminación

Aplicar §21.1 regla 7. **C-02/O-05: OBJETIVO FUNCIONAL APROBADO — PROCEDIMIENTO Y VALIDACIÓN LEGAL PENDIENTES.** La obligación aprobada no selecciona eliminación física/criptográfica, anonimización ni plazos; no-borrado no resuelve derechos jurídicos.

| ID | Comportamiento contractual | Estado | Evid. |
|---|---|---|---|
| SPA-SEC-RET-01 / DUP-07 | Objetivo funcional §21.1 regla 7 aprobado: corrección/conservación y medidas legales controladas; procedimiento y validación legal pendientes. No elegir prohibición absoluta ni borrado físico general por jerarquía técnica | **BLOQUEADO** (`C-02`, `O-05`) | T |
| SPA-SEC-RET-02 | El procedimiento exige permiso propio, solicitud registrada con fecha y titular, aprobación, y deja un registro del hecho sin datos personales | PROPUESTO (condicionado a `O-05`) | T |
| SPA-SEC-RET-03 | Aplicar §21.1 regla 7: trazabilidad de corrección, acceso restringido a sustituido y versión anterior no vigente | APROBADO objetivo; persistencia/plazos/ley pendientes | A + P |
| SPA-SEC-RET-04 | Un retiro de consentimiento suspende el uso futuro; las atenciones ya realizadas siguen su plazo de conservación | PROPUESTO | T |
| SPA-SEC-RET-05 | Los respaldos con datos eliminados siguen un ciclo de rotación documentado; con borrado criptográfico la clave eliminada cubre los respaldos | PROPUESTO | T |
| SPA-SEC-RET-06 | Plazos de conservación por tipo de dato | PENDIENTE (`L-NV`) | P |

Alternativas de eliminación o anonimización (sin elegir): A borrado criptográfico (cifrado por expediente o archivo); B anonimización en el lugar; C borrado físico con lápida (contradice la regla permanente); D
no eliminar nunca (incompatible con una obligación legal de eliminación, si aplica). Recomendación técnica no aprobada: A + B bajo un procedimiento aprobado. Distinciones: corrección, retiro de consentimiento,
restricción de acceso, conservación histórica y eliminación son conceptos distintos; el borrado arbitrario está prohibido.

### 22.3 Obligaciones legales y su estado de verificación

Fuente: R4 §7 (consulta del 2026-11-18 a textos de gob.ec extraídos de PDF con errores de reconocimiento de caracteres). **No es asesoría jurídica ni certificación de
cumplimiento**; cada cita debe confirmarse contra el Registro Oficial (`V-09`); no se verificó ninguna reforma posterior a enero de 2025.

| Estado | Contenido |
|---|---|
| **L-EXPRESO** (texto leído; no certificado) | Consentimiento libre, específico, informado, inequívoco y revocable (LOPDP Art. 8; Reglamento Arts. 5 y 6); demostrabilidad del consentimiento (Reglamento Art. 5); rectificación y eliminación atendidas en 15 días (LOPDP Arts. 14, 15); datos sensibles y de salud como categorías especiales (Arts. 25, 26, 30, 31); menores de 15 años: consentimiento del representante legal (Art. 21; Reglamento Arts. 19, 20); medidas de seguridad incluido el cifrado (Art. 37); notificación de vulneraciones en 5 días y del encargado al responsable en 2 días (Art. 43; Reglamento Arts. 24 a 28); delegado y evaluación de impacto en el tratamiento a gran escala de categorías especiales (Art. 48; Reglamento Arts. 31, 53, 54); firma electrónica y mensajes de datos (Ley 67, Arts. 2, 13 a 15, 52 a 55) |
| **L-INTERPRETACIÓN** (lectura del autor; requiere profesional legal) | Los antecedentes declarados son probablemente datos de salud (`L-INT-01`); el régimen sancionatorio estaría vigente desde el 26 de mayo de 2023 (`L-INT-04`); una firma capturada en pantalla o una aceptación electrónica pueden o no cumplir el Art. 15 de la Ley 67 (`L-INT-05`) |
| **L-NV** (no verificado) | Si Spa es «profesional de la salud» del Sistema Nacional de Salud (`L-INT-01`); si las fotografías son datos biométricos (`L-INT-02`); si el volumen es «gran escala» (`L-INT-03`); cómo se acredita la representación de un menor (`L-INT-06`); quién es «responsable» y quién «encargado» (`L-INT-07`); normativa no consultada: Ley Orgánica de Salud, normativa sanitaria de establecimientos de estética, Código de la Niñez y Adolescencia, protección al consumidor, derecho a la propia imagen, resoluciones de la Superintendencia de Protección de Datos Personales y plazos de conservación de historias clínicas |

Recomendaciones técnicas derivadas (no requisitos legales): `SPA-SEC-LEG-01` a `SPA-SEC-LEG-07` (mostrar la información del Art. 12 antes del consentimiento y registrar la versión mostrada; conservar la
evidencia demostrable; registrar solicitudes de derechos con plazo de 15 días; plan de incidentes de 5 y 2 días; minimización; anonimización para fines distintos de la atención; asesoría legal previa).
**Decisión `O-07` y verificación `V-06`: asesoría legal antes de aprobar retención, exportación, publicación, firma y atención a menores.**

### 22.4 SPA-TEC-01.6 — Auditoría clínica durable

Accesos sensibles registrados con persistencia durable comprobada **antes de entregar contenido**. Fallo obligatorio impide entrega; denegación no pasa a autorización si falla log. Metadatos necesarios sin clínica; cambios clínicos con trazabilidad transaccional adecuada. **EfAuditWriter.RecordAsync no equivale por sí solo a transacción confirmada.** Seleccionar mecanismo compatible con auditoría oficial sin cambios FROZEN inferidos; retención/legal C-02 pendientes.

## 23. Pantallas y experiencia de usuario

**Estado de la sección:** capacidades y responsive APROBADOS por SPA-DEC-115/116; composición visual resuelta con patrones oficiales en §23.4 (SPA-SEL-04, aprobada por el propietario el 2026-11-19; `SPA-UX-01`), sin modificar `MENU_MAP.md`. **Implementable hoy:** no.

| Superficie funcional incluida (presentación técnica por definir) | Función | Sección |
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
| Panel inicial | Jornada y acceso a procesos/seguimiento, espera y reportes básicos | §23.3 |
| Búsqueda por servicio | Servicio/fecha/sucursal sin profesional inicial, resultados y selección explícita sin confirmar automáticamente | §7.1, §23.3 |
| Visitas y llegada | Varios servicios/profesionales, llegada y cambios con motor único | §8.4, §23.3 |
| Lista de espera y consultas/reportes operativos | Capacidades básicas incluidas; diseño/datos/reglas exactos pendientes | §23.3 |

Reglas PROPUESTAS: usar los componentes y patrones oficiales ([frontend](../architecture/frontend.md)) y el componente decimal oficial; ocultar acciones sin permiso (la autoridad es el backend); indicar cuándo un dato
es sensible; no mostrar datos clínicos en listados, notificaciones ni agenda; adaptación móvil según las reglas de diseño existentes; sin sistemas paralelos de navegación, avisos ni validaciones.

**Lectura de las fichas conservadas:** las 19 fichas siguientes mantienen su texto previo. Sus menciones a reglas clínicas pendientes quedan actualizadas por la aprobación expresa SPA-ARQ-03 en §12; solo persisten los detalles técnicos, legales, comerciales y de correspondencia con agenda allí delimitados. No se modifica su estructura, diseño ni alcance.

**Actualización SPA-ARQ-04:** las 19 fichas se conservan literalmente. Sus menciones previas de contigüidad/restricciones pendientes se leen conforme a §7.2: consecutivas compatibles y obligaciones funcionales ya aprobadas; solo detalle técnico o políticas no aportadas permanecen pendientes.

**Actualización SPA-ARQ-05:** las 19 fichas se preservan literalmente; sus alternativas comerciales previas se leen bajo §11.2/11.3. Ausencia automática, composición/intercambio, vigencia, movilidad y cupos ya no son alternativas funcionales abiertas en el alcance aprobado; solo subsiste el detalle real señalado en §11.

**Actualización SPA-ARQ-06:** las 19 fichas se conservan literalmente; menciones previas de política Admin/sucursales/auditoría/privacidad/conservación pendientes se leen bajo §21.1 y §30: objetivos funcionales aprobados, mecanismos y validación jurídica pendientes, C-01 técnico bloqueado y C-02 no cerrado.

**Actualización SPA-ARQ-07:** fichas preservadas literalmente; sus referencias anteriores a borradores, valoración, mediciones, autorizaciones y correcciones se leen conforme §14.1/15.1/16/17.1/18/21.4. No volver a proponer lo aprobado; precisión, ley, mecanismos y contraste originales conservan pendientes expresos.

**Actualización SPA-ARQ-08:** las 19 fichas se preservan literalmente; sus estados previos de integración se leen conforme §4.2. Fronteras funcionales aprobadas, contratos/mecanismos pendientes; Gali Spa solo identifica evidencia histórica, nunca dependencia del producto genérico SPA.

### 23.1 Fichas de pantallas y superficies funcionales

Estas fichas conservan los diez elementos anteriores y explicitan flujos principales/excepcionales y reglas de negocio. **La estructura de presentación es evidencia de lo documentado, no una maqueta nueva ni un diseño original certificado.** Los datos [F]/[R] son planificación histórica recuperada; los controles, estados, permisos y disposiciones [T] conservan su condición de propuesta. SPA-DEC-115/116 aprueban las capacidades indicadas; O-15/O-25 conservan sus enunciados literales (§8.1/11.1). «No recuperado» conserva el pendiente, no excluye la función. En cada ficha, errores = §26 y aceptación = escenarios candidatos, sin ejecución ni aprobación implícita.

**Consolidación SPA-TEC-01/02/03:** las 19 fichas permanecen íntegras. Sus pendientes previos se actualizan mediante §7.3/8.7/10.2/11.5/11.6/15.2/18.1/21.5/22.4/27: dirección técnica parcial aprobada, cierre administrativo explícito sin falsa finalización clínica, cupos retenidos y relaciones comerciales explícitas. Registros históricos no reabren las decisiones posteriores.

#### Pantalla 01 — Agenda principal y cita

Procedencia: R1 SPA-AG-01/03; R2 §2.6/2.12; contrato §7/8.

| Elemento | Contenido consolidado y pendientes |
|---|---|
| Objetivo | Organizar citas y localizar disponibilidad. |
| Presentación documentada | Día/semana y filtros por sucursal/profesional APROBADOS (SPA-DEC-116); búsqueda por servicio sin profesional inicial (115). Sin columnas, colores o gestos originales recuperados. |
| Datos visibles | Cliente, servicios/profesionales de la visita, sucursal, horarios, estado, indicador visible de reprogramación con acceso a historial y vínculo de paquete; sin clínica/fotos. Vínculo técnico de cada prestación con los bloques aprobado conceptualmente por SPA-ARQ-01, persistencia pendiente. |
| Acciones del usuario | Buscar disponibilidad, seleccionar explícitamente profesional/hora, reservar, registrar llegada, reprogramar/cambiar/cancelar, marcar ausencia y documentar atención; no confirmación automática. |
| Comportamiento del sistema | Revalida disponibilidad; reprogramación válida conserva identidad/Programada y registra horas/profesionales anterior y nuevo cuando corresponda, autor/momento; indicador e historial en agenda. Sin consumo automático por transiciones; cita y atención independientes. |
| Validaciones y errores | Activos y elegibles, horario, duración positiva y sin solape; hora inválida o colisión se rechazan (§26). Anticipación/contigüidad O-09. |
| Permisos | spa.agenda.view/create/update/cancel/mark-absent; catálogo candidato. employees.view no basta; sucursales O-02. |
| Estados y transiciones | SPA-ARQ-02: Programada, En atención, Atendida, Cancelada, Ausente; inicio/fin por cita independiente. Reprogramación válida conserva Programada; evento/indicador Reprogramada sin sexto estado. Visita derivada, finaliza con todas las citas en estado final. |
| Integraciones | BP, Employee por IEmployeeDirectory, Branch, Item; contrato de paquete opcional. |
| Criterios de aceptación | PT-14 y SPA-AC-25-01/07: una reserva concurrente válida, trazabilidad y ningún consumo por reservar/reprogramar. |
| Flujos principales y excepcionales | Buscar por servicio o filtrar agenda → seleccionar profesional/hora → reservar/reprogramar; si cambia disponibilidad o hay conflicto, revalidar y no confirmar automáticamente. |
| Reglas de negocio | SPA-DEC-115/116, SPA-ARQ-01 y O-25: visita agrupa citas/bloques; cancelar una cita no cancela automáticamente las demás; varios bloques no duplican prestación; reserva/reprogramación sin consumo. |

#### Pantalla 02 — Horarios y disponibilidad

Procedencia: R2 §2.3/2.4; contrato §7.

| Elemento | Contenido consolidado y pendientes |
|---|---|
| Objetivo | Establecer franjas atendibles por sucursal y profesional. |
| Presentación documentada | Se documentaron patrón semanal del establecimiento y excepciones por fecha; franjas del profesional por sucursal. No se recuperó pantalla ni editor visual. |
| Datos visibles | Franjas locales, cierres por fecha, profesional/sucursal y elegibilidad; modelo técnico candidato. |
| Acciones del usuario | Administrar horarios y consultar disponibilidad; acciones exactas para excepciones sin recuperar. |
| Comportamiento del sistema | Mismo motor centralizado de agenda para consulta y reserva; combina habilitación y horarios con descansos/bloqueos/preparación/traslados aprobados. Cálculo exacto pendiente. Sin disponibilidad de cabinas/equipos. |
| Validaciones y errores | Respeta restricciones sin duraciones/tolerancias inventadas; reloj oficial, hora inválida y concurrencia V-05; O-09 de detalle. O-08 resuelta: HX-01. |
| Permisos | spa.availability.manage candidato; separación establecimiento/profesional pendiente. |
| Estados y transiciones | No se recuperaron estados de los horarios ni transiciones. |
| Integraciones | Branch, Company.Timezone, ICompanyClock e IEmployeeDirectory. |
| Criterios de aceptación | PT-14: citas fuera de horario o profesional no elegible se rechazan; no se infiere acceso ERP por disponibilidad. |
| Flujos principales y excepcionales | Administrar franjas/restricciones → consultar con motor único; sin profesional habilitado u horario compatible no ofrecer disponibilidad; códigos exactos pendientes. |
| Reglas de negocio | Restricciones profesionales aprobadas; sin valores nuevos ni control de cabinas/equipos HX-01. |

#### Pantalla 03 — Servicios y tratamientos

Procedencia: R2 §3; contrato §9.

| Elemento | Contenido consolidado y pendientes |
|---|---|
| Objetivo | Configurar las condiciones de atención del servicio comercial. |
| Presentación documentada | Lista → Editor, propuesta técnica documentada; sin maqueta aprobada. |
| Datos visibles | Item Service, duración, sucursales, especialidades, condiciones, estado e historial; precio/impuestos por lectura oficial. |
| Acciones del usuario | Configurar, activar/desactivar y consultar; relación procedimiento-servicio O-11. |
| Comportamiento del sistema | Reutiliza Item; datos operativos propios de Spa; historial de citas conserva instantánea propuesta. |
| Validaciones y errores | Item activo de empresa y Service, duración positiva, especialidades válidas; duplicado/inactivo/tipo erróneo en §26. |
| Permisos | spa.services.view/manage candidatos. |
| Estados y transiciones | Activo ↔ Inactivo propuesto; historial conservado. |
| Integraciones | Items/Pricing, Empleados; insumos documentales (§19). |
| Criterios de aceptación | PT-15: rechazar Product, impedir nuevas citas del inactivo, conservar historial; sin movimiento por insumo documental. |
| Flujos principales y excepcionales | Configurar Item Service → activar/desactivar; rechazar tipo/estado incompatible y preservar historial. |
| Reglas de negocio | Servicio comercial oficial sin catálogo/precios/impuestos paralelos; relación procedimiento pendiente O-11. |

#### Pantalla 04 — Paquetes y contratos

Procedencia: R2 §4/5; O-15; contrato §11/20.

| Elemento | Contenido consolidado y pendientes |
|---|---|
| Objetivo | Administrar definiciones y derechos de sesiones derivados de una compra. |
| Presentación documentada | Saldo visible al agendar y atender; consulta de movimientos. No se recuperó formulario de creación ni lista de facturas pendientes; no se inventan. |
| Datos visibles | Paquetes individuales/combinados, vigencia, cupos/cantidades, cliente, factura/línea, saldo en sesiones y movimientos; ampliaciones y sustituciones incluidas. |
| Acciones del usuario | Definir, crear contrato explícitamente desde factura autorizada, consultar y gestionar cupos, ampliaciones/sustituciones y movimientos; reglas de ajuste/consumo pendientes. |
| Comportamiento del sistema | Valida factura autorizada fuera de su autorización; evita duplicado y no revierte Ventas por fallo Spa (O-15). |
| Validaciones y errores | O-15: factura/línea sin duplicados fuera de autorización Ventas; SRI/crédito y reglas concretas O-13. Vigencia/combinación incluidas, sin elegir plazos ni equivalencias. |
| Permisos | spa.packages.view/manage/consume/adjust candidatos; lectura comercial con permiso oficial aún por verificar. |
| Estados y transiciones | Activo → Agotado/Vencido/Cancelado, candidatos; eventos y reversas finales pendientes O-13. |
| Integraciones | Ventas por lectura, Item Service; citas/sesiones; sin cobros ni saldos monetarios propios. |
| Criterios de aceptación | PT-16/17, SPA-AC-15-01/06: no duplicar línea concurrentemente, factura intacta ante fallo, ausencia de creación automática. |
| Flujos principales y excepcionales | Venta oficial → creación explícita contrato → consulta/ampliación/sustitución/consumo según política; duplicado o fallo Spa no modifica autorización de factura. |
| Reglas de negocio | Individual/combinado, vigencia/cupos/ampliación/sustitución aprobados; origen O-15 y consumo independiente O-25; reglas exactas O-13. |

#### Pantalla 05 — Expediente principal del cliente

Procedencia: R1 SPA-FC-01/03; R3 §2.7; contrato §5/13.

| Elemento | Contenido consolidado y pendientes |
|---|---|
| Objetivo | Consultar longitudinalmente las atenciones de una persona. |
| Presentación documentada | Secciones general, antecedentes/alertas, valoraciones/mediciones, planes/tratamientos, sesiones/evolución, fotografías privadas, consentimientos e historial. Sin orden visual original recuperado. |
| Datos visibles | Identidad oficial autorizada y datos/antecedentes §13.1; número de ficha, fecha, responsable y sucursal de atención; clínica solo autorizada. |
| Acciones del usuario | Abrir, consultar, registrar/corregir antecedentes con motivo e inactivar. Cliente inexistente: remisión oficial o exigencia de alta previa, sin elegir. |
| Comportamiento del sistema | Un expediente por cliente y empresa (SPA-DEC-116; O-20 resuelta); referencia BP sin duplicación. Alertas incluidas sin severidades o automatismos inventados; conserva historial. |
| Validaciones y errores | BP activo Customer; número único por empresa propuesto; formato O-19, campos O-21/O-24; errores clínicos no recuperados. |
| Permisos | spa.records.view/manage, spa.history.view/update candidatos; Admin O-01, sucursal O-02; MasterData conserva permisos propios. |
| Estados y transiciones | Activo ↔ Inactivo propuesto; cierre pendiente; corrección crea versión candidata. |
| Integraciones | MasterData, Empleados, Branch, valoraciones, sesiones, fotos y consentimientos. |
| Criterios de aceptación | PT-01/02/03/06/12/13 y SPA-AC-H-03: unicidad cliente/empresa, identidad referenciada, responsables separados, sin fuga de datos; ficha pendiente no invalida alcance. |
| Flujos principales y excepcionales | Consultar/abrir expediente único → antecedentes/alertas/valoraciones → historial; cliente inexistente remite a flujo oficial pendiente, sin duplicar identidad. |
| Reglas de negocio | Único por cliente/empresa, BP como identidad; privacidad no derivada de Empleados; alertas sin automatismos inventados. |

#### Pantalla 06 — Valoración facial

Procedencia: R1 SPA-FC-04; R3 §3; contrato §14.

| Elemento | Contenido consolidado y pendientes |
|---|---|
| Objetivo | Registrar valoración estética inicial del rostro. |
| Presentación documentada | Formulario con secciones plegables y campos opcionales, propuesta de R3; sin distribución original recuperada. |
| Datos visibles | Campos y valores completos de §14; fecha, responsable, registrador y sucursal. |
| Acciones del usuario | Registrar valoración, decisión proceder/adaptar/posponer y corregir con motivo; borrador pendiente. |
| Comportamiento del sistema | Versiona la corrección; decisión sin crear citas ni consumir; no rotula la valoración como diagnóstico médico. |
| Validaciones y errores | Fototipo I–VI o sin evaluar, selección única propuesta; obligatoriedad O-21; catálogo de errores no recuperado. |
| Permisos | spa.records.view y spa.assessments.create/update candidatos. |
| Estados y transiciones | Registrada; Corregida como versión, candidatos; cierre/bloqueo/borrador pendientes. |
| Integraciones | Expediente, profesional, procedimientos/plan; seguimiento por sesión. |
| Criterios de aceptación | PT-07/12: conservar versiones, mínimos propuestos sin exigir campos físicos por defecto, decisión sin efectos automáticos. |
| Flujos principales y excepcionales | Registrar valoración → decisión → corregir con trazabilidad; obligatoriedad de campos según ficha pendiente, no inferida de papel. |
| Reglas de negocio | Valoración estética humana; no diagnóstico médico automático ni IA (HX-09); campos [F] con O-24/V-01. |

#### Pantalla 07 — Valoración corporal y mediciones

Procedencia: R1 SPA-FC-05/06; R3 §4; contrato §15.

| Elemento | Contenido consolidado y pendientes |
|---|---|
| Objetivo | Registrar valoración corporal y comparar medidas iniciales, controles y finales. |
| Presentación documentada | Formulario corporal; tabla comparativa de solo lectura. Gráfica pendiente; no se recuperó otra composición. |
| Datos visibles | Zona, motivo, objetivos, hallazgos, procedimiento/equipo, precauciones/decisión; peso kg y contornos cm, fecha, observaciones y diferencias (§15). |
| Acciones del usuario | Registrar valoración y conjuntos Inicial/Control/Final; consultar comparación; corregir con motivo. |
| Comportamiento del sistema | Conserva conjuntos/versiones; diferencias calculadas en lectura y no persistidas. |
| Validaciones y errores | No medida ≠ cero; medidas opcionales; escalas, frecuencia y final único O-22; no elegir precisión por analogía. |
| Permisos | spa.records.view, spa.assessments.create/update candidatos. |
| Estados y transiciones | Registrada y versiones candidatos; tipo de medición no es estado; comparación tampoco. |
| Integraciones | Expediente, Employee, política temporal/numérica oficial; vínculo obligatorio a sesión pendiente. |
| Criterios de aceptación | PT-07/08/12: unidades coherentes, nulos distinguibles, comparación correcta sin almacenar diferencias; escala aprobada antes de implementar. |
| Flujos principales y excepcionales | Inicial → controles → final → comparar; corregir sin confundir ausencia de medida con cero; precisión pendiente antes de persistir. |
| Reglas de negocio | Unidades oficiales y comparación derivada propuestas; no nueva escala sin revisión formal; no IA. |

#### Pantalla 08 — Plan de tratamiento

Procedencia: R1 SPA-AT-01/04/05; contrato §10.

| Elemento | Contenido consolidado y pendientes |
|---|---|
| Objetivo | Gestionar plan individual y tratamiento multisesión, sus sesiones previstas/contratadas, cupos y evolución. |
| Presentación documentada | Registro asociado al expediente/valoración; no se recuperó pantalla propia, editor, posición ni tabla. |
| Datos visibles | Objetivo, procedimiento, productos/equipos, sesiones previstas, intervalos/cuidados/precauciones/derivación/decisión y resultados; datos exactos de cupos/ciclo pendientes. |
| Acciones del usuario | Planificar tratamiento individual/multisesión, ampliar, suspender, cancelar, cerrar y seguir resultados: capacidades APROBADAS por SPA-DEC-116. |
| Comportamiento del sistema | Distingue plan, tratamiento operativo, paquete vendido y sesión realizada; conserva historial. Ampliación/cierre no implica factura o consumo automático. |
| Validaciones y errores | Condiciones/permisos y errores de ampliación, suspensión, cancelación/cierre; efectos sobre cupos/paquetes y correcciones pendientes; no inclusión del ciclo. |
| Permisos | Sin permiso propio recuperado; no se da por autorizado por spa.assessments.* o spa.records.view. |
| Estados y transiciones | Operaciones de ampliación/suspensión/cancelación/cierre aprobadas; estados exactos, origen/destino, reanudación y cierre con pendientes por definir. |
| Integraciones | Expediente, valoraciones, sesiones; posible relación paquete O-13. |
| Criterios de aceptación | SPA-AC-H-04/05: operaciones de tratamiento disponibles y trazables; plan/ejecución/derechos/atención distinguidos; transiciones verificables después de definir sus reglas. |
| Flujos principales y excepcionales | Planificar → sesiones previstas/ejecución → ampliar/suspender/cancelar/cerrar; cupos insuficientes, interrupción o cambios requieren política pendiente, no efectos financieros automáticos. |
| Reglas de negocio | Plan previsto, tratamiento operativo, paquete comercial y atención son conceptos distintos; ciclo funcional aprobado, efectos concretos pendientes. |

#### Pantalla 09 — Registro de sesión realizada

Procedencia: R1 SPA-AT-02/06/07; O-25; contrato §12.

| Elemento | Contenido consolidado y pendientes |
|---|---|
| Objetivo | Preparar formulario técnico y documentar atención individual con borrador, finalización, interrupción y corrección. |
| Presentación documentada | Registro con vínculo a fotografías y consentimiento; no se recuperó distribución visual. |
| Datos visibles | Fecha/procedimiento, productos, incidencias, resultados, zona/aparatología, evolución/reacciones, próxima sesión, fotos autorizadas, responsable y registrador. |
| Acciones del usuario | Preparar/guardar borrador, documentar atención efectiva, finalizar, interrumpir y corregir; capacidades históricas aprobadas. |
| Comportamiento del sistema | Borrador no certifica atención ni consumo. Modelo de preparación y sesión parcial pendiente compatible con O-25; ausencia/cancelación nunca crea sesión clínica. |
| Validaciones y errores | Elegibilidad temporal; consentimiento vigente si se aprueba requisito; excepciones/interrupción y consumo O-13/O-16. Errores no recuperados. |
| Permisos | spa.sessions.create candidato; consulta sin alcance definitivo; consumo exige política/permiso separado. |
| Estados y transiciones | Borrador/finalización/interrupción/corrección aprobados como operaciones; representación técnica, mínimos y transiciones de sesión parcial pendientes. |
| Integraciones | Agenda, expediente, Employee, consentimiento/fotos, libro de paquete; sin factura/cobro/asiento. |
| Criterios de aceptación | PT-09, SPA-AC-25-02/05/06 y SPA-AC-H-06: borrador sin atención/consumo certificado, atención separada de cita, interrupción/corrección trazables según reglas a definir. |
| Flujos principales y excepcionales | Preparar borrador → registrar atención efectiva → finalizar/interrumpir/corregir; falta de consentimiento requerido o atención parcial se gestiona según regla pendiente sin certificar atención inexistente. |
| Reglas de negocio | Borrador no es cita ni prueba de atención; consumo separado y profesional/registrador distintos. |

#### Pantalla 10 — Seguimiento y evolución

Procedencia: R1 SPA-AT-02/04, SPA-CL-01; contrato §16.

| Elemento | Contenido consolidado y pendientes |
|---|---|
| Objetivo | Comparar la evolución de la misma persona. |
| Presentación documentada | Historial por sesiones y comparación de medidas/fotos; no se recuperó pantalla autónoma, cronología visual ni gráfica. |
| Datos visibles | Evolución, reacciones, próxima sesión sugerida y resultado final; medidas y fotos comparables autorizadas. |
| Acciones del usuario | Consultar evolución y registrar notas en la sesión; comparar conjuntos/fotos. |
| Comportamiento del sistema | Lee atenciones del cliente sin mezclarlas; comparación numérica derivada; condiciones fotográficas declaradas. |
| Validaciones y errores | Mismo cliente/contexto; acceso y consentimiento de imágenes; resultado final y edición pendientes. |
| Permisos | Permisos de sesiones, expediente y fotografías según recurso; no hay permiso autónomo recuperado. |
| Estados y transiciones | Sin ciclo propio recuperado; usa versiones/estados de registros de origen. |
| Integraciones | Sesiones, valoración, mediciones, fotografías, plan de tratamiento. |
| Criterios de aceptación | PT-07/08/09/11 parcial: no mezclar clientes, no filtrar fotos sin permiso, mantener evolución histórica. Cierre del tratamiento pendiente. |
| Flujos principales y excepcionales | Consultar atenciones/resultados → comparar medidas/fotos autorizadas; acceso denegado o finalidad no vigente impiden mostrar recurso según política aprobada. |
| Reglas de negocio | Seguimiento incluido, comparación sin mezclar clientes; resultado final del tratamiento no cierra derechos comerciales automáticamente. |

#### Pantalla 11 — Consentimientos

Procedencia: R1 SPA-CN-01/06; R3 §5; contrato §17.

| Elemento | Contenido consolidado y pendientes |
|---|---|
| Objetivo | Registrar decisiones informadas independientes y su evidencia. |
| Presentación documentada | Por procedimiento, versión/estado y firma en pantalla APROBADOS por SPA-DEC-116; disposición y mecanismo de captura no recuperados. |
| Datos visibles | Procedimiento, riesgos/limitaciones/cuidados, decisión, versión presentada, fecha y evidencia; finalidades separadas. |
| Acciones del usuario | Presentar consentimiento por procedimiento, firmar en pantalla, registrar decisión/versiones y finalidades; consultar/retirar según políticas legales por aprobar. |
| Comportamiento del sistema | No infiere publicidad desde seguimiento; retiro bloquea usos futuros según política, sin revertir atención/documentos. |
| Validaciones y errores | Firma en pantalla y versionado ya aprobados; O-16 solo mecanismo/autenticidad, vigencia y conservación. Menores O-18, académico O-17, validez jurídica pendientes. |
| Permisos | spa.consents.manage/templates candidatos; consulta pendiente de definición. |
| Estados y transiciones | Registro Vigente → Retirado; texto Vigente → Reemplazado, candidatos; rechazo conservado, ciclo exacto pendiente. |
| Integraciones | Expediente, sesión/interrupción y almacenamiento privado de evidencia. |
| Criterios de aceptación | PT-10/12 y SPA-AC-H-09: firma en pantalla y versión vinculada al procedimiento, autorizaciones diferenciadas sin inferir publicidad ni validez jurídica. |
| Flujos principales y excepcionales | Presentar versión por procedimiento → firma/decisión en pantalla → consultar/retirar; menor, representación, retiro y conservación legal según definición pendiente. |
| Reglas de negocio | Firma en pantalla/versiones/procedimiento aprobados; finalidades diferenciadas; conservación/retiro no se eligen sin regla legal. |

#### Pantalla 12 — Visor y comparación de fotografías

Procedencia: R1 SPA-AT-03; R3 §6; contrato §18.

| Elemento | Contenido consolidado y pendientes |
|---|---|
| Objetivo | Conservar y comparar evidencia visual con finalidad autorizada. |
| Presentación documentada | Dentro del expediente, miniaturas solo con permiso y grupo comparativo antes/control/después; sin fotos en agenda/listados. |
| Datos visibles | Foto, código de ficha, zona, ángulo, captura, profesional/registrador, sucursal, condiciones comparables y estado; sin ruta física. |
| Acciones del usuario | Subir, ver, comparar, exportar explícitamente y desactivar; sin publicación automática. |
| Comportamiento del sistema | Fotografías privadas con acceso y trazabilidad APROBADOS; validación de finalidad/consentimiento y mecanismos concretos pendientes; sin URL pública ni publicación automática. |
| Validaciones y errores | Sin sobrescritura/ruta externa, formatos/tamaño/metadatos pendientes; retiro y eliminación O-05; cifrado O-04. |
| Permisos | spa.photos.view/create y spa.records.export candidatos; desactivación sin permiso definido. |
| Estados y transiciones | Activa → Inactiva candidato; bloqueo por retiro pendiente; no se aprueba borrado. |
| Integraciones | Expediente, sesión/valoración, consentimientos, almacenamiento y CareAccessLog. |
| Criterios de aceptación | PT-10/11/12: no acceso público ni por finalidad distinta, consulta/exportación trazable, conservar aislamiento y evitar sobreescritura. |
| Flujos principales y excepcionales | Adjuntar a valoración/sesión → ver/comparar/exportar con autorización; fallo de archivo/acceso o retiro se trata según políticas pendientes, sin exposición pública. |
| Reglas de negocio | Archivo privado y acceso trazable; permiso de seguimiento no equivale a promoción; backups existentes sin nuevo sistema HX-03. |

#### Pantalla 13 — Productos, insumos y equipos

Procedencia: R1 SPA-IN-01/03; R2 §3; contrato §19.

| Elemento | Contenido consolidado y pendientes |
|---|---|
| Objetivo | Preservar lo utilizado o previsto en la atención. |
| Presentación documentada | Campos del procedimiento/plan/sesión; no hay pantalla autónoma ni catálogo de activos aprobado. |
| Datos visibles | Productos, equipos/aparatología e identificación operacional cuando proceda; no se recuperaron cantidades/costos obligatorios. |
| Acciones del usuario | Registrar productos/equipos usados y acceder a venta oficial según permisos del módulo comercial; cantidades/costos del registro y política de ajuste manual por precisar. |
| Comportamiento del sistema | Productos utilizados incluidos como registro técnico; venta oficial de productos por ERP incluida. Aplicados en sesión no se descuentan automáticamente de Kardex (HX-05). |
| Validaciones y errores | Contrato de inventario pendiente para todo movimiento; no inferir cantidades, bodega, lote ni costos. |
| Permisos | Sin permiso propio recuperado; depende de operación contenedora pendiente. |
| Estados y transiciones | Sin estados propios recuperados. |
| Integraciones | Items e Inventario como propietarios oficiales; sesión, servicio y plan como referencias documentales. |
| Criterios de aceptación | PT-15, SPA-OPS-T-SRV-04: registrar producto/equipo no crea Kardex; no aparece módulo de mantenimiento por esta mención. |
| Flujos principales y excepcionales | Registrar productos usados → conservar evidencia técnica; venta de producto usa flujo oficial, registro de aplicación no genera Kardex automático. |
| Reglas de negocio | HX-05 excluye consumo automático de Kardex; ventas, inventario y precios se mantienen en propietarios oficiales. |

#### Pantalla 14 — Consulta de cambios y accesos

Procedencia: R4 §4; contrato §22.

| Elemento | Contenido consolidado y pendientes |
|---|---|
| Objetivo | Consultar trazabilidad de modificaciones y lecturas sensibles. |
| Presentación documentada | Consulta restringida; se propone búsqueda por usuario/fechas, sin diseño aprobado. Entity Audit y accesos son vistas conceptualmente distintas. |
| Datos visibles | Actor, instante, recurso, operación, resultado, correlación y motivo; sin contenido clínico/nombre de archivo. |
| Acciones del usuario | Consultar cambios y accesos; edición/eliminación del registro no propuestas. |
| Comportamiento del sistema | Registra consulta de auditoría a su vez; modo síncrono/asíncrono/mixto O-03; no usar UserActivity. |
| Validaciones y errores | Scope tenant/empresa, retención pendiente; fallo del registro según O-03; errores por definir. |
| Permisos | spa.audit.view para cambios; spa.access-log.view para lecturas; candidatos. |
| Estados y transiciones | Append-only propuesto; sin estados de negocio propios. |
| Integraciones | Entity Audit oficial, AuditActor y CareAccessLog propio candidato. |
| Criterios de aceptación | PT-05/06/12: lecturas y denegaciones trazables, sin clínica en logs y consulta restringida. |
| Flujos principales y excepcionales | Consultar cambios o accesos con permiso → presentar trazabilidad sin contenido clínico; denegación/fallo del registro según O-03. |
| Reglas de negocio | Auditoría oficial y registro de acceso candidato; sin UserActivity como auditoría de negocio ni clínica en logs. |

### 23.2 Experiencia responsive aprobada

SPA-DEC-116 incluye computadora, tablet y celular. SPA-UX-01 conserva componentes y patrones oficiales; ya no se presenta el responsive como inclusión pendiente. Distribución exacta, breakpoints, gestos y mecanismos de captura no aportados siguen detalles técnicos, sin inventar widgets, métricas o maqueta original.

No incluye aplicación móvil independiente (HX-07) ni offline (HX-08). Recordatorios automáticos excluidos HX-02. Criterios candidatos: operar agenda/disponibilidad/visitas y consultar/completar expediente, tratamientos, sesiones y consentimiento en esos tamaños manteniendo permisos/validaciones; fotografías siguen privadas. La firma en pantalla es requisito funcional aprobado, su implementación/validez aún pendientes.

### 23.3 Panel inicial, búsqueda, visitas, lista de espera y reportes

Capacidades históricas recuperadas, no pantallas nuevas inventadas. Pueden compartir superficies integradas; no se exige un módulo por característica ni navegación vacía. Presentación exacta, métricas/campos no aportados se dejan pendientes.

#### Pantalla 15 — Panel inicial de Spa

Procedencia: SPA-DEC-116; encargo §4.2.

| Elemento | Contenido consolidado y pendientes |
|---|---|
| Objetivo | Visión operativa de la jornada y acceso al trabajo de Spa. |
| Presentación documentada | Panel operativo aprobado, sin distribución original, métricas/widgets o agregados inventados. |
| Datos visibles | Contexto de jornada y acceso a agenda, visitas, tratamientos, seguimiento, lista de espera e información/reportes básicos; contenido exacto pendiente. |
| Acciones del usuario | Consultar jornada y acceder a esas operaciones. |
| Comportamiento del sistema | Integra información operativa autorizada; no crea dashboard analítico general. |
| Flujos principales y excepcionales | Abrir panel → consultar contexto → acceder a proceso; datos no autorizados no se muestran y errores exactos pendientes. |
| Estados y transiciones | Sin estados propios recuperados; respeta estados de procesos de origen. |
| Reglas de negocio | Capacidad aprobada; HX-10 excluye dashboard analítico adicional. Sin indicadores clínicos públicos. |
| Integraciones | Agenda/visitas, tratamientos, lista de espera y consultas/reportes; contratos de lectura pendientes. |
| Permisos | Permisos oficiales según operación; clave de panel y composición por perfil pendientes, no usar employees.view como permiso clínico. |
| Validaciones y errores | Scope/privacidad/lecturas por O-01..03; catálogo de datos/mensajes sin recuperar. |
| Criterios de aceptación | SPA-AC-H-11: accesos operativos disponibles sin métricas inventadas ni dashboard analítico adicional. |

#### Pantalla 16 — Búsqueda de disponibilidad por servicio

Procedencia: SPA-DEC-115 #1..7; contrato §7.1.

| Elemento | Contenido consolidado y pendientes |
|---|---|
| Objetivo | Recepción encuentra profesional y horario a partir de la necesidad del cliente. |
| Presentación documentada | Superficie de la misma agenda; no segundo motor; diseño de resultados no recuperado. |
| Datos visibles | Servicio, fecha, sucursal; resultados de profesionales habilitados y horarios disponibles. |
| Acciones del usuario | Buscar sin profesional previo; comparar y elegir explícitamente profesional/hora; continuar reserva. |
| Comportamiento del sistema | Consulta mismo mecanismo central con descansos/bloqueos/preparación/traslados; nunca confirma automáticamente. |
| Flujos principales y excepcionales | Servicio/fecha/sucursal → buscar → resultados → selección explícita; sin resultados o disponibilidad cambiada, informar/revalidar sin reservar silenciosamente. |
| Estados y transiciones | Búsqueda sin estado de cita nuevo ni transición automática; estados de resultado no recuperados. |
| Reglas de negocio | Siete enunciados aprobados de SPA-DEC-115; sin valores de restricciones ni disponibilidad de cabinas/equipos. |
| Integraciones | Agenda única, servicio, Branch, directorio Empleados; sin agenda paralela. |
| Permisos | Capacidad de recepción aprobada; nombre de permiso para buscar y relación con spa.agenda.view pendientes. |
| Validaciones y errores | Habilitación y restricciones del motor; fecha/servicio/sucursal; cálculo/errores exactos y concurrencia pendientes. |
| Criterios de aceptación | SPA-AC-H-01/02: buscar sin profesional devuelve opciones elegibles y seleccionar no confirma por sí solo; mismo motor para consulta/reserva. |

#### Pantalla 17 — Visitas y llegada

Procedencia: SPA-DEC-116 agrupación visitas; contrato §8.4.

| Elemento | Contenido consolidado y pendientes |
|---|---|
| Objetivo | Coordinar varios servicios/profesionales y cambios de una visita. |
| Presentación documentada | Superficie integrada con agenda; no módulo independiente obligatorio ni diseño original recuperado. |
| Datos visibles | Cliente, sucursal, citas/prestaciones, profesionales, horarios e información de llegada/cambios; campos exactos pendientes. |
| Acciones del usuario | Organizar varios servicios/profesionales, registrar llegada, reprogramar, cancelar y gestionar cambios. |
| Comportamiento del sistema | Conserva historial y previene conflictos con motor central; llegada no genera atención ni consumo automático. |
| Flujos principales y excepcionales | Planificar con selección explícita → registrar llegada/cambios → documentar atención aparte; conflicto parcial o profesional no disponible requiere política pendiente. |
| Estados y transiciones | SPA-ARQ-02: situación general derivada de citas, sin máquina de estados de visita; finaliza al tener todas las citas en estado final. Llegada es evento con fecha/hora/usuario; modificar/cancelar una cita no cancela automáticamente toda la visita. |
| Reglas de negocio | SPA-ARQ-01: visita del mismo cliente/ocasión/sucursal agrupa una o varias citas, cada una con profesional/intervalo; múltiples bloques sin duplicar prestación comercial. Cancelación de una cita sin cancelación automática de las demás; sesiones clínicas independientes y cardinalidad por prestación pendiente. |
| Integraciones | Agenda/disponibilidad, expediente, Employee, servicios, sesiones y contrato de paquete cuando proceda. |
| Permisos | Permisos oficiales de agenda y operaciones contenedoras; llegada/cambios parciales sin clave definitiva recuperada. |
| Validaciones y errores | Habilitación, solapes/restricciones por prestación y trazabilidad; orden/simultaneidad/duración conjunta/errores pendientes. |
| Criterios de aceptación | SPA-AC-H-07/08: visita admite varios servicios/profesionales y llegada, sin crear sesión clínica por llegar ni confirmar automáticamente. |

#### Pantalla 18 — Lista de espera y consultas operativas

Procedencia: SPA-DEC-116 gestión operativa; encargo §4.2.

| Elemento | Contenido consolidado y pendientes |
|---|---|
| Objetivo | Conservar demanda pendiente y apoyar seguimiento operativo. |
| Presentación documentada | Acceso desde panel/agenda; lista y consultas como capacidad aprobada, sin columnas/orden original recuperados. |
| Datos visibles | Información de espera y contexto operativo útil; identidad mínima autorizada. Campos/prioridad/orden no aportados. |
| Acciones del usuario | Consultar y gestionar lista de espera y seguimiento; altas/bajas/promoción exactas deben precisarse. |
| Comportamiento del sistema | Usa agenda/disponibilidad única al continuar atención; no confirma ni asigna automáticamente por inferencia. |
| Flujos principales y excepcionales | Consultar espera → consultar disponibilidad → selección explícita al reservar; sin cupo o cambios de demanda, política operativa pendiente. |
| Estados y transiciones | No se recuperaron estados/transiciones propios de espera ni reglas de prioridad/caducidad. |
| Reglas de negocio | Capacidad incluida; HX-04 no permite reservas públicas ni HX-02 notificaciones automáticas por existir lista. |
| Integraciones | Panel, agenda/disponibilidad, clientes y seguimiento de tratamientos mediante contratos oficiales. |
| Permisos | Permisos oficiales, catálogo de operación/consulta pendiente; sin acceso clínico implícito. |
| Validaciones y errores | Scope, mínimo de datos, duplicados/prioridad y errores no recuperados: no inventarlos. |
| Criterios de aceptación | SPA-AC-H-12: lista accesible y continuidad con motor único y elección explícita; sin prioridad o asignación automática inventada. |

#### Pantalla 19 — Reportes básicos y seguimiento operativo

Procedencia: SPA-DEC-116 gestión operativa; encargo §4.2.

| Elemento | Contenido consolidado y pendientes |
|---|---|
| Objetivo | Consultar operación y seguimiento de tratamientos. |
| Presentación documentada | Consultas/reportes básicos integrados, sin dashboard analítico adicional. No hay gráficos/métricas originales recuperados. |
| Datos visibles | Datos operativos y evolución de tratamientos autorizados; catálogo exacto, filtros/métricas y formato pendientes. |
| Acciones del usuario | Consultar y seguir resultados; exportación solo si contrato/permiso oficial lo aprueba, no inferida. |
| Comportamiento del sistema | Reutiliza estados de origen y acceso mínimo; distingue saldo monetario comercial de saldo de sesiones. |
| Flujos principales y excepcionales | Seleccionar consulta autorizada → leer seguimiento; sin permiso o datos, respuesta según contrato pendiente, sin exponer clínica. |
| Estados y transiciones | Sin estados propios; tratamiento/sesiones y datos comerciales mantienen su ciclo oficial. |
| Reglas de negocio | Reportes básicos incluidos, analítica adicional/contabilidad paralela excluidas HX-10. |
| Integraciones | Tratamientos/sesiones, agenda/visitas y módulos comerciales por lectura cuando corresponda. |
| Permisos | Permisos oficiales por dato/consulta; clave propia y exportación aún no definidas. |
| Validaciones y errores | Scope, finalidad y auditoría; fórmulas/filtros/errores de reportes por definir. |
| Criterios de aceptación | SPA-AC-H-13: consultas operativas sin inventar métricas, sin analítica adicional y sin fuga de datos entre empresas. |

### 23.4 Especificación operativa de las 19 fichas (selecciones técnicas aprobadas)

**Estado:** completa las 19 fichas **sin modificarlas**: sus textos previos y sus aprobaciones permanecen y esta sección resuelve lo que quedaba como «pendiente de diseño» con patrones existentes del Dashboard ERP ZH. **Es la selección técnica SPA-SEL-04 (ADR §5.19), aprobada por el propietario el 2026-11-19 con sus condiciones (REG §25)**; ningún elemento de aquí cambia un alcance aprobado, añade una funcionalidad excluida (§3.1.1) ni crea una regla de negocio nueva (cuando un valor de negocio no está aprobado se dice «sin valor predeterminado»). Rutas y componentes son candidatos hasta la autorización de la fase (plan §7). Errores: catálogo §31.16. Aceptación: IDs `SPA-AC-UI-nn` candidatos; son pruebas futuras, no ejecutadas.

**Alcance de las preferencias visuales:** distribución, orden de pestañas, rejilla de 5 minutos de la agenda, tarjetas del panel y demás detalles de presentación forman parte de la selección técnica SPA-SEL-04 aprobada el 2026-11-19: son la referencia de implementación y se ajustan por la vía de decisión del propietario si se cambian; los datos, acciones, reglas y alcance de SPA-DEC-116 siguen siendo la base normativa. Las 19 pantallas cubren únicamente capacidades de SPA-DEC-116.

**Convenciones comunes a las 19 pantallas** (se citan como «CC»):
- **Estructura:** viven dentro del shell oficial (`AppLayout` → `<Outlet />`), una sola `H1`, `document.title` sin datos de personas; patrón Lista→Editor (`ConfigTabsLayout`/`items-catalog.css`) en configuración; `ZHTabBar` en contenedores con varias secciones; `ZHDataTable` para tablas; `ZHBtn`/`ZHField`/`ZhDecimalInput`/`CustomerPicker`/`ItemLookupPicker` para entrada; `ZHModal`/`ZHConfirmModal`/`ZHPromptModal`/`ZHDrawer` para diálogos (motivos obligatorios con `ZHPromptModal`).
- **Formularios:** `useForm` + `zodResolver` + `FormProvider` + `applyServerErrors`; validación en cliente es ayuda, la API decide; campos clínicos opcionales salvo los marcados; fechas de negocio `YYYY-MM-DD`; instantes UTC «Z» mostrados en la zona de empresa con los helpers oficiales; nunca `toLocaleString` en números.
- **Estados de UI:** `cargando` (indicador de carga existente), `vacío` (mensaje + acción primaria permitida), `error` (envelope normalizado + reintentar), `sin permiso` (ruta protegida por `RouteAccessGuard`; la API devuelve 403/404 sin revelar), `conflicto` (409 → mensaje «el dato cambió», recargar sin perder lo escrito), `guardando` (botón deshabilitado, `clientRequestId` estable por intento).
- **Responsive (SPA-DEC-116 incluye computadora, tablet y celular):** ≥ breakpoint de escritorio vigente de `zh-ui.css`: rejilla completa; tablet: drawers a ancho contraíble y tablas con desplazamiento interno; celular: una columna, tablas `ZHDataTable` con scroll interno, diálogos `ZHModal` a pantalla completa, acciones principales fijas al pie; **nunca desbordamiento horizontal de página**; sin breakpoints nuevos. Excepciones por pantalla se indican.
- **Privacidad:** agenda, listados, notificaciones y paneles no muestran datos clínicos ni fotos; los datos sensibles llevan indicador visual «Dato sensible»; las pestañas clínicas solo se montan si el permiso lo permite y cada apertura genera el intento de acceso auditado (§22.4).
- **Mensajes:** únicamente el sistema de mensajes visuales oficial (`message.*`), sin avisos propios.

#### 23.4.01 Pantalla 01 — Agenda principal y cita

| Elemento | Especificación operativa |
|---|---|
| Propósito | Organizar las citas del día/semana y localizar disponibilidad. |
| Datos visibles | Rejilla día/semana por sucursal y profesional; bloque de cita: hora, cliente (nombre oficial), servicios, profesional, sucursal, estado (5), insignia «Reprogramada», insignia de paquete, insignia «llegó»; bloques de ausencia/bloqueo; sin clínica |
| Campos editables | En el drawer de cita: servicios (`ItemLookupPicker` solo Service con perfil activo), profesional, hora, motivo (cancelar/reprogramar/ausencia), paquete vinculado (selector de contratos vigentes del cliente; **no se monta hasta entregar F6.c**, el backend rechaza el vínculo antes) |
| Acciones y navegación | `Buscar disponibilidad` → Pantalla 16; seleccionar celda libre abre el drawer prellenado; `Reservar`, `Reprogramar`, `Cancelar`, `Marcar ausencia`, `Registrar llegada` (→ Pantalla 17), `Iniciar atención` (→ Pantalla 09), `Cierre administrativo`, `Ver historial`; filtros sucursal/profesional/fecha; ir a expediente (si tiene permiso clínico) |
| Estados de UI | CC; conflicto 409 mantiene el drawer y recarga la disponibilidad; semana en celular = selector de día + lista por profesional |
| Validaciones y errores | `SPA_APPOINTMENT_OVERLAP`, `SPA_PROFESSIONAL_UNAVAILABLE`, `SPA_OUTSIDE_BUSINESS_HOURS`, `SPA_TRAVEL_TIME_NOT_CONFIGURED`, `SPA_APPOINTMENT_INVALID_STATE`, `SPA_CONCURRENT_MODIFICATION`, `SPA_PACKAGE_NO_BALANCE`, `SPA_PACKAGE_EXPIRED`, `SPA_CLIENT_INACTIVE` |
| Permisos | `spa.agenda.view/create/update/cancel/mark-absent`; iniciar atención/cierre: `spa.sessions.create/manage` |
| Integraciones | `CustomerPicker` (BP), directorio de Empleados, Branch, Items, contratos de paquete |
| Estados y transiciones | Cita: Programada, En atención, Atendida, Cancelada, Ausente (ADR §5.23; estados finales sin reversa); llegada y reprogramación son eventos con insignia, no estados; UI: drawer abierto/cerrado, guardando, conflicto. |
| Responsive y recuperación | Celular: lista del día agrupada por profesional y selector de día en lugar de la semana; tableta: semana con desplazamiento interno. Recuperación: 409 recarga la disponibilidad sin perder lo escrito; error de red → reintentar con el mismo `clientRequestId`. |
| Pruebas | `SPA-AC-UI-01`: sin permiso no aparecen acciones; conflicto 409 conserva los datos y no reserva; reprogramar muestra insignia e historial; PT-14, AC-25-01/07 |

#### 23.4.02 Pantalla 02 — Horarios y disponibilidad

| Elemento | Especificación operativa |
|---|---|
| Propósito | Establecer franjas atendibles, descansos, excepciones, bloqueos y traslados por sucursal y profesional. |
| Datos visibles | Pestañas: Profesionales (reglas semanales Work/Break por sucursal con vigencia), Sucursal (apertura/cierre semanal), Excepciones y bloqueos (por fecha local y por intervalo), Traslados (matriz sucursal origen→destino en minutos) |
| Campos editables | Día ISO, hora inicio/fin (hora de pared de la empresa), vigencia desde/hasta, tipo (Trabajo/Descanso), motivo, fecha, tipo de excepción (Cerrado/Reemplazo), intervalo de bloqueo, minutos de traslado |
| Acciones y navegación | Guardar reglas, crear/quitar excepción, crear/anular bloqueo, guardar traslado; «Citas por revisar» (listado de citas futuras afectadas) → Pantalla 01 |
| Estados de UI | CC; muestra la zona horaria de la empresa en el encabezado; solape de reglas marcado en la fila |
| Validaciones y errores | `SPA_SCHEDULE_OVERLAP`, `SPA_INVALID_DATETIME`; inicio < fin; el profesional debe estar asignado a la sucursal (directorio); sin valores predeterminados de horarios, descansos ni traslados |
| Permisos | `spa.agenda.view` (lectura de horarios), `spa.availability.manage` |
| Integraciones | Branch, `ICompanyClock`, directorio de Empleados |
| Estados y transiciones | Reglas con vigencia; bloqueo Activo/Anulado; excepción por fecha; sin máquina de estados propia. |
| Responsive y recuperación | Pestañas `ZHTabBar`; en celular cada regla se edita en un `ZHModal` a pantalla completa. Recuperación: solape → resaltar la fila afectada y conservar el formulario. |
| Pruebas | `SPA-AC-UI-02`: guardar con solape se rechaza; un par de traslado sin configurar produce el aviso al reservar; PT-14 |

#### 23.4.03 Pantalla 03 — Servicios y tratamientos

| Elemento | Especificación operativa |
|---|---|
| Propósito | Configurar las condiciones operativas del servicio comercial sin duplicar el catálogo. |
| Datos visibles | Lista: Item Service (nombre/código oficial), estado del perfil, duración, preparación, sucursales, especialidades, procedimientos; precio/impuesto en solo lectura vía resolutor oficial |
| Campos editables | Duración (min), preparación antes/después (min, opcional), sucursales (vacío = todas), especialidades (catálogo de Empleados), procedimientos vinculados y su orden, precauciones/contraindicaciones/preparación del procedimiento |
| Acciones y navegación | Lista → Editor; `Configurar servicio` (elige Item Service sin perfil), `Activar/Desactivar`, `Crear procedimiento`; historial de cambios del perfil |
| Estados de UI | CC; servicio inactivo se muestra atenuado; no se ofrece en agenda |
| Validaciones y errores | `SPA_ITEM_NOT_SERVICE`, `SPA_SERVICE_INACTIVE`, `SPA_SERVICE_ALREADY_CONFIGURED`, duración > 0 |
| Permisos | `spa.services.view/manage` |
| Integraciones | Items/Pricing (lectura), Empleados (especialidades), Branch |
| Estados y transiciones | Perfil: Activo ↔ Inactivo (nunca eliminar). |
| Responsive y recuperación | Lista → Editor: en celular el editor ocupa toda la pantalla y la lista se oculta. Recuperación: validación de servidor mapeada a campos con `applyServerErrors`. |
| Pruebas | `SPA-AC-UI-03`: Product rechazado; inactivo no aparece en búsqueda; PT-15 |

#### 23.4.04 Pantalla 04 — Paquetes y contratos

| Elemento | Especificación operativa |
|---|---|
| Propósito | Definir paquetes y gestionar contratos, saldo y movimientos derivados de documentos oficiales. |
| Datos visibles | Pestaña Definiciones (nombre, servicios y cantidades, vigencia o «sin vencimiento»); pestaña Contratos (cliente, factura/línea, vigencia, estado, saldos: contratadas/programadas/consumidas/disponibles por servicio, compromisos retenidos); detalle con libro de movimientos y lotes; estado de cobro leído de CxC (solo lectura) |
| Campos editables | Definición: nombre, ítems (`ItemLookupPicker` Service) y cantidades enteras, vigencia (elección explícita: días o «sin vencimiento»); contrato: seleccionar factura autorizada del cliente y línea(s); ampliación/sustitución/transferencia/ajuste: servicio, cantidad, motivo, documento/línea de respaldo, destinatario |
| Acciones y navegación | `Nueva definición`, `Crear contrato desde factura` (asistente: cliente → factura → líneas → confirmar), `Resolver compromiso` (consumir/liberar), `Compensar`, `Ampliar`, `Sustituir`, `Transferir` (bloqueada por COND-07), `Ajustar`, `Cancelar contrato`; enlace a la factura en Ventas |
| Estados de UI | CC; `ZHConfirmModal` antes de cada movimiento; saldo se muestra como cantidades, nunca como dinero |
| Validaciones y errores | `SPA_INVOICE_NOT_AUTHORIZED`, `SPA_INVOICE_CUSTOMER_MISMATCH`, `SPA_INVOICE_LINE_ALREADY_LINKED`, `SPA_PACKAGE_NO_BALANCE`, `SPA_PACKAGE_ALREADY_CONSUMED`, `SPA_PACKAGE_EXPIRED`, `SPA_PACKAGE_INACTIVE`, `SPA_SUBSTITUTION_NOT_EQUIVALENT`, `SPA_COMPENSATION_EXCEEDS_CONSUMED`; sin ampliaciones gratuitas |
| Permisos | `spa.packages.view/manage/consume/adjust`; lectura comercial exige además el permiso de Ventas/CxC aplicable verificado en F6 |
| Integraciones | `ISalesEntitlementReader` o `GetSalesInvoiceByIdQuery`, `GetReceivableByInvoiceQuery`, `IReceivableCollectionReader` (candidatos), Items |
| Estados y transiciones | Contrato: Activo, Agotado/Vencido (derivados), Cancelado; compromiso: Retenido → Consumido o Liberado; definición editable con `xmin`. |
| Responsive y recuperación | Asistente de creación en pasos de una columna; tablas con desplazamiento interno. Recuperación: reintento idempotente de cualquier movimiento; `SPA_PACKAGE_CAPABILITY_UNAVAILABLE` antes de F6.c deja la reserva vinculada a paquete no disponible. |
| Pruebas | `SPA-AC-UI-04`: contrato duplicado por línea rechazado; falla de Spa no afecta la factura; reintento con el mismo `clientRequestId` no duplica; AC-15-01..06, AC-25-03/04/06 |

#### 23.4.05 Pantalla 05 — Expediente principal del cliente

| Elemento | Especificación operativa |
|---|---|
| Propósito | Conservar el expediente único del cliente en la empresa y dar acceso autorizado a sus secciones. |
| Datos visibles | Encabezado: nombre oficial del cliente (BP), número de ficha si existe, estado, sucursal de última atención, alertas activas; pestañas (CC): General, Antecedentes, Valoraciones, Plan, Sesiones, Seguimiento, Consentimientos, Fotografías |
| Campos editables | Antecedentes por categoría (Sin responder/No/Sí + nota), alertas (texto), estado Activo/Inactivo; fecha de nacimiento y contacto de emergencia **no se capturan** hasta cerrar O-06 |
| Acciones y navegación | Búsqueda de cliente con `CustomerPicker` → `Abrir expediente` si no existe; si el cliente no existe en BP: aviso con acceso al alta oficial de MasterData (Spa no crea BP); `Revisar antecedentes` (nueva revisión con motivo), `Agregar/Desactivar alerta`, `Inactivar` |
| Estados de UI | CC; la lista de clientes del módulo nunca muestra datos clínicos; expediente inactivo es de solo lectura y no admite atenciones nuevas |
| Validaciones y errores | `SPA_CARE_RECORD_EXISTS` (devuelve el existente), `SPA_CLIENT_INACTIVE`, `SPA_CORRECTION_REASON_REQUIRED`, `SPA_ACCESS_DENIED`; «Sin responder» ≠ «No» |
| Permisos | `spa.records.view/manage`, `spa.history.view/update` (clínicos); acceso intersucursal según guard |
| Integraciones | BP (query oficial), directorio de Empleados (responsable de revisión), auditoría de acceso |
| Estados y transiciones | Expediente: Activo ↔ Inactivo; revisiones de antecedentes: vigente/no vigente. |
| Responsive y recuperación | Pestañas `ZHTabBar` con distribución responsive; alertas activas fijas en el encabezado. Recuperación: 403 muestra «sin acceso» genérico y el acceso queda registrado. |
| Pruebas | `SPA-AC-UI-05`: un solo expediente por cliente y empresa; Admin sin concesión no entra; otra empresa no ve el expediente; AC-ARQ06-01..08 |

#### 23.4.06 Pantalla 06 — Valoración facial

| Elemento | Especificación operativa |
|---|---|
| Propósito | Registrar la valoración estética facial sin diagnóstico automático. |
| Datos visibles | Lista de valoraciones faciales (fecha, profesional, sucursal, estado Borrador/Registrada, revisión vigente) y formulario con los campos de §14 |
| Campos editables | Todos los de §14 (ninguno obligatorio): motivo, condición de piel, sensibilidad, fototipo Fitzpatrick I–VI o «sin evaluar», alteraciones, zona/evolución, exposición solar, observaciones, diagnóstico médico previo (declarado), objetivo, procedimiento/productos/equipos previstos, precauciones/contraindicaciones, decisión (proceder/adaptar/posponer); fecha de negocio, profesional responsable (directorio) |
| Acciones y navegación | `Guardar borrador`, `Registrar`, `Corregir` (nueva revisión con motivo), ver revisiones; desde la pestaña Valoraciones |
| Estados de UI | CC; borrador visible con etiqueta; revisión anterior marcada «No vigente» y restringida |
| Validaciones y errores | Fototipo ∈ 1..6 o vacío; `SPA_CORRECTION_REASON_REQUIRED`, `SPA_CONCURRENT_MODIFICATION`; sin diagnóstico automático ni rótulo médico |
| Permisos | `spa.assessments.view/create/update` |
| Integraciones | Directorio (profesional), Branch, expediente |
| Estados y transiciones | Borrador → Registrada; corrección = revisión. |
| Responsive y recuperación | Formulario de una columna con secciones plegables. Recuperación: borrador conservado ante conflicto; recarga mostrando la revisión vigente. |
| Pruebas | `SPA-AC-UI-06`: borrador incompleto se guarda y no cuenta como atención ni consumo; corregir conserva la revisión previa; AC-ARQ07-01/03 |

#### 23.4.07 Pantalla 07 — Valoración corporal y mediciones

| Elemento | Especificación operativa |
|---|---|
| Propósito | Registrar la valoración corporal y comparar medidas Inicial/Control/Final. |
| Datos visibles | Datos generales (zona, motivo, objetivos, hallazgos, procedimiento/aparatología, precauciones, decisión) y tabla comparativa de conjuntos Inicial/Control/Final con diferencias calculadas solo donde hay dos valores |
| Campos editables | Peso (kg) y brazo D/I, abdomen alto/medio/bajo, cadera, muslo D/I (cm) con `ZhDecimalInput`; finalidad del conjunto; fecha de negocio |
| Acciones y navegación | `Guardar borrador`, `Registrar conjunto`, `Corregir` (revisión con motivo), `Comparar`; sin gráfica (no aprobada) |
| Estados de UI | CC; celda no medida muestra «—», nunca 0; diferencia vacía si falta un valor |
| Validaciones y errores | Valores ≥ 0, punto decimal, escala **según el gate V-04**; mientras V-04 esté abierta la persistencia de medidas está bloqueada (COND-05) y la pantalla se entrega sin el bloque de mediciones |
| Permisos | `spa.assessments.view/create/update` |
| Integraciones | Expediente, directorio |
| Estados y transiciones | Borrador → Registrada; conjunto de medidas inmutable; corrección = revisión. |
| Responsive y recuperación | Tabla comparativa con desplazamiento interno y entrada numérica táctil. Recuperación: valor inválido marcado por campo; ningún valor se completa con 0. |
| Pruebas | `SPA-AC-UI-07`: no medida ≠ cero; diferencia solo con valores comparables; corrección no sobrescribe; AC-ARQ07-02/03 |

#### 23.4.08 Pantalla 08 — Plan de tratamiento

| Elemento | Especificación operativa |
|---|---|
| Propósito | Planificar y seguir tratamientos individuales o multisesión. |
| Datos visibles | Plan: objetivo, decisión, estado, prestaciones (servicio, procedimiento, sesiones previstas, intervalo sugerido, cuidados/precauciones/derivación) y **vista comercial derivada por prestación**: propuesto/contratado/facturado/pagado/reservado/realizado/derechos restantes con enlaces a contrato y factura |
| Campos editables | Objetivo, decisión (proceder/adaptar/posponer: no ejecuta acciones), prestaciones (`ItemLookupPicker` Service + procedimiento), sesiones previstas (entero > 0), intervalo sugerido, notas |
| Acciones y navegación | `Crear plan`, `Ampliar`, `Suspender`/`Reanudar` (DP-01 aprobada), `Cancelar`, `Cerrar` (resultado final; confirmación si hay sesiones previstas pendientes), `Reservar sesión` (→ Pantalla 16 con la prestación), `Ver contrato` (→ Pantalla 04) |
| Estados de UI | CC; transiciones de 4 estados según ADR §5.23 (ampliar/suspender/cancelar/cerrar aprobados; reanudar y no reabrir = DP-01/DP-02 aprobadas el 2026-11-19); porcentaje de facturación **no** se muestra |
| Validaciones y errores | `SPA_PLAN_INVALID_STATE`, `SPA_PLAN_CLOSE_PENDING_SESSIONS_CONFIRM`; ampliar el plan no crea derechos comerciales; vínculo a contrato solo por referencia explícita |
| Permisos | `spa.treatment-plans.view/manage` (clínicos); la sección comercial exige lectura de Ventas/CxC |
| Integraciones | Items, contratos de paquete, lectores comerciales |
| Estados y transiciones | Plan: Activo, Suspendido, Cancelado, Cerrado, con historial de eventos (ADR §5.23); reanudar y no reabrir = DP-01/DP-02 aprobadas el 2026-11-19. |
| Responsive y recuperación | Prestaciones como tarjetas apiladas en celular. Recuperación: confirmación explícita al cerrar con pendientes. |
| Pruebas | `SPA-AC-UI-08`: plan sin contrato es válido; ampliar no altera el contrato; vista comercial sin vincular por BP; AC-TEC03-01..05 |

#### 23.4.09 Pantalla 09 — Registro de sesión realizada

| Elemento | Especificación operativa |
|---|---|
| Propósito | Documentar la atención realizada de forma independiente de la cita y del consumo. |
| Datos visibles | Estado (Borrador/En atención/Finalizada/Interrumpida), fecha/hora de inicio y fin, profesional responsable y registrador, cita vinculada, procedimiento, productos/aparatología (subpanel de la Pantalla 13), incidencias, resultados, evolución/reacciones, zona, próxima sesión sugerida, fotos autorizadas, revisiones |
| Campos editables | Todos los de contenido; `performedWork` obligatorio al interrumpir; motivo de interrupción; motivo de corrección |
| Acciones y navegación | `Guardar borrador`, `Iniciar atención` (profesional responsable ≠ registrador conservados), `Finalizar`, `Interrumpir`, `Corregir`, `Cierre administrativo` de la cita (con referencia a esta sesión), `Resolver cupo` (→ Pantalla 04) |
| Estados de UI | CC; si falta consentimiento aplicable el botón Iniciar queda deshabilitado con el motivo; sesión interrumpida muestra «Atención parcial» |
| Validaciones y errores | `SPA_CONSENT_REQUIRED`, `SPA_SESSION_INVALID_STATE`, `SPA_APPOINTMENT_REQUIRED` (se exige cita mientras O-10 siga abierta), `SPA_CONCURRENT_MODIFICATION`; finalizar/interrumpir no crea consumo, factura ni inventario |
| Permisos | `spa.sessions.create/view/manage`; consumo: `spa.packages.consume` |
| Integraciones | Agenda (cita), Consentimientos, Paquetes (solo vínculo), directorio |
| Estados y transiciones | Borrador → En atención → Finalizada o Interrumpida; corrección = revisión sin reabrir (ADR §5.23). |
| Responsive y recuperación | Acciones principales fijas al pie; guardado de borrador explícito (no se asume autoguardado). Recuperación: conflicto `xmin` recarga conservando el texto escrito. |
| Pruebas | `SPA-AC-UI-09`: borrador no consume; interrumpida conserva parcial y no libera agenda; corregir no crea atención ni consumo; AC-ARQ03-01..08 |

#### 23.4.10 Pantalla 10 — Seguimiento y evolución

| Elemento | Especificación operativa |
|---|---|
| Propósito | Consultar la evolución cronológica del cliente. |
| Datos visibles | Línea de tiempo cronológica del expediente: valoraciones, sesiones (completas y parciales diferenciadas), mediciones Inicial/Control/Final, resultado final, fotos comparables autorizadas |
| Campos editables | Ninguno (solo lectura); las notas se registran en la sesión |
| Acciones y navegación | Filtrar por tipo/fecha; abrir la sesión/valoración de origen; comparar conjuntos de medidas; abrir comparación de fotos (Pantalla 12) |
| Estados de UI | CC; secciones sin permiso se omiten sin revelarlas |
| Validaciones y errores | No se inventan valores faltantes ni se atribuyen cambios a un procedimiento |
| Permisos | Los del recurso mostrado (`spa.sessions.view`, `spa.assessments.view`, `spa.photos.view`) |
| Integraciones | Sesiones, valoraciones, fotografías |
| Estados y transiciones | Sin estados propios; refleja los de cada origen. |
| Responsive y recuperación | Línea de tiempo vertical en una columna. Recuperación: sección sin permiso omitida; error parcial reintentable. |
| Pruebas | `SPA-AC-UI-10`: orden cronológico; parcial ≠ completa; ausencia de valores no genera diferencias; AC-ARQ07-02 |

#### 23.4.11 Pantalla 11 — Consentimientos

| Elemento | Especificación operativa |
|---|---|
| Propósito | Presentar el consentimiento por procedimiento, registrar la decisión y firma, y gestionar versiones y retiro. |
| Datos visibles | Por expediente: lista de registros (procedimiento/finalidad, versión presentada, decisión, estado Vigente/Retirado, fecha, quién capturó) y, para administración, definiciones y versiones de texto |
| Campos editables | Registro: versión presentada (la vigente), decisión (Otorga/Rechaza), firma en pantalla (lienzo táctil/ratón), nota; retiro: motivo; administración: texto de la versión (inmutable al publicar) |
| Acciones y navegación | `Presentar consentimiento` (muestra texto, exige leer hasta el final), `Firmar y registrar`, `Retirar`, `Publicar nueva versión`; finalidades separadas: Procedimiento, Fotos de seguimiento, Promoción (cada una su propio registro) |
| Estados de UI | CC; lienzo de firma a ancho completo en celular; «Limpiar firma»; firma obligatoria cuando la decisión es Otorga |
| Validaciones y errores | `SPA_SIGNATURE_REQUIRED`, `SPA_CONSENT_VERSION_NOT_FOUND`, `SPA_CONSENT_ALREADY_WITHDRAWN`; la persona que decide es la atendida (representante y menores no soportados hasta O-18, COND-04); uso académico no existe |
| Permisos | `spa.consents.view/manage/templates` |
| Integraciones | Expediente, `ICareFileStore` (firma), auditoría |
| Estados y transiciones | Definición: Vigente/Reemplazada; registro: Vigente/Retirado. |
| Responsive y recuperación | Lienzo de firma a ancho completo con scroll de página bloqueado durante el trazo. Recuperación: firma no enviada se puede limpiar y repetir; nada se confirma sin archivo guardado. |
| Pruebas | `SPA-AC-UI-11`: aceptar una finalidad no habilita otra; versión sustituida no figura como vigente; retiro bloquea usos futuros sin revertir lo realizado; AC-ARQ07-04..06 |

#### 23.4.12 Pantalla 12 — Visor y comparación de fotografías

| Elemento | Especificación operativa |
|---|---|
| Propósito | Subir, ver y comparar fotografías privadas. |
| Datos visibles | Miniaturas (solo con permiso; carga por descarga autorizada), por foto: fecha de captura, zona/ángulo (nota), finalidad, consentimiento aplicable, profesional/registrador, sucursal, nota de condiciones comparables, estado; sin ruta física ni nombre de archivo |
| Campos editables | Archivo (formato/tamaño permitidos por configuración), finalidad, zona, ángulo, nota de captura, vínculo a valoración o sesión |
| Acciones y navegación | `Subir`, `Ver`, `Comparar` (2..n fotos, lado a lado), `Exportar` (acción explícita con permiso propio), `Desactivar`; sin publicación |
| Estados de UI | CC; visor en `ZHModal` pantalla completa en celular; miniaturas no cacheadas entre usuarios (`no-store`) |
| Validaciones y errores | `SPA_PHOTO_CONSENT_MISSING`, `SPA_FILE_TOO_LARGE`, `SPA_FILE_TYPE_NOT_ALLOWED`, `SPA_ACCESS_DENIED`; tamaño/formatos: valores de configuración sin predeterminado inventado |
| Permisos | `spa.photos.view/create/manage`, `spa.records.export` |
| Integraciones | `ICareFileStore`, consentimientos, auditoría durable |
| Estados y transiciones | Fotografía: Activa ↔ Inactiva (nunca eliminar). |
| Responsive y recuperación | Visor a pantalla completa; comparación apilada en celular. Recuperación: fallo de registro de acceso muestra error sin entregar la imagen. |
| Pruebas | `SPA-AC-UI-12`: sin consentimiento de seguimiento no se sube; cada apertura deja registro antes de mostrar; no hay URL pública; AC-ARQ07-06 |

#### 23.4.13 Pantalla 13 — Productos, insumos y equipos

| Elemento | Especificación operativa |
|---|---|
| Propósito | Documentar productos y equipos usados sin mover inventario. |
| Datos visibles | Subpanel de la Pantalla 09 y del procedimiento: productos usados, aparatología/equipos (texto o referencia), cantidad informativa opcional |
| Campos editables | Descripción, `Item` de producto opcional (referencia informativa), cantidad informativa opcional |
| Acciones y navegación | Agregar/quitar línea en la sesión en borrador; enlace a la venta oficial de productos en Ventas (con permiso de Ventas) |
| Estados de UI | CC; etiqueta fija «Registro documental, no descuenta inventario» |
| Validaciones y errores | Sin movimiento de inventario ni Kardex automático (HX-05); sin calendario de equipos (HX-01) |
| Permisos | Los de la sesión contenedora (`spa.sessions.create/manage`) |
| Integraciones | Items (lectura), Ventas (enlace) |
| Estados y transiciones | Editable mientras la sesión sea Borrador o En atención; sin estados propios. |
| Responsive y recuperación | Líneas apiladas en celular. Recuperación: línea inválida marcada, resto conservado. |
| Pruebas | `SPA-AC-UI-13`: registrar un producto no genera movimiento de inventario; PT-15 |

#### 23.4.14 Pantalla 14 — Consulta de cambios y accesos

| Elemento | Especificación operativa |
|---|---|
| Propósito | Consultar cambios y accesos sin contenido clínico. |
| Datos visibles | Dos pestañas: Cambios (Entity Audit del expediente: actor, instante, entidad, operación, motivo) y Accesos (`CareAccessLog`: actor, instante, recurso, acción, resultado Permitido/Denegado/Fallido, sucursal, correlación); nunca contenido clínico ni nombres de archivo |
| Campos editables | Ninguno; filtros de fecha, actor, tipo de recurso, resultado |
| Acciones y navegación | Filtrar y paginar; abrir el expediente de origen solo si el usuario tiene acceso; no hay editar/borrar/exportar |
| Estados de UI | CC |
| Validaciones y errores | `SPA_ACCESS_DENIED`; la consulta del registro se registra a su vez |
| Permisos | `spa.audit.view`, `spa.access-log.view` (clínicos) |
| Integraciones | Auditoría oficial FROZEN (solo lectura), `CareAccessLog` |
| Estados y transiciones | Sin estados; solo lectura. |
| Responsive y recuperación | Filtros en `ZHDrawer`; tabla con desplazamiento interno. Recuperación: 403 genérico; la consulta queda registrada. |
| Pruebas | `SPA-AC-UI-14`: denegaciones registradas; consultar el registro deja registro; sin contenido clínico; PT-05/06/12 |

#### 23.4.15 Pantalla 15 — Panel inicial de Spa

| Elemento | Especificación operativa |
|---|---|
| Propósito | Mostrar la jornada y dar acceso a los procesos de Spa. |
| Datos visibles | Tarjetas operativas de la jornada (fecha y sucursal elegidas): citas de hoy por estado (conteos), visitas con llegada pendiente de atención, lista de espera activa (conteo), «Citas por revisar», tratamientos con seguimiento pendiente (conteo), accesos directos; **sin gráficos, KPI ni porcentajes** (HX-10) |
| Campos editables | Selector de sucursal y fecha |
| Acciones y navegación | Cada tarjeta abre Agenda (01), Visitas (17), Lista de espera (18), Reportes (19), Planes (08), Servicios (03); solo se muestran tarjetas con permiso |
| Estados de UI | CC; tarjeta sin permiso se omite; error parcial por tarjeta no bloquea el resto |
| Validaciones y errores | Conteos calculados en backend sobre datos autorizados; sin clínica |
| Permisos | `spa.agenda.view` + permiso de cada destino (el panel no tiene clave propia) |
| Integraciones | Consultas de Agenda, espera y planes |
| Estados y transiciones | Sin estados propios. |
| Responsive y recuperación | Tarjetas en una columna. Recuperación: error por tarjeta, resto visible. |
| Pruebas | `SPA-AC-UI-15`: conteos coinciden con la agenda; no existen métricas inventadas; sin fuga entre empresas; SPA-AC-H-11 |

#### 23.4.16 Pantalla 16 — Búsqueda de disponibilidad por servicio

| Elemento | Especificación operativa |
|---|---|
| Propósito | Encontrar profesional y horario para un servicio sin profesional previo. |
| Datos visibles | Resultados por profesional elegible con sus horarios libres (hora local de la empresa) para el servicio, fecha y sucursal elegidos; duración efectiva y preparación informativas |
| Campos editables | Cliente (`CustomerPicker`), servicio(s) (`ItemLookupPicker`), sucursal, fecha, paquete vinculado (opcional) |
| Acciones y navegación | `Buscar`, `Elegir horario` (selección explícita; **no confirma**), `Reservar` (llama a `ReserveVisit`, revalida), `Agregar otro servicio` (visita múltiple), `Anotar en lista de espera` si no hay resultados (→ 18) |
| Estados de UI | CC; sin resultados: mensaje y acción de lista de espera; si al reservar cambió la disponibilidad: mensaje + nueva búsqueda automática de la misma consulta, sin reservar |
| Validaciones y errores | `SPA_SERVICE_INACTIVE`, `SPA_PROFESSIONAL_UNAVAILABLE`, `SPA_APPOINTMENT_OVERLAP`, `SPA_PACKAGE_NO_BALANCE`/`EXPIRED` |
| Permisos | `spa.agenda.view` para buscar y `spa.agenda.create` para reservar |
| Integraciones | Motor único de agenda, directorio, Items, paquetes |
| Estados y transiciones | Búsqueda sin estado persistido; el resultado es válido hasta reservar y se revalida. |
| Responsive y recuperación | Resultados como lista por profesional. Recuperación: disponibilidad cambiada → nueva búsqueda automática sin reservar. |
| Pruebas | `SPA-AC-UI-16`: buscar no crea cita; elegir no confirma; reservar revalida; SPA-AC-H-01/02 |

#### 23.4.17 Pantalla 17 — Visitas y llegada

| Elemento | Especificación operativa |
|---|---|
| Propósito | Coordinar varios servicios y profesionales de una visita y registrar llegada. |
| Datos visibles | Visita: cliente, sucursal, fecha, citas/bloques con prestación, profesional, hora y estado; evento de llegada (hora y usuario); situación general **derivada** («sin iniciar», «en curso», «finalizada» como texto calculado, no un estado almacenado) |
| Campos editables | Añadir cita/servicio/profesional a la visita, motivos de cancelación/reprogramación |
| Acciones y navegación | `Registrar llegada`, `Agregar servicio`, `Reprogramar`/`Cancelar` una cita (no cancela las demás), `Iniciar atención` por cita (→ 09), ver historial |
| Estados de UI | CC; la visita se marca finalizada solo cuando todas sus citas están en estado final |
| Validaciones y errores | `SPA_APPOINTMENT_INVALID_STATE`, `SPA_APPOINTMENT_OVERLAP`, `SPA_CONCURRENT_MODIFICATION`; llegada duplicada es idempotente |
| Permisos | `spa.agenda.view/update/cancel` (llegada y cambios con `spa.agenda.update`) |
| Integraciones | Agenda, directorio, sesiones, paquetes (vínculo) |
| Estados y transiciones | Situación de visita derivada (sin máquina de estados); estados de cada cita según ADR §5.23. |
| Responsive y recuperación | Citas de la visita apiladas. Recuperación: 409 recarga la visita; llegada repetida idempotente. |
| Pruebas | `SPA-AC-UI-17`: llegada no crea sesión ni consumo; cancelar un bloque no cancela los demás; SPA-AC-H-07/08 |

#### 23.4.18 Pantalla 18 — Lista de espera y consultas operativas

| Elemento | Especificación operativa |
|---|---|
| Propósito | Conservar demanda pendiente y apoyar el seguimiento operativo. |
| Datos visibles | Entradas activas: cliente (nombre oficial mínimo), servicio, sucursal, rango de fechas deseado, nota, antigüedad (orden cronológico informativo), estado; consulta «Citas por revisar» |
| Campos editables | Cliente, servicio, sucursal, fechas deseadas, nota |
| Acciones y navegación | `Anotar`, `Retirar` (motivo), `Buscar horario` (abre 16 con los datos), `Marcar reservada` automáticamente al reservar desde la entrada; sin asignación ni prioridad automática |
| Estados de UI | CC |
| Validaciones y errores | `SPA_WAITLIST_DUPLICATE`; sin notificaciones automáticas (HX-02) |
| Permisos | altas `spa.agenda.create`, retiros `spa.agenda.cancel`, lectura `spa.agenda.view` |
| Integraciones | Agenda, BP, Items |
| Estados y transiciones | Entrada Activa → Cerrada (Reservada, Retirada o Cancelada); sin prioridad automática. |
| Responsive y recuperación | Lista apilada. Recuperación: duplicado activo muestra la entrada existente. |
| Pruebas | `SPA-AC-UI-18`: duplicado activo rechazado; reservar cierra la entrada como «Reservada»; sin asignación automática; SPA-AC-H-12 |

#### 23.4.19 Pantalla 19 — Reportes básicos y seguimiento operativo

| Elemento | Especificación operativa |
|---|---|
| Propósito | Consultar reportes operativos básicos y seguimiento de tratamientos. |
| Datos visibles | Catálogo cerrado de consultas básicas (tablas, sin gráficos): **R1** citas por período/estado/profesional/sucursal; **R2** ausencias y cancelaciones con motivo; **R3** sesiones realizadas por servicio/profesional (completas vs parciales); **R4** contratos de paquete: vigentes, por vencer, vencidos, saldos en cantidades; **R5** seguimiento de planes (sesiones previstas vs realizadas); **R6** lista de espera activa; **R7** «Citas por revisar» |
| Campos editables | Filtros: período (fecha de negocio), sucursal, profesional, servicio, estado |
| Acciones y navegación | Aplicar filtros, ordenar, paginar; abrir el registro de origen según permiso; **sin exportación** (no aprobada) |
| Estados de UI | CC |
| Validaciones y errores | Agregados sin identificar contenido clínico; saldo de paquete en cantidades, nunca monetario; sin porcentajes sin comparables |
| Permisos | `spa.reports.view` + permiso del dato de origen |
| Integraciones | Agenda, sesiones, paquetes, lecturas comerciales cuando aplique |
| Estados y transiciones | Sin estados propios. |
| Responsive y recuperación | Tablas con desplazamiento interno; filtros en `ZHDrawer`. Recuperación: filtro sin resultados muestra estado vacío con restablecer. |
| Pruebas | `SPA-AC-UI-19`: cada reporte coincide con los datos de origen; filtros no cruzan empresas; sin exportar; SPA-AC-H-13 |

## 24. Entidades y relaciones

**Modelo físico candidato detallado (tablas, claves, unicidades, versionado):** [ADR §5.16](ADR-051-spa-v1.1-architecture.md#516--526-diseño-técnico-implementable-selecciones-spa-sel-aprobadas) (SPA-SEL-01, recomendación). **Lectura vigente del modelo técnico:** ADR §5.11 contiene la descomposición preferente recomendada para revisión. La tabla siguiente conserva nombres candidatos históricos y trazabilidad; no compite con ADR ni reabre las alternativas allí seleccionadas. Ninguna clase/tabla existe por documentarla.

**Estado de la sección:** todas las entidades son **candidatas**, ninguna existe y su separación definitiva en tablas y agregados requiere aprobación. Los nombres siguen los borradores; `Session` significa
exclusivamente sesión realizada (§0.3).

| Grupo | Entidades candidatas | Referencias a módulos oficiales (sin copiar sus datos) |
|---|---|---|
| Agenda y visitas | Visita → una o varias citas del mismo cliente/ocasión/sucursal; cita → bloque profesional/intervalo (SPA-ARQ-01). `Appointment`, `AppointmentChange`, agregados/tablas y vínculos de prestación son candidatos técnicos; sin calendario de cabinas/equipos HX-01 | `BusinessPartner`, `Employee`, `Branch`, `Item` |
| Servicios | `SpaServiceProfile` o `Procedure` (`O-11`); relaciones opcionales servicio–sucursal y servicio–especialidad | `Item`; especialidades de Empleados |
| Plan y tratamiento operativo | Plan individual y ejecución multisesión/cupos/ampliación/suspensión/cancelación/cierre; nombres/agregados pendientes, sin fusionar con paquete | Expediente, procedimiento |
| Paquetes | `PackageDefinition`, `PackageContract`, `PackageSessionLedger` | Factura (id y línea) de Ventas |
| Sesiones | `Session` (atención realizada), preparación/formulario borrador y `SessionInterruption`: representación compatible con O-25 por definir | `Appointment`, `Employee`, `Branch` |
| Expediente | `CareRecord`, `MedicalHistoryVersion`, `CareRecordPersonalData` (solo si `O-06`), valoración facial y corporal, `BodyMeasurementSet` | `BusinessPartner` |
| Consentimientos | `ConsentDefinition`, `ConsentRecord`, `ConsentWithdrawal` | — |
| Fotografías | `CarePhoto`, grupo comparativo | `IFileStorage` mediante `ICareFileStore` |
| Seguridad y auditoría | `CareAccessLog` (unifica `PhotoAccessLog`), `CareAccessGrant` (solo si `O-01` C o D), solicitud de eliminación (solo si `O-05`) | `AuditRecordBase`, `AuditActor` |

Relaciones: un cliente (BP) tiene un expediente único por empresa (SPA-DEC-116; O-20 resuelta); el expediente contiene valoraciones, sesiones realizadas, consentimientos y fotografías; una cita (resultado de una reserva) puede dar lugar
a una sesión realizada; una sesión sugerida de un paquete es una cita vinculada al contrato (`O-25` #4); el contrato referencia una línea de factura (`O-15` #2); el consumo es un movimiento del libro del contrato
que referencia contrato y, según el caso, cita o sesión. Profesional (`EmployeeId`) y usuario registrador (usuario ERP) se guardan siempre por separado. SPA-ARQ-01 fija visita que agrupa una o varias citas/bloques; varios bloques de un servicio no duplican prestación comercial. Esquema/vínculos persistidos y número de sesiones clínicas por prestación pendientes; borradores no equivalen a atenciones realizadas. Operación inicial/lista/reportes reutilizan esos procesos, sin otro módulo por característica.

## 25. API y contratos de aplicación

**Integraciones F0:** ADR §5.12 selecciona consultas públicas existentes y dos lectores propietarios candidatos; cada llamada interna exige autorización explícita porque no ejecuta atributos del controlador. Contratos/rutas nuevos sin implementación o aprobación automática.

**Estado de la sección:** `SPA-API-01` [T]; **ninguna ruta, endpoint ni DTO está aprobado.** Rutas, métodos, peticiones/respuestas, permisos e idempotencia candidatos: ADR §5.17–5.18 (SPA-SEL-02/03, aprobadas el 2026-11-19; rutas y DTO siguen siendo candidatos hasta la autorización de cada fase). Cada operación deberá declarar permiso, scope, validación, errores, transacción, concurrencia, respuesta y pruebas, y
usar el envelope oficial de la API.

| Dominio | Capacidades conceptuales | Permiso candidato (§21.2) |
|---|---|---|
| Expedientes | Abrir, consultar, listar por cliente, registrar y corregir antecedentes, inactivar | `spa.records.*`, `spa.history.*` |
| Valoraciones | Registrar, consultar y corregir valoraciones y mediciones | `spa.assessments.*` |
| Consentimientos | Por procedimiento, firma en pantalla/versiones, registrar/consultar/retirar y administrar textos | `spa.consents.*` |
| Fotografías | Subir, consultar metadatos, ver (flujo autorizado), comparar, exportar, desactivar | `spa.photos.*`, `spa.records.export` |
| Agenda y visitas | Búsqueda por servicio/fecha/sucursal sin profesional previo, disponibilidad centralizada, selección explícita; citas/visitas con prestaciones y profesionales múltiples, llegada/cambios/reprogramación/cancelación; horarios/restricciones y lista de espera | `spa.agenda.*`, `spa.availability.manage` |
| Sesiones | Formulario/borrador, documentar/finalizar/interrumpir/corregir atención, resultados/productos/incidencias/evolución | `spa.sessions.create` solo candidato parcial; permisos de cada operación pendientes |
| Servicios | Consultar y administrar la configuración de servicios | `spa.services.*` |
| Paquetes | Individuales/combinados, vigencia/cupos/ampliación/sustitución; **crear contrato desde factura autorizada** (`O-15`); saldo en sesiones/movimientos independientes; consumo/restitución/ajuste según política | `spa.packages.*` |
| Integraciones | Consultar clientes, empleados y facturas por sus contratos oficiales (solo lectura) | Permisos de los módulos origen |
| Auditoría | Consultar historial de cambios y registro de accesos | `spa.audit.view`, `spa.access-log.view` |
| Tratamientos | Plan individual/multisesión, sesiones previstas/cupos, ampliar/suspender/cancelar/cerrar y seguir resultados | Sin claves definitivas recuperadas; no asignarlas automáticamente por permisos de valoraciones |
| Operación | Panel inicial, lista de espera, consultas/reportes básicos y seguimiento | Permisos por proceso/dato y catálogo específico pendientes; no se infiere exportación |

## 26. Validaciones y errores

**Estado:** todo PROPUESTO; catálogo consolidado de códigos por categoría en §31.16. Los códigos viven en `ApiResponseCodes` y `MessageCatalog`; el HTTP lo deriva la categoría (`Validation` 422, `BusinessRule` 400, `Duplicate` 409, `NotFound` 404,
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

**Algoritmo operativo completo (ventanas, traslados, reserva atómica, reprogramación, cierre administrativo, pruebas PostgreSQL):** ADR §5.20 (SPA-SEL-05, recomendación). **Selección técnica F0 recomendada:** ADR §5.11 concreta locks transaccionales, orden total, xmin/exclusión e idempotencia; fragmentos siguientes permanecen evidencia ilustrativa, no alternativas igualmente abiertas. Mecanismo recomendado sujeto a aprobación/validación PostgreSQL futura.

**Dirección APROBADA SPA-TEC-01.4/01.5:** §7.3/11.5 establece transacción/revalidación, bloqueos por profesional sin sucursal, orden multiprofesional, libro/idempotencia/cupo por prestación. SQL/índices/exclusiones/locks siguientes solo ilustrativos hasta verificación/aprobación física; no implementación.

SPA-ARQ-05 §11.2 reglas 3/4/7 exige impedir sobreprogramación por servicio en operaciones concurrentes, separar ocupación de capacidad de `Consumed`, evitar consumos duplicados y conservar movimientos históricos. Su cálculo/reserva lógica, transacciones, locks, índices e idempotencia siguen candidatos técnicos; el esquema siguiente no los aprueba.

**Obligación funcional aprobada:** aplicar §7.2 reglas 9/10: revalidación y prevención de compromisos incompatibles concurrentes; fallo de reprogramación conserva original. **Mecanismos siguientes PROPUESTOS / NO APROBADOS:** locks, orden, transacciones, índices y exclusiones requieren revisión ADR-051 y PostgreSQL real en una fase futura autorizada. SPA-ARQ-01/02/03 y expediente único conservan su alcance; ninguna fórmula SQL ilustrativa constituye diseño aprobado.

| Tema | Mecanismo candidato o estado delimitado | Evidencia |
|---|---|---|
| Citas del mismo profesional | Restricción `EXCLUDE USING gist (professional_id WITH =, tstzrange(start_utc, end_utc) WITH &&) WHERE (activa)`; reutiliza el patrón de `accounting_periods` (que usa `daterange`; **no hay precedente de `tstzrange`**, `V-05b`) | [NV] |
| Reserva y reprogramación | Advisory lock transaccional por profesional (patrón `AccountingOperationalPolicyRepository`) y token de versión (`xmin`) | [E] el patrón; el diseño [NV] |
| Consumo de sesiones | Advisory lock por contrato en una transacción; idempotencia por `(contract_id, session_id)` (`SPA-OPS-PKG-02`) | T |
| Creación del contrato | Único por factura y línea; el fallo no revierte la autorización (`O-15` #4, #5) | APROBADO el principio; mecanismo T |
| Saldo y capacidad de programación | Aplicar §11.2 regla 3: no sobreprogramar por servicio; algoritmo de saldo/reserva lógica y mecanismos físicos pendientes | APROBADO funcional; mecanismo T |
| Perfil de servicio | `UNIQUE (company_id, item_id)`; versión optimista | T |
| Número de ficha y expediente | Número único por empresa pendiente O-19; expediente único por cliente/empresa aprobado O-20, mecanismo de unicidad por verificar bajo concurrencia | A + T |
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
| PT-09 | Sesiones y atención | §8, §12 | SPA-AT-02, SPA-AT-04, SPA-AT-06, SPA-AT-07, SPA-CARE-CNS-05, SPA-CARE-CNS-06 | `SPA-T-09`, `SPA-OPS-T-VTA-02` | `SPA-CARE-T-CNS-05`, `SPA-CARE-T-CNS-06` | Terminología **resuelta por `O-25`** (2026-11-18); saldo/programación y ausencia funcionalmente precisados por SPA-ARQ-05 §11; detalles O-13 pendientes |
| PT-10 | Consentimientos y autorizaciones independientes | §17 | SPA-CN-01 a CN-06, SPA-INV-05, SPA-CARE-CNS-01 a CNS-08 | `SPA-T-10`, `SPA-T-11`, `SPA-CARE-T-CNS-01`, `SPA-CARE-T-FOT-01`, `SPA-SEC-T-12` | `SPA-CARE-T-CNS-02`, `SPA-CARE-T-CNS-03`, `SPA-CARE-T-CNS-04`, `SPA-CARE-T-CNS-07`, `SPA-CARE-T-FOT-02` | Firma en pantalla/versiones incluidas; mecanismo/conservación, menores/académico pendientes O-16..18/V-06 |
| PT-11 | Fotografías y almacenamiento | §18 | SPA-FO-01, SPA-CARE-FOT-01 a FOT-11, SPA-SEC-INV-02, SPA-SEC-INV-03, SPA-SEC-INV-05 | `SPA-SEC-T-06`, `SPA-SEC-T-07`, `SPA-SEC-T-08`, `SPA-SEC-T-09` | `SPA-CARE-T-FOT-03`, `SPA-CARE-T-FOT-06` | Cifrado y claves pendientes (O-04) |
| PT-12 | Corrección, retención y eliminación | §22 | SPA-FC-08, SPA-INV-06, SPA-SEC-RET-01 a RET-06, SPA-SEC-INV-04 | `SPA-CARE-T-EXP-03`, `SPA-SEC-T-10`, `SPA-SEC-T-11` | `SPA-CARE-T-FAC-02`, `SPA-CARE-T-COR-02`, `SPA-CARE-T-FOT-05`, `SPA-CARE-T-FOT-08` | **Abierto** (C-02; O-05, O-07) |
| PT-13 | Expediente | §13 | SPA-CARE-EXP-02, SPA-CARE-EXP-07 | `SPA-CARE-T-EXP-02`, `SPA-CARE-T-EXP-04` | — | Número de ficha pendiente O-19; cardinalidad única resuelta O-20 por SPA-DEC-116 |
| PT-14 | Agenda, disponibilidad y concurrencia | §7, §8 | SPA-AG-01 a AG-03, SPA-OPS-AGE-01 a AGE-14, SPA-AR-06 | `SPA-T-13`, `SPA-T-18`, `SPA-OPS-T-AGE-01`, `SPA-OPS-T-AGE-02`, `SPA-OPS-T-AGE-03`, `SPA-OPS-T-AGE-04`, `SPA-OPS-T-AGE-05`, `SPA-OPS-T-AGE-06` | — | Restricciones incluidas; cálculo/tolerancias y ausencia pendientes O-09/10; cabinas/equipos excluidos O-08; depende de Empleados |
| PT-15 | Servicios y tratamientos | §9, §19 | SPA-SV-01, SPA-SV-02, SPA-OPS-SRV-01 a SRV-12, SPA-IN-01 a IN-03 | `SPA-OPS-T-SRV-01`, `SPA-OPS-T-SRV-02`, `SPA-OPS-T-SRV-03`, `SPA-OPS-T-SRV-04` | — | Relación servicio-procedimiento e insumos pendientes (O-11, O-12) |
| PT-16 | Paquetes y saldos de sesiones | §11 | SPA-PQ-01, SPA-OPS-PKG-01 a PKG-07, SPA-AT-05 | `SPA-T-14`, `SPA-OPS-T-PKG-01`, `SPA-OPS-T-PKG-02`, `SPA-OPS-T-PKG-03`, `SPA-OPS-T-PKG-04`, `SPA-OPS-T-PKG-05`, `SPA-OPS-T-PKG-06` | — | Capacidades de paquetes aprobadas; políticas concretas O-13 pendientes; Venta/Reprogramación resueltas |
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

### 28.4 Aceptación de la evidencia histórica recuperada (propuestas SPA-AC-H)

Los enunciados funcionales están aprobados; **estos 16 escenarios son propuestas de verificación nuevas**, no pruebas ejecutadas ni nuevas decisiones históricas. Detalles de estados/cálculos/errores pendientes se fijan antes de implementar. No se modifican los 102 escenarios heredados ni los 13 casos O-15/O-25, y sus criterios antiguos se leen bajo las aprobaciones recuperadas.

| Caso | Evidencia | Criterio candidato y límite |
|---|---|---|
| SPA-AC-H-01 | SPA-DEC-115 #1..3 | Buscar con servicio/fecha/sucursal y sin profesional previo devuelve profesionales habilitados y horarios disponibles |
| SPA-AC-H-02 | SPA-DEC-115 #4..7 | Resultados respetan restricciones documentadas; recepción elige profesional/hora, sin confirmación automática y con el mismo motor central. Cálculos/valores quedan condicionados a reglas recuperadas |
| SPA-AC-H-03 | SPA-DEC-116 clientes | Un expediente por cliente/empresa; antecedentes/alertas/valoraciones/planes/sesiones/medidas/fotos/consentimientos/historial sin copiar identidad BP; duplicado concurrente a probar en PostgreSQL |
| SPA-AC-H-04 | SPA-DEC-116 tratamientos | Admitir plan individual/multisesión y sesiones previstas/cupos; distinguirlo de paquete y atención realizada |
| SPA-AC-H-05 | SPA-DEC-116 tratamientos | Ampliar/suspender/cancelar/cerrar y consultar resultados con trazabilidad, según condiciones/efectos por definir; sin factura/consumo automático |
| SPA-AC-H-06 | SPA-DEC-116 sesiones + O-25 | Preparar borrador y finalizar/interrumpir/corregir atención sin certificar realización o consumo por mero borrador; productos/incidencias/resultados/evolución y responsables conservados; parcial conservada según SPA-ARQ-03 §12; estado final de cita y efectos comerciales pendientes |
| SPA-AC-H-07 | SPA-DEC-116 visitas + SPA-ARQ-01 | Visita del mismo cliente/ocasión/sucursal agrupa una o varias citas, cada una con profesional/intervalo; servicio con varios profesionales puede usar varios bloques sin duplicar prestación comercial; sin inferir número de sesiones clínicas |
| SPA-AC-H-08 | SPA-DEC-115/116 + O-25 | Llegada/reprogramación/cambios trazables con prevención de conflictos; no confirmar por búsqueda ni generar sesión clínica por llegada/ausencia/cancelación; concurrencia PostgreSQL futura |
| SPA-AC-H-09 | SPA-DEC-116 consentimiento | Firmar en pantalla por procedimiento con versión y autorizaciones diferenciadas; no sustituir requisito por otra alternativa ni inferir certificación jurídica |
| SPA-AC-H-10 | SPA-DEC-116 paquetes + O-15/O-25 | Gestionar individual/combinado, vigencia/cupos/ampliación/sustitución y saldo en sesiones; contrato explícito factura/línea y movimiento independiente; efectos comerciales exactos según O-13 |
| SPA-AC-H-11 | SPA-DEC-116 panel | Consultar jornada y acceder a agenda/visitas/tratamientos/seguimiento/espera/reportes sin métricas o widgets aprobados inventados |
| SPA-AC-H-12 | SPA-DEC-116 espera | Lista de espera y consultas operativas disponibles e integradas con agenda única; sin prioridad/expiración/promoción automática inventadas |
| SPA-AC-H-13 | SPA-DEC-116 reportes | Reportes básicos/seguimiento operativo con permisos y sin dashboard analítico adicional, métricas no recuperadas ni datos de otra empresa |
| SPA-AC-H-14 | SPA-DEC-116 interfaz | Recorridos de agenda/expediente/tratamiento/sesión/consentimiento utilizables en computadora/tablet/celular; sin app independiente ni offline |
| SPA-AC-H-15 | SPA-DEC-116 integración/seguridad | Servicios/productos, facturas/CxC/cobros usan módulos oficiales; Employee reutilizado, profesional/registrador distintos; permisos/aislamiento/auditoría/archivos privados sin motores paralelos |
| SPA-AC-H-16 | SPA-DEC-116 exclusiones HX-01..10 | Ninguna fase añade disponibilidad cabinas/equipos, recordatorios automáticos, sistema nuevo de backups, reservas públicas, Kardex automático de aplicados, nómina/RRHH completo, app móvil independiente, offline, diagnóstico automático/IA, dashboard analítico adicional o contabilidad paralela |

### 28.5 Criterios de aceptación de SPA-ARQ-03

Ocho especificaciones de **pruebas futuras, no ejecutadas**. Verifican reglas aprobadas de §12; no aprueban esquema ni mecanismos técnicos, no resuelven C-02/O-13 ni la aplicación técnica del cierre administrativo aprobado (§8.7). Los escenarios anteriores se interpretan bajo §12, conservando sus identificadores y evidencia histórica.

| ID candidato nuevo | Escenario | Criterio de aceptación futuro |
|---|---|---|
| SPA-AC-ARQ03-01 | Guardar y continuar un formulario incompleto | Borrador permanece editable; no acredita sesión realizada ni completada y no registra consumo comercial |
| SPA-AC-ARQ03-02 | Iniciar atención efectiva | Se registra fecha/hora de inicio y profesional responsable/usuario ERP registrador separadamente, para cliente/empresa/sucursal/contexto correctos |
| SPA-AC-ARQ03-03 | Finalizar atención efectiva | Requiere acción explícita del profesional autorizado; conserva procedimientos/observaciones/productos/incidencias/resultados/evolución según servicio. Sin factura/cobro/asiento/movimiento de inventario por finalizar clínica |
| SPA-AC-ARQ03-04 | Detener un procedimiento iniciado | Se conservan motivo, momento y trabajo realmente realizado; Interrumpida no queda completada automáticamente ni asigna Atendida a la cita por inferencia |
| SPA-AC-ARQ03-05 | Registrar atención efectiva parcial | Registro clínico conservado en expediente, distinguible de completada y de cita programada/cancelada/ausente; sin sexto estado de cita ni reapertura clínica inventados |
| SPA-AC-ARQ03-06 | Corregir un registro finalizado | Solo operación autorizada, con motivo/autor/fecha y trazabilidad de versiones sujeta a política legal pendiente C-02; no nueva atención efectiva ni consumo adicional |
| SPA-AC-ARQ03-07 | Consultar seguimiento del tratamiento | Incluye todas las atenciones documentadas, también interrupciones con parcial; distingue evolución completa de parcial sin fusionarlas |
| SPA-AC-ARQ03-08 | Preparar, finalizar, interrumpir y corregir clínica | Ninguna operación clínica determina por sí misma consumo comercial. Borrador no consume; finalizada/parcial no fijan cantidad; sin descuento completo ni gratuidad inferidos, políticas O-13 pendientes |

### 28.6 Criterios futuros de SPA-ARQ-04

Especificaciones de pruebas futuras, **no resultados ejecutados**. Referencia normativa: §7.2. No aprueban mecanismos técnicos.

| Caso | Regla | Criterio de aceptación futuro |
|---|---|---|
| SPA-AC-ARQ04-01 | 1 — Horarios | Buscar excluye intervalos fuera de horario de sucursal/profesional o asignación/elegibilidad laboral; acceso ERP por sí solo no habilita disponibilidad. |
| SPA-AC-ARQ04-02 | 2 — Duración | Con servicios de distinta duración configurada, ofrecer solo intervalos suficientes para la duración efectiva, sin segundo catálogo comercial. |
| SPA-AC-ARQ04-03 | 3 — Descansos/ausencias/bloqueos | Excluir intervalos incompatibles con indisponibilidad del profesional y restricciones de sucursal; sin recursos físicos. |
| SPA-AC-ARQ04-04 | 4 — Preparación | Con preparaciones configuradas diferentes, reservar el intervalo necesario por servicio; sin margen universal añadido. |
| SPA-AC-ARQ04-05 | 5 — Traslado | Con tiempo configurado entre sucursales, impedir compromisos incompatibles; sin minutos ni rutas externas inferidos. |
| SPA-AC-ARQ04-06 | 6 — Consecutivas | Aceptar citas consecutivas compatibles y rechazar las que incumplan solape, preparación, descanso o traslado; sin margen adicional. |
| SPA-AC-ARQ04-07 | 7 — Multiprofesional | Validar cada bloque de la visita con su profesional, conservar agrupación y no duplicar prestación comercial. |
| SPA-AC-ARQ04-08 | 8 — Búsqueda | Servicio/fecha/sucursal sin profesional inicial devuelve alternativas del motor único; selección explícita sin crear/confirmar cita. |
| SPA-AC-ARQ04-09 | 9 — Concurrencia | Dos confirmaciones concurrentes incompatibles para el mismo profesional no se consolidan ambas; mecanismo PostgreSQL pendiente de diseño y prueba real.; aplicar dirección §7.3/27: transacción, lock por profesional sin sucursal y orden multiprofesional, mecanismo físico por verificar |
| SPA-AC-ARQ04-10 | 10 — Reprogramación | Si nuevo horario deja de estar disponible, conservar íntegra cita original y comunicar conflicto; cambio válido conserva identidad/Programada e historial/indicador, sin consumo. |
| SPA-AC-ARQ04-11 | 11 — Tiempo | Presentar con configuración oficial existente y UTC consistente; usar zona empresa si sucursal no dispone de configuración aprobada. Horas inexistentes/ambiguas no producen reservas inconsistentes. |
| SPA-AC-ARQ04-12 | 12 — Interrupción | Interrumpir no libera automáticamente tiempo ni genera consumo; cierre administrativo explícito y sus efectos según SPA-TEC-02.B §8.7, sin alterar intervalo histórico ni sexto estado |

### 28.7 Criterios futuros de SPA-ARQ-05

Diez especificaciones de pruebas futuras, **no resultados ejecutados**. Referencia funcional §11; no aprueban mecanismos técnicos ni cierran O-13 integralmente.

| Caso | Regla | Criterio de aceptación futuro |
|---|---|---|
| SPA-AC-ARQ05-01 | 1 — Composición | Paquete combinado conserva cantidades por servicio; rechazar intercambio libre/no autorizado, sin duplicar Items. |
| SPA-AC-ARQ05-02 | 2 — Vigencia | Vigencia configurable desde creación: vencido rechaza nueva reserva vinculada y consumo; identifica citas futuras para revisión sin cancelarlas ni borrar historia. |
| SPA-AC-ARQ05-03 | 3 — Cupos | Mostrar contratadas/consumidas/disponibles/programadas/realizadas separadas; rechazar programación/reprogramación sobre cupo por servicio también concurrente, sin `Consumed` por reservar. |
| SPA-AC-ARQ05-04 | 4 — Consumo | Finalizada no descuenta automáticamente; parcial requiere resolución autorizada. Borrador/programación/reprogramación no consumen; reintento no duplica movimiento independiente.; consumo y compensación idempotentes bajo §11.5, sin reescribir históricos |
| SPA-AC-ARQ05-05 | 5 — Ausencia | Registrar ausencia trazable sin descuento/penalización automática, cargo/factura/cobro o sesión clínica. |
| SPA-AC-ARQ05-06 | 6 — Cancelación | Cancelar antes de consumo no descuenta; después no revierte consumo confirmado ni al corregir clínica; compensar solo mediante operación autorizada trazable. |
| SPA-AC-ARQ05-07 | 7 — Ampliación | Rechazar ampliación sin documento comercial oficial; autorizada conserva contrato y movimientos previos, identifica servicio/cantidad/documento sin facturar en Spa. |
| SPA-AC-ARQ05-08 | 8 — Sustitución | Rechazar sustitución sin autorización/equivalencia definida; registrar servicios/cantidades/motivo sin alterar clínica ni movimientos confirmados; documento adicional según política pendiente.; equivalencia en condiciones o autorización documentada y datos de sustitución de §11.6 reglas 1–3, rechazar sin equivalencia válida |
| SPA-AC-ARQ05-09 | 9 — NC/anulación | Documento solo oficial Ventas; identificar derechos afectados y ajuste autorizado trazable, sin modificar historia clínica ni devolución monetaria paralela. |
| SPA-AC-ARQ05-10 | 10 — Movilidad | Permitir profesionales/sucursales habilitados de misma empresa bajo vigencia, servicio, permisos y disponibilidad; rechazar consumo entre empresas distintas. |

### 28.8 Criterios futuros de SPA-ARQ-06

Ocho requisitos para pruebas futuras, **no resultados ejecutados**. Referencia normativa §21.1. C-01 exige autorización arquitectónica especial y C-02 procedimiento/validación legal; ningún criterio aprueba sus mecanismos.

| Caso | Regla | Criterio de aceptación futuro |
|---|---|---|
| SPA-AC-ARQ06-01 | 1 | Un expediente por cliente/empresa integra sucursales conservando contexto de cada registro; rechazar duplicado por sucursal y separar expedientes de empresas aunque BP compartido, sin duplicar identidad. |
| SPA-AC-ARQ06-02 | 2 | Denegar consulta/modificación/corrección sin facultades clínicas expresas aunque existan permisos Ventas/Empleados o asignación laboral; profesional/registrador diferenciados. |
| SPA-AC-ARQ06-03 | 3 | Admin gestiona permisos sin acceso clínico ordinario por su condición; acceso excepcional solo justificado, autorizado, identificado, auditado y delimitado. Verificación efectiva bloqueada hasta autorización especial/revisión LOCKED; no basta spa.*. |
| SPA-AC-ARQ06-04 | 4 | Denegar acceso intersucursal no justificado por función/permisos/contexto; permitir solo bajo política autorizada, sin acceso general por trabajar en varias sucursales. |
| SPA-AC-ARQ06-05 | 5 | Auditoría sensible sin contenido clínico; comprobar persistencia durable antes de entrega; si falla no entregar, y fallo de log de denegación nunca autoriza (§22.4). RecordAsync solo no equivale a commit |
| SPA-AC-ARQ06-06 | 6 | Denegar fotos/archivos sin autorización o contexto válido, sin enlaces públicos permanentes, con protección/cifrado/claves y trazabilidad bajo diseño aprobado; aislamiento entre empresas y asociación aplicable. |
| SPA-AC-ARQ06-07 | 7 | Corrección autorizada trazable no muestra versión anterior como vigente y restringe sustituido; medidas legalmente exigibles solo bajo procedimiento controlado/auditable validado; rechazar borrado indiscriminado, sin certificar cumplimiento. |
| SPA-AC-ARQ06-08 | 8 | Rechazar consulta cruzada por error y edición/corrección/descarga de otra empresa sin autorización/alcance válidos; validar referencias y usuario multiempresa en contexto autorizado, sin cambiar scope BP ni exponer clínica en agenda. |

### 28.9 Criterios futuros de SPA-ARQ-07

Diez especificaciones para pruebas futuras, **no ejecutadas**; una por regla de §14.1/15.1/16/17.1/18/21.4. Gates técnicos/legales siguen pendientes.

| Caso | Regla | Criterio futuro |
|---|---|---|
| SPA-AC-ARQ07-01 | 1 | Documentar datos faciales de §14.1, incluidos adicionales anteriores; permitir omitir no obligatorios según procedimiento, Fitzpatrick I–VI cuando evaluado, sin diagnóstico automático. |
| SPA-AC-ARQ07-02 | 2 | Registrar peso kg y contornos cm con tres niveles abdominales y lados; no sustituir por cintura ni imponer rangos/decimales sin V-04. |
| SPA-AC-ARQ07-03 | 3 | Inicial/Control/Final con fecha/expediente/profesional; vacíos distintos de cero, sin todas medidas obligatorias ni número fijo de controles. |
| SPA-AC-ARQ07-04 | 4 | Consulta cronológica y variaciones solo con valores comparables; mediciones faltantes no generan valores ni atribución causal/diagnóstico. |
| SPA-AC-ARQ07-05 | 5 | Guardar/continuar valoración incompleta sin acreditar atención ni consumo; no equiparar borrador con sesión finalizada ni exigir campos universales. |
| SPA-AC-ARQ07-06 | 6 | Procedimiento que requiere consentimiento no se atiende sin uno válido; presentar documento previo y registrar versión/decisión/firma/identificación-trazabilidad bajo reglas aplicables, sin certificación inferida. |
| SPA-AC-ARQ07-07 | 7 | Autorización de procedimiento/seguimiento no habilita promoción; rechazar uso promocional sin autorización específica; académico permanece O-17 no aprobado. |
| SPA-AC-ARQ07-08 | 8 | Foto con fecha/contexto/finalidad/autorización/responsable y asociaciones aplicables; denegar acceso no autorizado, sin URL pública permanente ni publicación automática. |
| SPA-AC-ARQ07-09 | 9 | Rechazar corrección sin autor/momento/motivo/trazabilidad; versión sustituida no vigente ni sobrescrita silenciosamente; retiro/rectificación y supresión sujetos a validación legal, no conservación indefinida inferida. |
| SPA-AC-ARQ07-10 | 10 | Denegar acceso cruzado entre empresas o sin autorización clínica; respetar tenant/empresa/intersucursal, auditoría y protección, profesional/registrador separados; pendientes técnicos/legales no aprobados por el caso. |

### 28.10 Criterios futuros de SPA-ARQ-08

Ocho especificaciones de pruebas futuras, **no ejecutadas**; referencia normativa §4.2. No aprueban APIs ni mecanismos o implementación de dependencias.

| Caso | Regla | Criterio futuro |
|---|---|---|
| SPA-AC-ARQ08-01 | 1 | Cliente BP Customer por contrato autorizado, sin crear catálogo paralelo ni duplicar identidad/comercio; contexto empresa del expediente separado de scope tenant BP. |
| SPA-AC-ARQ08-02 | 2 | Consultar identidad/asignación/elegibilidad solo por contrato oficial de Empleados; rechazar acceso laboral sin contrato autorizado y Employee paralelo. IEmployeeDirectory pendiente, no implementarlo por el caso. |
| SPA-AC-ARQ08-03 | 3 | Usar Items Service y precio/impuestos oficiales, permitir individuales/combinados con datos operativos SPA; rechazar duplicación de catálogo y extensión inferida Items/Pricing. |
| SPA-AC-ARQ08-04 | 4 | Contrato explícito factura/línea tras validar autorizada, fuera de transacción Ventas, sin duplicados/reversión por fallo SPA; rechazar creación por SalesInvoiceAuthorizedEvent, sin resolver fiscal/crédito/NC. |
| SPA-AC-ARQ08-05 | 5 | Consultar información autorizada necesaria de Caja/CxC; rechazar cobros/CxC/asientos/saldos monetarios generados desde SPA o anticipos propios; O-14 abierto. |
| SPA-AC-ARQ08-06 | 6 | Registrar productos aplicados técnicamente no mueve Kardex; venta física sigue procesos oficiales, rechazar consumo automático e inventario paralelo (HX-05). |
| SPA-AC-ARQ08-07 | 7 | Permisos generales/Empleados/Ventas no dan acceso clínico; integrar Identity/Access/Kernel/auditoría oficiales sin paralelo ni tocar LOCKED/FROZEN. C-01/C-02 siguen gates, sin dar por probado bypass resuelto. |
| SPA-AC-ARQ08-08 | 8 | SPA genérico sin marca como dependencia, interfaz SPA; denegar expedientes/paquetes de otra empresa fuera de alcance autorizado y traslado de derechos entre empresas; validar tenant/empresa/sucursal/elegibilidad, sin cliente clínico duplicado. |

### 28.11 Consolidación SPA-TEC-01/02/03 — cobertura futura

**Especificaciones de pruebas futuras, no ejecutadas.** Reutilizar SPA-AC-ARQ04-07/09/10 para prestación multiprofesional/concurrencia intersucursal/reprogramación atómica; ARQ05-04/08 para idempotencia/compensación y equivalencia; ARQ06-03/05/06 para Admin/auditoría durable/archivos; ARQ07-03 para medida ausente ≠ cero; ARQ08-04/05 para no creación automática ni finanzas SPA. No duplicar esos casos ni presentarlos como evidencia de implementación.

| Caso nuevo | Cobertura adicional |
|---|---|
| SPA-AC-TEC02-A01 | Backend/recurso deniega Admin ordinario; excepción solo por otra persona facultada, sin autoautorización, temporal/delimitada/auditada; autorización no comprobable o auditoría fallida impiden entrega (§21.5). |
| SPA-AC-TEC02-B01 | Cierre explícito autorizado Atendida tras atención real interrumpida; responsable/fecha/motivo/referencia, clínica sigue Interrumpida/parcial. Libera ocupación futura sin borrar eventos ni cambiar intervalo original; sin consumo ni cupo liberado por inferencia (§8.7/11.5). |
| SPA-AC-TEC02-C01 | Prestación multiprofesional reserva un cupo; reprogramar no duplica. Cancelar antes de atención sin consumo libera; parcial mantiene cupo hasta resolución autorizada consume/libera con evidencia, idempotente y sin tocar históricos (§11.5). |
| SPA-AC-TEC03-01 | Relacionar plan/prestaciones/contratos/facturas/líneas por referencias reales; rechazar vínculo basado solo en mismo BP. Plan previo/parcialmente contratado y parcialmente facturado conserva separación Propuesto/Contratado/Facturado/Pagado/Realizado (§10.2). |
| SPA-AC-TEC03-02 | Rechazar transferencia automática o sin autorización/respaldo/alcance/identidad/consentimiento verificables; excepción documentada no traslada expediente, sujeta a validación jurídica (§11.6 regla 4). |
| SPA-AC-TEC03-03 | Ampliación con contrato/documento/línea/nuevos derechos conserva historia y no extiende originales automáticamente; extensión expresa respaldada y vigencia por procedencia. |
| SPA-AC-TEC03-04 | Derechos vencidos no admiten nuevas reservas/consumos ordinarios; citas existentes identificadas a revisión manual sin cancelación automática ni pérdida de historial. |
| SPA-AC-TEC03-05 | Lecturas oficiales pagos/abonos/cuotas/vencimientos/saldo, sin reconstruir finanzas; cuotas pendientes no suspenden sesiones automáticamente. UI consulta referencias verificables, sin pantallas financieras duplicadas ni porcentaje sin datos comparables. |

Criterios de datos reales sujetos a seguridad/ley, C-01/C-02 y autorizaciones; candidato numeric(10,2) no aprueba escala definitiva ni sustituye V-04.

### 28.12 Verificación futura de la selección técnica F0

Extender los criterios existentes de §28.11, sin ejecutarlos: API directa/MediatR/archivos/reportes/menú y autoescalamiento bajo C-01; commit durable real de lecturas y denegaciones independiente del rollback de negocio, sin reproducción supuesta por Outbox; aislamiento y autorización de consulta del log; locks ordenados y conflictos multiprofesionales/intersucursal; reprogramación rollback íntegro; sustitución/compensación/transferencia idempotentes por scope/lote sin sobrecompensación; integridad por empresa; descifrado rechaza tag/contexto incorrectos y rutas manipuladas; recuperación de claves y supresiones antes de reexponer respaldos; lectores nuevos preservan permisos/scope sin conceder reversión de cobros para leer abonos. Evidencia PostgreSQL/operativa posterior solo bajo autorización propia; precisión definitiva y legal no se prueban por inferencia.

## 29. Dependencias bloqueantes

### 29.1 Bloqueos técnicos, legales y de autorización

| ID | Pendiente concreto | Afecta | Condición de resolución |
|---|---|---|---|
| BL-01 | Employee/directorio todavía no implementados; firmas y elegibilidad histórica por verificar | Profesionales, disponibilidad, atenciones | SPA-ARQ-08 confirma dependencia, no implementación; EMP-E6b bajo plan/autorización propios, contrato IEmployeeDirectory verificado; no Employee Spa |
| BL-02 | Mecanismos de acceso Admin/intersucursal, lecturas y protección §21.1 | Clínica, archivos y consultas | SPA-ARQ-06 funcional aprobada; C-01/O-01 mecanismo BLOQUEADO HASTA AUTORIZACIÓN ESPECIAL LOCKED; O-02..04 técnicos/operativos pendientes, FROZEN intacta |
| BL-03 | Conservación/no borrado, retiro y posible eliminación legal | Antecedentes, sesiones, consentimientos y fotos | C-02/O-05 OBJETIVO FUNCIONAL APROBADO — PROCEDIMIENTO Y VALIDACIÓN LEGAL PENDIENTES; O-07, sin borrado general ni plazo inventado |
| BL-04 | Anticipos/prepagos transversales sin diseño | Solo operaciones que dependan de anticipos | O-14 ABIERTA; origen de contrato ya resuelto O-15 |
| BL-05 | Contraste de formularios/versiones y detalles anteriores no recuperados | Campos/validaciones y reglas concretas | SPA-ARQ-07 contenido aprobado; O-24/V-01 y obligatoriedad específica O-21/evidencia histórica pendientes. **No bloquea aprobación general de alcance: O-23 cerrada en inclusión** |
| BL-06 | Validez jurídica aplicable, firma/menores/plazos e imágenes | Protección de datos y evidencia | O-07/O-16/O-18, V-06/V-09; firma en pantalla ya incluida, no se sustituye por otra alternativa |
| BL-07 | Precisión/escala numérica corporal tras SPA-ARQ-07 (medidas/unidades/finalidad aprobadas) | Medidas y comparación | O-22/V-04, revisión arquitectónica formal |
| BL-08 | Detalle técnico y comercial restante de §11.3/11.4 tras SPA-ARQ-05 | Tratamientos, paquetes, sesiones | O-13 y pendientes O-15; capacidades aprobadas, efectos no inventados |
| BL-09 | Aprobación técnica integral de contrato/ADR y autorización de fases | Toda implementación | ADR-051 ACCEPTED (2026-11-19); fronteras SPA-ARQ-08 funcionalmente aprobadas, contratos técnicos pendientes; plan sin fases ni preparación de código autorizadas; no reabrir inclusión funcional |
| BL-10 | Modelo conceptual visita–citas resuelto SPA-ARQ-01; pendientes esquema/vínculos, presentación técnica de la situación derivada SPA-ARQ-02, cardinalidad clínica por prestación, persistencia/concurrencia del ciclo SPA-ARQ-03 y aplicación técnica del cierre administrativo aprobado (§8.7), cálculo de restricciones | Agenda, visitas, sesiones y concurrencia | Detalle técnico sin reabrir modelo aprobado; V-05 y pruebas PostgreSQL futuras; sin inferir tiempos, mecanismos de parcial, aplicación técnica del cierre administrativo aprobado (§8.7) o ausencias |

### 29.2 Distinción de aprobación y ejecución

Las capacidades de §2 y exclusiones de §3 están aprobadas. Ninguna fase es implementable por este encargo: aprobación funcional no es diseño técnico completo ni permiso de programar. O-08 (disponibilidad cabinas/equipos excluida), O-20 (expediente único) y O-23 (inclusión general) no vuelven a figurar como bloqueos funcionales. El detalle pendiente de cada superficie está en §23 y §30.

### 29.3 Matriz de implementabilidad

| Funcionalidad | Estado funcional | ¿Implementable por este encargo? | Pendientes aplicables |
|---|---|---|---|
| Disponibilidad, agenda y visitas | APROBADO SPA-DEC-115/116; O-25 íntegra | No | BL-01/09/10, O-09/10/11 de detalle |
| Tratamientos, sesiones y evolución | APROBADO SPA-DEC-116; atención/consumo O-25 | No | BL-01/05/08/09/10 |
| Paquetes y creación del contrato | APROBADO SPA-DEC-116/O-15/O-25 | No | BL-08/09; BL-04 solo si depende de anticipos |
| Expediente único y valoraciones | APROBADO SPA-DEC-116 | No | BL-02/03/05/07/09 |
| Consentimiento/firma en pantalla y fotos privadas | APROBADO SPA-DEC-116 | No | BL-02/03/05/06/09 |
| Integraciones comerciales oficiales | APROBADO SPA-DEC-116; frontera O-15/DIR | No | BL-08/09, lectura/estado SRI/crédito; O-14 cuando corresponda |
| Panel, espera, reportes básicos y responsive | APROBADO SPA-DEC-116 | No | BL-02/09, detalle de consultas/interfaz y lista; sin dashboard adicional |
| Seguridad, auditoría y conservación | SPA-ARQ-06 funcional APROBADA; C-01 técnico bloqueado y C-02 procedimiento/ley pendientes | No | BL-02/03/06/09 |

## 30. Estado de decisiones y evidencia pendiente

### 30.1 Aprobadas e instrucciones del propietario

| Fuente | Estado y alcance | Evidencia |
|---|---|---|
| SPA-DEC-115 | APROBADA histórica: búsqueda por servicio/fecha/sucursal sin profesional previo; selección explícita y motor único, sin confirmar automáticamente | [REG §9](SPA-V1.1-APPROVED-DECISIONS-01.md) |
| SPA-DEC-116 | APROBADA histórica: once áreas, siete agrupaciones y diez exclusiones; orden de fases preliminar | [REG §10](SPA-V1.1-APPROVED-DECISIONS-01.md) |
| SPA-ARQ-01 | APROBADA 2026-11-19: visita agrupa citas/bloques, servicio multiprofesional sin duplicación comercial, sesiones realizadas independientes y cancelación no propagada | [REG §12](SPA-V1.1-APPROVED-DECISIONS-01.md#12-spa-arq-01--modelo-conceptual-de-visitas-citas-y-sesiones) |
| SPA-ARQ-02 | APROBADA 2026-11-19: cinco estados de cita, llegada como evento, inicio/fin independientes, situación derivada y finalización de visita; reprogramación evento/indicador, identidad/Programada e historial, sin consumo automático | REG §13; contrato §8.6 |
| SPA-ARQ-03 | APROBADA 2026-11-19: ciclo clínico Borrador/En atención/Finalizada/Interrumpida, parcial conservada, corrección autorizada/versiones bajo C-02, evolución completa/parcial y consumo independiente | REG §14; referencia funcional principal §12 |
| SPA-ARQ-04 | APROBADA 2026-11-19: doce reglas funcionales de agenda/disponibilidad; detalles técnicos físicos pendientes; dirección TEC-01 aprobada | REG §15; referencia principal §7.2 |
| SPA-ARQ-05 | APROBADA 2026-11-19: diez reglas de tratamientos/paquetes; O-13 parcialmente resuelta, direcciones TEC-01/02/03 aprobadas y mecanismos físicos pendientes | REG §16; referencia principal §11 |
| SPA-ARQ-06 | APROBADA 2026-11-19: ocho reglas de expedientes/seguridad; O-01..05 parcialmente resueltas, C-01 y C-02 visibles | REG §17; referencia normativa §21 |
| SPA-ARQ-07 | APROBADA 2026-11-19: diez reglas de valoraciones/consentimientos/fotografías; mecanismos/ley/originales pendientes | REG §18; §14–18/21 |
| SPA-ARQ-08 | APROBADA 2026-11-19: ocho fronteras funcionales ERP; contratos/mecanismos pendientes, sin extensiones ni preparación de código autorizadas | REG §19; referencia normativa §4 |
| SPA-TEC-01 | APROBADA POR EL PROPIETARIO 2026-11-19: nueve direcciones aprobadas; precisión numeric(10,2) solo propuesta preferente, no estándar definitivo | REG §20; referencias principales §4.3/7.3/8.7/10.2/11.5/11.6/15.2/18.1/21.5/22.4/27 |
| SPA-TEC-02 | APROBADA POR EL PROPIETARIO 2026-11-19: seguridad sin autoautorización, cierre administrativo Atendida sin falsa Finalizada y retención/resolución de cupos aprobados; C-01 especial pendiente | REG §21; referencias principales §4.3/7.3/8.7/10.2/11.5/11.6/15.2/18.1/21.5/22.4/27 |
| SPA-TEC-03 | APROBADA POR EL PROPIETARIO 2026-11-19: diez reglas y plan/prestaciones/contratos/documentos/pagos explícitos; equivalencias/transferencias/vigencias/cuotas precisadas, fiscal/ley pendientes | REG §22; referencias principales §4.3/7.3/8.7/10.2/11.5/11.6/15.2/18.1/21.5/22.4/27 |
| O-25 | APROBADA: cita distinta de sesión realizada y consumo independiente; sin consumir por reservar/reprogramar | REG §1, literal preservado §8.1 |
| O-15 | APROBADA: creación explícita desde factura/línea, fuera de autorización Ventas; sin duplicados/documentos propios | REG §2, literal preservado §11.1 |
| DIR-SSOT-01 | INSTRUCCIÓN DEL PROPIETARIO: Anticipos como proceso transversal, no diseño financiero ni cierre O-14 | REG §7; contrato §20.4 |
| ADR-050 | Decisiones aplicables de Employee reutilizable, profesional/registrador y permisos; directorio aún por implementar | REG §8 y ADR-050; sin modificación de Empleados |

Las fechas históricas exactas de 115/116 no fueron aportadas. Recuperación registrada 2026-11-18; aprobación anterior a planificación de Empleados. No inventar actas/fechas ni SPA-DEC-001..114.

### 30.2 Cuestiones O-xx: resueltas, parciales y abiertas

Se conservan los identificadores originales; no se cuenta toda cuestión como abierta después de recuperar la evidencia.

| O | Estado vigente | Detalle aún pendiente o cierre respaldado |
|---|---|---|
| O-01 | POLÍTICA FUNCIONAL APROBADA — MECANISMO TÉCNICO BLOQUEADO HASTA AUTORIZACIÓN ESPECIAL | SPA-TEC-02.A §21.5 aprueba backend/recurso incluso bypass y excepción temporal delimitada autorizada por otra persona, sin autoautorización; mecanismo detallado/revisión especial LOCKED pendientes, Kernel no autorizado; C-01 no cerrado |
| O-02 | POLÍTICA FUNCIONAL APROBADA — MECANISMO TÉCNICO PENDIENTE | Regla 4: acceso intersucursal por función/permisos/contexto justificados, sin acceso general por asignación laboral |
| O-03 | OBLIGACIÓN FUNCIONAL APROBADA — MECANISMO PENDIENTE | Regla 5 y SPA-TEC-01.6 §22.4: persistencia durable comprobada antes de entregar clínica, fallo deniega; mecanismo/confiabilidad/retención pendientes; FROZEN intacta |
| O-04 | PROTECCIÓN FUNCIONAL APROBADA — DISEÑO TÉCNICO/OPERATIVO PENDIENTE | Regla 6/SPA-TEC-01.7 §18.1: adaptador, ids/rutas controlados y autorización por descarga aprobados como dirección; cifrado/claves/restauración/backups específicos por revisar sin sistema nuevo |
| O-05 | OBJETIVO FUNCIONAL APROBADO — PROCEDIMIENTO Y VALIDACIÓN LEGAL PENDIENTES | C-02/regla 7: corrección/versiones, restricción y medidas legales controladas; sin borrado físico general ni cumplimiento certificado |
| O-06 | ABIERTA | Datos propios de edad/nacimiento/emergencia; no duplicar BP |
| O-07 | ABIERTA | Validación jurídica aplicable, no sustituida por aprobación funcional |
| O-08 | RESUELTA para recursos físicos | Control de disponibilidad de cabinas/equipos EXCLUIDO por SPA-DEC-116 HX-01; no añadirlo como alternativa pendiente |
| O-09 | PARCIALMENTE RESUELTA | SPA-ARQ-04 §7.2 resuelve horarios, duración, restricciones, consecutivas, revalidación/conflictos, UTC y no liberación automática; valores, configuración/representación, algoritmos/locks, traslados/excepciones y detalles no aportados pendientes; sin márgenes nuevos ni confirmación por búsqueda; recordatorios excluidos |
| O-10 | ABIERTA | Atención sin cita previa; llegada aprobada no equivale a aprobar walk-in |
| O-11 | PARCIALMENTE RESUELTA | Varios servicios/profesionales por visita APROBADOS; agrupación visita–citas aprobada SPA-ARQ-01; esquema/vínculos de prestación y relación servicio–procedimiento pendientes |
| O-12 | PARCIALMENTE RESUELTA | Registro técnico de productos y venta oficial incluidos; descuento Kardex automático EXCLUIDO; campos/cantidades o ajustes manuales requieren regla propia |
| O-13 | PARCIALMENTE RESUELTA | SPA-ARQ-05 §11 resuelve composición por servicio sin intercambio libre, vigencia desde creación, cupos programados sin consumo y prevención de exceso, consumo independiente/no duplicado, ausencia sin penalización automática, cancelación/compensación, ampliación respaldada, requisitos de sustitución, revisión por NC y movilidad intraempresa. SPA-TEC-02/03 resuelve cupos retenidos/resolución, equivalencia documentada, transferencia excepcional respaldada, ampliación/vigencia por derechos y relación plan/documentos. Pendientes solo detalles específicos de equivalencias/documentación/transferibilidad, efectos económicos/fiscal/ley y mecanismos físicos. No cierre integral |
| O-14 | ABIERTA | Anticipos/prepagos: proceso transversal independiente, sin diseño autorizado |
| O-15 | APROBADA | Origen y frontera del contrato preservados; significado de autorizada/SRI, NC/crédito y lista de pendientes fuera de esa aprobación |
| O-16 | PARCIALMENTE RESUELTA | SPA-ARQ-07 §17.1: documento previo si requerido, procedimiento/versión/decisión/firma e identificación/trazabilidad y finalidades independientes; autenticidad, vigencia, identidad específica y validez jurídica pendientes |
| O-17 | ABIERTA | SPA-ARQ-07 no incorpora uso académico; finalidad no aprobada, sin inferir inclusión |
| O-18 | ABIERTA | Menores/representantes, identificación y evidencia jurídica no resueltos por SPA-ARQ-07 |
| O-19 | ABIERTA | Numeración y significado de código del caso; no permite varios expedientes por empresa |
| O-20 | RESUELTA | Expediente único por cliente y empresa, SPA-DEC-116; exclusión de duplicados por caso/sucursal |
| O-21 | PARCIALMENTE RESUELTA | SPA-ARQ-07 §14.1/15.1: contenido y borradores aprobados, sin obligatoriedad universal; mínimos/validaciones por procedimiento pendientes |
| O-22 | PARCIALMENTE RESUELTA | Medidas/unidades, Inicial/Control/Final, vacío distinto de cero y comparación aprobados SPA-ARQ-07; escala/precisión V-04, rangos, frecuencia concreta/final único pendientes; sin cantidad fija de controles |
| O-23 | CERRADA EN INCLUSIÓN FUNCIONAL GENERAL | SPA-DEC-116 recuperada con declaración expresa de alcance y exclusiones; no cierra técnica/ley/implementación. Fichas separadas O-24/V-01, detalles históricos restantes §30.3 |
| O-24 | ABIERTA | Originales/versiones V-01 sin contraste; SPA-ARQ-07 aprueba contenido funcional pero no certifica equivalencia/inspección |
| O-25 | APROBADA | Reserva/cita/atención/consumo y trazabilidad preservados; no revoca multiplicidad de visita aprobada históricamente |

### 30.3 Evidencia restante y tensiones delimitadas

**Revisión técnica concentrada F0 — selección recomendada, no aprobación nueva:** ADR §5.10–5.15 y REG §23 distinguen mecanismos preferentes contrastados estáticamente de dirección aprobada, autorización especial, validación externa y dependencia no implementada. No se incluye esta recomendación entre las aprobaciones del propietario ni se acepta integralmente el ADR.

- **Historial:** SPA-DEC-115/116 recuperadas según contenido aportado en REG; no se recuperaron los textos SPA-DEC-001..114. Solo los detalles que dependan de ellos se mantienen por verificar; no se invalida alcance general.
- **Formularios:** O-24/V-01 y obligatoriedad O-21; no se presume equivalencia de versiones ni inspección de originales.
- **Técnica y legal:** directorio real V-02, devolución Service completa V-03a, precisión V-04, tiempo/concurrencia V-05, asesoría V-06/vigencia V-09 e infraestructura V-07/08 siguen según evidencia disponible.
- **Modelo visita/cita resuelto:** SPA-ARQ-01 fija visita → una o varias citas del mismo cliente/ocasión/sucursal y cita → bloque de profesional/intervalo. Varios bloques no duplican la prestación comercial; cancelación de una cita no cancela automáticamente las demás. Situación de visita derivada aprobada SPA-ARQ-02; cardinalidad clínica, esquema/vínculos y concurrencia siguen pendientes; O-25 intacta.
- **Ciclo clínico resuelto por SPA-ARQ-03:** aplicar §12. Pendientes solo mecanismos técnicos, aplicación técnica del cierre administrativo aprobado (§8.7), cardinalidad clínica por prestación, política legal C-02 y efectos comerciales O-13; no inferir Atendida, gratuidad o consumo.
- **Comercial:** origen O-15 intacto; detalle de autorizada/SRI/crédito/NC, reglas funcionales de paquetes aprobadas SPA-ARQ-05 §11; pendientes algoritmo, equivalencias/documento adicional, fiscal/crédito y ajustes económicos específicos. Anticipos O-14 sigue separado.
- **Sin revocación inferida:** ninguna evidencia aportada revoca expresamente O-15/O-25 ni las capacidades históricas. Las fronteras de procesos oficiales, exclusiones y detalle pendiente se conservan conjuntamente.
- **Aprobaciones técnicas y ejecución:** ADR-051 ACCEPTED / APROBADO y SPA-SEL-01..12 aprobadas el 2026-11-19 (REG §25); plan sin fase autorizada y COND-01..13 sin cumplir. No comenzar desarrollo sin autorización expresa de cada fase.

## 31. Especificación operativa por área funcional

**Estado:** completa el contrato para su futura implementación **sin alterar** las decisiones aprobadas (SPA-DEC-115/116, SPA-ARQ-01..08, SPA-TEC-01..03, O-15, O-25), las 19 fichas ni las diez exclusiones (§3.1.1). Cada fila condensa y operacionaliza reglas ya existentes en §5–§22; **los detalles que no estaban aprobados se resuelven con un patrón estándar y se marcan SPA-SEL-nn ([ADR §5.16–5.26](ADR-051-spa-v1.1-architecture.md#516--526-diseño-técnico-implementable-selecciones-spa-sel-aprobadas))**: el propietario las aprobó el 2026-11-19 (REG §25), sujetas a COND-01..13. Rutas, tablas, DTO y permisos son candidatos hasta la autorización de la fase. Los criterios `SPA-AC-OP-nn.m` son pruebas futuras, no ejecutadas. Los asuntos protegidos o legales no se dan por cumplidos: §31.15.

### 31.1 Perfiles operativos y matriz de permisos

Los perfiles siguientes son **composiciones sugeridas de permisos** sobre los perfiles de acceso oficiales de cada empresa (no crean roles, usuarios ni autenticación paralelos). Los permisos clínicos exigen concesión explícita; ser Admin, tener `employees.*` o estar asignado a una sucursal no los concede.

| Perfil sugerido | Puede (permisos candidatos) | No puede |
|---|---|---|
| Recepción / agenda | `spa.agenda.view/create/update/cancel/mark-absent`, lectura de clientes (MasterData) | Ver clínica, fotos, consentimientos; consumir cupos |
| Profesional | Recepción en solo lectura de su agenda; `spa.records.view`, `spa.history.view`, `spa.assessments.view/create/update`, `spa.treatment-plans.view/manage`, `spa.sessions.view/create`, `spa.consents.view/manage`, `spa.photos.view/create` | Corregir sesiones finalizadas, cierre administrativo, ajustar paquetes |
| Responsable clínico | Profesional + `spa.sessions.manage` (finalizar, interrumpir, corregir, cierre administrativo), `spa.history.update`, `spa.photos.manage` | Definir paquetes, ver registro de accesos |
| Coordinación comercial | `spa.packages.view/manage/consume/adjust`, `spa.reports.view`, lectura de Ventas/CxC | Ver clínica |
| Administración de Spa | `spa.services.view/manage`, `spa.availability.manage`, `spa.consents.templates`, `spa.reports.view` | Acceso clínico por ser administrador |
| Auditoría | `spa.audit.view`, `spa.access-log.view` | Modificar datos; cada consulta queda registrada |

### 31.2 Clientes y expedientes

| Elemento | Especificación |
|---|---|
| Objetivo | Conservar un expediente clínico-estético único por cliente y empresa, sin duplicar la identidad de BusinessPartner |
| Datos de entrada | BP del cliente (selección oficial), antecedentes por categoría (Sin responder/No/Sí + nota), alertas (texto), motivo de corrección |
| Datos que se guardan | `CareRecord` (BP, empresa, estado, número de ficha opcional), revisiones de antecedentes, alertas, sucursal de cada registro, auditoría de cambios y accesos. Nombre/contacto se leen de BP en cada consulta |
| Acciones | Abrir, consultar, listar por cliente, revisar antecedentes, añadir/desactivar alertas, inactivar/reactivar; correcciones autorizadas |
| Flujo principal | Buscar cliente → abrir expediente (si no existe) → registrar antecedentes iniciales (opcional) → trabajar desde las pestañas |
| Flujos alternativos | Cliente inexistente: remitir al alta oficial de MasterData, sin crear BP en Spa; expediente ya existente: devolver el existente; BP inactivo: expediente consultable, sin atenciones nuevas hasta reactivar; acceso de otra sucursal: según guard intersucursal |
| Validaciones | BP activo con rol Customer; un expediente por `(tenant, empresa, BP)`; «Sin responder» ≠ «No»; motivo obligatorio en toda corrección; sin campos clínicos obligatorios (O-21) |
| Estados y transiciones | Activo ↔ Inactivo (nunca eliminar); cada revisión de antecedentes es inmutable y la vigente es una sola |
| Reglas clínicas | Alertas sin niveles, categorías ni bloqueos automáticos (no recuperados); fecha de nacimiento y contacto de emergencia no se capturan hasta O-06; número de ficha sin secuencia hasta O-19 |
| Usuarios y permisos | `spa.records.view/manage`, `spa.history.view/update`; Admin sin acceso ordinario (COND-01) |
| Integraciones | `GetBusinessPartnerQuery`, `GetBusinessPartnerRolesQuery` (con permiso MasterData vigente), directorio de Empleados, auditoría |
| Errores y fallos | `SPA_CARE_RECORD_EXISTS`, `SPA_CLIENT_INACTIVE`, `SPA_CORRECTION_REASON_REQUIRED`, `SPA_ACCESS_DENIED`; si falla el registro de auditoría no se entrega el contenido |
| Salidas, responsable de cada dato y dependencias | **Salidas:** expediente (sin identidad copiada), listado por cliente, historial de revisiones. **Responsable de cada dato:** identidad y contacto = MasterData (BP); antecedentes, alertas, estado y sucursal por registro = Spa; permisos y usuario = Identity/Access; auditoría = infraestructura oficial. **Dependencias:** F1 (guard, log), F2.a; BP y Empleados. **Pruebas:** `SPA-AC-OP-01.x`, PT-01/05/12. |
| Criterios de aceptación | `SPA-AC-OP-01.1` dos aperturas concurrentes producen un solo expediente; `01.2` otra empresa no ve el expediente; `01.3` corregir conserva la revisión previa como no vigente; `01.4` el listado de clientes no muestra clínica; `01.5` Admin sin concesión recibe denegación y queda registrada |

### 31.3 Profesionales

| Elemento | Especificación |
|---|---|
| Objetivo | Identificar quién presta y quién registra cada atención reutilizando el directorio oficial de Empleados |
| Datos de entrada | `EmployeeId` elegido desde el directorio (nunca texto libre); usuario ERP autenticado como registrador |
| Datos que se guardan | `EmployeeId` responsable y `UserId` registrador por separado en citas, valoraciones y sesiones; especialidades requeridas por servicio (referencias); horarios propios de Spa por profesional/sucursal |
| Acciones | Consultar profesionales elegibles por sucursal/servicio/fecha; configurar horarios y bloqueos (Pantalla 02); consultar citas por revisar |
| Flujo principal | Búsqueda/reserva → directorio devuelve elegibles → motor aplica horarios y restricciones → selección explícita |
| Flujos alternativos | Profesional deja de estar activo/asignado: citas futuras **no se alteran**, aparecen en «Citas por revisar» para reasignación manual; profesional inactivo histórico sigue visible en atenciones pasadas |
| Validaciones | Activo en la fecha, asignado a la sucursal, especialidad exigida por el perfil (si existe); profesional ≠ registrador conservados aunque coincidan en persona |
| Estados | Los de Empleados (Spa no los modifica) |
| Reglas | Cargo/profesión nulos no implican elegibilidad; `employees.view` no concede acceso clínico ni de agenda; Spa no crea Employee |
| Usuarios y permisos | `spa.agenda.view` (consulta), `spa.availability.manage`; datos laborales por permisos de Empleados |
| Integraciones | `IEmployeeDirectory` (COND-02: no implementado): `GetByIdAsync`, `ListAsync`, `IsActiveOnAsync`, `GetBranchAssignmentsAsync`, `IsEligibleForBranchAsync`, `GetLinkedUserAsync` |
| Errores y fallos | `SPA_PROFESSIONAL_UNAVAILABLE`; directorio no disponible → la operación se rechaza con error controlado (no se reserva sin validar) |
| Salidas, responsable de cada dato y dependencias | **Salidas:** profesionales elegibles y sus franjas libres. **Responsable de cada dato:** ficha laboral, asignaciones, especialidades y actividad = Empleados; horarios, bloqueos, ocupación y disponibilidad = Spa; usuario registrador = Identity. **Dependencias:** COND-02, F1.a, F5. **Pruebas:** `SPA-AC-OP-02.x`, PT-02. |
| Criterios de aceptación | `SPA-AC-OP-02.1` profesional no asignado no se ofrece; `02.2` responsable y registrador se guardan distintos; `02.3` cambio laboral posterior no modifica atenciones pasadas ni citas futuras; `02.4` no existe tabla Employee en Spa |

### 31.4 Servicios

| Elemento | Especificación |
|---|---|
| Objetivo | Configurar las condiciones operativas del servicio comercial (Item Service) sin otro catálogo |
| Datos de entrada | Item Service activo de la empresa; duración, preparación, sucursales, especialidades, procedimientos |
| Datos que se guardan | `SpaServiceProfile`, procedimientos y vínculos, historial; precio e impuesto no se copian |
| Acciones | Configurar, activar/desactivar, vincular procedimientos, consultar |
| Flujo principal | Elegir Item Service sin perfil → completar duración y condiciones → activar |
| Flujos alternativos | Item ya configurado → abrir su editor; Item no Service/inactivo → rechazo; desactivar con citas futuras → permitido, esas citas se listan para revisión y el servicio deja de ofrecerse |
| Validaciones | Naturaleza Service, activo, de la empresa; duración > 0; preparación ≥ 0 o vacía; especialidades existentes |
| Estados | Activo ↔ Inactivo; nunca eliminar |
| Reglas | Sucursales: vacío = sin restricción (SPA-SEL-02, el valor por defecto no estaba aprobado); duración efectiva = la del perfil (sin sumar procedimientos); sin valores predeterminados de duración o preparación |
| Usuarios y permisos | `spa.services.view/manage` |
| Integraciones | `GetItemByIdQuery`, `IPricingResolver` (lectura), Empleados (especialidades) |
| Errores y fallos | `SPA_ITEM_NOT_SERVICE`, `SPA_SERVICE_INACTIVE`, `SPA_SERVICE_ALREADY_CONFIGURED`, `SPA_SERVICE_NOT_FOUND` |
| Salidas, responsable de cada dato y dependencias | **Salidas:** perfil de servicio y lista de procedimientos. **Responsable de cada dato:** Item, precio e impuesto = Items/Pricing; duración, preparación, procedimientos y restricciones = Spa; especialidades = Empleados. **Dependencias:** F1.a, F3.a. **Pruebas:** `SPA-AC-OP-03.x`, PT-15. |
| Criterios de aceptación | `SPA-AC-OP-03.1` Product rechazado; `03.2` inactivo no se ofrece en búsqueda; `03.3` historial de citas conserva la duración vigente al reservar; `03.4` no se duplica precio ni impuesto |

### 31.5 Planes

| Elemento | Especificación |
|---|---|
| Objetivo | Describir lo previsto para la persona: tratamientos individuales y multisesión |
| Datos de entrada | Expediente, objetivo, decisión, prestaciones (servicio, procedimiento, sesiones previstas, intervalo sugerido, cuidados) |
| Datos que se guardan | `TreatmentPlan`, `PlanProvision`, referencias opcionales a contrato/prestación; sin importes ni estados financieros |
| Acciones | Crear, ampliar, suspender, reanudar (DP-01 aprobada), cancelar, cerrar, corregir con motivo, consultar vista comercial derivada |
| Flujo principal | Valoración → crear plan con prestaciones (nace Activo) → reservar sesiones → documentar → cerrar con resultado final |
| Flujos alternativos | Plan sin contrato (válido); varios contratos/facturas por plan; cerrar con sesiones pendientes → confirmación explícita; cancelar no cancela contratos ni citas |
| Validaciones | Sesiones previstas entero > 0; transiciones del ADR §5.23; motivo obligatorio al suspender/cancelar |
| Estados y transiciones | Activo, Suspendido, Cancelado, Cerrado (sin Borrador): ampliar, suspender, cancelar y cerrar (aprobados); reanudar = DP-01 y no reabrir Cancelado/Cerrado = DP-02, ambos **aprobados por el propietario el 2026-11-19** (ADR §5.23); historial append-only de eventos; la reanudación conserva la suspensión previa |
| Reglas | Plan ≠ paquete ≠ sesión; cerrar/cancelar/suspender no altera sesiones realizadas, facturas, contratos, saldo ni compromisos y no cancela citas (pasan a «Citas por revisar», DP-03); ampliar el plan no concede derechos comerciales; la decisión proceder/adaptar/posponer no ejecuta acciones; nunca vincular por coincidencia de BP |
| Usuarios y permisos | `spa.treatment-plans.view/manage` (clínicos); vista comercial exige lectura de Ventas/CxC |
| Integraciones | Items, `PackageContract`, lectores comerciales |
| Errores y fallos | `SPA_PLAN_INVALID_STATE`, `SPA_PLAN_CLOSE_PENDING_SESSIONS_CONFIRM` |
| Salidas, responsable de cada dato y dependencias | **Salidas:** plan, prestaciones, historial de eventos y vista comercial derivada. **Responsable de cada dato:** plan y prestaciones = Spa; contratos = Spa (Paquetes); facturas y pagos = Ventas/CxC (solo lectura). **Dependencias:** F2.a, F3.b; F6.e para la vista comercial. **Pruebas:** `SPA-AC-OP-05.x`, AC-TEC03-01..05. |
| Criterios de aceptación | `SPA-AC-OP-05.1` plan sin contrato guarda y consulta; `05.2` ampliar no cambia el libro; `05.3` cerrar con pendientes exige confirmación; `05.4` vista comercial muestra propuesto/contratado/facturado/pagado/reservado/realizado/derechos por prestación sin porcentaje |

### 31.6 Sesiones

| Elemento | Especificación |
|---|---|
| Objetivo | Documentar cada atención efectivamente realizada, de forma independiente de la cita y del consumo comercial |
| Datos de entrada | Expediente, prestación, cita (si existe), procedimiento, productos/equipos, incidencias, resultados, evolución, próxima sesión sugerida; motivo/alcance al interrumpir; motivo al corregir |
| Datos que se guardan | `ClinicalSession`, revisiones, materiales documentales; inicio/fin, responsable, registrador, sucursal |
| Acciones | Guardar borrador, iniciar, finalizar, interrumpir, corregir (revisión), cierre administrativo de la cita, consultar evolución |
| Flujo principal | Cita Programada → Iniciar atención (consentimiento válido si aplica) → documentar → Finalizar → (opcional) resolución de cupo en Paquetes |
| Flujos alternativos | Interrupción: motivo, momento y trabajo realizado, queda Interrumpida (parcial); cierre administrativo → cita Atendida sin falsificar la clínica; atención sin cita: no habilitada (O-10 abierta; recomendación conservadora: se exige cita hasta que el propietario decida) |
| Validaciones | Responsable (directorio) y registrador separados; consentimiento aplicable vigente; solo borrador editable; correcciones con motivo/autor |
| Estados y transiciones | Borrador → En atención → Finalizada/Interrumpida; corregir = revisión, no reapertura (ADR §5.23) |
| Reglas | Borrador, programación y reprogramación no consumen; finalizar/interrumpir no factura, cobra, contabiliza ni mueve inventario; evolución diferencia completa y parcial |
| Usuarios y permisos | `spa.sessions.create/view/manage` (clínicos); consumo `spa.packages.consume` |
| Integraciones | Agenda, consentimientos, paquetes (referencia), Items (materiales informativos), directorio |
| Errores y fallos | `SPA_CONSENT_REQUIRED`, `SPA_SESSION_INVALID_STATE`, `SPA_APPOINTMENT_REQUIRED`, `SPA_CONCURRENT_MODIFICATION`; si la escritura de auditoría falla, no se confirma la lectura ni se entrega contenido |
| Salidas, responsable de cada dato y dependencias | **Salidas:** sesión, revisiones y evolución. **Responsable de cada dato:** contenido clínico = Spa; profesional = Empleados; registrador = Identity; consumo comercial = Spa (Paquetes, operación separada). **Dependencias:** F3.b, F4.a, F5.c (para iniciar). **Pruebas:** `SPA-AC-OP-06.x`, AC-ARQ03-01..08. |
| Criterios de aceptación | `SPA-AC-OP-06.1` borrador sin atención ni consumo; `06.2` iniciar sin consentimiento requerido se rechaza; `06.3` interrumpir conserva parcial y no libera agenda; `06.4` cierre administrativo deja la sesión Interrumpida; `06.5` corregir crea revisión sin nueva atención |

### 31.7 Agenda

| Elemento | Especificación |
|---|---|
| Objetivo | Programar visitas con uno o varios servicios y profesionales mediante un único motor de disponibilidad, sin recursos físicos |
| Datos de entrada | Cliente, servicio(s), sucursal, fecha, profesional y hora elegidos explícitamente, paquete opcional; horarios, descansos, bloqueos y traslados (administración) |
| Datos que se guardan | `Visit`, eventos de llegada, `Appointment`, `ServiceProvision`, enlaces, `AppointmentOccupancy`, `AppointmentChange`, reglas de horario, excepciones, bloqueos, traslados, lista de espera |
| Acciones | Buscar disponibilidad; reservar; registrar llegada; reprogramar; cancelar; marcar ausencia; iniciar atención; cierre administrativo; anotar en lista de espera; consultar día/semana e historial |
| Flujo principal | Buscar por servicio/fecha/sucursal → resultados por profesional → selección explícita → reserva con revalidación transaccional (ADR §5.20) → visita y citas Programadas |
| Flujos alternativos | Sin resultados → lista de espera; disponibilidad cambió → 409 y nueva búsqueda sin reservar; reprogramación fallida → cita original intacta; cancelar un bloque no cancela los demás; profesional/cliente/sucursal inactivo → rechazo; hora inexistente por cambio horario → descartada de la rejilla |
| Validaciones | Servicio con perfil activo; profesional elegible y activo; dentro de horarios/descansos/bloqueos; sin solape (incluye preparación) en todas las sucursales de la empresa; traslado entre sucursales distintas configurado para las citas vecinas inmediatas del día (misma sucursal: sin traslado; par ausente: el hueco no se ofrece y la reserva se rechaza, sin minutos inventados); vigencia y cupo del paquete (sin consumir); contigüidad `[inicio, fin)` permitida |
| Estados y transiciones | Programada, En atención, Atendida, Cancelada, Ausente (matriz ADR §5.23); llegada y reprogramación son eventos; visita derivada |
| Reglas | Reserva independiente sin paquete siempre válida; reserva vinculada a paquete requiere comprobación real de vigencia, servicio y cupo y, sin la capacidad F6.c, se rechaza (`SPA_PACKAGE_CAPABILITY_UNAVAILABLE`) sin crear derechos; reservar/reprogramar no consumen; cancelar/ausentarse no crean sesión; ausencia sin penalización ni descuento automático; interrupción no libera tiempo (solo el cierre administrativo); varios bloques de un servicio = una prestación y un compromiso; sin cabinas/equipos, recordatorios ni reservas públicas |
| Usuarios y permisos | `spa.agenda.view/create/update/cancel/mark-absent`, `spa.availability.manage` |
| Integraciones | Directorio de Empleados, Branch, `ICompanyClock`, Items, paquetes, BP |
| Errores y fallos | `SPA_APPOINTMENT_OVERLAP`, `SPA_PROFESSIONAL_UNAVAILABLE`, `SPA_OUTSIDE_BUSINESS_HOURS`, `SPA_TRAVEL_TIME_NOT_CONFIGURED`, `SPA_INVALID_DATETIME`, `SPA_APPOINTMENT_INVALID_STATE`, `SPA_CONCURRENT_MODIFICATION`, `SPA_WAITLIST_DUPLICATE` |
| Salidas, responsable de cada dato y dependencias | **Salidas:** resultados de disponibilidad, visita/citas, historial de cambios. **Responsable de cada dato:** cliente = BP; profesional y asignación = Empleados; zona horaria = Company; servicio = Items; horarios, citas, ocupación y lista de espera = Spa. **Dependencias:** F5.a–d, F3.a; F6.c solo para reservas vinculadas a paquete. **Pruebas:** `SPA-AC-OP-07.x`, SPA-AC-ARQ04-01..12, PT-14. |
| Criterios de aceptación | `SPA-AC-OP-07.1` dos reservas simultáneas del mismo profesional: una gana, la otra 409; `07.2` reprogramación fallida conserva la original; `07.3` visita multiprofesional atómica; `07.4` traslado sin configurar rechaza el encadenamiento entre sucursales; `07.5` ausencia no crea sesión ni descuento; `07.6` cerrar administrativamente libera solo el tiempo futuro; `07.7` la búsqueda no crea citas |

### 31.8 Valoración facial

| Elemento | Especificación |
|---|---|
| Objetivo | Registrar la valoración estética facial inicial y sus revisiones, sin diagnóstico médico automático |
| Datos de entrada | Campos de §14 (todos opcionales), fecha de negocio, profesional responsable |
| Datos que se guardan | `Assessment` (Facial), revisiones, hallazgos tipados, estado Borrador/Registrada |
| Acciones | Guardar borrador, registrar, corregir con motivo, consultar revisiones |
| Flujo principal | Crear valoración → completar (puede quedar incompleta como borrador) → Registrar |
| Flujos alternativos | Continuar un borrador; corregir una registrada (nueva revisión); decisión «posponer» no genera acciones |
| Validaciones | Fototipo 1..6 o vacío; motivo obligatorio al corregir; `xmin` en borrador |
| Estados y transiciones | Borrador → Registrada; corrección = revisión (la anterior pasa a no vigente y restringida) |
| Reglas | Valoración estética, no diagnóstico; «diagnóstico médico previo» es información declarada; guardar no acredita atención ni consume; sin obligatoriedad universal (O-21) |
| Usuarios y permisos | `spa.assessments.view/create/update` |
| Integraciones | Expediente, directorio |
| Errores y fallos | `SPA_ASSESSMENT_NOT_FOUND`, `SPA_CORRECTION_REASON_REQUIRED`, `SPA_CONCURRENT_MODIFICATION` |
| Salidas, responsable de cada dato y dependencias | **Salidas:** valoración facial y revisiones. **Responsable de cada dato:** contenido y fecha = Spa; profesional = Empleados; registrador = Identity. **Dependencias:** F2.a, F2.b. **Pruebas:** `SPA-AC-OP-08.x`, AC-ARQ07-01/03. |
| Criterios de aceptación | `SPA-AC-OP-08.1` borrador incompleto se guarda; `08.2` corrección conserva la revisión previa; `08.3` ninguna etiqueta de diagnóstico se genera |

### 31.9 Valoración corporal

| Elemento | Especificación |
|---|---|
| Objetivo | Registrar valoración corporal y mediciones comparables Inicial/Control/Final |
| Datos de entrada | Datos generales (§15), peso (kg) y contornos (cm) opcionales, finalidad, fecha |
| Datos que se guardan | `Assessment` (Body), revisiones, `BodyMeasurementSet` (nulos distintos de cero) |
| Acciones | Guardar borrador, registrar conjunto, corregir, comparar |
| Flujo principal | Valoración inicial + conjunto Inicial → conjuntos Control → conjunto Final → comparación en lectura |
| Flujos alternativos | Medidas parciales (válido); varios controles sin límite impuesto; corrección = revisión |
| Validaciones | Valores ≥ 0 con `ZhDecimalInput`; escala según el gate V-04; abdomen alto/medio/bajo nunca fusionados en «cintura» |
| Estados y transiciones | Borrador → Registrada; corrección = revisión |
| Reglas | No medida ≠ cero; diferencias calculadas en lectura solo con valores comparables; sin valores/rangos inferidos; sin gráfica (no aprobada) |
| Usuarios y permisos | `spa.assessments.view/create/update` |
| Integraciones | Expediente, directorio |
| Errores y fallos | `SPA_MEASUREMENT_INVALID`, `SPA_CORRECTION_REASON_REQUIRED`; persistencia bloqueada por COND-05 |
| Salidas, responsable de cada dato y dependencias | **Salidas:** valoración corporal, conjuntos de medidas y comparación derivada. **Responsable de cada dato:** medidas y contenido = Spa; profesional = Empleados. **Dependencias:** F2.c, COND-05. **Pruebas:** `SPA-AC-OP-09.x`, AC-ARQ07-02/03. |
| Criterios de aceptación | `SPA-AC-OP-09.1` vacío no se guarda como 0; `09.2` diferencia ausente si falta un valor; `09.3` corregir no sobrescribe; `09.4` persistencia no se activa sin el gate V-04 |

### 31.10 Consentimientos

| Elemento | Especificación |
|---|---|
| Objetivo | Conservar evidencia de que la persona fue informada y decidió, por finalidad |
| Datos de entrada | Versión vigente del texto, decisión, firma en pantalla, motivo de retiro |
| Datos que se guardan | Definiciones y versiones inmutables (texto + hash), `ConsentRecord` (versión presentada, decisión, hash y archivo de firma, instante, usuario que capturó), retiros |
| Acciones | Administrar textos, publicar versión, presentar y registrar, retirar, consultar |
| Flujo principal | Procedimiento que lo requiere → presentar versión vigente → persona decide y firma → registro Vigente → habilita la atención |
| Flujos alternativos | Rechaza (queda registro, no se habilita); retiro → deja de habilitar usos futuros sin revertir lo realizado; nueva versión → registros previos siguen ligados a la versión que firmaron |
| Validaciones | Una versión vigente por definición; firma obligatoria si Otorga; finalidades Procedimiento/Fotos de seguimiento/Promoción independientes; sin finalidad académica |
| Estados | Definición Vigente/Reemplazada; registro Vigente/Retirado |
| Reglas | No equivale automáticamente a firma electrónica certificada; no se atiende a menores ni se captura representante hasta COND-04; `valid_until` vacío = sin vencimiento declarado (vigencia no aprobada) |
| Usuarios y permisos | `spa.consents.view/manage/templates` |
| Integraciones | Expediente, `ICareFileStore`, sesiones, fotografías, auditoría |
| Errores y fallos | `SPA_SIGNATURE_REQUIRED`, `SPA_CONSENT_VERSION_NOT_FOUND`, `SPA_CONSENT_ALREADY_WITHDRAWN`, `SPA_CONSENT_REQUIRED` |
| Salidas, responsable de cada dato y dependencias | **Salidas:** definiciones, versiones, registros y retiros. **Responsable de cada dato:** textos, versiones y decisión = Spa; archivo de firma = `ICareFileStore` (Spa); identidad de la persona = BP. **Dependencias:** F1.d, F2.a, F4.a, COND-04. **Pruebas:** `SPA-AC-OP-10.x`, AC-ARQ07-04..06. |
| Criterios de aceptación | `SPA-AC-OP-10.1` sin consentimiento requerido no se inicia la atención; `10.2` otorgar una finalidad no otorga otra; `10.3` el texto firmado no cambia tras publicar otra versión; `10.4` retiro bloquea usos futuros |

### 31.11 Fotografías

| Elemento | Especificación |
|---|---|
| Objetivo | Conservar fotografías de seguimiento privadas, con finalidad y autorización, y compararlas |
| Datos de entrada | Archivo, finalidad, zona/ángulo, nota de condiciones, vínculo a valoración/sesión |
| Datos que se guardan | Metadatos (`CarePhoto`), archivo cifrado con id opaco (`spa_care_files`), consentimiento aplicable, grupos de comparación |
| Acciones | Subir, ver, comparar, exportar (explícito), desactivar |
| Flujo principal | Verificar consentimiento de seguimiento → subir → cifrar y guardar → consultar con auditoría durable previa |
| Flujos alternativos | Sin consentimiento → rechazo; fallo de auditoría → no se entrega el archivo; fallo de tag/clave → no se descifra; foto desactivada → no visible en uso ordinario |
| Validaciones | Tamaño/formato dentro de la configuración; ruta generada por servidor; descarga nunca por ruta arbitraria; finalidad respetada |
| Estados | Activa ↔ Inactiva (sin borrado físico) |
| Reglas | Sin URL pública, publicación ni exportación automática; no aparecen en listados ni agenda; retiro del consentimiento: política de conservar/bloquear/anonimizar/eliminar pendiente (COND-06) |
| Usuarios y permisos | `spa.photos.view/create/manage`, `spa.records.export` |
| Integraciones | `ICareFileStore` sobre `IFileStorage`, consentimientos, auditoría |
| Errores y fallos | `SPA_PHOTO_CONSENT_MISSING`, `SPA_FILE_TOO_LARGE`, `SPA_FILE_TYPE_NOT_ALLOWED`, `SPA_ACCESS_DENIED` |
| Salidas, responsable de cada dato y dependencias | **Salidas:** metadatos y contenido autorizado por descarga. **Responsable de cada dato:** metadatos y finalidad = Spa; bytes cifrados = `ICareFileStore` sobre `IFileStorage`; autorización = Identity/Access. **Dependencias:** F1.d, F4.a, F4.b, COND-09. **Pruebas:** `SPA-AC-OP-11.x`, PT-04/10/11. |
| Criterios de aceptación | `SPA-AC-OP-11.1` sin consentimiento no se sube; `11.2` el registro de acceso se confirma antes de entregar bytes; `11.3` ninguna respuesta incluye ruta/URL permanente; `11.4` otra empresa no accede; `11.5` el contenido en disco está cifrado |

### 31.12 Paquetes

| Elemento | Especificación |
|---|---|
| Objetivo | Controlar derechos de sesiones por servicio derivados de documentos comerciales oficiales |
| Datos de entrada | Definición (servicios/cantidades/vigencia), factura y línea(s), cita vinculada, motivos y respaldos de movimientos |
| Datos que se guardan | `PackageDefinition`, `PackageContract` (factura/línea únicos), lotes, movimientos append-only, compromisos, asignaciones de beneficiario |
| Acciones | Definir, crear contrato, programar con cupo, resolver compromiso (consumir/liberar), compensar, ampliar, sustituir, transferir (excepcional), ajustar, cancelar, consultar saldo/libro |
| Flujo principal | Factura autorizada → crear contrato (explícito) → reservar citas vinculadas (compromiso de cupo) → atención finalizada → resolución autorizada → movimiento `Consumed` |
| Flujos alternativos | Cita cancelada antes de consumir → libera el cupo; reprogramación conserva compromiso; parcial/interrumpida → cupo retenido hasta resolución; ausencia → sin descuento, compromiso retenido hasta resolución manual; vencimiento → sin nuevas reservas/consumos, citas futuras a revisión manual; compensación posterior mediante movimiento nuevo |
| Validaciones | Cliente del contrato = cliente de la factura; línea no vinculada; saldo = derechos vigentes − consumos netos − compromisos; idempotencia por `clientRequestId`/clave de origen; sobrecompensación imposible; sustitución solo con equivalencia y respaldo documentados; ampliación solo con documento/línea oficial; sin consumo entre empresas |
| Estados y transiciones | Contrato Activo → Agotado/Vencido (derivados) o Cancelado (autorizado); compromiso Retenido → Consumido/Liberado |
| Reglas | Reservar/reprogramar no consumen; finalizar no consume por sí solo; cuotas pendientes no suspenden derechos; ampliar no prolonga vigencia original; ampliaciones gratuitas inexistentes; transferencia no mueve el expediente; sin dinero en el libro |
| Usuarios y permisos | `spa.packages.view/manage/consume/adjust` |
| Integraciones | Ventas (factura/línea/ajustes), Items, agenda, sesiones; consultas CxC |
| Errores y fallos | `SPA_INVOICE_NOT_AUTHORIZED`, `SPA_INVOICE_CUSTOMER_MISMATCH`, `SPA_INVOICE_LINE_ALREADY_LINKED`, `SPA_PACKAGE_NO_BALANCE`, `SPA_PACKAGE_ALREADY_CONSUMED`, `SPA_PACKAGE_EXPIRED`, `SPA_PACKAGE_INACTIVE`, `SPA_SUBSTITUTION_NOT_EQUIVALENT`, `SPA_COMPENSATION_EXCEEDS_CONSUMED`; un error de Spa jamás revierte la factura |
| Salidas, responsable de cada dato y dependencias | **Salidas:** contrato, lotes, libro, saldo y compromisos. **Responsable de cada dato:** servicios = Items; factura y línea = Ventas; derechos, libro y compromisos = Spa; cobro = CxC. **Dependencias:** F5.b, F6.a–d, COND-03/07/08. **Pruebas:** `SPA-AC-OP-12.x`, AC-ARQ05-01..10, PT-16/17. |
| Criterios de aceptación | `SPA-AC-OP-12.1` segundo contrato para la misma línea → 409; `12.2` dos programaciones concurrentes no exceden el cupo; `12.3` reintento de consumo no duplica; `12.4` cancelar antes de consumir libera cupo y no descuenta; `12.5` vencido bloquea reservas; `12.6` sustitución sin equivalencia se rechaza; `12.7` el libro no admite UPDATE/DELETE |

### 31.13 Integración comercial

| Elemento | Especificación |
|---|---|
| Objetivo | Relacionar plan → prestación → contrato → factura/línea → pagos/cuotas sin duplicar Ventas ni CxC |
| Datos de entrada | Ids de factura y línea; consulta por contrato/prestación |
| Datos que se guardan | Solo referencias (ids) y el estado de las entidades de Spa |
| Acciones | Crear contrato desde factura; consultar por prestación: propuesto, contratado, facturado, pagado, reservado, realizado, derechos restantes; abrir la factura en Ventas |
| Flujo principal | Ventas autoriza la factura (sin intervención de Spa) → usuario con permiso crea el contrato en Spa → lecturas oficiales mantienen el estado comercial |
| Flujos alternativos | Spa no disponible/falla → la factura queda intacta; NC/anulación en Ventas → Spa consulta y un usuario autorizado ajusta derechos con referencia, sin revertir clínica; crédito pendiente → consulta de cuotas, sin suspensión automática |
| Validaciones | Autorizada (según COND-08), mismo cliente, línea libre; permisos de lectura de Ventas/CxC del consumidor; scope de sucursal original |
| Estados | Los de Ventas/CxC (solo lectura) |
| Reglas | No evento creador; no facturas, cobros, CxC ni asientos en Spa; no porcentaje facturado sin comparables; Anticipos (O-14) independiente |
| Usuarios y permisos | `spa.packages.manage` + permiso de Ventas (`SalesPermissions.View`) y de Finance (lectura, sin `reverse`) |
| Integraciones | `GetSalesInvoiceByIdQuery`/`ISalesEntitlementReader`, `GetSalesReturnByIdQuery`, `GetReceivableByInvoiceQuery`, `IReceivableCollectionReader` (candidatos, COND-03) |
| Errores y fallos | `SPA_INVOICE_*`, `SPA_COMMERCIAL_READ_UNAVAILABLE` (lectura oficial no disponible: error controlado, sin inferir) |
| Salidas, responsable de cada dato y dependencias | **Salidas:** vista comercial por prestación y contrato creado. **Responsable de cada dato:** facturas, NC y pagos = Ventas/CxC; referencias y derechos = Spa. **Dependencias:** F6.a, F6.b, F6.e, COND-03/08. **Pruebas:** `SPA-AC-OP-13.x`, AC-ARQ08-01..08. |
| Criterios de aceptación | `SPA-AC-OP-13.1` crear contrato no altera la factura; `13.2` fallo de Spa no revierte la autorización; `13.3` mostrar pagos no exige `finance.collections.reverse`; `13.4` ninguna tabla monetaria en Spa |

### 31.14 Operación

| Elemento | Especificación |
|---|---|
| Objetivo | Operar la jornada: panel, agenda diaria/semanal, lista de espera, seguimiento y reportes básicos |
| Datos de entrada | Sucursal, fecha, filtros |
| Datos que se guardan | Solo lista de espera (propia); panel y reportes son consultas sobre datos de origen |
| Acciones | Consultar panel y reportes R1..R7 (Pantalla 19), gestionar espera, revisar citas por revisar |
| Flujo principal | Abrir panel → acceder al proceso → resolver desde la pantalla de origen |
| Flujos alternativos | Sin permiso → tarjeta/consulta omitida; error parcial → el resto se muestra; sin datos → estado vacío |
| Validaciones | Scope tenant/empresa/sucursal; permiso del dato de origen; panel y reportes operativos sin contenido clínico; el seguimiento clínico (Pantalla 10, `GET .../evolution`) solo por consultas protegidas con permisos clínicos, guard de recurso y auditoría |
| Estados | Los de cada proceso de origen; espera Activa/Cerrada |
| Reglas | Sin dashboard analítico, KPI, gráficos, exportación, recordatorios ni reservas públicas (HX-02/04/10) |
| Usuarios y permisos | `spa.agenda.view`, `spa.reports.view`, `spa.agenda.create/cancel` (lista de espera) |
| Integraciones | Agenda, sesiones, paquetes, lecturas comerciales |
| Errores y fallos | `SPA_ACCESS_DENIED`, `SPA_WAITLIST_DUPLICATE` |
| Salidas, responsable de cada dato y dependencias | **Salidas:** panel, reportes R1–R7, lista de espera. **Responsable de cada dato:** cada dato pertenece a su proceso de origen; lista de espera = Spa. **Dependencias:** F5.d, F7 y los procesos consultados. **Pruebas:** `SPA-AC-OP-14.x`, H-11..15. |
| Criterios de aceptación | `SPA-AC-OP-14.1` conteos del panel coinciden con la agenda; `14.2` reportes no cruzan empresas; `14.3` sin exportación; `14.4` ningún dato clínico en panel ni reportes operativos; `14.5` la evolución clínica exige permiso clínico y guard: reportes/agenda sin ese permiso → 403, Admin sin concesión → 403, auditoría previa a la entrega |

### 31.15 Condiciones externas y protegidas no cumplidas

Ninguna se da por cumplida. «Prohibido mientras esté pendiente» es la restricción que aplica a cualquier agente. Cada condición bloquea **solo** la superficie indicada; el resto del diseño avanza.

| ID | Qué debe verificarse | Quién autoriza o valida | Funcionalidad afectada | Fase | Prohibido mientras esté pendiente | Evidencia que permite considerarla cumplida |
|---|---|---|---|---|---|---|
| COND-01 | Revisión arquitectónica especial del contrato LOCKED Identity/Access/Kernel y ejecución de la propuesta ADR §5.13 (clasificación sin bypass, guard, concesiones, anti-autoescalamiento) con pruebas negativas | Propietario + arquitecto (autorización especial expresa) | Toda la clínica, auditoría de accesos, Admin excepcional | F1 | Modificar Kernel/Access/Session/Navigation o sus consumidores; asignar o mostrar claves clínicas; habilitar rutas clínicas con datos reales | Acta de autorización especial del propietario, cambios revisados y pruebas negativas de ADR §5.13 (API directa, MediatR, descarga, reportes, menú, autoescalamiento) en verde sobre PostgreSQL real |
| COND-02 | `IEmployeeDirectory` implementado y probado (EMP-E1..E6b) con firmas finales | Propietario (plan/autorización de Empleados, ADR-050) | Profesionales, disponibilidad, agenda, atenciones | F1 (consumo), F5 | Crear entidad Employee o directorio paralelo; reservar sin validar contra el directorio | EMP-E6b cerrado con gate PASS del propietario y firmas de `IEmployeeDirectory` publicadas con sus pruebas |
| COND-03 | Si `GetSalesInvoiceByIdQuery` basta o se requiere `ISalesEntitlementReader`; `IReceivableCollectionReader`; permisos de lectura de Finance | Propietario + autorización protegida de Ventas/Finance (FROZEN) | Crear contrato desde factura; consulta de abonos | F0 (verificar), F6 | Editar Ventas/CxC; conceder `finance.collections.reverse`; leer repositorios internos | Decisión escrita del propietario de Ventas/Finance (el DTO existente basta o se autoriza el lector) y lector implementado y probado por su propietario |
| COND-04 | Validez jurídica de la firma en pantalla; menores y representante (O-18); finalidad académica no aprobada (O-17) | Asesor legal designado por el propietario (V-06/V-09) | Consentimientos, atención a menores | F4 | Declarar validez legal; atender menores con consentimiento propio; capturar representante o uso académico | Dictamen escrito del asesor legal designado sobre firma en pantalla, menores/representante y finalidades |
| COND-05 | Gate formal de precisión para kg/cm (V-04; `numeric(10,2)` es candidato) | Propietario + arquitecto (revisión del estándar de datos) | Persistencia de medidas corporales | F2 | Persistir medidas como definitivas; modificar `data-standards.md` | Aprobación arquitectónica formal registrada en el estándar de datos (escala y rango de kg y cm) |
| COND-06 | Procedimiento y plazos de conservación, corrección, supresión y anonimización (C-02/O-05) | Asesor legal + propietario | Retiro de consentimiento/fotos, supresión, plazos | F1 (diseño), F4 | Borrado físico, `DeleteAsync` sobre archivos clínicos, plazos inventados | Procedimiento y plazos validados por el asesor legal y aprobados por el propietario |
| COND-07 | Equivalencias, documentos adicionales y requisitos jurídicos de transferencia excepcional | Propietario comercial + asesor legal | Sustitución y transferencia de derechos | F6 | Ejecutar sustituciones/transferencias sin equivalencia y respaldo documentados | Documento aprobado de equivalencias y de transferibilidad con dictamen legal |
| COND-08 | Significado de «factura autorizada», estado SRI exigido, crédito pendiente y efectos de NC/anulación | Propietario + responsable fiscal/contable | Elegibilidad de creación de contrato; ajustes por NC | F6 | Inferir reglas tributarias; activar la creación en producción sin política definida | Política escrita aprobada por el propietario y el responsable fiscal/contable |
| COND-09 | Custodia de la KEK, recuperación, restauración y TLS productivos con evidencia operativa | Propietario / operación | Archivos clínicos, firmas, fotografías | F1, F4 | Datos reales en archivos; afirmar cifrado productivo; modificar scripts de respaldo | Runbook operativo con prueba de restauración de datos y claves y TLS verificado, firmado por el propietario |
| COND-10 | Salida de seguridad operativa: protección y auditoría validadas antes de datos reales | Propietario (gate) | Cualquier dato clínico real | F1, F8 | Cargar datos clínicos reales | Informe de verificación de COND-01, COND-06 y COND-09 más gate PASS del propietario |
| COND-11 | Anticipos de Clientes (O-14) como proceso transversal | Propietario | Operaciones que dependan de anticipos | Fuera de V1.1 | Diseñar o implementar anticipos dentro de Spa | Diseño y autorización separados del proceso transversal (fuera del alcance de V1.1) |
| COND-12 | Fecha de nacimiento y contacto de emergencia (O-06) | Propietario (ADR de BP si se amplía BP; FROZEN) | Datos personales adicionales | F2 | Ampliar BusinessPartner; capturarlos sin decisión | ADR de BusinessPartner aprobado y autorización del módulo FROZEN, o decisión expresa de no capturar esos datos |
| COND-13 | Aprobación arquitectónica de ADR-051 y autorización expresa de cada fase con su lista exacta de archivos | Propietario | Todo el código | F0..F8 | Implementar cualquier fase | Decisión escrita del propietario sobre ADR-051 y autorización expresa de cada fase con su lista exacta de archivos |

### 31.16 Catálogo consolidado de errores

Todos los códigos se registran en `ApiResponseCodes`/`MessageCatalog` (categoría fija; el HTTP lo deriva la categoría). Mensajes `message.user` en español, sin datos clínicos ni de personas.

| Categoría (HTTP) | Códigos |
|---|---|
| NotFound (404) | `SPA_APPOINTMENT_NOT_FOUND`, `SPA_SERVICE_NOT_FOUND`, `SPA_PACKAGE_NOT_FOUND`, `SPA_ASSESSMENT_NOT_FOUND`, `SPA_CONSENT_VERSION_NOT_FOUND`, `SPA_CARE_RECORD_NOT_FOUND`, `SPA_SESSION_NOT_FOUND` |
| Validation (422) | `SPA_INVALID_DATETIME`, `SPA_MEASUREMENT_INVALID`, `SPA_CORRECTION_REASON_REQUIRED`, `SPA_SIGNATURE_REQUIRED`, `SPA_FILE_TOO_LARGE`, `SPA_FILE_TYPE_NOT_ALLOWED` |
| BusinessRule (400) | `SPA_PROFESSIONAL_UNAVAILABLE`, `SPA_OUTSIDE_BUSINESS_HOURS`, `SPA_TRAVEL_TIME_NOT_CONFIGURED`, `SPA_APPOINTMENT_INVALID_STATE`, `SPA_SESSION_INVALID_STATE`, `SPA_PLAN_INVALID_STATE`, `SPA_PLAN_CLOSE_PENDING_SESSIONS_CONFIRM`, `SPA_SERVICE_INACTIVE`, `SPA_ITEM_NOT_SERVICE`, `SPA_CLIENT_INACTIVE`, `SPA_APPOINTMENT_REQUIRED`, `SPA_CONSENT_REQUIRED`, `SPA_PHOTO_CONSENT_MISSING`, `SPA_CONSENT_ALREADY_WITHDRAWN`, `SPA_PACKAGE_NO_BALANCE`, `SPA_PACKAGE_INACTIVE`, `SPA_PACKAGE_EXPIRED`, `SPA_PACKAGE_CAPABILITY_UNAVAILABLE` (reserva vinculada a paquete antes de F6.c), `SPA_INVOICE_NOT_AUTHORIZED`, `SPA_INVOICE_CUSTOMER_MISMATCH`, `SPA_SUBSTITUTION_NOT_EQUIVALENT`, `SPA_COMPENSATION_EXCEEDS_CONSUMED`, `SPA_COMMERCIAL_READ_UNAVAILABLE` |
| Duplicate (409) | `SPA_APPOINTMENT_OVERLAP`, `SPA_CONCURRENT_MODIFICATION`, `SPA_SCHEDULE_OVERLAP`, `SPA_SERVICE_ALREADY_CONFIGURED`, `SPA_CARE_RECORD_EXISTS`, `SPA_WAITLIST_DUPLICATE`, `SPA_PACKAGE_ALREADY_CONSUMED`, `SPA_INVOICE_LINE_ALREADY_LINKED` |
| Authorization (403) | `SPA_ACCESS_DENIED` (genérico: nunca revela si el recurso existe ni de quién es) |

### 31.17 Trazabilidad área → pantallas → fase → pruebas

| Área | Pantallas | Fase | Pruebas |
|---|---|---|---|
| 31.2 Expedientes | 05, 14 | F1 base, F2 | `SPA-AC-OP-01.x`, `SPA-AC-UI-05/14` |
| 31.3 Profesionales | 01, 02 | F1 (consumo), F5 | `SPA-AC-OP-02.x` |
| 31.4 Servicios | 03, 13 | F3 | `SPA-AC-OP-03.x`, `SPA-AC-UI-03/13` |
| 31.5 Planes | 08 | F3 | `SPA-AC-OP-05.x`, `SPA-AC-UI-08` |
| 31.6 Sesiones | 09, 10, 13 | F3 | `SPA-AC-OP-06.x`, `SPA-AC-UI-09/10` |
| 31.7 Agenda | 01, 02, 16, 17, 18 | F5 | `SPA-AC-OP-07.x`, `SPA-AC-UI-01/02/16/17/18` |
| 31.8 Facial | 06 | F2 | `SPA-AC-OP-08.x`, `SPA-AC-UI-06` |
| 31.9 Corporal | 07 | F2 | `SPA-AC-OP-09.x`, `SPA-AC-UI-07` |
| 31.10 Consentimientos | 11 | F4a | `SPA-AC-OP-10.x`, `SPA-AC-UI-11` |
| 31.11 Fotografías | 12 | F4b | `SPA-AC-OP-11.x`, `SPA-AC-UI-12` |
| 31.12 Paquetes | 04 | F6 | `SPA-AC-OP-12.x`, `SPA-AC-UI-04` |
| 31.13 Integración comercial | 04, 08 | F6 | `SPA-AC-OP-13.x` |
| 31.14 Operación | 15, 18, 19 | F7 | `SPA-AC-OP-14.x`, `SPA-AC-UI-15/18/19` |

## Anexo A. Responsabilidad de cada documento

| Documento | Responsabilidad | Estado |
|---|---|---|
| **Contrato central** (este documento) | Comportamiento funcional y técnico de Spa V1.1: qué se desarrollará, cómo funciona, qué está aprobado, qué no y qué queda excluido | Candidato a SSOT funcional; EN REVISIÓN |
| **[ADR-051](ADR-051-spa-v1.1-architecture.md)** | Arquitectura candidata, fronteras y condiciones | ACCEPTED / APROBADO |
| **[Plan](SPA-V1.1-IMPLEMENTATION-PLAN-01.md)** | Fases, dependencias, pruebas y cierre | PROPUESTO; ninguna fase autorizada |
| **Registro de decisiones** ([REG](SPA-V1.1-APPROVED-DECISIONS-01.md)) | Evidencia de las aprobaciones del propietario | Vigente; las aprobaciones nuevas (incluida `DIR-SSOT-01`) se registran allí o en su sucesor |
| R1 RECOVERY-01 | Respaldo de reconstrucción/códigos GS-*/V01 anterior a recuperar SPA-DEC-115/116 en REG | Referencia **no normativa** |
| R2 OPERATIONS-SPEC, R3 CARE-PRIVACY, R4 SECURITY-COMPLIANCE | Análisis, alternativas y evidencia técnica y legal que respaldan este contrato | Referencia **no normativa** (el texto funcional vive aquí; R4 §7 conserva el detalle legal) |
| MATRIZ | Trazabilidad de los 295 registros y conflictos `C-xx` | Referencia **no normativa** |
| NORM | Redacciones canónicas, glosario, catálogo de permisos, verificaciones `V-xx` cerradas | Referencia **no normativa**; sus contenidos funcionales y verificaciones están integrados aquí según su estado |

**Efecto práctico:** el contrato concentra el comportamiento planificado; ADR-051 explica la arquitectura candidata y el plan ordena las fases. Los seis antecedentes se preservan sin borrar ni renombrar, con aviso no normativo. El índice y los estados están sincronizados; REG conserva aprobaciones e instrucción transversal con su alcance exacto.

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
| B-09 | R3 `SPA-CARE-CNS-05` (sesión interrumpida «no realizada por completo») frente a `O-25` #3 | Interrupción/borrador/finalización/corrección incluidos por SPA-DEC-116; ciclo/atención parcial resueltos por SPA-ARQ-03 §12; persistencia, implementación del cierre administrativo (§8.7) y efectos comerciales pendientes, O-25 intacta | — |
| B-10 | R3 `SPA-CARE-EXP-08` («nunca se elimina») frente a R4 `SPA-SEC-RET-01` (procedimiento de eliminación) | No se resuelve en silencio: BL-03 | 1 vs legal |
| B-11 | Los borradores dicen «ninguna regla está aprobada» aunque `O-25` y `O-15` lo estén después | Las reglas afectadas se marcan con su aprobación (§8, §11, §12, §20); los cuerpos históricos se preservan y sus entradas remiten al contrato | 1 |
| B-12 | R1 §16 afirma que las exclusiones de V1.1 no están recuperadas | SPA-DEC-116 recuperó diez exclusiones expresas HX-01..10; las alternativas previas incompatibles dejan de ser candidatas | — |
| B-13 | La primera versión clasificaba como «excluidas» la creación automática del contrato y la sesión única desde la reserva | Reclasificadas como no autorizadas (N-10, N-11): `O-15` y `O-25` dicen «no se aprueba», no «excluido» | 1 |
| B-14 | La primera versión hablaba de «once» preguntas de paquetes pendientes | En la revisión previa eran diez tras O-15/O-25; ahora SPA-DEC-116 resuelve componentes adicionales de composición/vigencia/cupos; solo detalles de §11/30 pendientes | 1 |
| B-15 | Estados [F]/[R] se confundían con propuestas técnicas, y faltaban categorías de antecedentes en el contrato | Planificación histórica distinguida de [T], datos restaurados §13.1 y fichas §23.1 sin diseños inventados | Consolidación, no aprobación |
| B-16 | Contrato/estados decían ADR/plan inexistentes e índice pendiente | ADR-051 ACCEPTED (2026-11-19), plan sin autorización e índice/estado sincronizados; antecedentes con entrada no normativa | Documental |
| B-17 | BL-04 atribuía a O-14 el origen del contrato ya decidido por O-15 | Bloqueo limitado a funciones dependientes de anticipos; O-15 no se reabre | O-15 + DIR |
| B-18 | En §20.2 se rotulaba toda la fila como aprobada, incluyendo venta del paquete como Item Service | Aprobación limitada a origen y referencia factura/línea; modelado de venta sigue propuesto | Alcance exacto O-15 |

Nuevas diferencias resueltas por evidencia del propietario: la inclusión general antes pendiente queda aprobada (O-23 cerrada en ese componente); O-08 resuelve recursos físicos excluidos, O-20 expediente único, O-11 multiplicidad funcional, O-13 capacidades de paquetes y O-16 firma/versiones. Solo el detalle concreto permanece pendiente. SPA-DEC-115 se incorpora al motor único, sin crear agenda nueva; SPA-DEC-116 restaura panel/espera/reportes y exclusiones sin reabrir decisiones posteriores.

## Anexo C. Mapa de origen por identificador

Sección principal del contrato donde se consolida cada regla, invariante, riesgo y decisión de los documentos previos. Las pruebas figuran en §28.1 y los casos de aceptación nuevos en §28.3.

| Sección | Identificadores consolidados |
|---|---|
| §1, §2 | `SPA-AL-01` |
| §1, §6 | `SPA-AL-02` |
| §8.5, §12, §24, §30 | `SPA-ARQ-01` (REG §12; modelo conceptual aprobado) |
| §2, §3, §7.1, §23, §28.4, §30 | `SPA-DEC-115`, `SPA-DEC-116` (evidencia histórica recuperada REG §9/10) |
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

Esta revisión sincroniza Spa y las entradas de estado autorizadas. No modifica ADR-050 ni la especificación de Empleados, y no autoriza implementación de Spa ni de Empleados, migraciones, cambios en
módulos FROZEN o CLOSED, ni cambios en Identity, Access, Kernel, Items, Ventas, Cuentas por Cobrar, Contabilidad, almacenamiento o scripts de operación. SPA-DEC-115/116 están recuperadas con evidencia del propietario en REG §9/10. Las
fichas originales de Gali Spa siguen sin inspección directa por Claude. No hay asesoría jurídica ni certificación de cumplimiento.

## Anexo E. Procedencia V01 y GS-* conservada

Equivalencias transmitidas y fusionadas previamente en R1 §17. Las referencias § y «Ver §» dentro de esta tabla corresponden a R1, no a la numeración del contrato; la ubicación consolidada actual de cada identificador está en Anexo C. No son archivos originales inspeccionados, aprobaciones nuevas ni decisiones SPA-DEC. Se incluyen aquí para no exigir consultar borradores al seguir la procedencia de las funciones.

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

## Anexo F. Escenarios históricos de prueba consolidados

Complementa §28: aquí están los enunciados para no consultar borradores al entender el comportamiento previsto. **Son propuestas de verificación, no pruebas ejecutadas ni aprobaciones.** Se conserva el identificador y se distingue la expansión de sufijos de R3. Una condición dependiente de O-xx queda condicionada a su resolución; ninguna prueba convierte una propuesta en requisito aprobado.

Lectura actual: SPA-ARQ-05 §11 actualiza los efectos comerciales de los escenarios históricos sin alterar su literalidad; ausencia sin descuento/penalización automática, cancelación sin reversión automática y cupos programados sin consumo. Donde una fuente decía sesión programada se aplica O-25 (cita); ninguna ausencia crea sesión clínica. O-15 fija creación explícita y transacción separada. Cualquier criterio histórico de no eliminación está condicionado a O-05/C-02; no lo resuelve este catálogo. Los 13 casos SPA-AC de §28.3 son adicionales.

| Identificador | Escenario preservado | Procedencia | Literalidad |
|---|---|---|---|
| SPA-T-01 | La atención referencia al cliente correcto | R1 | literal |
| SPA-T-02 | No se crea un segundo empleado desde Spa | R1 | literal |
| SPA-T-03 | Se diferencia profesional de usuario registrador | R1 | literal |
| SPA-T-04 | Una asignación laboral no concede permisos | R1 | literal |
| SPA-T-05 | Se aplican las restricciones autorizadas de acceso a expedientes | R1 | literal |
| SPA-T-06 | Se conserva la valoración facial documentada | R1 | literal |
| SPA-T-07 | Se conserva la valoración corporal documentada | R1 | literal |
| SPA-T-08 | Las medidas respetan unidades y precisión oficial | R1 | literal |
| SPA-T-09 | Una sesión conserva sus observaciones y evolución | R1 | literal |
| SPA-T-10 | Las autorizaciones fotográficas son independientes | R1 | literal |
| SPA-T-11 | El permiso de seguimiento no habilita publicación promocional | R1 | literal |
| SPA-T-12 | Las consultas de agenda respetan los permisos correspondientes | R1 | literal |
| SPA-T-13 | Las reservas concurrentes cumplen el contrato de disponibilidad | R1 | literal |
| SPA-T-14 | Los saldos de sesiones cumplen las reglas comerciales aprobadas | R1 | literal |
| SPA-T-15 | Las operaciones comerciales reutilizan los módulos oficiales | R1 | literal |
| SPA-T-16 | Se mantiene aislamiento de datos por tenant y empresa | R1 | literal |
| SPA-T-17 | Los eventos relevantes quedan auditados | R1 | literal |
| SPA-T-18 | No se altera el historial por cambios posteriores en Empleados | R1 | literal |
| SPA-OPS-T-AGE-01 | Dos reservas simultáneas del mismo profesional en el mismo intervalo: una gana y la otra recibe `SPA_APPOINTMENT_OVERLAP` (PostgreSQL real) | R2 | literal |
| SPA-OPS-T-AGE-02 | Una cita fuera del horario de establecimiento se rechaza | R2 | literal |
| SPA-OPS-T-AGE-03 | Un profesional dado de baja en Empleados no figura como disponible y sus citas futuras no cambian solas | R2 | literal |
| SPA-OPS-T-AGE-04 | La cita en una hora inexistente por cambio de horario se rechaza | R2 | literal |
| SPA-OPS-T-AGE-05 | Reprogramar conserva la identidad y deja un cambio con valores anterior y nuevo | R2 | literal |
| SPA-OPS-T-AGE-06 | Cancelar y marcar ausencia no eliminan la cita y registran autor y motivo | R2 | literal |
| SPA-OPS-T-AGE-07 | Un usuario sin el permiso de agenda no accede; `employees.view` no alcanza | R2 | literal |
| SPA-OPS-T-AGE-08 | La asignación de sucursal de Empleados no concede acceso ni permisos de agenda | R2 | literal |
| SPA-OPS-T-AGE-09 | La cita de otra empresa o tenant no es visible | R2 | literal |
| SPA-OPS-T-SRV-01 | Solo un Item `Service` activo de la empresa puede configurarse como servicio de Spa | R2 | literal |
| SPA-OPS-T-SRV-02 | Un servicio inactivo no admite citas nuevas y su historial se conserva | R2 | literal |
| SPA-OPS-T-SRV-03 | La cita conserva el nombre y la duración del momento de la reserva aunque el servicio cambie | R2 | literal |
| SPA-OPS-T-SRV-04 | Registrar insumos documentales no crea ningún movimiento de inventario | R2 | literal |
| SPA-OPS-T-SRV-05 | Un profesional se propone solo si su especialidad cumple la regla vigente | R2 | literal |
| SPA-OPS-T-SRV-06 | Facturar el servicio reutiliza el flujo de ventas y no crea otro documento comercial (SPA-T-15) | R2 | literal |
| SPA-OPS-T-PKG-01 | Dos consumos simultáneos de la misma sesión: solo uno se registra | R2 | literal |
| SPA-OPS-T-PKG-02 | El saldo coincide con la suma de movimientos tras una secuencia mixta | R2 | literal |
| SPA-OPS-T-PKG-03 | Un contrato no puede crearse dos veces para la misma línea de factura | R2 | literal |
| SPA-OPS-T-PKG-04 | Un movimiento no se edita ni elimina; la corrección es otro movimiento | R2 | literal |
| SPA-OPS-T-PKG-05 | Cancelar, reprogramar o ausencia producen el efecto sobre el saldo que defina la política aprobada (pendiente) | R2 | literal |
| SPA-OPS-T-PKG-06 | Un contrato guarda la referencia a la factura sin copiar importes | R2 | literal |
| SPA-OPS-T-VTA-01 | Facturar un servicio de Spa reutiliza el flujo de ventas sin bodega | R2 | literal |
| SPA-OPS-T-VTA-02 | Registrar una sesión no crea factura, cobro ni asiento | R2 | literal |
| SPA-OPS-T-VTA-03 | El contrato referencia la factura sin copiar importes | R2 | literal |
| SPA-OPS-T-VTA-04 | Una factura de otro cliente no puede enlazarse al contrato | R2 | literal |
| SPA-OPS-T-VTA-05 | La anulación o devolución comercial sigue las reglas de Ventas y no altera el libro de sesiones de forma implícita | R2 | literal |
| SPA-OPS-T-VTA-06 | Profesional y usuario registrador se guardan por separado | R2 | literal |
| SPA-CARE-T-EXP-01 | El expediente referencia un BP activo con rol Customer y no copia su identidad | R3 | literal |
| SPA-CARE-T-EXP-02 | El número de ficha es único por empresa bajo concurrencia | R3 | literal |
| SPA-CARE-T-EXP-03 | Corregir antecedentes crea una versión nueva y conserva la anterior | R3 | literal |
| SPA-CARE-T-EXP-04 | «Sin responder» no se interpreta como «no» | R3 | literal |
| SPA-CARE-T-EXP-05 | Profesional y usuario registrador se guardan por separado | R3 | literal |
| SPA-CARE-T-EXP-06 | `employees.view` no permite consultar un expediente | R3 | literal |
| SPA-CARE-T-EXP-07 | El expediente de otra empresa o tenant no es visible | R3 | literal |
| SPA-CARE-T-SEG-01 | Un usuario con `employees.view` y sin permiso de Spa no accede a un expediente | R3 | literal |
| SPA-CARE-T-SEG-02 | Una asignación laboral a una sucursal no concede acceso a sus expedientes | R3 | literal |
| SPA-CARE-T-SEG-03 | Modificar antecedentes exige un permiso distinto del de consulta y del de valoraciones | R3 | literal |
| SPA-CARE-T-SEG-04 | Exportar exige un permiso propio y deja registro | R3 | literal |
| SPA-CARE-T-SEG-05 | Un perfil existente no recibe los permisos nuevos de Spa | R3 | literal |
| SPA-CARE-T-SEG-06 | Profesional y usuario registrador son distintos y ambos auditados | R3 | literal |
| SPA-CARE-T-SEG-07 | La agenda y los listados no exponen datos clínicos | R3 | literal |
| SPA-CARE-T-SEG-08 | Un usuario de otra empresa o tenant no accede | R3 | literal |
| SPA-CARE-T-SEG-09 | El comportamiento del rol Admin sigue la decisión aprobada (pendiente) | R3 | literal |
| SPA-CARE-T-FAC-01 | una valoración puede guardarse sin campos clínicos opcionales y con los mínimos de integridad | R3 | literal |
| SPA-CARE-T-FAC-02 | corregir conserva la versión anterior | R3 | inferido del sufijo abreviado |
| SPA-CARE-T-FAC-03 | el rótulo no indica diagnóstico médico | R3 | inferido del sufijo abreviado |
| SPA-CARE-T-FAC-04 | fototipo admite solo I a VI o sin evaluar | R3 | inferido del sufijo abreviado |
| SPA-CARE-T-FAC-05 | la decisión no crea citas ni consume sesiones | R3 | inferido del sufijo abreviado |
| SPA-CARE-T-COR-01 | medidas opcionales dentro de un conjunto | R3 | literal |
| SPA-CARE-T-COR-02 | una corrección conserva el conjunto anterior | R3 | inferido del sufijo abreviado |
| SPA-CARE-T-COR-03 | las diferencias no se almacenan y coinciden con los conjuntos | R3 | inferido del sufijo abreviado |
| SPA-CARE-T-COR-04 | las medidas respetan unidades y precisión oficial, SPA-T-08 | R3 | inferido del sufijo abreviado |
| SPA-CARE-T-COR-05 | «no medida» es distinto de cero | R3 | inferido del sufijo abreviado |
| SPA-CARE-T-COR-06 | fechas como `DateOnly` | R3 | inferido del sufijo abreviado |
| SPA-CARE-T-CNS-01 | cada tipo es independiente, SPA-T-10 y SPA-T-11 | R3 | literal |
| SPA-CARE-T-CNS-02 | el texto de una versión no cambia | R3 | inferido del sufijo abreviado |
| SPA-CARE-T-CNS-03 | un registro no se edita ni elimina | R3 | inferido del sufijo abreviado |
| SPA-CARE-T-CNS-04 | un retiro bloquea usos futuros sin alterar atenciones pasadas | R3 | inferido del sufijo abreviado |
| SPA-CARE-T-CNS-05 | la interrupción no marca la sesión como realizada completa | R3 | inferido del sufijo abreviado |
| SPA-CARE-T-CNS-06 | una sesión sin consentimiento de procedimiento vigente no puede registrarse como realizada | R3 | inferido del sufijo abreviado |
| SPA-CARE-T-CNS-07 | el uso académico no existe como funcionalidad hasta confirmarse | R3 | inferido del sufijo abreviado |
| SPA-CARE-T-FOT-01 | subir sin consentimiento de seguimiento vigente se rechaza | R3 | literal |
| SPA-CARE-T-FOT-02 | la finalidad promocional exige su autorización, SPA-T-11 | R3 | inferido del sufijo abreviado |
| SPA-CARE-T-FOT-03 | no hay acceso sin permiso | R3 | inferido del sufijo abreviado |
| SPA-CARE-T-FOT-04 | cada consulta queda registrada | R3 | inferido del sufijo abreviado |
| SPA-CARE-T-FOT-05 | una fotografía no se elimina físicamente | R3 | inferido del sufijo abreviado |
| SPA-CARE-T-FOT-06 | las fotografías no aparecen en listados ni en la agenda | R3 | inferido del sufijo abreviado |
| SPA-CARE-T-FOT-07 | aislamiento por empresa | R3 | inferido del sufijo abreviado |
| SPA-CARE-T-FOT-08 | un retiro bloquea el uso futuro según la política aprobada, pendiente | R3 | inferido del sufijo abreviado |
| SPA-SEC-T-01 | Un Admin sin concesión no obtiene el detalle de un expediente (si se adopta C) y un acceso excepcional exige motivo y queda registrado | R4 | literal |
| SPA-SEC-T-02 | Un usuario con perfil sin el permiso de Spa recibe 403 aunque tenga `employees.view` | R4 | literal |
| SPA-SEC-T-03 | Cada consulta de detalle, descarga y exportación crea un registro de acceso con usuario, empresa, recurso, operación, instante y resultado, sin contenido | R4 | literal |
| SPA-SEC-T-04 | Un acceso denegado a un recurso de Spa queda registrado con el resultado `Denied` | R4 | literal |
| SPA-SEC-T-05 | Si el registro síncrono falla, la lectura de una fotografía se deniega | R4 | literal |
| SPA-SEC-T-06 | Una ruta con `..` o absoluta no puede resolverse fuera del directorio base | R4 | literal |
| SPA-SEC-T-07 | Dos subidas simultáneas no sobrescriben un archivo existente | R4 | literal |
| SPA-SEC-T-08 | Un archivo cifrado no se lee sin su clave; el contenido en disco no es legible | R4 | literal |
| SPA-SEC-T-09 | La restauración de un respaldo cifrado recupera archivos y base y verifica sus hashes | R4 | literal |
| SPA-SEC-T-10 | La eliminación legal deja el registro del hecho sin datos personales y los archivos ilegibles | R4 | literal |
| SPA-SEC-T-11 | Una corrección crea una versión nueva y la anterior queda con acceso restringido | R4 | literal |
| SPA-SEC-T-12 | Un retiro de consentimiento bloquea el uso futuro y no altera atenciones previas | R4 | literal |
| SPA-SEC-T-13 | La fecha de nacimiento y el contacto de emergencia no se copian a BusinessPartner | R4 | literal |
| SPA-SEC-T-14 | El aislamiento por tenant y empresa se mantiene en lecturas, exportaciones y registros de acceso | R4 | literal |
| SPA-SEC-T-15 | Un usuario limitado a una sucursal no accede a expedientes de otra (si se adopta la alternativa 2) | R4 | literal |

## Anexo G. Evidencia legal consultada (Ecuador)

Incorporada desde el borrador de seguridad y cumplimiento retirado (historial Git). Detalle de la sección 22.3; no es asesoría jurídica ni certificación de cumplimiento.

### G.1 Fuentes y fecha de consulta

**Alcance de esta sección:** reúne las disposiciones consultadas, las interpretaciones del autor y los puntos no verificados. **No constituye una certificación de cumplimiento ni una
revisión jurídica definitiva**, y no sustituye la validación de un profesional legal.

Fecha de consulta: **18 de noviembre de 2026**. Los textos se leyeron extrayendo el texto de PDF publicados en el portal oficial del Estado (gob.ec) y de un sitio gubernamental; la extracción contiene algunos errores de reconocimiento de caracteres, por lo que
**cada cita debe confirmarse contra el Registro Oficial antes de usarse formalmente**.

| Norma | Datos de publicación | Fuente consultada | Estado indicado en la fuente |
|---|---|---|---|
| Ley Orgánica de Protección de Datos Personales (LOPDP) | Quinto Suplemento del Registro Oficial 459, 26 de mayo de 2021 | [gob.ec (PDF)](https://www.gob.ec/sites/default/files/regulations/2025-01/01%20Ley%20Org%C3%A1nica%20de%20Protecci%C3%B3n%20de%20Datos%20Personales.pdf) y copia del [Consejo de Comunicación](https://consejodecomunicacion.gob.ec/wp-content/uploads/downloads/2021/07/lotaip/Ley%20Org%C3%A1nica%20de%20Protecci%C3%B3n%20de%20Datos%20Personales.pdf) | Vigente |
| Reglamento General a la LOPDP | Decreto Ejecutivo 904, Suplemento del Registro Oficial 435, 13 de noviembre de 2023 | [gob.ec (PDF)](https://www.gob.ec/sites/default/files/regulations/2025-01/02%20Reglamento%20General%20a%20la%20Ley%20Org%C3%A1nica%20de%20Protecci%C3%B3n%20de%20Datos%20Personales_0.pdf) | Vigente; «Fecha de última modificación: No aplica» |
| Ley de Comercio Electrónico, Firmas Electrónicas y Mensajes de Datos | Ley 67, Suplemento del Registro Oficial 557, 17 de abril de 2002 | [gob.ec (PDF, 2025-11)](https://www.gob.ec/sites/default/files/regulations/2025-11/Ley%20de%20Comercio%20Electr%C3%B3nico,%20Firmas%20y%20Mensaje%20de%20Datos.pdf) | Reformado; última reforma indicada: 7 de febrero de 2023 |

**Vigencia de los textos [L-NV]:** las copias de la LOPDP y del Reglamento en gob.ec se publicaron en enero de 2025, y esta consulta no verificó reformas posteriores. Un resultado de búsqueda aislado mencionó una posible reforma de 2026 al artículo 43 de la LOPDP;
**no se confirmó en ninguna fuente oficial** y no se toma como hecho.

### G.2 Obligaciones expresas verificadas [L-EXPRESO]

| ID | Disposición | Contenido (resumido; verificar el texto) | Aplicación a Spa |
|---|---|---|---|
| L-01 | LOPDP, Art. 4 | Define **datos relativos a la salud** (datos sobre la salud física o mental, incluida la prestación de servicios de atención sanitaria, que revelen información sobre el estado de salud) y **datos sensibles** (incluye salud y datos biométricos) | Ver L-INT-01 |
| L-02 | LOPDP, Art. 8 | El consentimiento debe ser libre, específico, informado e inequívoco; revocable en cualquier momento sin justificación, con mecanismos ágiles, gratuitos y sencillos, similares al de la obtención; el tratamiento previo a la revocación es lícito; si hay varias finalidades, el consentimiento debe constar para todas | CARE-PRIVACY §5 |
| L-03 | Reglamento, Art. 5 | El responsable debe informar previa y detalladamente tipos de tratamiento, finalidades, tiempo de conservación, medidas de protección y consecuencias; el consentimiento debe ser demostrable cuando la autoridad lo requiera; el silencio o la inacción no lo presumen; el de niñas, niños y adolescentes se obtiene por sus representantes legales | Evidencia del consentimiento (§7.4) |
| L-04 | Reglamento, Art. 6 | Tras la revocatoria, el responsable debe suspender el tratamiento | SPA-SEC-RET-04 |
| L-05 | LOPDP, Art. 12 | Derecho a la información: fines, base legal, tipos de tratamiento, **tiempo de conservación**, existencia de la base de datos, transferencias, posibilidad de revocar, forma de ejercer los derechos, dónde reclamar | Aviso de privacidad |
| L-06 | LOPDP, Art. 14 | Rectificación y actualización de datos inexactos o incompletos; atención en un plazo de **quince (15) días** | SPA-SEC-RET-03 |
| L-07 | LOPDP, Art. 15 | Derecho de eliminación en los supuestos que enumera (incluye la revocación del consentimiento y el vencimiento del plazo de conservación); el responsable aplicará métodos para eliminar, hacer ilegible o dejar irreconocibles los datos de forma definitiva y segura, en **quince (15) días**, gratis | §6 |
| L-08 | LOPDP, Art. 18 | Excepciones a rectificación, eliminación, oposición y portabilidad, entre ellas obligación legal o contractual, defensa de reclamos y otros casos que enumera | §6 |
| L-09 | LOPDP, Art. 10 (i) | Los datos se conservan por un tiempo no mayor al necesario; el responsable establecerá plazos para su supresión o revisión periódica | SPA-SEC-RET-06 |
| L-10 | Reglamento, Arts. 8 a 11 | Plazos de conservación acotados a la finalidad; cumplida esta, eliminación, bloqueo o anonimización; el fichero de registro debe contener el plazo de conservación; la eliminación no aplica, entre otros supuestos, por razones de interés público en el ámbito de la salud pública y privada o para formular, ejercer o defender reclamaciones | §6 |
| L-11 | LOPDP, Arts. 25 y 26 | Son categorías especiales los datos sensibles, de niñas, niños y adolescentes, de salud y de personas con discapacidad; el tratamiento de datos sensibles está prohibido salvo, entre otros casos, el **consentimiento explícito** con fines claramente especificados | Consentimientos |
| L-12 | LOPDP, Arts. 30 y 31 | Datos de salud: los profesionales de la salud y las instituciones del Sistema Nacional de Salud pueden tratarlos según la ley; deber de **confidencialidad** que subsiste al terminar la relación; el tratamiento de datos de salud requiere consentimiento previo salvo excepciones; **siempre que sea posible, se anonimizarán o seudonimizarán** | Ver L-INT-01 |
| L-13 | LOPDP, Art. 21; Reglamento, Arts. 19 y 20 | No se tratan datos sensibles ni de niñas, niños y adolescentes sin la autorización expresa del titular o de su representante legal; para menores de 15 años se requiere el consentimiento del representante legal; los adolescentes desde los 15 años pueden otorgar su consentimiento explícito si se les informa en lenguaje sencillo; el consentimiento no puede menoscabar el interés superior del menor | CNS-08 |
| L-14 | LOPDP, Art. 37 | Medidas de seguridad según categorías y volumen de datos y riesgos; entre otras, anonimización, seudonimización o **cifrado**, medidas de confidencialidad, integridad y disponibilidad, y de resiliencia; evaluación continua de su eficacia | §5 |
| L-15 | LOPDP, Art. 39; Reglamento, Arts. 59 y 60 | Protección de datos desde el diseño y por defecto: solo se tratan los datos necesarios para cada finalidad | §8 |
| L-16 | LOPDP, Art. 43; Reglamento, Arts. 24 a 28 | Notificación de vulneraciones de seguridad a la autoridad en un término de **cinco (5) días**; el encargado notifica al responsable en **dos (2) días** | Plan de incidentes |
| L-17 | LOPDP, Art. 47 | Obligaciones del responsable: políticas de protección, análisis y gestión de riesgos, medidas, notificación de violaciones, protección desde el diseño y por defecto, contratos de confidencialidad con el personal y encargados, registro nacional, delegado cuando corresponda y permitir auditorías | Gobierno del proyecto |
| L-18 | LOPDP, Art. 48; Reglamento, Arts. 53 y 54 | Se designa un delegado de protección de datos, entre otros casos, en el tratamiento **a gran escala de categorías especiales de datos**, según los factores del Reglamento | Ver L-INT-03 |
| L-19 | Reglamento, Art. 31 | La evaluación de impacto es obligatoria en los casos que la ley establece, entre ellos el tratamiento a gran escala de categorías especiales | Ver L-INT-03 |
| L-20 | LOPDP, Disposición Transitoria Primera | El régimen de medidas correctivas y sancionatorio entra en vigencia dos años después de la publicación (26 de mayo de 2021) | Cómputo: L-INT-04 |
| L-21 | Ley 67, Arts. 2, 13, 14, 15 | Los mensajes de datos tienen igual valor jurídico que los documentos escritos; la **firma electrónica** son datos electrónicos asociados a un mensaje que identifican al titular e indican que aprueba el contenido; tiene la misma validez que la manuscrita y se admite como prueba; para su validez debe ser individual y vinculada exclusivamente a su titular, permitir verificar inequívocamente la autoría e identidad, tener un método de creación y verificación confiable, seguro e inalterable, y estar bajo control exclusivo del signatario | CNS firma |
| L-22 | Ley 67, Art. 5 | Principios de confidencialidad y reserva de los mensajes de datos, incluido el secreto profesional | Evidencia electrónica |
| L-23 | Ley 67, Arts. 52 a 55 | Los mensajes de datos y firmas electrónicas se admiten como prueba; existe una presunción para la firma electrónica certificada (Art. 53, solo se leyó el título) | CNS firma |

### G.3 Interpretaciones (no verificadas por un profesional legal)

| ID | Interpretación | Estado |
|---|---|---|
| L-INT-01 | Los antecedentes que declara la persona (enfermedades, medicamentos, alergias, embarazo, reacciones) **revelan información sobre su estado de salud** y probablemente son datos de salud y sensibles (L-01). Si Spa es un «profesional de la salud» o parte del Sistema Nacional de Salud (L-12) **no se verificó** | [L-INTERPRETACIÓN]; [L-NV] |
| L-INT-02 | Las fotografías del rostro o del cuerpo son datos personales; si constituyen **datos biométricos** depende de que se traten para identificar a la persona. El texto de la ley extraído no contiene las palabras «imagen» ni «fotografía». El derecho a la propia imagen en otras normas **no se consultó** | [L-INTERPRETACIÓN]; [L-NV] |
| L-INT-03 | Si el volumen de Spa constituye «gran escala» (delegado, evaluación de impacto, registro) depende de los factores del Reglamento y de la Autoridad; **no se evaluó** | [L-NV] |
| L-INT-04 | Con la publicación del 26 de mayo de 2021 y la transitoria primera, el régimen sancionatorio estaría vigente desde el 26 de mayo de 2023; es un cómputo del texto, no una confirmación oficial | [L-INTERPRETACIÓN] |
| L-INT-05 | Una firma manuscrita capturada en pantalla o una aceptación electrónica pueden o no cumplir los requisitos del Art. 15 (L-21) y su valor probatorio depende del caso; si el consentimiento de un tratamiento exige firma electrónica certificada **no se verificó** | [L-INTERPRETACIÓN]; [L-NV] |
| L-INT-06 | Un menor de 15 años exige consentimiento del representante legal (L-13); cómo se acredita la representación **no se verificó** | [L-NV] |
| L-INT-07 | Que Spa sea el «responsable» y el titular del ERP o un tercero el «encargado» depende de la relación contractual; **no se determinó** | [L-NV] |

### G.4 Recomendaciones técnicas derivadas [L-RECOMENDACIÓN]

| ID | Recomendación | Relación |
|---|---|---|
| SPA-SEC-LEG-01 | Mostrar al titular antes de recabar el consentimiento los puntos del Art. 12 aplicables y registrar la versión mostrada | L-02, L-03, L-05 |
| SPA-SEC-LEG-02 | Conservar la evidencia del consentimiento de forma que pueda **demostrarse** (versión del texto, instante, quién consintió y, si aplica, su representante) | L-03 |
| SPA-SEC-LEG-03 | Registrar las solicitudes de ejercicio de derechos con fecha de recepción y plazo de 15 días | L-06, L-07 |
| SPA-SEC-LEG-04 | Mantener un plan de respuesta a incidentes con los términos de 5 y 2 días | L-16 |
| SPA-SEC-LEG-05 | Minimizar los datos: no almacenar más de lo necesario para cada finalidad | L-15 |
| SPA-SEC-LEG-06 | Anonimizar o seudonimizar cuando se usen datos de seguimiento para fines distintos de la atención | L-12 |
| SPA-SEC-LEG-07 | Obtener asesoría legal para L-INT-01 a L-INT-07 antes de aprobar retención, exportación, publicación, firma y atención a menores | §7.3 |

### G.5 No consultado

Ley Orgánica de Salud (historia clínica y secreto profesional), normativa de la autoridad sanitaria sobre establecimientos de estética, Código de la Niñez y Adolescencia, normativa de protección al consumidor, el derecho constitucional a la propia imagen, resoluciones de la Superintendencia de
Protección de Datos Personales y plazos de conservación de historias clínicas: **[L-NV]**, sin ninguna afirmación.


## Anexo H. Glosario contractual

Distingue los conceptos que los documentos usan de forma entrelazada. **Todas las definiciones son propuestas [T]**, salvo donde se indica una etiqueta A o E y salvo las **alternativas A de G-09 y G-11, aprobadas por `O-25` el 2026-11-18**. Resuelven el conflicto terminológico C-06; **no resuelven sus políticas comerciales**, que siguen abiertas. **Lectura de este Anexo:** la procedencia histórica se conserva, pero toda frase marcada «SUPERADA» fue reemplazada por una decisión posterior (SPA-DEC-116, SPA-ARQ-01..08, SPA-TEC-01..03, O-25) y no debe leerse como alternativa abierta; las decisiones originales no se modifican.

| G | Término | Definición propuesta | No es | Entidad u origen oficial | Fuentes | Estado |
|---|---|---|---|---|---|---|
| G-01 | **Cliente** | Persona atendida. Su identidad es un BusinessPartner con rol Customer; Spa no crea otra identidad. | El expediente | MasterData (BusinessPartner) | `SPA-AR-01`, `SPA-INV-01` | R |
| G-02 | **Expediente** | Registro longitudinal, propio de Spa, de la atención de un cliente (antecedentes versionados, valoraciones, sesiones, consentimientos y fotografías). Referencia al cliente; no copia su identidad. **Cardinalidad aprobada (SPA-DEC-116; O-20 resuelta): exactamente un expediente por cliente y empresa**, con la sucursal conservada en cada registro; la cardinalidad «pendiente» de la redacción histórica está SUPERADA. | El cliente; la ficha física; un expediente por caso o sucursal | Spa | `SPA-FC-01`, `SPA-CARE-EXP-01`, `SPA-CARE-EXP-04` | T (definición); **A** (un expediente por cliente y empresa) |
| G-03 | **Profesional** | Empleado que presta o documenta la atención, identificado por `EmployeeId`. «Profesional responsable» es el que realiza la atención. | El usuario registrador | Empleados (Employee) | `SPA-AR-03`, `SPA-INV-02` | **A** (ADR-050 §3 DEC-E4, §13) |
| G-04 | **Usuario registrador** | Usuario ERP que registra la información en el sistema. Distinto del profesional aunque puedan coincidir en una misma persona (observación T). | El profesional | Identity / Access (IdentityUser) | `SPA-SG-02`, `SPA-INV-03` | **A** para la distinción; la coincidencia posible es T |
| G-05 | **Servicio** | Lo que se comercializa y factura: un Item de naturaleza `Service`. | El procedimiento | Items (`items.md` §3) | `SPA-SV-01`, `SPA-OPS-SRV-01` | **E** + T |
| G-06 | **Procedimiento** | Técnica estética realizada sobre la persona, con su protocolo, precauciones y equipos. Concepto propio de Spa. Distinto del servicio comercial (Item `Service`). Relación servicio–procedimiento: la apertura 1:1 o N:M de la redacción histórica queda **SUPERADA por la selección técnica aprobada N:M ordenado** (perfil por Item Service + procedimientos reutilizables; ADR-051 §5.11/§5.16, SPA-SEL-01), **selección SPA-SEL-01 aprobada el 2026-11-19; no implementada**. | El servicio | Spa | `SPA-OPS-SRV-02`, `SPA-FC-04` | F + R; representación N:M = selección técnica aprobada (SPA-SEL-01, 2026-11-19; no implementada) |
| G-07 | **Plan de tratamiento** | Lo previsto para la persona: objetivo, procedimiento, sesiones sugeridas, cuidados y decisión de proceder, adaptar o posponer. Sinónimo propuesto de «plan de atención» (`SPA-AT-01`). | El paquete comercial | Spa (expediente) | `SPA-AT-01`, `SPA-AT-05` | F; el sinónimo es T |
| G-08 | **Paquete comercial** | Derecho de consumo de sesiones derivado de una compra (definición, contrato y libro de movimientos). | El plan de tratamiento | Spa, con la factura de Ventas | `SPA-PQ-01`, `SPA-OPS-PKG-01` a `PKG-07` | T + P (O-13); reglas funcionales SUPERADAS por SPA-ARQ-05 y SPA-TEC-03 (§11) |
| G-09 | **Reserva** | Acto de apartar un intervalo para un profesional (la reserva de recursos físicos —cabinas/equipos— está excluida, HX-01; la mención histórica a un recurso está SUPERADA). **Alternativa A (APROBADA por `O-25`, 2026-11-18):** la reserva es la operación que crea una cita, no una entidad aparte. ~~Alternativa B: reserva provisional como entidad previa a la confirmación~~ (no aprobada). | La atención | Spa (agenda) | `SPA-AG-01`, `SPA-OPS-AGE-01` | **A (aprobada por `O-25`)** |
| G-10 | **Cita** | Bloque de agenda de una visita: profesional, intervalo (inicio y fin UTC) y uno de cinco estados (SPA-ARQ-01/02, que **superan** la redacción histórica de una cita única por cliente/servicio); el cliente, la sucursal y las prestaciones se relacionan a través de la visita y de los enlaces bloque–prestación. | La atención ni la sesión | Spa (agenda) | `SPA-OPS-AGE-01`, `SPA-OPS-AGE-02` | T |
| G-11 | **Sesión programada** | Sesión prevista dentro de un plan o paquete que aún no se realiza. **APROBADO por `O-25` (2026-11-18): no existe entidad «sesión programada»; es una cita vinculada al plan o contrato.** La alternativa B (entidad propia con estado) fue descartada y está SUPERADA: no se crea ninguna entidad adicional. | La sesión realizada | Spa | `SPA-AT-06`, `SPA-OPS-AGE-10` | **A (aprobada por `O-25`)**; políticas de saldo abiertas (O-13) |
| G-12 | **Atención** | El hecho de prestar un procedimiento a un cliente en un momento determinado. Se documenta mediante una sesión realizada. | La cita (programación) ni la sesión (registro) | Spa | `SPA-OPS-AGE-10`, `SPA-AT-06` | T |
| G-13 | **Sesión realizada** | Registro documentado de una atención efectivamente prestada (fecha, procedimiento, profesional, observaciones, reacciones, evolución). No se registra como realizada por haber llegado la fecha. | La sesión programada ni la consumida | Spa | `SPA-AT-02`, `SPA-AT-06`, `SPA-AT-07` | F + T |
| G-14 | **Sesión consumida** | Sesión que descuenta saldo de un paquete (movimiento de consumo del libro). **No equivale a «realizada»:** ausencia sin descuento automático ni penalización comercial automática V1.1 conforme SPA-ARQ-05 §11.2 regla 5. | La sesión realizada | Spa (libro del contrato) | `SPA-OPS-PKG-01`, §4.4 de R2 | T + P; propuesta de R2 (O-13) |
| G-15 | **Consentimiento** | Autorización registrada, con tipo (procedimiento, fotografías de seguimiento, publicación promocional, uso académico pendiente), versión del texto, decisión, fecha y evidencia. Cada tipo es independiente. | Una firma sola ni una fotografía | Spa | `SPA-CN-01` a `CN-06`, `SPA-CARE-CNS-01` | F + T |

### 2.1 Relaciones entre conceptos (propuesta)

- Un **cliente** tiene **exactamente un expediente por empresa** (SPA-DEC-116; la lectura histórica «uno o varios» está SUPERADA); el expediente contiene **planes de tratamiento**, **sesiones realizadas**, **consentimientos** y **fotografías**.
- Un **servicio** se vende por Ventas; un **paquete comercial** referencia la factura y el servicio y genera **sesiones consumidas** en su libro.
- Una **cita** (resultado de una **reserva**) puede dar lugar a una **atención**, que se documenta en una **sesión realizada**; esa sesión puede ser, además, **consumida** de un paquete.
- La **sesión programada** de un plan o paquete **es una cita vinculada** (O-25, aprobada; no hay una segunda entidad ni alternativa abierta).
- El **profesional** (Employee) y el **usuario registrador** (IdentityUser) se guardan siempre por separado.

## Anexo I. Conflictos documentales abiertos o compatibles y dependencias

| C | Tema | Naturaleza | Identificadores | Resolución |
|---|---|---|---|---|
| C-03 | Auditoría y privacidad | Decisión pendiente (sin contradicción) | `SPA-AU-01`, `SPA-AU-02`, `SPA-CARE-FOT-06`, `SPA-SEC-AUD-01`, `SPA-SEC-AUD-07`, `SPA-SEC-INV-01`, `SPA-SEC-R-02` | No hay contradicción: R3 deja el registro de lectura como mecanismo inexistente; R4 §4 lo propone. Pendiente: el propio registro de accesos es dato personal y su retención (`SPA-SEC-AUD-07`) no está definida (O-03). |
| C-04 | Fotografías y consentimiento | Compatibles; decisión pendiente | `SPA-CN-02`, `SPA-CN-03`, `SPA-CN-06`, `SPA-INV-05`, `SPA-CARE-FOT-03`, `SPA-CARE-FOT-08`, `SPA-CARE-CNS-01`, `SPA-SEC-INV-02`, `SPA-SEC-INV-10` | Compatibles: R1 `SPA-CN-06`, R3 `SPA-CARE-FOT-03` y R4 `SPA-SEC-INV-02` coinciden en que la finalidad se verifica contra autorizaciones independientes. Pendiente: retiro y conservación (`SPA-CARE-FOT-08`). |
| C-05 | Plan de atención y paquete comercial | Relación conceptual RESUELTA SPA-TEC-03; mecanismo pendiente | `SPA-AT-01`, `SPA-AT-05`, `SPA-PQ-01` | §10.2: plan/prestaciones/contratos/documentos/pagos explícitos, varias facturas/contratos y contratación parcial; no vínculo por mismo BP ni finanzas SPA. Representación técnica pendiente. |
| C-07 | Agenda y asignaciones de Empleados | Compatibles; dependencia no implementada | `SPA-OPS-AGE-04`, `SPA-OPS-AGE-14`, `SPA-AR-06`, `SPA-AG-03` | Compatibles con ADR-050 §8 y EMP-BR-11 y EMP-BR-12: la disponibilidad usa la elegibilidad de Empleados y no los permisos de acceso. Dependencia en un contrato **no implementado** (D-01). |
| C-10 | Fecha de nacimiento y contacto de emergencia | Compatibles (SECURITY amplía las alternativas de CARE-PRIVACY) | `SPA-FC-02`, `SPA-FC-07`, `SPA-CARE-EXP-01`, `SPA-SEC-R-07` | R3 §2.4 ofrece las alternativas A a C y R4 §8, A a E; son compatibles y R4 amplía a R3. Pendiente O-06. |

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

### Redacciones canónicas de reglas duplicadas (DUP-01, 02, 08, 10, 11)

Incorporadas desde la normalización retirada; las reglas se aplican en las secciones indicadas y no añaden aprobaciones.

| DUP | Identificadores | Redacción canónica | Evidencia | Estado |
|---|---|---|---|---|
| DUP-01 | `SPA-AR-03` | Cada atención registrada identifica al **profesional responsable** (Employee, `EmployeeId`) y, por separado, al **usuario ERP que la registró**; son conceptos distintos. Los profesionales que documentan atenciones utilizan usuarios ERP autorizados | A (ADR-050 §3 DEC-E4, §13); «se auditan por separado» A-der | Aplicada (§6) |
| DUP-02 | `SPA-AR-01`, `SPA-AR-02` | SPA no crea identidades paralelas: el cliente es un BusinessPartner con rol Customer y el profesional un Employee; SPA referencia sus identificadores y no copia nombre, identificación ni contacto | A para Employee (ADR-050 §3 DEC-E1, §13); R para el cliente | Aplicada (§5, §6) |
| DUP-08 | `SPA-AT-06` | La **cita** es la programación en la agenda y la **sesión realizada** es un registro independiente creado al documentar una atención; una **sesión programada** de un paquete es una cita vinculada al contrato; **reservar y reprogramar no consume saldo**; cancelar o registrar una ausencia **no crea una sesión realizada** | A (`O-25`, [registro](SPA-V1.1-APPROVED-DECISIONS-01.md)) | Aplicada (§8, §12); políticas de saldo abiertas (O-13) |
| DUP-10 | `SPA-OPS-VTA-03` | **Bloqueada:** depende de si el ERP debe tener anticipos de clientes, capacidad inexistente hoy | — | Bloqueada por O-14 (C-08, `DIR-SSOT-01`, §20.4) |
| DUP-11 | `SPA-INV-10` | Las fechas de negocio son `DateOnly`; los instantes, UTC mostrados en `Company.Timezone`; los valores numéricos siguen la política de precisión oficial. Las medidas corporales no coinciden con ninguna clase de la tabla de escalas y requieren revisión arquitectónica (V-04) | A-der (normas oficiales) + hallazgo V-04 | Aplicada (§15); medidas pendientes (O-22) |

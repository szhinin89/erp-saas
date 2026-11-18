# Gali Spa V1.1 — Contrato funcional consolidado (SPA-V1.1-CONTRACT-SPEC-01)

**ALCANCE FUNCIONAL APROBADO (SPA-DEC-115/116) — CONSOLIDACIÓN TÉCNICA EN REVISIÓN — SIN IMPLEMENTACIÓN AUTORIZADA.**

Candidato a fuente única de verdad **funcional** de Gali Spa V1.1 (2026-11-18, revisión 3 — integración de SPA-DEC-115/116 recuperadas). Responde a cuatro preguntas por funcionalidad: **qué se desarrollará, cómo debe funcionar, qué está
aprobado y qué no lo está, y qué queda excluido.** Consolida, sin añadir decisiones de negocio, los siete documentos previos de Spa (§0.7). **No es un ADR, no es una SSOT vigente** (la SSOT de un
módulo nace cuando existen código y pruebas: [enforcement](../architecture/enforcement.md#ssot-por-módulo)) **y no autoriza implementar Gali Spa ni Empleados**, migraciones, endpoints, pantallas,
permisos ni cambios en módulos FROZEN o CLOSED. Estado de entrega: [STATUS](../../STATUS.md); cola: [WORK_QUEUE](../agent/WORK_QUEUE.md); índice oficial: [decisiones](README.md).

Patrón documental: el mismo de Orden de Pedido de Venta V1 ([ADR-049](ADR-049-sales-order-v1-architecture.md), [especificación funcional](PEDIDOS-VENTA-V1-FUNCTIONAL-SPEC-01.md)) y de Empleados V1
([ADR-050](ADR-050-employees-v1-architecture.md), [especificación contractual](EMPLOYEES-V1-CONTRACT-SPEC-01.md)): una especificación central con etiquetas de estado, jerarquía de autoridad
explícita, decisiones del propietario separadas de las propuestas técnicas y [ADR-051](ADR-051-gali-spa-v1.1-architecture.md) PROPOSED / NO APROBADO y [plan](GALI-SPA-V1.1-IMPLEMENTATION-PLAN-01.md) preparado sin autorización de ejecución; su aprobación sigue condicionada a §29.

## 0. Cómo leer este contrato

### 0.1 Precedencia normativa

Ante una diferencia entre documentos se aplica este orden. Una diferencia puramente documental se corrige aquí; **dos decisiones aprobadas que realmente se contradigan no se resuelven en silencio:
se registran como bloqueo (§29).**

| Nivel | Fuente | Alcance |
|---|---|---|
| 1 | Decisiones expresamente aprobadas por el propietario | Solo dentro de su alcance literal: `SPA-DEC-115`, `SPA-DEC-116` (evidencia histórica aportada por el propietario, REG §9/10), `O-25`, `O-15` ([registro](GALI-SPA-V1.1-APPROVED-DECISIONS-01.md)), las decisiones de Empleados de [ADR-050](ADR-050-employees-v1-architecture.md) que atañen a Spa y la **instrucción** del propietario `DIR-SSOT-01` (§20.4), que fija una frontera y no aprueba un diseño |
| 2 | ADR y contratos vigentes del ERP | Respetando sus límites: [ADR-017](ADR-017-business-partner-scope.md) (BusinessPartner, FROZEN), [ADR-034](ADR-034-temporal-contract-single-source.md), [ADR-035](ADR-035-supplier-payment-unapplied-advance.md), [ADR-041](ADR-041-negative-sale-inventory-cost-obligations.md), «Security & Access Contract V1 — LOCKED» ([security](../architecture/security.md)), [auditoría](../architecture/audit-infrastructure.md), [datos](../architecture/data-standards.md), Ventas FROZEN |
| 3 | Evidencia técnica verificada del repositorio | Las verificaciones `V-xx` cerradas ([normalización §5](GALI-SPA-V1.1-CONTRACT-NORMALIZATION-01.md)) y las capacidades [E] citadas en cada sección |
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

**Ninguna funcionalidad es implementable hoy.** Causas comunes: (1) el alcance funcional está aprobado, pero falta aprobación técnica integral del contrato y ADR-051 está PROPOSED / NO APROBADO; el plan no autoriza fases; (2) Empleados no está implementado (`IEmployeeDirectory` es propuesta;
EMP-E1 PENDING / NO AUTORIZADA); (3) decisiones bloqueantes abiertas (§29); (4) fichas originales sin contraste (`V-01`); (5) validación jurídica pendiente (`V-06`).

### 0.5 Mapa de estado por área

| Área | Qué está aprobado | Detalle que sigue abierto | Implementable hoy |
|---|---|---|---|
| Disponibilidad, agenda y visitas (§7/8) | Búsqueda servicio/fecha/sucursal sin profesional previo; agenda única día/semana; restricciones; varios servicios/profesionales, llegada/cambios (SPA-DEC-115/116); O-25 íntegra | Cálculo/representación de restricciones y visita–cita, estados/locks, atención sin cita; no disponibilidad de cabinas/equipos | No |
| Expediente y valoración (§13/15) | Único por cliente/empresa; antecedentes, alertas, valoraciones, medidas/historial (SPA-DEC-116) | Fichas O-24/V-01, mínimos/errores, numeración, datos personales y precisión | No |
| Tratamientos y sesiones (§10/12/16) | Individual/multisesión, ampliación/suspensión/cancelación/cierre; borrador/finalización/interrupción/corrección, resultados/incidencias/evolución | Transiciones y efectos exactos, sesión parcial, permisos y relación con paquete | No |
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
| R1 | [GALI-SPA-V1.1-RECOVERY-01](GALI-SPA-V1.1-RECOVERY-01.md) | Reconstrucción, requisitos [F], invariantes `SPA-INV-*`, pruebas `SPA-T-*` |
| R2 | [GALI-SPA-V1.1-OPERATIONS-SPEC-DRAFT-01](GALI-SPA-V1.1-OPERATIONS-SPEC-DRAFT-01.md) | Agenda, servicios, paquetes, ventas (`SPA-OPS-*`) |
| R3 | [GALI-SPA-V1.1-CARE-PRIVACY-SPEC-DRAFT-01](GALI-SPA-V1.1-CARE-PRIVACY-SPEC-DRAFT-01.md) | Expediente, valoraciones, consentimientos, fotografías (`SPA-CARE-*`) |
| R4 | [GALI-SPA-V1.1-SECURITY-COMPLIANCE-DRAFT-01](GALI-SPA-V1.1-SECURITY-COMPLIANCE-DRAFT-01.md) | Seguridad, auditoría, retención, legal (`SPA-SEC-*`) |
| MATRIZ | [GALI-SPA-V1.1-CONTRACT-CONSOLIDATION-MATRIX-01](GALI-SPA-V1.1-CONTRACT-CONSOLIDATION-MATRIX-01.md) | 295 registros, conflictos `C-xx`, cuestiones `O-xx`, verificaciones `V-xx` |
| NORM | [GALI-SPA-V1.1-CONTRACT-NORMALIZATION-01](GALI-SPA-V1.1-CONTRACT-NORMALIZATION-01.md) | Redacciones canónicas, glosario, catálogo de permisos, grupos de pruebas `PT-xx` |
| REG | [GALI-SPA-V1.1-APPROVED-DECISIONS-01](GALI-SPA-V1.1-APPROVED-DECISIONS-01.md) | Fuente histórica aportada SPA-DEC-115/116, O-25/O-15, DIR-SSOT-01 y Empleados aplicable |

Inventario: ocho archivos previos GALI-SPA, más ADR-051 y plan preparados aquí. No se localizaron originales V01 ni GS-* independientes en el inventario de rutas; sus equivalencias transmitidas se conservan en Anexo E. Las fichas originales siguen sin contraste.

Fuentes externas sin inspección directa en esta consolidación: `Gali_Spa_Fichas_Actualizadas.docx`, `Gali_Spa_Fichas_Clinicas_y_Consentimiento.docx`, `Gali_Spa_Ficha_Compacta_Dos_Caras.pdf`, `Gali_Spa_Facial_Actualizada.pdf`, `Gali_Spa_Corporal_Actualizada.pdf` y otras variantes transmitidas. No se presume equivalencia entre versiones (O-24). SPA-DEC-115/116 se recuperaron con alcance aprobado mediante el encargo del propietario (§9/10 de REG), antes conocido solo parcialmente. No se recuperaron los textos SPA-DEC-001..114; la numeración no demuestra inventario completo ni fechas originales.

### 0.8 Cobertura y límites de la consolidación

§2 conserva las once áreas y siete agrupaciones de SPA-DEC-116; §3 las diez exclusiones; §7.1 los siete enunciados operativos de SPA-DEC-115. §23 reúne las fichas funcionales y §28 las verificaciones candidatas. Cada regla anterior mantiene identificador/procedencia; una aprobación general no aprueba automáticamente su traducción [T].

Detalles aún no recuperados: campos y obligatoriedad de fichas (O-21/O-24/V-01), flujo oficial de cliente inexistente, numeración/código de expediente (O-19), permisos/errores precisos por operación, representación de visita múltiple y borrador/interrupción de sesión, transiciones/efectos de tratamiento, restricciones temporales y ajustes/consumos de paquetes. Firma en pantalla, versiones, expediente único y las capacidades de §2 ya no se califican como pendientes de inclusión. Las fichas indican lo conocido y el detalle pendiente sin inventar diseño visual.

## 1. Objetivo del módulo

**Qué se desarrollará (alcance APROBADO por SPA-DEC-116; antecedente `SPA-AL-01` [R]):** el ámbito funcional del ERP destinado a administrar un establecimiento de servicios estéticos: clientes y expedientes de atención, profesionales
responsables, valoraciones faciales y corporales, consentimientos informados, fotografías y seguimiento, servicios y tratamientos, planes de tratamiento, agenda y reservas, sesiones realizadas, paquetes
comerciales, integración con ventas y cobros, y control de acceso y trazabilidad.

**Principio de reutilización (`SPA-AL-02`):** Spa no crea identidades laborales ni de cliente paralelas, ni registros comerciales o contables paralelos; reutiliza Empleados, BusinessPartner, Items, Ventas,
Cuentas por Cobrar y los mecanismos oficiales de seguridad y auditoría. Parte Empleados: APROBADO-DERIVADO (ADR-050 §3 DEC-E1, §13); parte resto: [R].

**Estado:** alcance funcional APROBADO por SPA-DEC-116 y búsqueda APROBADA por SPA-DEC-115; se conservan antecedentes [F]/[R] y mecanismos [T] separados. No se reinicia la planificación ni se autoriza desarrollo.

## 2. Alcance funcional aprobado de V1.1

**SPA-DEC-116 — APROBADA HISTÓRICA, recuperada en este encargo.** El propietario aporta expresamente su revisión de alcance y agrupaciones funcionales. Fuente canónica de evidencia: [REG §10](GALI-SPA-V1.1-APPROVED-DECISIONS-01.md). SPA-DEC-115 añade el comportamiento aprobado de búsqueda por servicio (§7.1). No se exige una nueva aprobación general ni se confunde inclusión con aprobación técnica integral.

| Área aprobada | Comportamiento incluido | Sección | Detalle pendiente, sin cuestionar inclusión |
|---|---|---|---|
| Clientes y expedientes | Expediente único por cliente y empresa; antecedentes, valoraciones, alertas e historial; sin identidad BP paralela | §5/13/15 | Versiones de fichas, datos/obligatoriedad, numeración y políticas de alerta |
| Empleados | Modelo ERP general reutilizable, vinculado con usuarios/sucursales; profesional separado del registrador | §6 | Directorio implementado/probado y contratos; no desarrollar Employee en Spa |
| Tratamientos | Plan individual, multisesión, ampliaciones, suspensión, cancelación, cierre y seguimiento de resultados | §10/16 | Transiciones, roles, validaciones y efectos sobre cupos/comercial |
| Sesiones | Registro técnico individual, formulario/borrador/finalización/interrupción/corrección; productos, incidencias, fotos, resultados y evolución | §12 | Atención efectiva vs borrador, sesión parcial, correcciones y consumo |
| Agenda y visitas | Día/semana, sucursal/profesional; disponibilidad por servicio; horarios, bloqueos, descansos, preparación, traslados/conflictos, reprogramaciones; varios servicios/profesionales por visita, llegada y cambios | §7/8 | Cálculo y representación de restricciones y visita–cita; sin valores inventados |
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
| SPA-AR-04 | Spa conserva servicios, tratamientos, seguimiento e información de atención estética | APROBADO como alcance por SPA-DEC-116; mecanismos pendientes | A + R |
| SPA-AR-06 | La elegibilidad del profesional se evalúa con `IEmployeeDirectory` y las fechas de la operación; el pasado no se altera porque el profesional deje la empresa | PROPUESTO | T |
| SPA-OPS-AGE-14 | Si un profesional deja de estar disponible, las citas futuras **no se modifican automáticamente**; se listan para reasignación manual. La política de reasignación es pendiente | PROPUESTO / PENDIENTE | T, P |
| SPA-OPS-SRV-06 | Profesionales elegibles por **especialidad** del catálogo de Empleados (DEC-E5); Spa no crea especialidades. Cargo y profesión pueden ser nulos: no suponer elegibilidad sin regla | PROPUESTO / PENDIENTE | T |

**Verificación pendiente:** `V-02` (contrato `IEmployeeDirectory` real, EMP-E1 a E6b). **Bloqueo:** `D-01` (§29).

## 7. Sucursales y disponibilidad

**Qué hace:** determina cuándo un profesional puede atender en una sucursal. **Estado de la sección:** la pertenencia, búsqueda por servicio y restricciones están APROBADAS (ADR-050, SPA-DEC-115/116); solo cálculo, representación técnica y políticas no aportadas siguen pendientes. **Implementable hoy:** no. **Bloqueos:** BL-01, BL-09 (§29.1).

| ID | Comportamiento contractual | Estado | Evid. |
|---|---|---|---|
| SPA-AG-01 a AG-03 / DUP-13 | Agenda, horarios y disponibilidad son de Spa. La disponibilidad combina horario del establecimiento, horario del profesional, elegibilidad informada por Empleados y ausencia de solape | pertenencia APROBADA; reglas PROPUESTAS | A (§13) + T |
| SPA-OPS-AGE-04 | Un profesional está **disponible** en un instante si: (a) está dentro del horario de la sucursal; (b) dentro de su horario de trabajo; (c) `IEmployeeDirectory` lo informa activo y elegible para la sucursal; (d) no tiene otra cita activa que se solape | PROPUESTO | T |
| SPA-OPS-AGE-03 | Horarios de establecimiento por sucursal como patrón de horas locales de la empresa; el instante UTC lo obtiene `ICompanyClock`; una hora inexistente por cambio de horario se rechaza. No hay precedente de horarios recurrentes (`V-05a`) | PROPUESTO | T, NV |
| SPA-OPS-AGE-05 | Control de disponibilidad de cabinas/equipos excluido de V1.1; alternativa B del borrador no se adopta. Bloqueos de horarios/profesionales siguen incluidos | EXCLUSIÓN APROBADA; O-08 resuelta en ese alcance | A (SPA-DEC-116, HX-01) |
| SPA-OPS-AGE-06 | Dos citas activas del mismo profesional no se solapan (condición de duplicidad). Si una cita puede comenzar cuando otra termina, o si se exige margen, es pendiente | PROPUESTO / PENDIENTE (`O-09`) | T, P |
| SPA-AG-03 / SPA-OPS-AGE-02 (nota) | La disponibilidad laboral no se confunde con el acceso ERP: una cita no concede ni requiere acceso a una sucursal y el acceso a una sucursal no hace disponible a un profesional | APROBADO-DERIVADO | A-der (ADR-050 §6, §8) |
| Descansos, bloqueos, preparación y traslados | Restricciones APROBADAS por SPA-DEC-115/116; no se inventan duración/tolerancia/margen ni su representación. Búsqueda sin confirmación automática | APROBADO objetivo; cálculo pendiente O-09 | A + P |

### 7.1 SPA-DEC-115 — Búsqueda de disponibilidad por servicio

**APROBADA; siete enunciados literales en REG §9.** Recepción introduce servicio, fecha y sucursal sin seleccionar profesional previamente. El mecanismo centralizado de la misma agenda considera habilitación de profesionales y horarios junto con descansos, bloqueos, preparación, traslados y restricciones aprobadas. Devuelve profesionales habilitados y horarios disponibles; recepción elige profesional/hora según conveniencia del cliente. **No confirma automáticamente la cita ni crea una segunda agenda o motor.**

Flujo principal: buscar → consultar alternativas del mismo motor → seleccionar explícitamente profesional/hora → continuar operación de reserva/cita según O-25. Excepciones: sin horarios o profesionales habilitados se informa ausencia de resultados; si cambia disponibilidad antes de reservar, revalidar con el mecanismo central. Este tratamiento de concurrencia es propuesta técnica; mensaje/código y política exacta de reconsulta pendientes, sin inventar plazos.

No se recuperaron de las decisiones anteriores los valores de descansos/preparación/traslado, prioridades, tolerancias, márgenes ni reglas para varias prestaciones. O-09/V-05 conserva ese detalle pendiente; no se reduce el objetivo aprobado ni se añade disponibilidad de cabinas/equipos.

## 8. Agenda, citas y reservas

**Qué hace:** programa atenciones y conserva su historial. **Estado de la sección:** agenda, visitas y búsqueda incluidas por SPA-DEC-115/116; modelo cita/sesión APROBADO por O-25. Mecanismos y políticas no aportadas siguen pendientes.
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

**Fuera del alcance de aprobación de O-25, sin ser exclusiones globales:** ausencias con consumo, atención sin cita y efectos comerciales de cancelación siguen pendientes. Varios servicios/profesionales por visita y restricciones de agenda sí están aprobados por SPA-DEC-115/116; O-25 no los revoca. Cálculo exacto y representación visita–cita pendientes; cabinas/equipos excluidos HX-01.

### 8.2 Reglas de la cita (PROPUESTAS, compatibles con O-25)

| ID | Comportamiento contractual | Estado | Evid. |
|---|---|---|---|
| SPA-OPS-AGE-01 | Una cita pertenece a una empresa y una sucursal, referencia al cliente BP; debe admitir las prestaciones y profesionales de una visita múltiple aprobada. La propuesta anterior de un único profesional/servicio no es una cardinalidad definitiva; estructura, intervalos y duración conjunta pendientes | PROPUESTO | T |
| SPA-OPS-AGE-02 | Estados candidatos: `Programada`, `Cancelada`, `Ausente`, `Atendida`. Llegada y cambios incluidos como capacidades por SPA-DEC-116; su estado técnico/confirmación manual pendientes. Recordatorios excluidos HX-02; búsqueda no confirma automáticamente | PROPUESTO | T |
| SPA-OPS-AGE-07 | Reprogramar **mantiene la identidad de la cita** y registra un cambio (instante anterior y nuevo, autor, motivo); no consume saldo (`O-25` #5). Cancelar y crear otra rompe la trazabilidad y no se propone | PROPUESTO; efecto sobre saldo APROBADO | T + `O-25` |
| SPA-OPS-AGE-08 | Cancelar fija estado `Cancelada`, motivo, autor e instante; nunca elimina. Quién cancela y con qué anticipación es pendiente; no crea sesión realizada (`O-25` #6) | PROPUESTO / PENDIENTE | T, P |
| SPA-OPS-AGE-09 | Ausencia: tras el inicio de la cita, un usuario autorizado la marca `Ausente`; no crea sesión realizada (`O-25` #6). **Su efecto sobre el saldo del paquete es pendiente** (`O-13`) | PROPUESTO / PENDIENTE | T, P |
| SPA-OPS-AGE-10 | La atención efectivamente realizada es un registro distinto de la cita (sesión, §12); la cita pasa a `Atendida` solo cuando existe una sesión realizada vinculada, no por llegar la fecha | APROBADO en el modelo (`O-25` #3); estado `Atendida` PROPUESTO | T + `O-25` |
| SPA-OPS-AGE-11 | Atención sin cita previa: alternativas A admitirla creando cita y sesión juntas, B exigir cita siempre | PENDIENTE (`O-10`) | P |
| SPA-OPS-AGE-12 | Todo cambio de estado, hora, profesional o sucursal queda en `AppointmentChange` y en Entity Audit | APROBADO en el principio de trazabilidad (`O-25` #8); mecanismo PROPUESTO | T |
| SPA-OPS-AGE-13 | Cambiar el profesional de una cita revalida la disponibilidad (§7) y registra el cambio | PROPUESTO | T |
| Transiciones | `Programada → Cancelada`, `→ Ausente`, `→ Atendida`; los tres destinos son finales en el borrador. Si una ausencia se revierte o una cancelada se reactiva es pendiente | PROPUESTO / PENDIENTE | T, P |
| Servicios y profesionales por visita | Varios servicios y varios profesionales aprobados; pendiente relación técnica entre visita, cita y cada prestación, sin imponer 1:1 | APROBADO funcional; O-11 parcial | A (SPA-DEC-116) + P |

### 8.3 Flujos (PROPUESTOS)

Reservar (cliente, servicio, sucursal, profesional, hora → comprobar disponibilidad → crear cita `Programada` y su primer registro de cambio, en una transacción); reprogramar; cancelar con motivo; marcar
ausencia solo sobre una cita pasada `Programada`; atender (crear la sesión realizada y marcar la cita `Atendida`). Casos excepcionales: profesional no disponible, cliente o sucursal inactivos, solape por
reserva simultánea, hora inexistente por cambio de horario.

### 8.4 Gestión de visitas y llegada

SPA-DEC-116 aprueba visita con citas, varios servicios, varios profesionales, llegada, reprogramación/cancelación y cambios. Se presenta el contexto de cliente, sucursal, servicios/profesionales y horarios documentados, sin inventar un formulario original. Recepción selecciona y registra esos hechos; el motor único controla conflictos y conserva historial. La búsqueda no confirma la cita automáticamente.

**Detalle pendiente:** si visita agrupa varias citas o una cita contiene prestaciones, si sus horarios se encadenan/superponen, registro/alcance de llegada, cambios parciales y estados colectivos. Los estados Programada/Cancelada/Ausente/Atendida son propuestas anteriores por cita, no ciclo definitivo de una visita múltiple. No se deduce un estado final por completar solo uno de varios servicios. Cancelar o ausentarse no genera sesión clínica; la atención efectiva sí se documenta separadamente (O-25). Flujos excepcionales: conflicto al modificar, profesional no habilitado o prestación interrumpida; efectos exactos pendientes, sin automatismos comerciales.

## 9. Servicios y tratamientos

**Qué hace:** define qué se vende y qué procedimiento se realiza. **Estado de la sección:** «servicio = Item `Service`» es VIGENTE-ERP; el resto PROPUESTO o PENDIENTE. **Implementable hoy:** no. **Bloqueos:** BL-01, BL-09 (§29.1).

| ID | Comportamiento contractual | Estado | Evid. |
|---|---|---|---|
| SPA-SV-01 / SPA-OPS-SRV-01 / DUP-14 | Todo servicio comercializable es un Item `Service` de la empresa; Spa no crea otro catálogo comercial. Precio e impuestos los resuelve el ERP | VIGENTE-ERP (Items) + PROPUESTO | E, T |
| SPA-OPS-SRV-03 / SRV-04 | Duración (minutos enteros positivos), sucursales y condiciones de atención son **datos propios de Spa** (Items no los tiene); la cita guarda una instantánea de duración y nombre | PROPUESTO | T |
| SPA-OPS-SRV-02 | **Relación servicio ↔ procedimiento:** A perfil 1:1 por Item (`SpaServiceProfile`); B catálogo `Procedure` con relación N:M a Items (permite servicios compuestos) | PENDIENTE (`O-11`) | P |
| Servicio comercial / procedimiento / tratamiento | El servicio es lo que se vende (Items); el procedimiento es lo que se realiza sobre la persona (Spa); tratamiento operativo ejecuta el plan individual/multisesión y su ciclo aprobado (§10); O-11 solo mantiene pendiente el modelado servicio–procedimiento y visita–prestaciones | PROPUESTO | T |
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
| SPA-AT-05 / DUP-09 | El plan de tratamiento describe lo previsto para la persona; el paquete comercial representa derechos de consumo derivados de una compra. **No se vinculan automáticamente**; su relación (automática, manual o por regla) es pendiente | PROPUESTO / PENDIENTE (`O-13`) | T, P |
| SPA-AT-04 | Resultado final y cierre del tratamiento incluidos por SPA-DEC-116; cuándo se cierra, quién registra y condiciones/correcciones exactas pendientes | APROBADO resultado/cierre; detalle PENDIENTE | A + F, P |
| SPA-CARE-FAC-05 / COR-08 | La decisión (proceder, adaptar, posponer) es de un solo valor y **no ejecuta acciones automáticas** (no crea citas ni consume sesiones) | PROPUESTO | T |

### 10.1 Tratamiento individual y multisesión — APROBADO

SPA-DEC-116 aprueba plan individual, tratamiento multisesión, sesiones previstas/contratadas y cupos, ampliación, suspensión, cancelación, cierre y seguimiento de resultados. El plan recoge lo previsto para la persona; el tratamiento operativo lleva su ejecución y ciclo. El paquete vendido representa derechos comerciales; no es el plan ni la sesión realizada.

Flujo funcional: definir plan individual → organizar el tratamiento y sus sesiones previstas → documentar atenciones/evolución → ampliar, suspender, cancelar o cerrar según la situación real y las reglas de detalle por definir. La ampliación puede modificar sesiones previstas/cupos conforme a la política que falta precisar; no se deducen cantidades, equivalencias o movimientos por el nombre de la operación. Los efectos comerciales requieren contrato oficial y política aprobada; Spa no emite facturas propias. Excepciones: falta de cupo/respaldo, interrupción o cambio de profesional; no se elige política sin evidencia.

**Pendiente exclusivamente de detalle:** condiciones/permisos de cada operación; origen/destino de estados; reanudación después de suspensión no aportada; cierre con sesiones pendientes, corrección de resultados, vínculo plan–tratamiento–contrato, unidad de cupo y efecto de ampliación/cancelación sobre derechos. Se reconocen operaciones aprobadas sin inventar un autómata ni nombres de clases/permisos. Las pruebas de aceptación de §28.4 cubren esas capacidades; transiciones y errores específicos se definirán antes de implementar.

## 11. Paquetes comerciales

**Qué hace:** controla los derechos de consumo de sesiones derivados de una compra, sin confundirlos con el plan de tratamiento ni crear documentos comerciales. **Estado de la sección:** el origen del
contrato está **APROBADO** por O-15; paquetes individuales/combinados, vigencia, cupos, ampliaciones y sustituciones por SPA-DEC-116. Cálculo y políticas comerciales concretas siguen pendientes O-13. **Implementable hoy:** no. **Bloqueos:** BL-08, BL-09; BL-04 solo para funciones dependientes de anticipos (§29.1).

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

Modelo candidato: `PackageDefinition` (Item `Service` que se vende, servicios y cantidades, vigencia incluida, valores/reglas pendientes, estado), `PackageContract` (cliente, definición, fecha, factura id y línea, vigencia final con cálculo pendiente, estado), `PackageSessionLedger`. Estados del contrato candidatos: `Activo`, `Agotado`, `Vencido`, `Cancelado` (qué estados existen es pendiente). Permisos y errores en §21 y §26. El libro controla **cantidades de sesiones**, no importes: Spa no mantiene saldos monetarios (§20.4).

### 11.3 Las doce preguntas contractuales (`SPA-PQ-01`)

SPA-DEC-116 aprueba capacidades; O-15/O-25 resuelven origen y no consumo por reservar/reprogramar. Las alternativas de R2 se conservan solo cuando siguen compatibles; no se ofrece «sin vigencia» ni paquete solo individual como alcance vigente.

| Tema | Pregunta | Estado | Alternativas (sin elegir) |
|---|---|---|---|
| Venta | Qué origina un paquete | **RESUELTA por `O-15`** (creación explícita tras validar una factura autorizada) | La alternativa «evento de autorización» queda no aprobada |
| Composición | Qué servicios y cantidades | Individuales/combinados y cupos APROBADOS; cantidades/equivalencias y sustitución concreta pendientes O-13 | No se impone intercambiabilidad libre por tener paquete combinado |
| Uso | Cuándo una sesión se considera consumida | PENDIENTE (`O-13`); el consumo comercial es un movimiento independiente «cuando corresponda» (`O-25` #7) | Al realizarse / al realizarse o registrarse ausencia |
| Saldo | Cómo calcular pendientes | PENDIENTE | Derivado del libro / almacenado y actualizado bajo lock. Reservar **no consume** (`O-25` #5); si las programadas apartan saldo sigue pendiente |
| Vigencia | Cómo aplicar su vigencia | Capacidad APROBADA por SPA-DEC-116; plazo, inicio, extensión y efecto pendientes O-13 | No se elige vencimiento automático, job ni expiración de derechos |
| Reprogramación | Si afecta al saldo | **RESUELTA por `O-25` #5:** reprogramar no consume saldo | La alternativa «afecta según anticipación» queda no aprobada |
| Ausencia | Si descuenta una sesión | PENDIENTE (`O-13`) | No descuenta / descuenta |
| Cancelación | Qué ocurre con el saldo | PENDIENTE (`O-13`) | Se restituye / se restituye según anticipación |
| Devolución | Relación con documentos comerciales | PENDIENTE (`O-13`; trato de líneas `Service` en devoluciones `V-03a` parcial) | La devolución cancela el contrato y sus sesiones no consumidas / no lo afecta y se ajusta manualmente |
| Transferencia | Si cambia el beneficiario | PENDIENTE | No transferible / transferible con trazabilidad |
| Profesional | Si puede variar entre sesiones | PENDIENTE | Puede variar / fijo salvo autorización |
| Sucursal | Si se usa en varias sucursales | PENDIENTE | Cualquiera de la empresa / solo la de contratación |

### 11.4 Cupos, ampliaciones y sustituciones — APROBADO funcional

El usuario consulta paquete individual/combinado, cantidades/cupos y vigencia y puede gestionar ampliaciones y sustituciones, capacidades históricas aprobadas. El sistema conserva saldo expresado en sesiones y movimientos comerciales independientes de la atención clínica (O-25); el contrato nace explícitamente de factura/línea según O-15, sin automatismo de Ventas.

Pendientes: unidades/equivalencias por servicio, quién amplía/sustituye y cuándo, necesidad de nueva línea facturada para nuevos derechos, trazabilidad del ajuste, vigencia resultante, límites y tratamiento de saldo usado/devuelto. Ni «combinado» implica sustitución irrestricta ni «ampliación» autoriza otorgar derechos sin respaldo. Conservar la política concreta como O-13, sin declarar pendientes las capacidades.

## 12. Sesiones realizadas y consumo

**Qué hace:** documenta la atención efectivamente prestada y, cuando corresponda, su consumo comercial. **Estado de la sección:** el modelo de atención/consumo está APROBADO por O-25; formulario técnico, borrador, finalización, interrupción, corrección, productos/resultados/incidencias/fotos/evolución por SPA-DEC-116; campos [F] y efectos exactos pendientes. **Implementable hoy:** no. **Bloqueos:** BL-01, BL-05, BL-08, BL-09 (§29.1).

| ID | Comportamiento contractual | Estado | Evid. |
|---|---|---|---|
| SPA-AT-06 | La sesión realizada es un registro independiente de la cita, creado al documentar una atención efectivamente realizada; cancelar o ausentarse no la crea | APROBADO (`O-25` #3, #6) | `O-25` |
| SPA-AT-06 (complemento) | No se registra como realizada por haber llegado la fecha programada | PROPUESTO | T |
| SPA-AT-02 | La sesión registra fecha, procedimiento, productos, aparatología, zona, evolución, observaciones, reacciones posteriores, próxima sesión sugerida y firma o identificación | PLANIFICACIÓN HISTÓRICA RECUPERADA | F |
| SPA-AT-07 | Una atención realizada conserva referencias estables a la persona, al profesional (`EmployeeId`), a la sucursal y al tratamiento; estados oficiales, correcciones, anulaciones y restricciones comerciales son pendientes | PROPUESTO / PENDIENTE | T, P |
| SPA-CARE-CNS-06 | Una sesión que requiere consentimiento de procedimiento vigente no se registra como realizada sin él; si se admite excepción o consentimiento verbal es pendiente | PROPUESTO / PENDIENTE | T, P |
| SPA-CARE-CNS-05 | Una sesión interrumpida se registra con instante y motivo y no se marca como realizada por completo; su efecto sobre paquete o cobro, y si genera sesión realizada parcial, son pendientes | PROPUESTO / PENDIENTE | T, P |
| SPA-OPS-PKG-01 / G-14 | **Consumo comercial** = movimiento `Consumed` del libro del contrato (independiente de la sesión realizada). «Consumida» no es sinónimo de «realizada» | APROBADO en la independencia (`O-25` #7); definición de «consumida» PENDIENTE (`O-13`) | T, P |
| Contadores | Contratadas = Σ `Granted`; Programadas = citas activas vinculadas al contrato; Realizadas = sesiones realizadas registradas; Consumidas = Σ `Consumed` − Σ `Restored`; Pendientes = Contratadas − Consumidas | PROPUESTO (Consumidas y Pendientes PENDIENTES) | T, P |
| SPA-OPS-VTA-04 | Registrar una sesión no genera factura, cobro ni movimiento contable | PROPUESTO (compatible con `O-15` #6) | T |
| SPA-AT-03 | Las fotografías de seguimiento se vinculan a la sesión o valoración (§18) | PLANIFICACIÓN HISTÓRICA RECUPERADA | F |

### 12.1 Formulario, borrador, finalización, interrupción y corrección

**Capacidades APROBADAS por SPA-DEC-116.** El usuario prepara el formulario técnico, guarda borrador, registra productos/incidencias/resultados/evolución y profesional/registrador, documenta la atención y puede finalizar, interrumpir o corregir conforme a reglas por precisar. Las fotografías se vinculan privadamente según finalidad/consentimiento. Borrador no acredita atención prestada ni consumo.

**Cuestión concreta con O-25:** cómo representar formulario borrador e interrupción parcial sin tratar la programación como sesión realizada. No se obliga a crear una Session realizada al reservar, llegar o guardar un formulario incompleto. El detalle de si el borrador es preparación separada, versión o estado técnico queda propuesto; tampoco se decide si atención parcial crea sesión realizada parcial. O-25 conserva que cancelar/ausentarse no crea sesión clínica y que consumo comercial es movimiento independiente.

Flujos y excepciones: preparar/guardar → documentar atención efectiva → finalizar; interrumpir deja constancia del hecho; corregir conserva trazabilidad. Mínimos del borrador, autor autorizado, momento de finalización, correcciones/versiones y efecto en cita/cupo/consumo quedan pendientes, con errores/permisos exactos por definir. La capacidad no vuelve a estar pendiente por esos detalles. No se facturan ni cobran sesiones automáticamente.

## 13. Expedientes estéticos

**Qué hace:** conserva de forma longitudinal la información específica de atención de una persona. **Estado de la sección:** expediente único por cliente y empresa, antecedentes/alertas/valoraciones/historial APROBADOS por SPA-DEC-116; profesional/registrador por ADR-050. Campos [F] siguen sin contraste (`V-01`). **Implementable hoy:** no. **Bloqueos:** BL-03, BL-05, BL-09 (§29.1).

| ID | Comportamiento contractual | Estado | Evid. |
|---|---|---|---|
| SPA-FC-01 / G-02 | El expediente es el registro longitudinal, propio de Spa, de la atención de un cliente (antecedentes versionados, valoraciones, sesiones, consentimientos y fotografías); referencia al cliente, no copia su identidad. Las fichas facial y corporal no exigen dos entidades separadas | PROPUESTO | F, T |
| SPA-FC-02 | La ficha contiene número de ficha, fecha, persona, edad y contacto, profesional, motivo de consulta y, en algunas versiones, código del caso y contacto de emergencia | PROPUESTO (qué campos proceden de BP y cuáles se conservan: PENDIENTE) | F, P |
| SPA-CARE-EXP-02 | Número de ficha único por empresa. A: secuencia propia con la infraestructura de secuencias existente; B: número libre digitado. Formato y reinicio pendientes | PENDIENTE (`O-19`) | P |
| SPA-CARE-EXP-03 | Código del caso: significado no recuperado (por tratamiento, por persona o por trabajo académico) | PENDIENTE (`O-19`) | P |
| SPA-CARE-EXP-04 | Expediente único por cliente y empresa; no varios por caso o sucursal | APROBADO; O-20 resuelta | A (SPA-DEC-116) |
| SPA-FC-03 / SPA-CARE-EXP-07 | Antecedentes: en cada categoría se distingue «sin responder», «no», «sí» y texto libre; «sin responder» no equivale a «no». Qué preguntas son obligatorias, opcionales o condicionales es pendiente | PROPUESTO / PENDIENTE (`O-21`) | F, T |
| SPA-CARE-EXP-06 / SPA-FC-08 / DUP-06 | Los antecedentes se guardan como **versiones append-only**: corregir crea una versión nueva con autor, fecha y motivo; la anterior se conserva con acceso restringido y no se muestra como vigente. Si una rectificación obliga a ocultar la versión inexacta es `L-NV` | PROPUESTO | T |
| SPA-CARE-EXP-08 | Un expediente nunca se elimina; puede inactivarse. Quién lo cierra y qué impide cerrarlo es pendiente. **Conflicto con la eliminación legal:** `C-02` (§29) | PROPUESTO / BLOQUEADO | T |
| SPA-CARE-EXP-10 | El expediente es de empresa; la sucursal es atributo de cada atención. El acceso entre sucursales es pendiente | PROPUESTO / PENDIENTE (`O-02`) | T, P |
| SPA-CARE-EXP-05 | Cada valoración y sesión referencia al profesional responsable, al usuario registrador y a la sucursal | APROBADO (la distinción); campos PROPUESTOS | A |

**Alertas incluidas (SPA-DEC-116):** el expediente conserva alertas junto con antecedentes y valoración, planes/tratamientos, sesiones/evolución, medidas, fotos privadas, consentimientos e historial. No se recuperaron categorías, niveles, origen manual/automático, obligatoriedad ni bloqueos derivados: no inventarlos ni declarar pendiente la capacidad. Consulta sin duplicar identidad de BusinessPartner; privacidad y controles oficiales.

### 13.1 Datos y antecedentes recuperados que se preservan

**PLANIFICACIÓN HISTÓRICA RECUPERADA [F], sin contraste directo (R1 SPA-FC-02/03).** Datos generales: nombre, identificación, teléfono, ciudad; las fichas extensas añaden nacimiento, dirección y correo; algunas versiones, edad, código del caso y contacto de emergencia. Se leerán del propietario oficial cuando existan allí; el almacenamiento de los restantes y las instantáneas históricas sigue pendiente (O-06), sin extender BusinessPartner por este documento.

Categorías conservadas: enfermedades o condiciones relevantes; medicamentos; alergias; embarazo o lactancia cuando corresponda; cirugías y tratamientos anteriores; implantes y procedimientos en la zona; irritación, infección, heridas o reacciones recientes; salud y estilo de vida. Las variantes antiguas incluyen tabaquismo, alcohol, actividad física, depilación y ciclo menstrual. No se descartan esas preguntas por no tener confirmación literal recuperada; su versión vigente, obligatoriedad y pertinencia por procedimiento quedan en O-21/O-24. Las respuestas sin contestar se distinguen de las negativas; el control de respuesta y las versiones son propuestas técnicas (§13).

Modelo candidato: `CareRecord` (empresa, cliente, número de ficha, código del caso, estado, fecha de apertura), `MedicalHistoryVersion` (append-only), `CareRecordPersonalData` (solo si se decide, `O-06`).
Estados: `Activo` e `Inactivo`. Flujos: abrir (cliente activo con rol Customer + número de ficha + versión inicial opcional), consultar (exige permiso de Spa; `employees.view` no basta), corregir con motivo,
inactivar. La lista de clientes no muestra información clínica (§21).

## 14. Valoración facial

**Qué hace:** registra la valoración estética inicial del rostro. **Estado de la sección:** campos y valores recuperados de la ficha [F] sin contraste; obligatoriedad PENDIENTE (`O-21`). **Implementable
hoy:** no. **Bloqueos:** BL-01, BL-05, BL-09 (§29.1).

| ID | Comportamiento contractual | Estado | Evid. |
|---|---|---|---|
| SPA-FC-04 / SPA-CARE-FAC-01 | La valoración pertenece a un expediente y fija fecha de negocio (`DateOnly`), profesional responsable, usuario registrador y sucursal | PROPUESTO | F, T |
| SPA-CARE-FAC-03 | Una valoración estética **no** se rotula ni exporta como diagnóstico médico; «diagnóstico médico previo» es información declarada, no un diagnóstico de Spa | PLANIFICACIÓN HISTÓRICA RECUPERADA | F |
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
| SPA-CN-06 / SPA-CARE-CNS-01 / SPA-INV-05 / DUP-04 | Las autorizaciones de procedimiento, de fotografías de seguimiento, de publicación promocional y de uso académico son **independientes**: aceptar una no implica aceptar otra, y la finalidad se verifica contra la autorización vigente en el momento del uso | PROPUESTO | F, T |
| SPA-CN-01 | Consentimiento por procedimiento y firma en pantalla APROBADOS; información/decisión de fichas y mecanismo de evidencia por contrastar | APROBADO procedimiento/firma; campos y mecanismo pendientes | A (SPA-DEC-116) + F/T |
| SPA-CN-02 / SPA-CN-03 | Fotografías de seguimiento y publicación promocional son autorizaciones diferenciadas; la primera no autoriza publicación | PLANIFICACIÓN HISTÓRICA RECUPERADA | F |
| SPA-CARE-CNS-02 | Versiones de consentimiento incluidas; entidades `ConsentDefinition`/`ConsentRecord`, vínculo al texto presentado e inmutabilidad como diseño técnico | APROBADO versiones; modelo PROPUESTO | A (SPA-DEC-116) + T |
| SPA-CARE-CNS-03 | Un registro no se edita ni elimina; un cambio de decisión es un registro nuevo o un retiro con fecha, motivo y autor | PROPUESTO | T |
| SPA-CN-05 / SPA-CARE-CNS-04 | **Retiro:** una autorización retirada deja de habilitar usos futuros; no revierte atenciones ni documentos ya emitidos; el material ya publicado fuera del ERP no es controlable por el ERP. Efectos sobre sesiones y material publicado: pendientes. La persona puede detener una sesión | PROPUESTO / PENDIENTE | F, T, P |
| SPA-CN-04 / SPA-CARE-CNS-07 | **Uso académico:** autorización separada solo si el propietario confirma que pertenece al producto; hasta entonces no se implementa ni se asume | PENDIENTE (`O-17`) | F, P |
| SPA-CARE-CNS-08 | Quién consiente: la persona atendida; representante o menores no recuperado | PENDIENTE (`O-18`) | P |

**O-16 parcialmente resuelta:** firma en pantalla y versiones APROBADAS; no se ofrecen documento escaneado o aceptación sin firma como sustitutos de ese requisito. Falta mecanismo de captura/identidad/autenticidad y evidencia, reglas de vigencia por procedimiento y conservación legal. Las alternativas previas de hash/metadatos o referencia de versión siguen propuestas de evidencia, no aprobaciones jurídicas. La validez legal de cada firma **no está verificada** (`L-INT-05`). Texto de consentimiento y entidades `ConsentWithdrawal` y `SessionInterruption` (§24). Estados: registro `Vigente` o
`Retirado`; definición `Vigente` o `Reemplazada`. **Flujos (PROPUESTOS):** presentar la versión vigente → registrar la decisión y la evidencia → habilitar la atención; retirar; registrar la interrupción de una sesión. Permisos `spa.consents.manage` y `spa.consents.templates`; errores sin catálogo recuperado (§26).

## 18. Fotografías y archivos

**Qué hace:** conserva fotografías de seguimiento con su finalidad y autorización y controla quién las ve. **Estado de la sección:** fotografías privadas, acceso y trazabilidad APROBADOS por SPA-DEC-116; mecanismos de almacenamiento dependen de decisiones abiertas
(`O-03`, `O-04`, `O-05`). **Implementable hoy:** no. **Bloqueos:** BL-02, BL-03, BL-06, BL-09 (§29.1).

| ID | Comportamiento contractual | Estado | Evid. |
|---|---|---|---|
| SPA-CARE-FOT-01 | Una fotografía pertenece a un expediente y a una atención (sesión o valoración) y guarda quién la tomó y quién la registró | PROPUESTO | T |
| SPA-CARE-FOT-02 / SPA-SEC-INV-03 | Archivo privado APROBADO; adaptador `ICareFileStore` sobre `IFileStorage`, identificador, ruta del servidor y endpoint autorizado son mecanismos candidatos; sin URL pública | APROBADO privacidad; mecanismo PROPUESTO | A (SPA-DEC-116) + T |
| SPA-CARE-FOT-03 / SPA-SEC-INV-02 | La finalidad es una **verificación de autorización en el momento del uso**: ver en seguimiento exige el consentimiento de seguimiento vigente; publicidad, el promocional; académico, el suyo. Ninguna fotografía se sirve sin autorización, consentimiento vigente y registro de acceso | PROPUESTO | T |
| SPA-CARE-FOT-04 | Ninguna fotografía se publica, exporta ni comparte automáticamente; toda exportación es una acción explícita con permiso propio | PROPUESTO | T |
| SPA-CARE-FOT-05 | Las fotografías no aparecen en listados ni en la agenda; solo dentro del expediente y con permiso | PROPUESTO | T |
| SPA-CARE-FOT-06 / SPA-SEC-AUD-01 | Cada consulta o exportación queda en el registro de accesos (§22) | PROPUESTO | T, NV |
| SPA-CARE-FOT-07 / SPA-SEC-INV-04 | Las fotografías se desactivan lógicamente; no se invoca `DeleteAsync` sobre ellas salvo el procedimiento de eliminación legal aprobado | PROPUESTO / BLOQUEADO (`C-02`) | T |
| SPA-CARE-FOT-08 | Retiro de la autorización de seguimiento: conservar y bloquear, anonimizar o eliminar | PENDIENTE (`O-05`) | P |
| SPA-CARE-FOT-09 a FOT-10 | Metadatos del dispositivo, formatos, tamaño máximo, compresión y número de fotografías por atención: no recuperados | PENDIENTE | P |
| SPA-CARE-FOT-11 | Las condiciones comparables se registran como nota de captura | PROPUESTO | F, T |
| SPA-FO-01 | La especificación de seguridad de imágenes cubre almacenamiento, referencias, permisos de lectura, acceso a originales, exportaciones, registro de accesos, retención, revocaciones y errores | PROPUESTO | T |

**Protección técnica (mecanismos de R4 §5 no aprobados; objetivo de archivos privados sí aprobado por SPA-DEC-116):** `LocalFileStorage` sobrescribe, borra físicamente, no valida contención de ruta ni cifra, y comparte volumen con certificados y keyring; Spa no
lo modifica. Recomendación técnica no aprobada: referencias privadas, lectura autorizada y auditada, rutas del servidor, hash SHA-256 del contenido, ausencia de borrado físico, y protección/cifrado de los respaldos existentes con
clave separada solo si se aprueba ese detalle, sin desarrollar sistema nuevo (HX-03) (`SPA-SEC-INV-05`); muy recomendable antes de datos reales de salud: cifrado de aplicación por archivo con cifrado de sobre y clave maestra externa. Cifrado de campos clínicos: segunda etapa.
Cifrado de disco y publicación del directorio de archivos: `V-07` (verificación con infraestructura). Decisión `O-04`.

**Flujos (PROPUESTOS):** subir una fotografía a una atención (comprobando el consentimiento de seguimiento vigente) → consultar dentro del expediente → comparar un grupo → exportar con permiso → desactivar. Estados: `Activa` e `Inactiva`; el bloqueo por retiro de autorización es pendiente (`SPA-CARE-FOT-08`). Permisos `spa.photos.view`, `spa.photos.create` y `spa.records.export`; errores sin catálogo recuperado (§26).

## 19. Equipos e insumos

**Qué hace:** documenta los productos y equipos usados en cada atención. **Estado de la sección:** productos usados y venta de productos oficiales incluidos por SPA-DEC-116; registro documental [F], **sin descuento automático de Kardex** (HX-05). **Implementable hoy:** no. **Bloqueos:** BL-09 (§29.1).

| ID | Comportamiento contractual | Estado | Evid. |
|---|---|---|---|
| SPA-IN-01 | Productos utilizados incluidos por SPA-DEC-116; formularios registran productos/equipos usados, sin calendario de disponibilidad ni Kardex automático | APROBADO productos; campos [F] sin contraste | A + F |
| SPA-IN-02 / SPA-OPS-SRV-07 / DUP-12 | El registro de insumos es documental y no genera movimientos de inventario; cualquier movimiento exige un contrato aprobado. Alternativas sin elegir: A registro documental por sesión; B lista sugerida por procedimiento; C salida manual con los ajustes de inventario existentes; D consumo automático expresamente excluido (HX-05, N-04) | PENDIENTE (`O-12`) | T, P |
| SPA-OPS-SRV-08 / SPA-IN-03 | Aparatología: texto o referencia de identificación por procedimiento y por sesión; sin módulo de activos (N-05) | PROPUESTO | T |

## 20. Integración con Ventas, Caja y CxC

**Qué hace:** relaciona la atención y el paquete con los documentos comerciales sin duplicarlos. **Estado de la sección:** integración de servicios, facturas, CxC/cobros y venta oficial de productos APROBADA por SPA-DEC-116; frontera y origen del contrato por O-15; anticipos y prepagos PENDIENTES (`O-14`). **Implementable hoy:** no. **Bloqueos:** BL-09 y pendientes específicos de O-15; BL-04 solo para funciones dependientes de anticipos (§29.1).

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
| SPA-OPS-VTA-02 / SPA-OPS-PKG-04 | El paquete se vendería como Item `Service` (PROPUESTO); el contrato se crea explícitamente tras validar una factura autorizada, referenciando factura y línea (§11) | APROBADO solo origen/referencia (`O-15` #1, #2); venta como Item PROPUESTA | T + `O-15` |
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
Alcance: esta directriz fija la **propiedad** del proceso; **no** decide si el proceso existirá, ni su diseño, ni cierra `O-14`. Se registra con este alcance en el
[registro de decisiones](GALI-SPA-V1.1-APPROVED-DECISIONS-01.md), §7.

- `O-14` **permanece abierta**. Su posible desarrollo es un **proyecto separado** (futuro, sin documento, sin ADR y sin autorización): no se diseña aquí ni dentro de Ventas ni de CxC.
- **Único punto de integración que Spa podría necesitar** (no es contrato): disponer, si ese proceso llegara a existir, de un modo de (a) consultar el saldo de anticipo de un cliente, (b) aplicarlo como
  pago de la factura que origina un paquete y (c) tramitar su devolución, **siempre mediante el contrato del propio proceso transversal**, sin copiar su estado ni duplicar sus reglas.
- Efectos contables y fiscales de los anticipos de clientes (cuenta, traductor, tratamiento SRI): **no verificados** [NV]; el repositorio no los documenta.
- **Mientras `O-14` siga abierta, ninguna regla de este contrato depende de anticipos**; las funciones que los requieren quedan **NO AUTORIZADAS** (N-02, N-03). Qué se hace en V1.1 cuando el negocio necesite
  cobrar antes de facturar es una **decisión comercial pendiente** (§30), no una conclusión de este documento.

## 21. Seguridad y privacidad

**Qué hace:** limita quién puede ver y hacer qué con datos de salud, fotografías y consentimientos. **Estado de la sección:** lo aprobado se limita a que Empleados, Access y las asignaciones **no** dan acceso
clínico (ADR-050); permisos oficiales, aislamiento/auditoría/archivos privados incluidos por SPA-DEC-116; política Admin/sucursales, cifrado y catálogo de claves concretas siguen propuestos o pendientes. **Implementable hoy:** no. **Bloqueos:** BL-02, BL-09 (§29.1).

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

**Qué hace:** deja rastro de quién hizo o consultó qué, y define cuánto se conserva. **Estado de la sección:** auditoría/protección incluidas por SPA-DEC-116, mecanismos y conservación pendientes; la tensión entre el no borrado y la eliminación legal está
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

**Estado de la sección:** capacidades y responsive APROBADOS por SPA-DEC-115/116; composición visual exacta pendiente (`SPA-UX-01`), sin modificar `MENU_MAP.md`. **Implementable hoy:** no.

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

### 23.1 Fichas de pantallas y superficies funcionales

Estas fichas conservan los diez elementos anteriores y explicitan flujos principales/excepcionales y reglas de negocio. **La estructura de presentación es evidencia de lo documentado, no una maqueta nueva ni un diseño original certificado.** Los datos [F]/[R] son planificación histórica recuperada; los controles, estados, permisos y disposiciones [T] conservan su condición de propuesta. SPA-DEC-115/116 aprueban las capacidades indicadas; O-15/O-25 conservan sus enunciados literales (§8.1/11.1). «No recuperado» conserva el pendiente, no excluye la función. En cada ficha, errores = §26 y aceptación = escenarios candidatos, sin ejecución ni aprobación implícita.

#### Pantalla 01 — Agenda principal y cita

Procedencia: R1 SPA-AG-01/03; R2 §2.6/2.12; contrato §7/8.

| Elemento | Contenido consolidado y pendientes |
|---|---|
| Objetivo | Organizar citas y localizar disponibilidad. |
| Presentación documentada | Día/semana y filtros por sucursal/profesional APROBADOS (SPA-DEC-116); búsqueda por servicio sin profesional inicial (115). Sin columnas, colores o gestos originales recuperados. |
| Datos visibles | Cliente, servicios/profesionales de la visita, sucursal, horarios, estado y vínculo de paquete; sin clínica/fotos. Representación de cada prestación pendiente. |
| Acciones del usuario | Buscar disponibilidad, seleccionar explícitamente profesional/hora, reservar, registrar llegada, reprogramar/cambiar/cancelar, marcar ausencia y documentar atención; no confirmación automática. |
| Comportamiento del sistema | Revalida disponibilidad, conserva identidad e historial; cita y sesión realizada separadas (O-25). Reservar/reprogramar no consume. |
| Validaciones y errores | Activos y elegibles, horario, duración positiva y sin solape; hora inválida o colisión se rechazan (§26). Anticipación/contigüidad O-09. |
| Permisos | spa.agenda.view/create/update/cancel/mark-absent; catálogo candidato. employees.view no basta; sucursales O-02. |
| Estados y transiciones | Llegada/cambios incluidos; Programada → Cancelada/Ausente/Atendida son estados candidatos por cita, sin cierre de visita colectiva por inferencia. O-25 separa sesión clínica. |
| Integraciones | BP, Employee por IEmployeeDirectory, Branch, Item; contrato de paquete opcional. |
| Criterios de aceptación | PT-14 y SPA-AC-25-01/07: una reserva concurrente válida, trazabilidad y ningún consumo por reservar/reprogramar. |
| Flujos principales y excepcionales | Buscar por servicio o filtrar agenda → seleccionar profesional/hora → reservar/reprogramar; si cambia disponibilidad o hay conflicto, revalidar y no confirmar automáticamente. |
| Reglas de negocio | SPA-DEC-115/116 y O-25; motor único, selección explícita, visita múltiple, sin sesión clínica por ausencia/cancelación. |

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
| Estados y transiciones | Llegada/cambio/cancelación incluidos; estados de visita y relación con estados de cada cita/prestación pendientes. |
| Reglas de negocio | O-25 separa cita/atención/consumo; multiplicidad aprobada no impone cita única ni una sola Session por visita. |
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

## 24. Entidades y relaciones

**Estado de la sección:** todas las entidades son **candidatas**, ninguna existe y su separación definitiva en tablas y agregados requiere aprobación. Los nombres siguen los borradores; `Session` significa
exclusivamente sesión realizada (§0.3).

| Grupo | Entidades candidatas | Referencias a módulos oficiales (sin copiar sus datos) |
|---|---|---|
| Agenda y visitas | `Appointment`, `AppointmentChange` (append-only), representación de visita y prestaciones/profesionales pendiente; horarios y restricciones sin calendario de cabinas/equipos HX-01 | `BusinessPartner`, `Employee`, `Branch`, `Item` |
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
que referencia contrato y, según el caso, cita o sesión. Profesional (`EmployeeId`) y usuario registrador (usuario ERP) se guardan siempre por separado. El modelo debe admitir varios profesionales/prestaciones por visita sin imponer su agrupación técnica; borradores no equivalen a atenciones realizadas. Operación inicial/lista/reportes reutilizan esos procesos, sin otro módulo por característica.

## 25. API y contratos de aplicación

**Estado de la sección:** `SPA-API-01` [T]; **ninguna ruta, endpoint ni DTO está aprobado.** Cada operación deberá declarar permiso, scope, validación, errores, transacción, concurrencia, respuesta y pruebas, y
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

**Estado:** PROPUESTO; verificaciones técnicas pendientes antes de programar. Con visita múltiple, definir intervalos por prestación/profesional y orden de locks; la fórmula de exclusión ilustrativa no impone un solo profesional por visita. Expediente único requiere índice/clave scoped por cliente/empresa a comprobar en PostgreSQL, no O-20 pendiente.

| Tema | Regla propuesta | Evidencia |
|---|---|---|
| Citas del mismo profesional | Restricción `EXCLUDE USING gist (professional_id WITH =, tstzrange(start_utc, end_utc) WITH &&) WHERE (activa)`; reutiliza el patrón de `accounting_periods` (que usa `daterange`; **no hay precedente de `tstzrange`**, `V-05b`) | [NV] |
| Reserva y reprogramación | Advisory lock transaccional por profesional (patrón `AccountingOperationalPolicyRepository`) y token de versión (`xmin`) | [E] el patrón; el diseño [NV] |
| Consumo de sesiones | Advisory lock por contrato en una transacción; idempotencia por `(contract_id, session_id)` (`SPA-OPS-PKG-02`) | T |
| Creación del contrato | Único por factura y línea; el fallo no revierte la autorización (`O-15` #4, #5) | APROBADO el principio; mecanismo T |
| Saldo | Sin saldo negativo salvo política expresa | PENDIENTE |
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
| PT-09 | Sesiones y atención | §8, §12 | SPA-AT-02, SPA-AT-04, SPA-AT-06, SPA-AT-07, SPA-CARE-CNS-05, SPA-CARE-CNS-06 | `SPA-T-09`, `SPA-OPS-T-VTA-02` | `SPA-CARE-T-CNS-05`, `SPA-CARE-T-CNS-06` | Terminología **resuelta por `O-25`** (2026-11-18); políticas de saldo y ausencia abiertas (O-13) |
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
| SPA-AC-H-06 | SPA-DEC-116 sesiones + O-25 | Preparar borrador y finalizar/interrumpir/corregir atención sin certificar realización o consumo por mero borrador; productos/incidencias/resultados/evolución y responsables conservados; sesión parcial según regla pendiente |
| SPA-AC-H-07 | SPA-DEC-116 visitas | Gestionar varios servicios y profesionales por visita sin imponer cardinalidad técnica 1:1 |
| SPA-AC-H-08 | SPA-DEC-115/116 + O-25 | Llegada/reprogramación/cambios trazables con prevención de conflictos; no confirmar por búsqueda ni generar sesión clínica por llegada/ausencia/cancelación; concurrencia PostgreSQL futura |
| SPA-AC-H-09 | SPA-DEC-116 consentimiento | Firmar en pantalla por procedimiento con versión y autorizaciones diferenciadas; no sustituir requisito por otra alternativa ni inferir certificación jurídica |
| SPA-AC-H-10 | SPA-DEC-116 paquetes + O-15/O-25 | Gestionar individual/combinado, vigencia/cupos/ampliación/sustitución y saldo en sesiones; contrato explícito factura/línea y movimiento independiente; efectos comerciales exactos según O-13 |
| SPA-AC-H-11 | SPA-DEC-116 panel | Consultar jornada y acceder a agenda/visitas/tratamientos/seguimiento/espera/reportes sin métricas o widgets aprobados inventados |
| SPA-AC-H-12 | SPA-DEC-116 espera | Lista de espera y consultas operativas disponibles e integradas con agenda única; sin prioridad/expiración/promoción automática inventadas |
| SPA-AC-H-13 | SPA-DEC-116 reportes | Reportes básicos/seguimiento operativo con permisos y sin dashboard analítico adicional, métricas no recuperadas ni datos de otra empresa |
| SPA-AC-H-14 | SPA-DEC-116 interfaz | Recorridos de agenda/expediente/tratamiento/sesión/consentimiento utilizables en computadora/tablet/celular; sin app independiente ni offline |
| SPA-AC-H-15 | SPA-DEC-116 integración/seguridad | Servicios/productos, facturas/CxC/cobros usan módulos oficiales; Employee reutilizado, profesional/registrador distintos; permisos/aislamiento/auditoría/archivos privados sin motores paralelos |
| SPA-AC-H-16 | SPA-DEC-116 exclusiones HX-01..10 | Ninguna fase añade disponibilidad cabinas/equipos, recordatorios automáticos, sistema nuevo de backups, reservas públicas, Kardex automático de aplicados, nómina/RRHH completo, app móvil independiente, offline, diagnóstico automático/IA, dashboard analítico adicional o contabilidad paralela |

## 29. Dependencias bloqueantes

### 29.1 Bloqueos técnicos, legales y de autorización

| ID | Pendiente concreto | Afecta | Condición de resolución |
|---|---|---|---|
| BL-01 | Employee/directorio todavía no implementados; firmas y elegibilidad histórica por verificar | Profesionales, disponibilidad, atenciones | EMP-E6b bajo plan/autorización propios; no crear Employee Spa |
| BL-02 | Admin, sucursales, controles/permisos exactos, registro de lecturas y cifrado | Clínica, archivos y consultas | O-01..04; revisión expresa si toca LOCKED. Seguridad funcional incluida, mecanismo no aprobado |
| BL-03 | Conservación/no borrado, retiro y posible eliminación legal | Antecedentes, sesiones, consentimientos y fotos | O-05/O-07; no elegir excepción o plazo sin respaldo |
| BL-04 | Anticipos/prepagos transversales sin diseño | Solo operaciones que dependan de anticipos | O-14 ABIERTA; origen de contrato ya resuelto O-15 |
| BL-05 | Contraste de formularios/versiones y detalles anteriores no recuperados | Campos/validaciones y reglas concretas | O-24/V-01 y evidencia histórica SPA-DEC-001..114. **No bloquea aprobación general de alcance: O-23 cerrada en inclusión** |
| BL-06 | Validez jurídica aplicable, firma/menores/plazos e imágenes | Protección de datos y evidencia | O-07/O-16/O-18, V-06/V-09; firma en pantalla ya incluida, no se sustituye por otra alternativa |
| BL-07 | Política/escala numérica corporal | Medidas y comparación | O-22/V-04, revisión arquitectónica formal |
| BL-08 | Políticas exactas de cupos/consumo/ampliación/sustitución/vigencia/devolución | Tratamientos, paquetes, sesiones | O-13 y pendientes O-15; capacidades aprobadas, efectos no inventados |
| BL-09 | Aprobación técnica integral de contrato/ADR y autorización de fases | Toda implementación | ADR-051 PROPOSED; plan preparado sin fases autorizadas; no reabrir inclusión funcional |
| BL-10 | Representación visita–cita múltiple, borrador/interrupción y cálculo de restricciones | Agenda, visitas, sesiones y concurrencia | Resolver detalle con ambas evidencias SPA-DEC-115/116 y O-25; V-05 y pruebas PostgreSQL futuras |

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
| Seguridad, auditoría y conservación | Objetivos incluidos; mecanismos/ley pendientes | No | BL-02/03/06/09 |

## 30. Estado de decisiones y evidencia pendiente

### 30.1 Aprobadas e instrucciones del propietario

| Fuente | Estado y alcance | Evidencia |
|---|---|---|
| SPA-DEC-115 | APROBADA histórica: búsqueda por servicio/fecha/sucursal sin profesional previo; selección explícita y motor único, sin confirmar automáticamente | [REG §9](GALI-SPA-V1.1-APPROVED-DECISIONS-01.md) |
| SPA-DEC-116 | APROBADA histórica: once áreas, siete agrupaciones y diez exclusiones; orden de fases preliminar | [REG §10](GALI-SPA-V1.1-APPROVED-DECISIONS-01.md) |
| O-25 | APROBADA: cita distinta de sesión realizada y consumo independiente; sin consumir por reservar/reprogramar | REG §1, literal preservado §8.1 |
| O-15 | APROBADA: creación explícita desde factura/línea, fuera de autorización Ventas; sin duplicados/documentos propios | REG §2, literal preservado §11.1 |
| DIR-SSOT-01 | INSTRUCCIÓN DEL PROPIETARIO: Anticipos como proceso transversal, no diseño financiero ni cierre O-14 | REG §7; contrato §20.4 |
| ADR-050 | Decisiones aplicables de Employee reutilizable, profesional/registrador y permisos; directorio aún por implementar | REG §8 y ADR-050; sin modificación de Empleados |

Las fechas históricas exactas de 115/116 no fueron aportadas. Recuperación registrada 2026-11-18; aprobación anterior a planificación de Empleados. No inventar actas/fechas ni SPA-DEC-001..114.

### 30.2 Cuestiones O-xx: resueltas, parciales y abiertas

Se conservan los identificadores originales; no se cuenta toda cuestión como abierta después de recuperar la evidencia.

| O | Estado vigente | Detalle aún pendiente o cierre respaldado |
|---|---|---|
| O-01 | ABIERTA | Admin y mecanismo de acceso clínico; no cambiar bypass LOCKED sin revisión |
| O-02 | ABIERTA | Visibilidad entre sucursales; expediente único por empresa no implica acceso irrestricto |
| O-03 | ABIERTA en mecanismo | Auditoría incluida; modo de escritura, errores y retención de lecturas pendientes |
| O-04 | ABIERTA en mecanismo | Archivos privados incluidos; cifrado/claves/custodia y uso protegido de backups existentes por definir; sistema nuevo de respaldo excluido |
| O-05 | ABIERTA | Conservación/eliminación legal y excepción a no borrado; sin plazos inventados |
| O-06 | ABIERTA | Datos propios de edad/nacimiento/emergencia; no duplicar BP |
| O-07 | ABIERTA | Validación jurídica aplicable, no sustituida por aprobación funcional |
| O-08 | RESUELTA para recursos físicos | Control de disponibilidad de cabinas/equipos EXCLUIDO por SPA-DEC-116 HX-01; no añadirlo como alternativa pendiente |
| O-09 | PARCIALMENTE RESUELTA | Descansos/bloqueos/preparación/traslados y conflictos incluidos; valores, cálculo/representación, tolerancias/anticipación y confirmación manual pendientes; búsqueda nunca confirma automáticamente; recordatorios excluidos |
| O-10 | ABIERTA | Atención sin cita previa; llegada aprobada no equivale a aprobar walk-in |
| O-11 | PARCIALMENTE RESUELTA | Varios servicios/profesionales por visita APROBADOS; relación visita–cita–prestaciones y servicio–procedimiento pendientes |
| O-12 | PARCIALMENTE RESUELTA | Registro técnico de productos y venta oficial incluidos; descuento Kardex automático EXCLUIDO; campos/cantidades o ajustes manuales requieren regla propia |
| O-13 | PARCIALMENTE RESUELTA | Paquetes individuales/combinados, vigencia/cupos/ampliaciones/sustituciones incluidos; reglas concretas de consumo, equivalencias, límites, vigencia, cancelación/ausencia, transferencia y devolución pendientes; Venta O-15 y Reprogramación O-25 resueltas |
| O-14 | ABIERTA | Anticipos/prepagos: proceso transversal independiente, sin diseño autorizado |
| O-15 | APROBADA | Origen y frontera del contrato preservados; significado de autorizada/SRI, NC/crédito y lista de pendientes fuera de esa aprobación |
| O-16 | PARCIALMENTE RESUELTA | Por procedimiento, firma en pantalla, versiones/autorizaciones específicas APROBADAS; captura/autenticidad, evidencia, vigencias y conservación jurídica pendientes |
| O-17 | ABIERTA | Alcance académico concreto no recuperado; no asumir inclusión por «autorizaciones específicas» |
| O-18 | ABIERTA | Menores/representantes, verificación y evidencia jurídica |
| O-19 | ABIERTA | Numeración y significado de código del caso; no permite varios expedientes por empresa |
| O-20 | RESUELTA | Expediente único por cliente y empresa, SPA-DEC-116; exclusión de duplicados por caso/sucursal |
| O-21 | ABIERTA en datos | Obligatoriedad/campos/validaciones de formularios, no inclusión de valoración o alertas |
| O-22 | ABIERTA en detalle | Escalas/frecuencia/control de medidas y clasificación numérica; no inclusión de mediciones |
| O-23 | CERRADA EN INCLUSIÓN FUNCIONAL GENERAL | SPA-DEC-116 recuperada con declaración expresa de alcance y exclusiones; no cierra técnica/ley/implementación. Fichas separadas O-24/V-01, detalles históricos restantes §30.3 |
| O-24 | ABIERTA | Versiones vigentes y contraste directo de fichas originales V-01; no se cierra por recuperación de 115/116 |
| O-25 | APROBADA | Reserva/cita/atención/consumo y trazabilidad preservados; no revoca multiplicidad de visita aprobada históricamente |

### 30.3 Evidencia restante y tensiones delimitadas

- **Historial:** SPA-DEC-115/116 recuperadas según contenido aportado en REG; no se recuperaron los textos SPA-DEC-001..114. Solo los detalles que dependan de ellos se mantienen por verificar; no se invalida alcance general.
- **Formularios:** O-24/V-01 y obligatoriedad O-21; no se presume equivalencia de versiones ni inspección de originales.
- **Técnica y legal:** directorio real V-02, devolución Service completa V-03a, precisión V-04, tiempo/concurrencia V-05, asesoría V-06/vigencia V-09 e infraestructura V-07/08 siguen según evidencia disponible.
- **Tensión concreta visita/cita:** multiplicidad por visita aprobada; representación de varias citas o prestaciones dentro de una cita no decidida. O-25 conserva cita distinta de atención y no consumo por programar.
- **Tensión concreta borrador/interrupción:** capacidades aprobadas; falta representar preparación no realizada y atención parcial, sus transiciones/efectos en cita/cupo. No se crea sesión clínica por ausencia/cancelación ni se inventa consumo.
- **Comercial:** origen O-15 intacto; detalle de autorizada/SRI/crédito/NC, saldos/ajustes y efectos de ampliación/sustitución pendientes. Anticipos O-14 sigue separado.
- **Sin revocación inferida:** ninguna evidencia aportada revoca expresamente O-15/O-25 ni las capacidades históricas. Las fronteras de procesos oficiales, exclusiones y detalle pendiente se conservan conjuntamente.
- **Aprobaciones técnicas y ejecución:** contrato central sin aprobación técnica integral, ADR-051 PROPOSED / NO APROBADO y plan sin fase autorizada. Revisión del propietario pendiente; no comenzar desarrollo.

## Anexo A. Responsabilidad de cada documento

| Documento | Responsabilidad | Estado |
|---|---|---|
| **Contrato central** (este documento) | Comportamiento funcional y técnico de Spa V1.1: qué se desarrollará, cómo funciona, qué está aprobado, qué no y qué queda excluido | Candidato a SSOT funcional; EN REVISIÓN |
| **[ADR-051](ADR-051-gali-spa-v1.1-architecture.md)** | Arquitectura candidata, fronteras y condiciones | PROPOSED / NO APROBADO |
| **[Plan](GALI-SPA-V1.1-IMPLEMENTATION-PLAN-01.md)** | Fases, dependencias, pruebas y cierre | PROPUESTO; ninguna fase autorizada |
| **Registro de decisiones** ([REG](GALI-SPA-V1.1-APPROVED-DECISIONS-01.md)) | Evidencia de las aprobaciones del propietario | Vigente; las aprobaciones nuevas (incluida `DIR-SSOT-01`) se registran allí o en su sucesor |
| R1 [RECOVERY-01](GALI-SPA-V1.1-RECOVERY-01.md) | Respaldo de reconstrucción/códigos GS-*/V01 anterior a recuperar SPA-DEC-115/116 en REG | Referencia **no normativa** |
| R2 [OPERATIONS-SPEC](GALI-SPA-V1.1-OPERATIONS-SPEC-DRAFT-01.md), R3 [CARE-PRIVACY](GALI-SPA-V1.1-CARE-PRIVACY-SPEC-DRAFT-01.md), R4 [SECURITY-COMPLIANCE](GALI-SPA-V1.1-SECURITY-COMPLIANCE-DRAFT-01.md) | Análisis, alternativas y evidencia técnica y legal que respaldan este contrato | Referencia **no normativa** (el texto funcional vive aquí; R4 §7 conserva el detalle legal) |
| [MATRIZ](GALI-SPA-V1.1-CONTRACT-CONSOLIDATION-MATRIX-01.md) | Trazabilidad de los 295 registros y conflictos `C-xx` | Referencia **no normativa** |
| [NORM](GALI-SPA-V1.1-CONTRACT-NORMALIZATION-01.md) | Redacciones canónicas, glosario, catálogo de permisos, verificaciones `V-xx` cerradas | Referencia **no normativa**; sus contenidos funcionales y verificaciones están integrados aquí según su estado |

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
| B-09 | R3 `SPA-CARE-CNS-05` (sesión interrumpida «no realizada por completo») frente a `O-25` #3 | Interrupción/borrador/finalización/corrección incluidos por SPA-DEC-116; representación de preparación o atención parcial y sus efectos pendientes junto con O-25 | — |
| B-10 | R3 `SPA-CARE-EXP-08` («nunca se elimina») frente a R4 `SPA-SEC-RET-01` (procedimiento de eliminación) | No se resuelve en silencio: BL-03 | 1 vs legal |
| B-11 | Los borradores dicen «ninguna regla está aprobada» aunque `O-25` y `O-15` lo estén después | Las reglas afectadas se marcan con su aprobación (§8, §11, §12, §20); los cuerpos históricos se preservan y sus entradas remiten al contrato | 1 |
| B-12 | R1 §16 afirma que las exclusiones de V1.1 no están recuperadas | SPA-DEC-116 recuperó diez exclusiones expresas HX-01..10; las alternativas previas incompatibles dejan de ser candidatas | — |
| B-13 | La primera versión clasificaba como «excluidas» la creación automática del contrato y la sesión única desde la reserva | Reclasificadas como no autorizadas (N-10, N-11): `O-15` y `O-25` dicen «no se aprueba», no «excluido» | 1 |
| B-14 | La primera versión hablaba de «once» preguntas de paquetes pendientes | En la revisión previa eran diez tras O-15/O-25; ahora SPA-DEC-116 resuelve componentes adicionales de composición/vigencia/cupos; solo detalles de §11/30 pendientes | 1 |
| B-15 | Estados [F]/[R] se confundían con propuestas técnicas, y faltaban categorías de antecedentes en el contrato | Planificación histórica distinguida de [T], datos restaurados §13.1 y fichas §23.1 sin diseños inventados | Consolidación, no aprobación |
| B-16 | Contrato/estados decían ADR/plan inexistentes e índice pendiente | ADR-051 PROPOSED, plan sin autorización e índice/estado sincronizados; antecedentes con entrada no normativa | Documental |
| B-17 | BL-04 atribuía a O-14 el origen del contrato ya decidido por O-15 | Bloqueo limitado a funciones dependientes de anticipos; O-15 no se reabre | O-15 + DIR |
| B-18 | En §20.2 se rotulaba toda la fila como aprobada, incluyendo venta del paquete como Item Service | Aprobación limitada a origen y referencia factura/línea; modelado de venta sigue propuesto | Alcance exacto O-15 |

Nuevas diferencias resueltas por evidencia del propietario: la inclusión general antes pendiente queda aprobada (O-23 cerrada en ese componente); O-08 resuelve recursos físicos excluidos, O-20 expediente único, O-11 multiplicidad funcional, O-13 capacidades de paquetes y O-16 firma/versiones. Solo el detalle concreto permanece pendiente. SPA-DEC-115 se incorpora al motor único, sin crear agenda nueva; SPA-DEC-116 restaura panel/espera/reportes y exclusiones sin reabrir decisiones posteriores.

## Anexo C. Mapa de origen por identificador

Sección principal del contrato donde se consolida cada regla, invariante, riesgo y decisión de los documentos previos. Las pruebas figuran en §28.1 y los casos de aceptación nuevos en §28.3.

| Sección | Identificadores consolidados |
|---|---|
| §1, §2 | `SPA-AL-01` |
| §1, §6 | `SPA-AL-02` |
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

Lectura actual: donde una fuente decía sesión programada se aplica O-25 (cita); ninguna ausencia crea sesión clínica. O-15 fija creación explícita y transacción separada. Cualquier criterio histórico de no eliminación está condicionado a O-05/C-02; no lo resuelve este catálogo. Los 13 casos SPA-AC de §28.3 son adicionales.

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

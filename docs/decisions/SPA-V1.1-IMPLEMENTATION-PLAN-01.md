# SPA V1.1 — Plan de implementación por fases

**ADR-051 ACCEPTED / APROBADO (2026-11-19, REG §25) — NO IMPLEMENTADO — NINGUNA FASE AUTORIZADA.** Fecha: 2026-11-18. Alcance funcional aprobado por SPA-DEC-115/116; este plan ordena trabajo futuro; **no autoriza comenzar ninguna fase**, ni siquiera la preparación de solo lectura, por existir. No autoriza implementar Empleados.

Arquitectura y justificación: [ADR-051](ADR-051-spa-v1.1-architecture.md). Comportamiento, pantallas y criterios: [contrato](SPA-V1.1-CONTRACT-SPEC-01.md). Aprobaciones literales: [registro](SPA-V1.1-APPROVED-DECISIONS-01.md). Estado: [STATUS](../../STATUS.md); cola: [WORK_QUEUE](../agent/WORK_QUEUE.md). Este plan no es una SSOT ni replica el contrato.

Patrón contrastado: el plan oficial de Pedidos se llama [PEDIDOS-VENTA-V1-IMPLEMENTATION-PLAN-01](../agent/PEDIDOS-VENTA-V1-IMPLEMENTATION-PLAN-01.md) y está en `docs/agent/`. Este archivo de Spa queda en `docs/decisions/` por ruta expresa del propietario.

## 1. Reglas para una futura autorización

1. Ejecutar solo la fase autorizada expresamente y su lista exacta revisada de rutas existentes, nuevas, protegidas y candidatas pendientes. Los módulos citados aquí no son allowlists de archivos.
2. Resolver decisiones de negocio antes de programar; una función histórica se conserva aunque falte acta literal. O-23 está cerrada en inclusión funcional general por SPA-DEC-116; O-24/V-01 y detalles técnicos/legales siguen pendientes. Una dependencia técnica pendiente no cambia un enunciado ya aprobado.
3. Contrato aprobado y ADR aceptado son condiciones previas al código, no permisos de ejecución. La preparación de evidencia puede autorizarse separadamente como lectura/documentación sin aceptar el diseño.
4. No extender módulos FROZEN/CLOSED ni seguridad LOCKED por inferencia. Las autorizaciones de Pedidos o Empleados no autorizan cambios de Spa.
5. Respetar instalación greenfield consolidada; antes de cualquier cambio de esquema verificar estrategia oficial, lista exacta y conservación de objetos SQL. No crear migraciones ni tocar bases reales en esta tarea.
6. Aplicar [flujo por fase](../architecture/enforcement.md#flujo-obligatorio-del-agente-por-fase): revisión focal KEEP/BUG REAL/DEBT/FALTA, pruebas pertinentes, gate PASS del propietario, diff/documentación; staging y commit del propietario. Solo entonces puede declararse CLOSED; chat nuevo por fase.

## 2. Secuencia histórica preliminar y dependencias reales

Fuente: SPA-DEC-116 recuperada por el propietario; orden preliminar transcrito en [REG §10.2](SPA-V1.1-APPROVED-DECISIONS-01.md). Se preservan F0 arquitectura/contratos; F1 Empleados/base segura; F2 expediente/valoraciones; F3 tratamientos/sesiones; F4 consentimientos/fotos; F5 agenda/espera; F6 integración comercial; F7 operación/reportes; F8 pruebas integrales. La fuente permite adaptar dependencias; no autoriza ninguna fase.

**Cada fila: PENDING / NO AUTORIZADA.** Toda prueba aquí indicada es futura, no ejecutada durante esta corrección documental. La lista exacta de archivos y autorización expresa siguen siendo requisito individual.

| Fase | Objetivo | Alcance futuro | Dependencias reales | Detalle que debe resolverse | Pruebas futuras | Criterio de salida | Autorización |
|---|---|---|---|---|---|---|---|
| SPA-F0 — Arquitectura y contratos | Cerrar diseño técnico coherente con alcance aprobado | Verificaciones/scopes, directorio, esquema/vínculos técnicos de visita–citas según SPA-ARQ-01 y borrador/sesión parcial, servicio/procedimiento, reglas de restricciones, lectura de factura, privacidad/archivos/precisión; listas exactas ; diseño técnico de disponibilidad conforme SPA-ARQ-04/contrato §7.2 y ADR AT-04/05, sin elegir de nuevo obligaciones funcionales| Autorización de preparación de lectura/documentación; decisiones históricas y contrato disponibles ; verificar contrato del directorio oficial de Empleados y fuente de horarios/asignaciones/elegibilidad sin ampliar capacidades| O-01..07/09..19/21/22/24 según impacto; O-23 no se reabre. V-01..09 según evidencia; ninguna duración o política inventada ; intervalos recurrentes, algoritmos, configuración de preparación/traslados, contratos API, UTC/horas ambiguas y mecanismos PostgreSQL pendientes; SPA-ARQ-06/contrato §21 objetivos aprobados: C-01 requiere autorización especial LOCKED, C-02 procedimiento/validación legal; O-02..04 mecanismos pendientes; SPA-ARQ-07 contenido aprobado; mecanismos de valoración/borradores/versiones/consentimiento/firma y fotos pendientes; V-01/04/06/07/08/09 en aspectos abiertos, sin elegir precisión/esquema; SPA-ARQ-08/contrato §4.2 fronteras aprobadas; verificar contratos autorizados BP/Empleados/Items/comerciales, scopes y propietarios sin ampliar módulos ni aprobar APIs/mecanismos; sin preparación de código; SPA-TEC-01/02/03 direcciones/reglas parciales aprobadas; seleccionar detalle físico, revisar C-01 LOCKED con autorización especial, C-02/ley/fiscal, lecturas oficiales, Empleados, claves/recuperación y precisión definitiva; no considerar numeric(10,2) definitivo | Guardrail documental, enlaces/anclas, trazabilidad de once áreas/siete agrupaciones/diez exclusiones ; trazabilidad de SPA-AC-ARQ04-01..12 (especificaciones futuras), separación funcional/técnica| Contrato y ADR técnicamente revisados, decisiones concretas registradas y rutas por fase preparadas; no habilita código por sí sola; Selección técnica única recomendada ADR §5.10–5.15 y REG §23 disponible; falta aprobación conjunta y gates específicos C-01/precisión/ley/lecturas/operación. No declarar cierre técnico integral por esta documentación | PENDING / NO AUTORIZADA |
| SPA-F1 — Empleados y base segura | Proveer identidad profesional y controles antes de exponer clínica | Consumir directorio oficial; profesional/registrador separados; implementar en Spa políticas autorizadas de acceso/scope/auditoría/lecturas y base de archivos privados | F0; aceptación técnica y autorización de código; **EMP-E6b implementado/probado bajo plan/autorización separada de Empleados**; protección y auditoría necesarias disponibles y validadas antes de datos clínicos reales; revisión especial C-01, política intersucursal, lecturas confiables y archivos/claves bajo diseño aprobado; sin modificar infraestructura FROZEN; SPA-ARQ-07 §21.4: sin datos clínicos reales hasta seguridad/cumplimiento necesarios resueltos y verificados, permisos/auditoría/aislamiento antes de valoraciones, fotos y consentimientos; SPA-ARQ-08 §4.2 reglas 2/7: IEmployeeDirectory pendiente bajo ADR-050/plan propio, sin Employee/usuarios/autenticación/permisos generales paralelos; C-01/C-02 y LOCKED/FROZEN intactos; TEC-02.A backend/recurso pese a bypass; excepción por otra persona facultada sin autoautorización temporal/delimitada/auditada; TEC-01.6 durable antes de entrega y TEC-01.7 adaptador privado. Revisión especial LOCKED y mecanismo pendientes; Kernel NO AUTORIZADO; Recomendación F0: clasificación clínica sin bypass y guard/concesiones (§5.13), auditoría síncrona durable y adaptador cifrado (§5.14); ejecución C-01 pendiente de autorización especial; EMP-E6b verificado previamente | Mecanismos Admin/sucursales O-01/02 tras SPA-ARQ-06, registro O-03, almacenamiento/claves O-04/05/07; directorio V-02 y archivos V-07/08. Sin nuevo sistema de backups | PT-02..06/11/12 aplicables y H-15; PostgreSQL para scope/locks; almacenamiento aislado; perfiles no heredan acceso clínico | Lecturas/escrituras sensibles protegidas y trazables conforme a política; sin identidad laboral ni autorizador paralelos; bloqueos externos identificados | PENDING / NO AUTORIZADA |
| SPA-F2 — Expediente y valoraciones | Dar soporte al expediente único e historia/valoraciones | Un expediente cliente/empresa, antecedentes/alertas/historial, valoración facial/corporal y comparación de medidas; referencias de plan/sesión/fotos/consentimiento completadas después | F1, BP oficial; sin datos sensibles antes de controles. Precisión corporal aprobada antes de persistir medidas | O-06/19/21/22/24, V-01/04; O-20 resuelta, sin volver a elegir uno o varios expedientes; políticas de alerta aún no aportadas; Modelo CareRecord/AssessmentRevision seleccionado como recomendación ADR §5.11; resolución/escala corporal sigue V-04 formal, C-02 y mínimos de fichas no resueltos por elegir tablas | PT-01/07/08/12/13 y H-03; unicidad concurrente PostgreSQL, identidad no copiada, no medida ≠ cero y comparación correcta; SPA-AC-ARQ07-01..05/09/10 futuros | Expediente único, datos/versiones según reglas contrastadas, sin alertas/obligatoriedad inventadas ni fuga de empresa; SPA-ARQ-06: expediente único cliente/empresa con sucursal por registro, acceso clínico autorizado/contextual, versiones sustituidas restringidas/no vigentes; no ampliar scope BP; SPA-ARQ-07 §14.1/15.1/16/17.1: borradores sin atención/consumo, medidas opcionales Inicial/Control/Final y vacíos distintos de cero, comparación sin inventar valores, correcciones trazables; precisión V-04 y obligatoriedad específica resueltas antes de persistencia aplicable; TEC-01.2 versiones/borradores y TEC-01.9 decimal? / numeric(10,2) candidato preferente sujeto a revisión formal; datos ausentes ≠ cero, C-02 no cerrado | PENDING / NO AUTORIZADA |
| SPA-F3 — Tratamientos y sesiones | Gestionar plan/ejecución multisesión y registro técnico individual | Configuración de servicios/procedimientos necesaria; plan individual, cupos/sesiones previstas, ampliación/suspensión/cancelación/cierre; formularios/borradores, finalización/interrupción/corrección y resultados/productos/incidencias/evolución | F1/F2; configuración de servicios incluida antes de atenciones. Subalcance de consentimiento F4 debe preceder habilitación de atención final si es requisito; vínculo cita se completa en F5; SPA-ARQ-05/contrato §11 separa clínica de consumo; parcial requiere resolución comercial autorizada sin automatismo; SPA-ARQ-07 §17.1: atención que requiere consentimiento depende de documento previo/evidencia válida aplicable F4; borrador de valoración no es sesión finalizada, sin consumo; SPA-ARQ-08 §4.2 reglas 3/6: Items Service/precios/impuestos oficiales y configuración operativa SPA, sin ampliar Items/Pricing; productos documentales sin Kardex automático; TEC-03 §10.2 plan/prestaciones/contratos/documentos explícitos, plan previo y contratación parcial; TEC-02.B cierre administrativo conserva Interrumpida/parcial y TEC-02.C cupo comercial retenido, sin falsa Finalizada ni consumo automático | SPA-ARQ-03/contrato §12 resuelve ciclo clínico y parcial; quedan persistencia/concurrencia/finalización, aplicación técnica del cierre administrativo aprobado TEC-02.B (§8.7) y detalles restantes O-13 bajo §11 SPA-ARQ-05. O-09/10/11/16 según contexto; no decidir walk-in ni excepciones de consentimiento | PT-02/07/09/12/15, AC-25 y H-04/05/06 más SPA-AC-ARQ03-01..08 (futuras): borrador sin atención/consumo; inicio contextual con responsables separados; finalización explícita; interrupción/parcial; corrección/versiones bajo C-02; evolución diferenciada; sin efectos comerciales/inventario por finalizar | Ciclo clínico aprobado documentado y comprobado conforme §12, sin habilitar atención sin consentimiento/cita cuando se requieran; parcial conservada y corrección autorizada trazable; cierre administrativo según TEC-02.B sin falsa finalización clínica, ni consumo o gratuidad; Modelo recomendado TreatmentPlan/PlanProvision, ServiceProvision y ClinicalSession/ClinicalRevision (§5.11); preservar cierre administrativo/consumo independientes; servicio-perfil/procedimiento N:M operativo sin catálogo comercial duplicado | PENDING / NO AUTORIZADA |
| SPA-F4 — Consentimientos y fotografías | Conservar firma/evidencia privada y finalidades diferenciadas | Consentimiento por procedimiento, firma en pantalla, versiones, retiro según ley; fotos de valoración/sesión y comparación privada con acceso trazable | F1/F2; consentimiento y fotos de valoración pueden preceder F3; fotos de sesión requieren atención documentada de F3. Base de archivo de F1 comprobada; F3 respeta los consentimientos aplicables también para interrupción/parcial/corrección según SPA-ARQ-03, sin excepción nueva ni cierre de C-02; SPA-ARQ-06 §21 exige archivos privados autorizados, cifrado adecuado/claves/aislamiento y trazabilidad de fotos/consentimientos antes de datos reales; almacenamiento/restauración/backups y gestión legal C-02 pendientes sin sistema nuevo; Formato privado/cifrado por objeto y commit de acceso antes de descarga recomendados ADR §5.14; firma/menores/finalidad/conservación/KEK/TLS/restore y evidencia externa obligatorios antes de datos reales | O-16 captura/autenticidad/ley/conservación, no existencia de firma/versiones; O-17/18, O-04/05/07 y V-06..09; sin sistema nuevo de respaldos; TEC-01.7 protección/cifrado/claves/restauración por validar; consentimiento/firma/menores/fotografías y conservación jurídica pendientes, sin nuevo sistema de backups | PT-04/05/06/10/11/12 y H-09; storage aislado, autorización/finalidad/cifrado según decisión, retiro/errores; no URL pública; SPA-AC-ARQ07-06..10 futuros | Firma en pantalla vinculada a procedimiento/versión, evidencia privada trazable y reglas legales aplicables comprobadas; no certificación legal inferida; SPA-ARQ-07 §17.1/18: procedimiento/seguimiento/promoción independientes, sin académico aprobado; evidencia de versión/decisión/firma/identidad y fotos privadas con metadatos/finalidad, correcciones trazables; autenticidad/ley/vigencia/menores y almacenamiento pendientes antes de habilitación aplicable | PENDING / NO AUTORIZADA |
| SPA-F5 — Agenda, visitas y lista de espera | Coordinar disponibilidad, selección y visitas múltiples | Motor único de horarios/descansos/bloqueos/preparación/traslados/conflictos; día/semana/sucursal/profesional, búsqueda por servicio sin profesional previo, selección explícita, varios servicios/profesionales, llegada/reprogramación/cancelación/cambios; lista de espera | F1 y servicios de F3; modelos de F0; F3/F4 para enlaces de atención/consentimiento, sin crear dependencia de reserva que consuma paquete ; contrato oficial de Empleados verificado para horarios/asignaciones/elegibilidad, sin modificaciones inferidas; configuración temporal oficial existente; validación de vigencia y cupo de programación por servicio del contrato (SPA-ARQ-05 §11), sin Consumed; concurrencia y varios bloques sin duplicar prestación | O-09 cálculo/representación/valores, O-10, O-11 solo vínculos técnicos del modelo visita–citas SPA-ARQ-01 y servicio–procedimiento; política exacta de espera/orden/duplicados/transiciones. O-08 resuelta: sin calendario cabinas/equipos; cierre administrativo TEC-02.B aprobado hacia Atendida con atención real, clínica parcial preservada; mecanismo detallado pendiente, sin sexto estado ; SPA-ARQ-04 fija obligaciones §7.2; algoritmos, recurrencias, locks/SQL, traslados/excepciones y valores pendientes, sin márgenes predeterminados| PT-14, AC-25-01..07 aplicables y H-01/02/07/08/12; PostgreSQL con varios profesionales/orden de locks, conflictos, horario inválido y disponibilidad cambiante ; SPA-AC-ARQ04-01..12: horarios/duración, indisponibilidad, preparación, traslado, consecutivas, multiprofesional, búsqueda, concurrencia PostgreSQL, fallo de reprogramación, UTC e interrupción sin liberar tiempo; Verificar selección F0: lock profesional sin sucursal ordenado, revalidación de vecinos/traslados, ocupación efectiva distinta de intervalo reservado, exclusión GiST y reprogramación rollback (§5.11); PostgreSQL real futuro autorizado | Búsqueda/reserva usan mismo motor, selección explícita e historial; SPA-ARQ-01: visita del mismo cliente/ocasión/sucursal agrupa citas/bloques profesional–intervalo, servicio multiprofesional sin duplicar prestación comercial y cancelar una cita no cancela las demás. Sesiones realizadas independientes, reserva/reprogramación sin consumo; sin reservas públicas ; cumplir §7.2, sin compromisos concurrentes incompatibles, fallo conserva original e interrupción no libera automáticamente agenda; TEC-01.1/01.3/01.4 prestación/bloques sin duplicación, recurrencia/excepciones/UTC/[inicio, fin), transacción/lock profesional sin sucursal/orden multiprofesional/reprogramación atómica. TEC-02.B cierre administrativo libera tiempo futuro sin alterar original; TEC-02.C conserva cupo hasta resolución | PENDING / NO AUTORIZADA |
| SPA-F6 — Integración comercial | Relacionar paquetes y atención con procesos comerciales oficiales | Paquetes individuales/combinados, vigencia/cupos/ampliación/sustitución, contratos factura/línea, saldo/movimientos de sesiones; servicios y venta oficial de productos, lectura de CxC/cobros/saldos/NC | F1/F3/F5 y F4 según consentimiento; consulta pública de Ventas verificada; dependencias de operación oficial de Caja/CxC sin cambios implícitos; registros F3 completados/parciales no determinan automáticamente consumo y correcciones no añaden atención o consumo (SPA-ARQ-03/O-25); políticas O-13 como prerrequisito de efectos comerciales; cupos temporales F5 y movimientos independientes de F3; ampliaciones respaldadas por documentos oficiales, NC/anulación solo Ventas; contratos autorizados factura/línea y consultas Caja/CxC verificados sin extender propietarios (SPA-ARQ-08); Firmas mínimas SalesEntitlementReader y ReceivableCollectionReader candidatas ADR §5.12; autorización propia de propietarios protegidos antes de implementarlas, sin exigir finance.collections.reverse para mostrar abonos. Libro/lotes/compromisos e idempotencia por contrato/servicio (§5.11) | O-13 detalles restantes de §11.3/11.4 y O-15 autorizada/SRI/crédito/NC; O-14 solo operaciones que dependan de anticipos. Sin factura/Caja/CxC/contabilidad Spa ni Kardex automático de aplicados; SPA-ARQ-05 resuelve diez reglas funcionales; O-13 conserva solo mecanismos/equivalencias/documento adicional, transferencia, vigencia de ampliación, parciales/compensaciones económicas y fiscal/SRI/crédito | PT-16/17, AC-15-01..06, AC-25-03/04/06 y H-10/15; PostgreSQL duplicado/doble consumo, transacción independiente, reintentos/ampliación/sustitución según política; SPA-AC-ARQ05-01..10 (futuros), PostgreSQL real para cupos/consumo concurrentes e idempotencia | Contratos sin duplicados ni reversión de factura por fallo Spa, movimientos independientes; capacidades de paquete cubiertas sin inventar equivalencias/consumo ni creación por evento; cumplir §11 sin penalización automática por ausencia, intercambio libre, ampliación sin respaldo, reversión automática ni consumo entre empresas; SPA-ARQ-08 §4.2 reglas 4/5: siete reglas O-15, ajustes SPA-ARQ-05, consultas comerciales autorizadas sin documentos financieros ni saldo monetario SPA; O-14 independiente, fiscal/crédito/NC específicos pendientes; TEC-01.5/TEC-02.C libro/cupos/idempotencia/compensaciones; TEC-03 documentos/líneas válidos, equivalencia documentada, transferencia excepcional respaldada, ampliación sin extensión automática y vigencia por procedencia; cuotas pendientes sin suspensión automática; fiscal/efectos económicos pendientes | PENDING / NO AUTORIZADA |
| SPA-F7 — Operación y reportes | Presentar jornada y seguimiento operativo | Panel inicial con acceso a agenda/visitas/tratamientos/seguimiento/espera; consultas y reportes básicos; revisión responsive computadora/tablet/celular y flujos integrados | F2..F6 según datos de cada consulta, controles F1; detalle operativo/UI definido sin widgets ni métricas inventadas | Catálogo exacto de datos/reportes/filtros/acciones, permisos y mensajes; no reabrir inclusión de panel/espera/reportes | H-11/12/13/14/15, PT-03..06 aplicables; permisos/scope/lecturas, uso en tamaños de pantalla y no exposición clínica en agenda/listados | Operación básica usable y autorizada, sin dashboard analítico adicional, app independiente ni offline; sin exportaciones implícitas; TEC-03 §10.2 consulta por prestación de propuesto/contratado/facturado/pagado/reservado/realizado/derechos; referencias reales sin vincular por BP ni copiar Ventas/CxC o calcular porcentajes sin comparables; permisos/privacidad oficiales; Proyecciones operativas locales de procesos propios y enlaces oficiales; permisos recurso/menú alineados con C-01 antes de clínica; no infraestructura analítica adicional | PENDING / NO AUTORIZADA |
| SPA-F8 — Pruebas integrales y sincronización documental | Validar alcance aprobado completo y preparar evidencia de cierre | Recorridos recepción→disponibilidad→visita→expediente/valoración→consentimiento→tratamiento/sesión→evolución; venta→contrato→movimientos; panel/espera/reportes, responsive y exclusiones; futura SSOT solo de código probado | F1..F7 autorizadas y gates de propietario PASS; detalles necesarios resueltos. O-23 ya cerrada en alcance; no exigir aprobación general nueva | Detalles comerciales/técnicos/legales aplicables, sin llevar a E2E reglas aún no definidas ni desarrollar exclusiones | PT-01..17, 13 AC-15/25 y 16 AC-H; E2E focal/denegaciones, concurrencia PostgreSQL e integraciones según impacto; guardrail docs, enlaces y diff; SPA-AC-ARQ05-01..10 futuros: composición, vencimiento, cupos concurrentes sin consumo, duplicados, ausencia, cancelación/compensación, respaldo, equivalencias, NC/clínica y aislamiento empresa; SPA-AC-ARQ06-01..08 futuros: unicidad, denegación sin autorización clínica, Admin ordinario/excepcional, intersucursal, auditoría sin clínica, fotos privadas, corrección/legal e aislamiento empresarial; SPA-AC-ARQ07-01..10 futuros: contenido facial/corporal, vacíos/comparación, borrador, consentimiento requerido, promoción autorizada, foto privada, corrección e aislamiento/facultad clínica; SPA-AC-ARQ08-01..08 futuros: rechazar cliente/Employee/catálogo paralelo, lectura laboral sin contrato, evento creador de paquetes, cobro/CxC SPA, Kardex automático, permisos generales clínicos y accesos/derechos entre empresas; interfaz SPA genérica; SPA-AC-TEC02-A01/B01/C01 y SPA-AC-TEC03-01..05 futuros, junto a criterios ARQ reutilizados §28.11: negativos Admin/autoautorización, durabilidad, cierre parcial, cupos/idempotencia, documentos/plan parcial, transferencias/ampliación/vencimiento/cuotas; PostgreSQL real solo en fase futura autorizada; Criterios F0 contrato §28.12: permisos/autoescalamiento, log durable separado del rollback, integridad empresarial/rangos, idempotencia/sobrecompensación, cifrado/rutas/contexto y restauración/supresiones; ninguna prueba ejecutada aquí | Evidencia de capacidades/exclusiones, regresiones pertinentes y SSOT futura sincronizada; CLOSED solo tras gate y staging/commit del propietario; objetivos SPA-ARQ-06 comprobados solo tras resolver gates técnicos/legales, sin cierre inferido de C-01/C-02 | PENDING / NO AUTORIZADA |

## 3. Adaptaciones del orden y trazabilidad del plan anterior

El plan local anterior se conserva como base de contenido, reorganizado por el orden preliminar recuperado: antigua F2 (servicios) se incorpora a preparación técnica F0 y configuración F3 antes de atender/agendar; antigua F3 (agenda) pasa a F5 con búsqueda/visitas/espera; antigua F4 (expediente) a F2; antigua F5 (sesiones) a F3; antigua F6 (consentimiento/fotos) a F4; antigua F7 (paquetes) a F6; integraciones/E2E y cierre documental anteriores F8/F9 a nueva F8, con panel/reportes explícitos en F7. Es reorganización documental de fases no ejecutadas, no renumeración de decisiones ni afirmación de fase cerrada.

**Consentimiento:** el subalcance de texto/firma/versiones de F4 puede preceder la habilitación de atenciones de F3 si es precondición. No depende de una sesión realizada; fotos de valoración dependen de F2 y fotos de sesión de F3. Esto evita ciclo F3↔F4 y no permite saltar consentimiento.

**Agenda:** el motor/restricciones y modelos pueden prepararse técnicamente conforme a autorización exacta antes de completar todas las pantallas de F3/F4; vinculación funcional se cierra en F5. Si se exige cita para atender (O-10), finalización operativa de F3 queda bloqueada hasta ese subalcance de F5. No habilitar walk-in por orden de fases. Estas adaptaciones son propuestas técnicas expresas pendientes de revisión y autorización, no ejecuciones paralelas autorizadas.

**Paquetes:** tratar borrador/realización no requiere por inferencia una compra. Saldo/cupos comerciales y efectos de ampliación/sustitución se integran en F6 con O-15/O-25 intactas. Reservar/reprogramar no consume; ausencia/cancelación no crea sesión clínica; tratamiento y paquete siguen conceptos distintos.

**Alcance seguro:** recursos físicos, recordatorios automáticos, backups nuevos, reservas públicas, Kardex automático, nómina/RRHH completo, app/offline, diagnóstico automático/IA, analítica adicional/contabilidad paralela quedan excluidos en todas las fases (HX-01..10). Proteger o verificar respaldos existentes no permite desarrollar otro sistema. Anticipos pertenece a proyecto transversal separado, O-14 abierta.

## 4. Pruebas y gates por tipo de cambio

El catálogo de 102 escenarios heredados, 13 casos O-15/O-25 y 16 casos candidatos SPA-AC-H de la evidencia recuperada se conserva en contrato §28 y Anexo F, con literalidad/inferencia distinguidas. Cada futura fase seleccionará los escenarios de su alcance y resolverá los criterios pendientes antes de escribir código. En F0 se verifica el diseño técnico contra SPA-ARQ-01 ya aprobada, sin volver a elegir la agrupación conceptual; F5 y F8 comprueban bloques/prestación, cancelación no propagada y separación clínica/comercial. SPA-ARQ-02 resuelve expresamente estados/eventos de cita y situación derivada de visita, incluida reprogramación con indicador/historial; no agrega una dependencia ni altera F0–F8. Ese prerrequisito funcional deja de requerir una decisión; SPA-ARQ-03 resuelve el ciclo clínico, la conservación de atención parcial y la corrección trazable conforme al contrato §12 y los criterios F3–F6; siguen pendientes sus mecanismos técnicos, la cantidad de sesiones por prestación, la aplicación técnica del cierre administrativo aprobado TEC-02.B (§8.7), configuración técnica de preparación y mecanismos de concurrencia (obligaciones funcionales aprobadas SPA-ARQ-04 en F0/F5), mecanismos de ausencias y detalles comerciales restantes de SPA-ARQ-05 (§11; sin penalización automática V1.1). La verificación técnica usa lo aprobado, sin una máquina de estados propia para visita. No se confunde propuesta de prueba con prueba ejecutada.

Concurrencia, locks, transacciones, unicidad y aislamiento persistido requieren PostgreSQL real; archivos/cifrado, almacenamiento aislado. Para futuras fases de código se aplican los checks focales y guardrails del [enforcement](../architecture/enforcement.md#tests-pre-merge). No se usa este plan para lanzar builds o pruebas de código durante la consolidación documental.

El gate del propietario, diff final y staging/commit del propietario son obligatorios para CLOSED. Este plan solo está preparado para revisión; no declara CLOSED la preparación ni habilita una fase de desarrollo.

## 5. Pendientes de aprobación

Contrato §30 es el registro de cuestiones resueltas, parciales y pendientes; ADR §7 explica su impacto arquitectónico. O-15/O-25 conservan su alcance aprobado; DIR-SSOT-01 fija propiedad transversal, no un diseño. SPA-DEC-115/116 están aprobadas y O-23 cerrada en inclusión general. Faltan aprobación técnica integral del contrato/ADR, contraste O-24/V-01 y evidencia técnica/legal aplicable, lista exacta por fase y autorización expresa. **Detenerse tras el informe documental para revisión arquitectónica.**

**Consolidación SPA-TEC-01/02/03:** dirección técnica/reglas parciales aprobadas, detalle físico y validaciones pendientes conforme ADR §5.9 y contrato §30. No reabrir como pendientes funcionales cierre administrativo, cupo retenido/resolución, relación explícita plan/documentos, requisitos de equivalencia/transferencia ni no extensión automática. F0–F8 siguen PENDING / NO AUTORIZADA; no cambios LOCKED autorizados ni implementación.

## 6. Preparación F0 concentrada — selección recomendada, no autorización

ADR §5.10–5.15 concentra una opción técnica preferente por AT-01..12, contrastada estáticamente, sin implementación ni pruebas de ejecución. REG §23 distingue esta recomendación de las aprobaciones SPA-ARQ/SPA-TEC. No solicitar de nuevo inclusión funcional. Presentar selección en bloque; autorizaciones especiales solo sobre cambios concretos, sin detener análisis independiente.

| Fase | Preparación y gate pendiente |
|---|---|
| F0 | Diseño recomendado preparado para revisión conjunta; C-01 LOCKED, precisión formal, contratos nuevos propietarios y condiciones jurídicas/operativas impiden aprobación integral sin reservas. Solo revisión/documentación autorizada en este encargo. |
| F1 | No implementable: EMP-E6b y autorizaciones de seguridad/almacenamiento/auditoría previas; C-01 no cerrado. |
| F2 | Modelo elegido como recomendación; F1, mínimos/versión de fichas y legal pendientes. V-04 bloquea medidas corporales, no definición independiente de expediente. |
| F3 | Agregados/servicio recomendados; F1/F2, configuración efectiva y consentimiento F4/cita F5 cuando sean requisito; sin clínica real antes de protección. |
| F4 | Diseño de evidencia/archivos recomendado; F1, custodia/restore y condiciones jurídicas firma/menores/finalidades/conservación necesarias antes de habilitar. |
| F5 | Temporalidad/locks/exclusión/cupo recomendados; directorio/servicios, verificación PostgreSQL y detalle de transiciones/permisos pendientes; no motor externo. |
| F6 | Libro/lotes/cupos y contratos mínimos recomendados; implementación autorizada de lectores oficiales, fiscal/elegibilidad/equivalencias/documentos particulares pendientes. O-14 solo bloquea operaciones dependientes de anticipos. |
| F7 | Consultas sobre procesos disponibles y permisos oficiales; no exige resolver Anticipos para reportes independientes. |
| F8 | Implementaciones previas autorizadas/gates PASS y evidencia técnica/operativa real, no sustituida por este diseño. |

Todos los estados siguen **PENDING / NO AUTORIZADA**, ADR-051 **ACCEPTED / APROBADO** (2026-11-19). Próxima acción posible: solicitar la autorización especial acotada de C-01 y la autorización expresa de F0; validaciones externas y dependencia Empleados se tramitan por sus propios alcances. No se autoriza preparar código, modificar módulos protegidos o migraciones, ni cambiar STATUS/WORK_QUEUE en esta operación.

## 7. Especificación operativa por fase

**Estado: PLAN DE EJECUCIÓN ALINEADO CON ADR-051 ACCEPTED (SPA-SEL-01..12 aprobadas el 2026-11-19) — NINGUNA FASE AUTORIZADA.** Desarrolla la secuencia de §2 hasta el nivel necesario para que otro agente desarrolle cada fase sin repetir la planificación. No cambia fases, decisiones aprobadas ni exclusiones. Cada fase conserva **PENDING / NO AUTORIZADA** y exige autorización expresa con su lista exacta de archivos (§1). Comportamiento: [contrato §31](SPA-V1.1-CONTRACT-SPEC-01.md#31-especificación-operativa-por-área-funcional) y [§23.4](SPA-V1.1-CONTRACT-SPEC-01.md#234-especificación-operativa-de-las-19-fichas-selecciones-técnicas-aprobadas); modelo, API y algoritmos: [ADR §5.16–5.26](ADR-051-spa-v1.1-architecture.md#516--526-diseño-técnico-implementable-selecciones-spa-sel-aprobadas). Condiciones externas `COND-nn`: [contrato §31.15](SPA-V1.1-CONTRACT-SPEC-01.md#3115-condiciones-externas-y-protegidas-no-cumplidas). **Las rutas de archivos son candidatas, no existen y no deben presentarse como existentes**; raíces candidatas: `backend/src/ERP.Domain/Modules/Spa/`, `backend/src/ERP.Application/Modules/Spa/UseCases/`, `backend/src/ERP.Infrastructure/Persistence/Spa/`, `backend/src/ERP.API/Controllers/Spa/Spa<Área>Controller.cs`, `frontend/src/modules/spa/`. Todo cambio de esquema se integra en la baseline única (AT-12) con autorización separada.

### 7.1 Entregas internas y orden de construcción (resolución de dependencias circulares)

Las fases conservan su numeración y alcance; las entregas internas permiten construir un subalcance antes de completar la fase que lo contiene, sin alterar funcionalidades aprobadas.

| Entrega | Contenido | Fase madre | Requiere antes | Qué resuelve |
|---|---|---|---|---|
| F1.a | Adaptador consumidor de `IEmployeeDirectory` + doble de pruebas | F1 | COND-02 para uso real | Profesional/registrador separados |
| F1.b | `SpaPermissions` candidatas, clasificación clínica y `ISpaClinicalAccessGuard` | F1 | COND-01 (autorización especial) para activar | Admin sin acceso ordinario |
| F1.c | `CareAccessAttempt`/`CareAccessLog` y flujo durable previo a entrega | F1 | F1.b | Auditoría de lecturas |
| F1.d | `ICareFileStore` cifrado sobre `IFileStorage` | F1 | COND-09 para datos reales | Archivos privados |
| F2.a | `CareRecord`, antecedentes, alertas | F2 | F1.b–c | Expediente único |
| F2.b | Valoración facial | F2 | F2.a | — |
| F2.c | Valoración corporal y medidas | F2 | F2.a, COND-05 | Precisión |
| F4.a | Consentimientos (textos, versiones, registro, firma, retiro) | F4 | F1.d, F2.a | **Evita el ciclo F3↔F4**: el consentimiento existe antes de habilitar atenciones |
| F3.a | Perfiles de servicio y procedimientos | F3 | F1.a | Servicios antes de agendar |
| F3.b | Planes y prestaciones | F3 | F2.a, F3.a | — |
| F5.a | Horarios, excepciones, bloqueos, traslados y búsqueda de disponibilidad | F5 | F1.a, F3.a | Motor antes de pantallas clínicas |
| F5.b | Reserva de visitas/citas, ocupación, exclusión GiST, contrato de aplicación `IPackageCommitmentService` **declarado sin implementación**: la reserva vinculada a paquete responde `SPA_PACKAGE_CAPABILITY_UNAVAILABLE` hasta F6.c; la reserva independiente funciona sin él | F5 | F5.a | **Evita el ciclo F5↔F6** sin simular derechos: no existe reserva con paquete antes de F6.c |
| F3.c | Sesiones (borrador, finalizar, interrumpir, corregir) con exigencia de consentimiento; **`start` se habilita al entregar F5.c** (cita obligatoria mientras O-10 siga abierta) y hasta entonces responde `SPA_APPOINTMENT_REQUIRED` | F3 | F4.a, F2.a, F3.b | Atención solo con consentimiento válido y cita; ninguna sesión se opera sin esas capacidades |
| F5.c | Ciclo de la cita: llegada, reprogramación, cancelación, ausencia, inicio de atención (habilita `start` de F3.c), cierre administrativo | F5 | F5.b, F3.c | Vínculo agenda–clínica |
| F5.d | Lista de espera | F5 | F5.a | — |
| F4.b | Fotografías | F4 | F1.d, F4.a, F3.c (fotos de sesión) | Fotos de valoración pueden salir tras F2 |
| F6.a | Verificación/implementación autorizada de lectores comerciales | F6 | COND-03 | Lecturas oficiales |
| F6.b | Definiciones y contrato desde factura | F6 | F6.a | O-15 |
| F6.c | Libro, lotes, compromisos; aporta la implementación real de `IPackageCommitmentService` (comprobación de vigencia, servicio y cupo) y habilita la reserva vinculada; las reglas de reserva de F5.b no cambian | F6 | F6.b, F5.b | Cupo sin sobreprogramar |
| F6.d | Ampliación, sustitución, compensación, ajuste, transferencia | F6 | F6.c; COND-07 para sustitución/transferencia | — |
| F6.e | Vista comercial por prestación | F6 | F6.a, F3.b | §10.2 regla 10 |
| F7.a–c | Panel, reportes R1–R7, revisión responsive | F7 | F2–F6 según dato | — |
| F8 | Recorridos E2E y cierre | F8 | F1–F7 | — |

Secuencia lineal sugerida: F0 → F1.a–d → F2.a → F4.a → F3.a → F5.a → F2.b → F3.b → F5.b → F3.c → F5.c → F5.d → F4.b → F2.c (cuando COND-05) → F6.a–e → F7 → F8. El orden es una propuesta; cada entrega se autoriza por separado.

### 7.2 SPA-F0 — Arquitectura y contratos

| Elemento | Especificación |
|---|---|
| 1 Objetivo | Obtener la aprobación técnica y los gates que permitan autorizar F1; dejar verificadas las dependencias reales |
| 2 Funcionalidades | Ninguna de producto. Decisión sobre ADR-051, verificación de contratos propietarios (BP, Items/Pricing, Ventas, CxC, `ICompanyClock`, `IUnitOfWork`, `IFileStorage`, `IAuditService`), decisión sobre lectores comerciales, lista exacta de archivos de F1 |
| 3 Entidades y casos de uso | Ninguno implementado; confirma el modelo ADR §5.16 y los casos de uso §5.17 |
| 4 Backend y API | Ninguno |
| 5 Pantallas y frontend | Ninguno |
| 6 Integraciones | Verificar con los propietarios si `GetSalesInvoiceByIdQuery` basta (COND-03); estado de EMP-E1..E6b (COND-02) |
| 7 Dependencias | Autorización de preparación documental/lectura |
| 8 Seguridad | Presentar la solicitud de autorización especial C-01 con el impacto exacto del ADR §5.13 |
| 9 Pruebas unitarias | No aplica |
| 10 Pruebas de integración | No aplica |
| 11 E2E | No aplica |
| 12 Escenarios negativos | Verificación documental: ninguna propuesta modifica FROZEN/LOCKED/CLOSED sin su gate; sin contradicciones contrato–ADR–plan–registro |
| 13 Criterios de salida | (a) decisión expresa del propietario sobre ADR-051 con reservas registradas; (b) lista exacta de rutas y objetos de esquema de F1; (c) respuesta a COND-01 (autorizada o diferida) y COND-03; (d) STATUS/WORK_QUEUE actualizados por el propietario; (e) guardrail documental y `git diff --check` en verde |
| 14 Fuera | Código, migraciones, Empleados, cambios de módulos protegidos |
| 15 Autorizaciones / validaciones | COND-13; COND-01 (solicitud) |

### 7.3 SPA-F1 — Empleados y base segura

| Elemento | Especificación |
|---|---|
| 1 Objetivo | Disponer de identidad profesional oficial, autorización clínica, auditoría durable y almacenamiento privado antes de cualquier dato clínico |
| 2 Funcionalidades | F1.a–F1.d; consulta de accesos (Pantalla 14, pestaña Accesos); sin contenido clínico |
| 3 Entidades y casos de uso | `CareAccessAttempt`, `CareAccessLog`, `spa_care_files`, `ISpaClinicalAccessGuard`, `ICareFileStore`; consulta `ListAccessLog`; (concesiones `spa_access_grants` solo si COND-01 autoriza) |
| 4 Backend y API | Tablas/configuración `spa_care_access_logs`, `spa_care_files`; `GET /spa/audit/access-log`; registro de códigos `SPA_ACCESS_DENIED`, `SPA_FILE_*`; **ningún endpoint clínico** |
| 5 Pantallas y frontend | Shell `/spa` con entradas condicionadas por permiso y Pantalla 14 (Accesos) |
| 6 Integraciones | `IEmployeeDirectory` (EMP-E6b), `IAuditService`/`EfAuditWriter`, `IFileStorage`, Identity/Access (solo lectura hasta COND-01) |
| 7 Dependencias | F0; EMP-E6b implementado y probado bajo el plan de Empleados; COND-01 para activar la clasificación clínica real |
| 8 Reglas de seguridad | Admin sin acceso ordinario; backend/recurso decide; sin autoautorización; excepción temporal por otra persona; auditoría durable antes de entregar; denegación segura; `AesGcm` con DEK por archivo y KEK custodiada aparte; ruta nunca arbitraria; sin `DeleteAsync` |
| 9 Pruebas unitarias | Decisiones del guard (permitido/denegado/excepción vigente/vencida/autoaprobación); cifrado ida y vuelta, detección de manipulación (tag/AAD), contención de rutas; validación de tamaño/tipo |
| 10 Pruebas de integración | PostgreSQL real: intento + log confirmados **antes** de devolver el DTO; denegación persistida en transacción separada; fallo de log → no entrega; aislamiento por empresa; almacenamiento aislado con fallo de clave; arquitectura: sin `DeleteAsync` sobre archivos clínicos, claves clínicas clasificadas |
| 11 E2E | Intento de acceso de Admin sin concesión → 403 y registro; usuario con permiso accede y queda registro |
| 12 Escenarios negativos | API directa, MediatR, descarga, reportes, menú, cambio de empresa, operador global, autoescalamiento de perfiles/memberships, ID de otra empresa → 404 |
| 13 Criterios de salida | Guard y log verificados con PostgreSQL real; archivos cifrados verificados; pruebas negativas de COND-01 en verde **o** funcionalidad clínica explícitamente desactivada; gate PASS del propietario |
| 14 Fuera | Contenido clínico, sistema nuevo de respaldos, cambios a `IFileStorage`/`LocalFileStorage`/scripts, Employee propio |
| 15 Autorizaciones / validaciones | COND-01, COND-02, COND-09, COND-10, COND-06 (diseño), COND-13 |

### 7.4 SPA-F2 — Expediente y valoraciones

| Elemento | Especificación |
|---|---|
| 1 Objetivo | Expediente único por cliente y empresa con antecedentes, alertas y valoraciones faciales/corporales versionadas |
| 2 Funcionalidades | Contrato §31.2, §31.8, §31.9; seguimiento parcial (Pantalla 10: valoraciones y medidas) |
| 3 Entidades y casos de uso | `CareRecord`, `HistoryRevision/Answer/Category`, `CareAlert`, `Assessment`, `AssessmentRevision`, `FacialFindings`, `BodyMeasurementSet`; OpenCareRecord, SetCareRecordActive, ReviseMedicalHistory, Add/DeactivateCareAlert, SaveAssessmentDraft, RegisterAssessment, ReviseAssessment, CompareMeasurements, consultas |
| 4 Backend y API | Rutas ADR §5.18 de expedientes y valoraciones; unicidad `(tenant, company, BP)`; revisiones con puntero vigente; lectura detallada audita antes de responder |
| 5 Pantallas y frontend | Pantallas 05, 06, 07, 10 (parcial), módulo `frontend/src/modules/spa/` con pestañas del expediente |
| 6 Integraciones | `GetBusinessPartnerQuery`/`...RolesQuery`, directorio, guard y log de F1 |
| 7 Dependencias | F1 completa (guard, log; archivos no requeridos); COND-05 para F2.c; COND-12 solo si se decide capturar nacimiento/emergencia |
| 8 Reglas de seguridad | Todo acceso clínico por guard; sin clínica en listados; corrección con motivo/autor; versiones sustituidas restringidas; sin datos clínicos reales hasta COND-10 |
| 9 Pruebas unitarias | Validadores (fototipo, categorías, motivo de corrección), transiciones Borrador→Registrada, diferencias solo con valores comparables, nulo ≠ cero |
| 10 Pruebas de integración | PostgreSQL real: apertura concurrente → un solo expediente; revisión inmutable; aislamiento entre empresas; `xmin` en borrador; auditoría durable de lectura |
| 11 E2E | Abrir expediente → antecedentes → valoración facial incompleta (borrador) → registrar → corregir |
| 12 Escenarios negativos | BP inactivo/sin rol Customer; cliente de otra empresa; Admin sin concesión; corregir sin motivo; fototipo 7; valor corporal negativo; persistir medidas sin gate (rechazado por bandera) |
| 13 Criterios de salida | `SPA-AC-OP-01.x`, `08.x`, `09.x` y `SPA-AC-UI-05/06/07` en verde; AC-ARQ06-01/05/08 y ARQ07-01/02/03/09/10; F2.c solo con COND-05 resuelta |
| 14 Fuera | Fecha de nacimiento/contacto de emergencia (O-06), numeración de ficha (O-19), gráficas, diagnóstico automático, ampliar BP |
| 15 Autorizaciones / validaciones | COND-05, COND-06 (corrección/conservación), COND-12, COND-10 |

### 7.5 SPA-F3 — Servicios, tratamientos y sesiones

| Elemento | Especificación |
|---|---|
| 1 Objetivo | Configurar servicios, planificar tratamientos y documentar sesiones con el ciclo clínico aprobado |
| 2 Funcionalidades | Contrato §31.4, §31.5, §31.6 (y registro documental de productos/equipos, Pantalla 13) |
| 3 Entidades y casos de uso | `SpaServiceProfile`, `Procedure`, vínculos, `TreatmentPlan`, `PlanProvision`, `ServiceProvision`, `ClinicalSession`, `ClinicalRevision`, `SessionMaterial`; casos ADR §5.17 «Servicios», «Tratamientos», «Sesiones» |
| 4 Backend y API | Rutas `service-profiles`, `procedures`, `treatment-plans`, `sessions`; matriz de transición ADR §5.23; `StartSession` exige consentimiento válido (F4.a) y cita Programada (F5.c; sin cita no se habilita) |
| 5 Pantallas y frontend | Pantallas 03, 08, 09, 10, 13; Lista→Editor en servicios; planes y sesiones como pestañas del expediente |
| 6 Integraciones | `GetItemByIdQuery`, `IPricingResolver` (lectura), directorio, consentimientos (F4.a), vínculo de referencia al contrato (sin lógica de cupo) |
| 7 Dependencias | F1, F2.a; F3.a necesita F1.a; F3.c necesita **F4.a**; vínculo a cita se completa en F5.c |
| 8 Reglas de seguridad | Datos clínicos por guard; sesión con responsable y registrador separados; sin consumo ni factura por finalizar; correcciones como revisión |
| 9 Pruebas unitarias | Matrices de transición; validación de perfil; reglas «borrador no consume», «interrumpida conserva parcial», «corrección no crea atención» |
| 10 Pruebas de integración | PostgreSQL real: unicidad perfil/Item; `xmin` en borrador; inicio con/sin consentimiento; revisión numerada única; materiales sin movimiento de inventario (consulta de Kardex sin cambios) |
| 11 E2E | Configurar servicio → plan → sesión borrador → iniciar (con consentimiento) → interrumpir → corregir; cerrar plan con pendientes |
| 12 Escenarios negativos | Product como servicio; servicio inactivo; iniciar sin consentimiento; finalizar sin permiso; corregir sin motivo; editar sesión finalizada directamente; cierre de plan sin confirmación; **DP-01..03 (aprobadas):** reanudar conserva el evento `Suspended`; reabrir un plan Cancelado/Cerrado → `SPA_PLAN_INVALID_STATE`; suspender/cancelar/cerrar un plan no cancela citas, no modifica sesiones ni libera compromisos de cupo (las citas aparecen en «Citas por revisar») |
| 13 Criterios de salida | `SPA-AC-OP-03.x`, `05.x`, `06.x`; AC-ARQ03-01..08; PT-15; plan sin contrato válido; DP-01..03 (aprobadas el 2026-11-19) verificados por pruebas de aceptación; ninguna sesión habilitada sin consentimiento aplicable |
| 14 Fuera | Consumo comercial (F6), cita↔sesión completa (F5.c), Kardex automático, calendario de equipos, walk-in sin decisión O-10 |
| 15 Autorizaciones / validaciones | COND-06 (versionado/conservación), COND-04 (consentimiento aplicable), COND-10 |

### 7.6 SPA-F4 — Consentimientos y fotografías

| Elemento | Especificación |
|---|---|
| 1 Objetivo | Registrar consentimientos con firma en pantalla por finalidad y conservar fotografías privadas comparables |
| 2 Funcionalidades | Contrato §31.10 (F4.a) y §31.11 (F4.b) |
| 3 Entidades y casos de uso | `ConsentDefinition/Version/Record/Withdrawal`, `CareFile`, `CarePhoto`, `PhotoComparison`; Upsert/Publish/Record/Withdraw consentimiento, UploadPhoto, OpenPhoto, DeactivatePhoto, CreatePhotoComparison |
| 4 Backend y API | Rutas ADR §5.18 de consentimientos y fotografías; contenido por descarga autorizada con `no-store`; firma almacenada por `ICareFileStore` |
| 5 Pantallas y frontend | Pantallas 11 y 12; lienzo de firma y visor/comparador como componentes nuevos justificados |
| 6 Integraciones | `ICareFileStore`, guard/log (F1), expediente (F2), sesiones (F3.c, para fotos de sesión) |
| 7 Dependencias | F4.a: F1.d, F2.a. F4.b: F4.a; fotos de sesión: F3.c; COND-09 antes de archivos reales |
| 8 Reglas de seguridad | Archivo privado cifrado, id opaco, sin URL pública, auditoría durable previa a servir, no-publicación automática, finalidades independientes, sin uso académico |
| 9 Pruebas unitarias | Una versión vigente por definición; hash del texto; independencia de finalidades; firma obligatoria si Otorga; validación tamaño/tipo |
| 10 Pruebas de integración | Almacenamiento aislado: cifrado en disco, descarga solo autorizada, fallo de log → no entrega, otra empresa → 404; PostgreSQL real: unicidad de versión vigente, retiro |
| 11 E2E | Presentar consentimiento → firmar → atención habilitada → subir foto → comparar → desactivar |
| 12 Escenarios negativos | Subir foto sin consentimiento de seguimiento; firma vacía; versión inexistente; retirar dos veces; descarga con ruta manipulada; menor de edad (no soportado, COND-04) |
| 13 Criterios de salida | `SPA-AC-OP-10.x`, `11.x`; AC-ARQ07-04..06; ningún dato real hasta COND-04/06/09/10 |
| 14 Fuera | Uso académico, representante/menores, validez jurídica certificada, publicación/exportación automática, backups nuevos |
| 15 Autorizaciones / validaciones | COND-04, COND-06, COND-09, COND-10 |

### 7.7 SPA-F5 — Agenda, visitas y lista de espera

| Elemento | Especificación |
|---|---|
| 1 Objetivo | Un motor único de disponibilidad para búsqueda y reserva, con visitas múltiples y concurrencia segura |
| 2 Funcionalidades | Contrato §31.3 (agenda), §31.7; Pantallas 01, 02, 16, 17, 18 |
| 3 Entidades y casos de uso | Reglas de horario, excepciones, bloqueos, traslados, `Visit`, `Appointment`, `ServiceProvision`, ocupaciones, cambios, lista de espera; casos ADR §5.17 «Disponibilidad», «Agenda», «Lista de espera» |
| 4 Backend y API | Rutas ADR §5.18; algoritmo ADR §5.20 (locks ordenados, revalidación, exclusión GiST, rollback de reprogramación); `btree_gist` en la baseline |
| 5 Pantallas y frontend | Calendario día/semana, drawers de cita/visita, búsqueda, administración de horarios, lista de espera |
| 6 Integraciones | Directorio, `ICompanyClock`, Branch, Items/perfil, `IPackageCommitmentService` (sin implementación hasta F6.c: error explícito, no simulación), sesiones (F5.c) |
| 7 Dependencias | F1.a, F3.a; F5.c necesita F3.c; paquetes opcionales hasta F6 |
| 8 Reglas de seguridad | Agenda sin clínica; permisos operativos; ocupación y cambios auditados |
| 9 Pruebas unitarias | Cálculo de ventanas (horario ∩ sucursal − descansos ± excepciones − bloqueos), preparación, traslado: misma sucursal sin traslado / sucursal distinta configurado, insuficiente y no configurado (no se ofrece en búsqueda ni se acepta en reserva con la misma función), contigüidad `[)`, horas inexistentes/ambiguas, matriz de transición de cita |
| 10 Pruebas de integración | **PostgreSQL real obligatorio:** reservas simultáneas, orden de locks sin deadlock, exclusión GiST sin lock, rollback atómico de reprogramación, visita multiprofesional atómica, cierre administrativo libera solo lo futuro |
| 11 E2E | Buscar → elegir → reservar → llegada → iniciar atención → cierre; reprogramar con conflicto; lista de espera → reserva |
| 12 Escenarios negativos | Profesional no asignado/inactivo; fuera de horario; solape; traslado sin configurar; servicio inactivo; ausencia sin penalización; cancelar un bloque no cancela los demás; versión obsoleta → 409; reserva con `packageContractId` antes de F6.c → `SPA_PACKAGE_CAPABILITY_UNAVAILABLE` sin crear cita, compromiso ni derecho; reserva independiente sin paquete → válida |
| 13 Criterios de salida | `SPA-AC-OP-07.x`, `02.x`; SPA-AC-ARQ04-01..12; PT-14, AC-25-01..07; H-01/02/07/08/12 |
| 14 Fuera | Cabinas/equipos, recordatorios, reservas públicas, reasignación automática, estados nuevos, penalización por ausencia |
| 15 Autorizaciones / validaciones | COND-02; verificación PostgreSQL autorizada; configuración de traslados la define el negocio (sin predeterminados) |

### 7.8 SPA-F6 — Paquetes e integración comercial

| Elemento | Especificación |
|---|---|
| 1 Objetivo | Derechos por servicio con libro trazable y contrato explícito desde factura, sin duplicar Ventas/CxC |
| 2 Funcionalidades | Contrato §31.12, §31.13; Pantalla 04 y vista comercial de Pantalla 08 |
| 3 Entidades y casos de uso | Definiciones, `PackageContract`, lotes, movimientos, compromisos, asignaciones; DefinePackage, CreateContractFromInvoice, ResolveCommitment, Compensate, Extend, Substitute, Transfer, Adjust, consultas |
| 4 Backend y API | Rutas ADR §5.18 de paquetes; lock contrato/servicio; `clientRequestId` + índice único; libro sin UPDATE/DELETE |
| 5 Pantallas y frontend | Pantalla 04 (definiciones, contratos, libro, asistente desde factura) |
| 6 Integraciones | `ISalesEntitlementReader` o `GetSalesInvoiceByIdQuery`, `GetReceivableByInvoiceQuery`, `IReceivableCollectionReader`, Items; agenda (implementación real de `IPackageCommitmentService` que habilita la reserva vinculada de F5.b) |
| 7 Dependencias | F1, F3, F5; F4 según consentimiento; COND-03 antes de F6.b |
| 8 Reglas de seguridad | Permisos de Ventas/Finance del consumidor; sin `finance.collections.reverse`; scope de sucursal original; sin consumo entre empresas |
| 9 Pruebas unitarias | Saldo = derechos − consumos netos − compromisos; idempotencia; sobrecompensación; vigencia por lote; equivalencia de sustitución |
| 10 Pruebas de integración | PostgreSQL real: contrato único por línea, programación concurrente sin exceder el cupo, consumo idempotente, fallo de Spa sin efecto en la factura, compromisos Held→Consumed/Released |
| 11 E2E | Factura autorizada → contrato → reserva vinculada → atención → resolución → consumo → compensación |
| 12 Escenarios negativos | Factura no autorizada; cliente distinto; línea ya vinculada; vencido; sin saldo; sustitución sin equivalencia; ampliación sin documento; transferencia sin respaldo (bloqueada); cuota pendiente sin suspensión automática |
| 13 Criterios de salida | `SPA-AC-OP-12.x`, `13.x`; SPA-AC-ARQ05-01..10, SPA-AC-ARQ08-01..08; AC-15-01..06; PT-16/17 |
| 14 Fuera | Facturas/cobros/CxC/asientos propios, evento creador, saldo monetario, Anticipos (O-14), Kardex automático |
| 15 Autorizaciones / validaciones | COND-03, COND-07, COND-08, COND-11 |

### 7.9 SPA-F7 — Operación y reportes

| Elemento | Especificación |
|---|---|
| 1 Objetivo | Presentar la jornada y el seguimiento operativo con consultas básicas **sin contenido clínico**, y verificar que el seguimiento clínico aprobado (evolución, Pantalla 10) solo se consulte por las consultas protegidas |
| 2 Funcionalidades | Contrato §31.14; Pantallas 15 y 19 (operativas); verificación de la Pantalla 10 y de `GET /spa/care-records/{id}/evolution` (seguimiento clínico, entregado en F2/F3) |
| 3 Entidades y casos de uso | GetPanel, ListFollowUp, GetOperationalReport R1–R7; sin entidades nuevas |
| 4 Backend y API | `GET /spa/panel`, `/spa/follow-up` (seguimiento **operativo**: conteos y pendientes) y `/spa/reports/{kind}` sobre agregados propios, sin contenido clínico. El **seguimiento clínico** no usa estos endpoints: `GET /spa/care-records/{id}/evolution` con `spa.sessions.view`/`spa.assessments.view`/`spa.photos.view` según la sección, guard clínico e intento auditado (ADR §5.21) |
| 5 Pantallas y frontend | Panel y reportes; revisión responsive de todo el módulo (computadora/tablet/celular) |
| 6 Integraciones | Agenda, sesiones, paquetes, lecturas comerciales |
| 7 Dependencias | F2–F6 según cada consulta |
| 8 Reglas de seguridad | Panel, seguimiento operativo y reportes: sin contenido clínico (solo agregados y referencias no clínicas); seguimiento clínico: únicamente consultas protegidas por permisos clínicos, guard de recurso y auditoría durable; permiso del dato de origen; sin exportación |
| 9 Pruebas unitarias | Cálculo de conteos y filtros; omisión por permiso |
| 10 Pruebas de integración | Conteos = datos de origen; aislamiento entre empresas; la evolución clínica se entrega solo con permiso clínico, guard e intento auditado confirmado |
| 11 E2E | Recorrido del panel a cada proceso; responsive en tres anchos |
| 12 Escenarios negativos | Usuario sin permiso; empresa distinta; filtros extremos; sin datos; **autorización negativa del seguimiento clínico:** usuario con `spa.reports.view` o `spa.agenda.view` sin permiso clínico → 403 en `evolution`; Admin sin concesión → 403; permiso de una sola sección (p. ej. solo `spa.photos.view`) → las demás secciones omitidas; recurso de otra empresa → 404; fallo del registro de acceso → no se entrega; ningún reporte o panel contiene texto clínico, mediciones ni fotografías |
| 13 Criterios de salida | `SPA-AC-OP-14.x`, `SPA-AC-UI-10/15/19`; H-11..15; pruebas de autorización negativa del seguimiento clínico en verde; sin gráficos ni métricas inventadas |
| 14 Fuera | Dashboard analítico, exportaciones, indicadores calculados no aprobados |
| 15 Autorizaciones / validaciones | Ninguna adicional |

### 7.10 SPA-F8 — Pruebas integrales y sincronización documental

| Elemento | Especificación |
|---|---|
| 1 Objetivo | Validar el alcance aprobado completo y preparar el cierre |
| 2 Funcionalidades | Recorridos recepción → disponibilidad → visita → expediente/valoración → consentimiento → sesión → evolución; venta → contrato → movimientos; panel/espera/reportes; exclusiones |
| 3 Entidades y casos de uso | Ninguna nueva |
| 4 Backend y API | Correcciones focales surgidas de las pruebas, solo con autorización |
| 5 Pantallas y frontend | Revisión funcional/responsive de las 19 pantallas |
| 6 Integraciones | Todas las anteriores |
| 7 Dependencias | F1–F7 con gates PASS |
| 8 Reglas de seguridad | Pruebas negativas completas de COND-01 y de aislamiento; verificación de las diez exclusiones |
| 9 Pruebas unitarias | Regresión de las de fases previas relacionadas con cualquier corrección |
| 10 Pruebas de integración | PostgreSQL real focal: concurrencia, unicidad, aislamiento, idempotencia; almacenamiento aislado |
| 11 E2E | Los recorridos de «2 Funcionalidades» en navegador real, con denegaciones (Admin sin concesión, otra empresa) |
| 12 Escenarios negativos | Catálogo SPA-AC-ARQ04..08, TEC02/03 y los `SPA-AC-OP`/`UI` completos |
| 13 Criterios de salida | Evidencia de capacidades y exclusiones; guardrails documentales y de arquitectura; SSOT de Spa creada **solo** con lo implementado y probado; CLOSED únicamente tras gate, staging y commit del propietario |
| 14 Fuera | Nuevas funcionalidades, fases no autorizadas |
| 15 Autorizaciones / validaciones | COND-04, COND-06, COND-08, COND-09, COND-10 deben estar resueltas para operar datos reales; sin ellas el módulo solo puede ejecutarse con datos de prueba |

### 7.11 Qué debe verificar cualquier agente antes de empezar una fase

1. Autorización expresa de la fase y lista exacta de rutas (existentes, nuevas, protegidas).
2. Que las rutas candidatas de este plan existan o no; ninguna se presenta como existente sin comprobarlo.
3. Estado de las `COND-nn` aplicables a la fase (§7.2–7.10 fila 15) y que no se ejecuta lo «prohibido mientras esté pendiente» (contrato §31.15).
4. Lectura previa de `CLAUDE.md`, STATUS, FEATURES, WORK_QUEUE y ADR-040/041; auditoría de reutilización de UI declarada.
5. Estrategia de esquema AT-12 (baseline única, objetos raw preservados) con autorización separada.
6. PostgreSQL desechable disponible para las pruebas de concurrencia; prohibido ejecutar la suite completa.

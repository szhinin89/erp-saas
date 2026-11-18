# ADR-051 — Gali Spa V1.1

Estado: **PROPOSED / NO APROBADO** — 2026-11-18. Arquitectura candidata para revisión; no implementada. **Ninguna fase está autorizada**, tampoco Empleados. SPA-DEC-115/116 aportan alcance funcional aprobado; esta integración no aprueba políticas o mecanismos técnicos pendientes ni autoriza cambios FROZEN/CLOSED.

Patrón: [ADR-049](ADR-049-sales-order-v1-architecture.md). Comportamiento y pantallas: [contrato central](GALI-SPA-V1.1-CONTRACT-SPEC-01.md); evidencia de decisiones: [registro](GALI-SPA-V1.1-APPROVED-DECISIONS-01.md); fases: [plan](GALI-SPA-V1.1-IMPLEMENTATION-PLAN-01.md). Entrega y gates: [STATUS](../../STATUS.md); trabajo abierto: [WORK_QUEUE](../agent/WORK_QUEUE.md). Este ADR explica alternativas y límites, no es una segunda especificación funcional ni una SSOT implementada.

**Numeración comprobada:** el inventario de `docs/decisions/ADR-*.md` y el índice llegaban a ADR-050; no había archivo ni referencia ADR-051 al preparar este documento. Los huecos históricos 029/030 no se reutilizan. No se declara Accepted por asignarle número.

## 1. Contexto y problema

Spa fue planificado antes de Empleados. Existen ocho documentos con recuperación, propuestas técnicas, una matriz, normalización, decisiones aprobadas y contrato central. No se reinicia el diseño: se integran SPA-DEC-115/116 históricamente aprobadas por evidencia del propietario, conservando planificación [F]/[R] separada de las propuestas técnicas [T], de las decisiones pendientes y de las aprobaciones comprobadas.

El problema arquitectónico consiste en soportar agenda y atención estética con datos sensibles y paquetes comerciales, reutilizando identidades y procesos oficiales, sin duplicar ERP ni acoplar fallos de Spa a la autorización de una factura. Empleados y su directorio aún no están implementados. Hay decisiones abiertas de privacidad, conservación, precisión y políticas de paquetes; por eso la arquitectura sigue propuesta.

## 2. Alcance y exclusiones

El alcance funcional está **APROBADO por SPA-DEC-116** y la búsqueda de disponibilidad por servicio por **SPA-DEC-115**. Se integra la evidencia histórica aportada por el propietario sin reiniciar los 13 archivos locales. **O-23 CERRADA en inclusión funcional general**; el contraste de fichas sigue O-24/V-01 y los textos SPA-DEC-001..114 no se declaran recuperados. Esto no acepta técnicamente este ADR ni autoriza ejecución.

Arquitectura debe admitir agenda/visitas con varios servicios/profesionales, motor central de disponibilidad y búsqueda por servicio sin profesional inicial; tratamientos individuales/multisesión con ampliación/suspensión/cancelación/cierre; sesiones/formularios/borradores/interrupciones/correcciones; expediente único por cliente/empresa con alertas/valoraciones/historial; paquetes individuales/combinados y movimientos de sesiones; consentimiento por procedimiento con firma en pantalla/versiones/finalidades; fotos privadas, panel/lista de espera/reportes operativos y responsive computadora/tablet/celular. Su comportamiento se concentra en contrato §2/7..23, no se duplica aquí.

**Diez exclusiones expresas** en contrato §3.1.1: disponibilidad de cabinas/equipos, recordatorios automáticos WhatsApp/correo/SMS, sistema nuevo de respaldo/recuperación, reservas públicas, Kardex automático de productos aplicados, nómina/RRHH completo, app móvil independiente, offline, diagnóstico automático/IA, dashboard analítico adicional y contabilidad paralela. No confundir panel/reportes con analítica adicional ni respaldo existente con desarrollo de otro sistema. Spa no crea facturas/CxC/cobros/asientos (O-15); anticipos son proceso transversal independiente (DIR).

## 3. Decisiones del propietario y límites exactos

| Fuente | Principio que condiciona la arquitectura | Lo que no aprueba |
|---|---|---|
| SPA-DEC-115, REG §9 | Servicio/fecha/sucursal sin profesional previo; resultados de profesionales habilitados/horarios; recepción selecciona, sin confirmación automática; motor único con restricciones | Valores/cálculo/representación de descansos/bloqueos/preparación/traslados; sin calendario de cabinas/equipos |
| SPA-DEC-116, REG §10; contrato §2/3 | Alcance histórico, agrupaciones y exclusiones recuperados; expediente único, visita múltiple, tratamientos/sesiones/paquetes completos, firma en pantalla, responsive y operación básica | Entidades/agregados, API/DTO, permisos técnicos exactos, estados/locks, políticas comerciales concretas y certificación legal |
| O-25, REG §1; contrato §8.1 | Reserva crea cita; atención documentada crea sesión realizada independiente; programación de paquete = cita vinculada; reservar/reprogramar no consume; cancelar/ausentarse no crea sesión clínica; consumo independiente y trazabilidad | Estados técnicos, ausencias con consumo, cancelación sobre saldo y atención sin cita. Visita múltiple y restricciones aprobadas por 115/116 no son revocadas por no pertenecer al alcance literal de O-25 |
| O-15, REG §2; contrato §11.1 | Contrato explícito tras validar factura autorizada, referencia factura/línea, operación fuera de autorización de Ventas, evitar duplicados, fallo Spa no revierte factura, sin documentos comerciales propios | Significado final de autorizada, estado SRI, crédito pendiente, NC/anulación, permisos o entidades; creación automática por evento no aprobada |
| ADR-050 §3 DEC-E1/DEC-E4, §6/8/13 | Reutilizar Employee y directorio de lectura con ids estables y consulta histórica de inactivos; profesional y registrador separados; disponibilidad y permisos clínicos son de Spa | Firmas definitivas del directorio, una segunda identidad laboral o acceso clínico por employees.* / asignación laboral |
| DIR-SSOT-01, REG §7 | Anticipos de Clientes es proceso transversal independiente | Diseño financiero, módulo implementador, tratamiento fiscal/contable o cierre O-14 |

Los textos literales y restantes decisiones aplicables de Empleados permanecen en REG y contrato; no se amplían por este resumen.

## 4. Base técnica y grado de verificación

Las evidencias heredadas de las verificaciones documentales anteriores se conservan con su procedencia (contrato §4 y NORM §5); no se presentan como nuevas pruebas ejecutadas aquí.

| Capacidad o límite | Evidencia y consecuencia |
|---|---|
| Modularidad y capas | [Arquitectura](../architecture/architecture.md), [backend](../architecture/backend.md), [frontend](../architecture/frontend.md): monolito modular, contratos públicos; sin acceso cruzado a internos |
| Identidad | ADR-017: BusinessPartner subscriber-scoped, FROZEN; no hacerlo Company-scoped. La habilitación Customer por empresa se distingue de la identidad compartida |
| Item y servicio | [Items](../architecture/items.md), ADR-040: Item por empresa; Service no tiene bodega/stock. Duración y condiciones de atención son datos candidatos de Spa |
| Venta y cartera | Contrato §20; V-03b: evento de autorización existe y no trae cliente/líneas; su procesamiento transaccional impide usarlo para crear contratos de Spa bajo O-15 |
| Seguridad | [Security & Access LOCKED](../architecture/security.md#security--access-contract-v1--locked): Admin tiene bypass oficial; claves nuevas no resuelven por sí solas privacidad clínica |
| Auditoría y archivos | [Entity Audit](../architecture/audit-infrastructure.md) FROZEN; no hay registro general de lecturas exitosas. IFileStorage no ofrece por sí mismo cifrado/control clínico; StorageObjectId no tiene resolutor reutilizable (V-08) |
| Tiempo y precisión | [Datos](../architecture/data-standards.md): fecha de negocio y UTC separados; horarios recurrentes/tstzrange sin precedente directo V-05; medidas corporales sin clase aprobada V-04 |

## 5. Arquitectura candidata y alternativas técnicas

**Todas las propuestas AT-xx siguen NO APROBADAS.** No crean rutas, interfaces ni clases en código. El contrato §24 conserva entidades candidatas y §25 capacidades conceptuales, sin aprobar esquema o API.

| ID | Candidato y motivo | Alternativa / condición |
|---|---|---|
| AT-01 | Módulo Spa en Domain/Application/Infrastructure y frontend propio, con procesos integrados de agenda/visitas, atención/tratamientos/sesiones, paquetes y operación inicial/espera/reportes, sin módulo independiente por característica; separa datos mínimos de agenda de recursos sensibles | Límites definitivos de agregados y nombres técnicos tras revisión; no crear otra infraestructura común |
| AT-02 | Identificadores de BP, Employee, Item y documentos comerciales; lectura mediante contratos públicos del propietario | Directorio Empleados previo; contrato de lectura de Ventas a confirmar. Sin repositorios cruzados ni copia de identidad/precios/impuestos |
| AT-03 | Operativos con TenantId/CompanyId; cita y sesión con sucursal de atención. Expediente único por cliente y empresa aprobado, modelo/índice candidato; catálogos sin sucursal solo si su clasificación lo permite | Resolver clasificación de agregados con Branch Ownership Rule antes de esquema. O-02 define visibilidad, no sustituye ownership; BP continúa tenant-scoped |
| AT-04 | Cita y atención separadas; modelo candidato de visita con prestaciones/profesionales múltiples, horarios/restricciones centralizados y trazabilidad; misma disponibilidad para búsqueda y reserva | O-08 resuelta: sin disponibilidad cabinas/equipos. Relación visita–cita–prestación, cálculo de restricciones, O-09/10/11 de detalle y horas ambiguas pendientes; no imponer profesional/servicio único por visita ni confirmar por búsqueda |
| AT-05 | Locks por profesionales y exclusión de intervalos como candidatos, con orden canónico a verificar para visitas múltiples; token optimista y revalidación tras búsqueda | Esquema del contrato §27 es ilustrativo: completar TenantId/CompanyId y confirmar si conflicto cruza sucursales; prueba real de tstzrange. No copiar fragmento SQL como contrato aprobado |
| AT-06 | PackageContract único por factura/línea en su scope, creación reintentable e idempotente, transacción propia de Spa | O-15 fija resultado, no índice ni llave de idempotencia; validar detalle de factura, SRI/crédito y concurrencia. No suscribir creación a SalesInvoiceAuthorizedEvent |
| AT-07 | Libro de sesiones append-only y compensaciones; consumo separado de atención | Individual/combinado, vigencia/cupos/ampliación/sustitución aprobados; O-13 conserva cálculo, equivalencias, límites, consumo/reserva de saldo, ausencia/cancelación, transferencia, devolución y sucursales. No ofrecer sin vigencia ni fijar saldo técnico por inferencia |
| AT-08 | Expediente único con alertas y planes; tratamiento individual/multisesión y ciclo aprobado; versiones/medidas en lectura y modelo de borrador/finalización/interrupción/corrección compatible con O-25 | Capacidades aprobadas no se reabren; entidad de preparación vs atención realizada, sesión parcial, transiciones/efectos de tratamiento/paquete pendientes. O-05/19/21/22 solo detalles; O-20 resuelta. Sin nueva escala inferida |
| AT-09 | Entity Audit por entidades Spa, más CareAccessLog específico para lectura, sin contenido clínico | O-03: síncrono fail-closed, asíncrono o mixto; infraestructura FROZEN intacta; definir fallos/retención |
| AT-10 | ICareFileStore propio sobre almacenamiento oficial con referencia privada, ruta generada por servidor y lectura autorizada | Firma en pantalla/versiones por procedimiento y fotos privadas aprobadas; resolver captura/autenticidad/conservación, O-04/05 y V-07/08 para archivos/claves y backups existentes. Sin nuevo sistema de respaldo (HX-03) ni resolutor ItemImage supuesto |
| AT-11 | Permisos propios declarados en Kernel; UI con componentes, navegación, mensajes y validación oficiales | Catálogo contrato §21 aún candidato; política Admin/sucursal pendiente. No reutilizar permisos de Empleados como acceso clínico |
| AT-12 | Modelo futuro integrado en instalación inicial consolidada greenfield | Estrategia y rutas exactas se verifican antes de autorizar esquema; no heredar DEC-06 de Pedidos como aprobación específica de Spa ni generar migraciones ahora |

El candidato de §5 debe admitir estas capacidades sin imponer agregados prematuros. El panel, lista de espera y reportes se apoyan en consultas autorizadas de los procesos de Spa; sin motor analítico adicional ni métricas nuevas presentadas como históricas. La interfaz usa patrones oficiales responsive y firma en pantalla; diseño exacto y transporte/evidencia quedan pendientes, sin app u offline.

## 6. Propiedad por módulo y dependencias

| Propietario | Responsabilidad y frontera de Spa |
|---|---|
| Spa | Motor único de disponibilidad/búsqueda, agenda/visitas múltiples, tratamientos/sesiones, expediente único/alertas, consentimiento/fotos, paquetes y operación básica; capacidades aprobadas, mecanismos pendientes |
| MasterData | Identidad BP y rol Customer; Spa consulta proyección autorizada. Fecha de nacimiento/contacto emergencia O-06, sin extensión implícita de ADR-017 |
| Empleados | Ficha, períodos, asignaciones, cargo/profesión/especialidad y vínculo de usuario. Spa no administra estos datos; depende de EMP-E6b probado |
| Items / Pricing | Servicio comercial, precio e impuestos. Spa configura duración/procedimiento según O-11 |
| Ventas / SRI | Factura y línea, autorización, NC/anulación; Spa lee y referencia, no participa en autorización fiscal |
| Caja / CxC / Contabilidad | Caja abierta para facturación y flujos oficiales de pagos/cartera/asientos. Registrar atención no los genera; no tocar FROZEN |
| Inventario | Productos y movimientos oficiales. Insumos documentales sin Kardex; O-12 para cualquier integración adicional |
| Identity / Access / Kernel | Autorización única; claves y perfiles oficiales. Cambiar bypass requiere revisión expresa, no un autorizador paralelo |
| Infraestructura de auditoría/archivos | Reutilización mediante extensión propia del dominio; sin refactor compartido por este ADR |
| Anticipos de Clientes | Proceso transversal independiente aún sin diseño autorizado; posible consumo futuro de su contrato, sin saldo monetario Spa |

## 7. Conflictos y decisiones abiertas

Registro completo y estados parciales: contrato §29/30. O-23 cerrada en inclusión funcional general; O-08 y O-20 resueltas en sus alcances. Esta tabla explica el impacto arquitectónico, no ofrece respuestas nuevas.

| Conflicto / decisión | Estado y condición |
|---|---|
| C-01 / O-01 | Admin: aceptar bypass, permisos sellados, concesión propia o acceso excepcional siguen alternativas. Cambiar LOCKED exige autorización expresa; C+D es recomendación heredada, no selección |
| C-02 / O-05 | Conservación/no borrado y posible eliminación legal no se resuelven por jerarquía técnica. Decisión propietario y asesoría O-07/V-06/V-09 |
| C-08 / O-14 | La propiedad transversal quedó registrada; diseño/operación de anticipos siguen abiertos. No bloquea por sí mismo toda creación de contrato aprobada en O-15; sí impide funciones dependientes de anticipos |
| C-06 | Terminología resuelta por O-25; visita múltiple y borradores/ciclo de sesión incluidos por 116. Representación visita–cita y preparación/atención parcial pendientes; no hay revocación expresa |
| O-02/03/04 | Sucursal, lecturas, cifrado/claves/backups condicionan exposición de datos sensibles |
| O-09..13 | Cálculo de restricciones, modelo de visita/servicio–procedimiento, registro de productos y políticas de paquetes; no inclusión de capacidades ni cabinas/equipos |
| O-16..22 | Firma en pantalla/versiones incluidas; evidencia/ley, menores/académico, numeración, campos y medidas pendientes. Cardinalidad única resuelta O-20 |
| O-23; O-24/V-01 | 115/116 recuperadas y alcance general resuelto; fichas y detalles históricos anteriores sin recuperar siguen como evidencia pendiente, no reabren alcance |
| V-02/03a/04/05/07/08 | Directorio, devolución Service completa, precisión, horarios/locks, infraestructura privada y resolutor requieren evidencia según su estado, no se declaran cerrados |

## 8. Consecuencias y riesgos

La separación agenda/atención evita convertir ausencias en historia clínica. La operación explícita de contrato añade un paso y requiere una consulta comercial, pero conserva la independencia de Ventas ante fallos de Spa. El directorio evita duplicación laboral y obliga a esperar a Empleados. Privacidad de lectura y archivos requiere trabajo nuevo propio del dominio y decisiones antes de admitir datos reales; no basta añadir permisos.

Riesgos abiertos: disponibilidad temporal aún no probada; acceso Admin sin política; retención y firma sin validación aplicable; tratamiento de anulaciones/crédito sin definición; diseño numérico corporal pendiente; material original sin inspección. Ninguno se resuelve excluyendo la función histórica ni inventando aprobación.

## 9. Condiciones de aprobación y habilitación

Para considerar Accepted: integrar el alcance ya aprobado y cierre funcional O-23, aprobar técnicamente contrato y alternativas AT aplicables, resolver política de privacidad/conservación y medidas corporales, definir representación de visita múltiple, preparación/borrador/sesión parcial, cálculo de restricciones y reglas comerciales/operativas que condicionan el modelo y documentar resultados de verificaciones técnicas. Recuperar o dejar expresamente acordado el tratamiento de fuentes no disponibles y obtener validación legal cuando corresponda. Los desacuerdos permanecen visibles; no se transforma una recomendación en decisión.

**Accepted tampoco autorizaría programar.** Cada fase posterior exige autorización expresa, lista exacta de archivos, dependencias comprobadas y gates del [flujo obligatorio](../architecture/enforcement.md#flujo-obligatorio-del-agente-por-fase). Empleados se implementaría bajo su propio plan/autorizaciones. No se reabre ningún módulo CLOSED por mencionar una integración.

## 10. Validación documental

Referencias relativas, coherencia contrato–registro–ADR–plan, guardrail documental y diff. Sin builds ni pruebas de código en la consolidación. El resultado real se registra en STATUS y en el informe de revisión; este ADR no declara fase CLOSED ni autoriza operaciones Git por sí mismo. El staging y commit local requieren instrucción separada del propietario, otorgada para la preservación documental de estos 13 archivos; no se autoriza push ni implementación.

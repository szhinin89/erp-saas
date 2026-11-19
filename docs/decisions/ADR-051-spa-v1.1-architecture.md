# ADR-051 — SPA V1.1: arquitectura del módulo de Gestión de Spa y Tratamientos Estéticos

Estado: **PROPOSED / NO APROBADO** — 2026-11-18. Arquitectura candidata para revisión; no implementada. **Ninguna fase está autorizada**, tampoco Empleados. SPA-DEC-115/116 aportan alcance funcional aprobado; esta integración no aprueba políticas o mecanismos técnicos pendientes ni autoriza cambios FROZEN/CLOSED.

Patrón: [ADR-049](ADR-049-sales-order-v1-architecture.md). Comportamiento y pantallas: [contrato central](SPA-V1.1-CONTRACT-SPEC-01.md); evidencia de decisiones: [registro](SPA-V1.1-APPROVED-DECISIONS-01.md); fases: [plan](SPA-V1.1-IMPLEMENTATION-PLAN-01.md). Entrega y gates: [STATUS](../../STATUS.md); trabajo abierto: [WORK_QUEUE](../agent/WORK_QUEUE.md). Este ADR explica alternativas y límites, no es una segunda especificación funcional ni una SSOT implementada.

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
| SPA-ARQ-01, REG §12 | Visita agrupa una o varias citas del mismo cliente/ocasión/sucursal; cita es bloque de profesional/intervalo; varios bloques de un servicio no duplican prestación comercial; sesiones realizadas independientes y cancelación de cita no propagada | Estados fuera del alcance literal de SPA-ARQ-01, resueltos expresamente por SPA-ARQ-02; número de sesiones clínicas por prestación, sesiones parciales, preparación, concurrencia y ausencias; esquema y vínculos físicos |
| SPA-ARQ-02, REG §13 | Cinco estados de cita, llegada como evento fecha/hora/usuario, atención independiente por cita, Atendida solo con atención documentada; situación derivada y fin de visita con todas las citas finales; reprogramación como evento/indicador visible e historial, conserva identidad/Programada, sin consumo automático | Interrupciones clínicas, sesiones parciales/correcciones, ausencias/penalizaciones, efectos comerciales y concurrencia; sin sexto estado ni máquina de estados propia de visita |
| SPA-ARQ-08, REG §19; contrato §4.2 | Ocho fronteras funcionales ERP aprobadas: BP, Empleados, Items, Ventas, Caja/CxC, Inventario, seguridad/auditoría y SPA genérico multiempresa | Contratos de lectura/mecanismos pendientes, Empleados no implementado, sin extensiones BP/Items/Pricing/Ventas/CxC ni LOCKED/FROZEN; fiscal/ley/O-14 abiertos |
| SPA-ARQ-07, REG §18; contrato §14–18/21 | Diez reglas funcionales aprobadas: valoraciones/mediciones, borradores, comparación, consentimiento previo/firma, tres autorizaciones independientes, fotos privadas, corrección trazable y protección | V-01/04/06/07/08/09 siguen abiertos en originales, precisión, ley, almacenamiento y claves; académico O-17 y menores O-18 no aprobados; sin esquema/API/firma/escala elegidos |
| SPA-ARQ-06, REG §17; contrato §21 | Ocho reglas funcionales aprobadas de expediente único, acceso clínico, Admin excepcional, intersucursal, auditoría, archivos privados, gestión legal e aislamiento | O-01..05 parcialmente resueltas: C-01 mecanismo bloqueado hasta autorización especial LOCKED; C-02 procedimiento/validación legal pendientes. Sin mecanismo seleccionado |
| SPA-ARQ-05, REG §16; contrato §11 | Diez reglas comerciales funcionales aprobadas; composición y saldo por servicio, vigencia, capacidad programada sin consumo, consumo independiente/no duplicado, ausencia sin penalización automática, compensación autorizada, ampliación respaldada, sustitución, documentos oficiales y movilidad intraempresa | O-13 parcialmente resuelta; mecanismos, equivalencias/documento adicional, transferencia, vigencia de ampliaciones, parciales/compensaciones económicas y fiscal/SRI/crédito pendientes |
| SPA-ARQ-04, REG §15; contrato §7.2 | Doce obligaciones funcionales aprobadas de agenda/disponibilidad, incluida revalidación, prevención de compromisos concurrentes incompatibles, conservación ante fallo y no liberación automática por interrupción | Algoritmos, recurrencias/intervalos, tablas, API, locks/exclusiones/índices, contratos con Empleados, configuración de traslados y valores pendientes; sin estados/capacidades nuevas |
| SPA-ARQ-03, REG §14; contrato §12 | Ciclo clínico Borrador/En atención/Finalizada/Interrumpida; inicio con profesional y registrador separados, finalización explícita autorizada, parcial conservada, corrección trazable sujeta a C-02 y evolución completa/parcial; consumo independiente | Persistencia/concurrencia/finalización, mecanismo de versiones y consistencia con agenda; estado final de cita interrumpida, O-13 comercial y conservación legal pendientes |
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

**Los mecanismos técnicos AT-xx siguen propuestos/no aprobados; el modelo conceptual SPA-ARQ-01 y los estados/eventos/situación derivada SPA-ARQ-02 incorporados en AT-04 sí están APROBADOS.** No crean rutas, interfaces ni clases en código. El contrato §24 conserva entidades candidatas y §25 capacidades conceptuales, sin aprobar esquema o API.

| ID | Candidato y motivo | Alternativa / condición |
|---|---|---|
| AT-01 | Módulo Spa en Domain/Application/Infrastructure y frontend propio, con procesos integrados de agenda/visitas, atención/tratamientos/sesiones, paquetes y operación inicial/espera/reportes, sin módulo independiente por característica; separa datos mínimos de agenda de recursos sensibles; SPA-ARQ-08 §4.2 regla 8 fija módulo genérico multiempresa con interfaz SPA, sin dependencia de marca ni responsabilidades duplicadas; capas/entidades siguen candidatas | Límites definitivos de agregados y nombres técnicos tras revisión; no crear otra infraestructura común |
| AT-02 | Identificadores de BP, Employee, Item y documentos comerciales; lectura mediante contratos públicos del propietario | Directorio Empleados previo; contrato de lectura de Ventas a confirmar. Sin repositorios cruzados ni copia de identidad/precios/impuestos; SPA-ARQ-08 aprueba fronteras, no firmas/APIs; BP tenant/Customer, lectura laboral oficial IEmployeeDirectory pendiente bajo ADR-050, Item Service/precios/impuestos oficiales; sin extender propietarios |
| AT-03 | SPA-ARQ-06 §21 fija expediente cliente/empresa, sucursal por atención, acceso intersucursal contextual e aislamiento de relaciones oficiales, sin cambiar scope BP tenant | Clasificación de agregados/Branch Ownership Rule, claves/filtros y autorización intersucursal son diseño pendiente. Ni CompanyUserBranch ni asignación laboral conceden acceso clínico por sí solas; SPA-ARQ-08 exige contexto tenant/empresa/sucursal y sin traslado de derechos entre empresas; no cambia ámbito BP ni aprueba filtros/claves físicos |
| AT-04 | Modelo conceptual APROBADO SPA-ARQ-01: visita agrupa una o varias citas del mismo cliente/ocasión/sucursal; cita es bloque de profesional/intervalo. Un servicio multiprofesional usa varios bloques sin duplicar prestación; cancelación local sin cancelar las demás citas | Propuestas técnicas: entidades/agregados, claves, persistencia y vínculo bloque–prestación. Estados de cita y situación derivada de visita resueltos SPA-ARQ-02; cardinalidad clínica por prestación, mecanismos del ciclo SPA-ARQ-03 (§5.3), configuración técnica de preparación y mecanismos de concurrencia SPA-ARQ-04 (§5.4), y mecanismos de ausencia según SPA-ARQ-05 pendientes. Agenda única; sin confirmación automática ni cabinas/equipos |
| AT-05 | SPA-ARQ-04 exige revalidar y prevenir compromisos incompatibles concurrentes; mecanismos candidatos: locks por profesionales, exclusión de intervalos, orden canónico y token optimista | Contrato §27 ilustrativo: scope TenantId/CompanyId y coordinación entre sucursales por diseñar sin permitir compromisos incompatibles del mismo profesional. Transacciones, índices, tstzrange y contratos con Empleados pendientes de revisión/prueba real. Sin SQL aprobado |
| AT-06 | Contrato y referencias comerciales conforme O-15 y SPA-ARQ-05 §11; ampliaciones respaldadas conservan contrato/historial, NC/anulación exige revisión y ajuste autorizado sin alterar clínica | `PackageContract`, unicidad por factura/línea y claves/reintentos son candidatos técnicos. Relación nuevas líneas, fiscal/SRI/crédito y ajustes económicos pendientes. Transacción separada de autorización; sin creación por SalesInvoiceAuthorizedEvent ni documentos financieros Spa; SPA-ARQ-08 ratifica siete reglas O-15 y ajustes SPA-ARQ-05; no resuelve SRI/crédito/NC específicos ni aprueba lectura pública definitiva |
| AT-07 | SPA-ARQ-05 fija composición/saldos por servicio, vigencia desde creación, cupos programados sin consumo, movimientos independientes/no duplicados, compensaciones autorizadas, ampliaciones respaldadas, requisitos de sustitución y aislamiento por empresa | Libro append-only, tipos de movimiento, índices/locks, algoritmo/reserva lógica/saldo, jobs de vencimiento, contratos API y modelo físico son candidatos. O-13 no cerrada: equivalencias/documento adicional, transferencia, vigencia tras ampliación, parciales/compensaciones económicas y fiscal/crédito; no penalización automática por ausencia ni reversión automática de consumo |
| AT-08 | SPA-ARQ-03 aprueba el ciclo del registro clínico y conservación diferenciada de parcial, con referencias al expediente/tratamiento y consumo independiente. Estados clínicos separados de agenda, sin imponer entidades nuevas; SPA-ARQ-07 aprueba contenido de valoraciones, borradores, mediciones/finalidad y comparación sin resultados inventados | Diseño técnico de persistencia de borradores y registro, cierre/concurrencia, versiones y consistencia con agenda pendientes; no reaperturas/anulaciones ni efectos comerciales inferidos. C-02/O-13 siguen abiertos; sin escalas nuevas; versionado/borradores y mediciones/precisión pendientes de diseño, V-01/V-04 sin cierre |
| AT-09 | SPA-ARQ-06 §21.1 regla 5 exige auditoría sensible de consultas/cambios/fotos/consentimientos y metadatos sin contenido clínico | Entity Audit oficial FROZEN; extensión de consultas exitosas propia de Spa solo candidata. CareAccessLog, persistencia/confiabilidad, granularidad, modo síncrono/asíncrono/mixto, fallos y retención pendientes de aprobación técnica; SPA-ARQ-08 exige reutilización oficial; extensión sensible candidata, sin auditoría paralela ni modificación FROZEN inferida |
| AT-10 | SPA-ARQ-06 §21.1 regla 6 exige archivos privados, acceso autorizado sin enlaces públicos permanentes, protección/cifrado adecuado, claves, trazabilidad y aislamiento; SPA-ARQ-07 añade asociaciones/metadatos fotográficos y respeto de finalidad | ICareFileStore sobre IFileStorage candidato, sin modificar IFileStorage/LocalFileStorage/scripts por inferencia. Cifrado/campos, custodia/rotación/recuperación de claves, almacenamiento/descifrado/restauración/backups pendientes de revisión técnica/operativa. Sin sistema nuevo de respaldo; evidencia de firma/consentimiento, identidad y vigencia requieren revisión; no firma certificada inferida, académico O-17 abierto; SPA-ARQ-08 no autoriza cambiar IFileStorage ni infraestructura compartida |
| AT-11 | SPA-ARQ-06 §21 exige facultades clínicas expresas y Admin sin acceso ordinario por su condición; acceso excepcional controlado, sin autenticación/permisos paralelos | C-01/O-01 POLÍTICA FUNCIONAL APROBADA — MECANISMO TÉCNICO BLOQUEADO HASTA AUTORIZACIÓN ESPECIAL; LOCKED intacto. No basta spa.*; permisos sellados, concesiones y acceso excepcional candidatos sin elegir, catálogo Kernel técnico pendiente; SPA-ARQ-08 conserva integración oficial sin usuarios/autenticación/permisos generales paralelos; C-01/C-02 abiertos |
| AT-12 | Modelo futuro integrado en instalación inicial consolidada greenfield | Estrategia y rutas exactas se verifican antes de autorizar esquema; no heredar DEC-06 de Pedidos como aprobación específica de Spa ni generar migraciones ahora |

El candidato de §5 debe admitir estas capacidades sin imponer agregados prematuros. El panel, lista de espera y reportes se apoyan en consultas autorizadas de los procesos de Spa; sin motor analítico adicional ni métricas nuevas presentadas como históricas. La interfaz usa patrones oficiales responsive y firma en pantalla; diseño exacto y transporte/evidencia quedan pendientes, sin app u offline.

### 5.1 Modelo aprobado y alternativas técnicas pendientes

SPA-ARQ-01, aprobada por el propietario el 2026-11-19 ([REG §12](SPA-V1.1-APPROVED-DECISIONS-01.md#12-spa-arq-01--modelo-conceptual-de-visitas-citas-y-sesiones)), resuelve la alternativa conceptual: visita que agrupa una o varias citas, cada una como bloque de profesional e intervalo, para el mismo cliente/ocasión/sucursal. No se mantiene como alternativa abierta sustituir esa agrupación por una sola cita indiferenciada de varios profesionales.

La prestación comercial no se duplica por requerir varios bloques/profesionales. Cancelar una cita no cancela automáticamente las demás; no fija por ello el estado de la visita. La atención efectiva y su registro clínico siguen independientes, con reserva/reprogramación sin consumo y consumo comercial separado conforme a O-25.

Quedan propuestas la descomposición en entidades/agregados/tablas, claves, contratos públicos/API/DTO y representación del vínculo bloque–prestación. No se elige un esquema físico ni mecanismo de concurrencia. Los estados se aprobaron expresamente en SPA-ARQ-02 (§5.2); SPA-ARQ-03 (§5.3) resuelve el ciclo clínico y conservación parcial; número de sesiones por prestación, valores/configuración técnica de preparación y mecanismos de concurrencia conforme SPA-ARQ-04 (§5.4), y detalle técnico de ausencias sigue pendiente (sin penalización automática V1.1, SPA-ARQ-05 §5.5). ADR-051 permanece **PROPOSED / NO APROBADO**; ninguna fase autorizada.

### 5.2 Estados/eventos aprobados SPA-ARQ-02 y límites técnicos

**APROBADA 2026-11-19**, incluida su confirmación de reprogramación; texto exacto en [REG §13](SPA-V1.1-APPROVED-DECISIONS-01.md#13-spa-arq-02--estados-de-citas-situación-de-visitas-y-reprogramación), comportamiento en contrato §8.6. Cita: `Programada`, `En atención`, `Atendida`, `Cancelada`, `Ausente`, con inicio/fin independiente. Atendida requiere atención efectiva documentada; interrupción no completa automáticamente.

La visita conserva la agrupación SPA-ARQ-01. Llegada es evento con fecha/hora/usuario; situación general derivada de citas, **sin máquina de estados independiente**. Finaliza cuando todas sus citas tienen estado final. Modificar/cancelar una no cancela automáticamente toda la visita.

`Reprogramada` es evento e indicador de agenda, nunca un sexto estado. Reprogramación válida conserva identidad/Programada y registra fecha/hora anterior/nueva, profesional anterior/nuevo cuando corresponda, autor/momento; indicador visible y consulta del historial. No se consumen paquetes automáticamente por transiciones o reprogramación.

Continúan propuestos el esquema de almacenamiento de eventos/indicador, consultas de situación derivada, contratos/API y concurrencia. El ciclo clínico de interrupción/parcial/corrección quedó aprobado después en SPA-ARQ-03 (§5.3), sin modificar SPA-ARQ-02; continúan pendientes su persistencia, estado final de cita interrumpida, mecanismos de ausencia sin penalización automática y cancelaciones/efectos comerciales restantes de SPA-ARQ-05, y concurrencia. Tampoco se crea una segunda máquina de estados para visita. ADR-051 permanece **PROPOSED / NO APROBADO** y F0–F8 no autorizadas; no cambia el orden ni las dependencias del plan.

### 5.3 Ciclo clínico aprobado SPA-ARQ-03 y diseño técnico pendiente

**Decisión funcional APROBADA 2026-11-19.** Reglas literales en [REG §14](SPA-V1.1-APPROVED-DECISIONS-01.md#14-spa-arq-03--ciclo-de-atención-clínica); única referencia funcional principal en contrato §12. Condiciona el diseño a conservar borrador editable sin atención acreditada, inicio contextual con profesional/registrador separados, finalización explícita autorizada, interrupción/parcial diferenciadas y corrección/evolución trazables, sin consumo comercial determinado por clínica.

El registro clínico usa los conceptos Borrador, En atención, Finalizada e Interrumpida. Son distintos de los cinco estados de cita de SPA-ARQ-02; En atención compartido como nombre no implica sincronización automática. Parcial debe conservarse en expediente/seguimiento; **el estado final de la cita interrumpida sigue pendiente**, sin Atendida automático ni sexto estado de cita.

Se mantienen candidatos la descomposición preparación/registro, persistencia de borradores, metadatos de inicio, transacciones/finalización, concurrencia, mecanismos de corrección/versiones y consistencia agenda–clínica. Esta aprobación no impone entidades, tablas, interfaces ni almacenamiento append-only absoluto. No inventar reapertura, anulación clínica o estados adicionales. Corrección autorizada conserva trazabilidad bajo política legal pendiente: C-02 no se cierra.

SPA-ARQ-05 (§5.5; contrato §11) precisa consumo/compensación y excluye penalización automática por ausencia; detalles restantes siguen O-13, sin descontar una sesión completa o conceder gratuidad por inferencia. Consentimiento/firma/menores, Admin excepcional, cifrado/claves y auditoría técnica continúan abiertos según sus contratos; ninguna excepción nueva. ADR-051 permanece **PROPOSED / NO APROBADO**, sin autorización F0–F8.

### 5.4 Agenda/disponibilidad aprobada SPA-ARQ-04 y alternativas pendientes

**Condicionante funcional APROBADO**, literal y límites en [REG §15](SPA-V1.1-APPROVED-DECISIONS-01.md#15-spa-arq-04--agenda-y-disponibilidad). Referencia normativa única: [contrato §7.2](SPA-V1.1-CONTRACT-SPEC-01.md#72-reglas-aprobadas-spa-arq-04); AT-04/05 deben satisfacerla sin reabrir SPA-ARQ-01/02/03 ni O-15/O-25.

Distinguir obligaciones funcionales de alternativas técnicas: quedan por elegir entidades/tablas, representación de horarios e intervalos recurrentes, cálculo centralizado, persistencia de duración/preparación, configuración de traslados y excepciones, contratos de consulta con Empleados, transacciones, claves/orden de locks, exclusiones/índices PostgreSQL y contratos API. El diseño debe coordinar compromisos del mismo profesional entre sucursales y resolver horas inexistentes/ambiguas consistentemente con UTC y la configuración horaria oficial existente; no se aprueba ampliar zonas por sucursal ni capacidades de Empleados.

No se eligen algoritmos, interfaces o SQL. No se autorizan duración predeterminada, márgenes adicionales, rutas externas, reasignación automática, estados nuevos o liberación manual tras interrupción. El estado final de cita interrumpida sigue abierto; ausencia de liberación automática y separación comercial son obligaciones funcionales, sin alterar el ciclo clínico. Sin segundo motor, cabinas/equipos, recordatorios ni reservas públicas. **ADR-051 PROPOSED / NO APROBADO; F0–F8 PENDING / NO AUTORIZADA.**

### 5.5 Tratamientos y paquetes SPA-ARQ-05: condicionante aprobado

**APROBADA funcionalmente**, texto en [REG §16](SPA-V1.1-APPROVED-DECISIONS-01.md#16-spa-arq-05--tratamientos-y-paquetes-comerciales), referencia normativa principal [contrato §11](SPA-V1.1-CONTRACT-SPEC-01.md#11-paquetes-comerciales). AT-06/07 deben satisfacer las diez reglas; no son una elección de entidades, tablas, tipos de movimientos o API.

Diseño pendiente: representación de servicios/cantidades y referencias comerciales, vigencia/cálculo temporal/estado/jobs, movimientos y compensaciones, reserva lógica de capacidad de programación sin `Consumed`, algoritmo de saldo por servicio, concurrencia de programación/consumo/ampliación/sustitución y prevención de duplicados; aislamiento por empresa y coordinación entre sucursales sin segundo motor de agenda ni consumo entre empresas. No se adopta llave, índice, lock, entidad o interfaz definitiva.

O-13 queda parcialmente resuelta: mantener abiertos solo detalles reales de §11.3/11.4, incluidas equivalencias/documento adicional de sustitución, transferencia de beneficiario, vigencia tras ampliación, resolución comercial de parciales, compensaciones económicas y fiscal/SRI/crédito. V1.1 no penaliza automáticamente ausencias ni revierte consumos por cancelar/corregir clínica; no inventar derechos gratuitos. O-15/O-25, seguridad/legal y DIR-SSOT-01 permanecen intactos. **ADR-051 PROPOSED / NO APROBADO; ninguna fase autorizada.**

### 5.6 Expedientes y seguridad SPA-ARQ-06: objetivos y límites técnicos

Decisión funcional **APROBADA**, literal en [REG §17](SPA-V1.1-APPROVED-DECISIONS-01.md#17-spa-arq-06--expedientes-y-seguridad). Referencia normativa única: [contrato §21](SPA-V1.1-CONTRACT-SPEC-01.md#21-seguridad-y-privacidad). AT-03/09/10/11 deben satisfacer las ocho reglas; las aprobaciones anteriores permanecen intactas.

Separar política aprobada, mecanismo pendiente y evidencia jurídica pendiente: C-01/O-01 exige autorización especial para revisar el bypass LOCKED; ningún autorizador, permiso sellado, concesión o mecanismo excepcional queda elegido. C-02/O-05 exige procedimiento controlado y validación legal, sin borrado general ni conservación absoluta que sustituya derechos. Persistencia/confiabilidad de lecturas, fallos de auditoría, autorización intersucursal, almacenamiento, cifrado/campos/claves y operación de restauración/backups siguen abiertos. No modificar infraestructura FROZEN, Identity/Access/Kernel, scopes BP ni capacidades de Empleados.

La protección y auditoría necesarias deben estar disponibles y validadas antes de operar datos clínicos reales. Esta obligación no acepta técnicamente el ADR ni autoriza datos reales, implementación o fase alguna: **PROPOSED / NO APROBADO; F0–F8 PENDING / NO AUTORIZADA**.

### 5.7 SPA-ARQ-07: valoraciones, evidencia y pendientes

Condicionante funcional **APROBADO**, reglas literales en [REG §18](SPA-V1.1-APPROVED-DECISIONS-01.md#18-spa-arq-07--valoraciones-consentimientos-y-fotografías). Referencia normativa única: contrato §14.1/15.1/16/17.1/18/21.4; no constituye esquema implementado.

Quedan candidatos la descomposición expediente/valoración/medición, persistencia de borradores y versiones/correcciones, lectura comparativa, contratos API, entidades de consentimiento/firma/fotografía y almacenamiento privado/auditoría. No se eligen tablas, interfaces, mecanismos de firma ni escalas. La independencia de procedimiento/seguimiento/promoción no incorpora uso académico; menores/representantes y requisitos jurídicos siguen abiertos. Correcciones y retiros no implican conservación indefinida ni resolución de C-02.

**Bloqueos vigentes:** V-01 originales/versiones no contrastados; V-04 precisión/escala oficial para medidas; V-06 y V-09 validación jurídica aplicable, firma, menores y conservación; V-07 infraestructura/almacenamiento/cifrado; V-08 contratos/protección de archivos aún por verificar según su alcance. No cierran por aprobar contenido funcional. C-01/LOCKED y C-02 mantienen sus gates especiales. No ampliar BP, Empleados, Items, Ventas ni infraestructura por inferencia. Condiciones necesarias de seguridad/cumplimiento resueltas y verificadas antes de datos clínicos reales.

**ADR-051 PROPOSED / NO APROBADO; F0–F8 PENDING / NO AUTORIZADA.** Ninguna implementación autorizada.

### 5.8 SPA-ARQ-08: fronteras ERP aprobadas, contratos pendientes

**APROBADA POR EL PROPIETARIO**, texto en [REG §19](SPA-V1.1-APPROVED-DECISIONS-01.md#19-spa-arq-08--integraciones-con-erp-zh); referencia normativa única [contrato §4](SPA-V1.1-CONTRACT-SPEC-01.md#4-dependencias-y-límites-con-otros-módulos). AT-01/02/03/06/09/10/11 quedan condicionadas por las ocho reglas, sin entidades, APIs o extensiones nuevas aprobadas.

Dependencias: contratos autorizados de consulta de cada propietario, IEmployeeDirectory implementado/probado bajo ADR-050 y autorización propia de Empleados, lectura comercial de factura/línea e información autorizada Caja/CxC; datos operativos propios SPA sin duplicar BP/Employee/Items/precios/impuestos. Contratos concretos, scopes persistidos, idempotencia, índices/locks/algoritmos y archivos/auditoría siguen candidatos a revisión. No preparación de código ni implementación de dependencias por aprobar fronteras.

Mantener SPA genérico multiempresa y denominación SPA en interfaz; Gali Spa solo atribución de evidencia histórica. No tocar BP tenant, Items/Pricing/Ventas/CxC, Kernel LOCKED, auditoría FROZEN o IFileStorage por inferencia. C-01/O-01 mecanismo bloqueado hasta autorización especial, C-02/O-05 procedimiento/ley pendientes, O-14 proceso independiente y fiscal/SRI/crédito/NC específicos abiertos. **ADR-051 PROPOSED / NO APROBADO; F0–F8 PENDING / NO AUTORIZADA.**

## 6. Propiedad por módulo y dependencias

SPA-ARQ-08 aprueba las fronteras funcionales según contrato §4.2; esta tabla asigna responsabilidades, no contratos técnicos implementados. Cada integración utiliza contratos autorizados del propietario; directorio Empleados y firmas/lecturas públicas siguen pendientes de evidencia y autorización propia.

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

SPA-ARQ-08 no cierra dependencias técnicas: IEmployeeDirectory/V-02 y lecturas públicas comerciales V-03, mecanismos de aislamiento/concurrencia/archivos/auditoría, C-01/C-02 y fiscal/ley permanecen abiertos; O-14 independiente. No ampliar módulos por inferencia.

Registro completo y estados parciales: contrato §29/30. O-23 cerrada en inclusión funcional general; O-08 y O-20 resueltas en sus alcances. Esta tabla explica el impacto arquitectónico, no ofrece respuestas nuevas.

| Conflicto / decisión | Estado y condición |
|---|---|
| C-01 / O-01 | **POLÍTICA FUNCIONAL APROBADA — MECANISMO TÉCNICO BLOQUEADO HASTA AUTORIZACIÓN ESPECIAL.** SPA-ARQ-06: Admin sin acceso clínico ordinario por su condición; excepcional justificado/autorizado/auditable/delimitado. Aceptar bypass ordinario no satisface objetivo; B/C/D no seleccionados, C+D recomendación no aprobada. Conflicto técnico abierto, LOCKED intacto |
| C-02 / O-05 | **OBJETIVO FUNCIONAL APROBADO — PROCEDIMIENTO Y VALIDACIÓN LEGAL PENDIENTES.** SPA-ARQ-06: correcciones/versiones y medidas legales controladas. No-borrado no resuelve derechos por sí solo; sin borrado físico general ni cumplimiento certificado. O-07/V-06/V-09 abiertos |
| C-08 / O-14 | La propiedad transversal quedó registrada; diseño/operación de anticipos siguen abiertos. No bloquea por sí mismo toda creación de contrato aprobada en O-15; sí impide funciones dependientes de anticipos |
| C-06 | Terminología resuelta por O-25; visita múltiple y borradores/ciclo de sesión incluidos por 116. Modelo visita–citas resuelto SPA-ARQ-01; esquema/vínculos técnicos, presentación de situación derivada SPA-ARQ-02 y mecanismos del ciclo clínico aprobado SPA-ARQ-03 pendientes; no hay revocación expresa |
| O-02/03/04 | SPA-ARQ-06 aprueba acceso intersucursal justificado, auditoría sensible sin contenido clínico y protección privada/cifrado/claves. Autorización detallada, persistencia/confiabilidad/fallos y diseño técnico/operativo de archivos/backups siguen pendientes antes de exponer datos reales |
| O-09..13 | Restricciones SPA-ARQ-04 y paquetes SPA-ARQ-05 aprobados funcionalmente; pendientes sus mecanismos técnicos y cuestiones restantes de contrato §11.3/11.4, relación servicio–procedimiento y productos. O-13 parcialmente resuelta, sin reabrir ausencia automática, intercambio libre, inicio de vigencia o movilidad intraempresa |
| O-16..22 | SPA-ARQ-07 aprueba documento previo cuando requerido, evidencia funcional/firma, finalidades independientes, contenido de valoración/medición y borradores. Persistencia/versión/firma, vigencia/identidad, precisión V-04, menores O-18, académico O-17, numeración y obligatoriedad por procedimiento siguen pendientes; no reabrir alcance ni cerrar V-01 |
| O-23; O-24/V-01 | 115/116 recuperadas y alcance general resuelto; fichas y detalles históricos anteriores sin recuperar siguen como evidencia pendiente, no reabren alcance |
| V-02/03a/04/05/07/08 | Directorio, devolución Service completa, precisión, horarios/locks, infraestructura privada y resolutor requieren evidencia según su estado, no se declaran cerrados |

## 8. Consecuencias y riesgos

La separación agenda/atención evita convertir ausencias en historia clínica. La operación explícita de contrato añade un paso y requiere una consulta comercial, pero conserva la independencia de Ventas ante fallos de Spa. El directorio evita duplicación laboral y obliga a esperar a Empleados. Privacidad de lectura y archivos requiere trabajo nuevo propio del dominio y decisiones antes de admitir datos reales; no basta añadir permisos.

Riesgos abiertos: disponibilidad temporal aún no probada; mecanismo Admin bloqueado pese a política funcional SPA-ARQ-06 aprobada; retención y firma sin validación aplicable; tratamiento de anulaciones/crédito sin definición; diseño numérico corporal pendiente; material original sin inspección. Ninguno se resuelve excluyendo la función histórica ni inventando aprobación.

## 9. Condiciones de aprobación y habilitación

Contrastar diseño técnico con fronteras SPA-ARQ-08/contrato §4.2 y verificar contratos de lectura autorizados, dependencia oficial Empleados y scopes/propietarios antes de futura integración. Aprobación funcional no implementa contratos, prepara código ni autoriza modificaciones compartidas o fases.

Para considerar Accepted: integrar el alcance ya aprobado y cierre funcional O-23, aprobar técnicamente contrato y alternativas AT aplicables, resolver política de privacidad/conservación y medidas corporales, definir esquema y vínculos técnicos del modelo visita–citas aprobado por SPA-ARQ-01, presentación técnica de situación derivada SPA-ARQ-02, cardinalidad clínica por prestación, persistencia/concurrencia del ciclo SPA-ARQ-03 y estado final de cita interrumpida, cálculo de restricciones y reglas comerciales/operativas que condicionan el modelo y documentar resultados de verificaciones técnicas. Recuperar o dejar expresamente acordado el tratamiento de fuentes no disponibles y obtener validación legal cuando corresponda. Los desacuerdos permanecen visibles; no se transforma una recomendación en decisión.

**Accepted tampoco autorizaría programar.** Cada fase posterior exige autorización expresa, lista exacta de archivos, dependencias comprobadas y gates del [flujo obligatorio](../architecture/enforcement.md#flujo-obligatorio-del-agente-por-fase). Empleados se implementaría bajo su propio plan/autorizaciones. No se reabre ningún módulo CLOSED por mencionar una integración.

### 9.1 Cambios de arquitectura que exigen autorización especial

Mecanismos candidatos heredados, no seleccionados por SPA-ARQ-06. Sus objetivos funcionales están aprobados en contrato §21; cada cambio conserva los gates especiales de esta tabla. No se autoriza implementación ni modificación LOCKED/FROZEN. C-01 exige revisión especial; C-02 procedimiento y validación legal.

| Cambio | Módulos afectados | Autorización especial | Alternativa que lo origina |
|---|---|---|---|
| Permisos «sellados» sin bypass del Admin | Kernel, Access, sesión, navegación, frontend | **Sí**: revisión arquitectónica explícita del contrato LOCKED | §3.3 B |
| Entidad de concesión de acceso clínico | Spa e integración Identity/Access/Kernel | Diseño no aprobado; no elude revisión especial C-01/LOCKED aunque la entidad sea propia de Spa | §5.6; C/D candidatas |
| Entidad `CareAccessLog` | Spa | No (nueva en Spa); sin tocar la infraestructura FROZEN | §4 |
| Adaptador `ICareFileStore` sobre `IFileStorage` | Spa, Infrastructure | No si es nuevo; sí si cambia `LocalFileStorage` | §5 |
| Cifrado de aplicación y gestión de claves | Spa, Infrastructure, operación | Posible (claves externas, despliegue) | §5.3 |
| Respaldos cifrados | `scripts/backup-restore.ps1`, operación | **Sí**: cambiar scripts de operación | §5 |
| Procedimiento de eliminación legal | Spa, política | Objetivo aprobado SPA-ARQ-06; procedimiento y validación jurídica pendientes C-02/O-05, sin borrado general autorizado | §5.6 y §7 |
| Campos propios de nacimiento y emergencia | Spa | No | §8 A |
| Ampliar BusinessPartner | MasterData | **Sí**: ADR nuevo, módulo FROZEN | §8 B |
| Empleados implementado | Empleados | Autorizaciones EMP-E1 a E6b | SPA-SEC-R-09 |

## 10. Validación documental

Referencias relativas, coherencia contrato–registro–ADR–plan, guardrail documental y diff. Sin builds ni pruebas de código en la consolidación. El resultado real se registra en STATUS y en el informe de revisión; este ADR no declara fase CLOSED ni autoriza operaciones Git por sí mismo. El staging y commit local requieren instrucción separada del propietario, otorgada para la preservación documental de estos 13 archivos; no se autoriza push ni implementación.

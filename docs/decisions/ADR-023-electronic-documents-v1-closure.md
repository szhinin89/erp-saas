# ADR-023: ElectronicDocuments v1.0 — Cierre de Módulo (FROZEN core, mantenimiento)

## Status

**Accepted — FROZEN.** 2026-07-11. El núcleo funcional del módulo `ElectronicDocuments` (facturación electrónica SRI Ecuador, esquema offline) queda cerrado y pasa a mantenimiento. No se agregan funcionalidades nuevas al núcleo salvo las cuatro excepciones listadas en "Cambios permitidos".

## Contexto

`ElectronicDocuments` se implementó en fases sucesivas (Fase 8 recepción, Fase 9 autorización, auditoría SRI Fase 2 — códigos SOAP-01/02, EST-01, AUTH-01, FIRMA-01/02) y fue sometido a tres rondas de estabilización antes de este cierre:

1. **Auditoría de robustez** (Fase H1/H2) — 2 críticos y 3 altos confirmados con evidencia y reproducción, todos corregidos: TIMEOUT deadlletering prematuro, pipeline sin manejo de excepciones, Hangfire sin guard de concurrencia, IDOR de Company Scope en reintento manual, excepción 503 en vez de 409 en carrera de registro concurrente.
2. **Validación de cumplimiento del Anexo Técnico SRI** — verificado texto por texto contra el PDF oficial (`docs/FICHA TECNICA COMPROBANTES ELECTRONICOS ESQUEMA OFFLINE Versio232.pdf`): clave de acceso (algoritmo módulo 11 reproducido bit a bit), firma XAdES-BES, recepción, autorización, código 70, catálogo de errores (reescrito con los 33 códigos reales, 6 códigos fabricados eliminados).
3. **Pruebas reales contra el ambiente de Pruebas del SRI** (`celcer.sri.gob.ec`) — 8 comprobantes reales emitidos con certificado real, incluyendo un rechazo real confirmado (código `[65]` Fecha de emisión extemporánea, coincidente exactamente con el catálogo corregido en el paso 2) y validación en vivo del fix de TIMEOUT (recuperación real de un documento que había quedado en `DeadLetter` antes del fix).

Con las tres rondas cerradas y la suite en verde, el módulo cumple el criterio de cierre.

## Decisión

Declarar `ElectronicDocuments` v1.0 como **núcleo FROZEN, módulo en mantenimiento**, con el siguiente contrato documentado.

### Responsabilidades

- Generar el XML de un comprobante electrónico SRI a partir de un modelo de datos común (`ElectronicDocumentData`), provisto por el módulo de negocio de origen (hoy: Ventas).
- Validar el XML contra el XSD oficial correspondiente antes de firmar.
- Firmar el XML bajo el estándar XAdES-BES (RSA-SHA1, C14N, tres `Reference` protegidas).
- Enviar el XML firmado al servicio de Recepción del SRI y a Autorización, respetando el protocolo asíncrono (RECIBIDA → esperar → consultar autorización).
- Persistir el ciclo de vida completo del comprobante (estado, clave de acceso, número de autorización, XML en cada etapa) de forma que ningún comprobante quede invisible ante un fallo.
- Reintentar automáticamente (Hangfire) los comprobantes varados, con backoff y límite de intentos, hasta `DeadLetter` reversible.
- Exponer un Monitor de consulta (dashboard, lista, detalle, timeline, reintento manual) para operación humana.

### Límites (lo que este módulo explícitamente NO hace)

- **No conoce información comercial del documento de origen** (cliente, ítems, impuestos, totales) — solo `SourceModule` + `SourceEntityId`, sin FK física. El desacoplamiento es deliberado (ver comentario en `ElectronicDocument.cs`).
- **No calcula impuestos** — consume el modelo ya calculado por el módulo de origen (Regla 2 de "Configuración Tributaria" en `CLAUDE.md`).
- **No decide números de secuencia** — delega en `IDocumentSequenceRepository.CaptureNextAsync` (infraestructura FROZEN, ADR-019).
- **No implementa builders para todos los tipos de comprobante** — solo `Invoice` tiene `IElectronicDocumentXmlBuilder` real hoy; CreditNote/DebitNote/ShippingGuide/Retention/PurchaseSettlement tienen XSD embebidos y catálogo de manifiesto, pero sin builder/provider/validador activo (`activeVersion: null` en `manifest.json`). Extenderlos es trabajo nuevo, no mantenimiento.
- **No es la fuente de verdad de catálogos SRI** — consume `sri_vat_rate`, `sri_ice_rate`, `sri_doc_type`, `sri_environment`, `sri_error_code`, etc., pero no los gestiona (son catálogos globales, tenant-independientes).

### Dependencias

| Dependencia | Naturaleza | Estado |
|---|---|---|
| `IDocumentSequenceRepository` | Infraestructura FROZEN (ADR-019) | congelada |
| `NewChildEntityTrackingInterceptor` | Infraestructura FROZEN (ADR-020) | congelada (no aplica directamente — `ElectronicDocument` no tiene colecciones hijas) |
| `IAuditWriter<ElectronicDocumentAudit>` / Entity Audit | Infraestructura FROZEN (ADR-022) | congelada |
| `IDatabaseExceptionTranslator` | Compartida, usada para traducir violaciones de unicidad | estable |
| Catálogos SRI (`sri_vat_rate`, `sri_ice_rate`, `sri_doc_type`, `sri_environment`, `sri_error_code`, `sri_payment_method`, etc.) | Datos globales, schema `global` | estable, verificados contra Ficha Técnica |
| `SriSettings` / certificado .p12 (`IFileStorage`) | Configuración por empresa | estable |
| Módulo Ventas (`SalesInvoiceElectronicDocumentDataProvider`) | Único consumidor real hoy vía `IElectronicDocumentDataProvider` | acoplamiento por interfaz, no por referencia directa |
| Hangfire (`IServiceScopeFactory`, recurring job) | Infraestructura de jobs | estable |
| `celcer.sri.gob.ec` / `cel.sri.gob.ec` (SOAP externo) | Servicio del SRI, fuera de control del equipo | externo |

### Interfaces públicas (contrato congelado)

```csharp
IElectronicDocumentIssuer.RegisterAsync(RegisterElectronicDocumentRequest, ct) -> Result<ElectronicDocumentDto>
IElectronicDocumentIssuer.RetryAsync(tenantId, electronicDocumentId, userId, ct) -> Result<ElectronicDocumentDto>

IElectronicDocumentDataProvider          // implementado por cada módulo de origen (hoy: Sales)
IElectronicDocumentXmlBuilder            // uno por (tipo de documento)
IElectronicDocumentSchemaValidator       // uno por (tipo de documento)
IElectronicDocumentSigner                // XAdES-BES
IElectronicDocumentReceptionService      // SRI Recepción
IElectronicDocumentAuthorizationService  // SRI Autorización
ISriConnectivityChecker / ISriCertificateInspector

GET  /api/v1/electronic-documents
GET  /api/v1/electronic-documents/dashboard
GET  /api/v1/electronic-documents/{id}
GET  /api/v1/electronic-documents/{id}/timeline
GET  /api/v1/electronic-documents/xml?sourceModule&sourceEntityId&variant
POST /api/v1/electronic-documents/{id}/retry
POST /api/v1/electronic-documents/register   (backfill)
```

Estos nombres, firmas y rutas no cambian sin una nueva ADR.

### Estados (máquina congelada)

```
Draft ──┬──► Failed ──┐
        │             │ (reintento pipeline)
        └──► XmlGenerated ──► Signed ──► Sent ──┬──► Received ──┬──► Authorized ──► Cancelled
                                                 │               ├──► Rejected
                                                 └──► Rejected   └──► DeadLetter ──(Reactivate)──► [estado previo]
```

- `Failed`: fallo en cualquier etapa antes de la firma (proveedor, XML, XSD, firma, storage). Reintentable, incrementa `RetryCount`.
- `DeadLetter`: solo tras agotar `ElectronicDocumentRetryPolicy.MaxAttempts` (5, backoff 1-16 min) desde `Failed`/`Signed`/`Sent`/`Received` — **nunca** por un único `TIMEOUT` de consulta de autorización (corregido en la auditoría de esta sesión). `PreDeadLetterState` permite reversión exacta vía `Reactivate()`.
- `Rejected`/`Authorized`/`Cancelled`: terminales.

### Pipeline

```
Provider.GetDataAsync → XmlBuilder.Build → SchemaValidator.ValidateAsync → Signer.SignAsync
  → XmlStorageService.StoreAsync → MarkXmlGenerated/MarkSigned (checkpoint BD)
  → ReceptionService.SendAsync → MarkSent/MarkReceived (checkpoint BD)
  → AuthorizationService.CheckAsync → MarkAuthorized/MarkRejected (o sin cambio si no hay respuesta definitiva)
```

Cada checkpoint persiste antes del siguiente paso de red — ningún fallo de comunicación posterior a un checkpoint pierde el registro de lo ya logrado (verificado real: certificado válido, XML firmado y almacenado sobreviven a un `TIMEOUT` de autorización).

### Eventos de dominio

`ElectronicDocumentCreatedEvent`, `XmlGeneratedEvent`, `SignedEvent`, `SentEvent`, `ReceivedEvent`, `AuthorizedEvent`, `RejectedEvent`, `FailedEvent`, `RetryAttemptedEvent`, `DeadLetterEvent`, `ReactivatedEvent`, `CancelledEvent` — todos consumidos únicamente por `ElectronicDocumentAuditHandler` (Entity Audit, ADR-022). Ningún otro handler debe suscribirse sin justificación arquitectónica.

### Restricciones permanentes

- Un `ElectronicDocument` nunca se crea sin persistir primero en `Draft` (visibilidad garantizada en el Monitor ante cualquier fallo).
- Un reintento nunca genera una nueva `claveAcceso` ni un nuevo secuencial mientras el documento original no esté resuelto — invariante verificada contra la Ficha Técnica (código 70, nota 2, §11).
- El número de autorización en el esquema offline es siempre la clave de acceso del propio documento (AUTH-01), nunca un valor reportado ciegamente por el SOAP.
- Ninguna transición de estado se ejecuta fuera de los métodos públicos de `ElectronicDocument` (todas con guards de estado origen explícitos).

### Cambios permitidos (sin nueva ADR, siguiendo el checklist de auditoría ya establecido)

1. **Cambios obligatorios del SRI** — nueva versión de XSD, nuevo código de error, cambio de URL de servicio, nuevo campo obligatorio exigido por una actualización de la Ficha Técnica.
2. **Bugs demostrados** — con reproducción, causa raíz y test de regresión, siguiendo el mismo protocolo de esta sesión (6 preguntas de gate antes de tocar código).
3. **Vulnerabilidades de seguridad** — con evidencia de explotabilidad real.
4. **Rendimiento crítico** — con medición objetiva del problema (no "podría ser más rápido").

### Cambios prohibidos

- Agregar builders/providers/validadores para nuevos tipos de comprobante como parte de "mantenimiento" — es una funcionalidad nueva, requiere su propia fase con roadmap explícito.
- Modificar la máquina de estados, el pipeline o los contratos públicos por "limpieza" o "consistencia" sin bug demostrado.
- Reintroducir lógica de cálculo tributario, numeración documental o auditoría propia dentro de este módulo — todo eso son infraestructuras FROZEN de otros ADR, se consumen, nunca se reimplementan.
- Relajar el catálogo de errores (`sri_error_code`) con datos no verificados textualmente contra la Ficha Técnica oficial.

## Consecuencias

**Positivas:** el equipo tiene un contrato estable y verificado tanto documentalmente (Ficha Técnica) como empíricamente (envíos reales al SRI) antes de congelar. Los 4 canales de cambio permitido (SRI/bug/seguridad/rendimiento) evitan que el módulo quede completamente inamovible ante necesidades reales, sin abrir la puerta a scope creep.

**Negativas / deuda aceptada conscientemente:**
- Sin health check dedicado para conectividad SRI en `/health/*` — el Monitor y el dashboard (`GetElectronicInvoicingStatusQueryHandler`) cubren esta necesidad operativamente, pero no está integrado al framework de health checks de la API. No bloquea el cierre; es candidato a "rendimiento crítico"/observabilidad en una fase futura si se demuestra necesidad real.
- Sin métricas formales (contadores/histogramas) de tasa de autorización, latencia de SRI o tasa de rechazo — el dashboard SQL-agregado (`GetElectronicDocumentsDashboardQueryHandler`) cubre esto para consumo humano, no para alerting automatizado.
- 5 de 6 tipos de comprobante (CreditNote, DebitNote, ShippingGuide, Retention, PurchaseSettlement) tienen XSD y catálogo listos pero sin implementación activa — documentado como límite explícito, no como deuda oculta.

## Alternativas consideradas

- **No declarar cierre formal, seguir en "desarrollo activo" indefinidamente**: descartada — sin un punto de corte explícito, cualquier cambio futuro carece de un gate de justificación, y el riesgo de deuda técnica silenciosa aumenta (ver AI-RULES/ENFORCEMENT.md sobre infraestructuras CLOSED).
- **Cerrar sin las pruebas reales contra el SRI**: descartada — la validación documental por sí sola no garantiza que el comportamiento real del servicio SRI coincida; el rechazo real logrado (código 65) confirmó exactamente lo que la Ficha Técnica documenta, cerrando el círculo evidencia-documento-comportamiento.

## Addendum (2026-07-11): Infraestructura de Diagnóstico SRI (ADR-024)

`ElectronicDocumentRejectedEvent` gana un segundo suscriptor (`ElectronicDocumentSriMessageAuditHandler`), extendiendo la restricción de la sección "Eventos de dominio" bajo la causa 1 de "Cambios permitidos" (campo real de la Ficha Técnica, `<mensaje>/<tipo>`, descartado silenciosamente hasta ahora). `RetryElectronicDocumentCommandHandler` cambia su tipo de retorno de `ElectronicDocumentDto` a `ElectronicDocumentDetailDto` — `IElectronicDocumentIssuer.RetryAsync` (la interfaz listada arriba) no cambia. Se agrega `GET /api/v1/electronic-documents/by-source` como endpoint nuevo, agnóstico de módulo. Ninguna transición de estado, guard ni contrato de `IElectronicDocumentIssuer` se modificó. Detalle completo: [`ADR-024-electronic-document-diagnostic-infrastructure.md`](ADR-024-electronic-document-diagnostic-infrastructure.md).

## Addendum (2026-07-11): RESP-01 — códigos 43/45 en reenvío de Recepción (bug demostrado, causa 2)

Auditoría de cierre final (previa a la declaración de CLOSED de esta ADR) encontró que `ElectronicDocumentIssuer.TrySendToReceptionAsync` solo trataba el código SRI `[70]` ("Clave acceso en procesamiento") como "ya existe, consultar autorización antes de decidir". Los códigos `[43]` ("Clave acceso registrada") y `[45]` ("Secuencial registrado") — misma tabla oficial de errores de RECEPCIÓN de la Ficha Técnica, ya citados como fundamento del índice único `uq_electronic_document_access_key` (ver BD-01 en `ElectronicDocumentConfiguration.cs`) — caían en el rechazo genérico y podían marcar `Rejected` un documento que en realidad seguía vivo (o ya autorizado) en el SRI. Escenario real: timeout de red en el primer envío → reenvío automático/manual desde `Signed` (reutilizando el mismo XML ya firmado, sin regenerar clave de acceso) → el SRI responde 43/45 en vez de 70 → antes del fix, rechazo local falso.

**Corrección** (causa 2, bug demostrado con reproducción y test de regresión — sin ADR nueva, según el protocolo ya establecido en esta misma ADR): `ProcessingErrorPrefix` (constante única) reemplazada por `AlreadyExistsErrorPrefixes` (`["[70]", "[43]", "[45]"]`) en `ElectronicDocumentIssuer.cs`; los tres códigos siguen ahora el mismo camino ya probado (`MarkReceived` → consulta de autorización), nunca rechazo automático. 2 tests de regresión agregados (`ElectronicDocumentIssuerReceptionTests`, casos 43/45). Ningún contrato público, estado, guard ni pipeline se modificó — cambio confinado a la lista de prefijos reconocidos dentro de una rama ya existente.

## Addendum (2026-07-31): ADR-031 — Activación de Nota de Crédito V1.1.0

Primera excepción real a "Agregar builders/providers/validadores para nuevos tipos de comprobante... requiere su propia fase con roadmap explícito": `SalesReturn` (P0-01) necesitaba emitir Nota de Crédito, y esa fase con roadmap explícito ya existía (Fase 11 de `P0-01_SALES_RETURN_IMPLEMENTATION_PLAN.md`). Se agregó `CreditNoteXmlSchemaValidator` (mismo contrato `IElectronicDocumentSchemaValidator` que `InvoiceXmlSchemaValidator`) en el punto de extensión que `ElectronicDocumentSchemaValidatorResolver`/DI ya preveían sin cambios, y se activó `CreditNote.activeVersion: "1.1.0"` en `manifest.json`. Ningún contrato público, estado, guard ni pipeline listado en esta ADR se modificó. Detalle completo: [`ADR-031-credit-note-v1-activation.md`](ADR-031-credit-note-v1-activation.md).

## Addendum (2026-10-02): ZH-SRI-ANEXO26-PROVIDER-RUC-01 — RUC Proveedor (cambio obligatorio del SRI, causa 1)

La Ficha Técnica 2.34 (Anexo 26; Res. NAC-DGERCGC26-00000027, RO No. 335, 28/07/2026) exige
`<campoAdicional nombre="RUC Proveedor">RUC</campoAdicional>` en los comprobantes de emisores que usan sistemas
de terceros. Se implementa según ADR-038 (D6/D7), con salida XML nueva solo en `infoAdicional` de 01/04/07:

- `IElectronicDocumentAdditionalInfoComposer` (SSOT de `infoAdicional`) + `SystemProviderRucAdditionalInfoContributor`
  (único consumidor fiscal de `SystemProviderSettings`), en `ElectronicDocuments/AdditionalInfo`.
- Se invoca solo en los dos orquestadores de ensamblado fiscal: `CommercialElectronicDocumentXmlSupplier` (01/04)
  y `RetentionElectronicDocumentXmlService` (07: pipeline, vista previa XML y RIDE).
- `EffectiveDate` es la fecha de aplicabilidad y se compara con la fecha de emisión de negocio: antes, sin campo
  (XML idéntico al anterior); desde ella, campo obligatorio o fallo cerrado
  `SRI_SYSTEM_PROVIDER_RUC_NOT_CONFIGURED` (ED `Failed`, sin XML, firma ni llamada al SRI).

**Sin cambios:** builders, providers, validadores, XSD, firma, `SriSoapClient`, máquina de estados, pipeline del
Issuer, jobs y RIDE. Los tres XML con el campo validan contra los XSD actuales (Factura 1.1.0, NC 1.1.0,
Retención 1.0.0). Documentos firmados o autorizados no se regeneran.

## Addendum (2026-10-29): Caso #32 — respuestas inciertas del SRI (decisión del propietario)

Un `Signed` de Ventas/NC cuyo envío no obtuvo respuesta pudo haber llegado al SRI; `RetryAsync` lo reenviaba
sin consultar. Decisión aprobada: consultar antes de reenviar, conservar AUTORIZADO, dejar lo inconcluso
pendiente de conciliación, nunca reenviar automáticamente un estado desconocido ni marcar autorizado sin
respuesta válida del SRI.

- `RetryAsync` (sin guard) desde `Signed`/`Received` solo consulta la autorización por clave de acceso, igual
  que el camino con guard (ADR-036). Se retira la relectura del XML firmado para reenviar. El primer envío
  del pipeline no cambia.
- `ElectronicDocument.RequiresSriReconciliation` (Signed/Dispatching/Sent/Received, o DeadLetter que venía de
  ellos) y `RecordReconciliationEvidence` (sin transición; evento `ReconciliationRecorded` → Entity Audit).
- Conciliación manual: `POST /api/v1/electronic-documents/{id}/reconciliation`, permiso
  `electronic-documents.reconcile`, evidencia de texto obligatoria; registra y reconsulta por el mismo
  `RetryAsync`. Nunca autoriza, rechaza ni libera un reenvío.

**Sin cambios:** estados, transiciones existentes, `SriSoapClient`, firma, XML, jobs y la política de reintentos.
Contrato: [Backend § Límites y finalización](../architecture/backend.md#límites-y-finalización).

## Addendum (2026-10-31): cierre de conformidad SRI 2.34 — cambios focales (causas 1 y 2)

Hallazgos vigentes de la [auditoría 2.34](../sri/SRI-2.34-CURRENT-COMPLIANCE-AUDIT.md), con fuente en la
Ficha Técnica Offline (tabla de errores y notas 1–2, §7.5, Anexos 1 y 3). Informe:
[SRI-2.34-COMPLIANCE-CLOSURE](../sri/SRI-2.34-COMPLIANCE-CLOSURE.md).

- **R14 (causa 2):** `SriSoapClient.ParseAutorizacionResponse` elige la `<autorizacion>` AUTORIZADO si la
  respuesta trae varias (el SRI conserva los NO AUTORIZADO previos de la misma clave); si ninguna lo es, la primera.
- **R13 (causa 2):** DEVUELTA cuyos mensajes son todos código 50 (error interno del SRI; los devueltos no se
  guardan) no se marca `Rejected`: el documento conserva su estado como un fallo de transporte y el Caso #32
  rige (solo consulta, sin reenvío automático).
- **R16 (causa 1):** XML firmado > 320 × 1024 bytes (§7.5 "320 Kb", error 26; límite provisional, la ficha no
  define la unidad) → `Failed` antes de almacenar, firmar como `Signed` o transmitir; el secuencial ya fue
  consumido por el origen.
- **R11 (causa 1):** el máximo de reintentos de la empresa ya no corta la consulta de `Signed`/`Received` antes
  del plazo del SRI (24 h desde RECIBIDA, §7.5 y nota 2): siguen en el job (solo consulta, backoff ≤ 60 min)
  hasta 24 h desde el primer `Received` del timeline; después, DeadLetter con conciliación pendiente. Sin
  evidencia de recepción no se aplica DeadLetter por plazo (fail-closed).
- **R23 (causa 1):** `contribuyenteEspecial` en Factura y Nota de crédito ("obligatorio cuando corresponda")
  desde `ElectronicDocumentIssuerData.SpecialTaxpayerNumber` (← `Company.SpecialTaxpayerNo`), fuente única
  también para Retención (antes en `RetentionElectronicDocumentInfo`; XML de retención idéntico).

**Sin cambios:** estados y transiciones, firma, endpoints, clave de acceso, secuenciales, tratamiento
70/43/45, ConsultaComprobante, scheduler/jobs, migraciones, XSD y catálogos. Contrato vigente:
[backend.md § Límites y finalización](../architecture/backend.md#límites-y-finalización).

## Addendum (2026-10-31): perfil fiscal del emisor — R21/R22 (causa 1)

Ficha Técnica Offline 2.34, Anexos 21 y 24 (contrastados con la 2.32: mismo contenido y ejemplos). Detalle y
evidencia: [SRI-2.34-COMPLIANCE-CLOSURE §3.1](../sri/SRI-2.34-COMPLIANCE-CLOSURE.md).

- **R21:** `<agenteRetencion>` entre `dirMatriz` y `contribuyenteRimpe` en Factura, NC y Retención, desde
  `Company.RetentionAgentResolutionNo` cuando `RetentionAgentEffectiveFrom` ≤ fecha de emisión. El bloque
  `infoTributaria` de los tres builders se extrae a `InfoTributariaXmlBuilder` (ADR-038 D8); sin designación la
  salida es idéntica.
- **R22:** `campoAdicional` "Gran Contribuyente" con `Company.LargeTaxpayerResolutionNo` (vigencia ≤ fecha de
  emisión) mediante el contributor `sri.anexo24.large-taxpayer` del composer (ADR-038 D6); nunca en Retención.
- **Emisor:** `ElectronicDocumentIssuerProjection` es la proyección única (ADR-038 D5) y reemplaza las tres
  construcciones y las tres copias de la leyenda RIMPE de los providers (resultado idéntico).
- **R23 (complemento):** RIDE de 01/04/07 con `contribuyenteEspecial`; su formato XSD se valida al guardar y en el
  gate de emisión.

**Sin cambios:** estados, firma, SOAP, clave de acceso, secuenciales, jobs, XSD y catálogos. Esquema
(columnas nulas de `company` + `CHECK` de pares) en la migración única `InitialProductionBaseline`. Los comprobantes firmados o
autorizados no se regeneran.

## Addendum (2026-10-31): R12 Fase A — concurrencia fiscal de Factura 01 / Nota de crédito 04 (causa 2)

Defecto demostrado: Ventas no tenía guard (ADR-036), así que `CancelSalesInvoice` anulaba la factura sin
consultar su comprobante (incluso `Authorized` o con resultado SRI incierto) y el envío no revalidaba el origen
bajo lock: una anulación concurrente podía terminar con el XML de una venta anulada en el SRI.

- `SalesElectronicSourceLifecycleGuard` (origen `Sales`, compartido por Factura y NC): solo una factura o
  devolución `Authorized` procesa; lock = la fila de la factura (la misma de `CancelSalesInvoice`) o de la devolución.
- `IElectronicDocumentSourceLifecycleGuard.UsesDispatchProtocol` (único cambio de contrato): Retenciones `true`
  (sin cambios); Ventas `false`. El reclamo bajo lock persiste `Signed` (no `Dispatching`) y `RetryAsync` de Ventas
  sigue el camino v1.0: el job, la consulta por clave de acceso, R11 y el Caso #32 no cambian.
- `CancelSalesInvoice` decide con `IElectronicDocumentSourceCancellation` bajo el lock de la factura, antes de
  cualquier efecto: sin comprobante o `Rejected` → anula; `Draft`/`Failed` (o DeadLetter de ellos) → `Discarded`
  en la misma transacción; `Authorized` → bloquea (revertir la venta = devolución con Nota de Crédito); resultado
  incierto → bloquea hasta resolverlo.

**Sin cambios:** estados, transiciones, firma, XML, SOAP, clave de acceso, secuenciales, política de reintentos,
jobs, migraciones y la anulación de retenciones. Contrato: [backend.md § Revertir una venta](../architecture/backend.md#revertir-una-venta-un-solo-camino-por-factura-zh-sales-cancel-authorized-return-rule-01).

## Addendum (2026-10-31): R12 Fase B — corrección y retransmisión desde Rejected (causa 1)

Ficha Técnica Offline 2.34, tabla de errores, nota 1: el comprobante NO AUTORIZADO puede reenviarse corregido con
la misma clave; `Rejected` no tenía salida. Decisiones R12 del propietario: solo 01/04/07, solo errores del archivo
35/36/47/48/52/58/67, acción explícita con motivo y permiso, misma clave y operación comercial.

- **Transición nueva** `ElectronicDocument.ClaimCorrectedRetransmission` (`Rejected→Signed`, misma clave, rutas
  nuevas obligatorias): `TransmissionAttempt`, `TransmissionAttemptStartedUtc` y `SupersededSriDecisionUtc`
  (reloj del SRI) delimitan el intento; `IsCurrentAttemptSriDecision` descarta las NO AUTORIZADO históricas.
  Evidencia append-only `ElectronicDocumentTransmissionAttempt` (únicos por intento y por clave de idempotencia).
- **Emisor:** `IElectronicDocumentCorrectedRetransmission` (implementado por `ElectronicDocumentIssuer`, sin otro
  motor): elegibilidad pura `ElectronicDocumentRetransmissionPolicy`, consulta única del historial
  (`ISriAuthorizationClient.QueryDecisionsAsync`, sobre el mismo `SriSoapClient`), pipeline de emisión compartido
  (`PrepareSignedXmlAsync`, refactor sin cambio de comportamiento), `ElectronicDocumentXmlCorrectionComparer`,
  almacenamiento versionado (`StoreAttemptAsync`), reclamo bajo el lock del guard de Fase A y una transmisión.
  `SriAuthorizationResult.Decisions` expone todas las `<autorizacion>`; R14 no cambia.
- **R11 por intento:** `GetFirstReceivedAtUtcAsync(…, sinceUtc)`; sin retransmisión, `null` (sin cambios).
- **API/Monitor:** permiso `electronic-documents.retransmit-corrected`; elegibilidad y motivos en
  `ElectronicDocumentDiagnosticDto.Retransmission`; acción con confirmación.

**Sin cambios:** firma, builders, XSD, clave de acceso, secuenciales, política de reintentos, job, Caso #32,
anulación de retenciones y el comportamiento de comprobantes sin retransmisión. Esquema
(3 columnas de `electronic_documents`, tabla nueva) en la migración única `InitialProductionBaseline`. Contrato:
[backend.md § Límites y finalización](../architecture/backend.md#límites-y-finalización).

## Addendum (2026-11-02): RESP-01 — código 45 por colisión de secuencial (bug demostrado, causa 2)

Corrige en parte el addendum RESP-01 del 2026-07-11. El código `[45]` "Secuencial registrado" no identifica la clave
de acceso enviada, sino **otra** clave con el mismo secuencial. Evidencia real (logs del 2026-10-31, tras reiniciar la
secuencia en una instalación nueva): las facturas 001 y 002 recibieron `[45]` en su primera transmisión, el emisor las
marcó `Received` y la consulta de autorización de una clave que el SRI nunca guardó respondió `TIMEOUT`
indefinidamente (8+ reintentos).

**Corrección:** `AlreadyExistsErrorPrefixes` queda en `["[70]", "[43]"]`. En la primera transmisión
(`TransmissionAttempt == 1`), un `[45]` sin `[43]`/`[70]` es colisión confirmada y sigue el rechazo genérico
existente (`MarkRejected` con el mensaje y los `SriMessage` originales). Esa clave nunca salió antes: `Signed` solo
se consulta (Caso #32) y `Failed` nunca sigue a `Signed`. Si el SRI la tuviera, respondería `[43]`/`[70]`. En una
retransmisión corregida (R12, `TransmissionAttempt > 1`, misma clave que ya llegó al SRI), `[45]` conserva la
consulta de autorización. Lo mismo ocurre si la respuesta llegó tras un POST incierto del mismo envío: `SriSoapClient`
reintenta hasta 3 POST ante red/timeout, el anterior pudo haber llegado al SRI y lo declara
`SriReceptionResult.PrecededByUncertainPost`. `[45]` no es corregible en R12 (`CorrectableCodes`), así que el comprobante rechazado
requiere un nuevo secuencial vía el flujo normal del documento de origen.

**Sin cambios:** estados, transiciones, guards, contratos públicos, R12, Caso #32, BD y migraciones. Los
comprobantes históricos ya marcados `Received` por un `[45]` no se migran: se resuelven operativamente.
Regresión: `ElectronicDocumentIssuerReceptionTests` (45 en primera transmisión → Rejected sin consultar
autorización; 43+45 → consulta; 45 tras POST incierto → consulta), `ElectronicDocumentCorrectedRetransmissionTests`
(45 en retransmisión → solo consulta, sin cambios) y `SriSoapClientTests` (reintento HTTP → `PrecededByUncertainPost`).

## Addendum (2026-11-02): SRI-TOGGLE-02 — emisión electrónica habilitable por empresa (decisión del propietario)

**Causa.** No es una de las cuatro causas de la regla permanente: el propietario autorizó expresamente una
intervención en el núcleo FROZEN **limitada al control de emisión** (SRI-TOGGLE-01 aprobado con cambios:
valor ausente = deshabilitada; sin reanudación automática al rehabilitar).

**Decisión.**
- Fuente única: `electronic_documents.emission_enabled` en `org_settings` (scope Company, `RequiresAudit`), escrita
  por `PUT /api/v1/settings/operations` (`settings.operations.configure`) a través de `OrgSettingsRepository`
  (historial en `ConfigurationChangeLog`). Sin migración: la key es una fila más de `org_settings`.
- Un único control, `IElectronicEmissionSwitch` (Application) / `ElectronicEmissionSwitch` (Infrastructure), leído con
  el `TenantId`+`CompanyId` del comprobante, nunca del body. Lo consulta solo `ElectronicDocumentIssuer`:
  inicio de `RunPipelineAsync` (antes de XML/firma), dentro de `ClaimDispatchAsync` y `ClaimRetransmissionAsync`
  con el lock compartido del interruptor hasta su commit (serializa con el apagado; en 02 era `FOR SHARE` sobre la
  fila, sustituido en 04 por un lock consultivo por empresa), revalidación previa a `Signed` en
  orígenes sin guard, `RetransmitCorrectedAsync` y antes de `Reactivate` de un DeadLetter hacia Draft/Failed.
- Bloqueado: `ELECTRONIC_DOCUMENT_EMISSION_DISABLED` (422), sin escrituras sobre el comprobante (sigue Draft/Failed,
  sin clave, sin intento ni DeadLetter). Ventas/NC/Retenciones conservan su confirmación comercial (aviso).
- Reanudación explícita: un proceso automático (usuario `Guid.Empty`) solo transmite si la última actividad del
  comprobante (`LastAttemptUtc ?? CreatedAt`) es posterior a la última habilitación (último cambio auditado a
  `true`; sin historial, escritura de la fila). Los jobs filtran igual en SQL (`ElectronicEmissionSwitchQuery`):
  reintentos (Draft/Failed), recuperación de Sales y de Retentions (orígenes anteriores a la habilitación).
- ~~Sin bloqueo: consultas de autorización/estado (Caso #32, R11), conciliación, anulación por consulta~~ — sustituido
  por SRI-TOGGLE-06 (pausa total, abajo). Sin bloqueo: RIDE, correo y lectura local.
- Banner: `ElectronicInvoicingStatus.Disabled` (ya reservado) precede a certificado y conectividad.

**Sin cambios:** XML, XAdES, SOAP (`ElectronicDocumentReceptionService` y clientes), secuencias, máquina de estados,
eventos, `ElectronicInvoicingController`, R11/R12/Caso #32 y migraciones. No se agrega bloqueo en
`ElectronicDocumentReceptionService`: bloquear tras `Signed` dejaría comprobantes firmados nunca enviados que el
Caso #32 solo consultaría.

**Riesgos aceptados.** (1) Despliegue: toda empresa sin la key deja de transmitir hasta habilitarla (decisión
fail-closed del propietario). (2) Un reclamo ya confirmado antes del apagado completa su único envío. (3) Orígenes sin
guard (no existen en producción) revalidan sin lock. (4) ~~Carrera en la recuperación de Retentions~~ — cerrada
por SRI-TOGGLE-03 (abajo).

**SRI-TOGGLE-03/04 (2026-11-02, mismo alcance autorizado).** La reanudación automática decidía con
`coalesce(LastAttemptUtc, CreatedAt)` del comprobante: un Draft creado tarde (recuperación de Retentions o Sales tras una
caída) para un origen anterior a la habilitación nacía "posterior" y se transmitía solo, también si la habilitación se
confirmaba entre el control previo y el INSERT del Draft. 03 lo mitigó con filtros y una revalidación en la
recuperación (quedaba la ventana precheck→INSERT); 04 lo cierra sin migración y sustituye esos parches:
- **Actividad = dato persistido del origen, nunca la creación del comprobante.** Último intento del comprobante (solo
  existe si un intento pasó el control) o, sin intentos, el `UpdatedAt` que el dominio fija en `SalesInvoice.Authorize`,
  `SalesReturn.Authorize` y `RetentionDocument.Issue` (después solo lo mueve `Cancel`, que deja el origen no procesable
  bajo el lock del guard; no hay auto-estampado ni updates masivos sobre esas tablas). Origen desconocido = sin
  transmisión automática. Misma regla en el issuer (`IElectronicEmissionSwitch.GetSourceActivityUtcAsync`) y en el SQL
  del job (`ElectronicEmissionSwitchQuery.SourceActivity`).
- **Serialización por lock consultivo de empresa.** Toda lectura del interruptor toma el lock compartido (el reclamo lo
  sostiene hasta su commit; una lectura puntual usa una transacción corta) y todo cambio, en una transacción
  (`UpdateOperationalPreferencesCommandHandler`), toma el exclusivo antes de que el repositorio feche el registro
  auditado. Cubre también la primera habilitación (INSERT sin fila que bloquear con `FOR SHARE`).
- **Integridad de la fecha de corte:** solo el último registro auditado de la misma key/empresa/scope
  (`OrgSetting`/`Value`) y solo si explica el valor vigente; si no, la última escritura de la fila.
- Se retiran de 02/03: los filtros por corte de las recuperaciones de Sales/Retentions, la revalidación de
  `SalesElectronicDocumentRecovery.RecoverAsync`, `RegisterElectronicDocumentRequest.SourceActivityUtc`,
  `IRetentionElectronicTransmission.StartRecoveryAsync` y `RetentionElectronicStartCandidate.IssuedAtUtc` (los contratos
  vuelven a su forma previa). Las recuperaciones vuelven a registrar el Draft (visible en el Monitor) y el issuer decide.
- El corte y la actividad viven en PostgreSQL: un reinicio de API o workers no los altera.

Regresión 04: `PurchaseRetentionConfirmIntegrationTests` (`SRI_TOGGLE_04_*`: anteriores/posteriores con Draft tardío,
re-habilitación entre la lectura del job y el INSERT, re-habilitación durante la firma, reinicio),
`ElectronicEmissionSwitchPostgreSqlTests` (apagado serializado con un reclamo abierto, lectura durante una habilitación
en curso, aislamiento por empresa, candidatos con origen desconocido), A9 (venta autorizada antes de habilitar con Draft
recuperado después), `ElectronicDocumentIssuerEmissionSwitchTests`, `UpdateOperationalPreferencesCommandHandlerTests`
(lock antes del upsert). Riesgos residuales: (i) los instantes comparados son relojes de aplicación; con varios nodos se
requiere sincronización (NTP): una desviación entre nodos mayor que el tiempo entre la emisión del origen y su control
podría invertir el orden; (ii) una escritura futura de `UpdatedAt` en `SalesInvoice`/`SalesReturn`/`RetentionDocument`
después de autorizar/emitir rompería la premisa (hoy la garantiza el dominio; todo cambio debe revisar esta regla);
(iii) escrituras de la key fuera de `PUT /settings/operations` (SQL directo) no toman el lock; su corte cae en la
escritura de la fila.

**Regresión:** `ElectronicDocumentIssuerEmissionSwitchTests`, `ElectronicEmissionSwitchPostgreSqlTests`,
`GetElectronicInvoicingStatusQueryHandlerTests`, `UpdateOperationalPreferencesCommandHandlerTests`, A9 y
`PurchaseRetentionConfirmIntegrationTests` (recuperación), `ElectronicDocumentRetryJobTenantContextTests`.

**SRI-TOGGLE-06 (2026-11-02, mismo alcance autorizado) — pausa total.** 02/04 solo controlaban transmisiones
nuevas: con la emisión deshabilitada el job y el Monitor seguían consultando autorización (Signed/Received), se
reactivaban DeadLetter de consulta y ConsultaComprobante verificaba anulaciones de retención. Ahora OFF = ninguna
llamada al SRI sobre comprobantes propios de la empresa; ON restablece las funciones, pero los procesos automáticos
nunca retoman pendientes anteriores a la última habilitación (transmisión ni consulta): requieren una acción explícita.
- **Una regla** (`ElectronicEmissionState.AllowsSriAccess`, antes `AllowsTransmission`): deshabilitada = nunca;
  usuario = solo habilitada; automático = habilitada y última actividad (último intento / última consulta) o, sin
  ella, instante del origen (autorización/emisión / presentación de la anulación) ≥ corte. Misma regla en SQL
  (`ElectronicEmissionSwitchQuery.WhereAutomaticSriAccessAllowed`, para comprobantes y solicitudes de anulación).
- **Issuer:** control al inicio de TODO reintento (v1.0 y con dispatch protocol), antes de reactivar un DeadLetter
  de cualquier estado previo, registrar el intento o consultar (sustituye el control limitado a reactivaciones hacia
  Draft/Failed); revalidación inmediatamente antes de cada consulta de autorización (pipeline, reintento, R12): un
  apagado confirmado durante el intento omite la consulta sin escribir; sin DeadLetter por exhaución mientras esté
  deshabilitada. Historial R12 ya estaba detrás del control de 02.
- **ConsultaComprobante:** `RetentionAnnulmentService.VerifyWithSriAsync` (en línea, "Ya presenté la solicitud" y job)
  controla antes de consultar, sin registrar la consulta (`ELECTRONIC_DOCUMENT_EMISSION_DISABLED`); la presentación
  se registra igual. El polling (`GetDueForSriVerificationAsync`) filtra por el interruptor.
- **Jobs:** `GetRetryCandidatesAsync` filtra en todo estado (una empresa pausada no ocupa el lote de las demás).
  Recuperación de Retentions/Sales: solo registran Draft (local) y el issuer decide; finalización de anulaciones ya
  confirmadas: local, sin SRI.
- **Fuera del alcance (06; incorporado por SRI-TOGGLE-07, abajo):** descarga del XML autorizado de un proveedor
  (`SriReceptionXmlProvider`) y vigencia de una NC de proveedor (`PurchaseCreditNoteReprocessingGuard`). El ping de
  diagnóstico explícito (`ValidateSriConfiguration`) no consulta comprobantes; el estado del banner ya lo omitía.
  Inventario cerrado y verificado por reflexión: `SriAccessEmissionSwitchBoundaryTests`.
- **UI:** aviso de pausa en el Monitor y en el panel de anulación; reintentar/conciliar/retransmitir/verificar
  deshabilitados (el permiso no cambia; el backend bloquea igual); guardar el interruptor refresca el estado global.

**Concurrencia (06; sustituida por el ámbito de llamada de SRI-TOGGLE-07, abajo).** El apagado (lock exclusivo) espera a los reclamos abiertos; un envío ya reclamado se completa (no se
aborta una transmisión en vuelo: bloquearla tras `Signed` dejaría comprobantes nunca enviados que el Caso #32 solo
consultaría). Toda consulta de autorización posterior se revalida y se omite si el apagado ya se confirmó.
**Riesgos residuales:** (1) una consulta/envío cuya llamada HTTP ya empezó cuando se confirma el apagado termina
(la ventana es la de una llamada); (2) una solicitud de anulación presentada en el ERP antes de rehabilitar no se
verifica sola hasta una verificación explícita; (3) los de 04 (relojes de aplicación, premisa de `UpdatedAt`).
Regresión 06: `ElectronicDocumentIssuerEmissionSwitchTests` (`SRI_TOGGLE_06_*`), `ElectronicEmissionSwitchPostgreSqlTests`
(candidatos en todo estado, aislamiento por tenant, apagar/rehabilitar), `PurchaseRetentionConfirmIntegrationTests`
(`SRI_TOGGLE_06_*`: ConsultaComprobante manual/automática y polling), `SriAccessEmissionSwitchBoundaryTests`, vitest
del Monitor y del panel de anulación. Sin migraciones; XML, XAdES, firma, SOAP, BD y secuenciales sin cambios.

**SRI-TOGGLE-07 (2026-11-02, mismo alcance autorizado) — pausa total sin excepciones y garantía de corte.** 06 dejaba
fuera la descarga del XML de proveedores, la verificación de vigencia de NC de proveedores y el ping de diagnóstico,
y su corte era "control y después llamada": un apagado confirmado entre ambos (o tras el reclamo de envío) no detenía
esa llamada.
- **Alcance:** con OFF, `DownloadPurchaseReceptionXmlHandler` responde `ELECTRONIC_DOCUMENT_EMISSION_DISABLED` sin
  consultar (el comprobante sigue Importado; también por el lote, documento a documento);
  `PurchaseCreditNoteReprocessingGuard` bloquea el reprocesamiento sin consultar (fail-closed: nunca supone vigencia);
  `ValidateSriConfiguration` informa la conectividad "no verificada" sin ping; el estado ya devolvía `Disabled` sin
  ping. Registro manual de compras/gastos/NC y lectura local sin cambios.
- **Ámbito de llamada SRI** (`IElectronicEmissionSwitch.EnterSriCallAsync`): el lock consultivo de empresa de 04, en
  modo compartido y de SESIÓN sobre la conexión del contexto, sostenido desde el control hasta que termina la llamada
  (reentrante; sin transacción ni locks de filas durante el SOAP; el reclamo del mismo backend obtiene su lock de
  transacción sin esperar). El cambio del interruptor (exclusivo) espera a todas las llamadas en curso de la empresa;
  una llamada nueva que llega mientras un cambio espera queda detrás de él y ve el valor confirmado. Con ello: tras
  confirmar el apagado no se inicia ninguna llamada nueva; las ya iniciadas terminaron antes de la confirmación
  (incluido el único envío de un reclamo y su consulta posterior). Las revalidaciones de 06 quedan como defensa en
  profundidad (el estado es estable durante el ámbito). Ámbitos: pipeline, reintento y retransmisión del issuer,
  verificación de anulación, descarga de XML, verificación de NC de proveedor, estado y validación SRI.
- **Inventario:** `SriAccessEmissionSwitchBoundaryTests` ya no admite excepciones de alcance y exige que todo
  consumidor de Application que origina llamadas dependa del interruptor.
- **UI:** recepción de compras muestra la pausa y deshabilita "Consultar XML" y "Descargar XML pendientes".

**Riesgos residuales 07.** (1) El guardado del interruptor espera a las llamadas SRI en curso de la empresa (acotado
por los timeouts HTTP y el polling de autorización del cliente). (2) El ámbito usa una conexión del pool durante
el SOAP (una por llamada en curso; el lote de recepción usa hasta 3). (3) Si la conexión del ámbito se rompe durante
la llamada, PostgreSQL libera el lock y un cambio podría confirmarse antes de que esa llamada termine (no hay
estrategia de reintento de conexión configurada). (4) Escrituras de la key por SQL directo no toman el lock (04).
Regresión 07: `ElectronicEmissionSwitchPostgreSqlTests` (`SRI_TOGGLE_07_*`: llamada en curso retiene apagado y
habilitación, llamada posterior ve el valor confirmado, reentrancia, reclamo del mismo backend, otra empresa no
espera), `ElectronicDocumentIssuerEmissionSwitchTests`, `ValidateSriConfigurationQueryHandlerTests`,
`DownloadPurchaseReceptionXmlHandlerTests`, `PurchaseReturnEndToEndTests` (reprocesamiento con interruptor real),
`ApiErrorContractHttpTests` (download-xml pausado → 422), `SriAccessEmissionSwitchBoundaryTests`, vitest de recepción;
`PurchaseRetentionConfirmIntegrationTests.SRI_TOGGLE_07_un_cambio_durante_la_firma_*` sustituye al test de 04 de
re-habilitación durante la firma (esa ventana ya no existe: el cambio espera a la llamada en curso).

## Addendum (2026-11-12): API-DOC-V1 — excepción documental de los controladores (decisión del propietario)

**Causa.** No es una de las cuatro causas de la regla permanente: el propietario autorizó expresamente, para el
alcance API-DOC-V1 y de forma limitada, la documentación Swagger/OpenAPI de las 20 operaciones de Facturación
electrónica (`ElectronicDocumentsController` 10, `ElectronicInvoicingController` 6, `SriServiceHealthController` 1 y
`RideController` 3). Sin esa autorización los dos primeros, componentes FROZEN de este ADR, no se habrían tocado.

**Decisión.** Se permite en esos cuatro controladores, y solo en ellos: comentarios XML de documentación,
atributos `[ProducesResponseType]` que reflejen contratos existentes e imports estrictamente necesarios.
La comparación contra `HEAD` (retirados `///`, `[ProducesResponseType]` y `using`) debe dejar el código idéntico.

**Sin cambios:** estados, transiciones, pipeline, XML, XAdES, SOAP, clientes SRI, DTO, servicios, rutas, métodos,
permisos, `operationId`, validadores, handlers, persistencia y respuestas HTTP reales. La excepción **no** modifica
las cuatro causas ordinarias de reapertura funcional, no autoriza ningún cambio de comportamiento del SRI ni de otros
componentes congelados, y no crea precedente para otros cambios en el núcleo. Registro de ejecución:
[STATUS.md](../../STATUS.md) (API-DOC-V1, Fase 2B-5).

## Cierre oficial

**Estado: CLOSED.** **Versión: v1.0.** **Fecha de cierre: 2026-07-11.** **Responsable: Sebastian Zhinin (Lead/Architect del proyecto).**

**Alcance del cierre**: núcleo funcional de `ElectronicDocuments` — pipeline de emisión (generación XML, validación XSD, firma XAdES-BES, envío a Recepción, consulta de Autorización), retry con backoff y Dead Letter reversible, Monitor de consulta, almacenamiento de XML por etapa, configuración SRI por empresa, multi-tenant. Cubre exclusivamente el comprobante `Invoice` (Factura) — los otros 5 tipos declarados en `ElectronicDocumentType` tienen XSD/catálogo pero sin implementación activa (ver "Límites" arriba).

Con el addendum RESP-01 incorporado, las siete rondas de revisión (funcional, arquitectónica, Clean Architecture, desacoplamiento, pipeline de emisión, XAdES, cliente SOAP, persistencia, seguridad, concurrencia, retries, flujo completo, respuestas SRI) quedan cerradas. No queda ningún hallazgo abierto que bloquee producción. La deuda técnica listada abajo queda **aceptada y documentada, no bloqueante**, y solo se resuelve si se demuestra que bloquea una necesidad real (nunca por "mejora").

### Deuda técnica aceptada (registrada, no implementada)

| Deuda | Ubicación | Severidad | Condición de remediación |
|---|---|---|---|
| Búsqueda del Monitor acoplada a Sales | `ElectronicDocumentRepository.GetPagedAsync` (`_db.SalesInvoices`, `"Sales"` hardcodeado) | Media | Extraer a un search provider del módulo dueño si se agrega un segundo `SourceModule` real (hoy solo existe Sales) |
| Contraseñas de certificado legacy en texto plano | `DataProtectionSecretProtector.UnprotectOrPlaintext` (acepta valores sin prefijo `dp1:`) | Media-Alta | Job de migración forzada a `dp1:` antes de un despliegue con certificados legacy sin reguardar |
| `AVG` calculado en memoria en vez de SQL | `ElectronicDocumentRepository.GetAverageAuthorizationMinutesAsync` | Baja | Migrar a `AVG()` en la query si el dashboard se usa con historia larga y se demuestra latencia real |
| `GetRetryCandidatesAsync` sin paginación (cross-tenant intencional) | `ElectronicDocumentRepository.cs` | Media | Agregar `Take(N)`/cursor si el volumen de reintentos crece a un punto medible |

Ninguna de estas cuatro es un defecto de correctitud — todas fueron evaluadas y confirmadas como no bloqueantes en la auditoría de cierre final.

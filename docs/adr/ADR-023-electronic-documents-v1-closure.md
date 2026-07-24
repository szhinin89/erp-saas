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

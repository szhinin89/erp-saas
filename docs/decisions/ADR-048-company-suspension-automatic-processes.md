# ADR-048 — Empresa suspendida o deshabilitada: procesos automáticos y sesiones

Estado: **Accepted** (decisiones del propietario 2026-11-13). SEC-SUSP-01 **publicado, pendiente de
validación CI**; SEC-SUSP-02b (refresh token huérfano) **sin implementar**, propuesta pendiente.
Extiende, con una excepción limitada y autorizada, infraestructura CLOSED/FROZEN de ElectronicDocuments/SRI y
Communications; no cambia reglas fiscales, firma, SOAP, XML, numeración, estados de comprobantes ni migraciones.

## Contexto

`CompanyAccessGuard` bloquea toda solicitud HTTP de empresa cuando `IsActive=false` o
`OperationalStatus != Operational`. Los procesos sin HTTP (Hangfire: ADR-043) no consultaban ese estado: la
reproducción con PostgreSQL desechable mostró que una empresa suspendida y una deshabilitada procesaban por igual
reintentos SRI (3/3 comprobantes) y entrega de correo. El único control en ese canal era el interruptor de emisión
por empresa (SRI-TOGGLE), independiente del estado.

## Decisión

**D1 — Predicado.** «Empresa pausada» = `IsActive = false` **o** `OperationalStatus = Suspended`
(`CompanyAutomaticProcessingPause`, única definición, en memoria y SQL). `PendingSetup` no pausa. La empresa
reanuda cuando ambos estados se restablecen.

**D2 — SRI: se pausa solo la transmisión nueva.** El estado del comprobante prueba si salió al SRI
(`ElectronicDocument.CanBeDiscarded` / `RequiresSriReconciliation`, ADR-036): `Draft`, `Failed` y `DeadLetter` desde
ellos nunca salieron; la retransmisión corregida (R12 Fase B) es un envío nuevo. Esas transmisiones no se generan,
firman ni envían, sin escrituras (conservan estado, intentos y backoff, sin DeadLetter por exhaución).
`Signed`, `Dispatching`, `Sent`, `Received` y `DeadLetter` desde ellos **siguen consultándose** (R11, Caso #32,
conciliación, cierre por plazo de 24 h). El interruptor de emisión conserva su pausa total y su precedencia.

**D3 — Correo: se pausa la entrega.** Las filas Company de una empresa pausada no se reclaman (ni se recuperan con
lease vencido): permanecen `Pending`/`Processing`, sin intento y sin tocar `retry_count`. Encolar continúa. El
alcance System no depende de ninguna empresa.

**D4 — Continúan:** outbox de eventos de dominio, expiración de sesiones, consulta de anulaciones de retención,
reconciliador de comunicaciones (encolado) y la recuperación de ventas, que sigue siendo create-only (ADR-043).

**D5 — Concurrencia.** El reclamo de envío es el punto de linealización. Ventas (`SalesElectronicSourceLifecycleGuard`,
sin protocolo Dispatching) y Retenciones tienen guard de origen registrado y reclaman con `ClaimDispatchAsync`: dentro
de su transacción el interruptor lee el estado de la empresa y bloquea su fila `FOR SHARE` hasta el commit. Una
suspensión confirmada después espera a que termine el reclamo (milisegundos, nunca la llamada al SRI) y ningún reclamo
nuevo puede ver «operativa» tras confirmarse la suspensión; lo mismo vale para el reclamo de la retransmisión corregida.
Lo ya reclamado (en vuelo) termina; en correo, a lo sumo una fila por worker. Las comprobaciones anteriores al reclamo
(inicio del pipeline y primera lectura de la retransmisión) no toman ese lock: una suspensión intermedia la detecta
el reclamo. **Único camino sin lock de fila:** un origen sin guard registrado (hoy solo comprobantes creados por la API
genérica de ElectronicDocuments, nunca Ventas ni Retenciones) revalida inmediatamente antes de persistir `Signed` con una
lectura puntual; su ventana es el intervalo entre esa lectura y el guardado más la llamada ya iniciada, sin
linealización estricta. Se acepta porque ese origen solo se crea con una solicitud HTTP de empresa (bloqueada por el
guard cuando la empresa está pausada), los jobs de recuperación de Ventas y Retenciones no lo crean, y el job de
reintentos que sí puede retomarlo ya lo excluye en SQL y revalida con esa lectura puntual.

**D6 — Código de error.** Se conserva `ELECTRONIC_DOCUMENT_EMISSION_DISABLED` (422) con mensaje propio de la pausa.
Un código nuevo (`COMPANY_ELECTRONIC_EMISSION_PAUSED`) no es compatible hoy: `ElectronicDocumentIssuer` (reintento
v1.0 y guardado), `RetentionAnnulmentService`/`UseCases` y la confirmación de compras con retención comparan
exactamente ese código para tratar el bloqueo como pendiente; con otro código el flujo continuaría hacia
`ApplyDeadLetterIfExhaustedAsync`. Además exigiría `MessageCatalog`, la tabla de `ApiErrorContractTests` y OpenAPI.
El bloqueo por estado de empresa solo es alcanzable por jobs (el guard bloquea antes toda solicitud HTTP de empresa),
por lo que no hay consumidor público. Alternativa disponible si el propietario la pide: código nuevo más un helper de
equivalencia en los consumidores citados.

**D9 — Pausa empresarial frente al interruptor SRI.** Son controles distintos y se componen: el interruptor
(`electronic_documents.emission_enabled`, opt-in por empresa, default OFF) es una pausa total que incluye consultas y
exige reanudación explícita de los pendientes anteriores a su habilitación; la pausa empresarial se deriva del estado de
la empresa, solo detiene la transmisión nueva, no pausa consultas ni conciliación y reanuda sola al restablecerse ambos
estados (no mueve el corte de la habilitación). Con ambos activos manda el interruptor (mismo código, su mensaje).

**D7 — Observabilidad.** Log estructurado y gauge por pasada de job: identificador técnico de empresa, cantidad de
pendientes pausados, antigüedad del más antiguo y motivo (`Suspended`/`Inactive`). Sin comprobantes, claves de
acceso, tokens ni datos personales. No se amplía Integration v1.

**D8 — Sesiones (KEEP, SEC-SUSP-02).** Se mantiene la emisión de sesión (SwitchCompany, Refresh, Reauthenticate,
OperateCompany) para empresas suspendidas: la interfaz la espera (`CompanySelectPage`, `ProtectedRoute`) y el acceso
empresarial lo bloquea el guard en cada solicitud. Una empresa deshabilitada sigue rechazada. `PendingSetup` sin cambios.

## Implementación SEC-SUSP-01 (local)

`ElectronicEmissionState.CompanyPause` (lectura del interruptor); `ElectronicDocumentIssuer.EmissionBlockedAsync(…,
newTransmission)` solo en el inicio del pipeline, los dos reclamos y la retransmisión corregida (los puntos de consulta no
cambian); filtro de candidatos en `ElectronicEmissionSwitchQuery.WhereAutomaticSriAccessAllowed` y
`RetentionDocumentRepository.GetPendingElectronicStartAsync`; SQL de `CommunicationOutboxDeliveryStore.ClaimNextAsync`.

## Consecuencias y riesgos

- Una pausa prolongada puede superar la tolerancia de fecha de emisión del SRI (90 días, error [65]): al reanudar, los
  pendientes más antiguos serían rechazados. Mitigación: la métrica y el log de antigüedad del pendiente más antiguo.
- Mientras la empresa está pausada el Monitor empresarial está bloqueado por el guard; la visibilidad es el log/métrica
  del job. Un campo en `/api/integration/v1/companies/{id}/status` queda fuera (Integration v1 no se amplía).
- Un correo ya reclamado al suspenderse se entrega (tope: una fila por worker).

## Fuera de alcance

SEC-SUSP-02b: secuencia segura de rotación del refresh token (propuesta aparte, ver STATUS), migraciones, cambios de
contrato HTTP/OpenAPI y permisos.

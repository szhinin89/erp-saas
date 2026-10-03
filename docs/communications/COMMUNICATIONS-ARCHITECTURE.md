# Communications — Auditoría y arquitectura objetivo

**Tickets:** ZH-COMMUNICATIONS-ARCHITECTURE-01 (auditoría) · ZH-COMMUNICATIONS-ARCHITECTURE-02 (cierre)
**Fecha:** 2026-10-02 · **Decisión:** [ADR-039](../decisions/ADR-039-communications-architecture.md) (Accepted)
**Relacionado:** [ADR-038 D14](../decisions/ADR-038-sri-electronic-compliance-architecture.md) (frontera
ElectronicDocuments → Communications), [`SRI-ELECTRONIC-COMPLIANCE-ARCHITECTURE.md` §O](../sri/SRI-ELECTRONIC-COMPLIANCE-ARCHITECTURE.md),
ADR-022 (auditoría), ADR-034 (contrato temporal), ADR-005 (filtros multi-tenant).

Este documento es la evidencia y el diseño detallado de ADR-039. No hay código productivo asociado: cada fase
(§U) es un ticket propio.

---

## Guía rápida — ¿dónde va cada cosa?

| Pregunta | Respuesta |
|---|---|
| Un documento autorizado (Factura, NC, Retención) necesita correo | Communications: handler genérico + contributor del módulo origen |
| Recuperar contraseña / verificar email / invitar usuario | Auth genera y valida el token; Communications entrega (scope System) |
| Cambiar SMTP o proveedor | `IEmailSender` (transporte) + `CommunicationSettingsResolver` (configuración) |
| Cambiar el texto de un correo | Template por defecto versionado o override de la empresa (`CommunicationTemplate`) |
| Reintentar / cancelar / reenviar | Communications (`CommunicationOutbox` + política de reintento + monitor) |
| Saber si se envió y qué pasó en cada intento | `CommunicationOutbox` (estado actual) + `CommunicationDeliveryAttempt` (historial) |
| Un módulo quiere "mandar un correo" | Encola una intención en `ICommunicationQueue`. Nunca SMTP, nunca `IEmailSender` |

---

## A. Decisiones abiertas cerradas

### DA-1 — Configuración y remitente de los correos System

**Decidido:** los mensajes de scope **System** (password reset, verificación de email, alertas de seguridad,
invitaciones globales) usan **siempre el perfil de instancia** (`Communications:Email:*`). No dependen de ninguna
empresa y **nunca** se resuelven con "la primera empresa del usuario". `CompanyId` no aplica a ese scope.

Motivo: un usuario puede pertenecer a varias empresas o tenants, una empresa puede no tener SMTP, y el remitente
de una alerta de seguridad es la instalación, no un cliente de la instalación.

### DA-2 — Dónde viven los templates

**Decidido:** el template **por defecto** es código/archivo embebido versionado en el repositorio (fuente de
verdad, revisado en PR, probado). La entidad existente `CommunicationTemplate` es un **override opcional por
empresa**, solo para propósitos Company que lo permitan. No hay templates exclusivamente en BD. El motor es un
**renderer propio de placeholders**; RazorLight no se adopta.

---

## B. Mapa actual revisado (verificado en código, 2026-10-02)

| Capa | Componente | Observación |
|---|---|---|
| Domain | `CommunicationOutbox`, `CommunicationOutboxAttachment`, `CommunicationTemplate`, `CommunicationChannel` (Email/WhatsApp/Sms/Internal), `CommunicationStatus` (Pending/Processing/Sent/Failed/Cancelled), `CommunicationPriority`, `CommunicationAttachmentType`, `CommunicationPurposes` (solo `SALES_INVOICE_AUTHORIZED`) | Outbox **genérica** (no es una cola de Factura). Implementa `ICompanyOperationalEntity` (Tenant/Company Guid no nulos, filtro global fail-closed) |
| Application | `ICommunicationQueue`/`CommunicationQueue` (entrada real), `IEmailSender`, `ICommunicationSettingsResolver`, `ICommunicationOutboxProcessor`, `SalesInvoiceAuthorizedCommunicationHandler`, `QueueEmailCommand` (sin llamadores), `SendTestEmailCommand`, `Get/UpdateCompanyEmailSettings` | Tenant/Company de la cola salen del contexto ambiente |
| Infrastructure | `CommunicationOutboxProcessor`, `SmtpEmailSender` (System.Net.Mail), `CommunicationSettingsResolver`, repositorios, tablas `communication_outbox`, `communication_outbox_attachments`, `communication_templates` | Sin FK a tenants/companies |
| API | `CommunicationsEmailSettingsController`, `ProcessCommunicationsJob` (`process-communications`, cada minuto) | Job sin `[DisableConcurrentExecution]` |
| Frontend | `/settings/communications/email` | No hay monitor |
| Auth | `ForgotPasswordHandler` → `IPasswordResetLinkSender` → **`LoggingPasswordResetLinkSender`** (única implementación) | Escribe el enlace con el token en el log; no envía correo |
| Otros | `OutboxMessage`/`OutboxProcessor` (outbox técnica de domain events), `PurchaseCommunication` (nota de seguimiento de Compras, no envía) | Concerns distintos, solo comparten nombre |

### Flujo actual de Factura

```
ElectronicDocument.Authorize → ElectronicDocumentAuthorizedEvent
  → ErpDbContext.SaveChangesAsync: guarda estado + OutboxMessage, publica in-process dentro de la MISMA transacción
  → SalesInvoiceAuthorizedCommunicationHandler (Communications)
       lee ED + SalesInvoice (depende de Sales), revisa electronic_documents.email_on_authorization,
       destinatario = invoice.Customer.Email (snapshot), XML por ruta, RIDE generado con GetOrGenerateRideQuery
       DENTRO de la transacción de autorización, asunto/cuerpo concatenados en C#,
       QueueEmailAsync(SaveImmediately:false), toda excepción se traga y se registra en log
  → process-communications → CommunicationOutboxProcessor: 50 Pending de todos los tenants (AsPlatformQuery),
       JobExecutionContext por fila, MarkProcessing+Save, settings de la empresa, SmtpEmailSender, MarkSent/MarkFailed
```

### Gaps de la auditoría 01 (todos resueltos por diseño en este documento)

| # | Gap | Severidad | Resuelto en |
|---|---|---|---|
| G1 | Doble envío con dos ejecuciones del job (sin claim atómico ni token de concurrencia) | 🟥 | §G, Fase 2 |
| G2 | Password reset: no envía correo en producción; token raw en logs | 🟥 | §K, §L, Fases 1 y 6 |
| G3 | Password reset: enumeración de usuarios, sin rate limit | 🟧 | §L, Fase 1 |
| G4 | Filas atascadas en `Processing` para siempre | 🟧 | §G, Fase 2 |
| G5 | NC y Retención sin correo; handler acoplado a Sales | 🟧 | §M, Fase 5 — ✅ resuelto 2026-10-03 |
| G6 | Sin templates reales (HTML en el handler); `CommunicationTemplate` y RazorLight sin uso | 🟧 | §J, Fase 4 |
| G7 | `MaxRetries` de configuración nunca aplicado; sin clasificación de errores; sin requeue | 🟨 | §H, Fases 2 y 7 |
| G8 | Scope tenant/company implícito (contexto ambiente) | 🟨 | §D, Fase 3 |
| G9 | Sin monitor ni historial de intentos | 🟨 | §I, §P, Fases 3 y 7 |
| G10 | Código muerto/duplicado: `QueueEmailCommand`, RazorLight, `sales_invoice_authorized.enabled` | 🟩 | §S |
| G11 | Intención perdida en silencio si el handler falla (excepción tragada) | 🟧 | §E, Fase 5 — ✅ resuelto (reconciliación) |
| G12 | RIDE generado dentro de la transacción de autorización SRI | 🟨 | §N, Fase 5 — ✅ resuelto (RIDE al enviar) |
| G13 | Adjuntos por ruta local del nodo | 🟨 | §N, Fase 5 — ✅ resuelto (referencias + IFileStorage) |
| G14 | Fallback de configuración campo por campo (mezcla host de empresa con password de instancia) | 🟨 | §O, Fase 3 |

---

## C. Arquitectura objetivo

```
 Módulos de negocio (Sales, Retentions, …)          Auth / Security
   hecho de dominio (evento)                          token emitido (hash, expiración, single-use)
        │ handler in-process, misma transacción          │ IPasswordResetLinkSender (puerto de Auth)
        ▼                                                ▼
 ┌──────────────────────────── Communications ─────────────────────────────┐
 │ ICommunicationQueue ── intención durable (INSERT … ON CONFLICT DO NOTHING) │
 │        │   identidad lógica central (CommunicationIdentity)                │
 │        ▼                                                                   │
 │ CommunicationOutbox  (SSOT del estado de entrega, un registro = 1 canal    │
 │        │               × 1 rol de destinatario)                            │
 │        ▼                                                                   │
 │ CommunicationOutboxProcessor (job)                                         │
 │   claim atómico + lease (FOR UPDATE SKIP LOCKED … RETURNING)               │
 │   composición (primera vez): ICommunicationSourceContributor (módulo       │
 │     origen) → destinatario, variables, referencias de adjuntos             │
 │   template: default embebido versionado | override CommunicationTemplate   │
 │   renderer único (placeholders, escape HTML)                               │
 │   adjuntos: ICommunicationAttachmentResolver (módulo dueño) → stream       │
 │   CommunicationRetryPolicy (Transient/Permanent/Configuration/Unknown)     │
 │   CommunicationDeliveryAttempt (historial)                                 │
 │   CommunicationSettingsResolver (System → instancia; Company → empresa)    │
 └──────────────────────────────────┬─────────────────────────────────────────┘
                                     ▼
                       IEmailSender → SmtpEmailSender (sustituible)
```

Nombres nuevos (solo los indispensables): `CommunicationScopeKind`, `CommunicationIdentity`,
`CommunicationDeliveryAttempt`, `CommunicationRetryPolicy`, `CommunicationFailureCategory`,
`ICommunicationSourceContributor`, `ICommunicationAttachmentResolver`, el registro de templates y su renderer, y
`ElectronicDocumentAuthorizedCommunicationHandler` (reemplaza al de Factura). Todo lo demás es evolución de
piezas existentes.

---

## D. Scope: System vs Company

`CommunicationScopeKind { System, Company }` es un dato del mensaje y una propiedad declarada de cada propósito.

| | **Company** | **System** |
|---|---|---|
| Uso | Factura, NC, Retención, estado de cuenta, avisos operativos | Password reset, verificación de email, alertas de seguridad, invitaciones, mensajes de la instalación |
| `TenantId` | Obligatorio | **No aplica** (NULL; el sujeto se traza por el origen, p. ej. el token) |
| `CompanyId` | Obligatorio | **No aplica** (NULL) |
| `BranchId` | Opcional | No aplica |
| Configuración de transporte | Perfil de la empresa; perfil de instancia solo según la política de fallback (§O) | Perfil de instancia, siempre |
| Template override | Permitido si el propósito lo declara | Prohibido |
| Visible en el monitor ERP | Sí, filtrado por empresa | No (operación de instancia) |

**Invariantes (fail-fast en el factory de dominio y con CHECK constraint en BD):**

1. `Company` ⇒ `TenantId` y `CompanyId` presentes (no vacíos); `BranchId` opcional.
2. `System` ⇒ `TenantId`, `CompanyId` y `BranchId` NULL.
3. El `ScopeKind` del mensaje debe coincidir con el scope que declara su propósito (`PASSWORD_RESET` solo
   System; `SALES_INVOICE_AUTHORIZED` solo Company). Una combinación inválida falla al construir la identidad,
   antes de persistir.

**Representación persistida (decisión definitiva de la fase 3).** "No aplica" es **NULL**, nunca un centinela:

```sql
CONSTRAINT ck_communication_outbox_scope CHECK (
  (scope_kind = 'Company' AND tenant_id IS NOT NULL AND company_id IS NOT NULL)
  OR
  (scope_kind = 'System' AND tenant_id IS NULL AND company_id IS NULL AND branch_id IS NULL)
)
```

- `CommunicationOutbox`, `CommunicationOutboxAttachment` y `CommunicationDeliveryAttempt` heredan de
  `SystemAggregateRoot`/`SystemBaseEntity` (no de `BaseEntity`, que impone `Guid TenantId` + `IMustHaveTenant`) e
  implementan `IOptionalCompanyScopeEntity` (`Guid?` tenant/empresa). La interfaz está restringida a Communications
  por un test de arquitectura.
- **Filtro global centralizado** (`EnterpriseQueryFilterConfigurator`, una rama nueva): misma semántica fail-closed
  que `ICompanyOperationalEntity`. Una fila NULL nunca coincide con un contexto (comparación SQL con NULL = falso),
  así que los mensajes System son invisibles para cualquier consulta de empresa y solo se ven con
  `AsPlatformQuery()`.
- **Idempotencia:** `ux_communication_outbox_idempotency (tenant_id, company_id, idempotency_key)` con
  `NULLS NOT DISTINCT` (PostgreSQL 16), de modo que las filas System también están protegidas.
- El dominio expone `CommunicationScope` (`Company(...)`/`System`); los módulos nunca construyen tenant/empresa
  sueltos.

**Por qué no `Guid.Empty`** (era la redacción inicial):
- `CompanyTenantInterceptor`, activo en producción, rechaza `TenantId`/`CompanyId = Guid.Empty` al guardar.
- `Guid.Empty` ya significa "tenant global" (`GlobalLoginHandler`), "sin contexto" y "actor del sistema"; agregar
  "instancia" sería un cuarto significado ambiguo.
- Impediría una FK futura.

Tabla separada para System: descartada (sería una segunda outbox).

El processor deja de depender del contexto ambiente. Las filas Company abren
`JobExecutionContext.Begin(tenant, company)` como hoy. Las filas System no abren contexto de tenant (`Begin` exige
tenant). En ambos casos la configuración se pide con el scope **explícito**:
`ResolveEmailAsync(CommunicationScope)`.

---

## E. Intención durable y relación con la outbox técnica

### Concepto

```
Hecho de negocio/seguridad → Communication Intent → ICommunicationQueue → fila CommunicationOutbox (Pending)
```

La **intención** es la fila de `CommunicationOutbox` en estado `Pending`, todavía sin componer. No es una tabla
nueva.

### Garantías

1. **Atomicidad con el hecho.** El handler in-process corre dentro de la transacción del hecho (`ErpDbContext`
   publica después del primer `SaveChanges` y antes del commit). `ICommunicationQueue` inserta la intención en esa
   misma transacción con `INSERT … ON CONFLICT (tenant_id, company_id, idempotency_key) DO NOTHING RETURNING id`
   (si no devuelve fila, lee la existente). Si el negocio hace rollback, la intención desaparece con él. Si el
   negocio confirma, la intención queda confirmada. Un duplicado concurrente nunca produce una violación de unique
   que tumbe la transacción del negocio (hoy podría ocurrir con `AddAsync` + índice único).
2. **La intención es barata y local.** Al crearla solo se escriben scope, propósito, canal, origen, rol de
   destinatario e identidad. No se lee el email, no se renderiza, no se genera RIDE ni se resuelve SMTP. Así la
   probabilidad de que falle es mínima y no hay E/S remota dentro de la transacción del negocio.
3. **SMTP nunca revierte el negocio.** La entrega ocurre después, en el job y fuera de esa transacción.
4. **Una falla al crear la intención no se pierde en silencio.** El handler no puede abortar un hecho fiscal ya
   ocurrido (la autorización SRI existe fuera del ERP), así que si falla captura la excepción y registra un
   **Error** estructurado. Cada puente de un hecho de negocio de scope Company declara además una
   **reconciliación**: una consulta periódica sobre la tabla dueña del hecho (p. ej. EDs `Authorized` con
   `email_on_authorization` activo, desde un horizonte configurable) que encola las intenciones faltantes. La
   identidad idempotente (§F) hace inofensiva la doble vía.
5. **Intenciones System interactivas** (password reset): se encolan de forma síncrona en el request. Si falla, se
   registra un Error sin token y la respuesta pública sigue siendo neutral (§L). El usuario puede volver a
   solicitarlo; el token emitido expira solo.

### `OutboxMessage` vs `CommunicationOutbox`

| | `OutboxMessage` / `OutboxProcessor` | `CommunicationOutbox` / `CommunicationOutboxProcessor` |
|---|---|---|
| Qué entrega | Domain events (registro técnico durable, futuro bus/analytics) | Mensajes a destinatarios externos o internos |
| Dueño | Infraestructura de persistencia | Communications |
| Consumidor | Hoy ninguno (marca como procesado) | Transporte por canal |

**No se fusionan ni se duplican.** El puente es el **handler in-process del domain event**: un handler de
Communications reacciona al evento dentro de la transacción y crea la intención. Communications **no** lee ni
deserializa `OutboxMessage`. La reconciliación consulta la tabla dueña del hecho, no la outbox técnica.

---

## Modelo oficial de mensaje (evolución de `CommunicationOutbox`)

Un registro = **una entrega por un canal a un rol de destinatario**. Varios destinatarios (cliente + copia a la
empresa) son varios registros con la misma fuente y distinto `RecipientRole`. Así cada uno tiene su propio estado,
sus reintentos y su auditoría. CC/BCC dentro de un mismo correo queda como mejora del transporte (§Transporte),
no como modelo.

| Concepto | Hoy | Objetivo | Cambio |
|---|---|---|---|
| Scope | — | `ScopeKind` | nuevo (filas existentes = `Company`) |
| Tenant/Company/Branch | `TenantId`, `CompanyId`, `BranchId` | Igual, con las invariantes de §D | semántica |
| Propósito / TemplateKey | `Purpose` | `Purpose` **es** la TemplateKey (catálogo `CommunicationPurposes`) | ampliar catálogo |
| Canal | `Channel` | Igual | — |
| Origen | `CorrelationType`, `CorrelationId` | `SourceModule` (nuevo), `SourceType`, `SourceId` (renombre de Correlation*) | renombre + 1 columna |
| Texto visible del origen | — | `SourceDisplay` (p. ej. "Factura 001-001-000000123"), lo aporta el contributor | nuevo |
| Traza | — | `TraceId` opcional (`BaseDomainEvent.CorrelationId`) | nuevo |
| Destinatario | `RecipientName/Email/Phone` | Igual + `RecipientRole` (`Primary`, `CompanyCopy`, `User`) | nuevo rol |
| Template | — | `TemplateVersion`, `TemplateSource` (`Default` / `CompanyOverride` / `DefaultFallback`), `TemplateOverrideId`, `Language` | nuevo |
| Contenido | `Subject`, `BodyHtml`, `BodyText` (obligatorio al crear) | Snapshot renderizado al componer; `null` hasta componer; `BodyHtml/BodyText` siempre `null` en mensajes sensibles | nullable hasta composición |
| Variables explícitas | — | `PayloadJson` (variables no sensibles de intenciones explícitas) | nuevo |
| Variables sensibles | — | `SensitivePayloadProtected` (`ISecretProtector`) + `SensitivePayloadScrubbedAtUtc` | nuevo |
| Composición | — | `ComposedAtUtc` | nuevo |
| Estado | 5 estados | + `Skipped` (no hay nada que entregar, con `SkipReason`) + `Expired` | ampliar enum (string) |
| Claim/lease | `ProcessingStartedAtUtc` | + `ClaimToken`, `LeaseUntilUtc` | nuevo |
| Intentos | `RetryCount`, `MaxRetries` | `AttemptCount`, `MaxAttempts` (snapshot de configuración al encolar) | semántica |
| Vencimiento | — | `ExpiresAtUtc` | nuevo |
| Error | `LastError` (mensaje crudo) | `LastFailureCategory` + `LastErrorSafeText` (sanitizado) | sanitización |
| Proveedor | — | `ProviderMessageId` (último envío exitoso) | nuevo |
| Idempotencia | `IdempotencyKey` (formato libre por handler) | Formato único `CommunicationIdentity` (§F) | central |
| Reenvío | — | `ResendOfCommunicationId`, `ResendSequence` | nuevo |
| Quién la solicitó | `CreatedBy` (`Guid.Empty` = sistema) | Igual | — |

**Estados:** `Pending → Processing → Sent | Failed | Skipped | Expired`, y `Pending/Failed → Cancelled`.
"Claimed" es `Processing` con `ClaimToken` vigente; no se agrega otro estado. `Sent`, `Skipped`, `Expired` y
`Cancelled` son terminales. `Failed` es terminal salvo un requeue explícito.

---

## F. Idempotencia

- **Se mantiene** el índice único `ux_communication_outbox_idempotency (tenant_id, company_id, idempotency_key)`.
  Con la representación de §D también protege a las filas System, porque no hay NULL que lo anule.
- **La identidad lógica la genera un solo lugar:** `CommunicationIdentity` (value object de Domain). Ningún
  handler, contributor ni caller arma claves. `QueueEmailRequest.IdempotencyKey` desaparece y se reemplaza por
  `Source` + `RecipientRole`.

```
v1:{scope}:{tenantId|-}:{companyId|-}:{purpose}:{channel}:{sourceType}:{sourceId}:{recipientRole}
```

- **El email del destinatario no forma parte de la identidad.** Si cambia el email del cliente, eso no genera una
  segunda comunicación del mismo hecho.
- Mismo hecho publicado dos veces, handler y reconciliación, o reintento del handler ⇒ misma identidad ⇒ una sola
  fila.
- **Reenvío manual explícito:** crea una fila nueva con `ResendOfCommunicationId`, `ResendSequence = n` e identidad
  `…:{recipientRole}:resend:{n}`. Puede ir a otro email. Requiere permiso y queda auditado. Nunca es una colisión
  accidental.
- Password reset: `SourceType = PasswordResetToken`, `SourceId = id del token`. Cada solicitud emite un token
  nuevo, así que cada solicitud legítima produce su propio mensaje, y el mismo token nunca produce dos.
- **Transición (Fase 5):** las filas existentes conservan su clave legacy. La reconciliación busca la intención
  previa por `(Purpose, SourceType, SourceId, RecipientRole)` (índice de correlación existente) y no por la clave,
  y solo considera hechos posteriores a un horizonte de corte (`ReconcileSinceUtc` de instancia). Así no se reenvían
  correos históricos.

---

## G. Concurrencia: claim atómico y lease

La garantía es de PostgreSQL, no del scheduler. `[DisableConcurrentExecution]` se agrega solo como defensa
secundaria, para reducir trabajo inútil.

**Claim** (una fila por iteración, en una transacción corta, patrón `FOR UPDATE` ya usado en el repo):

```sql
UPDATE communication_outbox o
SET    status = 'Processing',
       claim_token = @claimToken,
       lease_until_utc = now() + @lease,
       processing_started_at_utc = now(),
       attempt_count = o.attempt_count + 1          -- ver §H: Configuration no consume presupuesto
FROM (
    SELECT id FROM communication_outbox
    WHERE (status = 'Pending' AND scheduled_at_utc <= now()
           AND (next_attempt_at_utc IS NULL OR next_attempt_at_utc <= now()))
       OR (status = 'Processing' AND lease_until_utc < now())          -- recuperación de lease vencido
    ORDER BY priority DESC, scheduled_at_utc
    LIMIT 1
    FOR UPDATE SKIP LOCKED
) c
WHERE o.id = c.id
RETURNING o.id;
```

En la misma transacción se inserta la fila de `CommunicationDeliveryAttempt` (`Started`). Si se recuperó un lease
vencido, primero se cierra el intento anterior como `LeaseExpired`.

- **Fencing:** toda finalización (`Sent`, `Failed`, reprogramación, composición) es
  `UPDATE … WHERE id = @id AND claim_token = @claimToken AND status = 'Processing'`. Si afecta 0 filas, el worker
  perdió el lease: descarta su resultado y solo registra un warning. Un worker lento nunca pisa a otro.
- **Nunca se mantiene una transacción abierta durante SMTP.** El claim y la finalización son transacciones cortas;
  el envío ocurre entre ambas.
- **Lease:** `LeaseDuration` (instancia, por defecto 5 min) > timeout SMTP (instancia, por defecto 30 s) +
  resolución de adjuntos + margen. Se reclama una fila a la vez, hasta un presupuesto por ejecución (cantidad y
  tiempo), para que ningún lease venza por esperar en un lote.
- **Recuperación:** una fila `Processing` con lease vencido vuelve a ser reclamable. Nunca hay `Processing` eterno.
  El reintento se cuenta (`AttemptCount`), así que un worker que muere siempre en el mismo mensaje termina en
  `Failed`.
- **Ventana residual (inherente al email):** si SMTP aceptó el mensaje y el worker murió antes de finalizar, la
  recuperación lo reenvía. La entrega es **al menos una vez, con duplicación acotada al lease**. Se mitiga con un
  header `Message-ID` determinístico (`<{communicationId}@communications.zh-erp>`, independiente de la configuración SMTP), igual en todos los intentos,
  que permite a los clientes de correo deduplicar. Para mensajes sensibles el riesgo es aceptable: el segundo
  correo lleva el mismo enlace.

Seguro con varios procesos, varios nodos y varias instancias de Hangfire: `SKIP LOCKED` reparte las filas y el
fencing invalida resultados tardíos.

### Implementación efectiva — Fase 2 (ZH-COMMUNICATIONS-DELIVERY-HARDENING-01, 2026-10-02)

- **Componentes:** `CommunicationOutboxDeliveryStore` (claim, `MarkSentAsync`, `MarkFailedAsync`, `LoadOwnedAsync`),
  `CommunicationOutboxProcessor` (orquesta), `CommunicationRetryPolicy` (Domain), `CommunicationFailureClassifier`,
  `CommunicationDeliveryTiming`. Las transiciones `MarkProcessing`/`MarkSent`/`MarkFailed`/`IsDue` de la entidad se
  retiraron: ahora son `UPDATE` condicionados en PostgreSQL.
- **Claim real:** el del diseño, con tres diferencias. (1) No hay tabla de intentos (fase 3): el intento perdido de un
  worker muerto se cuenta sumando 1 a `retry_count` al recuperarlo. (2) Un `Processing` con `lease_until_utc` NULL
  (filas atascadas antes de la migración) cuenta como vencido. (3) La prioridad se ordena con un `CASE`
  (`High` > `Normal` > `Low`), porque la columna es texto; el `OrderByDescending` anterior ordenaba alfabéticamente.
  El `RETURNING` devuelve solo id, tenant, empresa, token, contadores, propósito, canal y si fue recuperación.
- **Instantes:** salen de `TimeProvider` (parámetros del SQL), no de `now()`. Supuesto: el desfase de reloj entre
  nodos es muy inferior al lease.
- **Un claim por iteración**, hasta 50 por ejecución: el lease nunca espera detrás de otros envíos.
- **Lease = 5 min; timeout SMTP = `Communications:Email:SmtpTimeoutSeconds`** (solo instancia; por defecto 30 s,
  acotado a [5, 120] s). Invariante probada: timeout máximo (120 s) + margen de trabajo (60 s) < lease. Un worker
  muerto libera la fila como máximo 5 min después del claim.
- **Timeout:** lo aplica el processor (para cualquier `IEmailSender`) y también `SmtpEmailSender`. `SmtpClient`
  envuelve la cancelación de su propio token como `SmtpException`; se convierte en `TimeoutException` según el
  estado de los tokens, no por el texto.
- **Fencing:** `MarkSentAsync`/`MarkFailedAsync` exigen `id + status = Processing + claim_token`; con 0 filas
  registran `CommunicationClaimLost` y no tocan la fila. `MarkSent` está fuera del `try` de envío: si falla tras
  aceptar SMTP (p. ej. BD caída), la fila no se marca fallida y la recupera el lease.
- **Message-ID:** `<{CommunicationOutbox.Id:N}@communications.zh-erp>` (`CommunicationMessageId`): depende solo del Id, nunca del remitente ni de otra configuración SMTP mutable; igual en todos los reintentos aunque cambie el SMTP (test), sin
  destinatario, tenant ni secretos (verificado en el `.eml` generado por System.Net.Mail: un único header). No se
  asume que el servidor SMTP deduplique.
- **Garantía real:** un solo claim vigente por fila + fencing + entrega **al menos una vez**. Ventana residual
  inevitable: SMTP acepta, el proceso muere antes de `MarkSent` y la recuperación reenvía (mismo Message-ID).
- **Multi-tenant:** el claim es cross-tenant y devuelve solo el scope. Carga, configuración y finalización corren
  dentro de `JobExecutionContext.Begin(tenant, empresa)` de cada fila, con los filtros globales activos.
- **Migración `20261003010236_CommunicationDeliveryHardening`** (aditiva): `claim_token uuid`,
  `lease_until_utc timestamptz`, `failure_category varchar(30)` e índice parcial `ix_communication_outbox_claimable
  (status, scheduled_at_utc) WHERE status IN ('Pending','Processing')`. `EXPLAIN` con 20 000 filas `Sent` + 60
  reclamables: `Index Scan using ix_communication_outbox_claimable` (no recorre las terminales).
- **Hangfire:** `[DisableConcurrentExecution(10)]` en `ProcessCommunicationsJob`, solo como defensa secundaria.
- **Eventos de log:** `CommunicationClaimed`, `CommunicationRecoveredAfterLease`, `CommunicationDeliverySucceeded`,
  `CommunicationDeliveryFailed`, `CommunicationClaimLost` (id, tenant, empresa, propósito, canal, contadores,
  categoría). Nunca cuerpo, destinatario, adjuntos ni credenciales. La traza completa solo se registra para fallos
  `Unknown` (`CommunicationUnknownFailure`).

### Implementación efectiva — Fase 3 (ZH-COMMUNICATIONS-CONTRACT-01, 2026-10-02)

- **Alcance:** `CommunicationScopeKind` + `CommunicationScope` (value object con invariantes) y almacenamiento NULL
  (§D). Ver la tabla de abajo para la diferencia con el diseño de §D.
- **Request explícito:** `ICommunicationQueue.EnqueueAsync(CommunicationRequest)` es el único camino
  (`QueueEmailAsync`, `QueueEmailRequest` y `QueueEmailCommand` se retiraron).
  - La request trae `Scope`, `Purpose`, `Source`, `RecipientRole`, destinatario, contenido, adjuntos, canal,
    prioridad, programación y `MaxRetries` opcional.
  - La cola no lee el tenant del contexto. Solo usa la empresa autenticada como **guarda**: una request Company para
    otra empresa se rechaza.
  - `MaxRetries` sale del perfil del mismo alcance.
- **Origen:** `SourceModule`/`SourceType`/`SourceId`. Las columnas `correlation_type`/`correlation_id` se
  **renombraron** a `source_type`/`source_id` (no conviven dos representaciones); el índice
  `ix_communication_outbox_correlation` pasa a ser `ix_communication_outbox_source`.
- **Identidad:** `CommunicationIdentity` es el único constructor. Clave = `cid:v1:` + SHA-256 (hex, 71 caracteres
  en total) de `v1|scope|tenant|empresa|propósito|canal|módulo|tipo|id|rol|secuenciaReenvío`.
  - Difiere de §F (clave legible): el hash la deja acotada y opaca; los componentes ya están en sus columnas.
  - Las filas previas conservan su clave legacy.
- **Encolado idempotente:** `CommunicationOutboxRepository.EnqueueAsync` = `INSERT … ON CONFLICT DO NOTHING`
  (columnas tomadas del modelo EF) + lectura de la existente.
  - Corre en la transacción ambiente si existe; si no, en una propia y corta (comunicación + adjuntos).
  - La colisión nunca lanza ni aborta la transacción de negocio. Si la otra transacción revierte, el segundo
    encolado crea la fila. Probado contra PostgreSQL.
- **Registro de propósitos:** `CommunicationPurposes.Get(code)` → scope, canales, `IsSensitive`,
  `AllowsManualResend`. El código es la TemplateKey.
  - `SALES_INVOICE_AUTHORIZED`: Company, no sensible, admite reenvío.
  - `PASSWORD_RESET`: System, sensible, sin reenvío; reservado sin productor hasta la fase 6.
- **Rol del destinatario:** `Customer`, `Supplier`, `User`, `CompanyCopy`. La factura usa `Customer`.
- **Reenvío manual:** nueva comunicación con `ResendOfCommunicationId` + `ResendSequence` e identidad
  `ForResend(n)`; no colisiona con el índice. Sin UI todavía.
- **Historial `CommunicationDeliveryAttempt`** (tabla `communication_delivery_attempts`):
  - El claim abre el intento `n+1` en la misma sentencia y cierra como `Abandoned` el intento abierto de un worker
    muerto.
  - La finalización cierra el intento propio en una transacción corta con la comunicación: `Sent`/`Failed` con
    categoría, `provider_code` (p. ej. `smtp:421`) y `provider_message_id`.
  - Si el fencing falló, el intento queda `ClaimLost` y la comunicación no se toca.
  - `ErrorSafeText`/`LastError` = tipo de error + código. Nunca el mensaje de la excepción, porque los rechazos SMTP
    incluyen el destinatario.
- **ProviderMessageId ≠ Message-ID propio:** `IEmailSender` devuelve `EmailDeliveryReceipt(ProviderMessageId)`;
  System.Net.Mail no expone uno, así que queda null (nunca inventado).
- **Configuración por alcance:** `ResolveEmailAsync(scope)`. Company → OrgSettings de esa empresa + fallback de
  instancia actual; System → solo instancia. El processor ya no usa el contexto ambiente para resolverla.
- **Migración `20261003020718_CommunicationContract`:**
  - Agrega `scope_kind` (backfill `Company` para todas las filas previas, que tenían tenant y empresa),
    `source_module` y `recipient_role` (backfill `Sales`/`Customer` solo para filas de factura demostrables; el
    resto queda NULL), `resend_*` y la tabla de intentos.
  - Hace nullable tenant/empresa de outbox y adjuntos, agrega la CHECK y el índice `NULLS NOT DISTINCT`.
  - El `Down` falla de forma segura si existen filas System: no las convierte en centinela.

---

## H. Política de reintento

> **Fase 2 implementada (diferencias con lo de abajo hasta la fase 3):** backoff `min(2^n, 60) min` tras el n-ésimo
> fallo (2, 4, 8, 16, 32, 60), sin jitter. `Configuration` y `Permanent` terminan en `Failed` al primer fallo, con
> `failure_category`, sin busy-loop ni consumir los demás intentos. El reintento de `Configuration` "cada 15 min hasta
> `ExpiresAtUtc`" requiere `ExpiresAtUtc` (fase 3); hasta entonces, corregir la configuración exige reencolar
> explícitamente (fase 7). `MaxRetries` ya tiene efecto: se copia al encolar desde el perfil resuelto. Un fichero
> adjunto inexistente (`FileNotFoundException`) se clasifica como Transient: reintenta hasta agotar.

`CommunicationRetryPolicy` (Domain, pura y testeable) es la **única** dueña de presupuesto, backoff y vencimiento.

| Categoría (`CommunicationFailureCategory`) | Ejemplos | Comportamiento |
|---|---|---|
| **Transient** | Timeout, `IOException`, SMTP 4xx, servicio no disponible, almacenamiento de adjunto no accesible | Reintento con backoff exponencial `min(2^(n-1) min, 60 min)` ± 20 % jitter; al agotar `MaxAttempts` → `Failed` |
| **Permanent** | SMTP 5xx de buzón/dirección (550/553), adjunto demasiado grande (552), origen inexistente, variable obligatoria faltante, canal no soportado | `Failed` inmediato, sin reintento automático |
| **Configuration** | Perfil incompleto o deshabilitado, autenticación SMTP (535), TLS | **No consume presupuesto**. Queda `Pending` con reintento fijo cada 15 min hasta `ExpiresAtUtc` → `Failed(Configuration)`. El monitor lo muestra como "bloqueado por configuración" |
| **Unknown** | Cualquier otra excepción, lease vencido | Como Transient (consume presupuesto) |

- **Dueño real de `MaxRetries`:** Company → `communications.email.max_retries` (OrgSettings, ya existe; significa
  intentos automáticos totales). System → `Communications:Email:MaxRetries` (instancia). Se copia en `MaxAttempts`
  **al encolar**, así un cambio de configuración no reescribe mensajes ya creados.
- **Vencimiento (`ExpiresAtUtc`):** Company = creación + horizonte de instancia (por defecto 7 días). Sensible =
  expiración del token que fija Auth. Una fila vencida al ser reclamada pasa a `Expired` sin enviarse.
- **Clasificación:** el transporte lanza un error tipado (categoría, código del proveedor, texto seguro). La
  composición clasifica sus propios errores. Nada se clasifica por el texto del mensaje.
- **Composición y adjuntos:** cada adjunto se declara `Required` u `Optional`. Si falta uno `Optional`, se envía sin
  él y se registra en el intento. Si falta uno `Required`, es Transient hasta el vencimiento. La migración de
  Factura conserva el comportamiento actual (XML y RIDE opcionales).
- **Requeue** (manual): `Failed`/`Expired` → `Pending`, abre un nuevo presupuesto y recalcula el vencimiento. Está
  prohibido en mensajes sensibles: el usuario vuelve a solicitarlo.
- **Cancel:** `Pending`/`Failed` → `Cancelled`. No se permite sobre `Processing`, porque el envío puede estar en
  curso.
- **Hangfire:** el job captura todo, así que `AutomaticRetry` de Hangfire nunca reintenta (no hay doble mecanismo).
  Se mantiene así.

---

## I. Historial de intentos

`CommunicationDeliveryAttempt` (tabla hija `communication_delivery_attempts`, mismo scope y misma representación
que su mensaje):

| Campo | Contenido |
|---|---|
| `CommunicationId`, `AttemptNumber`, `ClaimToken` | Identidad del intento |
| `Kind` | `Delivery`, `Composition`, `LeaseExpired`, `ManualRequeue`, `ManualCancel` |
| `StartedAtUtc`, `CompletedAtUtc` | Tiempos |
| `Transport` | `smtp` (futuro: otros) |
| `Result` | `Sent`, `Failed`, `Rescheduled`, `Skipped`, `Expired` |
| `FailureCategory`, `ProviderCode` | p. ej. `Transient`, `421` |
| `ProviderMessageId` | Si el proveedor lo entrega |
| `ErrorSafeText` | Sanitizado, ≤ 1000 caracteres |

**Nunca contiene** passwords, tokens, enlaces sensibles, credenciales SMTP, cuerpos ni variables.
`CommunicationOutbox` sigue siendo el SSOT del estado actual; los intentos son historial y no se leen para decidir
el estado. Las acciones manuales (requeue, cancel, resend) además emiten un evento de auditoría por la
infraestructura existente (ADR-022, `IAuditEvent`), con el actor.

---

## J. Templates

### Ubicación y forma

```
ERP.Application/Modules/Communications/Templates/
  CommunicationTemplateCatalog.cs          ← definiciones: key, version, scope, canal, contrato de variables
  SALES_INVOICE_AUTHORIZED/v1/es/subject.txt | body.html | body.txt   (EmbeddedResource)
```

`CommunicationTemplateDefinition`:

- `TemplateKey` (= `Purpose`), `Version` (entero; cambiar el texto de salida = nueva versión), `ScopeKind`,
  `Channel`, `Languages`, `AllowsCompanyOverride`, `IsSensitive`.
- **Contrato de variables:** nombre, `Required`, `Kind` (`Text`, `Multiline`, `Url`), `Sensitive`. El asunto no puede
  usar variables `Sensitive`.

| TemplateKey | Scope | Sensible | Override empresa | Estado |
|---|---|---|---|---|
| `SALES_INVOICE_AUTHORIZED` | Company | No | Sí | Existe (en el handler); se migra en Fase 4 sin cambiar la salida |
| `SALES_CREDIT_NOTE_AUTHORIZED` | Company | No | Sí | Activo (Fase 5, default v1) |
| `RETENTION_AUTHORIZED` | Company | No | Sí | Activo (Fase 5, default v1) |
| `ACCOUNT_STATEMENT` | Company | No | Sí | Futuro (sin ticket) |
| `PASSWORD_RESET` | System | **Sí** | No | Futuro (Fase 6) |
| `EMAIL_VERIFICATION` | System | **Sí** | No | Futuro (sin ticket) |
| `USER_INVITATION` | System | **Sí** (si lleva token) | No | Futuro (sin ticket) |

### Override por empresa

- Se reutiliza la entidad existente `CommunicationTemplate` (`Code` = TemplateKey, `Channel`, `Language`) y se le
  agrega `BaseTemplateVersion`. `BranchId` queda sin uso.
- Al guardar, se valida contra el contrato vigente: un placeholder desconocido es error 422.
- Al renderizar, si el override ya no cumple el contrato (cambió la versión por defecto), se usa el default con
  `TemplateSource = DefaultFallback`, y el monitor lo muestra. Nunca se bloquea la entrega por un override inválido.
- La UI de overrides no está planificada: no hay necesidad real todavía.

### Renderer único

- Sintaxis `{{ nombre }}`, sin condicionales ni loops.
- En HTML, toda variable se escapa (`HtmlEncode`) por defecto. Las variables `Url` deben ser http(s) absolutas y se
  escapan para atributo. En el asunto se eliminan CR/LF (contra inyección de headers).
- Variable obligatoria faltante o placeholder desconocido → error de composición `Permanent`. El mensaje de error
  nunca incluye valores.
- **Determinístico:** el renderer no formatea. Los valores llegan ya formateados por el contributor con los
  helpers estándar (montos `InvariantCulture` `0.00`, fechas por ADR-034 en la zona horaria de la empresa).
- `BodyMaxLen` (16 000) se amplía en la Fase 4 si un template de marca lo necesita.
- **RazorLight:** no se adopta. Solo se reconsidera si aparece una necesidad real de condicionales, loops o
  estructuras complejas. El paquete hoy no tiene uso → REMOVE EVENTUALLY (Fase 4).

---

### Implementación efectiva — Fase 4 (ZH-COMMUNICATIONS-TEMPLATES-01, 2026-10-02)

| Pieza | Implementación real |
|---|---|
| TemplateKey | = Purpose (`CommunicationPurposes`). Defaults registrados: solo `SALES_INVOICE_AUTHORIZED`. Los propósitos reservados (`PASSWORD_RESET`) no tienen default hasta su fase: encolarlos da `COMMUNICATION_TEMPLATE_NOT_FOUND` |
| Default | `CommunicationDefaultTemplates`: clases C# en `ERP.Application/Modules/Communications/Templates`, versionadas (`SalesInvoiceAuthorizedV1`). Forman parte del release; no dependen de BD ni seeds |
| Override | Entidad existente `CommunicationTemplate` (empresa, `Code` = TemplateKey, canal Email, idioma `es`, `IsActive`) + columna nueva `revision` (1 al crear, +1 por cambio de contenido). Es la "versión" registrada cuando se usa |
| Resolver | `ICommunicationTemplateResolver` (único): System → default sin consultar overrides; Company → override activo de ESA empresa (filtro global) o default |
| Override inválido | `COMMUNICATION_TEMPLATE_INVALID` al encolar, sin fallback (difiere del `DefaultFallback` del diseño). Nunca se envía contenido corrupto; la comunicación queda registrada como `Failed` (ver "Durabilidad ante fallo de template") |
| Renderer | `CommunicationTemplateRenderer` (único, estático, sin RazorLight). Ver reglas abajo |
| Variables | `ICommunicationTemplateModel` por template (p. ej. `SalesInvoiceAuthorizedTemplateModel`), con valores ya formateados por el módulo origen. El contrato (`CommunicationTemplateVariable`) es el del default; un override no puede salirse de él |
| Momento | AL ENCOLAR (`CommunicationQueue`). La fila guarda asunto/cuerpo + `template_key`/`template_version`/`template_source`. El processor envía lo guardado |
| Identidad | La versión del template NO forma parte de `CommunicationIdentity`: cambiar un template nunca duplica la comunicación de un hecho |
| Reenvío manual | Al no existir todavía, la regla queda fijada: un reenvío es una comunicación nueva y se renderiza con el template vigente en ese momento |
| Errores | `COMMUNICATION_TEMPLATE_NOT_FOUND` / `_INVALID` (categoría de fallo Configuration) y `_RENDER_FAILED` (Permanent), Validation en la API y nunca SMTP. La cola no lanza: devuelve el código en `QueuedCommunicationDto.TemplateFailureCode` y persiste la fila `Failed`. Los mensajes nombran variables, nunca valores |
| Durabilidad ante fallo de template | El hecho de negocio nunca se revierte y la intención nunca se pierde. En la MISMA outbox, `CommunicationOutbox.CreateEmailTemplateFailure`: `Failed` + `failure_category` + `last_error` con el código, misma identidad (repetir o reconciliar no duplica), destinatario, origen, adjuntos y `template_payload_json` (variables, solo en propósitos no sensibles) para re-renderizar y reencolar tras corregir el template. Sin asunto ni cuerpo inventados. La CHECK `ck_communication_outbox_content` impide que una fila sin contenido quede `Pending`/`Processing`/`Sent`: un reencolado futuro debe re-renderizar primero. Probado por el flujo real (`ErpDbContext` → handler de Factura → cola → resolver → PostgreSQL) |
| Logs | `CommunicationTemplateRendered`: id, TemplateKey, versión, fuente y SourceId. Sin variables, asunto ni cuerpo |
| Filas previas | La migración marca las facturas como `template_key = SALES_INVOICE_AUTHORIZED`, `template_source = Legacy`, sin versión (se armaban en código). El resto queda NULL |
| RazorLight | Sin consumidores → REMOVE EVENTUALLY (no se retira en este ticket) |

**Reglas del renderer:**
- Sintaxis `{{Nombre}}` (admite espacios internos). Nombres `[A-Za-z][A-Za-z0-9_]*`, sin navegación (`{{A.B}}` es inválido).
- Fallos:
  - placeholder no declarado o mal formado (`{{`/`}}` sueltos) → `INVALID`;
  - variable obligatoria ausente o vacía, variable no declarada en el contrato, modelo de otro template, o asunto/cuerpo fuera de límites → `RENDER_FAILED`.
- HTML: toda variable pasa por `WebUtility.HtmlEncode` (el mismo escape del correo anterior; no existe inserción cruda).
- Texto: sin escape.
- Asunto: CR/LF → espacio.
- Determinístico; un valor con `{{…}}` no se reinterpreta.

**Factura migrada:** el handler arma solo `SalesInvoiceAuthorizedTemplateModel`. La salida del default v1 coincide byte a byte con la anterior (`SalesInvoiceAuthorizedGoldenEmail`, capturado antes de la migración, incluido el escape `Jos&#233; &amp; Hijos &lt;S.A.&gt;`).

## K. Comunicaciones sensibles

Aplica a los propósitos `IsSensitive` (password reset, verificación de email, MFA, invitaciones con token).

1. **El valor sensible** (p. ej. el enlace con el token) viaja en la intención solo cifrado:
   `SensitivePayloadProtected = ISecretProtector.Protect(json)`, el mismo mecanismo que la contraseña SMTP y la del
   certificado.
2. **Nunca se persiste en claro:** `BodyHtml` y `BodyText` quedan en `null` para estos mensajes. El cuerpo se
   renderiza en memoria al enviar y se descarta. `Subject` sí se persiste (el contrato le prohíbe variables
   sensibles).
3. **Nunca se registra:** ni en logs, ni en `LastErrorSafeText`, ni en `ErrorSafeText`, ni en auditoría. En logs los
   emails se enmascaran (`j***@dominio.com`).
4. **Scrub:** en cualquier estado terminal (`Sent`, `Failed`, `Cancelled`, `Expired`) se borra
   `SensitivePayloadProtected` y se fija `SensitivePayloadScrubbedAtUtc`. Un barrido del job aplica el scrub a filas
   cuyo `ExpiresAtUtc` ya pasó.
5. **Sin requeue ni resend** de mensajes sensibles.
6. **Auth sigue siendo dueño del token** (emisión, hash, expiración, single-use, invalidación). Communications no lo
   genera, no lo valida, no decide expiración y no conoce contraseñas: solo recibe un valor opaco cifrado y una
   fecha de vencimiento.

---

## L. Password Reset

### Objetivo

```
Auth (ForgotPasswordHandler)
  → PasswordResetTokenIssuer: token raw + hash + expiración + invalidación de previos   (sin cambios)
  → IPasswordResetLinkSender (puerto de Auth, se conserva)
       implementación: CommunicationsPasswordResetLinkSender  (Communications)
         → ICommunicationQueue: PASSWORD_RESET, scope System, destinatario explícito,
           enlace en SensitivePayload, Source = (Auth, PasswordResetToken, tokenId), ExpiresAt = expiración del token
Communications → template PASSWORD_RESET → perfil de instancia → IEmailSender
```

- El puerto se amplía en la Fase 6 para recibir `tokenId`, `expiresAtUtc` y opcionalmente el nombre visible. Hoy
  el issuer devuelve `(raw, expires)`.
- Auth no conoce SMTP. Communications no conoce la lógica del token. La prueba arquitectónica se cumple.
- `LoggingPasswordResetLinkSender` se elimina. En desarrollo se usa un SMTP local de captura (configuración de
  instancia), nunca un log con el enlace.

### Corrección prioritaria de seguridad (Auth, Fase 1, independiente de Communications)

1. **Quitar el token del log:** el log registra "solicitud de reset para usuario {UserId}", sin email ni enlace.
2. **Respuesta neutral:** `forgot-password` responde siempre lo mismo (éxito con mensaje genérico) para cuenta
   inexistente, inactiva, múltiples cuentas o cuenta válida. Los motivos solo quedan en el log interno, sin el email
   en claro.
3. **Rate limit:** política por IP (`[EnableRateLimiting]`, mismo mecanismo que `auth-refresh-ip`) más un límite
   por cuenta en el handler (tokens emitidos en la última hora vía `PasswordResetToken`). Si se excede, no emite y
   responde lo mismo (neutral).
4. Recomendado: no hacer diferencias de trabajo observables entre ramas (tiempo de respuesta).

Mientras no exista la Fase 6, el reset por email no funciona en producción. Se documenta como limitación conocida
(el login con contraseña temporal + `RequirePasswordReset` sigue disponible).

---

## M. Destinatarios: contributors por módulo

`ICommunicationSourceContributor` (Communications/Application; implementado en el módulo origen, mismo patrón que
`ISourceDocumentSummaryProvider`):

- Declara `SourceModule` y los `SourceType` que resuelve.
- `ComposeAsync(source, recipientRole)` devuelve los destinatarios del rol, las variables del template (ya
  formateadas), `SourceDisplay` y referencias de adjuntos propios del negocio, si los hay.
- Lee **snapshots** del documento (p. ej. email del cliente en la factura), no el maestro actual.
- Si no hay destinatario → `Skipped(NoRecipient)`: visible y auditable, ya no solo una línea de log.

### Documentos electrónicos autorizados (un solo handler)

`ElectronicDocumentAuthorizedCommunicationHandler` (Communications) reemplaza a
`SalesInvoiceAuthorizedCommunicationHandler`:

1. Recibe `ElectronicDocumentAuthorizedEvent`, lee el ED con su tenant/empresa explícitos.
2. Mapea `DocumentType` → propósito (`Invoice` → `SALES_INVOICE_AUTHORIZED`, `CreditNote` →
   `SALES_CREDIT_NOTE_AUTHORIZED`, `Retention` → `RETENTION_AUTHORIZED`). Si el tipo o el `SourceModule` no tienen
   contributor, no hace nada.
3. Revisa `electronic_documents.email_on_authorization` (única autoridad) con el contexto explícito.
4. Encola la intención `Primary` y, si `communications.send_copy_to_company_email` está activo, `CompanyCopy`
   (destinatario `Company.CorporateEmail`, resuelto por Communications, no por el contributor).
5. Componer, renderizar y adjuntar ocurre después, en el processor.

Contributors: **Sales** (Factura y NC: cliente del snapshot) y **Retentions** (Retención: proveedor). No se copian
handlers. ElectronicDocuments solo publica el hecho fiscal (ADR-038 D14).

### Implementación efectiva — Fase 5 (ZH-EDOC-COMMUNICATIONS-01, 2026-10-03)

Concreción del diseño anterior. Prevalece sobre él donde difiere.

```
ElectronicDocumentAuthorizedEvent (dentro de la transacción fiscal)
  → ElectronicDocumentAuthorizedCommunicationHandler        (único; absorbe fallos)
    → IElectronicDocumentCommunicationService               (camino único: evento y reconciliación)
      → contributor por (SourceModule, DocumentType)         (módulo dueño; solo datos)
      → ICommunicationQueue → CommunicationOutbox            (INSERT … ON CONFLICT DO NOTHING)
Job process-communications → processor
  → ICommunicationAttachmentResolver → proveedor del módulo dueño (XML: ElectronicDocuments; RIDE: Ride)
  → IEmailSender (bytes ya resueltos)
Job reconcile-electronic-document-communications (cada 10 min) → mismo servicio, misma identidad
```

| Ruta | Propósito | Origen oficial | Rol | Destinatario |
|---|---|---|---|---|
| `Sales` / Invoice | `SALES_INVOICE_AUTHORIZED` (v1, golden intacto) | `Sales`/`SalesInvoice` | Customer | snapshot del cliente de la factura |
| `Sales` / CreditNote | `SALES_CREDIT_NOTE_AUTHORIZED` (v1 nuevo) | `Sales`/`SalesReturn` | Customer | snapshot del cliente de la factura modificada (el del XML de la NC) |
| `Retentions` / Retention | `RETENTION_AUTHORIZED` (v1 nuevo) | `Retentions`/`RetentionDocument` | Supplier | contacto primario del tercero (`BusinessPartnerContactResolver`, regla única movida desde Ventas); nunca inventado |

- **Contrato.** `IElectronicDocumentCommunicationContributor` declara `SourceModule` y
  `PurposesByDocumentType`. `ContributeAsync` devuelve `ElectronicDocumentCommunicationContribution` (propósito,
  origen, rol, nombre/correo, sucursal, número del comprobante y modelo tipado) o un fallo estructurado
  (`SOURCE_NOT_FOUND` / `SOURCE_NOT_ELIGIBLE`). `ElectronicDocumentCommunicationContributorResolver` rechaza dos
  contributors para la misma ruta. El servicio exige que el propósito sea el de la ruta y que el origen sea el mismo
  par (módulo, id) del comprobante.
- **Diferencia con §M.** No se usa `ICommunicationSourceContributor.ComposeAsync` ni "componer en el processor": se
  renderiza al encolar (fase 4). `CompanyCopy` queda diferido.
- **Adjuntos (concreción de §N).** `communication_outbox_attachments.reference_id` = `ElectronicDocument.Id`. Las
  referencias las agrega el servicio genérico, no cada contributor: XML si `AuthorizedXmlPath` existe, más RIDE.
  Al enviar, `CommunicationAttachmentResolver` materializa:
  - bytes guardados → tal cual;
  - `FileStoragePath` → `IFileStorage` (corrige el `File.Exists` sobre rutas relativas);
  - referencia → `ICommunicationAttachmentContentProvider` del tipo.

  Proveedores:
  - `ElectronicDocumentAuthorizedXmlAttachmentProvider`: repositorio de ED + `IFileStorage`, byte a byte;
    obligatorio.
  - `RidePdfCommunicationAttachmentProvider`: `GetOrGenerateRideQuery` + `IFileStorage`. `PendingSource` →
    reintento; cualquier otro desenlace sin PDF → se omite y el correo sale con el XML legal.

  Un adjunto obligatorio ausente lanza `CommunicationAttachmentException` → `Transient` (backoff normal).
  `EmailAttachment` es `(FileName, ContentType, byte[])`: el transporte no conoce rutas. CHECK
  `ck_communication_outbox_attachments_content_source`.
- **Destinatario ausente (transversal, en la cola).** Correo nulo, vacío o inválido (incluye display name) → fila
  `Failed`/`Permanent` con `COMMUNICATION_RECIPIENT_MISSING`, sin correo ni contenido, con las variables y los
  adjuntos por referencia. No se renderiza ni se lanza. Usa la misma identidad. Factory única:
  `CommunicationOutbox.CreateEmailFailedBeforeDelivery` (antes `CreateEmailTemplateFailure`). DTO:
  `QueuedCommunicationDto.FailureCode`.
- **Template inválido.** Misma semántica de la fase 4 para los tres propósitos.
- **Reconciliación.** `ElectronicDocumentCommunicationReconciliationQuery` es SQL cross-tenant explícito y solo
  devuelve ids. Busca comprobantes `Authorized` de una ruta soportada, **de cualquier antigüedad**, sin cambios en
  los últimos 5 min, que no tengan fila en la outbox con el mismo tenant, empresa, `source_module`, `SourceType`
  (declarado por la ruta del contributor), `source_id` y propósito, en cualquier estado.

  Horizonte (verificación final): se eliminó la ventana de 7 días sobre `created_at`. Medido con EXPLAIN ANALYZE
  sobre 100k comprobantes, no ahorraba ningún escaneo (`created_at` no tiene índice en `electronic_documents`,
  tabla CLOSED): solo descartaba faltantes, incluidos los autorizados tarde. El conjunto de faltantes se calcula sin
  `LIMIT` en un CTE `MATERIALIZED`, para que el planner elija el anti-join por costo (con `LIMIT` apostaba por un
  nested loop que sondeaba cada comprobante, 3× más lento). Después se ordena y se pagina. El costo es lineal en
  autorizados + outbox, como antes; no hay índice nuevo.

  El reconciliador:
  - resuelve la preferencia una vez por empresa (sin duplicar su SSOT) y excluye en SQL a las desactivadas;
  - orden global determinístico (`created_at`, `id`), más antiguos primero, páginas de 50 y hasta 200 por corrida;
  - usa un cursor process-local (singleton): la corrida siguiente continúa tras el último examinado y vuelve al
    inicio al llegar al final. Así los faltantes que el servicio omite y siguen faltando (origen aún no autorizado
    o inexistente) nunca bloquean a los posteriores. Si se pierde en un reinicio, solo se reexamina desde el más
    antiguo;
  - procesa cada documento en su propio scope bajo `JobExecutionContext`.

  No reencola `Failed` ni crea tablas. Job Hangfire `*/10 * * * *` con `[DisableConcurrentExecution]` como
  defensa secundaria; la exclusión real es la identidad única.
- **Logs** (sin PII):
  - 4230–4237 `ElectronicDocumentCommunication{Requested,Skipped,Queued,SourceMissing,Failed,Reconciled,ReconciliationFailed,ReconciliationRun}`;
  - 4212 `CommunicationRecipientMissing`;
  - 4220 `CommunicationAttachmentSkipped`.
- **Tests:**
  - unitarios: handler/servicio/contributors con golden de Factura; cola; resolvedor y proveedores de adjuntos;
  - PostgreSQL (`ElectronicDocumentCommunicationIntegrationTests`):
    - los tres tipos con XML/RIDE reales al enviar;
    - sin correo por tipo;
    - override inválido por tipo;
    - evento duplicado;
    - reconciliación simple, repetida y concurrente con eventos duplicados;
    - preferencia y antigüedad mínima;
    - multi-tenant;
  - arquitectura (`CommunicationsBoundaryTests`, baseline 0): un solo handler; contributors solo datos;
    ElectronicDocuments y el gateway SRI sin Communications ni SMTP; el camino del evento sin RIDE ni archivos; el
    transporte sin filesystem.

---

## N. Adjuntos

- **Se guardan referencias, no bytes.** `CommunicationOutboxAttachment` evoluciona a una **referencia lógica
  durable**: `OwnerModule`, `ReferenceKind` (`AuthorizedXml`, `RidePdf`, …), `ReferenceId`, `FileName`,
  `ContentType`, `Required`.
- **El módulo dueño resuelve.** `ICommunicationAttachmentResolver` (implementado por el dueño) devuelve un stream al
  momento del envío. ElectronicDocuments resuelve `AuthorizedXml` con `IElectronicDocumentXmlStorageService` y
  `RidePdf` con `GetOrGenerateRideQuery` / `IRidePdfStorageService`, sobre `IFileStorage`. No se asume el
  filesystem local del nodo. Las referencias de ED (XML + RIDE) son comunes a todo documento autorizado y las
  aporta ElectronicDocuments, no cada contributor.
- **El RIDE se genera fuera de la transacción de autorización SRI** (en la composición del job) y nunca dentro del
  sender: `IEmailSender` solo recibe streams.
- `FileStoragePath` (ruta local) se mantiene solo como lectura para las filas existentes → REMOVE EVENTUALLY.
- `BinaryContent`: sin consumidor → REMOVE EVENTUALLY, salvo que aparezca un adjunto generado que no tenga dueño
  con almacenamiento.
- No se vuelve a guardar XML ni PDF en la outbox.

---

## Transporte de email

- **KEEP** `IEmailSender` / `SmtpEmailSender`: único camino técnico de envío. Solo lo invocan el processor y
  `SendTestEmailCommandHandler` (excepción legítima: prueba síncrona de configuración, sin outbox).
- El contrato se prepara para sustituir la implementación sin afectar a Communications:
  - entrada: mensaje + perfil resuelto + `Message-ID`;
  - salida: resultado con `ProviderMessageId`;
  - errores: excepción tipada con categoría, código del proveedor y texto seguro.
- No se migra a MailKit todavía.
- Mejoras registradas para el transporte: timeout explícito (Fase 2), clasificación de errores (Fase 2),
  `ProviderMessageId` (Fase 3), CC/BCC y TLS implícito en el puerto 465 (cuando haya necesidad; con
  System.Net.Mail solo se logra cambiando de implementación).

---

## O. Configuración

| Nivel | SSOT | Contenido | Consumido por |
|---|---|---|---|
| **Instancia** | `Communications:Email:*` (IConfiguration/env) | Perfil SMTP de la instalación, `MaxRetries` System, timeout SMTP, lease, horizonte de vencimiento, `AllowCompanyFallback`, `ReconcileSinceUtc` | Scope System (siempre); scope Company solo como fallback |
| **Empresa** | OrgSettings `communications.email.*` (scope Company) + pantalla existente | Perfil SMTP de la empresa, `max_retries`, `default_language`, `send_copy_to_company_email`; toggles de negocio en su módulo (`electronic_documents.email_on_authorization`) | Scope Company |
| **Mensaje** | `CommunicationOutbox` | Propósito, destinatario(s), template/versión, adjuntos, origen | Processor |

**Única puerta:** `CommunicationSettingsResolver.ResolveEmailAsync(CommunicationScope)`.

**Política de fallback Company → instancia (corrige G14):**

1. Si la empresa tiene un perfil propio completo y habilitado, se usa ese perfil **completo**. Nunca se mezclan
   campos de empresa con campos de instancia.
2. Si la empresa deshabilitó explícitamente el correo (`enabled = false`), el mensaje pasa a
   `Skipped(CompanyEmailDisabled)` y no se usa el fallback.
3. Si no tiene perfil y la instancia permite fallback (`AllowCompanyFallback`, por defecto `true` para no romper el
   piloto), se usa el perfil de instancia. En ese caso `From` es el remitente de la instancia (no se suplanta el
   dominio de la empresa), el nombre visible es el nombre comercial de la empresa y `Reply-To` es el `ReplyToEmail`
   de la empresa o su `CorporateEmail`.
4. En cualquier otro caso → `Configuration`.

**Settings duplicados:**

- `communications.sales_invoice_authorized.enabled` → REMOVE EVENTUALLY (Fase 5). Diferido en la implementación de la Fase 5 (sigue sin consumidor; su retiro es una migración de settings aparte). La única autoridad es
  `electronic_documents.email_on_authorization`.
- `communications.send_copy_to_company_email` → se **implementa** en la Fase 5 como `RecipientRole = CompanyCopy`. **Diferido** por ZH-EDOC-COMMUNICATIONS-01 (alcance explícito del ticket).
  Dueño: la empresa (configuración); consumidor: el handler genérico de ED.

---

## P. Monitor (capacidad futura, Fase 7)

- **Read model** sobre `communication_outbox` + `communication_delivery_attempts`, filtrado por empresa (filtro
  global). Endpoints bajo `/api/v1/communications/messages`.
- **Filtros:** estado (`Pending`/`Processing`/`Sent`/`Failed`/`Cancelled`/`Skipped`/`Expired`), propósito, canal,
  origen, rango de fechas, "bloqueado por configuración".
- **Columnas:** origen (`SourceModule`/`SourceDisplay`), propósito, canal, destinatario **enmascarado** en la lista
  (completo en el detalle), template/versión/fuente, intentos, última categoría de fallo, `ProviderMessageId`,
  tiempos.
- **Detalle:** cuerpo renderizado solo de mensajes no sensibles. Para los sensibles se muestra "contenido sensible
  no almacenado".
- **Acciones:** Requeue, Cancel, Resend y ver intentos.
- **Permisos separados** (nombres finales según la convención de `KernelRegistry`): ver monitor, requeue, cancelar,
  reenviar. Distintos de `communications.configure`.
- Los mensajes System no aparecen en el monitor ERP de una empresa (operación de instancia, fuera del alcance ERP
  Core).

### Privacidad y seguridad (transversal)

- Ningún secreto en logs (password SMTP, tokens, enlaces sensibles). El password SMTP nunca se devuelve (ya se
  cumple).
- Los emails en logs se enmascaran. Nunca se registran cuerpos.
- La auditoría registra metadatos, no cuerpos sensibles.
- La retención y purga de `communication_outbox` e intentos queda como política futura configurable en la
  instancia. El scrub sensible (§K) no espera a esa política.
- El acceso al monitor está protegido por permisos propios.

---

## Q. Frontera multicanal

- **KEEP** `CommunicationChannel` (Email/WhatsApp/Sms/Internal). Se implementa solo **Email**.
- `Channel` forma parte de la fila y de la identidad. Una misma intención de negocio puede producir en el futuro
  varias filas (una por canal), según una política por propósito, sin cambiar el modelo.
- **Hoy, encolar un canal distinto de Email falla de inmediato** (en lugar de crear filas que terminan `Failed`).
- No se crean `ISmsSender`, `IWhatsAppSender` ni abstracciones genéricas de canal hasta que exista un caso real.

---

## R. Matriz SSOT definitiva

| Concern | SSOT actual | SSOT objetivo | Dueño | Consumidores | Acción |
|---|---|---|---|---|---|
| Hecho de negocio | Domain event del módulo | Igual | Módulo origen | Handlers in-process | KEEP |
| Communication intent | `QueueEmailAsync` desde el handler | Fila `Pending` sin componer vía `ICommunicationQueue` (INSERT ON CONFLICT, misma transacción) | Communications | Handlers, adapters de Auth | EXTEND |
| Scope | Implícito (contexto ambiente) | `ScopeKind` + invariantes + `CommunicationScope` | Communications | Queue, processor, resolver | EXTEND |
| Propósito | `Purpose` + `CommunicationPurposes` | Igual; `Purpose` = TemplateKey, scope declarado | Communications | Todos | EXTEND |
| Canal | `CommunicationChannel` | Igual (solo Email implementado) | Communications | Processor | KEEP |
| Mensaje / outbox | `CommunicationOutbox` | Igual, evolucionada (§modelo) | Communications | Processor, monitor | EXTEND |
| Idempotencia | Índice único + clave por handler | Índice único + `CommunicationIdentity` | Communications (Domain) | Queue | EXTEND |
| Claim | — (`MarkProcessing` en memoria) | `FOR UPDATE SKIP LOCKED … RETURNING` + fencing por `ClaimToken` | Communications.Infrastructure | Processor | NEW (en la entidad existente) |
| Lease | — | `LeaseUntilUtc` + recuperación en el claim | Communications.Infrastructure | Processor | NEW |
| Retry | `MarkFailed` (3 fijos, 2^n) | `CommunicationRetryPolicy` por categoría | Communications (Domain) | Processor | EXTEND |
| Intentos | Solo `LastError` | `CommunicationDeliveryAttempt` | Communications | Monitor, auditoría | NEW |
| Destinatarios | Handler de Factura | `ICommunicationSourceContributor` (módulo origen) + `CompanyCopy` (Communications) | Módulo origen (datos) / Communications (composición) | Processor | EXTEND |
| Templates | Concatenación en el handler | Catálogo embebido versionado + override `CommunicationTemplate` | Communications | Renderer | MOVE |
| Renderer | — | Renderer único de placeholders | Communications | Processor | NEW |
| Payload sensible | Log (token en claro) | `SensitivePayloadProtected` + scrub | Communications (entrega) / Auth (token) | Processor | NEW |
| Adjuntos | `FileStoragePath` local | Referencia lógica + `ICommunicationAttachmentResolver` del dueño | Módulo dueño (ED) | Processor | EXTEND |
| Transporte | `IEmailSender` / `SmtpEmailSender` | Igual, con contrato de resultado y error tipado | Communications.Infrastructure | Processor, SendTestEmail | KEEP / EXTEND |
| Configuración del proveedor | `CommunicationSettingsResolver` (fallback por campo) | `CommunicationSettingsResolver(scope)`, perfil completo | Communications | Processor, SendTestEmail | EXTEND |
| Configuración de empresa | OrgSettings `communications.email.*` | Igual | Empresa | Resolver | KEEP |
| Configuración System | Env vars (solo fallback) | `Communications:Email:*` = perfil de instancia oficial | Instalación | Resolver | EXTEND (formalizar) |
| Estado | `CommunicationStatus` | + `Skipped`, `Expired` | Communications | Monitor | EXTEND |
| Auditoría | Columnas de la outbox | Outbox (estado) + intentos + `IAuditEvent` para acciones manuales | Communications | Monitor | EXTEND |
| Monitor | — | Read model + acciones con permisos | Communications | Frontend | NEW (Fase 7) |
| Entrega del password reset | `LoggingPasswordResetLinkSender` | `CommunicationsPasswordResetLinkSender` (scope System) | Auth (token) / Communications (entrega) | `ForgotPasswordHandler` | REPLACE impl |
| Entrega de ED autorizado | `SalesInvoiceAuthorizedCommunicationHandler` | `ElectronicDocumentAuthorizedCommunicationHandler` + contributors + reconciliación | Communications | — | EXTEND (reemplazo) |

---

## S. KEEP / EXTEND / MOVE LATER / REMOVE EVENTUALLY / DO NOT TOUCH

| Componente | Clasificación | Nota |
|---|---|---|
| `CommunicationOutbox` | **EXTEND** | Scope, origen, claim/lease, template, payload sensible, intentos, vencimiento (§modelo) |
| `CommunicationOutboxAttachment` | **EXTEND** | Referencia lógica; `FileStoragePath` y `BinaryContent` → REMOVE EVENTUALLY |
| `CommunicationQueue` / `ICommunicationQueue` | **EXTEND** | Scope explícito, identidad central, INSERT ON CONFLICT; se elimina `SaveImmediately` |
| `CommunicationOutboxProcessor` | **EXTEND** | Claim/lease/fencing, composición, política de reintento, intentos |
| `ProcessCommunicationsJob` | **EXTEND** | `[DisableConcurrentExecution]` como defensa secundaria; presupuesto por ejecución |
| `IEmailSender` | **KEEP / EXTEND** | Resultado + error tipado |
| `SmtpEmailSender` | **KEEP / EXTEND** | Timeout, clasificación, `Message-ID`; MailKit solo si hace falta |
| `CommunicationSettingsResolver` | **EXTEND** | Scope explícito, perfil completo, política de fallback |
| `CommunicationTemplate` (+ repositorio) | **KEEP → EXTEND** | Pasa a ser el override por empresa (`BaseTemplateVersion`) |
| `SalesInvoiceAuthorizedCommunicationHandler` | **REMOVED (2026-10-03)** | Reemplazado en Fase 5 por el handler genérico + contributor Sales, con el mismo correo (golden) |
| `IPasswordResetLinkSender` | **KEEP** | Frontera de Auth; firma ampliada en Fase 6 |
| `LoggingPasswordResetLinkSender` | **REMOVE EVENTUALLY** | Fase 1 quita el enlace del log; Fase 6 lo elimina |
| `QueueEmailCommand` (+ validator, handler) | **REMOVE EVENTUALLY** | Sin llamadores; las reglas útiles del validador pasan al dominio/queue |
| `SendTestEmailCommand` | **KEEP** | Excepción legítima documentada |
| Paquete RazorLight | **REMOVE EVENTUALLY** | Sin uso (Fase 4) |
| `communications.sales_invoice_authorized.enabled` | **REMOVE EVENTUALLY** | Duplicado de `email_on_authorization` |
| `communications.send_copy_to_company_email` | **EXTEND** | Se implementa como `CompanyCopy` (Fase 5) |
| `CommunicationChannel` | **KEEP** | Solo Email implementado |
| `OutboxMessage` / `OutboxProcessor` | **DO NOT TOUCH** | Concern distinto |
| `PurchaseCommunication` | **DO NOT TOUCH** | Purchases CLOSED; no es una comunicación entregable |

Nada se clasifica como REWRITE.

---

## U. Fases de implementación

Cada fase es un ticket, deja la suite verde, se revierte con su commit y no cambia el comportamiento visible salvo
lo indicado.

| Fase | Ticket sugerido | Contenido | Esquema | Depende de |
|---|---|---|---|---|
| 0 | ZH-COMMUNICATIONS-ARCHITECTURE-02 | ADR-039 + este documento | No | — |
| 1 | ZH-AUTH-PASSWORD-RESET-SECURITY-HOTFIX-01 | Quitar el enlace/token del log, respuesta neutral, rate limit por IP + por cuenta | No | — |
| 2 | ZH-COMMUNICATIONS-DELIVERY-HARDENING-01 | Claim atómico + lease + fencing, recuperación de `Processing`, `[DisableConcurrentExecution]` secundario, `MaxRetries` real (snapshot), clasificación de errores, timeout SMTP, `Message-ID` determinístico | Sí (`claim_token`, `lease_until_utc`) | — |
| 3 | ZH-COMMUNICATIONS-CONTRACT-01 | `ScopeKind` + invariantes + CHECK, `CommunicationIdentity`, origen (`SourceModule`/`SourceType`/`SourceId`/`SourceDisplay`/`TraceId`), `RecipientRole`, `ExpiresAtUtc`, `Skipped`/`Expired`, `CommunicationDeliveryAttempt`, `LastErrorSafeText`, resolver con scope explícito y perfil completo, INSERT ON CONFLICT, backfill de `RecipientRole = Primary` | Sí | 2 |
| 4 | ZH-COMMUNICATIONS-TEMPLATES-01 | Catálogo embebido + renderer + override por empresa (sin UI); migrar Factura **byte a byte**; quitar RazorLight | Sí (`base_template_version`, columnas de template en la outbox) | 3 |
| 5 | ZH-EDOC-COMMUNICATIONS-01 (= ADR-038 fase 6) | **IMPLEMENTED 2026-10-03.** Handler genérico, contributors Sales (Factura, NC) y Retentions, composición en el processor (RIDE fuera de la transacción SRI), referencias de adjuntos + resolver de ED, reconciliación con horizonte, `CompanyCopy`, retirar `SalesInvoiceAuthorizedCommunicationHandler` y el setting duplicado. Implementado con composición al encolar (fase 4); `CompanyCopy` y retiro del setting duplicado diferidos | Sí (columnas de referencia en adjuntos) | 3, 4 |
| 6 | ZH-AUTH-PASSWORD-RESET-DELIVERY-01 | `CommunicationsPasswordResetLinkSender`, scope System, payload sensible + scrub, template `PASSWORD_RESET`, eliminar `LoggingPasswordResetLinkSender` | Sí (columnas sensibles) | 1, 3, 4 |
| 7 | ZH-COMMUNICATIONS-MONITOR-01 | Read model, intentos, requeue/cancel/resend, permisos, pantalla | Posible (índices) | 3 |

No hay fase multicanal. Las fases 1 y 2 son independientes y pueden ir en paralelo.

---

## V. Estrategia de pruebas

| Categoría | Casos obligatorios |
|---|---|
| **Arquitectura** (`ERP.Architecture.Tests`, ratchet) | `System.Net.Mail`/`SmtpClient` solo en `SmtpEmailSender`; `IEmailSender` solo lo usan el processor y `SendTestEmailCommandHandler`; ningún módulo fuera de Communications referencia `CommunicationOutbox`; ningún código fuera de `CommunicationIdentity` arma claves de idempotencia; ElectronicDocuments no referencia Communications; Auth no referencia `IEmailSender`/SMTP |
| **Invariantes de dominio** | Company sin tenant/empresa → excepción; System con CompanyId/BranchId → excepción; propósito con scope incorrecto → excepción; transiciones de estado válidas e inválidas; cancel de `Processing` rechazado; requeue de sensible rechazado |
| **Queue/idempotencia** | Mismo hecho dos veces → una fila; handler + reconciliación → una fila; **cambio de email del cliente → no duplica la comunicación original**; resend crea una fila nueva con `ResendSequence`; carrera concurrente de encolado no rompe la transacción del negocio |
| **Concurrencia/PostgreSQL** (integración real) | **Dos workers simultáneos → una sola entrega** (sender de prueba que cuenta llamadas); claim con `SKIP LOCKED` reparte filas distintas; finalización con `ClaimToken` viejo afecta 0 filas |
| **Retry/recovery** | **Worker muere tras el claim → el lease vence → se recupera y entrega**; Transient reprograma con backoff; Permanent → `Failed` inmediato; Configuration no consume presupuesto y vence en `ExpiresAtUtc`; `MaxAttempts` copiado de la configuración al encolar; fila vencida → `Expired` sin envío |
| **Templates** | Salida de Factura idéntica byte a byte a la actual; escape HTML; Url inválida rechazada; CR/LF eliminados del asunto; variable faltante y placeholder desconocido → error sin valores; override inválido → `DefaultFallback`; todos los templates del catálogo cumplen su contrato (test de catálogo) |
| **Payload sensible/seguridad** | **El token nunca aparece en log, `BodyHtml`/`BodyText`, `LastErrorSafeText`, intentos ni auditoría** (logger de captura + lectura de filas); scrub en cada estado terminal y por vencimiento; respuesta de `forgot-password` idéntica para todas las ramas; rate limit por IP y por cuenta |
| **Adapter SMTP** | Mapeo de códigos SMTP → categorías; timeout → Transient; `Message-ID` estable entre intentos; Reply-To/From según la política de fallback |
| **Integración** | Factura autorizada → intención en la misma transacción → entrega con XML + RIDE; NC y Retención por sus contributors; sin email → `Skipped(NoRecipient)`; falla del handler → la reconciliación la recupera |
| **Multi-tenant** | Monitor de empresa A no ve filas de B ni filas System; el processor fija el contexto correcto por fila; la configuración se resuelve con el scope de la fila, nunca con el ambiente |
| **Scope System** | Password reset usa el perfil de instancia aunque la empresa del usuario tenga otro SMTP; usuario con varias empresas → un solo mensaje; no requiere empresa |
| **Scope Company** | Perfil completo de empresa; `enabled=false` → `Skipped`; sin perfil + fallback permitido → instancia con From de instancia y nombre visible de la empresa; sin fallback → Configuration |
| **Autorización del monitor** | Cada acción exige su permiso; sin permiso → 403; los destinatarios se enmascaran en la lista |

---

## Documentación desactualizada detectada (no corregida aquí)

| Documento | Problema | Acción sugerida |
|---|---|---|
| `docs/deployment/README.md` § SMTP | Afirma que no existe endpoint/pantalla para `communications.email.*` (existe desde COMMUNICATIONS-SETTINGS-UI-01); describe el fallback campo por campo (cambia en Fase 3); solo menciona Factura | Actualizar en la Fase 3 |
| `docs/sri/SRI-ELECTRONIC-COMPLIANCE-ARCHITECTURE.md` §O, §S, §T y ADR-038 D14 | Coherentes, pero el contributor allí solo aporta destinatario; ADR-039 lo amplía (variables, `SourceDisplay`) y asigna los adjuntos de ED al resolver de ElectronicDocuments | Referenciar ADR-039 en la próxima edición de ese documento |
| `STATUS.md` (entradas históricas ERP-CORE-CLOSEOUT) | "Índice único impide duplicados reales": cierto para el encolado, no para la entrega (G1) | Histórico, no se edita |
| `docs/architecture/ARCHITECTURE-BACKLOG.md` | Sin iniciativa para ADR-039 | Agregar `GOV-xxx` al empezar la Fase 2 |
| `FEATURES.md` | No menciona Communications ni el correo de documentos autorizados | Actualizar al cerrar la Fase 5 |
| Comentario de `IPasswordResetLinkSender` | Dice "implementación real = SMTP" (no existe) | Corregir en la Fase 1 |

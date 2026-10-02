# ADR-039 — Arquitectura de Communications

**Status:** Accepted · **Fecha:** 2026-10-02 · **Tickets:** ZH-COMMUNICATIONS-ARCHITECTURE-01 (auditoría),
ZH-COMMUNICATIONS-ARCHITECTURE-02 (cierre)
**Evidencia y diseño detallado:** [`docs/communications/COMMUNICATIONS-ARCHITECTURE.md`](../communications/COMMUNICATIONS-ARCHITECTURE.md)
**Relacionado:** ADR-038 D14 (este ADR la **refina**, no la reemplaza), ADR-022 (auditoría), ADR-034 (contrato
temporal), ADR-005 (filtros multi-tenant), ADR-ERP-002 (frontera ERP ↔ Platform).

## Contexto

ZH ERP ya tiene un módulo **Communications** transversal, pero parcial:

- outbox genérica `CommunicationOutbox` (canal, propósito, correlación, idempotencia por índice único, adjuntos);
- cola `ICommunicationQueue`;
- un único transporte `IEmailSender` → `SmtpEmailSender`;
- job `process-communications`;
- configuración SMTP por empresa con pantalla y fallback de instancia.

Hoy su único consumidor es `SalesInvoiceAuthorizedCommunicationHandler`.

La auditoría encontró:

1. **Doble entrega posible:** no hay claim atómico, token de concurrencia ni `[DisableConcurrentExecution]`.
2. **Filas `Processing` sin recuperación.**
3. **Templates inexistentes:** el HTML está en el handler; `CommunicationTemplate` y RazorLight no tienen uso.
4. **Password reset fuera de Communications:** usa `IPasswordResetLinkSender` → `LoggingPasswordResetLinkSender`,
   que no envía correo y escribe el enlace con el token en el log. Además hay enumeración de usuarios y no hay rate
   limit.
5. **Scope implícito:** tenant y empresa salen del contexto ambiente, y no existe un scope de instancia para
   mensajes de seguridad.
6. **`MaxRetries` de configuración nunca aplicado,** sin clasificación de errores ni requeue.
7. **NC y Retención sin correo,** con el riesgo de copiar el handler de Factura.
8. **RIDE generado dentro de la transacción de autorización SRI;** adjuntos por ruta local del nodo.
9. **Una falla del handler pierde la intención en silencio.**

Sin una decisión escrita, cada nueva necesidad (NC, retención, password reset, invitaciones) tiende a crear su
propio servicio de correo o su propia cola. Este ADR fija una única forma de evolucionar lo existente.

## Decisión

- **D1 — Communications es una capacidad transversal del ERP.** Sirve a documentos electrónicos, ventas, compras,
  CxC/CxP, reportes, formularios, usuarios, seguridad y futuras comunicaciones. Se evoluciona el módulo existente.
  No hay "Mail Core", "Email Service v2" ni un módulo paralelo.
- **D2 — Email es un canal, no el módulo dueño.** El módulo se llama Communications; `CommunicationChannel` se
  conserva.
- **D3 — System y Company son scopes distintos (`CommunicationScopeKind`).**
  - *Company:* Tenant y Company obligatorios; usa el perfil de la empresa.
  - *System:* Company no aplica; el tenant es opcional y solo informativo; usa siempre el perfil de **instancia**.
  - Cada propósito declara su scope y una combinación inválida falla al encolar.
  - Un mensaje System **nunca** se resuelve con "la primera empresa del usuario".
  - Persistencia: como la entidad cumple `ICompanyOperationalEntity`, "no aplica" se guarda como `Guid.Empty`, solo
    válido con `System` y garantizado por CHECK constraint. Ningún contexto de empresa ve un mensaje System, por
    construcción del filtro fail-closed existente.
- **D4 — `CommunicationOutbox` es el SSOT durable de la entrega.** Un registro = una entrega por un canal a un rol
  de destinatario. Se evoluciona esa entidad; no se crea otra outbox ni otra tabla de mensajes.
- **D5 — La outbox de domain events y la de Communications son concerns distintos.**
  - `OutboxMessage`/`OutboxProcessor` registran domain events.
  - `CommunicationOutbox` entrega mensajes a destinatarios.
  - No se fusionan ni se duplican. El puente es un handler in-process del domain event; Communications no lee
    `OutboxMessage`.
- **D6 — Ningún módulo llama SMTP ni `IEmailSender`.** Los módulos encolan intenciones en `ICommunicationQueue`.
  Única excepción: `SendTestEmailCommandHandler` (prueba síncrona de configuración).
- **D7 — La intención queda durable antes de la entrega.**
  - El handler del hecho inserta una intención barata (sin E/S remota, sin render, sin RIDE) **en la misma
    transacción** del hecho, con `INSERT … ON CONFLICT DO NOTHING`.
  - Una falla SMTP nunca revierte el negocio.
  - Si el handler falla, registra un Error y una reconciliación periódica sobre la tabla dueña del hecho encola lo
    faltante.
- **D8 — La idempotencia es central.**
  - Se mantiene el índice único actual. La identidad lógica la genera solo `CommunicationIdentity`:
    `scope + tenant + company + purpose + channel + sourceType + sourceId + recipientRole`.
  - El email del destinatario no forma parte de la identidad.
  - El reenvío manual es explícito (fila nueva con `ResendOfCommunicationId`), nunca una colisión accidental.
- **D9 — Claim y lease protegen la concurrencia en PostgreSQL.**
  - Claim: `UPDATE … FROM (SELECT … FOR UPDATE SKIP LOCKED LIMIT 1) … RETURNING`, con `ClaimToken` y
    `LeaseUntilUtc`.
  - Toda finalización exige el `ClaimToken` (fencing). Un lease vencido vuelve a ser reclamable: no existe
    `Processing` eterno.
  - Nunca se mantiene una transacción abierta durante el envío. `[DisableConcurrentExecution]` es solo defensa
    secundaria.
  - La entrega es al menos una vez, con duplicación acotada al lease y `Message-ID` determinístico.
- **D10 — El reintento es central y tipado por categoría.** `CommunicationRetryPolicy`:
  - Transient → backoff con presupuesto.
  - Permanent → `Failed` sin reintento.
  - Configuration → no consume presupuesto, visible, hasta vencer.
  - Unknown → como Transient.
  - `MaxRetries` tiene dueño (empresa para Company, instancia para System) y se copia al encolar.
  - Requeue y cancel son manuales y pertenecen a Communications.
- **D11 — El historial de intentos está separado del estado actual.** `CommunicationDeliveryAttempt` guarda cada
  intento (categoría, código y message-id del proveedor, texto de error sanitizado). Nunca guarda secretos, tokens,
  credenciales ni cuerpos. La outbox sigue siendo el SSOT del estado.
- **D12 — Templates por defecto versionados en el repositorio, con override por empresa.**
  - Los defaults son archivos embebidos con un contrato de variables (`TemplateKey` = `Purpose`, `Version`).
  - La entidad existente `CommunicationTemplate` es el override opcional por empresa, solo para propósitos Company
    que lo permitan, validado contra el contrato y con fallback al default si queda inválido.
  - No hay templates exclusivamente en BD.
- **D13 — Hay un solo renderer.** Placeholders `{{ nombre }}`, escape HTML por defecto, validación de URL, CR/LF
  eliminados del asunto, error si falta una variable obligatoria o hay un placeholder desconocido, resultado
  determinístico (los valores llegan ya formateados). RazorLight no se adopta y su paquete se elimina mientras no
  haya necesidad real de condicionales o loops.
- **D14 — Destinatarios y adjuntos varían mediante contributors, no copiando handlers.**
  - `ICommunicationSourceContributor`, implementado por el módulo origen y elegido por `SourceModule`, aporta
    destinatarios (desde snapshots), variables, `SourceDisplay` y referencias propias.
  - Un único `ElectronicDocumentAuthorizedCommunicationHandler` cubre Factura, NC y Retención.
  - ElectronicDocuments solo publica el hecho fiscal (ADR-038 D14).
- **D15 — Auth es dueño de los tokens.** Emite, guarda el hash, define expiración, single-use e invalidación, y
  valida. Communications solo entrega un valor opaco cifrado y nunca genera ni valida tokens ni conoce contraseñas.
  `IPasswordResetLinkSender` se conserva como frontera de Auth; su implementación pasa a ser un adapter sobre
  Communications.
- **D16 — El payload sensible nunca se persiste ni se registra en claro.**
  - Va cifrado con `ISecretProtector` en `SensitivePayloadProtected`.
  - Cuerpo renderizado solo en memoria (`BodyHtml`/`BodyText` = null).
  - No aparece en logs, `LastError`, intentos ni auditoría.
  - Se elimina (scrub) al llegar a un estado terminal o vencer.
  - Sin requeue ni resend.
- **D17 — El transporte es sustituible detrás de `IEmailSender`.** `SmtpEmailSender` sigue siendo el único camino
  técnico. El contrato devuelve un resultado (message-id) y un error tipado. MailKit, CC/BCC y TLS implícito en el
  puerto 465 solo cuando haya necesidad, sin afectar a Communications.
- **D18 — Los adjuntos usan referencias durables.** Referencia lógica (`OwnerModule`, `ReferenceKind`,
  `ReferenceId`) resuelta a stream por el módulo dueño (`ICommunicationAttachmentResolver`), sobre su
  almacenamiento. No se asume el filesystem local del nodo. No se duplica XML ni PDF en la outbox. El RIDE no se
  genera dentro de la transacción de autorización ni en el sender.
- **D19 — La configuración System y Company es explícita.**
  - Instancia: `Communications:Email:*`. Empresa: OrgSettings `communications.email.*`. Mensaje: la propia fila.
  - Única puerta: `CommunicationSettingsResolver.ResolveEmailAsync(scope)`.
  - Se usa un perfil completo, nunca una mezcla campo por campo.
  - El fallback Company → instancia solo procede si la instancia lo permite y la empresa no lo deshabilitó, con el
    remitente de la instancia y el nombre visible y Reply-To de la empresa.
  - `communications.sales_invoice_authorized.enabled` se elimina (la autoridad es
    `electronic_documents.email_on_authorization`); `send_copy_to_company_email` se implementa como rol
    `CompanyCopy`.
- **D20 — El monitor, requeue, cancel y resend pertenecen a Communications.**
  - Read model sobre la outbox y los intentos, filtrado por empresa.
  - Destinatario enmascarado en la lista; sin cuerpo para mensajes sensibles.
  - Permisos propios por acción.
  - Los mensajes System no se muestran en el monitor de empresa.
- **D21 — Los canales futuros no se implementan hasta que haya una necesidad real.** Solo Email. Encolar otro canal
  falla de inmediato. No se crean interfaces genéricas de canal todavía; `Channel` en la fila y en la identidad
  basta para agregarlos después.
- **D22 — La evolución es incremental, nunca big-bang.** Fases independientes (documento §U), cada una con su
  suite verde, reversible con su commit y sin cambio visible salvo lo declarado. La migración de Factura a
  templates y al handler genérico conserva el correo byte a byte.

## Consecuencias

- Ningún ticket futuro vuelve a decidir dónde se envía email, quién maneja SMTP, quién reintenta, cómo se evita la
  duplicación, dónde viven los templates, quién resuelve destinatarios, cómo se adjuntan documentos, cómo se
  entregan tokens, cómo se auditan intentos, cómo se recuperan mensajes atascados ni qué configuración usa System o
  Company. Los tickets implementan fases sobre esta arquitectura.
- `CommunicationOutbox` gana columnas (scope, origen, claim/lease, template, payload sensible, vencimiento, error
  sanitizado, message-id) y una tabla hija de intentos. `CommunicationOutboxAttachment` pasa a referencia lógica.
  Todo es aditivo, con backfill de las filas existentes como `Company`/`Primary`.
- `SalesInvoiceAuthorizedCommunicationHandler`, `LoggingPasswordResetLinkSender`, `QueueEmailCommand`, el paquete
  RazorLight y el setting duplicado se retiran en sus fases.
- **Riesgo actual hasta la Fase 1:** el enlace de reset con el token queda en los logs y `forgot-password` permite
  enumeración. Es la primera corrección. Hasta la Fase 6, el reset por email no funciona en producción (limitación
  conocida).
- **Riesgo actual hasta la Fase 2:** una ejecución superpuesta del job o varios nodos pueden enviar dos veces la
  misma factura.
- Riesgo residual aceptado: la entrega de email es al menos una vez. Un crash entre la aceptación SMTP y la
  finalización puede reenviar una vez tras el lease (mitigado con `Message-ID`).
- Se agregan tests de arquitectura (ratchet): SMTP solo en `SmtpEmailSender`; `IEmailSender` solo en el processor y
  `SendTestEmail`; la outbox solo en Communications; las claves de idempotencia solo en `CommunicationIdentity`;
  Auth no conoce SMTP.

## Implementación

Por fases (detalle en el documento, §U):

1. Hotfix de seguridad del password reset (Auth).
2. Hardening de la entrega (claim, lease, reintento, timeout).
3. Contrato de comunicación (scope, identidad, origen, intentos, configuración).
4. Templates y renderer (Factura byte a byte).
5. ED genérico + contributors Sales/Retentions (= ADR-038 fase 6).
6. Entrega de password reset sobre Communications (scope System, payload sensible).
7. Monitor.

No hay fase multicanal. Estado: **diseño aceptado; ninguna fase implementada.**

## Alternativas consideradas

- **Crear un "Mail Core"/"Email Service v2" o una segunda cola.** Descartado: duplicaría el SSOT de entrega que ya
  existe y funciona.
- **Fusionar `CommunicationOutbox` con `OutboxMessage`.** Descartado: son concerns distintos (domain events vs.
  entrega a personas), con ciclos de vida, reintentos y privacidad diferentes.
- **Hacer `TenantId`/`CompanyId` nullables para System.** Descartado: rompe `ITenantScopedEntity`/
  `ICompanyOperationalEntity` y obliga a un filtro especial en la infraestructura multi-tenant compartida. El
  centinela con `ScopeKind` y CHECK conserva el aislamiento fail-closed por construcción.
- **Resolver el SMTP del password reset con la primera empresa del usuario.** Descartado: es ambiguo con varias
  membresías, falla si la empresa no tiene SMTP, y el remitente de seguridad es la instalación.
- **Templates solo en BD (CMS).** Descartado: sin revisión en PR ni pruebas, y obligaría a seeds por empresa. El
  default vive en el repositorio y la BD solo guarda overrides.
- **Adoptar RazorLight ya.** Descartado: no hay necesidad de lógica en templates y aumenta la superficie (ejecución
  de código en templates editables).
- **Renderizar y adjuntar dentro de la transacción del hecho (como hoy).** Descartado: mete E/S y generación de PDF
  en la transacción fiscal. Se encola una intención barata y se compone en el job.
- **Solo `[DisableConcurrentExecution]` para evitar duplicados.** Descartado como garantía: no cubre varios nodos ni
  procesos que expiran. Se mantiene como defensa secundaria.
- **Clave de idempotencia con el email del destinatario (como hoy).** Descartado: un cambio de email duplicaría la
  comunicación del mismo hecho.
- **Copiar el handler de Factura para NC y Retención.** Descartado (ya lo descartaba ADR-038 D14): un handler
  genérico + contributors.

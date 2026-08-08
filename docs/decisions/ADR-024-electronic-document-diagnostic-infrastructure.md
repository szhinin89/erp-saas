# ADR-024: Infraestructura de Diagnóstico SRI reutilizable (extensión controlada de ADR-023)

## Status

**Accepted.** 2026-07-11. Extiende de forma aditiva y controlada el núcleo FROZEN de `ElectronicDocuments` (ADR-023) bajo la causa 1 de "Cambios permitidos" (cambio obligatorio: pérdida silenciosa de un campo real de la Ficha Técnica) y agrega infraestructura nueva de solo-lectura (DTOs, componentes React) sin tocar la máquina de estados, el pipeline ni los contratos de `IElectronicDocumentIssuer`.

## Contexto

El SRI responde a recepción y autorización con mensajes estructurados reales — `identificador`/`mensaje`/`informacionAdicional`/`tipo` por cada `<mensaje>` (Ficha Técnica, ejemplo "Comprobante No Autorizado", §7.2.3). Antes de este cambio, `SriSoapClient.ParseRecepcionResponse`/`ParseAutorizacionResponse` ya parseaban los tres primeros campos pero:

1. Los concatenaba de inmediato en un string (`"[identificador] mensaje: informacionAdicional"`) y unía varios mensajes con `string.Join(" ", ...)`.
2. **Nunca leía `<tipo>`** (ERROR/ADVERTENCIA/INFORMATIVO) — un campo real de la Ficha Técnica, descartado silenciosamente.
3. Lo único que sobrevivía en el dominio era `ElectronicDocument.LastError`, una única columna de texto — si una respuesta traía varios `<mensaje>`, todos colapsaban en un string sin distinguir código, tipo ni mensaje individual.

Esto se detectó al investigar dos síntomas reales en la misma sesión:

- El panel del Monitor no podía mostrar "Código 39 — FIRMA INVÁLIDA" por separado del resto del mensaje SRI — solo un string opaco.
- `RetryElectronicDocumentCommandHandler` devolvía `ElectronicDocumentDto` (proyección mínima del Monitor) en vez del detalle completo, rompiendo el contrato que el frontend esperaba tras un reintento manual — causa raíz de un `TypeError: Cannot read properties of undefined (reading 'hasError')` reproducido y documentado en esta misma sesión.

Adicionalmente, `SalesInvoiceDto` (frontend) duplicaba su propio subconjunto de campos electrónicos (`accessKey`, `authorizationNumber`, `authorizationDate`, `electronicStatus`) en vez de consumir el módulo `electronicDocuments` — no existía ninguna infraestructura pensada para reutilizarse fuera del Monitor.

## Decisión

### 1. Captura estructurada de mensajes SRI (aditiva, sin romper el contrato existente)

Nuevo value object de Domain `SriMessage(Code, MessageType, Message, AdditionalInfo)` (`ERP.Domain/Modules/ElectronicDocuments/ValueObjects/SriMessage.cs`) — vive en Domain, no en Application, porque lo transportan directamente el aggregate root y sus domain events (Domain nunca depende de Application).

`SriSoapClient.ParseRecepcionResponse`/`ParseAutorizacionResponse` ahora también leen `<tipo>` y construyen `List<SriMessage> StructuredMessages` **en paralelo** a los `Errors`/`Messages` de texto existentes, que se mantienen intactos — ningún consumidor actual (incluida la detección del código `[70]` por prefijo literal en `ElectronicDocumentIssuer.TrySendToReceptionAsync`) cambia de comportamiento. `MessageType` se guarda **verbatim** tal como llega en `<tipo>`, sin normalizar a un enum cerrado — soporta tipos nuevos del SRI sin cambios de código. Si `<tipo>` viene ausente, único fallback documentado: `"ERROR"`.

**Bug real corregido en el camino** (encontrado al escribir el primer test de `StructuredMessages`): el esquema del SRI reutiliza el nombre de etiqueta `mensaje` tanto para el contenedor de un mensaje individual como para uno de sus campos internos. `GetElementsByTagName("mensaje")` sobre todo el documento capturaba ambos, produciendo un segundo "mensaje" fantasma vacío por cada mensaje real — enmascarado hasta ahora porque el test existente usaba `ContainSingle(predicate)` (verifica que *algún* elemento cumpla el predicado, no que la colección tenga exactamente uno) sobre la lista de texto aplanado. Corregido restringiendo la selección a hijos directos de `<mensajes>` (`SelectMensajeNodes`, XPath `.//*[local-name()='mensajes']/*[local-name()='mensaje']`).

`SriReceptionResult`/`SriAutorizacionResult` (Infrastructure) y `SriReceptionResult`/`SriAuthorizationResult` (Application, vía `ISriReceptionClient`/`ISriAuthorizationClient`) ganan el campo `StructuredMessages` — mismo patrón field-copy que ya usan los adaptadores `SriReceptionClient`/`SriAuthorizationClient` para el resto de campos.

### 2. Un único punto de la máquina de estados se toca: `MarkRejected`

`ElectronicDocument.MarkRejected(string reason, Guid updatedBy, IReadOnlyList<SriMessage>? sriMessages = null)` — parámetro opcional al final, compatible hacia atrás con todos los llamadores existentes. `MarkFailed`/`MarkDeadLetter` **no se tocan**: esos casos (SOAP Fault, timeout, DNS, TLS, certificado, XML inválido, excepciones, Hangfire) nunca tuvieron una respuesta SRI estructurada real — siguen representados por su `LastError`/`Reason` de texto libre existente, sin cambio de contrato.

`ElectronicDocumentRejectedEvent` gana `IReadOnlyList<SriMessage>? SriMessages = null` al final del record — mismo patrón de compatibilidad.

### 3. Segundo handler de auditoría del mismo evento (extensión explícita de la restricción de ADR-023)

ADR-023 declara: *"todos [los eventos de dominio] consumidos únicamente por `ElectronicDocumentAuditHandler` ... ningún otro handler debe suscribirse sin justificación arquitectónica"*. Esta ADR provee esa justificación: nueva entidad de auditoría `ElectronicDocumentSriMessage : AuditRecordBase` (mismo patrón ya usado dos veces en el repo — `PricingRuleAudit`, `ElectronicDocumentAudit` — reutilizando `IAuditReader<T>`/`IAuditWriter<T>` genéricos **sin ninguna modificación**, confirmando que son open-generic) + nuevo `ElectronicDocumentSriMessageAuditHandler : INotificationHandler<ElectronicDocumentRejectedEvent>`, que coexiste con `ElectronicDocumentAuditHandler` (MediatR ya soporta múltiples handlers por notificación) y persiste cada mensaje SRI individualmente, sin resumir ni fusionar. Se descartó escribir estos mensajes directamente desde `ElectronicDocumentIssuer` (Application) a una tabla nueva — `AI-RULES/AUDIT-INFRASTRUCTURE.md` prohíbe explícitamente escribir auditoría desde un handler de negocio fuera de un `*AuditHandler` dedicado; toda auditoría debe originarse en un evento de dominio disparado por el propio aggregate root.

Nueva tabla `electronic_document_sri_message` (migración `AddElectronicDocumentSriMessage`) — mismas columnas comunes de `AuditRecordBase` + `company_id`, `code`, `message_type`, `message` (sin límite de longitud, mismo motivo BD-02 ya documentado en `ElectronicDocumentAuditConfiguration` — el texto del SRI puede superar 500 caracteres), `additional_info`.

### 4. DTOs de diagnóstico reutilizables, agnósticos de módulo

Nuevo archivo `ElectronicDocumentDiagnosticDtos.cs`: `ElectronicDocumentMessageDto`, `ElectronicDocumentTechnicalInfoDto`, `ElectronicDocumentDiagnosticDto` (reutiliza `ElectronicDocumentTimelineEventDto` ya existente, no lo duplica). No conocen Monitor ni Ventas.

`ElectronicDocumentDetailDto` (Monitor) reemplaza sus campos sueltos `Error`/`Timeline`/`XmlXxxAvailable` por un único `Diagnostic: ElectronicDocumentDiagnosticDto` — se retira `ElectronicDocumentErrorInfoDto` (queda subsumido por `Messages`, un solo concepto en vez de dos).

Nuevo `ElectronicDocumentDiagnosticAssembler` (internal static) — único ensamblador usado por los tres puntos que construyen el diagnóstico completo:
- `GetElectronicDocumentDetailQueryHandler` (Monitor, existente).
- `RetryElectronicDocumentCommandHandler` — **cambia de `Result<ElectronicDocumentDto>` a `Result<ElectronicDocumentDetailDto>`**, cerrando el bug de contrato roto que causaba el `TypeError` documentado en Contexto.
- `GetElectronicDocumentDiagnosticBySourceQueryHandler` (nuevo) → `GET /api/v1/electronic-documents/by-source?sourceModule&sourceEntityId` — el punto de entrada agnóstico de módulo, usa `IElectronicDocumentRepository.GetBySourceAsync` ya existente, sin cambios.

Si no existe ningún `ElectronicDocumentSriMessage` real para el documento (errores técnicos/internos sin respuesta SRI estructurada) y `LastError` no es null, el ensamblador sintetiza **un único** mensaje de respaldo (`Code=null, MessageType="ERROR", Message=LastError` tal cual) — nunca ambas fuentes a la vez, para no duplicar el mismo contenido ni inventar códigos SRI falsos.

### 5. Componentes React reutilizables — un único punto de entrada

`frontend/src/components/zh/electronicDocuments/` (mismo patrón ya usado por `ZHElectronicEnvironmentBanner`/`electronicInvoicingStatusRegistry.ts` para un componente consumido por varios módulos, no anidado dentro de `modules/electronicDocuments/monitor/`): `ElectronicDocumentStatusBadge`, `ElectronicDocumentSriMessages` (verbatim, sin resumir), `ElectronicDocumentTimeline`, `ElectronicDocumentTechnicalInfo` (colapsable, "solo para soporte"), `ElectronicDocumentXmlActions`, y el componente raíz `ElectronicDocumentDiagnosticPanel` — la única propiedad que cualquier pantalla del ERP necesita es `diagnostic`.

Integrado en dos consumidores reales:
- **Monitor** (`ElectronicDocumentDetailPanel.tsx`) — refactorizado para delegar en el panel compartido.
- **Ventas** (`SalesElectronicDiagnosticDrawer.tsx`, nuevo) — segundo consumidor real, obtiene el diagnóstico vía `getDiagnosticBySource('Sales', invoiceId)`. Los campos ya duplicados en `SalesInvoiceDto` (`accessKey`, `authorizationNumber`, etc.) no se tocan — retirarlos es un cambio de UX de Ventas más amplio, fuera de este alcance (ver Deuda técnica).

**Alcance deliberadamente excluido**: Retenciones/Notas de Crédito-Débito/Guías de Remisión no tienen implementación activa de emisión (solo XSD/catálogo, ver Límites de ADR-023) — construirles integración de diagnóstico ahora sería funcionalidad nueva sin roadmap, prohibida como "mantenimiento" por la propia ADR-023. Los componentes son genéricos (operan sobre `diagnostic`/`sourceModule`/`sourceEntityId`, sin saber qué módulo los usa) y quedan listos para esos módulos cuando tengan su propia fase de emisión.

## Deuda técnica conocida (no bloquea esta ADR)

- **SOAP Action / servicio utilizado / tiempo de respuesta** no están en `ElectronicDocumentTechnicalInfoDto` — los dos primeros son constantes de protocolo (`RecepcionComprobantesOffline`/`AutorizacionComprobantesOffline`) resolubles en el frontend sin dato de backend; el tercero requeriría instrumentar `SriSoapClient.PostSoapAsync`, no implementado — no se fabrica un valor para llenar el campo.
- `SalesInvoiceDto` sigue duplicando `accessKey`/`authorizationNumber`/`authorizationDate`/`electronicStatus` — el nuevo drawer es aditivo (icono junto al bloque existente), no reemplaza esa UI. Retirar la duplicación es un seguimiento explícito, no resuelto aquí.
- `ElectronicDocumentTechnicalInfoDto.CorrelationId` siempre viaja `null` — `AuditActor.CorrelationId` existe en la infraestructura de auditoría pero el ensamblador no lo está leyendo todavía desde `ElectronicDocumentAudit`; queda como extensión futura de una línea, no requiere cambio de contrato.

## Alternativas consideradas

| Alternativa | Razón de descarte |
|---|---|
| Columna `jsonb` con los mensajes crudos directamente en `ElectronicDocument` | Bloatea el aggregate FROZEN y rompe el invariante "una fila = un documento" — un ciclo de vida puede generar mensajes en varios intentos/rechazos distintos, no uno solo. |
| Escribir los mensajes directamente desde `ElectronicDocumentIssuer` (Application) a una tabla nueva | Viola `AI-RULES/AUDIT-INFRASTRUCTURE.md`: toda auditoría debe originarse en un evento de dominio disparado por el aggregate root, nunca desde un servicio de aplicación. |
| Enriquecer también `MarkFailed`/`MarkDeadLetter` con mensajes estructurados | Esos estados nunca tienen una respuesta SRI real de por medio (son errores técnicos/internos) — hacerlo habría requerido inventar códigos SRI inexistentes, exactamente lo que la ADR-023 prohíbe para el catálogo `sri_error_code`. |
| Mantener `ElectronicDocumentErrorInfoDto` junto al nuevo `Diagnostic` | Habría dejado dos conceptos para lo mismo (hasError/technicalDetail vs. Messages) — contradice "no duplicar contrato". |

## Consecuencias

**Positivas:** el Monitor y Ventas ahora comparten una única infraestructura de diagnóstico verificada (13 tests nuevos/actualizados en `SriSoapClientTests`, 2 en `ElectronicDocumentSriMessageAuditHandlerTests`, suite completa de `ElectronicDocuments` en verde); el bug de contrato roto del retry queda cerrado con el mismo cambio; un defecto real de parsing (mensaje fantasma) se corrigió con evidencia y test de regresión, no solo se documentó.

**Negativas / deuda aceptada:** ver sección anterior — ninguna bloquea el cierre de esta ADR, todas están acotadas y documentadas explícitamente en vez de ocultas.

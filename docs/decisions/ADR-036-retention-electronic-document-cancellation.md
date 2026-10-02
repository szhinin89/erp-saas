# ADR-036 — Documento electrónico de una retención cuyo origen se anula

**Status:** Accepted (política) — decisiones D-1…D-11 aprobadas el 2026-10-01 · **fases 1–2 y transmisión inmediata implementadas en ZH-RETENTION-ELECTRONIC-LIFECYCLE-01A (§23)** · anulación oficial SRI pendiente (ZH-RETENTION-SRI-ANNULMENT-01) · quedan decisiones abiertas (§22) · **Fecha:** 2026-10-01 · **Ticket:** ZH-RETENTION-ELECTRONIC-CANCELLATION-ADR-01
**Relacionado:** ADR-023 (ElectronicDocuments v1.0 CLOSED; este ADR **la extiende de forma controlada**), ADR-024 (diagnóstico SRI), ADR-025 (RIDE), ADR-034 (contrato temporal), `RETENTIONS-MODULE-DESIGN-01`, `RETENTIONS-SRI-AUTHORIZATION-WIRING-DESIGN-04B`, `docs/architecture/backend.md` § Retenciones (ZH-RETENTION-CANCELLATION-LIFECYCLE-01).

> §1–§22 fijan la política aprobada, los invariantes, el modelo de estados objetivo y la evidencia previa. §15 describe el comportamiento ANTERIOR a la implementación (sus pruebas ya fueron invertidas). Lo implementado y sus decisiones concretas están en §23.

## 1. Contexto

La retención (`RetentionDocument`) se emite dentro de la confirmación de su Compra o Gasto, y solo se anula al anular ese origen (`CancelPurchaseHandler` o `CancelExpenseDocumentHandler`, ambos vía `RetentionCanceller`). `Cancelled` es terminal. Su comprobante electrónico es un `ElectronicDocument` (ED) con `SourceModule="Retentions"` y `SourceEntityId=RetentionDocument.Id`. Se registra **a mano** (`POST /retentions/{id}/electronic/register`) y luego avanza por el pipeline genérico `ElectronicDocumentIssuer`: el reintento manual del Monitor y el job Hangfire.

## 2. Normativa incorporada

El responsable funcional aportó estas reglas el 2026-10-01. **El texto literal de las resoluciones no está en el repositorio:** antes de implementar hay que adjuntarlo o verificarlo (el mismo criterio de "no inventar sin fuente" que ya rige para NAC-DGERCGC26-00000027 en `frozen-infrastructure.md`).

| Resolución | Regla incorporada (tal como fue aprobada) |
|---|---|
| NAC-DGERCGC25-00000014 / NAC-DGERCGC25-00000017 | Los comprobantes de retención **se anulan exclusivamente en línea** (servicios en línea del SRI) |
| ídem | Se requiere **aceptación del receptor cuando corresponda** |
| ídem | **Plazo vigente:** hasta el **día 7 del mes siguiente** |
| ídem | **Sin aceptación, el comprobante sigue válido** |
| (obligación vigente) | **Transmisión inmediata** del comprobante electrónico (ver §14) |

Lo que este ADR **no** supone, porque no está documentado: qué casos exigen aceptación del receptor; desde qué fecha corre el plazo (emisión de la retención u otra); el canal o formato con que el SRI informa "ANULADO"; y si el WS de autorización (`autorizacionComprobante`) refleja la anulación. Todo eso queda en §22.

## 3. Problema (reproducido)

En PostgreSQL real (§15): **hoy un XML de retención sale hacia el SRI después de que la retención quedó `Cancelled`**, y el resultado puede terminar en ERP `Cancelled` / SRI `Authorized`, sin señal alguna. Además, la anulación de la Compra/Gasto revierte la CxP y los asientos aunque el comprobante esté autorizado, es decir, todavía vigente ante el SRI.

## 4. Estado actual — flujo electrónico completo

| # | Transición | Quién la inicia | Estado exigido → resultante | ¿Relee `RetentionDocument`? | ¿Automática? |
|---|---|---|---|---|---|
| 1 | Registro | `RegisterRetentionElectronicDocumentHandler` (`POST /retentions/{id}/electronic/register`); también `CreateElectronicDocumentCommand` (`POST /electronic-documents/register`, con módulo/tipo arbitrarios en el body) | sin ED → `Draft` (persistido antes del pipeline). Con ED en `Draft`/`Failed`, reanuda; en otro estado → `Conflict` | **Sí** (`Issued`) solo en el handler de Retentions. El genérico no lo relee, pero lo cubre el gate #2 | No (manual) |
| 2 | Proveedor de datos + XML | `RunPipelineAsync` → `RetentionElectronicDocumentXmlSupplier` → `RetentionElectronicDocumentDataProvider` → `RetentionXmlBuilder` | `Draft`/`Failed`; si falla → `Failed` (`RetryCount++`) | **Sí**: exige `Issued` (único gate real del pipeline) | Sí (vía job) |
| 3 | XSD | `RunPipelineAsync` | si falla → `Failed` | No | Sí |
| 4 | Firma | `IElectronicDocumentSigningService` | si falla → `Failed` | No | Sí |
| 5 | Almacenamiento + `MarkXmlGenerated` + `MarkSigned` | `RunPipelineAsync` | `Draft`/`Failed` → `XmlGenerated` → `Signed` en **un solo `SaveChanges`** (`XmlGenerated` nunca se persiste solo) | No | Sí |
| 6 | Envío a Recepción | `TrySendToReceptionAsync`, desde el pipeline (justo tras #5) o desde `RetryAsync` en `Signed` (releyendo `SignedXmlPath`) | `Signed` → `Sent` → `Received` / `Rejected`. `Sent` es transitorio: se persiste junto con el estado siguiente. Fallo de red → sigue en `Signed`. Códigos SRI 70/43/45 → `Received` | **No** | Sí |
| 7 | Consulta de autorización | `AuthorizeAsync` tras #6, o `RetryAsync` en `Received`. `SriSoapClient.CheckAuthorizationAsync`: 5 sondeos (2/4/8/16 s) | `Sent`/`Received` → `Authorized` / `Rejected`. `TIMEOUT`/fallo → sigue en `Received` | **No** | Sí |
| 8 | Agotamiento | `ApplyDeadLetterIfExhaustedAsync` | `Failed`/`Signed`/`Received` con `RetryCount ≥ 5` → `DeadLetter` (guarda `PreDeadLetterState`) | No | Sí |
| 9 | Reactivación | `RetryAsync` (Monitor, `electronic-documents.retry`) | `DeadLetter` → `PreDeadLetterState`, y a continuación #2–#7 **sin límite** | **No** | No (manual) |
| 10 | Job | `ElectronicDocumentRetryJob` (cada minuto, entre tenants, `DisableConcurrentExecution`, preferencia `electronic_documents.auto_retry_enabled`) | candidatos `Draft`/`Failed`/`Signed`/`Received`, con backoff 1/2/4/8/16 min → `RetryAsync` | No | Sí |
| 11 | `MarkCancelled` | **nadie** (§10) | `Authorized` → `Cancelled` | — | — |
| 12 | XML/RIDE de vista previa | `GET /retentions/{id}/electronic/xml` y `/ride/pdf` | se generan al vuelo; nada se persiste | **Sí** (exige `Issued`) | No |
| 13 | XML persistido (Monitor) | `GET /electronic-documents/xml` (`electronic-documents.detail`) | borrador, firmado o autorizado ya guardados | No | No |

Ningún handler de eventos ni outbox registra el ED de una retención, y la confirmación de Compra/Gasto **no** dispara el registro. Las anulaciones (`CancelPurchaseHandler`, `CancelExpenseDocumentHandler`, `RetentionCanceller`) **no leen ni bloquean** el ED. No hay lock ni versión compartidos entre `retention_documents` y `electronic_documents`.

Respuesta SRI ambigua: `SriSoapClient` no distingue "el SRI no conoce la clave" (sin nodo `<autorizacion>`, `SIN_RESPUESTA`) de "en proceso" (`PENDIENTE`/`EN_PROCESO`). Ambos casos terminan en `TIMEOUT`.

## 5. Decisiones aprobadas (2026-10-01)

| ID | Decisión |
|---|---|
| **D-1** | Una retención `Cancelled` **nunca** puede avanzar su ElectronicDocument |
| **D-2** | Si el ED **nunca tuvo un intento externo**, puede quedar `Discarded` al anular el origen |
| **D-3** | **No** usar `Sent` como estado previo a la llamada. Se crea un estado explícito, **`Dispatching`** (antes llamado *SubmissionPending*) |
| **D-4** | Un `Signed` existente se considera **ambiguo**: hay que consultar al SRI antes de cualquier reenvío |
| **D-5** | `Received`/en proceso: **se bloquea la anulación del origen** y solo se consulta |
| **D-6** | `Authorized`: **se bloquea la anulación local**. Requiere la **anulación oficial ante el SRI**, y ningún reverso local se ejecuta hasta confirmar **ANULADO** |
| **D-7** | **Pendiente de anular:** el comprobante sigue **vigente** y no se completan reversos locales |
| **D-8** | **ANULADO confirmado por el SRI:** recién entonces se completan la anulación y los reversos del ERP |
| **D-9** | Permisos por origen: Compra → `purchases.view`; Gasto → `expenses.documents.view`. **Sin permiso transversal** por ahora |
| **D-10** | Un resultado desconocido o `TIMEOUT` **nunca** autoriza el reenvío: el estado sigue incierto y se vuelve a consultar |
| **D-11** | Se incorpora la normativa de §2 |
| **D-12** | Auditar `Issued` frente a la obligación de transmisión inmediata, y dejar la decisión explícita (§14) |

## 6. Invariantes

- **I-1 (D-1).** Si `RetentionDocument.Status = Cancelled`, ningún proceso, manual o automático, puede registrar, generar o regenerar XML, firmar, despachar, enviar o reenviar, reactivar, ni aplicar una transición de avance (`Received`/`Authorized`/…) sobre su ED. Con la política aprobada (D-5…D-8) **ya no puede surgir** una retención `Cancelled` con el ED en vuelo o autorizado, porque la anulación se bloquea antes. Si ese caso existe, viene de datos previos a la implementación (§16) y se resuelve con un **procedimiento manual de conciliación auditado**, nunca con el pipeline automático (O-9).
- **I-2 (D-2).** `Discarded` solo es alcanzable con **certeza de que no hubo intento externo**: `Draft`, `Failed`, `DeadLetter(pre=Failed)`, o `Signed` creado **después** de introducir `Dispatching` y que nunca pasó por él. Un `Signed` existente hoy no es descartable (I-3).
- **I-3 (D-4, D-10).** Un `Signed` existente es incierto. Antes de cualquier reenvío se consulta la autorización por clave de acceso, y solo una respuesta **positivamente concluyente** de que el SRI no tiene el comprobante habilita el reenvío o el descarte. `TIMEOUT`, `SIN_RESPUESTA`, un error de transporte u otra respuesta no concluyente **nunca** autorizan el reenvío: el ED sigue incierto y se vuelve a consultar. Mientras no se resuelva P-2 (§22), un `Signed` existente **no puede reenviarse automáticamente**.
- **I-4 (D-6, D-7).** Mientras el ED esté `Dispatching`, `Received`, `Authorized` o `AnnulmentPending`, el comprobante se considera **vigente o potencialmente vigente**: el origen no se anula y la CxP, el asiento y el Kardex no se revierten.
- **I-5 (D-8).** Los reversos del ERP (retención, CxP, asientos, Kardex y el resto del origen) se ejecutan **exactamente una vez**, por el camino único de anulación del origen ya existente, y **solo** cuando el ED llega a `Cancelled` con la evidencia de ANULADO.

## 7. Modelo de estados objetivo del ElectronicDocument (extensión de ADR-023)

| Estado | Nuevo | Significado |
|---|---|---|
| `Draft`, `Failed` | — | Sin cambios. Pre-firma; nunca salieron |
| `Signed` | semántica | Firmado y **nunca despachado**: solo garantizado para documentos firmados después de introducir `Dispatching`. Los existentes son inciertos (I-3) |
| **`Dispatching`** | **sí** (D-3) | Reclamo de envío persistido **antes** de la llamada de red. Desde aquí el comprobante puede estar en el SRI: nunca se reenvía a ciegas, solo se consulta |
| `Sent` | — | Se mantiene como hoy (transitorio, junto con la respuesta de Recepción). No se usa como reclamo previo |
| `Received` | — | Sin cambios |
| `Authorized` | — | Sin cambios. Vigente |
| **`AnnulmentPending`** | **sí** (D-7) | Se solicitó la anulación oficial en línea; el comprobante **sigue vigente** hasta la confirmación |
| `Cancelled` | semántica (D-8) | **ANULADO confirmado por el SRI**, siempre con evidencia (§10) |
| **`Discarded`** | **sí** (D-2) | Terminal: el origen se anuló antes de cualquier intento externo; nunca saldrá |
| `Rejected`, `DeadLetter` | — | Sin cambios de semántica. `DeadLetter` con `PreDeadLetterState ∈ {Dispatching, Signed existente, Received}` se reactiva **solo para consultar** |

Transiciones nuevas o cambiadas:
- `Signed` → `Dispatching`: despacho.
- `Dispatching` → `Received`/`Rejected`: respuesta de Recepción.
- `Dispatching` → `Dispatching`: incierto; se consulta.
- `Draft`/`Failed`/`DeadLetter(pre=Failed)`/`Signed(nuevo, no despachado)` → `Discarded`.
- `Authorized` → `AnnulmentPending`.
- `AnnulmentPending` → `Cancelled` (ANULADO confirmado) o → `Authorized` (anulación rechazada, no aceptada o vencida; §9.5).

Los estados son valores nuevos del enum persistido como `int`, sin migración de columnas. Requieren el Monitor, los filtros, el dashboard y la auditoría.

## 8. Matriz Retention × ElectronicDocument — política aprobada

Columnas: **A** = ¿se puede anular el origen? · **B** = ¿el reintento continúa? · **C** = ¿regenerar XML? · **D** = ¿firmar? · **E** = ¿enviar o reenviar? · **F** = ¿reactivar? · **G** = estado local resultante.

### Retention = Issued

| ED | A | B | C/D | E | F | G |
|---|---|---|---|---|---|---|
| inexistente | **Sí** → la retención `Cancelled`, sin ED | — | — | — | — | (no se crea ED) |
| Draft / Failed | **Sí** | no tras anular | no tras anular | no | — | `Discarded` (D-2) |
| Signed (nuevo, no despachado) | **Sí** | no tras anular | no | no | — | `Discarded` |
| Signed (existente, incierto) | **Bloqueada** hasta resolver la consulta (I-3) | solo consulta | no | **no**, salvo respuesta concluyente (P-2) | — | según el SRI |
| Dispatching / Sent | **Bloqueada** (D-5) | solo consulta | no | **no** | — | según el SRI |
| Received | **Bloqueada** (D-5) | solo consulta | no | no | — | según el SRI |
| Authorized | **Bloqueada** (D-6) → se solicita la anulación oficial | no | no | no | — | `AnnulmentPending` |
| AnnulmentPending | **Bloqueada** (D-7) | consulta o registro de evidencia | no | no | — | `Cancelled` y entonces anulación del origen (D-8), o vuelve a `Authorized` |
| Rejected | **Sí** (nada vigente en el SRI) | no | no | no | — | `Rejected` (sin cambio) |
| DeadLetter (pre=Failed) | **Sí** | no | no | no | **no** | `Discarded` |
| DeadLetter (pre=Signed existente/Dispatching/Received) | **Bloqueada** | solo consulta | no | no | solo para consultar | según el SRI |
| Cancelled (ED, ANULADO) | se ejecuta la anulación del origen (D-8) | — | — | — | — | `Cancelled` |
| Discarded | ya anulada | no | no | no | no | `Discarded` |

### Retention = Cancelled

Con la política aprobada solo puede coexistir con ED inexistente, `Discarded`, `Rejected` o `Cancelled` (ANULADO). En todos ellos B…F = **no**. Cualquier otra combinación es un dato heredado (§16) y se resuelve con I-1, por conciliación manual (O-9).

### Hoy (antes de implementar) — divergencias reproducidas

Con la retención anulada, hoy pasa lo siguiente:
- `Signed` y `DeadLetter(pre=Signed)` reenvían el XML.
- `Received` consulta y puede quedar `Authorized`.
- `Authorized` no bloquea nada y se revierte todo localmente.
- `Sent` persistido queda varado.
- `Failed` es ruido hasta `DeadLetter`.

Evidencia en §15.

## 9. Casos

### 9.1 Nunca tuvo intento externo (D-2)

Corresponde a: ED inexistente, `Draft`, `Failed`, `DeadLetter(pre=Failed)` y, cuando exista `Dispatching`, `Signed` nunca despachado. La anulación del origen se permite y, en la **misma transacción**, el ED pasa a `Discarded`. Ni el job ni el reintento lo toman, y el Monitor lo muestra.

`MarkCancelled` **no** se usa aquí: su significado es ANULADO ante el SRI.

### 9.2 Signed existente (D-4, D-10)

Antes de cualquier reenvío, o de permitir la anulación del origen, se consulta la autorización por clave de acceso:
- `AUTORIZADO` → `Authorized` (pasa a §9.4).
- `NO AUTORIZADO` → `Rejected`.
- Respuesta **concluyente** de que el SRI no tiene el comprobante (P-2) → se permite el reenvío (retención `Issued`) o el `Discarded` (anulación).
- `TIMEOUT`, `SIN_RESPUESTA` o error → sigue incierto, se vuelve a consultar y **no se reenvía**.

Consecuencia aceptada: mientras no exista P-2, un `Signed` existente que realmente nunca llegó al SRI no se reenvía solo y requiere intervención (O-10).

### 9.3 Dispatching / Sent / Received (D-5)

La anulación del origen se rechaza con un mensaje explícito. El job **solo consulta**, y `Dispatching`/`Sent` pasan a ser candidatos de consulta (hoy un `Sent` persistido queda varado, caso D). El caso no se da por resuelto hasta tener una respuesta concluyente.

### 9.4 Authorized → anulación oficial (D-6)

La anulación del origen **no** se ejecuta: no se revierten la retención, la CxP, los asientos ni el Kardex. El usuario debe tramitar la anulación **en línea ante el SRI**: el ERP no tiene integración para eso y la normativa la define como exclusivamente en línea. El ERP registra la solicitud como `AnnulmentPending`.

Dato que debe guardarse: la fecha de solicitud, el usuario y el motivo del origen, porque la anulación del origen se ejecutará más tarde con ese motivo.

El plazo (día 7 del mes siguiente, §2) debe validarse al registrar la solicitud. La fecha de referencia está en O-11, y se calcula como fecha de negocio en `Company.Timezone` (ADR-034).

### 9.5 Pendiente de anular (D-7)

El comprobante **sigue vigente**. El origen permanece confirmado y sin reversos. Salidas posibles:
- **ANULADO confirmado** → §9.6.
- **Rechazada, no aceptada por el receptor o plazo vencido** → el ED vuelve a `Authorized` y el comprobante sigue válido (§2: "sin aceptación sigue válido"); el origen no se anula. La vía de corrección queda abierta (O-12).

Riesgo detectado: mientras esté pendiente, ¿se pueden aplicar pagos o créditos a la CxP del origen? Si se aplican, la anulación del origen quedaría luego bloqueada por la regla vigente ("CxP con pagos"), con el comprobante ya anulado en el SRI. Decisión pendiente O-13 (recomendación: bloquear la aplicación de pagos y créditos mientras la anulación esté pendiente).

### 9.6 ANULADO confirmado (D-8)

El ED pasa a `Cancelled` con la evidencia. Entonces se ejecuta **el mismo camino único** de anulación del origen (`CancelPurchaseHandler`/`CancelExpenseDocumentHandler` → `RetentionCanceller`), exactamente una vez (I-5). Los guards vigentes (pagos, devoluciones, crédito de proveedor) se reevalúan en ese momento.

Cómo se confirma ANULADO (O-14, sin suponer): por consulta al WS de autorización, si el SRI refleja la anulación (no hay evidencia en el repo), o por registro manual de la evidencia (fecha, referencia, adjunto). Cómo se dispara la anulación del origen desde el ED respetando la referencia débil de ADR-023 es la opción técnica §17-T1.

## 10. `MarkCancelled`

- **Hoy:** solo admite `Authorized` → `Cancelled` y exige un motivo. No guarda evidencia externa, no comunica con el SRI y **ningún código productivo la llama**. Solo se usa en `ElectronicDocumentEntityTests` y `ElectronicDocumentRideSourceXmlProviderTests` (RIDE `NotApplicable`, ADR-025). Su evento solo lo consume la auditoría. Clasificación: **C — intención futura no terminada**; si alguien la llamara hoy significaría **A (cancelado solo en el ERP)**.
- **Aprobado (D-8):** pasa a significar **B — ANULADO confirmado externamente**. Su origen cambia a `AnnulmentPending` (ya no `Authorized` directo) y exige evidencia (fecha de anulación SRI, referencia o modo de confirmación, usuario). Nunca es un efecto automático de anular el origen, y nunca se usa para lo no enviado (para eso existe `Discarded`).

## 11. Gate SSOT

- **Dónde:** un único gate en `ElectronicDocumentIssuer`, que es donde convergen registro, reintento manual, job y reactivación. No se toca `ElectronicDocumentRetryJob` ni `RetryElectronicDocumentCommandHandler`, y la regla no se repite por job.
- **Contrato:** `IElectronicDocumentSourceEmissionGate`, resuelto por `SourceModule` con el mismo patrón que `ISourceDocumentSummaryProviderResolver`. ElectronicDocuments define el contrato y Retentions lo implementa (emitible si la retención está `Issued`), de modo que ElectronicDocuments no referencia Retentions. Un módulo sin gate registrado se comporta como hoy (compatibilidad con Sales y CreditNote).
- **Puntos de evaluación:** antes del pipeline; **dentro de la transacción del reclamo `Dispatching`** (§12); antes de reactivar; y antes de aplicar cualquier transición de avance (I-1).
- **Reglas de reenvío** (D-4, D-10): las aplica el emisor según el estado del ED, sin importar el origen; ver la nota de alcance en O-15.

## 12. Concurrencia

| Carrera | Hoy | Diseño aprobado |
|---|---|---|
| Anulación vs. XML/firma | La anulación hace commit a mitad del pipeline; el pipeline continúa | El gate en el reclamo `Dispatching` corta antes de cualquier salida |
| Anulación vs. envío o reintento | **Reproducido** (casos A y B): el XML sale tras `Cancelled` | Transacción corta: `SELECT … FOR UPDATE` sobre `retention_documents`, gate `Issued`, `Signed` → `Dispatching` y commit; **después** `SendAsync` (nunca con el lock tomado durante la llamada SOAP). La anulación del origen toma el mismo lock y lee el ED: `Dispatching`/`Received`/`Authorized` → bloqueo (D-5/D-6); `Draft`/`Failed`/`Signed` nuevo → `Discarded` en su misma transacción. Orden de locks: origen (Lock A + `xmin`) → retención → ED |
| Anulación vs. respuesta de autorización | Sin conflicto: la anulación no toca el ED | Imposible por construcción: con el ED en `Dispatching`/`Received` la anulación se rechaza |
| Solicitud de anulación oficial vs. reintento | — | `AnnulmentPending` no es candidato de emisión; el `xmin` del ED serializa la solicitud y la consulta |
| Confirmación de ANULADO vs. pagos | — | O-13. La anulación del origen reevalúa sus guards bajo Lock A |

## 13. Permisos (D-9)

| Ruta | Hoy | Aprobado |
|---|---|---|
| `GET /retentions/{id}/electronic/xml` | `expenses.documents.view` (cualquier origen) | por origen: Compra → `purchases.view`; Gasto → `expenses.documents.view`; `Manual` → denegado |
| `GET /retentions/{id}/ride/pdf` | `expenses.documents.view` | ídem |
| `POST /retentions/{id}/electronic/register` | `electronic-documents.retry` | sin cambio (O-4: ¿exigir además la lectura del origen?) |
| `GET /purchases/{id}/retention`, `GET /expenses/{id}/retention` | `purchases.view` / `expenses.documents.view` | sin cambio |

El origen se conoce por el dato (`SourceDocumentType`), así que la verificación va en el handler con `IRuntimePermissionAuthorizer` (patrón ya existente en `CashFundingRequestQueries`). Sin permiso, la respuesta es `NotFound` (fail-closed). Sin permiso transversal `retentions.*` por ahora.

## 14. `Issued` frente a la obligación de transmisión inmediata (D-12)

**Hallazgos:**
- `RetentionDocument.Issue()` corre dentro de la confirmación del origen: asigna el número de la secuencia `07` y la `IssueDate`, aplica la retención en la CxP y contabiliza. Con eso, la retención ya es un hecho contable y fiscal del ERP.
- La transmisión al SRI es **manual y separada**. `RETENTIONS-SRI-AUTHORIZATION-WIRING-DESIGN-04B` §K eligió la *Opción B — manual/endpoint, "para reducir riesgo inicial"* frente al disparo automático, como decisión de QA de puesta en marcha, no como política fiscal. `RegisterRetentionElectronicDocumentHandler` lo reitera: "Deliberadamente manual en esta fase".
- **Compras** tiene el botón "Registrar electrónicamente" (`PurchasesPage`/`usePurchasesPage` → `purchaseRetentionFacade.registerElectronic`). **Gastos no tiene ningún camino de UI**: las retenciones de Gastos solo se pueden transmitir llamando a la API directamente.
- Nada detecta ni alerta una retención `Issued` sin ED. `RetentionDocumentDto` no expone el estado electrónico y las pantallas no consultan `electronic-documents/by-source`.
- El retraso agranda otra brecha: mientras la retención está `Issued` sin transmitir, el origen puede anularse libremente (§9.1 → `Discarded`). Eso es correcto, pero sin transmisión inmediata la ventana es indefinida.

**Brecha:** si la obligación vigente es transmitir de inmediato, el ERP actual **no garantiza** que ocurra: depende de que alguien pulse un botón, que en Gastos ni siquiera existe.

**Opciones (no implementadas):**
- (a) Disparo automático posterior al commit de la confirmación: en la misma petición, después del `CommitAsync`, nunca dentro de la transacción, porque la llamada SOAP no debe sostener locks ni revertir la compra.
- (b) Disparo diferido idempotente: el job registra las retenciones `Issued` sin ED (`RegisterAsync` ya es idempotente por `uq_electronic_document_source`).
- (c) Mantener el disparo manual y añadir una alerta en el Monitor y en el detalle del origen, más la UI de Gastos.

Recomendación técnica: **(a) + (b)**. (a) da la inmediatez y (b) es la red de seguridad ante fallos. Respeta la preferencia de empresa (`electronic_documents.*`) solo si la normativa lo permite.

**DECISIÓN REQUERIDA (O-16):** confirmar la obligación de transmisión inmediata (texto y plazo) y elegir entre (a)/(b)/(c). Hasta entonces, se mantiene el registro manual.

## 15. Reproducción (PostgreSQL real)

Archivo: `backend/src/ERP.Infrastructure.Tests/Modules/Purchases/PurchaseRetentionConfirmIntegrationTests.ElectronicCancellation.cs`. Es parcial del fixture existente: `ConfirmPurchaseHandler`, `CancelPurchaseHandler`, `RetentionCanceller`, `ElectronicDocumentRepository` (`xmin`) y `ElectronicDocumentIssuer` son reales; solo se simulan la frontera SRI y el almacenamiento. Son **tests de caracterización** (afirman el comportamiento de hoy): al implementar este ADR deben **invertirse**, no borrarse.

| Caso | Resultado hoy | Esperado tras implementar |
|---|---|---|
| **A** `Signed`, reintento en vuelo y anulación durante `SendAsync` | La anulación hace commit sin bloquearse; queda `Authorized` con la retención `Cancelled` | Gana uno solo: si el reclamo `Dispatching` ganó, la anulación se rechaza; si ganó la anulación, cero `SendAsync` |
| **B** La anulación gana y el reintento llega después | `SendAsync` se invoca → `Authorized` | Anulación bloqueada (Signed existente, I-3) o `Discarded` (Signed nuevo); nunca `SendAsync` |
| **C** El reintento gana (`Authorized`) y la anulación llega después | La anulación se acepta; el ED queda `Authorized` y todo se revierte localmente | La anulación se rechaza (D-6); cero reversos |
| **D** `Sent` persistido | Queda varado: ni reintento ni job | Candidato de consulta; anulación bloqueada |
| **E** `Received` | No reenvía y consulta → `Authorized` tras la anulación | La anulación se rechaza (D-5) |
| **Failed** | Sin XML (gate del proveedor de datos); sigue siendo candidato | La anulación lo deja `Discarded` en su misma transacción |

Gastos usa el mismo `RetentionCanceller` y tampoco lee el ED (verificado leyendo el código): mismas conclusiones.

## 16. Datos existentes — consultas del piloto (SOLO LECTURA)

Codificación de los enums persistidos como `int`:
- `electronic_documents.current_state`: 1 Draft, 2 XmlGenerated, 3 Signed, 4 Sent, 5 Received, 6 Authorized, 7 Rejected, 8 DeadLetter, 9 Cancelled, 10 Failed.
- `retention_documents.status`: 0 Draft, 1 Issued, 2 Cancelled.
- `source_document_type`: 0 Expense, 1 Purchase, 2 Manual.

```sql
-- Q1. Retención ANULADA con un ED que salió o pudo salir al SRI, o que sigue vivo (datos heredados, I-1).
SELECT r.tenant_id, r.company_id, r.id AS retention_id, r.retention_number,
       r.source_document_type, r.source_document_id, r.cancelled_at,
       e.id AS electronic_document_id, e.current_state, e.pre_dead_letter_state,
       e.access_key, e.authorization_number, e.authorization_date,
       e.retry_count, e.last_attempt_utc, e.updated_at, e.last_error
FROM retention_documents r
JOIN electronic_documents e
  ON e.tenant_id = r.tenant_id AND e.source_module = 'Retentions' AND e.source_entity_id = r.id
WHERE r.status = 2
  AND e.current_state IN (3, 4, 5, 6, 7, 8, 10)
ORDER BY e.current_state, r.cancelled_at;

-- Q1b. Autorizado en el SRI DESPUÉS de anular en el ERP (salida posterior a la anulación).
SELECT r.id, r.retention_number, r.cancelled_at, e.authorization_date, e.access_key
FROM retention_documents r
JOIN electronic_documents e
  ON e.tenant_id = r.tenant_id AND e.source_module = 'Retentions' AND e.source_entity_id = r.id
WHERE r.status = 2 AND e.current_state = 6 AND e.authorization_date > r.cancelled_at;

-- Q1c. Anulada en el ERP con el comprobante autorizado (antes o después): vigente en el SRI, con reversos ya hechos.
SELECT r.id, r.retention_number, r.cancelled_at, e.authorization_date, e.access_key
FROM retention_documents r
JOIN electronic_documents e
  ON e.tenant_id = r.tenant_id AND e.source_module = 'Retentions' AND e.source_entity_id = r.id
WHERE r.status = 2 AND e.current_state = 6;

-- Q2. Retención EMITIDA con un ED inconsistente o atascado.
SELECT r.tenant_id, r.company_id, r.id AS retention_id, r.retention_number,
       e.id AS electronic_document_id, e.current_state, e.pre_dead_letter_state,
       e.retry_count, e.last_attempt_utc, e.last_error
FROM retention_documents r
JOIN electronic_documents e
  ON e.tenant_id = r.tenant_id AND e.source_module = 'Retentions' AND e.source_entity_id = r.id
WHERE r.status = 1
  AND (
        e.current_state IN (2, 4, 9)
     OR (e.current_state IN (3, 5) AND e.access_key IS NULL)
     OR (e.current_state = 6 AND e.authorization_number IS NULL)
     OR e.current_state IN (7, 8)
  );

-- Q3. ED de retención cuya retención no existe o es de otra empresa (integridad de la referencia débil).
SELECT e.id, e.tenant_id, e.company_id, e.source_entity_id, e.current_state
FROM electronic_documents e
LEFT JOIN retention_documents r ON r.id = e.source_entity_id AND r.tenant_id = e.tenant_id
WHERE e.source_module = 'Retentions'
  AND (r.id IS NULL OR r.company_id <> e.company_id);

-- Q4. Resumen.
SELECT r.status AS retention_status, e.current_state, count(*)
FROM retention_documents r
LEFT JOIN electronic_documents e
  ON e.tenant_id = r.tenant_id AND e.source_module = 'Retentions' AND e.source_entity_id = r.id
GROUP BY r.status, e.current_state ORDER BY 1, 2;

-- Q5 (D-12). Retenciones EMITIDAS nunca transmitidas, con antigüedad, por origen.
SELECT r.tenant_id, r.company_id, r.source_document_type, r.id, r.retention_number,
       r.issue_date, (current_date - r.issue_date) AS days_since_issue
FROM retention_documents r
LEFT JOIN electronic_documents e
  ON e.tenant_id = r.tenant_id AND e.source_module = 'Retentions' AND e.source_entity_id = r.id
WHERE r.status = 1 AND e.id IS NULL
ORDER BY r.issue_date;

-- Q6 (D-12). Demora entre la emisión y la autorización.
SELECT r.source_document_type, count(*) AS authorized,
       avg(e.authorization_date::date - r.issue_date) AS avg_days,
       max(e.authorization_date::date - r.issue_date) AS max_days
FROM retention_documents r
JOIN electronic_documents e
  ON e.tenant_id = r.tenant_id AND e.source_module = 'Retentions' AND e.source_entity_id = r.id
WHERE e.current_state = 6
GROUP BY r.source_document_type;
```

`issue_date` es la columna de `RetentionDocument.IssueDate` (confirmado en `RetentionDocumentConfiguration`). **No se ejecutaron contra el piloto** (sin acceso). Las filas de Q1/Q1c entran en la conciliación manual (O-9) y en O-1.

## 17. Opciones consideradas

1. **Solo un gate en `RetryAsync`.** Insuficiente: deja la carrera A abierta (comprobar y luego enviar es TOCTOU) y no cubre `Received`/`Authorized`.
2. **Filtrar en el job.** Descartada: duplica la regla, el reintento manual y la reactivación la eluden, y viola SSOT.
3. **`MarkCancelled` en cascada al anular el origen.** Descartada: no anula nada ante el SRI y oculta la divergencia (contradice D-6).
4. **Persistir `Sent` antes del envío.** Descartada por D-3: mezclaría "despachado" con "respondido por Recepción". Se reemplaza por `Dispatching`.
5. **Bloquear la anulación del origen si existe cualquier ED.** Demasiado restrictiva: contradice D-2.
6. **Política aprobada:** D-1…D-12 con los invariantes I-1…I-5, el gate único, el reclamo `Dispatching` bajo el lock de la retención y la anulación en dos fases (`AnnulmentPending` → `Cancelled`) antes de cualquier reverso.

**T1 — cómo dispara un ED `Cancelled` la anulación del origen** (decisión técnica abierta, O-17):
- (a) Un handler de `ElectronicDocumentCancelledEvent` en Retentions invoca el comando de anulación del origen. ADR-023 limita a la auditoría como suscriptor "sin justificación arquitectónica"; este ADR sería esa justificación.
- (b) Comando explícito del usuario: tras confirmar ANULADO, se ejecuta la anulación del origen, que exige el ED `Cancelled`.
- Recomendación: **(b)**. Es explícita, no agrega suscriptores al dominio CLOSED y reutiliza la autorización y los guards del origen tal como están.

## 18. Rollout

1. Adjuntar o verificar el texto de NAC-DGERCGC25-00000014/-00000017 y de la obligación de transmisión inmediata. Ejecutar Q1–Q6 en el piloto.
2. **Fase 1 (sin estados nuevos):**
   - gate SSOT (I-1);
   - anulación del origen bloqueada con el ED en `Signed` existente, `Sent`, `Received` o `Authorized`, y permitida con ED inexistente, `Draft`, `Failed`, `Rejected` o `DeadLetter(pre=Failed)`;
   - reenvío desde `Signed` reemplazado por consulta previa (D-4, D-10);
   - `Sent` como candidato de consulta;
   - permisos por origen (D-9);
   - inversión de los tests de §15.
3. **Fase 2 (extensión de ADR-023):** `Dispatching` con el lock de la retención, `Discarded`, Monitor y dashboard.
4. **Fase 3:** `AnnulmentPending`, `MarkCancelled` con evidencia y anulación diferida del origen (T1), según O-11…O-14.
5. **D-12:** el disparo de transmisión que se elija en O-16, más la UI de Gastos.
6. Conciliación manual de los datos heredados (O-9).

## 19. Riesgos

- Bloquear la anulación con el comprobante autorizado deja al usuario sin anulación inmediata. Es deliberado: el comprobante es vigente y la normativa exige anularlo en línea.
- Con un `Signed` existente y sin P-2, un comprobante que nunca llegó al SRI queda retenido hasta intervenir manualmente. Es el costo aceptado de D-10.
- `Dispatching` huérfano (caída entre el commit del reclamo y la llamada): se resuelve solo por consulta, nunca por reenvío.
- Pagos aplicados durante `AnnulmentPending` (O-13).
- Con transmisión inmediata automática (O-16), un fallo de datos del proveedor dejaría retenciones `Failed`. Ya son visibles en el Monitor.

## 20. Tests requeridos para la implementación

- Inversión de los 6 tests de §15, con Gastos espejo de B/C/E.
- Concurrencia en PostgreSQL: reclamo `Dispatching` vs. anulación (un solo ganador; cero `SendAsync` si gana la anulación).
- `TIMEOUT`/`SIN_RESPUESTA` en `Signed` → cero reenvíos (D-10).
- `Discarded` solo desde los estados de I-2.
- `AnnulmentPending` sin reversos; `Cancelled` con evidencia → reversos exactamente una vez.
- Rechazo o vencimiento → vuelve a `Authorized` sin reversos.
- Gate: un módulo sin gate registrado no cambia (regresión de Sales/CreditNote).
- Architecture: ElectronicDocuments no referencia Retentions.
- Permisos (D-9): matriz con y sin permiso por origen, `NotFound` fail-closed.
- D-12, según la opción elegida: idempotencia del disparo y cero transmisiones de retenciones no `Issued`.

## 21. Interpretaciones a confirmar

- **D-1 frente a los datos heredados:** "nunca avanzar" se aplica también a la consulta automática de autorización de una retención ya `Cancelled` con el ED en vuelo. Esos casos (que solo pueden venir de antes de implementar) se concilian **manualmente** (O-9), no por el job.
- **D-4/D-10 frente al alcance:** la regla "Signed existente = incierto, nunca reenviar sin respuesta concluyente" se definió para retenciones, pero el código afectado es el emisor genérico. Aplicarla a facturas y notas de crédito es una decisión aparte (O-15).

## 22. Decisiones pendientes

| ID | Decisión | Tipo |
|---|---|---|
| **O-1** | Tratamiento de los comprobantes autorizados ya revertidos localmente (Q1c) | Funcional/fiscal |
| **O-4** | ¿El registro electrónico exige, además de `electronic-documents.retry`, la lectura del origen? | Seguridad |
| **O-9** | Procedimiento manual de conciliación de los datos heredados (I-1) | Funcional/operativa |
| **O-10** | Intervención manual para un `Signed` existente sin respuesta concluyente | Operativa |
| **O-11** | Fecha de referencia del plazo "día 7 del mes siguiente" | Fiscal (texto normativo) |
| **O-12** | Vía de corrección cuando la anulación no es aceptada o vence | Funcional/fiscal |
| **O-13** | ¿Bloquear pagos y créditos sobre la CxP del origen durante `AnnulmentPending`? (recomendado: sí) | Funcional |
| **O-14** | Cómo se confirma ANULADO: consulta al WS (sin evidencia hoy) o registro manual de evidencia; qué casos exigen aceptación del receptor | Fiscal + técnica |
| **O-15** | Extender D-4/D-10 a todos los tipos de comprobante | Técnica/funcional |
| **O-16** | Transmisión inmediata: ~~elegir (a)/(b)/(c) de §14~~ — implementado (a)+(b) en 01A (§23); queda confirmar el texto normativo de la obligación | Fiscal (texto) |
| **O-17** | T1: disparo de la anulación del origen tras ANULADO (recomendado: comando explícito) | Técnica |
| **P-2** | Reconocer con evidencia "clave desconocida por el SRI" frente a "en proceso" en `autorizacionComprobante` | Técnica (requiere evidencia SRI) |

## 23. Implementación — ZH-RETENTION-ELECTRONIC-LIFECYCLE-01A (2026-10-01)

**Alcance implementado:** fases 1 y 2 del rollout (§18) y la transmisión inmediata (O-16, opción (a)+(b)). **Fuera de alcance** (→ ZH-RETENTION-SRI-ANNULMENT-01): `AnnulmentPending`, solicitud oficial de anulación, aceptación del receptor, `Cancelled` con evidencia y anulación diferida del origen. No se cambió nada equivalente en Ventas ni en Notas de Crédito.

### 23.1 Arquitectura

| Pieza | Dónde | Rol |
|---|---|---|
| `IElectronicDocumentSourceLifecycleGuard` + resolver por `SourceModule` | ElectronicDocuments (Application) | Gate SSOT "el origen todavía permite procesamiento". Lee el estado actual de la BD (escalar, filtrado por tenant y empresa) y, con `lockForUpdate`, toma `FOR UPDATE` sobre la fila del origen |
| `RetentionElectronicSourceLifecycleGuard` | Retentions | Implementación: solo `Issued`. ElectronicDocuments no referencia Retentions |
| `ElectronicDocumentIssuer` | ElectronicDocuments | Único pipeline. Con guard: gate antes de registrar, antes de generar XML, antes de reactivar y antes de cada consulta externa. Reclamo `Dispatching` bajo lock. Reintento solo de consulta. Sin guard: v1.0 intacto |
| `IElectronicDocumentSourceCancellation` | ElectronicDocuments | Única decisión de anulación del origen según el estado electrónico. Toma el mismo lock y deja el descarte en staging |
| `RetentionCanceller` | Retentions | La consulta antes de mutar. Compras y Gastos la heredan sin código propio |
| `IRetentionElectronicTransmission` | Retentions | Única entrada de transmisión: post-commit de Compra/Gasto, job de recuperación y acción de recuperación |
| `RetentionElectronicRecoveryJob` | API/Hangfire | Cada minuto, con gracia de 2 min. Toma retenciones `Issued` sin comprobante o con uno en `Draft` |
| `IRetentionSourceAccess` | Retentions | Autorización de XML/RIDE y de la recuperación según el origen (D-9) |
| `ElectronicDocumentSourceStatus` | ElectronicDocuments (DTO) | Estado compacto para la UI, calculado en backend |

### 23.2 Decisiones de implementación

1. **`Dispatching` sin `Signed` intermedio persistido.** Para orígenes con guard, `XmlGenerated→Signed→Dispatching` se guardan en un solo `SaveChanges` dentro de la transacción del reclamo (lock de la retención → revalidar `Issued` → releer el ED bajo el lock → transicionar → commit). Después se llama al SRI. Consecuencia: desde 01A, una retención nunca queda en `Signed` sola. Todo `Signed` de retención es **histórico** (ambiguo, D-4) sin necesidad de una columna nueva que lo distinga.
2. **Sin migración.** `current_state` se persiste como `int` y no tiene CHECK: `Dispatching = 11` y `Discarded = 12` son valores nuevos, compatibles con todas las filas existentes. No se reclasifica ningún dato histórico.
3. **Dominio.** Cambios en la entidad:
   - Métodos nuevos `MarkDispatching` (desde `Signed`) y `MarkDiscarded` (desde `Draft`, `Failed` o `DeadLetter` de esos estados), con motivo y evento auditado.
   - `MarkSent` acepta `Dispatching`.
   - `MarkAuthorized`/`MarkRejected` aceptan `Signed`/`Dispatching`. Así, la consulta de un estado incierto puede registrar un resultado concluyente sin reenviar.
   - Los dos tests de dominio v1.0 que prohibían autorizar desde `Signed` se reescribieron a "prohibido antes de firmar".
4. **Cancel vs. Send** (sin locks globales; solo la fila de la retención):
   - Si la anulación gana, el reclamo espera el lock, ve `Cancelled` y no despacha.
   - Si el reclamo gana, la anulación ve `Dispatching` y se bloquea.
   - Orden de locks: origen (Lock A / `xmin`) → retención → ED. Nunca se sostiene un lock ni una transacción durante SOAP. El emisor rechaza reclamar dentro de una transacción de negocio abierta.
5. **Transmisión inmediata durable.** El outbox existente (`OutboxProcessor`) solo marca los mensajes como procesados y no enruta. Por eso la fuente durable de lo pendiente es la propia retención `Issued` sin comprobante, que el job de recuperación retoma. El disparo inmediato sigue el patrón ya existente de Ventas (`AuthorizeSalesHandler`): en la misma petición, después del commit. Un fallo del SRI no revierte la compra ni el gasto.
6. **UI.**
   - Compras: se eliminó el botón "Registrar electrónicamente".
   - Compras y Gastos muestran el estado electrónico compacto (Pendiente / Procesando / Autorizado / Rechazado / Requiere conciliación / Descartado).
   - El Monitor conoce `Dispatching` (reintentable solo como consulta) y `Discarded`.
7. **Códigos estables (422):** `ELECTRONIC_DOCUMENT_SOURCE_NOT_PROCESSABLE`, `ELECTRONIC_DOCUMENT_IN_PROCESS`, `ELECTRONIC_DOCUMENT_REQUIRES_SRI_ANNULMENT`.

### 23.3 Hallazgo fuera de alcance — job genérico de reintento inactivo

`ElectronicDocumentRetryJob` consulta candidatos (`GetRetryCandidatesAsync`) **antes** de fijar `JobExecutionContext`. En Hangfire no hay HttpContext, y el filtro global fail-closed (tenant + empresa) devuelve **0 filas**. Por eso el reintento automático genérico no procesa nada en producción para ningún tipo de comprobante. Lo demuestra el test `Hallazgo_GetRetryCandidatesAsync_sin_contexto_de_tenant_no_devuelve_candidatos`.

No se corrigió aquí: hacerlo activaría el reintento automático de Ventas y Notas de Crédito (CLOSED), un cambio de comportamiento que requiere su propio análisis.

Para Retenciones, la recuperación de lo no iniciado la cubre `RetentionElectronicRecoveryJob`. Los comprobantes `Failed`, `Dispatching` o `Received` se resuelven desde el Monitor ("Reintentar" = consulta, sin reenvío). Cuando se corrija el job genérico, debe verificarse que no compita con la recuperación de retenciones (hoy no se solapan: el genérico no ve filas y la recuperación solo toma retenciones sin comprobante o en `Draft`).

### 23.4 Limitaciones conocidas

- Si al despachar falta la URL del WS en `SriSettings` (fallo de prerrequisito, sin llamada de red), el documento queda en `Dispatching` y requiere conciliación. Por D-10 no se reintenta el envío.
- P-2 sigue abierta: un `Signed` histórico o un `Dispatching` sin respuesta concluyente del SRI no se resuelve solo (O-10).
- El RIDE y el XML de vista previa siguen exigiendo `Issued` (§8): no cambió.

### 23.5 Consultas de datos históricos (SOLO LECTURA, no ejecutadas: sin acceso al piloto)

La codificación de `current_state` está en §16, más 11 Dispatching y 12 Discarded.

```sql
-- A. Retención ANULADA con comprobante AUTORIZADO (ERP Cancelled / SRI Authorized).
SELECT r.tenant_id, r.company_id, r.id, r.retention_number, r.cancelled_at,
       e.id AS electronic_document_id, e.access_key, e.authorization_date,
       (e.authorization_date > r.cancelled_at) AS authorized_after_cancel
FROM retention_documents r
JOIN electronic_documents e
  ON e.tenant_id = r.tenant_id AND e.source_module = 'Retentions' AND e.source_entity_id = r.id
WHERE r.status = 2 AND e.current_state = 6;

-- B. Retención ANULADA con comprobante en vuelo o incierto (Signed / Dispatching / Received / DeadLetter).
SELECT r.tenant_id, r.company_id, r.id, r.retention_number, r.cancelled_at,
       e.id AS electronic_document_id, e.current_state, e.pre_dead_letter_state, e.retry_count, e.last_error
FROM retention_documents r
JOIN electronic_documents e
  ON e.tenant_id = r.tenant_id AND e.source_module = 'Retentions' AND e.source_entity_id = r.id
WHERE r.status = 2 AND e.current_state IN (3, 4, 5, 8, 11);

-- C. Retención EMITIDA sin comprobante electrónico (la recuperación las tomará tras el despliegue).
SELECT r.tenant_id, r.company_id, r.source_document_type, r.id, r.retention_number,
       r.issue_date, r.updated_at
FROM retention_documents r
WHERE r.status = 1
  AND NOT EXISTS (SELECT 1 FROM electronic_documents e
                  WHERE e.tenant_id = r.tenant_id AND e.source_module = 'Retentions' AND e.source_entity_id = r.id)
ORDER BY r.issue_date;

-- D. Signed históricos de retención sin resultado final (requieren conciliación por consulta).
SELECT r.tenant_id, r.company_id, r.id AS retention_id, r.status AS retention_status,
       e.id AS electronic_document_id, e.access_key, e.retry_count, e.last_attempt_utc, e.last_error
FROM electronic_documents e
JOIN retention_documents r ON r.id = e.source_entity_id AND r.tenant_id = e.tenant_id
WHERE e.source_module = 'Retentions'
  AND (e.current_state = 3 OR (e.current_state = 8 AND e.pre_dead_letter_state = 3));

-- E. Demora emisión → inicio de transmisión → autorización (por origen).
SELECT r.source_document_type,
       count(*) AS with_electronic,
       avg(extract(epoch FROM (e.created_at - r.updated_at)) / 60) AS avg_minutes_issue_to_start,
       avg(extract(epoch FROM (e.authorization_date - e.created_at)) / 60)
         FILTER (WHERE e.current_state = 6) AS avg_minutes_start_to_authorized,
       max(e.created_at::date - r.issue_date) AS max_days_issue_to_start
FROM retention_documents r
JOIN electronic_documents e
  ON e.tenant_id = r.tenant_id AND e.source_module = 'Retentions' AND e.source_entity_id = r.id
WHERE r.status = 1
GROUP BY r.source_document_type;
```

Nota para E: en una retención `Issued`, `updated_at` es el momento de la emisión (`Issue()` llama a `SetUpdated`), porque solo `Cancel()` la vuelve a modificar.

Tratamiento: A y B son datos heredados y entran en la conciliación manual (O-9, O-1). C se procesa automáticamente al desplegar. D requiere conciliación por consulta (O-10, P-2).

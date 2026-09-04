# DOCUMENT-SEQUENCES-DESIGN-02 — Diseño de configuración de secuencias documentales SRI

## Estado

**Aprobado como decisión técnica.** 2026-09-04.

Este documento **no implementa cambios por sí mismo**. Continúa [DOCUMENT-SEQUENCES-ARCHITECTURE-AUDIT-01](./ADR-019-document-sequence-infrastructure.md) (auditoría realizada sin cambios de código) y fija las decisiones de arquitectura necesarias antes de conectar `RetentionDocument` a `CaptureNextAsync` y antes de construir cualquier configuración de número inicial. Cualquier implementación futura (`DOCUMENT-SEQUENCES-CONFIG-03`, `DOCUMENT-SEQUENCES-CAPTURE-HARDENING-04`, `RETENTIONS-DOCUMENT-SEQUENCE-02E`) requiere su propia entrega, con sus propios tests y migraciones, respetando lo decidido aquí.

## Contexto

- `DocumentSequence` + `IDocumentSequenceRepository.CaptureNextAsync` (ADR-019, FROZEN 2026-06-29) ya está production-ready para Facturas, Notas de Crédito y Retención de Compras (`Withholding`, doc type "07").
- `RetentionDocument` (módulo transversal Retentions, sobre `ExpenseDocument`) recibe hoy `RetentionNumber` como texto libre validado solo con `NotEmpty()` — no está conectado a ninguna secuencia real.
- La auditoría confirmó una brecha real: `DocumentSequence.Create()` fija `CurrentSeq = 1` de forma incondicional; no existe forma de arrancar una secuencia en un número distinto de 1.
- Coexisten tres secuencias internas independientes (`PurchaseReturnSequence`, `SupplierPaymentSequence`, `JournalEntrySequence`) que replican el mismo patrón de advisory lock pero no son numeración SRI.

## Decisiones aprobadas

### B. Alcance oficial de la secuencia

La clave única de numeración SRI es, y permanece:

```
(TenantId, CompanyId, EmissionPointId, DocTypeCode)
```

Esto ya está implementado en `uq_doc_seq` (`DocumentSequenceConfiguration.cs`) y **no cambia**. Cualquier documento SRI nuevo (Guía de Remisión, Liquidación de Compra) se incorpora reusando esta misma clave, sin cambio de modelo — solo agregando un `DocTypeCode` nuevo al catálogo `sri_doc_types` y llamando `CaptureNextAsync(..., docTypeCode)` desde su propio handler.

### C. `BranchId` — decisión

**`BranchId` NO forma parte de la clave de numeración SRI.** Se confirma como decisión definitiva, no como omisión.

Razón: la serie SRI (`estab-ptoEmi-secuencial`) la define el par `Establishment` + `EmissionPoint`, entidades de configuración administrativa de la empresa ante el SRI. `BranchId` es contexto operativo del usuario (desde qué sucursal opera, qué inventario/caja usa) — una sucursal puede emitir desde cualquier punto de emisión que tenga habilitado, y un mismo punto de emisión puede en principio ser usado por más de una sucursal operativa sin que eso implique una segunda serie SRI. Mezclar `BranchId` en la clave de secuencia crearía series SRI ficticias que el SRI no reconoce y duplicaría secuenciales para el mismo `estab-ptoEmi` real.

Si en el futuro el negocio requiere que cada sucursal tenga su propio punto de emisión exclusivo, la forma correcta de modelarlo es crear un `EmissionPoint` por sucursal — no agregar `BranchId` a `DocumentSequence`.

### D. Formato `estab-ptoEmi-secuencial`

Confirmado y sin cambios respecto al comportamiento actual:

- `estab` = `Establishment.Code`.
- `ptoEmi` = `EmissionPoint.Code`.
- `secuencial` = retorno de `CaptureNextAsync`, ya formateado `D9` (`CultureInfo.InvariantCulture`) dentro de `DocumentSequence.CaptureAndIncrement()`.
- La concatenación (`$"{est.Code}-{ep.Code}-{sequential}"`) es responsabilidad del handler de cada módulo, no de `DocumentSequence` — patrón ya usado en `AuthorizeSalesUseCases`/`AuthorizeSalesReturnUseCases`/`IssueWithholdingUseCases` y que Retentions debe replicar igual, sin inventar una variante.

### E. Número inicial configurable — decisión

**Se aprueba la necesidad funcional**, con implementación diferida a `DOCUMENT-SEQUENCES-CONFIG-03`. Motivo de negocio: empresas que migran desde otro sistema (facturación física o electrónica previa) ya tienen numeración SRI en curso ante el SRI y deben poder continuarla — arrancar siempre en 1 rompe la continuidad exigida por el SRI y forzaría re-autorización de rangos ya usados.

Diseño aprobado para la fase de implementación:

- El número inicial se configura por la misma clave de la secuencia: `(CompanyId, EmissionPointId, DocTypeCode)` — no por campo separado. En la práctica, "configurar el número inicial" es un nuevo comando que crea o ajusta el `DocumentSequence.CurrentSeq` antes de la primera captura real.
- No se agrega una tabla de "configuración de secuencia" distinta de `DocumentSequence` — se reutiliza la misma entidad. Un command `ConfigureDocumentSequenceStartingNumber` (nombre tentativo) actúa sobre la fila existente o la crea si no existe, dejando `CurrentSeq = valorConfigurado`.
- Ejemplos del ticket (Factura en 2500, Retención en 850, NC en 120) son casos válidos de este mismo mecanismo — un comando por `(EmissionPointId, DocTypeCode)`, no un campo por tipo de documento hardcodeado.

### F. Reglas de configuración/ajuste de secuencia

Aprobadas para la fase de implementación futura, como invariantes de dominio (no de UI):

1. **Secuencia no usada** (no existe fila en `DocumentSequence`, o existe con `CurrentSeq` nunca capturado realmente — ver nota de diseño abajo): se puede configurar libremente el número inicial vía el comando de configuración, sin restricción de permiso especial.
2. **Secuencia ya usada** (existe al menos una captura real — es decir, existe un documento emitido con ese número): el ajuste **no es libre**. Requiere:
   - Permiso especial explícito (nuevo, distinto del permiso de configuración inicial).
   - Motivo obligatorio (texto).
   - Registro de auditoría (ver sección I).
3. **No se puede configurar un valor `CurrentSeq` implícito menor o igual al último número realmente emitido** — el nuevo valor debe ser estrictamente mayor al último secuencial capturado y consumido por un documento persistido.
4. **No se acepta cero ni negativo** — mínimo válido es 1 (mismo invariante ya existente en `DocumentSequence.CaptureAndIncrement()` vía `chk_doc_seq_positive`).
5. **El valor debe caber en 9 dígitos** — máximo `999999999`, validado en el validator del command, no solo en el formateo `D9` (evita que un valor de 10 dígitos se trunque silenciosamente).

Nota de diseño para la fase de implementación: distinguir "secuencia no usada" de "secuencia usada" requiere una señal explícita — no basta con `CurrentSeq > 1`, porque el número inicial configurado también deja `CurrentSeq > 1`. La fase de implementación debe decidir cómo se detecta "ya hubo al menos una captura real" (p. ej. un flag `HasBeenCaptured`/`LastCapturedAt`, o verificar si existe un documento con ese secuencial persistido). Esta fase de diseño deja el requisito fijado; el mecanismo concreto se define en `DOCUMENT-SEQUENCES-CONFIG-03`.

**Resuelto en `DOCUMENT-SEQUENCES-CONFIG-03` (implementado):** se agregó `DocumentSequence.HasBeenUsed` (columna `has_been_used`, migración `AddDocumentSequenceHasBeenUsed`, con backfill `has_been_used = TRUE WHERE current_seq > 1` para filas históricas). Se descartó la heurística `CurrentSeq > 1` porque bloqueaba incorrectamente una reconfiguración legítima antes del primer uso real (p. ej. corregir un número inicial mal tecleado). `HasBeenUsed` se fija en `true` tanto en `DocumentSequence.CaptureAndIncrement()` como en el SQL raw de `DocumentSequenceRepository.CaptureNextAsync` (que no pasa por ese método) — ambos caminos de captura real quedan cubiertos.

### G. Captura en Draft vs. emisión

Confirmado, sin cambios — es el comportamiento actual y correcto:

- **No se captura número en Draft.** Los borradores (`SalesDraftUseCases`, `PurchaseDraftUseCases`, y el futuro draft de Retenciones) nunca llaman `CaptureNextAsync`.
- **Se captura solo al confirmar/emitir/autorizar** — mismo punto donde hoy lo hacen `AuthorizeSalesUseCases`/`AuthorizeSalesReturnUseCases`/`IssueWithholdingUseCases`.
- **Un número capturado nunca se reutiliza** — ni siquiera si el documento que lo consumió se anula/cancela después. La anulación de un documento (Nota de Crédito, cancelación de retención) no libera ni reutiliza su secuencial.
- **Los huecos por fallo posterior a la captura son un riesgo aceptado y documentado**, no un defecto — ya establecido en ADR-019 y confirmado aquí sin cambios.

### H. Ambiente SRI (pruebas/producción)

**Decisión: opción A — mantener la clave actual sin `Environment`, por ahora.**

Razón: el ambiente SRI (pruebas/producción) se refleja en el dígito de ambiente de la Access Key del comprobante electrónico, no en la numeración del punto de emisión — el SRI no exige series secuenciales separadas por ambiente para el mismo `estab-ptoEmi-docType`; exige que el ambiente quede correctamente marcado en el documento y su Access Key. Introducir `Environment` en la clave de `DocumentSequence` sin una necesidad SRI confirmada duplicaría series sin beneficio y complicaría la migración de número inicial (sección E).

Esta decisión queda **abierta a revisión** si una fase futura de XML/RIDE confirma, con evidencia normativa concreta, que el SRI exige (o que el proceso de pruebas de certificación del ERP requiere operativamente) secuencias independientes por ambiente. Si eso ocurre, se abre una ADR nueva — no se modifica esta decisión por interpretación, solo por evidencia verificada.

### I. Auditoría — mínima actual vs. granular futura

**Auditoría mínima actual (ya existente, sin cambios):** cada documento emitido (`SalesInvoice.InvoiceNumber`, `Withholding.WithholdingNumber`, futuro `RetentionDocument.RetentionNumber`) persiste el número capturado junto con `CreatedAt`/`UpdatedAt`/usuario emisor del propio agregado. Esto permite reconstruir, por inferencia, qué número usó qué documento y cuándo — es la auditoría que existe hoy y es suficiente para operación normal.

**Auditoría granular futura (recomendada, no implementada en esta fase):** se recomienda una tabla `document_sequence_audit` (o similar) para `DOCUMENT-SEQUENCES-CAPTURE-HARDENING-04`, con:

| Campo | Propósito |
|---|---|
| `SequenceId` | FK a `DocumentSequence` |
| `CapturedNumber` | Secuencial entregado (D9) |
| `ConsumingDocumentType` / `ConsumingDocumentId` | Qué documento lo consumió (patrón débil string+Guid, igual que `JournalEntry.SourceModule`/`SourceEventId`) |
| `UserId` | Quién disparó la captura |
| `CapturedAt` | Timestamp UTC |
| `Action` | `Configure` / `Capture` / `Lock` / `Unlock` / `Correct` |
| `Reason` | Obligatorio solo para `Configure` (secuencia ya usada) y `Correct`; null para `Capture` normal |

Esta tabla es independiente de `DocumentSequence` (que sigue siendo solo el contador vivo) y no participa en la ruta caliente de `CaptureNextAsync` salvo un insert adicional dentro de la misma transacción — evaluar impacto en throughput en la fase de implementación, dado que ADR-019 ya reporta el advisory lock como el limitante de throughput.

### J. Fuera de alcance de esta fase

- `PurchaseReturnSequence`, `SupplierPaymentSequence`, `JournalEntrySequence` — **no se migran ni se tocan.** Son secuencias internas del ERP (numeración interna de control, no comprobantes SRI electrónicos); no comparten clave (`CompanyId`+`FiscalYear` o `TenantId`+`CompanyId` sin `EmissionPointId`/`DocTypeCode`) ni objetivo con `DocumentSequence`. Cualquier decisión sobre ellas es un ticket distinto, explícitamente fuera de este diseño.
- Conexión real de `RetentionDocument` a `CaptureNextAsync` — queda fijada como decisión (sección siguiente) pero **no se implementa aquí**; es `RETENTIONS-DOCUMENT-SEQUENCE-02E`.
- XML/RIDE de Retenciones — depende de 02E, no de este diseño.
- Cualquier cambio a SaaS/Platform.
- Migraciones EF, código de dominio/aplicación/infraestructura, endpoints, UI.

### Retenciones — decisión de conexión futura (sin implementar)

Se fija como diseño aprobado para `RETENTIONS-DOCUMENT-SEQUENCE-02E`:

- `RetentionDocument` deja de recibir `RetentionNumber` como input manual del comando de emisión (`IssueRetentionUseCases`/`RetentionIssuer.RetentionIssueRequest`).
- El número se captura internamente vía `CaptureNextAsync(tenantId, companyId, emissionPointId, "07")`, replicando exactamente el patrón ya probado en `IssueWithholdingUseCases.cs` (cargar `EmissionPoint` + `Establishment`, capturar, formatear `estab-ptoEmi-secuencial`).
- `RetentionIssueRequest` ya recibe `EmissionPointId` hoy — no requiere cambio de contrato en ese campo, solo eliminar `RetentionNumber` como campo de entrada y calcularlo dentro del issuer.
- Esta conexión es de bajo riesgo arquitectónico porque no crea infraestructura nueva; consume la ya FROZEN y ya usada por un flujo de retención existente (Compras) con el mismo `DocTypeCode`.

## Fases siguientes recomendadas

1. `DOCUMENT-SEQUENCES-CONFIG-03` — implementar el comando de configuración de número inicial (sección E) + las reglas de la sección F, incluyendo el mecanismo de detección "secuencia usada vs. no usada", permiso especial y auditoría mínima del ajuste.
2. `DOCUMENT-SEQUENCES-CAPTURE-HARDENING-04` — evaluar e implementar, si se confirma necesario, la tabla de auditoría granular de la sección I.
3. `RETENTIONS-DOCUMENT-SEQUENCE-02E` — conectar `RetentionDocument` a `CaptureNextAsync("07")` según la decisión fijada arriba.
4. XML/RIDE Retenciones — posterior a 02E.

## Confirmación explícita

No se implementó ningún cambio de código en esta fase. No se creó ninguna migración. No se modificó `RetentionDocument`, `IssueRetentionUseCases` ni `RetentionIssuer`. No se tocó XML/RIDE. No se tocó SaaS/Platform. Este documento es exclusivamente de diseño y decisión técnica.

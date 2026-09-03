# RETENTIONS-MODULE-DESIGN-01 — Diseño del módulo transversal Retentions

## Estado

**Aprobado para planificación por fases.** 2026-09-03.

Este documento **no implementa cambios por sí mismo**. Rediseña la fase E1 de [`EXPENSES-FUTURE-ROADMAP-02.md`](./EXPENSES-FUTURE-ROADMAP-02.md), que originalmente proponía retención IVA como campos directos en `ExpenseLine`/`ExpenseDocument`. Esa propuesta queda **corregida**: Retenciones es un módulo/agregado independiente y transversal del ERP — no una funcionalidad interna de Gastos ni de Compras. Documenta una auditoría de código real y el diseño aprobado. Cualquier implementación futura requiere su propia entrega de desarrollo, con sus propios tests, y el detalle de fases E1-0 a E1-E definido aquí.

## Contexto

- El roadmap de Gastos ([EXPENSES-FUTURE-ROADMAP-02](./EXPENSES-FUTURE-ROADMAP-02.md)) proponía originalmente E1 como retención IVA modelada dentro de `ExpenseDocument`/`ExpenseLine`.
- Esa propuesta quedó corregida: Compras, Gastos y futuros documentos pueden originar retenciones — el concepto de retención es un proceso tributario transversal, no propiedad de un documento específico.
- El proyecto usa Clean Architecture, DDD, CQRS/MediatR, multi-tenant/company/branch fail-closed. Contexto Ecuador/SRI.
- Ya existen piezas reutilizables verificadas en código: `AccountsPayable.ApplyRetention()`/`ReverseRetention()`, `RetentionCalculator`, `IRetentionCodeResolver`, y el flujo de emisión de retenciones de Compras (`IssueWithholdingUseCases.cs`).

## Hallazgos técnicos

Evidencia verificada en el código real (rutas relativas a la raíz del repo):

- **`AccountsPayable`** (`backend/src/ERP.Domain/Modules/Payables/Entities/AccountsPayable.cs`) ya es transversal por diseño: usa `OriginType` (`AccountsPayableOriginType`: `PurchaseInvoice`/`ExpenseDocument`/`Manual`) + `OriginId` genérico (líneas 11-14, 32-52), y `ApplyRetention()`/`ReverseRetention()` (líneas 238-254) son agnósticas del origen. Este es el patrón que el módulo `Retentions` replica — no se inventa uno nuevo.
- **`IssuedWithholding`** (Purchases, `IssuedWithholding.cs`) **no es transversal**: hardcodea `PurchaseInvoiceId` como única FK de origen, a diferencia de `AccountsPayable` (origen genérico) o `ElectronicDocument` (`SourceModule`+`SourceEntityId`, string+Guid deliberadamente débil — `ElectronicDocument.cs:13-16`). Confirma que el diseño actual de retenciones está atado a Compras.
- **Placeholders ya reservados que anticipan este módulo**, todos sin implementación:
  - `AccountsPayableOriginType.Manual` — reservado, cero usos en el código.
  - `ElectronicDocumentType.Retention = 4` — reservado en el enum, cero productores.
  - `DocTypeCodes.ExpenseWithholding = "RETGAS"` — ya seedeado como `DocType` (`DocTypeConfiguration.cs:34-39`) y mapeado a SRI `"07"` (`DocTypeSriMapConfiguration.cs:36`), pero sin handler, entidad ni endpoint — doc-type fantasma.
- **`RetentionCalculator`** (`backend/src/ERP.Domain/Modules/Purchases/Services/RetentionCalculator.cs`) es un servicio de dominio puro, sin dependencia de `PurchaseInvoice` — reutilizable tal cual.
- **`IRetentionCodeResolver`** (`backend/src/ERP.Application/Modules/Purchases/Services/IRetentionCodeResolver.cs`, implementación en `RetentionCodeResolver.cs`) ya lee de catálogo real (`SriRetentionCodes` en BD) — cumple la regla de SSOT dinámico, no hardcodea porcentajes ni códigos.
- **`CaptureNextAsync`/`DocumentSequence`** (`DocumentSequence.cs`, `IDocumentSequenceRepository.cs`) soporta múltiples `DocTypeCode` por punto de emisión de forma aditiva — un tipo de documento nuevo para Retentions se registra sin tocar infraestructura CLOSED.
- **`JournalEntry`** (`backend/src/ERP.Domain/Modules/Accounting/Entities/JournalEntry.cs`) traza su origen vía `SourceModule`/`SourceEventType`/`SourceEventId` (string+string+Guid) — patrón débil ya usado por Expenses/Purchases, directamente reutilizable por Retentions sin modificar `JournalEntry` ni `IPostingEngine`.
- **Gap funcional detectado, no corregido por este diseño**: `SupplierRoleConfig.IsRequiredToKeepAccounting` está persistido pero `RetentionCalculator` nunca lo consume para diferenciar porcentajes IVA 30%/70% o Renta 1%/2% — el cálculo depende solo del código resuelto del catálogo. Se documenta como deuda heredada (ver "Decisiones aprobadas" #10).
- **ISD nunca se calcula** (`RetentionCalculator` retorna `TotalRetainedIsd = 0` siempre) — permanece fuera de alcance en E1.
- **Punto de creación de `AccountsPayable` en Gastos duplicado**: `ExpenseDocumentConfirmUseCases.cs` líneas 178-216 y 446-484 contienen el mismo bloque de creación de CxP en dos handlers distintos (confirmar borrador vs. crear ya confirmado). Retentions no agrega una tercera responsabilidad ahí — se integra como acción posterior e independiente (ver "Flujo desde Gastos").

## Elegibilidad para emitir retenciones

No toda empresa puede o debe emitir retenciones. Según normativa SRI: actúan como agentes de retención del Impuesto a la Renta las personas jurídicas y personas naturales obligadas a llevar contabilidad que paguen o acrediten ingresos gravados; el SRI mantiene una calificación de agentes de retención y de contribuyentes especiales, con número de resolución y fecha de vigencia; el agente de retención debe emitir el comprobante en el momento del pago o acreditación en cuenta, lo que ocurra primero; para IVA se expide un comprobante de retención por cada comprobante de venta con transacciones sujetas a retención; los comprobantes de retención electrónicos están reconocidos por la normativa de comprobantes electrónicos SRI (relevante para la fase futura de XML/RIDE, no para E1).

Antes de emitir/generar un `RetentionDocument`, el ERP debe validar:

1. **La empresa actual es agente de retención o está obligada según su configuración tributaria** — condición a nivel `Company`, nunca decidida por el usuario ni por el frontend.
2. **El proveedor/sujeto retenido no está exento** — `SupplierRoleConfig.IsRetentionExempt` debe respetarse tal cual existe hoy; si está `true`, no se emite retención sobre ese proveedor.
3. **El documento origen tiene base retenible** — el `RetentionCalculator` ya modela esto (`SkipReason` cuando no aplica); la validación de elegibilidad de empresa/proveedor es una guarda *previa* a invocar el cálculo, no un reemplazo de él.
4. **Existen códigos de retención aplicables desde catálogo/SSOT** — vía `IRetentionCodeResolver` contra `SriRetentionCodes`, sin fallback hardcodeado: si no hay código activo, la emisión debe fallar explícitamente, no asumir un valor por defecto.

### Origen de la condición de agente de retención

La condición de agente de retención **debe leerse de la configuración empresarial (`Company`), nunca del usuario ni del frontend** — mismo principio ya vigente en el proyecto de no confiar en el body para autoridad de tenant/company/branch.

**Campos verificados en el código real** (`backend/src/ERP.Domain/Modules/Company/Entities/Company.cs`) — auditoría de reutilización antes de proponer campos nuevos:

| Campo propuesto | Estado en `Company` | Equivalente encontrado |
|---|---|---|
| `IsRetentionAgent` | **Parcial — reutilizar** | `WithholdsRenta` (bool) y `WithholdsVat` (bool) ya existen, uno por tipo de impuesto — más preciso que un único booleano genérico. No hay campo agregado "es agente de retención" y no hace falta: se deriva de `WithholdsRenta || WithholdsVat` según el tipo de retención que se esté emitiendo. |
| `RetentionAgentResolutionNumber` | **No existe — pendiente** | Ningún campo asocia número de resolución SRI a `WithholdsRenta`/`WithholdsVat`. |
| `RetentionAgentEffectiveFrom` | **No existe — pendiente** | Ningún campo asocia fecha de vigencia a `WithholdsRenta`/`WithholdsVat`. |
| `IsSpecialTaxpayer` | **Parcial — reutilizar** | `SpecialTaxpayerNo` (string) ya existe; el booleano se infiere de `!string.IsNullOrWhiteSpace(SpecialTaxpayerNo)`, sin campo booleano explícito. |
| `SpecialTaxpayerResolutionNumber` | **Ya cubierto** | `SpecialTaxpayerNo` ya cumple ese rol (es, de hecho, el número de resolución de contribuyente especial). |
| `IsRequiredToKeepAccounting` (nivel empresa) | **Ya existe con otro nombre — reutilizar** | `IsAccountingReq` (bool, `Company.cs:36`, seteado en `UpdateFiscalSettings`, `Company.cs:255`). |

No existe `CompanyTaxProfile` como entidad separada (solo referenciado como concepto futuro en docs de arquitectura). `SriSettings` (`backend/src/ERP.Domain/Modules/Configuration/Entities/SriSettings.cs`) existe pero solo contiene certificado digital/ambiente/WSDL — nada de agente de retención ni contribuyente especial.

**Conclusión de reutilización**: `WithholdsRenta`, `WithholdsVat`, `IsAccountingReq` y `SpecialTaxpayerNo` en `Company` cubren la mayor parte de la necesidad y **deben reutilizarse tal cual**, sin duplicarlos. Lo único genuinamente ausente es la trazabilidad de la resolución SRI que habilita `WithholdsRenta`/`WithholdsVat` (número + fecha de vigencia) — sin eso, el ERP puede saber *que* la empresa retiene pero no *desde cuándo* ni *con qué respaldo documental*, lo cual es relevante si el estado de agente de retención cambia en el tiempo (calificación/descalificación SRI).

### Campos nuevos propuestos (solo si se confirma la brecha)

Si se decide cerrar esa brecha antes de emitir retenciones en producción:

```
Company (extensión aditiva, no romper UpdateFiscalSettings existente):
  RetentionAgentResolutionNumber : string?   // resolución SRI que habilita WithholdsRenta/WithholdsVat
  RetentionAgentEffectiveFrom    : DateTime? // vigencia de esa resolución
```

No se propone `IsRetentionAgent` ni `IsSpecialTaxpayer` como campos nuevos — serían booleanos redundantes sobre datos ya existentes (`WithholdsRenta`/`WithholdsVat`/`SpecialTaxpayerNo`), y el proyecto ya tiene una regla general contra duplicar fuentes de verdad.

### `SupplierRoleConfig.IsRequiredToKeepAccounting`

Se reafirma lo ya documentado en "Hallazgos técnicos": este campo existe y se valida como requisito de datos, pero **no se usa hoy** para diferenciar porcentajes de retención (IVA 30%/70%, Renta 1%/2%). Esta sección no cambia esa decisión — queda como regla fiscal a analizar en una fase posterior (ver "Decisiones aprobadas" y "Preguntas que quedan para análisis futuro"), no se corrige en E1 ni en E1-0.

### Alcance de esta sección en E1

- `RetentionDocument` sigue siendo un módulo independiente — la validación de elegibilidad es una guarda que el módulo `Retentions` consulta sobre `Company` (vía un servicio de solo lectura, sin duplicar el dato), no una entidad nueva de Retentions.
- La UI/proceso de emisión sigue integrada dentro de Gastos (y, en el futuro, Compras) — no se crea una pantalla separada de "elegibilidad" para el usuario; el sistema simplemente bloquea la emisión con un mensaje explícito si la empresa no es agente de retención para el tipo de impuesto correspondiente.
- E1 no inventa automatización de calificación/descalificación SRI, ni sincronización con el SRI para verificar vigencia — la fuente es exclusivamente la configuración ya cargada en `Company`.
- E1 no hardcodea ningún porcentaje ni código de retención — todo sigue viniendo de `IRetentionCodeResolver`/`SriRetentionCodes`, sin excepción por el hecho de agregar esta validación.

## Arquitectura propuesta

Módulo nuevo, transversal, paralelo a Purchases/Expenses/Payables — no submódulo de ninguno:

```
ERP.Domain/Modules/Retentions/
  Entities/        RetentionDocument.cs, RetentionDocumentLine.cs
  Enums/           RetentionStatus.cs, RetentionSourceDocumentType.cs, RetentionTaxType.cs
  Events/          RetentionDocumentIssuedEvent.cs, RetentionDocumentCancelledEvent.cs
  Interfaces/      IRetentionDocumentRepository.cs

ERP.Application/Modules/Retentions/
  Services/        IRetentionCodeResolver.cs (reubicado desde Purchases)
  UseCases/         IssueRetentionUseCases.cs, CancelRetentionUseCases.cs, GetRetentionUseCases.cs, CalculateRetentionPreviewUseCases.cs

ERP.Infrastructure/Persistence/Repositories/Retentions/
  RetentionDocumentRepository.cs
ERP.Infrastructure/Persistence/Configurations/
  RetentionDocumentConfiguration.cs, RetentionDocumentLineConfiguration.cs
ERP.Infrastructure/Persistence/Services/
  RetentionCodeResolver.cs (reubicado desde Purchases)

ERP.API/Controllers/RetentionsController.cs

frontend/src/modules/retentions/   (fases UI futuras, no en E1-A..E1-D)
```

**Principio rector**: `Retentions` conoce a `AccountsPayable` (para aplicar/revertir el neto) y a `JournalEntry` (vía su propio Domain Event → Translator), pero no conoce `PurchaseInvoice` ni `ExpenseDocument` directamente. La relación con el documento origen es genérica (`SourceDocumentType` + `SourceDocumentId`), replicando el patrón ya probado de `AccountsPayable.OriginType`/`OriginId` — no el patrón acoplado de `IssuedWithholding.PurchaseInvoiceId`.

## Agregado raíz: `RetentionDocument`

```
Id, TenantId, CompanyId, BranchId
SourceDocumentType        : RetentionSourceDocumentType   (ExpenseDocument | PurchaseInvoice | Manual reservado)
SourceDocumentId          : Guid
SubjectBusinessPartnerId  : Guid       // proveedor/sujeto retenido
EmissionPointId           : Guid
RetentionNumber           : string?    // asignado solo al emitir (CaptureNextAsync)
IssueDate                 : DateTime
Status                    : RetentionStatus
TotalRetainedVat           : decimal
TotalRetainedIncome         : decimal
TotalRetained              : decimal
CancelReason / CancelledAt / CancelledBy : nullable
_lines : List<RetentionDocumentLine>
```

Campos de ciclo electrónico SRI (`AccessKey`, `XmlPath`, `SriStatus`, etc.) **no se incluyen en E1** — quedan reservados para la fase futura de XML/RIDE, evitando repetir el error de `IssuedWithholding` de mezclar el ciclo tributario nacional con el documento de negocio desde el día 1.

`CompanyId`+`BranchId` presentes → `ICompanyOperationalEntity`, mismo contrato que `AccountsPayable`/`ExpenseDocument`/`JournalEntry`.

**Unicidad por origen**: índice único `(TenantId, CompanyId, SourceDocumentType, SourceDocumentId)` con filtro `WHERE Status != Cancelled`, más verificación explícita en el handler antes de crear — replica el doble mecanismo (BD + aplicación) que ya usa `AccountsPayable` para su propio origen. Garantiza que nunca existan múltiples retenciones activas sobre el mismo documento origen.

## Entidad `RetentionDocumentLine`

```
Id, TenantId, RetentionDocumentId
TaxType                   : RetentionTaxType   (Vat | Income — ISD fuera de alcance en E1)
RetentionCode             : string
RetentionCodeDescription  : string    // snapshot
TaxableBase                : decimal
RetentionPct                : decimal
AmountRetained             : decimal   // calculado, redondeado
```

Estructura calcada de `IssuedWithholdingDetail` (`IssuedWithholdingDetail.cs`), ya validada en producción para Purchases — no se reinventa.

### Enums nuevos

```csharp
enum RetentionStatus { Draft, Issued, Cancelled }

enum RetentionSourceDocumentType { ExpenseDocument, PurchaseInvoice, Manual }
// Manual reservado para futuro, sin implementación en E1 — espeja AccountsPayableOriginType.Manual,
// ya reservado y sin uso en el código actual.

enum RetentionTaxType { Vat, Income }
// ISD deliberadamente omitido — RetentionCalculator tampoco lo calcula hoy (TotalRetainedIsd=0).
```

### Nomenclatura del documento interno y tipo SRI

- El documento interno ERP usa la nomenclatura transversal **`RET`** (código de tipo de documento propio del módulo Retentions), salvo que la inspección técnica al implementar E1-B demuestre que debe mantenerse `RETGAS` u otro código ya seedeado por compatibilidad.
- El tipo de comprobante SRI **se resuelve desde `sri_doc_type`/`DocTypeSriMap`**, nunca hardcodeado en lógica de negocio — mismo mecanismo ya usado por el resto del ERP para mapear `DocTypeCode` interno a código SRI.

## Estados y transiciones

```
Draft ──Issue()──> Issued ──Cancel()──> Cancelled
Draft ──(descartado, sin persistir número)
```

- **`Draft`**: creado por preview/cálculo, sin número, sin efecto en CxP/contabilidad. Equivalente al `Draft` de `IssuedWithholding.CreateDraft()`.
- **`Issue(retentionNumber, issuedBy)`**: asigna número (vía `CaptureNextAsync`), pasa a `Issued`, dispara `RetentionDocumentIssuedEvent`. Efectos: aplica retención a CxP, genera asiento contable.
- **`Cancel(reason, cancelledBy)`**: solo desde `Issued`. Dispara `RetentionDocumentCancelledEvent`. Efectos: reversa CxP, reversa asiento.
- No hay transición `Cancelled → *` — estado terminal, igual que `ExpenseDocument`/`AccountsPayable`.

Guarda de dominio: `Issue()` solo permite `TotalRetained > 0` — si el cálculo da 0, no se emite un documento vacío (igual que `RetentionCalculator` hoy retorna `SkipReason` cuando no aplica).

## Flujo desde Gastos (primer consumidor)

```
ExpenseDocument (Confirmed)
   ↓
[acción manual explícita del usuario — NO automática al confirmar]
   ↓
POST /retentions  { sourceDocumentType: ExpenseDocument, sourceDocumentId, emissionPointId }
   ↓
IssueRetentionHandler:
   1. Carga ExpenseDocument, valida Confirmed, valida BranchId del usuario == BranchId del documento
   2. Idempotencia: ¿ya existe RetentionDocument Issued para este origen? → Conflict
   3. Resuelve SupplierRoleConfig del proveedor del gasto (IsRetentionExempt, códigos default)
   4. RetentionCalculator.Calculate(...) — mismo servicio que Purchases, sin fork
   5. RetentionDocument.CreateDraft(...) + líneas
   6. CaptureNextAsync(tenantId, companyId, emissionPointId, RetentionDocTypeCode) → número
   7. retentionDoc.Issue(number, userId) → evento
   8. Carga AccountsPayable por (OriginType=ExpenseDocument, OriginId=expenseDocument.Id)
   9. Si TotalRetained > 0: payable.ApplyRetention(TotalRetained, userId)
   10. Si no existe AP y TotalRetained > 0: rollback con error (igual que hoy en Purchases)
   ↓
JournalEntry de retención (vía RetentionDocumentIssuedPostingTranslator, nuevo)
```

**Emisión manual explícita**: por decisión aprobada, la emisión de retención en Gastos es siempre una acción manual del usuario sobre un gasto ya `Confirmed` — nunca automática al confirmar. La automatización se analizará en una fase posterior, condicionada a normativa y reglas fiscales específicas del cliente.

**Punto de integración**: no se agrega nada dentro de `ExpenseDocumentConfirmUseCases.cs` — la emisión de retención es una acción posterior e independiente sobre un documento ya confirmado, igual que Compras lo hace hoy (`POST {id}/withholding` es un endpoint separado de `Confirm`). Esto evita duplicar por tercera vez el bloque de creación de AP ya repetido dos veces en ese archivo.

## Flujo futuro desde Compras

En E1, **Compras sigue usando `IssuedWithholding`/`IssueWithholdingUseCases.cs` sin cambios** — cero riesgo de regresión sobre un flujo ya en producción con 4 endpoints, UI y tests. La migración de Compras al módulo `Retentions` (reemplazando `IssuedWithholding` por `RetentionDocument` con `SourceDocumentType = PurchaseInvoice`) queda diferida a una fase posterior separada, solo tras validar `Retentions` en producción con Gastos y con acuerdo explícito de que vale la pena el retrabajo.

Único cambio que toca código de Compras en E1: `RetentionCalculator` e `IRetentionCodeResolver` se **reubican** (no se duplican) desde `Modules/Purchases/Services` hacia el namespace de `Retentions`, con el using actualizado en `IssueWithholdingUseCases.cs` — sin cambiar comportamiento ni firma. Requiere tests de regresión completos de Purchases antes de mergear.

## Impacto en CxP

Ninguno estructural — `AccountsPayable.ApplyRetention()`/`ReverseRetention()` no cambian de firma ni de comportamiento (`AccountsPayable.cs:238-254`). `Retentions` es un consumidor de esa API ya existente, igual que `IssueWithholdingHandler` lo es hoy para Compras.

Se mantiene la unicidad por origen (ver "Agregado raíz") precisamente para evitar el riesgo de que `ReverseRetention()` — que siempre revierte el monto total actualmente retenido del AP, no un monto específico — revierta el retenido de más de una retención si alguna vez existiera más de una activa sobre el mismo origen. Con la unicidad garantizada, ese caso no puede ocurrir.

## Impacto contable

Mismo patrón ya usado (Domain Event → Translator → `PostingFact` → `IPostingEngine.PostAsync` → `JournalEntry`), sin tocar `IPostingEngine` ni `JournalEntry`:

```csharp
new PostingFact(tenantId, companyId, "Retentions", "DocumentIssued", retentionDocument.Id, issueDate, ...)
```

Nuevos `RetentionDocumentIssuedPostingTranslator` y `RetentionDocumentCancelledPostingTranslator` (análogos a los de Expenses), no modifican los traductores existentes de Expenses/Purchases.

**Posting estricto (decisión aprobada)**: si falla la generación del asiento, la retención **no se emite** — la transacción completa se revierte, sin excepción. Una retención sin asiento sería un pasivo fiscal fantasma. La regeneración de un asiento fallido queda para una herramienta futura y controlada, fuera del alcance de E1.

## Relación futura con SRI XML/RIDE

Explícitamente fuera de alcance de E1. `ElectronicDocumentType.Retention = 4` ya existe reservado en el enum (`ElectronicDocumentType.cs`) — la fase futura debería conectar `RetentionDocument` al pipeline genérico `ElectronicDocument` (`SourceModule="Retentions"`, `SourceEntityId=retentionDocument.Id`) en vez de replicar el patrón bespoke de `IssuedWithholding`, que mezcla campos SRI directamente en la entidad de negocio — decisión que este diseño considera un error a no repetir. Requiere su propia ADR (regla ya fijada: no tocar `ElectronicDocuments v1.0` sin ADR + evidencia técnica + tests + revisión de compatibilidad).

## Migración gradual

1. Construir `Retentions` como módulo nuevo, funcional primero para Gastos únicamente.
2. Compras sigue usando `IssuedWithholding` sin cambios durante toda esta fase.
3. Reubicar (no duplicar) `RetentionCalculator`/`IRetentionCodeResolver` hacia el namespace de `Retentions`, consumido por ambos módulos.
4. Migración completa de Compras a `RetentionDocument` se difiere a una fase posterior separada, con su propia decisión de negocio y su propio análisis de retrabajo.

## Fases pequeñas de implementación

- **E1-0 — Elegibilidad / configuración de agente de retención.** Servicio de solo lectura que resuelve elegibilidad desde `Company` (`WithholdsRenta`/`WithholdsVat`/`IsAccountingReq`/`SpecialTaxpayerNo`, reutilizados tal cual); si se confirma la brecha de trazabilidad SRI, agrega `RetentionAgentResolutionNumber`/`RetentionAgentEffectiveFrom` a `Company` como extensión aditiva de `UpdateFiscalSettings`. Sin este servicio, ninguna fase posterior puede validar elegibilidad antes de emitir. Tests de dominio/aplicación de la regla de elegibilidad, incluyendo el caso "empresa no es agente de retención" bloqueando la emisión.
- **E1-A — Dominio + Aplicación de `Retentions`.** Entidades, enums, `IssueRetentionUseCases`, `CancelRetentionUseCases`, reubicar `RetentionCalculator`/`IRetentionCodeResolver`, consumir el servicio de elegibilidad de E1-0 como guarda previa al cálculo. Tests de dominio + regresión completa de Purchases (por la reubicación de servicios compartidos).
- **E1-B — Infraestructura / persistencia.** `RetentionDocumentConfiguration`, repositorio, migración EF, registro de `DocTypeCode` para Retentions en `CaptureNextAsync` (nomenclatura `RET`, salvo que la inspección técnica exija mantener `RETGAS`).
- **E1-C — Integración con Expenses Confirm.** No se agrega nada dentro de `ExpenseDocumentConfirmUseCases.cs` (ver "Flujo desde Gastos"); se expone el endpoint/comando de emisión manual sobre un `ExpenseDocument` ya `Confirmed`, incluyendo `ApplyRetention`/`ReverseRetention` desde `IssueRetentionHandler` y los translators de posting (estrictos). Incluye permisos finos (`retentions.issue`, `retentions.cancel`, `retentions.view`) y la `DocumentFlowPolicy` propia de Retentions con reglas mínimas (motivo de cancelación si corresponde).
- **E1-D — UI integrada en `ExpenseDocumentFormPage`.** Acción "Emitir retención" visible sobre un gasto confirmado (bloqueada con mensaje explícito si la empresa no es agente de retención, resuelto por E1-0), vista de la retención emitida, cancelación. Integrada dentro de la página existente de Gastos — no se crea un módulo de UI separado en E1 (`frontend/src/modules/retentions` queda para una fase posterior si el alcance UI lo justifica).
- **E1-E — Tests end-to-end.** Flujo completo desde Gastos: elegibilidad→emitir→CxP neto→asiento→cancelar→reverso, incluyendo el caso bloqueado por falta de elegibilidad.

Cada fase cierra sus propios tests antes de darse por completa.

## Riesgos

- **Riesgo de doble motor de retenciones en paralelo** durante la transición: Compras sigue usando `IssuedWithholding` mientras Gastos usa `RetentionDocument` — dos entidades distintas conviven hasta la migración de Compras (fase futura). Debe documentarse explícitamente para que nadie asuma que son lo mismo.
- **Riesgo de reversa parcial mal calculada**: `ReverseRetention()` revierte el total retenido del AP, no un monto específico — la unicidad por origen es la mitigación, y debe mantenerse estrictamente.
- **Riesgo de heredar el gap de `IsRequiredToKeepAccounting`**: `RetentionCalculator` reutilizado tal cual hereda el defecto de no diferenciar porcentajes 30%/70% ni 1%/2% según ese flag — documentado como deuda consciente, no corregida en E1.
- **Riesgo al mover código compartido**: reubicar `RetentionCalculator`/`IRetentionCodeResolver` toca un flujo en producción (Compras) — exige tests de regresión completos antes de dar por cerrada E1-A.
- **Riesgo de nomenclatura de documento**: adoptar `RET` como código transversal sin verificar en E1-B si colisiona con `RETGAS` ya seedeado podría requerir migración de datos de catálogo — debe resolverse en la inspección técnica de esa fase, no asumirse de antemano.
- **Riesgo normativo SRI**: la relación futura con `ElectronicDocuments` (XML/RIDE) queda sin resolver hasta su propia ADR — cualquier necesidad de compliance fiscal antes de esa fase deberá evaluarse manualmente (comprobante físico/PDF, ver decisión #11).
- **Riesgo de emisión indebida por configuración incompleta de `Company`**: si `WithholdsRenta`/`WithholdsVat` no están correctamente configurados para una empresa que en realidad sí es agente de retención (o viceversa), el ERP bloquearía o permitiría emisiones incorrectamente sin que sea un bug del módulo `Retentions` en sí — depende enteramente de que el dato en `Company` esté al día. Mitigado únicamente por E1-0 validando explícitamente antes de emitir, nunca asumiendo.

## Decisiones aprobadas

1. `Retentions` es un módulo independiente y transversal: `ERP.Domain/Modules/Retentions`, `ERP.Application/Modules/Retentions`, `ERP.Infrastructure/Persistence/Repositories/Retentions`, `ERP.API/Controllers/RetentionsController`, y `frontend/src/modules/retentions` en fases de UI futuras.
2. `RetentionDocument` se relaciona con su documento origen de forma genérica: `SourceDocumentType` + `SourceDocumentId`.
3. `SourceDocumentType` contempla `ExpenseDocument`, `PurchaseInvoice`, y `Manual` reservado para futuro (sin implementación en E1).
4. Gastos es el primer consumidor implementado. Compras sigue usando `IssuedWithholding` en E1; su migración queda para una fase separada.
5. La emisión es manual y explícita: desde un gasto confirmado, el usuario decide si generar/emitir la retención. La automatización se analiza después, según normativa y reglas fiscales.
6. Nomenclatura del documento interno: transversal `RET`, salvo que la inspección técnica de E1-B demuestre que debe mantenerse otra (p. ej. `RETGAS`). El tipo SRI se resuelve desde `sri_doc_type`/`DocTypeSriMap`, nunca hardcodeado.
7. `Retentions` tendrá su propia `DocumentFlowPolicy`. En E1 solo se aplican reglas mínimas necesarias (p. ej. motivo de cancelación si corresponde). Reglas normativas SRI avanzadas se analizan después.
8. `Retentions` aplica/revierte retención usando `AccountsPayable.ApplyRetention()`/`ReverseRetention()` sin modificarlos. Se mantiene unicidad por origen para evitar múltiples retenciones activas sobre el mismo documento.
9. El posting de retenciones es estricto: si falla el asiento, la retención no se emite. La regeneración de un asiento fallido queda para herramienta futura y controlada, fuera de E1.
10. El gap de `IsRequiredToKeepAccounting` (no consumido hoy por `RetentionCalculator`) no se corrige en E1 — se documenta como deuda fiscal heredada para análisis posterior.
11. Fuera de alcance de E1: XML/RIDE de retención, autorización SRI, migración de Compras, retenciones automáticas, corrección de reglas fiscales avanzadas, ISD.
12. Antes de emitir un `RetentionDocument`, el ERP valida elegibilidad tributaria: empresa agente de retención según `Company` (`WithholdsRenta`/`WithholdsVat`/`IsAccountingReq`/`SpecialTaxpayerNo`, reutilizados sin duplicar), proveedor no exento (`SupplierRoleConfig.IsRetentionExempt`), base retenible en el documento origen, y código de retención activo en catálogo. La condición de agente de retención viene siempre de la configuración empresarial, nunca del usuario ni del frontend.
13. No se crean campos booleanos nuevos redundantes (`IsRetentionAgent`/`IsSpecialTaxpayer`) sobre datos que ya existen en `Company`. Si se confirma la necesidad de trazar resolución SRI y vigencia, se agregan únicamente `RetentionAgentResolutionNumber`/`RetentionAgentEffectiveFrom` como extensión aditiva, en la fase E1-0.

## Preguntas que quedan para análisis futuro

1. ¿La automatización de emisión de retención en Gastos (condicionada a normativa fiscal) se implementará como una fase posterior de E1, o como una decisión de negocio independiente evaluada más adelante?
2. ¿Se requiere un control de tipo `FinancialLock` (como el que usa Compras hoy antes de emitir una retención) también para Gastos, o Gastos no tiene un concepto equivalente de concurrencia financiera sobre el mismo documento?
3. ¿La nomenclatura `RET` reemplaza a `RETGAS` en catálogo, o conviven ambos códigos con significados distintos? Se resuelve en la inspección técnica de E1-B.
4. ¿Vale la pena migrar Compras a `RetentionDocument` en el mediano plazo, dado que `IssuedWithholding` ya está en producción con endpoints, UI y tests propios — o se acepta convivencia permanente de ambas entidades mientras funcionen?
5. ¿Cuándo y cómo se corrige el gap de `IsRequiredToKeepAccounting` en `RetentionCalculator`? Requiere su propio análisis fiscal antes de tocar el cálculo de porcentajes.
6. ¿Qué forma toma el comprobante de retención antes de que exista XML/RIDE (E1 no lo contempla siquiera en PDF) — se define en una fase E1-H futura o se posterga junto con la relación a `ElectronicDocuments`?
7. ¿Se confirma la necesidad real de trazar `RetentionAgentResolutionNumber`/`RetentionAgentEffectiveFrom` en E1-0, o basta con `WithholdsRenta`/`WithholdsVat` como están hoy (sin fecha de vigencia) mientras no exista un caso real de cambio de calificación SRI en el tiempo?
8. ¿Quién actualiza `WithholdsRenta`/`WithholdsVat`/`SpecialTaxpayerNo` en `Company` cuando cambia la calificación SRI de la empresa — proceso manual del administrador, o se evalúa alguna sincronización futura con el SRI? Fuera de alcance de E1 en cualquier caso, pero afecta qué tan urgente es E1-0.

## Entrega

- No se modificó código productivo (backend ni frontend).
- No se realizó ningún cambio funcional.
- Este documento es la guía oficial de diseño para la implementación futura del módulo `Retentions`, y reemplaza la fase E1 original de [`EXPENSES-FUTURE-ROADMAP-02.md`](./EXPENSES-FUTURE-ROADMAP-02.md).
- Cualquier implementación futura requiere su propia entrega de desarrollo y, para la relación con `ElectronicDocuments`, una ADR previa.

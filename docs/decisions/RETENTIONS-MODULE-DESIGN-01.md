# RETENTIONS-MODULE-DESIGN-01 — Diseño del módulo transversal Retentions

## Estado

**Aprobado para planificación por fases.** 2026-09-03.

Este documento **no implementa cambios por sí mismo**. Rediseña la fase E1 de [`EXPENSES-FUTURE-ROADMAP-02.md`](./EXPENSES-FUTURE-ROADMAP-02.md), que originalmente proponía retención IVA como campos directos en `ExpenseLine`/`ExpenseDocument`. Esa propuesta queda **corregida**: Retenciones es un módulo/agregado independiente y transversal del ERP — no una funcionalidad interna de Gastos ni de Compras. Documenta una auditoría de código real y el diseño aprobado. Cualquier implementación futura requiere su propia entrega de desarrollo, con sus propios tests, y el detalle de fases E1-A a E1-G definido aquí.

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

- **E1-A** — Dominio + Aplicación de `Retentions` (entidades, enums, `IssueRetentionUseCases`, `CancelRetentionUseCases`, reubicar `RetentionCalculator`/`IRetentionCodeResolver`). Tests de dominio + regresión completa de Purchases.
- **E1-B** — Infraestructura (`RetentionDocumentConfiguration`, repositorio, migración EF) + registro de `DocTypeCode` para Retentions en `CaptureNextAsync` (nomenclatura `RET`, salvo que la inspección técnica exija mantener `RETGAS`).
- **E1-C** — Integración con CxP y Contabilidad: `ApplyRetention`/`ReverseRetention` desde `IssueRetentionHandler`, nuevos translators de posting (estrictos).
- **E1-D** — API (`RetentionsController`) + integración desde Gastos (endpoint de preview + emisión manual, acción posterior a la confirmación).
- **E1-E** — UI mínima en Gastos: acción "Emitir retención" sobre gasto confirmado, vista de retención emitida, cancelación. (`frontend/src/modules/retentions` en esta fase o posterior, según alcance UI real).
- **E1-F** — Permisos finos (`retentions.issue`, `retentions.cancel`, `retentions.view`) + `DocumentFlowPolicy` propia de Retentions, con reglas mínimas (motivo de cancelación si corresponde).
- **E1-G** — Tests end-to-end (emitir→CxP neto→asiento→cancelar→reverso) para el flujo desde Gastos.

Cada fase cierra sus propios tests antes de darse por completa.

## Riesgos

- **Riesgo de doble motor de retenciones en paralelo** durante la transición: Compras sigue usando `IssuedWithholding` mientras Gastos usa `RetentionDocument` — dos entidades distintas conviven hasta la migración de Compras (fase futura). Debe documentarse explícitamente para que nadie asuma que son lo mismo.
- **Riesgo de reversa parcial mal calculada**: `ReverseRetention()` revierte el total retenido del AP, no un monto específico — la unicidad por origen es la mitigación, y debe mantenerse estrictamente.
- **Riesgo de heredar el gap de `IsRequiredToKeepAccounting`**: `RetentionCalculator` reutilizado tal cual hereda el defecto de no diferenciar porcentajes 30%/70% ni 1%/2% según ese flag — documentado como deuda consciente, no corregida en E1.
- **Riesgo al mover código compartido**: reubicar `RetentionCalculator`/`IRetentionCodeResolver` toca un flujo en producción (Compras) — exige tests de regresión completos antes de dar por cerrada E1-A.
- **Riesgo de nomenclatura de documento**: adoptar `RET` como código transversal sin verificar en E1-B si colisiona con `RETGAS` ya seedeado podría requerir migración de datos de catálogo — debe resolverse en la inspección técnica de esa fase, no asumirse de antemano.
- **Riesgo normativo SRI**: la relación futura con `ElectronicDocuments` (XML/RIDE) queda sin resolver hasta su propia ADR — cualquier necesidad de compliance fiscal antes de esa fase deberá evaluarse manualmente (comprobante físico/PDF, ver decisión #11).

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

## Preguntas que quedan para análisis futuro

1. ¿La automatización de emisión de retención en Gastos (condicionada a normativa fiscal) se implementará como una fase posterior de E1, o como una decisión de negocio independiente evaluada más adelante?
2. ¿Se requiere un control de tipo `FinancialLock` (como el que usa Compras hoy antes de emitir una retención) también para Gastos, o Gastos no tiene un concepto equivalente de concurrencia financiera sobre el mismo documento?
3. ¿La nomenclatura `RET` reemplaza a `RETGAS` en catálogo, o conviven ambos códigos con significados distintos? Se resuelve en la inspección técnica de E1-B.
4. ¿Vale la pena migrar Compras a `RetentionDocument` en el mediano plazo, dado que `IssuedWithholding` ya está en producción con endpoints, UI y tests propios — o se acepta convivencia permanente de ambas entidades mientras funcionen?
5. ¿Cuándo y cómo se corrige el gap de `IsRequiredToKeepAccounting` en `RetentionCalculator`? Requiere su propio análisis fiscal antes de tocar el cálculo de porcentajes.
6. ¿Qué forma toma el comprobante de retención antes de que exista XML/RIDE (E1 no lo contempla siquiera en PDF) — se define en una fase E1-H futura o se posterga junto con la relación a `ElectronicDocuments`?

## Entrega

- No se modificó código productivo (backend ni frontend).
- No se realizó ningún cambio funcional.
- Este documento es la guía oficial de diseño para la implementación futura del módulo `Retentions`, y reemplaza la fase E1 original de [`EXPENSES-FUTURE-ROADMAP-02.md`](./EXPENSES-FUTURE-ROADMAP-02.md).
- Cualquier implementación futura requiere su propia entrega de desarrollo y, para la relación con `ElectronicDocuments`, una ADR previa.

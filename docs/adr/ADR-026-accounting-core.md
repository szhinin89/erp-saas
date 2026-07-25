# ADR-026 — Accounting Core Architecture

## Estado

**Accepted.** 2026-07-24. Aprobado por el Architecture Review Board tras dos rondas de revisión (revisión inicial con cambios obligatorios; revisión final de aceptación sin hallazgos bloqueantes). Este ADR documenta el resultado del Architecture Review Board sobre las 5 decisiones pendientes para incorporar el módulo `Accounting` al ERP Core.

**Estado de implementación (2026-07-25)**: fundamentos de dominio, persistencia (migración `20260725000917_AddAccountingCoreFoundations` aplicada) y capa Application/API (CQRS Commands/Queries, `AccountingController`) ya implementados y auditados — ver detalle en `docs/STATUS.md`. Posting Engine (§8) implementado (Fase 3.1) con dos consumidores reales conectados por eventos: `SalesInvoiceAuthorizedPostingTranslator` (Fase 3.3) y `PurchaseInvoiceConfirmedPostingTranslator` (Fase 3.4). `PostingFact`/eventos de origen enriquecidos con `Subtotal`/`TotalVat`/`TotalIce`/`TotalDiscount`/`GrandTotal` (Fase 3.5.2, §4) — prerrequisito del motor de partida doble; `JournalFactory` todavía no consume estos montos (sigue generando solo el encabezado del asiento). Modelo de dominio de partida doble (`JournalEntryLine`/`PostingRuleLine`/`PostingAmountKind`) implementado en Fase 3.5.3 — diseñado en Fase 3.5.1. Persistencia EF Core de ambas líneas (migración `20260725165737_AddJournalEntryLineAndPostingRuleLine`) implementada en Fase 3.5.4. **Motor de partida doble real (Fase 3.5.5, 2026-07-25)**: `JournalFactory` construye `JournalEntryLine` reales a partir de `PostingRule.Lines`, resolviendo montos exclusivamente por `PostingAmountKind`; `JournalValidator` deja de ser NO-OP y valida balance, mínimo de líneas, cuentas duplicadas inválidas y montos. Sales y Purchases ya contabilizan con partida doble real, no solo encabezado. `Post()`/`Reverse()` y numeración `JournalEntrySequence` (§7) e integración con Caja/Inventory siguen sin implementar. Nota: la ADR-027 recomendada en Fase 3.5.1 para formalizar esta evolución no llegó a crearse como documento separado — su contenido se registra como enmienda de este mismo ADR-026 (§6/§6.2 abajo) hasta que se decida lo contrario.

## Contexto

El ERP Core no posee hoy un módulo `Accounting` funcional. `ERP_CORE_FREEZE.md` lo declara explícitamente como módulo futuro (junto con `Sales`, `HR`, `CRM`, `Production`, `Reporting`): *"no tienen implementación en este freeze. Se incorporarán como nuevos módulos `company_id`-scoped siguiendo el patrón CQRS existente"*.

La arquitectura vigente sobre la que `Accounting` debe apoyarse es Clean Architecture (`API → Application → Domain ← Infrastructure`) + DDD (agregados con factories `Create()`, invariantes en el dominio) + CQRS (MediatR, pipeline `ValidationBehavior → CompanyScopeBehavior → CachingBehavior`) + Domain Events + Outbox (ADR-007/ADR-008, FROZEN) como mecanismo de propagación de hechos entre módulos.

Existe un artefacto huérfano, `ERP.Application/Common/Interfaces/IAccountingService.cs`: una interfaz con un método `CreateXJournalEntryAsync` por cada tipo de documento (compra, venta, gasto, notas de crédito/débito, retenciones). No tiene implementación, no tiene consumidores, y `docs/STATUS.md` ya documenta bajo el encabezado *"snapshot histórico — pre FASE 1"* que la implementación real fue eliminada en la FASE 1 (2026-06-05) junto con el resto del dominio Billing/Subscriptions/Platform de aquella época. Es un residuo de un diseño anterior, incompatible además con el patrón de eventos de dominio ya vigente (un método nuevo por tipo de documento es el antipatrón que este ADR evita repetir).

## Problema

Definir cómo incorporar contabilidad al ERP sin romper los módulos operativos existentes (Sales, Purchases, Caja, Inventory) y sin introducir acoplamiento transversal — ni de los módulos operativos hacia `Accounting`, ni de `Accounting` hacia los agregados internos de otros módulos.

## Decisiones

### 1. Bounded Context — Accounting

`Accounting` es responsable de:

- Chart of Accounts (Plan de Cuentas)
- Accounting Periods (Períodos contables)
- Journal Entries (Asientos contables)
- Posting Rules (Reglas de contabilización)
- Financial Reports (Reportes financieros)

`Accounting` **no** es responsable de:

- impuestos (fuente de verdad: Configuración Tributaria, `ISriTaxResolver` — infraestructura CLOSED, ver CLAUDE.md)
- precios (fuente de verdad: Pricing Engine v2, ADR-021)
- documentos SRI (fuente de verdad: `ElectronicDocuments`, ADR-023, FROZEN)
- el origen del hecho económico — `Accounting` registra el efecto contable de un hecho ya ocurrido y ya validado en su módulo de origen; nunca decide si ese hecho es correcto ni lo recalcula.

### 2. Modelo de aislamiento

**Decisión: `Accounting` es exclusivamente `CompanyId`-scoped.**

Justificación:

- Separación fiscal por RUC — en Ecuador cada `Company` corresponde a una identidad fiscal propia ante el SRI/Superintendencia; compartir contabilidad entre companies mezclaría el registro legal de RUCs distintos.
- Independencia contable entre empresas de un mismo tenant — distinto giro de negocio, distinto plan de cuentas, distinta necesidad NIIF.
- Compatibilidad directa con `ERP_CORE_FREEZE.md`, que ya exige `company_id`-scoped para todo módulo operativo nuevo — `Accounting` no es una excepción a ese patrón.

**Declaración global de aislamiento:** todas las entidades y aggregates del módulo `Accounting` son `CompanyId`-scoped obligatoriamente: `Account`, `AccountingPeriod`, `JournalEntry`, `PostingRule`. Ninguno de los cuatro admite un modo tenant-scoped-compartido ni ninguna otra excepción — la propiedad `CompanyId` es obligatoria en el aggregate root de cada uno, no una particularidad exclusiva de `Account`.

### 3. Integración entre módulos

**Decisión: `Accounting` consume Domain Events. Ningún otro módulo consume `Accounting`.**

Flujo:

```
Aggregate
  → Domain Event
  → Outbox
  → MediatR
  → Accounting Handler
  → Posting Engine
  → JournalEntry
```

Reglas:

- Ningún módulo operativo (Sales, Purchases, Caja, Inventory) referencia `Accounting` — ni por `ProjectReference`, ni por interfaz inyectada, ni por request MediatR directo.
- `Accounting` no consulta agregados externos para generar asientos — no lee `SalesInvoice`, `PurchaseInvoice` ni ninguna entidad de otro módulo directamente.
- Cada módulo de origen publica hechos ya resueltos (montos, impuestos, referencias) en su propio Domain Event — `Accounting` solo interpreta el evento, nunca reconstruye el hecho económico por su cuenta.

### 4. Eventos Sales/Purchases

**Aprobado enriquecimiento aditivo solamente**, sobre `SalesInvoiceAuthorizedEvent` y `PurchaseInvoiceConfirmedEvent`.

Condiciones:

- No eliminar propiedades existentes.
- No cambiar el tipo de ninguna propiedad existente.
- Únicamente agregar campos nuevos.
- Documentar el impacto en los call sites afectados al momento de implementar.

Campos requeridos:

**`SalesInvoiceAuthorizedEvent`:**
- `Subtotal`
- `TotalVat`
- `TotalIce`
- información de pago necesaria para la contabilización (forma de pago / referencia de cobro)

**`PurchaseInvoiceConfirmedEvent`:**
- `Subtotal`
- `TotalVat`
- `TotalIce`

La implementación final puede evaluar, en lugar del enriquecimiento aditivo, la alternativa de que `Accounting` resuelva estos valores por consulta read-only al módulo de origen (mismo patrón que ya usa `PurchaseInvoiceAuditHandler`), si eso reduce el acoplamiento de Sales/Purchases a necesidades exclusivas de `Accounting`. Ambas rutas quedan abiertas; la decisión final se toma al implementar, con evidencia del impacto real en los call sites.

**Estado de implementación (Fase 3.5.2, 2026-07-25)**: `Subtotal`/`TotalVat`/`TotalIce` implementados en ambos eventos vía enriquecimiento aditivo (ruta elegida: campos en el evento, no consulta read-only), más `TotalDiscount` (adicional, identificado como necesario en el diseño de Fase 3.5.1 para el futuro mapeo de descuentos en `PostingRuleLine`). **Pendiente**: el requisito original de `SalesInvoiceAuthorizedEvent` sobre *"información de pago necesaria para la contabilización (forma de pago / referencia de cobro)"* — no formaba parte del alcance aprobado de Fase 3.5.2 (acotado explícitamente a los 4 montos), queda para una fase posterior o para una reevaluación explícita de si el motor de partida doble (Fase 3.5.x) realmente lo necesita antes de implementarlo.

### 5. Plan de cuentas

`Account` como Aggregate Root.

Propiedades principales:

- `CompanyId`
- `Code`
- `Name`
- `ParentAccountId`
- `AccountType`
- `Nature`
- `AllowsPosting`
- `IsActive`

Reglas:

- Estructura jerárquica (`ParentAccountId`).
- Solo las cuentas hoja (`AllowsPosting = true`) admiten contabilización directa.
- Baja lógica — soft disable (`IsActive = false`), consistente con la regla general del proyecto de nunca eliminar físicamente registros de negocio.

**`AccountType` — clasificación contable universal.** `AccountType` representa una clasificación contable universal de partida doble:

- `Asset` (Activo)
- `Liability` (Pasivo)
- `Equity` (Patrimonio)
- `Income` (Ingreso)
- `Expense` (Gasto)

`AccountType` **no** representa una clasificación de negocio configurable. A diferencia de `ItemTypeDefinition` (catálogo tenant-editable, porque clasifica ítems según decisiones de negocio propias de cada tenant), `AccountType` corresponde a fundamentos universales de contabilidad de partida doble que no varían por tenant ni por país — es la misma partición que usa cualquier plan de cuentas bajo NIIF o cualquier otro marco contable. Por eso se modela como un conjunto fijo, no como un catálogo editable.

Se mantiene abierta únicamente la extensión futura de agregar categorías adicionales si un marco contable específico lo exige (p. ej. subclasificaciones NIIF para reportes especializados) — pero esa extensión, si ocurre, se agrega como un valor nuevo dentro del mismo conjunto fijo, nunca como lógica condicional hardcodeada por tenant o por país en el Posting Engine ni en ningún otro componente.

### 6. Journal Entry

`JournalEntry` como Aggregate Root.

Reglas:

- Partida doble obligatoria — todo asiento debe balancear débitos y créditos.
- Append-only — un asiento contabilizado (`Posted`) no se edita.
- Ninguna corrección directa después de `Posted`; la única vía de corrección es `Reverse()`, que genera un asiento de reversión, nunca una modificación in-place.

**Estado de implementación (Fase 3.5.3, 2026-07-25)**: `JournalEntryLine` implementado como entidad hija del mismo aggregate (no aggregate independiente, no VO — justificación en el diseño de Fase 3.5.1: el balance es un invariante de agregado completo, no de línea individual). Campos: `Id`, `TenantId`, `JournalEntryId`, `AccountId`, `Description`, `Debit`, `Credit`, `SortOrder`. Invariante propio de la línea (`JournalEntryLine.Create`): exactamente uno de `Debit`/`Credit` mayor a cero, nunca ambos con valor ni ambos en cero. `JournalEntry.AddLine(...)` construye y agrega la línea; `JournalEntry.EnsureBalanced()` implementa el invariante de agregado (Σ Debit == Σ Credit). **Persistencia EF Core (Fase 3.5.4, 2026-07-25)**: `JournalEntryLineConfiguration` mapea `journal_entry_lines` (`Debit`/`Credit` en `numeric(18,2)`), con FK real a `accounts` (`Restrict`) — a diferencia de `PostingRuleLine`, esta es la línea del hecho contable ya persistido, no configuración. `JournalEntryConfiguration.HasMany(x => x.Lines)` con `OnDelete(Cascade)` hacia `journal_entry_id`. **Consumidor real (Fase 3.5.5, 2026-07-25)**: `JournalFactory` ahora llama `AddLine(...)` por cada `PostingRuleLine` de la regla, con monto resuelto por `PostingAmountKind`; `JournalValidator` invoca `EnsureBalanced()` (además de mínimo de líneas, cuentas duplicadas inválidas y montos) antes de aceptar el asiento. `Post()`/`Reverse()` y numeración quedan para una fase posterior.

### 6.1. Accounting Period

`AccountingPeriod` como Aggregate Root.

Propiedades mínimas:

- `Id`
- `TenantId`
- `CompanyId` (obligatorio)
- `FiscalYear`
- `PeriodNumber`
- `StartDate`
- `EndDate`
- `Status` (`Open`, `Closed`, `Locked`)
- `ClosedAtUtc`
- `ClosedBy`

Invariantes:

- Todo `AccountingPeriod` pertenece a una única `Company` — no existe período compartido entre companies.
- No existen períodos solapados dentro de una misma `Company` (el rango `[StartDate, EndDate]` de un período nuevo no puede intersectar el de un período existente de la misma `Company`).
- Un `JournalEntry` no puede ejecutar `Post()` contra un `AccountingPeriod` en estado `Closed` o `Locked` — el Posting Engine (§8) valida el estado del período antes de contabilizar, como parte del mismo mecanismo fail-closed.
- El cierre de un período es comportamiento del dominio (`AccountingPeriod.Close()`, `AccountingPeriod.Lock()`), nunca una actualización directa de `Status` desde Application o Infrastructure — mismo principio ya exigido para `JournalEntry.Reverse()` (§6) y consistente con el patrón de factories/comportamiento de dominio del resto del ERP (`Create()`, sin setters públicos de estado).

### 6.2. Posting Rule

`PostingRule` como Aggregate Root.

Propiedades mínimas:

- `Id`
- `TenantId`
- `CompanyId` (obligatorio)
- `SourceModule` (módulo de origen del hecho contable: `Sales`, `Purchases`, `Caja`, `Inventory`, …)
- `FactType` (tipo de hecho contable que resuelve, p. ej. `SalesInvoiceAuthorized`, `PurchaseInvoiceConfirmed`)
- mapeos de cuenta necesarios (p. ej. cuenta débito / cuenta crédito, o una colección de líneas de mapeo si un hecho requiere más de dos cuentas)
- `TaxCode` (opcional)
- `IsActive`

Reglas:

- `PostingRule` es **configuración de datos**, administrada por `Company` (consistente con §2 — `CompanyId`-scoped), nunca lógica de código.
- Prohibido implementar la resolución de reglas como condicionales cerrados en código — nunca `if (documentType == "Factura")`, nunca `if (module == "Sales")`, ni construcciones equivalentes (`switch` por `FactType`/`SourceModule`) en el Posting Engine ni en ningún handler de `Accounting`.
- Sigue el mismo principio ya validado en el proyecto para `PricingRule` (ADR-021) e `ItemTypeDefinition`: clasificación/comportamiento resuelto por datos consultados en tiempo de ejecución, con Strategy + Resolver donde aplique — no por ramas de código cerradas que solo el desarrollador puede extender.
- Agregar un hecho contable nuevo (`FactType` nuevo, `SourceModule` nuevo) debe requerir únicamente: (1) nuevos datos/configuración de `PostingRule`, y (2) un nuevo handler específico que escuche el Domain Event correspondiente — nunca una modificación del Posting Engine (§8) en sí.

**Estado de implementación (Fase 3.5.3, 2026-07-25)**: `PostingRuleLine` implementado como entidad hija del mismo aggregate, mismo patrón encabezado + líneas ya usado en el ERP (`PurchaseInvoice`+`PurchaseInvoiceDetail`, etc.). Campos: `Id`, `PostingRuleId`, `AccountId`, `Nature` (`AccountNature`, reutilizado — sin enum nuevo), `AmountKind` (`PostingAmountKind`, enum nuevo — ver justificación abajo), `SortOrder`. `PostingRule.Lines`/`AddLine(...)` coexisten con `DebitAccountId`/`CreditAccountId` planos — ninguno de los dos modelos se retira todavía, transición en curso. **Persistencia EF Core (Fase 3.5.4, 2026-07-25)**: `PostingRuleLineConfiguration` mapea `posting_rule_lines` — `AccountId` sin FK (columna plana, mismo criterio que `DebitAccountId`/`CreditAccountId`: configuración, existencia se valida en Application al resolver). `PostingRuleConfiguration.HasMany(x => x.Lines)` con `OnDelete(Cascade)` hacia `posting_rule_id`. Migración `20260725165737_AddJournalEntryLineAndPostingRuleLine` no modifica ninguna columna existente de `posting_rules`. **Consumidor real (Fase 3.5.5, 2026-07-25)**: `PostingRuleResolver` ahora resuelve `PostingRule.Lines` cargadas (`PostingRuleRepository.FindByKeyAsync` con `.Include(x => x.Lines)`, cambio mínimo necesario — sin él, `Lines` llegaría siempre vacío porque `PostingRule` es `sealed` sin navegación `virtual`, no hay lazy loading posible); `JournalFactory` las consume para construir el asiento real.

`PostingAmountKind` (`Subtotal`, `TaxVat`, `TaxIce`, `Discount`, `Retention`, `GrandTotal`) — enum nuevo, mismo criterio de justificación ya usado para `AccountType`/`AccountNature`: clasificación contable universal y fija de los componentes monetarios que un hecho contable puede aportar a una línea, no un catálogo tenant-editable. Únicos 6 valores aprobados en Fase 3.5.1 — ninguno adicional.

### 7. Numeración contable

**Decisión: crear `JournalEntrySequence` como componente independiente.**

**No reutilizar `IDocumentSequenceRepository`.**

Justificación:

`DocumentSequence` (ADR-019, FROZEN) numera comprobantes **SRI**, con clave `(tenantId, companyId, emissionPointId, docTypeCode)` — `emissionPointId` es un concepto exclusivo de documentos fiscales electrónicos.

`JournalEntry` es un registro interno contable, sin punto de emisión SRI. Reutilizar `CaptureNextAsync` obligaría a inventar un `emissionPointId` ficticio, lo cual es semánticamente incorrecto y viola la misma disciplina de "no inventar valores" ya vigente para Configuración Tributaria. `JournalEntry` queda, por definición, fuera del alcance de ADR-019 — no es una enmienda a esa infraestructura FROZEN, es un componente nuevo y distinto que replica su **patrón** de concurrencia:

- `pg_advisory_xact_lock` + transacción explícita, igual que `DocumentSequenceRepository`.
- Creación on-demand del registro de secuencia si no existe.
- Prueba de concurrencia real contra PostgreSQL (mismo estándar que `DocumentSequenceConcurrencyTests`), no solo unit tests con mocks.

### 8. Posting Engine

```
Domain Event
  → PostingFact
  → PostingRule
  → JournalEntry
```

Reglas:

- Fail closed — si no hay `PostingRule` configurada para un `PostingFact`, no se genera ningún asiento (nunca un asiento parcial o con cuentas adivinadas).
- Nunca inventar cuentas contables — toda cuenta usada en un asiento debe existir y estar activa en el Plan de Cuentas de la `Company` correspondiente; ausencia de configuración es un error de configuración del maestro, igual que la Regla 3 ya vigente en Configuración Tributaria (CLAUDE.md).

#### Persistencia y concurrencia (Fase 3.3.5)

- **`PostingPipeline` hace staging, nunca commit.** Prepara el `JournalEntry` (`AddAsync` sobre el `ChangeTracker`) y retorna — la persistencia física pertenece exclusivamente al ciclo externo de `ErpDbContext.SaveChangesAsync` que originó el Domain Event que disparó el Posting Engine. Ningún Domain Event Handler de este ERP llama `SaveChangesAsync` desde dentro de su propio `Handle()` — es la misma convención que ya siguen `SalesInvoiceAuthorizedHandler` (Caja) y todos los `*AuditHandler` (vía `EfAuditWriter<TAudit>.RecordAsync`, que solo hace `Add`).
- **Idempotencia concurrente vía advisory lock, no vía captura de excepción UNIQUE.** `PostingIdempotencyGuard` adquiere un `pg_advisory_xact_lock` transaccional (clave: `CompanyId`, `SourceModule`, `SourceEventId`, `FactType` — sin `TenantId`, no forma parte del índice único) antes de `FindByKeyAsync`, replicando el mismo mecanismo ya aprobado para `DocumentSequence` (ADR-019). Dos ejecuciones concurrentes para la misma clave se serializan antes de llegar a competir por el mismo `INSERT` — nunca generan una violación UNIQUE en el camino normal.
- **El índice único (`uq_journal_entries_company_source_event_fact`) queda como protección final**, no como mecanismo primario de detección de duplicados — sigue vigente sin cambios.

### 9. Eliminación de `IAccountingService`

**Eliminar:** `ERP.Application/Common/Interfaces/IAccountingService.cs`

Motivo:

- Dead code confirmado por búsqueda exhaustiva en `backend/` — cero implementaciones.
- Cero consumidores — ninguna inyección DI, ningún call site.
- Diseño anterior incompatible con la arquitectura de eventos de dominio ya vigente (un método por tipo de documento en vez de Domain Event + Handler).
- `docs/STATUS.md` ya documenta que la implementación real fue eliminada en FASE 1 (2026-06-05); el archivo de interfaz es el único residuo pendiente de limpieza.

### 10. Alcance Accounting v1

**Incluye:**

- Sales
- Purchases
- Caja
- Inventory

**Fuera de alcance v1:**

- Collections
- Cash & Banks avanzado (más allá de Caja ya existente)
- Manufacturing
- Consolidación
- Multi-moneda contable

Los módulos futuros se integrarán únicamente mediante nuevos Domain Events consumidos por nuevos handlers en `Accounting` — sin modificar el Posting Engine, el modelo de `JournalEntry` ni el Plan de Cuentas definidos en este ADR. Mismo patrón Open/Closed ya validado en el proyecto para Entity Audit (ADR-022, Regla 1: cada dominio nuevo agrega su propio evento + su propio handler, sin tocar el núcleo).

### 11. Compatibilidad con ADR existentes

| ADR | Relación |
|---|---|
| ADR-007 — Domain Events | `Accounting` consume el mecanismo de eventos de dominio ya FROZEN. No modifica `BaseDomainEvent` ni el despachador. |
| ADR-008 — Outbox | `Accounting` consume el patrón Outbox ya FROZEN para recibir eventos de forma confiable. No modifica la infraestructura de Outbox. |
| ADR-015 — Estrategia PostgreSQL | `JournalEntrySequence` (§7) sigue la misma estrategia de concurrencia ya validada (advisory locks) sin desviarse de las convenciones PostgreSQL del proyecto. |
| ADR-019 — Document Sequence | `Accounting` **no** reutiliza `IDocumentSequenceRepository` — queda fuera de su alcance por definición (§7). No se modifica ningún componente FROZEN de ADR-019. |
| ADR-021 — Pricing Engine SSOT | `Accounting` no calcula ni resuelve precios; consume montos ya resueltos por Sales/Purchases vía Domain Events. |
| ADR-022 — Audit Infrastructure | `Accounting` sigue el mismo patrón Open/Closed para su propia auditoría de entidad (`JournalEntryAudit`, `AccountAudit`) heredando `AuditRecordBase` — sin modificar los contratos FROZEN de `IAuditWriter<T>`/`IAuditReader<T>`/`IAuditService`. |

`Accounting` consume estas infraestructuras; no modifica ninguna.

### 12. Riesgos

- Doble contabilización — un evento procesado más de una vez (reintento de Outbox, error de idempotencia) podría generar dos asientos para el mismo hecho económico.
- Eventos incompletos — si un módulo de origen no publica todos los campos necesarios para el Posting Engine, el fail-closed de §8 bloquea la contabilización en vez de generar un asiento parcial, lo cual es correcto pero requiere monitoreo operativo.
- Performance de reportes financieros — consultas de Chart of Accounts/Journal Entries a gran volumen (cierre contable, balance general) pueden requerir estrategias de agregación o materialización que no están definidas en este ADR.
- Cierre contable — el bloqueo de períodos (`AccountingPeriod`) y su interacción con asientos tardíos o de ajuste no está detallado en este ADR y requiere diseño posterior antes de implementar.

### 13. Consecuencias

**Positivas:**

- Contabilidad trazable — cada asiento tiene origen explícito en un Domain Event identificable.
- Extensible — nuevos módulos se integran sin tocar el núcleo de `Accounting` (§10).
- Compatible con el modelo SaaS/multiempresa ya vigente (`CompanyId`-scoped, §2).
- Preparado para NIIF — el Plan de Cuentas jerárquico (§5) no impone una estructura fiscal única.

**Negativas:**

- Mayor disciplina de eventos exigida a los módulos de origen — cualquier campo faltante en un evento bloquea la contabilización (fail-closed, §8), lo cual traslada responsabilidad de calidad de datos a Sales/Purchases/Caja/Inventory.
- Necesidad de configuración inicial — sin Plan de Cuentas y `PostingRule` configurados por `Company`, no se genera ningún asiento; no hay valores por defecto que permitan operar "out of the box".

## Estado

**ACCEPTED**

## Pendiente

Fase 0 (housekeeping), Fase 1 (fundamentos de dominio), Fase 1.2-1.4 (persistencia EF Core + migración aplicada), Fase 2.0-2.2 (Application/API — Commands, Queries, Controller), Fase 3.1 (Posting Engine), Fase 3.3 (integración por eventos con Sales), Fase 3.4 (integración por eventos con Purchases), Fase 3.5.2 (enriquecimiento de `PostingFact`/eventos con montos, §4), Fase 3.5.3 (modelo de dominio de partida doble — `JournalEntryLine`/`PostingRuleLine`/`PostingAmountKind`, §6/§6.2), Fase 3.5.4 (persistencia EF Core de `JournalEntryLine`/`PostingRuleLine`, migración `20260725165737_AddJournalEntryLineAndPostingRuleLine`) y Fase 3.5.5 (`JournalFactory`/`JournalValidator` reales — motor de partida doble consumiendo `PostingRuleLine`/`PostingAmountKind`) completadas y auditadas — ver `docs/STATUS.md`. Pendiente: validación de existencia/actividad de cuentas en `JournalValidator` (hoy solo protegida por FK física), `Post()`/`Reverse()`, numeración `JournalEntrySequence` (§7), integración por eventos con Caja/Inventory (§3), reportes financieros, información de pago en `SalesInvoiceAuthorizedEvent` (resto de §4, ver nota en §4), monto de retenciones en `PostingFact` (`PostingAmountKind.Retention` sin efecto hasta entonces).

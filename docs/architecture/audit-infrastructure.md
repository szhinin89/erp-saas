# Infraestructura de Auditoría — Entity Audit (FROZEN) + Process Audit (futuro)

**Estado:** ✅ FROZEN — Entity Audit (contratos, dispatcher, flujo de extensión) cerrado definitivamente.
**Fecha de cierre:** 2026-07-07
**ADR:** [docs/decisions/ADR-022-audit-infrastructure-entity-vs-process.md](../decisions/ADR-022-audit-infrastructure-entity-vs-process.md)

Complementa [EVENT-DRIVEN-RULES.md](./events.md) (de donde esta infraestructura consume el pipeline de domain events/Outbox ya congelado) y [CORE-ARCHITECTURE.md](./architecture.md).

> **FROZEN:** Los contratos `IAuditWriter<T>`, `IAuditReader<T>`, `IAuditService`,
> `IAuditContext`, `AuditRecordBase`, `AuditActor`, `AuditSource`, `IAuditEvent` y el
> flujo de dispatch (`AggregateRoot.RaiseDomainEvent` → `ErpDbContext.SaveChangesAsync`
> → Outbox → MediatR `IPublisher` → `*AuditHandler` → `IAuditService` → `IAuditWriter<T>`)
> están cerrados. Ningún dominio nuevo puede modificar estas firmas ni esta secuencia.
> Todo dominio nuevo — de Entity Audit o de Process Audit — únicamente **agrega**
> consumidores nuevos (entidad + eventos + handler), nunca modifica lo anterior.

---

## 1. Qué queda FROZEN

La infraestructura común, sin conocimiento de ningún módulo de negocio:

| Capa | Componentes | Ubicación |
|---|---|---|
| Domain | `AuditRecordBase`, `AuditActor`, `AuditSource`, `IAuditEvent` | `ERP.Domain/Audit/` |
| Application | `IAuditWriter<T>`, `IAuditReader<T>`, `IAuditContext`, `IAuditService` | `ERP.Application/Audit/` |
| Infrastructure | `EfAuditWriter<T>`, `EfAuditReader<T>`, `HttpAuditContext`, `AuditService`, `ConfigureAuditBase<T>()` | `ERP.Infrastructure/Audit/` |
| Dispatcher | `ErpDbContext.SaveChangesAsync` (ya FROZEN por ADR-007/ADR-008) + MediatR `IPublisher` | `ERP.Infrastructure/Persistence/ErpDbContext.cs` |

Ningún componente de esta tabla es específico de Pricing, Inventory, Sales, Accounting,
ni de ningún otro dominio. `EfAuditWriter<T>`/`EfAuditReader<T>` están registrados como
**open generics** en DI — la misma clase sirve a todos los dominios sin recompilar
ni duplicar código.

## 2. Qué agrega cada dominio nuevo (y NADA MÁS)

1. Su propia entidad de auditoría, heredando de `AuditRecordBase`
   (ej. `PricingRuleAudit`, `PriceListItemAudit`), con **solo** los campos que ese
   dominio necesita — prohibido agregar columnas opcionales para otros dominios
   ("God table").
2. Sus propios domain events (levantados **solo** desde el `AggregateRoot`
   correspondiente, nunca desde el handler de Application — regla ya vigente en
   `EVENT-DRIVEN-RULES.md`).
3. Su propio `*AuditHandler` (`INotificationHandler<TEvent>` por cada evento),
   que traduce el evento a su entidad de auditoría y llama a
   `IAuditService.RecordAsync<TAudit>(...)`.
4. Su propia configuración EF (`IEntityTypeConfiguration<TAudit>`) invocando
   `builder.ConfigureAuditBase(tableName)` + sus índices específicos.
5. Su propia migración (`dotnet ef migrations add`).

**Prohibido en cualquier dominio nuevo:**

- Modificar `AuditRecordBase`, `IAuditWriter<T>`, `IAuditReader<T>`, `IAuditService`,
  `IAuditContext`, `AuditActor`, `AuditSource`, `IAuditEvent`, `EfAuditWriter<T>`,
  `EfAuditReader<T>`, `HttpAuditContext`, `AuditService`, `ConfigureAuditBase<T>()`.
- Escribir auditoría desde un Controller, desde un Repository, o desde React —
  toda auditoría nace de un domain event, se procesa en un `*AuditHandler` de
  Application.
- Reutilizar `UserActivity`/`IUserActivityRepository` para auditoría de negocio con
  campos tipados antes/después — esa tabla queda reservada para el feed liviano de
  actividad ("mi actividad reciente"), no para Entity Audit ni Process Audit.
- Crear una segunda implementación de `IAuditWriter<T>`/`IAuditReader<T>` para un
  dominio específico — la implementación genérica ya sirve a todos.
- Agregar columnas de identidad del actor (nombre, email, rol, o variantes) a la
  entidad de auditoría de un dominio — esa información vive exclusivamente en
  `AuditActor` (ver sección 9).

## 3. Dos categorías oficiales de auditoría

### 3.1 Entity Audit (implementado — Pricing es el piloto de referencia)

**Responsabilidad:** registrar cambios sobre una entidad de dominio identificable
por un `EntityId` propio (una fila de negocio concreta).

**Ejemplos de dominios futuros que reutilizan exactamente este mismo patrón:**
`InventoryItem`, `SalesInvoice`, `PurchaseOrder`, `JournalEntry`, `Customer`,
`Supplier`, etc.

**Responde:** ¿qué cambió? ¿quién? ¿cuándo? ¿cuál era el valor anterior? ¿cuál es
el valor nuevo?

### 3.2 Process Audit (futuro — NO implementado, solo documentado aquí)

**Responsabilidad:** registrar la ejecución de un **proceso completo** del ERP —
una operación que no pertenece a una única entidad de negocio, sino a una corrida
("run") de un proceso: importación masiva, recálculo de precios, cierre contable,
cierre diario de caja, conteo físico, recosteo, sincronización SRI, facturación
masiva, generación de asientos, backups, jobs de Hangfire, provisionamiento SaaS,
migraciones de datos, ETL, integraciones externas.

**Responde:** ¿qué proceso se ejecutó? ¿cuándo inició? ¿cuándo terminó? ¿cuánto
duró? ¿quién lo ejecutó (o qué job/sistema)? ¿cuántos registros procesó? ¿cuántos
errores hubo? ¿cuál fue el resultado?

### 3.3 Regla de no-mezcla

Una auditoría de proceso **no reemplaza** una auditoría de entidad, y viceversa.
Son complementarias:

- Un recálculo masivo de precios genera **una fila de Process Audit** ("se
  recalcularon 25.000 precios, terminó con 12 errores") **y**, si además cada
  precio individual cambia via el mismo camino de dominio que un cambio manual
  (`PricingRule.UpdateRule()`), sigue generando **su propia fila de Entity Audit**
  por cada regla modificada. Un proceso masivo no es una excusa para saltarse la
  auditoría de entidad de cada cambio individual.

## 4. Cómo Process Audit extenderá esta infraestructura sin modificarla

**Clave de diseño:** un `EntityId` en `AuditRecordBase` no está obligado a ser el Id
de una entidad de negocio persistente — puede ser el Id sintético de una **corrida
de proceso** (`ProcessRunId`, un `Guid` generado al iniciar el proceso, que
identifica unívocamente esa ejecución). Esto significa que Process Audit **no
necesita ningún cambio** en `AuditRecordBase`, `IAuditWriter<T>`, `IAuditReader<T>`,
`IAuditService` ni `IAuditContext` — los reutiliza exactamente igual que Pricing:

1. Un "proceso" se modela como un pseudo-agregado (`ProcessRun` o uno por tipo de
   proceso, ej. `PriceRecalculationRun`) que expone `Start()`, `Complete()`,
   `Fail()` y levanta domain events (`ProcessStartedEvent`, `ProcessCompletedEvent`,
   `ProcessFailedEvent`) implementando `IAuditEvent`, igual que `PricingRule`.
2. Una entidad `XxxProcessAudit : AuditRecordBase` (una por tipo de proceso, sin
   "God table" — mismo principio que Entity Audit) agrega los campos propios de
   proceso: `ProcessName`, `StartedAtUtc`, `FinishedAtUtc`, `DurationMs`,
   `RecordsProcessed`, `ErrorCount`, `ResultStatus`. `EntityId` = el `ProcessRunId`.
   `Action` = `"Started"` / `"Completed"` / `"Failed"` (mismo campo, mismo tipo,
   mismo significado de transición que en Entity Audit).
3. Un `XxxProcessAuditHandler` (`INotificationHandler<TEvent>`) traduce el evento a
   la entidad de proceso y llama a `IAuditService.RecordAsync<TAudit>(...)` — el
   mismo método, la misma firma, cero cambios.
4. `IAuditContext` necesitará una implementación adicional para contextos **no
   HTTP** (jobs de Hangfire, migraciones, integraciones) — una nueva clase que
   implemente la interfaz existente (ej. `SystemAuditContext`/`JobAuditContext`
   con `Source = AuditSource.System`), **no una modificación de la interfaz**.

Esto confirma que Process Audit es una **extensión** (nuevos consumidores) y no una
**modificación** de la infraestructura FROZEN — cumple Open/Closed Principle.

## 5. Reglas obligatorias para toda implementación futura (Entity o Process Audit)

1. Toda auditoría nace de un domain event levantado por un `AggregateRoot` (o
   pseudo-agregado de proceso) — nunca se escribe auditoría directamente desde un
   Controller, un Repository, un command handler sin pasar por evento, ni desde
   React.
2. Ningún dominio nuevo crea una segunda implementación de `IAuditWriter<T>` o
   `IAuditReader<T>` — la genérica basta siempre.
3. Ninguna entidad de auditoría agrega campos de otro dominio ("God table"
   prohibida, sin excepción).
4. Toda entidad de auditoría es append-only: sin `Update()`, sin `Delete()`, sin
   soft-delete — se factory-crea una vez y nunca se modifica.
5. Toda tabla de auditoría nueva invoca `ConfigureAuditBase<T>()` para las columnas
   comunes, y agrega únicamente los índices propios de sus consultas específicas.
6. Queda prohibido agregar lógica de auditoría directamente en Controllers,
   Handlers de negocio (fuera de un `*AuditHandler` dedicado), Repositories o
   Frontend. Toda nueva auditoría se integra exclusivamente mediante esta
   infraestructura oficial.
7. Cualquier necesidad de Process Audit se implementa como una extensión
   independiente (entidades + eventos + handler propios), sin modificar la
   infraestructura base de Entity Audit descrita en la sección 1.
8. Cualquier cambio real a los contratos FROZEN de la sección 1 requiere una nueva
   ADR aprobada — no una extensión menor ni un ajuste "de paso" dentro de un
   dominio de negocio.

## 6. Deuda técnica conocida (no bloquea el freeze del contrato, sí requiere seguimiento)

La auditoría de estabilidad previa (pre-freeze) detectó defectos de **implementación
concreta** — no de forma del contrato — que deben remediarse antes de confiar en los
datos ya capturados en producción:

- ~~`CurrentUserService.Email`/`FullName` devuelven `null` hardcodeado~~ —
  **RESUELTO (2026-07-07).** `AccessTokenService` ahora embebe `ClaimTypes.Email`/
  `ClaimTypes.GivenName` como claims del JWT al emitir el token (snapshot al
  momento de login/refresh, no una consulta en vivo). `CurrentUserService` lee
  esas claims. `AuditRecordBase.UserName` es ahora `string` no-nullable con
  fallback defensivo a `"Unknown"` (nunca `NULL` en BD — migración
  `MakeAuditUserNameRequired` con backfill). Ver sección 8.
- `HttpAuditContext.Actor.Source` está hardcodeado a `AuditSource.UserAction` — no
  existe todavía ninguna implementación de `IAuditContext` para procesos de
  sistema/batch. Se resuelve agregando una implementación nueva (ver sección 4,
  punto 4), no modificando la interfaz. **Pendiente.**
- `CorrelationId`/`RequestId` no tienen validación de longitud antes de persistir
  en columnas `varchar(100)` — un header HTTP `X-Correlation-Id` fuera de rango
  puede hacer fallar la transacción de negocio completa. Se resuelve truncando en
  `HttpAuditContext`/`AuditRecordBase.SetCommon`, no requiere tocar la forma del
  contrato. **Pendiente.**

Los dos puntos pendientes quedan como remediación obligatoria de corto plazo,
rastreados fuera de esta ADR de cierre — no invalidan el cierre de los
**contratos**, pero sí deben resolverse antes de tratar los datos de
`pricing_rule_audit`/`price_list_item_audit` como fuente confiable y completa
para decisiones de negocio.

## 8. AuditActor — único modelo oficial del actor, snapshot histórico (FROZEN)

> **FROZEN (ampliado 2026-07-07):** `AuditActor` es el **único** lugar donde vive
> información sobre quién ejecutó una acción auditable. Es un **snapshot histórico
> inmutable**: se calcula una vez en el momento del evento, se persiste, y nunca se
> vuelve a calcular, sincronizar ni actualizar — ni siquiera si el usuario cambia su
> nombre, se desactiva o se elimina después.

```csharp
public readonly record struct AuditActor(
    Guid TenantId, Guid UserId, string UserName,
    string? FullName, string? Email, string? RoleName,
    string? CorrelationId, string? RequestId, AuditSource Source);
```

- `UserId` (obligatorio) mantiene la identidad; `UserName` (obligatorio,
  no-nullable) mantiene el snapshot histórico del nombre visible — nunca vacío
  para un actor autenticado. `FullName`/`Email`/`RoleName` son detalle opcional
  adicional — ya poblados hoy desde `HttpAuditContext` porque el dato está
  disponible sin costo extra, pensados para que auditorías futuras (mostrar
  "Juan Pérez (Administrador)", filtrar por rol) no requieran modificar de nuevo
  este contrato.
- Solo contiene información **estable e histórica** del actor — nunca
  información de negocio (eso vive en la entidad de auditoría de cada dominio,
  ej. `OldRuleValue`/`NewRuleValue` en `PricingRuleAudit`) ni información
  temporal/derivable en el momento de la lectura.

### Claims — origen y justificación

`HttpAuditContext.Actor.UserName` se resuelve como `FullName ?? Email ??
"Unknown"` (con `ILogger` de advertencia si cae al fallback), y
`AuditRecordBase.SetCommon` aplica el mismo fallback como última línea de
defensa — nunca se persiste `NULL`.

`FullName`/`Email` vienen de claims embebidas en el JWT **al emitirlo**
(`AccessTokenService`), no de una consulta en vivo a la tabla de usuarios:

| Claim | Valor | Por qué |
|---|---|---|
| `ClaimTypes.Email` | `IdentityUser.Email.Value` | Estándar, sin ambigüedad. |
| `ClaimTypes.Name` | `IdentityUser.FullName` (FirstName + LastName) | Representa el nombre visible completo. **`ClaimTypes.GivenName` fue descartado deliberadamente** — esa claim representa semánticamente "solo el nombre" (no el nombre completo) y su uso inicial fue un error corregido el mismo día. `CurrentUserService.FullName` mantiene un fallback transitorio a `GivenName` únicamente para no invalidar tokens ya emitidos antes de la corrección (expira solo por vencimiento natural del token, `Jwt:ExpirationMinutes`). Ningún token nuevo emite `GivenName`. |

Esto significa que el nombre auditado es un snapshot **al momento de
login/refresh**, no al momento exacto del evento de negocio — una aproximación
aceptada: dentro de la vida de una sesión (`Jwt:ExpirationMinutes`, default 60
min) el nombre no cambia salvo edición de perfil concurrente, y el sistema no
depende de ninguna consulta posterior a Identity para resolverlo.

### Por qué esto no rompe nada de lo FROZEN

Ningún contrato cambió de forma incompatible: `AuditActor` es un `record struct`
ampliado de forma **aditiva** (nuevos campos, mismo significado de los
existentes); `IAuditContext`, `IAuditWriter<T>`, `IAuditReader<T>`,
`IAuditService`, el dispatcher y `EfAuditWriter<T>`/`EfAuditReader<T>` no
cambiaron ninguna firma. Domain Events y CQRS no se tocaron.

## 9. Regla Open/Closed sobre la identidad del actor

> Toda información relacionada con el actor que ejecutó una operación auditada
> debe vivir **exclusivamente** dentro de `AuditActor`. Queda **prohibido**
> agregar columnas relacionadas con el usuario (nombre, email, rol, o cualquier
> variante) directamente en las entidades de auditoría de cada dominio
> (`PricingRuleAudit`, `PriceListItemAudit`, o cualquier futura `XxxAudit`) —
> esas entidades ya heredan `UserId`/`UserName`/etc. de `AuditRecordBase` vía
> `AuditActor`, y no deben duplicarlos ni reinterpretarlos. Cualquier evolución
> futura sobre "qué se sabe del actor" se realiza **únicamente** mediante una
> extensión controlada y aditiva de `AuditActor` (nuevos campos opcionales),
> nunca agregando el dato en otro lugar. Esta regla es permanente y forma parte
> de la infraestructura FROZEN.

## 10. Referencia cruzada

| Documento | Tema |
|---|---|
| [EVENT-DRIVEN-RULES.md](./events.md) | Domain Events / Outbox que esta infraestructura consume |
| [ADR-007](../decisions/ADR-007-domain-events-foundation.md) | Domain Events Foundation |
| [ADR-008](../decisions/ADR-008-outbox-pattern-foundation.md) | Outbox Pattern Foundation |
| [ADR-022](../decisions/ADR-022-audit-infrastructure-entity-vs-process.md) | Decisión de cierre Entity Audit + diseño Process Audit |

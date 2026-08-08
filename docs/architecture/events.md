# Event-Driven Rules — ERP SaaS ZH Technologies

Reglas canónicas para Domain Events, Outbox y patrones event-driven.
Complementa [BACKEND-RULES.md](./backend.md) y [CORE-ARCHITECTURE.md](./architecture.md).

---

## Cuándo usar Domain Events

### ✅ USAR cuando

- Un AggregateRoot completa una transición de estado significativa para el negocio
- La acción tiene consecuencias en otros módulos (stock, contabilidad, notificaciones)
- La IA/analytics/automation futura necesitará reaccionar a esta acción
- Se requiere desacoplamiento entre módulos Application

### ❌ NO USAR cuando

- La acción es técnica, no de negocio (e.g., actualizar un índice de búsqueda)
- Es un evento de UI o de capa de presentación
- El cambio ocurre dentro de la misma transacción ya manejada por EF Core
- Es una consulta (CQRS: los queries no emiten eventos)

**Ejemplos correctos:**

```
InvoiceCreatedEvent        ✅
PaymentReceivedEvent       ✅
StockBelowThresholdEvent   ✅
SalesNoteAuthorizedEvent   ✅
JournalEntryCreatedEvent   ✅
StockTransferCompletedEvent ✅
```

**Ejemplos incorrectos:**

```
ButtonClickedEvent         ❌  (UI event)
PageOpenedEvent            ❌  (UI event)
UserLoggedInEvent          ❌  (técnico, no de negocio ERP)
QueryExecutedEvent         ❌  (no emitir en queries)
DoSomethingEvent           ❌  (nombre no descriptivo)
CreateInvoiceEvent         ❌  (tiempo presente — viola naming)
```

---

## Naming Convention

### Regla: Past Tense + Noun + "Event"

```
{Noun}{PastTenseVerb}Event
```

**Verbos estándar:**

| Acción | Verbo |
|--------|-------|
| Crear | Created |
| Actualizar | Updated |
| Aprobar | Approved |
| Cancelar | Cancelled |
| Contabilizar | Posted |
| Anular | Voided |
| Autorizar | Authorized |
| Completar | Completed |
| Recibir | Received |
| Superar umbral | ThresholdExceeded |

**Ejemplos:**

```csharp
// ✅ Correcto
public sealed record SalesBillPostedEvent : BaseDomainEvent { ... }
public sealed record StockAdjustmentApprovedEvent : BaseDomainEvent { ... }
public sealed record PaymentReceivedEvent : BaseDomainEvent { ... }
public sealed record InventoryThresholdExceededEvent : BaseDomainEvent { ... }

// ❌ Incorrecto
public class CreateSalesBillEvent { ... }       // tiempo presente
public class SalesBillEvent { ... }             // sin verbo
public class OnSalesBillPosted { ... }          // sin sufijo Event
```

---

## Estructura de un evento

### Implementación mínima (existente — compatible)

```csharp
public sealed class MiEventoExistente : IDomainEvent
{
    public Guid Id { get; } = Guid.NewGuid();
    public DateTime OccurredOn { get; } = DateTime.UtcNow;
    // propiedades de negocio...
}
```

### Implementación recomendada (nuevos eventos)

```csharp
public sealed record InvoiceCreatedEvent : BaseDomainEvent
{
    public required Guid InvoiceId { get; init; }
    public required decimal TotalAmount { get; init; }

    // El TenantId viene de BaseDomainEvent (init) — NO redeclarar aquí.
    // El CorrelationId viene de BaseDomainEvent (init)
}
```

Usar `sealed record` con `required` + `init` para garantizar immutabilidad.

> **Nota de dominio:** `TenantId` (scope ERP Core, heredado de `BaseDomainEvent`) nunca debe confundirse con `Subscriber`, terminología exclusiva del Control Plane Platform SaaS (fuera de `ERP.Domain` — ver [`ERP_CORE_FREEZE.md`](../../ERP_CORE_FREEZE.md)). Un evento de dominio de `ERP.Domain` no declara ni consume `SubscriberId`.

---

## Dónde viven los eventos

```
ERP.Domain/Modules/{Modulo}/Events/
├── InvoiceCreatedEvent.cs
├── PaymentReceivedEvent.cs
└── StockBelowThresholdEvent.cs
```

**Regla:** Los eventos viven en `ERP.Domain`. Los handlers viven en `ERP.Application`.
Los eventos NUNCA conocen sus handlers.

---

## Quién emite eventos

**Solo AggregateRoots.**

```csharp
// ✅ Correcto — dentro del AggregateRoot
public void Post(Guid postedBy)
{
    Status = DocumentStatus.Posted;
    // ...
    RaiseDomainEvent(new SalesBillPostedEvent { BillId = Id, TenantId = TenantId });
}

// ❌ Incorrecto — desde el handler de Application
_context.OutboxMessages.Add(OutboxMessage.From(new SalesBillPostedEvent(...)));
```

Los handlers de Application pueden REACCIONAR a eventos, pero no emitirlos directamente
a menos que lo hagan via otro AggregateRoot.

---

## Pipeline de dispatch

```
AggregateRoot.RaiseDomainEvent()
    ↓
SaveChangesAsync() en ErpDbContext
    ↓
Collect domain events (antes de persistir)
    ↓
Persist OutboxMessage + business data (ATÓMICO)
    ↓
MediatR IPublisher.Publish() para handlers in-process
    ↓
[Futuro] OutboxProcessor → message bus / analytics
```

La escritura al Outbox es ATÓMICA con los datos de negocio.
Si la transacción falla, no hay eventos huérfanos en el Outbox.

---

## Outbox Pattern

### Reglas

1. **Todo evento de dominio persiste en `OutboxMessages`** automáticamente vía `ErpDbContext`.
2. El `OutboxProcessor` (job Hangfire `process-outbox`) marca mensajes como procesados.
3. `ProcessedOnUtc == null` significa pendiente; no-null significa procesado.
4. El campo `Error` registra fallas de procesamiento (no null = error).
5. La tabla `OutboxMessages` es global (sin filtro tenant en queries).

### NO hacer con Outbox

```
❌ Leer OutboxMessages desde Application layer
❌ Escribir OutboxMessages directamente (solo via domain events)
❌ Borrar mensajes del Outbox (inmutable — para auditoría)
❌ Usar Outbox como sustituto de una base de datos de eventos (event sourcing)
```

---

## Tenant Context en Eventos

Para nuevos eventos que extienden `BaseDomainEvent`:

```csharp
// En el AggregateRoot
RaiseDomainEvent(new InvoiceCreatedEvent
{
    InvoiceId = Id,
    TenantId = TenantId,              // el AggregateRoot ya lo conoce (ITenantScopedEntity) — no se declara de nuevo en el evento hijo
    CorrelationId = _correlationId,   // si el AggregateRoot lo tiene
});
```

Para eventos existentes (implementan `IDomainEvent` directamente), el `TenantId`
en `OutboxMessage` será `null` — esto es aceptable durante la migración gradual.

---

## Versionado de Eventos

Cuando un evento cambia su estructura:

1. Crear nueva versión: `InvoiceCreatedEventV2`
2. Mantener versión original mientras haya handlers activos
3. El campo `Type` en `OutboxMessage` guarda el tipo completo — incluye versión
4. **NO renombrar** eventos con handlers en producción sin coordinación

---

## Granularidad

- Un evento = una transición de estado de negocio
- No combinar múltiples acciones en un evento
- No hacer eventos demasiado granulares (e.g., `InvoiceLineQuantityChangedEvent` es demasiado fino si hay `InvoiceUpdatedEvent`)
- Si un proceso de negocio requiere múltiples eventos, son OK siempre que cada uno sea coherente por sí solo

---

## Idempotencia

Los handlers de eventos DEBEN ser idempotentes:

```csharp
// ✅ Idempotente — verifica antes de actuar
public async Task Handle(InvoiceCreatedEvent notification, CancellationToken ct)
{
    var existing = await _db.JournalEntries
        .FirstOrDefaultAsync(j => j.SourceEventId == notification.Id, ct);
    if (existing is not null) return;  // ya procesado
    // ...crear asiento contable
}
```

El mismo evento puede publicarse más de una vez si el proceso se reinicia.
Los handlers no deben asumir entrega única.

---

---

# Event Versioning

Política canónica de versionado para Domain Events y OutboxMessages. Complementa la sección de Domain Events de arriba y [ADR-010](../decisions/ADR-010-event-versioning.md).

## Principio: Additive-First

Antes de cambiar un evento existente, pregunta:
> ¿Puedo agregar un campo opcional y los consumidores actuales siguen funcionando?

Si la respuesta es **sí** → agrega el campo. **No subas la versión.**
Si la respuesta es **no** → crea `EventoNombreV2Event` o sube `EventVersion`.

## Cuándo subir EventVersion

| Cambio | Acción | EventVersion |
|--------|--------|-------------|
| Agregar campo opcional (`string? NuevoCampo`) | Solo agregar | **No cambia (sigue en 1)** |
| Agregar campo requerido con default válido | Agregar + default | **No cambia** |
| Renombrar propiedad existente | ❌ Prohibido sin plan de migración | **Sube a N+1** |
| Eliminar propiedad usada por consumers | ❌ Prohibido | **Sube a N+1 + mantener N** |
| Cambiar tipo de propiedad (string → Guid) | Plan explícito | **Sube a N+1** |
| Cambiar semántica de propiedad existente | Plan explícito | **Sube a N+1** |

## Convención de nombres al versionar

```
// Versión 1 — existente
public sealed record InvoiceCreatedEvent : BaseDomainEvent { ... }

// Versión 2 — MANTENER v1 en paralelo mientras haya consumers activos
public sealed record InvoiceCreatedEventV2 : BaseDomainEvent { ... }
```

`EventVersion` en `OutboxMessage` refleja la versión del schema:
- `InvoiceCreatedEvent`   → `EventVersion = 1`
- `InvoiceCreatedEventV2` → `EventVersion = 2`

El campo `Type` en `OutboxMessage` guarda el `AssemblyQualifiedName` que incluye la versión implícita.

## Compatibilidad histórica (Outbox / Analytics)

El Outbox es un **log inmutable**. Los mensajes ya procesados NO se modifican.

| Escenario | Política |
|-----------|---------|
| Consumer nuevo que lee mensajes v1 históricos | Debe tolerar campos faltantes (null) |
| Analytics pipeline sobre mensajes v1 y v2 | Normalizar en la capa de proyección, no en el evento |
| Renombrar clase de evento en producción | Migrar el Type en Outbox o mantener alias |

## Cómo agregar un campo de forma segura

```csharp
// ✅ CORRECTO — additive-only, EventVersion sin cambio
public sealed record InvoiceCreatedEvent : BaseDomainEvent
{
    public required Guid InvoiceId { get; init; }
    public required Guid TenantId { get; init; }
    public required decimal TotalAmount { get; init; }

    // Nuevo campo — nullable, no breaking para consumers existentes
    public string? PaymentMethod { get; init; }
}

// ❌ INCORRECTO — breaking change
public sealed record InvoiceCreatedEvent : BaseDomainEvent
{
    public required Guid DocumentId { get; init; }  // renombrado de InvoiceId → ROMPEDOR
    public required Guid CompanyId { get; init; }    // cambio de tipo/semántica no planificado → ROMPEDOR
}
```

> Nota: los eventos de dominio ERP nunca declaran `SubscriberId` — esa terminología es exclusiva del Control Plane Platform SaaS, fuera de `ERP.Domain` (ver nota de dominio en la sección de Domain Events arriba). El scope correcto en ERP Core es `TenantId`/`CompanyId`.

## Política de deprecación

1. Marcar evento como `[Obsolete("Use InvoiceCreatedEventV2")]`
2. Mantener durante al menos **2 sprints** (handlers y analytics pueden adaptarse)
3. Verificar que no queden `OutboxMessages` pendientes del evento deprecado
4. Remover solo cuando todos los consumers actualizaron

## Prohibiciones absolutas (versionado)

```
❌ NO renombrar eventos con OutboxMessages pendientes en producción
❌ NO eliminar campos que analytics pipeline ya indexa
❌ NO cambiar el tipo de Id, TenantId, o CorrelationId
❌ NO hacer breaking changes sin ADR de migración
```

---

# Outbox Retention Strategy

Política canónica de retención para la tabla `OutboxMessages`. Ver [ADR-011](../decisions/ADR-011-outbox-retention-strategy.md) para la decisión.

## Estado actual (Fase 3)

La tabla `OutboxMessages` crece indefinidamente. El `OutboxProcessor` solo marca mensajes como `ProcessedOnUtc` — no borra ni archiva.

**Esto es correcto para Fase 3.** Implementar retención antes de necesitarla es complejidad prematura. Estado de fase del proyecto: ver `STATUS.md`.

## Política de retención (aplicar en Fase 4)

| Ventana | Acción |
|---------|--------|
| `ProcessedOnUtc < UtcNow - 30 días` | Candidato a purga o archivo |
| `ProcessedOnUtc IS NULL AND OccurredOnUtc < UtcNow - 7 días` | Alerta: mensaje huérfano |
| Mensajes con `Error IS NOT NULL` (sin procesar) | Retener indefinidamente hasta resolución manual |

## Implementación futura: IOutboxRetentionJob

Cuando sea necesario (tabla > ~500k filas o impacto en queries), crear:

```
ERP.API/Hangfire/
├── IOutboxRetentionJob.cs   (interface)
└── OutboxRetentionJob.cs    (implementación)
```

**No usar `OutboxProcessor` para purga** — son responsabilidades separadas:
- `OutboxProcessor` → procesar eventos
- `OutboxRetentionJob` → limpiar histórico procesado

El job de retención debe:
1. Operar en batches pequeños (100-500 filas)
2. Nunca borrar mensajes con `ProcessedOnUtc IS NULL` (pendientes)
3. Nunca borrar mensajes con `Error IS NOT NULL` (fallidos sin resolver)
4. Loggear cuántos mensajes purgó

## Archivo vs. Borrado

Antes de borrar, considerar si el histórico tiene valor para:

| Uso | Retención recomendada |
|-----|-----------------------|
| Debugging / soporte | 30 días post-procesado |
| Analytics / BI | Mover a tabla separada `OutboxMessagesArchive` |
| Compliance / auditoría | Según regulación local (LOPD/SRI) |
| AI training data | Mover a Data Warehouse antes de purgar |

**Regla:** No purgar sin confirmar que el Analytics pipeline ya proyectó los mensajes.

## Configuración propuesta (appsettings.json)

```json
"OutboxRetention": {
  "Enabled": false,
  "RetentionDays": 30,
  "BatchSize": 200,
  "CronSchedule": "0 3 * * *"
}
```

`Enabled: false` por defecto — activar explícitamente en producción.

## Cumplimiento (Compliance)

| Regulación | Consideración |
|------------|--------------|
| LOPD Ecuador | Datos personales en Payload: definir ventana de retención |
| SRI | Documentos electrónicos: retener referencias por 7 años |
| GDPR (futuro) | Derecho al olvido: el Payload puede contener PII |

**Acción futura:** para datos PII en eventos, usar un campo de referencia (ID) en lugar de datos directos en el Payload.

## Monitoreo (agregar en Fase 4)

Query de salud del Outbox:

```sql
-- Mensajes pendientes > 5 minutos (posible problema)
SELECT COUNT(*) FROM "OutboxMessages"
WHERE "ProcessedOnUtc" IS NULL
  AND "OccurredOnUtc" < NOW() - INTERVAL '5 minutes';

-- Tamaño de tabla
SELECT pg_size_pretty(pg_total_relation_size('"OutboxMessages"'));
```

Integrar en Health Check endpoint cuando la tabla supere ~100k filas.

---

## Referencia cruzada

| Documento | Tema |
|-----------|------|
| [AI-FOUNDATION.md](./ai-foundation.md) | Cómo la IA futura consume eventos |
| [BACKEND-RULES.md](./backend.md) | Reglas generales backend |
| [CORE-ARCHITECTURE.md](./architecture.md) | Arquitectura y capas |
| [ADR-007](../decisions/ADR-007-domain-events-foundation.md) | Decisión: domain events foundation |
| [ADR-008](../decisions/ADR-008-outbox-pattern-foundation.md) | Decisión: outbox pattern |
| [ADR-010](../decisions/ADR-010-event-versioning.md) | Decisión: versioning strategy |
| [ADR-011](../decisions/ADR-011-outbox-retention-strategy.md) | Decisión: retención |

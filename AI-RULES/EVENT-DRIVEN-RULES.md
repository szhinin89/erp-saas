# Event-Driven Rules — ERP SaaS ZH Technologies

Reglas canónicas para Domain Events, Outbox y patrones event-driven.
Complementa [BACKEND-RULES.md](./BACKEND-RULES.md) y [CORE-ARCHITECTURE.md](./CORE-ARCHITECTURE.md).

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
    public required Guid SubscriberId { get; init; }
    public required decimal TotalAmount { get; init; }

    // El TenantId viene de BaseDomainEvent (init)
    // El CorrelationId viene de BaseDomainEvent (init)
}
```

Usar `sealed record` con `required` + `init` para garantizar immutabilidad.

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
    RaiseDomainEvent(new SalesBillPostedEvent { BillId = Id, SubscriberId = SubscriberId });
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
    TenantId = SubscriberId,          // propagar el tenant
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

## Referencia cruzada

| Documento | Tema |
|-----------|------|
| [AI-FOUNDATION.md](./AI-FOUNDATION.md) | Cómo la IA futura consume eventos |
| [BACKEND-RULES.md](./BACKEND-RULES.md) | Reglas generales backend |
| [CORE-ARCHITECTURE.md](./CORE-ARCHITECTURE.md) | Arquitectura y capas |
| [ADR-007](../docs/adr/ADR-007-domain-events-foundation.md) | Decisión: domain events foundation |
| [ADR-008](../docs/adr/ADR-008-outbox-pattern-foundation.md) | Decisión: outbox pattern |

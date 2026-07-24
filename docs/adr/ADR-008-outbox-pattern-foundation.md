# ADR-008: Outbox Pattern Foundation

## Status

Accepted (2026-05)

## Context

El pipeline actual de domain events en `ErpDbContext.SaveChangesAsync()` persiste los datos de negocio y luego publica via MediatR `IPublisher`. Esto presenta dos riesgos:

1. **Pérdida de eventos:** si el proceso falla entre `base.SaveChangesAsync()` y el `_publisher.Publish()`, los datos se persisten pero los eventos nunca se publican.
2. **Sin log duradero:** no existe registro de qué eventos ocurrieron históricamente — necesario para analytics, auditoría y futuros pipelines de IA.

A futuro, conectar sistemas externos (analytics, automation, integraciones) requiere un canal confiable de eventos que no acople los consumidores al modelo de dominio del ERP.

## Decision

Implementar el **Outbox Pattern** como foundation layer:

1. En `SaveChangesAsync()`, serializar cada domain event a una fila `OutboxMessage` y persistirla en la **misma transacción** que los datos de negocio.
2. Un job Hangfire (`process-outbox`, cada minuto) procesa mensajes pendientes.
3. En esta fase, el processor solo marca mensajes como procesados.
4. El esquema `OutboxMessages` es global (no filtrado por tenant) para acceso cross-tenant desde analytics.

**NO implementar:**
- Kafka, RabbitMQ, Azure Service Bus (complejidad distribuida innecesaria ahora)
- Event sourcing completo
- Replay de eventos

## Consequences

- ✅ Atomicidad: si la transacción falla, no hay eventos huérfanos
- ✅ Log duradero: cada domain event queda registrado con tipo, payload y tenant
- ✅ Foundation para IA: la IA futura puede leer el log sin acoplarse al DbContext
- ✅ Cero cambio en comportamiento funcional: MediatR publish en-proceso sigue igual
- ⚠️ Crecimiento de tabla: `OutboxMessages` crece indefinidamente — necesita política de retención futura
- ⚠️ Serialización polimórfica: el campo `Type` usa `AssemblyQualifiedName` — sensible a renombrado de clases

## Tabla OutboxMessages

| Columna | Tipo | Propósito |
|---------|------|-----------|
| `Id` | uuid | PK, idempotencia |
| `Type` | varchar(500) | Tipo .NET completo del evento |
| `Payload` | text | JSON del evento |
| `OccurredOnUtc` | timestamp | Cuándo ocurrió |
| `ProcessedOnUtc` | timestamp? | null = pendiente |
| `Error` | varchar(2000)? | Último error de procesamiento |
| `TenantId` | uuid? | Subscriber que originó el evento |
| `CorrelationId` | uuid? | Request HTTP correlacionado |

## Alternatives Considered

- **Solo MediatR en-proceso:** rechazado — sin durabilidad ni log histórico
- **Kafka desde el inicio:** rechazado — complejidad operacional innecesaria para monolito modular en MVP
- **Interceptor EF Core:** posible alternativa de implementación — la decisión de usar `SaveChangesAsync` override mantiene consistencia con el código existente

# Event Catalog — ERP SaaS ZH Technologies

Catálogo oficial de Domain Events del ERP. Fuente de verdad para:
- analytics pipelines
- AI event handlers
- integrations
- documentación de producto

---

## Qué es este catálogo

Cada event tiene:
- **Nombre** — `EventName` en OutboxMessages
- **Módulo** — quién lo emite
- **Descripción** — qué significa para el negocio
- **Tenant scope** — si es tenant-specific o global
- **Payload** — propiedades principales
- **Consumers futuros** — quién reaccionará en Fase 5+

---

## Eventos por módulo

| Módulo | Archivo |
|--------|---------|
| Core (todos los módulos principales) | [core-events.md](./core-events.md) |

---

## Convenciones

- Todos los eventos usan **past tense** + sufijo `Event`
- `EventVersion = 1` por defecto; subir solo en breaking changes
- Nuevos eventos deben agregarse a este catálogo **antes** de mergearse
- Política de versionado: [docs/architecture/events.md § Event Versioning](../../docs/architecture/events.md#event-versioning)

---

## Cómo agregar un evento al catálogo

1. Crear el evento en `ERP.Domain/Modules/{Modulo}/Events/`
2. Extender `BaseDomainEvent` con `TenantId` y `CorrelationId`
3. Agregar entrada en [core-events.md](./core-events.md)
4. Hacer `RaiseDomainEvent(...)` desde el AggregateRoot correspondiente

---

## Cómo funciona el pipeline de entrega

```
AggregateRoot.RaiseDomainEvent(XyzEvent)
    ↓
ErpDbContext.SaveChangesAsync()
    ↓
OutboxMessage { EventName="XyzEvent", EventVersion=1, Payload=JSON }
    ↓
ProcessOutboxJob (Hangfire, cada minuto)
    ↓
[Fase 4] Projection Engine → Analytics / Read Models
[Fase 5] ERP.AI.Application → AI Handlers
[Fase 6] External Bus → Integrations
```

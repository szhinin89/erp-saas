# Event Versioning — ERP SaaS ZH Technologies

Política canónica de versionado para Domain Events y OutboxMessages.
Complementa [EVENT-DRIVEN-RULES.md](./EVENT-DRIVEN-RULES.md) y [ADR-010](../docs/adr/ADR-010-event-versioning.md).

---

## Principio: Additive-First

Antes de cambiar un evento existente, pregunta:
> ¿Puedo agregar un campo opcional y los consumidores actuales siguen funcionando?

Si la respuesta es **sí** → agrega el campo. **No subas la versión.**
Si la respuesta es **no** → crea `EventoNombreV2Event` o sube `EventVersion`.

---

## Cuándo subir EventVersion

| Cambio | Acción | EventVersion |
|--------|--------|-------------|
| Agregar campo opcional (`string? NuevoCampo`) | Solo agregar | **No cambia (sigue en 1)** |
| Agregar campo requerido con default válido | Agregar + default | **No cambia** |
| Renombrar propiedad existente | ❌ Prohibido sin plan de migración | **Sube a N+1** |
| Eliminar propiedad usada por consumers | ❌ Prohibido | **Sube a N+1 + mantener N** |
| Cambiar tipo de propiedad (string → Guid) | Plan explícito | **Sube a N+1** |
| Cambiar semántica de propiedad existente | Plan explícito | **Sube a N+1** |

---

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

---

## Compatibilidad histórica (Outbox / Analytics)

El Outbox es un **log inmutable**. Los mensajes ya procesados NO se modifican.

| Escenario | Política |
|-----------|---------|
| Consumer nuevo que lee mensajes v1 históricos | Debe tolerar campos faltantes (null) |
| Analytics pipeline sobre mensajes v1 y v2 | Normalizar en la capa de proyección, no en el evento |
| Renombrar clase de evento en producción | Migrar el Type en Outbox o mantener alias |

---

## Cómo agregar un campo de forma segura

```csharp
// ✅ CORRECTO — additive-only, EventVersion sin cambio
public sealed record InvoiceCreatedEvent : BaseDomainEvent
{
    public required Guid InvoiceId { get; init; }
    public required Guid SubscriberId { get; init; }
    public required decimal TotalAmount { get; init; }

    // Nuevo campo — nullable, no breaking para consumers existentes
    public string? PaymentMethod { get; init; }
}

// ❌ INCORRECTO — breaking change
public sealed record InvoiceCreatedEvent : BaseDomainEvent
{
    public required Guid DocumentId { get; init; }  // renombrado de InvoiceId → ROMPEDOR
    public required Guid TenantId { get; init; }    // era SubscriberId → ROMPEDOR
}
```

---

## Política de deprecación

1. Marcar evento como `[Obsolete("Use InvoiceCreatedEventV2")]`
2. Mantener durante al menos **2 sprints** (handlers y analytics pueden adaptarse)
3. Verificar que no queden `OutboxMessages` pendientes del evento deprecado
4. Remover solo cuando todos los consumers actualizaron

---

## Prohibiciones absolutas

```
❌ NO renombrar eventos con OutboxMessages pendientes en producción
❌ NO eliminar campos que analytics pipeline ya indexa
❌ NO cambiar el tipo de Id, TenantId, o CorrelationId
❌ NO hacer breaking changes sin ADR de migración
```

---

## Referencia cruzada

| Documento | Tema |
|-----------|------|
| [EVENT-DRIVEN-RULES.md](./EVENT-DRIVEN-RULES.md) | Naming, dispatch, idempotencia |
| [OUTBOX-RETENTION.md](./OUTBOX-RETENTION.md) | Retención e histórico |
| [ADR-010](../docs/adr/ADR-010-event-versioning.md) | Decisión: versioning strategy |

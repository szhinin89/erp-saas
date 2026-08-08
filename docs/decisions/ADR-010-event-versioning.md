# ADR-010: Event Versioning Strategy

## Status

Accepted (2026-05)

## Context

El ERP ya tiene Domain Events con Outbox Pattern (ADR-007/008). A medida que crecen los módulos:
- Los eventos evolucionan: nuevas propiedades, cambios de nombre, nuevos consumidores
- El Outbox es un log histórico inmutable — los mensajes procesados no se borran
- Analytics pipelines y futuros handlers de IA dependerán del schema del evento
- Sin convención explícita, los developers harán breaking changes que rompen el pipeline

## Decision

1. Adoptar **Additive-First Evolution**: el default es agregar campos opcionales sin subir versión.
2. `OutboxMessage` tiene campo `EventVersion` (int, default 1) para tracking explícito de schema.
3. `EventName` almacena el nombre limpio del evento (sin namespace ni assembly) para routing estable.
4. Cuando un cambio es truly breaking: crear `EventoNombreV2Event` como clase separada.
5. El campo `Type` (AssemblyQualifiedName) se usa para deserialización interna; `EventName` para routing/analytics.
6. Documentar todos los eventos en `docs/event-catalog/core-events.md`.

## Consequences

- ✅ Analytics pipelines robustos: el EventName no cambia aunque el assembly se mueva
- ✅ Versioning explícito: fácil detectar mensajes v1 vs v2 en queries de analytics
- ✅ Zero breaking changes por defecto: additive-first protege consumers existentes
- ✅ Event catalog vivo: nuevos devs entienden qué eventos existen y qué significan
- ⚠️ Discipline required: devs deben consultar AI-RULES/EVENT-VERSIONING.md antes de modificar un evento
- ⚠️ Doble clase durante migración: temporalmente existirán EventXEvent y EventXEventV2

## Alternatives Considered

- **Usar solo AssemblyQualifiedName**: rechazado — frágil ante renombrado de clases/namespaces
- **JSON Schema Registry externo**: rechazado — complejidad innecesaria en Fase 3
- **Semantic versioning en nombre** (`InvoiceCreated_v2`): rechazado — inconsistente con naming convention pasado

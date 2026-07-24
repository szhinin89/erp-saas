# ADR-007: Domain Events Foundation

## Status

Accepted (2026-05)

## Context

El ERP SaaS ya implementa Domain Events via `IDomainEvent` + MediatR `IPublisher` en `SaveChangesAsync`. Sin embargo, la infraestructura de eventos no tenía:

1. Una clase base conveniente con soporte de trazabilidad (`CorrelationId`, `TenantId`, `CausationId`)
2. Convenciones documentadas sobre naming, granularidad e idempotencia
3. Reglas de enforcement automatizadas

Sin convenciones estables, el crecimiento del equipo o la adición de módulos IA generaría eventos inconsistentes, difíciles de consumir por sistemas externos.

## Decision

1. Crear `BaseDomainEvent` como clase abstracta opcional que implementa `IDomainEvent` y agrega `CorrelationId`, `TenantId`, `CausationId`.
2. Los eventos existentes siguen funcionando sin cambios (implementan `IDomainEvent` directamente).
3. Los nuevos eventos deben extender `BaseDomainEvent` usando `sealed record` + `required init`.
4. Documentar convenciones en `AI-RULES/EVENT-DRIVEN-RULES.md`.
5. Agregar checks en `tools/architecture/check-domain-events-rules.mjs`.

## Consequences

- ✅ Backward compatible: cero cambios en eventos existentes
- ✅ Nuevos eventos con trazabilidad completa para IA/analytics
- ✅ Naming explícito: past tense + noun + Event (verificable automáticamente)
- ✅ Idempotencia documentada y obligatoria en handlers
- ⚠️ Migración gradual: eventos legacy sin `TenantId` en Outbox — aceptable durante transición

## Alternatives Considered

- **Modificar `IDomainEvent` directamente:** rechazado — rompe todos los eventos existentes
- **Crear nueva interfaz `ITracedDomainEvent`:** posible, pero añade complejidad innecesaria cuando una clase base abstracta es suficiente
- **No hacer nada:** rechazado — sin convenciones, el crecimiento genera drift irrecuperable

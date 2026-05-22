# ADR-011: Outbox Retention Strategy

## Status

Accepted (2026-05) — Implementación diferida a Fase 4

## Context

La tabla `OutboxMessages` crece indefinidamente:
- Cada domain event persiste una fila
- El processor solo marca `ProcessedOnUtc`, no borra
- Sin retención, la tabla puede crecer a millones de filas en producción
- Al mismo tiempo, el histórico tiene valor para analytics y auditoría
- Implementar purge prematuramente agrega complejidad antes de necesitarla

## Decision

**Fase 3 (actual):** No implementar retención todavía. Aceptar crecimiento de tabla.

**Fase 4 (futuro):** Implementar `IOutboxRetentionJob` como job Hangfire separado con:
- Política configurable vía `appsettings.json` (`OutboxRetention:Enabled`, `RetentionDays`)
- Purga solo mensajes procesados (`ProcessedOnUtc IS NOT NULL`) fuera de la ventana
- Retención indefinida de mensajes fallidos (`Error IS NOT NULL AND ProcessedOnUtc IS NULL`)
- Archivo opcional a tabla `OutboxMessagesArchive` antes de borrar (para compliance)
- Responsabilidad separada de `OutboxProcessor` — no mezclar procesamiento con limpieza

## Trigger para implementar (señales de alerta)

- Tabla `OutboxMessages` supera ~500k filas
- Query de pending messages tarda > 50ms
- PostgreSQL reporta bloat en la tabla
- Espacio en disco presionado

## Consequences

- ✅ Simplicidad en Fase 3: zero complejidad adicional ahora
- ✅ Separación de responsabilidades: processor ≠ retention job
- ✅ Datos históricos disponibles para analytics pipeline de Fase 4
- ✅ Configurable: activar en producción, desactivar en dev
- ⚠️ Tabla grande si el trigger no se activa a tiempo — monitorear con query de salud
- ⚠️ Compliance: verificar si Payload contiene PII antes de purgar

## Alternatives Considered

- **Borrar inmediatamente tras procesar**: rechazado — pierde histórico necesario para analytics
- **TTL automático en PostgreSQL**: rechazado — requiere pg_partman o configuración extra
- **Purge inline en OutboxProcessor**: rechazado — mezcla responsabilidades, dificulta testing

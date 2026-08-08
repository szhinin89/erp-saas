# Outbox Retention Strategy — ERP SaaS ZH Technologies

Política canónica de retención para la tabla `OutboxMessages`.
Ver [ADR-011](../docs/adr/ADR-011-outbox-retention-strategy.md) para la decisión.

---

## Estado actual (Fase 3)

La tabla `OutboxMessages` crece indefinidamente.
El `OutboxProcessor` solo marca mensajes como `ProcessedOnUtc` — no borra ni archiva.

**Esto es correcto para Fase 3.** Implementar retención antes de necesitarla es complejidad prematura.

---

## Política de retención (aplicar en Fase 4)

| Ventana | Acción |
|---------|--------|
| `ProcessedOnUtc < UtcNow - 30 días` | Candidato a purga o archivo |
| `ProcessedOnUtc IS NULL AND OccurredOnUtc < UtcNow - 7 días` | Alerta: mensaje huérfano |
| Mensajes con `Error IS NOT NULL` (sin procesar) | Retener indefinidamente hasta resolución manual |

---

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

---

## Archivo vs. Borrado

Antes de borrar, considerar si el histórico tiene valor para:

| Uso | Retención recomendada |
|-----|-----------------------|
| Debugging / soporte | 30 días post-procesado |
| Analytics / BI | Mover a tabla separada `OutboxMessagesArchive` |
| Compliance / auditoría | Según regulación local (LOPD/SRI) |
| AI training data | Mover a Data Warehouse antes de purgar |

**Regla:** No purgar sin confirmar que el Analytics pipeline ya proyectó los mensajes.

---

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

---

## Cumplimiento (Compliance)

| Regulación | Consideración |
|------------|--------------|
| LOPD Ecuador | Datos personales en Payload: definir ventana de retención |
| SRI | Documentos electrónicos: retener referencias por 7 años |
| GDPR (futuro) | Derecho al olvido: el Payload puede contener PII |

**Acción futura:** para datos PII en eventos, usar un campo de referencia (ID) en lugar de datos directos en el Payload.

---

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
| [EVENT-VERSIONING.md](./EVENT-VERSIONING.md) | Compatibilidad histórica |
| [ANALYTICS-FOUNDATION.md](./ANALYTICS-FOUNDATION.md) | Proyecciones antes de purga |
| [ADR-011](../docs/adr/ADR-011-outbox-retention-strategy.md) | Decisión de retención |

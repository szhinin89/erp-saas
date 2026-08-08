# ADR-012: AI Read Model Strategy

## Status

Accepted (2026-05) — Implementación diferida a Fase 4/5

## Context

La IA futura (ERP.AI.Application) necesita datos del ERP para:
- Predicciones: scoring de clientes, predicción de demanda, detección de anomalías
- Analytics: tendencias, KPIs, análisis de rentabilidad
- RAG: contexto de documentos ERP para Chat AI

Si la IA consulta directamente las tablas transaccionales (ErpDbContext):
- Degrada el performance del ERP OLTP con queries OLAP complejas
- Acopla la IA al schema transaccional — cualquier cambio de tabla rompe la IA
- Viola Clean Architecture: ERP.AI.Infrastructure no debe referenciar dominio ERP directamente
- Dificulta el testing de la capa IA

## Decision

1. La IA **nunca** consulta `ErpDbContext` directamente.
2. La IA consume datos por **dos canales**:
   a. **Domain Events** (via Outbox) — para reaccionar a lo que ocurrió
   b. **Read Models / Projections** — para consultar estado actual o histórico
3. Los Read Models viven en `ERP.Infrastructure/ReadModels/` — son tablas/vistas desnormalizadas.
4. Los Read Models se actualizan vía Domain Events (event-driven projection) o batch periodical.
5. El Data Warehouse (Fase 4) es un Read Model extendido: schema `analytics` en PostgreSQL.
6. `ERP.AI.Infrastructure` implementa puertos (`ICustomerRevenueReadModel`) que abstraen el origen.

## Arquitectura resultante

```
ERP Core (OLTP, ErpDbContext)
    ↓ Domain Events via Outbox
Projection Engine (Fase 4)
    ↓
Read Models (PostgreSQL)
    ↓
ERP.AI.Infrastructure (implementa puertos)
    ↓
ERP.AI.Application (casos de uso IA)
```

## Consequences

- ✅ ERP core no degradado por queries OLAP de IA
- ✅ Schema estable para IA: Read Models cambian independientemente del schema transaccional
- ✅ Testing aislado: la IA puede testearse con Read Models mockeados
- ✅ Escalable: Read Models pueden moverse a infra dedicada sin tocar el core
- ⚠️ Latencia eventual: los Read Models se actualizan via eventos (segundos de lag)
- ⚠️ Trabajo extra: cada caso de uso IA requiere definir el Read Model correspondiente
- ⚠️ Fase 4 requerida: hasta implementar proyecciones, la IA no puede consumir datos históricos

## Alternatives Considered

- **IA consulta DbContext directamente**: rechazado — OLTP/OLAP conflict, acoplamiento fuerte
- **DbContext separado solo para lectura**: rechazado — aún comparte el schema transaccional
- **CQRS read side completo (Event Sourcing)**: rechazado — complejidad excesiva para MVP
- **Data Warehouse externo desde el inicio**: rechazado — infraestructura prematura

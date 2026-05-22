# Analytics Foundation — ERP SaaS ZH Technologies

Principios y arquitectura futura para analytics, BI, y capa de lectura de IA.
Ver [ADR-012](../docs/adr/ADR-012-ai-read-model-strategy.md) para la decisión.

---

## Principio fundamental

```
La IA y los analytics NO consultan tablas transaccionales del ERP.
Usan read models, proyecciones, o un Data Warehouse separado.
```

El ERP core (Domain + Application + DbContext) es optimizado para **escritura transaccional** (OLTP).
Analytics y AI son **lectura intensiva** (OLAP). Mezclarlos degrada ambos.

---

## Arquitectura objetivo (Fase 4+)

```
ERP Core (OLTP)
    ↓ Domain Events
    ↓ OutboxMessages (durable log)
    ↓
OutboxProcessor (Fase 4: enhanced)
    ↓
Projection Engine
    ├── Read Models (PostgreSQL views/tables optimizadas)
    ├── Analytics Sink (PostgreSQL analytics schema o warehouse externo)
    └── AI Data Layer (ERP.AI.Infrastructure)
```

---

## Read Models / Proyecciones

Un **read model** es una tabla o vista desnormalizada optimizada para una consulta específica.

### Cuándo crear un read model

```
✅ CREAR cuando:
- Un query requiere JOIN de 4+ tablas
- Un query es ejecutado por analytics o IA con alta frecuencia
- Los datos necesarios cambian raramente vs. se leen frecuentemente
- La query directa a tablas transaccionales tarda > 200ms

❌ NO CREAR cuando:
- El dato es consultado 1 vez por usuario (pantalla ERP normal)
- El dato cambia con cada transacción (mejor Materializar con refresh)
```

### Estructura recomendada

```
ERP.Infrastructure/
└── ReadModels/
    ├── Sales/
    │   ├── DailySalesSummaryProjection.cs
    │   └── CustomerRevenueSummaryProjection.cs
    ├── Inventory/
    │   └── StockLevelSummaryProjection.cs
    └── Accounting/
        └── TrialBalanceSummaryProjection.cs
```

---

## Data Warehouse (Fase 4 — Futuro)

El Data Warehouse recibe proyecciones del Outbox y permite queries analíticas sin impactar el ERP OLTP.

**NO implementar todavía.** Cuando sea necesario:

| Opción | Trade-off |
|--------|-----------|
| PostgreSQL analytics schema separado | Simple, mismo servidor, sin infraestructura extra |
| TimescaleDB (extensión PostgreSQL) | Time-series optimizado, mínima complejidad |
| Clickhouse | Máximo rendimiento OLAP, requiere infraestructura |
| BigQuery / Snowflake | Cloud-managed, mayor costo, escala masiva |

**Recomendación para MVP:** PostgreSQL analytics schema separado (mismo cluster, schema `analytics`).

---

## Qué datos proyectar (candidatos Fase 4)

| Proyección | Fuente (eventos) | Frecuencia refresh |
|------------|-----------------|-------------------|
| Ventas diarias por empresa | `SalesBillPostedEvent` | Diaria |
| Stock actual por bodega | `StockMovementRegisteredEvent` | Por evento |
| Cuentas por cobrar aging | `InvoiceCreatedEvent` + `PaymentReceivedEvent` | Diaria |
| Compras por proveedor | `PurchaseOrderApprovedEvent` | Semanal |
| Actividad por tenant (SaaS) | `*Event` donde TenantId | Horaria |

---

## AI Read Layer

La IA futura (ERP.AI.Infrastructure) consumirá datos via:

1. **Outbox Events** — reaccionar a lo que ocurrió (event-driven AI)
2. **Read Models** — consultar estado actual sin tocar tablas transaccionales
3. **Analytics Projections** — datos históricos para entrenamiento y predicciones

```
// ✅ CORRECTO — IA lee read model
class PredictPaymentDelayHandler
{
    ICustomerRevenueReadModel _readModel; // No ErpDbContext
}

// ❌ INCORRECTO — IA accede a DbContext transaccional
class PredictPaymentDelayHandler
{
    ErpDbContext _db; // Viola separación de capas
}
```

---

## Reporting Strategy

| Tipo de reporte | Implementación |
|----------------|---------------|
| Reporte operativo en pantalla | Query directa a DbContext (EF, CQRS query) |
| Reporte analítico (histórico, cross-tenant) | Read model o proyección |
| Dashboard ejecutivo | Analytics projection o materialized view |
| Exportación Excel/PDF | CQRS query + generador (QuestPDF existente) |
| AI insights | ERP.AI.Application consumiendo read models |

---

## Prohibiciones

```
❌ Analytics queries en controllers ERP
❌ IA consultando tablas transaccionales directamente
❌ Read models mezclados con entidades de dominio
❌ Implementar Data Warehouse antes de necesitarlo
❌ Proyecciones que bloquean transacciones de negocio
```

---

## Referencia cruzada

| Documento | Tema |
|-----------|------|
| [AI-FOUNDATION.md](./AI-FOUNDATION.md) | Separación de capas IA |
| [OUTBOX-RETENTION.md](./OUTBOX-RETENTION.md) | Retención antes de analytics purge |
| [EVENT-DRIVEN-RULES.md](./EVENT-DRIVEN-RULES.md) | Eventos como fuente de proyecciones |
| [ADR-012](../docs/adr/ADR-012-ai-read-model-strategy.md) | Decisión: read model strategy |

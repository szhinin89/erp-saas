# AI Foundation — ERP SaaS ZH Technologies

Principios arquitectónicos para integrar IA futura SIN acoplarla al core del ERP.

> **IMPORTANTE:** Este documento describe la arquitectura OBJETIVO, no el estado actual.
> Ningún módulo IA está implementado todavía. Este archivo es el contrato de diseño.

---

## Principio fundamental

```
IA no vive dentro del Domain.
IA no vive dentro de Controllers.
IA no se mezcla con entidades de negocio.
```

La IA futura es un **consumidor externo** del ERP, no un componente interno.

---

## Arquitectura objetivo

```
ERP Core (Domain + Application + Infrastructure)
    ↓  Domain Events
    ↓  Outbox Table
    ↓  Background Processing
    ↓
ERP.AI.Application        ← casos de uso de IA
ERP.AI.Infrastructure     ← LLMs, vector DBs, embeddings, external APIs
    ↓
AI Features               ← predicciones, automatizaciones, análisis
```

### Separación de capas

| Capa | Responsabilidad | Prohibido |
|------|----------------|-----------|
| `ERP.Domain` | Entidades, eventos, reglas de negocio | Llamadas LLM, embeddings, HTTP AI |
| `ERP.Application` | Casos de uso ERP | Llamar OpenAI directamente |
| `ERP.Infrastructure` | Persistencia, servicios técnicos | Lógica de IA inline |
| `ERP.API` | HTTP endpoints ERP | Endpoints IA mezclados con ERP |
| `ERP.AI.Application` | Casos de uso de IA (futuro) | Acceder directamente a DbContext ERP |
| `ERP.AI.Infrastructure` | LLMs, vector DB, embeddings (futuro) | Reglas de negocio ERP |

---

## Cómo la IA consumirá el ERP

### Vía Domain Events (canal principal)

```
ERP Domain Event
    → Outbox (persiste atómicamente)
    → OutboxProcessor (procesa background)
    → [Futuro] ERP.AI.Infrastructure recibe evento
    → ERP.AI.Application ejecuta caso de uso IA
```

Ejemplos de flujos futuros:

```
InvoiceCreatedEvent        → AI predice impago → alerta al usuario
StockBelowThresholdEvent   → AI recomienda orden de compra
PaymentReceivedEvent       → AI actualiza scoring de cliente
SalesBillPostedEvent       → AI analiza rentabilidad por producto
```

### Vía Read Models / Proyecciones

La IA puede leer datos históricos del ERP via:
- Read models específicos (query-side, sin tocar transacciones)
- Analytics tables (proyecciones denormalizadas)
- Snapshots periódicos para entrenamiento

**Prohibido:** La IA NO escribe directamente en tablas de negocio del ERP.
Si la IA necesita crear algo, lo hace via Command en `ERP.AI.Application`
que llama al `ERP.Application` command correspondiente.

---

## Estructura de módulos AI (placeholder — futuro)

```
backend/src/
├── ERP.AI.Application/
│   ├── UseCases/
│   │   ├── PredictPaymentDelay/
│   │   ├── RecommendPurchaseOrder/
│   │   └── AnalyzeProfitability/
│   ├── Ports/
│   │   ├── ILanguageModelPort.cs
│   │   └── IVectorStorePort.cs
│   └── Events/
│       └── Handlers/         ← escuchan Domain Events del ERP
│
└── ERP.AI.Infrastructure/
    ├── LLM/
    │   ├── OpenAIAdapter.cs
    │   └── AnthropicAdapter.cs
    ├── VectorStore/
    │   └── PineconeAdapter.cs
    └── Analytics/
        └── AnalyticsProjectionService.cs
```

### Reglas de referencia

```
ERP.AI.Application puede referenciar:
    ✅ ERP.Domain (para leer tipos de eventos)
    ✅ ERP.Application (para enviar commands vía IMediator)
    ❌ ERP.Infrastructure directamente
    ❌ ERP.API

ERP.AI.Infrastructure puede referenciar:
    ✅ ERP.AI.Application (para implementar puertos)
    ✅ ERP.Infrastructure (para leer datos via proyecciones)
    ❌ ERP.Domain directamente (solo via contratos)
    ❌ ERP.Application (para negocio — solo vía commands)
```

---

## Cuándo NO meter IA en el ERP Core

```
❌ if (useAI) { var prediction = await _openAI.Predict(...); }
    // Esto acopla el core a IA y viola separación de capas

❌ class SalesBill { public float RiskScore { get; set; } }
    // Las entidades no llevan scores de IA

❌ // En un handler Application
   var embedding = await _vectorDb.Embed(invoice.Description);
    // Application no llama a infraestructura IA

✅ // La IA reacciona al evento, no vive en el evento
   // SalesNoteAuthorizedEvent → handler en ERP.AI.Application
   //                         → llama LLM → genera recomendación
```

---

## Correlation y Trazabilidad

Para preparar el ERP para IA:

1. **CorrelationId** en `BaseDomainEvent` — propaga el ID de la request HTTP al evento
2. **TenantId** en `BaseDomainEvent` — mantiene contexto multi-tenant en la IA
3. **CausationId** en `BaseDomainEvent` — cadena de causalidad entre eventos

Esto permite a la IA:
- Rastrear qué acción del usuario originó el evento
- Mantener contexto tenant en pipelines de IA
- Construir grafos de causalidad para analytics avanzados

---

## Prohibiciones absolutas (enforcement)

| Prohibición | Por qué |
|-------------|---------|
| `ERP.Domain` NO referencia ningún paquete IA | El dominio es puro |
| `ERP.Application` NO llama OpenAI/Anthropic directamente | Viola capas |
| LLM calls NO van en handlers de MediatR del ERP | Latencia + acoplamiento |
| IA NO modifica DbContext del ERP directamente | Integridad transaccional |
| IA NO bypasea Domain Events para leer estado | Acoplamiento temporal |

Estos checks ya están automatizados en [`tools/architecture/check-ai-layer-boundaries.mjs`](../../tools/architecture/check-ai-layer-boundaries.mjs) (reglas AI-001 a AI-005) y corren en CI — ver [enforcement.md](./enforcement.md).

---

## Stack IA permitido (futuro — cuando se implemente)

Solo herramientas aprobadas en [`stack.md`](./stack.md) (añadir cuando se seleccionen):

- LLM: Claude API (Anthropic) — modelo preferido
- Embeddings: decidir en ADR
- Vector DB: decidir en ADR
- Orquestación: decidir en ADR (no LangChain por defecto)

**NO implementar hasta ADR aprobado.**

---

# Analytics Foundation

Principios y arquitectura futura para analytics, BI, y capa de lectura de IA. Ver [ADR-012](../decisions/ADR-012-ai-read-model-strategy.md) para la decisión.

## Principio fundamental

```
La IA y los analytics NO consultan tablas transaccionales del ERP.
Usan read models, proyecciones, o un Data Warehouse separado.
```

El ERP core (Domain + Application + DbContext) es optimizado para **escritura transaccional** (OLTP). Analytics y AI son **lectura intensiva** (OLAP). Mezclarlos degrada ambos.

## Arquitectura objetivo (Fase 4+ — no implementado; fase actual del proyecto: ver `STATUS.md`)

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

## Qué datos proyectar (candidatos Fase 4)

| Proyección | Fuente (eventos) | Frecuencia refresh |
|------------|-----------------|-------------------|
| Ventas diarias por empresa | `SalesBillPostedEvent` | Diaria |
| Stock actual por bodega | `StockMovementRegisteredEvent` | Por evento |
| Cuentas por cobrar aging | `InvoiceCreatedEvent` + `PaymentReceivedEvent` | Diaria |
| Compras por proveedor | `PurchaseOrderApprovedEvent` | Semanal |
| Actividad por tenant (SaaS) | `*Event` donde TenantId | Horaria |

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

## Reporting Strategy

| Tipo de reporte | Implementación |
|----------------|---------------|
| Reporte operativo en pantalla | Query directa a DbContext (EF, CQRS query) |
| Reporte analítico (histórico, cross-tenant) | Read model o proyección |
| Dashboard ejecutivo | Analytics projection o materialized view |
| Exportación Excel/PDF | CQRS query + generador (QuestPDF existente) |
| AI insights | ERP.AI.Application consumiendo read models |

## Prohibiciones (analytics)

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
| [events.md](./events.md) | Reglas de domain events y outbox |
| [backend.md](./backend.md) | Reglas generales backend |
| [ADR-009](../decisions/ADR-009-ai-layer-separation.md) | Decisión: separación capa IA |
| [ADR-012](../decisions/ADR-012-ai-read-model-strategy.md) | Decisión: read model strategy |
| [ERP.AI.Application README](../../backend/src/ERP.AI.Application/README.md) | Placeholder futuro |
| [ERP.AI.Infrastructure README](../../backend/src/ERP.AI.Infrastructure/README.md) | Placeholder futuro |

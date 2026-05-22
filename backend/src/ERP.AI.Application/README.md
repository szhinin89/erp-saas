# ERP.AI.Application — Placeholder

> **Estado: NO IMPLEMENTADO.** Este directorio es un placeholder que documenta
> la posición futura de los casos de uso de IA en la arquitectura.

---

## Propósito

Contiene los **casos de uso de IA** del ERP SaaS:
- Predicción de impagos
- Recomendaciones de órdenes de compra
- Análisis de rentabilidad
- Detección de anomalías en gastos
- Asistente de consultas ERP

---

## Límites arquitectónicos

### Este proyecto PUEDE referenciar

- `ERP.Domain` — para leer tipos de Domain Events y entidades
- `ERP.Application` (via IMediator) — para ejecutar commands ERP

### Este proyecto NO PUEDE

- Acceder a `ErpDbContext` directamente
- Referenciar `ERP.Infrastructure` para negocio
- Mezclar lógica LLM con lógica de negocio ERP

---

## Estructura futura esperada

```
ERP.AI.Application/
├── UseCases/
│   ├── PredictPaymentDelay/
│   │   ├── PredictPaymentDelayQuery.cs
│   │   └── PredictPaymentDelayHandler.cs
│   └── RecommendPurchaseOrder/
│       ├── RecommendPurchaseOrderQuery.cs
│       └── RecommendPurchaseOrderHandler.cs
├── Ports/
│   ├── ILanguageModelPort.cs     ← abstracción del LLM
│   └── IVectorStorePort.cs       ← abstracción del vector DB
└── Events/
    └── Handlers/
        └── InvoiceCreatedEventHandler.cs  ← reacciona a domain events
```

---

## Cuándo implementar

Antes de añadir código aquí, crear un ADR que documente:
1. El caso de uso específico
2. El proveedor LLM seleccionado
3. El stack de vector DB (si aplica)
4. La política de privacidad de datos del tenant

Ver: [AI-RULES/AI-FOUNDATION.md](../../AI-RULES/AI-FOUNDATION.md)

# ERP.AI.Infrastructure — Placeholder

> **Estado: NO IMPLEMENTADO.** Este directorio es un placeholder que documenta
> la posición futura de la infraestructura de IA en la arquitectura.

---

## Propósito

Implementa los **puertos de IA** definidos en `ERP.AI.Application`:
- Adaptadores LLM (OpenAI, Anthropic Claude, local models)
- Adaptadores de Vector DB (Pinecone, Qdrant, pgvector)
- Servicios de embeddings
- Proyecciones de analytics (leer datos ERP para entrenamiento)

---

## Límites arquitectónicos

### Este proyecto PUEDE referenciar

- `ERP.AI.Application` — implementar puertos
- `ERP.Infrastructure` — leer proyecciones/read models (no transacciones)

### Este proyecto NO PUEDE

- Contener lógica de negocio ERP
- Escribir directamente en tablas transaccionales del ERP
- Bypass de Domain Events para leer estado actual

---

## Estructura futura esperada

```
ERP.AI.Infrastructure/
├── LLM/
│   ├── AnthropicAdapter.cs       ← implementa ILanguageModelPort
│   └── OpenAIAdapter.cs
├── VectorStore/
│   └── PineconeAdapter.cs        ← implementa IVectorStorePort
├── Analytics/
│   └── OutboxEventProjection.cs  ← lee OutboxMessages para análisis
└── DependencyInjection.cs
```

---

## Stack preferido (a confirmar via ADR)

- **LLM principal:** Claude API (Anthropic) — ver `AI-RULES/STACK.md` cuando se defina
- **Embeddings:** por definir
- **Vector DB:** por definir (candidatos: pgvector in PostgreSQL, Qdrant)

---

## Cuándo implementar

No implementar hasta que `ERP.AI.Application` tenga al menos un caso de uso concreto
aprobado via ADR.

Ver: [AI-RULES/AI-FOUNDATION.md](../../AI-RULES/AI-FOUNDATION.md)

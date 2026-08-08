# ADR-009: AI Layer Separation

## Status

Accepted (2026-05)

## Context

El ERP necesita estar preparado para integrar capacidades de IA (predicciones, automatizaciones, análisis) sin:
1. Acoplar el dominio de negocio a herramientas de IA específicas
2. Introducir latencia de LLM en el camino crítico de transacciones
3. Violar la arquitectura limpia (Clean Architecture)
4. Crear dependencias que imposibiliten cambiar de proveedor IA

Sin una decisión explícita, los desarrolladores podrían meter llamadas OpenAI directamente en handlers de Application, generando un acoplamiento difícil de revertir.

## Decision

1. La IA futura vivirá en proyectos separados: `ERP.AI.Application` y `ERP.AI.Infrastructure`.
2. `ERP.Domain` y `ERP.Application` **nunca** referenciarán paquetes de IA.
3. La IA consumirá el ERP **solo** via:
   - Domain Events / Outbox (canal asíncrono)
   - Read models / proyecciones (datos históricos)
   - Commands de `ERP.Application` (si necesita actuar sobre el sistema)
4. Los módulos IA no accederán al `ErpDbContext` directamente.
5. Crear proyectos placeholder con README que documente límites y responsabilidades.
6. Automatizar checks en `tools/architecture/check-ai-layer-boundaries.mjs`.

## Consequences

- ✅ ERP core no depende de ningún proveedor IA — portable y mantenible
- ✅ La IA puede evolucionar independientemente del ERP core
- ✅ Fácil cambio de proveedor (OpenAI → Anthropic → local) sin tocar el dominio
- ✅ Latencia IA no afecta transacciones ERP (asíncrono via eventos)
- ✅ Multi-tenant preservado: el `TenantId` en `BaseDomainEvent` propaga contexto
- ⚠️ Requiere disciplina: enforcement automático (check CI) para prevenir regresiones
- ⚠️ Curva de aprendizaje: los desarrolladores deben entender el patrón antes de implementar IA

## Cuándo implementar ERP.AI.*

Estos proyectos son placeholders hasta que exista un requerimiento concreto de IA con:
- ADR aprobado que defina el proveedor y caso de uso
- Stack IA seleccionado y documentado en `AI-RULES/STACK.md`
- Definition of Done que incluya tests de integración

## Alternatives Considered

- **IA inline en handlers:** rechazado — acoplamiento fuerte, viola Clean Architecture
- **Microservicio IA desde el inicio:** rechazado — complejidad distribuida prematura
- **IA en Infrastructure layer del ERP:** rechazado — mezcla responsabilidades técnicas con lógica IA
- **Sin separación explícita:** rechazado — sin boundaries claros, el acoplamiento ocurre por inercia

# ADR-0004: Atomic subscription usage increment

**Estado:** Aceptado (iteración 04)  
**Fecha:** 2026-05-20

---

## Contexto

`SubscriptionGateBehavior` ejecuta el handler MediatR (que suele persistir vía `IUnitOfWork.SaveChangesAsync`) y después llama a `SubscriptionService.IncrementUsageAsync`, que hacía read-modify-write en EF y un **`SaveChangesAsync` propio** en el mismo `DbContext`.

Problemas:

- Doble flush de unidad de trabajo en la misma petición.
- Condición de carrera: dos requests concurrentes pueden leer el mismo `quantity` y sobrescribir incrementos.
- El servicio de dominio/aplicación no debe cerrar transacciones; eso corresponde al pipeline o al handler.

---

## Decisión

1. **`SubscriptionUsageIncrementer`** (infra): en PostgreSQL, un único `INSERT … ON CONFLICT (tenant_id, feature_id, period_key) DO UPDATE SET quantity = quantity + @amount` vía `ExecuteSqlAsync` (mismo patrón que `StockRepository`).
2. **`SubscriptionService.IncrementUsageAsync`** ya no llama a `SaveChangesAsync`.
3. Retorno `bool`: `false` = persistido por SQL; `true` = staged en change tracker (solo proveedor InMemory en tests).
4. **`SubscriptionGateBehavior`**: si retorna `true`, invoca `IUnitOfWork.SaveChangesAsync` una vez (tests / InMemory).

`CheckLimitAsync` sigue siendo lectura previa al handler; la corrección de carrera en el check es fuera de alcance de esta iteración.

---

## Consecuencias

### Positivas

- Incremento de uso atómico en producción (PostgreSQL).
- Un solo `SaveChanges` de negocio en el handler; el behavior solo flushea si InMemory.

### Negativas

- Tests de integración con InMemory no validan el SQL UPSERT (se cubre el fallback EF).
- Validación de límite y consumo no están en la misma sentencia SQL (posible overshoot bajo alta concurrencia).

---

## Rollback

Revertir rama `refactor/saas-enterprise-04-usage-upsert`; restaurar read-modify-write + `SaveChangesAsync` en `SubscriptionService`.

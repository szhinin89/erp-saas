# ADR-020: Infraestructura de seguimiento de entidades (EF Core Change Tracking)

**Estado:** ✅ FROZEN — Infraestructura cerrada definitivamente
**Fecha aprobación:** 2026-06-30
**Autor:** Sebastian Zhinin
**Contexto:** ERP SaaS multiempresa — persistencia de agregados con hijos nuevos descubiertos por fixup de navegación bajo EF Core 10 / PostgreSQL

> **FROZEN:** El diagnóstico, el diseño y la API pública de esta infraestructura están
> cerrados. No se aceptan cambios estructurales sin una nueva ADR aprobada. La única
> infraestructura autorizada para corregir una clasificación ambigua del
> `ChangeTracker` es `NewChildEntityTrackingInterceptor`.

---

## Contexto

Varios flujos del ERP modifican un agregado raíz ya trackeado por el `DbContext`
agregándole entidades hijas nuevas **dentro de un domain event handler**, es decir,
entre el primer y el segundo `SaveChangesAsync()` de la misma unidad de trabajo. Dos
ejemplos reales detectados en producción:

- **Caja**: al autorizar una factura de venta con caja abierta,
  `SalesInvoiceAuthorizedHandler` llama a `CashSession.RecordMovement(...)`, que
  agrega un `CashMovement` nuevo a la colección de navegación `Movements` de una
  `CashSession` que ya fue materializada por una query previa en el mismo
  `DbContext`.
- **Ventas**: el patrón "Replace" de `SalesInvoicePayment` reemplaza la colección de
  pagos de una factura ya trackeada, agregando instancias nuevas con clave generada
  por dominio (`Guid.NewGuid()` en el factory `Create()`).

En ambos casos, la entidad hija nunca pasó por una query: nace ya con un `Guid`
"real" (no temporal para EF) y se descubre recién cuando EF Core hace
`DetectChanges()` sobre la colección de navegación del padre. Como el padre ya está
trackeado y no es `Added`, y la clave de la hija no es temporal, el `ChangeTracker`
no tiene forma de inferir que la entidad es nueva.

---

## Problema

EF Core clasifica la entidad hija nueva como `Modified` en vez de `Added`, marcando
**todas** sus propiedades con `IsModified = true` pero con `OriginalValue ==
CurrentValue` para cada una (no existe snapshot real de base de datos, porque la
fila nunca existió). El `UPDATE` resultante es un no-op: afecta **0 filas**, lo cual
PostgreSQL reporta como conflicto de concurrencia optimista →
`DbUpdateConcurrencyException`.

Consecuencias observadas:

- **HTTP 500** al cliente en el flujo de autorización de factura con caja abierta
  (`POST /api/v1/sales/{id}/authorize`), pese a que la factura sí quedaba autorizada
  en BD — estado parcial inconsistente.
- El mismo patrón es **estructural**, no específico de Caja: cualquier módulo del
  ERP que agregue un hijo nuevo a un agregado ya trackeado desde un domain event
  handler (o desde cualquier código que opere entre dos `SaveChangesAsync` sobre el
  mismo `DbContext`) está expuesto al mismo bug.
- El error no es determinístico a primera vista: el flujo funciona si el agregado
  nunca fue cargado por una query previa en el mismo `DbContext` (porque entonces
  los hijos sí quedan `Added` correctamente vía fixup normal), pero falla en el
  patrón "cargar → mutar dentro de un handler → segundo SaveChanges", que es
  exactamente el patrón que usan los domain event handlers del ERP.

---

## Alternativas evaluadas

| Alternativa | Razón de descarte |
|---|---|
| **`ChangeTracker.TrackGraph()` manual** | Requiere que cada handler conozca y reimplemente la lógica de clasificación; traslada la responsabilidad de un problema transversal de infraestructura a cada módulo de negocio. Alto riesgo de inconsistencia entre módulos. |
| **`Attach()`/`Add()` manual explícito en cada handler** | El handler tendría que saber de antemano qué hijos son nuevos, lo cual es exactamente la información que EF Core no logra inferir automáticamente; duplica lógica de tracking fuera del ORM. |
| **Heurísticas dentro de `SaveChangesAsync` de `ErpDbContext`** | Mezcla responsabilidad de persistencia genérica con una corrección puntual de un caso de borde de EF Core; dificulta testear la corrección de forma aislada y la acopla al método más sensible del `DbContext`. |
| **Cambiar la estrategia de generación de claves (claves temporales / store-generated)** | Rompe el patrón establecido de factories `Create()` que generan `Guid` en dominio (requisito arquitectónico transversal del ERP — entidades válidas e identificables antes de persistir). Cambiarlo afecta a todos los agregados del sistema, no solo a los dos casos detectados. |
| **Detectar y corregir por convención de nombres o tipo de entidad** | Frágil: cualquier entidad nueva con el mismo patrón quedaría desprotegida hasta que alguien la agregue a una lista hardcodeada. No escala con el crecimiento del ERP. |
| **`ISaveChangesInterceptor` dedicado (elegida)** | Seguiel patrón ya establecido en el proyecto para lógica transversal de persistencia (`CompanyTenantInterceptor`, `DbCommandTenantInterceptor`, `PostgreSqlSessionContextInterceptor`). Se ejecuta automáticamente para todo agregado de todo módulo sin requerir cambios en los handlers existentes ni en los futuros. Testeable de forma aislada con PostgreSQL real. |

---

## Decisión

Se implementa `NewChildEntityTrackingInterceptor` (`SaveChangesInterceptor`),
registrado junto a los demás interceptores transversales en
`DependencyInjection.AddInfrastructure()`.

### Señal de origen: `ErpDbContext.WasTrackedFromQuery`

`ErpDbContext` se suscribe a `ChangeTracker.Tracked` en su constructor y mantiene un
`HashSet` (`_queryTrackedEntities`) de las entidades cuyo evento `Tracked` llegó con
`FromQuery == true`. El método interno `WasTrackedFromQuery(object entity)` expone
esa señal por instancia de `DbContext` (su ciclo de vida es scoped, igual que el
propio `DbContext`).

### Regla de decisión del interceptor

En `SavingChanges`/`SavingChangesAsync`, tras `ChangeTracker.DetectChanges()`, para
cada entrada con `State == Modified`:

1. Si **ninguna** propiedad marcada `IsModified` tiene una diferencia real
   (`OriginalValue == CurrentValue` para todas) **y** la entidad **nunca** fue
   materializada por una query en este `DbContext`
   (`!WasTrackedFromQuery(entity)`) → es la firma inequívoca de una entidad nunca
   persistida. Se corrige el estado a `Added`.
2. Si se cumple la condición (1) pero la entidad **sí** vino de una query — combinación
   anómala bajo operación normal de EF Core, que normalmente dejaría la entidad
   `Unchanged` — el interceptor **no adivina**: lanza `InvalidOperationException`
   con un mensaje explícito señalando la entidad y exigiendo revisión manual del
   flujo que produjo ese estado.

### Filosofía fail-fast — por qué no se intenta autocorregir el caso anómalo

Mutar el estado de una entidad que sí tiene snapshot real de base de datos sin
evidencia inequívoca de que es nueva podría enmascarar un defecto distinto o, en el
peor caso, producir un `INSERT` duplicado sobre una fila que ya existe. El diseño
prioriza fallar de forma ruidosa e inmediata sobre "corregir" silenciosamente un
estado ambiguo.

### Invariante arquitectónico del que depende la señal `WasTrackedFromQuery`

La fiabilidad de `WasTrackedFromQuery` depende de que ningún código de producción
inyecte una entidad directamente en el `ChangeTracker` como `Modified` —vía
`DbSet<T>.Attach()`/`DbSet<T>.Update()`— sin que haya pasado antes por una query
trackeada en el mismo `DbContext`. Si esa regla se rompe, una entidad legítimamente
existente pero reatachada "a ciegas" sería indistinguible de una entidad nueva
descubierta por fixup de navegación, y el interceptor podría corregirla
incorrectamente a `Added` (riesgo de `INSERT` duplicado).

Por esta razón, la decisión arquitectónica incluye una **regla permanente de
reatachamiento de agregados** (ver sección "Regla arquitectónica permanente" más
abajo), garantizada en CI por `ATT-GATE-01`.

---

## Evidencia

### Suite de tests (PostgreSQL 16-alpine real, vía Testcontainers)

`NewChildEntityTrackingInterceptorTests` — 6 escenarios de integración, todos
verdes:

| Escenario | Verifica |
|---|---|
| `Original_bug_scenario_RecordMovement_on_query_loaded_session_does_not_throw` | El bug original (Caja) no vuelve a ocurrir |
| `Multiple_new_children_in_one_SaveChanges_are_all_persisted` | Varios hijos nuevos en un mismo `SaveChanges` se clasifican y persisten todos correctamente |
| `Two_aggregates_with_new_children_in_the_same_SaveChanges_both_succeed` | Dos agregados distintos con hijos nuevos en el mismo `SaveChanges` no interfieren entre sí |
| `Genuinely_modified_existing_entity_is_not_misclassified_as_Added` | Una entidad existente genuinamente modificada (`Close()`) nunca se reclasifica como `Added` (no produce `INSERT` duplicado) |
| `Repeated_SaveChanges_calls_in_the_same_context_keep_working` | El interceptor es correcto a través de múltiples `SaveChanges` consecutivos sobre el mismo `DbContext` |
| `Query_tracked_entity_forced_Modified_with_zero_real_diff_throws_instead_of_guessing` | La combinación anómala (entidad query-tracked, `Modified`, sin diff real) falla explícito en vez de adivinar |

Se usó PostgreSQL real (no el provider InMemory) porque `CashSession` tiene un token
de concurrencia `xmin`, no soportado por InMemory — el mismo mecanismo que produce
la excepción original en producción.

### Architecture gate

`NewChildEntityTrackingArchitectureTests.ATT_GATE_01_no_blind_reattach_of_detached_entities_outside_allowed_repositories`
escanea todo el código fuente de `backend/src` (excluyendo migraciones, `bin/obj` y
proyectos de test) buscando el patrón
`(_db|_context|context)\.\w+\.(Attach|Update)\(` y falla el build si aparece fuera
de una lista explícita de 3 repositorios autorizados (ver sección "Regla
arquitectónica permanente").

### Validación end-to-end vía API en ejecución

- **Caja**: `POST /api/v1/sales/{id}/authorize` con caja abierta — antes del fix,
  500 (`DbUpdateConcurrencyException`); después del fix, 200, factura `Authorized`,
  `CashMovement` tipo `SaleIncome` creado con el monto correcto, sin excepción en
  logs.
- **Ventas**: patrón "Replace" de `SalesInvoicePayment` verificado en el mismo flujo
  de autorización — pagos persistidos correctamente sin reclasificación errónea.

### Ausencia de regresiones

Build completo de la solución (`ERP.Infrastructure`, `ERP.API`) sin errores. La
única falla de test detectada (`SEQ_GATE_02` en
`DocumentSequenceExclusivityTests`) es preexistente y no relacionada — confirmado
vía `git log` que el fallo antecede a este cambio.

---

## Consecuencias

### Positivas

- **Corrección transversal automática**: cualquier módulo del ERP que agregue
  hijos nuevos a un agregado ya trackeado desde un domain event handler queda
  protegido sin cambios en su propio código.
- **Sin cambios en los handlers existentes**: el fix vive enteramente en
  infraestructura (`ErpDbContext` + `NewChildEntityTrackingInterceptor`); ningún
  módulo de negocio fue modificado.
- **Fail-fast**: estados ambiguos no se "adivinan"; se reportan con un mensaje que
  identifica la entidad y exige revisión manual, evitando enmascarar bugs futuros.
- **Verificado con PostgreSQL real**, no InMemory, replicando el mecanismo exacto
  (`xmin`) que produce el error en producción.

### Limitaciones y riesgos residuales

- La corrección depende del invariante de reatachamiento (ver regla permanente).
  Si una nueva ruta de código introduce `Attach()`/`Update()` directo sin pasar por
  una query, `ATT-GATE-01` debe detectarlo en CI antes de merge — la protección no
  es solo documental.
- El interceptor opera sobre **todo** el `ChangeTracker` en cada `SavingChanges`;
  el costo es proporcional al número de entradas trackeadas en la unidad de
  trabajo, consistente con el costo ya pagado por `DetectChanges()` que EF Core
  ejecuta de todas formas antes de persistir.
- El interceptor no puede distinguir un caso anómalo genuinamente reparable de un
  bug real; por diseño, ambos terminan en excepción. Esto es intencional (ver
  filosofía fail-fast) pero implica que cualquier caso anómalo nuevo requiere
  intervención humana, no autorecuperación.

### Consideraciones para evolución futura

- Si aparece un nuevo módulo con el mismo patrón (hijo nuevo descubierto por fixup
  sobre un padre ya trackeado), no requiere cambio de infraestructura: el
  interceptor ya lo cubre automáticamente.
- Si se detecta un escenario legítimo que el interceptor rechaza incorrectamente
  con `InvalidOperationException`, la corrección debe revisarse como una nueva ADR
  — no debe "silenciarse" relajando la condición de fail-fast sin análisis
  arquitectónico.
- Cualquier necesidad real de reatachar un agregado sin pasar por una query previa
  (caso no identificado a la fecha) requiere ampliar explícitamente la lista
  blanca de `ATT-GATE-01`, justificando por qué esa entidad no participa del
  patrón de fixup que protege el interceptor (p. ej., entidades de catálogo sin
  colecciones de navegación hijas, como ya ocurre con los 3 repositorios
  actualmente autorizados).

---

## Regla arquitectónica permanente

> Ningún agregado existente podrá ser reatachado mediante `DbSet.Attach()`,
> `DbSet.Update()` o mecanismos equivalentes sin haber sido previamente cargado
> mediante una consulta del mismo `DbContext`. Toda modificación de un agregado
> existente deberá iniciarse desde una entidad obtenida mediante el repositorio
> correspondiente. La infraestructura de persistencia asume este invariante y lo
> protege mediante `ATT-GATE-01` y la validación interna del
> `ISaveChangesInterceptor`. Si este invariante se viola, la infraestructura
> deberá fallar explícitamente mediante una excepción en lugar de intentar
> corregir automáticamente el estado del `ChangeTracker`. Esta regla debe
> considerarse permanente.

### Excepciones explícitas (lista blanca cerrada)

Los siguientes repositorios usan deliberadamente `Attach()`/`Update()` sobre
entidades de catálogo/configuración **sin colecciones de navegación hijas** — no
están expuestos al patrón de fixup que protege el interceptor, porque no existe
grafo de hijos que pueda ser descubierto erróneamente:

1. `src/ERP.Infrastructure/MasterData/Repositories/PaymentTermRepository.cs`
2. `src/ERP.Infrastructure/Persistence/Repositories/Sales/PaymentMethodRepository.cs`
3. `src/ERP.Infrastructure/Persistence/Repositories/SriSettingsRepository.cs`

Ampliar esta lista requiere justificar explícitamente, para la entidad nueva, que
no tiene colecciones de navegación hijas con claves generadas por dominio
expuestas al mismo patrón.

### Restricciones definitivas (permanentes)

1. La única infraestructura autorizada para corregir una clasificación ambigua del
   `ChangeTracker` es `NewChildEntityTrackingInterceptor`.
2. Ningún módulo puede mutar manualmente `EntityState` de una entrada del
   `ChangeTracker` como mecanismo de negocio.
3. Ningún módulo puede introducir `Attach()`/`Update()` directo sobre un `DbSet`
   fuera de la lista blanca cerrada de `ATT-GATE-01`.
4. Toda modificación de un agregado existente debe iniciarse desde una entidad
   obtenida vía el repositorio correspondiente (que la carga con una query en el
   `DbContext` activo).
5. Cualquier cambio en la estrategia de detección (señal `WasTrackedFromQuery`,
   condición de clasificación, o la decisión fail-fast) requiere una nueva ADR
   aprobada y repetir la suite de integración con PostgreSQL real.

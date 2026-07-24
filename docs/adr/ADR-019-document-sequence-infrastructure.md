# ADR-019: Infraestructura centralizada de secuencias documentales

**Estado:** ✅ FROZEN — Infraestructura cerrada definitivamente  
**Fecha aprobación:** 2026-06-29  
**Autor:** Sebastian Zhinin  
**Contexto:** ERP SaaS multiempresa — numeración SRI de comprobantes electrónicos

> **FROZEN:** La arquitectura, el modelo de datos, la estrategia de concurrencia y
> la API pública de esta infraestructura están cerrados. No se aceptan cambios
> estructurales sin una nueva ADR aprobada. La única operación autorizada para
> obtener un consecutivo documental es `IDocumentSequenceRepository.CaptureNextAsync()`.

---

## Contexto

El SRI ecuatoriano exige que cada comprobante electrónico lleve un número secuencial
de 9 dígitos por punto de emisión y tipo de comprobante (`001-001-000000001`). Este
número es irrepetible, auditado por el SRI y constituye parte del Access Key de 49
caracteres que identifica unívocamente al documento ante la autoridad tributaria.

El ERP emite múltiples tipos de comprobante (Factura "01", Nota de Crédito "04",
Nota de Débito "05", Guía de Remisión "06", Retención "07") desde múltiples puntos
de emisión, con múltiples empresas (multi-tenant) operando sobre la misma base de
datos.

En el estado previo a esta infraestructura, varios módulos (Sales, Purchases)
mantenían su propia lógica de asignación de secuencial usando `SELECT … FOR UPDATE`
sin transacción explícita, lo que producía condiciones de carrera bajo carga
concurrente.

---

## Problema

Permitir que cada módulo asigne su propio secuencial genera los siguientes riesgos:

| Riesgo | Impacto |
|--------|---------|
| **Duplicados bajo concurrencia** | `SELECT FOR UPDATE` en modo autocommit no bloquea; dos hilos concurrentes pueden leer el mismo `CurrentSeq` y emitir el mismo número. Resultado: rechazo SRI y multa por numeración duplicada. |
| **Condiciones de carrera en first-insert** | Si la fila no existe, dos hilos la crean simultáneamente; viola la restricción `UNIQUE` y genera excepción 500 no controlada. |
| **Secuencial incorrecto en módulo nuevo** | Cada módulo nuevo tiene que reimplementar la lógica correctamente; la probabilidad de error crece con el número de módulos. |
| **Inicio en 0** | Un secuencial inicial de `000000000` es inválido para el SRI. El primer documento válido debe ser `000000001`. |
| **Hardcode del tipo documental** | El módulo de Ventas usaba `"01"` hardcodeado, haciendo imposible emitir Notas de Crédito/Débito por el mismo punto de emisión. |
| **Inconsistencia multi-tenant** | Sin restricción `UNIQUE(tenant_id, company_id, emission_point_id, doc_type_code)` la BD no garantiza aislamiento; dos tenants podrían compartir una fila de secuencia. |
| **Auditorías imposibles** | Sin una tabla centralizada de secuencias no se puede auditar cuántos documentos emitió cada punto de emisión por tipo, ni detectar huecos anómalos. |

---

## Decisión arquitectónica

### Entidad `DocumentSequence`

Se crea la entidad de dominio `DocumentSequence` (tabla `document_sequence`) como
**infraestructura transversal del ERP**, independiente de cualquier módulo operativo.

Responsabilidades:
- Mantener el estado del próximo secuencial por `(EmissionPointId, DocTypeCode)`.
- Exponer `CaptureAndIncrement()` como método de dominio que retorna el número
  formateado y avanza el contador. Este método solo puede ser invocado desde
  `IDocumentSequenceRepository.CaptureNextAsync()`.

Responsabilidades excluidas:
- No almacena configuración del punto de emisión (eso pertenece a `EmissionPoint`).
- No conoce la estructura del Access Key (eso pertenece al módulo de facturación).
- No valida reglas de negocio SRI (eso pertenece a FluentValidation del módulo).

### Por qué `EmissionPoint` no almacena numeración

`EmissionPoint` es una entidad de configuración: código SRI, nombre, establecimiento
padre, tipo de emisión. Su ciclo de vida es administrativo (creación, activación,
desactivación). Mezclar estado transaccional de alta concurrencia (secuenciales)
con configuración produciría:

- Bloqueos excesivos en la fila de `EmissionPoint` durante emisión masiva.
- Acoplamiento entre administración de puntos de emisión y operación documental.
- Imposibilidad de soportar múltiples `DocTypeCode` por punto de emisión sin
  duplicar columnas o agregar una relación N:1 ad-hoc.

La separación en `DocumentSequence` permite escalar independientemente:
N tipos documentales por punto de emisión, sin tocar el modelo de configuración.

---

## Estrategia de concurrencia

### Mecanismo elegido: `pg_advisory_xact_lock` + transacción explícita

```sql
-- Dentro de BEGIN … COMMIT ReadCommitted:
SELECT pg_advisory_xact_lock(:ep_hash, :doc_hash);
-- Luego: SELECT … IgnoreQueryFilters + INSERT/UPDATE raw SQL
```

**Flujo de `CaptureNextAsync`:**

1. Abrir transacción `ReadCommitted`.
2. Adquirir `pg_advisory_xact_lock(stable_hash(emissionPointId), stable_hash(docTypeCode))`.
   - La cerradura es a nivel de transacción: se libera automáticamente al hacer
     `COMMIT` o `ROLLBACK`, sin riesgo de leak.
   - El scope del lock es `(epHash, docHash)`: no bloquea otros puntos de emisión
     ni otros tipos documentales.
3. Leer la fila existente (o detectar ausencia) con `IgnoreQueryFilters()` para
   bypassar el filtro global de tenant (la transacción dedicada garantiza el scope
   correcto mediante los parámetros explícitos de `tenantId`/`companyId`).
4. Si la fila no existe: `INSERT` con `current_seq = 2` (la fila nace con el primer
   número ya entregado).
5. Si la fila existe: `UPDATE current_seq = GREATEST(current_seq, 1) + 1`.
6. `COMMIT` → libera el advisory lock.
7. Retornar el secuencial formateado en 9 dígitos (`D9`, `InvariantCulture`).

### Por qué se descartaron las alternativas

| Alternativa | Razón de descarte |
|---|---|
| **`SELECT … FOR UPDATE` sin transacción** | PostgreSQL en autocommit libera el row lock inmediatamente; no serializa concurrentes. Era el bug original. |
| **`SELECT … FOR UPDATE` con transacción ambient** | Correcto en teoría, pero el caso "fila no existe" no tiene row que bloquear; dos hilos hacen `INSERT` simultáneo → excepción `UNIQUE`. |
| **Optimistic concurrency (`xmin`)** | Requiere retry loop; bajo alta contención el retry puede fallar N veces; no es determinista. No aceptable para numeración SRI. |
| **`SEQUENCE` de PostgreSQL** | Los sequences de PG no tienen garantía de continuidad (huecos por transacciones abortadas). El SRI no exige continuidad estricta pero los huecos son auditables y generan preguntas. Advisory lock + UPDATE da secuencia continua sin huecos bajo funcionamiento normal. |
| **Redis / contador atómico externo** | Añade dependencia de infraestructura; no disponible en todos los despliegues; complejidad de sincronización con la BD. |
| **Hash `GetHashCode()`** | No determinístico en .NET 5+; dos procesos podrían generar keys distintos para el mismo `EmissionPointId`. Se usa hash FNV estable (`h = h * 31 + b`). |

---

## Restricciones implementadas

### Base de datos

| Restricción | SQL | Propósito |
|---|---|---|
| `UNIQUE` compuesto | `uq_doc_seq (tenant_id, company_id, emission_point_id, doc_type_code)` | Aislamiento multi-tenant explícito en la BD |
| `CHECK` positivo | `chk_doc_seq_positive: current_seq >= 1` | La BD rechaza cualquier UPDATE que deje el secuencial en 0 o negativo |
| FK `emission_point_id` | `→ emission_point(id) RESTRICT` | La secuencia no puede existir sin un punto de emisión válido |
| FK `doc_type_code` | `→ global.sri_doc_type(code) RESTRICT` | Solo tipos documentales SRI válidos |

### Dominio

- `DocumentSequence.CaptureAndIncrement()` lanza `InvalidOperationException` si
  `CurrentSeq < 1` (guard de invariante — última línea de defensa en dominio).
- `DocumentSequence.Create(...)` inicializa `CurrentSeq = 1` (primer documento = `000000001`).

### Architecture gates (CI-bloqueantes)

Los siguientes tests en `ERP.Infrastructure.Tests` fallan el build si se viola la regla:

| Gate | Descripción |
|---|---|
| `SEQ-GATE-01` | `.CaptureAndIncrement(` no aparece en ningún archivo de producción fuera de la entidad |
| `SEQ-GATE-02` | `CurrentSeq` solo se muta en `DocumentSequence.cs` |
| `SEQ-GATE-03` | `INSERT INTO document_sequence` / `UPDATE document_sequence` solo en el repositorio |
| `SEQ-GATE-04` | `.GetForUpdateAsync(` no es invocado desde capa Application |

---

## Evidencia de validación

Suite ejecutada el 2026-06-29 con PostgreSQL 16-alpine real (Testcontainers).
**8/8 tests passing, tiempo total 31.8 s.**

| Escenario | Req | Duplicados | Errores | `CurrentSeq` final |
|---|---:|---:|---:|---:|
| Concurrentes 10 | 10 | 0 | 0 | 11 |
| Concurrentes 50 | 50 | 0 | 0 | 51 |
| Concurrentes 100 | 100 | 0 | 0 | 101 |
| Concurrentes 500 | 500 | 0 | 0 | 501 |
| Repetición 20 × 20 | 400 | 0 | 0 | 401 |
| Multi-EP (2 × 30) | 60 | 0 | 0 | 31 c/u |
| Multi-doctype (2 × 25) | 50 | 0 | 0 | 26 c/u |
| Guard dominio | — | — | InvalidOp (esperada) | — |

Métricas representativas (escenario 100 req):
- avg: 294 ms · max: 540 ms · min: 89 ms · throughput: ~169 req/s · errores: 0

No se detectaron deadlocks, bloqueos permanentes ni excepciones inesperadas en
ningún escenario.

---

## Consecuencias

### Positivas

- **Unicidad garantizada** bajo cualquier nivel de concurrencia.
- **Punto de emisión administrativo limpio**: `EmissionPoint` solo tiene
  configuración; el estado transaccional vive en `DocumentSequence`.
- **Soporte de todos los tipos SRI** sin cambio de modelo: agregar un nuevo
  `DocTypeCode` no requiere migración ni código nuevo.
- **Auditoría centralizada**: la tabla `document_sequence` muestra cuántos
  documentos emitió cada punto por tipo, con fechas de creación y actualización.
- **Multi-tenant seguro**: el `UNIQUE` compuesto impide colisión entre tenants
  incluso si se ignoran los filtros globales.

### Limitaciones y trade-offs

- El advisory lock serializa las capturas para el mismo `(emissionPointId, docTypeCode)`.
  Esto es intencional; el throughput por punto de emisión está limitado por la
  capacidad de PostgreSQL de procesar transacciones serializadas (~150–250 req/s
  en la suite de pruebas con infraestructura de desarrollo).
- Los huecos en la secuencia son posibles si una transacción externa falla después
  de `CaptureNextAsync` pero antes de persistir el documento. Esto no es un defecto
  de la infraestructura de secuencias; es el comportamiento correcto bajo fallo
  parcial. El SRI no penaliza huecos ocasionales.
- `CaptureNextAsync` abre su propia transacción interna. Si el handler que la
  invoca también tiene una transacción ambient activa, la transacción interna es
  independiente (no participa en la transacción del handler). Esto es intencional:
  el número se asigna de forma irrevocable incluso si el handler falla posteriormente.

### Consideraciones para evolución futura

- **Reseteo de secuencia**: si el SRI requiere reseteo al inicio de año fiscal,
  se necesita un nuevo use case que actualice `CurrentSeq` respetando el advisory
  lock. No debe hacerse vía SQL directo.
- **Múltiples puntos de emisión por usuario**: la selección del punto de emisión
  es responsabilidad del handler que invoca `CaptureNextAsync`; la infraestructura
  no toma esa decisión.
- **Reporting de secuenciales**: leer `document_sequence` directamente para
  reportes es válido (lectura, no escritura); no viola esta ADR.

---

## Restricciones definitivas (permanentes)

1. La única operación autorizada para obtener un consecutivo documental es
   `IDocumentSequenceRepository.CaptureNextAsync()`.
2. Ningún módulo puede llamar directamente a `DocumentSequence.CaptureAndIncrement()`.
3. Ningún módulo puede leer y luego escribir `CurrentSeq` fuera del repositorio.
4. Ningún módulo puede emitir SQL raw de escritura sobre la tabla `document_sequence`
   fuera de `DocumentSequenceRepository`.
5. `IDocumentSequenceRepository.GetForUpdateAsync()` existe solo por compatibilidad
   de interfaz; no debe invocarse desde ningún handler de Application.
6. Todo nuevo tipo documental SRI se incorpora sin cambio de modelo:
   se llama `CaptureNextAsync(…, docTypeCode, …)` con el código SRI correspondiente.
7. Cualquier cambio en la estrategia de concurrencia (lock, nivel de aislamiento,
   mecanismo de increment) requiere una nueva ADR aprobada y repetir la suite de
   pruebas concurrentes con PostgreSQL real.

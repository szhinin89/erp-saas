# Estándar de Manejo de Errores

Decisión arquitectónica 2026-07-27. Rige el diseño; la migración de código existente sigue el plan de fases de ADR-027 — hasta que cada fase se implemente, este documento es el estándar **obligatorio para todo código nuevo** y la referencia de corrección para código existente.

ADR: [`docs/adr/ADR-027-error-handling-architecture.md`](../docs/adr/ADR-027-error-handling-architecture.md)
Auditoría de estado actual: [`docs/architecture/ERROR-HANDLING-AUDIT.md`](../docs/architecture/ERROR-HANDLING-AUDIT.md)

---

## Arquitectura

```
Domain            → condiciones de negocio puras (no encontrado, duplicado, regla violada)
                    nunca conoce HTTP ni el envelope JSON
Application       → Result<T> con Code SIEMPRE desde ApiResponseCodes (nunca string literal)
                    MessageCatalog: cada Code → Category + Severity + message.user + message.dev
Infrastructure    → IDatabaseExceptionTranslator es el único traductor de excepciones técnicas
                    a condiciones de negocio reconocidas por Result<T>
API               → ApiResultExtensions / ExceptionMiddleware son los ÚNICOS puntos de decisión
                    de HTTP status — derivado de Category, nunca hardcodeado por handler/controller
Frontend          → Axios (transporte puro)
                       ↓
                    normalizeApiError(err) → ApiError   [ÚNICO parser de response.data]
                       ↓
                    applyServerErrors<T>()  → campos RHF (categoría Validation)
                    formatApiRequestError() → mensaje general (fallback)
                       ↓
                    ZHFormAlert / ZHToast / ZHPageNotice / modal (presentación pura)
```

---

## Envelope oficial (congelado)

```json
{
  "code": "SKU_DUPLICATE",
  "severity": "error",
  "message": { "user": "El SKU ya existe en el catálogo de ítems.", "dev": "Unique violation on items.sku" },
  "data": { "errors": { "sku": ["El SKU ya existe en el catálogo de ítems."] } },
  "meta": { "correlationId": "...", "timestamp": "...", "traceId": null }
}
```

`code`, `severity`, `message.user`, `meta.correlationId`, `meta.timestamp` son siempre obligatorios. `message.dev` solo se serializa en Development. `data.errors` es mapa `campo→mensajes` para `Validation`, arreglo plano para el resto, ausente si no aplica.

## Categorías → HTTP (única tabla, ningún módulo la reimplementa)

| Categoría | HTTP |
|---|---|
| Validation | 422 |
| BusinessRule | 400 |
| Duplicate | 409 |
| NotFound | 404 |
| Authentication | 401 |
| Authorization | 403 |
| Infrastructure | 503 |
| Integration / ExternalProvider | 502 |
| RateLimit | 429 |
| InternalError | 500 |

---

## Reglas obligatorias — Backend

| # | Regla | Estado |
|---|-------|--------|
| E-B1 | Todo `Result<T>.Failure`/`.Conflict`/`.ValidationFailure`/`.NotFound`/`.Forbidden` usa un `Code` declarado en `ApiResponseCodes` (global o namespace del módulo) — nunca un string literal inline | ✅ obligatorio |
| E-B2 | Todo código en `ApiResponseCodes` tiene entrada correspondiente en `MessageCatalog` con `Category` + `Severity` + `message.user` + `message.dev` | ✅ obligatorio |
| E-B3 | El HTTP status de una respuesta de error se deriva de la `Category` del código — nunca decidido a mano en un controller (`BadRequest(string)`, `StatusCode(n, ...)` fuera de `ApiResultExtensions`) | ✅ obligatorio |
| E-B4 | Toda respuesta (éxito o error) pasa por `ResponseFactory`/`ApiResultExtensions` — prohibido `Ok(dto)`/`BadRequest(string)` crudos que devuelvan payload fuera del envelope oficial | ✅ obligatorio |
| E-B5 | Excepciones técnicas de infraestructura (Postgres, EF Core) se traducen exclusivamente vía `IDatabaseExceptionTranslator` — ningún handler inspecciona `PostgresException`/`DbUpdateException` directamente | ✅ obligatorio |
| E-B6 | `message.user` nunca contiene texto de excepción técnica cruda (`exception.Message` de una excepción no curada para el usuario) | ❌ prohibido lo contrario |
| E-B7 | Un `Code` no registrado en `MessageCatalog` es un defecto de build/CI, nunca un caso tolerado en runtime con fallback silencioso | ❌ prohibido el fallback silencioso |
| E-B8 | Cada `FailureCode` nuevo tiene al menos un test que confirme HTTP status + forma del envelope | ✅ obligatorio |

## Reglas obligatorias — Frontend

| # | Regla | Estado |
|---|-------|--------|
| E-F1 | Ningún componente/página accede a `error.response.data` directamente — todo pasa por `normalizeApiError`/`apiError.ts` | ❌ prohibido lo contrario |
| E-F2 | Errores de campo (`Validation` con `data.errors` como mapa) se aplican exclusivamente vía `applyServerErrors<T>()` | ✅ obligatorio (ya vigente, ver `CLAUDE.md` F-V5) |
| E-F3 | Todo error sin campo específico se resuelve vía `formatApiRequestError()` — prohibido reimplementar ese parsing en una página o componente | ❌ prohibido lo contrario |
| E-F4 | Ninguna mutación (crear/editar/eliminar/activar/desactivar) puede terminar en un `catch` vacío — como mínimo, `message.error(...)` | ❌ prohibido `catch {}`/`catch { /* */ }` sin acción visible al usuario |
| E-F5 | El canal de presentación se elige según la tabla de UX de ADR-027 §12 (campo / `ZHFormAlert` / `ZHToast` / `ZHPageNotice` / modal) — no "lo que el desarrollador tuvo a mano" | ✅ obligatorio |
| E-F6 | `message.dev`/detalle técnico nunca se renderiza en UI — solo se loggea vía `logApiDevError`/consola de desarrollo | ❌ prohibido mostrarlo al usuario |
| E-F7 | Errores bloqueantes usan `role="alert"`/`aria-live="assertive"`; no bloqueantes usan `role="status"`/`aria-live="polite"` — nunca solo color | ✅ obligatorio |

---

## Prohibido en todo el sistema

- Códigos de error (`FailureCode`) como strings literales sueltos fuera de `ApiResponseCodes`.
- Un factory method de `Result<T>` (`.Conflict()`, `.ValidationFailure()`, etc.) cuyo HTTP status real no coincide con lo que su nombre promete.
- `switch`/mapeos HTTP duplicados fuera de la tabla única categoría→HTTP (ningún controller, ningún handler decide su propio status).
- Componentes/páginas de React que interpretan `response.data` fuera de `modules/lib/apiError.ts`.
- Un segundo formateador de error de UI compitiendo con `formatApiRequestError` (`formatApiError.ts` está deprecado — ver plan de migración).
- `catch` vacío en cualquier acción de mutación.
- Mostrar `message.dev`, stack traces o detalle técnico de excepciones al usuario final.

## Reglas de evolución

- Un dominio nuevo (Inventory, Accounting, CRM, …) agrega su propia clase anidada en `ApiResponseCodes` y sus entradas en `MessageCatalog` — nunca modifica `Result<T>`, `ApiResultExtensions`, `ExceptionMiddleware` ni `MessageCatalog` como clase.
- Un canal de presentación nuevo (ej. un tipo de banner adicional) requiere actualizar la tabla de UX de ADR-027 §12, no improvisarse por módulo.
- Cualquier cambio a la tabla categoría→HTTP, al envelope oficial, o a los componentes de presentación (`ZHToast`/`ZHFormAlert`/`ZHPageNotice`, `lib/messages`) sigue las reglas de evolución de su propia infraestructura FROZEN (`AI-RULES/VISUAL-MESSAGES.md` para presentación) o requiere una nueva ADR si toca el contrato del envelope.

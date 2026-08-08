# Auditoría — Estado Actual de Manejo de Errores (Entregable 10, ADR-027)

- **Fecha**: 2026-07-27
- **Alcance**: Auditoría de solo lectura. No se modificó código de producción.
- **Referencia normativa**: [`ADR-027`](../decisions/ADR-027-error-handling-architecture.md) · [`docs/architecture/error-handling.md`](./error-handling.md)

Este documento es la fotografía del estado real del código al momento de la auditoría, componente por componente, usada como evidencia para el diseño de ADR-027. No prescribe la solución (eso vive en el ADR) — documenta el `Cumple`/`Parcial`/`No cumple` de cada pieza y las observaciones concretas (archivo:línea) que lo sustentan.

---

## Backend

| Componente | Cumple | Observaciones |
|---|---|---|
| `Result<T>` (`ERP.Application/Modules/Common/Result.cs`) | ⚠️ Parcial | Único vehículo de fallo del backend (no hay `Failure` separado). `Error` es un string plano (sin soporte de campo-a-campo); `Code` es `string` libre, no restringido a `ApiResponseCodes`. Los nombres de factory method (`Conflict`, `ValidationFailure`, `NotFound`, `Forbidden`) **prometen** un HTTP status que no se garantiza — ver `ApiResultExtensions`. |
| `ApiResponseCodes` (`ERP.Application/Common/ApiResponseCodes.cs`) | ❌ No cumple | El propio doc-comment de la clase documenta el patrón de extensión por módulo (clases anidadas, ej. `ApiResponseCodes.Inventory`). **Adopción real: cero.** Solo existe la clase `Common` (17 constantes). Todos los módulos (Items, MasterData, Accounting) usan strings literales (`"SKU_DUPLICATE"`, `"BARCODE_DUPLICATE"`, `"SUPPLIER_CODE_DUPLICATE"`, `"IDENTIFICATION_DUPLICATE"`, `"PERIOD_NOT_OPEN"`, `"RULE_NOT_FOUND"`) directamente en los handlers. |
| `MessageCatalog` (`ERP.Application/Common/MessageCatalog.cs`) | ⚠️ Parcial | Correcto para los 17 códigos `Common` registrados (severity + user + dev). Para cualquier código no registrado, `Resolve()` devuelve silenciosamente el fallback genérico ("Ocurrió un error inesperado.") — sin fallar build ni test, sin alertar. No tiene campo `Category`. |
| `ApiResultExtensions.MapFailure` (`ERP.API/Extensions/ApiResultExtensions.cs:87-103`) | ❌ No cumple | Switch hardcodeado que solo reconoce 6 códigos `Common` exactos. Cualquier código de módulo cae al `default → 400 BadRequest`, **sin importar si se creó con `.Conflict()` o `.ValidationFailure()`**. Evidencia concreta: `SKU_DUPLICATE`/`BARCODE_DUPLICATE`/`SUPPLIER_CODE_DUPLICATE` (creados vía `.Conflict()`, intención 409) → HTTP real **400**. `PERIOD_NOT_OPEN`/`RULE_NOT_FOUND` (vía `.ValidationFailure()`, intención 422) → HTTP real **400**. |
| `ExceptionMiddleware` (`ERP.API/Middleware/ExceptionMiddleware.cs`) | ⚠️ Parcial | Envelope correcto y consistente (`ResponseFactory`), no filtra stack traces, respeta `IsDevelopment()` para `message.dev` (verificado por test). Pero: (a) switch por *tipo* de excepción, independiente y desincronizado del switch por *código* de `MapFailure` — dos fuentes de verdad; (b) `DbUpdateException` sin traducir → 503 "database unavailable", un status engañoso para lo que suele ser una violación UNIQUE no capturada; (c) `InvalidOperationException.Message` (texto libre de cualquier desarrollador) se expone tal cual en `message.user`/`data.errors` vía el whitelist de tipos — riesgo de fuga de detalle técnico si alguien escribe `throw new InvalidOperationException("detalle interno")`; (d) `XmlParseException` no está en el switch — cae al 500 genérico sin mensaje. |
| FluentValidation → 422 (`ValidationException` → `ExceptionMiddleware` → `data.errors` como mapa) | ✅ Cumple | Verificado end-to-end contra `ERP.API.Tests/ExceptionMiddlewareValidationTests.cs`. Es la única infraestructura de errores que hoy cumple el contrato completo (envelope, camelCase, 422, mapa campo→mensajes). |
| `IDatabaseExceptionTranslator` (`ERP.Infrastructure/Persistence/PostgresDatabaseExceptionTranslator.cs`) | ⚠️ Parcial | Traduce correctamente `PostgresException` (SQLSTATE 23505) a `DatabaseUniqueViolationInfo`. Uso **opt-in por handler**, no automático — varios handlers prefieren un chequeo previo (`ExistsBySkuAsync`) en vez de este traductor, patrón propenso a condición de carrera bajo concurrencia. Cuando sí se usa, el `Result.Conflict(...)` resultante igual cae en el defecto de `MapFailure` (código module-specific → 400, no 409) — el traductor cumple su parte, pero el defecto aguas abajo anula su propósito. |
| Excepciones de dominio (`ERP.Domain/Exceptions/*`) | ⚠️ Parcial | Solo 4 tipos: `CompanyScopeException`, `BranchScopeException`, `CompanyRucAlreadyExistsException`, `SystemSeededRecordException`. No existe una jerarquía general (`DomainException`/`NotFoundException`/`DuplicateException`). La misma condición conceptual ("no encontrado", "duplicado") se representa de 3 formas distintas según el módulo/autor: `Result.NotFound(...)`, `ArgumentException` capturado y traducido en la capa Application, o excepción de dominio dedicada que escapa hasta el middleware. `SystemSeededRecordException` hereda de `InvalidOperationException` como atajo intencional pero frágil (documentado en su propio código). |
| Controllers — uso consistente del envelope | ❌ No cumple (casos puntuales) | `PaymentTermsController.List` (`ERP.API/Controllers/PaymentTermsController.cs`) devuelve el DTO crudo (`Ok(dto)`), sin envelope. `PaymentTermsController.Update` devuelve `BadRequest("ID mismatch.")` — string plano en inglés, sin `code`/`severity`/`message`, fuera del contrato F-V/B-V. |
| `BulkMatchItemsHandler` (Item Matching) | ❌ No cumple | Siempre devuelve `Result.Success` (HTTP 200), incluso si todos los ítems del lote fallaron. Los fallos individuales viajan como strings planos dentro del payload de éxito (`BulkMatchItemResultEntry(lineId, success, message)`) — una cuarta forma de reportar error, incompatible con `code`/`MessageCatalog`/status HTTP. |
| Cobertura CI del catálogo (`MessageCatalog_has_exactly_one_entry_per_ApiResponseCode`, `ERP.Architecture.Tests/ApiResponseContractTests.cs`) | ❌ No cumple su propósito real | El test solo reflexiona sobre símbolos `const string` declarados dentro de `ApiResponseCodes` y sus clases anidadas. Como ningún módulo usa ese patrón (ver fila `ApiResponseCodes` arriba), el test no detecta ninguno de los 6 códigos huérfanos documentados — punto ciego total sobre el patrón que realmente se usa en producción. |

### Caso de estudio — Item Matching (motivador de esta fase)

| Handler | Situación | HTTP real | HTTP prometido por el nombre del factory |
|---|---|---|---|
| `MatchItemHandler` / `FindItemMatchesHandler` | `Result.NotFound(...)` con código `Common.NotFound` (registrado) | 404 ✅ correcto | 404 |
| `CreateItemCommandHandler` (SKU/barcode dup, reusado por `CreateItemFromReceptionLineCommandHandler`) | `Result.Conflict(..., "SKU_DUPLICATE")` (código no registrado) | **400** ❌ | 409 |
| `BulkMatchItemsHandler` | Siempre `Result.Success`, fallos como strings en el payload | 200 (fallos ocultos al nivel HTTP) | — (no aplica el contrato) |

El commit `008ac6b7` ("fix manejo de errores en Item Matching Fase 2.1.2") corrigió el síntoma **solo en el frontend** (`CreateItemForm.tsx` ahora cae explícitamente a `formatApiRequestError` en vez de depender del fallback interno de `applyServerErrors`, que solo dispara en 422). La causa raíz backend (`SKU_DUPLICATE` sin registrar, `MapFailure` sin reconocerlo) sigue sin corregir — esta es exactamente la deuda que motiva ADR-027.

---

## Frontend

| Componente | Cumple | Observaciones |
|---|---|---|
| Axios (`modules/lib/api.ts`) | ✅ Cumple (alcance limitado) | Interceptor de response solo maneja refresh de token en 401; no transforma ni interpreta errores de negocio — correcto como capa de transporte pura. No existe normalización a un tipo `ApiError` en este nivel (por diseño, se delega a `apiError.ts` — ver fila siguiente). |
| `modules/lib/apiError.ts` — `formatApiRequestError` | ✅ Cumple | Implementación correcta: prioriza `data.errors` (422), luego `message.user`/`Message.User`, luego campos planos, luego `ModelState`-style. Maneja `!response` (offline) y 401 explícitamente. Nunca expone el texto crudo de Axios ("Request failed with status code..."). Es el formateador que deberían usar todos los módulos. |
| `modules/lib/validationErrors.ts` — `applyServerErrors<T>` | ✅ Cumple | Coincide exactamente con el contrato 422 documentado en `CLAUDE.md`. Devuelve `false` para cualquier forma que no sea el mapa `data.errors` de un 422 — comportamiento correcto por diseño (no es un defecto), pero exige que **todo call site** revise el retorno y aplique un fallback — la omisión de esa revisión fue el bug de `CreateItemForm` (corregido en `008ac6b7`). |
| Tipo `ApiError` compartido | ❌ No cumple — no existe | No hay `interface ApiError`/`ApiErrorResponse` en todo el frontend. Cada call site que no usa `apiError.ts` re-declara su propio shape inline (`any` o cast ad hoc), con variaciones sutiles entre sí. |
| `formatApiError.ts` (formateador alterno) | ❌ No cumple — duplicidad sin resolver | Segunda implementación de "formatear error para UI", i18n-aware, con reglas distintas (502/503/504 especiales, sin 401 hardcodeado). Sin consumidores confirmados en los módulos muestreados, pero exportado y vivo — riesgo de que un desarrollador nuevo lo elija por el nombre similar. |
| `lib/messages` (`message.*`, ADR-018, FROZEN) | ✅ Cumple | API pública correcta y respetada; no se tocó ni se propone tocar (infraestructura FROZEN). |
| `ZHToast` | ✅ Cumple | Presentación correcta; el problema nunca está en el componente, sino en qué string le llega (ver filas de módulos abajo). |
| `ZHFormAlert` (dentro de `ZHForm.tsx`) | ✅ Cumple (funcionalidad) / ⚠️ Parcial (accesibilidad) | Renderiza correctamente icon+mensaje+detalle. No confirmado que tenga `role`/`aria-live` como sí tiene `ZHPageNotice` — pendiente de verificación explícita en Fase 2. |
| `ZHPageNotice` | ✅ Cumple | Implementación con `role="alert"`/`aria-live="assertive"` (error/warning) vs `role="status"`/`aria-live="polite"` (resto) — el estándar de accesibilidad correcto, y el que debería generalizarse. |
| `items/ItemFormTabs.tsx` | ✅ Cumple | Patrón canónico: `applyServerErrors` → si `false` → `formatApiRequestError` → `setFormError` (banner). |
| `items/CreateItemModal/CreateItemForm.tsx` | ✅ Cumple (tras fix `008ac6b7`) | Mismo patrón canónico, con comentario explícito documentando por qué no puede depender solo del fallback interno de `applyServerErrors`. |
| `purchases/ZHItemMatchingPanel.tsx` | ✅ Cumple | Usa `formatApiRequestError` consistentemente (3 ocurrencias); presenta vía `ErrorState` inline. Un `catch` silencioso deliberado y documentado (`/* Silencioso */`) para un enriquecimiento en segundo plano no crítico — aceptable, pero es el único silencioso "a propósito" encontrado; el resto de silencios (ver abajo) no lo son. |
| `purchases/CreateItemFromReceptionLineModal.tsx` | ✅ Cumple | Usa `message.success`/`message.warning` + `logApiDevError`; mensaje de warning es deliberadamente hardcodeado (describe un escenario de dos pasos, no el error crudo) — decisión de UX razonable, no un defecto. |
| `items/ItemTypesPage.tsx` | ❌ No cumple | **Dentro del mismo módulo `items`** que `ItemFormTabs.tsx`/`CreateItemForm.tsx` (ambos ✅). Hand-parsea `response.data.data.errors` con un tipo inline propio, aplana todos los mensajes de campo en un único string (pierde el mapeo campo-a-campo), y usa canal distinto según la acción: banner inline para `handleSave`, toast (`message.error`) para `handleToggle` — inconsistencia de presentación para la misma clase de error dentro de una sola página. |
| `finance/CreditTermsPage.tsx` | ❌ No cumple | Parsing manual `e?.response?.data?.message?.user ?? e?.response?.data?.data?.errors?.[0] ?? e?.message` con `e: any`. **Además**, `catch { /* */ }` vacío en el fetch de lista (línea ~30) y en el toggle activar/desactivar (línea ~72) — fallo 100% silencioso, sin toast, sin log, sin cambio de estado visible. |
| `masterData/PaymentTermsPage.tsx` | ❌ No cumple | Mismo patrón de parsing manual duplicado; mismo `catch` vacío en list-fetch y toggle. |
| `pricing/PriceListsPage.tsx` | ❌ No cumple | Mismo patrón de parsing manual duplicado (variante). |
| `sales/PaymentMethodsPage.tsx` | ❌ No cumple | Mismo patrón de parsing manual duplicado; mismo `catch` vacío en list-fetch y toggle. |
| `auth/CompanySelectPage.tsx` | ❌ No cumple | Accede a `ax?.response?.data?.message` sin la anidación real (`message.user`) — probablemente siempre `undefined` en la práctica; violación de contrato y bug latente simultáneo. |

### Patrón transversal detectado

Al menos **6 páginas fuera del flujo de Item Matching** (`CreditTermsPage`, `PaymentTermsPage`, `PriceListsPage`, `PaymentMethodsPage`, `ItemTypesPage`, `CompanySelectPage`) re-implementan a mano, con variaciones, la misma lógica que ya existe correctamente centralizada en `apiError.ts`. Ninguna usa `any`/casts ad hoc por necesidad real — es duplicación evitable. El propio módulo `items` (origen del patrón correcto) contiene simultáneamente la mejor implementación (`ItemFormTabs.tsx`) y una de las peores (`ItemTypesPage.tsx`), lo que descarta la hipótesis de que el problema sea "un módulo legado antiguo" — es ausencia de enforcement, no antigüedad del código.

No se encontró el patrón "solo `console.error`, sin ningún feedback visual" mencionado en el prompt original como hipótesis — el anti-patrón dominante real es (a) parsing manual duplicado, y (b) `catch` completamente vacío (ni siquiera `console.error`) en acciones secundarias (toggle, refresh de lista).

---

## Matriz resumen (para referencia rápida)

| Área | Cumple | Parcial | No cumple |
|---|---|---|---|
| Backend — infraestructura central (`Result`, `MessageCatalog`, `ExceptionMiddleware`, `IDatabaseExceptionTranslator`) | 1 | 5 | 0 |
| Backend — adopción por módulos (`ApiResponseCodes`, controllers, Item Matching) | 0 | 0 | 4 |
| Frontend — infraestructura central (`apiError.ts`, `validationErrors.ts`, `lib/messages`, `ZHToast`/`ZHPageNotice`) | 6 | 1 | 1 |
| Frontend — adopción por módulos | 3 | 0 | 6 |

**Lectura:** la infraestructura central (lo que ADR-018/019/020/022 ya cerraron como FROZEN, y lo que `apiError.ts`/`ExceptionMiddleware` ya implementan) está mayormente sana. **La deuda está concentrada en la adopción por módulo** — exactamente donde no hay un gate de CI ni una regla canónica que la fuerce. Esta es la brecha que `ADR-027`/`docs/architecture/error-handling.md` cierran a nivel de diseño; la Fase 1-3 de migración (ADR-027 §15) la cierra a nivel de código.

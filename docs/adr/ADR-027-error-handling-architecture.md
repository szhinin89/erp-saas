# ADR-027: Arquitectura Unificada de Manejo de Errores

- **Estado**: Aceptado (arquitectura) — Pendiente de migración (ver Plan de Migración)
- **Fecha**: 2026-07-27
- **Autor**: Sebastian Zhinin (con Claude Code — Fase Gobernanza 2.0)
- **Reglas ejecutables**: [`AI-RULES/ERROR-HANDLING.md`](../../AI-RULES/ERROR-HANDLING.md)
- **Auditoría de estado actual**: [`docs/architecture/ERROR-HANDLING-AUDIT.md`](../architecture/ERROR-HANDLING-AUDIT.md)

---

## 1. Contexto

Durante la implementación de Item Matching (Purchase Reception → catálogo de Items, commits `b35d0dd4`…`008ac6b7`) se detectó que un error de negocio perfectamente válido (`SKU_DUPLICATE`) no llegaba a mostrarse al usuario. La causa raíz no fue un bug puntual: fue la ausencia de una arquitectura de errores única. El fix de Item Matching corrigió el síntoma en el frontend; este ADR corrige la causa en el sistema completo.

La auditoría de estado actual (Entregable 10, documento separado) encontró que el problema es sistémico, no aislado a un módulo. En resumen:

- **Backend**: `Result<T>.Conflict(...)`/`.ValidationFailure(...)` prometen un HTTP status por el nombre del método, pero el mapeo real (`ApiResultExtensions.MapFailure`) solo reconoce 9 códigos hardcodeados de `ApiResponseCodes.Common`. Todo código de módulo (`SKU_DUPLICATE`, `BARCODE_DUPLICATE`, `IDENTIFICATION_DUPLICATE`, `PERIOD_NOT_OPEN`, `RULE_NOT_FOUND`, …) cae al `default` del switch y se convierte silenciosamente en **400**, sin importar qué método de fábrica lo creó. El mismo defecto hace que `MessageCatalog.Resolve(code)` devuelva el mensaje genérico de fallback ("Ocurrió un error inesperado.") para cualquier código no registrado, aunque `Result.Error` ya traía un mensaje específico y correcto.
- **Backend**: la determinación de HTTP status está duplicada en dos lugares que no se sincronizan entre sí — `ExceptionMiddleware` (switch por *tipo* de excepción) y `ApiResultExtensions.MapFailure` (switch por *string* de código) — y una misma causa raíz (violación UNIQUE de Postgres) puede terminar en 409, 400 o 503 según qué camino la capturó.
- **Frontend**: no existe un tipo `ApiError` compartido. Al menos 6 páginas (Finance, MasterData, Pricing, Sales, y hasta `ItemTypesPage.tsx` dentro del mismo módulo `items` que ya tiene la implementación correcta) re-implementan a mano, con formas ligeramente distintas, el mismo parsing de `response.data.message.user` / `response.data.data.errors`, en tensión directa con el gate F-V5/F-V6 de `CLAUDE.md`. Varias acciones (toggle activar/desactivar, refresco de listas) fallan en **silencio total** sin ningún tipo de feedback.
- **Frontend**: existen dos formateadores de error paralelos y con comportamiento distinto (`formatApiRequestError` en `apiError.ts`, activamente usado, y `formatApiError` en `formatApiError.ts`, aparentemente huérfano) — riesgo de colisión y drift.

Ninguno de estos hallazgos es negligencia de un desarrollador puntual: es la consecuencia esperable de no tener, hasta ahora, un contrato arquitectónico único Backend ↔ Frontend para errores. Este ADR lo establece.

---

## 2. Objetivos

1. Un único contrato de error Backend ↔ Frontend, versionado y estable.
2. Una única estrategia de normalización de errores en cada lado (backend: `Result<T>` + catálogo; frontend: `ApiError` + un solo formateador).
3. Mensajes consistentes para usuario final y para desarrollador, nunca mezclados.
4. Componentes de presentación reutilizables, con reglas claras de cuándo usar cada uno.
5. Semántica HTTP correcta y **decidida en un único lugar** por categoría de error, no por módulo.
6. Escalabilidad: cualquier dominio nuevo del ERP (Inventory, Sales, Purchasing, Accounting, Cash & Banks, CRM, Assets, Manufacturing, RRHH) se suma sin modificar la infraestructura base — mismo patrón que ya rige Entity Audit (ADR-022) y Document Sequence (ADR-019).

## 3. No objetivos

- No se rediseña `ExceptionMiddleware`, `ResponseFactory`, `Result<T>` ni ningún contrato de negocio en esta fase — este documento es de **arquitectura y auditoría**, no de implementación (ver Restricciones).
- No se introduce un framework de terceros para manejo de errores (ni backend ni frontend). La solución se construye sobre lo que ya existe (`Result<T>`, `ApiResponseCodes`, `MessageCatalog`, `ExceptionMiddleware`, `lib/messages`, `apiError.ts`) corrigiendo sus huecos, no reemplazándolo.
- No se cambia el envelope `{ code, severity, message, data, meta }` — ya es correcto y ya está implementado; se **formaliza y se corrige su cobertura**, no se rediseña.
- No se implementa Process Audit, Retry policies de negocio, ni Circuit Breakers — fuera de alcance de este ADR.
- No se resuelve en este documento la deuda de Entity Audit sobre `IAuditContext` para jobs (ver `CLAUDE.md` sección Entity Audit) — es una infraestructura distinta.

## 4. Motivación

Un ERP multiempresa que procesa facturación electrónica SRI, inventario, compras y contabilidad no puede permitirse que un error de negocio se pierda entre capas. Cada error silencioso es, en producción, un usuario bloqueado sin saber por qué, o — peor — una operación que el usuario cree exitosa y no lo fue. La deuda encontrada no es estética: **es un riesgo operativo real** ya observado (Item Matching) y latente en otros 4+ módulos (ver auditoría).

---

## 5. Principios

1. **Fail Fast, nunca fail silent.** Ningún `catch` vacío. Ninguna operación que falla puede dejar al usuario sin ninguna señal.
2. **Single Error Contract.** Existe un único envelope de error, una única fuente de verdad de categorías, una única tabla categoría→HTTP. Ningún módulo decide su propio mapeo.
3. **Backend nunca expone excepciones técnicas.** `exception.Message`, stack traces, nombres de constraints de base de datos o excepciones de infraestructura nunca llegan al usuario final — solo a `message.dev` en Development, y a logs con `correlationId`.
4. **Frontend nunca interpreta JSON de error directamente.** Ningún componente ni página hace `error.response.data.algo`. Todo pasa por un normalizador único (`ApiError`) y un formateador único.
5. **Todo `FailureCode` tiene mensaje de usuario y mensaje de desarrollador — sin excepción, sin fallback silencioso a "Ocurrió un error inesperado."** Un código no registrado es un defecto de build, no un caso de runtime a tolerar (ver Entregable 5).
6. **Toda mutación informa su resultado al usuario** — éxito o error, siempre, en el canal apropiado (Entregable 7).
7. **Un código de error, un HTTP status, siempre el mismo.** El HTTP status es una función pura de la categoría del error, no una decisión ad hoc del handler que lo lanza.
8. **Domain nunca conoce HTTP.** El dominio expone condiciones de negocio (no encontrado, duplicado, regla violada); la traducción a HTTP ocurre exclusivamente en la frontera API.

---

## 6. Responsabilidades por capa

### 6.1 Domain

**Qué errores puede generar:** condiciones de negocio puras, sin conocimiento de transporte: "entidad no encontrada", "regla de negocio violada", "duplicado", "estado inválido para la operación". Se representan mediante los factory methods de `Result<T>` devueltos por el propio dominio/aplicación, o mediante excepciones de dominio específicas y con nombre (`XxxScopeException`, futuras `XxxNotFoundException`/`XxxDuplicateException` si el patrón se formaliza en la migración).

**Qué nunca debe conocer:** códigos HTTP, la forma del envelope JSON, `IWebHostEnvironment`, ni nada de la capa API. Un factory method de dominio no debe usar palabras como "Conflict" o "BadRequest" salvo como alias legible de una condición de negocio — nunca como promesa de un status HTTP (ese acoplamiento implícito es exactamente el defecto detectado en `Result<T>.Conflict()`/`.ValidationFailure()`, ver auditoría §2.4).

### 6.2 Application

**Cómo representar Failures:** `Result<T>` sigue siendo el vehículo, pero su `Code` deja de ser un `string` libre — debe provenir siempre de una constante registrada en `ApiResponseCodes` (global o del namespace del módulo). Un `Result.Failure`/`.Conflict`/`.ValidationFailure`/`.NotFound` con un código no registrado debe fallar la build (test de arquitectura, ver Entregable 5), no solo el runtime.

Cada entrada de catálogo (`MessageCatalog`) pasa a llevar, además de `Severity`/`User`/`Dev`, la **categoría** del error (Entregable 3) — de la cual se deriva el HTTP status de forma automática y única (Entregable 4), eliminando el switch duplicado y desincronizado de hoy.

### 6.3 Infrastructure

**Conversión de excepciones técnicas:** `IDatabaseExceptionTranslator` sigue siendo el único punto autorizado para traducir excepciones de Postgres (violaciones UNIQUE, etc.) a condiciones de negocio reconocidas por `Result<T>`. Ninguna otra capa inspecciona `PostgresException`/`DbUpdateException` directamente. Toda excepción de infraestructura que escape sin traducir (ej. `DbUpdateException` no capturado) sigue cayendo en `ExceptionMiddleware`, pero su categoría (`Infrastructure`) debe mapear a un status honesto (503), nunca a 400/409 por accidente de un `switch` que no la reconoce.

### 6.4 API

**Transformación HTTP y envelope oficial:** única responsabilidad de la capa API es traducir `Result<T>`/excepciones a la respuesta HTTP, usando exclusivamente la tabla categoría→HTTP (Entregable 4) y el envelope oficial (Entregable 2). Ningún controller decide su propio código de estado a mano (ver hallazgo de `PaymentTermsController` en la auditoría — un endpoint que devuelve el DTO crudo sin envelope, y un `BadRequest(string)` fuera de contrato). `ApiResultExtensions` y `ExceptionMiddleware` son los **únicos** puntos de decisión de HTTP status en todo el backend.

### 6.5 Frontend

**Normalización:** una única función (`normalizeApiError`, formalización de la lógica ya existente en `apiError.ts`) convierte cualquier error de Axios (con o sin respuesta, con o sin envelope) en un tipo `ApiError` tipado y exportado. Ningún componente recibe el error crudo de Axios.

**Presentación:** los componentes de UI (Entregable 6/7) reciben siempre `ApiError`, nunca `unknown`/`any`, nunca `error.response.data` a mano.

**UX:** decidir el canal de presentación (campo, banner, toast, modal, page notice) según el tipo de acción y la categoría del error (Entregable 7) — nunca "lo que el desarrollador tuvo a mano".

---

## 7. Entregable 2 — Contrato oficial (envelope)

El envelope ya existe en producción (`ERP.API/Contracts/ApiResponse.cs`, `types/api.ts`) y se **formaliza como contrato congelado** a partir de este ADR:

```json
{
  "code": "SKU_DUPLICATE",
  "severity": "error",
  "message": {
    "user": "El SKU ya existe en el catálogo de ítems.",
    "dev": "Unique violation on items.sku (constraint ix_items_sku_tenant)"
  },
  "data": {
    "errors": { "sku": ["El SKU ya existe en el catálogo de ítems."] }
  },
  "meta": {
    "correlationId": "8f2b6c1a-...",
    "timestamp": "2026-07-27T19:35:42Z",
    "traceId": null
  }
}
```

### Significado y obligatoriedad de cada propiedad

| Propiedad | Tipo | Obligatoria | Significado |
|---|---|---|---|
| `code` | `string` | **Sí** | Identificador estable del error. Debe existir en `ApiResponseCodes` (global o de módulo) y tener entrada en `MessageCatalog`. Nunca un string improvisado inline. |
| `severity` | `"success" \| "error" \| "warning" \| "info"` | **Sí** | Deriva de la categoría del código (Entregable 3), nunca se setea a mano por handler. |
| `message.user` | `string` | **Sí** | Mensaje en español, orientado a que el usuario entienda y actúe. Nunca vacío, nunca genérico salvo que la categoría sea realmente `InternalError`. |
| `message.dev` | `string \| null` | **Sí** (puede ser `null` en Production) | Detalle técnico. Solo se serializa cuando `IWebHostEnvironment.IsDevelopment()`. Nunca se muestra en UI. |
| `data.errors` | `Record<string,string[]> \| string[] \| null` | Condicional | Mapa `campo → mensajes` para errores de categoría `Validation` (422); arreglo plano para otras categorías con detalle adicional; `null`/ausente si no aplica. Dos formas coexisten hoy (mapa vs. arreglo) — **ambas siguen siendo válidas**, el frontend debe distinguirlas por forma, no por status code (ver Entregable 6). |
| `data.*` (éxito) | `T` | — | Payload de negocio en respuestas exitosas. No aplica a errores salvo `errors`. |
| `meta.correlationId` | `string` | **Sí** | Debe coincidir con el header `x-correlation-id` de la request. Es el nexo entre logs de frontend, logs de backend y soporte al usuario. |
| `meta.timestamp` | `string` (ISO 8601 UTC) | **Sí** | Momento de generación de la respuesta. |
| `meta.traceId` | `string \| null` | No | Reservado para tracing distribuido futuro (no implementado hoy). |

**Regla de escritura:** ningún handler, controller ni middleware construye este JSON a mano. Siempre a través de `ResponseFactory.Success`/`Error`/`ValidationError` (backend) — nunca `Ok(dto)` ni `BadRequest(string)` crudos (ver hallazgo `PaymentTermsController`).

---

## 8. Entregable 3 — Clasificación de errores (categorías oficiales)

| Categoría | Significa | Quién la genera típicamente | `data.errors` |
|---|---|---|---|
| **Validation** | Formato/obligatoriedad de campos, detectado por FluentValidation antes de ejecutar la regla de negocio | `ValidationException` (pipeline MediatR) | mapa `campo → mensajes` |
| **BusinessRule** | Regla de negocio del dominio impide la operación en su estado actual (ej. período contable cerrado, categoría deshabilitada) | `Result<T>.Failure`/`.ValidationFailure` con código de dominio | arreglo plano o `null` |
| **Duplicate** | Conflicto de unicidad — el recurso ya existe (SKU, RUC, código de barras, identificación) | `IDatabaseExceptionTranslator` + `Result<T>.Conflict`, o `DbUpdateConcurrencyException` | arreglo plano con el campo/constraint en el mensaje |
| **NotFound** | El recurso referenciado no existe o no es visible en el tenant/branch actual | `Result<T>.NotFound` | `null` |
| **Authentication** | No hay sesión válida o el token expiró | `UnauthorizedAccessException`, refresh fallido | `null` |
| **Authorization** | Sesión válida, pero sin permiso para el recurso/tenant/branch | `CompanyScopeException`, `BranchScopeException`, RBAC | `null` |
| **Infrastructure** | Falla técnica interna (BD no disponible, timeout de conexión, excepción no traducida) | `DbUpdateException` no traducido, timeouts | `null` — nunca detalle técnico en `user` |
| **Integration** / **ExternalProvider** | Falla de un sistema externo (SRI, pasarela de pago futura) | `SriCommunicationException` y equivalentes futuros | `null` o detalle del proveedor si es seguro exponerlo |
| **RateLimit** | Se excedió un límite de tasa | Middleware de rate limiting | `null` |
| **InternalError** | Cualquier excepción no clasificada — última red de seguridad | `catch (Exception)` genérico en `ExceptionMiddleware` | `null`, mensaje siempre genérico y seguro |

Cada `FailureCode` pertenece a **exactamente una** categoría, declarada junto a su entrada en `MessageCatalog` (Entregable 5). La categoría es la única fuente de la que se deriva `severity` y el HTTP status (Entregable 4) — un módulo no puede "decidir" que su duplicado sea 400 en vez de 409: la categoría `Duplicate` siempre mapea a 409.

---

## 9. Entregable 4 — Mapeo HTTP oficial

| Categoría | HTTP | Justificación |
|---|---|---|
| Validation | **422** | Ya implementado correctamente vía `ExceptionMiddleware` para FluentValidation; se extiende como regla general para toda categoría `Validation`, venga de donde venga. |
| BusinessRule | **400** | Petición sintácticamente válida, pero el estado de negocio no permite ejecutarla. |
| Duplicate | **409** | Conflicto de unicidad — semánticamente correcto y ya usado para los códigos `Common` registrados; se extiende a **todos** los códigos de esta categoría, no solo los de `ApiResponseCodes.Common`. |
| NotFound | **404** | Recurso inexistente o fuera de alcance del tenant. |
| Authentication | **401** | Sin sesión válida. |
| Authorization | **403** | Sesión válida, sin permiso. |
| Infrastructure | **503** | Indisponibilidad técnica — invita a reintentar, no a corregir datos. |
| Integration / ExternalProvider | **502** | Falla de un sistema externo (SRI). |
| RateLimit | **429** | Límite de tasa excedido. |
| InternalError | **500** | Última red de seguridad — nunca debería ser el resultado esperado de una condición de negocio conocida. |

**Regla estructural (corrige el defecto raíz de la auditoría):** este mapeo se implementa **una sola vez**, indexado por categoría, no por una lista de strings hardcodeada en un `switch`. Tanto `ApiResultExtensions.MapFailure` como `ExceptionMiddleware.HandleExceptionAsync` deben consultar la **misma** tabla categoría→HTTP (vía `MessageCatalog`/`ApiResponseCodes` extendido con `Category`), eliminando la duplicación y desincronización documentada en la auditoría (§1, §2.4, §3.2). Ningún módulo nuevo puede introducir un código sin categoría — y por lo tanto sin HTTP status determinado automáticamente.

---

## 10. Entregable 5 — Reglas de Failure Codes

Todo `FailureCode` (constante en `ApiResponseCodes` o su namespace de módulo) debe declarar, en un único lugar (`MessageCatalog`, extendido):

1. **Código estable** — `SCREAMING_SNAKE_CASE`, nunca renombrado una vez en producción (romper esto rompe integraciones y logs históricos).
2. **Mensaje de usuario** (`message.user`) — español, orientado a la corrección, nunca genérico salvo `InternalError`.
3. **Mensaje de desarrollador** (`message.dev`) — contexto técnico, solo visible en Development.
4. **Categoría** (Entregable 3) — de la cual se deriva `severity` y HTTP status automáticamente.
5. **Prueba** — al menos un test (unitario o de integración) que ejercite el código y confirme HTTP status + forma del envelope. Sigue el patrón ya existente en `ERP.API.Tests\ExceptionMiddlewareValidationTests.cs`.

**Nunca permitido:**

- **Unmapped response code.** Un `Result<T>.Conflict("mensaje", "UN_CODIGO_NUEVO")` sin entrada en `MessageCatalog`/`ApiResponseCodes` debe **fallar en build o en CI**, no degradarse silenciosamente a `INTERNAL_ERROR`/400 genérico en runtime. Esto reemplaza el comportamiento actual (`MessageCatalog.Resolve` con fallback silencioso) por un fail-fast real.
- Un código raíz (`string` literal) pasado directamente a un factory method de `Result<T>` sin pasar por una constante de `ApiResponseCodes`. Esta es la causa directa de `SKU_DUPLICATE`/`BARCODE_DUPLICATE`/`IDENTIFICATION_DUPLICATE`/`PERIOD_NOT_OPEN`/`RULE_NOT_FOUND` quedando fuera del alcance del test de arquitectura existente (`MessageCatalog_has_exactly_one_entry_per_ApiResponseCode`, que solo recorre símbolos de `ApiResponseCodes` y por tanto no ve literales sueltos).
- Dos códigos distintos con el mismo significado de negocio en dos módulos (ej. duplicar `IDENTIFICATION_DUPLICATE` con otro nombre en un módulo nuevo cuando el concepto ya existe).

**Regla de extensión (ya documentada, hasta hoy sin adopción real — se retoma en la migración):** cada dominio nuevo agrega su propia clase anidada en `ApiResponseCodes` (`ApiResponseCodes.Items`, `ApiResponseCodes.Purchases`, `ApiResponseCodes.Accounting`, …), nunca strings sueltos en el handler.

---

## 11. Entregable 6 — Arquitectura Frontend

```
Axios (transporte puro, sin interpretar negocio)
    ↓  (error crudo de Axios, con o sin response)
normalizeApiError(err) → ApiError            [ÚNICO punto de parsing de response.data]
    ↓
    ├── applyServerErrors<T>(apiError, setError)     → errores de campo (categoría Validation, RHF)
    └── getApiErrorMessage(apiError, labels)         → mensaje general (fallback / no-field)
    ↓
Componentes de presentación (ZHFormAlert / ZHToast / ZHPageNotice / modal)
```

**Ningún componente puede interpretar directamente `response.data`.** Esta regla, hoy solo aplicada a errores 422 (F-V5/F-V6 de `CLAUDE.md`), se **extiende a toda forma de error** — es exactamente la violación encontrada en 6+ páginas fuera del módulo Items (Finance, MasterData, Pricing, Sales) y dentro de él (`ItemTypesPage.tsx`).

### Responsabilidades

| Pieza | Responsabilidad | Estado actual | Decisión |
|---|---|---|---|
| `api.ts` (interceptor Axios) | Transporte, refresh de token, correlationId/headers. **Nunca** formatea mensajes de negocio. | Correcto hoy — no toca `response.data` de negocio. | Se mantiene sin cambios. |
| `normalizeApiError` | Punto único que convierte `unknown` (error de Axios) en un tipo `ApiError` tipado y exportado (`code`, `severity`, `message`, `fieldErrors`, `status`, `correlationId`). | **No existe como tipo compartido.** La lógica está dispersa dentro de `apiError.ts` sin un tipo `ApiError` exportado — cada consumidor re-declara su propio shape inline. | **Crear** `ApiError` (tipo) + `normalizeApiError()` (función) en `modules/lib/apiError.ts`, formalizando la lógica ya correcta de `readApiErrorMessage`/`parseValidationErrors`, sin reescribirla desde cero. |
| `applyServerErrors<T>` | Mapea errores de campo (categoría `Validation`) a RHF. | Correcto y ya documentado en `CLAUDE.md`. Alcance limitado a 422 con `data.errors` como mapa — **correcto por diseño**, no un defecto: campo-a-campo solo tiene sentido para esa forma. | Se mantiene; pasa a recibir `ApiError` en vez de `unknown` una vez exista el tipo. |
| `formatApiRequestError` | Mensaje general de fallback cuando no hay campo específico (todas las categorías no-`Validation`, y el caso `_` de `Validation`). | Correcto y es el que ya usan los módulos bien implementados (`items/ItemFormTabs.tsx`, `purchases/ZHItemMatchingPanel.tsx`, `items/CreateItemForm.tsx` tras el fix `008ac6b7`). | **Se declara función canónica única.** Se renombra conceptualmente a "el formateador" del contrato (el nombre de archivo/función puede conservarse por compatibilidad — decisión de implementación en Fase 2). |
| `formatApiError.ts` (competidor huérfano) | Formateador i18n alternativo, sin consumidores confirmados, con reglas distintas (502/503/504 especiales, sin mensaje 401 hardcodeado). | Riesgo de drift — dos utilidades con el mismo propósito y comportamiento distinto. | **Deprecar y eliminar en Fase 2**, migrando cualquier consumidor real (si aparece) a `formatApiRequestError`/`normalizeApiError`. |
| `ZHToast` / `ZHFormAlert` / `ZHPageNotice` | Presentación pura. Reciben un mensaje ya resuelto (string) o un `ApiError`, nunca el error crudo de Axios. | Correcto en su implementación interna; el problema está en **quién las alimenta** (6+ páginas les pasan strings armados a mano con parsing propio en vez de pasar por el formateador único). | Se mantienen sin cambios de implementación; se refuerza la regla de que solo reciben salida de `normalizeApiError`/`formatApiRequestError`/`applyServerErrors`. |

---

## 12. Entregable 7 — UX: cuándo usar cada canal

No todos los errores se presentan igual. Regla de decisión:

| Situación | Canal | Componente | Ejemplo |
|---|---|---|---|
| Error de campo específico (categoría `Validation` con `data.errors` como mapa) | Bajo el campo del formulario | RHF + `applyServerErrors` | "El RUC debe tener 13 dígitos." |
| Error de envío del formulario sin campo específico (`BusinessRule`, `Duplicate` sin mapa) | Banner dentro del formulario/modal, visible sin scroll | `ZHFormAlert` (`type="error"`, `root` de RHF) | "El SKU ya existe en el catálogo de ítems." |
| Falla de una acción secundaria que no bloquea el flujo principal (activar/desactivar, guardar en background) | Notificación flotante, no bloqueante | `ZHToast` (`message.error`) | "No se pudo cambiar el estado del tipo de ítem." |
| Confirmación de éxito de una mutación | Notificación flotante | `ZHToast` (`message.success`) | "Item creado correctamente." |
| Falla al cargar los datos de una página completa (no se puede continuar) | Banner de página, con `role="alert"` | `ZHPageNotice` | "No se pudieron cargar las líneas del comprobante." |
| Error que exige una decisión explícita del usuario antes de continuar (pérdida de datos, conflicto de concurrencia que requiere recargar) | Modal bloqueante | `message.confirm()` / `ZHModal` | "Este registro fue modificado por otro usuario. ¿Recargar?" |
| Falla de red (sin `response`) | Igual que falla de acción — toast o banner según si bloquea o no | según contexto | "Sin conexión. Intenta nuevamente." |

**Regla dura:** ninguna acción de mutación (crear/editar/eliminar/activar/desactivar) puede terminar en un `catch` vacío. La auditoría encontró exactamente este patrón en Finance, MasterData y Sales (toggles y refrescos de lista) — queda prohibido a partir de este ADR (ver `AI-RULES/ERROR-HANDLING.md`).

---

## 13. Entregable 8 — Logging

| Audiencia | Contenido | Dónde |
|---|---|---|
| **Usuario** | Exclusivamente `message.user` — comprensible, orientado a la acción. Nunca stack traces, nunca nombres de constraints SQL, nunca excepciones .NET. | UI (toast/banner/campo/modal) |
| **Desarrollador** | `message.dev`, `correlationId`, `traceId` (cuando exista), stack trace (solo en logs de servidor, nunca en la respuesta HTTP fuera de Development). | Consola de desarrollo (`logApiDevError` en frontend, ya implementado), logs estructurados de backend (Serilog/lo que corresponda), nunca la respuesta al cliente en Production. |

**Regla dura:** nunca mezclar ambos canales. Un mensaje que contiene simultáneamente `message.user` y detalle técnico (ej. "El SKU ya existe (constraint ix_items_sku_tenant, Npgsql error 23505)") es un defecto — viola tanto el principio 3 (Fail Fast/no exponer técnico) como la separación de audiencias. `correlationId` es el nexo obligatorio entre ambos logs: todo log de error, en ambos lados, debe incluirlo para poder correlacionar un reporte de usuario con el log de servidor correspondiente.

---

## 14. Entregable 9 — Accesibilidad

- Todo error visible debe tener **texto**, no solo color — un borde/fondo rojo sin ícono ni texto no es una notificación de error válida.
- Errores bloqueantes (`ZHFormAlert`/`ZHPageNotice` en modo error, o field errors) usan `role="alert"` + `aria-live="assertive"` — patrón ya correctamente implementado en `ZHPageNotice`, que debe extenderse formalmente a `ZHFormAlert`.
- Errores no bloqueantes/informativos (`ZHToast` info/success) usan `role="status"` + `aria-live="polite"`.
- El mensaje debe indicar la **acción esperada** cuando sea posible ("Verifica el RUC ingresado", no solo "Dato inválido").
- Al fallar el envío de un formulario, el foco debe moverse al primer campo inválido o al banner raíz — evita que un lector de pantalla no anuncie el error porque el foco no se movió.

---

## 15. Entregable 11 — Plan de migración (solo diseño, no implementar en esta fase)

### Fase 1 — Backend

- Extender `MessageCatalog`/`ApiResponseCodes` para que cada entrada declare `Category` (Entregable 3) junto a `Severity`/`User`/`Dev`.
- Reescribir `ApiResultExtensions.MapFailure` y `ExceptionMiddleware.HandleExceptionAsync` para derivar el HTTP status de la **categoría**, no de un switch de strings/tipos hardcodeado — única fuente de verdad (Entregable 4).
- Registrar en `ApiResponseCodes` (con clases anidadas por módulo) los códigos hoy huérfanos: `SKU_DUPLICATE`, `BARCODE_DUPLICATE`, `SUPPLIER_CODE_DUPLICATE`, `IDENTIFICATION_DUPLICATE`, `PERIOD_NOT_OPEN`, `RULE_NOT_FOUND`.
- Corregir `BulkMatchItemsHandler` para que fallas individuales dentro de un lote sigan el mismo contrato de envelope (o se documente explícitamente como una forma reconocida adicional — decisión a tomar en Fase 1, no en este ADR).
- Corregir el endpoint `PaymentTermsController.List`/`.Update` para pasar por `ResponseFactory`/`ApiResultExtensions` como el resto de controllers.
- Ampliar el test de arquitectura `MessageCatalog_has_exactly_one_entry_per_ApiResponseCode` para que además falle si detecta un string literal `SCREAMING_SNAKE_CASE` pasado directamente a un factory method de `Result<T>` fuera de `ApiResponseCodes`.

### Fase 2 — Frontend

- Crear el tipo `ApiError` y la función `normalizeApiError()` en `modules/lib/apiError.ts`, formalizando (sin reescribir) la lógica ya correcta de `readApiErrorMessage`/`parseValidationErrors`.
- Migrar las 6+ páginas identificadas (Finance `CreditTermsPage`, MasterData `PaymentTermsPage`, Pricing `PriceListsPage`, Sales `PaymentMethodsPage`, Items `ItemTypesPage`, Auth `CompanySelectPage`) al patrón `applyServerErrors` + `formatApiRequestError`, eliminando el parsing manual de `response.data`.
- Eliminar los `catch` vacíos encontrados (toggles y refrescos de lista en Finance/MasterData/Sales), reemplazándolos por `message.error(...)` como mínimo.
- Deprecar y eliminar `formatApiError.ts` tras confirmar ausencia de consumidores reales.
- Extender `role`/`aria-live` de `ZHPageNotice` a `ZHFormAlert` (Entregable 9).

### Fase 3 — Tests

- Test de contrato por `FailureCode`: cada entrada de `MessageCatalog` tiene al menos un test que confirma HTTP status + forma de envelope (extensión del patrón ya usado en `ExceptionMiddlewareValidationTests.cs`).
- Test de arquitectura backend que impida un `Result<T>.Conflict/.ValidationFailure/.NotFound/.Failure` con código no registrado.
- Test de arquitectura/lint frontend (ESLint rule, siguiendo el precedente de la regla que bloquea `_internal/messageStore` en ADR-018) que impida importar `error.response.data` fuera de `modules/lib/apiError.ts`.

### Fase 4 — Documentación

- Publicar `AI-RULES/ERROR-HANDLING.md` (Entregable 12, este ADR lo entrega ya).
- Actualizar `AI-RULES/BACKEND-RULES.md`/`FRONTEND-RULES.md` para referenciar el archivo canónico.
- Agregar entradas B-xx/F-xx correspondientes a `AI-RULES/PR-RULES-CATALOG.md` una vez la migración esté implementada (no en esta fase, ya que el catálogo B-xx/F-xx es para reglas ya exigibles en CI, y las fases 1-3 aún no están implementadas).

---

## 16. Restricciones de esta fase

Esta fase es exclusivamente de arquitectura, auditoría y documentación. **No se modificó** lógica de negocio, endpoints, DTOs, entidades ni casos de uso. Los únicos artefactos producidos son documentales: este ADR, la auditoría (`docs/architecture/ERROR-HANDLING-AUDIT.md`) y las reglas canónicas (`AI-RULES/ERROR-HANDLING.md`), más las referencias mínimas agregadas a `AI-RULES/README.md`, `BACKEND-RULES.md` y `FRONTEND-RULES.md` para que la nueva regla sea descubrible.

---

## 17. Alternativas descartadas

| Alternativa | Razón de descarte |
|---|---|
| Adoptar `ProblemDetails` (RFC 7807) como envelope oficial, reemplazando el actual | El envelope actual (`code`/`severity`/`message.user`/`message.dev`/`data`/`meta`) ya resuelve lo que `ProblemDetails` resuelve y además separa explícitamente audiencia usuario/desarrollador, algo que `ProblemDetails` no modela nativamente. Migrar el contrato completo es una ruptura innecesaria; ya existe soporte legado a `ProblemDetails` como fallback de parsing en el frontend (interoperabilidad), no como contrato primario. |
| Usar una librería de "Result" de terceros (ej. `FluentResults`, `ErrorOr`) en vez del `Result<T>` propio | `Result<T>` ya está extendido por todo el codebase (decenas de handlers); reemplazarlo es una migración masiva sin beneficio claro sobre corregir su cobertura de categorías/HTTP. |
| Enum C# para `FailureCode` en vez de constantes string en `ApiResponseCodes` | Un enum obliga a recompilar el backend completo para agregar un código de un módulo nuevo, y no serializa naturalmente como el string ya consumido por el frontend y por logs históricos. Las constantes string organizadas en namespaces por módulo (patrón ya iniciado, solo sin adopción real) logran el mismo objetivo de catálogo cerrado sin ese costo. |
| Middleware único que también reemplace `ApiResultExtensions.MapFailure` (colapsar los dos mecanismos en uno) | Deseable a mediano plazo, pero cambia la forma en que **todos** los controllers devuelven resultados hoy (una migración de superficie amplia). La Fase 1 de este ADR corrige la *fuente* del defecto (tabla categoría→HTTP única) sin forzar esa refactorización estructural mayor en la misma fase. |

---

## 18. Criterio de aceptación de este ADR

- [x] ¿Cómo se representa un error desde el dominio hasta la UI? — §6, §7.
- [x] ¿Qué responsabilidad tiene cada capa? — §6.
- [x] ¿Qué código HTTP corresponde a cada tipo de error? — §9.
- [x] ¿Cómo se garantiza que todo `FailureCode` tenga mensajes adecuados para usuario y desarrollador? — §10.
- [x] ¿Cómo debe consumir el frontend los errores sin interpretar directamente la respuesta HTTP? — §11.
- [x] ¿Cómo se presentan los errores al usuario según el contexto? — §12.
- [x] ¿Cuál es el plan para migrar gradualmente el ERP sin romper compatibilidad? — §15.

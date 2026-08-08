# ADR-028: Recepción XML de Compras → Compra — Cierre de Flujo (FROZEN)

## Status

**Accepted — Frozen.** 2026-07-28. El flujo funcional que conecta la Recepción XML de comprobantes electrónicos del SRI con la creación de una Compra (`PurchaseInvoice`) queda cerrado y congelado. Ningún refactor futuro puede modificar el flujo visible del usuario, la separación XML/Snapshot/Compra, ni los contratos públicos documentados aquí sin una nueva ADR.

## Contexto

El ERP recibe comprobantes de compra ya autorizados por el SRI (Factura, esquema offline) a través de un archivo TXT de cabeceras (`ImportPurchaseReceptionCommand`). Para cada cabecera, el sistema consulta el SRI, descarga el XML autorizado y lo persiste (`DownloadPurchaseReceptionXmlHandler`). Ese XML es, simultáneamente:

- **Evidencia fiscal**: el documento legal que el SRI autorizó, con su clave de acceso y número de autorización — no debe modificarse jamás.
- **Origen de datos operativos**: cada `<detalle>` del XML describe una línea de mercadería/servicio que, eventualmente, el usuario querrá convertir en una Compra dentro del ERP.

Estas dos necesidades (preservar evidencia legal íntegra vs. operar con esos datos en Compras, Inventario, Item Matching y, a futuro, Cuentas por Pagar y Contabilidad) requerían una decisión explícita sobre **quién es dueño de qué dato, en qué momento se interpreta el XML, y qué relación existe entre el comprobante recibido y la Compra que el usuario finalmente confirma.**

Sin esta decisión, cada extensión futura (nuevo campo tributario, nuevo estado de Item Matching, reintento de parsing, etc.) corría el riesgo de reintroducir parseo de XML en capas equivocadas, perder líneas sin match, o acoplar la Compra a la disponibilidad del XML crudo.

## Problema

1. ¿El XML se reinterpreta cada vez que se necesita el detalle de una recepción, o se interpreta una única vez y se persiste?
2. ¿Qué ocurre con una línea del XML cuyo proveedor no tiene un Item asociado en el catálogo? ¿Se descarta, se oculta, o se conserva?
3. ¿Dónde vive el Item Matching — en la Compra, en la Recepción, o en ambos?
4. ¿Qué pasa si el parseo del XML falla parcial o totalmente — se bloquea la verificación fiscal del comprobante, se pierde el documento, o se separan ambos ejes?
5. ¿Cómo se recupera un documento cuyo detalle nunca se pudo interpretar (snapshot ausente o inconsistente) sin exponer al usuario una acción técnica ("reprocesar", "reconstruir snapshot") ajena al vocabulario de negocio?
6. ¿Qué relación estructural existe entre `PurchaseReceptionDocument` y `PurchaseInvoice` — una FK obligatoria, una referencia opcional, o ninguna relación en absoluto?

## Decisión

Se adopta y congela el siguiente modelo:

### 1. El XML es evidencia fiscal inmutable

`PurchaseReceptionDocument` conserva `XmlContent`, `AuthorizationNumber`, `AuthorizationDate`, `AccessKey`, proveedor (`SupplierId`/RUC/razón social), cabecera (`DocTypeCode`, `SriPaymentMethodCode`, fecha de emisión), y dos ejes de estado independientes:

- `Status` (`PurchaseReceptionDocumentStatus`: `Imported → Verified → Processed` / `Cancelled`) — ciclo de vida fiscal.
- `ProcessingStatus` (`PurchaseReceptionProcessingStatus`: `Pending`, `Processed`, `ProcessedWithWarnings`, `Failed`) — si pudimos interpretar el **contenido** del XML ya autorizado.

Un documento puede estar `Verified` (fiscalmente válido, autorizado por el SRI) con `ProcessingStatus.Failed` (detalle no interpretable) al mismo tiempo — la validez fiscal del comprobante nunca depende de la capacidad del ERP de parsear su detalle. `XmlContent` no se modifica una vez guardado.

### 2. `PurchaseReceptionLine` es el Snapshot Operativo

Cada `<detalle>` del XML produce exactamente una `PurchaseReceptionLine`, con el snapshot completo del proveedor: `Description`, `SupplierCode`, `SupplierAuxCode`, `Quantity`, `UnitPrice`, `VatCode`, `TaxCode`, `VatPercentage`, `TaxValue`, `IceCode`, `IceValue`, `DiscountPct`, `Discount`, `LineSubtotal`, `TotalLine`.

Una línea **nunca** se elimina ni se oculta porque:

- no exista un Item correspondiente en el catálogo,
- el Item Matching no encuentre coincidencia,
- exista cualquier otro problema interno de conciliación.

La única causa legítima para que una línea individual no se persista es que el propio `<detalle>` del XML sea inválido a nivel de datos (p. ej. cantidad cero) — ese caso queda registrado en `ProcessingNotes`, nunca silenciado.

### 3. El Snapshot es la única fuente operativa

Toda lógica posterior al procesamiento inicial —Item Matching manual/masivo, construcción del borrador de Compra— opera exclusivamente sobre `PurchaseReceptionDocument` + `PurchaseReceptionLine`. Ninguna capa de negocio vuelve a leer `XmlContent` directamente; el XML persistido existe únicamente para auditoría, evidencia fiscal y como insumo de la reconstrucción interna descrita en la sección "Recuperación".

### 4. Item Matching ocurre una única vez, al construir el Snapshot

`IPurchaseReceptionDetailProcessor` (`ERP.Application.Modules.Purchases.PurchaseReception.Services`) es la única implementación de "XML → líneas persistibles + Item Matching resuelto": parsea el XML (`IPurchaseXmlDraftParser`), resuelve conciliación automática por código de proveedor exacto (`IItemRepository.FindItemIdBySupplierCodeAsync`) y marca sugerencias por similitud (`IItemMatchFinder`) cuando no hay código exacto. Se invoca desde `DownloadPurchaseReceptionXmlHandler` (primera descarga) y, de forma transparente, desde `CreatePurchaseReceptionDraftHandler` (ver "Recuperación"). Item Matching **no** vuelve a ejecutarse al abrir Compras, al construir el borrador, ni al guardar `PurchaseInvoice`.

### 5. `CreatePurchaseReceptionDraftHandler` nunca reinterpreta el XML del lado del usuario

Arma `PurchaseDraftDto` exclusivamente desde el `PurchaseReceptionDocument` ya `Verified` y sus `PurchaseReceptionLine` persistidas — cabecera SRI y líneas (incluido `ItemId`/`MatchStatus` ya resueltos) se leen tal cual quedaron guardadas en la descarga. El único parseo adicional que este handler puede disparar es la reconstrucción interna descrita en "Recuperación", nunca expuesta como una operación distinta.

### 6. `PurchaseDraftDto`/`PurchaseDraft` (modelo de aplicación) nunca se persiste

Es un modelo temporal, calculado en memoria en cada invocación de `create-draft`, usado únicamente para precargar el formulario de Compras. No tiene tabla propia ni entidad de dominio persistida.

### 7. `PurchaseInvoice` únicamente existe cuando el usuario confirma la Compra

La Compra (`PurchaseInvoice`, vía `CreatePurchaseDraftCommand`/`CreatePurchaseDraftHandler` en `ERP.Application.Modules.Purchases.UseCases.PurchaseDraftUseCases`) se crea exclusivamente cuando el usuario guarda el formulario ya precargado. Ningún paso anterior (importar TXT, descargar XML, abrir el formulario) crea una Compra.

## Arquitectura

| Capa | Responsabilidad en este flujo |
|---|---|
| `ERP.Domain.Modules.Purchases.PurchaseReception` | `PurchaseReceptionDocument` (agregado raíz, máquina de estados fiscal + de procesamiento), `PurchaseReceptionLine` (snapshot inmutable + conciliación mutable), enums `PurchaseReceptionDocumentStatus`/`PurchaseReceptionProcessingStatus`/`ItemMatchStatus`, modelo `PurchaseReceptionProcessingOutcome`. Sin dependencias externas. |
| `ERP.Application.Modules.Purchases.PurchaseReception` | `IPurchaseReceptionDetailProcessor` (único traductor XML→snapshot+matching), `IPurchaseXmlDraftParser` (parseo puro de XML), UseCases (`ImportPurchaseReception`, `DownloadPurchaseReceptionXml`, `CreatePurchaseReceptionDraft`), DTOs de solo lectura hacia el frontend. |
| `ERP.Application.Modules.Purchases.UseCases.PurchaseDraftUseCases` | `CreatePurchaseDraftHandler` — construye y persiste `PurchaseInvoice` real. Consumidor del `PurchaseDraftDto` solo a través de la precarga del formulario en el frontend; no tiene dependencia de compilación hacia `PurchaseReception`. |
| `ERP.Infrastructure.Modules.Purchases.PurchaseReception` | `SriReceptionXmlProvider` (consulta al SRI), `PurchaseXmlDraftParser` (implementación concreta), persistencia EF de `PurchaseReceptionDocument`/`PurchaseReceptionLine`. |
| `ERP.API.Controllers.Purchases.PurchaseReceptionController` | Expone los UseCases vía HTTP, sin lógica de negocio propia. |

Dirección de dependencias: `API → Application → Domain`, `Infrastructure → Application` (implementa sus interfaces). El módulo de Recepción XML no depende de Compras; Compras no depende de Recepción XML a nivel de compilación — la única conexión es el DTO de precarga (`PurchaseDraftDto`) consumido por el frontend, que copia sus valores a los campos del formulario de Compras antes de guardar.

## Flujo

Flujo funcional oficial, congelado — ningún cambio técnico puede alterar esta secuencia visible para el usuario:

```
Recepción XML (importar TXT)
   ↓
Descargar XML  →  PurchaseReceptionDocument (Verified) + PurchaseReceptionLine[] (Snapshot + Item Matching)
   ↓
Crear Compra   →  abre el formulario de Compras (nunca crea una PurchaseInvoice todavía)
   ↓
Formulario de Compras precargado  →  GET create-draft arma PurchaseDraftDto desde el Snapshot
   ↓
Guardar Compra  →  se crea PurchaseInvoice (recién en este paso)
```

Un único botón "Crear Compra" existe para todo documento `Verified`, sin variantes de label ni de comportamiento visible según el estado interno de procesamiento (ver "Recuperación").

## Responsabilidades

| Responsable | Obligación |
|---|---|
| `PurchaseReceptionDocument` | Custodiar el XML autorizado sin modificarlo; exponer los dos ejes de estado (fiscal/procesamiento) de forma independiente; ser la única puerta de entrada para adjuntar autorización (`AttachSriAuthorization`) o reconstruir el detalle (`ReprocessDetail`). |
| `PurchaseReceptionLine` | Representar exactamente una línea del XML; exponer únicamente `AutoMatch`/`ManualMatch`/`MarkNeedsReview` como mecanismo de mutación de la conciliación — el resto de sus campos es de solo lectura tras `Create`. |
| `IPurchaseReceptionDetailProcessor` | Única lógica de interpretación XML→snapshot+matching, reutilizada por descarga inicial y reconstrucción interna — nunca duplicada. |
| `DownloadPurchaseReceptionXmlHandler` | Consultar el SRI, guardar el XML y persistir el snapshot inicial en una única operación atómica. |
| `CreatePurchaseReceptionDraftHandler` | Construir el `PurchaseDraftDto` desde el snapshot persistido; ejecutar (solo si `ProcessingStatus == Failed`) la reconstrucción interna descrita abajo; nunca crear `PurchaseInvoice`. |
| Frontend (`PurchaseReceptionPage`, `PurchasesPage`) | Mantener el flujo visible de 3 pasos; nunca exponer conceptos internos (reprocesar, snapshot, processing status) como acciones separadas del usuario. |
| Usuario | Confirmar explícitamente la Compra — ningún paso previo genera una `PurchaseInvoice` sin esa confirmación. |

## Reglas

- La información proveniente del XML es **inmutable** tras `PurchaseReceptionLine.Create`: `Description`, `SupplierCode`, `SupplierAuxCode`, `Quantity`, `UnitPrice`, `VatCode`, `IceCode`, `TaxCode`/`TaxValue`/`VatPercentage`, `Discount`/`DiscountPct`, `LineSubtotal`, `TotalLine` nunca cambian después de persistidas.
- Los únicos campos que pueden evolucionar en una línea ya persistida son `ItemId`, `MatchStatus` (`ItemMatchStatus`), `MatchedBy`, `MatchedAt` — exclusivamente a través de `AutoMatch`/`ManualMatch`/`MarkNeedsReview`.
- Nunca se elimina ni oculta una `PurchaseReceptionLine` por ausencia de Item o fallo de matching.
- `CreatePurchaseReceptionDraftHandler` nunca vuelve a parsear el XML durante la carga normal (`ProcessingStatus` distinto de `Failed`) — cero costo de parseo adicional en el camino feliz.
- La reconstrucción automática del snapshot solo puede dispararse cuando `ProcessingStatus == Failed`. Nunca en `Processed` ni en `ProcessedWithWarnings` — verificado por tests dedicados (`Handle_never_reconstructs_a_document_that_is_already_processed`, `Handle_never_reconstructs_a_document_with_processing_warnings`).
- Toda reconstrucción exitosa se persiste de inmediato (mismo `SaveChangesAsync` que actualiza `PurchaseReceptionLine`, `ProcessingStatus`, `LinesDetectedCount`/`LinesProcessedCount` y `ProcessingNotes`) — la siguiente invocación encuentra el snapshot ya reparado y no vuelve a reconstruir (verificado por `Handle_reconstructs_only_once_and_loads_directly_from_the_repaired_snapshot_on_a_second_call`).
- Si la reconstrucción falla (el XML sigue sin poder interpretarse), no se crea `PurchaseDraftDto`, no se abre el formulario con datos vacíos, y el usuario recibe un mensaje funcional claro con el motivo (`ProcessingNotes`).
- No existen endpoints públicos, comandos ni botones dedicados a "reprocesar" — la recuperación es exclusivamente interna a `create-draft`.
- Ningún módulo distinto de `PurchaseReception` interpreta XML de comprobantes de compra.

## Consecuencias

**Positivas:**

- La Compra puede precargarse de forma instantánea en el camino feliz (sin costo de parseo XML), y de forma resiliente en el camino de recuperación (sin exponer complejidad técnica al usuario).
- Ninguna línea del proveedor se pierde jamás, independientemente del estado del catálogo de Items en el momento de la recepción — Item Matching es un proceso que puede completarse después, sin bloquear la evidencia fiscal ni el snapshot operativo.
- El XML autorizado queda disponible indefinidamente como evidencia ante una auditoría SRI, sin riesgo de alteración por lógica de negocio.

**Negativas / deuda aceptada conscientemente:**

- `PurchaseReceptionDocument.MarkProcessed(purchaseId, updatedBy)` — el método de dominio que vincula el documento de recepción con la `PurchaseInvoice` resultante y transiciona `Status` a `Processed` — existe en el agregado pero **no tiene ningún invocador hoy**: `CreatePurchaseDraftCommand` (creación real de `PurchaseInvoice`) no recibe ni persiste un `PurchaseReceptionDocumentId`. La vinculación es hoy exclusivamente de UI (parámetro `fromReceptionId` en la URL, usado solo para precargar el formulario). Esto significa que, en el estado actual, ningún documento de recepción llega realmente a `Status = Processed` ni queda formalmente enlazado a su compra. Ver "Consideraciones futuras".

## Riesgos

- **Documentos de recepción huérfanos en `Verified`**: sin la vinculación real de `MarkProcessed`, es posible generar múltiples compras desde el mismo documento de recepción sin que el sistema lo detecte o lo impida — no existe hoy una regla de negocio que lo prevenga a nivel de dominio.
- **Snapshots parcialmente reconstruidos con datos históricos anómalos**: si un documento antiguo, previo a la introducción del snapshot tributario completo (`VatCode`/`TaxCode`/etc.), llega a `ProcessingStatus.Failed` por una causa no relacionada con el parser, la reconstrucción transparente podría no resolver el problema real y devolver el mismo error indefinidamente — mitigado porque el error se comunica siempre de forma explícita, nunca silenciosa.
- **Evolución del parser sin control de versión explícito**: la reconstrucción depende de que `IPurchaseXmlDraftParser` haya mejorado desde el intento original; no existe un mecanismo de versión de parser que permita diagnosticar *por qué* una reconstrucción tuvo éxito o siguió fallando, más allá de `ProcessingNotes`.

## Beneficios

- Separación de responsabilidades verificable en tiempo de compilación: `Application.Modules.Purchases.UseCases` no referencia `PurchaseReception`, y `PurchaseReception` no referencia `PurchaseInvoice`.
- Recuperación transparente sin deuda técnica de UX: no existen botones, labels ni endpoints que expongan al usuario el concepto de "reprocesamiento".
- Extensibilidad hacia Cuentas por Pagar y Contabilidad sin reabrir este flujo: ambos módulos futuros consumirán `PurchaseInvoice` ya confirmada, nunca `PurchaseReceptionDocument`/`PurchaseReceptionLine` directamente — el snapshot de recepción queda fuera de su superficie de integración.
- Testabilidad explícita de las reglas más sensibles (gate de `ProcessingStatus == Failed`, no-repetición de la reconstrucción, rechazo sin draft vacío) mediante tests unitarios dedicados, no solo por inspección de código.

## Restricciones

- No se permite reintroducir el parseo de XML en `CreatePurchaseReceptionDraftHandler` para el camino normal (`Processed`/`ProcessedWithWarnings`).
- No se permite agregar un segundo servicio de interpretación de XML de recepción — `IPurchaseReceptionDetailProcessor` es la única implementación autorizada.
- No se permite exponer un endpoint, comando o botón de "reprocesar"/"reconstruir snapshot" como acción distinta de "Crear Compra".
- No se permite eliminar u ocultar una `PurchaseReceptionLine` por ausencia de Item o fallo de Item Matching.
- No se permite modificar los campos inmutables de `PurchaseReceptionLine` (listados en "Reglas") fuera del factory `Create`.
- No se permite que `PurchaseDraftDto`/`PurchaseDraft` (modelo de aplicación) se persista como entidad propia.
- No se permite que la reconstrucción automática se dispare fuera de `ProcessingStatus == Failed`.

## Impacto sobre módulos existentes

| Módulo | Impacto |
|---|---|
| **Compras** | Consume `PurchaseDraftDto` únicamente como precarga de formulario; la creación real de `PurchaseInvoice` (`CreatePurchaseDraftHandler`) es independiente y no referencia `PurchaseReception` a nivel de compilación. Sin impacto en su ciclo de vida propio (`Draft`/`Confirmed`, retenciones, costeo). |
| **Recepción XML** | Es el módulo cuyo diseño se congela con esta ADR — ver todas las secciones anteriores. |
| **Item Matching** | Su ejecución queda anclada exclusivamente al momento de construcción del snapshot (`IPurchaseReceptionDetailProcessor`) y a la confirmación manual/masiva posterior (`MatchItemCommand`/`BulkMatchItemsCommand`) sobre líneas ya persistidas — sin cambios a su motor de sugerencias (`IItemMatchFinder`). |
| **Inventario** | Sin impacto directo hoy — el ingreso de stock se origina desde `PurchaseInvoice` confirmada, no desde `PurchaseReceptionDocument`. Cualquier automatización futura de recepción física de mercadería debe consumir la Compra confirmada, nunca el snapshot de recepción XML. |
| **Cuentas por Pagar (futuro)** | Consumirá `PurchaseInvoice` confirmada — el snapshot de recepción XML queda fuera de su superficie de integración por diseño. |
| **Contabilidad (futuro)** | Igual criterio que Cuentas por Pagar: el asiento contable se genera desde `PurchaseInvoice`, nunca desde `PurchaseReceptionDocument`/`PurchaseReceptionLine`. |

Compatibilidad confirmada con los principios ya vigentes del proyecto: **Clean Architecture** (dependencias unidireccionales `API → Application → Domain`, `Infrastructure` implementando `Application`), **CQRS con MediatR** (cada paso del flujo es un `IRequest`/`IRequestHandler` independiente), **DDD** (`PurchaseReceptionDocument` como agregado raíz con invariantes propias, `PurchaseReceptionLine` como entidad hija sin identidad fuera de su agregado), **Multi-Tenant** (`TenantId` en ambas entidades, `ICurrentTenant` en todos los handlers).

## Consideraciones futuras

- **Cerrar el vínculo `PurchaseReceptionDocument` ↔ `PurchaseInvoice`**: extender `CreatePurchaseDraftCommand`/`CreatePurchaseDraftHandler` para recibir opcionalmente un `PurchaseReceptionDocumentId` y, tras persistir la `PurchaseInvoice`, invocar `PurchaseReceptionDocument.MarkProcessed(purchaseId, updatedBy)`. Esto resolvería la deuda aceptada de la sección "Consecuencias" y el riesgo de documentos huérfanos/duplicados de la sección "Riesgos". No es parte del alcance de esta ADR — es una extensión aditiva que no modifica el flujo visible ya congelado.
- **Regla de negocio "un documento de recepción, una compra"**: una vez cerrado el vínculo anterior, evaluar si `CreatePurchaseReceptionDraftHandler` debe rechazar la generación de un nuevo borrador cuando `Status == Processed` (hoy no lo hace explícitamente, dado que ese estado nunca se alcanza en la práctica).
- **Versión de parser explícita en `ProcessingNotes`**: para diagnosticar con precisión por qué una reconstrucción automática tuvo éxito o siguió fallando, sin depender de inspección manual de logs.
- **Extensión a otros tipos de comprobante de compra** (Notas de Crédito/Débito de proveedor, Liquidación de Compra): deben seguir el mismo patrón Snapshot-primero + Item Matching-una-vez ya congelado aquí, nunca reinterpretar XML en la capa de Compras.

Cualquiera de estos puntos, al implementarse, es una extensión aditiva sobre los contratos ya congelados — no requiere reabrir esta ADR salvo que modifique el flujo visible de 3 pasos, la separación XML/Snapshot/Compra, o alguna de las reglas de inmutabilidad listadas arriba.

---

**Esta decisión queda considerada FROZEN y cualquier modificación futura requerirá un nuevo ADR.**

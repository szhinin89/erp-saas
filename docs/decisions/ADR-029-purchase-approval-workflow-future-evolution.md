# ADR-029: Purchase Approval Workflow — Guía de Evolución Futura (No Implementado)

## Status

**Accepted — Guía de diseño, NO implementado.** 2026-07-28. Esta ADR **no introduce código, tablas, migraciones, parámetros ni Feature Flags**. Documenta una auditoría de la arquitectura actual del módulo de Compras y la evolución futura recomendada hacia un flujo de aprobación configurable por empresa. El ERP continúa usando exclusivamente **Compra Directa** (`PurchaseStatus.Draft → Confirmed`) hasta que el ERP Core esté completamente estabilizado, conforme a [ADR-028](./ADR-028-purchase-reception-to-purchase-flow-freeze.md) (FROZEN). Cualquier implementación futura de lo aquí descrito requiere una nueva fase de desarrollo y, si se aparta de esta guía, una nueva ADR.

## Objetivo

Analizar la arquitectura actual del módulo de Compras y documentar cómo debería evolucionar hacia un flujo configurable de aprobación por empresa (Direct / Approval / MultiApproval), de forma que una futura implementación no requiera rediseñar Compras ni romper el flujo ya congelado por ADR-028. El resultado es guía oficial, no una entrega de código.

## Versión Actual (FROZEN)

El flujo oficial del ERP, ya congelado por ADR-028 y extendido aquí hasta su alcance completo actualmente implementado, es:

```
Recepción XML
   ↓
Crear Compra
   ↓
Formulario de Compras
   ↓
Guardar Compra                    → PurchaseInvoice (Status = Draft)
   ↓
Compra Confirmada                 → ConfirmPurchaseHandler.Handle() / PurchaseInvoice.Confirm()
   ↓                                 (Status: Draft → Confirmed, congela costos, PurchaseInvoiceConfirmedEvent)
   ↓
Inventario                        → IStockRepository.AppendMovementAsync (StockMovementType.PurchaseEntry)
   ↓
Cuentas por Pagar                 → PurchasePayable.Create + GenerateInstallments
   ↓
Contabilidad                      → PurchaseInvoiceConfirmedPostingTranslator (consumidor de PurchaseInvoiceConfirmedEvent)
   ↓
PurchaseReceptionDocument.MarkProcessed(...)   [diseñado, sin invocador real — ver ADR-028 "Consecuencias"]
```

Este flujo permanece **congelado**. Ninguna sección de esta ADR lo modifica. `ConfirmPurchaseHandler` ejecuta hoy, en una única transacción imperativa (Steps 1-6): recálculo de impuestos, `PurchaseInvoice.Confirm()`, generación de calendario de pagos, movimientos de inventario, creación de `PurchasePayable`, actualización del precio base del ítem (SSOT de Pricing) y registro de comunicación de seguimiento. Todo eso ocurre **sin ningún punto de espera humano** — es exactamente la propiedad que define "Compra Directa" y la que un flujo de aprobación futuro tendría que insertar como una etapa previa a `Confirm()`, no como una reescritura de estos pasos.

## Alcance de la auditoría

### ¿Qué partes ya están preparadas para soportar un flujo de aprobación?

- **`PurchaseStatus` como enum de dominio, no como bool.** `Draft`/`Confirmed`/`Cancelled` (`ERP.Domain.Modules.Purchases.Enums.PurchaseStatus`) ya modela el ciclo de vida como una máquina de estados explícita, con guardas (`EnsureDraft()`) dentro del propio agregado. Insertar un estado intermedio (`PendingApproval`) es una extensión aditiva del mismo enum, no un rediseño.
- **`PurchaseInvoice.Confirm(Guid updatedBy)` como único punto de transición a `Confirmed`.** Todos los efectos de negocio (congelar costos, calcular totales, `RaiseDomainEvent(PurchaseInvoiceConfirmedEvent)`) están centralizados en este método. Un flujo de aprobación futuro solo necesita decidir **cuándo se invoca `Confirm()`**, no reimplementar lo que hace.
- **Domain Events + Outbox ya desacoplan consumidores del acto de confirmar** (ADR-007, ADR-008). `PurchaseInvoiceConfirmedEvent` ya tiene dos consumidores independientes y sin conocerse entre sí: `PurchaseInvoiceAuditHandler` (Entity Audit, ADR-022) y `PurchaseInvoiceConfirmedPostingTranslator` (Contabilidad). Agregar un tercer consumidor (p. ej. una notificación de aprobación pendiente) no modifica a los otros dos.
- **`org_settings` (`OrgSettingKeys`) ya es el mecanismo genérico de configuración por empresa, con jerarquía Company → Branch → EmissionPoint/Warehouse ya implementada** (ver `OrgSettingKeys.Invoice.*`, "Valores por Defecto de Facturación" en `CLAUDE.md`). Es el punto de extensión natural para `PurchaseWorkflowMode` y sus reglas de aprobación (monto, sucursal, departamento, centro de costo) — no requiere una tabla de configuración nueva desde cero, solo nuevas claves sobre la infraestructura ya existente.
- **`AuditableEntity` + Entity Audit (ADR-022) ya es el patrón congelado para "qué cambió, quién, cuándo".** Un historial de aprobaciones (quién aprobó, cuándo, con qué comentario) es, por diseño, el mismo patrón ya usado por `PricingRuleAudit`/`PriceListItemAudit` — no un mecanismo nuevo.
- **`IBranchScopedRequest`/`ICompanyScopedRequest` (CQRS, ADR-013) ya obligan a todo comando de Compras a declarar su alcance multi-tenant.** Un comando futuro `SubmitPurchaseForApprovalCommand`/`ApprovePurchaseCommand` se integra bajo el mismo contrato, sin infraestructura nueva.
- **`PurchasePermissions` (`purchases.view/create/update`) ya sigue el patrón de permisos por acción, no por rol fijo.** Agregar `purchases.approve` es aditivo sobre un catálogo ya existente (`ERP.Domain.Kernel.Permissions`), no un sistema de permisos paralelo.

### ¿Qué partes deberían evolucionar en el futuro?

- `PurchaseStatus` necesitará un estado intermedio (`PendingApproval`, nombre final a decidir en la fase de implementación) entre `Draft` y `Confirmed`.
- `ConfirmPurchaseHandler` necesitará una guarda adicional: solo puede invocar `PurchaseInvoice.Confirm()` si el modo de workflow vigente es `Direct`, o si el modo es `Approval`/`MultiApproval` y la aprobación ya fue registrada.
- `PurchasePermissions` necesitará `Submit`/`Approve`/`Reject` como acciones nuevas, análogas a `Create`/`Update`.
- `org_settings` necesitará las claves nuevas descritas en "Arquitectura Futura".
- El frontend de Compras necesitará un estado visual nuevo ("Pendiente de aprobación") y una acción condicional ("Enviar a aprobar"/"Aprobar"/"Rechazar"), visibles solo cuando el modo de workflow de la empresa no sea `Direct`.

### ¿Qué componentes NO deben modificarse?

- El flujo Recepción XML → Crear Compra → Formulario precargado, congelado por ADR-028 completo — un workflow de aprobación se inserta **después** de "Guardar Compra", nunca antes.
- `PurchaseReceptionDocument`, `PurchaseReceptionLine`, `IPurchaseReceptionDetailProcessor` — ninguno de estos conoce ni debe conocer el estado de aprobación de la Compra que eventualmente se genere desde su snapshot.
- La lógica interna de `PurchaseInvoice.Confirm()` (congelamiento de costos, cálculo de totales) — el workflow de aprobación decide **cuándo** se llama, nunca **qué hace**.
- `PurchaseInvoiceConfirmedEvent` y sus dos consumidores actuales (`PurchaseInvoiceAuditHandler`, `PurchaseInvoiceConfirmedPostingTranslator`) — deben seguir disparándose únicamente cuando la compra queda efectivamente `Confirmed`, sea cual sea el camino (directo o aprobado) que llevó hasta ahí.
- El motor de Pricing SSOT (`Item.BaseSalePrice`, ADR-021) — la actualización de precio en `ConfirmPurchaseHandler` Step 5 sigue ocurriendo únicamente al confirmar, nunca al enviar a aprobación.

### ¿Qué decisiones ya permiten crecer sin romper la arquitectura?

- CQRS con MediatR (ADR-013): cada transición de estado es un comando independiente: hoy `ConfirmPurchaseCommand`; mañana `SubmitPurchaseForApprovalCommand`, `ApprovePurchaseCommand`, `RejectPurchaseCommand` se agregan sin tocar los existentes.
- Domain Events + Outbox (ADR-007/008): nuevos eventos se suman sin acoplar a los consumidores actuales.
- Entity Audit (ADR-022): open/closed por diseño — un nuevo dominio de auditoría (aprobaciones) se agrega sin tocar la infraestructura base.
- `org_settings` jerárquico: ya soporta añadir claves nuevas sin nueva tabla ni migración de esquema por cada regla de negocio configurable.

### ¿Qué riesgos existirían si se implementara hoy?

- El ERP Core (Recepción XML → Compra, Inventario, Cuentas por Pagar, Contabilidad) todavía está estabilizándose — introducir una ramificación de estados en `PurchaseInvoice` antes de que ese núcleo esté firme multiplicaría la superficie de regresión de cada ajuste futuro al núcleo.
- Sin casos reales de clientes con necesidad de aprobación multinivel todavía en producción, cualquier diseño de reglas (monto, sucursal, departamento, centro de costo, escalonamiento) sería especulativo — alto riesgo de over-engineering y de tener que rediseñar tras el primer cliente real con una necesidad distinta a la anticipada.
- `MarkProcessed` (ver sección dedicada) ya tiene una brecha de implementación conocida (ADR-028) — apilar un workflow de aprobación sobre una vinculación Recepción↔Compra todavía no cerrada aumentaría la superficie de bugs simultáneos a diagnosticar.

### ¿Qué riesgos existirían si se implementara demasiado tarde?

- Si `ConfirmPurchaseHandler` u otros módulos (Inventario, CxP, Contabilidad) llegan a asumir implícitamente "toda compra pasa de Draft a Confirmed sin pasos intermedios" en código nuevo no preparado para un estado adicional, la migración futura tendría que auditar y corregir esas asunciones dispersas en vez de extender un único punto de guarda.
- Si se posterga indefinidamente sin dejar esta guía, el conocimiento de **por qué** la arquitectura actual (enum de estado, evento único de confirmación, `org_settings` jerárquico) ya es compatible con aprobación se pierde con el tiempo, y una futura implementación podría reconstruir desde cero mecanismos que ya existen (el riesgo que esta ADR existe para evitar).

## Análisis funcional

Casos analizados exclusivamente para verificar que la arquitectura actual **permite evolucionar** hacia ellos — ninguno se implementa en esta entrega.

| Caso | Regla de negocio | Compatible con la arquitectura actual vía |
|---|---|---|
| Empresa pequeña | Compra Directa (sin aprobación) | `PurchaseWorkflowMode.Direct` — comportamiento actual, sin cambios |
| Empresa mediana | Un aprobador | `PurchaseWorkflowMode.Approval` + una regla de asignación de aprobador por empresa (`org_settings`) |
| Empresa grande | Múltiples aprobadores | `PurchaseWorkflowMode.MultiApproval` + lista ordenada de aprobadores (o roles) por empresa |
| Aprobación por monto | Umbral(es) que determinan si se requiere aprobación y cuántos niveles | Regla de negocio evaluada en el comando `SubmitPurchaseForApprovalCommand`, parametrizada vía `org_settings` (ver "Arquitectura Futura") — no requiere cambiar `PurchaseInvoice` |
| Aprobación por sucursal | El aprobador o el umbral depende de `BranchId` | Ya existe `BranchId` en `PurchaseInvoice` (Branch Ownership, ver `CORE-ARCHITECTURE.md`) y la jerarquía Company→Branch de `org_settings` ya resuelve overrides por sucursal (mismo patrón que "Valores por Defecto de Facturación") |
| Aprobación por departamento | El aprobador depende de una dimensión organizacional adicional | Requiere que exista (o se modele en su propia fase) un concepto de "departamento" en el ERP — hoy no existe como entidad; **fuera del alcance de esta guía**, ver "Decisiones Diferidas" |
| Aprobación por centro de costo | El aprobador o el reporte depende de un centro de costo | Mismo caso que departamento: depende de una entidad de Contabilidad/Costos que hoy no está definida en el ERP Core — **decisión diferida**, no bloquea el diseño de `PurchaseWorkflowMode` en sí |
| Aprobación escalonada | Secuencia de aprobadores (N niveles, cada uno condicionado al anterior) | `PurchaseWorkflowMode.MultiApproval` + una colección ordenada de niveles de aprobación asociada a la compra (entidad nueva, a diseñar en su fase — ver "Arquitectura Futura" y "Decisiones Diferidas") |

Conclusión de este análisis: **ningún caso exige modificar el núcleo ya congelado** (`PurchaseInvoice.Confirm()`, `PurchaseInvoiceConfirmedEvent`, Inventario, CxP, Contabilidad). Todos se resuelven agregando una etapa previa a `Confirm()`, parametrizada por empresa/sucursal, con nuevas entidades de aprobación aditivas.

## Arquitectura futura (propuesta de evolución — NO implementar)

### `PurchaseWorkflowMode`

```csharp
// Propuesta de diseño — NO crear este enum todavía.
public enum PurchaseWorkflowMode
{
    Direct       = 1,  // comportamiento actual — sin cambios
    Approval     = 2,  // un aprobador
    MultiApproval = 3, // aprobación escalonada / múltiples aprobadores
}
```

**Dónde debería vivir**: como valor de configuración en `org_settings`, bajo una nueva clase `OrgSettingKeys.Purchases` (análoga a `OrgSettingKeys.Invoice`), con `scope=Company` como nivel base y posible override por `Branch` si la fase de implementación confirma esa necesidad real (mismo patrón jerárquico ya usado por defaults de factura). **No** como columna nueva en `PurchaseInvoice` ni en `Company` — el valor de configuración debe leerse en el momento de decidir el camino (enviar a aprobar vs. confirmar directo), nunca copiarse/cachearse dentro de la propia compra.

**Cómo se integraría** (solo diseño, no implementación):

1. Al "Guardar Compra", en vez de invocar directamente `ConfirmPurchaseCommand`, un futuro `SubmitPurchaseCommand` resolvería `PurchaseWorkflowMode` desde `org_settings`:
   - `Direct` → comportamiento idéntico al actual: invoca `ConfirmPurchaseCommand` de inmediato.
   - `Approval`/`MultiApproval` → transiciona `PurchaseInvoice` a un estado `PendingApproval` (extensión del enum `PurchaseStatus`) y **no** invoca `Confirm()` todavía.
2. Un futuro `ApprovePurchaseCommand` (uno por nivel, en el caso `MultiApproval`) registraría la aprobación (entidad nueva, p. ej. `PurchaseApprovalStep`, con su propio Entity Audit) y, solo al completarse el último nivel requerido, invocaría `ConfirmPurchaseCommand` — el mismo comando que hoy ejecuta Compra Directa, sin duplicar sus Steps 1-6.
3. Un futuro `RejectPurchaseCommand` transicionaría `PurchaseInvoice` de `PendingApproval` de vuelta a `Draft` (para corrección) o a un estado terminal `Rejected`, sin tocar Inventario/CxP/Contabilidad porque `Confirm()` nunca se invocó.

Este diseño garantiza que **Confirm() sigue siendo el único punto de entrada a los efectos de negocio** (Inventario, CxP, Contabilidad, Pricing SSOT), sea cual sea el camino (directo o aprobado) que llevó hasta ahí — el workflow de aprobación es una guarda *antes* de `Confirm()`, nunca una reimplementación de lo que ocurre *dentro* de él.

## Impacto potencial sobre módulos existentes (sin modificarlos)

| Módulo | Impacto potencial futuro |
|---|---|
| **Compras** | Nuevo estado `PendingApproval` en `PurchaseStatus`; nuevos comandos `Submit`/`Approve`/`Reject`; `ConfirmPurchaseCommand` sigue siendo el único ejecutor de Steps 1-6, invocado por el propio flujo directo o por el último nivel de aprobación. |
| **Recepción XML** | Ninguno — el snapshot y el Item Matching (ADR-028) terminan en "Crear Compra"/"Guardar Compra"; el estado de aprobación es exclusivamente un concepto de `PurchaseInvoice`. |
| **Item Matching** | Ninguno — ya resuelto antes de que exista una `PurchaseInvoice`. |
| **Inventario** | Ninguno en su lógica — sigue recibiendo movimientos únicamente desde `Confirm()`. Impacto operativo: el stock reflejará la compra más tarde en el tiempo (tras la aprobación), nunca al "Guardar Compra". |
| **Kardex** | Mismo impacto que Inventario — el kardex registra el movimiento en el momento de `Confirm()`, sea cual sea la fecha de aprobación. |
| **Cuentas por Pagar** | Ninguno en su lógica — `PurchasePayable` se sigue creando únicamente en Step 4 de `ConfirmPurchaseHandler`. |
| **Contabilidad** | Ninguno en su lógica — `PurchaseInvoiceConfirmedPostingTranslator` sigue disparándose solo por `PurchaseInvoiceConfirmedEvent`, que solo se levanta al confirmar. |
| **Caja** | Ninguno directo — Caja no consume `PurchaseInvoice` en su estado `Draft`/`PendingApproval`. |
| **Bancos** | Ninguno directo — mismo razonamiento que Caja. |
| **Auditoría** | Extensión aditiva: un nuevo dominio de Entity Audit (aprobaciones) sigue el checklist de ADR-022 sin tocar la infraestructura base (`AuditRecordBase`, `IAuditWriter<T>`, etc.). |
| **Permisos** | Extensión aditiva sobre `PurchasePermissions` (`purchases.submit`/`purchases.approve`/`purchases.reject`), sin cambiar el mecanismo de autorización (`perm:` policies). |
| **Domain Events** | Extensión aditiva — ver "Eventos futuros". Ningún evento existente (`PurchaseInvoiceConfirmedEvent`, `PurchaseInvoiceCancelledEvent`) cambia de firma ni de semántica. |
| **Outbox** | Sin cambios en el mecanismo (ADR-008) — los nuevos eventos siguen el mismo pipeline ya congelado. |
| **Hangfire** | Impacto potencial *opcional*: recordatorios de aprobación pendiente (job recurrente que notifica aprobadores con solicitudes vencidas) seguiría el mismo patrón ya usado por `ElectronicDocumentRetryJob` — no es un requisito, es una extensión posible si un caso real lo justifica. |
| **Multi-Tenant** | Sin cambios en el mecanismo — todo comando nuevo declara `IBranchScopedRequest`/`ICompanyScopedRequest` como ya lo hace `ConfirmPurchaseCommand`. |
| **Configuración por Empresa** | Extensión aditiva sobre `org_settings`/`OrgSettingKeys` — nueva clase `Purchases`, mismo mecanismo ya usado por `Invoice`/`Catalog`/branding de Ride. |

## Eventos futuros (identificación únicamente — no implementar)

| Evento | Cuándo se produciría | Quién lo consumiría (futuro) |
|---|---|---|
| `PurchaseSubmittedForApprovalEvent` | Al transicionar `Draft → PendingApproval` (equivalente a un futuro `PurchaseInvoiceSubmittedEvent`) | Auditoría (nuevo dominio); notificaciones al/los aprobador(es) asignado(s); dashboard de compras pendientes |
| `PurchaseApprovedEvent` (por nivel, si `MultiApproval`) | Cada vez que un nivel de aprobación se completa | Auditoría; siguiente nivel de aprobación (si existe); notificación al solicitante del avance |
| `PurchaseRejectedEvent` | Al rechazar en cualquier nivel | Auditoría; notificación al solicitante; retorno a `Draft` para corrección o cierre como `Rejected` |
| `PurchaseInvoiceConfirmedEvent` (ya existe) | Sin cambios — se sigue levantando únicamente dentro de `PurchaseInvoice.Confirm()`, sea cual sea el camino previo | `PurchaseInvoiceAuditHandler`, `PurchaseInvoiceConfirmedPostingTranslator` (sin cambios) |
| `PurchaseInvoiceCancelledEvent` (ya existe) | Sin cambios | Consumidores actuales, sin cambios |
| `PurchaseReopenedEvent` (especulativo) | Si se decide permitir reabrir una compra rechazada para corrección, en vez de forzar una nueva | Auditoría; el propio flujo de aprobación (reinicia en `Draft` o vuelve a `PendingApproval` en el nivel donde se rechazó, decisión a tomar en su propia fase) |

Ninguno de estos eventos se crea en esta entrega. La tabla existe para que la fase de implementación futura no tenga que redescubrir cuáles son necesarios ni quién los consumiría.

## `PurchaseReceptionDocument.MarkProcessed(...)` — análisis específico

- **Responsabilidad definitiva**: vincular de forma permanente un `PurchaseReceptionDocument` con la `PurchaseInvoice` que se generó a partir de su snapshot, y transicionar `Status` de `Verified` a `Processed` — cerrando el ciclo de vida fiscal del documento de recepción. Es un método de dominio, no un evento: la vinculación es un hecho estructural (`PurchaseId`), no una notificación.
- **Momento en que debería ejecutarse**: únicamente cuando la `PurchaseInvoice` asociada alcanza `Confirmed` — nunca antes. Esto es válido tanto en Compra Directa como en un futuro flujo de aprobación: mientras la compra esté en `Draft` o en un futuro `PendingApproval`, el documento de recepción debe permanecer en `Verified`, porque la compra todavía podría no confirmarse nunca (rechazo, corrección, abandono).
- **Por qué funciona correctamente hoy dentro del flujo Direct** (con la salvedad ya documentada en ADR-028): conceptualmente, `Confirm()` es instantáneo respecto a `Submit`/`Save` — no hay ventana de tiempo donde una compra "en progreso" deba bloquear al documento de recepción. La brecha real (documentada en ADR-028 "Consecuencias") es que `CreatePurchaseDraftCommand` no recibe hoy un `PurchaseReceptionDocumentId`, por lo que `MarkProcessed` nunca se invoca — pero **conceptualmente** el diseño ya es correcto para Compra Directa: el momento de invocación correcto sigue siendo "al confirmar", una vez que se cierre esa brecha (fuera del alcance de esta ADR, ver ADR-028 "Consideraciones futuras").
- **Cómo debería comportarse con un flujo de aprobación**: sin cambios en su contrato ni en su momento de invocación. `MarkProcessed` debería seguir invocándose exclusivamente desde el mismo punto que hoy —el momento en que la compra pasa a `Confirmed`—, sea que ese `Confirmed` se alcance de forma directa o tras completar todos los niveles de aprobación. Esto significa que la invocación de `MarkProcessed` debería vivir junto a (o inmediatamente después de) `ConfirmPurchaseCommand`, **nunca** junto a `SubmitPurchaseCommand`/`ApprovePurchaseCommand` — un documento de recepción no debe marcarse `Processed` mientras la compra todavía puede ser rechazada.
- **Decisión recomendada**: `MarkProcessed` no requiere ningún cambio de firma ni de comportamiento para soportar aprobación futura. Su contrato actual (`MarkProcessed(Guid purchaseId, Guid updatedBy)`, invocable solo desde `Verified`) ya es compatible con ambos modelos (directo y aprobado) precisamente porque su disparador conceptual correcto — "la compra fue confirmada" — es el mismo evento (`PurchaseInvoiceConfirmedEvent` o la invocación de `Confirm()`) independientemente de cuántos pasos de aprobación existieron antes.

## Compatibilidad

| Principio/ADR | Compatibilidad verificada |
|---|---|
| Clean Architecture | Sí — todo lo propuesto son nuevas entidades/comandos dentro de las capas ya existentes (`Domain`/`Application`/`Infrastructure`/`API`), sin invertir dependencias. |
| DDD | Sí — `PurchaseInvoice` sigue siendo el agregado raíz único que decide sus propias transiciones de estado vía métodos de dominio (`Submit`/`Approve`/`Reject` seguirían el mismo patrón que `Confirm`/`Cancel`). |
| CQRS + MediatR (ADR-013) | Sí — cada transición nueva es un comando independiente, sin mezclar lecturas y escrituras. |
| Domain Events (ADR-007) | Sí — los eventos nuevos son aditivos; ninguno reemplaza o modifica la semántica de `PurchaseInvoiceConfirmedEvent`/`PurchaseInvoiceCancelledEvent`. |
| Outbox (ADR-008) | Sí — sin cambios al mecanismo de publicación transaccional; los eventos nuevos lo reutilizan tal cual. |
| SOLID | Sí — Open/Closed explícito: `PurchaseStatus` se extiende (no se reemplaza), `ConfirmPurchaseCommand` se reutiliza (no se duplica), `org_settings` se extiende con nuevas claves (no una tabla paralela). |
| ADR-013 (CQRS/MediatR) | Ver arriba. |
| ADR-020 (Entity Tracking) | Sin conflicto — cualquier entidad nueva de aprobación (`PurchaseApprovalStep`, si se decide modelarla como colección hija de `PurchaseInvoice`) debe respetar el invariante ya congelado: agregarse solo sobre un agregado cargado por query, nunca reatachado sin pasar por el mismo `DbContext`. |
| ADR-021 (Pricing SSOT) | Sin conflicto — Step 5 de `ConfirmPurchaseHandler` (actualización de `Item.BaseSalePrice`) sigue disparándose únicamente al confirmar, nunca al enviar a aprobación. |
| ADR-022 (Audit Infrastructure) | Sin conflicto — un nuevo dominio "Purchase Approval Audit" es exactamente el caso de uso para el que Entity Audit fue diseñado Open/Closed. |
| ADR-028 (Recepción XML → Compra) | Sin conflicto — ver sección dedicada a `MarkProcessed` arriba; el flujo congelado por ADR-028 termina en "Guardar Compra", el workflow de aprobación empieza después de ese punto. |

**Ningún conflicto potencial fue encontrado** entre la evolución aquí descrita y la arquitectura o los ADR vigentes.

## No generar deuda técnica

Se confirma explícitamente que esta entrega **no recomienda introducir** ninguno de los siguientes elementos en este momento:

- **Feature Flags** para alternar Direct/Approval — el modo de workflow, cuando exista, es una decisión de configuración por empresa (`org_settings`), no un flag de despliegue; introducir un flag hoy sin implementación detrás sería exponer una opción que no hace nada.
- **Parámetros sin uso** (p. ej. agregar `PurchaseWorkflowMode` como columna de `Company` ya): no hay ningún lector de ese valor hoy — sería un campo persistido sin consumidor, deuda técnica inmediata.
- **Estados sin consumidores** (p. ej. agregar `PendingApproval` a `PurchaseStatus` ya): ningún handler sabría transicionar hacia o desde ese estado — rompería el invariante de que todo estado del enum es alcanzable y observable.
- **Interfaces vacías** (p. ej. `IPurchaseApprovalPolicy` sin implementación): una interfaz sin implementador real no es abstracción, es ruido — se define en la fase que la necesite, con su primera implementación real.
- **Código comentado** o esqueletos de clases (`PurchaseApprovalStep` como shell sin lógica): mayor riesgo de que quede a medio terminar y confunda a un desarrollador futuro sobre si "ya existe" o no.
- **TODOs permanentes** en el código de Compras señalando "aquí va la aprobación": el lugar correcto para esa intención es esta ADR, no un comentario disperso en `ConfirmPurchaseHandler`.
- **Workflows incompletos** (p. ej. `SubmitPurchaseCommand` que no hace nada todavía): un comando sin efecto real invita a que otro desarrollador lo complete apurado, sin pasar por el diseño completo descrito aquí.
- **Migraciones preventivas** (crear la tabla de `purchase_approval_step` "para no tener que hacerlo después"): una tabla sin código que la use es deuda de esquema — más cara de revertir que de crear cuando haga falta.
- **Implementaciones parciales** de cualquier tipo: el criterio de esta ADR es "documentar completo, implementar nada", precisamente para no dejar ningún artefacto a medio camino.

Si en el futuro alguno de estos elementos pareciera necesario antes de una implementación completa, debe justificarse técnicamente por qué NO debe implementarse todavía (regla general: ningún elemento de esta lista tiene justificación válida mientras no exista al menos un caso de cliente real con la necesidad, y una fase de desarrollo dedicada con su propio ADR de implementación si se aparta de esta guía).

## Decisiones Diferidas

Todas las decisiones que conscientemente se posponen para versiones futuras, con su justificación:

1. **Nombre final del estado intermedio de `PurchaseStatus`** (`PendingApproval` vs. alternativas). Diferida porque el nombre debe validarse contra el vocabulario de negocio real que se use en la UI de esa fase, no decidirse en abstracto.
2. **Modelo de datos de los niveles de aprobación** (¿colección hija de `PurchaseInvoice` tipo `PurchaseApprovalStep`, o una entidad independiente referenciando `PurchaseInvoiceId`?). Diferida porque depende de si `MultiApproval` termina requiriendo historial completo por nivel (auditable independientemente) o solo el estado agregado — decisión que debe tomarse con casos reales de aprobación escalonada, no especulativamente.
3. **Cómo se asigna el/los aprobador(es)** (por rol, por usuario específico, por posición jerárquica). Diferida: ningún cliente real ha definido todavía su estructura organizacional de aprobación; modelarlo ahora sería adivinar.
4. **Aprobación por departamento / centro de costo**. Diferida explícitamente: ninguna de las dos existe hoy como entidad en el ERP Core — introducirlas exigiría su propia fase de diseño (posiblemente ligada a Contabilidad/Costos), fuera del alcance de un workflow de aprobación de Compras.
5. **Reglas de escalonamiento por monto** (umbrales, moneda, si el umbral es por línea o por total de la compra). Diferida: requiere validación con casos reales de negocio antes de fijar una fórmula.
6. **Reapertura de una compra rechazada** (`PurchaseReopenedEvent` especulativo): diferida si debe reiniciar en `Draft` completo o reanudar en el nivel de aprobación donde fue rechazada — ambas son válidas, la elección depende de la política de negocio que se confirme en su fase.
7. **Notificaciones a aprobadores** (email, in-app, ambas) y si requieren un job de Hangfire para recordatorios de solicitudes vencidas. Diferida: es una extensión operativa, no un requisito estructural del workflow — se decide cuando exista el flujo base.
8. **Vinculación real `PurchaseReceptionDocument ↔ PurchaseInvoice`** (cierre de la brecha de `MarkProcessed` documentada en ADR-028). Diferida explícitamente en esa ADR, reafirmada aquí: debe resolverse **antes o junto con** la primera fase de workflow de aprobación, para que `MarkProcessed` se invoque desde el lugar correcto (`Confirm()`) independientemente del camino que se tomó para llegar ahí.

## Roadmap de Evolución (propuesta técnica — no implementar)

```
Fase 1 — Compra Directa (actual, FROZEN por ADR-028)
   PurchaseStatus: Draft → Confirmed / Cancelled
   Sin workflow de aprobación.

Fase 2 — Workflow configurable (un aprobador)
   PurchaseWorkflowMode en org_settings (Direct | Approval)
   PurchaseStatus: + PendingApproval
   SubmitPurchaseCommand / ApprovePurchaseCommand / RejectPurchaseCommand
   Cierre previo o simultáneo de la brecha MarkProcessed (ver Decisión Diferida 8)

Fase 3 — Aprobación multinivel
   PurchaseWorkflowMode.MultiApproval
   Modelo de niveles de aprobación (Decisión Diferida 2)
   Reglas por monto / sucursal (Decisión Diferida 5, ya soportado parcialmente por BranchId)

Fase 4 — Motor de Workflow reutilizable para todo el ERP
   Generalización del mecanismo de aprobación (hoy específico de Compras) hacia
   otros flujos que ya tienen la misma necesidad diferida y documentada
   (Transferencias/Ajustes de Inventario — ver docs/ROADMAP.md), bajo un contrato
   común, evaluado solo si Compras y al menos un segundo módulo confirman la
   misma forma de aprobación.
```

Cada fase es una entrega independiente, con su propio ADR de implementación si introduce algo que se aparte de lo aquí documentado.

## Calidad y alineación

Esta guía se apoya exclusivamente en mecanismos ya existentes y verificados en el código real (`PurchaseStatus`, `PurchaseInvoice.Confirm()`, `PurchaseInvoiceConfirmedEvent`, `org_settings`/`OrgSettingKeys`, Entity Audit, `PurchasePermissions`, `IBranchScopedRequest`) — no propone infraestructura nueva de bajo nivel, precisamente para minimizar el riesgo de que la implementación futura descubra incompatibilidades. Es consistente con los principios de ZH Technologies: arquitectura limpia (capas y dependencias sin alterar), mantenibilidad (extensión aditiva sobre patrones ya probados), escalabilidad (Fase 4 generaliza sin comprometer Fases 1-3), simplicidad operativa (Compra Directa sigue siendo el camino por defecto y el único disponible hasta nueva orden), mínimo acoplamiento (Recepción XML y Compras permanecen desacopladas, tal como fija ADR-028), evolución controlada (cada fase requiere validación con casos reales antes de avanzar) y cero riesgo sobre lo ya funcional (ningún componente FROZEN se modifica).

## Entrega

- No se modificó ningún flujo funcional.
- No se modificó ningún comportamiento del ERP.
- No se agregó código.
- No se generó deuda técnica.
- No se introdujeron implementaciones parciales.
- El documento constituye únicamente la guía oficial de evolución para futuras versiones del ERP.
- Cualquier implementación futura deberá realizarse mediante una nueva fase de desarrollo y, de ser necesario, mediante un nuevo ADR.

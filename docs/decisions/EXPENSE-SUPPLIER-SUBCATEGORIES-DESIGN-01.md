# EXPENSE-SUPPLIER-SUBCATEGORIES-01 — Proveedor ↔ Subcategorías de Gastos

**Estado: IMPLEMENTADO (fases 1–3 publicadas: `72a9afc3d`, `f99332051`, `f4a0bc483`); cierre documental (fase 4) en gate del
propietario (2026-11-14). D4 sigue pendiente.** El comportamiento implementado y probado vive en la SSOT
[Gastos](../architecture/expenses.md); este documento conserva el diseño y la justificación, no una segunda especificación.
Estado y gates: [STATUS](../../STATUS.md). Cola: [WORK_QUEUE](../agent/WORK_QUEUE.md).

## Objetivo y alcance

Cada empresa configura, por proveedor, qué subcategorías de gasto aplican. El formulario de Gastos (manual y
desde XML) ofrece solo esas, preselecciona la única configurada y **el backend rechaza** las no permitidas.
No cambia contabilización, impuestos, retenciones, CxP ni SRI. Fuera de alcance: columnas nuevas en tablas
existentes, migraciones incrementales y cambios al árbol de categorías, a `expense_documents`/`expense_lines`,
a Compras o a Contabilidad.

## Decisiones del propietario aplicadas

| ID | Decisión | Dónde se aplica |
|---|---|---|
| D1 | Restricción en frontend **y** backend, preservando compatibilidad histórica | Reglas de validación y Compatibilidad |
| D2 | Sin asignaciones activas ≠ asignaciones activas sin subcategorías disponibles; solo la primera permite el catálogo completo | Modos de configuración |
| D3 | Sin pérdida silenciosa al cambiar de proveedor; se limpian solo selecciones nuevas incompatibles | Cambio de proveedor |
| D4 | Recreación de BD **pendiente** de autorización expresa; no se ejecuta nada destructivo | Ubicación en el esquema; Decisiones pendientes |
| D5 | Configuración en `MasterData` | Diseño técnico |
| D6 | SSOT de Gastos incorporado | [expenses.md](../architecture/expenses.md) |
| D7 | Confirmar un borrador existente **no** revalida la restricción | Validación en Gastos |
| D8 | `Unconfigured` = cero asignaciones activas; `PUT []` desactiva todas (sin DELETE físico); reactivar reutiliza el registro; «Quitar restricción» con confirmación | Modos, API, Frontend |
| D9 | Altas, bajas lógicas y reactivaciones se registran con Entity Audit (ADR-022) | Modelo de datos; Auditoría |
| D10 | Misma empresa garantizada por handler y pruebas PostgreSQL; sin trigger en BD | Integridad multiempresa |

## Verificado en el repositorio (no asumido)

- Subcategoría = `ExpenseCategoryNode` nivel 2, scope tenant+empresa, con cuenta obligatoria e `IsActive`.
  `ExpenseLine.ExpenseSubcategoryId` → FK Restrict; `ExpenseDocument.SupplierId` es el proveedor
  (`BusinessPartner`). `ExpenseDraftLineRequest` **no trae id de línea** y `UpdateDraft` usa `ReplaceLines`
  (reemplaza todas): no se puede comparar línea a línea con lo persistido, solo por conjunto de subcategorías.
- Un único punto resuelve las líneas para crear, editar y crear-confirmado: `ExpenseDraftRules` (subcategoría
  existente/activa, IVA). `ConfirmExpenseDocumentHandler` revisa la cuenta de cada línea al confirmar un borrador.
- Precedente exacto de configuración proveedor × empresa: `SupplierRetentionDefault`
  (`master_supplier_retention_defaults`; `AuditableEntity`, `ITenantScopedEntity`, `ICompanyScopedEntity`, filtro
  estricto fail-closed, FKs simples Restrict, único por tenant+empresa+proveedor+código **sin filtro de
  `is_active`**, soft-disable, controlador bajo `api/v1/master/business-partners/{bpId}/…`, sección en el modal
  del proveedor).
- Permisos reutilizables: `masterdata.businesspartners.view`/`configure-company` (como el precedente);
  `expenses.catalog.view` y `expenses.documents.*`. El buscador de proveedores del formulario de Gastos ya
  consulta `GET /master/business-partners` (`…view`): un perfil que captura gastos ya posee `…view`. No se crea
  ningún permiso.
- Baseline única `20261101012214_InitialProductionBaseline` sin upgrade-path ([DATABASE](../DATABASE.md#migraciones)).
- ADR-022 (Entity Audit, FROZEN): toda auditoría tipada nace de un evento levantado por un `AggregateRoot`
  (`AuditableEntity` ya lo es), con su propia tabla `AuditRecordBase`. Precedente exacto: `PriceListItem`
  (asignación ítem↔lista) levanta `Assigned`/`Enabled`/`Disabled`; `PriceListItemAuditHandler` los traduce a
  `PriceListItemAudit` (`price_list_item_audit`, `ConfigureAuditBase`). `Enable`/`Disable` lanzan si el estado ya
  es el pedido, por lo que el reemplazo de conjunto solo los invoca cuando el estado cambia.
- Precedente de «sin configuración»: `SupplierRetentionDefault` lee solo filas **activas**
  (`GetActiveByBusinessPartnerAsync`); desactivarlas todas equivale a no tener configuración, sin borrar.
- BusinessPartner es de tenant (ADR-017) y esta configuración es de empresa: la configuración de A nunca afecta a B.

## Modelo de datos propuesto

Tabla **`master_supplier_expense_subcategories`** (prefijo `master_` como su precedente; scope empresa).
Entidad `SupplierExpenseSubcategory` en `ERP.Domain/MasterData/Entities`.

| Columna | Tipo | Notas |
|---|---|---|
| `id` | uuid PK | |
| `tenant_id`, `company_id` | uuid NOT NULL | `ITenantScopedEntity` / `ICompanyScopedEntity`; FK `company_id` → `company` Restrict |
| `business_partner_id` | uuid NOT NULL | FK `fk_bpes_business_partner` → `business_partners` Restrict |
| `expense_subcategory_id` | uuid NOT NULL | FK `fk_bpes_expense_subcategory` → `expense_category_nodes` Restrict |
| `is_active` | boolean NOT NULL default true | baja lógica; nunca DELETE físico |
| `created_at/by`, `updated_at/by` | estándar `AuditableEntity` | |

Índices: único `uq_bpes_company_bp_subcategory (tenant_id, company_id, business_partner_id,
expense_subcategory_id)` **sin filtro por `is_active`**: la baja es lógica, así que una asignación desactivada
conserva su fila y se **reactiva** reutilizándola, nunca se inserta otra (un índice parcial permitiría
duplicados); `ix_bpes_company_bp_active (tenant_id, company_id, business_partner_id, is_active)`; índices de FK
que EF genere.

**Tabla de auditoría (D9, ADR-022): `supplier_expense_subcategory_audit`**, entidad
`SupplierExpenseSubcategoryAudit : AuditRecordBase, ICompanyOperationalEntity`, calcada de
`PriceListItemAudit`. `ConfigureAuditBase("supplier_expense_subcategory_audit")` aporta `id`, `tenant_id`,
`entity_id` (= id de la asignación), `action`, `user_id`, `user_name`, `occurred_at_utc`, `correlation_id`,
`request_id`, `source`, `reason` y sus índices `ix_…_entity_occurred_at` y `ix_…_user_occurred_at`. Columnas
propias: `company_id`, `business_partner_id`, `expense_subcategory_id` (sin FK, como las demás tablas de
auditoría; append-only, sin Update/Delete) e índice `ix_supplier_expense_subcategory_audit_bp_occurred_at
(tenant_id, business_partner_id, occurred_at_utc)`. Es la **segunda y última tabla nueva** del diseño y no
modifica ninguna infraestructura FROZEN. Eventos levantados por `SupplierExpenseSubcategory`
(`Create`/`Enable`/`Disable`): `SupplierExpenseSubcategoryAssignedEvent` («Assigned»),
`…EnabledEvent` («Enabled», reactivación) y `…DisabledEvent` («Disabled», baja lógica);
`SupplierExpenseSubcategoryAuditHandler` en `Application/MasterData/EventHandlers` los traduce con
`IAuditService`. Quitar la restricción genera un `Disabled` por cada asignación activa, de modo que el historial
reconstruye cuándo, quién y qué se retiró. Nada de auditoría se escribe desde controlador, repositorio ni React.

**Integridad multiempresa verificada (D10: handler y pruebas, sin trigger en BD):** (1) filtro estricto por tenant y empresa en consultas y escrituras;
(2) `tenant_id`/`company_id` solo del contexto, nunca del cuerpo; (3) el handler exige que la subcategoría sea
visible en la empresa activa (el filtro de `expense_category_nodes` la oculta si es de otra → 404 sin revelar
existencia); (4) «cero asignaciones activas» se evalúa por empresa; (5) la auditoría lleva `company_id` del contexto.
**Residual aceptado (D10):** la BD no impide por sí sola una fila con `company_id` distinto del nodo (haría falta
una FK compuesta o un índice único nuevo sobre `expense_category_nodes`, o un trigger raw SQL). Mitigación:
handler + prueba PostgreSQL de escritura cruzada.

**Ubicación en el esquema inicial (a ejecutar en la implementación, no ahora):** en
`20261101012214_InitialProductionBaseline.cs`, `CreateTable` justo después de `expense_category_nodes` (antes de
`inventory_adjustment_reason_accounts`, hoy ~línea 3492); `CreateIndex` junto a los de
`master_supplier_retention_defaults` (~línea 9038); `DropTable` en el `Down` antes de
`master_supplier_retention_defaults` (~línea 11367). Mismos bloques en `…Baseline.Designer.cs` y
`ErpDbContextModelSnapshot.cs`. **Tabla de auditoría:** `CreateTable` de `supplier_expense_subcategory_audit`
en el bloque alfabético de tablas de auditoría sin FK, inmediatamente después de `supplier_credit_audit`
(hoy ~línea 1939, antes de `supplier_payment_sequences`), sus `CreateIndex` junto a los
`ix_supplier_credit_audit_*` (~línea 10600) y `DropTable` en el `Down` en orden inverso; más `DbSet` en
`ErpDbContext`. Procedimiento: generar los bloques con una migración temporal fuera del repo, copiarlos y
verificar que el diff contra el snapshot contiene solo estas dos tablas (regenerar la baseline omitiría los
objetos raw SQL). No son objetos raw SQL (no cambia `RawSqlDatabaseObjectsSurviveMigrationSquashTests`). Efecto:
una BD ya creada con la baseline actual **no** recibe la tabla; requiere recrearla, operación destructiva que
queda **pendiente de autorización expresa (D4)**: servidor, base y entorno efectivos y confirmación del
propietario. Esta fase no tocó ninguna base.

## Modos de configuración (D2)

Se calculan en servidor, por proveedor y empresa, con un único servicio de Application
(`ISupplierExpenseSubcategoryPolicy`, en `MasterData`; Gastos lo consume por interfaz):

Una **asignación activa** (`is_active = true`) es una asignación vigente; desactivarla es retirarla. Una
asignación activa es **disponible** si su subcategoría está activa y es de nivel Subcategoría.

| Modo | Condición | Efecto |
|---|---|---|
| `Unconfigured` | **Cero asignaciones activas** (nunca configurado, o todas retiradas) | Catálogo completo; el backend no restringe. |
| `Restricted` | Asignaciones activas con al menos una **disponible** | Solo las disponibles. Una sola disponible → autoselección. |
| `RestrictedEmpty` | Asignaciones activas, pero **ninguna disponible** | **No se permite ninguna** selección nueva; el selector queda vacío con aviso («El proveedor tiene subcategorías asignadas pero ninguna está disponible»); el backend rechaza. |

`RestrictedEmpty` nunca degrada a catálogo completo: el catálogo solo se abre por una acción explícita del
administrador («Quitar restricción»). Salidas del bloqueo: reactivar la subcategoría en el catálogo (vuelve a
`Restricted`), reasignar otras o quitar la restricción. Desactivar en el catálogo una subcategoría asignada no se
bloquea (sin lock cruzado entre módulos, como en ACCOUNTING-CROSS-MODULE-ACCOUNT-LOCK-01); deja de ser disponible.

## Diseño técnico

**Capas (D5):** Domain: entidad + `ISupplierExpenseSubcategoryRepository`. Application (`MasterData/UseCases`,
patrón de `SupplierRetentionDefault`): `GetSupplierExpenseSubcategoriesQuery`,
`SetSupplierExpenseSubcategoriesCommand`, `ISupplierExpenseSubcategoryPolicy`. Infrastructure: configuración EF,
repositorio, `DbSet`, DI. API: un controlador delgado. Módulo Gastos: solo consume la política en
`ExpenseDraftRules` (ver validación); sin cambios de DTO, rutas ni permisos de Gastos.

**Contratos API** (envelope estándar, `IRequiresCompanyContext`):

- `GET /api/v1/master/business-partners/{bpId}/expense-subcategories` — `masterdata.businesspartners.view`.
  200: `{ mode, items: [{ expenseSubcategoryId, code, name, categoryName, typeName, isActive,
  isSubcategoryActive }], availableSubcategoryIds }`. `items` incluye asignaciones activas e inactivas
  (`isActive`); el modo y las subcategorías **disponibles** se calculan en el servidor solo sobre las activas
  (regla única, no duplicada en el frontend). Nombres resueltos en servidor; **sin cuentas contables**.
- `PUT …/expense-subcategories` — `masterdata.businesspartners.configure-company`. Cuerpo
  `{ expenseSubcategoryIds: Guid[] }` = conjunto deseado, reemplazo atómico en una transacción: crea las nuevas
  (`Create`, evento «Assigned»), **reactiva reutilizando su registro** las inactivas (`Enable`, «Enabled»),
  desactiva las ausentes (`Disable`, «Disabled») y no toca las que ya están en el estado pedido (idempotente;
  `Enable`/`Disable` lanzan si el estado ya coincide). **`[]` es válido:** desactiva todas las asignaciones
  activas y deja al proveedor en `Unconfigured`, sin DELETE físico. Cada cambio actualiza `updated_at/by` y
  genera su registro de auditoría (D9). Un `PUT []` sobre un proveedor sin asignaciones activas es un no-op 200.

**Validaciones de la configuración:** contexto de empresa; proveedor existente (404), activo y con rol
Proveedor activo (422, como el precedente); IDs sin duplicados y cantidad acotada (422); cada ID nuevo o reactivado visible en la
empresa (404), de nivel Subcategoría (422) y activo (422) (retirar una asignación no exige que la subcategoría siga activa); violación de unicidad concurrente → 409 vía
`IDatabaseExceptionTranslator` (reintentable: la segunda ejecución reactiva o ya existe).

**Validación en Gastos (D1, backend):** `ExpenseDraftRules` recibe el proveedor y, en edición, las
subcategorías ya persistidas del borrador; para cada línea consulta la política:

| Flujo | Regla |
|---|---|
| Crear borrador (incluye el preparado desde XML) | Todas las líneas deben estar permitidas. |
| Editar borrador, proveedor sin cambio | Cada subcategoría debe estar permitida **o** estar ya entre las persistidas del borrador (se preserva lo previo; solo lo nuevo se valida). |
| Editar borrador, proveedor cambiado | Todas las líneas deben estar permitidas (sin excepción): obliga a corregir (ver D3). |
| Crear y confirmar (`POST confirmed`) | Todas las líneas deben estar permitidas (documento nuevo). |
| Confirmar un borrador existente | No revalida la restricción: sus líneas ya pasaron la validación al guardarse y los borradores anteriores a esta función siguen confirmables. Mantiene la comprobación de cuenta vigente. **Decisión D7.** |
| Anular / consultar confirmados | Sin cambios: nunca se revalidan. |

Rechazo: 422 con mensaje general («La subcategoría 'X' no está permitida para el proveedor.» /
`RestrictedEmpty`: «El proveedor tiene subcategorías asignadas pero ninguna está disponible.»). El contrato de error
por línea no se amplía (B2 de EXPENSE-UX-01 no autorizado); el frontend evita el rechazo y marca la línea
antes de enviar. `Unconfigured` no restringe. El backend lee la configuración con su repositorio: no exige
`masterdata.*` al usuario de Gastos. El `GET` que usa el formulario acepta `masterdata.businesspartners.view` **o** `expenses.documents.create`/`update` (decisión en el caso de uso con el autorizador oficial; `catalog` sigue siendo solo de `configure-company`). Cuidar el techo de tamaño de handlers (ADR-046): la lógica va en
`ExpenseDraftRules`/política, no en los handlers.

**Auditoría (D9, ADR-022):** Entity Audit sobre la asignación: `SupplierExpenseSubcategory` levanta
`Assigned`/`Enabled`/`Disabled` desde su agregado, `SupplierExpenseSubcategoryAuditHandler` escribe
`supplier_expense_subcategory_audit` vía `IAuditService` (despacho por Outbox tras el `SaveChanges`, mismo flujo
que Pricing). `AuditableEntity` conserva además autor y fecha de la última acción. No se modifica ningún contrato
FROZEN de ADR-022.

**Frontend:**

- *Ficha del proveedor:* `SupplierExpenseSubcategoriesSection` en archivo propio (`masterData/components`),
  montada junto a retenciones en el modal de configuración. Selección múltiple agrupada Tipo → Categoría →
  Subcategoría con componentes `ZH*`, muestra el modo y ofrece **«Quitar restricción»** (visible solo con
  asignaciones activas y permiso de edición): pide confirmación con `message.confirm`, advierte que el proveedor
  podrá usar cualquier subcategoría y envía `PUT []`; i18n es/en. Visible con `…view`, editable con `…configure-company`;
  i18n es/en con paridad. El `GET` incluye `catalog` (candidatas activas, sin cuentas) solo con `…configure-company`; la sección no consulta el catálogo de Gastos. `MasterDataSuppliersPage.tsx` está bajo techo de ADR-047 (811): el montaje no puede
  aumentarlo (regla 6); verificar con `architecture:check`.
- *Gastos:* hook `useExpenseSupplierScope` por `header.supplierId` (manual y XML comparten camino).
  Filtra el árbol que recibe el `ExpenseSubcategorySelector` (oculta Tipo/Categoría sin permitidas). Si la lectura falla
  (403/red) el formulario **no** cae al catálogo completo: muestra el error, porque el backend rechazaría igual.
- *Reutilización:* `ExpenseSubcategorySelector`, `SupplierSearchSelect`/`supplierPickerFacade`, `ZHField/ZhSelect/
  ZHBtn`, `businessPartnerService`/facade (método nuevo) y `supplierExpenseSubcategoryLookupFacade` (lectura para otros módulos). Nuevo solo la sección, el hook y su modelo puro.

**Cambio de proveedor (D3):**

| Línea | Comportamiento |
|---|---|
| Selección **nueva** (línea sin guardar, o subcategoría elegida en esta sesión) incompatible | Se limpia (criterio de U3) con aviso visible; si hay exactamente una permitida, se asigna. |
| Línea sin subcategoría | Recibe la única permitida, si la hay. |
| Línea **persistida** de un borrador existente | **Nunca se borra ni se reemplaza:** se conserva visible, marcada «no permitida para este proveedor», y bloquea guardar y confirmar en cliente hasta que el usuario elija una permitida (el backend también rechaza al cambiar de proveedor). |
| `RestrictedEmpty` | Líneas nuevas quedan vacías; las persistidas se conservan marcadas. |

Abrir un borrador nunca modifica sus líneas. Con el proveedor sin cambio, una subcategoría persistida fuera del
conjunto se muestra (el selector la incluye como opción) con aviso y el borrador puede guardarse y confirmarse.
Documentos confirmados: solo lectura.

## Compatibilidad

Sin cambios en tablas, DTO, rutas ni permisos existentes. Sin configuración (`Unconfigured`) todo funciona como
hoy. Borradores anteriores o con valores fuera de conjunto se abren, guardan sin cambios y confirman. Confirmados
y reportes no dependen de esta tabla. Módulos FROZEN/CLOSED (BP V2/Proveedor, Gastos): cambio aditivo, sin tocar
contratos congelados; la reapertura se limita al modal del proveedor, `ExpenseDraftRules` y el formulario, y
requiere autorización expresa.

## Pruebas necesarias (implementación; PostgreSQL real para persistencia, scope y concurrencia)

- Dominio/Application: modos `Unconfigured/Restricted/RestrictedEmpty`, validadores y handlers de configuración
  (404/422/409, proveedor inactivo, sin rol, nivel/inactiva/duplicados, conjunto vacío = `Unconfigured`), política, eventos y handler de auditoría.
- Gastos: crear, editar con y sin cambio de proveedor (preservación por conjunto persistido), crear-confirmado,
  confirmar borrador existente, flujo XML, borrador histórico fuera de conjunto, mensajes 422.
- PostgreSQL: unicidad sin filtro, **reactivación** (sin fila duplicada), reemplazo atómico, concurrencia de dos
  `PUT`, scope: A no lee ni escribe en B, subcategoría de otra empresa → 404, «cero activas» por empresa;
  desactivar subcategoría → `RestrictedEmpty`; reactivarla → `Restricted`; `PUT []` → `Unconfigured` sin borrar
  filas; reasignar tras quitar reutiliza la fila; `PUT []` repetido es no-op; auditoría (ADR-022): una fila de
  `supplier_expense_subcategory_audit` por alta, baja y reactivación, con `company_id`, actor y `entity_id`, y
  ninguna en un no-op; clase de auditoría registrada en `tools/ci/backend-test-groups.json` como
  `PriceListItemAuditIntegrationTests`.
- API HTTP: 401/403 por `…view`/`…configure-company`, perfil de Gastos sin `masterdata.*` no puede configurar
  pero sí es validado, OpenAPI; clase nueva registrada en `tools/ci/backend-test-groups.json`.
- Frontend: sin configuración, una, varias, `RestrictedEmpty`, cambio de proveedor (nueva vs persistida),
  borrador histórico, XML, permisos solo lectura, fallo de lectura; `tsc -b`, ESLint, i18n, design-system,
  `architecture:check`, guardrail oficial.

## Fases de implementación (ejecutadas)

1. **Esquema, backend y validación en Gastos** (`72a9afc3d`): entidad, EF, baseline, repositorio, política, casos de uso,
   controlador, eventos y auditoría ADR-022, `ExpenseDraftRules` y pruebas. Recreación de BD solo tras D4 (no ejecutada).
2. **Ficha del proveedor** (`f99332051`): sección, servicio/facade, i18n y `catalog` en el `GET` (solo `configure-company`).
3. **Formulario de Gastos** (`f4a0bc483`): hook, filtro, autoselección, cambio de proveedor, XML y autorización de lectura del `GET`.
4. **Cierre documental** (este cambio): comportamiento en [expenses.md](../architecture/expenses.md), estado en STATUS; este diseño
   queda como justificación. Corrección CI-1196 (`e8c067146`) registrada en STATUS y en [Seguridad](../architecture/security.md#refresh-rotation).

## Criterios de aceptación

Reglas aprobadas en manual y XML; backend rechaza lo no permitido en crear/editar/crear-confirmado y preserva
históricos; `RestrictedEmpty` nunca abre el catálogo; ninguna pérdida silenciosa de líneas persistidas; A
invisible e inmodificable desde B (probado); sin DELETE físico, `PUT []` devuelve `Unconfigured` y reactivación sin duplicados; historial auditado de altas, bajas y reactivaciones; baseline única con
las dos tablas y sin migraciones nuevas; ningún permiso ni contrato existente cambia; guardrails y pruebas focales PASS
y gate del propietario PASS.

Resultado: cumplidos en las fases 1–3 con las pruebas registradas en STATUS; el propietario aprobó el cierre técnico y
documental de la fase 4 el 2026-11-26, sin acreditar E2E ni validación con API real (ver [STATUS](../../STATUS.md)).

## Riesgos

Baseline editada ⇒ BD existente sin las dos tablas hasta recrearla (D4); techo ADR-047 del modal; sin garantía de
BD de misma empresa (D10, aceptado); borrador confirmable fuera de conjunto si la configuración se endurece tras
guardarlo (D7, aceptado); un `PUT []` simultáneo con el guardado de un gasto puede validar contra la
configuración anterior (misma semántica que las demás configuraciones, sin invariante financiera).

## Decisiones pendientes del propietario

| ID | Decisión | Recomendación |
|---|---|---|
| D4 | **Autorización expresa** para recrear la BD local tras editar la baseline (destructivo). | Solicitarla al iniciar la fase 1, mostrando servidor, base y entorno. |

D1–D3 y D5–D10 están aprobadas e incorporadas arriba.

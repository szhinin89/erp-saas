# Gastos — SSOT funcional

Única especificación del comportamiento **implementado y comprobado** de Gastos. Las reglas
transversales no se repiten aquí: retención ([Backend](./backend.md#retenciones-una-capacidad-una-política-de-emisión-zh-retention-emission-ssot-01-zh-purchase-retention-confirm-01)),
asientos ([Accounting](./accounting.md)), recepción XML ([Purchases](./purchases.md)), permisos y
aislamiento ([Seguridad](./security.md)), precisión y fechas ([Data standards](./data-standards.md)).
Estado y gates: [STATUS](../../STATUS.md). Capacidades: [FEATURES](../../FEATURES.md).

## 1. Alcance y scope

Gasto = comprobante de un proveedor sin movimiento de inventario, clasificado por subcategoría de gasto.
Scope **empresa** (`tenant_id` + `company_id`; el documento además tiene sucursal). El catálogo de gastos
es por empresa. Rutas UI: `/expenses/documents`, `/expenses/categories`.

## 2. Catálogo de gastos (`expense_category_nodes`)

- Árbol de tres niveles: **Tipo → Categoría → Subcategoría**. Solo la subcategoría tiene cuenta contable
  (obligatoria; activa, de tipo gasto y que admite registros contables), `IsDeductible` y `RequiresInvoice`.
- Código y nombre únicos por nivel dentro de su padre (y entre tipos de la empresa); códigos en mayúsculas.
- Un nodo no se activa con el padre inactivo ni se desactiva con hijos activos. Sin DELETE físico.
- API `api/v1/expenses/categories` (`GET tree`, `GET {id}`, `POST`, `PUT {id}`, `activate`, `deactivate`);
  el árbol incluye código y nombre de la cuenta vinculada (sin exponer otras cuentas del plan, por lo que
  no exige `accounting.view`).

## 3. Documento de gasto

Estados: **Draft → Confirmed → Cancelled** (terminal). Solo un borrador se edita; solo un confirmado se anula.

**Borrador** (`POST/PUT api/v1/expenses/documents`): proveedor existente, activo y con rol Proveedor activo;
tipo de documento activo en el catálogo SRI (no exige que sea electrónico); fecha de vencimiento no anterior
a la emisión; condición de pago (valor explícito, si no la del proveedor); al menos una línea; cada línea
apunta a una subcategoría existente y activa, con código de IVA activo. La edición reemplaza todas las
líneas. Opcionales: autorización, sustento tributario (por defecto el del proveedor) e intención de retención.

**Confirmar** (`POST {id}/confirm`; también `POST confirmed`, que crea y confirma): la condición de pago debe
seguir activa; cada línea toma el snapshot de su cuenta contable vigente; se congelan los totales; se levanta
`ExpenseDocumentConfirmedEvent` (asiento automático durable) y, según la política de flujo de la empresa, se
genera la CxP. La retención se define antes de confirmar y se emite en la misma unidad de trabajo.

**Anular** (`POST {id}/cancel`): solo confirmado, con motivo obligatorio; revierte CxP, asiento y retención
activa de forma conjunta. Se bloquea si hay saldo a favor del proveedor aplicado contra su CxP o pagos que
impiden reversar la retención.

**Desde XML:** el gasto se prepara desde una recepción electrónica (permisos y reglas en
[Purchases](./purchases.md)); se aplican las mismas validaciones del borrador y la exclusividad
Compra↔Gasto por clave de acceso (trigger de BD, [DATABASE](../DATABASE.md)).

### Subcategorías permitidas por proveedor (backend)

Cada empresa puede restringir las subcategorías de un proveedor (`master_supplier_expense_subcategories`,
scope tenant+empresa; auditoría ADR-022 en `supplier_expense_subcategory_audit`). El modo se calcula solo
sobre asignaciones **activas**: **`Unconfigured`** (cero activas: catálogo completo), **`Restricted`** (hay
activas y al menos una con subcategoría activa de nivel Subcategoría) y **`RestrictedEmpty`** (hay activas
pero ninguna disponible: no se permite ninguna selección nueva; nunca abre el catálogo).

- API `api/v1/master/business-partners/{bpId}/expense-subcategories`: `GET` (`masterdata.businesspartners.view` o `expenses.documents.create` o `expenses.documents.update`, evaluados por el autorizador runtime en el caso de uso)
  devuelve modo, asignaciones y subcategorías disponibles y, **solo con `…configure-company`**, `catalog` (subcategorías activas de la empresa: id, código, nombre, categoría y tipo, sin cuentas; `null` para quien solo consulta y en la respuesta del `PUT`); `PUT` (`…configure-company`) fija el conjunto de
  forma atómica: crea, **reactiva el mismo registro** y retira con baja lógica; `[]` retira todas (vuelve a
  `Unconfigured`); sin DELETE físico; cada alta, retiro y reactivación genera su registro de auditoría.
- Validación en Gastos (`ExpenseDraftRules`): crear borrador, crear-confirmado y editar con cambio de proveedor
  exigen subcategorías permitidas; editar con el mismo proveedor conserva las ya persistidas del borrador y solo
  valida lo nuevo; **confirmar un borrador existente no revalida** (422 «La subcategoría 'X' no está permitida
  para el proveedor.» / «…ninguna está disponible.»). Anular y consultar no se ven afectados.
- Interfaz: la ficha del proveedor la administra y el formulario de Gastos la aplica (§ 5).

## 4. Permisos

Catálogo: `expenses.catalog.{view,create,update,activate,deactivate}`. Documentos:
`expenses.documents.{view,create,update,confirm,cancel}` (confirmar y `confirmed` exigen `confirm`).
Permiso = quién puede; el estado del documento = en qué etapa está ([Seguridad](./security.md)).

## 5. Frontend

`ExpenseDocumentFormPage` (cabecera, líneas, totales, retención) y `ExpenseCategoriesPage` (árbol en
`ZHModal`). Selector en cascada Tipo → Categoría → Subcategoría: solo nodos activos y con cuenta visible.
Al cambiar Tipo o Categoría, una subcategoría que deja de ser válida se limpia. El cálculo de líneas usa
`decimal.js` half-up equivalente al backend.

**Ficha del proveedor — subcategorías permitidas.** `SupplierExpenseSubcategoriesSection` (modal de configuración,
tras las retenciones) usa solo el `GET`/`PUT` de §3. Muestra el modo que calcula el servidor y las subcategorías
del `catalog` agrupadas Tipo › Categoría (las asignadas que dejaron de estar disponibles siguen visibles y
marcadas). Guardar envía el conjunto elegido; **«Quitar restricción»** (visible solo con asignaciones activas) y
guardar con todo desmarcado piden confirmación y envían `PUT []`. Lectura con `masterdata.businesspartners.view`;
edición con `…configure-company`. Los candidatos salen del `catalog` del propio `GET` (autorizado en servidor), por lo que
no depende de `expenses.catalog.view`; sin `catalog` la sección queda en solo lectura. No se calcula nada en el cliente.

**Formulario de documentos — subcategorías permitidas.** Al elegir proveedor (manual o desde XML) `useExpenseSupplierScope` lee el
`GET` de §3 para la empresa activa (se relee al cambiar de proveedor o de empresa; basta `expenses.documents.create`/`update`, no exige `masterdata.*`; solo con
borrador editable y `expenses.catalog.view`) y el selector en cascada muestra: `Unconfigured` todo el catálogo; `Restricted`
solo las autorizadas (sin Tipo/Categoría vacíos); `RestrictedEmpty` aviso y ninguna selección nueva. Una sola permitida se
autoselecciona en líneas nuevas o vacías. Abrir un borrador nunca modifica sus líneas. Al cambiar de proveedor se limpian con
aviso las selecciones **nuevas** incompatibles; una línea **guardada** incompatible se conserva visible y marcada y bloquea
guardar y confirmar hasta elegir una permitida. Con el mismo proveedor, una guardada fuera del conjunto se conserva con aviso y el
borrador puede guardarse y confirmarse (D7; el backend preserva lo persistido). Si la lectura falla no se ofrece el catálogo
completo: aviso y guardar bloqueado (confirmar un borrador ya guardado no depende de la lectura). Una lectura fallida
tras cambiar de empresa tampoco reutiliza la configuración anterior; el resultado debe corresponder a la sesión de
empresa vigente. El backend revalida siempre.

## 6. Pendiente (no operativo)

Adjuntos, reportes operativos, filtros, exportación y aprobación: planificación en
[ROADMAP](../ROADMAP.md#etapa-1--completar-erp-core).

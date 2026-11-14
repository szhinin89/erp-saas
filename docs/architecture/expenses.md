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

- API `api/v1/master/business-partners/{bpId}/expense-subcategories`: `GET` (`masterdata.businesspartners.view`)
  devuelve modo, asignaciones y subcategorías disponibles; `PUT` (`…configure-company`) fija el conjunto de
  forma atómica: crea, **reactiva el mismo registro** y retira con baja lógica; `[]` retira todas (vuelve a
  `Unconfigured`); sin DELETE físico; cada alta, retiro y reactivación genera su registro de auditoría.
- Validación en Gastos (`ExpenseDraftRules`): crear borrador, crear-confirmado y editar con cambio de proveedor
  exigen subcategorías permitidas; editar con el mismo proveedor conserva las ya persistidas del borrador y solo
  valida lo nuevo; **confirmar un borrador existente no revalida** (422 «La subcategoría 'X' no está permitida
  para el proveedor.» / «…ninguna está disponible.»). Anular y consultar no se ven afectados.
- Aún sin interfaz: la ficha del proveedor y el formulario de Gastos no usan esta configuración
  (ver § 6).

## 4. Permisos

Catálogo: `expenses.catalog.{view,create,update,activate,deactivate}`. Documentos:
`expenses.documents.{view,create,update,confirm,cancel}` (confirmar y `confirmed` exigen `confirm`).
Permiso = quién puede; el estado del documento = en qué etapa está ([Seguridad](./security.md)).

## 5. Frontend

`ExpenseDocumentFormPage` (cabecera, líneas, totales, retención) y `ExpenseCategoriesPage` (árbol en
`ZHModal`). Selector en cascada Tipo → Categoría → Subcategoría: solo nodos activos y con cuenta visible.
Al cambiar Tipo o Categoría, una subcategoría que deja de ser válida se limpia. El cálculo de líneas usa
`decimal.js` half-up equivalente al backend.

## 6. Pendiente (no operativo)

Interfaz de subcategorías por proveedor (ficha del proveedor, filtro y autoselección en el formulario, «Quitar
restricción»): **diseño, sin implementar** — [diseño](../decisions/EXPENSE-SUPPLIER-SUBCATEGORIES-DESIGN-01.md).
Adjuntos, reportes operativos, filtros, exportación y aprobación: planificación en
[ROADMAP](../ROADMAP.md#etapa-1--completar-erp-core).

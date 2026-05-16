# FUNCIONALIDADES — ERP SaaS ZH Technologies

Arquitectura → `ARCHITECTURE.md` | Reglas → `CLAUDE.md` | Estado → `STATUS.md`

---

## Pantallas del frontend

### Auth / Sesión
| Ruta | Componente | Notas |
|------|-----------|-------|
| `/login` | `LoginPage.tsx` | Selector de empresa + credenciales |
| `/select-tenant` | `TenantSelectPage.tsx` | Selección de empresa tras bootstrap-login |
| `/forgot-password` | Solo email → envía correo con token |
| `/reset-password?token=…&tenantId=…` | Nueva contraseña + token |
| `/password-reset` | Modo directo (ID empresa + email + nueva contraseña) |

### Módulos de negocio
| Ruta | Componente | Permiso |
|------|-----------|---------|
| `/dashboard` | `DashboardPage.tsx` | — |
| `/products` | `modules/products/pages/ProductPage.tsx` | `inventario.products.view` |
| `/ventas/customers` | `modules/customers/pages/CustomersPage.tsx` | `ventas.customers.view` |
| `/ventas/facturas` | `modules/ventas/pages/VentasFacturasPage.tsx` | `ventas.facturas.view` |
| `/compras/proveedores` | `modules/compras/proveedores/pages/ProveedoresPage.tsx` | `compras.proveedores.view` |
| `/compras/ordenes` | `modules/compras/ordenes/pages/OrdenesCompraListPage.tsx` | `compras.ordenes.view` |
| `/compras/ordenes/nueva` | `CrearOrdenCompraPage.tsx` | `compras.ordenes.create` |
| `/compras/ordenes/:id` | `OrdenCompraDetailPage.tsx` | `compras.ordenes.view` |
| `/inventario/ajustes` | `modules/inventario/ajustes/pages/AjustesListPage.tsx` | `inventario.ajustes.view` |
| `/inventario/ajustes/nuevo` | `CrearAjustePage.tsx` | `inventario.ajustes.create` |
| `/inventario/ajustes/:id` | `AjusteDetailPage.tsx` | `inventario.ajustes.view` |
| `/inventario/transferencias` | `modules/inventario/transferencias/pages/TransferenciasListPage.tsx` | `inventario.transferencias.view` |
| `/inventario/transferencias/nueva` | `CrearTransferenciaPage.tsx` | `inventario.transferencias.create` |
| `/inventario/transferencias/:id` | `TransferenciaDetailPage.tsx` | `inventario.transferencias.view` |
| `/inventario/bodegas` | `pages/BodegasPage.tsx` | `inventario.bodegas.view` |
| `/accounting` | `pages/AccountingPage.tsx` | `accounting.accounts.view` |
| `/reportes/ventas` | `pages/SalesReportPage.tsx` | — |

### Catálogos
| Ruta | Componente |
|------|-----------|
| `/inventario/brands` | `BrandsCatalogPage` |
| `/inventario/product-types` | `ProductTypesCatalogPage` |
| `/inventario/units` | `UnitsCatalogPage` |
| `/inventario/tariffs` | `TariffsCatalogPage` |
| `/inventario/structure` | `CatalogStructurePage` (líneas, categorías, subcategorías) |
| ~~`/inventario/tax-rates`~~ | **Eliminado** — tarifas SRI son de solo lectura desde `sri_vat_rate` |

### Configuración / Admin
| Ruta | Componente | Rol |
|------|-----------|-----|
| `/access` | `TenantAccessPage.tsx` | Admin, SuperAdmin |
| `/profiles` | `ProfilesPage.tsx` | Admin, SuperAdmin |
| `/saas/branches` | `BranchesPage.tsx` | Admin, SuperAdmin |
| `/security` | `SecuritySettingsPage.tsx` | SuperAdmin |

### SuperAdmin
| Ruta | Componente |
|------|-----------|
| `/companies` | `CompaniesPage.tsx` — Datos / Plan y módulos / Plan↔menú / Auditoría |
| `/superadmin/overview` | `SuperAdminPanelPage.tsx` |
| `/superadmin/plans` | `SuperAdminPlansPage.tsx` |
| `/superadmin/navigation-menu` | `SuperAdminNavMenuPage.tsx` |

---

## Módulos backend — casos de uso por módulo

### Productos (`ERP.Application/Modules/Products/`)
- Crear / actualizar producto (con variantes, códigos de barra, impuestos)
- Activar / desactivar producto
- Listar con filtros y paginación (`GetProductReportQuery` + `GetProductReportRequest` DTO)
- Catálogos: Marcas, Tipos, Unidades, Líneas, Categorías, Subcategorías, Aranceles

### Clientes (`ERP.Application/Modules/Customers/`)
- Crear / actualizar cliente
- Activar / desactivar
- Listar con filtros

### Proveedores (`ERP.Application/Modules/Proveedores/`)
- Crear / actualizar proveedor (RUC ecuatoriano validado)
- Cambiar estado (activo / pendiente / inactivo)
- Listar con filtros

### Ventas (`ERP.Application/Modules/Sales/`)
- Crear factura electrónica (simulada SRI)
- Autorizar / anular factura
- Crear notas de crédito / débito
- Registrar retenciones recibidas (desde XML SRI)

### Compras (`ERP.Application/Modules/Compras/`)
- Crear / validar / aprobar / rechazar factura de compra
- Vincular factura a OC (con validación de precio ±1%)
- Actualizar stock al aprobar (+ asiento contable en transacción)

### Órdenes de Compra (`UseCases/OrdenesCompra/`)
- Crear OC (Borrador)
- Enviar al proveedor
- Aprobar (directo o desde Enviada)
- Cancelar
- Vincular factura electrónica aprobada
- Listar paginado con filtros (estado, proveedor, fechas)
- Detalle con líneas y facturas vinculadas
- Lista de OC pendientes por facturar

### Gastos (`ERP.Application/Modules/Gastos/`)
- Crear / validar / aprobar / rechazar gasto

### Inventario (`ERP.Application/Modules/Inventario/`)
- Ajuste de inventario (Borrador → Ejecutado / Cancelado)
- Transferencia entre bodegas (Borrador → Enviada → Recibida / Cancelada)
- Consulta de stock actual (`StockActual`)
- Registro de movimientos (`InventarioMovimiento`)

### Bodegas (`ERP.Application/Modules/Bodegas/`)
- CRUD de bodegas + activar / desactivar

### Contabilidad (`ERP.Application/Modules/Contabilidad/`)
- Plan de cuentas (árbol jerárquico)
- Asientos contables (journal entries)
- Config contable por empresa (mapeo de cuentas para asientos automáticos)

### Caja / Bancos
- Caja chica, cuentas bancarias, extractos, conciliación bancaria

### Accesos / Seguridad (`ERP.Application/Modules/Access/`)
- Perfiles de acceso con permisos granulares
- Membresías (usuario ↔ empresa)
- Bootstrap login + switch-tenant
- Registro de usuarios y alta inicial

### SaaS / SuperAdmin
- CRUD de tenants + suscripción
- CRUD de planes SaaS + features + ordering
- Menú dinámico por tenant (árbol editable)
- Config jerárquica (global → módulo → feature)
- Auditoría de actividad de usuario

---

## Endpoints principales (por módulo)

### Auth
```
POST /api/auth/register
POST /api/auth/login
POST /api/auth/superadmin-login
POST /api/auth/switch-tenant
POST /api/auth/refresh
POST /api/auth/forgot-password
POST /api/auth/reset-password
POST /api/auth/password-reset       ← modo directo
POST /api/setup/superadmin          ← first-run
POST /api/dev/reset-first-run       ← solo Development
```

### Acceso / Bootstrap
```
POST /api/access/bootstrap-login
POST /api/access/switch-tenant
GET  /api/access/superadmin/tenants
POST /api/access/superadmin/tenants
POST /api/access/memberships/grant
```

### Productos
```
GET    /api/products
GET    /api/products/{id}
POST   /api/products
PUT    /api/products/{id}
PATCH  /api/products/{id}/enable
PATCH  /api/products/{id}/disable
GET    /api/products/report
GET    /api/tax-rates               ← tarifas SRI (solo lectura)
```

### Clientes
```
GET    /api/customers
POST   /api/customers
PUT    /api/customers/{id}
PATCH  /api/customers/{id}/enable
PATCH  /api/customers/{id}/disable
```

### Proveedores
```
GET    /api/suppliers
POST   /api/suppliers
PUT    /api/suppliers/{id}
PATCH  /api/suppliers/{id}/status
```

### Órdenes de Compra
```
GET    /api/ordenes-compra
POST   /api/ordenes-compra
GET    /api/ordenes-compra/{id}
POST   /api/ordenes-compra/{id}/enviar
POST   /api/ordenes-compra/{id}/aprobar
POST   /api/ordenes-compra/{id}/cancelar
POST   /api/ordenes-compra/{id}/vincular-factura
GET    /api/ordenes-compra/pendientes-por-facturar
```

### Inventario
```
GET    /api/inventario/ajustes
POST   /api/inventario/ajustes
GET    /api/inventario/ajustes/{id}
POST   /api/inventario/ajustes/{id}/ejecutar
POST   /api/inventario/ajustes/{id}/cancelar

GET    /api/inventario/transferencias
POST   /api/inventario/transferencias
GET    /api/inventario/transferencias/{id}
POST   /api/inventario/transferencias/{id}/enviar
POST   /api/inventario/transferencias/{id}/recibir
POST   /api/inventario/transferencias/{id}/cancelar
```

### Contabilidad
```
GET    /api/accounts
POST   /api/accounts
GET    /api/accounts/{id}
GET    /api/accounts/journal-entries
POST   /api/accounts/journal-entries
```

### SuperAdmin / SaaS
```
GET    /api/superadmin/saas-plans
POST   /api/superadmin/saas-plans
PUT    /api/superadmin/saas-plans/{id}
DELETE /api/superadmin/saas-plans/{id}
GET    /api/superadmin/navigation-menu
PUT    /api/superadmin/navigation-menu/groups/reorder
PUT    /api/superadmin/navigation-menu/items/reorder-levels
POST   /api/superadmin/navigation-menu/items
GET    /api/tenants/{id}
PATCH  /api/tenants/{id}/company
PATCH  /api/tenants/{id}/subscription
GET    /api/me/menu                 ← menú de sesión del usuario autenticado
GET    /api/public/deployment       ← config pública (superAdminPanelEnabled, límites)
```

---

## Permisos — convención

Formato: `perm:modulo.recurso.accion`

Ejemplos:
```
perm:inventario.products.view
perm:inventario.products.create
perm:inventario.products.edit
perm:inventario.ajustes.view
perm:inventario.ajustes.create
perm:inventario.ajustes.execute
perm:inventario.transferencias.view
perm:compras.ordenes.view
perm:compras.ordenes.create
perm:compras.ordenes.send
perm:compras.ordenes.approve
perm:compras.ordenes.cancel
perm:compras.ordenes.link-invoice
perm:ventas.customers.view
perm:ventas.customers.create
perm:ventas.customers.update
perm:ventas.facturas.view
perm:accounting.accounts.view
perm:accounting.journal.view
perm:saas.branches.view
perm:access.memberships.view
perm:access.profiles.view
```

Los permisos se resuelven dinámicamente desde claims del JWT; no se registran en startup.

# Mapa de menú ERP — SSOT de navegación

**Tickets:** MAPA-MENU-ERP-SSOT-01, URLS-MENU-ALIGNMENT-01 · **Actualizado:** 2026-09-16

Fuente de verdad **ejecutable** del menú: `backend/src/ERP.Domain/Kernel/Modules/*.cs`
(atributos `[Module]`/`[NavItem]`, leídos por reflexión en `KernelRegistry`). Este documento es
la fuente **legible** — describe el árbol resultante y las reglas que lo gobiernan, pero si un día
diverge del código, **el código gana** (ver `docs/architecture/enforcement.md`).

El frontend nunca hardcodea el árbol: consume `GET /api/v1/me/menu` (`NavigationBuilder`,
filtrado por permisos del usuario) y lo renderiza recursivamente (`ZHAppLauncher` /
`LauncherCategoryGroup`, sin límite de profundidad). Los únicos mapas estáticos que quedan en
frontend son puramente cosméticos y no afectan qué aparece en el menú: el orden de fallback de la
barra principal para grupos sin `[Module]` real (`MAIN_NAV_GROUP_ORDER` en `nav/navConfig.ts` —
home/access/security/saas/plan-custom, sintéticos) y el glifo de ícono por código de módulo
(`MODULE_ICON_BY_ID` en `LauncherIcon.tsx`).

---

## Árbol oficial (12 módulos de nivel superior)

```
Clientes                                    [Module("customers")]
├─ Gestión de clientes
│  └─ Clientes                              /customers
└─ Cuentas por cobrar
   └─ Cuentas por cobrar                    /finance/receivables

Proveedores                                 [Module("suppliers")]
├─ Gestión de proveedores
│  └─ Proveedores                           /suppliers
├─ Cuentas por pagar
│  ├─ Cuentas por pagar                     /payables
│  ├─ Pagos a proveedores                   /supplier-payments
│  └─ Créditos de proveedor                 /suppliers/credits
└─ Condiciones comerciales
   ├─ Condiciones de Pago                   /suppliers/payment-terms
   └─ Condiciones de Crédito                /suppliers/credit-terms

Productos y servicios                       [Module("products")]
├─ Gestión de ítems
│  ├─ Productos                             /products/items
│  ├─ Tipos de Producto                     /products/item-types
│  ├─ Categorías de Productos                /products/categories
│  ├─ Marcas                                /products/brands
│  ├─ Atributos de Productos                /products/attribute-groups
│  └─ Definiciones de Atributos             /products/attribute-definitions
└─ Precios
   └─ Listas de Precios                     /products/pricing

Inventario                                  [Module("inventory")]
├─ Inventario
│  ├─ Bodegas                               /inventory/warehouses
│  ├─ Historial de Existencias (Kardex)     /inventory/kardex
│  ├─ Ajustes de inventario                 /inventory/adjustments
│  └─ Transferencias entre bodegas          /inventory/transfers
├─ Configuración
│  ├─ Preferencias de Inventario            /settings/operations?tab=inventory
│  └─ Motivos de ajuste                     /inventory/adjustment-reasons
└─ Reportes
   └─ Reporte de Inventario                 /reportes/stock

Ventas                                      [Module("sales")]
├─ Ventas
│  ├─ Facturas de venta / Punto de venta    /sales
│  └─ Devoluciones de venta                 /sales/returns
├─ Configuración
│  └─ Preferencias de Ventas/POS            /settings/operations?tab=salesPos
└─ Reportes
   └─ Reporte de Ventas                     /reportes/ventas

Compras                                     [Module("purchases")]
├─ Compras
│  ├─ Facturas de compra                    /purchases
│  ├─ Recepción electrónica (TXT)           /purchases/reception
│  └─ Notas de crédito de compra            /purchases/credit-notes
├─ Configuración
│  └─ Preferencias de Compras               /settings/operations?tab=purchases
└─ Reportes
   └─ Reporte de Compras                    /reportes/compras

Gastos                                      [Module("expenses")]
└─ Gastos
   ├─ Documentos de Gastos                  /expenses/documents
   └─ Catalogo de Gastos                    /expenses/categories

Tesorería                                   [Module("treasury")]
├─ Caja
│  ├─ Turno de Caja                         /treasury/cash
│  └─ Configuración
│     ├─ Cajas registradoras                /treasury/cash/registers
│     └─ Preferencias de Caja               /settings/operations?tab=cash
└─ Bancos
   └─ Destinos financieros                  /treasury/banks/financial-destinations

Contabilidad                                [Module("accounting")]
├─ Asientos
│  └─ Asientos contables                    /accounting/journal-entries
├─ Plan contable
│  └─ Plan de cuentas                       /accounting/chart-of-accounts
├─ Reportes
│  └─ Reportes contables                    /accounting/reports
└─ Configuración
   ├─ Reglas contables                      /accounting/configuration/posting-rules
   └─ Destinos contables
      └─ Cobros de ventas                   /accounting/configuration/sales-collection-destinations

SRI / Documentos electrónicos               [Module("sri")]
├─ Documentos electrónicos
│  └─ Monitor electrónico                   /sri/electronic-documents/monitor
└─ Configuración
   └─ Facturación electrónica               /sri/configuration/electronic-invoicing

Configuración                               [Module("settings")]
├─ Empresa
│  ├─ Mis empresas                          /companies
│  ├─ Datos de la empresa                   /settings/company
│  ├─ Branches (Sucursales)                 /settings/branches
│  ├─ Establishments (Establecimientos)     /settings/establishments
│  ├─ Emission Points (Puntos de emisión)   /settings/emission-points
│  └─ Geography (Geografía)                 /settings/geography
├─ Documentos y flujos
│  └─ Documentos y flujos                   /settings/document-flows
├─ Catálogos
│  └─ Bancos                                /settings/catalogs/banks
├─ Comunicaciones
│  └─ Correo SMTP                           /settings/communications/email
└─ Sistema
   ├─ Parámetros Generales                  /settings/operations
   └─ Carga Inicial                         /initial-load

Administración                              [Module("admin")]
├─ Usuarios y roles
│  ├─ Usuarios                              /access/users
│  ├─ Perfiles                              /admin/roles
│  └─ Permisos                              /admin/permissions
└─ Seguridad
   ├─ Seguridad administrativa              /admin/security
   ├─ Sesiones                              /admin/access/sessions
   └─ Actividad                             /admin/activity
```

### Desviaciones conocidas del árbol objetivo (documentadas, no accidentales)

El ticket pedía un grupo "Reportes" también en Clientes, Proveedores, Productos y servicios y
Gastos. No se agregó en esos cuatro módulos porque **no existe una pantalla de reporte real**
para esos dominios todavía (solo existen `/reportes/ventas`, `/reportes/compras` y
`/reportes/stock`) — el alcance del ticket prohíbe explícitamente crear pantallas nuevas. Cuando
exista una pantalla de reporte de clientes/proveedores/productos/gastos, agregar su `[NavItem]`
bajo un grupo "Reportes" en el módulo correspondiente siguiendo el mismo patrón que Ventas/
Compras/Inventario/Contabilidad.

---

## Regla de ubicación por tipo de pantalla

1. **Un `[Module]` = un tile del launcher (nivel 1).** Cada módulo agrupa un dominio de negocio
   reconocible por el usuario final, no una capa técnica. Compras/Gastos/Proveedores son tiles
   separados aunque comparten datos de negocio (BusinessPartner) porque son flujos operativos
   distintos para roles distintos (comprador vs. contador vs. gestor de proveedores).
2. **Ningún ítem plano bajo un módulo (NAV-HIERARCHY-UNIFY-01).** Todo módulo agrupa sus
   pantallas en al menos una categoría de nivel 2 (`PermissionsAnyCsv`, sin `Permission` propio).
   Una categoría puede anidar sub-categorías (nivel 3+) cuando el dominio lo justifica —p. ej.
   Contabilidad → Configuración → Destinos contables, o Tesorería → Caja → Configuración— sin
   límite de profundidad; el launcher ya soporta recursión arbitraria
   (`LauncherCategoryGroup`/`collectActiveTrailGroupKeys`).
3. **Una pantalla real (con endpoint/componente propio) es responsabilidad de un solo módulo.**
   Nunca se registra el mismo `[NavItem]` (mismo `Id`) en dos módulos a la vez — "no duplicar
   accesos" es una regla dura. Si una pantalla sirve a dos dominios (p. ej. el listado de formas
   de pago, usado tanto por Ventas/POS como por Contabilidad), el **endpoint** puede quedar
   compartido con autorización `[Authorize]` simple, pero el **NavItem** vive en un solo módulo
   (el que mejor representa su propósito principal).
4. **Contenedores sintéticos (`*/group`, `*/operation-group`, etc.) nunca tienen `<Route>` propio**
   en el frontend — no son pantallas, solo agrupan visualmente. `NavigationBuilder` los filtra
   igual que cualquier `NavItem` (por `PermissionsAnyCsv`), pero no renderizan contenido propio.
5. **No crear pantallas nuevas para completar el árbol.** Si el árbol objetivo pide una categoría
   sin pantalla real detrás (ver "Desviaciones conocidas" arriba), esa categoría queda fuera del
   menú hasta que la pantalla exista — nunca se agrega un `[NavItem]` "vacío" o decorativo.
6. **Evitar contenedores visibles sin hijos.** Un grupo que en un momento dado no resuelve a
   ningún hijo visible para el usuario actual (por permisos) simplemente no aparece —
   `NavigationBuilder.BuildInternalAsync` descarta grupos con `itemDtos.Count == 0`. Al diseñar un
   nuevo grupo, verificar que siempre tenga al menos un hijo real registrado (nunca un grupo
   creado "para el futuro").

## Política de URLs

- La URL de una pantalla real debe empezar con el prefijo del módulo al que pertenece
  conceptualmente (`/treasury/*` para Tesorería, `/sri/*` para SRI, `/accounting/*` para
  Contabilidad, etc.) — la URL visible debe ser coherente con dónde vive la pantalla en el menú,
  no con dónde vivió históricamente.
- Excepción explícita y permanente: los **deep-links a pestañas** de la pantalla única de
  Preferencias Operativas (`/settings/operations?tab=X`) no se mueven — no son una pantalla
  propia, son un ancla a una pestaña dentro de `OperationalPreferencesPage`, y moverlas
  fragmentaría esa pantalla en URLs que no existen.
- Rutas movidas en MAPA-MENU-ERP-SSOT-01:

  | Antes | Ahora |
  |---|---|
  | `/cash` | `/treasury/cash` |
  | `/cash/registers` | `/treasury/cash/registers` |
  | `/settings/financial-destinations` | `/treasury/banks/financial-destinations` |
  | `/electronic-documents/monitor` | `/sri/electronic-documents/monitor` |
  | `/settings/electronic-invoicing` | `/sri/configuration/electronic-invoicing` |
  | `/accounting/posting-rules` | `/accounting/configuration/posting-rules` |

- Rutas movidas en URLS-MENU-ALIGNMENT-01:

  | Antes | Ahora |
  |---|---|
  | `/inventory/items` | `/products/items` |
  | `/inventory/item-types` | `/products/item-types` |
  | `/catalog/tree` | `/products/categories` |
  | `/catalog/brands` | `/products/brands` |
  | `/catalog/attribute-groups` | `/products/attribute-groups` |
  | `/catalog/attribute-definitions` | `/products/attribute-definitions` |
  | `/pricing` | `/products/pricing` |
  | `/masterdata/customers` | `/customers` |
  | `/masterdata/suppliers` | `/suppliers` |
  | `/finance/supplier-credits` (+ `/:id`) | `/suppliers/credits` (+ `/:id`) |
  | `/master/payment-terms` | `/suppliers/payment-terms` |
  | `/finance/credit-terms` | `/suppliers/credit-terms` |

- Ruta nueva en BANK-CATALOG-01 (pantalla nueva, sin URL anterior — no aplica redirect legacy):
  `/settings/catalogs/banks`.

- Sin cambios (ya coherentes con su módulo, confirmados en URLS-MENU-ALIGNMENT-01): `/inventory/warehouses`,
  `/inventory/kardex`, `/inventory/transfers`, `/inventory/adjustments`,
  `/inventory/adjustment-reasons`, `/purchases/*`, `/expenses/*`, `/payables`,
  `/supplier-payments`, `/finance/receivables`, `/sales/*`, `/treasury/cash`,
  `/treasury/cash/registers`, `/treasury/banks/financial-destinations`,
  `/accounting/journal-entries`, `/accounting/chart-of-accounts`, `/accounting/reports`,
  `/accounting/configuration/posting-rules`,
  `/accounting/configuration/sales-collection-destinations`,
  `/sri/electronic-documents/monitor`, `/sri/configuration/electronic-invoicing`,
  `/settings/company`, `/settings/branches`, `/settings/establishments`,
  `/settings/emission-points`, `/settings/geography`, `/settings/document-flows`,
  `/settings/communications/email`, `/settings/operations`, `/initial-load`, `/admin/*`,
  `/access/*`.

## Política de redirects legacy

- Toda URL real que cambia de lugar deja un `<Route>` en el frontend que hace
  `<Navigate to="<url-nueva>" replace />` desde la URL anterior — nunca se elimina una ruta que
  pudo haber sido bookmarkeada o enlazada externamente (RIDE, emails, favoritos).
- El redirect es **solo de ruteo** (frontend, `catalogRoutes.tsx`/`mainRoutes.tsx`); nunca se
  duplica el `[NavItem]` en el backend — una sola entrada de menú por pantalla, siempre apuntando
  a la URL nueva.
- El redirect no tiene fecha de expiración por defecto — se retira solo si un ticket futuro
  confirma que no hay tráfico/enlaces externos activos a la URL vieja.

## Dashboard / Home

El Dashboard **no** es un `[NavItem]` de ningún módulo backend y **no** aparece en el launcher de
módulos (`ZHAppLauncher`). Vive exclusivamente en el header como "Inicio": un `NavItem` sintético
(`id: "synthetic-home-erp-dashboard"`, prefijo `synthetic-` para no colisionar con ids reales del
backend) que `ensureTenantHomeOverview` (`nav/navConfig.ts`) inyecta en el grupo `home`, y que
`ZHAppLauncher` filtra explícitamente fuera de la grilla de módulos antes de renderizarla. La ruta
`/dashboard` sigue existiendo y renderizando `DashboardPage` con normalidad — el usuario llega ahí
por el botón de Inicio del header, nunca desde el launcher de módulos.

---

## Historial

- **2026-09-16 — MAPA-MENU-ERP-SSOT-01**: primera versión de este documento. Reordenó el árbol a
  los 12 módulos de nivel superior descritos arriba; separó Compras y Gastos de Proveedores
  (revierte la consolidación de NAVIGATION-OPERATING-CYCLES-03, decisión explícita de este
  ticket); creó Tesorería (Caja + Bancos) y SRI/Documentos electrónicos como módulos nuevos;
  reubicó Reglas contables bajo Contabilidad → Configuración; aplicó la política de URLs anterior
  con redirects legacy; agregó `NavMenuGroupDto.SortOrder` para que el orden de los tiles del
  launcher deje de depender de una lista hardcodeada en frontend.
- **2026-09-16 — URLS-MENU-ALIGNMENT-01**: terminó de alinear las URLs visibles de Productos y
  servicios (`/inventory/*`, `/catalog/*`, `/pricing` → `/products/*`), Clientes
  (`/masterdata/customers` → `/customers`) y Proveedores (`/masterdata/suppliers` → `/suppliers`,
  `/finance/supplier-credits` → `/suppliers/credits`, `/master/payment-terms` →
  `/suppliers/payment-terms`, `/finance/credit-terms` → `/suppliers/credit-terms`) con el árbol de
  MAPA-MENU-ERP-SSOT-01 — esos módulos habían quedado con URLs heredadas de sus módulos de origen
  (`masterdata`/`catalog`/`master`/`finance`/`pricing`, ya disueltos). Confirmó sin cambios
  Inventario/Tesorería/Contabilidad/SRI/Configuración (ya coherentes). Se agregó
  `RouteAccessGuard.LEGACY_REDIRECT_PREFIXES` para `/masterdata`, `/catalog`, `/master` y
  `/pricing` — sin ningún NavItem activo produciendo ya esos prefijos, el guard de acceso los
  habría bloqueado antes de que el `<Navigate>` legacy llegara a montar.
- **2026-09-16 — BANK-CATALOG-01**: agregó Configuración → Catálogos → Bancos
  (`/settings/catalogs/banks`) — catálogo maestro de bancos (CRUD + activar/desactivar, sin
  cuenta contable ni relación con `PaymentMethodAccount`/Destinos financieros), tenant-wide sin
  CompanyId, seed mínimo Ecuador (9 bancos). Primera categoría "Catálogos" bajo Configuración;
  preparada para futuros catálogos maestros sin necesitar un módulo Tesorería/Bancos propio
  todavía.

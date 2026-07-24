# Frontend QA — Checklist de validación manual

**Arquitectura:** [`FRONTEND_ARCHITECTURE_BASELINE.md`](./FRONTEND_ARCHITECTURE_BASELINE.md)

Ejecutar en **Chrome/Edge** con ventanas **≥1280px** y **≤980px** (DevTools responsive).

Marcar: ✅ OK · ⚠️ defecto menor · ❌ bloqueante

---

## Pre-requisitos

- [ ] `npm run build` — exit 0
- [ ] `npm run lint` — 0 errores
- [ ] Backend + Docker según [`DEVELOPMENT.md`](./DEVELOPMENT.md)
- [ ] Usuario tenant Admin disponible
- [ ] Datos seed mínimos (clientes, productos, bodegas)

---

## Criterios transversales (todas las rutas)

| # | Verificación | Desktop | ≤980px |
|---|--------------|---------|--------|
| T1 | Un solo título de página visible (shell); sin `pg-header-row` duplicado | | |
| T2 | Sin scroll horizontal en el shell (solo tablas con scroll interno) | | |
| T3 | Modales centrados, legibles, botones accesibles | | |
| T4 | Tabs `.zh-form-tabs` — orden Datos → extras → listado | | |
| T5 | Estados vacío/carga con padding consistente (`pg-pad-40`) | | |
| T6 | Topbar/sidebar no tapa contenido al abrir modales | | |

---

## Tenant ERP

### Dashboard

- Ruta: `/dashboard`
- [ ] KPIs en grid; colapsa en ≤980px
- [ ] Sin overflow horizontal

### Ventas

| Ruta | Checks |
|------|--------|
| `/ventas/facturas` | KPIs, tabla `pg-overflow-x`, acciones print/ride, búsqueda |
| `/ventas/facturas/nueva` | Formulario cliente, tabla líneas scroll, totales, guardar borrador |
| `/ventas/notas-credito` | Listado estados, overflow |
| `/ventas/notas-credito/nueva` | Grid ítems, totales |

### Compras

| Ruta | Checks |
|------|--------|
| `/compras` | Listado, modal rechazo `pg-modal--sm` |
| `/compras/nueva` | Tabla líneas, totales |
| `/compras/ordenes` | Listado |
| `/compras/ordenes/nueva` | Formulario |
| `/compras/proveedores` | Tabs/ficha si aplica |

### Inventario

| Ruta | Checks |
|------|--------|
| `/inventario/bodegas` | Listado + modal |
| `/inventario/ajustes` | Listado |
| `/inventario/ajustes/nueva` | Panel stock hint (ok/warn) |
| `/inventario/transferencias` | Listado + detalle |

### Contabilidad

- Ruta: `/contabilidad`
- [ ] 5 tabs: scroll tablas, modales, sin inline visual roto
- [ ] Responsive en tabs con grids

### Catálogo

| Ruta | Checks |
|------|--------|
| `/catalogo/estructura` | Cascade 3 columnas → 1 col en ≤980px; modal |
| `/catalogo/marcas`, `/unidades`, `/tipos` | Listados |
| Categorías / subcategorías | PageShell + tabs |

### Clientes y sucursales

| Ruta | Checks |
|------|--------|
| `/clientes` | Tabs paneles, tablas overflow |
| `/sucursales` | Listado + modal ancho `pg-modal--lg` |

### Configuración

| Ruta | Checks |
|------|--------|
| `/settings/company` | Secciones empresa, tabs branches/sri |
| `/settings/sri` | Redirect a `/settings/company?tab=sri` — tab datos, ambiente, WSDL |
| `/settings/ride` | Logo, textarea footer |

### Otros tenant

| Ruta | Checks |
|------|--------|
| `/productos` | Tabs producto |
| `/perfiles` | Modal RBAC ancho |
| `/reportes/ventas` | Gráfico, tooltip, tabla |

---

## Auth (smoke)

| Ruta | Checks |
|------|--------|
| `/login` | Layout `zh-auth-*` |
| `/select-company` | Excepción documentada |

---

## Regresiones conocidas a vigilar

- Cascade catálogo: selección/hover solo CSS (sin estado hover React).
- Crear factura: dropdown cliente no cortado por overflow.

---

## Sign-off

| Rol | Nombre | Fecha | Resultado |
|-----|--------|-------|-----------|
| Dev | | | |
| QA | | | |
| PO | | | |

**Resultado global:** ☐ Aprobado para siguiente fase · ☐ Con defectos documentados · ☐ Bloqueado

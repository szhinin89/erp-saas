# Refactor modular por sprints (`Domain.Modules.*` / `Application.Modules.*`)

Objetivo a largo plazo: **cada módulo funcional** vive bajo rutas y namespaces predecibles:

| Capa | Ruta física (objetivo) | Namespace (objetivo) |
|------|------------------------|----------------------|
| Dominio | `ERP.Domain/Modules/{Modulo}/Entities`, `…/Interfaces`, … | `ERP.Domain.Modules.{Modulo}.Entities` |
| Aplicación | `ERP.Application/Modules/{Modulo}/UseCases/…` | `ERP.Application.Modules.{Modulo}.…` |

Los módulos hoy dispersos en `ERP.Domain/Ventas`, `Compras`, `Inventario`, etc. se **convergen** sin cambiar reglas de negocio: solo mover archivos + actualizar namespaces y `using`.

**Reglas:** un sprint = un PR revisable; tras cada sprint `dotnet build` + tests afectados en verde. No mezclar dos módulos en el mismo PR.

---

## Sprint 0 (hecho en el arranque del plan)

- [x] Documentar visión y criterios en [`ARCHITECTURE.md`](ARCHITECTURE.md).
- [x] **Ventas (dominio):** mover `VentasFactura`, `VentasDetalle`, `IVentasRepository` a `ERP.Domain/Modules/Ventas/` y namespaces `ERP.Domain.Modules.Ventas.*` (agregado pequeño y cohesivo).

---

## Sprint 1 — Dominio modular (Inventario, Compras, Gastos, Ventas/Clientes, Contabilidad) — **hecho**

- [x] `ERP.Domain/Inventario`, `Bodegas`, `Compras`, `Proveedores`, `Gastos`, `Customers` → `ERP.Domain/Modules/{Inventario,Compras,Gastos,Ventas}/…` con subcarpetas `Entities` / `Enums` / `Interfaces` / `ValueObjects` / `Events` (vacías donde aplica).
- [x] `ERP.Domain/Modules/Accounting` → `Modules/Contabilidad`; namespaces `ERP.Domain.Modules.Contabilidad.*` (antes `ERP.Domain.Accounting.*`).
- [x] **Application:** `Bodegas` y casos de bodega bajo `Modules/Inventario/UseCases` (carpetas en español: `CrearBodega`, …); `Proveedores` bajo `Modules/Compras`; clientes bajo `Modules/Ventas`; contabilidad en `Modules/Contabilidad` (`ERP.Application.Modules.Contabilidad.*`).
- [x] **Infrastructure:** configuraciones EF agrupadas en `Persistence/Configurations/{Inventario,Compras,Ventas,Gastos,Contabilidad}/`.
- [x] Migraciones / snapshot: cadenas CLR de tipos de dominio actualizadas a `ERP.Domain.Modules.*`.

---

## Sprint 2 — (siguiente) Productos, Auth, Tenants bajo `Modules/*` o `SharedKernel`

- [ ] Evaluar mover `ERP.Domain/Modules/Products` solo si se renombra namespace a criterio único del equipo.
- [ ] `ERP.Domain/Common` vs `Modules/SharedKernel`: definir qué es kernel compartido (entidades base, `Result`, etc.).

---

## Sprint 5 — Application: namespaces `Modules.{Modulo}` (resto)

- [ ] Unificar namespaces que hoy omiten `Modules` (p. ej. `ERP.Application.Ventas.*` → `ERP.Application.Modules.Ventas.*`).
- [ ] Mover `ERP.Application/Ventas/` (Models/Helpers) bajo `ERP.Application/Modules/Ventas/`.
- [ ] Repetir por módulo (Inventario, Compras) en sprints siguientes si el diff es grande.

---

## Sprint 6+ — Infra / tests / limpieza

- [ ] Revisar `ERP.Infrastructure` por `using` obsoletos y comentarios XML.
- [ ] Snapshot EF: los nombres CLR en migraciones históricas pueden quedar como referencia; el **modelo actual** debe usar los tipos nuevos (ya actualizado en el último snapshot al renombrar).
- [ ] Opcional: script `dotnet format` o analizador de capas (Architecture tests) que falle si se introduce `ERP.Domain.Ventas` de nuevo.

---

## Orden sugerido (cohesión → tamaño)

1. Ventas facturación (sprint 0) — hecho  
2. Inventario + Bodegas + Compras + Proveedores + Gastos + Customers + Contabilidad (sprint 1) — hecho  
3. Products / Auth / Tenants / `Common` → `SharedKernel` (sprint 2)  
4. Application: `Ventas` models bajo `Modules/Ventas`, limpieza de `using` duplicados  

Actualizar este checklist con `[x]` al cerrar cada sprint.

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

## Sprint 1 — Dominio: Gastos + Proveedores (o solo Gastos)

- [ ] Mover `ERP.Domain/Gastos/**` → `ERP.Domain/Modules/Gastos/**` (`ERP.Domain.Modules.Gastos.*`).
- [ ] Opcional mismo sprint: `Proveedores` si el PR sigue razonable (< ~15 archivos de dominio + usings).

**Salida:** handlers que usan `GastoFactura` actualizan `using`; sin cambio de comportamiento.

---

## Sprint 2 — Dominio: Compras

- [ ] Mover `ERP.Domain/Compras/**` → `ERP.Domain/Modules/Compras/**` (entidades + enums + interfaces de compra/orden).
- [ ] Revisar acoplamientos con `Proveedores` e `Inventario` (solo imports, sin fusionar contextos).

---

## Sprint 3 — Dominio: Inventario (+ Kardex si aplica)

- [ ] Mover `ERP.Domain/Inventario/**` → `ERP.Domain/Modules/Inventario/**`.
- [ ] Kardex (`KardexReporte`, `KardexSnapshot`) dentro del mismo módulo o subcarpeta `Inventario/Kardex` según convención de equipo.

---

## Sprint 4 — Dominio: Bodegas, Customers, ramas sueltas

- [ ] `Bodegas`, `Customers`, `Branches` (si siguen en raíz) → `Modules/Bodegas`, `Modules/Customers`, `Modules/Branches`.
- [ ] Alinear con tabla de módulos en `ARCHITECTURE.md`.

---

## Sprint 5 — Application: namespaces `Modules.{Modulo}`

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

1. Ventas (hecho sprint 0)  
2. Gastos  
3. Compras  
4. Inventario  
5. Bodegas / Customers / Proveedores  
6. Application namespaces + carpetas huérfanas  

Actualizar este checklist con `[x]` al cerrar cada sprint.

# Estado del Proyecto ERP SaaS — Mayo 2026

> **Documento de referencia rápida.** Leer este archivo al inicio de cada sesión para saber
> exactamente en qué punto está el proyecto, qué está hecho y qué sigue.
>
> **Enlaces legacy:** `docs/STATUS-2026-05-ERP.md` redirige aquí (un solo lugar para el estado).  
> **Backlog de refactor** y **refactor modular por sprints** (antes `docs/REFACTOR-*.md`) están en las secciones más abajo en este mismo archivo.
>
> Última actualización: **11 de mayo de 2026** (notas proveedor compras/gastos, reset DB desde cero, migración baseline única, seeder dev, ajustes de verificación local) | Commit: `0aab504`

---

## ¿Dónde estamos? (leer en 30 segundos)

El ERP SaaS tiene **backend completo** para los módulos de **Compras (facturas + órdenes de compra + retención en la fuente emitida + notas proveedor crédito/débito), Gastos, Inventario,
Transferencias entre Bodegas, Ajustes de Inventario**, **Ventas con facturación electrónica SRI Ecuador (simulada)** y **notas de crédito/débito** asociadas a facturas autorizadas, más **registro de retenciones recibidas** desde XML, **configuración contable por empresa** (mapeo de cuentas para asientos) y **Caja / bancos** (caja chica, cuentas bancarias, extractos, conciliación).
El frontend de Transferencias, Ajustes y **Órdenes de Compra** está implementado; hay **pantallas parciales** de contabilidad (config de cuentas por empresa, árbol de cuentas) e i18n para nuevas rutas.
Hay **cobertura automática activa** (cuatro proyectos de test; ver sección *Tests*) y la API corre en local con la configuración de desarrollo (HTTP 5003 / HTTPS 5001).

**Lo que falta para el MVP comercial:**
1. Implementar el WSDL real del SRI (firma P12 + envío + polling) — facturas, notas y comprobantes de retención en servicio real (`SriFacturaElectronicaRealService`, `SriComprobanteRetencionService` solo simulado hoy)
2. Frontend del módulo Ventas (pantallas de facturación; notas y retenciones recibidas pueden engancharse a la misma área)
3. Frontend de módulos Compras/Gastos facturas (pantallas) y de **retenciones emitidas** (listado / generar / enviar)

---

## Cómo retomar el trabajo hoy

```bash
# 1. Levantar base de datos
cd erp-saas
docker compose up -d          # PostgreSQL (5435) + Redis (6379); ver docker-compose.yml

# 2. Alinear la base con el modelo EF (tras pull o nueva migración)
cd backend/src
dotnet ef database update --project ERP.Infrastructure --startup-project ERP.API

# 3. Levantar API
cd ERP.API
dotnet run                    # HTTP 5003 + HTTPS 5001, Swagger en https://localhost:5001/swagger

# 4. Levantar Frontend
cd ../../../../frontend
npm run dev                   # Puerto 5173, proxy /api → localhost:5003

# 5. Correr todos los tests (no hay .sln — ejecutar por proyecto)
cd ../../../../backend
dotnet test src/ERP.API.Tests/ERP.API.Tests.csproj                     # 153 tests
dotnet test src/ERP.Application.Tests/ERP.Application.Tests.csproj     #  87 tests
dotnet test src/ERP.Domain.Tests/ERP.Domain.Tests.csproj               #  23 tests
dotnet test src/ERP.Infrastructure.Tests/ERP.Infrastructure.Tests.csproj #   3 tests
# Total: 266 tests (verificar con los comandos; cifras al 2026-05-11)
```

> **Credenciales de dev:** ver `backend/src/ERP.API/appsettings.Development.json`
> (copiado desde `.json.example`; no versionado).

---

## Stack técnico

| Capa | Tecnología |
|------|-----------|
| Backend | .NET 10, ASP.NET Core Web API, EF Core 10 |
| Base de datos | PostgreSQL 16 (Npgsql) |
| Frontend | React 19, Vite 8, TypeScript, Zustand |
| Auth | JWT (sesión + bootstrap token), BCrypt |
| Tests | xUnit, FluentAssertions, WebApplicationFactory |
| CI | GitHub Actions |
| i18n | Español (es) · Inglés (en) · **Kichwa de Cañar** (qu) |

---

## Arquitectura en una página

```
┌─────────────────────────────────────────────┐
│  ERP.API       Controllers / Middleware      │  ← HTTP, JWT, Swagger, Permisos
├─────────────────────────────────────────────┤
│  ERP.Application  Handlers / DTOs           │  ← CQRS con MediatR, FluentValidation
├─────────────────────────────────────────────┤
│  ERP.Infrastructure  EF Core / Repositorios │  ← PostgreSQL, Migraciones, Servicios externos
├─────────────────────────────────────────────┤
│  ERP.Domain    Entidades / Interfaces       │  ← Sin dependencias de frameworks
└─────────────────────────────────────────────┘
```

**Reglas clave:**
- Toda operación de escritura devuelve `Result<T>` (nunca lanza excepciones al controlador)
- Multi-tenancy por **Global Query Filter** en EF Core; `TenantId` del JWT
- Permisos: `perm:modulo.recurso.accion` resueltos dinámicamente sin registro en startup
- Módulo nuevo = 1 carpeta en cada capa; convención idéntica en todos los módulos

---

## Módulos completados ✅

### Plataforma SaaS / SuperAdmin
| Qué | Archivos clave |
|-----|---------------|
| Gestión de tenants, planes, features | `TenantsController`, `SaasPlansAdminController` |
| Menú dinámico por tenant (BD) | `ERP.Application/Modules/Navigation/` |
| Perfiles de acceso + permisos granulares | `ERP.Application/Modules/Access/` |
| Actividad de usuario (auditoría) | `ERP.Domain/Modules/Audit/` |
| Config jerárquica (global → módulo → feature) | `ERP.Domain/Modules/Configuration/` |

### Catálogos base
| Qué | Archivos clave |
|-----|---------------|
| Productos (CRUD, variantes, imágenes, códigos) | `ERP.Application/Modules/Products/` |
| Categorías, Marcas, UoM, Aranceles, Impuestos | `ERP.Application/Modules/Products/Catalogs/` |
| Clientes (maestro por tenant) | `ERP.Application/Modules/Customers/` |
| Sucursales + Geografía INEC Ecuador | `ERP.Application/Modules/Branches/` |
| Plan de cuentas + Asientos + **config. contable por empresa** | `ERP.Application/Modules/Contabilidad/` (`ConfiguracionContableController`, `CuentaContableService`) |

### Logística — completado el 09/05/2026
| Módulo | Flujo | Archivos clave |
|--------|-------|---------------|
| **Bodegas** | CRUD + disable/enable | `ERP.Application/Modules/Bodegas/` |
| **Proveedores** | CRUD + validación RUC ecuatoriano | `ERP.Application/Modules/Proveedores/` |
| **Parser SRI** | XML comprobantes recibidos | `ERP.Infrastructure/Services/SriFacturaParser.cs` |
| **Compras** | Borrador → Validado → Aprobado/Rechazado | `ERP.Application/Modules/Compras/` |
| **Gastos** | Borrador → Validado → Aprobado/Rechazado | `ERP.Application/Modules/Gastos/` |
| **Inventario** | StockActual + InventarioMovimiento | `ERP.Application/Modules/Inventario/` |

> Al aprobar una compra: se incrementa el stock (EntradaCompra) y se crea un asiento contable.
> Todo en una única transacción con rollback automático.

### Órdenes de Compra — completado el 10/05/2026 (commit `3076aa7`)

| Funcionalidad | Estado | Archivos clave |
|--------------|--------|---------------|
| Crear OC en Borrador (proveedor, fecha requerida, líneas con precio e IVA) | ✅ | `UseCases/CrearOrdenCompra/` |
| Enviar OC al proveedor (Borrador → Enviada) | ✅ | `UseCases/EnviarOrdenCompra/` |
| Aprobar OC (Borrador/Enviada → Aprobada) | ✅ | `UseCases/AprobarOrdenCompra/` |
| Cancelar OC (cualquier estado activo → Cancelada) | ✅ | `UseCases/CancelarOrdenCompra/` |
| Vincular factura electrónica aprobada → OC | ✅ | `UseCases/VincularFacturaAOrdenCompra/` |
| Cobertura total → OC cierra; cobertura parcial → RecibidaParcial | ✅ | `VincularFacturaAOrdenCompraCommandHandler.cs` |
| Validación de precio OC vs Factura (tolerancia 1%, advertencias no bloqueantes) | ✅ | `VincularFacturaAOrdenCompraCommandHandler.cs` + `OrdenCompraDto.Advertencias` |
| `CompraDetalle.OrdenCompraDetalleId` (nullable) — trazabilidad línea factura → línea OC | ✅ | `CompraDetalle.cs` + migración `AddOrdenCompraDetalleIdToCompraDetalle` |
| Listar paginado con filtros (estado, proveedor, fechas) | ✅ | `UseCases/GetOrdenesCompraList/` |
| Detalle con líneas y facturas vinculadas | ✅ | `UseCases/GetOrdenCompraById/` |
| Lista de OC pendientes por facturar | ✅ | `UseCases/GetOrdenesPendientesPorFacturar/` |
| Permisos: view / create / send / approve / cancel / link-invoice | ✅ | Migración `AddOrdenesCompra` (sentinel `77777777-...`) |
| Tests Moq (11): vinculación, estados, precio discrepante/coincidente/umbral | ✅ | `Compras/VincularFacturaAOrdenCompraCommandHandlerTests.cs` |
| Tests validador unit (12): cada regla de `CrearOrdenCompraCommandValidator` | ✅ | `Compras/CrearOrdenCompraCommandValidatorTests.cs` |
| Tests pipeline (4): ValidationException via MediatR behavior | ✅ | `Integration/OrdenCompraValidatorPipelineTests.cs` |
| Test E2E flujo completo (1): 2 productos, vincular parcial→Cerrada, rechazo exceso | ✅ | `Integration/OrdenCompraFlujoCompletoTests.cs` |
| Tests E2E básicos (5): crear, enviar/aprobar, cancelar, vincular total, parcial | ✅ | `Integration/OrdenesCompraEndToEndTests.cs` |
| Frontend: listado, crear con líneas dinámicas, detalle con acciones | ✅ | `frontend/src/modules/compras/ordenes/` |

**Flujo de estados OrdenCompra:**
```
Borrador ──enviar──► Enviada ──aprobar──► Aprobada ──[facturas]──► RecibidaParcial ──[completo]──► Cerrada
Borrador ──aprobar──────────────────────► Aprobada   (atajo: aprobación directa)
Cualquier estado activo ──cancelar──► Cancelada
```

**Reglas clave:**
- Número: `OC-{secuencial:D4}` (ej. `OC-0001`), único por tenant
- Solo OC en `Aprobada` o `RecibidaParcial` puede recibir facturas vinculadas
- La factura vinculada DEBE estar en estado `Aprobado`
- Matching por `ProductoId`; líneas sin producto (servicios/fletes de XML) se omiten
- Si toda la cantidad pedida queda cubierta → estado `Cerrada` automáticamente
- `SubscriptionFeatureCodes.Purchases = "COMPRAS"` (mismo feature gate que facturas de compra)
- Stock: la OC **no mueve** inventario; el stock se actualiza al aprobar la `CompraFactura`
- Precio: si la factura difiere >1% del precio acordado → `OrdenCompraDto.Advertencias[]` (no bloquea)
- Trazabilidad: `CompraDetalle.OrdenCompraDetalleId` (nullable) se establece al vincular

**Extras diferidos (documentados en memoria del proyecto):**
- Recepción física sin factura: `RecibirOrdenCompraParcial` (actualiza stock sin factura; cuadre al llegar la factura)
- Tolerancia de precio configurable (actualmente hardcodeada al 1% en handler)
- Frontend: mostrar banner amarillo cuando respuesta de vincular incluye `advertencias != null`

---

### Ajustes de Inventario — completado el 10/05/2026 (commit `0c490e8`)
| Funcionalidad | Estado | Archivos clave |
|--------------|--------|---------------|
| Crear ajuste en Borrador (valida bodega, producto, cantidad ≠ 0) | ✅ | `UseCases/CrearAjuste/` |
| Ejecutar (actualiza stock atómico, UPDATE WHERE disponible >= delta) | ✅ | `UseCases/EjecutarAjuste/` |
| Cancelar (solo en Borrador, sin efecto en stock) | ✅ | `UseCases/CancelarAjuste/` |
| Listar paginado con filtros | ✅ | `UseCases/GetAjustesList/` |
| Obtener detalle | ✅ | `UseCases/GetAjusteById/` |
| InventarioMovimiento: AjustePositivo / AjusteNegativo | ✅ | `EjecutarAjusteCommandHandler.cs` |
| Permisos: view / create / execute / cancel | ✅ | Migración `AddAjustesInventario` (sentinel `66666666-...`) |
| Auditoría triple: AuditableEntity + EjecutadoPor + UserActivity | ✅ | Entidad + handlers |
| Frontend completo (listado, formulario, detalle) | ✅ | `frontend/src/modules/inventario/ajustes/` |

**Flujo de estados AjusteInventario:**
```
Borrador ──ejecutar──► Ejecutado  (stock actualizado: +/− CantidadAjuste)
         ──cancelar──► Cancelado  (sin efecto en stock)
```

**Reglas clave:**
- `CantidadAjuste` con signo: positivo = `Incremento` (AjustePositivo), negativo = `Disminucion` (AjusteNegativo)
- El stock se mueve al **Ejecutar** con SQL atómico `UPDATE WHERE disponible >= delta` — resiste carrera concurrente
- `BodegaNombre` y `ProductoNombre` se denormalizan al crear (auditoría permanente aunque cambien los catálogos)
- `inventario.ajustes.execute` solo se habilita en seed para perfiles con `compras.facturas.approve`, `ventas.facturas.emit` o `inventario.transferencias.confirm`
- Número: `AJ-{secuencial:D4}` (ej. `AJ-0001`), único por tenant

**Extras diferidos (documentados en memoria del proyecto):**
- Aprobación supervisora: estado `PendienteAprobacion` + campo `AprobadoPor` + permiso `inventario.ajustes.approve`
- Reversar ajuste: `ReversarAjusteCommand` crea ajuste espejo, lo ejecuta, marca el original como `Revertido`

---

### Transferencias entre Bodegas — completado el 09/05/2026 (commit `911b4eb`)
| Funcionalidad | Estado | Archivos clave |
|--------------|--------|---------------|
| Crear transferencia (Borrador, valida stock origen) | ✅ | `UseCases/CrearTransferencia/` |
| Confirmar (mueve stock origen → destino, atómico) | ✅ | `UseCases/ConfirmarTransferencia/` |
| Cancelar (solo en Borrador, sin efecto en stock) | ✅ | `UseCases/CancelarTransferencia/` |
| Listar paginado con filtros | ✅ | `UseCases/GetTransferenciasList/` |
| Obtener detalle con ítems | ✅ | `UseCases/GetTransferenciaById/` |
| InventarioMovimiento: TransferenciaSalida + TransferenciaEntrada | ✅ | `ConfirmarTransferenciaCommandHandler.cs` |
| Permisos: view / create / confirm / cancel | ✅ | Migración `AddTransferenciasInventario` |

**Flujo de estados Transferencia:**
```
Borrador ──confirmar──► Confirmado  (stock movido: origen −X, destino +X)
         ──cancelar──►  Cancelado   (sin efecto en stock)
```

**Reglas clave:**
- El stock solo se valida y mueve al **Confirmar** (operación atómica con IUnitOfWork)
- Si hay stock insuficiente en cualquier ítem → rollback completo, ningún stock se mueve
- El número de transferencia es `TR-{secuencial:D4}` (ej. `TR-0001`), único por tenant

### Ventas — completado el 09/05/2026
| Funcionalidad | Estado | Archivos clave |
|--------------|--------|---------------|
| Crear factura (validar stock) | ✅ | `UseCases/CrearVenta/` |
| Validar (Borrador → Validado) | ✅ | `UseCases/ValidarVenta/` |
| Emitir al SRI (Validado → Autorizado) | ✅ simulado | `UseCases/EmitirFacturaElectronica/` |
| Reintentar envío (ErrorEnvio → SRI) | ✅ | `UseCases/ReintentarEnvio/` |
| Anular (Borrador/Validado → Anulado) | ✅ | `UseCases/AnularFactura/` |
| Descuento de inventario al emitir | ✅ | Handler + `InventarioMovimiento.SalidaVenta` |
| Asiento contable al emitir | ✅ | `AccountingService.CrearAsientoVentaAsync` |
| Configuración SRI por tenant | ✅ | `UseCases/UpsertConfiguracionSRI/` |
| Clave de acceso 49 dígitos (módulo-11) | ✅ | `Helpers/ClaveAccesoHelper.cs` |
| XML SRI v1.1.0 | ✅ simulado | `SriFacturaElectronicaSimuladoService.cs` |
| Firma digital P12 | 🔄 esqueleto | `SriFacturaElectronicaRealService.cs` |
| Envío WSDL SRI real | 🔄 esqueleto | `SriFacturaElectronicaRealService.cs` |

**Flujo de estados VentasFactura:**
```
Borrador ──validar──► Validado ──emitir──► Autorizado
                                    │
                              SRI rechaza──► Rechazado ──reintentar──► (repite emitir)
                                    │
                              Error de red──► ErrorEnvio ──reintentar──► (repite emitir)

Borrador, Validado, Rechazado, ErrorEnvio ──anular──► Anulado
Autorizado → NO se puede anular directamente (requiere Nota de Crédito)
```

---

### Notas de crédito/débito, retenciones y contabilidad ampliada — completado el 11/05/2026 (commit `2bf300b`)

| Área | Estado | Archivos / notas clave |
|------|--------|-------------------------|
| **Notas ventas (dominio + EF)** | ✅ | `VentasNotaCreditoDebito`, `VentasNotaDetalle`; tablas `ventas_notas_credito_debito`, `ventas_nota_detalles` |
| **Crear / listar / enviar nota** | ✅ | `CrearVentasNotaCreditoDebitoCommand`, `GetVentasNotasListQuery`, `EnviarVentasNotaSriCommand` + handlers; `VentasNotasController` |
| **XML nota + SRI simulado** | ✅ | `ISriFacturaElectronicaService.GenerarXmlNotaCreditoDebitoAsync` en `SriFacturaElectronicaSimuladoService` (real: sin implementar) |
| **Asiento NC / ND** | ✅ | `AccountingService.CrearAsientoNotaCreditoVentaAsync`, `CrearAsientoNotaDebitoVentaAsync` (usa `ICuentaContableService` o heurística) |
| **Inventario NC** | ✅ | `NotaCreditoAutorizadaEvent` + `NotaCreditoAutorizadaEventHandler` → `DevolucionVenta`, reingreso de stock |
| **Retención emitida (compras)** | ✅ | `CompraRetencionEmitida`, detalle, `GenerarCompraRetencionEmitida`, `EnviarCompraRetencionEmitida`; `ISriComprobanteRetencionService` + `SriComprobanteRetencionSimuladoService`; `ComprasRetencionesController` |
| **Cálculo retención** | ✅ | `ConfiguracionRetencion` + `CompraRetencionCalculo` (IVA sobre `IvaTotal` de compra, RENTA sobre `Subtotal`) |
| **Retención recibida (ventas)** | ✅ | `VentasRetencionRecibida`, `RegistrarVentasRetencionRecibidaCommand`, `RetencionRecibidaXmlParser`; `VentasRetencionesRecibidasController` |
| **Asientos retención** | ✅ | `CrearAsientoRetencionEmitidaAsync`, `CrearAsientoRetencionRecibidaAsync` (requieren cuentas pasivo/activo acordes o heurística por nombre) |
| **Configuración contable por empresa** | ✅ | `ConfiguracionContableEmpresa`, `ConfiguracionGastoCategoria`, `CuentaContableService`, `ConfiguracionContableController`; migración `AddConfiguracionContablePorEmpresa` |
| **Módulo Caja / bancos** | ✅ | Dominio `ERP.Domain/Modules/Caja/`, handlers en `ERP.Application/Modules/Caja/`, `CajaController`, `CajaRepository`, migración `AddCajaBancosModule` |
| **Permisos nuevos** | ✅ | Migración `AddNotasCreditoDebitoRetenciones`: `ventas.notas.*`, `compras.retenciones.*`, `ventas.retenciones-recibidas.*` |
| **Tests** | ✅ | `NotasYRetencionesEndToEndTests`, `VentasNotaTotalsTests`, `CompraRetencionCalculoTests`; HTTP caja y config contable |

**Flujo resumido nota de crédito:** factura `Autorizado` → crear nota (ítems) → enviar SRI (simulado) → asiento + `Autorizado` + evento de stock en productos con inventario.

**Flujo retención compra:** compra `Aprobado` → tasas en `configuracion_retenciones` → generar borrador → enviar (XML simulado + asiento).

---

### Notas proveedor (compras/gastos) + reset de base desde cero — completado el 11/05/2026 (commit `0aab504`)

| Área | Estado | Archivos / notas clave |
|------|--------|-------------------------|
| **Notas proveedor (dominio + EF)** | ✅ | `CompraNotaProveedor`, `CompraNotaProveedorDetalle`; configuración EF en `Persistence/Configurations/Compras/*NotaProveedor*` |
| **Importar XML nota proveedor** | ✅ | `ImportarCompraNotaProveedorCommand` + handler (`IXmlFacturaParser.ParseNotaProveedorAsync`) |
| **Aprobar nota proveedor (evento)** | ✅ | `AprobarCompraNotaProveedorCommand` + `CompraNotaProveedorAprobadaEvent` |
| **Inventario en notas proveedor (solo compras)** | ✅ | `CompraNotaProveedorAprobadaEventHandler` (`NotaCreditoProveedor` / `NotaDebitoProveedor`) |
| **Contabilidad NC/ND proveedor** | ✅ | `AccountingService`: asientos para compra y gasto (crédito/débito) |
| **API + permisos** | ✅ | `ComprasNotasProveedorController` (`view/create/approve`) |
| **Pruebas integración** | ✅ | `NotasYRetencionesEndToEndTests` con escenarios NC compra, ND compra, NC gasto |
| **Reset DB baseline único** | ✅ | eliminación de historial de migraciones + nueva `InitialCreate` |
| **Seeder dev mínimo** | ✅ | `DevDatabaseSeeder` (tenant demo, admin, bodega principal, cuentas base, IVA 12/0) |
| **Verificación local alineada** | ✅ | Swagger en `https://localhost:5001/swagger`; alias `GET /api/logistica/bodegas` |

---

## Tests actuales — 266 tests (fuente: `dotnet test`)

> **Mantenimiento:** no copiar desgloses por archivo aquí (se desactualizan en cada PR). Tras cambios relevantes, volver a ejecutar los cuatro proyectos y actualizar solo la tabla de totales.

```
ERP.Domain.Tests         →  23 tests   (entidades, value objects, RUC ecuatoriano)
ERP.Application.Tests    →  87 tests   (handlers Moq, validators, behaviors, DTOs)
ERP.Infrastructure.Tests →   3 tests   (repositorios, parser XML SRI)
ERP.API.Tests            → 153 tests   (integración E2E, HTTP, notas/retenciones, caja/config contable, dominio, algoritmos)
──────────────────────────────────────────
TOTAL                    → 266 tests   ✅ 0 fallos (Release, 2026-05-11)
```

CI ejecuta el conjunto vía `dotnet test backend/src/ERP.slnx` (ver ADR 0003 y `.github/workflows/ci.yml`).

---

## API REST — Endpoints disponibles hoy

| Módulo | Base URL | Nro. endpoints |
|--------|----------|---------------|
| Auth | `/api/auth` | 5 |
| IAM (acceso, perfiles, permisos) | `/api/access` | 12+ |
| Tenants / SaaS Admin | `/api/tenants`, `/api/saas-plans-admin` | 15+ |
| Productos + catálogos | `/api/products`, `/api/brands`… | 30+ |
| Contabilidad | `/api/accounts`, `/api/journal-entries`, `/api/configuracion-contable` | 8+ |
| Clientes | `/api/customers` | 6 |
| Sucursales + Geografía | `/api/branches`, `/api/geography` | 8 |
| Bodegas | `/api/bodegas` | 6 |
| Bodegas (alias compatibilidad) | `/api/logistica/bodegas` | 6 |
| Proveedores | `/api/proveedores` | 6 |
| Compras | `/api/compras` | 7 |
| Gastos | `/api/gastos` | 7 |
| Inventario (stock) | `/api/inventario` | 2 |
| **Transferencias** | **`/api/inventario/transferencias`** | **5** |
| **Ajustes inventario** | **`/api/inventario/ajustes`** | **5** |
| **Órdenes de Compra** | **`/api/compras/ordenes`** | **8** |
| **Ventas** | **`/api/ventas`** | **8** |
| **Ventas — notas crédito/débito** | **`/api/ventas/notas`** | **3** |
| **Ventas — retenciones recibidas** | **`/api/ventas/retenciones-recibidas`** | **2** |
| **Compras — retenciones emitidas** | **`/api/compras/retenciones`** | **3** |
| **Compras — notas proveedor** | **`/api/compras/notas-proveedor`** | **3** |
| **Caja** | **`/api/caja`** | **varios** |
| **Config SRI** | **`/api/configuracion-sri`** | **2** |
| SuperAdmin | `/api/superadmin`, `/api/setup` | 10+ |

---

## Migraciones EF Core

Desde el **11/05/2026** se mantiene una **baseline única** en `backend/src/ERP.Infrastructure/Migrations/` (`InitialCreate`) generada desde el modelo actual.  
Si necesitas reset completo local, usa el flujo de esquema limpio + `dotnet ef database update` (sin re-aplicar cadena histórica de migraciones viejas).

```bash
cd backend
dotnet ef migrations list --project src/ERP.Infrastructure --startup-project src/ERP.API
dotnet ef database update --project src/ERP.Infrastructure --startup-project src/ERP.API
```

**Referencia histórica:** la cadena antigua de migraciones fue consolidada en `InitialCreate`; para trazabilidad funcional usar el historial de commits y esta sección de estado.

> **No** fijar en este archivo un contador “N aplicadas / M pendientes”: eso solo tiene sentido comparando `migrations list` con el estado real de la base.

---

## Hoja de ruta — Lo que falta

### Prioridad 1 — SRI Real (BLOQUEANTE para producción)

**Archivo a implementar:** `backend/src/ERP.Infrastructure/Services/SriFacturaElectronicaRealService.cs`

Los tres métodos están esqueletizados con logging y `SriCommunicationException` lista:

```csharp
// 1. GenerarXmlFacturaAsync — ya existe versión simulada como referencia
//    Implementar: validar estructura contra XSD oficial SRI v1.1.0
//    Referencia: SriFacturaElectronicaSimuladoService.cs (misma clase, misma lógica base)

// 2. FirmarXmlAsync
//    Implementar: XAdES-BES con System.Security.Cryptography / paquete NuGet
//    Input: xmlContent (string), p12Path (ruta al certificado), password
//    Output: byte[] del XML firmado

// 3. EnviarAlSriAsync
//    Implementar: consumir WSDL RecepcionComprobantesOffline?wsdl (SRI pruebas/producción)
//    Luego: polling AutorizacionComprobantesOffline?wsdl con backoff
//    Mapear estados SRI: RECIBIDA, DEVUELTA, AUTORIZADA, NO_AUTORIZADA
//    URLs SRI pruebas: https://celcer.sri.gob.ec/comprobantes-electronicos-ws/...
//    URLs SRI producción: https://cel.sri.gob.ec/comprobantes-electronicos-ws/...
```

**El cambio en Program.cs ya está listo** — en Production usa `SriFacturaElectronicaRealService` automáticamente:
```csharp
if (builder.Environment.IsDevelopment())
    services.AddScoped<ISriFacturaElectronicaService, SriFacturaElectronicaSimuladoService>();
else
    services.AddScoped<ISriFacturaElectronicaService, SriFacturaElectronicaRealService>();
```

---

### Prioridad 2 — Frontend Ventas + Compras/Gastos (Transferencias, Ajustes y Órdenes de Compra ya listos)

#### ✅ Ya implementados
- **Transferencias** — `frontend/src/modules/inventario/transferencias/` (listado, crear, detalle con Confirmar/Cancelar, stock en tiempo real)
- **Ajustes de Inventario** — `frontend/src/modules/inventario/ajustes/` (listado, crear con selector de motivo predefinido y stock disponible, detalle con Ejecutar/Cancelar)
- **Órdenes de Compra** — `frontend/src/modules/compras/ordenes/` (listado, crear con líneas dinámicas, detalle con Enviar/Aprobar/Cancelar/Vincular factura)

#### Pendiente — Ventas
```
frontend/src/modules/billing/ventas/
├── pages/
│   ├── VentasListPage.tsx          ← tabla paginada con filtros
│   ├── VentaDetailPage.tsx         ← detalle con líneas + estado + acciones
│   └── CrearVentaPage.tsx          ← formulario de creación
├── components/
│   ├── VentaEstadoBadge.tsx        ← chip de color por estado
│   ├── VentaItemsTable.tsx         ← tabla de líneas de la factura
│   └── EmitirFacturaButton.tsx     ← botón con confirmación
├── hooks/useVentas.ts              ← llamadas a /api/ventas
└── schemas/crearVentaSchema.ts     ← Zod validación
```

APIs Ventas disponibles: `GET/POST /api/ventas` · `PATCH /{id}/validar|emitir|reintentar|anular` · `GET /api/configuracion-sri`

#### Pendiente — Compras / Gastos (facturas)
- `frontend/src/modules/billing/compras/` → listar facturas de compra, crear desde XML/manual, aprobar
- `frontend/src/modules/billing/gastos/` → listar, crear, aprobar
> Las Órdenes de Compra ya tienen frontend; lo pendiente son las **facturas de compra** (CompraFactura)

---

### Prioridad 3 — Notas de crédito/débito y retenciones — **backend listo (2026-05-11)**

Implementado en dominio, aplicación, infraestructura, API y migración `AddNotasCreditoDebitoRetenciones`. Pendiente principal: **pantallas SPA** para crear/listar/enviar notas, registrar retenciones recibidas y flujo de retenciones en compras; **SRI real** para notas (`GenerarXmlNotaCreditoDebitoAsync` en servicio real) y retención.

---

### Prioridad 4 — Mejoras técnicas menores

| Tarea | Archivo | Dificultad |
|-------|---------|-----------|
| Paginación en Compras y Gastos (actualmente sin paginado) | `ICompraRepository`, `IGastoRepository` | Baja |
| `GetVentaByIdQuery` retorna `Success(null)` para no-encontrado → cambiarlo a `Failure` + 404 | `GetVentaByIdQueryHandler.cs` | Muy baja |
| Agregar `GetFacturaByIdWithDetailsAsync` en `IVentasRepository` (actualmente solo `GetFacturaByIdAsync`) | `IVentasRepository.cs` | Baja |
| Seed de `ConfiguracionSRI` de prueba en entorno dev | nueva migración o script | Baja |
| Reporte PDF de factura autorizada (QR con clave de acceso) | servicio nuevo | Media |
| Retención en la fuente (SRI real + UI) | backend simulado listo; WSDL real y pantallas | Media–Alta |
| **Ajustes — aprobación supervisora** (diferido): estado `PendienteAprobacion` + permiso `inventario.ajustes.approve` | `AjusteInventario.cs` + commands | Media |
| **Ajustes — reversar** (diferido): `ReversarAjusteCommand` crea ajuste espejo y lo ejecuta | nuevo command | Baja |
| **Transferencias — aprobación** (diferido): estado `PendienteAprobacion` antes de Confirmar | `Transferencia.cs` + commands | Media |
| **OC — tolerancia precio configurable**: mover el 1% a `ConfigModule` o `appsettings` | `VincularFacturaAOrdenCompraCommandHandler.cs` | Muy baja |
| **OC — recepción sin factura** (diferido): `RecibirOrdenCompraParcial` actualiza stock antes de tener factura | nuevo command + `CantidadRecibida` en `OrdenCompraDetalle` | Alta |
| **OC — advertencia en frontend**: mostrar banner cuando `OrdenCompraDto.Advertencias != null` | `OrdenCompraDetailPage.tsx` | Baja |

---

## Permisos por módulo

### Transferencias (migración `AddTransferenciasInventario`)

| Clave de permiso | Qué habilita |
|-----------------|-------------|
| `inventario.transferencias.view` | Ver listado y detalle de transferencias |
| `inventario.transferencias.create` | Crear transferencia en Borrador |
| `inventario.transferencias.confirm` | Confirmar (mueve el stock) |
| `inventario.transferencias.cancel` | Cancelar (solo en Borrador) |

Lógica de seed automático:
- Perfiles con `inventario.*.view` → obtienen `view`
- Perfiles con `inventario.bodegas.*` o `compras.facturas.create` → obtienen `create` + `cancel`
- Perfiles con `compras.facturas.approve`, `ventas.facturas.emit` o `accounting.journal.edit` → obtienen `confirm`
- Tenant de desarrollo (`d0aabb1f-...`) → obtiene todos los permisos

---

### Órdenes de Compra (migración `AddOrdenesCompra`)

| Clave de permiso | Qué habilita |
|-----------------|-------------|
| `compras.ordenes.view` | Ver listado y detalle de órdenes de compra |
| `compras.ordenes.create` | Crear OC en Borrador |
| `compras.ordenes.send` | Enviar OC al proveedor (→ Enviada) |
| `compras.ordenes.approve` | Aprobar OC (→ Aprobada) |
| `compras.ordenes.cancel` | Cancelar OC |
| `compras.ordenes.link-invoice` | Vincular factura de compra aprobada a la OC |

Lógica de seed automático:
- Perfiles con `compras.facturas.view` → obtienen `view`
- Perfiles con `compras.facturas.create` → obtienen `create` + `cancel`
- Perfiles con `compras.facturas.approve` → obtienen `send` + `approve` + `link-invoice`
- Tenant de desarrollo (`d0aabb1f-...`) → obtiene todos los permisos

---

### Ajustes de Inventario (migración `AddAjustesInventario`)

| Clave de permiso | Qué habilita |
|-----------------|-------------|
| `inventario.ajustes.view` | Ver listado y detalle de ajustes |
| `inventario.ajustes.create` | Crear ajuste en Borrador |
| `inventario.ajustes.execute` | Ejecutar (actualiza stock — solo para roles de confianza) |
| `inventario.ajustes.cancel` | Cancelar (solo en Borrador) |

Lógica de seed automático:
- Perfiles con `inventario.*.view` → obtienen `view`
- Perfiles con `inventario.bodegas.*` o `inventario.transferencias.create` → obtienen `create` + `cancel`
- Perfiles con `compras.facturas.approve`, `ventas.facturas.emit` o `inventario.transferencias.confirm` → obtienen `execute`
- Tenant de desarrollo (`d0aabb1f-...`) → obtiene todos los permisos

> ⚠️ **Nota de seguridad:** `inventario.ajustes.execute` es sensible — solo asignarlo a jefes de bodega o contadores. Un bodeguero puede **crear** pero no **ejecutar** sin autorización.

---

### Ventas (migración `Paso3_VentasFacturaAsientoAndPermissions`)

Sembrados automáticamente:

| Clave de permiso | Qué habilita |
|-----------------|-------------|
| `ventas.facturas.view` | Ver listado y detalle de facturas |
| `ventas.facturas.create` | Crear nueva factura |
| `ventas.facturas.validate` | Validar (Borrador → Validado) |
| `ventas.facturas.emit` | Emitir al SRI + reintentar |
| `ventas.facturas.cancel` | Anular factura |
| `ventas.stock.view` | Consultar stock disponible |
| `ventas.configuracion.view` | Ver configuración SRI del tenant |
| `ventas.configuracion.edit` | Modificar configuración SRI |

El seed asigna permisos automáticamente según permisos existentes del perfil:
- Perfiles con `inventario.*.view` → obtienen `view` y `stock.view`
- Perfiles con `inventario.*.create` → obtienen `create` + `validate`
- Perfiles con `compras.facturas.approve` → obtienen `emit` + `cancel`
- Tenant de desarrollo (`d0aabb1f-...`) → obtiene todos los permisos

---

### Notas de venta y retenciones (migración `AddNotasCreditoDebitoRetenciones`)

| Clave de permiso | Qué habilita |
|-----------------|-------------|
| `ventas.notas.create` | Crear nota de crédito/débito en borrador |
| `ventas.notas.send` | Validar (si aplica) y enviar nota al SRI |
| `ventas.notas.list` | Listar notas (filtro por factura y estado) |
| `compras.retenciones.create` | Generar comprobante de retención emitida desde compra aprobada |
| `compras.retenciones.send` | Enviar retención al SRI (simulado) |
| `compras.retenciones.list` | Listar retenciones emitidas |
| `ventas.retenciones-recibidas.create` | Registrar retención recibida (XML + factura venta) |
| `ventas.retenciones-recibidas.list` | Listar retenciones recibidas |

Seed en la migración: perfiles **Administrador / Contador / Vendedor** (y nombres en inglés) reciben permisos de notas y retenciones recibidas; **Comprador** además retenciones compras.

---

## Backlog de refactor (calidad y arquitectura)

Checklist para ir cerrando en PRs. Marca con `[x]` lo completado. Última revisión de lista: generada desde el estado del repo (commands sin validador, transacciones, CQRS, tenant, auditoría).

### P0 — Multi-tenant y seguridad (revisión) — **cerrado**

- [x] Inventario de usos de `IgnoreQueryFilters()` y comprobación explícita de `TenantId` donde aplique  
  - [x] `ERP.Infrastructure/Persistence/Repositories/UserRepository.cs` — `GetById`/`GetByEmail`/`Exists` con IQF + `TenantId` en predicado (registro/login sin tenant ambiente); IQF documentado en el resto de métodos.  
  - [x] `ERP.Infrastructure/Persistence/Repositories/AccessRepository.cs` — IQF solo donde el predicado fija `TenantId` o `IdentityUserId` (bootstrap / cupos); comentarios de clase.  
  - [x] `ERP.Infrastructure/Services/ConfigService.cs` — IQF + `TenantId` en todas las consultas; comentario de clase.  
  - [x] `ERP.Infrastructure/Persistence/SaasPlansAdminService.cs` — IQF en borrado global por `FeatureId` (todos los tenants); comentario inline.  
  - [x] `ERP.Infrastructure/Persistence/GrowthAnalyticsReader.cs` — IQF en memberships para agregados de plataforma; comentario.  
  - [x] `ERP.Infrastructure/BackgroundServices/KardexReporteProcessor.cs` — IQF por job sin HTTP; tenant aplicado vía `ManualCurrentTenant` tras cargar fila; comentario.  
  - [x] Otros hallazgos por `rg IgnoreQueryFilters` — sin usos adicionales fuera de la lista anterior.
- [x] Auditar controllers: `[Authorize]` vs política `perm:...` — **criterio unificado (aplicado / documentado)**  
  - **DefaultPolicy = `Session`** (`Program.cs`): JWT de sesión con `tenant_id` real; cualquier `[Authorize]` sin `Policy` hereda esto (no usar `FallbackPolicy` para no romper endpoints públicos).  
  - **`perm:recurso.acción`**: pantallas de negocio ERP; `PermissionHandler` valida plan + perfil; **Admin** y **SuperAdmin** (en tenant) pasan sin comprobar filas de perfil; SuperAdmin con `tenant_id = Guid.Empty` en claim pasa cualquier `perm:` (operador de plataforma).  
  - **`GlobalSuperAdmin`**: panel SaaS / planes / config global (`SuperAdminController`, `Saas*Admin`, `SuperAdminConfig`).  
  - **`Bootstrap`**: solo `switch-tenant` tras login IAM (token corto).  
  - **`[Authorize(Roles = "...")]`**: IAM donde aún no hay claves `perm:` (membresías globales, perfiles en `AccessController`, parte de `TenantsController`, `SecurityController`). No es inconsistencia: es capa identidad vs. capa menú/ERP.  
  - **Solo `Session` sin `perm:`** (explícito `Policy = "Session"` o `[Authorize]` en clase): catálogos de bajo riesgo o “datos propios” — `GeographyController`, `ActivityController`, `GET access/me/menu`, `GET access/me/permissions`.  
  - **Públicos**: `[AllowAnonymous]` en `AuthController`, `SetupController`, planes/deployment públicos, `TenantsController` `public-settings`, etc.  
  - **Regla para código nuevo**: si el endpoint expone datos de negocio del tenant y no es “solo menú / solo yo”, añadir `perm:...` alineado al menú y a `PermissionHandler`; si es operación solo Admin/SuperAdmin de plataforma, `Roles` o `GlobalSuperAdmin` según aplique.

### P1 — FluentValidation: comandos sin `*CommandValidator.cs` en su carpeta

#### Compras / órdenes

- [x] `ValidarCompraCommand`
- [x] `RechazarCompraCommand`
- [x] `AprobarCompraCommand` (validador además de la transacción existente)
- [x] `EnviarOrdenCompraCommand`
- [x] `AprobarOrdenCompraCommand`
- [x] `CancelarOrdenCompraCommand`
- [x] `VincularFacturaAOrdenCompraCommand`

#### Gastos

- [x] `ValidarGastoCommand`
- [x] `RechazarGastoCommand`
- [x] `AprobarGastoCommand`

#### Inventario

- [x] `EjecutarAjusteCommand`
- [x] `CancelarAjusteCommand`
- [x] `CancelarTransferenciaCommand`
- [x] `RecalcularSnapshotsCommand`

#### Clientes

- [x] `UpdateCustomerCommand`
- [x] `DisableCustomerCommand`
- [x] `EnableCustomerCommand`

#### Bodegas / proveedores

- [x] `EnableBodegaCommand`
- [x] `DisableBodegaCommand`
- [x] `EnableProveedorCommand`
- [x] `DisableProveedorCommand`

#### Sucursales

- [x] `UpdateBranchCommand`
- [x] `DisableBranchCommand`
- [x] `EnableBranchCommand`

#### Catálogo productos (catalogs)

- [x] `CreateBrandCommand`
- [x] `CreateProductCategoryCommand`
- [x] `CreateProductLineCommand`
- [x] `CreateProductSubcategoryCommand`
- [x] `CreateProductTypeCommand`
- [x] `CreateTariffCommand`
- [x] `CreateTaxRateCommand`
- [x] `CreateUnitOfMeasureCommand`
- [x] `UpdateProductCategoryCommand`
- [x] `UpdateProductLineCommand`
- [x] `UpdateProductSubcategoryCommand`

#### Acceso / auth / tenants / security

- [x] `BootstrapLoginCommand`
- [x] `RegisterTenantWithAdminCommand`
- [x] `SwitchTenantCommand` (módulo Access)
- [x] `UpsertMembershipCommand`
- [x] `RevokeMembershipCommand`
- [x] `UpsertProfilePermissionsCommand`
- [x] `LoginCommand`
- [x] `RegisterCommand`
- [x] `PasswordResetCommand`
- [x] `SuperAdminLoginCommand`
- [x] `ClaimInitialSuperAdminCommand`
- [x] `SwitchTenantCommand` (módulo Auth, si aplica mismo criterio)
- [x] `CreateTenantCommand`
- [x] `UpdateTenantCompanyCommand`
- [x] `UpdateTenantGlobalParametersCommand`
- [x] `UpdateTenantSubscriptionCommand`
- [x] `UpdateTenantPasswordResetModeCommand`
- [x] `UpsertSecurityAdminScopesCommand`

### P2 — Transacciones explícitas (`IUnitOfWork.BeginTransactionAsync`)

**Ya cubiertos (referencia):** `AprobarCompra`, `EjecutarAjuste`, `ConfirmarTransferencia`, `EmitirFacturaElectronica`, `AprobarGasto`.

#### Candidatos a auditar / envolver en transacción

- [x] `CrearCompraCommandHandler`
- [x] `CrearVentaCommandHandler` (secuencial SRI + factura + actividad en una transacción; validación de stock sigue antes del `Begin`)
- [x] `ValidarVentaCommandHandler`
- [x] `CrearGastoCommandHandler`
- [x] `ValidarGastoCommandHandler`
- [x] `RechazarGastoCommandHandler`
- [x] `ValidarCompraCommandHandler`
- [x] `RechazarCompraCommandHandler`
- [x] `VincularFacturaAOrdenCompraCommandHandler`
- [x] `CrearTransferenciaCommandHandler`
- [x] `CancelarTransferenciaCommandHandler`
- [x] `CrearAjusteCommandHandler`
- [x] `CancelarAjusteCommandHandler`
- [x] `CreateJournalEntryCommandHandler`
- [ ] Otros comandos multi-agregado detectados en revisión

### P3 — CQRS estricto (comandos que devuelven `Result<*Dto>`) — **cerrado (decisión documentada)**

**Decisión:** **CQRS pragmático** para este monolito API + cliente SPA. Los comandos MediatR **pueden** devolver `Result<TDto>` (o DTOs de sesión / config) cuando el caso de uso ya tiene el agregado materializado y el contrato de respuesta es estable para la pantalla que dispara la acción. **No** se impone migrar masivamente a `Guid` / `Unit` + `GET` subsiguiente.

**Rationale breve:** menos ida-vuelta HTTP, menos desincronización entre “id devuelto” y vista, y coherencia con controladores que ya serializan el DTO del comando. Un CQRS “puro” (solo escritura + proyecciones en capa lectura) queda reservado para cuando exista read model explícito, proyección pesada o necesidad de caché HTTP (`ETag` / `304`) en un recurso GET dedicado.

**Cuándo valorar `Guid`/`Unit` + GET en comandos nuevos o refactors puntuales:** payload muy grande; composición de varias fuentes de lectura; mismo recurso consumido por clientes que solo leen y se benefician de GET cacheable; o testabilidad del handler de escritura aislando proyección.

**Ámbito al que aplica la convención (firmas actuales válidas; sin refactor forzado):**

- [x] Compras: `CrearCompra`, `ValidarCompra`, `RechazarCompra`, `AprobarCompra`, órdenes (`OrdenCompraDto`, etc.)
- [x] Gastos: `CrearGasto`, `ValidarGasto`, `RechazarGasto`, `AprobarGasto`
- [x] Config: `UpsertConfiguracionSRI`, `UpsertConfiguracionFacturacion` (y similares)
- [x] Inventario: ajustes / transferencias (DTOs de respuesta en comandos)
- [x] Maestros: productos, marcas, proveedores, bodegas, clientes, sucursales, contabilidad
- [x] Access: `RegisterTenantWithAdmin`, `BootstrapLogin`, `SwitchTenant` → `SessionResponseDto` (respuestas de flujo de identidad / sesión)

### P4 — Auditoría de dominio — **cerrado (política documentada)**

#### Jerarquía habitual (`AuditableEntity` y derivados)

- **`AuditableEntity`**: `CreatedAt` / `UpdatedAt` / `CreatedBy` / `UpdatedBy` — pensado para **mutaciones iniciadas por un usuario** (HTTP / comando con `ICurrentUser`).
- **`MasterEntity`**: hereda auditoría + `IsActive` + `Disable`/`Enable` — **catálogos y maestros** que no se borran físicamente.
- **`DocumentEntity`**: hereda auditoría + ciclo de vida **Borrador → Contabilizado → Anulado** — documentos transaccionales.
- **`ITenantEntity`**: solo exige `TenantId` — puede combinarse con las bases anteriores **o** usarse **sola** cuando aplique la excepción siguiente.

#### Excepción: `ITenantEntity` sin `AuditableEntity` (filas técnicas / materializadas)

**Regla:** una entidad puede implementar **solo** `ITenantEntity` (sin heredar `AuditableEntity`) cuando:

1. El ciclo de vida lo gobierna **código de infraestructura** (hosted services, colas, jobs) y no un flujo CRUD de pantalla con actor humano estable en cada paso, **y**
2. La trazabilidad relevante ya está modelada con **campos propios del dominio** (timestamps de job, estado, mensaje de error, JSON de resultado, etc.).

**Casos concretos en el repo:**

- [x] **`KardexReporte`**: cola de informes asíncronos; estados `Pendiente` → `Procesando` → `Completado`/`Error`; `SolicitadoEn` / `CompletadoEn` sustituyen el significado de auditoría genérica. No aplica `CreatedBy` único por transición de worker.
- [x] **`KardexSnapshot`**: saldo valorizado **calculado** por worker nocturno; `ComputadoEn` es la huella temporal. Recomputar sobrescribe métricas: no es un “update por usuario” al estilo maestro.

**No** se introduce `IAuditableEntity` en esta iteración: el tipado actual (`AuditableEntity` / `MasterEntity` / `DocumentEntity` / solo `ITenantEntity`) es suficiente si se respeta la regla anterior al añadir entidades nuevas.

### P5 — Front / contratos (opcional)

- [ ] Alinear manejo de errores de features nuevas con respuesta del `ExceptionMiddleware`
- [ ] Paridad validación cliente (p. ej. Zod) donde falte

### Cómo usar esta sección del backlog

1. Cada PR puede marcar ítems concretos con `[x]`.  
2. Si un ítem se divide en sub-tareas, enlazar el PR en una nota bajo el ítem.  
3. Regenerar listas P1 si se añaden comandos nuevos (script: carpeta `*Command.cs` sin `*CommandValidator.cs` en el mismo directorio).

---

## Refactor modular por sprints

Objetivo a largo plazo: **cada módulo funcional** vive bajo rutas y namespaces predecibles:

| Capa | Ruta física (objetivo) | Namespace (objetivo) |
|------|------------------------|----------------------|
| Dominio | `ERP.Domain/Modules/{Modulo}/Entities`, `…/Interfaces`, … | `ERP.Domain.Modules.{Modulo}.Entities` |
| Aplicación | `ERP.Application/Modules/{Modulo}/UseCases/…` | `ERP.Application.Modules.{Modulo}.…` |

Gran parte del dominio ya está bajo `ERP.Domain/Modules/{Modulo}/` (Ventas, Compras, Inventario, Gastos, Contabilidad, etc.); puede quedar código o carpetas fuera de ese patrón hasta cerrar los sprints siguientes — la convergencia es **mover archivos + namespaces** sin cambiar reglas de negocio.

**Reglas:** un sprint = un PR revisable; tras cada sprint `dotnet build` + tests afectados en verde. No mezclar dos módulos en el mismo PR.

### Sprint 0 (hecho en el arranque del plan)

- [x] Documentar visión y criterios en [`ARCHITECTURE.md`](ARCHITECTURE.md).
- [x] **Ventas (dominio):** mover `VentasFactura`, `VentasDetalle`, `IVentasRepository` a `ERP.Domain/Modules/Ventas/` y namespaces `ERP.Domain.Modules.Ventas.*` (agregado pequeño y cohesivo).

### Sprint 1 — Dominio modular (Inventario, Compras, Gastos, Ventas/Clientes, Contabilidad) — **hecho**

- [x] `ERP.Domain/Inventario`, `Bodegas`, `Compras`, `Proveedores`, `Gastos`, `Customers` → `ERP.Domain/Modules/{Inventario,Compras,Gastos,Ventas}/…` con subcarpetas `Entities` / `Enums` / `Interfaces` / `ValueObjects` / `Events` (vacías donde aplica).
- [x] `ERP.Domain/Modules/Accounting` → `Modules/Contabilidad`; namespaces `ERP.Domain.Modules.Contabilidad.*` (antes `ERP.Domain.Accounting.*`).
- [x] **Application:** `Bodegas` y casos de bodega bajo `Modules/Inventario/UseCases` (carpetas en español: `CrearBodega`, …); `Proveedores` bajo `Modules/Compras`; clientes bajo `Modules/Ventas`; contabilidad en `Modules/Contabilidad` (`ERP.Application.Modules.Contabilidad.*`).
- [x] **Infrastructure:** configuraciones EF agrupadas en `Persistence/Configurations/{Inventario,Compras,Ventas,Gastos,Contabilidad}/`.
- [x] Migraciones / snapshot: cadenas CLR de tipos de dominio actualizadas a `ERP.Domain.Modules.*`.

### Sprint 2 — (siguiente) Productos, Auth, Tenants bajo `Modules/*` o `SharedKernel`

- [ ] Evaluar mover `ERP.Domain/Modules/Products` solo si se renombra namespace a criterio único del equipo.
- [ ] `ERP.Domain/Common` vs `Modules/SharedKernel`: definir qué es kernel compartido (entidades base, `Result`, etc.).

### Sprint 5 — Application: namespaces `Modules.{Modulo}` (resto)

- [ ] Unificar namespaces que hoy omiten `Modules` (p. ej. `ERP.Application.Ventas.*` → `ERP.Application.Modules.Ventas.*`).
- [ ] Mover `ERP.Application/Ventas/` (Models/Helpers) bajo `ERP.Application/Modules/Ventas/`.
- [ ] Repetir por módulo (Inventario, Compras) en sprints siguientes si el diff es grande.

### Sprint 6+ — Infra / tests / limpieza

- [ ] Revisar `ERP.Infrastructure` por `using` obsoletos y comentarios XML.
- [ ] Snapshot EF: los nombres CLR en migraciones históricas pueden quedar como referencia; el **modelo actual** debe usar los tipos nuevos (ya actualizado en el último snapshot al renombrar).
- [ ] Opcional: script `dotnet format` o analizador de capas (Architecture tests) que falle si se introduce `ERP.Domain.Ventas` de nuevo.

### Orden sugerido (cohesión → tamaño)

1. Ventas facturación (sprint 0) — hecho  
2. Inventario + Bodegas + Compras + Proveedores + Gastos + Customers + Contabilidad (sprint 1) — hecho  
3. Products / Auth / Tenants / `Common` → `SharedKernel` (sprint 2)  
4. Application: `Ventas` models bajo `Modules/Ventas`, limpieza de `using` duplicados  

Actualizar este checklist con `[x]` al cerrar cada sprint.

---

## Archivos de referencia rápida

| Qué buscar | Dónde encontrarlo |
|-----------|-------------------|
| Controladores API | `backend/src/ERP.API/Controllers/` |
| Entidades de dominio | `backend/src/ERP.Domain/Modules/` y `ERP.Domain/{Modulo}/` |
| Casos de uso (handlers) | `backend/src/ERP.Application/Modules/` |
| Migraciones EF Core | `backend/src/ERP.Infrastructure/Migrations/` |
| Repositorios EF | `backend/src/ERP.Infrastructure/Persistence/Repositories/` |
| Servicios externos (SRI, contabilidad) | `backend/src/ERP.Infrastructure/Services/` |
| Tests integración E2E | `backend/src/ERP.API.Tests/Integration/` |
| Tests unitarios | `backend/src/ERP.API.Tests/Unit/` |
| Módulo Ventas — dominio | `ERP.Domain/Modules/Ventas/` (factura, detalle, cliente; namespaces `ERP.Domain.Modules.Ventas.*`) |
| Módulo Ventas — aplicación | `ERP.Application/Modules/Ventas/` |
| Módulo Transferencias — dominio | `ERP.Domain/Inventario/Entities/Transferencia*.cs` |
| Módulo Transferencias — aplicación | `ERP.Application/Modules/Inventario/UseCases/*Transferencia*/` |
| Módulo Transferencias — repositorio | `ERP.Infrastructure/Persistence/Repositories/TransferenciaRepository.cs` |
| Configuración SRI (algoritmo clave acceso) | `ERP.Application/Modules/Ventas/Helpers/ClaveAccesoHelper.cs` |
| Servicio SRI simulado | `ERP.Infrastructure/Services/SriFacturaElectronicaSimuladoService.cs` |
| Servicio SRI real (esqueleto) | `ERP.Infrastructure/Services/SriFacturaElectronicaRealService.cs` |
| Seed permisos Ventas | `ERP.Infrastructure/Migrations/20260509235417_Paso3_VentasFacturaAsientoAndPermissions.cs` |
| Seed permisos Transferencias | `ERP.Infrastructure/Migrations/20260510005830_AddTransferenciasInventario.cs` |
| Módulo Ajustes — dominio | `ERP.Domain/Inventario/Entities/AjusteInventario.cs` |
| Módulo Ajustes — aplicación | `ERP.Application/Modules/Inventario/UseCases/*Ajuste*/` |
| Módulo Ajustes — repositorio | `ERP.Infrastructure/Persistence/Repositories/AjusteInventarioRepository.cs` |
| Módulo Ajustes — frontend | `frontend/src/modules/inventario/ajustes/` |
| Seed permisos Ajustes | `ERP.Infrastructure/Migrations/20260510123203_AddAjustesInventario.cs` |
| Módulo OrdeneCompra — dominio | `ERP.Domain/Compras/Entities/OrdenCompra*.cs` + `IOrdenCompraRepository.cs` |
| Módulo OrdeneCompra — aplicación | `ERP.Application/Modules/Compras/UseCases/*OrdenCompra*/` + `VincularFactura*/` |
| Módulo OrdeneCompra — repositorio | `ERP.Infrastructure/Persistence/Repositories/OrdenCompraRepository.cs` |
| Módulo OrdeneCompra — frontend | `frontend/src/modules/compras/ordenes/` |
| Seed permisos OrdeneCompra | `ERP.Infrastructure/Migrations/20260510131553_AddOrdenesCompra.cs` |
| Notas crédito/débito + retenciones (dominio ventas/compras) | `VentasNotaCreditoDebito.cs`, `CompraRetencionEmitida.cs`, `VentasRetencionRecibida.cs`, `ConfiguracionRetencion.cs` |
| Notas — aplicación | `ERP.Application/Ventas/UseCases/Notas/`, `RetencionesRecibidas/` |
| Retenciones compras — aplicación | `ERP.Application/Modules/Compras/UseCases/Retenciones/` |
| XML nota / retención simulados | `SriFacturaElectronicaSimuladoService.cs`, `SriComprobanteRetencionSimuladoService.cs` |
| Tests notas y retenciones | `ERP.API.Tests/Integration/NotasYRetencionesEndToEndTests.cs`, `Unit/VentasNotaTotalsTests.cs`, `Unit/CompraRetencionCalculoTests.cs` |
| Migración notas + retenciones + permisos | `20260511202304_AddNotasCreditoDebitoRetenciones.cs` |
| Config contable empresa + caja | Migraciones `20260511182157_AddConfiguracionContablePorEmpresa`, `20260511195443_AddCajaBancosModule` |

---

## Comandos frecuentes

```bash
# Compilar el backend (desde raíz del repo)
cd backend && dotnet build src/ERP.API/ERP.API.csproj

# Correr todos los tests (no hay .sln — ejecutar por proyecto)
cd backend
dotnet test src/ERP.API.Tests/ERP.API.Tests.csproj
dotnet test src/ERP.Application.Tests/ERP.Application.Tests.csproj
dotnet test src/ERP.Domain.Tests/ERP.Domain.Tests.csproj
dotnet test src/ERP.Infrastructure.Tests/ERP.Infrastructure.Tests.csproj

# Filtrar tests de un módulo específico
dotnet test src/ERP.API.Tests/ERP.API.Tests.csproj --filter "FullyQualifiedName~Transferencias"
dotnet test src/ERP.API.Tests/ERP.API.Tests.csproj --filter "FullyQualifiedName~Ajustes"
dotnet test src/ERP.API.Tests/ERP.API.Tests.csproj --filter "FullyQualifiedName~Ventas"
dotnet test src/ERP.API.Tests/ERP.API.Tests.csproj --filter "FullyQualifiedName~NotasYRetenciones"

# Agregar migración (desde backend/)
dotnet ef migrations add NombreMigracion \
  --project src/ERP.Infrastructure \
  --startup-project src/ERP.API

# Aplicar migraciones pendientes
dotnet ef database update \
  --project src/ERP.Infrastructure \
  --startup-project src/ERP.API

# Ver estado de migraciones
dotnet ef migrations list \
  --project src/ERP.Infrastructure \
  --startup-project src/ERP.API

# Linting frontend
cd frontend && npm run lint

# Build frontend
cd frontend && npm run build
```

---

## Notas para el próximo desarrollador / sesión

1. **Leer este archivo primero.** Si hay diferencias con el código real, confiar en el código.

2. **Base de datos** — tras cada `git pull` que traiga migraciones nuevas, ejecutar `dotnet ef database update` (desde `backend/` con `--project src/ERP.Infrastructure --startup-project src/ERP.API`). Ver también la sección **Migraciones EF Core** más arriba.

3. **El servicio SRI Simulado es funcional en Development** — genera XML, simula autorización
   con un número aleatorio. Suficiente para probar toda la lógica de negocio.

4. **Para agregar un módulo nuevo**, seguir la misma estructura que Compras o Ventas:
   - Dominio preferentemente en `ERP.Domain/Modules/{Modulo}/` (y namespaces `ERP.Domain.Modules.{Modulo}.*`), o el patrón que lleve el módulo de referencia
   - Aplicación en `ERP.Application/Modules/{Modulo}/`
   - Repositorio en `ERP.Infrastructure/Persistence/Repositories/`
   - Configuración EF en `ERP.Infrastructure/Persistence/Configurations/`
   - Controlador en `ERP.API/Controllers/`
   - Migración con `dotnet ef migrations add`
   - Tests en `ERP.API.Tests/Integration/`

5. **Convención de permisos:**
   `perm:modulo.recurso.accion` — sembrar en migración SQL, no hardcodear en código.

6. **Result pattern siempre** — nunca lanzar excepciones desde handlers. Usar
   `Result<T>.Failure("mensaje")` para errores de negocio controlados.

---

*Próxima actualización de este documento: cuando avance el frontend de Ventas (facturas + notas/retenciones), Compras-Gastos facturas o la implementación SRI real.*

---

## Historial de cambios recientes

| Fecha | Commit | Qué se hizo |
|-------|--------|-------------|
| 11/05/2026 | `0aab504` | Notas proveedor (compras/gastos), tests E2E de NC/ND proveedor, baseline DB `InitialCreate`, seeder dev mínimo, Swagger HTTPS 5001 y alias `/api/logistica/bodegas` |
| 11/05/2026 | `2bf300b` | Notas crédito/débito ventas, retenciones emitidas/recibidas, config. contable por empresa, caja/bancos, permisos y tests (266 totales) |
| 09/05/2026 | — | Documentación alineada con el repo: totales de tests (248), ruta dominio Ventas, migraciones sin contador fijo “pendientes”, `docker compose` |
| 12/05/2026 | — | Documentación: `REFACTOR-BACKLOG.md` y `REFACTOR-MODULES-SPRINTS.md` fusionados en este archivo; enlaces y comentarios en código actualizados |
| 10/05/2026 | `3076aa7` | Validación precio OC vs Factura (tolerancia 1%, `Advertencias[]` en DTO) — 3 tests nuevos |
| 10/05/2026 | `307f0f9` | `CompraDetalle.OrdenCompraDetalleId` nullable para trazabilidad factura↔OC |
| 10/05/2026 | `907ba1d` | Tests: flujo completo 2 productos (E2E), validator unit (12), pipeline (4) |
| 10/05/2026 | `edfc711` | Módulo Órdenes de Compra completo (domain, application, infrastructure, API, frontend) |
| 10/05/2026 | `0c490e8` | Módulo Ajustes de Inventario completo |
| 09/05/2026 | `911b4eb` | Módulo Transferencias entre bodegas + tests FASE 4 |
| 09/05/2026 | `d20a14d` | Módulo Ventas con facturación electrónica SRI (simulada) |

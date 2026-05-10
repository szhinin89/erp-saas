# Estado del Proyecto ERP SaaS — Mayo 2026

> **Documento de referencia rápida.** Leer este archivo al inicio de cada sesión para saber
> exactamente en qué punto está el proyecto, qué está hecho y qué sigue.
>
> Última actualización: **10 de mayo de 2026** | Commit: ver git log

---

## ¿Dónde estamos? (leer en 30 segundos)

El ERP SaaS tiene **backend completo** para los módulos de **Compras (facturas + órdenes de compra), Gastos, Inventario,
Transferencias entre Bodegas, Ajustes de Inventario** y **Ventas con facturación electrónica SRI Ecuador** (simulada).
El frontend de Transferencias, Ajustes y **Órdenes de Compra** también está implementado.
Hay **184 tests automáticos pasando** y la API corre en producción-local.

**Lo que falta para el MVP comercial:**
1. Implementar el WSDL real del SRI (firma P12 + envío + polling)
2. Frontend del módulo Ventas (pantallas)
3. Frontend de módulos Compras/Gastos facturas (pantallas)
4. Notas de crédito (anulación de facturas autorizadas)

---

## Cómo retomar el trabajo hoy

```bash
# 1. Levantar base de datos
cd erp-saas
docker-compose up -d          # PostgreSQL en puerto 5435

# 2. Aplicar migración pendiente (IMPORTANTE — aún no aplicada)
cd backend/src
dotnet ef database update --project ERP.Infrastructure --startup-project ERP.API
# Añade: columna asiento_contable_id en ventas_facturas + permisos ventas.*

# 3. Levantar API
cd ERP.API
dotnet run                    # Puerto 5003, Swagger en /swagger

# 4. Levantar Frontend
cd ../../../../frontend
npm run dev                   # Puerto 5173, proxy /api → localhost:5003

# 5. Correr todos los tests (no hay .sln — ejecutar por proyecto)
cd ../../../../backend
dotnet test src/ERP.API.Tests/ERP.API.Tests.csproj                     # 82 tests
dotnet test src/ERP.Application.Tests/ERP.Application.Tests.csproj     # 63 tests
dotnet test src/ERP.Domain.Tests/ERP.Domain.Tests.csproj               # 23 tests
dotnet test src/ERP.Infrastructure.Tests/ERP.Infrastructure.Tests.csproj #  3 tests
# Total: 171 tests, todos deben pasar
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
| Plan de cuentas + Asientos contables | `ERP.Application/Modules/Accounting/` |

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

### Órdenes de Compra — completado el 10/05/2026

| Funcionalidad | Estado | Archivos clave |
|--------------|--------|---------------|
| Crear OC en Borrador (proveedor, fecha requerida, líneas con precio e IVA) | ✅ | `UseCases/CrearOrdenCompra/` |
| Enviar OC al proveedor (Borrador → Enviada) | ✅ | `UseCases/EnviarOrdenCompra/` |
| Aprobar OC (Borrador/Enviada → Aprobada) | ✅ | `UseCases/AprobarOrdenCompra/` |
| Cancelar OC (cualquier estado activo → Cancelada) | ✅ | `UseCases/CancelarOrdenCompra/` |
| Vincular factura electrónica aprobada → OC | ✅ | `UseCases/VincularFacturaAOrdenCompra/` |
| Cobertura total → OC cierra; cobertura parcial → RecibidaParcial | ✅ | `VincularFacturaAOrdenCompraCommandHandler.cs` |
| Listar paginado con filtros (estado, proveedor, fechas) | ✅ | `UseCases/GetOrdenesCompraList/` |
| Detalle con líneas y facturas vinculadas | ✅ | `UseCases/GetOrdenCompraById/` |
| Lista de OC pendientes por facturar | ✅ | `UseCases/GetOrdenesPendientesPorFacturar/` |
| Permisos: view / create / send / approve / cancel / link-invoice | ✅ | Migración `AddOrdenesCompra` (sentinel `77777777-...`) |
| Tests Moq (8): vinculación exitosa/parcial, OC/factura no encontrada, etc. | ✅ | `Compras/VincularFacturaAOrdenCompraCommandHandlerTests.cs` |
| Tests E2E (5): crear, enviar/aprobar, cancelar, vincular total, vincular parcial | ✅ | `Integration/OrdenesCompraEndToEndTests.cs` |
| Frontend: listado, crear, detalle con acciones | ✅ | `frontend/src/modules/compras/ordenes/` |

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

## Tests actuales — 184 tests, 0 fallos

```
ERP.Domain.Tests         →  23 tests   (entidades, Value Objects, RUC ecuatoriano)
ERP.Application.Tests    →  71 tests   (handlers Moq, behaviors, DTOs)
ERP.Infrastructure.Tests →   3 tests   (repositorios, parser XML SRI)
ERP.API.Tests            →  87 tests   (integración E2E, HTTP, dominio, algoritmos)
──────────────────────────────────────────
TOTAL                    → 184 tests   ✅ 0 fallos
```

Desglose `ERP.Application.Tests` (71):
```
Compras/AprobarCompraCommandHandlerStockTests.cs        →  2  (stock + rollback contabilidad)
Compras/VincularFacturaAOrdenCompraCommandHandlerTests.cs →  8  (Moq: éxito total/parcial, OC no encontrada, etc.)
Inventario/CrearTransferenciaCommandHandlerTests.cs     →  7  (Moq: stock, bodegas, productos)
Inventario/ConfirmarTransferenciaCommandHandlerTests.cs →  6  (Moq: atómico, concurrencia)
Inventario/EjecutarAjusteCommandHandlerTests.cs         →  8  (Moq: incremento/disminución/fallo)
Otros (customers, products, login, gastos, etc.)        → 40
```

Desglose `ERP.API.Tests` (87):
```
Unit/VentasDomainTests.cs                     → 12  (máquina de estados, totales)
Unit/ClaveAccesoHelperTests.cs                →  7  (algoritmo módulo-11, Theory)
Unit/StockValidationHandlerTests.cs           →  5  (stock Ventas: suficiente/insuficiente)
Unit/TransferenciasStockTests.cs              →  5  (stock Transferencias: 5 escenarios)
Integration/VentasEndToEndTests.cs            →  4  (flujo completo E2E)
Integration/VentasHttpTests.cs                → 10  (HTTP + JWT simulado)
Integration/TransferenciasEndToEndTests.cs    →  5  (crear→confirmar stock; cancelar; estados)
Integration/AjustesInventarioEndToEndTests.cs →  7  (incremento/disminución/fallo/validación)
Integration/OrdenesCompraEndToEndTests.cs     →  5  (crear, enviar/aprobar, cancelar, vincular total/parcial)
Integration/CompraGastoEndToEndTests.cs       →  2  (E2E compras + gastos)
Integration/AuthenticatedApiTests.cs          →  2  (HTTP con token)
Controller/VentasControllerContractTests.cs   →  9  (StubMediator, status codes)
Otros (middleware, contratos)                 → 14
```

---

## API REST — Endpoints disponibles hoy

| Módulo | Base URL | Nro. endpoints |
|--------|----------|---------------|
| Auth | `/api/auth` | 5 |
| IAM (acceso, perfiles, permisos) | `/api/access` | 12+ |
| Tenants / SaaS Admin | `/api/tenants`, `/api/saas-plans-admin` | 15+ |
| Productos + catálogos | `/api/products`, `/api/brands`… | 30+ |
| Contabilidad | `/api/accounts`, `/api/journal-entries` | 8 |
| Clientes | `/api/customers` | 6 |
| Sucursales + Geografía | `/api/branches`, `/api/geography` | 8 |
| Bodegas | `/api/bodegas` | 6 |
| Proveedores | `/api/proveedores` | 6 |
| Compras | `/api/compras` | 7 |
| Gastos | `/api/gastos` | 7 |
| Inventario (stock) | `/api/inventario` | 2 |
| **Transferencias** | **`/api/inventario/transferencias`** | **5** |
| **Ajustes inventario** | **`/api/inventario/ajustes`** | **5** |
| **Órdenes de Compra** | **`/api/compras/ordenes`** | **8** |
| **Ventas** | **`/api/ventas`** | **8** |
| **Config SRI** | **`/api/configuracion-sri`** | **2** |
| SuperAdmin | `/api/superadmin`, `/api/setup` | 10+ |

---

## Migraciones — 47 aplicadas, 3 pendientes

```bash
cd backend
dotnet ef migrations list --project src/ERP.Infrastructure --startup-project src/ERP.API
```

| Estado | Migración | Descripción |
|--------|-----------|-------------|
| ✅ | `InitialCreate` … `AddCustomersTable` | Auth, IAM, Productos, Clientes |
| ✅ | `TenantPlanAndModules` … `SeedSaasFeaturesPlanMenuLinking` | SaaS/Planes/Menú |
| ✅ | `AddHierarchicalConfigTables` | Config jerárquica |
| ✅ | `AddLogisticaProveedoresComprasGastos` | Bodegas, Proveedores, Compras, Gastos |
| ✅ | `AddComprasFullSchema` | Esquema completo Compras |
| ✅ | `SeedLogisticaAccessProfilePermissions` | Permisos logística |
| ✅ | `AddFacturacionElectronicaVentas` | Tablas ventas_facturas, ventas_detalles, configuracion_sri |
| **🔄 PENDIENTE** | **`Paso3_VentasFacturaAsientoAndPermissions`** | **asiento_contable_id + permisos ventas.*** |
| **🔄 PENDIENTE** | **`AddTransferenciasInventario`** | **tablas transferencias + transferencia_detalles + permisos inventario.transferencias.*** |
| **🔄 PENDIENTE** | **`AddAjustesInventario`** | **tabla ajustes_inventario + permisos inventario.ajustes.*** |
| **🔄 PENDIENTE** | **`AddOrdenesCompra`** | **tablas ordenes_compra + detalles + facturas + permisos compras.ordenes.*** |

> Las 4 migraciones se aplican con un solo comando: `dotnet ef database update`

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

### Prioridad 3 — Notas de Crédito

Anulación de facturas ya **Autorizadas** por el SRI. Requiere:

1. **Dominio:** nueva entidad `NotaCredito` (referencia a `VentasFactura` autorizada)
2. **SRI:** tipo de documento `04` (nota de crédito electrónica)
3. **Inventario:** `InventarioMovimiento.DevolucionVenta` → incrementa stock
4. **Contabilidad:** asiento inverso al de la venta original
5. **Flujo:** Borrador → Validado → Autorizado (igual que factura)

---

### Prioridad 4 — Mejoras técnicas menores

| Tarea | Archivo | Dificultad |
|-------|---------|-----------|
| Paginación en Compras y Gastos (actualmente sin paginado) | `ICompraRepository`, `IGastoRepository` | Baja |
| `GetVentaByIdQuery` retorna `Success(null)` para no-encontrado → cambiarlo a `Failure` + 404 | `GetVentaByIdQueryHandler.cs` | Muy baja |
| Agregar `GetFacturaByIdWithDetailsAsync` en `IVentasRepository` (actualmente solo `GetFacturaByIdAsync`) | `IVentasRepository.cs` | Baja |
| Seed de `ConfiguracionSRI` de prueba en entorno dev | nueva migración o script | Baja |
| Reporte PDF de factura autorizada (QR con clave de acceso) | servicio nuevo | Media |
| Retención en la fuente (módulo RETENCIONES SRI) | módulo nuevo | Alta |
| **Ajustes — aprobación supervisora** (diferido): estado `PendienteAprobacion` + permiso `inventario.ajustes.approve` | `AjusteInventario.cs` + commands | Media |
| **Ajustes — reversar** (diferido): `ReversarAjusteCommand` crea ajuste espejo y lo ejecuta | nuevo command | Baja |
| **Transferencias — aprobación** (diferido): estado `PendienteAprobacion` antes de Confirmar | `Transferencia.cs` + commands | Media |

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
| Módulo Ventas — dominio | `ERP.Domain/Ventas/` |
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

2. **Hay 4 migraciones PENDIENTES** — aplicar todas antes de usar la API en un entorno real:
   - `Paso3_VentasFacturaAsientoAndPermissions` — añade `asiento_contable_id` a `ventas_facturas` y siembra permisos del módulo Ventas
   - `AddTransferenciasInventario` — crea las tablas `transferencias` + `transferencia_detalles` y siembra permisos del módulo Transferencias
   - `AddAjustesInventario` — crea la tabla `ajustes_inventario` y siembra permisos del módulo Ajustes
   - `AddOrdenesCompra` — crea las tablas `ordenes_compra` + `detalles` + `facturas` y siembra permisos del módulo OrdeneCompra
   ```bash
   cd backend && dotnet ef database update --project src/ERP.Infrastructure --startup-project src/ERP.API
   ```

3. **El servicio SRI Simulado es funcional en Development** — genera XML, simula autorización
   con un número aleatorio. Suficiente para probar toda la lógica de negocio.

4. **Para agregar un módulo nuevo**, seguir la misma estructura que Compras o Ventas:
   - Dominio en `ERP.Domain/{Modulo}/`
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

*Próxima actualización de este documento: cuando se complete el frontend de Ventas/Compras-Gastos facturas o la implementación SRI real.*

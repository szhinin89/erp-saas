# Estado del Proyecto ERP SaaS — Mayo 2026

> **Documento de referencia rápida.** Leer este archivo al inicio de cada sesión para saber
> exactamente en qué punto está el proyecto, qué está hecho y qué sigue.
>
> Última actualización: **09 de mayo de 2026** | Commit: `911b4eb`

---

## ¿Dónde estamos? (leer en 30 segundos)

El ERP SaaS tiene **backend completo** para los módulos de **Compras, Gastos, Inventario,
Transferencias entre Bodegas** y **Ventas con facturación electrónica SRI Ecuador** (simulada).
Hay **141 tests automáticos pasando** y la API corre en producción-local.

**Lo que falta para el MVP comercial:**
1. Implementar el WSDL real del SRI (firma P12 + envío + polling)
2. Frontend del módulo Ventas (pantallas)
3. Frontend de módulos Compras/Gastos/Transferencias (pantallas)
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
dotnet test src/ERP.API.Tests/ERP.API.Tests.csproj                     # 75 tests
dotnet test src/ERP.Application.Tests/ERP.Application.Tests.csproj     # 40 tests
dotnet test src/ERP.Domain.Tests/ERP.Domain.Tests.csproj               # 23 tests
dotnet test src/ERP.Infrastructure.Tests/ERP.Infrastructure.Tests.csproj #  3 tests
# Total: 141 tests, todos deben pasar
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

## Tests actuales — 141 tests, 0 fallos

```
ERP.Domain.Tests         →  23 tests   (entidades, Value Objects, RUC ecuatoriano)
ERP.Application.Tests    →  40 tests   (handlers, behaviors, DTOs)
ERP.Infrastructure.Tests →   3 tests   (repositorios, parser XML SRI)
ERP.API.Tests            →  75 tests   (integración E2E, HTTP, dominio, algoritmos)
──────────────────────────────────────────
TOTAL                    → 141 tests   ✅ 0 fallos
```

Desglose `ERP.API.Tests` (75):
```
Unit/VentasDomainTests.cs                     → 12  (máquina de estados, totales)
Unit/ClaveAccesoHelperTests.cs                →  7  (algoritmo módulo-11, Theory)
Unit/StockValidationHandlerTests.cs           →  5  (stock Ventas: suficiente/insuficiente)
Unit/TransferenciasStockTests.cs              →  5  (stock Transferencias: 5 escenarios)
Integration/VentasEndToEndTests.cs            →  4  (flujo completo E2E)
Integration/VentasHttpTests.cs                → 10  (HTTP + JWT simulado)
Integration/TransferenciasEndToEndTests.cs    →  5  (crear→confirmar stock; cancelar; estados inválidos)
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
| **Ventas** | **`/api/ventas`** | **8** |
| **Config SRI** | **`/api/configuracion-sri`** | **2** |
| SuperAdmin | `/api/superadmin`, `/api/setup` | 10+ |

---

## Migraciones — 47 aplicadas, 2 pendientes

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

> Ambas migraciones se aplican con un solo comando: `dotnet ef database update`

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

### Prioridad 2 — Frontend (Ventas + Transferencias + Compras/Gastos)

Todo el backend está listo. Solo falta la capa visual. Seguir la estructura de `frontend/src/modules/`.

#### Ventas
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

APIs Ventas (ya operativas):
- `GET  /api/ventas?pageNumber=1&pageSize=20&estado=Borrador&clienteId=...`
- `POST /api/ventas` · `PATCH /api/ventas/{id}/validar` · `/emitir` · `/reintentar` · `/anular`
- `GET  /api/ventas/{id}` · `GET /api/ventas/stock?productoId=...&bodegaId=...`
- `GET/PUT /api/configuracion-sri`

#### Transferencias entre Bodegas
```
frontend/src/modules/inventario/transferencias/
├── pages/
│   ├── TransferenciasListPage.tsx  ← tabla paginada con filtros de bodega y estado
│   ├── TransferenciaDetailPage.tsx ← detalle con ítems + botones Confirmar/Cancelar
│   └── CrearTransferenciaPage.tsx  ← formulario: bodega origen, destino, ítems
├── components/
│   ├── TransferenciaEstadoBadge.tsx
│   └── ItemsTransferenciaTable.tsx
└── hooks/useTransferencias.ts      ← llamadas a /api/inventario/transferencias
```

APIs Transferencias (ya operativas):
- `GET  /api/inventario/transferencias?bodegaOrigenId=...&estado=Borrador`
- `POST /api/inventario/transferencias`
- `GET  /api/inventario/transferencias/{id}`
- `PATCH /api/inventario/transferencias/{id}/confirmar`
- `PATCH /api/inventario/transferencias/{id}/cancelar`

#### Compras / Gastos
- `frontend/src/modules/billing/compras/` → listar, crear desde XML/manual, aprobar
- `frontend/src/modules/billing/gastos/` → listar, crear, aprobar

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

2. **Hay 2 migraciones PENDIENTES** — aplicar ambas antes de usar la API en un entorno real:
   - `Paso3_VentasFacturaAsientoAndPermissions` — añade `asiento_contable_id` a `ventas_facturas` y siembra permisos del módulo Ventas
   - `AddTransferenciasInventario` — crea las tablas `transferencias` + `transferencia_detalles` y siembra permisos del módulo Transferencias
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

*Próxima actualización de este documento: cuando se complete el frontend de Ventas/Transferencias o la implementación SRI real.*

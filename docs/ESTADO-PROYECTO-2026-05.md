# Estado del Proyecto ERP SaaS — Mayo 2026

> **Documento de referencia rápida.** Leer este archivo al inicio de cada sesión para saber
> exactamente en qué punto está el proyecto, qué está hecho y qué sigue.
>
> Última actualización: **09 de mayo de 2026** | Commit: `d20a14d`

---

## ¿Dónde estamos? (leer en 30 segundos)

El ERP SaaS tiene **backend completo** para los módulos de **Compras, Gastos e Inventario**,
y acaba de terminar el módulo de **Ventas con facturación electrónica SRI Ecuador** (simulada).
Hay **131 tests automáticos pasando** y la API corre en producción-local.

**Lo que falta para el MVP comercial:**
1. Implementar el WSDL real del SRI (firma P12 + envío + polling)
2. Frontend del módulo Ventas (pantallas)
3. Frontend de módulos Compras/Gastos (pantallas listado/detalle)
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

# 5. Correr todos los tests
cd ../backend/src
dotnet test                   # 131 tests, todos deben pasar
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

### Ventas — completado el 09/05/2026 (commit `d20a14d`)
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

## Tests actuales — 131 tests, 0 fallos

```
ERP.Domain.Tests        →  23 tests   (entidades, Value Objects, RUC ecuatoriano)
ERP.Application.Tests   →  40 tests   (handlers, behaviors, DTOs)
ERP.Infrastructure.Tests →   3 tests  (repositorios, parser XML SRI)
ERP.API.Tests           →  65 tests   (integración E2E, HTTP, dominio, algoritmos)
─────────────────────────────────────────
TOTAL                   → 131 tests   ✅ 0 fallos
```

Desglose `ERP.API.Tests`:
```
Unit/VentasDomainTests.cs           → 12  (máquina de estados, totales)
Unit/ClaveAccesoHelperTests.cs      →  7  (algoritmo módulo-11, Theory)
Unit/StockValidationHandlerTests.cs →  5  (stock suficiente/insuficiente)
Integration/VentasEndToEndTests.cs  →  4  (flujo completo E2E)
Integration/VentasHttpTests.cs      → 10  (HTTP + JWT simulado)
Controller/VentasControllerContractTests.cs → 9 (StubMediator, status codes)
Integration/CompraGastoEndToEndTests.cs →  2 (E2E compras + gastos)
Integration/AuthenticatedApiTests.cs →   2 (HTTP con token)
Otros (middleware, contratos)       → 14
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
| Inventario | `/api/inventario` | 2 |
| **Ventas** | **`/api/ventas`** | **8** |
| **Config SRI** | **`/api/configuracion-sri`** | **2** |
| SuperAdmin | `/api/superadmin`, `/api/setup` | 10+ |

---

## Migraciones — 47 aplicadas, 1 pendiente

```bash
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
| **🔄 PENDING** | **`Paso3_VentasFacturaAsientoAndPermissions`** | **asiento_contable_id + permisos ventas.*** |

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

### Prioridad 2 — Frontend Ventas

**Pantallas a crear** (seguir la estructura de `frontend/src/modules/billing/`):

```
frontend/src/modules/billing/
├── ventas/
│   ├── pages/
│   │   ├── VentasListPage.tsx          ← tabla paginada con filtros
│   │   ├── VentaDetailPage.tsx         ← detalle con detalles + estado + acciones
│   │   └── CrearVentaPage.tsx          ← formulario de creación
│   ├── components/
│   │   ├── VentaEstadoBadge.tsx        ← chip de color por estado
│   │   ├── VentaItemsTable.tsx         ← tabla de líneas de la factura
│   │   └── EmitirFacturaButton.tsx     ← botón con confirmación
│   ├── hooks/
│   │   └── useVentas.ts               ← llamadas a /api/ventas
│   └── schemas/
│       └── crearVentaSchema.ts         ← Zod validación
```

**APIs disponibles** (ya funcionan, solo conectar):
- `GET  /api/ventas?pageNumber=1&pageSize=20&estado=Borrador&clienteId=...`
- `POST /api/ventas` (crear factura)
- `PATCH /api/ventas/{id}/validar`
- `PATCH /api/ventas/{id}/emitir`
- `PATCH /api/ventas/{id}/reintentar`
- `PATCH /api/ventas/{id}/anular`
- `GET  /api/ventas/{id}` (detalle con líneas)
- `GET  /api/ventas/stock?productoId=...&bodegaId=...`
- `GET  /api/configuracion-sri`
- `PUT  /api/configuracion-sri`

**Permisos requeridos** (para el token de usuario):
- `ventas.facturas.view` — listar/ver
- `ventas.facturas.create` — crear
- `ventas.facturas.validate` — validar
- `ventas.facturas.emit` — emitir/reintentar
- `ventas.facturas.cancel` — anular
- `ventas.stock.view` — consultar stock
- `ventas.configuracion.view` / `ventas.configuracion.edit` — config SRI

---

### Prioridad 3 — Notas de Crédito

Anulación de facturas ya **Autorizadas** por el SRI. Requiere:

1. **Dominio:** nueva entidad `NotaCredito` (referencia a `VentasFactura` autorizada)
2. **SRI:** tipo de documento `04` (nota de crédito electrónica)
3. **Inventario:** `InventarioMovimiento.DevolucionVenta` → incrementa stock
4. **Contabilidad:** asiento inverso al de la venta original
5. **Flujo:** Borrador → Validado → Autorizado (igual que factura)

---

### Prioridad 4 — Frontend Compras / Gastos

Las pantallas de backend están completas. Falta conectar el frontend:
- `frontend/src/modules/billing/compras/` → listar, crear desde XML/manual, aprobar
- `frontend/src/modules/billing/gastos/` → listar, crear, aprobar

---

### Prioridad 5 — Mejoras técnicas menores

| Tarea | Archivo | Dificultad |
|-------|---------|-----------|
| Paginación en Compras y Gastos (actualmente sin paginado) | `ICompraRepository`, `IGastoRepository` | Baja |
| `GetVentaByIdQuery` retorna `Success(null)` para no-encontrado → cambiarlo a `Failure` + 404 | `GetVentaByIdQueryHandler.cs` | Muy baja |
| Agregar `GetFacturaByIdWithDetailsAsync` en `IVentasRepository` (actualmente solo `GetFacturaByIdAsync`) | `IVentasRepository.cs` | Baja |
| Seed de `ConfiguracionSRI` de prueba en entorno dev | nueva migración o script | Baja |
| Reporte PDF de factura autorizada (QR con clave de acceso) | servicio nuevo | Media |
| Retención en la fuente (módulo RETENCIONES SRI) | módulo nuevo | Alta |

---

## Permisos del módulo Ventas

Sembrados automáticamente por la migración `Paso3_VentasFacturaAsientoAndPermissions`:

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
| Configuración SRI (algoritmo clave acceso) | `ERP.Application/Modules/Ventas/Helpers/ClaveAccesoHelper.cs` |
| Servicio SRI simulado | `ERP.Infrastructure/Services/SriFacturaElectronicaSimuladoService.cs` |
| Servicio SRI real (esqueleto) | `ERP.Infrastructure/Services/SriFacturaElectronicaRealService.cs` |
| Permisos (reglas de seed) | `ERP.Infrastructure/Migrations/20260509235417_Paso3_VentasFacturaAsientoAndPermissions.cs` |
| Módulo Ventas — dominio | `ERP.Domain/Ventas/` |
| Módulo Ventas — aplicación | `ERP.Application/Modules/Ventas/` |

---

## Comandos frecuentes

```bash
# Compilar todo
dotnet build backend/src/ERP.slnx

# Correr todos los tests
dotnet test backend/src/ERP.slnx

# Solo tests de la API (incluyen E2E)
dotnet test backend/src/ERP.API.Tests

# Agregar migración
dotnet ef migrations add NombreMigracion \
  --project backend/src/ERP.Infrastructure \
  --startup-project backend/src/ERP.API

# Aplicar migraciones
dotnet ef database update \
  --project backend/src/ERP.Infrastructure \
  --startup-project backend/src/ERP.API

# Ver estado de migraciones
dotnet ef migrations list \
  --project backend/src/ERP.Infrastructure \
  --startup-project backend/src/ERP.API

# Linting frontend
cd frontend && npm run lint

# Build frontend
cd frontend && npm run build
```

---

## Notas para el próximo desarrollador / sesión

1. **Leer este archivo primero.** Si hay diferencias con el código real, confiar en el código.

2. **La migración `Paso3_VentasFacturaAsientoAndPermissions` está PENDIENTE.**
   Aplicarla antes de usar el módulo Ventas en un entorno real.

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

*Próxima actualización de este documento: cuando se complete la implementación SRI real o el frontend de Ventas.*

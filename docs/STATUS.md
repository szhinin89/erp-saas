# ESTADO DE DESARROLLO — ERP SaaS ZH Technologies

Arquitectura → `ARCHITECTURE.md` | Reglas → `CLAUDE.md` | Funcionalidades → `FEATURES.md`

Última actualización: **2026-05-16**

---

## Cómo retomar el trabajo

```powershell
docker compose up -d
cd backend/src && dotnet ef database update --project ERP.Infrastructure --startup-project ERP.API
cd ERP.API && dotnet run                    # http://localhost:5003  swagger: /swagger
cd ../../../../frontend && npm run dev      # http://localhost:5173

# Tests (desde backend/)
dotnet test src/ERP.API.Tests/ERP.API.Tests.csproj            # 153 tests
dotnet test src/ERP.Application.Tests/ERP.Application.Tests.csproj  #  87 tests
dotnet test src/ERP.Domain.Tests/ERP.Domain.Tests.csproj      #  23 tests
dotnet test src/ERP.Infrastructure.Tests/ERP.Infrastructure.Tests.csproj  # 3 tests
# Total: 266 tests
```

> **Credenciales dev:** `backend/src/ERP.API/appsettings.Development.json` (copiado de `.json.example`)  
> PostgreSQL: `Host=localhost;Port=5435;Database=dberpsaas;Username=postgres;Password=zhin@2024`

---

## Resumen de estado

| Área | Estado |
|------|--------|
| Backend módulos core | ✅ Completo |
| Tests automatizados | ✅ 266 tests |
| Frontend catálogos e inventario | ✅ Completo |
| Frontend compras (OC, proveedores) | ✅ Completo |
| Frontend ventas (facturas) | 🚧 Parcial |
| SRI real (firma P12 + envío) | ⏳ Pendiente MVP |
| Frontend compras/gastos facturas | ⏳ Pendiente |

---

## Backend — módulos completados ✅

### Plataforma SaaS / SuperAdmin
- Gestión de tenants, planes, features (`TenantsController`, `SaasPlansAdminController`)
- Menú dinámico por tenant en BD (`ERP.Application/Modules/Navigation/`)
- Menu Builder: árbol, operaciones, historial, validación integridad plan-menú
- Integración planes SaaS con sidebar layout
- Perfiles de acceso + permisos granulares (`ERP.Application/Modules/Access/`)
- Auditoría de usuario (`ERP.Domain/Modules/Audit/`)
- Config jerárquica global → módulo → feature

### Catálogos base
- Productos (CRUD, variantes, códigos de barra): `ERP.Application/Modules/Products/`
- Categorías, Marcas, Unidades, Aranceles: `ERP.Application/Modules/Products/Catalogs/`
- Tarifas SRI (solo lectura desde `sri_vat_rate`)
- Clientes: `ERP.Application/Modules/Customers/`
- Proveedores (CRUD + validación RUC ecuatoriano): `ERP.Application/Modules/Proveedores/`
- Sucursales + Geografía INEC Ecuador: `ERP.Application/Modules/Branches/`

### Contabilidad
- Plan de cuentas + Asientos + Config contable por empresa
- `AccountsController`, `ConfiguracionContableController`, `CuentaContableService`

### Logística e Inventario
- Bodegas (CRUD): `ERP.Application/Modules/Bodegas/`
- Inventario (StockActual + InventarioMovimiento): `ERP.Application/Modules/Inventario/`
- Transferencias entre Bodegas: flujo completo
- Ajustes de Inventario: flujo completo
- Parser XML SRI (comprobantes recibidos): `ERP.Infrastructure/Services/SriFacturaParser.cs`

### Compras (completado 2026-05-10)
- Facturas de compra: Borrador → Validado → Aprobado/Rechazado
- Órdenes de Compra: flujo completo con vinculación a facturas electrónicas
  - 11 tests Moq + 12 tests validador + 4 tests pipeline + 1 E2E flujo completo + 5 E2E básicos
- Gastos: Borrador → Validado → Aprobado/Rechazado
- Retención en la fuente emitida

### Ventas
- Facturas con facturación electrónica SRI (simulada)
- Notas de crédito/débito asociadas a facturas autorizadas
- Retenciones recibidas (registro desde XML)
- Caja / bancos (caja chica, extractos, conciliación)

---

## Frontend — pantallas completadas ✅

| Módulo | Pantallas |
|--------|-----------|
| Auth | Login, PasswordReset, TenantSelect, ForgotPassword |
| Dashboard | DashboardPage con KPIs, actividad, accesos rápidos |
| Productos | ProductPage (listado + formulario completo) |
| Clientes | CustomersPage (listado, contactos, categorización, auditoría) |
| Proveedores | ProveedoresPage (listado, formulario, notas, pagos) |
| Ventas facturas | VentasFacturasPage (listado con badges de estado) |
| Inventario | AjustesListPage, CrearAjustePage, AjusteDetailPage |
| Inventario | TransferenciasListPage, CrearTransferenciaPage, TransferenciaDetailPage |
| Inventario | BodegasPage |
| Compras OC | OrdenesCompraListPage, CrearOrdenCompraPage, OrdenCompraDetailPage |
| Contabilidad | AccountingPage |
| Catálogos | Brands, ProductTypes, Units, Tariffs, CatalogStructure |
| Reportes | SalesReportPage (plantilla reutilizable ReportPageTemplate) |
| Configuración | TenantAccessPage, ProfilesPage, BranchesPage |
| SuperAdmin | PanelPage, PlansPage, OverviewPage, NavMenuPage |

---

## Flujos de estado de documentos

### Orden de Compra
```
Borrador
├──[enviar]──► Enviada ──[aprobar]──► Aprobada
└──[aprobar]────────────────────────► Aprobada
                                          ├──[factura parcial]──► RecibidaParcial ──[completo]──► Cerrada
                                          └──[factura total]───────────────────────────────────► Cerrada
Cualquier estado activo ──[cancelar]──► Cancelada
```
Reglas clave:
- Número: `OC-{secuencial:D4}` único por tenant
- Solo OC `Aprobada` o `RecibidaParcial` puede recibir facturas
- Factura vinculada DEBE estar en estado `Aprobado`
- Diferencia de precio >1% → `Advertencias[]` (no bloquea)
- Stock: la OC NO mueve inventario; se actualiza al aprobar `CompraFactura`

### Ajuste de Inventario
```
Borrador ──[ejecutar]──► Ejecutado  (stock +/− CantidadAjuste)
         ──[cancelar]──► Cancelado  (sin efecto en stock)
```

### Transferencia entre Bodegas
```
Borrador ──[enviar]──► Enviada ──[recibir]──► Recibida
Cualquier estado activo ──[cancelar]──► Cancelada
```

### Factura de Venta (electrónica SRI simulada)
```
Borrador ──[autorizar]──► Autorizado
         ──[anular]──────► Anulado
```

### Compra / Gasto (factura recibida)
```
Borrador ──[validar]──► Validado ──[aprobar]──► Aprobado  (stock++ + asiento contable)
                                  └[rechazar]──► Rechazado
```

---

## Pendiente para MVP comercial

1. **SRI real:** implementar WSDL real (firma P12 + envío + polling) en `SriFacturaElectronicaRealService` y `SriComprobanteRetencionService`
2. **Frontend Ventas:** pantallas completas de facturación (notas, retenciones)
3. **Frontend Compras/Gastos:** pantallas de facturas recibidas y retenciones emitidas

---

## Extras diferidos (documentados, no bloqueantes)

- OC: recepción física sin factura (`RecibirOrdenCompraParcial`)
- OC: tolerancia de precio configurable (actualmente hardcodeada al 1%)
- OC: banner de advertencias en frontend al vincular factura con precio discrepante
- SuperAdmin: log de impersonación con motivo obligatorio
- SuperAdmin: JWT de duración distinta para sesión impersonada

---

## Tests

| Proyecto | Tipo | Tests |
|----------|------|-------|
| `ERP.API.Tests` | Integración HTTP (WebApplicationFactory) | 153 |
| `ERP.Application.Tests` | Unitario + integración MediatR | 87 |
| `ERP.Domain.Tests` | Unitario (entidades, VOs) | 23 |
| `ERP.Infrastructure.Tests` | Integración ligera | 3 |
| **Total** | | **266** |

Frontend E2E (Playwright):
```powershell
cd frontend
npx playwright install chromium   # solo primera vez
npm run build && npm run test:e2e # vite preview en puerto 4173
```

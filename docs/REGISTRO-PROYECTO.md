# Registro del Proyecto — ERP SaaS ZH Technologies

> **Uso:** entradas cronológicas de trabajo realizado, decisiones, deuda técnica y pendientes.
> Leer junto con `ESTADO-PROYECTO-2026-05.md` para contexto completo.
>
> **Notas de continuación de sesión** (antes en `docs/CONTINUAR-sesion.md`): usar este mismo archivo — añadir una entrada con fecha cuando se cierre un hilo.

---

## Referencia rápida — SuperAdmin y cuotas de instancia

- **SuperAdmin único por servidor:** `POST /api/setup/superadmin` (token `Deployment:InitialSuperAdminSetupToken`), login `superadmin-login`, panel `/superadmin`.
- **Cuotas:** `DedicatedSingleClientInstance`, `MaxActiveTenants`, `MaxIdentityUsers`, `MaxUsersPerTenant`; opcional `App_Data/instance-quota.json` (ignorado en git); API `GET/PUT /api/superadmin/instance-quota`.
- **Código:** `SetupController`, `SuperAdminController` (instance-quota), `DeploymentFeatureFlags`, `SuperAdminInstanceQuotaPage.tsx`.
- **Scripts:** `scripts/create-superadmin.ps1`, `scripts/create-superadmin-interactive.ps1`.
- **Retomar en local:** PostgreSQL + migraciones → `backend/src/ERP.API` → `dotnet run` (puerto típico 5003) → `frontend` → `npm run dev`. Tras cambiar user-secrets o `Program.cs`, reiniciar la API.

---

## Entradas de trabajo

### 09 de mayo de 2026 — Módulo Ventas completo (commit `d20a14d`)

**Alcance:** Implementación completa del módulo de Ventas con Facturación Electrónica SRI Ecuador.

**Completado en esta sesión:**
- Dominio: `VentasFactura`, `VentasDetalle`, `ConfiguracionSRI` (entidades + máquina de estados)
- 10 casos de uso CQRS: Crear, Validar, Emitir, Reintentar, Anular, GetList, GetById, GetStock, GetConfigSRI, UpsertConfigSRI
- 8 validators FluentValidation (uno por command/query con input de usuario)
- `ClaveAccesoHelper` — algoritmo módulo-11 SRI extraído y testeable
- `SriFacturaElectronicaSimuladoService` — XML completo SRI v1.1.0, firma simulada, autorización simulada
- `SriFacturaElectronicaRealService` — esqueleto con logging y `SriCommunicationException`
- `SriCommunicationException` — excepción semántica para errores SRI → 502 Bad Gateway
- `CrearAsientoVentaAsync` en `AccountingService` (DR Activo / CR Revenue)
- `VentasController` — 8 endpoints con permisos `perm:ventas.*`
- `ConfiguracionSRIController` — GET + PUT upsert
- Migración `AddFacturacionElectronicaVentas` — tablas ventas_facturas, ventas_detalles, configuracion_sri
- Migración `Paso3_VentasFacturaAsientoAndPermissions` — columna asiento_contable_id + seed permisos ventas.*
- **65 tests en ERP.API.Tests** (dominio puro, algoritmos, stock, E2E, HTTP, contrato)
- Total suite: **131 tests, 0 fallos**
- `ILogger<T>` agregado a todos los handlers del módulo Ventas

**Decisiones tomadas:**
- `ReintentarEnvioCommand` delega vía MediatR a `EmitirFacturaElectronicaCommand` (sin duplicar lógica)
- Stock se descuenta al **emitir** (no al crear), dentro de la misma transacción que el asiento
- `GetVentaByIdQuery` retorna `Success(null)` para no-encontrado (consistente con patrón Compras)
- Servicio SRI se registra condicionalmente: simulado en Development, real en Production (`Program.cs`)

**Pendiente de esta sesión (deuda técnica menor):**
- `GetVentaByIdQuery`: considerar cambiar `Success(null)` → `Failure` + 404 en futura sesión
- Migración `Paso3` está **PENDIENTE** de aplicar (`dotnet ef database update`)

---

### 09 de mayo de 2026 — Módulo Compras + Parser SRI + Bodegas + Proveedores (commit `a49c318`)

**Alcance:** Logística completa — infraestructura de compras desde XML SRI.

**Completado:**
- `SriFacturaParser` — parseo XML SRI Ecuador (facturas de compra recibidas)
- Bodegas: CRUD + disable/enable por tenant
- Proveedores: CRUD + validación RUC ecuatoriano (algoritmo módulo-11)
- Compras: flujo Borrador → Validado → Aprobado/Rechazado
- Asignación por bodega al aprobar (distribución de cantidades por producto)
- Stock actualizado al aprobar compra (`StockActual` + `InventarioMovimiento.EntradaCompra`)
- Asiento contable al aprobar (DR Gasto / CR Cuentas por Pagar)
- Auto-creación de proveedor desde RUC en XML si no existe
- Gastos: flujo equivalente a Compras (sin asignación de bodega)
- Tests E2E: compra XML con asignación bodega, gasto manual

**Migraciones aplicadas:**
- `AddLogisticaProveedoresComprasGastos`
- `SeedBodegasProveedor`
- `AddComprasFullSchema`
- `CompraBodegaAsignacionProductoNullable`
- `GastoFacturaModuloElectronico`
- `SeedLogisticaAccessProfilePermissions`

---

### Antes del 09 de mayo de 2026 — Base del proyecto

**Fase 0 — Infraestructura core (múltiples commits):**
- SuperAdmin único, cuotas de instancia, bootstrap token
- Auth JWT (sesión + reset contraseña)
- IAM: perfiles de acceso, permisos granulares `perm:*`, membresías
- SaaS: planes, features, asignación plan ↔ tenant
- Menú dinámico por tenant (tablas `ui_nav_*`)
- Configuración jerárquica (global / módulo / feature)
- Productos: CRUD completo con 10+ entidades de catálogo (marcas, UoM, etc.)
- Tarifas de impuestos (IVA, ICE)
- Plan de cuentas + Asientos contables (partida doble)
- Clientes maestro por tenant
- Sucursales + Geografía INEC Ecuador (seed DPA completo)
- Frontend: autenticación, productos, clientes, sucursales, catálogos, SuperAdmin

---

## Pendientes y backlog

| Prioridad | Tarea | Módulo | Dificultad | Notas |
|-----------|-------|--------|-----------|-------|
| 🔴 Alta | Implementar WSDL real SRI | Ventas | Alta | Ver `SriFacturaElectronicaRealService.cs` |
| 🔴 Alta | Frontend módulo Ventas | Frontend | Media | APIs ya completas, solo conectar |
| 🟡 Media | Notas de crédito SRI | Ventas | Alta | Requiere dominio + contabilidad inversa |
| 🟡 Media | Frontend Compras (pantallas) | Frontend | Media | Backend 100% completo |
| 🟡 Media | Frontend Gastos (pantallas) | Frontend | Media | Backend 100% completo |
| 🟡 Media | Paginación en Compras y Gastos | Backend | Baja | Hoy devuelve lista completa |
| 🟢 Baja | `GetVentaByIdQuery` → 404 real | Backend | Muy baja | Actualmente `Success(null)` |
| 🟢 Baja | Reporte PDF factura autorizada | Backend | Media | QR con clave de acceso |
| 🟢 Baja | Módulo Retenciones SRI | Backend + Frontend | Alta | Módulo nuevo independiente |
| 🟢 Baja | Frontend configuración SRI | Frontend | Baja | APIs ya completas |

---

## Decisiones arquitectónicas vigentes

| Decisión | Motivo |
|----------|--------|
| Result pattern en todos los handlers | Nunca lanzar excepciones al controlador; errores de negocio controlados |
| Multi-tenancy con Global Query Filter | Aislamiento garantizado a nivel ORM; no depende de WHERE manual |
| CQRS con MediatR | Separación clara entre lecturas y escrituras; pipeline behaviors |
| Permisos dinámicos `perm:*` | Sin registro en startup; nuevos módulos no requieren cambiar `Program.cs` |
| Simulado/Real por entorno | SRI simulado en dev, real en prod; switch en `Program.cs` |
| Stock se descuenta al emitir | El stock no se reserva al crear; se valida al crear y se descuenta al autorizar |
| `SriCommunicationException` → 502 | Errores SRI tipados y mapeados a HTTP semánticamente correcto |
| Seed de permisos por migración SQL | Sin hardcoding en código; rollback limpio con UUID centinela |

---

## Deuda técnica conocida

- `GetVentaByIdQuery` retorna `Success(null)` para no-encontrado (consistente con Compras pero semánticamente podría ser `Failure` para 404)
- `VentasRepository.GetFacturasAsync` (sin paginar) existe en la interfaz pero no se usa; el endpoint usa `GetFacturasPagedAsync`
- El generador XML simulado usa IDs de producto como código principal (debería usar el código de venta)
- `ClaveAccesoHelper.GenerarCodigoNumerico()` usa `Random.Shared` — suficiente para dev; en producción evaluar generador criptográfico

---

## Ideas / investigación

- **Módulo Retenciones:** el SRI Ecuador requiere retenciones (formulario 103 para IVA, 104 para renta). Módulo independiente con su propio XML SRI tipo `07`.
- **Factura electrónica de exportación:** tipo `01` con información adicional de exportación.
- **Facturación por lotes:** emitir múltiples facturas en una operación para cierre de mes.
- **WebSockets para estado de autorización SRI:** cuando el polling de autorización SRI tarda (hasta 5 minutos), notificar al frontend en tiempo real.

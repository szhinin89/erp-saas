# First Run — instalación inicial del ERP

**SSOT** de la instalación inicial: flujo, contrato, seguridad, operación local y pruebas que la garantizan. Otros documentos solo enlazan aquí.

---

## Flujo

```
launcher (opción 1/2) → migraciones + API + frontend
  → GET /api/v1/setup/status
     ├─ isInitialized=false → /setup → alta inicial manual del propietario → /login
     └─ isInitialized=true  → ERP operativo (raíz del frontend)
```

1. Al arrancar, la API aplica migraciones y el bootstrap global; después ejecuta `IFirstRunSetupService.EnsureSetupTokenAsync` (`Program.cs`).
2. Si el sistema no está inicializado, la API emite un token de instalación y lo imprime **solo** en su consola.
3. El propietario abre `/setup`, completa el formulario con sus datos y el token de la consola. El frontend llama a `POST /api/v1/setup/admin`.
4. El alta crea en una sola transacción el tenant `Principal`, su empresa por defecto con bootstrap contable, el usuario administrador, su `CompanyUserMembership` (Admin), su `GlobalUserRole` (Admin) y la autorización a la sucursal principal (`CompanyUserBranch`). Marca el sistema como inicializado e invalida el token.
5. La página redirige a `/login`. Desde ese momento la API arranca sin token ni banner.

No existe otra ruta de alta del primer administrador: sin registro anónimo, sin credenciales predefinidas y sin tokens configurables por appsettings/variables de entorno.

## Componentes

| Capa | Componente | Responsabilidad |
|------|------------|-----------------|
| Domain | `Setup/SystemSetupState` | Fila única (`Id=1`): `IsInitialized`, `InitializedAtUtc`, `AdminEmail`, `SetupTokenHash`, `SetupTokenExpiryUtc`, `IsFirstRun` |
| Application | `Setup/SetupTokenCrypto` | Token aleatorio de 32 bytes (hex), hash SHA-256, comparación en tiempo constante |
| Application | `Setup/GetSetupStatus` | `isInitialized` y `adminEmail` (solo si está inicializado) |
| Application | `Setup/CreateInitialAdmin` | Command, validator FluentValidation y handler del alta |
| Application | `IFirstRunSetupService`, `IFirstRunSession` | Contratos de emisión y de ejecución en la sesión propietaria |
| Infrastructure | `Setup/FirstRunSetupService` | Emisión del token y banner de consola |
| Infrastructure | `Setup/FirstRunOwnershipCoordinator` | Conexión dedicada, advisory lock, monitor (hosted service) |
| Infrastructure | `Setup/FirstRunSession` | Vincula el `ErpDbContext` scoped a la conexión propietaria |
| Infrastructure | `Setup/FirstRunConnectionGuard`, `FirstRunCommandGuard` | Interceptores EF: impiden reabrir o usar otra conexión |
| API | `Controllers/SetupController` | `GET status`, `POST admin` (anónimos, token-gated) |
| API | `Hosting/LauncherResetGuard` | Guard del reset local en la factory EF de diseño |
| Frontend | `modules/auth/pages/SetupPage.tsx`, `api/setupService.ts`, ruta pública `/setup` | Formulario de alta (Zod + RHF) |
| Script | `scripts/dev-launcher.ps1` | Arranque local, consulta de estado, navegación a `/setup` o raíz |

Nota técnica: `SystemSetupState.IsFirstRun` permanece en el modelo y se escribe, pero no tiene lectores operativos identificados. Su eliminación requeriría una modificación explícita de la baseline V1, que no está autorizada.

## Token de instalación

- Mientras `IsInitialized=false`, **cada arranque propietario** de la API emite un token nuevo y reemplaza el anterior, aunque siga vigente. El valor original no es recuperable desde su hash.
- Solo se persisten el hash SHA-256 y la expiración: `Setup:TokenExpiryMinutes`, 15 minutos por defecto.
- El valor se imprime en la consola del proceso **después** de guardar correctamente. No se entrega por endpoints ni por Serilog: el log solo indica que se generó y cuándo expira.
- Es de un solo uso: el alta confirmada anula hash y expiración.
- Token expirado o perdido: reiniciar la API para emitir uno nuevo.

## Propietario exclusivo por base

- Una sola instancia de API es propietaria de la instalación pendiente, coordinada con `pg_try_advisory_lock(1514686021, 1)` de sesión.
- La conexión propietaria es física y dedicada (`Pooling=false`, `Multiplexing=false`, `Enlist=false`). Adquiere el lock antes de emitir y relee el estado después de adquirirlo. Si la fila de estado no existe, se crea solo bajo propiedad.
- Otra instancia con instalación pendiente **rechaza el arranque**, sin emitir ni reemplazar tokens. El worker no adquiere propiedad.
- Emisión y alta usan el mismo `ErpDbContext` scoped vinculado a la sesión propietaria. Los guards EF prohíben reabrir conexiones de ese contexto o ejecutar sus comandos sobre otra conexión. Las solicitudes del mismo proceso se serializan.
- La sesión se conserva durante rechazos y rollbacks. Se libera después de confirmar la inicialización o al cerrar la API (incluidos errores de arranque).
- Un monitor verifica PID y lock cada 2 segundos, con comandos acotados a 5 segundos. Ante pérdida cancela First Run y detiene la API; no reconecta ni readquiere. También reconcilia un commit confirmado cuya respuesta haya fallado.
- Con la instalación completada no hay lock duradero ni conexión dedicada.

## Alta inicial

- `CreateInitialAdminHandler` se ejecuta en la sesión propietaria y dentro de `IUnitOfWork.ExecuteInTransactionAsync`. Relee el estado y valida el token **dentro** de la transacción; el provisioning y sus flushes intermedios comparten esa transacción.
- Cualquier fallo revierte todo: no quedan altas parciales ni se libera la propiedad.
- Nunca acepta `TenantId`, `CompanyId` ni rol del cliente: tenant `Principal`, rol `Admin`.
- Username normalizado a minúsculas (3–50 caracteres `[a-zA-Z0-9._-]`); email opcional; contraseña con la complejidad estándar (`ApplyPasswordComplexity`).

## Contrato HTTP

| Método | Ruta | Auth | Cuerpo | Respuesta |
|--------|------|------|--------|-----------|
| GET | `/api/v1/setup/status` | Anónimo | — | `SetupStatusDto` (`isInitialized`, `adminEmail`) |
| POST | `/api/v1/setup/admin` | Anónimo + token | `CreateInitialAdminCommand` (`username`, `firstName`, `lastName`, `email?`, `password`, `setupToken`) | `string` |

Resultados de `POST admin`, todos con el envelope oficial de [error-handling.md](./error-handling.md):

| Caso | HTTP |
|------|------|
| Alta correcta | 200 |
| Validación (incluido `setupToken` ausente, vía adaptador MVC) | 422 `VALIDATION_ERROR` |
| Token inválido | 401 |
| Sin token activo o token expirado | 400 |
| Sistema ya inicializado, o alta concurrente perdedora | 409 |

## Frontend `/setup`

- Ruta pública. Consulta el estado: si ya está inicializado, muestra el aviso y el acceso a `/login`; si no, muestra el formulario.
- Validación Zod + RHF alineada con el validator backend; trim/minúsculas antes de enviar; errores vía `formatApiRequestError`.
- No obtiene, guarda ni sugiere el token: el propietario lo copia de la consola API.

## Launcher local (`scripts/dev-launcher.ps1`)

Requiere PowerShell 7, `dotnet`, `npm` y PostgreSQL local en marcha (`docker compose up -d`, puerto 5435, base `erp_zh_dev`).

| Opción | Efecto |
|--------|--------|
| `[1] Iniciar ERP` | Detiene servicios propios, limpia, restaura, `dotnet ef database update`, compila, arranca API y frontend; abre `/setup` si está pendiente o la raíz si está inicializado, más Swagger |
| `[2] Reset completo` | **Destructivo** — ver [Reset local](#reset-local); después, mismo arranque que `[1]` |
| `[3] Solo Backend API` | Compila y arranca la API; abre Swagger |
| `[4] Tests completos` | Suites backend/frontend |

Reglas del launcher:

- **Solo consulta** `GET /api/v1/setup/status`. No lee la consola ni los logs de la API, no captura ni envía tokens, no pide credenciales y no crea administradores.
- **API:** confirma el arranque solo si el proceso lanzado sigue vivo y los listeners del puerto 5003 pertenecen al apphost `ERP.API.exe` del proyecto, dentro de su árbol de procesos (handle y fecha de creación retenidos), **antes y después** de la consulta HTTP. Una respuesta de otro proceso no acredita el arranque.
- **Frontend:** `npm.cmd run dev -- --strictPort` (no se desplaza de 5173). Está listo cuando el padre retenido sigue vivo, los listeners pertenecen a su `node.exe` y `/@vite/client` responde 200 con contenido Vite; la identidad se revalida después de HTTP.
- **Parada:** solo de procesos propios de esta sesión del launcher (listener `Listen`, ruta y fecha del ejecutable, ascendencia hasta el padre retenido), con `Process.Kill` sobre el handle retenido, nunca por PID. Un puerto ocupado por un proceso ajeno o de otra sesión se rechaza sin detenerlo.
- Cualquier fallo de disponibilidad, inspección o propiedad aborta sin anunciar éxito ni abrir navegador.

## Reset local

- Solo mediante la opción `[2]`: confirmación `RESETEAR` en el launcher y después `dotnet ef database drop --force ... -- --launcher-reset=true`.
- La factory EF de diseño ejecuta `LauncherResetGuard` antes de construir el contexto: muestra servidor, puerto, base y entorno efectivos (nunca la cadena de conexión) y bloquea todo destino distinto de `Development` + loopback (`localhost`/`127.0.0.1`/`::1`) + puerto `5435` + base `erp_zh_dev`.
- Exige escribir exactamente `ELIMINAR <host>:<puerto>/<base> <entorno>`. Una respuesta distinta o EOF cancela el reset.
- No sustituir la guarda por un drop directo.

## Seguridad y reglas para agentes

- Ningún agente puede leer, extraer ni consumir tokens First Run, crear administradores ni restablecer credenciales sin autorización expresa del propietario para esa operación (regla global en [`CLAUDE.md`](../../CLAUDE.md)).
- No obtener tokens desde consola, logs ni BD. No redirigir ni grabar la consola API.
- `E2E:SeedEnabled` (`E2ESeedService`, nunca en Production) es infraestructura de pruebas E2E, no una vía de instalación.

## Requisitos operativos y límites

- PostgreSQL directo o proxy que preserve sesiones; **no** pooling externo por transacción (PgBouncer transaction mode rompe el advisory lock de sesión).
- La detección de caídas de red no es instantánea.
- Si la API cae después de guardar el token y antes del banner, el siguiente arranque propietario lo reemplaza.
- El token es un secreto bearer visible en la consola local: el acceso a esa consola y al host es responsabilidad operativa del propietario.

## Pruebas que garantizan la implementación

| Ámbito | Tests |
|--------|-------|
| Emisión del token | `ERP.Infrastructure.Tests/Setup/FirstRunSetupServiceTests` |
| Propiedad exclusiva y concurrencia (PostgreSQL Testcontainers) | `ERP.Infrastructure.Tests/Setup/FirstRunSetupConcurrencyTests` |
| Instalación completa atómica (baseline + bootstrap reales, Testcontainers) | `ERP.Infrastructure.Tests/Setup/FirstRunFullInstallationTests` |
| Handler | `ERP.Application.Tests/Setup/CreateInitialAdminHandlerTests` |
| HTTP real (Kestrel + Testcontainers) | `ERP.API.Tests/Integration/FirstRunHttpTests` (+ `Support/FirstRunHttpProcess`) |
| Contrato del estado del token | `ERP.API.Tests/Hosting/FirstRunTokenContractTests` |
| Guard del reset | `ERP.API.Tests/Hosting/LauncherResetGuardTests` |
| Cliente frontend | `frontend/src/modules/auth/api/authPublicFlows.test.ts` |
| Launcher (funciones extraídas por AST, sin servicios reales) | `scripts/tests/first-run-security.tests.ps1`, `launcher-startup.tests.ps1`, `frontend-process-ownership.tests.ps1`, `frontend-readiness.tests.ps1` |

Gate focal desde la raíz (Docker accesible para Testcontainers):

```powershell
dotnet test backend/src/ERP.Infrastructure.Tests/ERP.Infrastructure.Tests.csproj --filter "FullyQualifiedName~ERP.Infrastructure.Tests.Setup"
dotnet test backend/src/ERP.Application.Tests/ERP.Application.Tests.csproj --filter FullyQualifiedName~CreateInitialAdminHandlerTests
dotnet test backend/src/ERP.API.Tests/ERP.API.Tests.csproj --filter "FullyQualifiedName~FirstRunHttpTests|FullyQualifiedName~FirstRunTokenContractTests|FullyQualifiedName~LauncherResetGuardTests"
foreach ($t in 'first-run-security','launcher-startup','frontend-process-ownership','frontend-readiness') {
    pwsh -NoProfile -ExecutionPolicy Bypass -File ".\scripts\tests\$t.tests.ps1"
    if ($LASTEXITCODE -ne 0) { throw "FAIL $t" }
}
```

Los scripts del launcher son scripts autónomos de assertions (no Pester): el resultado es su exit code, no un resumen de `Invoke-Pester`.

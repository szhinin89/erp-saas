# ZH Print Agent

Agente local independiente para imprimir tirillas POS de ZH Technologies en cada caja. No referencia `backend/` ni `frontend/`; solo expone una API local, guarda una cola persistente y procesa trabajos en background.

## Estructura

```text
print-agent/
├── ZH.PrintAgent.sln
├── src/
│   ├── ZH.PrintAgent.App/
│   ├── ZH.PrintAgent.Contracts/
│   ├── ZH.PrintAgent.Core/
│   └── ZH.PrintAgent.Infrastructure/
├── tests/
│   ├── ZH.PrintAgent.App.Tests/
│   ├── ZH.PrintAgent.Core.Tests/
│   └── ZH.PrintAgent.Infrastructure.Tests/
├── scripts/
└── installers/windows/
```

## Seguridad local

- Escucha en `127.0.0.1:9817` por defecto.
- Requiere `X-ZH-PrintAgent-Key` en todos los endpoints reales (`/health`, `/print-jobs`, `/printers`), sin excepción.
- `/admin` (panel web) y `/api/admin/*` solo quedan exentos de la API key durante el arranque en frío: mientras `SetupCompleted=false` **y** el agente sigue en `127.0.0.1` (`AllowLan=false`). En cuanto se completa el asistente, o si `AllowLan=true`, `/admin` exige la key igual que el resto. Ver [Panel de administración](#panel-de-administración-y-asistente-de-primer-arranque).
- En `Production` falla al iniciar si `ApiKey` sigue siendo `local-dev-key-change-me` o el valor de ejemplo del instalador **y** ya se completó el setup (`SetupCompleted=true`), o si el binding no es loopback (`AllowLan=true`/`BindHost` no local) incluso durante el arranque en frío.
- CORS queda restringido a orígenes locales configurados.
- `AllowLan=false` bloquea cualquier bind no-loopback. Para LAN se debe cambiar explícitamente `PrintAgent:AllowLan=true` y `PrintAgent:BindHost`.
- No guarda credenciales del ERP ni ejecuta reglas de negocio.
- Rechaza payloads mayores a `PrintAgent:MaxPayloadBytes`.

## Endpoints

Header requerido:

```http
X-ZH-PrintAgent-Key: local-dev-key-change-me
```

- `GET /health`
- `GET /health/ready`
- `POST /print-jobs`
- `GET /print-jobs`
- `GET /print-jobs/{jobId}`
- `POST /print-jobs/{jobId}/cancel`
- `POST /print-jobs/{jobId}/retry`
- `GET /printers`
- `GET /printers/config`

Panel de administración (ver detalle abajo; `/api/admin/*` exento de key solo durante el arranque en frío):

- `GET /api/admin/status`
- `GET /api/admin/printers/windows`
- `GET /api/admin/printers`
- `PUT /api/admin/printers`
- `POST /api/admin/printers/{name}/test-print`
- `POST /api/admin/apikey/regenerate`
- `POST /api/admin/setup/complete`
- `GET /api/admin/queue`
- `POST /api/admin/queue/{jobId}/retry`
- `POST /api/admin/queue/{jobId}/cancel`
- `POST /api/admin/queue/{jobId}/mark-reviewed`

Ejemplo de trabajo:

```json
{
  "jobId": "pos-001",
  "printerName": "POS-80",
  "copies": 1,
  "receipt": {
    "merchantName": "ZH Technologies",
    "headerLines": ["Caja 1", "Factura FAKE-001"],
    "items": [
      { "name": "Producto demo", "quantity": 1, "unitPrice": 12.5, "total": 12.5 }
    ],
    "totals": [
      { "label": "TOTAL", "amount": 12.5 }
    ],
    "footerLines": ["Gracias por su compra"]
  }
}
```

## Drivers de impresión

Cada impresora se configura con un `Driver`:

- `simulated`: solo para `Development`, `Test` o `Testing`; escribe archivos `.txt` para pruebas.
- `windows-raw`: usa WinSpool con datos `RAW` y comandos ESC/POS básicos para impresoras térmicas Windows.

Ejemplo físico:

```json
{
  "Name": "POS-80",
  "Driver": "windows-raw",
  "Enabled": true,
  "IsDefault": true
}
```

`printerName` debe existir como impresora habilitada en la configuración. Para `windows-raw`, el agente también intenta abrir la cola local de Windows antes de aceptar el trabajo; si no está disponible, responde 400 y el job no entra a la cola.

`/health/ready` valida carpeta de datos, lectura de cola, driver seleccionado e impresoras habilitadas.

## Panel de administración y asistente de primer arranque

Cada caja se configura desde el navegador local, sin editar JSON a mano:

```text
http://127.0.0.1:9817/admin
```

**Primer arranque (asistente):** mientras el agente no tenga configuración (`SetupCompleted=false`) y esté en `127.0.0.1`, `/admin` es accesible sin API key. El asistente guía estos pasos:

1. Verifica que el servicio esté activo (`/api/admin/status`).
2. Genera la API key del equipo (`POST /api/admin/apikey/regenerate`) — se muestra una sola vez, cópiala antes de continuar.
3. Lista las impresoras Windows detectadas en el equipo.
4. Elige impresora por defecto, driver (`windows-raw` / `simulated`) y ancho de papel (80mm / 58mm).
5. Guarda la configuración y envía una impresión de prueba.
6. Completa el setup (`POST /api/admin/setup/complete`) — a partir de aquí `/admin` también exige la API key.

**Pantallas del panel** (una vez completado el setup):

- **Estado**: servicio activo, puerto, binding, modo localhost/LAN, si hay API key configurada, impresora/driver por defecto, salud (`health`)/disponibilidad (`ready`), carpetas de datos y logs.
- **Impresoras**: impresoras configuradas (habilitar/deshabilitar, impresora por defecto, driver, ancho de papel, imprimir prueba) e impresoras Windows detectadas para agregarlas con un clic.
- **Cola**: contadores por estado (`Pending`, `Processing`, `Printed`, `Failed`, `Cancelled`, `NeedsReview`), listado de trabajos y acciones (`reintentar`, `cancelar`, `marcar revisado`).

**Si se completó el setup nunca / se perdió la key**: entra por consola al equipo, detén el servicio (`stop-windows-service.ps1`), borra o edita manualmente `SetupCompleted: false` en `C:\ProgramData\ZH Technologies\PrintAgent\config\settings.json`, y vuelve a iniciar el servicio — el asistente se activa de nuevo en `127.0.0.1` sin key. Si además se cambió `AllowLan` a `true` antes de completar el setup, el agente rechaza iniciar en `Production` con la key de ejemplo (ver [Seguridad local](#seguridad-local)); vuelve a poner `AllowLan: false` para recuperar el modo de arranque en frío.

### Rotar la API key

Desde el panel (**Impresoras** → regenerar, o directamente `POST /api/admin/apikey/regenerate` con la key vigente una vez completado el setup). La key anterior queda inválida de inmediato; la nueva se devuelve una sola vez en la respuesta — cópiala antes de cerrar la pantalla.

### Diagnóstico

| Síntoma | Causa típica | Dónde verlo |
|---|---|---|
| No abre `/admin` ni `/health` | Servicio no iniciado | `status-windows-service.ps1` |
| `/health` no responde en el puerto esperado | Puerto ocupado por otro proceso | Revisar logs de arranque; cambiar `PrintAgent:Port` |
| `401 Unauthorized` en cualquier endpoint | API key incorrecta o ausente | Header `X-ZH-PrintAgent-Key`; regenerar key desde `/admin` si se perdió |
| Error de CORS en el navegador | Origen no está en `AllowedCorsOrigins` | Agregar el origen a la config y reiniciar |
| `Printer '...' is not configured or is disabled` | Impresora no existe o está deshabilitada en la config | Pantalla **Impresoras** del panel |
| Falla `windows-raw` al imprimir | Cola de Windows no abre la impresora (offline, sin driver, nombre incorrecto) | `/health/ready`, pantalla **Estado** |
| Job queda en `NeedsReview` tras reiniciar | El agente se detuvo mientras imprimía; se recuperó de forma segura sin reintentar solo | Pantalla **Cola**: revisar la tirilla física y usar `reintentar` o `marcar revisado` |
| `print-jobs.json` corrupto recuperado | Corrupción de archivo (corte de energía, disco lleno) | Se conserva copia `*.corrupt-*`; el agente sigue con `.bak` |

## Concurrencia e idempotencia

- `jobId` es idempotente.
- Si el mismo `jobId` vuelve en `Pending`, `Processing` o `Printed`, no se duplica.
- La impresión se serializa por `printerName` mediante semáforos por impresora.
- La cola se guarda en `data/queue/print-jobs.json` por defecto y mantiene `print-jobs.json.bak` antes de cada reemplazo.

## Reintentos y recuperación

Estados soportados: `Pending`, `Processing`, `Printed`, `Failed`, `Cancelled`, `NeedsReview`.

La política definida para reinicio es:

- Al iniciar, trabajos `Processing` más antiguos que `ProcessingStaleAfterSeconds` pasan a `NeedsReview`.
- `NeedsReview` evita reimprimir automáticamente un trabajo que pudo haber llegado físicamente a la impresora antes de una caída.
- El operador debe revisar la tirilla física y ejecutar `POST /print-jobs/{jobId}/retry` solo si confirma que debe reintentarse.
- Fallos de impresión reintentan con backoff exponencial hasta `MaxAttempts`.
- Cuando se agotan intentos, el estado queda en `Failed`.
- `POST /print-jobs/{jobId}/retry` reinicia manualmente un `Failed`, `Cancelled` o `NeedsReview` a `Pending`.

El agente ofrece semántica at-least-once para impresión física. No promete exactly-once: si el proceso cae después de enviar bytes a la impresora y antes de guardar `Printed`, puede existir una tirilla ya impresa con job en `NeedsReview`.

Si `print-jobs.json` queda corrupto, el agente conserva una copia `*.corrupt-*` e intenta recuperar desde `print-jobs.json.bak`.

## Ejecución local

```powershell
dotnet restore .\print-agent\ZH.PrintAgent.sln --configfile .\print-agent\NuGet.Config
dotnet run --project .\print-agent\src\ZH.PrintAgent.App\ZH.PrintAgent.App.csproj --no-restore
```

Prueba manual:

```powershell
.\print-agent\scripts\manual-smoke-test.ps1
.\print-agent\scripts\admin-smoke-test.ps1
```

La impresora simulada escribe archivos `.txt` en `src/ZH.PrintAgent.App/data/printed` cuando se ejecuta desde el proyecto. La impresora `FAIL-POS-80` simula fallo para validar `Failed` y retry. `admin-smoke-test.ps1` recorre el asistente completo (`/api/admin/*`) contra una instancia recién levantada y sin `SetupCompleted`.

## Validaciones

```powershell
dotnet build .\print-agent\ZH.PrintAgent.sln --no-restore
dotnet test .\print-agent\ZH.PrintAgent.sln --no-build
git diff --check
```

## Windows Service

Publicar:

```powershell
dotnet publish .\print-agent\src\ZH.PrintAgent.App\ZH.PrintAgent.App.csproj -c Release -o .\print-agent\publish
Copy-Item .\print-agent\installers\windows\appsettings.Production.sample.json .\print-agent\publish\appsettings.Production.json
```

`appsettings.Production.json` trae `SetupCompleted: false` y un `ApiKey` de ejemplo — **no es necesario editarlos a mano**: el asistente en `/admin` genera la API key y configura la impresora en el primer arranque (ver [Panel de administración](#panel-de-administración-y-asistente-de-primer-arranque)). Si se prefiere preconfigurar todo antes de instalar (por ejemplo, para clonar la config a varias cajas), sí se puede editar el archivo:

- Cambiar `ApiKey` y poner `SetupCompleted: true`.
- Definir `DataDirectory`, por ejemplo `C:\ProgramData\ZH Technologies\PrintAgent`.
- Configurar impresoras físicas con `Driver: "windows-raw"`.
- Verificar que el nombre configurado coincida con la cola de impresora instalada en Windows.

### Configuración y datos persistentes

El agente guarda todo bajo `DataDirectory` (por defecto `C:\ProgramData\ZH Technologies\PrintAgent` en producción), nunca en `bin`/`obj`, para sobrevivir actualizaciones del binario publicado:

```text
C:\ProgramData\ZH Technologies\PrintAgent\
├── config\settings.json   # API key, impresoras, SetupCompleted — editado por el panel /admin
├── queue\print-jobs.json  # cola persistente (+ .bak y *.corrupt-* si hubo corrupción)
├── printed\               # salida del driver "simulated"
└── logs\                  # printagent-YYYY-MM-DD.log, purgados tras LogRetentionDays (30 por defecto)
```

`config\settings.json` se crea automáticamente en el primer arranque (a partir de `appsettings.Production.json`) y desde ahí lo administra el panel `/admin` — no se edita a mano salvo para recuperación (ver [diagnóstico](#diagnóstico)).

Instalar:

```powershell
.\print-agent\scripts\install-windows-service.ps1 -PublishDirectory .\print-agent\publish
```

El script crea `config\`, `data\`, `logs\`, `queue\` y `printed\` bajo `DataDirectory`, configura inicio automático y reinicio ante fallo con `sc.exe failure`, e imprime la URL de `/admin` al terminar. Debe ejecutarse como administrador; falla si no existe `appsettings.Production.json`, o si `AllowLan:true` se combina con una `ApiKey` de ejemplo. Si la `ApiKey` sigue siendo la de ejemplo pero el binding es loopback, el servicio arranca igual en modo de configuración local (arranque en frío).

**Pendiente externo antes del piloto**: la prueba con impresora térmica física está pendiente por falta
de hardware disponible — todo lo anterior (cola persistente, reintentos, `Driver: "windows-raw"`,
instalación como servicio, panel de administración) está implementado y cubierto por los tests de
`ZH.PrintAgent.sln`, pero no hay todavía una tirilla real impresa en una impresora física para confirmar
el driver/cola de Windows end-to-end. Usar `manual-smoke-test.ps1`/`admin-smoke-test.ps1` (impresora
simulada) mientras tanto.

Administrar el servicio:

```powershell
.\print-agent\scripts\start-windows-service.ps1
.\print-agent\scripts\stop-windows-service.ps1
.\print-agent\scripts\restart-windows-service.ps1
.\print-agent\scripts\status-windows-service.ps1 -CheckHealth -ApiKey <key>
```

Desinstalar:

```powershell
.\print-agent\scripts\uninstall-windows-service.ps1
```

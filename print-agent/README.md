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
- Requiere `X-ZH-PrintAgent-Key` en todos los endpoints reales, incluido `/health`.
- En `Production` falla al iniciar si `ApiKey` sigue siendo `local-dev-key-change-me` o el valor de ejemplo del instalador.
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
```

La impresora simulada escribe archivos `.txt` en `src/ZH.PrintAgent.App/data/printed` cuando se ejecuta desde el proyecto. La impresora `FAIL-POS-80` simula fallo para validar `Failed` y retry.

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

Editar `appsettings.Production.json` antes de instalar:

- Cambiar `ApiKey`.
- Definir `DataDirectory`, por ejemplo `C:\ProgramData\ZH Technologies\PrintAgent`.
- Configurar impresoras físicas con `Driver: "windows-raw"`.
- Verificar que el nombre configurado coincida con la cola de impresora instalada en Windows.

Instalar:

```powershell
.\print-agent\scripts\install-windows-service.ps1 -PublishDirectory .\print-agent\publish
```

**Pendiente externo antes del piloto**: la prueba con impresora térmica física está pendiente por falta
de hardware disponible — todo lo anterior (cola persistente, reintentos, `Driver: "windows-raw"`,
instalación como servicio) está implementado y cubierto por los 21 tests de `ZH.PrintAgent.sln`, pero
no hay todavía una tirilla real impresa en una impresora física para confirmar el driver/cola de
Windows end-to-end. Usar `manual-smoke-test.ps1` (impresora simulada) mientras tanto.

El script configura inicio automático y reinicio ante fallo con `sc.exe failure`.
El script debe ejecutarse como administrador y falla si no existe `appsettings.Production.json` o si `ApiKey` conserva un valor de ejemplo.

Desinstalar:

```powershell
.\print-agent\scripts\uninstall-windows-service.ps1
```

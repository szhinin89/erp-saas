<#!
  Asistente interactivo: pide parametros y crea el SuperAdmin (POST /api/setup/superadmin).

  Requiere ERP.API en marcha. El token es el de **first-run** (bloque verde en consola al arrancar)
  o el devuelto por POST /api/dev/reset-first-run en Development. Ver docs/SUPERADMIN-Y-FIRST-RUN.md.

  Uso (desde la carpeta erp-saas):
    .\scripts\create-superadmin-interactive.ps1

  Si bloquea la ejecucion de scripts:
    powershell -ExecutionPolicy Bypass -File .\scripts\create-superadmin-interactive.ps1
#>
$ErrorActionPreference = "Stop"

function Read-Prompt {
  param(
    [Parameter(Mandatory)][string] $Message,
    [string] $Default = "",
    [string] $DefaultHint = "",
    [string] $Help = ""
  )
  if ($Help) {
    Write-Host $Help -ForegroundColor DarkGray
  }
  $showHint = $DefaultHint
  if ($showHint -eq "" -and $Default -ne "") {
    if ($Default.Length -gt 36) {
      $showHint = $Default.Substring(0, 30) + "..."
    }
    else {
      $showHint = $Default
    }
  }
  if ($showHint -ne "") {
    $hint = "  [Pulse Enter sin escribir = usar: $showHint]"
  }
  else {
    $hint = "  [Obligatorio: escriba un valor y pulse Enter]"
  }
  $v = Read-Host "$Message`n$hint"
  if ([string]::IsNullOrWhiteSpace($v)) { return $Default }
  return $v.Trim()
}

function Read-PromptSecret {
  param(
    [Parameter(Mandatory)][string] $Message,
    [string] $Help = ""
  )
  if ($Help) {
    Write-Host $Help -ForegroundColor DarkGray
  }
  Write-Host "  [Obligatorio: mínimo 10 caracteres; no se muestra al escribir]" -ForegroundColor DarkGray
  $sec = Read-Host $Message -AsSecureString
  if ($null -eq $sec -or $sec.Length -eq 0) { return "" }
  $ptr = [Runtime.InteropServices.Marshal]::SecureStringToBSTR($sec)
  try {
    return [Runtime.InteropServices.Marshal]::PtrToStringBSTR($ptr)
  }
  finally {
    [Runtime.InteropServices.Marshal]::ZeroFreeBSTR($ptr) | Out-Null
  }
}

$scriptDir = $PSScriptRoot

Clear-Host
Write-Host "=== Crear SuperAdmin (instalacion inicial) ===" -ForegroundColor Cyan
Write-Host @"
Solo puede existir un SuperAdmin por base de datos.
Copie el token del mensaje FIRST-RUN en la consola donde corre ERP.API (caduca en 15 min).
En Development puede obtener uno nuevo con POST /api/dev/reset-first-run.
"@ -ForegroundColor Gray
Write-Host ""

Write-Host "`n[1/7] Direccion del API" -ForegroundColor Cyan
$apiBase = Read-Prompt "URL base (sin barra final)" "http://localhost:5003" -Help @"
  Ejemplo: http://localhost:5003
  Es la raiz donde responde su ERP.API (mismo host y puerto que dotnet run).
"@

$setupFromEnv = $env:ERP_SUPERADMIN_SETUP_TOKEN
if ([string]::IsNullOrWhiteSpace($setupFromEnv)) { $setupFromEnv = $env:Deployment__InitialSuperAdminSetupToken }

Write-Host "`n[2/7] Token first-run (setupToken)" -ForegroundColor Cyan
$tokenHint = ""
if (-not [string]::IsNullOrWhiteSpace($setupFromEnv)) {
  $tokenHint = "(variable de entorno en esta consola)"
}
$setupToken = Read-Prompt "Pegue el token completo" $setupFromEnv -DefaultHint $tokenHint -Help @"
  Debe ser el token mostrado por la API al arrancar (no user-secrets Deployment:InitialSuperAdminSetupToken).
  Opcional: exporte ERP_SUPERADMIN_SETUP_TOKEN antes de ejecutar este script.
"@
if ([string]::IsNullOrWhiteSpace($setupToken)) {
  Write-Host "Token obligatorio." -ForegroundColor Red
  exit 1
}

Write-Host "`n[3/7] Nombre del usuario SuperAdmin" -ForegroundColor Cyan
$firstName = Read-Prompt "Nombre de pila" "Super" -Help "  Nombre que verá el usuario en el sistema."

Write-Host "`n[4/7] Apellido del usuario SuperAdmin" -ForegroundColor Cyan
$lastName = Read-Prompt "Apellido" "Admin" -Help "  Apellido que verá el usuario en el sistema."

Write-Host "`n[5/7] Correo del SuperAdmin" -ForegroundColor Cyan
$email = Read-Prompt "Email (login)" "superadmin@test.local" -Help @"
  Será el correo para iniciar sesión (superadmin-login). Debe ser único en la base de datos.
"@

Write-Host "`n[6/7] Contraseña del SuperAdmin" -ForegroundColor Cyan
$passwordPlain = Read-PromptSecret "Contraseña" -Help @"
  Mínimo 10 caracteres. Es la contraseña de inicio de sesión del SuperAdmin (no la del token de instalación).
"@
if ([string]::IsNullOrWhiteSpace($passwordPlain) -or $passwordPlain.Length -lt 10) {
  Write-Host "Contraseña vacía o menor de 10 caracteres." -ForegroundColor Red
  exit 1
}

Write-Host "`n[7/7] HTTPS (solo si usa certificado no confiable)" -ForegroundColor Cyan
$skipTls = Read-Prompt "Si el API usa HTTPS con certificado no confiable, escriba s" "n" -Help @"
  Solo marque s si Invoke-RestMethod falla por certificado (poco habitual en http://localhost).
"@ -DefaultHint "n = no (recomendado en local HTTP)"
$skipSwitch = $false
if ($skipTls -match '^[sSyY]') { $skipSwitch = $true }

Write-Host "`n--- Resumen (revise antes de crear) ---" -ForegroundColor Cyan
Write-Host "  API:        $apiBase"
Write-Host "  Email:      $email"
Write-Host "  Nombre:     $firstName $lastName"
Write-Host "  Token:      $($setupToken.Substring(0, [Math]::Min(6, $setupToken.Length)))..."
Write-Host ""
$go = Read-Prompt "Crear SuperAdmin ahora (POST /api/setup/superadmin)" "s" -Help @"
  s o Enter = Enviar petición al API.
  n = Cancelar sin llamar al API.
"@ -DefaultHint "s = sí, crear ahora"
if ($go -notmatch '^[sSyY]') {
  Write-Host "Cancelado." -ForegroundColor Yellow
  exit 0
}

$child = Join-Path $scriptDir "create-superadmin.ps1"
if (-not (Test-Path -LiteralPath $child)) {
  Write-Host "No se encontro: $child" -ForegroundColor Red
  exit 1
}

$params = @{
  ApiBase      = $apiBase
  SetupToken   = $setupToken
  FirstName    = $firstName
  LastName     = $lastName
  Email        = $email
  Password     = $passwordPlain
}
if ($skipSwitch) { $params.SkipTlsCheck = $true }

try {
  & $child @params
  exit $LASTEXITCODE
}
finally {
  $passwordPlain = $null
  $setupToken = $null
}

<#!
  Asistente interactivo: pide parametros y crea el SuperAdmin (POST /api/setup/superadmin).
  Requiere que la API este en marcha y que el token coincida con user-secrets o variable de entorno en el servidor.

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
$repoRoot = Split-Path $scriptDir -Parent
$apiCsproj = Join-Path $repoRoot "backend\src\ERP.API\ERP.API.csproj"

Clear-Host
Write-Host "=== Crear SuperAdmin (instalacion inicial) ===" -ForegroundColor Cyan
Write-Host @"
Solo puede existir un SuperAdmin por base de datos.
El token debe coincidir con lo que lee la API (clave Deployment:InitialSuperAdminSetupToken).
"@ -ForegroundColor Gray
Write-Host ""

$setupToken = ""
Write-Host "[1/8] Token en user-secrets (opcional)" -ForegroundColor Cyan
Write-Host @"
  s = Guarda el token en dotnet user-secrets del proyecto ERP.API (recomendado la primera vez).
  n = No guardar aqui; luego deberá pegar el mismo token que ya configuró en la API.
"@ -ForegroundColor DarkGray
$saveSecret = Read-Prompt "Escriba s o n y pulse Enter" "n" -DefaultHint "n = no (solo pegara el token en el siguiente paso)"
if ($saveSecret -match '^[sSyY]') {
  if (-not (Test-Path -LiteralPath $apiCsproj)) {
    Write-Host "No se encontro: $apiCsproj" -ForegroundColor Red
    exit 1
  }
  $secretToStore = Read-PromptSecret "Token secreto de instalacion" -Help @"
  Mismo texto que usara la API en user-secrets (Deployment:InitialSuperAdminSetupToken).
  Si acaba de inventarlo, guardelo en un sitio seguro; lo necesitara en el siguiente paso si no guarda aqui.
"@
  if ([string]::IsNullOrWhiteSpace($secretToStore)) {
    Write-Host "Token vacio; cancelado." -ForegroundColor Red
    exit 1
  }
  & dotnet user-secrets set "Deployment:InitialSuperAdminSetupToken" $secretToStore --project $apiCsproj
  if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
  $setupToken = $secretToStore
  Write-Host "User-secrets actualizado. Reinicie la API (dotnet run) y pulse Enter aqui cuando este escuchando..." -ForegroundColor Yellow
  [void](Read-Host)
}

Write-Host "`n[2/8] Direccion del API" -ForegroundColor Cyan
$apiBase = Read-Prompt "URL base (sin barra final)" "http://localhost:5003" -Help @"
  Ejemplo: http://localhost:5003
  Es la raiz donde responde su ERP.API (mismo host y puerto que dotnet run).
"@

if ([string]::IsNullOrWhiteSpace($setupToken)) {
  $setupFromEnv = $env:Deployment__InitialSuperAdminSetupToken
  Write-Host "`n[3/8] Token de instalacion" -ForegroundColor Cyan
  $tokenHint = ""
  if (-not [string]::IsNullOrWhiteSpace($setupFromEnv)) {
    $tokenHint = "(variable `$env:Deployment__InitialSuperAdminSetupToken en ESTA consola)"
  }
  $setupToken = Read-Prompt "Pegue el token o pulse Enter" $setupFromEnv -DefaultHint $tokenHint -Help @"
  Debe ser exactamente el valor de Deployment:InitialSuperAdminSetupToken en el servidor (user-secrets o variable de entorno del proceso de la API).
"@
}
else {
  Write-Host "`n[3/8] Token de instalacion" -ForegroundColor Cyan
  Write-Host "  Se usara el token que acaba de guardar en user-secrets." -ForegroundColor DarkGray
}
if ([string]::IsNullOrWhiteSpace($setupToken)) {
  Write-Host "Token obligatorio." -ForegroundColor Red
  exit 1
}

Write-Host "`n[4/8] Nombre del usuario SuperAdmin" -ForegroundColor Cyan
$firstName = Read-Prompt "Nombre de pila" "Super" -Help "  Nombre que verá el usuario en el sistema."

Write-Host "`n[5/8] Apellido del usuario SuperAdmin" -ForegroundColor Cyan
$lastName = Read-Prompt "Apellido" "Admin" -Help "  Apellido que verá el usuario en el sistema."

Write-Host "`n[6/8] Correo del SuperAdmin" -ForegroundColor Cyan
$email = Read-Prompt "Email (login)" "superadmin@test.local" -Help @"
  Será el correo para iniciar sesión (superadmin-login). Debe ser único en la base de datos.
"@

Write-Host "`n[7/8] Contraseña del SuperAdmin" -ForegroundColor Cyan
$passwordPlain = Read-PromptSecret "Contraseña" -Help @"
  Mínimo 10 caracteres. Es la contraseña de inicio de sesión del SuperAdmin (no la del token de instalación).
"@
if ([string]::IsNullOrWhiteSpace($passwordPlain) -or $passwordPlain.Length -lt 10) {
  Write-Host "Contraseña vacía o menor de 10 caracteres." -ForegroundColor Red
  exit 1
}

Write-Host "`n[8/8] HTTPS (solo si usa certificado no confiable)" -ForegroundColor Cyan
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

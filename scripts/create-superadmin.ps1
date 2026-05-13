<#!
  Crea el único SuperAdmin de la instancia vía POST /api/setup/superadmin.

  El -SetupToken debe ser el token efímero de **first-run** que imprime la consola
  del proceso ERP.API al arrancar (o el devuelto por POST /api/dev/reset-first-run en Development).
  No coincide con Deployment:InitialSuperAdminSetupToken (esa clave no valida el claim en el código actual).

  Desde la carpeta erp-saas (Windows PowerShell 5.1 — sin pwsh):
    .\scripts\create-superadmin.ps1 -SetupToken "<pegar-desde-consola-API>"

  Si PowerShell bloquea scripts:
    powershell -ExecutionPolicy Bypass -File .\scripts\create-superadmin.ps1 -SetupToken "..."

  Con PowerShell 7+ instalado (pwsh en PATH):
    pwsh .\scripts\create-superadmin.ps1 -SetupToken "..."

  Opcional: puede pasar el mismo token por variable de entorno (solo comodidad local):
    $env:ERP_SUPERADMIN_SETUP_TOKEN = "..."; .\scripts\create-superadmin.ps1

  Asistente interactivo (solo preguntas en consola):
    .\scripts\create-superadmin-interactive.ps1
#>
[CmdletBinding()]
param(
  [string] $ApiBase = "http://localhost:5003",
  [string] $SetupToken = "",
  [string] $FirstName = "Super",
  [string] $LastName = "Admin",
  [string] $Email = "superadmin@test.local",
  [string] $Password = "ChangeMe12345",
  [switch] $SkipTlsCheck
)

$ErrorActionPreference = "Stop"

if ([string]::IsNullOrWhiteSpace($SetupToken)) {
  $SetupToken = $env:ERP_SUPERADMIN_SETUP_TOKEN
}
if ([string]::IsNullOrWhiteSpace($SetupToken)) {
  $SetupToken = $env:Deployment__InitialSuperAdminSetupToken
}

function Read-HttpErrorBody {
  param([System.Management.Automation.ErrorRecord] $Err)
  if ($Err.ErrorDetails -and $Err.ErrorDetails.Message) {
    return [string]$Err.ErrorDetails.Message
  }
  $resp = $Err.Exception.Response
  if ($null -eq $resp) { return $null }
  try {
    $stream = $resp.GetResponseStream()
    if ($null -eq $stream) { return $null }
    $sr = New-Object System.IO.StreamReader($stream)
    $text = $sr.ReadToEnd()
    $sr.Dispose()
    return $text
  }
  catch {
    return $null
  }
}

if ([string]::IsNullOrWhiteSpace($SetupToken)) {
  Write-Error @"
No hay token de first-run.

  1) Arranque ERP.API y copie el token del bloque verde FIRST-RUN en la consola del servidor.
  2) O en Development: POST /api/dev/reset-first-run y copie setupToken del JSON.

  Luego:
    pwsh ./scripts/create-superadmin.ps1 -SetupToken 'PEGUE-AQUI'

  Opcional (sesión actual):
    `$env:ERP_SUPERADMIN_SETUP_TOKEN = '...'; pwsh ./scripts/create-superadmin.ps1

  Documentación: docs/SUPERADMIN-Y-FIRST-RUN.md
"@
}

$uri = "$($ApiBase.TrimEnd('/'))/api/setup/superadmin"
$bodyObj = [ordered]@{
  setupToken = $SetupToken.Trim()
  firstName  = $FirstName.Trim()
  lastName   = $LastName.Trim()
  email      = $Email.Trim()
  password   = $Password
}
$json = ($bodyObj | ConvertTo-Json -Compress)

if ($SkipTlsCheck) {
  if (-not ([System.Management.Automation.PSTypeName]'TrustAllCertsPolicy').Type) {
    Add-Type @"
using System.Net;
using System.Security.Cryptography.X509Certificates;
public class TrustAllCertsPolicy : ICertificatePolicy {
  public bool CheckValidationResult(ServicePoint srvPoint, X509Certificate certificate, WebRequest request, int certificateProblem) { return true; }
}
"@
  }
  [System.Net.ServicePointManager]::CertificatePolicy = New-Object TrustAllCertsPolicy
  [System.Net.ServicePointManager]::SecurityProtocol = [System.Net.SecurityProtocolType]::Tls12 -bor [System.Net.SecurityProtocolType]::Tls11 -bor [System.Net.SecurityProtocolType]::Tls
}

Write-Host "POST $uri" -ForegroundColor Cyan
Write-Host "Email: $($bodyObj.email)" -ForegroundColor Gray

try {
  $response = Invoke-RestMethod -Uri $uri -Method Post -ContentType "application/json; charset=utf-8" -Body $json
}
catch {
  Write-Host "Error: $($_.Exception.Message)" -ForegroundColor Red
  $raw = Read-HttpErrorBody $_
  if ($raw) {
    Write-Host "`nRespuesta del API:" -ForegroundColor Yellow
    Write-Host $raw -ForegroundColor Red
    try {
      $jo = $raw | ConvertFrom-Json
      $m = $jo.message
      if ([string]::IsNullOrEmpty($m)) { $m = $jo.Message }
      if ($m) {
        Write-Host "`n--> $($m.Trim())" -ForegroundColor Yellow
      }
    }
    catch { }
  }
  Write-Host "`nComprueba:" -ForegroundColor Cyan
  Write-Host "  1) La API usa el MISMO token: desde ERP.API ejecuta  dotnet user-secrets list" -ForegroundColor Gray
  Write-Host "     y compara Deployment:InitialSuperAdminSetupToken con -SetupToken." -ForegroundColor Gray
  Write-Host "  2) Si ya creaste SuperAdmin antes: la API responde error (solo uno por BD)." -ForegroundColor Gray
  Write-Host "  3) Contraseña por defecto del script: minimo 10 caracteres (-Password)." -ForegroundColor Gray
  exit 1
}

$ok = $response.success
if ($null -eq $ok) { $ok = $response.Success }
if (-not $ok) {
  Write-Host ($response | ConvertTo-Json -Depth 8) -ForegroundColor Red
  exit 1
}

$msg = $response.message
if ([string]::IsNullOrEmpty($msg)) { $msg = $response.Message }
Write-Host "OK: $msg" -ForegroundColor Green

$ro = $response.responseObject
if ($null -eq $ro) { $ro = $response.ResponseObject }
if ($ro) {
  Write-Host ($ro | ConvertTo-Json -Depth 4 -Compress) -ForegroundColor Gray
  $tok = $ro.token; if ([string]::IsNullOrEmpty($tok)) { $tok = $ro.Token }
  if ($tok) {
    Write-Host "JWT (muestra): $($tok.Substring(0, [Math]::Min(48, $tok.Length)))..." -ForegroundColor DarkGray
  }
}

Write-Host "`nSiguiente: abre el frontend, inicia sesión con ese email/contraseña (superadmin-login) o usa el token en el cliente." -ForegroundColor Green

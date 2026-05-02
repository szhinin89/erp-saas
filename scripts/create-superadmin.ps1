<#!
  Crea el único SuperAdmin de la instancia vía POST /api/setup/superadmin.
  Requiere Deployment:InitialSuperAdminSetupToken configurado (user-secrets o env).

  Desde la carpeta erp-saas (Windows PowerShell 5.1 — sin pwsh):
    .\scripts\create-superadmin.ps1 -SetupToken "mi-token-secreto"

  Si PowerShell bloquea scripts:
    powershell -ExecutionPolicy Bypass -File .\scripts\create-superadmin.ps1 -SetupToken "..."

  Con PowerShell 7+ instalado (pwsh en PATH):
    pwsh .\scripts\create-superadmin.ps1 -SetupToken "..."

  Token por variable de entorno:
    $env:Deployment__InitialSuperAdminSetupToken = "mi-token"; .\scripts\create-superadmin.ps1

  Asistente interactivo (solo preguntas en consola):
    .\scripts\create-superadmin-interactive.ps1
#>
[CmdletBinding()]
param(
  [string] $ApiBase = "http://localhost:5003",
  [string] $SetupToken = $env:Deployment__InitialSuperAdminSetupToken,
  [string] $FirstName = "Super",
  [string] $LastName = "Admin",
  [string] $Email = "superadmin@test.local",
  [string] $Password = "ChangeMe12345",
  [switch] $SkipTlsCheck
)

$ErrorActionPreference = "Stop"

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
No hay token de instalación.

  Opción A — variable de entorno (sesión actual):
    `$env:Deployment__InitialSuperAdminSetupToken = 'tu-token'
    pwsh ./scripts/create-superadmin.ps1

  Opción B — parámetro:
    pwsh ./scripts/create-superadmin.ps1 -SetupToken 'tu-token'

  Opción C — user-secrets (desde ERP.API):
    cd backend/src/ERP.API
    dotnet user-secrets set Deployment:InitialSuperAdminSetupToken "tu-token"

Luego vuelve a ejecutar este script. El mismo valor debe enviarse en -SetupToken (no el hash; el API compara de forma segura).
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

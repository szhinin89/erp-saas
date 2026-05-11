<#!
  Prueba E2E del flujo SuperAdmin -> tenant -> contexto tenant -> volver global.

  Uso rápido (desde la raíz del repo):
    powershell -ExecutionPolicy Bypass -File .\scripts\test-superadmin-flow.ps1

  Requisitos:
  - API corriendo en localhost:5003 (o pasar -ApiBase).
  - Token de setup configurado en user-secrets de ERP.API para crear el primer SuperAdmin.
  - Si el SuperAdmin ya existe, el paso setup se omite automáticamente.
#>
[CmdletBinding()]
param(
    [string] $ApiBase = "http://localhost:5003",
    [string] $SetupToken = "",
    [string] $SuperAdminEmail = "superadmin@erp.com",
    [string] $SuperAdminPassword = "SuperAdmin12345!",
    [switch] $SkipSetup
)

$ErrorActionPreference = "Stop"

function Get-ResponseObject {
    param([object] $Response)
    if ($null -eq $Response) { return $null }
    if ($null -ne $Response.responseObject) { return $Response.responseObject }
    if ($null -ne $Response.ResponseObject) { return $Response.ResponseObject }
    if ($null -ne $Response.data) { return $Response.data }
    if ($null -ne $Response.Data) { return $Response.Data }
    return $Response
}

function Invoke-Api {
    param(
        [Parameter(Mandatory = $true)][string] $Method,
        [Parameter(Mandatory = $true)][string] $Path,
        [hashtable] $Headers,
        [object] $Body
    )

    $uri = "{0}{1}" -f $ApiBase.TrimEnd('/'), $Path
    if ($null -eq $Body) {
        return Invoke-RestMethod -Method $Method -Uri $uri -Headers $Headers
    }

    $json = $Body | ConvertTo-Json -Depth 8
    return Invoke-RestMethod -Method $Method -Uri $uri -Headers $Headers -ContentType "application/json" -Body $json
}

Write-Host ""
Write-Host "==============================================" -ForegroundColor White
Write-Host "  ERP SaaS - Prueba flujo SuperAdmin E2E" -ForegroundColor White
Write-Host "==============================================" -ForegroundColor White
Write-Host ("  API Base: {0}" -f $ApiBase)

# 0) Health check
Write-Host ""
Write-Host "STEP 0/8 Verificar API viva" -ForegroundColor Cyan
try {
    $health = Invoke-Api -Method "Get" -Path "/health/live"
    Write-Host "  OK API viva." -ForegroundColor Green
}
catch {
    Write-Host "  ERROR: API no responde en /health/live." -ForegroundColor Red
    throw
}

# 1) Setup superadmin (idempotente)
if (-not $SkipSetup) {
    Write-Host ""
    Write-Host "STEP 1/8 Setup inicial SuperAdmin (idempotente)" -ForegroundColor Cyan

    if ([string]::IsNullOrWhiteSpace($SetupToken)) {
        try {
            $secretsRaw = & dotnet user-secrets list --project "backend/src/ERP.API/ERP.API.csproj" 2>$null
            $line = $secretsRaw | Where-Object { $_ -match '^Deployment:InitialSuperAdminSetupToken\s*=' } | Select-Object -First 1
            if ($line) {
                $SetupToken = ($line -split '=', 2)[1].Trim()
            }
        }
        catch {
            # Se intenta sin token; si falla setup, continuamos si login funciona.
        }
    }

    if ([string]::IsNullOrWhiteSpace($SetupToken)) {
        Write-Host "  WARN: No hay SetupToken disponible; se omite setup y se intentará login." -ForegroundColor Yellow
    }
    else {
        try {
            $null = Invoke-Api -Method "Post" -Path "/api/setup/superadmin" -Body @{
                setupToken = $SetupToken
                firstName  = "Super"
                lastName   = "Admin"
                email      = $SuperAdminEmail
                password   = $SuperAdminPassword
            }
            Write-Host "  OK SuperAdmin creado por setup." -ForegroundColor Green
        }
        catch {
            Write-Host "  INFO: Setup omitido (ya creado o token no aplicable)." -ForegroundColor Yellow
        }
    }
}

# 2) Login superadmin
Write-Host ""
Write-Host "STEP 2/8 Login SuperAdmin global (sin tenant)" -ForegroundColor Cyan
$superLogin = Invoke-Api -Method "Post" -Path "/api/auth/superadmin-login" -Body @{
    email    = $SuperAdminEmail
    password = $SuperAdminPassword
}
$superObj = Get-ResponseObject $superLogin
$superToken = $superObj.token
if ([string]::IsNullOrWhiteSpace($superToken)) {
    throw "No se recibió token de SuperAdmin."
}
Write-Host ("  OK role={0} tenantId={1}" -f $superObj.role, $superObj.tenantId) -ForegroundColor Green
$superHeaders = @{ Authorization = "Bearer $superToken" }

# 3) Crear tenant + admin local
Write-Host ""
Write-Host "STEP 3/8 Crear empresa con admin local" -ForegroundColor Cyan
$stamp = Get-Date -Format "yyyyMMddHHmmss"
$tenantName = "Empresa QA $stamp"
$tenantSlug = "empresa-$stamp"
$localAdminEmail = "admin.$stamp@erp.com"
$localAdminPassword = "AdminLocal12345!"

$createTenant = Invoke-Api -Method "Post" -Path "/api/access/superadmin/tenants" -Headers $superHeaders -Body @{
    tenantName      = $tenantName
    tenantSlug      = $tenantSlug
    adminFirstName  = "Admin"
    adminLastName   = "Local"
    adminEmail      = $localAdminEmail
    adminPassword   = $localAdminPassword
    passwordResetMode = 1
}
$created = Get-ResponseObject $createTenant
$tenantId = $created.tenantId
if ([string]::IsNullOrWhiteSpace($tenantId)) {
    throw "No se recibió tenantId al crear la empresa."
}
Write-Host ("  OK tenantId={0}, admin={1}" -f $tenantId, $localAdminEmail) -ForegroundColor Green

# 4) Login admin local
Write-Host ""
Write-Host "STEP 4/8 Login del admin local" -ForegroundColor Cyan
$localLogin = Invoke-Api -Method "Post" -Path "/api/auth/login" -Body @{
    email    = $localAdminEmail
    password = $localAdminPassword
}
$localObj = Get-ResponseObject $localLogin
if ([string]::IsNullOrWhiteSpace($localObj.token)) {
    throw "No se recibió token de admin local."
}
Write-Host ("  OK role={0} tenantId={1}" -f $localObj.role, $localObj.tenantId) -ForegroundColor Green

# 5) Listado de empresas desde superadmin
Write-Host ""
Write-Host "STEP 5/8 Listar empresas desde SuperAdmin" -ForegroundColor Cyan
$tenantsResp = Invoke-Api -Method "Get" -Path "/api/access/superadmin/tenants" -Headers $superHeaders
$tenantList = @(Get-ResponseObject $tenantsResp)
$containsCreated = @($tenantList | Where-Object { $_.id -eq $tenantId }).Count -gt 0
if (-not $containsCreated) {
    throw "El tenant recién creado no aparece en el listado de SuperAdmin."
}
Write-Host ("  OK total={0}, incluye nueva={1}" -f $tenantList.Count, $containsCreated) -ForegroundColor Green

# 6) Switch tenant
Write-Host ""
Write-Host "STEP 6/8 Cambiar contexto a empresa" -ForegroundColor Cyan
$switchResp = Invoke-Api -Method "Post" -Path "/api/auth/switch-tenant" -Headers $superHeaders -Body @{
    tenantId = $tenantId
}
$switchObj = Get-ResponseObject $switchResp
$tenantToken = $switchObj.token
if ([string]::IsNullOrWhiteSpace($tenantToken)) {
    throw "No se recibió token al cambiar contexto."
}
Write-Host ("  OK tenantId={0} role={1}" -f $switchObj.tenantId, $switchObj.role) -ForegroundColor Green

# 7) Endpoint protegido con token con contexto de empresa
Write-Host ""
Write-Host "STEP 7/8 Endpoint protegido con token conmutado" -ForegroundColor Cyan
$tenantHeaders = @{ Authorization = "Bearer $tenantToken" }
$protectedResp = Invoke-Api -Method "Get" -Path "/api/access/tenant/memberships?onlyActive=true" -Headers $tenantHeaders
$memberships = @(Get-ResponseObject $protectedResp)
Write-Host ("  OK memberships={0}" -f $memberships.Count) -ForegroundColor Green

# 8) Volver a global
Write-Host ""
Write-Host "STEP 8/8 Volver a contexto global" -ForegroundColor Cyan
$backResp = Invoke-Api -Method "Post" -Path "/api/auth/switch-tenant" -Headers $tenantHeaders -Body @{
    tenantId = "00000000-0000-0000-0000-000000000000"
}
$backObj = Get-ResponseObject $backResp
Write-Host ("  OK tenantId={0} role={1}" -f $backObj.tenantId, $backObj.role) -ForegroundColor Green

Write-Host ""
Write-Host "RESULTADO FINAL: FLUJO SUPERADMIN E2E OK" -ForegroundColor Green
Write-Host ("  SuperAdmin: {0}" -f $SuperAdminEmail)
Write-Host ("  Tenant: {0} ({1})" -f $tenantName, $tenantId)
Write-Host ("  Admin local: {0}" -f $localAdminEmail)

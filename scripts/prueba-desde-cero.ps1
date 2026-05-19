<#
.SYNOPSIS
    Encadena una prueba "desde cero" contra la API en Development (reset first-run → SuperAdmin → plan → empresa).
.DESCRIPTION
    Requiere API en Development escuchando en -ApiBaseUrl (por defecto http://localhost:5003).
    Usa POST /api/dev/reset-first-run para obtener setupToken sin leer consola.
.NOTES
    No borra la base de datos: solo resetea first-run en modo dev (elimina SuperAdmins y vuelve a emitir token).
    Ajusta contraseñas y emails si colisionan con datos existentes.
.EXAMPLE
    pwsh -File scripts/prueba-desde-cero.ps1 -ApiBaseUrl "http://localhost:5003"
#>
[CmdletBinding()]
param(
    [string] $ApiBaseUrl = "http://localhost:5003",
    [string] $SuperAdminEmail = "superadmin@test.local",
    [string] $SuperAdminPassword = "PruebaDesdeCero1!",
    [string] $TenantAdminEmail = "admin@empresa-demo.local",
    [string] $TenantAdminPassword = "PruebaDesdeCero2!"
)

$ErrorActionPreference = "Stop"

function Invoke-ApiJson {
    param(
        [string] $Method,
        [string] $Path,
        [object] $Body = $null,
        [hashtable] $Headers = @{}
    )
    $uri = "$($ApiBaseUrl.TrimEnd('/'))/$($Path.TrimStart('/'))"
    $params = @{
        Uri             = $uri
        Method          = $Method
        Headers         = $Headers
        ContentType     = "application/json; charset=utf-8"
        UseBasicParsing = $true
    }
    if ($null -ne $Body) {
        $params.Body = ($Body | ConvertTo-Json -Depth 12)
    }
    return Invoke-RestMethod @params
}

Write-Host "== 1) Reset first-run (solo Development) ==" -ForegroundColor Cyan
$reset = Invoke-ApiJson -Method Post -Path "/api/dev/reset-first-run"
$setupToken = $reset.responseObject.setupToken
if (-not $setupToken) { $setupToken = $reset.responseObject.SetupToken }
if ([string]::IsNullOrWhiteSpace($setupToken)) {
    throw "No se obtuvo setupToken. ¿API en Development y endpoint /api/dev/reset-first-run disponible?"
}

Write-Host "== 2) Claim SuperAdmin ==" -ForegroundColor Cyan
$claimBody = @{
    setupToken = $setupToken
    firstName  = "Super"
    lastName   = "Admin"
    email      = $SuperAdminEmail
    password   = $SuperAdminPassword
}
$claim = Invoke-ApiJson -Method Post -Path "/api/setup/superadmin" -Body $claimBody
if (-not ($claim.success -or $claim.Success)) {
    throw "Claim SuperAdmin falló: $($claim.message) $($claim.Message)"
}

Write-Host "== 3) Login SuperAdmin ==" -ForegroundColor Cyan
$loginSa = Invoke-ApiJson -Method Post -Path "/api/auth/superadmin-login" -Body @{
    email    = $SuperAdminEmail
    password = $SuperAdminPassword
}
$saToken = $loginSa.responseObject.token
if (-not $saToken) { $saToken = $loginSa.responseObject.Token }
if ([string]::IsNullOrWhiteSpace($saToken)) { throw "No se obtuvo JWT de SuperAdmin." }

$auth = @{ Authorization = "Bearer $saToken" }

Write-Host "== 4) Obtener plan Starter (creado por bootstrap) ==" -ForegroundColor Cyan
$plansRes = Invoke-ApiJson -Method Get -Path "/api/superadmin/saas-plans" -Headers $auth
$starterPlan = $plansRes.responseObject | Where-Object { $_.code -eq 'starter' } | Select-Object -First 1
$planId = $starterPlan.id
if (-not $planId) { $planId = $starterPlan.Id }
Write-Host "Plan Starter id: $planId"

Write-Host "== 5) Crear empresa + admin local (módulos inventario + saas = productos + sucursales) ==" -ForegroundColor Cyan
$tenantBody = @{
    tenantName          = "Empresa Demo"
    tenantSlug          = "empresa-demo"
    adminFirstName      = "Local"
    adminLastName       = "Admin"
    adminEmail          = $TenantAdminEmail
    adminPassword       = $TenantAdminPassword
    passwordResetMode   = 2
    linkExistingAdmin   = $false
    planCode            = "starter"
    enabledModules      = @("inventario", "saas")
}
$tenantRes = Invoke-ApiJson -Method Post -Path "/api/access/superadmin/tenants" -Body $tenantBody -Headers $auth
if (-not ($tenantRes.success -or $tenantRes.Success)) {
    throw "Alta empresa falló: $($tenantRes.message) $($tenantRes.Message)"
}
$tenantId = $tenantRes.responseObject.tenantId
if (-not $tenantId) { $tenantId = $tenantRes.responseObject.TenantId }
Write-Host "Tenant id: $tenantId"

Write-Host "== 6) Listar empresas ==" -ForegroundColor Cyan
$tenants = Invoke-ApiJson -Method Get -Path "/api/superadmin/tenants" -Headers $auth
$arr = $tenants.responseObject.tenants
if (-not $arr) { $arr = $tenants.responseObject.Tenants }
Write-Host "Empresas: $($arr.Count)"

Write-Host "== 7) Login admin local (tenant resuelto por email) ==" -ForegroundColor Cyan
$loginTenant = Invoke-ApiJson -Method Post -Path "/api/auth/login" -Body @{
    email    = $TenantAdminEmail
    password = $TenantAdminPassword
}
$tenantJwt = $loginTenant.responseObject.token
if (-not $tenantJwt) { $tenantJwt = $loginTenant.responseObject.Token }
if ([string]::IsNullOrWhiteSpace($tenantJwt)) { throw "Login admin local falló." }
Write-Host "JWT admin local (primeros 40 chars): $($tenantJwt.Substring(0, [Math]::Min(40, $tenantJwt.Length)))..."

Write-Host ""
Write-Host "OK. Siguiente paso manual: abrir el SPA, iniciar sesión con el admin local y comprobar menú (Inventario + Sucursales)." -ForegroundColor Green

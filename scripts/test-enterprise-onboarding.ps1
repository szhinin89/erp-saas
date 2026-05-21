<#!
  Smoke tests enterprise onboarding (Subscriber → Company → Membership ACID).

  Uso:
    powershell -ExecutionPolicy Bypass -File .\scripts\test-enterprise-onboarding.ps1
#>
[CmdletBinding()]
param(
    [string] $ApiBase = "http://localhost:5003"
)

$ErrorActionPreference = "Stop"

function Get-Ro($r) {
    if ($null -ne $r.responseObject) { return $r.responseObject }
    if ($null -ne $r.ResponseObject) { return $r.ResponseObject }
    return $r
}

Write-Host "== Enterprise onboarding smoke ==" -ForegroundColor Cyan

# Login superadmin (assume exists)
$login = Invoke-RestMethod -Uri "$ApiBase/api/auth/superadmin-login" -Method Post -ContentType "application/json" -Body (@{
    email = "superadmin@erp.com"; password = "SuperAdmin12345!"
} | ConvertTo-Json)
$token = (Get-Ro $login).token
if ([string]::IsNullOrWhiteSpace($token)) { throw "SuperAdmin login failed" }
$headers = @{ Authorization = "Bearer $token" }

$stamp = Get-Date -Format "HHmmss"
$slugA = "onboard-a-$stamp"
$slugB = "onboard-b-$stamp"

function New-Subscriber($slug, $ruc) {
    $body = @{
        subscriberName = "Onboard $slug"
        subscriberSlug = $slug
        adminFirstName = "Admin"
        adminLastName  = "Test"
        adminEmail     = "$slug@test.local"
        adminPassword  = "AdminTest12345!"
        planCode       = "starter"
        countryCode    = "ECU"
        timezone       = "America/Guayaquil"
    }
    if ($ruc) { $body.ruc = $ruc }
    return Invoke-RestMethod -Uri "$ApiBase/api/admin/iam/superadmin/subscribers" -Method Post -Headers $headers -ContentType "application/json" -Body ($body | ConvertTo-Json)
}

Write-Host "1) Crear subscriber sin RUC (provisional)..." -ForegroundColor Yellow
$r1 = New-Subscriber $slugA $null
if (-not $r1.success) { throw "Create A failed: $($r1.message)" }
Write-Host "   OK subscriberId=$((Get-Ro $r1).subscriberId)" -ForegroundColor Green

Write-Host "2) Segundo subscriber sin RUC (provisional distinto)..." -ForegroundColor Yellow
$r2 = New-Subscriber $slugB $null
if (-not $r2.success) { throw "Create B failed: $($r2.message)" }
Write-Host "   OK" -ForegroundColor Green

Write-Host "3) RUC duplicado debe retornar 409..." -ForegroundColor Yellow
try {
    $dupSlug = "onboard-dup-$stamp"
    $body = @{
        subscriberName = "Dup"; subscriberSlug = $dupSlug
        adminFirstName = "A"; adminLastName = "B"
        adminEmail = "$dupSlug@test.local"; adminPassword = "AdminTest12345!"
        planCode = "starter"; ruc = "1791234567001"
    } | ConvertTo-Json
    Invoke-RestMethod -Uri "$ApiBase/api/admin/iam/superadmin/subscribers" -Method Post -Headers $headers -ContentType "application/json" -Body $body
    throw "Expected 409 for duplicate RUC"
}
catch {
    if ($_.Exception.Response.StatusCode.value__ -ne 409) {
        throw "Expected HTTP 409, got: $($_.Exception.Message)"
    }
    Write-Host "   OK 409 Conflict" -ForegroundColor Green
}

Write-Host "Smoke enterprise onboarding OK" -ForegroundColor Green

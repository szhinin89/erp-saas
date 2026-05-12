[CmdletBinding()]
param(
    [string] $ApiBase = "http://localhost:5003",
    [string] $Email = "superadmin@prueba.com",
    [string] $Password = "SuperSegura123!",
    [string] $FirstName = "Super",
    [string] $LastName = "Admin Test"
)

$ErrorActionPreference = "Stop"

function Get-ResponseObject {
    param([object] $Response)
    if ($null -eq $Response) { return $null }
    if ($null -ne $Response.responseObject) { return $Response.responseObject }
    if ($null -ne $Response.ResponseObject) { return $Response.ResponseObject }
    return $Response
}

Write-Host "🧪 Iniciando prueba automática de first-run..." -ForegroundColor Yellow

Write-Host "1) Verificando API en $ApiBase ..." -ForegroundColor Yellow
try {
    $swagger = Invoke-WebRequest -Uri "$ApiBase/swagger/index.html" -Method Get -UseBasicParsing
    if ($swagger.StatusCode -ne 200) { throw "Status $($swagger.StatusCode)" }
    Write-Host "✅ API accesible." -ForegroundColor Green
}
catch {
    Write-Host "❌ API no accesible. Asegúrate de que está corriendo (dotnet run)." -ForegroundColor Red
    exit 1
}

Write-Host "2) Reseteando first-run (solo Development)..." -ForegroundColor Yellow
try {
    $resetResponse = Invoke-RestMethod -Uri "$ApiBase/api/dev/reset-first-run" -Method Post
}
catch {
    Write-Host "❌ Error al resetear first-run: $($_.Exception.Message)" -ForegroundColor Red
    exit 1
}

$resetObj = Get-ResponseObject $resetResponse
$token = $resetObj.SetupToken
if ([string]::IsNullOrWhiteSpace($token)) {
    Write-Host "❌ No se pudo obtener SetupToken desde el endpoint de reset." -ForegroundColor Red
    $resetResponse | ConvertTo-Json -Depth 6
    exit 1
}
Write-Host "✅ First-run reseteado. Token efímero recibido." -ForegroundColor Green

Write-Host "3) Creando primer SuperAdmin..." -ForegroundColor Yellow
$createBody = @{
    setupToken = $token
    email      = $Email
    firstName  = $FirstName
    lastName   = $LastName
    password   = $Password
} | ConvertTo-Json

try {
    $createResponse = Invoke-RestMethod -Uri "$ApiBase/api/setup/claim-initial-superadmin" -Method Post -Body $createBody -ContentType "application/json"
}
catch {
    Write-Host "❌ Error al crear SuperAdmin: $($_.Exception.Message)" -ForegroundColor Red
    exit 1
}

if (-not $createResponse.success) {
    Write-Host "❌ El API respondió fallo en creación de SuperAdmin." -ForegroundColor Red
    $createResponse | ConvertTo-Json -Depth 6
    exit 1
}
Write-Host "✅ SuperAdmin creado exitosamente." -ForegroundColor Green

Write-Host "4) Probando login del SuperAdmin..." -ForegroundColor Yellow
$loginBody = @{
    email    = $Email
    password = $Password
} | ConvertTo-Json

try {
    $loginResponse = Invoke-RestMethod -Uri "$ApiBase/api/auth/superadmin-login" -Method Post -Body $loginBody -ContentType "application/json"
}
catch {
    Write-Host "❌ Error en login: $($_.Exception.Message)" -ForegroundColor Red
    exit 1
}

$loginObj = Get-ResponseObject $loginResponse
if ([string]::IsNullOrWhiteSpace($loginObj.token)) {
    Write-Host "❌ Login fallido: no se recibió token." -ForegroundColor Red
    $loginResponse | ConvertTo-Json -Depth 6
    exit 1
}

Write-Host "✅ Login exitoso. SuperAdmin operativo." -ForegroundColor Green
Write-Host "🎉 Prueba first-run completada con éxito." -ForegroundColor Green

<#
.SYNOPSIS
    Crea el SuperAdmin global en el ERP SaaS, usando el modo first-run.
.DESCRIPTION
    Este script interactivo guía al usuario para:
      - Conectarse a la API.
      - Capturar el token generado por la API.
      - Enviar los datos del SuperAdmin al endpoint de creación.
      - Validar el login (POST /api/auth/superadmin-login; la API envuelve el JWT en responseObject.token).
.EXAMPLE
    .\Crear-SuperAdmin.ps1
#>

# Salida UTF-8 en consola Windows (acentos en mensajes del script)
try {
    if ($PSVersionTable.PSVersion.Major -ge 6) {
        [Console]::OutputEncoding = [System.Text.UTF8Encoding]::new($false)
        $OutputEncoding = [Console]::OutputEncoding
    }
    else {
        chcp 65001 | Out-Null
        [Console]::OutputEncoding = [System.Text.UTF8Encoding]::new($false)
    }
}
catch { }

# Colores para la consola
$Host.UI.RawUI.ForegroundColor = "White"

function Write-Info { Write-Host "[INFO] $($args[0])" -ForegroundColor Cyan }
function Write-Success { Write-Host "[SUCCESS] $($args[0])" -ForegroundColor Green }
function Write-Error { Write-Host "[ERROR] $($args[0])" -ForegroundColor Red }
function Write-Warning { Write-Host "[WARNING] $($args[0])" -ForegroundColor Yellow }

# ------------------------------------------------------------
# 1. Configuración del endpoint de la API
# ------------------------------------------------------------
$defaultUrl = "http://localhost:5003"
Write-Info "Por favor, indica la URL de la API (dejar vacío para usar $defaultUrl):"
$userUrl = Read-Host "URL"
if ([string]::IsNullOrWhiteSpace($userUrl)) { $apiUrl = $defaultUrl } else { $apiUrl = $userUrl }

# Verificar conectividad
Write-Info "Probando conexión a $apiUrl ..."
try {
    $test = Invoke-WebRequest -Uri "$apiUrl/swagger" -Method Get -TimeoutSec 5 -UseBasicParsing
    Write-Success "API accesible."
} catch {
    Write-Error "No se pudo conectar a la API en $apiUrl. ¿Está corriendo? (dotnet run)"
    exit 1
}

# ------------------------------------------------------------
# 2. Datos del SuperAdmin
# ------------------------------------------------------------
Write-Info "Ingresa los datos del SuperAdmin:"
do {
    $email = Read-Host "Email (ej. admin@erp.com)"
    if ($email -notmatch '^[^@]+@[^@]+\.[^@]+$') {
        Write-Error "Formato de email inválido. Intenta de nuevo."
    }
} while ($email -notmatch '^[^@]+@[^@]+\.[^@]+$')

$nombreCompleto = Read-Host "Nombre completo (nombre y apellido, p. ej. Ana Garcia)"
# Dividir en FirstName y LastName (primer palabra como nombre, el resto como apellido)
$nameParts = $nombreCompleto -split ' ', 2
$firstName = $nameParts[0]
$lastName = if ($nameParts.Length -gt 1) { $nameParts[1] } else { "" }
if ([string]::IsNullOrWhiteSpace($firstName) -or [string]::IsNullOrWhiteSpace($lastName)) {
    Write-Error "Se requieren al menos dos palabras (nombre y apellido), como exige la API."
    exit 1
}

do {
    $password = Read-Host "Contraseña (mínimo 10 caracteres, igual que la API)" -AsSecureString
    $BSTR = [System.Runtime.InteropServices.Marshal]::SecureStringToBSTR($password)
    $plainPassword = [System.Runtime.InteropServices.Marshal]::PtrToStringAuto($BSTR)
    if ($plainPassword.Length -lt 10) {
        Write-Error "La contraseña debe tener al menos 10 caracteres."
        $valid = $false
    } else {
        $valid = $true
    }
} while (-not $valid)

Write-Info "Confirmar contraseña:"
$confirm = Read-Host "Repite la contraseña" -AsSecureString
$BSTR2 = [System.Runtime.InteropServices.Marshal]::SecureStringToBSTR($confirm)
$plainConfirm = [System.Runtime.InteropServices.Marshal]::PtrToStringAuto($BSTR2)
if ($plainPassword -ne $plainConfirm) {
    Write-Error "Las contraseñas no coinciden."
    exit 1
}

# ------------------------------------------------------------
# 3. Obtener el token first-run (desde la consola del servidor)
# ------------------------------------------------------------
Write-Warning "Asegúrate de que la API esté ejecutándose y que no exista un SuperAdmin previamente."
Write-Info "Si ya existe un SuperAdmin, el modo first-run no estará activo y este script fallará."
Write-Info "La API imprime el token en el bloque FIRST-RUN (consola del servidor)."
Read-Host "Presiona ENTER cuando tengas ese token copiado"
$setupToken = Read-Host "Pega aquí el token (ej. base64 ...==)"

# ------------------------------------------------------------
# 4. Construir el payload con los nombres correctos y llamar al endpoint
# ------------------------------------------------------------
$body = @{
    SetupToken = $setupToken
    Email = $email
    FirstName = $firstName
    LastName = $lastName
    Password = $plainPassword
} | ConvertTo-Json

Write-Info "Enviando solicitud a $apiUrl/api/setup/claim-initial-superadmin ..."

try {
    $response = Invoke-RestMethod -Uri "$apiUrl/api/setup/claim-initial-superadmin" -Method Post -Body $body -ContentType "application/json; charset=utf-8"
    $claimOk = $response.success
    if ($null -eq $claimOk) { $claimOk = $response.Success }
    if (-not $claimOk) {
        $msg = $response.message
        if ([string]::IsNullOrWhiteSpace($msg)) { $msg = $response.Message }
        Write-Error "El servidor respondió sin éxito: $msg"
        exit 1
    }
    Write-Success "SuperAdmin creado. $(if ($response.message) { $response.message } else { $response.Message })"
} catch {
    Write-Error "Falló la creación del SuperAdmin."
    if ($_.Exception.Response) {
        $reader = New-Object System.IO.StreamReader($_.Exception.Response.GetResponseStream())
        $errorBody = $reader.ReadToEnd() | ConvertFrom-Json
        Write-Error "Detalle: $($errorBody.title) - $($errorBody.errors | Out-String)"
    } else {
        Write-Error $_.Exception.Message
    }
    exit 1
}

# ------------------------------------------------------------
# 5. Probar login del SuperAdmin (endpoint dedicado; cuerpo ApiResponse con responseObject.token)
# ------------------------------------------------------------
Write-Info "Probando superadmin-login con las credenciales proporcionadas..."
$loginBody = @{
    email    = $email
    password = $plainPassword
} | ConvertTo-Json

$loginOk = $false
try {
    $loginResponse = Invoke-RestMethod -Uri "$apiUrl/api/auth/superadmin-login" -Method Post -Body $loginBody -ContentType "application/json; charset=utf-8"
    $auth = $loginResponse.responseObject
    if (-not $auth) { $auth = $loginResponse.ResponseObject }
    $tokenValue = $null
    if ($auth) {
        $tokenValue = $auth.token
        if ([string]::IsNullOrWhiteSpace($tokenValue)) { $tokenValue = $auth.Token }
    }
    $ok = $loginResponse.success
    if ($null -eq $ok) { $ok = $loginResponse.Success }
    if ($ok -and $auth -and -not [string]::IsNullOrWhiteSpace($tokenValue)) {
        $loginOk = $true
        Write-Success "Login verificado. SuperAdmin operativo."
        $t = [string]$tokenValue
        Write-Info "JWT (primeros 50 caracteres): $($t.Substring(0, [Math]::Min(50, $t.Length)))..."
    }
    else {
        $msg = $loginResponse.message
        if ([string]::IsNullOrWhiteSpace($msg)) { $msg = $loginResponse.Message }
        Write-Error "Login respondió sin token. success=$ok message=$msg"
    }
}
catch {
    Write-Error "El SuperAdmin se creó, pero falló la verificación de login: $($_.Exception.Message)"
}

if ($loginOk) {
    Write-Success "Proceso completado."
}
else {
    Write-Warning "Proceso terminado con advertencias (revisar login arriba)."
    exit 2
}
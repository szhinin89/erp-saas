<#
.SYNOPSIS
    Crea el operador platform global en el ERP SaaS (modo first-run).
.DESCRIPTION
    Guía interactiva para:
      - Conectar con la API.
      - Usar el token efímero de first-run (tabla first_run_setup_state, hash en BD).
      - Crear el operador en identity_users (user_type=Platform, platform_role=PlatformOperator).
      - Verificar JWT (respuesta del setup o login POST /api/platform/auth/login).

    NOTAS:
      - El token first-run expira en ~15 minutos y solo se muestra en consola del servidor
        (o vía POST /api/dev/reset-first-run en Development).
      - Deployment:InitialPlatformOperatorSetupToken NO valida este flujo; no uses esa clave como sustituto.
      - El login requiere Deployment:PlatformPanelEnabled = true (por defecto suele estarlo).
.EXAMPLE
    .\scripts\setup\Crear-PlatformOperator.ps1
.EXAMPLE
    $env:ERP_PLATFORM_OPERATOR_SETUP_TOKEN = '<token-desde-consola>'; .\scripts\setup\Crear-PlatformOperator.ps1
#>

$ErrorActionPreference = "Stop"

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

function Write-Info { Write-Host "[INFO] $($args[0])" -ForegroundColor Cyan }
function Write-Success { Write-Host "[SUCCESS] $($args[0])" -ForegroundColor Green }
function Write-Err { Write-Host "[ERROR] $($args[0])" -ForegroundColor Red }
function Write-Warn { Write-Host "[WARNING] $($args[0])" -ForegroundColor Yellow }

function Get-ApiResponseObject {
    param([object] $Response)
    if ($null -eq $Response) { return $null }
    if ($null -ne $Response.responseObject) { return $Response.responseObject }
    if ($null -ne $Response.ResponseObject) { return $Response.ResponseObject }
    return $Response
}

function Get-ApiSuccess {
    param([object] $Response)
    if ($null -eq $Response) { return $false }
    $ok = $Response.success
    if ($null -eq $ok) { $ok = $Response.Success }
    return [bool]$ok
}

function Get-ApiMessage {
    param([object] $Response)
    if ($null -eq $Response) { return $null }
    $msg = $Response.message
    if ([string]::IsNullOrWhiteSpace($msg)) { $msg = $Response.Message }
    return $msg
}

function Get-AuthTokenFromResponse {
    param([object] $Response)
    $auth = Get-ApiResponseObject $Response
    if (-not $auth) { return $null }
    $token = $auth.token
    if ([string]::IsNullOrWhiteSpace($token)) { $token = $auth.Token }
    if ([string]::IsNullOrWhiteSpace($token)) { return $null }
    return [string]$token
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
        $reader = New-Object System.IO.StreamReader($stream)
        $text = $reader.ReadToEnd()
        $reader.Dispose()
        return $text
    }
    catch {
        return $null
    }
}

function Read-SecurePlainText {
    param([string] $Prompt)
    $secure = Read-Host $Prompt -AsSecureString
    if ($null -eq $secure -or $secure.Length -eq 0) { return "" }
    $ptr = [Runtime.InteropServices.Marshal]::SecureStringToBSTR($secure)
    try {
        return [Runtime.InteropServices.Marshal]::PtrToStringBSTR($ptr)
    }
    finally {
        [Runtime.InteropServices.Marshal]::ZeroFreeBSTR($ptr) | Out-Null
    }
}

function Test-ApiReachable {
    param([Parameter(Mandatory)][string] $ApiBase)
    $base = $ApiBase.TrimEnd('/')
    $candidates = @(
        "$base/health/live",
        "$base/swagger/index.html"
    )
    foreach ($uri in $candidates) {
        try {
            $r = Invoke-WebRequest -Uri $uri -Method Get -TimeoutSec 8 -UseBasicParsing
            if ($r.StatusCode -eq 200) {
                Write-Success "API accesible ($uri)."
                return $true
            }
        }
        catch { }
    }
    return $false
}

function Invoke-SetupPlatformOperator {
    param(
        [Parameter(Mandatory)][string] $ApiBase,
        [Parameter(Mandatory)][string] $SetupToken,
        [Parameter(Mandatory)][string] $FirstName,
        [Parameter(Mandatory)][string] $LastName,
        [Parameter(Mandatory)][string] $Email,
        [Parameter(Mandatory)][string] $Password
    )

    $uri = "$($ApiBase.TrimEnd('/'))/api/setup/platform-operator"
    $body = @{
        setupToken = $SetupToken.Trim()
        firstName  = $FirstName.Trim()
        lastName   = $LastName.Trim()
        email      = $Email.Trim()
        password   = $Password
    } | ConvertTo-Json -Compress

    Write-Info "Enviando solicitud a $uri ..."
    return Invoke-RestMethod -Uri $uri -Method Post -Body $body -ContentType "application/json; charset=utf-8"
}

function Invoke-PlatformLogin {
    param(
        [Parameter(Mandatory)][string] $ApiBase,
        [Parameter(Mandatory)][string] $Email,
        [Parameter(Mandatory)][string] $Password
    )

    $base = $ApiBase.TrimEnd('/')
    $uri = "$base/api/platform/auth/login"
    $body = @{
        email    = $Email.Trim()
        password = $Password
    } | ConvertTo-Json -Compress

    return Invoke-RestMethod -Uri $uri -Method Post -Body $body -ContentType "application/json; charset=utf-8"
}

function Write-SetupFailureHints {
    Write-Warn "Comprueba:"
    Write-Warn "  1) Token copiado del bloque FIRST-RUN en consola del API (expira ~15 min)."
    Write-Warn "  2) En Development: POST /api/dev/reset-first-run para un token nuevo."
    Write-Warn "  3) Si ya hay operador platform: first-run está cerrado (solo uno por instancia)."
    Write-Warn "  4) Deployment:InitialPlatformOperatorSetupToken NO sirve para este endpoint."
}

# ------------------------------------------------------------
# 1. Configuración del endpoint de la API
# ------------------------------------------------------------
$defaultUrl = "http://localhost:5003"
Write-Info "Indica la URL de la API (vacío = $defaultUrl):"
$userUrl = Read-Host "URL"
if ([string]::IsNullOrWhiteSpace($userUrl)) { $apiUrl = $defaultUrl } else { $apiUrl = $userUrl.TrimEnd('/') }

Write-Info "Probando conexión a $apiUrl ..."
if (-not (Test-ApiReachable -ApiBase $apiUrl)) {
    Write-Err "No se pudo conectar a la API en $apiUrl. ¿Está corriendo? (dotnet run --project backend/src/ERP.API)"
    exit 1
}

# ------------------------------------------------------------
# 2. Datos del operador platform
# ------------------------------------------------------------
Write-Info "Ingresa los datos del operador platform:"
do {
    $email = Read-Host "Email (ej. admin@erp.com)"
    $email = $email.Trim()
    if ($email -notmatch '^[^@]+@[^@]+\.[^@]+$') {
        Write-Err "Formato de email inválido. Intenta de nuevo."
    }
} while ($email -notmatch '^[^@]+@[^@]+\.[^@]+$')

$nombreCompleto = Read-Host "Nombre completo (nombre y apellido, p. ej. Ana Garcia)"
$nameParts = $nombreCompleto.Trim() -split '\s+', 2
$firstName = $nameParts[0]
$lastName = if ($nameParts.Length -gt 1) { $nameParts[1] } else { "" }
if ([string]::IsNullOrWhiteSpace($firstName) -or [string]::IsNullOrWhiteSpace($lastName)) {
    Write-Err "Se requieren al menos dos palabras (nombre y apellido), como exige la API."
    exit 1
}

do {
    $plainPassword = Read-SecurePlainText "Contraseña (mínimo 10 caracteres, igual que la API)"
    if ($plainPassword.Length -lt 10) {
        Write-Err "La contraseña debe tener al menos 10 caracteres."
        $valid = $false
    }
    else {
        $valid = $true
    }
} while (-not $valid)

Write-Info "Confirmar contraseña:"
$plainConfirm = Read-SecurePlainText "Repite la contraseña"
if ($plainPassword -ne $plainConfirm) {
    Write-Err "Las contraseñas no coinciden."
    exit 1
}

# ------------------------------------------------------------
# 3. Token efímero first-run (consola del servidor)
# ------------------------------------------------------------
Write-Warn "Requisitos: API en marcha, sin operador platform previo, token first-run vigente (~15 min)."
Write-Info "Copia el token del bloque FIRST-RUN en la consola del proceso ERP.API."
Write-Info "Development: POST $apiUrl/api/dev/reset-first-run devuelve setupToken en JSON."

$setupTokenDefault = $env:ERP_PLATFORM_OPERATOR_SETUP_TOKEN
if (-not [string]::IsNullOrWhiteSpace($env:Deployment__InitialPlatformOperatorSetupToken)) {
    Write-Warn "Se ignoró Deployment__InitialPlatformOperatorSetupToken (no valida POST /api/setup/platform-operator)."
}

if ([string]::IsNullOrWhiteSpace($setupTokenDefault)) {
    Read-Host "Presiona ENTER cuando tengas el token copiado (usa el token en los próximos ~15 min)"
    $setupToken = Read-Host "Pega aquí el token (ej. base64 ...==)"
}
else {
    Write-Info "ERP_PLATFORM_OPERATOR_SETUP_TOKEN detectado."
    $setupToken = Read-Host "Pega el token o pulsa Enter para usar ERP_PLATFORM_OPERATOR_SETUP_TOKEN"
    if ([string]::IsNullOrWhiteSpace($setupToken)) {
        $setupToken = $setupTokenDefault
    }
}

if ([string]::IsNullOrWhiteSpace($setupToken)) {
    Write-Err "El token de first-run es obligatorio."
    exit 1
}

# ------------------------------------------------------------
# 4. Crear operador platform (identity_users: Platform / PlatformOperator)
# ------------------------------------------------------------
$setupTokenFromResponse = $null
try {
    $response = Invoke-SetupPlatformOperator `
        -ApiBase $apiUrl `
        -SetupToken $setupToken `
        -FirstName $firstName `
        -LastName $lastName `
        -Email $email `
        -Password $plainPassword

    if (-not (Get-ApiSuccess $response)) {
        Write-Err "El servidor respondió sin éxito: $(Get-ApiMessage $response)"
        Write-SetupFailureHints
        exit 1
    }

    $setupTokenFromResponse = Get-AuthTokenFromResponse $response
    Write-Success "Operador platform creado en identity_users. $(Get-ApiMessage $response)"
    if ($setupTokenFromResponse) {
        $t = $setupTokenFromResponse
        Write-Info "JWT del setup (primeros 50 caracteres): $($t.Substring(0, [Math]::Min(50, $t.Length)))..."
    }
}
catch {
    Write-Err "Falló la creación del operador platform."
    $raw = Read-HttpErrorBody $_
    if ($raw) {
        Write-Err "Detalle: $raw"
        try {
            $errorBody = $raw | ConvertFrom-Json
            $detail = Get-ApiMessage $errorBody
            if (-not [string]::IsNullOrWhiteSpace($detail)) {
                Write-Err "--> $detail"
            }
            elseif ($errorBody.title) {
                Write-Err "--> $($errorBody.title)"
            }
        }
        catch { }
    }
    else {
        Write-Err $_.Exception.Message
    }
    Write-SetupFailureHints
    exit 1
}

# ------------------------------------------------------------
# 5. Verificar login platform (opcional si el setup ya devolvió JWT)
# ------------------------------------------------------------
Write-Info "Verificando login platform (POST /api/platform/auth/login)..."
Write-Info "Si falla, comprueba Deployment:PlatformPanelEnabled=true en configuración."

$loginOk = -not [string]::IsNullOrWhiteSpace($setupTokenFromResponse)
if ($loginOk) {
    Write-Success "JWT recibido en la respuesta del setup; alta confirmada."
}

if (-not $loginOk) {
    try {
        Write-Info "Intento login platform..."
        $loginResponse = Invoke-PlatformLogin -ApiBase $apiUrl -Email $email -Password $plainPassword
        $tokenValue = Get-AuthTokenFromResponse $loginResponse

        if ((Get-ApiSuccess $loginResponse) -and -not [string]::IsNullOrWhiteSpace($tokenValue)) {
            $loginOk = $true
            Write-Success "Login verificado (POST /api/platform/auth/login)."
            $t = [string]$tokenValue
            Write-Info "JWT (primeros 50 caracteres): $($t.Substring(0, [Math]::Min(50, $t.Length)))..."
        }
        else {
            Write-Warn "Login platform: sin token. message=$(Get-ApiMessage $loginResponse)"
        }
    }
    catch {
        Write-Warn "Login platform falló: $($_.Exception.Message)"
        $raw = Read-HttpErrorBody $_
        if ($raw) { Write-Warn "Detalle: $raw" }
    }
}

$plainPassword = $null
$plainConfirm = $null
$setupToken = $null

if ($loginOk) {
    Write-Success "Proceso completado. Usuario en identity_users (Platform / PlatformOperator)."
    Write-Info "Frontend: inicia sesión con el mismo email/contraseña (panel platform)."
    exit 0
}

Write-Warn "Operador platform creado, pero no se pudo verificar login."
Write-Warn "Revisa Deployment:PlatformPanelEnabled y credenciales."
exit 2
